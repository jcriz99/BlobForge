using System.Diagnostics;
using System.Numerics;
using BlobForge.World;

namespace BlobForge.Physics;

public sealed class BlobWorld
{
    private const int TopologyBodiesPerStep = 4;
    private const int MaxActiveWounds = 128;
    private const float GranularDt = 1f / 60f;
    private readonly Stopwatch _timer = new();
    private readonly RepresentationScheduler _scheduler = new();
    private readonly BlobParticleSpatialHash _blobHash = new();
    private readonly List<WoundEvent> _woundBuffer = new(32);
    private readonly List<ActiveWound> _activeWounds = new(64);
    private readonly List<DetachedChunkState> _detachedChunks = new(32);
    private readonly List<BloodDripEmission> _surfaceDripBuffer = new(4);
    private readonly List<int> _dispatchedParentBuffer = new(4);
    private readonly HashSet<int> _conveyorCommittedParents = new();
    private float _granularAccumulator = 1f / 120f;

    public BlobWorld(DestructibleGrid grid)
    {
        Grid = grid;
    }

    public List<SoftBody> Bodies { get; } = new();
    public List<ConveyorBelt> Conveyors { get; } = new();
    public GranularMaterialSystem Granular { get; } = new();
    public LightingRig Lighting { get; } = new();
    public HoldingChamber? HoldingChamber { get; set; }
    public ProcessingLine? ProcessingLine { get; set; }
    public OverheadTubeFeed? TubeFeed { get; set; }
    public PhysicalKnife? Knife { get; set; }
    public DestructibleGrid Grid { get; }
    public Vector2 Gravity { get; set; } = new(0f, 980f);
    public bool EnableBlobPersonalities { get; set; }
    public double LastSimulationMs { get; private set; }
    public double LastBodyPhysicsMs { get; private set; }
    public double LastGranularSimulationMs { get; private set; }
    public double LastBlobParticleCollisionMs { get; private set; }
    public double LastHullCollisionMs { get; private set; }
    public int LastConstraintIterations { get; private set; }
    public int ContactsThisStep { get; private set; }
    public int BlobContactsThisStep { get; private set; }
    public int TopologySplitsThisStep { get; private set; }
    public int TotalTopologySplits { get; private set; }
    public int ComponentsCreatedThisStep { get; private set; }
    public int StepsThisFrame { get; set; }
    public int SkippedSteps { get; set; }
    public int SleepingCount => Bodies.Count(b => b.IsSleeping);
    public int DetachedChunkCount => _detachedChunks.Count;
    public int ActiveCrumbleOriginCount => _detachedChunks.Sum(chunk => chunk.ImpactOriginCount);
    public int ActiveWoundCount => _activeWounds.Count;

