using System.Numerics;
using BlobForge.Physics;

namespace BlobForge.World;

public enum WeaponDumbwaiterPhase : byte
{
    Closed,
    Opening,
    Open,
    Closing,
    ClosedHold
}

public readonly record struct DumbwaiterDebris(
    Vector2 Position,
    Vector2 Velocity,
    float RemainingSeconds,
    float Size,
    byte Variation);

/// <summary>
/// Owns the one-token weapon reroll loop. The coin is a real conveyor-carried
/// object; depositing it arms the adjacent button, which destroys the current
/// tool and runs the authored shutter before delivering the replacement.
/// </summary>
public sealed class WeaponDumbwaiter
{
    public const float TokenRadius = 7f;
    public const float BreakDropChance = 0.0045f;
    private const float DoorCloseSeconds = 0.49f;
    private const float ClosedHoldSeconds = 0.18f;
    private const float DoorOpenSeconds = 0.49f;
    private readonly Dictionary<int, int> _observedBrokenLinks = new();
    private readonly Dictionary<int, (int Broken, Vector2 Position)> _parentDamageScratch = new();
    private readonly List<DumbwaiterDebris> _debris = new(20);
    private uint _randomState = 0xA341316Cu;
    private float _phaseTime;
    private float _buttonFlashRemaining;
    private int _pendingVariant = int.MinValue;

    public WeaponDumbwaiter(Vector2 toolSocket)
    {
        ToolSocket = toolSocket;
        Phase = WeaponDumbwaiterPhase.Closed;
    }

    public Vector2 ToolSocket { get; }
    public WeaponDumbwaiterPhase Phase { get; private set; }
    public WeaponRerollToken? Token { get; private set; }
    public bool TokenDeposited { get; private set; }
    public bool ButtonArmed => TokenDeposited &&
                               Phase is WeaponDumbwaiterPhase.Closed or WeaponDumbwaiterPhase.Open;
    public bool HasTokenInSystem => Token is not null || TokenDeposited;
    public bool CanSpawnToken => !HasTokenInSystem;
    public IReadOnlyList<DumbwaiterDebris> Debris => _debris;

    public RectangleF DisplayBounds => new(ToolSocket.X - 72f, ToolSocket.Y - 108f, 144f, 192f);
    public RectangleF ControlsBounds => new(ToolSocket.X + 68f, ToolSocket.Y - 64f, 64f, 128f);
    public Vector2 CoinSlotCenter => new(ControlsBounds.Left + 32f, ControlsBounds.Top + 34f);
    public Vector2 ButtonCenter => new(ControlsBounds.Left + 32f, ControlsBounds.Top + 88f);

    public int DoorFrame
    {
        get
        {
            var progress = Phase switch
            {
                WeaponDumbwaiterPhase.Closing => Math.Clamp(_phaseTime / DoorCloseSeconds, 0f, 1f),
                WeaponDumbwaiterPhase.Closed => 1f,
                WeaponDumbwaiterPhase.ClosedHold => 1f,
                WeaponDumbwaiterPhase.Opening => 1f - Math.Clamp(_phaseTime / DoorOpenSeconds, 0f, 1f),
                WeaponDumbwaiterPhase.Open => 0f,
                _ => 1f
            };
            return progress switch
            {
                < 0.18f => 0,
                < 0.50f => 1,
                < 0.82f => 2,
                _ => 3
            };
        }
    }

    public int ControlsFrame => _buttonFlashRemaining > 0f ? 2 : ButtonArmed ? 1 : 0;

    public bool HitCoinSlot(Vector2 point) =>
        Vector2.DistanceSquared(point, CoinSlotCenter) <= 21f * 21f;

    public bool HitButton(Vector2 point) =>
        Vector2.DistanceSquared(point, ButtonCenter) <= 23f * 23f;

    public bool BeginHover(Vector2 point) =>
        Token is { } token &&
        Vector2.DistanceSquared(point, token.Position) <=
        (TokenRadius + 5f) * (TokenRadius + 5f) ||
        Token?.IsGrabbed == true && HitCoinSlot(point) ||
        ButtonArmed && HitButton(point);

    public bool BeginTokenGrab(Vector2 point)
    {
        if (Token is not { } token || token.IsGrabbed ||
            Vector2.DistanceSquared(point, token.Position) > (TokenRadius + 5f) * (TokenRadius + 5f))
            return false;
        token.BeginGrab(point);
        return true;
    }

    public void SetTokenGrabTarget(Vector2 point) => Token?.SetGrabTarget(point);

