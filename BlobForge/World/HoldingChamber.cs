using System.Numerics;
using BlobForge.Physics;

namespace BlobForge.World;

public sealed class HoldingChamber
{
    private const float ReleaseHoldSeconds = 1f;
    private const float HatchTravelSpeed = 5.6f;
    private float _openTimeRemaining;
    private bool _leverHeld;
    private bool _leverHoldingOpen;
    private Vector2 _counterPosition;
    private readonly HashSet<int> _admittedLineages = new();

    public HoldingChamber(Vector2 center, float innerRadius, Vector2? counterPosition = null)
    {
        Center = center;
        InnerRadius = Math.Clamp(innerRadius, 64f, 150f);
        var spriteBounds = SpriteBounds;
        _counterPosition = counterPosition ?? new Vector2(
            spriteBounds.Left - 55f, spriteBounds.Top + 45f);
    }

    public static HoldingChamber CreateProcessingStation(Vector2? counterPosition = null)
        => new(new Vector2(160f, 205f), 70f, counterPosition);

    public Vector2 Center { get; }
    public float InnerRadius { get; }
    public float NeckHalfWidth => InnerRadius * 0.64f;
    public float HatchHalfWidth => InnerRadius * 0.66f;
    public float HatchOpen { get; private set; }
    public float LeverPull { get; private set; }
    public bool IsOpen => HatchOpen >= 0.62f;
    public Vector2 SpawnPoint => new(Center.X, 18f);
    public int UnitsProduced { get; private set; }
    public bool CounterSelected { get; set; }
    public RectangleF CounterBounds => new(_counterPosition.X, _counterPosition.Y, 50f, 34f);

    public bool HitCounter(Vector2 point) => CounterBounds.Contains(point.X, point.Y);

    public void SetCounterPosition(Vector2 position, float worldWidth, float worldHeight)
    {
        _counterPosition = new Vector2(
            Math.Clamp(position.X, 0f, MathF.Max(0f, worldWidth - CounterBounds.Width)),
            Math.Clamp(position.Y, 0f, MathF.Max(0f, worldHeight - CounterBounds.Height)));
    }

    private float SpriteScale => InnerRadius / 69f;
    private float GranularShellRadius => InnerRadius + 9f * SpriteScale;
    public RectangleF SpriteBounds => new(
        Center.X - 82f * SpriteScale,
        Center.Y - 105f * SpriteScale,
        192f * SpriteScale,
        192f * SpriteScale);
    public RectangleF FeedTubeBounds
    {
        get
        {
            var width = InnerRadius * 0.99f;
            var spritePipeBottom = SpriteBounds.Top + InnerRadius * 0.18f;
            // Extend behind the sprite collar until it overlaps the circular
            // shell. Rendering remains unchanged because the chamber sprite is
            // drawn afterward, while blood sees one sealed foreground shape.
            var collisionPipeBottom = Center.Y - GranularShellRadius + 4f;
            var bottom = MathF.Max(spritePipeBottom, collisionPipeBottom);
            return new RectangleF(Center.X - width * 0.5f, -2f, width, bottom + 2f);
        }
    }
    public Vector2 LeverPivot => Center + new Vector2(82f, -2f) * SpriteScale;
    public float LeverLength => 48f * SpriteScale;
    public Vector2 LeverRestHandle => LeverPivot - Vector2.UnitY * LeverLength;
    public Vector2 LeverHandle
    {
        get
        {
            const float restAngle = -MathF.PI * 0.5f;
            const float pulledAngle = -0.06f;
            var angle = restAngle + (pulledAngle - restAngle) * SmoothStep(LeverPull);
            return LeverPivot + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * LeverLength;
        }
    }

    public bool HitLever(Vector2 point)
    {
        var handleRadius = 16f * SpriteScale;
        if (Vector2.DistanceSquared(point, LeverHandle) <= handleRadius * handleRadius) return true;
        return DistanceToSegmentSquared(point, LeverPivot, LeverHandle) <= 10f * 10f;
    }

    public void BeginLeverDrag(Vector2 pointer)
    {
        _leverHeld = true;
        UpdateLeverDrag(pointer);
    }

    public void UpdateLeverDrag(Vector2 pointer)
    {
        if (!_leverHeld) return;
        const float restAngle = -MathF.PI * 0.5f;
        const float pulledAngle = -0.06f;
        var delta = pointer - LeverPivot;
        var angle = delta.LengthSquared() < 0.001f ? restAngle : MathF.Atan2(delta.Y, delta.X);
        // Do not allow the handle to wrap beneath or behind the pivot.
        angle = Math.Clamp(angle, restAngle, pulledAngle);
        LeverPull = Math.Clamp((angle - restAngle) / (pulledAngle - restAngle), 0f, 1f);
        _leverHoldingOpen = LeverPull >= 0.72f;
    }