    public void Step(float dt)
    {
        _timer.Restart();
        Grid.BeginStep(dt);
        Granular.BeginStep();
        _surfaceDripBuffer.Clear();
        Grid.DrainBloodDrops(_surfaceDripBuffer);
        foreach (var drip in _surfaceDripBuffer) Granular.TryEmitBloodDrip(drip, dt);
        foreach (var conveyor in Conveyors)
            if (!conveyor.IsSystemControlled || ProcessingLine?.Powered != false)
                conveyor.Step(dt);
        Lighting.Step(dt);
        HoldingChamber?.Step(dt);
        ProcessingLine?.PreStep(Bodies, Granular.Particles, dt);
        Knife?.Step(dt, Gravity, Conveyors, Bodies, Grid.Columns * Grid.CellSize,
            Grid.Rows * Grid.CellSize, TubeFeed, Grid, Granular);
        // Weapons act after ProcessingLine.PreStep. Observe their damage before topology
        // splitting can convert a one-hit kill into detached pieces and erase the only
        // intact-body sampling opportunity.
        ProcessingLine?.ObserveProcessedDamage(Bodies);
        if (ProcessingLine is not null)
        {
            _dispatchedParentBuffer.Clear();
            ProcessingLine.DrainDispatchedParents(_dispatchedParentBuffer);
            for (var parentIndex = 0; parentIndex < _dispatchedParentBuffer.Count; parentIndex++)
            {
                var parentId = _dispatchedParentBuffer[parentIndex];
                _conveyorCommittedParents.Remove(parentId);
                for (var bodyIndex = Bodies.Count - 1; bodyIndex >= 0; bodyIndex--)
                {
                    var body = Bodies[bodyIndex];
                    if (body.ParentId != parentId) continue;
                    Bodies.RemoveAt(bodyIndex);
                    RemoveDetachedState(body);
                    RemoveWounds(body);
                }
            }
        }
        if (HoldingChamber is { HatchOpen: > 0.02f } chamber)
            foreach (var body in Bodies)
                if (chamber.IsInFeedEnvelope(body)) body.Wake();
        _scheduler.Apply(Bodies);
        foreach (var body in Bodies) body.AdvanceFaceAnimation(dt);
        ContactsThisStep = 0;
        BlobContactsThisStep = 0;
        TopologySplitsThisStep = 0;
        ComponentsCreatedThisStep = 0;
        LastConstraintIterations = 0;

        var maxIterations = 0;
        var grabbedBodyPresent = false;
        long blobParticleCollisionTicks = 0;
        long hullCollisionTicks = 0;
        foreach (var body in Bodies)
        {
            grabbedBodyPresent |= body.IsGrabbed;
            var wasSleeping = body.IsSleeping;
            body.BeginImpactStep();
            if (!wasSleeping) body.PrepareConstraintSolve();
            body.Integrate(dt, Gravity);
            TubeFeed?.ConstrainBody(body, dt);
            // A tube event may wake a body after sleeping integration returned.
            // Reset its XPBD lambdas before it joins this step's solver, while
            // leaving truly dormant bodies completely out of constraint setup.
            if (wasSleeping && !body.IsSleeping) body.PrepareConstraintSolve();
            if (!body.IsSleeping)
                maxIterations = Math.Max(maxIterations, ModeSettings.For(body.Mode).SolverIterations);
        }

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            foreach (var body in Bodies)
            {
                if (body.IsSleeping || iteration >= ModeSettings.For(body.Mode).SolverIterations) continue;
                body.SolveConstraintIteration(dt);
                if (TubeFeed?.ConstrainBody(body, dt) == true) continue;
                ResolveWorld(body, dt);
                ContactsThisStep += TubeFeed?.ResolveExteriorBody(body, dt) ?? 0;
                LastConstraintIterations++;
            }
            foreach (var body in Bodies) body.BeginPressureContactPass();
            var blobParticleStart = Stopwatch.GetTimestamp();
            var blobContacts = _blobHash.BuildAndResolve(Bodies, dt, TubeFeed);
            blobParticleCollisionTicks += Stopwatch.GetTimestamp() - blobParticleStart;
            BlobContactsThisStep += blobContacts;
            ContactsThisStep += blobContacts;
            // Local surface particles are the actual blob contact solver and run on
            // every XPBD pass. The convex hull is the deep-overlap safety guard; an
            // alternating cadence keeps pressure/backstop behavior continuous without
            // rebuilding every damaged contour on consecutive sub-iterations.
            if (grabbedBodyPresent || (iteration & 1) == 0)
            {
                var hullStart = Stopwatch.GetTimestamp();
                var hullContacts = BlobHullCollision.ResolveAll(Bodies, dt, 1f, TubeFeed);
                hullCollisionTicks += Stopwatch.GetTimestamp() - hullStart;
                BlobContactsThisStep += hullContacts;
                ContactsThisStep += hullContacts;
            }
        }