    public bool ReleaseToken(Vector2 releasePoint, Vector2 velocity)
    {
        if (Token is not { IsGrabbed: true } token) return false;
        if (HitCoinSlot(token.Position) || HitCoinSlot(releasePoint))
        {
            Token = null;
            TokenDeposited = true;
            return true;
        }
        token.EndGrab(velocity);
        return true;
    }

    public bool Activate(int replacementVariant, PhysicalKnife knife)
    {
        if (!ButtonArmed) return false;
        TokenDeposited = false;
        _buttonFlashRemaining = 0.12f;
        _pendingVariant = Math.Clamp(replacementVariant, -1, PhysicalKnife.ArsenalVariantCount - 1);
        SpawnWeaponDebris(knife.Position);
        knife.BeginDumbwaiterExchange();
        Phase = Phase == WeaponDumbwaiterPhase.Open
            ? WeaponDumbwaiterPhase.Closing
            : WeaponDumbwaiterPhase.ClosedHold;
        _phaseTime = 0f;
        return true;
    }

    public void PrepareInitialDelivery(int weaponVariant, PhysicalKnife knife)
    {
        _pendingVariant = Math.Clamp(
            weaponVariant, -1, PhysicalKnife.ArsenalVariantCount - 1);
        knife.BeginDumbwaiterExchange();
        Phase = WeaponDumbwaiterPhase.Closed;
        _phaseTime = 0f;
    }

    public void NotifyWeaponTaken()
    {
        if (Phase != WeaponDumbwaiterPhase.Open) return;
        Phase = WeaponDumbwaiterPhase.Closing;
        _phaseTime = 0f;
    }

    public void ObserveDamage(IReadOnlyList<SoftBody> bodies, bool allowDrop)
    {
        if (bodies.Count == 0) return;
        _parentDamageScratch.Clear();
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.IsDetachedDebris) continue;
            if (!_parentDamageScratch.TryGetValue(body.ParentId, out var current) ||
                body.BrokenLinkCount > current.Broken)
                _parentDamageScratch[body.ParentId] = (body.BrokenLinkCount, body.Center);
        }

        foreach (var pair in _parentDamageScratch)
        {
            _observedBrokenLinks.TryGetValue(pair.Key, out var previous);
            var current = pair.Value.Broken;
            _observedBrokenLinks[pair.Key] = Math.Max(previous, current);
            if (!allowDrop || HasTokenInSystem ||
                Phase is WeaponDumbwaiterPhase.Opening or
                    WeaponDumbwaiterPhase.Closing or
                    WeaponDumbwaiterPhase.ClosedHold ||
                current <= previous) continue;
            var newBreaks = Math.Min(64, current - previous);
            for (var broken = 0; broken < newBreaks; broken++)
            {
                if (Next01() >= BreakDropChance) continue;
                SpawnToken(pair.Value.Position + new Vector2(0f, -18f));
                break;
            }
        }

        if (_observedBrokenLinks.Count > 192)
        {
            var liveParents = _parentDamageScratch.Keys.ToHashSet();
            foreach (var parent in _observedBrokenLinks.Keys.ToArray())
                if (!liveParents.Contains(parent)) _observedBrokenLinks.Remove(parent);
        }
    }

    internal void SpawnToken(Vector2 position)
    {
        if (HasTokenInSystem) return;
        Token = new WeaponRerollToken(position,
            new Vector2((Next01() - 0.5f) * 70f, -55f - Next01() * 35f));
    }

    public bool TrySpawnDebugToken(Vector2 position)
    {
        if (!CanSpawnToken) return false;
        SpawnToken(position);
        return Token is not null;
    }

    public void Step(
        float dt,
        Vector2 gravity,
        IReadOnlyList<ConveyorBelt> conveyors,
        DestructibleGrid grid,
        float worldWidth,
        float worldHeight,
        PhysicalKnife? knife,
        bool powered)
    {
        _buttonFlashRemaining = MathF.Max(0f, _buttonFlashRemaining - dt);
        StepDebris(dt, gravity, conveyors, grid);
        if (Token is { } token)
        {
            token.Step(dt, gravity, conveyors, grid);
            if (token.Position.X < -96f || token.Position.X > worldWidth + 96f ||
                token.Position.Y > worldHeight + 96f)
                Token = null;
        }

        if (knife is null) return;
        if (Phase == WeaponDumbwaiterPhase.Closed)
        {
            if (!powered || _pendingVariant == int.MinValue) return;
            Phase = WeaponDumbwaiterPhase.Opening;
            _phaseTime = 0f;
            return;
        }
        if (Phase == WeaponDumbwaiterPhase.Open)
        {
            if (!knife.IsGrabbed && !knife.IsDeployed) return;
            NotifyWeaponTaken();
            return;
        }
        _phaseTime += dt;
        switch (Phase)
        {
            case WeaponDumbwaiterPhase.Closing when _phaseTime >= DoorCloseSeconds:
                Phase = _pendingVariant == int.MinValue
                    ? WeaponDumbwaiterPhase.Closed
                    : WeaponDumbwaiterPhase.ClosedHold;
                _phaseTime = 0f;
                break;
            case WeaponDumbwaiterPhase.ClosedHold when _phaseTime >= ClosedHoldSeconds:
                Phase = WeaponDumbwaiterPhase.Opening;
                _phaseTime = 0f;
                break;
            case WeaponDumbwaiterPhase.Opening when _phaseTime >= DoorOpenSeconds:
                knife.CompleteDumbwaiterExchange(_pendingVariant);
                _pendingVariant = int.MinValue;
                Phase = WeaponDumbwaiterPhase.Open;
                _phaseTime = 0f;
                break;
        }
    }

    private void SpawnWeaponDebris(Vector2 center)
    {
        _debris.Clear();
        for (var i = 0; i < 18; i++)
        {
            var angle = Next01() * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var speed = 85f + Next01() * 180f;
            _debris.Add(new DumbwaiterDebris(
                center + direction * (3f + Next01() * 11f),
                direction * speed + new Vector2(0f, -55f),
                0.42f + Next01() * 0.34f,
                2f + Next01() * 3f,
                (byte)(i + (int)(Next01() * 19f))));
        }
    }

    private void StepDebris(float dt, Vector2 gravity,
        IReadOnlyList<ConveyorBelt> conveyors, DestructibleGrid grid)
    {
        for (var i = _debris.Count - 1; i >= 0; i--)
        {
            var debris = _debris[i];
            var remaining = debris.RemainingSeconds - dt;
            if (remaining <= 0f)
            {
                _debris.RemoveAt(i);
                continue;
            }
            var previous = debris.Position;
            var velocity = debris.Velocity + gravity * dt * 0.72f;
            var proxy = new Particle
            {
                Position = previous + velocity * dt,
                PreviousPosition = previous,
                Radius = MathF.Max(1f, debris.Size * 0.5f),
                InverseMass = 1f
            };
            grid.ResolveParticle(ref proxy, dt);
            for (var conveyorIndex = 0; conveyorIndex < conveyors.Count; conveyorIndex++)
                conveyors[conveyorIndex].ResolveParticle(ref proxy, dt, true);
            velocity = (proxy.Position - proxy.PreviousPosition) / MathF.Max(0.0001f, dt);
            _debris[i] = debris with
            {
                Position = proxy.Position,
                Velocity = velocity,
                RemainingSeconds = remaining
            };
        }
    }

    private float Next01()
    {
        var x = _randomState;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _randomState = x;
        return (x & 0x00FFFFFFu) / 16777216f;
    }
}

