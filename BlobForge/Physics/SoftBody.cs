using System.Numerics;

namespace BlobForge.Physics;

public sealed class SoftBody
{
    private const float BondCompliance = 0.00012f;
    private const float AreaCompliance = 0.006f;
    // Supported XPBD tissue continually makes tiny positional corrections. Treat
    // that bounded internal relaxation as rest when the body center is also quiet.
    private const float SleepSpeed = 48f;
    private const float CenterSleepSpeed = 4.5f;
    private const float WakeSpeed = 6f;
    private const float SleepDelay = 0.9f;
    private static int _nextId;

    private float _restAreaTotal;
    private float _quietTime;
    private Vector2 _lastGravity;
    private float _pressureDampingTime;
    private Vector2 _pressureDirection;
    private bool _pressureDampedThisStep;
    private Vector2 _lastSleepCenter;
    private SimulationMode _modeBeforeGrab = SimulationMode.ReducedTissue;
    private SimulationMode _modeBeforeSleep = SimulationMode.ReducedTissue;
    private readonly float[] _grabWeights;
    private readonly Vector2[] _grabOffsets;
    private Vector2 _grabMinimumOffset;
    private Vector2 _grabMaximumOffset;
    private readonly bool[] _surfaceMask;
    private readonly bool[] _damageMask;
    private readonly bool[] _convertedMask;
    // A released particle no longer belongs to any intact material cell. It is
    // removed from physics immediately, but remains an unconverted material
    // source until the granular erosion budget has emitted its pixels.
    private readonly bool[] _releasedMask;
    private int _activeParticleCount;
    private int _physicalParticleCount;
    private readonly List<CutSegment> _cutSegments = new(8);
    private readonly Vector2[] _shapeReferencePositions;
    private readonly Vector2 _shapeReferenceCenter;
    private readonly List<WoundEvent> _pendingWounds = new();
    private readonly List<Vector2> _breakupImpactPoints = new(8);

    public SoftBody(Vector2 center, float radius, int targetParticleCount = 61)
    {
        Id = Interlocked.Increment(ref _nextId);
        ParentId = Id;
        Radius = radius;

        var target = Math.Max(7, targetParticleCount);
        var hexCellArea = MathF.PI * radius * radius / target;
        var spacing = MathF.Sqrt(hexCellArea / 0.8660254f);
        spacing = Math.Clamp(spacing, radius / 8f, radius / 1.45f);
        ParticleSpacing = spacing;

        var particles = new List<Particle>(target + 12);
        var lattice = new Dictionary<(int Q, int R), int>();
        var range = (int)MathF.Ceiling(radius / spacing) + 2;
        var inclusionRadius = radius - spacing * 0.12f;

        for (var r = -range; r <= range; r++)
        for (var q = -range; q <= range; q++)
        {
            var offset = new Vector2(
                spacing * (q + r * 0.5f),
                spacing * 0.8660254f * r);
            if (offset.LengthSquared() > inclusionRadius * inclusionRadius) continue;
            var position = center + offset;
            lattice[(q, r)] = particles.Count;
            particles.Add(new Particle
            {
                Position = position,
                PreviousPosition = position,
                InverseMass = 1f,
                Radius = spacing * 0.58f
            });
        }

        Particles = particles.ToArray();
        Constraints = new List<DistanceConstraint>(Particles.Length * 3);
        AreaConstraints = new List<AreaConstraint>(Particles.Length * 2);

        var bondDirections = new[] { (Q: 1, R: 0), (Q: 0, R: 1), (Q: 1, R: -1) };
        foreach (var pair in lattice)
        {
            foreach (var direction in bondDirections)
            {
                if (!lattice.TryGetValue((pair.Key.Q + direction.Q, pair.Key.R + direction.R), out var neighbor)) continue;
                Constraints.Add(MakeBond(pair.Value, neighbor));
            }

            AddTriangle(lattice, pair.Value, (pair.Key.Q + 1, pair.Key.R), (pair.Key.Q, pair.Key.R + 1));
            AddTriangle(lattice, pair.Value, (pair.Key.Q + 1, pair.Key.R), (pair.Key.Q + 1, pair.Key.R - 1));
        }

        _restAreaTotal = AreaConstraints.Sum(a => MathF.Abs(a.RestArea));
        _lastSleepCenter = center;
        _grabWeights = new float[Particles.Length];
        _grabOffsets = new Vector2[Particles.Length];
        _surfaceMask = new bool[Particles.Length];
        _damageMask = new bool[Particles.Length];
        _convertedMask = new bool[Particles.Length];
        _releasedMask = new bool[Particles.Length];
        _activeParticleCount = Particles.Length;
        _physicalParticleCount = Particles.Length;
        _shapeReferencePositions = Particles.Select(particle => particle.Position).ToArray();
        _shapeReferenceCenter = Center;
        RefreshSurfaceMask();
        Mode = SimulationMode.ReducedTissue;
    }

    private SoftBody(
        Particle[] particles,
        List<DistanceConstraint> constraints,
        List<AreaConstraint> areaConstraints,
        float particleSpacing,
        int parentId,
        SimulationMode mode,
        bool[] damageMask,
        IReadOnlyList<CutSegment> worldCutSegments)
    {
        Id = Interlocked.Increment(ref _nextId);
        ParentId = parentId;
        Particles = particles;
        Constraints = constraints;
        AreaConstraints = areaConstraints;
        ParticleSpacing = particleSpacing;
        Mode = particles.Length <= 3 ? SimulationMode.LooseFragment : mode;
        _restAreaTotal = areaConstraints.Where(a => !a.Broken).Sum(a => MathF.Abs(a.RestArea));
        _grabWeights = new float[particles.Length];
        _grabOffsets = new Vector2[particles.Length];
        _surfaceMask = new bool[particles.Length];
        _damageMask = damageMask;
        _convertedMask = new bool[particles.Length];
        _releasedMask = new bool[particles.Length];
        _activeParticleCount = particles.Length;
        _physicalParticleCount = particles.Length;
        _shapeReferencePositions = particles.Select(particle => particle.Position).ToArray();
        _shapeReferenceCenter = Center;
        foreach (var segment in ClipCutSegmentsToComponent(worldCutSegments))
            _cutSegments.Add(new CutSegment(
                segment.Start - _shapeReferenceCenter,
                segment.End - _shapeReferenceCenter));
        RefreshSurfaceMask();
        _lastSleepCenter = Center;
        var center = Center;
        Radius = particles.Length == 0
            ? particleSpacing
            : particles.Max(p => Vector2.Distance(center, p.Position) + p.Radius);
    }

    public int Id { get; }
    public int ParentId { get; }
    public Particle[] Particles { get; }
    public List<DistanceConstraint> Constraints { get; }
    public List<AreaConstraint> AreaConstraints { get; }
    public float ParticleSpacing { get; }
    public float Radius { get; private set; }
    public float VisualRotation => CurrentShapeAngle(Center);
    public SimulationMode Mode { get; private set; }
    public bool IsSleeping { get; private set; }
    public bool IsGrabbed { get; private set; }
    public bool TopologyDirty { get; private set; }
    internal int TopologyRevision { get; private set; }
    public int GrabbedParticle { get; private set; } = -1;
    public Vector2 GrabTarget { get; private set; }
    public float LastImpact { get; internal set; }
    public Vector2 LastImpactPoint { get; internal set; }
    public float LastTerrainImpact { get; internal set; }
    public Vector2 LastTerrainImpactPoint { get; internal set; }
    public IReadOnlyList<Vector2> BreakupImpactPoints => _breakupImpactPoints;
    public float LastBreakupImpact { get; private set; }
    public Vector2 LastBreakupImpactPoint { get; private set; }
    public int BrokenLinkCount { get; private set; }
    public float LastAverageSpeed { get; private set; }
    public float LastCenterSpeed { get; private set; }
    public int LastSupportedParticles { get; private set; }
    public bool IsDetachedDebris { get; private set; }
    public bool IsCrumbling { get; private set; }
    public bool HasLocalDamage => BrokenLinkCount > 0 || Array.IndexOf(_damageMask, true) >= 0;
    public bool IsPickable => !IsDetachedDebris && Mode != SimulationMode.LooseFragment && Particles.Length >= 7;
    public int ActiveParticleCount => _activeParticleCount;
    public int PhysicalParticleCount => _physicalParticleCount;