        var arenaLeft = Grid.CellSize;
        var arenaRight = (Grid.Columns - 1) * Grid.CellSize;
        var arenaBottom = Grid.Rows * Grid.CellSize + 96f;
        foreach (var body in Bodies)
        {
            if (TubeFeed?.ConstrainBody(body, dt) == true) continue;
            if (!body.IsSleeping) ResolveWorld(body, dt);
            ContactsThisStep += TubeFeed?.ResolveExteriorBody(body, dt) ?? 0;
            if (body.IsSleeping) continue;
            if (ProcessingLine?.IsInTransit(body) != true &&
                ProcessingLine?.IsContinuousPortalTransit(body) != true)
                body.EnforceArenaBounds(arenaLeft, arenaRight, 0f, arenaBottom, dt);
        }
        foreach (var body in Bodies) body.BeginPressureContactPass();
        var finalHullStart = Stopwatch.GetTimestamp();
        var finalHullContacts = BlobHullCollision.ResolveAll(Bodies, dt, tubeFeed: TubeFeed);
        hullCollisionTicks += Stopwatch.GetTimestamp() - finalHullStart;
        BlobContactsThisStep += finalHullContacts;
        ContactsThisStep += finalHullContacts;
        foreach (var body in Bodies)
        {
            if (TubeFeed?.ConstrainBody(body, dt) == true) continue;
            if (!body.IsSleeping) ResolveWorld(body, dt);
            ContactsThisStep += TubeFeed?.ResolveExteriorBody(body, dt) ?? 0;
            if (body.IsSleeping) continue;
            if (ProcessingLine?.IsInTransit(body) != true &&
                ProcessingLine?.IsContinuousPortalTransit(body) != true)
                body.EnforceArenaBounds(arenaLeft, arenaRight, 0f, arenaBottom, dt);
        }

