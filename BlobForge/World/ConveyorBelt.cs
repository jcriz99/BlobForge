using System.Numerics;
using BlobForge.Physics;

namespace BlobForge.World;

public sealed class ConveyorBelt
{
    private const int PersistentStainSoftLimit = 192;
    private const float PersistentPigmentFloor = 0.01f;
    private static int _nextId;
    private readonly List<BloodSurfaceMark> _bloodStains = new(64);
    private readonly List<TransientConveyorBloodDrop> _transientDrops = new(6);
    private readonly List<ConveyorDripEmitter> _dripEmitters = new(10);
    private uint _paintRandomState = 0xD1B54A35u;
    private readonly float _minimumWidth;

    public ConveyorBelt(
        Vector2 position,
        float width,
        float height,
        float speed,
        float minimumWidth = 96f,
        bool systemControlled = false)
    {
        Id = Interlocked.Increment(ref _nextId);
        Position = position;
        _minimumWidth = Math.Clamp(minimumWidth, 24f, 96f);
        // The playable continuous-flow line is one physical belt spanning beyond
        // both viewport edges. Authoring belts retain their compact limit.
        Width = Math.Clamp(width, _minimumWidth, systemControlled ? 1600f : 520f);
        Height = Math.Clamp(height, 20f, 72f);
        Speed = Math.Clamp(speed, -420f, 420f);
        IsSystemControlled = systemControlled;
    }

    public int Id { get; }
    public Vector2 Position { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }
    public float Speed { get; private set; }
    public float AnimationOffset { get; private set; }
    public bool IsSelected { get; set; }
    public bool IsSystemControlled { get; }
    public IReadOnlyList<BloodSurfaceMark> BloodStains => _bloodStains;
    public IReadOnlyList<TransientConveyorBloodDrop> TransientDrops => _transientDrops;
    public IReadOnlyList<ConveyorDripEmitter> DripEmitters => _dripEmitters;

    public void Move(Vector2 delta, float arenaWidth, float arenaHeight)
    {
        Position = new Vector2(
            Math.Clamp(Position.X + delta.X, 32f, MathF.Max(32f, arenaWidth - Width - 32f)),
            Math.Clamp(Position.Y + delta.Y, 32f, MathF.Max(32f, arenaHeight - Height - 32f)));
    }

    public void Resize(float widthDelta, float heightDelta, float arenaWidth, float arenaHeight)
    {
        Width = Math.Clamp(Width + widthDelta, _minimumWidth, MathF.Max(_minimumWidth, arenaWidth - Position.X - 32f));
        Height = Math.Clamp(Height + heightDelta, 20f, MathF.Min(72f, arenaHeight - Position.Y - 32f));
    }

    public void ChangeSpeed(float delta) => Speed = Math.Clamp(Speed + delta, -420f, 420f);
    public void Reverse() => Speed = -Speed;
    public void SetAutomationSpeed(float speed)
    {
        if (!IsSystemControlled) return;
        Speed = Math.Clamp(speed, -420f, 420f);
    }

    public bool ContainsPoint(Vector2 point, float padding = 0f)
        => point.X >= Position.X - padding && point.X <= Position.X + Width + padding &&
           point.Y >= Position.Y - padding && point.Y <= Position.Y + Height + padding;

    public ConveyorEditHandle HitEditHandle(Vector2 point)
    {
        if (!ContainsPoint(point, 10f)) return ConveyorEditHandle.None;
        if (MathF.Abs(point.X - (Position.X + Width)) <= 11f) return ConveyorEditHandle.Length;
        if (MathF.Abs(point.Y - (Position.Y + Height)) <= 10f) return ConveyorEditHandle.Height;
        return ContainsPoint(point) ? ConveyorEditHandle.Move : ConveyorEditHandle.None;
    }