    public Vector2 Center
    {
        get
        {
            var sum = Vector2.Zero;
            var count = 0;
            for (var i = 0; i < Particles.Length; i++)
            {
                if (!IsPhysicalParticle(i)) continue;
                sum += Particles[i].Position;
                count++;
            }
            if (count > 0) return sum / count;

            // Released sources can briefly outlive the coherent object while
            // their remaining pixels are emitted. Keep their last location as a
            // stable material origin without putting them back into physics.
            for (var i = 0; i < Particles.Length; i++)
            {
                if (_convertedMask[i]) continue;
                sum += Particles[i].Position;
                count++;
            }
            return count > 0 ? sum / count : Vector2.Zero;
        }
    }

    private DistanceConstraint MakeBond(int a, int b)
        => new(a, b, Vector2.Distance(Particles[a].Position, Particles[b].Position), BondCompliance);

    private void AddTriangle(
        IReadOnlyDictionary<(int Q, int R), int> lattice,
        int a,
        (int Q, int R) bCoordinate,
        (int Q, int R) cCoordinate)
    {
        if (!lattice.TryGetValue(bCoordinate, out var b) || !lattice.TryGetValue(cCoordinate, out var c)) return;
        var restArea = AreaConstraint.SignedArea(Particles[a].Position, Particles[b].Position, Particles[c].Position);
        if (MathF.Abs(restArea) < 0.001f) return;
        AreaConstraints.Add(new AreaConstraint(a, b, c, restArea, AreaCompliance));
    }

    public void SetMode(SimulationMode mode)
    {
        if (mode == SimulationMode.Asleep)
        {
            if (!IsSleeping) Sleep();
            return;
        }
        if (IsSleeping) return;
        Mode = mode;
    }

    public void Integrate(float dt, Vector2 gravity)
    {
        _lastGravity = gravity;
        if (IsSleeping) return;
        var settings = ModeSettings.For(Mode);
        var dt2 = dt * dt;

        for (var i = 0; i < Particles.Length; i++)
        {
            ref var p = ref Particles[i];
            if (!IsPhysicalParticle(i)) continue;
            if (p.SupportMemory > 0) p.SupportMemory--;
            p.Supported = p.SupportMemory > 0;
            if (p.ContactMemory > 0) p.ContactMemory--;
            p.Contacting = p.ContactMemory > 0;
            if (p.InverseMass <= 0f) continue;

            var velocity = (p.Position - p.PreviousPosition) * settings.LinearDamping;
            p.PreviousPosition = p.Position;
            p.Position += velocity + (gravity + p.Acceleration) * dt2;
            p.Acceleration = Vector2.Zero;
        }
    }

    public void PrepareConstraintSolve()
    {
        for (var i = 0; i < Constraints.Count; i++)
        {
            var constraint = Constraints[i];
            constraint.Lambda = 0f;
            Constraints[i] = constraint;
        }
        for (var i = 0; i < AreaConstraints.Count; i++)
        {
            var constraint = AreaConstraints[i];
            constraint.Lambda = 0f;
            AreaConstraints[i] = constraint;
        }
    }

    public void SolveConstraintIteration(float dt)
    {
        if (IsSleeping) return;
        for (var i = 0; i < Constraints.Count; i++)
        {
            var constraint = Constraints[i];
            if (!constraint.Broken) SolveDistance(ref constraint, dt);
            Constraints[i] = constraint;
        }
        for (var i = 0; i < AreaConstraints.Count; i++)
        {
            var constraint = AreaConstraints[i];
            if (!constraint.Broken) SolveArea(ref constraint, dt);
            AreaConstraints[i] = constraint;
        }
        if (IsGrabbed) SolveGrab();
    }

    private void SolveDistance(ref DistanceConstraint constraint, float dt)
    {
        if (!IsPhysicalParticle(constraint.A) || !IsPhysicalParticle(constraint.B)) return;
        ref var a = ref Particles[constraint.A];
        ref var b = ref Particles[constraint.B];
        var delta = b.Position - a.Position;
        var lengthSq = delta.LengthSquared();
        if (lengthSq < 0.0001f) return;
        var length = MathF.Sqrt(lengthSq);
        var normal = delta / length;
        // A passive blob under deliberate held pressure temporarily becomes
        // more compliant. This lets the contact form a broad material dent
        // instead of translating the whole lattice as a stiff puck. The short
        // pressure timer then expires after release, restoring the ordinary
        // bond stiffness and producing visible shape-recovery recoil.
        var pressureCompliance = _pressureDampingTime > 0f && !IsGrabbed ? 3.2f : 1f;
        var alpha = constraint.Compliance * pressureCompliance / (dt * dt);
        var denominator = a.InverseMass + b.InverseMass + alpha;
        if (denominator <= 0f) return;

        var c = length - constraint.RestLength;
        var deltaLambda = (-c - alpha * constraint.Lambda) / denominator;
        var maxDeltaLambda = ParticleSpacing * 0.22f / MathF.Max(0.001f, MathF.Max(a.InverseMass, b.InverseMass));
        deltaLambda = Math.Clamp(deltaLambda, -maxDeltaLambda, maxDeltaLambda);
        constraint.Lambda += deltaLambda;
        a.Position -= normal * (a.InverseMass * deltaLambda);
        b.Position += normal * (b.InverseMass * deltaLambda);
    }

    private void SolveArea(ref AreaConstraint constraint, float dt)
    {
        if (!IsPhysicalParticle(constraint.A) || !IsPhysicalParticle(constraint.B) || !IsPhysicalParticle(constraint.C)) return;
        ref var a = ref Particles[constraint.A];
        ref var b = ref Particles[constraint.B];
        ref var c = ref Particles[constraint.C];
        var currentArea = AreaConstraint.SignedArea(a.Position, b.Position, c.Position);
        var value = currentArea - constraint.RestArea;

        var gradA = new Vector2(b.Position.Y - c.Position.Y, c.Position.X - b.Position.X) * 0.5f;
        var gradB = new Vector2(c.Position.Y - a.Position.Y, a.Position.X - c.Position.X) * 0.5f;
        var gradC = new Vector2(a.Position.Y - b.Position.Y, b.Position.X - a.Position.X) * 0.5f;
        var alpha = constraint.Compliance / (dt * dt);
        var denominator =
            a.InverseMass * gradA.LengthSquared() +
            b.InverseMass * gradB.LengthSquared() +
            c.InverseMass * gradC.LengthSquared() + alpha;
        if (denominator < 0.0001f) return;

        var deltaLambda = (-value - alpha * constraint.Lambda) / denominator;
        var maxWeightedGradient = MathF.Max(
            a.InverseMass * gradA.Length(),
            MathF.Max(b.InverseMass * gradB.Length(), c.InverseMass * gradC.Length()));
        var maxDeltaLambda = ParticleSpacing * 0.14f / MathF.Max(0.001f, maxWeightedGradient);
        deltaLambda = Math.Clamp(deltaLambda, -maxDeltaLambda, maxDeltaLambda);
        constraint.Lambda += deltaLambda;
        a.Position += gradA * (a.InverseMass * deltaLambda);
        b.Position += gradB * (b.InverseMass * deltaLambda);
        c.Position += gradC * (c.InverseMass * deltaLambda);
    }