        foreach (var body in Bodies) body.ApplyResidualPressureDamping(dt);
        foreach (var body in Bodies) body.ApplyRestingViscosity(dt);
        foreach (var body in Bodies) body.ApplyDamagedShapeRecovery(dt);
        if (EnableBlobPersonalities) foreach (var body in Bodies)
        {
            if (TubeFeed?.Contains(body) == true) continue;
            var personalityAllowed =
                ProcessingLine?.Powered != false &&
                ProcessingLine?.IsLocked(body) != true &&
                ProcessingLine?.HasEnteredBayOne(body) != true &&
                ProcessingLine?.IsContinuousPortalTransit(body) != true &&
                (HoldingChamber is null || !HoldingChamber.IsInFeedEnvelope(body));
            body.TryApplyPersonalityHop(dt, inTube: false, allowed: personalityAllowed);
        }
        foreach (var body in Bodies) body.UpdateSleep(dt);
        ProcessTopology(TopologyBodiesPerStep, dt);
        RegisterPendingWounds(dt);
        UpdateDetachedChunks(dt);
        UpdateWounds(dt);
        _granularAccumulator += dt;
        var granularRan = _granularAccumulator >= GranularDt;
        var granularElapsed = 0d;
        if (granularRan)
        {
            var granularDt = MathF.Min(_granularAccumulator, 1f / 30f);
            _granularAccumulator = 0f;
            var granularStart = Stopwatch.GetTimestamp();
            Granular.Step(granularDt, Gravity, Grid, Bodies, Conveyors, HoldingChamber, ProcessingLine, Knife);
            granularElapsed = Stopwatch.GetElapsedTime(granularStart).TotalMilliseconds;
            LastGranularSimulationMs = granularElapsed;
        }
        _timer.Stop();
        LastSimulationMs = _timer.Elapsed.TotalMilliseconds;
        LastBodyPhysicsMs = Math.Max(0d, LastSimulationMs - granularElapsed);
        LastBlobParticleCollisionMs = blobParticleCollisionTicks * 1000d / Stopwatch.Frequency;
        LastHullCollisionMs = hullCollisionTicks * 1000d / Stopwatch.Frequency;
    }

    private void ProcessTopology(int bodyBudget, float dt)
    {
        for (var i = Bodies.Count - 1; i >= 0 && bodyBudget > 0; i--)
        {
            var source = Bodies[i];
            if (!source.TopologyDirty || source.IsGrabbed) continue;
            bodyBudget--;
            var components = source.SplitDisconnectedComponents();
            _woundBuffer.Clear();
            source.DrainWounds(_woundBuffer);
            if (components.Count <= 1)
            {
                RegisterWoundEvents(source, _woundBuffer, dt);
                continue;
            }

            Bodies.RemoveAt(i);
            RemoveDetachedState(source);
            var largest = components.MaxBy(component => component.Particles.Length)!;
            SoftBody? woundHost = null;

            if (!source.IsDetachedDebris && largest.Particles.Length >= 7)
            {
                largest.SetMode(SimulationMode.FullTissue);
                Bodies.Add(largest);
                woundHost = largest;
            }
            else
            {
                AddDetachedChunk(largest, dt);
                woundHost = largest;
            }

            foreach (var component in components)
            {
                if (ReferenceEquals(component, largest)) continue;
                AddDetachedChunk(component, dt);
            }

            MigrateWounds(source, woundHost);
            RegisterWoundEvents(woundHost, _woundBuffer, dt);
            TopologySplitsThisStep++;
            TotalTopologySplits++;
            ComponentsCreatedThisStep += components.Count;
        }
    }

    private void AddDetachedChunk(SoftBody body, float dt)
    {
        body.MarkDetachedDebris(dt);
        Bodies.Add(body);
        var state = new DetachedChunkState(body);
        _detachedChunks.Add(state);
        // Point bites can isolate lattice nodes after every surrounding tissue
        // triangle has gone. They have mass but no coherent visible material,
        // so start their budgeted pixel conversion immediately instead of
        // keeping an almost invisible ghost mini-body alive.
        if (!body.AreaConstraints.Any(area => !area.Broken))
            state.StartCrumbling(0f, body.Center);
        if (_detachedChunks.Count > 64)
            _detachedChunks[0].StartCrumbling(0f, _detachedChunks[0].LowestPoint());
    }

    private void UpdateDetachedChunks(float dt)
    {
        var emissionBudgetRemaining = 3;
        for (var i = _detachedChunks.Count - 1; i >= 0; i--)
        {
            var chunk = _detachedChunks[i];
            chunk.Age += dt;
            if (chunk.Body.LastTerrainImpact > 0.01f)
            {
                chunk.HasTerrainContact = true;
                chunk.LastTerrainContactPoint = chunk.Body.LastTerrainImpactPoint;
            }
            if (!chunk.IsCrumbling)
            {
                if (chunk.Age >= 0.12f && chunk.Body.LastBreakupImpact >= 55f)
                {
                    var delay = Math.Clamp(0.42f - chunk.Body.LastBreakupImpact * 0.0015f, 0.08f, 0.34f);
                    chunk.StartCrumbling(delay, chunk.Body.LastBreakupImpactPoint);
                }
                else if (chunk.Age >= 0.55f && chunk.Body.IsSleeping && chunk.HasTerrainContact)
                {
                    chunk.StartCrumbling(0.14f, chunk.LastTerrainContactPoint);
                }
                else if (chunk.Age >= 8f)
                {
                    chunk.StartCrumbling(0.10f, chunk.LowestPoint());
                }
            }
            if (!chunk.IsCrumbling) continue;
            foreach (var impactPoint in chunk.Body.BreakupImpactPoints) chunk.AddImpactOrigin(impactPoint);
            chunk.CrumbleRemaining -= dt;
            if (chunk.CrumbleRemaining > 0f) continue;
            chunk.CrumbleElapsed += dt;
            chunk.EmissionAccumulator = MathF.Min(3f, chunk.EmissionAccumulator + 240f * dt);
            var pixelsToEmit = Math.Min((int)chunk.EmissionAccumulator, emissionBudgetRemaining);
            var pixelsEmitted = 0;
            while (pixelsEmitted < pixelsToEmit)
            {
                var particleIndex = chunk.NextEligibleParticle();
                if (particleIndex < 0 || !Granular.TryEmitDetachedPixel(chunk.Body, particleIndex, dt)) break;
                pixelsEmitted++;
                chunk.PixelsRemaining[particleIndex]--;
                if (chunk.PixelsRemaining[particleIndex] > 0) continue;
                Granular.RecordDetachedSourceConverted();
                chunk.Body.MarkParticleConverted(particleIndex);
            }
            chunk.EmissionAccumulator -= pixelsEmitted;
            emissionBudgetRemaining -= pixelsEmitted;
            if (chunk.Body.ActiveParticleCount > 0) continue;
            Bodies.Remove(chunk.Body);
            RemoveWounds(chunk.Body);
            _detachedChunks.RemoveAt(i);
        }
    }

    private void RemoveDetachedState(SoftBody body)
    {
        for (var i = _detachedChunks.Count - 1; i >= 0; i--)
            if (ReferenceEquals(_detachedChunks[i].Body, body)) _detachedChunks.RemoveAt(i);
    }

    private void RegisterPendingWounds(float dt)
    {
        foreach (var body in Bodies)
        {
            _woundBuffer.Clear();
            body.DrainWounds(_woundBuffer);
            RegisterWoundEvents(body, _woundBuffer, dt);
        }
    }

    private void RegisterWoundEvents(SoftBody body, IReadOnlyList<WoundEvent> events, float dt)
    {
        foreach (var woundEvent in events)
        {
            var particleIndex = NearestParticle(body, woundEvent.Position);
            ActiveWound? existing = null;
            for (var i = 0; i < _activeWounds.Count; i++)
            {
                var wound = _activeWounds[i];
                if (!ReferenceEquals(wound.Body, body) || wound.ParticleIndex != particleIndex) continue;
                existing = wound;
                break;
            }

            if (existing is not null)
            {
                existing.Severity = MathF.Min(8f, existing.Severity + woundEvent.Severity * 0.7f);
                var combinedNormal = existing.Normal + woundEvent.Normal;
                if (combinedNormal.LengthSquared() > 0.001f) existing.Normal = Vector2.Normalize(combinedNormal);
            }
            else if (_activeWounds.Count < MaxActiveWounds)
            {
                existing = new ActiveWound(body, particleIndex, woundEvent.Normal, woundEvent.Severity);
                _activeWounds.Add(existing);
            }

            if (existing is not null)
            {
                var initialCount = Math.Clamp((int)MathF.Ceiling(woundEvent.Severity * 1.4f), 1, 4);
                Granular.EmitBlood(woundEvent, dt, initialCount, speedScale: 0.9f);
            }
        }
    }

    private void UpdateWounds(float dt)
    {
        for (var i = _activeWounds.Count - 1; i >= 0; i--)
        {
            var wound = _activeWounds[i];
            if (!Bodies.Contains(wound.Body) || wound.ParticleIndex >= wound.Body.Particles.Length)
            {
                _activeWounds.RemoveAt(i);
                continue;
            }

            wound.Age += dt;
            wound.Severity = MathF.Max(0f, wound.Severity - (0.10f + 0.035f * wound.Age) * dt);
            if (wound.Severity <= 0.04f || wound.Age >= 15f)
            {
                _activeWounds.RemoveAt(i);
                continue;
            }

            var bleedRate = 0.35f + wound.Severity * 2.8f;
            wound.EmissionAccumulator = MathF.Min(3f, wound.EmissionAccumulator + bleedRate * dt);
            var requested = Math.Min(3, (int)wound.EmissionAccumulator);
            if (requested <= 0) continue;
            var position = wound.Body.Particles[wound.ParticleIndex].Position;
            var emitted = Granular.EmitBlood(
                new WoundEvent(position, wound.Normal, wound.Severity),
                dt,
                requested,
                speedScale: 0.55f);
            wound.EmissionAccumulator -= emitted;
        }
    }

    private void MigrateWounds(SoftBody source, SoftBody target)
    {
        for (var i = 0; i < _activeWounds.Count; i++)
        {
            var wound = _activeWounds[i];
            if (!ReferenceEquals(wound.Body, source)) continue;
            var position = source.Particles[Math.Min(wound.ParticleIndex, source.Particles.Length - 1)].Position;
            wound.Body = target;
            wound.ParticleIndex = NearestParticle(target, position);
            wound.Severity *= 0.92f;
        }
    }

    private void RemoveWounds(SoftBody body)
    {
        for (var i = _activeWounds.Count - 1; i >= 0; i--)
            if (ReferenceEquals(_activeWounds[i].Body, body)) _activeWounds.RemoveAt(i);
    }

    private static int NearestParticle(SoftBody body, Vector2 point)
    {
        var best = 0;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < body.Particles.Length; i++)
        {
            var distance = Vector2.DistanceSquared(body.Particles[i].Position, point);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }
        return best;
    }

    private void ResolveWorld(SoftBody body, float dt)
    {
        for (var i = 0; i < body.Particles.Length; i++)
        {
            if (!body.IsPhysicalParticle(i)) continue;
            ref var particle = ref body.Particles[i];
            var collision = Grid.ResolveParticle(ref particle, dt);
            if (collision.Hit)
            {
                ContactsThisStep++;
                if (collision.Impact >= body.LastImpact) body.LastImpactPoint = particle.Position;
                if (collision.Impact >= body.LastTerrainImpact) body.LastTerrainImpactPoint = particle.Position;
                body.LastImpact = MathF.Max(body.LastImpact, collision.Impact);
                body.LastTerrainImpact = MathF.Max(body.LastTerrainImpact, collision.Impact);
                body.RecordBreakupImpact(particle.Position, collision.Impact);
                if (Grid.ApplyImpactDamage(collision.CellX, collision.CellY, collision.Impact))
                    body.AddImpulse(new Vector2(0f, -collision.Impact * 0.025f), dt);
            }

            foreach (var conveyor in Conveyors)
            {
                var forceTopContainment = conveyor.IsSystemControlled &&
                                          _conveyorCommittedParents.Contains(body.ParentId) &&
                                          particle.Position.X >= conveyor.Position.X - particle.Radius &&
                                          particle.Position.X <= conveyor.Position.X + conveyor.Width + particle.Radius;
                if (!forceTopContainment && !conveyor.ContainsPoint(particle.Position, particle.Radius)) continue;
                var contact = conveyor.ResolveParticle(ref particle, dt, true, forceTopContainment);
                if (!contact.Hit) continue;
                if (contact.IsTop) _conveyorCommittedParents.Add(body.ParentId);
                ContactsThisStep++;
                if (contact.Impact >= body.LastImpact) body.LastImpactPoint = contact.ContactPoint;
                if (contact.Impact >= body.LastTerrainImpact) body.LastTerrainImpactPoint = contact.ContactPoint;
                body.LastImpact = MathF.Max(body.LastImpact, contact.Impact);
                body.LastTerrainImpact = MathF.Max(body.LastTerrainImpact, contact.Impact);
                body.RecordBreakupImpact(contact.ContactPoint, contact.Impact);
            }

            if (HoldingChamber is not null)
            {
                var chamberContact = HoldingChamber.ResolveParticle(
                    ref particle,
                    dt,
                    HoldingChamber.IsAdmitted(body));
                if (chamberContact.Hit)
                {
                    ContactsThisStep++;
                    if (chamberContact.Impact >= body.LastImpact)
                        body.LastImpactPoint = chamberContact.ContactPoint;
                    body.LastImpact = MathF.Max(body.LastImpact, chamberContact.Impact);
                }
            }

            if (ProcessingLine is not null)
            {
                var machineContact = ProcessingLine.ResolveParticle(body, ref particle, dt);
                if (machineContact.Hit)
                {
                    ContactsThisStep++;
                    if (machineContact.Impact >= body.LastImpact)
                        body.LastImpactPoint = machineContact.ContactPoint;
                    body.LastImpact = MathF.Max(body.LastImpact, machineContact.Impact);
                }
            }
        }
    }

    public SoftBody? PickBody(Vector2 point)
    {
        SoftBody? best = null;
        var bestDistance = float.MaxValue;
        foreach (var body in Bodies)
        {
            if (!body.IsPickable) continue;
            if (TubeFeed?.Contains(body) == true) continue;
            if (ProcessingLine?.HasEnteredBayOne(body) == true) continue;
            if (ProcessingLine?.IsLocked(body) == true) continue;
            if (HoldingChamber is not null &&
                HoldingChamber.IsInFeedEnvelope(body) &&
                !HoldingChamber.HasExited(body))
                continue;
            var distance = body.DistanceToPointSquared(point);
            var pickRadius = MathF.Max(16f, body.ParticleSpacing * 1.2f);
            if (distance > pickRadius * pickRadius || distance >= bestDistance) continue;
            best = body;
            bestDistance = distance;
        }
        return best;
    }

    public bool IsConveyorCommitted(SoftBody body)
        => _conveyorCommittedParents.Contains(body.ParentId);

    public Vector2 ConstrainGrabTarget(SoftBody body, Vector2 target)
    {
        var left = Grid.CellSize;
        var right = (Grid.Columns - 1) * Grid.CellSize;
        var top = 0f;
        var ground = (Grid.Rows - 1) * Grid.CellSize;
        return body.ConstrainGrabTarget(target, left, right, top, ground);
    }

    private sealed class ActiveWound
    {
        public ActiveWound(SoftBody body, int particleIndex, Vector2 normal, float severity)
        {
            Body = body;
            ParticleIndex = particleIndex;
            Normal = normal.LengthSquared() > 0.001f ? Vector2.Normalize(normal) : Vector2.UnitY;
            Severity = severity;
        }

        public SoftBody Body;
        public int ParticleIndex;
        public Vector2 Normal;
        public float Severity;
        public float Age;
        public float EmissionAccumulator;
    }

    private sealed class DetachedChunkState
    {
        public DetachedChunkState(SoftBody body)
        {
            Body = body;
            PixelsRemaining = body.Particles
                .Select(particle => (byte)Math.Clamp((int)(particle.Radius * 0.38f), 3, 6))
                .ToArray();
        }

        public SoftBody Body { get; }
        public float Age { get; set; }
        public bool IsCrumbling { get; private set; }
        public float CrumbleRemaining { get; set; }
        public float CrumbleElapsed { get; set; }
        public float[] CrumbleArrivalTimes { get; private set; } = Array.Empty<float>();
        public byte[] PixelsRemaining { get; }
        public float EmissionAccumulator { get; set; }
        private int EmissionCursor { get; set; }
        private List<Vector2> ImpactOrigins { get; } = new(6);
        public int ImpactOriginCount => ImpactOrigins.Count;
        public bool HasTerrainContact { get; set; }
        public Vector2 LastTerrainContactPoint { get; set; }

        public void StartCrumbling(float delay, Vector2 origin)
        {
            if (IsCrumbling)
            {
                AddImpactOrigin(origin);
                CrumbleRemaining = MathF.Min(CrumbleRemaining, MathF.Max(0f, delay));
                return;
            }
            IsCrumbling = true;
            Body.BeginCrumbling();
            CrumbleRemaining = MathF.Max(0f, delay);
            CrumbleElapsed = 0f;
            EmissionAccumulator = 0f;
            CrumbleArrivalTimes = Enumerable.Repeat(float.MaxValue, Body.Particles.Length).ToArray();
            AddImpactOrigin(origin);
        }

        public void AddImpactOrigin(Vector2 origin)
        {
            var minimumDistanceSq = Body.ParticleSpacing * Body.ParticleSpacing * 0.49f;
            for (var i = 0; i < ImpactOrigins.Count; i++)
                if (Vector2.DistanceSquared(ImpactOrigins[i], origin) < minimumDistanceSq) return;
            if (ImpactOrigins.Count >= 8) return;
            ImpactOrigins.Add(origin);
            for (var index = 0; index < Body.Particles.Length; index++)
            {
                var hash = unchecked((uint)(index * 1103515245 + Body.ParentId * 12345));
                var phase = (hash & 1023u) / 1023f - 0.5f;
                var distance = MathF.Max(0f,
                    Vector2.Distance(origin, Body.Particles[index].Position) + phase * Body.ParticleSpacing * 0.9f);
                CrumbleArrivalTimes[index] = MathF.Min(
                    CrumbleArrivalTimes[index],
                    CrumbleElapsed + distance / 96f);
            }
        }

        public int NextEligibleParticle()
        {
            for (var scanned = 0; scanned < Body.Particles.Length; scanned++)
            {
                var index = (EmissionCursor + scanned) % Body.Particles.Length;
                if (Body.IsConvertedParticle(index) || PixelsRemaining[index] == 0 ||
                    (!Body.IsReleasedParticle(index) && CrumbleArrivalTimes[index] > CrumbleElapsed)) continue;
                EmissionCursor = (index + 1) % Body.Particles.Length;
                return index;
            }
            return -1;
        }

        public Vector2 LowestPoint()
        {
            var lowest = Body.Center;
            for (var i = 0; i < Body.Particles.Length; i++)
            {
                if (!Body.IsPhysicalParticle(i)) continue;
                if (Body.Particles[i].Position.Y > lowest.Y) lowest = Body.Particles[i].Position;
            }
            return lowest;
        }
    }
}