    public void Step(float dt)
    {
        AnimationOffset = (AnimationOffset + Speed * dt) % 100000f;
        for (var i = _bloodStains.Count - 1; i >= 0; i--)
        {
            var mark = _bloodStains[i];
            mark.LoopCoordinate = WrapLoopCoordinate(mark.LoopCoordinate + Speed * dt);
            (mark.Position, mark.SurfaceNormal) = PointOnLoop(mark.LoopCoordinate);
            mark.Wetness = MathF.Max(0f, mark.Wetness - dt * 0.085f);

            // A wet pool can establish one stationary drip point at its current
            // location. The flat pigment keeps riding the tread, but the emitter
            // stays on the conveyor frame and releases falling droplets only.
            if (!mark.IsRunoffLeader && mark.SurfaceNormal.Y < -0.55f &&
                     mark.Wetness > 0.16f && mark.Amount > 0.035f &&
                     _dripEmitters.Count < 10)
            {
                mark.FlowAccumulator += dt * (1.02f + mark.Amount * 3.1f);
                if (mark.FlowAccumulator >= 1f && !HasNearbyDripEmitter(mark.Position.X))
                {
                    mark.FlowAccumulator -= 1f;
                    var variation = NextPaintVariation();
                    var transfer = MathF.Min(0.035f, mark.Amount * 0.10f);
                    mark.Amount -= transfer;
                    mark.IsRunoffLeader = true;
                    _dripEmitters.Add(new ConveyorDripEmitter
                    {
                        LocalX = mark.Position.X,
                        Lifetime = 2.2f + MathF.Min(1.8f, mark.Amount * 1.1f),
                        Intensity = Math.Clamp(0.55f + mark.Amount * 0.55f, 0.55f, 1.8f),
                        Accumulator = 0.72f,
                        Variation = variation
                    });
                }
            }
            if (_transientDrops.Count < 6 && mark.Wetness > 0.18f && mark.Amount > 0.38f &&
                mark.SurfaceNormal.Y > 0.45f)
            {
                mark.FlowAccumulator += dt * (mark.Amount - 0.30f) * 0.42f;
                if (mark.FlowAccumulator >= 1f)
                {
                    mark.FlowAccumulator -= 1f;
                    var transfer = MathF.Min(0.025f, mark.Amount * 0.05f);
                    mark.Amount -= transfer;
                    _transientDrops.Add(new TransientConveyorBloodDrop
                    {
                        Position = new Vector2(
                            Math.Clamp(mark.Position.X + (NextPaint01() - 0.5f) * 7f, 2f, Width - 2f),
                            Height + 2f),
                        Velocity = new Vector2((NextPaint01() - 0.5f) * 12f, 18f + NextPaint01() * 22f),
                        Lifetime = 0.38f + NextPaint01() * 0.42f,
                        Variation = NextPaintVariation()
                    });
                }
            }
            mark.Amount = MathF.Max(PersistentPigmentFloor, mark.Amount);
            _bloodStains[i] = mark;
        }
        for (var i = _dripEmitters.Count - 1; i >= 0; i--)
        {
            var emitter = _dripEmitters[i];
            emitter.Lifetime -= dt;
            emitter.Accumulator += dt * (3.2f + emitter.Intensity * 2.1f);
            if (emitter.Accumulator >= 1f && _transientDrops.Count < 6)
            {
                emitter.Accumulator -= 1f;
                _transientDrops.Add(new TransientConveyorBloodDrop
                {
                    Position = new Vector2(
                        Math.Clamp(emitter.LocalX + (NextPaint01() - 0.5f) * 5f, 2f, Width - 2f),
                        Height + 2f),
                    Velocity = new Vector2((NextPaint01() - 0.5f) * 9f, 24f + NextPaint01() * 28f),
                    Lifetime = 0.58f + NextPaint01() * 0.55f,
                    Variation = NextPaintVariation()
                });
            }
            if (emitter.Lifetime <= 0f) _dripEmitters.RemoveAt(i);
            else _dripEmitters[i] = emitter;
        }
        for (var i = _transientDrops.Count - 1; i >= 0; i--)
        {
            var drop = _transientDrops[i];
            drop.Lifetime -= dt;
            drop.Velocity.Y += 220f * dt;
            drop.Position += drop.Velocity * dt;
            if (drop.Lifetime <= 0f) _transientDrops.RemoveAt(i);
            else _transientDrops[i] = drop;
        }
    }

    private bool HasNearbyDripEmitter(float localX)
    {
        foreach (var emitter in _dripEmitters)
            if (MathF.Abs(emitter.LocalX - localX) < 18f) return true;
        return false;
    }