    private void SolveGrab()
    {
        for (var i = 0; i < Particles.Length; i++)
        {
            var weight = _grabWeights[i];
            if (weight <= 0f) continue;
            var desired = GrabTarget + _grabOffsets[i];
            var correction = desired - Particles[i].Position;
            var maxLength = ParticleSpacing * 3f;
            var length = correction.Length();
            if (length > maxLength) correction *= maxLength / length;
            Particles[i].Position += correction * (0.10f + 0.32f * weight);
        }
    }

    public void BeginGrab(Vector2 target)
    {
        Wake();
        _modeBeforeGrab = Mode;
        Mode = SimulationMode.FullTissue;
        IsGrabbed = true;
        GrabTarget = target;

        var bestDistance = float.MaxValue;
        for (var i = 0; i < Particles.Length; i++)
        {
            var distance = Vector2.DistanceSquared(Particles[i].Position, target);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            GrabbedParticle = i;
        }

        var anchor = Particles[GrabbedParticle].Position;
        var influenceRadius = MathF.Max(ParticleSpacing * 2.8f, Radius * 0.88f);
        _grabMinimumOffset = new Vector2(float.MaxValue, float.MaxValue);
        _grabMaximumOffset = new Vector2(float.MinValue, float.MinValue);
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            var distance = Vector2.Distance(Particles[i].Position, anchor);
            var normalized = Math.Clamp(1f - distance / influenceRadius, 0f, 1f);
            _grabWeights[i] = normalized * normalized;
            _grabOffsets[i] = Particles[i].Position - anchor;
            _grabMinimumOffset.X = MathF.Min(_grabMinimumOffset.X, _grabOffsets[i].X - Particles[i].Radius);
            _grabMinimumOffset.Y = MathF.Min(_grabMinimumOffset.Y, _grabOffsets[i].Y - Particles[i].Radius);
            _grabMaximumOffset.X = MathF.Max(_grabMaximumOffset.X, _grabOffsets[i].X + Particles[i].Radius);
            _grabMaximumOffset.Y = MathF.Max(_grabMaximumOffset.Y, _grabOffsets[i].Y + Particles[i].Radius);
        }
        _grabWeights[GrabbedParticle] = 1f;
        _grabOffsets[GrabbedParticle] = Vector2.Zero;
    }

    public void UpdateGrabTarget(Vector2 desiredTarget, float dt)
    {
        if (!IsGrabbed) return;
        var delta = desiredTarget - GrabTarget;
        var maxSpeed = LastImpact > 12f ? 600f : 1900f;
        var maxDistance = maxSpeed * dt;
        var distance = delta.Length();
        if (distance > maxDistance) delta *= maxDistance / distance;
        GrabTarget += delta;
    }

    public Vector2 ConstrainGrabTarget(Vector2 target, float left, float right, float top, float bottom)
    {
        if (!IsGrabbed || GrabbedParticle < 0) return target;
        // The target may press a short distance beyond the rigid-fit position.
        // Actual particles remain arena/grid constrained, so this distance turns
        // into tissue compression instead of wall penetration.
        var compressionAllowance = MathF.Min(Radius * 0.32f, ParticleSpacing * 1.55f);
        var minTargetX = left - _grabMinimumOffset.X - compressionAllowance;
        var maxTargetX = right - _grabMaximumOffset.X + compressionAllowance;
        var minTargetY = top - _grabMinimumOffset.Y;
        var maxTargetY = bottom - _grabMaximumOffset.Y;
        if (minTargetX > maxTargetX) (minTargetX, maxTargetX) = (left, right);
        if (minTargetY > maxTargetY) (minTargetY, maxTargetY) = (top, bottom);
        return new Vector2(
            Math.Clamp(target.X, minTargetX, maxTargetX),
            Math.Clamp(target.Y, minTargetY, maxTargetY));
    }

    public void EndGrab(Vector2 releaseVelocity, float dt)
    {
        if (!IsGrabbed) return;
        var releaseSpeed = releaseVelocity.Length();
        if (releaseSpeed > 1950f) releaseVelocity *= 1950f / releaseSpeed;
        for (var i = 0; i < Particles.Length; i++)
        {
            var influence = 0.58f + 0.42f * _grabWeights[i];
            Particles[i].PreviousPosition = Particles[i].Position - releaseVelocity * dt * influence;
            _grabWeights[i] = 0f;
            _grabOffsets[i] = Vector2.Zero;
        }
        IsGrabbed = false;
        GrabbedParticle = -1;
        Mode = _modeBeforeGrab == SimulationMode.Asleep ? SimulationMode.ReducedTissue : _modeBeforeGrab;
        Wake();
    }

    public void AddImpulse(Vector2 impulse, float dt)
    {
        Wake();
        for (var i = 0; i < Particles.Length; i++)
        {
            Particles[i].PreviousPosition -= impulse * dt;
        }
    }

    public int DamageBonds(Vector2 point, float radius, float damage)
        => DamageLine(point, point, radius, damage);

    public int DamagePath(IReadOnlyList<Vector2> path, float thickness, float damage)
    {
        if (path.Count < 2) return 0;
        var broken = 0;
        for (var i = 0; i < Constraints.Count; i++)
        {
            var bond = Constraints[i];
            if (bond.Broken) continue;
            var a = Particles[bond.A].Position;
            var b = Particles[bond.B].Position;
            var distance = float.MaxValue;
            for (var segmentIndex = 1; segmentIndex < path.Count; segmentIndex++)
                distance = MathF.Min(distance, SegmentDistance(a, b, path[segmentIndex - 1], path[segmentIndex]));
            if (distance > thickness) continue;

            var falloff = Math.Clamp(1f - distance / MathF.Max(0.001f, thickness), 0.2f, 1f);
            bond.Health -= damage * falloff;
            Constraints[i] = bond;
            if (bond.Health <= 0f && BreakBond(i, true)) broken++;
        }

        if (broken <= 0) return 0;
        for (var segmentIndex = 1; segmentIndex < path.Count; segmentIndex++)
            RecordCutSegment(new CutSegment(path[segmentIndex - 1], path[segmentIndex]));
        RefreshSurfaceMask();
        TopologyDirty = true;
        Wake();
        return broken;
    }

    public int DamageLine(Vector2 start, Vector2 end, float thickness, float damage)
    {
        var requestedStart = start;
        var requestedEnd = end;
        var isSlice = Vector2.DistanceSquared(start, end) >= 1f;
        if (Vector2.DistanceSquared(start, end) < 1f)
        {
            if (!ContainsVisibleTissue(start)) return 0;
            var closestSurface = ClosestSurfacePosition(start);
            if (Vector2.DistanceSquared(start, closestSurface) > ParticleSpacing * ParticleSpacing * 0.36f)
                start = end = closestSurface;
        }

        var broken = 0;
        for (var i = 0; i < Constraints.Count; i++)
        {
            var bond = Constraints[i];
            if (bond.Broken) continue;
            var a = Particles[bond.A].Position;
            var b = Particles[bond.B].Position;
            var distance = SegmentDistance(a, b, start, end);
            if (distance > thickness) continue;

            var falloff = Math.Clamp(1f - distance / MathF.Max(0.001f, thickness), 0.2f, 1f);
            bond.Health -= damage * falloff;
            Constraints[i] = bond;
            if (bond.Health <= 0f && BreakBond(i, true)) broken++;
        }

        if (broken > 0)
        {
            if (isSlice)
            {
                RecordCutSegment(new CutSegment(requestedStart, requestedEnd));
            }
            RefreshSurfaceMask();
            TopologyDirty = true;
            Wake();
        }
        return broken;
    }

    public void DrainWounds(List<WoundEvent> destination)
    {
        destination.AddRange(_pendingWounds);
        _pendingWounds.Clear();
    }

    private bool BreakBond(int index, bool emitWound)
    {
        var bond = Constraints[index];
        if (bond.Broken) return false;
        bond.Broken = true;
        Constraints[index] = bond;
        TopologyRevision++;
        _damageMask[bond.A] = true;
        _damageMask[bond.B] = true;
        BrokenLinkCount++;
        BreakAreasUsingEdge(bond.A, bond.B);
        TopologyDirty = true;

        if (emitWound)
        {
            var midpoint = (Particles[bond.A].Position + Particles[bond.B].Position) * 0.5f;
            var normal = midpoint - Center;
            if (normal.LengthSquared() < 0.001f)
            {
                var edge = Particles[bond.B].Position - Particles[bond.A].Position;
                normal = new Vector2(-edge.Y, edge.X);
            }
            if (normal.LengthSquared() > 0.001f) normal = Vector2.Normalize(normal);
            _pendingWounds.Add(new WoundEvent(midpoint, normal, 1f));
        }
        return true;
    }

    private void BreakAreasUsingEdge(int a, int b)
    {
        for (var i = 0; i < AreaConstraints.Count; i++)
        {
            var area = AreaConstraints[i];
            if (!area.Broken && area.IncludesEdge(a, b)) area.Broken = true;
            AreaConstraints[i] = area;
        }
    }

    public List<SoftBody> SplitDisconnectedComponents()
    {
        if (!TopologyDirty || IsGrabbed) return new List<SoftBody>();
        StabilizeDamagedTopology();
        TopologyDirty = false;

        var adjacency = new List<int>[Particles.Length];
        for (var i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>(6);
        foreach (var bond in Constraints)
        {
            if (bond.Broken) continue;
            adjacency[bond.A].Add(bond.B);
            adjacency[bond.B].Add(bond.A);
        }

        var visited = new bool[Particles.Length];
        var components = new List<List<int>>();
        for (var seed = 0; seed < Particles.Length; seed++)
        {
            if (visited[seed]) continue;
            var component = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(seed);
            visited[seed] = true;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var neighbor in adjacency[current])
                {
                    if (visited[neighbor]) continue;
                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }
            components.Add(component);
        }

        if (components.Count <= 1) return new List<SoftBody>();
        var result = new List<SoftBody>(components.Count);
        var worldCutSegments = GetWorldCutSegments();
        foreach (var component in components)
        {
            var map = new Dictionary<int, int>(component.Count);
            var particles = new Particle[component.Count];
            for (var i = 0; i < component.Count; i++)
            {
                map[component[i]] = i;
                particles[i] = Particles[component[i]];
            }

            var bonds = new List<DistanceConstraint>();
            foreach (var source in Constraints)
            {
                if (source.Broken || !map.TryGetValue(source.A, out var a) || !map.TryGetValue(source.B, out var b)) continue;
                bonds.Add(new DistanceConstraint(a, b, source.RestLength, source.Compliance, source.Health));
            }

            var areas = new List<AreaConstraint>();
            foreach (var source in AreaConstraints)
            {
                if (source.Broken ||
                    !map.TryGetValue(source.A, out var a) ||
                    !map.TryGetValue(source.B, out var b) ||
                    !map.TryGetValue(source.C, out var c)) continue;
                areas.Add(new AreaConstraint(a, b, c, source.RestArea, source.Compliance));
            }

            var damageMask = new bool[component.Count];
            for (var i = 0; i < component.Count; i++) damageMask[i] = _damageMask[component[i]];
            var child = new SoftBody(particles, bonds, areas, ParticleSpacing, ParentId, Mode, damageMask, worldCutSegments)
            {
                BrokenLinkCount = damageMask.Count(damaged => damaged)
            };
            child.Wake();
            result.Add(child);
        }
        return result;
    }

    private void StabilizeDamagedTopology()
    {
        // Distance-only links have no rendered material and must never hold two
        // visible regions together or contribute collision appendages.
        var cohesiveEdges = new HashSet<long>();
        foreach (var area in AreaConstraints)
        {
            if (area.Broken) continue;
            cohesiveEdges.Add(EdgeKey(area.A, area.B));
            cohesiveEdges.Add(EdgeKey(area.B, area.C));
            cohesiveEdges.Add(EdgeKey(area.C, area.A));
        }
        var severedOrphans = 0;
        for (var edgeIndex = 0; edgeIndex < Constraints.Count; edgeIndex++)
        {
            var edge = Constraints[edgeIndex];
            if (edge.Broken || cohesiveEdges.Contains(EdgeKey(edge.A, edge.B))) continue;
            if (!_damageMask[edge.A] && !_damageMask[edge.B]) continue;
            if (BreakBond(edgeIndex, false)) severedOrphans++;
        }

        // A cut can leave a lattice node connected by distance bonds even though
        // every tissue triangle around it has been removed. Such a node is not
        // rendered, but it would still take part in surface-hull collision. Sever
        // those ghost appendages before component extraction so the existing
        // loose-fragment pipeline can turn them into physical pixels.
        var areaParticipation = new bool[Particles.Length];
        foreach (var area in AreaConstraints)
        {
            if (area.Broken) continue;
            areaParticipation[area.A] = true;
            areaParticipation[area.B] = true;
            areaParticipation[area.C] = true;
        }

        for (var particleIndex = 0; particleIndex < Particles.Length; particleIndex++)
        {
            if (!_damageMask[particleIndex] || areaParticipation[particleIndex]) continue;
            for (var edgeIndex = 0; edgeIndex < Constraints.Count; edgeIndex++)
            {
                var edge = Constraints[edgeIndex];
                if (edge.Broken || edge.A != particleIndex && edge.B != particleIndex) continue;
                if (BreakBond(edgeIndex, false)) severedOrphans++;
            }
        }

        var adjacency = new List<(int Neighbor, int Edge)>[Particles.Length];
        for (var i = 0; i < adjacency.Length; i++) adjacency[i] = new List<(int, int)>(6);
        for (var edgeIndex = 0; edgeIndex < Constraints.Count; edgeIndex++)
        {
            var edge = Constraints[edgeIndex];
            if (edge.Broken) continue;
            adjacency[edge.A].Add((edge.B, edgeIndex));
            adjacency[edge.B].Add((edge.A, edgeIndex));
        }

        var discovery = new int[Particles.Length];
        Array.Fill(discovery, -1);
        var low = new int[Particles.Length];
        var time = 0;
        var bridges = new List<int>();

        void FindBridges(int particle, int parentEdge)
        {
            discovery[particle] = low[particle] = time++;
            foreach (var connection in adjacency[particle])
            {
                if (connection.Edge == parentEdge) continue;
                if (discovery[connection.Neighbor] < 0)
                {
                    FindBridges(connection.Neighbor, connection.Edge);
                    low[particle] = Math.Min(low[particle], low[connection.Neighbor]);
                    if (low[connection.Neighbor] > discovery[particle]) bridges.Add(connection.Edge);
                }
                else
                {
                    low[particle] = Math.Min(low[particle], discovery[connection.Neighbor]);
                }
            }
        }

        for (var i = 0; i < Particles.Length; i++)
            if (discovery[i] < 0) FindBridges(i, -1);

        foreach (var bridge in bridges) BreakBond(bridge, true);
        if (bridges.Count > 0 || severedOrphans > 0) RefreshSurfaceMask();
    }

    public void MarkDetachedDebris(float dt)
    {
        IsDetachedDebris = true;
        Wake();
        // Preserve the exact cut-time material while airborne. Large coherent
        // chunks need enough iterations to remain that piece instead of folding
        // into lattice triangles before their first impact.
        Mode = Particles.Length >= 4 ? SimulationMode.ShapeProxy : SimulationMode.LooseFragment;

        var averageVelocity = AverageVelocity(dt);
        var averageSpeed = averageVelocity.Length();
        if (averageSpeed > 1200f) averageVelocity *= 1200f / averageSpeed;
        for (var i = 0; i < Particles.Length; i++)
        {
            var velocity = (Particles[i].Position - Particles[i].PreviousPosition) / dt;
            var relative = velocity - averageVelocity;
            var relativeSpeed = relative.Length();
            if (relativeSpeed > 140f) relative *= 140f / relativeSpeed;
            Particles[i].PreviousPosition = Particles[i].Position - (averageVelocity + relative) * dt;
        }

        for (var i = 0; i < Constraints.Count; i++)
        {
            var bond = Constraints[i];
            if (!bond.Broken)
                bond.RestLength = Vector2.Distance(Particles[bond.A].Position, Particles[bond.B].Position);
            bond.Lambda = 0f;
            Constraints[i] = bond;
        }
        for (var i = 0; i < AreaConstraints.Count; i++)
        {
            var area = AreaConstraints[i];
            if (!area.Broken)
                area.RestArea = AreaConstraint.SignedArea(
                    Particles[area.A].Position,
                    Particles[area.B].Position,
                    Particles[area.C].Position);
            area.Lambda = 0f;
            AreaConstraints[i] = area;
        }
        _restAreaTotal = AreaConstraints.Where(area => !area.Broken).Sum(area => MathF.Abs(area.RestArea));
    }

    public void BeginCrumbling()
    {
        if (IsCrumbling) return;
        IsCrumbling = true;
        Mode = PhysicalParticleCount >= 4 ? SimulationMode.ShapeProxy : SimulationMode.LooseFragment;
        Wake();
    }

    public float DistanceToPointSquared(Vector2 point)
    {
        var best = float.MaxValue;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            var distance = Vector2.Distance(Particles[i].Position, point) - Particles[i].Radius;
            best = MathF.Min(best, MathF.Max(0f, distance) * MathF.Max(0f, distance));
        }
        return best;
    }

    public bool IsSurfaceParticle(int particleIndex)
        => IsPhysicalParticle(particleIndex) && _surfaceMask[particleIndex];

    public bool HasGroundSupport
    {
        get
        {
            for (var i = 0; i < Particles.Length; i++)
                if (IsPhysicalParticle(i) && Particles[i].Supported) return true;
            return false;
        }
    }

    public bool IsConvertedParticle(int particleIndex)
        => _convertedMask[particleIndex];

    public bool IsReleasedParticle(int particleIndex)
        => _releasedMask[particleIndex];

    public bool IsPhysicalParticle(int particleIndex)
        => !_convertedMask[particleIndex] && !_releasedMask[particleIndex];

    public bool IsDamageAdjacentParticle(int particleIndex)
        => _damageMask[particleIndex];

    public bool TryProjectToCutSurface(Vector2 point, out Vector2 projected)
    {
        projected = point;
        var bestDistanceSq = float.MaxValue;
        var center = Center;
        var angle = CurrentShapeAngle(center);
        for (var i = 0; i < _cutSegments.Count; i++)
        {
            var worldStart = center + Rotate(_cutSegments[i].Start, angle);
            var worldEnd = center + Rotate(_cutSegments[i].End, angle);
            var direction = worldEnd - worldStart;
            var lengthSq = direction.LengthSquared();
            if (lengthSq < 0.0001f) continue;
            var t = Vector2.Dot(point - worldStart, direction) / lengthSq;
            if (t < 0f || t > 1f) continue;
            var candidate = worldStart + direction * t;
            var distanceSq = Vector2.DistanceSquared(point, candidate);
            if (distanceSq >= bestDistanceSq) continue;
            bestDistanceSq = distanceSq;
            projected = candidate;
        }
        // Only vertices in the immediate cut row may be flattened. The older
        // 1.5-cell reach could pull a neighboring outer-contour vertex all the
        // way to a segment endpoint, drawing a diagonal tail beyond the wound.
        return bestDistanceSq <= ParticleSpacing * ParticleSpacing * 1.1025f;
    }

    internal bool TryGetWoundBoundaryBinding(
        int particleA,
        int particleB,
        out WoundEdgeBinding binding)
    {
        binding = default;
        if (!_damageMask[particleA] && !_damageMask[particleB]) return false;

        var referenceA = _shapeReferencePositions[particleA] - _shapeReferenceCenter;
        var referenceB = _shapeReferencePositions[particleB] - _shapeReferenceCenter;
        var referenceEdge = referenceB - referenceA;
        if (referenceEdge.LengthSquared() < 0.0001f) return false;
        var referenceMidpoint = (referenceA + referenceB) * 0.5f;
        var bestDistanceSq = float.MaxValue;
        var bestSegment = -1;
        var bestTA = 0f;
        var bestTB = 0f;
        for (var i = 0; i < _cutSegments.Count; i++)
        {
            var segment = _cutSegments[i];
            var direction = segment.End - segment.Start;
            var lengthSq = direction.LengthSquared();
            if (lengthSq < ParticleSpacing * ParticleSpacing * 0.0625f) continue;
            var alignment = MathF.Abs(Vector2.Dot(
                Vector2.Normalize(referenceEdge),
                Vector2.Normalize(direction)));
            if (alignment < 0.34f) continue;

            var midpointT = Vector2.Dot(referenceMidpoint - segment.Start, direction) / lengthSq;
            if (midpointT < 0f || midpointT > 1f) continue;
            var midpointProjection = segment.Start + direction * midpointT;
            var distanceSq = Vector2.DistanceSquared(referenceMidpoint, midpointProjection);
            if (distanceSq > ParticleSpacing * ParticleSpacing * 1.21f || distanceSq >= bestDistanceSq) continue;
            var tA = Vector2.Dot(referenceA - segment.Start, direction) / lengthSq;
            var tB = Vector2.Dot(referenceB - segment.Start, direction) / lengthSq;
            if (tA < -0.05f || tA > 1.05f || tB < -0.05f || tB > 1.05f) continue;
            bestDistanceSq = distanceSq;
            bestSegment = i;
            bestTA = Math.Clamp(tA, 0f, 1f);
            bestTB = Math.Clamp(tB, 0f, 1f);
        }
        if (bestSegment < 0) return false;

        binding = new WoundEdgeBinding(bestSegment, bestTA, bestTB);
        return true;
    }

    internal CutSegment CurrentWorldCutSegment(int segmentIndex)
    {
        var center = Center;
        var angle = CurrentShapeAngle(center);
        var segment = _cutSegments[segmentIndex];
        return new CutSegment(
            center + Rotate(segment.Start, angle),
            center + Rotate(segment.End, angle));
    }

    private CutSegment ToLocalCutSegment(CutSegment worldSegment)
    {
        var center = Center;
        var inverseAngle = -CurrentShapeAngle(center);
        return new CutSegment(
            Rotate(worldSegment.Start - center, inverseAngle),
            Rotate(worldSegment.End - center, inverseAngle));
    }

    private void RecordCutSegment(CutSegment worldSegment)
    {
        var local = ToLocalCutSegment(worldSegment);
        var direction = local.End - local.Start;
        if (direction.LengthSquared() < 0.0001f) return;
        if (_cutSegments.Count > 0)
        {
            var previous = _cutSegments[^1];
            var previousDirection = previous.End - previous.Start;
            var contiguous = Vector2.DistanceSquared(previous.End, local.Start) <=
                             ParticleSpacing * ParticleSpacing * 0.64f;
            var aligned = previousDirection.LengthSquared() > 0.0001f &&
                          Vector2.Dot(Vector2.Normalize(previousDirection), Vector2.Normalize(direction)) >= 0.94f;
            if (contiguous && aligned)
            {
                _cutSegments[^1] = new CutSegment(previous.Start, local.End);
                return;
            }
        }
        _cutSegments.Add(local);
        if (_cutSegments.Count > 32) _cutSegments.RemoveAt(0);
    }

    private List<CutSegment> GetWorldCutSegments()
    {
        var center = Center;
        var angle = CurrentShapeAngle(center);
        return _cutSegments.Select(segment => new CutSegment(
            center + Rotate(segment.Start, angle),
            center + Rotate(segment.End, angle))).ToList();
    }

    internal IReadOnlyList<CutSegment> CurrentWorldCutSegments => GetWorldCutSegments();

    private IEnumerable<CutSegment> ClipCutSegmentsToComponent(IReadOnlyList<CutSegment> worldSegments)
    {
        var cohesiveParticle = new bool[Particles.Length];
        foreach (var area in AreaConstraints)
        {
            if (area.Broken) continue;
            cohesiveParticle[area.A] = true;
            cohesiveParticle[area.B] = true;
            cohesiveParticle[area.C] = true;
        }

        foreach (var segment in worldSegments)
        {
            var direction = segment.End - segment.Start;
            var lengthSq = direction.LengthSquared();
            if (lengthSq < 0.0001f) continue;
            var minimumT = float.MaxValue;
            var maximumT = float.MinValue;
            var supportingParticles = 0;
            for (var particleIndex = 0; particleIndex < Particles.Length; particleIndex++)
            {
                if (!_damageMask[particleIndex] || !cohesiveParticle[particleIndex]) continue;
                var position = Particles[particleIndex].Position;
                var t = Math.Clamp(Vector2.Dot(position - segment.Start, direction) / lengthSq, 0f, 1f);
                var projected = segment.Start + direction * t;
                if (Vector2.DistanceSquared(position, projected) > ParticleSpacing * ParticleSpacing * 1.1025f) continue;
                minimumT = MathF.Min(minimumT, t);
                maximumT = MathF.Max(maximumT, t);
                supportingParticles++;
            }
            if (supportingParticles < 2 || minimumT == float.MaxValue) continue;
            if ((maximumT - minimumT) * MathF.Sqrt(lengthSq) < ParticleSpacing * 0.25f) continue;
            yield return new CutSegment(
                segment.Start + direction * minimumT,
                segment.Start + direction * maximumT);
        }
    }

    private float CurrentShapeAngle(Vector2 currentCenter)
    {
        var dot = 0f;
        var cross = 0f;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            var reference = _shapeReferencePositions[i] - _shapeReferenceCenter;
            var current = Particles[i].Position - currentCenter;
            dot += Vector2.Dot(reference, current);
            cross += reference.X * current.Y - reference.Y * current.X;
        }
        return MathF.Atan2(cross, dot);
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(
            vector.X * cosine - vector.Y * sine,
            vector.X * sine + vector.Y * cosine);
    }

    public void MarkParticleConverted(int particleIndex)
    {
        if ((uint)particleIndex >= (uint)Particles.Length || _convertedMask[particleIndex]) return;
        _convertedMask[particleIndex] = true;
        _activeParticleCount--;
        if (!_releasedMask[particleIndex]) _physicalParticleCount--;
        TopologyRevision++;
        Particles[particleIndex].InverseMass = 0f;
        Particles[particleIndex].PreviousPosition = Particles[particleIndex].Position;
        for (var i = 0; i < Constraints.Count; i++)
        {
            var bond = Constraints[i];
            if (bond.A == particleIndex || bond.B == particleIndex) bond.Broken = true;
            Constraints[i] = bond;
        }
        for (var i = 0; i < AreaConstraints.Count; i++)
        {
            var area = AreaConstraints[i];
            if (area.A == particleIndex || area.B == particleIndex || area.C == particleIndex) area.Broken = true;
            AreaConstraints[i] = area;
        }
        ReleaseOrphanedErosionParticles();
        RefreshSurfaceMask();
        if (IsCrumbling && PhysicalParticleCount <= 3) Mode = SimulationMode.LooseFragment;
    }

    private void ReleaseOrphanedErosionParticles()
    {
        // Normal cuts are reconciled by the topology splitter. This cleanup is
        // specifically for progressive chunk erosion, where removing one cell
        // can otherwise leave a bonded-but-cell-less chain hanging in space.
        if (!IsDetachedDebris || !IsCrumbling || _physicalParticleCount == 0) return;

        var belongsToIntactCell = new bool[Particles.Length];
        for (var i = 0; i < AreaConstraints.Count; i++)
        {
            var area = AreaConstraints[i];
            if (area.Broken || !IsPhysicalParticle(area.A) ||
                !IsPhysicalParticle(area.B) || !IsPhysicalParticle(area.C)) continue;
            belongsToIntactCell[area.A] = true;
            belongsToIntactCell[area.B] = true;
            belongsToIntactCell[area.C] = true;
        }

        var releasedAny = false;
        for (var particleIndex = 0; particleIndex < Particles.Length; particleIndex++)
        {
            if (!IsPhysicalParticle(particleIndex) || belongsToIntactCell[particleIndex]) continue;
            _releasedMask[particleIndex] = true;
            _physicalParticleCount--;
            Particles[particleIndex].InverseMass = 0f;
            Particles[particleIndex].PreviousPosition = Particles[particleIndex].Position;
            releasedAny = true;

            for (var i = 0; i < Constraints.Count; i++)
            {
                var bond = Constraints[i];
                if (bond.A == particleIndex || bond.B == particleIndex) bond.Broken = true;
                Constraints[i] = bond;
            }
            for (var i = 0; i < AreaConstraints.Count; i++)
            {
                var area = AreaConstraints[i];
                if (area.A == particleIndex || area.B == particleIndex || area.C == particleIndex) area.Broken = true;
                AreaConstraints[i] = area;
            }
        }

        if (releasedAny) TopologyRevision++;
    }

    private Vector2 ClosestSurfacePosition(Vector2 point)
    {
        var best = point;
        var bestDistance = float.MaxValue;
        var center = Center;
        var outerRadius = 0f;
        for (var i = 0; i < Particles.Length; i++)
            outerRadius = MathF.Max(outerRadius, Vector2.Distance(center, Particles[i].Position));
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!_surfaceMask[i]) continue;
            if (Vector2.Distance(center, Particles[i].Position) < outerRadius * 0.68f) continue;
            var distance = Vector2.DistanceSquared(Particles[i].Position, point);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = Particles[i].Position;
        }
        return best;
    }

    private bool ContainsVisibleTissue(Vector2 point)
    {
        // Match the rendered skin closely enough that clicking a visible edge counts,
        // without allowing empty-space clicks to magnetize onto the nearest blob.
        var skin = ParticleSpacing * 0.34f;
        for (var i = 0; i < Particles.Length; i++)
        {
            var radius = Particles[i].Radius + skin;
            if (Vector2.DistanceSquared(Particles[i].Position, point) <= radius * radius) return true;
        }
        return false;
    }

    public bool ContainsVisiblePoint(Vector2 point) => ContainsVisibleTissue(point);

    internal void BeginImpactStep()
    {
        _pressureDampedThisStep = false;
        _breakupImpactPoints.Clear();
        LastImpact = 0f;
        LastTerrainImpact = 0f;
        LastBreakupImpact = 0f;
    }

    internal void BeginPressureContactPass() => _pressureDampedThisStep = false;

    internal void RecordBreakupImpact(Vector2 point, float impact)
    {
        if (impact < 18f) return;
        if (impact >= LastBreakupImpact)
        {
            LastBreakupImpact = impact;
            LastBreakupImpactPoint = point;
        }
        var minimumDistanceSq = ParticleSpacing * ParticleSpacing * 0.36f;
        for (var i = 0; i < _breakupImpactPoints.Count; i++)
            if (Vector2.DistanceSquared(_breakupImpactPoints[i], point) < minimumDistanceSq) return;
        if (_breakupImpactPoints.Count < 8) _breakupImpactPoints.Add(point);
    }

    private void RefreshSurfaceMask()
    {
        for (var i = 0; i < _surfaceMask.Length; i++) RefreshSurfaceParticle(i);
    }

    private void RefreshSurfaceParticle(int particleIndex)
    {
        var intactNeighbors = 0;
        for (var i = 0; i < Constraints.Count; i++)
        {
            var bond = Constraints[i];
            if (bond.Broken) continue;
            if (bond.A == particleIndex || bond.B == particleIndex) intactNeighbors++;
        }
        _surfaceMask[particleIndex] = intactNeighbors < 6;
    }

    public Vector2 AverageVelocity(float dt)
    {
        if (IsSleeping) return Vector2.Zero;
        var velocity = Vector2.Zero;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            velocity += (Particles[i].Position - Particles[i].PreviousPosition) / dt;
        }
        return velocity / Math.Max(1, PhysicalParticleCount);
    }

    public void ApplyPositionCorrection(Vector2 correction)
    {
        if (IsSleeping) return;
        for (var i = 0; i < Particles.Length; i++) Particles[i].Position += correction;
    }

    public void ApplyTranslation(Vector2 correction, bool preserveVelocity)
    {
        if (IsSleeping) return;
        for (var i = 0; i < Particles.Length; i++)
        {
            Particles[i].Position += correction;
            if (preserveVelocity) Particles[i].PreviousPosition += correction;
        }
    }

    public void ApplyRotation(Vector2 center, float radians, bool preserveVelocity)
    {
        if (IsSleeping || MathF.Abs(radians) < 0.00001f) return;
        var cosine = MathF.Cos(radians);
        var sine = MathF.Sin(radians);
        for (var i = 0; i < Particles.Length; i++)
        {
            var relative = Particles[i].Position - center;
            Particles[i].Position = center + new Vector2(
                relative.X * cosine - relative.Y * sine,
                relative.X * sine + relative.Y * cosine);
            if (!preserveVelocity) continue;
            relative = Particles[i].PreviousPosition - center;
            Particles[i].PreviousPosition = center + new Vector2(
                relative.X * cosine - relative.Y * sine,
                relative.X * sine + relative.Y * cosine);
        }
    }

    public void YieldGrabTargetToSeparation(Vector2 correction)
    {
        if (!IsGrabbed) return;
        // Follow most emergency whole-body separation so the distributed grab
        // does not immediately pull the hull back through its contact partner.
        // The remaining fraction plus local particle contacts still supplies
        // visible pressure and soft-body deformation.
        GrabTarget += correction * 0.20f;
    }

    public void EnforceArenaBounds(float left, float right, float top, float bottom, float dt)
    {
        if (PhysicalParticleCount == 0) return;
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            minX = MathF.Min(minX, Particles[i].Position.X - Particles[i].Radius);
            maxX = MathF.Max(maxX, Particles[i].Position.X + Particles[i].Radius);
            minY = MathF.Min(minY, Particles[i].Position.Y - Particles[i].Radius);
            maxY = MathF.Max(maxY, Particles[i].Position.Y + Particles[i].Radius);
        }

        var correction = Vector2.Zero;
        if (minX < left) correction.X = left - minX;
        else if (maxX > right) correction.X = right - maxX;
        if (minY < top) correction.Y = top - minY;
        else if (maxY > bottom) correction.Y = bottom - maxY;
        if (correction == Vector2.Zero) return;

        Wake();
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            var velocity = (Particles[i].Position - Particles[i].PreviousPosition) / dt;
            Particles[i].Position += correction;
            Particles[i].PreviousPosition += correction;
            if ((correction.X > 0f && velocity.X < 0f) || (correction.X < 0f && velocity.X > 0f)) velocity.X = 0f;
            if ((correction.Y > 0f && velocity.Y < 0f) || (correction.Y < 0f && velocity.Y > 0f)) velocity.Y = 0f;
            Particles[i].PreviousPosition = Particles[i].Position - velocity * dt;
            var touchesCorrectedSide =
                correction.X > 0f && Particles[i].Position.X - Particles[i].Radius <= left + 0.5f ||
                correction.X < 0f && Particles[i].Position.X + Particles[i].Radius >= right - 0.5f ||
                correction.Y > 0f && Particles[i].Position.Y - Particles[i].Radius <= top + 0.5f ||
                correction.Y < 0f && Particles[i].Position.Y + Particles[i].Radius >= bottom - 0.5f;
            if (touchesCorrectedSide)
            {
                Particles[i].Contacting = true;
                Particles[i].ContactMemory = 6;
            }
        }
    }

    public void ApplyPressureDamping(Vector2 pressureDirection, float dt)
    {
        if (pressureDirection.LengthSquared() < 0.001f) return;
        _pressureDirection = Vector2.Normalize(pressureDirection);
        _pressureDampingTime = 0.04f;
        // One body can have many surface-particle contacts and the contact pass
        // runs once per solver iteration. Multiplying this damping dozens of
        // times made the passive blob behave like a rigid dead stop and erased
        // the elastic motion that should emerge when pressure is released.
        if (_pressureDampedThisStep) return;
        _pressureDampedThisStep = true;
        DampPressureVelocity(_pressureDirection, dt);
    }

    public void ApplyResidualPressureDamping(float dt)
    {
        if (_pressureDampingTime <= 0f || _pressureDampedThisStep || IsGrabbed || IsSleeping) return;
        _pressureDampingTime = MathF.Max(0f, _pressureDampingTime - dt);
        DampPressureVelocity(_pressureDirection, dt);
    }

    private void DampPressureVelocity(Vector2 normal, float dt)
    {
        var centerVelocity = AverageVelocity(dt);
        var gravityVelocity = Vector2.Zero;
        if (_lastGravity.LengthSquared() > 0.001f)
        {
            var gravityDirection = Vector2.Normalize(_lastGravity);
            var fallingSpeed = MathF.Max(0f, Vector2.Dot(centerVelocity, gravityDirection));
            gravityVelocity = gravityDirection * fallingSpeed;
        }
        var contactVelocity = centerVelocity - gravityVelocity;
        var centerNormalSpeed = Vector2.Dot(contactVelocity, normal);
        var centerTangent = contactVelocity - normal * centerNormalSpeed;
        var dampedCenterNormal = Math.Clamp(centerNormalSpeed * 0.45f, -110f, 110f);
        var dampedTangent = centerTangent * 0.62f;
        var tangentSpeed = dampedTangent.Length();
        if (tangentSpeed > 110f) dampedTangent *= 110f / tangentSpeed;
        var dampedCenter = gravityVelocity + dampedTangent + normal * dampedCenterNormal;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            var velocity = (Particles[i].Position - Particles[i].PreviousPosition) / dt;
            var internalVelocity = velocity - centerVelocity;
            // Whole-body tangent (including gravity beside a held blob) survives;
            // only internal shear and center motion launched away are dissipated.
            var damped = dampedCenter + internalVelocity * 0.82f;
            Particles[i].PreviousPosition = Particles[i].Position - damped * dt;
        }
    }

    public void ApplyRestingViscosity(float dt)
    {
        if (IsSleeping || IsGrabbed || IsDetachedDebris) return;
        var supported = 0;
        for (var i = 0; i < Particles.Length; i++)
            if (Particles[i].Supported) supported++;
        if (supported == 0) return;

        var centerVelocity = AverageVelocity(dt);
        if (centerVelocity.LengthSquared() > 220f * 220f) return;
        var stepScale = dt / (1f / 120f);
        // Floor/body support should calm compression without behaving like glue.
        // Horizontal center motion is left to ordinary material damping so blobs
        // can roll, slide, and search for lower gaps under their own momentum.
        var horizontalDamping = 1f;
        var verticalDamping = MathF.Pow(0.94f, stepScale);
        var internalDamping = MathF.Pow(0.60f, stepScale);
        var dampedCenter = new Vector2(
            centerVelocity.X * horizontalDamping,
            centerVelocity.Y * verticalDamping);

        for (var i = 0; i < Particles.Length; i++)
        {
            var velocity = (Particles[i].Position - Particles[i].PreviousPosition) / dt;
            var relative = velocity - centerVelocity;
            var damped = dampedCenter + relative * internalDamping;
            Particles[i].PreviousPosition = Particles[i].Position - damped * dt;
        }
    }

    public void ApplyDamagedShapeRecovery(float dt)
    {
        if (IsSleeping || IsGrabbed || !HasLocalDamage || PhysicalParticleCount < 4) return;
        var supported = 0;
        var referenceCenter = Vector2.Zero;
        var currentCenter = Vector2.Zero;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            referenceCenter += _shapeReferencePositions[i];
            currentCenter += Particles[i].Position;
            if (Particles[i].Supported) supported++;
        }
        referenceCenter /= PhysicalParticleCount;
        currentCenter /= PhysicalParticleCount;

        // This is a weak cluster recovery, not a rigid constraint. It removes the
        // unsupported scale/breathing mode while keeping translation, rotation,
        // contact squish, grabs, and intentional damage fully physical.
        var dot = 0f;
        var cross = 0f;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            var reference = _shapeReferencePositions[i] - referenceCenter;
            var current = Particles[i].Position - currentCenter;
            dot += Vector2.Dot(reference, current);
            cross += reference.X * current.Y - reference.Y * current.X;
        }
        var angle = MathF.Atan2(cross, dot);
        var baseStrength = IsDetachedDebris
            ? IsCrumbling ? 0.006f : 0.010f
            : supported > 0 ? 0.012f : 0.003f;
        var strength = 1f - MathF.Pow(1f - baseStrength, dt / (1f / 120f));
        var maximumCorrection = ParticleSpacing * 0.045f;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            var target = currentCenter + Rotate(_shapeReferencePositions[i] - referenceCenter, angle);
            var correction = (target - Particles[i].Position) * strength;
            var correctionLength = correction.Length();
            if (correctionLength > maximumCorrection)
                correction *= maximumCorrection / correctionLength;
            Particles[i].Position += correction;
            Particles[i].PreviousPosition += correction;
        }
    }

    public void MarkSupportedByBody(Vector2 supportDirection)
    {
        var center = Center;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (Vector2.Dot(Particles[i].Position - center, supportDirection) < Radius * 0.18f) continue;
            Particles[i].Contacting = true;
            Particles[i].ContactMemory = 6;
            if (supportDirection.Y >= 0.45f)
            {
                Particles[i].Supported = true;
                Particles[i].SupportMemory = 10;
            }
        }
    }

    public void UpdateSleep(float dt)
    {
        if (IsSleeping || IsGrabbed) return;
        var inverseDt = 1f / dt;
        var totalSpeedSq = 0f;
        var supported = 0;
        for (var i = 0; i < Particles.Length; i++)
        {
            totalSpeedSq += Particles[i].Velocity(inverseDt).LengthSquared();
            if (!Particles[i].Supported) continue;
            supported++;
        }

        var center = Center;
        var centerSpeedSq = Vector2.DistanceSquared(center, _lastSleepCenter) * inverseDt * inverseDt;
        _lastSleepCenter = center;
        var averageSpeedSq = totalSpeedSq / Math.Max(1, Particles.Length);
        LastAverageSpeed = MathF.Sqrt(averageSpeedSq);
        LastCenterSpeed = MathF.Sqrt(centerSpeedSq);
        LastSupportedParticles = supported;

        var requiredSupport = Math.Max(2, (int)(MathF.Sqrt(Particles.Length) * 0.5f));
        var hasStableSupport = supported >= requiredSupport;
        if (hasStableSupport &&
            averageSpeedSq < SleepSpeed * SleepSpeed &&
            centerSpeedSq < CenterSleepSpeed * CenterSleepSpeed)
        {
            _quietTime += dt;
            if (_quietTime >= SleepDelay) Sleep();
        }
        else
        {
            _quietTime = 0f;
        }
    }

    public void Wake()
    {
        _lastSleepCenter = Center;
        if (IsSleeping)
        {
            IsSleeping = false;
            Mode = _modeBeforeSleep == SimulationMode.Asleep ? SimulationMode.ReducedTissue : _modeBeforeSleep;
            for (var i = 0; i < Particles.Length; i++)
                Particles[i].PreviousPosition = Particles[i].Position;
        }
        _quietTime = 0f;
    }

    public void WakeIfFast(float speed)
    {
        if (speed >= WakeSpeed) Wake();
    }

    public void Sleep()
    {
        if (!IsSleeping) _modeBeforeSleep = Mode;
        IsSleeping = true;
        Mode = SimulationMode.Asleep;
        _quietTime = 0f;
        _lastSleepCenter = Center;
        for (var i = 0; i < Particles.Length; i++)
        {
            Particles[i].PreviousPosition = Particles[i].Position;
            Particles[i].Acceleration = Vector2.Zero;
        }
    }

    public float AreaRatio
    {
        get
        {
            if (_restAreaTotal <= 0.001f) return 1f;
            var area = 0f;
            foreach (var constraint in AreaConstraints)
            {
                if (constraint.Broken) continue;
                area += MathF.Abs(AreaConstraint.SignedArea(
                    Particles[constraint.A].Position,
                    Particles[constraint.B].Position,
                    Particles[constraint.C].Position));
            }
            return area / _restAreaTotal;
        }
    }

    private static float SegmentDistance(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        if (SegmentsIntersect(a0, a1, b0, b1)) return 0f;
        return MathF.Min(
            MathF.Min(PointSegmentDistance(a0, b0, b1), PointSegmentDistance(a1, b0, b1)),
            MathF.Min(PointSegmentDistance(b0, a0, a1), PointSegmentDistance(b1, a0, a1)));
    }

    private static float PointSegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        var lengthSq = segment.LengthSquared();
        if (lengthSq < 0.0001f) return Vector2.Distance(point, a);
        var t = Math.Clamp(Vector2.Dot(point - a, segment) / lengthSq, 0f, 1f);
        return Vector2.Distance(point, a + segment * t);
    }

    public readonly record struct CutSegment(Vector2 Start, Vector2 End);
    internal readonly record struct WoundEdgeBinding(int SegmentIndex, float TA, float TB);

    private static long EdgeKey(int a, int b)
    {
        if (a > b) (a, b) = (b, a);
        return ((long)a << 32) | (uint)b;
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Cross(Vector2 p, Vector2 q, Vector2 r)
            => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);

        static bool OnSegment(Vector2 p, Vector2 q, Vector2 r)
            => q.X >= MathF.Min(p.X, r.X) - 0.0001f &&
               q.X <= MathF.Max(p.X, r.X) + 0.0001f &&
               q.Y >= MathF.Min(p.Y, r.Y) - 0.0001f &&
               q.Y <= MathF.Max(p.Y, r.Y) + 0.0001f;

        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        if (((abC > 0f && abD < 0f) || (abC < 0f && abD > 0f)) &&
            ((cdA > 0f && cdB < 0f) || (cdA < 0f && cdB > 0f))) return true;
        if (MathF.Abs(abC) <= 0.0001f && OnSegment(a, c, b)) return true;
        if (MathF.Abs(abD) <= 0.0001f && OnSegment(a, d, b)) return true;
        if (MathF.Abs(cdA) <= 0.0001f && OnSegment(c, a, d)) return true;
        return MathF.Abs(cdB) <= 0.0001f && OnSegment(c, b, d);
    }
}