    public void EndLeverDrag()
    {
        _leverHeld = false;
        _leverHoldingOpen = false;
    }

    public void TriggerRelease()
    {
        _openTimeRemaining = MathF.Max(_openTimeRemaining, ReleaseHoldSeconds);
    }

    public void Step(float dt)
    {
        _openTimeRemaining = MathF.Max(0f, _openTimeRemaining - dt);
        var commandedOpen = _leverHoldingOpen || _openTimeRemaining > 0f;
        HatchOpen = MoveTowards(HatchOpen, commandedOpen ? 1f : 0f, dt * HatchTravelSpeed);
        if (!_leverHeld) LeverPull = MoveTowards(LeverPull, 0f, dt * 2.8f);
    }

    public void Admit(SoftBody body) => _admittedLineages.Add(body.ParentId);

    public void RegisterProducedUnit() => UnitsProduced++;

    public void Revoke(SoftBody body) => _admittedLineages.Remove(body.ParentId);

    public bool IsAdmitted(SoftBody body) => _admittedLineages.Contains(body.ParentId);

    public SurfaceContact ResolveParticle(ref Particle particle, float dt) =>
        ResolveInteriorParticle(ref particle, dt);

    public SurfaceContact ResolveParticle(ref Particle particle, float dt, bool admitted) =>
        admitted
            ? ResolveInteriorParticle(ref particle, dt)
            : ResolveExteriorParticle(ref particle, dt);

    public SurfaceContact ResolveGranularExterior(ref Particle particle, float dt)
    {
        var strongest = ResolveSolidRectangle(ref particle, dt, FeedTubeBounds);
        var shell = ResolveSolidCircle(ref particle, dt, Center, GranularShellRadius);
        if (shell.Hit && IntersectsRectangle(particle.Position, particle.Radius, FeedTubeBounds))
            shell = ResolveUpperShoulder(ref particle, dt);
        return shell.Hit && (!strongest.Hit || shell.Impact >= strongest.Impact) ? shell : strongest;
    }

    public bool IntersectsGranularObstacle(Vector2 position, float radius)
    {
        if (Vector2.DistanceSquared(position, Center) <
            (GranularShellRadius + radius) * (GranularShellRadius + radius)) return true;
        var tube = FeedTubeBounds;
        return IntersectsRectangle(position, radius, tube);
    }

    private static bool IntersectsRectangle(Vector2 position, float radius, RectangleF rectangle)
    {
        var closest = Vector2.Clamp(
            position,
            new Vector2(rectangle.Left, rectangle.Top),
            new Vector2(rectangle.Right, rectangle.Bottom));
        return Vector2.DistanceSquared(position, closest) < radius * radius - 0.0001f;
    }