    public SurfaceContact ResolveParticle(ref Particle particle, float dt, bool applyBeltVelocity,
        bool forceTopContainment = false)
    {
        var min = Position;
        var max = Position + new Vector2(Width, Height);
        if (forceTopContainment &&
            particle.Position.X >= min.X - particle.Radius &&
            particle.Position.X <= max.X + particle.Radius &&
            particle.Position.Y > min.Y - particle.Radius)
        {
            var containmentVelocity = (particle.Position - particle.PreviousPosition) / dt;
            var containmentImpact = MathF.Max(0f, containmentVelocity.Y);
            particle.Position.Y = min.Y - particle.Radius;
            particle.Contacting = true;
            particle.ContactMemory = 6;
            particle.Supported = true;
            particle.SupportMemory = 10;
            var correctedX = containmentVelocity.X;
            if (applyBeltVelocity)
                correctedX += (Speed - correctedX) * 0.20f;
            particle.PreviousPosition = particle.Position - new Vector2(correctedX * 0.93f, 0f) * dt;
            return new SurfaceContact(true,
                new Vector2(particle.Position.X, min.Y), -Vector2.UnitY, containmentImpact, true);
        }
        var closest = Vector2.Clamp(particle.Position, min, max);
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
            var left = particle.Position.X - min.X;
            var right = max.X - particle.Position.X;
            var top = particle.Position.Y - min.Y;
            var bottom = max.Y - particle.Position.Y;
            var nearest = MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));
            if (nearest == left) normal = -Vector2.UnitX;
            else if (nearest == right) normal = Vector2.UnitX;
            else if (nearest == top) normal = -Vector2.UnitY;
            else normal = Vector2.UnitY;
            depth = particle.Radius + nearest;
        }
        if (depth <= 0f) return SurfaceContact.None;

        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var impact = MathF.Max(0f, -Vector2.Dot(velocity, normal));
        particle.Position += normal * depth;
        particle.Contacting = true;
        particle.ContactMemory = 6;
        var tangent = velocity - normal * Vector2.Dot(velocity, normal);
        var corrected = tangent * 0.93f;
        if (normal.Y < -0.55f)
        {
            if (applyBeltVelocity)
                corrected.X += (Speed - corrected.X) * 0.20f;
            corrected.Y = MathF.Min(0f, corrected.Y);
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        particle.PreviousPosition = particle.Position - corrected * dt;
        var contactPoint = particle.Position - normal * particle.Radius;
        return new SurfaceContact(true, contactPoint, normal, impact, normal.Y < -0.55f);
    }

    public void DepositBlood(Vector2 worldPosition, Vector2 normal, float amount)
    {
        var local = worldPosition - Position;
        local.X = Math.Clamp(local.X, 0f, Width);
        local.Y = Math.Clamp(local.Y, 0f, Height);
        var coordinate = WrapLoopCoordinate(CoordinateFromLocal(local, normal) + (NextPaint01() - 0.5f) * 3.5f);
        var (loopPosition, loopNormal) = PointOnLoop(coordinate);
        AddOrMergeBlood(new BloodSurfaceMark
        {
            Position = loopPosition,
            SurfaceNormal = loopNormal,
            Amount = amount,
            Wetness = 1f,
            Radius = (2.2f + MathF.Sqrt(amount) * 5.5f) * (0.78f + NextPaint01() * 0.44f),
            LoopCoordinate = coordinate,
            Variation = NextPaintVariation()
        });
    }

    private void AddOrMergeBlood(BloodSurfaceMark incoming)
    {
        var searchStart = Math.Max(0, _bloodStains.Count - 96);
        for (var i = _bloodStains.Count - 1; i >= searchStart; i--)
        {
            var mark = _bloodStains[i];
            if (mark.IsDrip != incoming.IsDrip) continue;
            var distance = mark.IsDrip
                ? MathF.Abs(mark.Position.X - incoming.Position.X)
                : LoopDistance(mark.LoopCoordinate, incoming.LoopCoordinate);
            if (distance > 5f) continue;
            mark.Amount = MathF.Min(mark.IsDrip ? 0.42f : 2.4f, mark.Amount + incoming.Amount);
            if (mark.IsDrip)
                mark.RunoffLoad = MathF.Min(1.25f, MathF.Max(mark.RunoffLoad, incoming.RunoffLoad));
            mark.Wetness = 1f;
            mark.Radius = Math.Clamp(2.1f + MathF.Sqrt(mark.Amount) * 5.4f,
                2f, mark.IsDrip ? 5.8f : 10.5f);
            _bloodStains[i] = mark;
            return;
        }
        if (_bloodStains.Count >= PersistentStainSoftLimit)
        {
            var closestIndex = -1;
            var closestDistance = float.MaxValue;
            for (var i = 0; i < _bloodStains.Count; i++)
            {
                var mark = _bloodStains[i];
                if (mark.IsDrip != incoming.IsDrip) continue;
                var distance = mark.IsDrip
                    ? MathF.Abs(mark.Position.X - incoming.Position.X)
                    : LoopDistance(mark.LoopCoordinate, incoming.LoopCoordinate);
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closestIndex = i;
            }
            if (closestIndex >= 0)
            {
                var mark = _bloodStains[closestIndex];
                mark.Amount = MathF.Min(mark.IsDrip ? 0.42f : 2.4f, mark.Amount + incoming.Amount);
                if (mark.IsDrip)
                    mark.RunoffLoad = MathF.Min(1.25f, MathF.Max(mark.RunoffLoad, incoming.RunoffLoad));
                mark.Wetness = 1f;
                mark.Radius = Math.Clamp(2.1f + MathF.Sqrt(mark.Amount) * 5.4f,
                    2f, mark.IsDrip ? 5.8f : 10.5f);
                _bloodStains[closestIndex] = mark;
                return;
            }
        }
        _bloodStains.Add(incoming);
    }

    private float LoopDistance(float a, float b)
    {
        var distance = MathF.Abs(a - b);
        return MathF.Min(distance, LoopLength - distance);
    }

    private float LoopRadius => Math.Clamp(Height * 0.5f, 10f, 36f);
    private float StraightLength => MathF.Max(1f, Width - LoopRadius * 2f);
    private float LoopLength => StraightLength * 2f + MathF.PI * LoopRadius * 2f;

    private float WrapLoopCoordinate(float coordinate)
    {
        var length = LoopLength;
        coordinate %= length;
        return coordinate < 0f ? coordinate + length : coordinate;
    }

    private float CoordinateFromLocal(Vector2 local, Vector2 normal)
    {
        var radius = LoopRadius;
        var straight = StraightLength;
        if (normal.Y < -0.5f) return Math.Clamp(local.X - radius, 0f, straight);
        if (normal.X > 0.5f)
        {
            var angle = MathF.Atan2(local.Y - radius, local.X - (Width - radius));
            return straight + Math.Clamp(angle + MathF.PI * 0.5f, 0f, MathF.PI) * radius;
        }
        if (normal.Y > 0.5f)
            return straight + MathF.PI * radius + Math.Clamp(Width - radius - local.X, 0f, straight);
        var leftAngle = MathF.Atan2(local.Y - radius, local.X - radius);
        if (leftAngle < MathF.PI * 0.5f) leftAngle += MathF.Tau;
        return straight * 2f + MathF.PI * radius +
               Math.Clamp(leftAngle - MathF.PI * 0.5f, 0f, MathF.PI) * radius;
    }

    private (Vector2 Position, Vector2 Normal) PointOnLoop(float coordinate)
    {
        var radius = LoopRadius;
        var straight = StraightLength;
        var arc = MathF.PI * radius;
        coordinate = WrapLoopCoordinate(coordinate);
        if (coordinate <= straight)
            return (new Vector2(radius + coordinate, 0f), -Vector2.UnitY);
        coordinate -= straight;
        if (coordinate <= arc)
        {
            var angle = -MathF.PI * 0.5f + coordinate / radius;
            var normal = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            return (new Vector2(Width - radius, radius) + normal * radius, normal);
        }
        coordinate -= arc;
        if (coordinate <= straight)
            return (new Vector2(Width - radius - coordinate, Height), Vector2.UnitY);
        coordinate -= straight;
        var leftAngle = MathF.PI * 0.5f + coordinate / radius;
        var leftNormal = new Vector2(MathF.Cos(leftAngle), MathF.Sin(leftAngle));
        return (new Vector2(radius, radius) + leftNormal * radius, leftNormal);
    }

    private float NextPaint01()
    {
        _paintRandomState ^= _paintRandomState << 13;
        _paintRandomState ^= _paintRandomState >> 17;
        _paintRandomState ^= _paintRandomState << 5;
        return (_paintRandomState & 0x00FFFFFFu) / 16777216f;
    }

    private byte NextPaintVariation() => (byte)(NextPaint01() * 255f);
}

public struct TransientConveyorBloodDrop
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Lifetime;
    public byte Variation;
}

public struct ConveyorDripEmitter
{
    public float LocalX;
    public float Lifetime;
    public float Intensity;
    public float Accumulator;
    public byte Variation;
}

public enum ConveyorEditHandle : byte
{
    None,
    Move,
    Length,
    Height
}

public readonly record struct SurfaceContact(
    bool Hit,
    Vector2 ContactPoint,
    Vector2 Normal,
    float Impact,
    bool IsTop)
{
    public static SurfaceContact None => new(false, Vector2.Zero, Vector2.Zero, 0f, false);
}