public sealed class WeaponRerollToken
{
    private Vector2 _grabOffset;
    private Vector2 _grabTarget;

    public WeaponRerollToken(Vector2 position, Vector2 velocity)
    {
        Position = position;
        Velocity = velocity;
        _grabTarget = position;
    }

    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public bool IsGrabbed { get; private set; }
    public float Age { get; private set; }
    public int SpinFrame => (int)(Age / 0.085f) & 3;

    public void BeginGrab(Vector2 pointer)
    {
        IsGrabbed = true;
        _grabOffset = Position - pointer;
        _grabTarget = pointer;
        Velocity = Vector2.Zero;
    }

    public void SetGrabTarget(Vector2 pointer) => _grabTarget = pointer;

    public void EndGrab(Vector2 velocity)
    {
        IsGrabbed = false;
        Velocity = Vector2.Clamp(velocity, new Vector2(-900f), new Vector2(900f));
    }

    public void Step(float dt, Vector2 gravity,
        IReadOnlyList<ConveyorBelt> conveyors, DestructibleGrid grid)
    {
        Age += dt;
        if (IsGrabbed)
        {
            var target = _grabTarget + _grabOffset;
            Velocity = (target - Position) / MathF.Max(0.0001f, dt);
            Position = target;
            return;
        }

        Velocity += gravity * dt;
        var previous = Position;
        var proxy = new Particle
        {
            Position = Position + Velocity * dt,
            PreviousPosition = previous,
            Radius = WeaponDumbwaiter.TokenRadius,
            InverseMass = 1f
        };
        grid.ResolveParticle(ref proxy, dt);
        for (var i = 0; i < conveyors.Count; i++)
            conveyors[i].ResolveParticle(ref proxy, dt, true);
        Position = proxy.Position;
        Velocity = (proxy.Position - proxy.PreviousPosition) / MathF.Max(0.0001f, dt);
    }
}