    private SurfaceContact ResolveInteriorParticle(ref Particle particle, float dt)
    {
        var delta = particle.Position - Center;
        var distanceSquared = delta.LengthSquared();
        var previousDelta = particle.PreviousPosition - Center;
        var interactionRadius = InnerRadius + particle.Radius * 2.25f;
        // The glass is a local one-way container surface, not a world-wide
        // attraction field. Once a particle has fully cleared the shell it is
        // free to travel sideways across the table and conveyor.
        if (distanceSquared > interactionRadius * interactionRadius &&
            previousDelta.LengthSquared() > interactionRadius * interactionRadius)
            return SurfaceContact.None;
        var allowedRadius = MathF.Max(4f, InnerRadius - particle.Radius);
        if (distanceSquared <= allowedRadius * allowedRadius) return SurfaceContact.None;

        var inNeckOpening = delta.Y < -InnerRadius * 0.22f &&
                            MathF.Abs(delta.X) <= NeckHalfWidth - particle.Radius * 0.2f;
        var inHatchOpening = IsOpen && delta.Y > InnerRadius * 0.18f &&
                             MathF.Abs(delta.X) <= HatchHalfWidth - particle.Radius * 0.2f;
        if (inNeckOpening || inHatchOpening) return SurfaceContact.None;

        var distance = MathF.Sqrt(MathF.Max(0.0001f, distanceSquared));
        var outward = delta / distance;
        var normal = -outward;
        var depth = distance - allowedRadius;
        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var impact = MathF.Max(0f, -Vector2.Dot(velocity, normal));
        particle.Position += normal * depth;
        particle.Contacting = true;
        particle.ContactMemory = 6;
        var tangent = velocity - normal * Vector2.Dot(velocity, normal);
        var rebound = impact > 140f ? normal * impact * 0.06f : Vector2.Zero;
        var correctedVelocity = tangent * 0.94f + rebound;
        particle.PreviousPosition = particle.Position - correctedVelocity * dt;
        if (normal.Y < -0.55f)
        {
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        return new SurfaceContact(
            true,
            particle.Position - normal * particle.Radius,
            normal,
            impact,
            normal.Y < -0.55f);
    }

    private SurfaceContact ResolveExteriorParticle(ref Particle particle, float dt)
    {
        var delta = particle.Position - Center;
        var previousDelta = particle.PreviousPosition - Center;
        var blockedRadius = InnerRadius + 5f + particle.Radius;
        if (delta.LengthSquared() >= blockedRadius * blockedRadius &&
            previousDelta.LengthSquared() >= blockedRadius * blockedRadius)
            return SurfaceContact.None;

        var outward = delta;
        if (outward.LengthSquared() < 0.001f) outward = previousDelta;
        if (outward.LengthSquared() < 0.001f) outward = Vector2.UnitY;
        outward = Vector2.Normalize(outward);
        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var impact = MathF.Max(0f, -Vector2.Dot(velocity, outward));
        particle.Position = Center + outward * blockedRadius;
        particle.Contacting = true;
        particle.ContactMemory = 6;
        var tangent = velocity - outward * Vector2.Dot(velocity, outward);
        var rebound = impact > 140f ? outward * impact * 0.04f : Vector2.Zero;
        particle.PreviousPosition = particle.Position - (tangent * 0.92f + rebound) * dt;
        var supported = outward.Y < -0.55f;
        if (supported)
        {
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        return new SurfaceContact(
            true,
            particle.Position - outward * particle.Radius,
            outward,
            impact,
            supported);
    }

    private static SurfaceContact ResolveSolidCircle(
        ref Particle particle,
        float dt,
        Vector2 center,
        float radius)
    {
        var delta = particle.Position - center;
        var blockedRadius = radius + particle.Radius;
        if (delta.LengthSquared() >= blockedRadius * blockedRadius) return SurfaceContact.None;
        var outward = delta;
        if (outward.LengthSquared() < 0.001f) outward = particle.PreviousPosition - center;
        if (outward.LengthSquared() < 0.001f) outward = -Vector2.UnitY;
        outward = Vector2.Normalize(outward);
        var depth = blockedRadius - MathF.Sqrt(MathF.Max(0f, delta.LengthSquared()));
        return ApplySolidContact(ref particle, dt, outward, depth);
    }

    private static SurfaceContact ResolveSolidRectangle(
        ref Particle particle,
        float dt,
        RectangleF rectangle)
    {
        var minimum = new Vector2(rectangle.Left, rectangle.Top);
        var maximum = new Vector2(rectangle.Right, rectangle.Bottom);
        var closest = Vector2.Clamp(particle.Position, minimum, maximum);
        var delta = particle.Position - closest;
        var distanceSquared = delta.LengthSquared();
        if (distanceSquared > particle.Radius * particle.Radius) return SurfaceContact.None;

        Vector2 normal;
        float depth;
        if (distanceSquared > 0.0001f)
        {
            var distance = MathF.Sqrt(distanceSquared);
            normal = delta / distance;
            depth = particle.Radius - distance;
        }
        else
        {
            var left = particle.Position.X - rectangle.Left;
            var right = rectangle.Right - particle.Position.X;
            var top = particle.Position.Y - rectangle.Top;
            var bottom = rectangle.Bottom - particle.Position.Y;
            var nearest = MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));
            if (nearest == left) normal = -Vector2.UnitX;
            else if (nearest == right) normal = Vector2.UnitX;
            else if (nearest == top) normal = -Vector2.UnitY;
            else normal = Vector2.UnitY;
            depth = particle.Radius + nearest;
        }
        return ApplySolidContact(ref particle, dt, normal, depth);
    }

