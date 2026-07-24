using System.Numerics;

namespace BlobForge.Physics;

public readonly record struct BlobBloodStain(
    int ParticleIndex,
    Vector2 LocalOffset,
    Vector2 ReferenceDirection,
    float Amount,
    float Wetness,
    byte Variation);

public enum BlobFaceExpression : byte
{
    Neutral,
    Blink,
    Hurt
}

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
    private bool _hasDamageMask;
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
    private readonly List<BlobBloodStain> _bloodStains = new(32);
    private byte _bloodStainSerial;
    private float _faceClock;
    private float _nextBlinkTime;
    private float _blinkRemaining;
    private float _hurtRemaining;
    private float _hitFlashRemaining;
    private uint _blinkSequence;
    private uint _personalitySequence;
    private float _personalityHopTimer;
    private bool _pendingExplosionFragmentMotion;
    private Vector2 _pendingExplosionCenter;
    private float _pendingExplosionMinimumSpeed;
    private float _pendingExplosionMaximumSpeed;
    private float _pendingExplosionDt;
    private uint _explosionMotionSerial;
    private float _looseFragmentAngle;
    private float _looseFragmentAngularVelocity;
    private Vector2[] _frozenReferenceOffsets = Array.Empty<Vector2>();
    private float _frozenCollisionPadding;
    private const int MaximumBloodStains = 36;

    public SoftBody(Vector2 center, float radius, int targetParticleCount = 61)
    {
        Id = Interlocked.Increment(ref _nextId);
        ParentId = Id;
        InitializeFaceAnimation();
        InitializePersonality();
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
        InitializeFaceAnimation();
        InitializePersonality();
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
        _hasDamageMask = Array.IndexOf(damageMask, true) >= 0;
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
    public float FragmentVisualRotation => PhysicalParticleCount < 2
        ? _looseFragmentAngle
        : VisualRotation;
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
    public bool IsFrozen { get; private set; }
    public float FrozenCollisionPadding => _frozenCollisionPadding;
    public bool PersonalityCanHop { get; private set; }
    public float PersonalityJumpiness { get; private set; }
    public float PersonalityHopSpeed { get; private set; }
    public float NextPersonalityHopSeconds => _personalityHopTimer;
    public int PersonalityHopCount { get; private set; }
    public float LastPersonalityHopSpeed { get; private set; }
    public bool LastPersonalityHopWasInTube { get; private set; }
    public bool HasLocalDamage => BrokenLinkCount > 0 || _hasDamageMask;
    public bool IsPickable => !IsDetachedDebris && Mode != SimulationMode.LooseFragment && Particles.Length >= 7;
    public int ActiveParticleCount => _activeParticleCount;
    public int PhysicalParticleCount => _physicalParticleCount;
    public IReadOnlyList<BlobBloodStain> BloodStains => _bloodStains;
    public BlobFaceExpression FaceExpression => IsFrozen
        ? BlobFaceExpression.Neutral
        : _hurtRemaining > 0f
        ? BlobFaceExpression.Hurt
        : _blinkRemaining > 0f ? BlobFaceExpression.Blink : BlobFaceExpression.Neutral;
    public float HitFlash01 => Math.Clamp(_hitFlashRemaining / 0.18f, 0f, 1f);

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

    private void InitializeFaceAnimation()
    {
        _blinkSequence = unchecked((uint)(ParentId * 747796405) ^ (uint)(Id * 2891336453L));
        _nextBlinkTime = 1.4f + NextFaceSample() * 3.1f;
    }

    private void InitializePersonality()
    {
        _personalitySequence = MixPersonalitySeed(
            unchecked((uint)ParentId * 0x9E3779B9u) ^ 0xA511E9B3u);
        // A meaningful quiet population keeps hopping readable as personality
        // instead of turning the entire conveyor into a synchronized trampoline.
        PersonalityCanHop = NextPersonalitySample() < 0.58f;
        if (!PersonalityCanHop)
        {
            PersonalityJumpiness = 0f;
            PersonalityHopSpeed = 0f;
            _personalityHopTimer = float.PositiveInfinity;
            return;
        }

        PersonalityJumpiness = 0.24f + NextPersonalitySample() * 0.76f;
        PersonalityHopSpeed = 190f + NextPersonalitySample() * 95f;
        _personalityHopTimer = 0.75f + NextPersonalitySample() * 4.25f;
    }

    internal bool TryApplyPersonalityHop(float dt, bool inTube, bool allowed = true)
    {
        if (dt <= 0f || !allowed || !PersonalityCanHop ||
            IsDetachedDebris || IsCrumbling || IsGrabbed || IsFrozen ||
            PhysicalParticleCount < 7)
            return false;

        _personalityHopTimer -= dt * (inTube ? 1.16f : 1f);
        if (_personalityHopTimer > 0f) return false;

        if (!inTube)
        {
            var supportedParticles = 0;
            for (var index = 0; index < Particles.Length; index++)
            {
                if (IsPhysicalParticle(index) && Particles[index].Supported)
                    supportedParticles++;
            }
            if (supportedParticles < 2) return false;
        }

        var eventVariation = 0.78f + NextPersonalitySample() * 0.44f;
        var hopSpeed = PersonalityHopSpeed * eventVariation * (inTube ? 0.42f : 1f);
        var horizontal = (NextPersonalitySample() * 2f - 1f) *
                         (inTube ? 18f : 26f);
        AddImpulse(new Vector2(horizontal, -hopSpeed), dt);
        PersonalityHopCount++;
        LastPersonalityHopSpeed = hopSpeed;
        LastPersonalityHopWasInTube = inTube;

        var intervalSample = NextPersonalitySample();
        var minimumInterval = inTube ? 1.65f : 3.4f;
        var maximumInterval = inTube ? 5.2f : 9.2f;
        var personalityScale = 1.12f - PersonalityJumpiness * 0.38f;
        _personalityHopTimer =
            (minimumInterval + (maximumInterval - minimumInterval) * intervalSample) *
            personalityScale;
        return true;
    }

    private float NextPersonalitySample()
    {
        _personalitySequence ^= _personalitySequence << 13;
        _personalitySequence ^= _personalitySequence >> 17;
        _personalitySequence ^= _personalitySequence << 5;
        return (_personalitySequence & 0x00FFFFFFu) / 16777216f;
    }

    private static uint MixPersonalitySeed(uint seed)
    {
        seed ^= seed >> 16;
        seed *= 0x7FEB352Du;
        seed ^= seed >> 15;
        seed *= 0x846CA68Bu;
        seed ^= seed >> 16;
        return seed == 0u ? 0x6D2B79F5u : seed;
    }

    internal void AdvanceFaceAnimation(float dt)
    {
        if (dt <= 0f) return;
        _hitFlashRemaining = MathF.Max(0f, _hitFlashRemaining - dt);
        if (IsFrozen)
        {
            _blinkRemaining = 0f;
            _hurtRemaining = 0f;
            return;
        }
        _faceClock += dt;
        if (_hurtRemaining > 0f)
        {
            _hurtRemaining = MathF.Max(0f, _hurtRemaining - dt);
            _blinkRemaining = 0f;
            if (_hurtRemaining <= 0f && _nextBlinkTime <= _faceClock)
                _nextBlinkTime = _faceClock + 1.1f + NextFaceSample() * 2.4f;
            return;
        }
        if (_blinkRemaining > 0f)
        {
            _blinkRemaining = MathF.Max(0f, _blinkRemaining - dt);
            return;
        }
        if (_faceClock < _nextBlinkTime) return;
        _blinkRemaining = 0.12f;
        _nextBlinkTime = _faceClock + 2.3f + NextFaceSample() * 3.4f;
    }

    public void SetFrozen(
        bool frozen,
        float collisionPadding = 7f,
        bool collisionRadiiAlreadyExpanded = false)
    {
        collisionPadding = Math.Clamp(collisionPadding, 0f, ParticleSpacing * 1.4f);
        if (!frozen)
        {
            if (!IsFrozen) return;
            for (var index = 0; index < Particles.Length; index++)
            {
                if (!IsPhysicalParticle(index)) continue;
                Particles[index].Radius = MathF.Max(
                    1f,
                    Particles[index].Radius - _frozenCollisionPadding);
            }
            IsFrozen = false;
            _frozenCollisionPadding = 0f;
            _frozenReferenceOffsets = Array.Empty<Vector2>();
            Wake();
            return;
        }

        var radiusAdjustment = IsFrozen
            ? collisionPadding - _frozenCollisionPadding
            : collisionRadiiAlreadyExpanded ? 0f : collisionPadding;
        if (!IsFrozen && !collisionRadiiAlreadyExpanded && collisionPadding > 0.001f)
            ShiftAwayFromFreezeSupport(collisionPadding);
        if (MathF.Abs(radiusAdjustment) > 0.001f)
        {
            for (var index = 0; index < Particles.Length; index++)
            {
                if (!IsPhysicalParticle(index)) continue;
                Particles[index].Radius = MathF.Max(
                    1f,
                    Particles[index].Radius + radiusAdjustment);
            }
        }

        IsFrozen = true;
        _frozenCollisionPadding = collisionPadding;
        _blinkRemaining = 0f;
        _hurtRemaining = 0f;
        var center = Center;
        _frozenReferenceOffsets = new Vector2[Particles.Length];
        for (var index = 0; index < Particles.Length; index++)
            _frozenReferenceOffsets[index] = Particles[index].Position - center;
        Wake();
    }

    private void ShiftAwayFromFreezeSupport(float collisionPadding)
    {
        var center = Center;
        var bottomContacts = 0;
        var topContacts = 0;
        var leftContacts = 0;
        var rightContacts = 0;
        for (var index = 0; index < Particles.Length; index++)
        {
            if (!IsPhysicalParticle(index) || !Particles[index].Contacting) continue;
            var offset = Particles[index].Position - center;
            if (MathF.Abs(offset.Y) >= MathF.Abs(offset.X))
            {
                if (offset.Y >= 0f) bottomContacts++;
                else topContacts++;
            }
            else if (offset.X >= 0f)
            {
                rightContacts++;
            }
            else
            {
                leftContacts++;
            }
        }

        var maximumContacts = Math.Max(
            Math.Max(bottomContacts, topContacts),
            Math.Max(leftContacts, rightContacts));
        if (maximumContacts <= 0) return;

        // Move the old body away from its dominant supporting surface before
        // expanding the particle radii. Translating both Verlet positions and
        // clearing their relative velocity makes this a quiet accommodation for
        // the new ice thickness, not an impact that launches or shatters the blob.
        var shift = maximumContacts == bottomContacts
            ? -Vector2.UnitY * collisionPadding
            : maximumContacts == topContacts
                ? Vector2.UnitY * collisionPadding
                : maximumContacts == leftContacts
                    ? Vector2.UnitX * collisionPadding
                    : -Vector2.UnitX * collisionPadding;
        for (var index = 0; index < Particles.Length; index++)
        {
            Particles[index].Position += shift;
            Particles[index].PreviousPosition = Particles[index].Position;
        }
    }

    private void ShowHurtExpression(float intensity)
    {
        if (intensity <= 0f || IsDetachedDebris) return;
        var duration = 0.30f + Math.Clamp(intensity * 0.035f, 0f, 0.14f);
        _hurtRemaining = MathF.Max(_hurtRemaining, duration);
        _blinkRemaining = 0f;
    }

    public void RegisterHitReaction(float intensity = 1f, float flashSeconds = 0.18f)
    {
        ShowHurtExpression(MathF.Max(0.1f, intensity));
        _hitFlashRemaining = MathF.Max(
            _hitFlashRemaining,
            Math.Clamp(flashSeconds, 0.04f, 0.30f));
        Wake();
    }

    private float NextFaceSample()
    {
        _blinkSequence ^= _blinkSequence << 13;
        _blinkSequence ^= _blinkSequence >> 17;
        _blinkSequence ^= _blinkSequence << 5;
        return (_blinkSequence & 0x00FFFFFFu) / 16777216f;
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
        for (var i = 0; i < _bloodStains.Count; i++)
        {
            var stain = _bloodStains[i];
            _bloodStains[i] = stain with { Wetness = MathF.Max(0f, stain.Wetness - dt * 0.045f) };
        }
        if (IsSleeping) return;
        var settings = ModeSettings.For(Mode);
        if (MathF.Abs(_looseFragmentAngularVelocity) > 0.001f)
        {
            _looseFragmentAngle = MathF.IEEERemainder(
                _looseFragmentAngle + _looseFragmentAngularVelocity * dt,
                MathF.Tau);
            _looseFragmentAngularVelocity *= settings.LinearDamping;
        }
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

    public void DepositBloodStain(int particleIndex, Vector2 worldContact, float amount)
    {
        if ((uint)particleIndex >= (uint)Particles.Length || !IsPhysicalParticle(particleIndex)) return;
        var radius = MathF.Max(2f, Particles[particleIndex].Radius * 0.92f);
        var localOffset = worldContact - Particles[particleIndex].Position;
        var offsetLength = localOffset.Length();
        if (offsetLength > radius) localOffset *= radius / offsetLength;
        var referenceDirection = Particles[particleIndex].Position - Center;
        if (referenceDirection.LengthSquared() < 0.0001f) referenceDirection = -Vector2.UnitY;
        else referenceDirection = Vector2.Normalize(referenceDirection);

        var searchStart = Math.Max(0, _bloodStains.Count - 16);
        for (var i = _bloodStains.Count - 1; i >= searchStart; i--)
        {
            var existing = _bloodStains[i];
            if (existing.ParticleIndex != particleIndex ||
                Vector2.DistanceSquared(BloodStainWorldPosition(existing), worldContact) > 24f) continue;
            var blendedWorld = Vector2.Lerp(BloodStainWorldPosition(existing), worldContact, 0.25f);
            _bloodStains[i] = existing with
            {
                LocalOffset = blendedWorld - Particles[particleIndex].Position,
                ReferenceDirection = referenceDirection,
                Amount = MathF.Min(1f, existing.Amount + amount * 0.55f),
                Wetness = 1f
            };
            return;
        }

        var mark = new BlobBloodStain(
            particleIndex,
            localOffset,
            referenceDirection,
            Math.Clamp(amount, 0.06f, 1f),
            1f,
            _bloodStainSerial++);
        if (_bloodStains.Count < MaximumBloodStains)
        {
            _bloodStains.Add(mark);
            return;
        }

        var weakest = 0;
        for (var i = 1; i < _bloodStains.Count; i++)
            if (_bloodStains[i].Amount < _bloodStains[weakest].Amount) weakest = i;
        _bloodStains[weakest] = mark;
    }

    public Vector2 BloodStainWorldPosition(BlobBloodStain stain)
    {
        if ((uint)stain.ParticleIndex >= (uint)Particles.Length) return Center;
        var currentDirection = Particles[stain.ParticleIndex].Position - Center;
        if (currentDirection.LengthSquared() < 0.0001f || stain.ReferenceDirection.LengthSquared() < 0.0001f)
            return Particles[stain.ParticleIndex].Position + stain.LocalOffset;
        currentDirection = Vector2.Normalize(currentDirection);
        var referenceDirection = Vector2.Normalize(stain.ReferenceDirection);
        var cosine = Math.Clamp(Vector2.Dot(referenceDirection, currentDirection), -1f, 1f);
        var sine = referenceDirection.X * currentDirection.Y - referenceDirection.Y * currentDirection.X;
        var rotatedOffset = new Vector2(
            stain.LocalOffset.X * cosine - stain.LocalOffset.Y * sine,
            stain.LocalOffset.X * sine + stain.LocalOffset.Y * cosine);
        return Particles[stain.ParticleIndex].Position + rotatedOffset;
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
        if (IsFrozen) SolveFrozenShape();
    }

    private void SolveFrozenShape()
    {
        if (_frozenReferenceOffsets.Length != Particles.Length ||
            PhysicalParticleCount <= 1)
            return;

        var center = Center;
        var dot = 0f;
        var cross = 0f;
        for (var index = 0; index < Particles.Length; index++)
        {
            if (!IsPhysicalParticle(index)) continue;
            var reference = _frozenReferenceOffsets[index];
            var current = Particles[index].Position - center;
            dot += Vector2.Dot(reference, current);
            cross += reference.X * current.Y - reference.Y * current.X;
        }
        var angle = MathF.Atan2(cross, dot);
        for (var index = 0; index < Particles.Length; index++)
        {
            if (!IsPhysicalParticle(index) || Particles[index].InverseMass <= 0f) continue;
            var target = center + Rotate(_frozenReferenceOffsets[index], angle);
            Particles[index].Position =
                Vector2.Lerp(Particles[index].Position, target, 0.86f);
        }
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

    public void AddLocalizedImpulse(Vector2 point, float radius, Vector2 impulse, float dt)
    {
        if (radius <= 0f || impulse.LengthSquared() <= 0.0001f) return;
        Wake();
        var radiusSquared = radius * radius;
        for (var i = 0; i < Particles.Length; i++)
        {
            if (!IsPhysicalParticle(i)) continue;
            var distanceSquared = Vector2.DistanceSquared(Particles[i].Position, point);
            if (distanceSquared >= radiusSquared) continue;
            var distance = MathF.Sqrt(distanceSquared);
            var normalized = 1f - distance / radius;
            var weight = normalized * normalized;
            Particles[i].PreviousPosition -= impulse * (dt * weight);
        }
    }

    public void AddRadialImpulse(Vector2 center, float radius, float strength, float dt)
    {
        if (radius <= 0f || strength <= 0f) return;
        Wake();
        var radiusSquared = radius * radius;
        for (var index = 0; index < Particles.Length; index++)
        {
            if (!IsPhysicalParticle(index)) continue;
            var delta = Particles[index].Position - center;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared >= radiusSquared) continue;
            var distance = MathF.Sqrt(distanceSquared);
            var direction = distance > 0.001f
                ? delta / distance
                : new Vector2(index % 2 == 0 ? -1f : 1f, -1f);
            var falloff = 1f - distance / radius;
            Particles[index].PreviousPosition -=
                direction * (strength * falloff * falloff * dt);
        }
    }

    public void AddRadialExplosion(
        Vector2 center,
        float minimumSpeed,
        float maximumSpeed,
        float dt)
    {
        if (dt <= 0f || maximumSpeed <= 0f) return;
        minimumSpeed = Math.Clamp(minimumSpeed, 0f, maximumSpeed);
        Wake();
        _pendingExplosionFragmentMotion = true;
        _pendingExplosionCenter = center;
        _pendingExplosionMinimumSpeed = minimumSpeed;
        _pendingExplosionMaximumSpeed = maximumSpeed;
        _pendingExplosionDt = dt;
        _explosionMotionSerial++;
        var inverseDt = 1f / MathF.Max(dt, 0.0001f);
        var radius = MathF.Max(Radius, 1f);
        for (var index = 0; index < Particles.Length; index++)
        {
            if (!IsPhysicalParticle(index)) continue;
            ref var particle = ref Particles[index];
            var delta = particle.Position - center;
            var distance = delta.Length();
            var direction = distance > 0.001f
                ? delta / distance
                : Vector2.Normalize(new Vector2(
                    (index & 1) == 0 ? -1f : 1f,
                    (index & 2) == 0 ? -1f : 1f));
            var normalizedDistance = Math.Clamp(distance / radius, 0f, 1f);
            var targetSpeed = minimumSpeed +
                              (maximumSpeed - minimumSpeed) *
                              (0.82f + normalizedDistance * 0.18f);
            var velocity = (particle.Position - particle.PreviousPosition) * inverseDt;
            var outwardSpeed = Vector2.Dot(velocity, direction);
            if (outwardSpeed < targetSpeed)
                velocity += direction * (targetSpeed - outwardSpeed);
            particle.PreviousPosition = particle.Position - velocity * dt;
        }
    }

    public void AddAngularImpulse(float angularVelocity, float dt)
    {
        if (dt <= 0f || MathF.Abs(angularVelocity) < 0.001f ||
            PhysicalParticleCount <= 0) return;
        Wake();
        if (PhysicalParticleCount < 2)
        {
            _looseFragmentAngularVelocity += angularVelocity;
            return;
        }
        var center = Center;
        for (var index = 0; index < Particles.Length; index++)
        {
            if (!IsPhysicalParticle(index)) continue;
            var offset = Particles[index].Position - center;
            var tangentialVelocity = new Vector2(-offset.Y, offset.X) * angularVelocity;
            Particles[index].PreviousPosition -= tangentialVelocity * dt;
        }
    }

    public void ScaleMaterial(float factor)
    {
        factor = Math.Clamp(factor, 1f, 1.08f);
        if (factor <= 1.0001f || PhysicalParticleCount <= 0) return;
        var center = Center;
        for (var index = 0; index < Particles.Length; index++)
        {
            if (!IsPhysicalParticle(index)) continue;
            var positionOffset = Particles[index].Position - center;
            var previousOffset = Particles[index].PreviousPosition - center;
            Particles[index].Position = center + positionOffset * factor;
            Particles[index].PreviousPosition = center + previousOffset * factor;
            Particles[index].Radius *= factor;
        }
        for (var index = 0; index < Constraints.Count; index++)
        {
            var constraint = Constraints[index];
            constraint.RestLength *= factor;
            constraint.Lambda = 0f;
            Constraints[index] = constraint;
        }
        var areaScale = factor * factor;
        for (var index = 0; index < AreaConstraints.Count; index++)
        {
            var area = AreaConstraints[index];
            area.RestArea *= areaScale;
            area.Lambda = 0f;
            AreaConstraints[index] = area;
        }
        _restAreaTotal *= areaScale;
        Radius *= factor;
        TopologyRevision++;
        Wake();
    }

    public int DamageBonds(Vector2 point, float radius, float damage)
        => DamageLine(point, point, radius, damage);

    public int DamagePath(IReadOnlyList<Vector2> path, float thickness, float damage)
    {
        if (path.Count < 2) return 0;
        var broken = 0;
        var touched = false;
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

            touched = true;
            var falloff = Math.Clamp(1f - distance / MathF.Max(0.001f, thickness), 0.2f, 1f);
            bond.Health -= damage * falloff;
            Constraints[i] = bond;
            if (bond.Health <= 0f && BreakBond(i, true)) broken++;
        }

        if (touched) ShowHurtExpression(damage);
        if (broken <= 0) return 0;
        for (var segmentIndex = 1; segmentIndex < path.Count; segmentIndex++)
            RecordCutSegment(new CutSegment(path[segmentIndex - 1], path[segmentIndex]));
        RefreshSurfaceMask();
        TopologyDirty = true;
        Wake();
        return broken;
    }

    public int DamageLine(
        Vector2 start,
        Vector2 end,
        float thickness,
        float damage,
        int maximumBreaks = int.MaxValue)
    {
        if (maximumBreaks <= 0) return 0;
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
        var touched = false;
        for (var i = 0; i < Constraints.Count; i++)
        {
            if (broken >= maximumBreaks) break;
            var bond = Constraints[i];
            if (bond.Broken) continue;
            var a = Particles[bond.A].Position;
            var b = Particles[bond.B].Position;
            var distance = SegmentDistance(a, b, start, end);
            if (distance > thickness) continue;

            touched = true;
            var falloff = Math.Clamp(1f - distance / MathF.Max(0.001f, thickness), 0.2f, 1f);
            bond.Health -= damage * falloff;
            Constraints[i] = bond;
            if (bond.Health <= 0f && BreakBond(i, true)) broken++;
        }

        if (touched) ShowHurtExpression(damage);
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
        _hasDamageMask = true;
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

        if (components.Count <= 1)
        {
            _pendingExplosionFragmentMotion = false;
            return new List<SoftBody>();
        }
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
                BrokenLinkCount = damageMask.Count(damaged => damaged),
                _faceClock = _faceClock,
                _nextBlinkTime = _nextBlinkTime,
                _blinkRemaining = _blinkRemaining,
                _hurtRemaining = _hurtRemaining,
                _hitFlashRemaining = _hitFlashRemaining,
                _blinkSequence = _blinkSequence,
                _personalitySequence = _personalitySequence,
                _personalityHopTimer = _personalityHopTimer,
                PersonalityHopCount = PersonalityHopCount,
                LastPersonalityHopSpeed = LastPersonalityHopSpeed,
                LastPersonalityHopWasInTube = LastPersonalityHopWasInTube
            };
            foreach (var stain in _bloodStains)
            {
                if (!map.TryGetValue(stain.ParticleIndex, out var childParticle)) continue;
                var worldMark = BloodStainWorldPosition(stain);
                var childReference = child.Particles[childParticle].Position - child.Center;
                if (childReference.LengthSquared() < 0.0001f) childReference = -Vector2.UnitY;
                else childReference = Vector2.Normalize(childReference);
                child._bloodStains.Add(stain with
                {
                    ParticleIndex = childParticle,
                    LocalOffset = worldMark - child.Particles[childParticle].Position,
                    ReferenceDirection = childReference
                });
                child._bloodStainSerial = Math.Max(child._bloodStainSerial, (byte)(stain.Variation + 1));
            }
            if (IsFrozen)
                child.SetFrozen(
                    true,
                    _frozenCollisionPadding,
                    collisionRadiiAlreadyExpanded: true);
            child.Wake();
            result.Add(child);
        }
        ApplyExplosionFragmentMotion(result);
        return result;
    }

    private void ApplyExplosionFragmentMotion(IReadOnlyList<SoftBody> fragments)
    {
        if (!_pendingExplosionFragmentMotion || fragments.Count == 0) return;
        _pendingExplosionFragmentMotion = false;

        var simulationDt = MathF.Max(_pendingExplosionDt, 0.0001f);
        var minimumSpeed = _pendingExplosionMinimumSpeed;
        var maximumSpeed = MathF.Max(minimumSpeed, _pendingExplosionMaximumSpeed);
        var seed = MixExplosionSeed(
            unchecked((uint)ParentId * 0x9E3779B9u) ^
            _explosionMotionSerial * 0x85EBCA6Bu ^
            unchecked((uint)BrokenLinkCount * 0xC2B2AE35u));

        for (var fragmentIndex = 0; fragmentIndex < fragments.Count; fragmentIndex++)
        {
            var fragment = fragments[fragmentIndex];
            var fragmentSeed = MixExplosionSeed(
                seed ^ unchecked((uint)(fragmentIndex + 1) * 0x27D4EB2Du));
            var radial = fragment.Center - _pendingExplosionCenter;
            if (radial.LengthSquared() < 0.001f)
            {
                var fallbackAngle = MathF.Tau * ExplosionSample(fragmentSeed);
                radial = new Vector2(MathF.Cos(fallbackAngle), MathF.Sin(fallbackAngle));
            }
            else
            {
                radial = Vector2.Normalize(radial);
            }

            var tangent = new Vector2(-radial.Y, radial.X);
            var launchSample = ExplosionSample(MixExplosionSeed(fragmentSeed + 0x68E31DA4u));
            var lateralSample = ExplosionSample(MixExplosionSeed(fragmentSeed + 0xB5297A4Du)) * 2f - 1f;
            var spinSample = ExplosionSample(MixExplosionSeed(fragmentSeed + 0x1B56C4E9u));
            // Alternate the deterministic sign while leaving magnitude random.
            // Hash parity alone occasionally gave every fragment the same rotation.
            var spinSign = ((fragmentIndex + (int)(seed & 1u)) & 1) == 0 ? -1f : 1f;

            var velocity = fragment.AverageVelocity(simulationDt);
            var currentOutwardSpeed = Vector2.Dot(velocity, radial);
            var desiredOutwardSpeed = minimumSpeed +
                                       (maximumSpeed - minimumSpeed) *
                                       (0.76f + launchSample * 0.24f);
            var outwardCorrection = MathF.Max(0f, desiredOutwardSpeed - currentOutwardSpeed);
            var lateralSpeed = lateralSample *
                               Math.Clamp(maximumSpeed * 0.15f, 34f, 128f);
            fragment.AddImpulse(
                radial * outwardCorrection + tangent * lateralSpeed,
                simulationDt);

            // Smaller pieces can tumble faster, while larger coherent slabs still
            // receive enough torque to visibly break the radial/grid uniformity.
            var sizeFactor = Math.Clamp(
                8f / MathF.Sqrt(MathF.Max(2f, fragment.PhysicalParticleCount)),
                0.48f,
                1f);
            var angularVelocity = spinSign *
                                  (2.4f + spinSample * 5.8f) *
                                  sizeFactor;
            fragment.AddAngularImpulse(angularVelocity, simulationDt);
        }
    }

    private static uint MixExplosionSeed(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        return value ^ (value >> 16);
    }

    private static float ExplosionSample(uint value)
        => (value & 0x00FFFFFFu) / 16777216f;

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
        => (uint)particleIndex < (uint)Particles.Length &&
           !_convertedMask[particleIndex] &&
           !_releasedMask[particleIndex];

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

    public int CrushAgainstSurface(
        Vector2 impactCenter,
        float halfWidth,
        float crushDepth,
        float surfaceY,
        float severity)
    {
        if (_physicalParticleCount <= 0 || halfWidth <= 0f || crushDepth <= 0f)
            return 0;

        var crushTop = surfaceY - crushDepth;
        var crushMask = new bool[Particles.Length];
        var crushed = 0;
        for (var particleIndex = 0; particleIndex < Particles.Length; particleIndex++)
        {
            if (!IsPhysicalParticle(particleIndex)) continue;
            var particle = Particles[particleIndex];
            if (MathF.Abs(particle.Position.X - impactCenter.X) >
                    halfWidth + particle.Radius ||
                particle.Position.Y + particle.Radius < crushTop ||
                particle.Position.Y - particle.Radius > surfaceY + ParticleSpacing * 0.35f)
                continue;
            crushMask[particleIndex] = true;
            crushed++;
        }
        if (crushed <= 0) return 0;

        var brokenBonds = 0;
        for (var bondIndex = 0; bondIndex < Constraints.Count; bondIndex++)
        {
            var bond = Constraints[bondIndex];
            if (bond.Broken || (!crushMask[bond.A] && !crushMask[bond.B])) continue;
            bond.Broken = true;
            Constraints[bondIndex] = bond;
            brokenBonds++;
            if (!crushMask[bond.A]) _damageMask[bond.A] = true;
            if (!crushMask[bond.B]) _damageMask[bond.B] = true;
        }
        for (var areaIndex = 0; areaIndex < AreaConstraints.Count; areaIndex++)
        {
            var area = AreaConstraints[areaIndex];
            if (area.Broken ||
                (!crushMask[area.A] && !crushMask[area.B] && !crushMask[area.C]))
                continue;
            area.Broken = true;
            AreaConstraints[areaIndex] = area;
        }

        for (var particleIndex = 0; particleIndex < Particles.Length; particleIndex++)
        {
            if (!crushMask[particleIndex]) continue;
            _convertedMask[particleIndex] = true;
            _activeParticleCount--;
            _physicalParticleCount--;
            Particles[particleIndex].InverseMass = 0f;
            Particles[particleIndex].Position = new Vector2(
                Particles[particleIndex].Position.X,
                MathF.Min(surfaceY, MathF.Max(crushTop, Particles[particleIndex].Position.Y)));
            Particles[particleIndex].PreviousPosition = Particles[particleIndex].Position;
        }

        BrokenLinkCount += brokenBonds;
        _hasDamageMask = true;
        TopologyRevision++;
        TopologyDirty = true;
        RecordCutSegment(new CutSegment(
            new Vector2(impactCenter.X - halfWidth, crushTop),
            new Vector2(impactCenter.X + halfWidth, crushTop)));

        var woundCount = 0;
        var woundReach = ParticleSpacing * 2.25f;
        for (var particleIndex = 0;
             particleIndex < Particles.Length && woundCount < 5;
             particleIndex++)
        {
            if (!IsPhysicalParticle(particleIndex)) continue;
            var position = Particles[particleIndex].Position;
            if (MathF.Abs(position.X - impactCenter.X) > halfWidth + ParticleSpacing ||
                MathF.Abs(position.Y - crushTop) > woundReach)
                continue;
            _damageMask[particleIndex] = true;
            _pendingWounds.Add(new WoundEvent(
                position,
                -Vector2.UnitY,
                Math.Clamp(severity * 0.85f, 1.4f, 5.5f)));
            woundCount++;
        }

        ShowHurtExpression(severity);
        RefreshSurfaceMask();
        Wake();
        if (_physicalParticleCount <= 3) Mode = SimulationMode.LooseFragment;
        return crushed;
    }

    public int ExciseSweptBand(
        Vector2 start,
        Vector2 end,
        float radius,
        int maximumParticles = 8)
    {
        if (_physicalParticleCount <= 0 || radius <= 0f || maximumParticles <= 0)
            return 0;

        var segment = end - start;
        var segmentLengthSquared = segment.LengthSquared();
        var candidates = new List<(int Index, float DistanceSquared)>(12);
        for (var particleIndex = 0; particleIndex < Particles.Length; particleIndex++)
        {
            if (!IsPhysicalParticle(particleIndex)) continue;
            var position = Particles[particleIndex].Position;
            var amount = segmentLengthSquared < 0.0001f
                ? 0f
                : Math.Clamp(Vector2.Dot(position - start, segment) / segmentLengthSquared, 0f, 1f);
            var closest = start + segment * amount;
            var reach = radius + Particles[particleIndex].Radius * 0.35f;
            var distanceSquared = Vector2.DistanceSquared(position, closest);
            if (distanceSquared <= reach * reach)
                candidates.Add((particleIndex, distanceSquared));
        }
        if (candidates.Count == 0) return 0;
        candidates.Sort(static (left, right) =>
            left.DistanceSquared.CompareTo(right.DistanceSquared));

        var removed = Math.Min(maximumParticles, candidates.Count);
        var removalMask = new bool[Particles.Length];
        for (var index = 0; index < removed; index++)
            removalMask[candidates[index].Index] = true;

        var brokenBonds = 0;
        for (var bondIndex = 0; bondIndex < Constraints.Count; bondIndex++)
        {
            var bond = Constraints[bondIndex];
            if (bond.Broken || (!removalMask[bond.A] && !removalMask[bond.B])) continue;
            bond.Broken = true;
            Constraints[bondIndex] = bond;
            brokenBonds++;
            if (!removalMask[bond.A]) _damageMask[bond.A] = true;
            if (!removalMask[bond.B]) _damageMask[bond.B] = true;
        }
        for (var areaIndex = 0; areaIndex < AreaConstraints.Count; areaIndex++)
        {
            var area = AreaConstraints[areaIndex];
            if (area.Broken ||
                (!removalMask[area.A] && !removalMask[area.B] && !removalMask[area.C]))
                continue;
            area.Broken = true;
            AreaConstraints[areaIndex] = area;
        }
        for (var particleIndex = 0; particleIndex < Particles.Length; particleIndex++)
        {
            if (!removalMask[particleIndex]) continue;
            _convertedMask[particleIndex] = true;
            _activeParticleCount--;
            _physicalParticleCount--;
            Particles[particleIndex].InverseMass = 0f;
            Particles[particleIndex].PreviousPosition = Particles[particleIndex].Position;
        }

        BrokenLinkCount += brokenBonds;
        _hasDamageMask = true;
        TopologyRevision++;
        TopologyDirty = true;
        RecordCutSegment(new CutSegment(start, end));
        var woundDirection = segmentLengthSquared < 0.0001f
            ? -Vector2.UnitY
            : Vector2.Normalize(new Vector2(-segment.Y, segment.X));
        for (var index = 0; index < Math.Min(3, removed); index++)
        {
            var position = Particles[candidates[index].Index].Position;
            _pendingWounds.Add(new WoundEvent(position, woundDirection, 3.8f));
        }
        ShowHurtExpression(4.2f);
        RefreshSurfaceMask();
        Wake();
        if (_physicalParticleCount <= 3) Mode = SimulationMode.LooseFragment;
        return removed;
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