    private SurfaceContact ResolveUpperShoulder(ref Particle particle, float dt)
    {
        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var side = particle.Position.X < Center.X ||
                   MathF.Abs(particle.Position.X - Center.X) < 0.01f && velocity.X <= 0f
            ? -1f
            : 1f;
        var blockedRadius = GranularShellRadius + particle.Radius + 0.08f;
        var shoulderX = MathF.Min(
            blockedRadius * 0.88f,
            FeedTubeBounds.Width * 0.5f + particle.Radius + 0.08f);
        var shoulderY = -MathF.Sqrt(MathF.Max(0.001f,
            blockedRadius * blockedRadius - shoulderX * shoulderX));
        var target = Center + new Vector2(side * shoulderX, shoulderY);
        var normal = Vector2.Normalize(target - Center);
        var impact = MathF.Max(0f, -Vector2.Dot(velocity, normal));
        particle.Position = target;
        particle.Contacting = true;
        particle.ContactMemory = 6;
        var tangent = velocity - normal * Vector2.Dot(velocity, normal);
        var rebound = impact > 90f ? normal * impact * 0.08f : Vector2.Zero;
        particle.PreviousPosition = particle.Position - (tangent * 0.90f + rebound) * dt;
        var supported = normal.Y < -0.55f;
        if (supported)
        {
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        return new SurfaceContact(
            true,
            particle.Position - normal * particle.Radius,
            normal,
            impact,
            supported);
    }

    private static SurfaceContact ApplySolidContact(
        ref Particle particle,
        float dt,
        Vector2 normal,
        float depth)
    {
        if (depth <= 0f) return SurfaceContact.None;
        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var impact = MathF.Max(0f, -Vector2.Dot(velocity, normal));
        particle.Position += normal * (depth + 0.05f);
        particle.Contacting = true;
        particle.ContactMemory = 6;
        var tangent = velocity - normal * Vector2.Dot(velocity, normal);
        var rebound = impact > 90f ? normal * impact * 0.08f : Vector2.Zero;
        particle.PreviousPosition = particle.Position - (tangent * 0.90f + rebound) * dt;
        var supported = normal.Y < -0.55f;
        if (supported)
        {
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        return new SurfaceContact(
            true,
            particle.Position - normal * particle.Radius,
            normal,
            impact,
            supported);
    }

    public bool HasExited(SoftBody body) =>
        body.Center.Y > Center.Y + InnerRadius + MathF.Max(34f, body.Radius * 0.55f);

    public bool IsInFeedEnvelope(SoftBody body) =>
        !body.IsDetachedDebris &&
        MathF.Abs(body.Center.X - Center.X) < InnerRadius + body.Radius &&
        body.Center.Y < Center.Y + InnerRadius + body.Radius * 0.45f;

    private static float MoveTowards(float current, float target, float maximumDelta) =>
        current < target
            ? MathF.Min(target, current + maximumDelta)
            : MathF.Max(target, current - maximumDelta);

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared < 0.001f) return Vector2.DistanceSquared(point, start);
        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.DistanceSquared(point, start + segment * t);
    }
}

public sealed class ChamberFeedController
{
    private readonly HoldingChamber _chamber;
    private SoftBody? _currentUnit;
    private float _spawnDelay = 0.22f;

    public ChamberFeedController(HoldingChamber chamber)
    {
        _chamber = chamber;
    }

    public SoftBody? CurrentUnit => _currentUnit;
    public int UnitsSpawned { get; private set; }

    public SoftBody? Update(IList<SoftBody> bodies, float dt, Func<Vector2, SoftBody> factory)
    {
        var lostCurrentUnit = _currentUnit is not null && !bodies.Contains(_currentUnit);
        if (lostCurrentUnit)
        {
            var lostUnit = _currentUnit!;
            _currentUnit = bodies.FirstOrDefault(body =>
                body.ParentId == lostUnit.ParentId &&
                _chamber.IsAdmitted(body) &&
                _chamber.IsInFeedEnvelope(body));
            if (_currentUnit is null) _chamber.Revoke(lostUnit);
        }
        if (_currentUnit is null)
            _currentUnit = bodies.FirstOrDefault(body =>
                _chamber.IsAdmitted(body) && _chamber.IsInFeedEnvelope(body));

        if (lostCurrentUnit && _currentUnit is null && float.IsPositiveInfinity(_spawnDelay))
            _spawnDelay = 0.72f;

        if (_currentUnit is not null)
        {
            if (!_chamber.HasExited(_currentUnit)) return null;
            _chamber.Revoke(_currentUnit);
            _currentUnit = null;
            _spawnDelay = 0.72f;
        }

        _spawnDelay -= dt;
        if (_chamber.HatchOpen > 0.015f) return null;
        if (_spawnDelay > 0f) return null;
        var unit = factory(_chamber.SpawnPoint);
        _chamber.Admit(unit);
        bodies.Add(unit);
        _currentUnit = unit;
        UnitsSpawned++;
        _chamber.RegisterProducedUnit();
        _spawnDelay = float.PositiveInfinity;
        return unit;
    }

    public void RequestNext()
    {
        if (_currentUnit is null) _spawnDelay = 0f;
    }
}
