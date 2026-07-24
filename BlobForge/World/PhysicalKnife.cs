using System.Numerics;
using BlobForge.Physics;

namespace BlobForge.World;

public readonly record struct CleaverBloodStain(
    Vector2 LocalPosition,
    float Amount,
    float Wetness,
    byte Variation);

public enum CleaverControlState : byte
{
    Carry,
    Windup,
    Swing,
    Impact,
    Recovery
}

public enum ToolHoldMode : byte
{
    None,
    DirectDrag,
    Equipped
}

public enum ArsenalProjectileKind : byte
{
    Nail,
    ShotgunPellet,
    MagnumBullet,
    SmgBullet,
    SawBlade,
    Grenade,
    BlackHole,
    Rat,
    GrowthPulse,
    Flame,
    IceBolt,
    LightningSeed,
    AcidGlob,
    WaterTear,
    Baseball
}

public readonly record struct ArsenalProjectile(
    ArsenalProjectileKind Kind,
    Vector2 Position,
    Vector2 Velocity,
    float Angle,
    float RemainingSeconds,
    float Power,
    int PenetrationRemaining,
    int LastHitParentId,
    bool Stuck,
    Vector2 AttachmentOffset);

public readonly record struct GrenadeTrajectoryPoint(
    Vector2 Position,
    bool Bounced,
    bool Final);

public readonly record struct ArsenalActionEffect(
    int Variant,
    Vector2 Start,
    Vector2 End,
    float RemainingSeconds,
    float Size);

public readonly record struct HeavyBloodBridge(
    Vector2 ToolLocalAnchor,
    Vector2 GroundAnchor,
    float RemainingSeconds,
    float LifetimeSeconds,
    float Thickness,
    byte Variation);

public readonly record struct RatAgent(
    Vector2 Position,
    Vector2 Velocity,
    SoftBody? Target,
    Vector2 BodyOffset,
    int TargetParticleIndex,
    bool Attached,
    bool HasAttachedOnce,
    int TargetParentId,
    float ChewCooldown,
    float RemainingSeconds,
    byte Frame);

public readonly record struct AcidPool(
    Vector2 Position,
    float Radius,
    float RemainingSeconds,
    float DamageCooldown,
    Vector2 SurfaceNormal,
    SoftBody? AttachedBody,
    int BodyParticleIndex,
    ConveyorBelt? Conveyor,
    byte Variation);

public readonly record struct FlamePatch(
    Vector2 Position,
    Vector2 Velocity,
    float RemainingSeconds,
    SoftBody? AttachedBody,
    int BodyParticleIndex,
    Vector2 SurfaceNormal,
    ConveyorBelt? Conveyor,
    bool SurfaceFire,
    float SpreadCooldown,
    byte Variation);

public enum SmokeKind : byte
{
    Fire,
    Acid,
    Saber
}

public readonly record struct SmokeParticle(
    Vector2 Position,
    Vector2 Velocity,
    float RemainingSeconds,
    float LifetimeSeconds,
    float Radius,
    SmokeKind Kind,
    byte Variation);

public sealed class BurningBlobState
{
    public required SoftBody Body { get; set; }
    public Vector2 LastPosition { get; set; }
    public float RemainingSeconds { get; set; }
    public float DamageCooldown { get; set; }
    public byte Variation { get; init; }
}

public sealed class FrozenBlobState
{
    public required SoftBody Body { get; init; }
    public required Vector2[] Offsets { get; init; }
    public float RemainingSeconds { get; set; }
    public float ShatterCooldown { get; set; }
    public bool PendingSplitPropagation { get; set; }
    public int Generation { get; init; }
}

public sealed class PhysicalKnife
{
    public const int ArsenalVariantCount = 22;
    private static readonly Vector2[] ArsenalVisualAnchors =
    {
        new(71f, 32f), new(63f, 44f), new(70f, 35f), new(61f, 42f), new(33f, 43f),
        new(72f, 41f), new(69f, 33f), new(72f, 40f), new(55f, 55f), new(79f, 32f),
        new(68f, 34f), new(48f, 42f), new(68f, 40f),
        new(70f, 40f), new(70f, 41f), new(70f, 41f), new(70f, 42f),
        new(70f, 42f), new(70f, 42f), new(70f, 42f), new(66f, 43f), new(68f, 39f)
    };
    private readonly record struct ToolBounds(float MinX, float MinY, float MaxX, float MaxY);
    private static readonly ToolBounds SaberHiltBounds = new(-17f, -8f, 12f, 9f);
    private static readonly ToolBounds[] ArsenalLocalBounds =
    {
        new(-59f, -8f, 12f, 9f), new(-45f, -24f, 8f, 13f),
        new(-60f, -11f, 18f, 15f), new(-43f, -21f, 18f, 14f),
        new(-44f, -23f, 21f, 15f), new(-67f, -18f, 5f, 18f),
        new(-57f, -15f, 22f, 15f), new(-63f, -22f, 8f, 14f),
        new(-28f, -47f, 27f, 4f), new(-73f, -16f, 7f, 18f),
        new(-60f, -22f, 9f, 16f), new(-18f, -34f, 24f, 11f),
        new(-64f, -32f, 17f, 14f),
        new(-63f, -20f, 9f, 13f), new(-60f, -16f, 8f, 12f),
        new(-61f, -22f, 9f, 12f), new(-64f, -27f, 8f, 12f),
        new(-65f, -17f, 8f, 12f), new(-62f, -19f, 9f, 12f),
        new(-65f, -27f, 8f, 12f), new(-51f, -32f, 7f, 10f),
        new(-62f, -23f, 23f, 4f)
    };
    private static readonly Vector2[] ArsenalEdgeStarts =
    {
        new(-57f, 0f), Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero,
        Vector2.Zero, Vector2.Zero, new(-63f, 14f), Vector2.Zero,
        new(-72f, 0f), new(-60f, -14f), Vector2.Zero, new(-62f, -22f),
        Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero,
        Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero
    };
    private static readonly Vector2[] ArsenalEdgeEnds =
    {
        new(-14f, 0f), Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero,
        Vector2.Zero, Vector2.Zero, new(-47f, 14f), Vector2.Zero,
        new(-61f, -6f), new(-60f, 10f), Vector2.Zero, new(-62f, 5f),
        Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero,
        Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero
    };
    private static readonly Vector2[] ArsenalMuzzleOffsets =
    {
        Vector2.Zero,
        new(-44f, -15f), // nail gun
        new(-59f, -6f),  // shotgun
        new(-41f, -13f), // magnum
        new(-20f, -13f), // SMG
        new(-67f, -11f), // spinning blade launcher
        new(-55f, 0f),
        Vector2.Zero,
        new(-1f, -23f),
        Vector2.Zero,
        Vector2.Zero,
        new(0f, -32f),
        Vector2.Zero,
        new(-63f, -7f), new(-60f, -7f), new(-61f, -7f),
        new(-64f, -8f), new(-65f, -7f), new(-62f, -7f),
        new(-65f, -7f), new(-38f, -16f), Vector2.Zero
    };
    private readonly record struct NailPin(
        SoftBody Body,
        Vector2 BodyOffset,
        Vector2 StaticAnchor,
        SoftBody? JoinedBody,
        Vector2 JoinedBodyOffset);
    private readonly record struct PikePin(
        SoftBody Body,
        int ParticleIndex,
        Vector2 StaticAnchor);
    private readonly Dictionary<int, float> _damageCooldowns = new();
    private readonly List<CleaverBloodStain> _bloodStains = new(24);
    private readonly List<ArsenalProjectile> _arsenalProjectiles = new(64);
    private readonly List<NailPin> _nailPins = new(16);
    private readonly List<PikePin> _pikePins = new(8);
    private readonly List<ArsenalActionEffect> _arsenalActionEffects = new(24);
    private readonly List<GrenadeTrajectoryPoint> _grenadeTrajectory = new(48);
    private readonly List<HeavyBloodBridge> _heavyBloodBridges = new(8);
    private readonly HashSet<int> _sledgeCrushedParentIds = new();
    private readonly List<RatAgent> _rats = new(12);
    private readonly List<AcidPool> _acidPools = new(8);
    private readonly List<FlamePatch> _flamePatches = new(48);
    private readonly List<SmokeParticle> _smokeParticles = new(128);
    private readonly List<BurningBlobState> _burningBlobs = new(12);
    private readonly List<FrozenBlobState> _frozenBlobs = new(24);
    private readonly Dictionary<int, float> _enlargementScales = new();
    private readonly Dictionary<int, int> _tearHitCounts = new();
    private Vector2 _previousPosition;
    private Vector2 _grabTarget;
    private Vector2 _lastGrabTarget;
    private Vector2 _gripVelocity;
    private Vector2 _chopDirection = Vector2.UnitY;
    private float _angularVelocity;
    private float _previousAngle;
    private float _windupDistance;
    private float _windupStrength;
    private float _controlStateTime;
    private bool _primaryActionHeld;
    private bool _primaryActionBuffered;
    private bool _primaryActionSwing;
    private bool _strongHitConsumed;
    private float _heavyImpactAge = -1f;
    private float _heavyImpactStrength;
    private bool _arsenalPrimaryHeld;
    private bool _arsenalTriggerPending;
    private float _arsenalPrimaryTime;
    private float _arsenalFireCooldown;
    private Vector2 _arsenalAimDirection = -Vector2.UnitX;
    private SoftBody? _slingshotBody;
    private float _slingshotCharge;
    private Vector2 _actionPointer;
    private bool _placementPreviewValid;
    private Vector2 _placementPreviewPosition;
    private float _placementPreviewAngle;
    private int _slingshotHeightIndex = 1;
    private SoftBody? _launchedSlingshotBody;
    private Vector2 _slingshotPreviousVelocity;
    private float _slingshotLaunchRemaining;
    private float _slingshotLaunchPower;
    private float _baseRotationAngle;
    private bool _rotationAdjusting;
    private Vector2 _rotationPointerStart;
    private float _rotationStartAngle;
    private float _rotationDragDistance;
    private bool _saberIgnited;
    private Vector2 _grenadeAimAnchor;
    private Vector2 _grenadeThrowDirection = -Vector2.UnitX;
    private float _grenadeThrowSpeed = 440f;
    private Vector2 _sledgeSlamStartPosition;
    private float _sledgeGroundSurfaceY;
    private bool _sledgeGroundInitialized;
    private bool _sledgeSwingRight;
    private bool _sledgeToggleGesture;
    private Vector2 _sledgeImpactGripPosition;
    private Vector2 _sledgeRecoveryStartPosition;
    private float _sledgeImpactAngle;
    private bool _sledgeGranularAftershockPending;
    private Vector2 _sledgeAftershockPoint;
    private float _sledgeAftershockStrength;
    private bool _placementPreviewShown;
    private float _gloveStrikeAge = -1f;
    private float _gloveStrikeCharge;
    private Vector2 _gloveStrikeDirection;
    private Vector2 _glovePreviousOffset;
    private int _gloveHitParentId = -1;
    private float _gloveBlockedReach = 1f;
    private SoftBody? _gloveCarriedBody;
    private bool _gloveUppercutFinished;
    private float _specialEffectCooldown;
    private float _flameTrailCooldown;
    private float _smokeSpawnCooldown;
    private bool _baseballInPlay;
    private uint _arsenalRandom = 0x9E3779B9u;
    private const float MagneticReturnRadius = 112f;
    private const float MaximumGripDistance = 96f;
    private const float MaximumSwingAngularSpeed = 13f;
    private const float ReadyAngle = MathF.PI * 0.5f;
    private const float PrimaryChargeSeconds = 0.70f;
    private const float PrimaryChargeBarDelay = PrimaryChargeSeconds * 0.25f;
    private const float PrimarySwingDriveSeconds = 0.085f;
    private const float SledgeOverheadAngle = MathF.PI * 0.5f;
    private const float SledgeGroundAngle = 0f;
    private const float SledgeSlamSeconds = 0.28f;
    private const float SledgeImpactHoldSeconds = 0.13f;
    private const float SledgeLiftSeconds = 0.18f;
    private const float SledgeReturnSeconds = 0.20f;
    private const float SledgeRecoverySeconds =
        SledgeLiftSeconds + SledgeReturnSeconds;
    private const float SledgeLiftDistance = 34f;
    private const float SledgeFaceCollisionRadius = 3f;
    private const int MaximumBloodStains = 24;
    private byte _stainSerial;

    public PhysicalKnife(Vector2 holsterPosition)
    {
        HolsterPosition = holsterPosition;
        _baseRotationAngle = ReadyAngle;
        Position = holsterPosition;
        _previousPosition = Position;
        _previousAngle = Angle;
        _grabTarget = Position;
        _lastGrabTarget = Position;
        IsHolstered = true;
    }

    public Vector2 HolsterPosition { get; }
    public int ArsenalVisualVariant { get; private set; } = -1;
    public Vector2 Position { get; private set; }
    public float Angle { get; private set; }
    public bool IsGrabbed { get; private set; }
    public ToolHoldMode HoldMode { get; private set; }
    public bool IsEquipped => IsGrabbed && HoldMode == ToolHoldMode.Equipped;
    public bool IsDeployed { get; private set; }
    public bool PlacementPreviewVisible =>
        IsEquipped && ArsenalVisualVariant is 8 or 9 && _placementPreviewShown;
    public bool PlacementPreviewValid => PlacementPreviewVisible && _placementPreviewValid;
    public Vector2 PlacementPreviewPosition => _placementPreviewPosition;
    public float PlacementPreviewAngle => _placementPreviewAngle;
    public int SlingshotHeightIndex => _slingshotHeightIndex;
    public SoftBody? SlingshotBody => _slingshotBody;
    public bool IsCharging => IsEquipped && _primaryActionHeld && ControlState == CleaverControlState.Windup;
    public bool IsHolstered { get; private set; }
    public bool IsReturningToHolster { get; private set; }
    public bool IsRespawning => RespawnRemaining > 0f;
    public float RespawnRemaining { get; private set; }
    public bool Visible => !IsRespawning;
    public int BlobContactsThisStep { get; private set; }
    public bool PuncturedThisStep { get; private set; }
    public CleaverControlState ControlState { get; private set; }
    public float WindupStrength => _windupStrength;
    public float PrimaryCharge => ArsenalVisualVariant == 10 && _arsenalPrimaryHeld
        ? Math.Clamp(_arsenalPrimaryTime / 0.72f, 0f, 1f)
        : IsCharging ? _windupStrength : 0f;
    public bool PrimaryChargeVisible => ArsenalVisualVariant == 10
        ? IsEquipped && _arsenalPrimaryHeld && _arsenalPrimaryTime >= 0.12f
        : IsCharging && _controlStateTime >= PrimaryChargeBarDelay;
    public bool HeavyImpactActive => _heavyImpactAge >= 0f;
    public float HeavyImpactAge => MathF.Max(0f, _heavyImpactAge);
    public float HeavyImpactStrength => _heavyImpactStrength;
    public Vector2 HeavyImpactPosition { get; private set; }
    public float HeavyImpactAngle { get; private set; }
    public Vector2 ChopDirection => _chopDirection;
    public IReadOnlyList<CleaverBloodStain> BloodStains => _bloodStains;
    public IReadOnlyList<ArsenalProjectile> ArsenalProjectiles => _arsenalProjectiles;
    public IReadOnlyList<ArsenalActionEffect> ArsenalActionEffects => _arsenalActionEffects;
    public IReadOnlyList<GrenadeTrajectoryPoint> GrenadeTrajectory => _grenadeTrajectory;
    public IReadOnlyList<HeavyBloodBridge> HeavyBloodBridges => _heavyBloodBridges;
    public IReadOnlyList<RatAgent> Rats => _rats;
    public IReadOnlyList<AcidPool> AcidPools => _acidPools;
    public IReadOnlyList<FlamePatch> FlamePatches => _flamePatches;
    public IReadOnlyList<SmokeParticle> SmokeParticles => _smokeParticles;
    public IReadOnlyList<BurningBlobState> BurningBlobs => _burningBlobs;
    public IReadOnlyList<FrozenBlobState> FrozenBlobs => _frozenBlobs;
    public bool BaseballInPlay => _baseballInPlay;
    public bool ArsenalPrimaryHeld => _arsenalPrimaryHeld;
    public bool RotationAdjusting => _rotationAdjusting || _sledgeToggleGesture;
    public float BaseRotationAngle => _baseRotationAngle;
    public Vector2 BaseAimDirection => Rotate(-Vector2.UnitX, _baseRotationAngle);
    public Vector2 LiveBarrelDirection => Rotate(-Vector2.UnitX, Angle);
    public Vector2 LiveMuzzlePosition =>
        (uint)ArsenalVisualVariant < ArsenalMuzzleOffsets.Length
            ? Position + Rotate(ArsenalMuzzleOffsets[ArsenalVisualVariant], Angle)
            : Position;
    public int NailPinCount => _nailPins.Count;
    public int JoinedNailPinCount => _nailPins.Count(pin => pin.JoinedBody is not null);
    public bool SaberIgnited => _saberIgnited;
    public bool SledgeSwingRight => _sledgeSwingRight;
    public int PikePinCount => _pikePins.Count;
    public int ArsenalShotSerial { get; private set; }
    public int ArsenalExplosionSerial { get; private set; }
    public int SaberSizzleSerial { get; private set; }
    public Vector2 LastArsenalActionPosition { get; private set; }
    public Vector2 GloveStrikeOffset
    {
        get
        {
            if (_gloveStrikeAge < 0f) return Vector2.Zero;
            var fullCharge = _gloveStrikeCharge >= 0.98f;
            var duration = fullCharge ? 0.34f : 0.24f;
            var phase = Math.Clamp(_gloveStrikeAge / duration, 0f, 1f);
            var reach = MathF.Min(MathF.Sin(phase * MathF.PI), _gloveBlockedReach);
            if (fullCharge)
            {
                if (phase < 0.18f)
                    return Vector2.UnitY * (52f * SmoothStep01(phase / 0.18f));
                if (phase < 0.72f)
                    return Vector2.Lerp(
                        Vector2.UnitY * 52f,
                        _gloveStrikeDirection * 76f - Vector2.UnitY * 88f,
                        SmoothStep01((phase - 0.18f) / 0.54f));
                return Vector2.Lerp(
                    _gloveStrikeDirection * 76f - Vector2.UnitY * 88f,
                    Vector2.Zero,
                    SmoothStep01((phase - 0.72f) / 0.28f));
            }
            return _gloveStrikeDirection * (reach * (58f + _gloveStrikeCharge * 34f));
        }
    }
    public Vector2 RenderPosition => Position + GloveStrikeOffset;
    public Vector2 ScreenShakeOffset
    {
        get
        {
            if (ArsenalVisualVariant != 7 || _heavyImpactAge < 0f || _heavyImpactAge > 0.18f)
                return Vector2.Zero;
            var falloff = 1f - _heavyImpactAge / 0.18f;
            var amplitude = (2.2f + _heavyImpactStrength * 2.6f) * falloff;
            return new Vector2(
                MathF.Sin(_heavyImpactAge * 227f) * amplitude,
                MathF.Cos(_heavyImpactAge * 173f) * amplitude * 0.72f);
        }
    }
    private Vector2 LocalEdgeStart => ArsenalVisualVariant == 7 && _sledgeSwingRight
        ? new Vector2(-63f, -22f)
        : (uint)ArsenalVisualVariant < ArsenalEdgeStarts.Length &&
                                      ArsenalEdgeStarts[ArsenalVisualVariant] != Vector2.Zero
        ? ArsenalEdgeStarts[ArsenalVisualVariant]
        : new Vector2(-9f, 19f);
    private Vector2 LocalEdgeEnd => ArsenalVisualVariant == 7 && _sledgeSwingRight
        ? new Vector2(-47f, -22f)
        : (uint)ArsenalVisualVariant < ArsenalEdgeEnds.Length &&
                                    ArsenalEdgeEnds[ArsenalVisualVariant] != Vector2.Zero
        ? ArsenalEdgeEnds[ArsenalVisualVariant]
        : new Vector2(-51f, 19f);
    public Vector2 HandleStart => Position + RotateToolLocal(new Vector2(13f, 0f));
    public Vector2 HandleEnd => Position + RotateToolLocal(new Vector2(-8f, 0f));
    public Vector2 BladeCoreStart => Position + RotateToolLocal(
        ArsenalVisualVariant < 0 ? new Vector2(-9f, -7f) : LocalEdgeStart);
    public Vector2 BladeCoreEnd => Position + RotateToolLocal(
        ArsenalVisualVariant < 0 ? new Vector2(-51f, -7f) : LocalEdgeEnd);
    public Vector2 BladeEdgeStart => Position + RotateToolLocal(LocalEdgeStart);
    public Vector2 BladeEdgeEnd => Position + RotateToolLocal(LocalEdgeEnd);
    public Vector2 SlingshotCradlePosition => Position + Rotate(
        new Vector2(0f, _slingshotHeightIndex switch { 0 => -102f, 2 => -162f, _ => -134f }),
        Angle);
    public Vector2 SlingshotForkLeft => SlingshotCradlePosition +
                                         Rotate(new Vector2(-18f, -10f), Angle);
    public Vector2 SlingshotForkRight => SlingshotCradlePosition +
                                          Rotate(new Vector2(17f, -10f), Angle);
    public Vector2 HeavyBloodBridgeToolPosition(HeavyBloodBridge bridge) =>
        Position + Rotate(bridge.ToolLocalAnchor, Angle);

    public bool HitTest(Vector2 point)
    {
        if (!Visible) return false;
        if (IsReturningToHolster) return false;
        if ((uint)ArsenalVisualVariant < ArsenalVariantCount)
        {
            if (IsDeployed && ArsenalVisualVariant == 8)
            {
                var deployedLocal = InverseRotate(point - Position, Angle);
                return deployedLocal.X >= -48f && deployedLocal.X <= 48f &&
                       deployedLocal.Y >= -184f && deployedLocal.Y <= 8f;
            }
            var local = InverseRotate(point - Position, Angle);
            var anchor = ArsenalVisualAnchors[ArsenalVisualVariant];
            const float padding = 3f;
            return local.X >= -anchor.X - padding && local.X <= 96f - anchor.X + padding &&
                   local.Y >= -anchor.Y - padding && local.Y <= 64f - anchor.Y + padding;
        }
        var handleClosest = ClosestPoint(point, HandleStart, HandleEnd);
        var bladeClosest = ClosestPoint(point, BladeCoreStart, BladeCoreEnd);
        return Vector2.DistanceSquared(point, handleClosest) <= 10f * 10f ||
               Vector2.DistanceSquared(point, bladeClosest) <= 14f * 14f;
    }

    public bool BeginGrab(Vector2 point) => BeginHold(point, point, ToolHoldMode.DirectDrag);

    public bool Equip(Vector2 point, Vector2 cursor) => BeginHold(point, cursor, ToolHoldMode.Equipped);

    public void SelectArsenalVisual(int variant)
    {
        ArsenalVisualVariant = Math.Clamp(variant, -1, ArsenalVariantCount - 1);
        _baseRotationAngle = UsesBarrelOrientation || ArsenalVisualVariant is 8 or 10 or 21
            ? 0f
            : ReadyAngle;
        _saberIgnited = false;
        _rotationAdjusting = false;
        _bloodStains.Clear();
        _arsenalProjectiles.Clear();
        _arsenalActionEffects.Clear();
        _grenadeTrajectory.Clear();
        _heavyBloodBridges.Clear();
        _pikePins.Clear();
        _damageCooldowns.Clear();
        _placementPreviewShown = false;
        _gloveStrikeAge = -1f;
        _baseballInPlay = false;
        ResetArsenalPrimary();
        ReturnToHolster();
    }

    private bool BeginHold(Vector2 point, Vector2 target, ToolHoldMode mode)
    {
        if (!HitTest(point)) return false;
        IsDeployed = false;
        IsGrabbed = true;
        HoldMode = mode;
        IsHolstered = false;
        _grabTarget = target;
        _lastGrabTarget = target;
        _gripVelocity = Vector2.Zero;
        _windupDistance = 0f;
        _windupStrength = 0f;
        _controlStateTime = 0f;
        _strongHitConsumed = false;
        _primaryActionHeld = false;
        _primaryActionBuffered = false;
        _primaryActionSwing = false;
        ControlState = CleaverControlState.Carry;
        if (mode == ToolHoldMode.Equipped)
        {
            if (ArsenalVisualVariant == 8) _baseRotationAngle = 0f;
            if (ArsenalVisualVariant == 10)
                _baseRotationAngle = MathF.Abs(ShortestAngle(_baseRotationAngle, 0f)) <
                                     MathF.PI * 0.5f
                    ? 0f
                    : MathF.PI;
            // E-equipping establishes the useful blade-up ready pose immediately.
            // The old blade-down pose forced a full half-turn before every chop.
            Angle = _baseRotationAngle;
            _previousAngle = Angle;
            _angularVelocity = 0f;
        }
        return true;
    }

    public void SetGrabTarget(Vector2 target)
    {
        _actionPointer = target;
        if (ArsenalVisualVariant == 11 && _arsenalPrimaryHeld) return;
        _grabTarget = target;
    }

    public bool BeginRotationAdjust(Vector2 pointer)
    {
        if (!IsEquipped) return false;
        if (ArsenalVisualVariant == 8) return false;
        if (ArsenalVisualVariant == 7)
        {
            // The hammer has a deliberate two-sided stance instead of free
            // rotation. RMB captures the gesture and flips the next arc.
            _sledgeSwingRight = !_sledgeSwingRight;
            _sledgeToggleGesture = true;
            return true;
        }
        if (ArsenalVisualVariant == 10)
        {
            _baseRotationAngle = MathF.Abs(ShortestAngle(_baseRotationAngle, 0f)) <
                                 MathF.PI * 0.5f
                ? MathF.PI
                : 0f;
            Angle = _baseRotationAngle;
            _angularVelocity = 0f;
            _sledgeToggleGesture = true;
            return true;
        }
        // Rotation is a player override. It must remain available during swing
        // recovery and after recoil instead of being silently eaten by a state lock.
        ResetArsenalPrimary();
        _primaryActionHeld = false;
        _primaryActionBuffered = false;
        _primaryActionSwing = false;
        _windupDistance = 0f;
        _windupStrength = 0f;
        _controlStateTime = 0f;
        _strongHitConsumed = true;
        ControlState = CleaverControlState.Carry;
        _rotationAdjusting = true;
        _rotationPointerStart = pointer;
        _rotationStartAngle = _baseRotationAngle;
        _rotationDragDistance = 0f;
        return true;
    }

    public void UpdateRotationAdjust(Vector2 pointer)
    {
        if (_sledgeToggleGesture) return;
        if (!_rotationAdjusting) return;
        var drag = pointer - _rotationPointerStart;
        _rotationDragDistance = MathF.Max(_rotationDragDistance, drag.Length());
        if (drag.LengthSquared() < 5f * 5f) return;
        _baseRotationAngle = MathF.Atan2(drag.Y, drag.X) - MathF.PI;
        // While the player is explicitly rotating the held tool, the physical
        // barrel/blade follows the guide exactly. Recoil and swing momentum take
        // control again after the gesture ends.
        Angle = _baseRotationAngle;
        _angularVelocity = 0f;
        _arsenalAimDirection = BaseAimDirection;
    }

    public bool RotateBaseBy(float radians)
    {
        if (!IsEquipped || ArsenalVisualVariant is 7 or 8 or 10 ||
            !float.IsFinite(radians) || MathF.Abs(radians) < 0.0001f)
            return false;
        ResetArsenalPrimary();
        _primaryActionHeld = false;
        _primaryActionBuffered = false;
        _primaryActionSwing = false;
        _windupDistance = 0f;
        _windupStrength = 0f;
        _controlStateTime = 0f;
        ControlState = CleaverControlState.Carry;
        _baseRotationAngle += radians;
        Angle = _baseRotationAngle;
        _angularVelocity = 0f;
        _arsenalAimDirection = BaseAimDirection;
        return true;
    }

    public void EndRotationAdjust()
    {
        if (_sledgeToggleGesture)
        {
            _sledgeToggleGesture = false;
            return;
        }
        if (!_rotationAdjusting) return;
        _rotationAdjusting = false;
    }

    public bool DeigniteSaber()
    {
        if (!IsEquipped || ArsenalVisualVariant != 0 || !_saberIgnited) return false;
        _saberIgnited = false;
        _primaryActionHeld = false;
        _primaryActionBuffered = false;
        _primaryActionSwing = false;
        ControlState = CleaverControlState.Carry;
        return true;
    }

    public void EndGrab(Vector2 releaseVelocity, float dt)
    {
        if (!IsGrabbed) return;
        _rotationAdjusting = false;
        ResetArsenalPrimary();
        IsGrabbed = false;
        HoldMode = ToolHoldMode.None;
        _primaryActionHeld = false;
        _primaryActionBuffered = false;
        _primaryActionSwing = false;
        ControlState = CleaverControlState.Carry;
        if (Vector2.DistanceSquared(Position, HolsterPosition) <= MagneticReturnRadius * MagneticReturnRadius)
        {
            IsReturningToHolster = true;
            _previousPosition = Position;
            _angularVelocity = 0f;
            return;
        }
        // The spring-driven grip velocity is the authoritative momentum of the
        // physical tool. Mouse history contributes only a small directional hint;
        // it must never replace a fast moving cleaver with a near-zero key-up sample.
        var velocity = _gripVelocity * 0.97f + releaseVelocity * 0.03f;
        var speed = velocity.Length();
        if (speed > 1800f) velocity *= 1800f / speed;
        _previousPosition = Position - velocity * dt;
        // Preserve the angular momentum built while swinging. Horizontal belt-bound
        // throws add only a small hand-release twist rather than turning the tool
        // into a wheel before it has even landed.
        _angularVelocity += Math.Clamp(releaseVelocity.X * 0.003f, -2.5f, 2.5f);
    }

    public bool BeginPrimaryAction()
    {
        if (RotationAdjusting) return false;
        if (ArsenalVisualVariant == 0)
        {
            if (!IsEquipped) return false;
            if (!_saberIgnited)
            {
                _saberIgnited = true;
                return false;
            }
        }
        if (ArsenalVisualVariant >= 0 && !UsesCleaverSwing)
        {
            var deployedSling = ArsenalVisualVariant == 8 && IsDeployed;
            if ((!IsEquipped && !deployedSling) || _arsenalPrimaryHeld) return false;
            _arsenalPrimaryHeld = true;
            _arsenalPrimaryTime = 0f;
            _arsenalTriggerPending = ArsenalVisualVariant is
                1 or 2 or 4 or 13 or 14 or 17 or 18 or 20;
            if (ArsenalVisualVariant == 11)
            {
                _grenadeAimAnchor = _grabTarget;
                _actionPointer = _grabTarget;
                _grenadeThrowDirection = BaseAimDirection;
                _grenadeThrowSpeed = 440f;
            }
            if (ArsenalVisualVariant != 8) _slingshotBody = null;
            _slingshotCharge = 0f;
            return true;
        }
        if (!IsEquipped)
            return false;
        if (ControlState is CleaverControlState.Swing or
            CleaverControlState.Impact or CleaverControlState.Recovery)
        {
            // Keep a held LMB intent alive through the current attack. The next
            // windup begins on the first carry-capable fixed step, without
            // requiring the player to release and click again.
            _primaryActionBuffered = true;
            return true;
        }
        _primaryActionHeld = true;
        _primaryActionBuffered = false;
        _primaryActionSwing = false;
        _windupStrength = 0f;
        _windupDistance = 0f;
        _controlStateTime = 0f;
        ControlState = CleaverControlState.Windup;
        return true;
    }

    public bool TryDeploy(IReadOnlyList<ConveyorBelt> conveyors, float worldWidth)
    {
        if (!IsEquipped || ArsenalVisualVariant is not (8 or 9)) return false;

        if (ArsenalVisualVariant == 8)
        {
            const float sideWallSnapDistance = 72f;
            if (Position.X <= sideWallSnapDistance)
            {
                Position = new Vector2(8f, Math.Clamp(Position.Y, 196f, 620f));
                Angle = MathF.PI * 0.5f;
                FinishDeployment();
                return true;
            }
            if (Position.X >= worldWidth - sideWallSnapDistance)
            {
                Position = new Vector2(worldWidth - 8f, Math.Clamp(Position.Y, 196f, 620f));
                Angle = -MathF.PI * 0.5f;
                FinishDeployment();
                return true;
            }
            ConveyorBelt? mount = null;
            var bestDistance = 96f;
            foreach (var conveyor in conveyors)
            {
                if (Position.X < conveyor.Position.X - 28f ||
                    Position.X > conveyor.Position.X + conveyor.Width + 28f)
                    continue;
                var distance = MathF.Abs(Position.Y - conveyor.Position.Y);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                mount = conveyor;
            }
            if (mount is null) return false;
            Position = new Vector2(
                Math.Clamp(Position.X, mount.Position.X + 24f, mount.Position.X + mount.Width - 24f),
                mount.Position.Y);
            Angle = 0f;
        }
        else
        {
            const float wallSnapDistance = 92f;
            if (Position.X <= wallSnapDistance)
            {
                Position = new Vector2(22f, Math.Clamp(Position.Y, 72f, 620f));
                Angle = MathF.PI;
            }
            else if (Position.X >= worldWidth - wallSnapDistance)
            {
                Position = new Vector2(worldWidth - 22f, Math.Clamp(Position.Y, 72f, 620f));
                Angle = 0f;
            }
            else
                return false;
        }

        FinishDeployment();
        return true;

        void FinishDeployment()
        {
            _previousPosition = Position;
            _gripVelocity = Vector2.Zero;
            _angularVelocity = 0f;
            IsGrabbed = false;
            HoldMode = ToolHoldMode.None;
            IsHolstered = false;
            IsReturningToHolster = false;
            IsDeployed = true;
            ResetArsenalPrimary();
        }
    }

    public bool PlaceAtPreview()
    {
        if (!PlacementPreviewValid) return false;
        Position = _placementPreviewPosition;
        Angle = _placementPreviewAngle;
        _baseRotationAngle = Angle;
        _previousPosition = Position;
        _gripVelocity = Vector2.Zero;
        _angularVelocity = 0f;
        IsGrabbed = false;
        HoldMode = ToolHoldMode.None;
        IsHolstered = false;
        IsReturningToHolster = false;
        IsDeployed = true;
        _placementPreviewValid = false;
        ResetArsenalPrimary();
        return true;
    }

    public bool CanBeginSlingshotPull(Vector2 point) =>
        IsDeployed && ArsenalVisualVariant == 8 && _slingshotBody is { } loaded &&
        loaded.ContainsVisiblePoint(point);

    private void UpdatePlacementPreview(
        IReadOnlyList<ConveyorBelt> conveyors, float worldWidth, float worldHeight)
    {
        _placementPreviewShown = false;
        _placementPreviewValid = false;
        if (!IsEquipped || ArsenalVisualVariant is not (8 or 9)) return;
        if (ArsenalVisualVariant == 8)
        {
            const float sideWallSnapDistance = 72f;
            if (_grabTarget.X <= sideWallSnapDistance)
            {
                _placementPreviewPosition = new Vector2(
                    8f,
                    Math.Clamp(MathF.Round(_grabTarget.Y / 8f) * 8f,
                        196f, worldHeight - 64f));
                _placementPreviewAngle = MathF.PI * 0.5f;
                _placementPreviewShown = true;
                _placementPreviewValid = true;
                return;
            }
            if (_grabTarget.X >= worldWidth - sideWallSnapDistance)
            {
                _placementPreviewPosition = new Vector2(
                    worldWidth - 8f,
                    Math.Clamp(MathF.Round(_grabTarget.Y / 8f) * 8f,
                        196f, worldHeight - 64f));
                _placementPreviewAngle = -MathF.PI * 0.5f;
                _placementPreviewShown = true;
                _placementPreviewValid = true;
                return;
            }
            ConveyorBelt? bestBelt = null;
            var best = float.MaxValue;
            foreach (var conveyor in conveyors)
            {
                if (_grabTarget.X < conveyor.Position.X + 20f ||
                    _grabTarget.X > conveyor.Position.X + conveyor.Width - 20f)
                    continue;
                var height = conveyor.Position.Y - _grabTarget.Y;
                if (height < 72f || height > 208f) continue;
                var distance = MathF.Abs(height - 136f);
                if (distance >= best) continue;
                best = distance;
                bestBelt = conveyor;
            }
            if (bestBelt is null) return;
            var requestedHeight = bestBelt.Position.Y - _grabTarget.Y;
            _slingshotHeightIndex = requestedHeight < 120f ? 0 : requestedHeight > 152f ? 2 : 1;
            _placementPreviewPosition = new Vector2(
                Math.Clamp(MathF.Round(_grabTarget.X / 8f) * 8f,
                    bestBelt.Position.X + 24f, bestBelt.Position.X + bestBelt.Width - 24f),
                bestBelt.Position.Y);
            _placementPreviewAngle = 0f;
            _placementPreviewShown = true;
            _placementPreviewValid = true;
            return;
        }

        var candidate = new Vector2(
            Math.Clamp(MathF.Round(_grabTarget.X / 8f) * 8f, 48f, worldWidth - 48f),
            Math.Clamp(MathF.Round(_grabTarget.Y / 8f) * 8f, 72f, worldHeight - 72f));
        const float minimumBeltClearance = 48f;
        foreach (var conveyor in conveyors)
        {
            // Include the complete authored pike width, not just its mount point.
            if (candidate.X + 84f < conveyor.Position.X ||
                candidate.X - 84f > conveyor.Position.X + conveyor.Width)
                continue;
            if (candidate.Y + 28f > conveyor.Position.Y - minimumBeltClearance)
                return;
        }
        _placementPreviewPosition = candidate;
        _placementPreviewAngle = _baseRotationAngle;
        _placementPreviewShown = true;
        _placementPreviewValid = true;
    }

    public bool EndPrimaryAction()
    {
        if (ArsenalVisualVariant >= 0 && !UsesCleaverSwing)
        {
            if (!_arsenalPrimaryHeld) return false;
            _arsenalPrimaryHeld = false;
            if (ArsenalVisualVariant is 3 or 5 or 8 or 10 or 11 or 19 or 21)
                _arsenalTriggerPending = true;
            return true;
        }
        if (!IsEquipped) return false;
        if (_primaryActionBuffered &&
            ControlState is CleaverControlState.Swing or
                CleaverControlState.Impact or
                CleaverControlState.Recovery)
        {
            _primaryActionBuffered = false;
            return true;
        }
        if (!_primaryActionHeld || ControlState != CleaverControlState.Windup)
            return false;
        _primaryActionHeld = false;
        _windupStrength = MathF.Max(0.12f, _windupStrength);
        BeginAssistedSwing(primaryAction: true);
        return true;
    }

    public void Step(float dt, Vector2 gravity, IReadOnlyList<ConveyorBelt> conveyors,
        IReadOnlyList<SoftBody> bodies, float worldWidth, float worldHeight,
        OverheadTubeFeed? tubeFeed = null, DestructibleGrid? grid = null,
        GranularMaterialSystem? granular = null)
    {
        BlobContactsThisStep = 0;
        PuncturedThisStep = false;
        if (_heavyImpactAge >= 0f)
        {
            _heavyImpactAge += dt;
            if (_heavyImpactAge >= 0.235f) _heavyImpactAge = -1f;
        }
        UpdateBloodStains(dt);
        UpdateArsenalActionEffects(dt);
        UpdateHeavyBloodBridges(dt);
        if (_sledgeGranularAftershockPending)
        {
            BounceSledgeGranular(
                granular,
                _sledgeAftershockPoint,
                dt,
                _sledgeAftershockStrength,
                struckMatter: true);
            _sledgeGranularAftershockPending = false;
        }
        foreach (var key in _damageCooldowns.Keys.ToArray())
        {
            var remaining = _damageCooldowns[key] - dt;
            if (remaining <= 0f) _damageCooldowns.Remove(key);
            else _damageCooldowns[key] = remaining;
        }
        _arsenalFireCooldown = MathF.Max(0f, _arsenalFireCooldown - dt);
        _specialEffectCooldown = MathF.Max(0f, _specialEffectCooldown - dt);
        _flameTrailCooldown = MathF.Max(0f, _flameTrailCooldown - dt);
        _smokeSpawnCooldown = MathF.Max(0f, _smokeSpawnCooldown - dt);
        UpdateNailPins(dt);
        UpdatePikePins(dt);
        UpdateRats(dt, bodies, conveyors, tubeFeed, grid, granular);
        UpdateFlameEffects(dt, bodies, conveyors, tubeFeed);
        UpdateAcidPools(dt, bodies, conveyors, tubeFeed);
        UpdateSmokeParticles(dt);
        UpdateFrozenBlobs(dt, bodies, granular);
        UpdatePlacementPreview(conveyors, worldWidth, worldHeight);
        UpdateSlingshotImpact(dt, bodies, tubeFeed);
        UpdateArsenalProjectiles(dt, gravity, conveyors, bodies, worldWidth, worldHeight,
            tubeFeed, grid, granular);
        if (ArsenalVisualVariant >= 0 && (IsGrabbed || IsDeployed))
        {
            UpdateArsenalPrimary(dt, bodies, tubeFeed);
            if (ArsenalVisualVariant == 11 && _arsenalPrimaryHeld)
                BuildGrenadeTrajectory(gravity, conveyors, worldWidth, worldHeight, grid);
            else
                _grenadeTrajectory.Clear();
        }
        else
            _grenadeTrajectory.Clear();
        if (RespawnRemaining > 0f)
        {
            RespawnRemaining -= dt;
            if (RespawnRemaining <= 0f) ReturnToHolster();
            return;
        }
        if (IsHolstered) return;
        if (IsDeployed)
        {
            if (ArsenalVisualVariant == 9)
                ResolveBlobContacts(dt, bodies, Vector2.Zero, tubeFeed);
            return;
        }
        if (IsReturningToHolster)
        {
            var toSocket = HolsterPosition - Position;
            var distance = toSocket.Length();
            if (distance <= 5f)
            {
                ReturnToHolster();
                return;
            }
            _previousPosition = Position;
            Position += toSocket / distance * MathF.Min(distance, 820f * dt);
            Angle = LerpAngle(Angle, 0f, MathF.Min(1f, 13f * dt));
            return;
        }

        Vector2 velocity;
        if (IsGrabbed)
        {
            var cursorDelta = _grabTarget - _lastGrabTarget;
            var target = Vector2.Clamp(_grabTarget, new Vector2(8f), new Vector2(worldWidth - 8f, worldHeight - 8f));
            if (ArsenalVisualVariant == 7 &&
                ControlState == CleaverControlState.Swing &&
                !_sledgeGroundInitialized)
            {
                _sledgeGroundSurfaceY = ResolveSledgeGroundSurfaceY(
                    _sledgeSlamStartPosition, conveyors, grid, worldHeight);
                _sledgeGroundInitialized = true;
            }

            _controlStateTime += dt;
            if (IsCharging)
            {
                // Holding primary action is an explicit, readable windup. Full
                // strength arrives quickly so the click action stays snappy.
                _windupStrength = Math.Clamp(_controlStateTime / PrimaryChargeSeconds, 0f, 1f);
                _windupDistance = 44f + _windupStrength * 52f;
            }

            if (ControlState == CleaverControlState.Swing &&
                ArsenalVisualVariant != 7 &&
                _controlStateTime >= 0.09f)
                BeginRecovery();
            else if (ControlState == CleaverControlState.Impact &&
                     _controlStateTime >= (ArsenalVisualVariant == 7
                         ? SledgeImpactHoldSeconds
                         : 0.025f))
                BeginRecovery();
            else if (ControlState == CleaverControlState.Recovery &&
                     _controlStateTime >= (ArsenalVisualVariant == 7
                         ? SledgeRecoverySeconds
                         : 0.045f))
            {
                ControlState = CleaverControlState.Carry;
                _primaryActionSwing = false;
                _controlStateTime = 0f;
                _windupDistance = 0f;
                _windupStrength = 0f;
                if (_primaryActionBuffered)
                {
                    _primaryActionBuffered = false;
                    _primaryActionHeld = true;
                    ControlState = CleaverControlState.Windup;
                }
            }

            var fullChargeShake = IsCharging && _windupStrength >= 0.999f;
            if (fullChargeShake)
            {
                // This offset acts on the physical tool rather than its sprite, so
                // the shaking blade edge and its collision geometry remain identical.
                var shakeTime = _controlStateTime - PrimaryChargeSeconds;
                target += new Vector2(
                    MathF.Sin(shakeTime * 47f) * 1.35f,
                    MathF.Cos(shakeTime * 39f) * 0.65f);
            }

            var handError = target - Position;
            var handDistance = handError.Length();
            if (handDistance > MaximumGripDistance)
                target = Position + handError / handDistance * MaximumGripDistance;
            var (gripSpring, gripDamping) = ControlState switch
            {
                CleaverControlState.Windup => (162f, 22f),
                CleaverControlState.Swing when ArsenalVisualVariant == 7 => (210f, 20f),
                CleaverControlState.Swing when _primaryActionSwing => (210f, 6f),
                CleaverControlState.Impact => (36f, 18f),
                CleaverControlState.Recovery when ArsenalVisualVariant == 7 => (105f, 18f),
                CleaverControlState.Recovery when _primaryActionSwing => (230f, 13f),
                _ => (148f, 20f)
            };
            var gripAcceleration = (target - Position) * gripSpring - _gripVelocity * gripDamping;
            if (ControlState == CleaverControlState.Swing &&
                ArsenalVisualVariant != 7 &&
                _controlStateTime <= PrimarySwingDriveSeconds)
            {
                var swingDrive = 5000f + _windupStrength * 3500f;
                gripAcceleration += _chopDirection * swingDrive;
            }
            _gripVelocity += gripAcceleration * dt;
            var gripSpeed = _gripVelocity.Length();
            var maximumGripSpeed = ArsenalVisualVariant == 7 ? 760f : 1800f;
            if (gripSpeed > maximumGripSpeed) _gripVelocity *= maximumGripSpeed / gripSpeed;
            _previousPosition = Position;
            Position += _gripVelocity * dt;
            velocity = Position - _previousPosition;

            var swingPose = UsesCleaverSwing;
            var targetAngle = swingPose ? ControlState switch
            {
                CleaverControlState.Windup when ArsenalVisualVariant == 7 =>
                    SledgeOverheadAngle + (fullChargeShake
                        ? MathF.Sin((_controlStateTime - PrimaryChargeSeconds) * 31f) * 0.025f
                        : 0f),
                CleaverControlState.Windup => _baseRotationAngle + (fullChargeShake
                    ? MathF.Sin((_controlStateTime - PrimaryChargeSeconds) * 31f) * 0.055f
                    : 0f),
                CleaverControlState.Swing => MathF.Atan2(_chopDirection.Y, _chopDirection.X) - MathF.PI * 0.5f,
                _ => _baseRotationAngle
            } : _baseRotationAngle;
            var (rotationSpring, rotationDamping) = ControlState switch
            {
                CleaverControlState.Windup => (58f, 13f),
                CleaverControlState.Swing when ArsenalVisualVariant == 7 => (74f, 9f),
                CleaverControlState.Swing when _primaryActionSwing &&
                    _controlStateTime <= PrimarySwingDriveSeconds => (220f, 3.5f),
                CleaverControlState.Impact => (0f, 25f),
                CleaverControlState.Recovery when ArsenalVisualVariant == 7 => (92f, 17f),
                CleaverControlState.Recovery when _primaryActionSwing => (260f, 10f),
                _ => (22f, 9f)
            };
            _previousAngle = Angle;
            if (ArsenalVisualVariant == 7 && ControlState == CleaverControlState.Swing)
            {
                // The hammer owns a deterministic overhead-to-ground arc. Its old
                // spring-driven rotation could lose energy or choose a partial arc,
                // producing the weak "baby swing" despite a charged release.
                var slamProgress = Math.Clamp(_controlStateTime / SledgeSlamSeconds, 0f, 1f);
                var acceleratedProgress = slamProgress * slamProgress;
                // The head begins directly north of the grip and completes a
                // clean quarter-circle to west (or east after the RMB toggle).
                var signedArc = _sledgeSwingRight
                    ? MathF.PI * 0.5f
                    : -MathF.PI * 0.5f;
                Angle = SledgeOverheadAngle + signedArc * acceleratedProgress;
                _angularVelocity = signedArc * 2f * slamProgress / SledgeSlamSeconds;
                var terminalAngle = _sledgeSwingRight ? MathF.PI : SledgeGroundAngle;
                var groundHeadBottom = MathF.Max(
                    Rotate(LocalEdgeStart, terminalAngle).Y,
                    Rotate(LocalEdgeEnd, terminalAngle).Y);
                var groundGripY = _sledgeGroundSurfaceY - groundHeadBottom;
                var slamTarget = new Vector2(
                    _sledgeSlamStartPosition.X,
                    MathF.Max(_sledgeSlamStartPosition.Y, groundGripY));
                Position = Vector2.Lerp(
                    _sledgeSlamStartPosition, slamTarget, acceleratedProgress);
                _gripVelocity = (Position - _previousPosition) / MathF.Max(dt, 0.0001f);
            }
            else if (ArsenalVisualVariant == 7 &&
                     ControlState == CleaverControlState.Impact)
            {
                Position = _sledgeImpactGripPosition;
                Angle = _sledgeImpactAngle;
                _gripVelocity = Vector2.Zero;
                _angularVelocity = 0f;
            }
            else if (ArsenalVisualVariant == 7 &&
                     ControlState == CleaverControlState.Recovery)
            {
                var liftTarget = _sledgeRecoveryStartPosition -
                                 Vector2.UnitY * SledgeLiftDistance;
                if (_controlStateTime <= SledgeLiftSeconds)
                {
                    var lift = Math.Clamp(
                        _controlStateTime / SledgeLiftSeconds,
                        0f,
                        1f);
                    var easedLift = lift * lift * (3f - 2f * lift);
                    Position = Vector2.Lerp(
                        _sledgeRecoveryStartPosition,
                        liftTarget,
                        easedLift);
                    Angle = _sledgeImpactAngle;
                }
                else
                {
                    var recovery = Math.Clamp(
                        (_controlStateTime - SledgeLiftSeconds) /
                        SledgeReturnSeconds,
                        0f,
                        1f);
                    var eased = recovery * recovery * (3f - 2f * recovery);
                    Position = Vector2.Lerp(liftTarget, target, eased);
                    Angle = _sledgeImpactAngle +
                            ShortestAngle(
                                _sledgeImpactAngle,
                                _baseRotationAngle) *
                            eased;
                }
                _gripVelocity = (Position - _previousPosition) / MathF.Max(dt, 0.0001f);
                _angularVelocity = 0f;
            }
            else if (ArsenalVisualVariant == 8)
            {
                Angle = _baseRotationAngle;
                _angularVelocity = 0f;
            }
            else if (ArsenalVisualVariant == 12 && _arsenalPrimaryHeld)
            {
                Angle += 11.5f * dt;
                _angularVelocity = 11.5f;
            }
            else
            {
                var torque = ShortestAngle(Angle, targetAngle) * rotationSpring -
                             _angularVelocity * rotationDamping;
                var maximumAngularSpeed = ArsenalVisualVariant == 7 ? 11f : 38f;
                _angularVelocity = Math.Clamp(_angularVelocity + torque * dt,
                    -maximumAngularSpeed, maximumAngularSpeed);
                Angle += _angularVelocity * dt;
            }
            _lastGrabTarget = _grabTarget;
        }
        else
        {
            velocity = (Position - _previousPosition) * 0.9985f;
            _previousPosition = Position;
            Position += velocity + gravity * (dt * dt);
            Angle += _angularVelocity * dt;
            _angularVelocity *= 0.996f;
        }

        ResolveTubeGlass(tubeFeed, dt);
        var displacement = Position - _previousPosition;
        ResolveBlobContacts(dt, bodies, displacement, tubeFeed);

        foreach (var conveyor in conveyors)
        {
            if (Position.X < conveyor.Position.X - 64f ||
                Position.X > conveyor.Position.X + conveyor.Width + 64f)
                continue;
            var beltTop = conveyor.Position.Y;
            var sledgeFaceY = MathF.Max(BladeEdgeStart.Y, BladeEdgeEnd.Y);
            var lowestY = ArsenalVisualVariant == 7
                ? sledgeFaceY
                : MathF.Max(
                    MathF.Max(HandleStart.Y + 7f, HandleEnd.Y + 7f),
                    MathF.Max(BladeCoreStart.Y + 12f, BladeCoreEnd.Y + 12f));
            if (lowestY < beltTop || lowestY > beltTop + 72f) continue;

            var landing = velocity.Y > 0.08f;
            var headLowestY = ArsenalVisualVariant == 7
                ? sledgeFaceY
                : MathF.Max(BladeCoreStart.Y + 12f, BladeCoreEnd.Y + 12f);
            var sledgeGroundSlam = ArsenalVisualVariant == 7 &&
                                   ControlState == CleaverControlState.Swing &&
                                   _controlStateTime >= SledgeSlamSeconds * 0.55f &&
                                   headLowestY >= beltTop;
            var contactX = ArsenalVisualVariant == 7
                ? (BladeCoreStart.X + BladeCoreEnd.X) * 0.5f
                : BladeCoreStart.Y >= BladeCoreEnd.Y ? BladeCoreStart.X : BladeCoreEnd.X;
            if (HandleStart.Y + 7f > MathF.Max(BladeCoreStart.Y + 12f, BladeCoreEnd.Y + 12f))
                contactX = HandleStart.X;
            Position = new Vector2(Position.X, Position.Y - (lowestY - beltTop));
            if (IsGrabbed)
            {
                // The hand remains responsive above the work surface, but the physical
                // cleaver geometry is never allowed to follow the cursor through it.
                _gripVelocity.Y = MathF.Min(0f, _gripVelocity.Y);
                _previousPosition = Position - new Vector2(velocity.X, MathF.Min(0f, velocity.Y));
                if (sledgeGroundSlam)
                {
                    var crushed = CrushSledgeAtSurface(
                        bodies, contactX, beltTop, dt, _windupStrength);
                    EnterSledgeImpact(
                        new Vector2(contactX, beltTop),
                        _windupStrength,
                        crushed,
                        bodies,
                        granular,
                        dt);
                }
                break;
            }
            var corrected = new Vector2(
                velocity.X + (conveyor.Speed * dt - velocity.X) * 0.24f,
                -MathF.Abs(velocity.Y) * 0.24f);
            _previousPosition = Position - corrected;
            if (landing)
            {
                var impactSpeed = velocity.Y / MathF.Max(dt, 0.0001f);
                var lever = Math.Clamp(contactX - Position.X, -56f, 56f);
                _angularVelocity += Math.Clamp(-lever * impactSpeed * 0.00075f, -2.1f, 2.1f);
            }
            // Gravity acting through the offset contact point makes a dropped cleaver
            // settle broad-side-down. The belt supplies linear transport, not an
            // endless wheel torque, so it can visibly tip over and then ride flat.
            var restingAngle = MathF.Round(Angle / MathF.PI) * MathF.PI;
            var settleError = ShortestAngle(Angle, restingAngle);
            _angularVelocity += Math.Clamp(settleError * 26f - _angularVelocity * 7f,
                -18f, 18f) * dt;
            _angularVelocity *= MathF.Exp(-2.2f * dt);
            break;
        }

        if (ArsenalVisualVariant == 7 &&
            ControlState == CleaverControlState.Swing &&
            _sledgeGroundInitialized &&
            _controlStateTime >= SledgeSlamSeconds)
        {
            var impactX = (BladeCoreStart.X + BladeCoreEnd.X) * 0.5f;
            var crushed = CrushSledgeAtSurface(
                bodies, impactX, _sledgeGroundSurfaceY, dt, _windupStrength);
            EnterSledgeImpact(
                new Vector2(impactX, _sledgeGroundSurfaceY),
                _windupStrength,
                crushed,
                bodies,
                granular,
                dt);
        }

        if (_gloveStrikeAge >= 0f)
            UpdateGloveStrike(dt, bodies, tubeFeed);

        if (Position.X < -54f || Position.X > worldWidth + 54f || Position.Y > worldHeight + 48f)
            BeginRespawn();
    }

    private bool UsesCleaverSwing => ArsenalVisualVariant is -1 or 0 or 7;
    private bool UsesBarrelOrientation =>
        ArsenalVisualVariant is 1 or 2 or 3 or 4 or 5 or 6 or
        13 or 14 or 15 or 16 or 17 or 18 or 19 or 20;

    private void ResetArsenalPrimary()
    {
        _arsenalPrimaryHeld = false;
        _arsenalTriggerPending = false;
        _arsenalPrimaryTime = 0f;
        _slingshotBody = null;
        _slingshotCharge = 0f;
        _grenadeTrajectory.Clear();
    }

    private void UpdateArsenalPrimary(float dt, IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed)
    {
        _arsenalAimDirection = ResolveArsenalAim(bodies, tubeFeed);
        if (_arsenalPrimaryHeld) _arsenalPrimaryTime += dt;

        switch (ArsenalVisualVariant)
        {
            case 0: // lightsaber: LMB swings add a wider sweep to its passive contact cutting
                if (_saberIgnited && ControlState == CleaverControlState.Swing &&
                    _arsenalFireCooldown <= 0f)
                {
                    ApplySaberSweep(bodies, tubeFeed, dt);
                    _arsenalFireCooldown = 0.035f;
                }
                break;
            case 1: // nail gun
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var direction = LiveBarrelDirection;
                    SpawnProjectile(ArsenalProjectileKind.Nail,
                        LiveMuzzlePosition,
                        direction * 690f, 4f, penetration: 2);
                    ApplyFirearmRecoil(direction, 150f, 4.2f);
                    _arsenalFireCooldown = 0.62f;
                }
                _arsenalTriggerPending = false;
                break;
            case 2: // shotgun
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var barrelDirection = LiveBarrelDirection;
                    var muzzle = LiveMuzzlePosition;
                    for (var pellet = -3; pellet <= 3; pellet++)
                    {
                        var direction = Rotate(barrelDirection, pellet * 0.075f);
                        SpawnProjectile(ArsenalProjectileKind.ShotgunPellet,
                            muzzle, direction * (650f + (3 - MathF.Abs(pellet)) * 12f),
                            4f, power: 0.78f, penetration: 1);
                    }
                    ApplyFirearmRecoil(barrelDirection, 430f, 8.5f);
                    _arsenalFireCooldown = 0.72f;
                }
                _arsenalTriggerPending = false;
                break;
            case 3: // magnum
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var charge = Math.Clamp(_arsenalPrimaryTime / 0.55f, 0.25f, 1f);
                    var direction = LiveBarrelDirection;
                    SpawnProjectile(ArsenalProjectileKind.MagnumBullet,
                        LiveMuzzlePosition,
                        direction * (860f + charge * 240f),
                        4f, power: 0.55f + charge * 0.65f, penetration: 4);
                    ApplyFirearmRecoil(direction, 240f + charge * 190f, 5f + charge * 4f);
                    _arsenalFireCooldown = 0.48f;
                }
                _arsenalTriggerPending = false;
                break;
            case 4: // SMG
                if ((_arsenalPrimaryHeld || _arsenalTriggerPending) && _arsenalFireCooldown <= 0f)
                {
                    var climb = Math.Clamp(_arsenalPrimaryTime * 0.018f, 0f, 0.06f);
                    var barrelDirection = LiveBarrelDirection;
                    var direction = Rotate(barrelDirection, climb);
                    SpawnProjectile(ArsenalProjectileKind.SmgBullet,
                        LiveMuzzlePosition, direction * 790f,
                        4f, power: 0.62f, penetration: 2);
                    ApplyFirearmRecoil(barrelDirection, 105f, 2.8f);
                    _arsenalFireCooldown = 0.082f;
                }
                _arsenalTriggerPending = false;
                break;
            case 5: // spinning blade shooter
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var charge = Math.Clamp(_arsenalPrimaryTime / 0.8f, 0.25f, 1f);
                    var direction = LiveBarrelDirection;
                    SpawnProjectile(ArsenalProjectileKind.SawBlade,
                        LiveMuzzlePosition,
                        direction * (410f + charge * 330f),
                        4f, penetration: 3);
                    _arsenalFireCooldown = 0.4f;
                }
                _arsenalTriggerPending = false;
                break;
            case 6: // wood chipper vacuum
                if (_arsenalPrimaryHeld) ApplyVacuum(bodies, tubeFeed, dt);
                break;
            case 8: // blob slingshot
                UpdateSlingshot(bodies, tubeFeed, dt);
                break;
            case 10: // single boxing glove
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    BeginGloveStrike();
                    _arsenalFireCooldown = 0.28f;
                }
                _arsenalTriggerPending = false;
                break;
            case 11: // grenade
                if (_arsenalPrimaryHeld)
                {
                    var aimDrag = _actionPointer - _grenadeAimAnchor;
                    if (aimDrag.LengthSquared() > 8f * 8f)
                    {
                        _grenadeThrowDirection = Vector2.Normalize(aimDrag);
                        _grenadeThrowSpeed = 320f + Math.Clamp(aimDrag.Length() / 170f, 0f, 1f) * 430f;
                    }
                }
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    SpawnProjectile(ArsenalProjectileKind.Grenade,
                        Position + _grenadeThrowDirection * 18f,
                        _grenadeThrowDirection * _grenadeThrowSpeed + _gripVelocity * 0.18f,
                        1.8f, penetration: int.MaxValue);
                    _arsenalFireCooldown = 0.35f;
                    ReturnToHolster();
                }
                _arsenalTriggerPending = false;
                break;
            case 12: // whirlwind axe: the held trigger is the attack
                if (_arsenalPrimaryHeld)
                    ApplyWhirlwindSweep(bodies, tubeFeed, dt);
                break;
            case 13: // miniature black-hole projector
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var direction = LiveBarrelDirection;
                    SpawnProjectile(ArsenalProjectileKind.BlackHole,
                        LiveMuzzlePosition, direction * 285f, 2.4f,
                        power: 1f, penetration: int.MaxValue);
                    ApplyFirearmRecoil(direction, 180f, 4.5f);
                    _arsenalFireCooldown = 0.75f;
                }
                _arsenalTriggerPending = false;
                break;
            case 14: // rat launcher
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var direction = LiveBarrelDirection;
                    SpawnProjectile(ArsenalProjectileKind.Rat,
                        LiveMuzzlePosition, direction * 420f - Vector2.UnitY * 55f,
                        5f, penetration: 1);
                    ApplyFirearmRecoil(direction, 75f, 1.5f);
                    _arsenalFireCooldown = 0.34f;
                }
                _arsenalTriggerPending = false;
                break;
            case 15: // auto-aim enlarging gun
                if (_arsenalPrimaryHeld && _arsenalFireCooldown <= 0f)
                {
                    var target = FindNearestTarget(bodies, tubeFeed, Position, 420f);
                    if (target is not null)
                    {
                        var direction = Vector2.Normalize(target.Center - LiveMuzzlePosition);
                        SpawnProjectile(ArsenalProjectileKind.GrowthPulse,
                            LiveMuzzlePosition, direction * 610f, 1.1f,
                            power: 1f, penetration: 1);
                        ApplyFirearmRecoil(direction, 28f, 0.6f);
                    }
                    _arsenalFireCooldown = 0.11f;
                }
                break;
            case 16: // flamethrower
                if (_arsenalPrimaryHeld && _arsenalFireCooldown <= 0f)
                {
                    var spread = (NextArsenal01() - 0.5f) * 0.20f;
                    var direction = Rotate(LiveBarrelDirection, spread);
                    SpawnProjectile(ArsenalProjectileKind.Flame,
                        LiveMuzzlePosition, direction * (350f + NextArsenal01() * 120f),
                        4.5f, power: 0.7f, penetration: 1);
                    ApplyFirearmRecoil(direction, 16f, 0.25f);
                    _arsenalFireCooldown = 0.045f;
                }
                break;
            case 17: // freeze ray projectile
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var direction = LiveBarrelDirection;
                    SpawnProjectile(ArsenalProjectileKind.IceBolt,
                        LiveMuzzlePosition, direction * 560f, 2.2f,
                        penetration: 1);
                    ApplyFirearmRecoil(direction, 90f, 2f);
                    _arsenalFireCooldown = 0.42f;
                }
                _arsenalTriggerPending = false;
                break;
            case 18: // arcing lightning seed
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var target = FindNearestTarget(bodies, tubeFeed, Position, 460f);
                    var direction = target is null
                        ? LiveBarrelDirection
                        : Vector2.Normalize(target.Center - LiveMuzzlePosition);
                    SpawnProjectile(ArsenalProjectileKind.LightningSeed,
                        LiveMuzzlePosition, direction * 720f, 1.4f,
                        penetration: 1);
                    ApplyFirearmRecoil(direction, 110f, 3f);
                    _arsenalFireCooldown = 0.38f;
                }
                _arsenalTriggerPending = false;
                break;
            case 19: // acid lobber
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var charge = Math.Clamp(_arsenalPrimaryTime / 0.8f, 0.25f, 1f);
                    var direction = Rotate(
                        LiveBarrelDirection,
                        (NextArsenal01() - 0.5f) * 0.16f);
                    var speedNoise = 0.90f + NextArsenal01() * 0.20f;
                    SpawnProjectile(ArsenalProjectileKind.AcidGlob,
                        LiveMuzzlePosition,
                        direction * ((300f + charge * 260f) * speedNoise) -
                        Vector2.UnitY * (90f + charge * 90f),
                        3.5f, power: charge, penetration: 1);
                    ApplyFirearmRecoil(direction, 90f, 1.8f);
                    _arsenalFireCooldown = 0.52f;
                }
                _arsenalTriggerPending = false;
                break;
            case 20: // crying water doll
                if (_arsenalTriggerPending && _arsenalFireCooldown <= 0f)
                {
                    var direction = LiveBarrelDirection;
                    SpawnProjectile(ArsenalProjectileKind.WaterTear,
                        LiveMuzzlePosition, direction * 330f, 3.2f,
                        power: 1f, penetration: 1);
                    _arsenalFireCooldown = 0.58f;
                }
                _arsenalTriggerPending = false;
                break;
            case 21: // baseball bat and ball
                if (_arsenalTriggerPending)
                {
                    if (_arsenalFireCooldown <= 0f)
                    {
                        if (!_baseballInPlay)
                            LobBaseball();
                        else
                            SwingBaseballBat(bodies, tubeFeed, dt);
                        _arsenalFireCooldown = 0.26f;
                        _arsenalTriggerPending = false;
                    }
                }
                break;
        }
    }

    private Vector2 ResolveArsenalAim(IReadOnlyList<SoftBody> bodies, OverheadTubeFeed? tubeFeed)
    {
        if (ArsenalVisualVariant == 8 && IsDeployed)
        {
            var launchDirection = SlingshotCradlePosition - _grabTarget;
            if (launchDirection.LengthSquared() > 12f * 12f)
                return Vector2.Normalize(launchDirection);
        }
        if (ArsenalVisualVariant == 8) return -Vector2.UnitY;
        if (ArsenalVisualVariant == 11 && _arsenalPrimaryHeld) return _grenadeThrowDirection;
        return BaseAimDirection;
    }

    private void ApplyFirearmRecoil(Vector2 firedDirection, float linearKick, float angularKick)
    {
        _gripVelocity -= firedDirection * linearKick;
        var climbSign = firedDirection.X >= 0f ? -1f : 1f;
        _angularVelocity += climbSign * angularKick;
    }

    private void ApplySaberSweep(IReadOnlyList<SoftBody> bodies, OverheadTubeFeed? tubeFeed, float dt)
    {
        var start = BladeEdgeStart;
        var end = BladeEdgeEnd;
        foreach (var body in bodies)
        {
            if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                DistanceToSegment(body.Center, start, end) > body.Radius + 8f)
                continue;
            var contact = ClosestPoint(body.Center, start, end);
            var broken = body.DamageLine(start, end, 8f, 4.8f, maximumBreaks: 24);
            broken += body.DamageBonds(contact, 12f, 5.5f);
            if (broken > 0) PuncturedThisStep = true;
        }
    }

    private void ApplyWhirlwindSweep(
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed,
        float dt)
    {
        var start = BladeEdgeStart;
        var end = BladeEdgeEnd;
        var edgeCenter = (start + end) * 0.5f;
        var outward = edgeCenter - Position;
        if (outward.LengthSquared() < 0.001f) outward = -Vector2.UnitX;
        else outward = Vector2.Normalize(outward);
        foreach (var body in bodies)
        {
            if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                _damageCooldowns.ContainsKey(body.ParentId) ||
                DistanceToSegment(body.Center, start, end) > body.Radius + 13f)
                continue;
            var contact = ClosestPoint(body.Center, start, end);
            body.DamageLine(start, end, 12f, 3.8f, maximumBreaks: 12);
            body.DamageBonds(contact, 18f, 4.6f);
            body.AddLocalizedImpulse(contact, 34f, outward * 235f, dt);
            body.RegisterHitReaction(1f, 0.13f);
            _damageCooldowns[body.ParentId] = 0.085f;
            PuncturedThisStep = true;
        }
    }

    private void ApplyVacuum(IReadOnlyList<SoftBody> bodies, OverheadTubeFeed? tubeFeed, float dt)
    {
        foreach (var body in bodies)
        {
            if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true) continue;
            var toIntake = Position - body.Center;
            var distance = toIntake.Length();
            if (distance <= 0.001f || distance > 245f) continue;
            var direction = toIntake / distance;
            var cone = Vector2.Dot(direction, -_arsenalAimDirection);
            if (cone < 0.28f) continue;
            body.AddImpulse(direction * (2.2f + (1f - distance / 245f) * 5.8f), dt);
            if (distance < 58f && _arsenalFireCooldown <= 0f)
            {
                body.DamageLine(Position, body.Center, 10f, 0.82f, maximumBreaks: 3);
                _arsenalFireCooldown = 0.09f;
                PuncturedThisStep = true;
            }
        }
    }

    private void UpdateSlingshot(IReadOnlyList<SoftBody> bodies, OverheadTubeFeed? tubeFeed, float dt)
    {
        if (IsDeployed && _slingshotBody is null)
        {
            foreach (var body in bodies)
            {
                if (!body.IsGrabbed || body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                    Vector2.DistanceSquared(body.Center, SlingshotCradlePosition) >
                    (body.Radius + 26f) * (body.Radius + 26f))
                    continue;
                body.EndGrab(Vector2.Zero, dt);
                _slingshotBody = body;
                _slingshotCharge = 0f;
                break;
            }
        }

        if (IsDeployed && _slingshotBody is { } resting && !_arsenalPrimaryHeld &&
            !_arsenalTriggerPending)
            PullBodyToward(resting, SlingshotCradlePosition, 8.5f, 300f, dt);

        if (_arsenalPrimaryHeld)
        {
            if (_slingshotBody is null)
            {
                var best = 150f * 150f;
                foreach (var body in bodies)
                {
                    if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true) continue;
                    var distance = Vector2.DistanceSquared(Position, body.Center);
                    if (distance >= best) continue;
                    best = distance;
                    _slingshotBody = body;
                }
            }
            if (_slingshotBody is { } loaded)
            {
                var pull = _grabTarget - SlingshotCradlePosition;
                var pullDistance = pull.Length();
                if (pullDistance > 118f) pull *= 118f / pullDistance;
                var cradle = IsDeployed ? SlingshotCradlePosition + pull : Position;
                PullBodyToward(loaded, cradle, 9.5f, 380f, dt);
                _slingshotCharge = IsDeployed
                    ? Math.Clamp(pullDistance / 118f, 0f, 1f)
                    : Math.Clamp(_arsenalPrimaryTime / 1.1f, 0f, 1f);
            }
        }
        else if (_arsenalTriggerPending)
        {
            if (_slingshotBody is { } loaded)
            {
                var launchVelocity = _arsenalAimDirection * (430f + _slingshotCharge * 670f);
                loaded.AddImpulse(launchVelocity - loaded.AverageVelocity(dt), dt);
                LastArsenalActionPosition = loaded.Center;
                ArsenalShotSerial++;
                _launchedSlingshotBody = loaded;
                _slingshotPreviousVelocity = launchVelocity;
                _slingshotLaunchRemaining = 1.35f;
                _slingshotLaunchPower = _slingshotCharge;
            }
            _slingshotBody = null;
            _slingshotCharge = 0f;
            _arsenalTriggerPending = false;
        }
    }

    private static void PullBodyToward(
        SoftBody body, Vector2 target, float spring, float maximumSpeed, float dt)
    {
        var desiredVelocity = (target - body.Center) * spring;
        var speed = desiredVelocity.Length();
        if (speed > maximumSpeed) desiredVelocity *= maximumSpeed / speed;
        body.AddImpulse((desiredVelocity - body.AverageVelocity(dt)) * 0.28f, dt);
    }

    private void UpdateSlingshotImpact(
        float dt, IReadOnlyList<SoftBody> bodies, OverheadTubeFeed? tubeFeed)
    {
        if (_launchedSlingshotBody is not { } launched || _slingshotLaunchRemaining <= 0f) return;
        _slingshotLaunchRemaining -= dt;
        var velocity = launched.AverageVelocity(dt);
        var previousSpeed = _slingshotPreviousVelocity.Length();
        var speed = velocity.Length();
        SoftBody? struckBody = null;
        var struckDistance = float.MaxValue;
        foreach (var body in bodies)
        {
            if (body == launched || body.IsDetachedDebris || tubeFeed?.Contains(body) == true)
                continue;
            var distance = Vector2.DistanceSquared(body.Center, launched.Center);
            var reach = body.Radius + launched.Radius + 12f;
            if (distance >= reach * reach || distance >= struckDistance) continue;
            struckDistance = distance;
            struckBody = body;
        }
        var hitEnvironment = launched.LastTerrainImpact > 260f;
        if (previousSpeed > 360f &&
            (speed < previousSpeed * 0.78f || struckBody is not null || hitEnvironment))
        {
            var direction = _slingshotPreviousVelocity.LengthSquared() > 0.01f
                ? Vector2.Normalize(_slingshotPreviousVelocity)
                : Vector2.UnitX;
            var impactPoint = hitEnvironment
                ? launched.LastTerrainImpactPoint
                : launched.Center + direction * launched.Radius * 0.68f;
            var severity = Math.Clamp(_slingshotLaunchPower, 0.25f, 1f);
            launched.DamageLine(impactPoint - direction * 8f, impactPoint + direction * 8f,
                12f + severity * 13f, 1.6f + severity * 3.8f,
                maximumBreaks: 8 + (int)MathF.Round(severity * 18f));
            launched.DamageBonds(impactPoint, 15f + severity * 12f, 3f + severity * 4f);
            launched.RegisterHitReaction(1.1f + severity, 0.18f);
            if (struckBody is not null)
            {
                var struckPoint = struckBody.Center - direction * struckBody.Radius * 0.72f;
                struckBody.DamageLine(
                    struckPoint - new Vector2(-direction.Y, direction.X) * 10f,
                    struckPoint + new Vector2(-direction.Y, direction.X) * 10f,
                    11f + severity * 11f,
                    1.4f + severity * 3.2f,
                    maximumBreaks: 7 + (int)MathF.Round(severity * 15f));
                struckBody.DamageBonds(
                    struckPoint, 15f + severity * 13f, 2.8f + severity * 4.4f);
                struckBody.AddLocalizedImpulse(
                    struckPoint, 46f, direction * (260f + severity * 390f), dt);
                struckBody.RegisterHitReaction(1.2f + severity, 0.2f);
            }
            PuncturedThisStep = true;
            _slingshotLaunchRemaining = 0f;
            _launchedSlingshotBody = null;
            return;
        }
        _slingshotPreviousVelocity = velocity;
        if (_slingshotLaunchRemaining <= 0f) _launchedSlingshotBody = null;
    }

    private void BeginGloveStrike()
    {
        _gloveStrikeAge = 0f;
        _gloveStrikeCharge = Math.Clamp(_arsenalPrimaryTime / 0.72f, 0.18f, 1f);
        _gloveStrikeDirection = new Vector2(
            _arsenalAimDirection.X >= 0f ? 1f : -1f,
            0f);
        _glovePreviousOffset = Vector2.Zero;
        _gloveHitParentId = -1;
        _gloveBlockedReach = 1f;
        _gloveCarriedBody = null;
        _gloveUppercutFinished = false;
        LastArsenalActionPosition = Position;
        ArsenalShotSerial++;
    }

    private void UpdateGloveStrike(
        float dt,
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed)
    {
        var previousOffset = GloveStrikeOffset;
        _gloveStrikeAge += dt;
        var currentOffset = GloveStrikeOffset;
        var fullCharge = _gloveStrikeCharge >= 0.98f;
        var duration = fullCharge ? 0.34f : 0.24f;
        var phase = Math.Clamp(_gloveStrikeAge / duration, 0f, 1f);
        // The authored glove face sits roughly 48 px forward of the grip after
        // the sprite is rotated into its aim direction. Sweep that visible face,
        // not the hidden piston pivot behind it.
        var previousCenter = Position + previousOffset + _gloveStrikeDirection * 48f;
        var currentCenter = Position + currentOffset + _gloveStrikeDirection * 48f;
        var outward = Vector2.Dot(currentOffset - previousOffset,
            fullCharge
                ? Vector2.Normalize(new Vector2(_gloveStrikeDirection.X * 0.42f, -1f))
                : _gloveStrikeDirection) > 0f;
        if (outward)
        {
            foreach (var body in bodies)
            {
                if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                    body.ParentId == _gloveHitParentId ||
                    DistanceToSegment(body.Center, previousCenter, currentCenter) >
                    body.Radius + 28f)
                    continue;
                var contactPoint = ClosestPoint(body.Center, previousCenter, currentCenter);
                if (!body.ContainsVisiblePoint(contactPoint)) continue;
                var strikeDirection = fullCharge
                    ? Vector2.Normalize(new Vector2(_gloveStrikeDirection.X * 0.32f, -1f))
                    : _gloveStrikeDirection;
                if (fullCharge)
                {
                    _gloveCarriedBody = body;
                    body.AddLocalizedImpulse(
                        contactPoint,
                        44f,
                        new Vector2(_gloveStrikeDirection.X * 170f, -130f),
                        dt);
                    body.DamageBonds(contactPoint, 20f, 1.8f);
                }
                else
                {
                    _gloveBlockedReach = MathF.Min(
                        _gloveBlockedReach,
                        MathF.Max(0.18f, MathF.Sin(phase * MathF.PI)));
                    body.AddLocalizedImpulse(contactPoint, 34f + _gloveStrikeCharge * 18f,
                        strikeDirection * (320f + _gloveStrikeCharge * 420f), dt);
                    body.AddImpulse(strikeDirection * (65f + _gloveStrikeCharge * 105f), dt);
                    body.DamageBonds(contactPoint, 15f + _gloveStrikeCharge * 9f,
                        0.45f + _gloveStrikeCharge * 0.65f);
                }
                _gloveHitParentId = body.ParentId;
                LastArsenalActionPosition = contactPoint;
                PuncturedThisStep = true;
                break;
            }
        }
        if (fullCharge && _gloveCarriedBody is { } carried)
        {
            if (!bodies.Contains(carried))
            {
                _gloveCarriedBody = null;
            }
            else
            {
                var carryPoint = Position + currentOffset + _gloveStrikeDirection * 48f;
                var desiredVelocity = (carryPoint - carried.Center) * 9.5f;
                var desiredSpeed = desiredVelocity.Length();
                if (desiredSpeed > 620f) desiredVelocity *= 620f / desiredSpeed;
                carried.AddImpulse(
                    (desiredVelocity - carried.AverageVelocity(dt)) * 0.42f,
                    dt);
                if (phase >= 0.68f && !_gloveUppercutFinished)
                {
                    carried.AddLocalizedImpulse(
                        carryPoint,
                        62f,
                        new Vector2(_gloveStrikeDirection.X * 210f, -720f),
                        dt);
                    carried.DamageLine(
                        carryPoint - new Vector2(24f, 10f),
                        carryPoint + new Vector2(24f, -22f),
                        16f,
                        7.2f,
                        maximumBreaks: 18);
                    carried.DamageBonds(carryPoint, 34f, 8.4f);
                    carried.RegisterHitReaction(2f, 0.24f);
                    _gloveUppercutFinished = true;
                    PuncturedThisStep = true;
                }
            }
        }
        _glovePreviousOffset = currentOffset;
        if (_gloveStrikeAge >= duration)
        {
            _gloveStrikeAge = -1f;
            _glovePreviousOffset = Vector2.Zero;
            _gloveCarriedBody = null;
            _gloveBlockedReach = 1f;
        }
    }

    private void AddArsenalActionEffect(int variant, Vector2 start, Vector2 end,
        float seconds, float size = 0f)
    {
        if (_arsenalActionEffects.Count >= 24) _arsenalActionEffects.RemoveAt(0);
        _arsenalActionEffects.Add(new ArsenalActionEffect(variant, start, end, seconds, size));
    }

    private void UpdateArsenalActionEffects(float dt)
    {
        for (var index = _arsenalActionEffects.Count - 1; index >= 0; index--)
        {
            var effect = _arsenalActionEffects[index];
            var remaining = effect.RemainingSeconds - dt;
            if (remaining <= 0f)
                _arsenalActionEffects.RemoveAt(index);
            else
                _arsenalActionEffects[index] = effect with { RemainingSeconds = remaining };
        }
    }

    private void SpawnProjectile(ArsenalProjectileKind kind, Vector2 position, Vector2 velocity,
        float seconds, float power = 1f, int penetration = 1)
    {
        if (_arsenalProjectiles.Count >= 64) _arsenalProjectiles.RemoveAt(0);
        var angle = velocity.LengthSquared() > 0.001f
            ? MathF.Atan2(velocity.Y, velocity.X)
            : 0f;
        _arsenalProjectiles.Add(new ArsenalProjectile(kind, position, velocity, angle, seconds,
            power, penetration, -1, false, Vector2.Zero));
        LastArsenalActionPosition = position;
        ArsenalShotSerial++;
    }

    private void UpdateNailPins(float dt)
    {
        for (var index = _nailPins.Count - 1; index >= 0; index--)
        {
            var pin = _nailPins[index];
            var pinnedPoint = pin.Body.Center + pin.BodyOffset;
            var targetPoint = pin.JoinedBody is { } joined
                ? joined.Center + pin.JoinedBodyOffset
                : pin.StaticAnchor;
            var error = targetPoint - pinnedPoint;
            if (error.LengthSquared() > 240f * 240f)
            {
                _nailPins.RemoveAt(index);
                continue;
            }
            var relativeVelocity = pin.JoinedBody is { } targetBody
                ? targetBody.AverageVelocity(dt) - pin.Body.AverageVelocity(dt)
                : -pin.Body.AverageVelocity(dt);
            var impulse = error * 8.5f + relativeVelocity * 0.52f;
            var impulseLength = impulse.Length();
            if (impulseLength > 220f) impulse *= 220f / impulseLength;
            if (pin.JoinedBody is { } joinedBody)
            {
                pin.Body.AddLocalizedImpulse(pinnedPoint, 28f, impulse * 0.55f, dt);
                joinedBody.AddLocalizedImpulse(targetPoint, 28f, -impulse * 0.55f, dt);
            }
            else
                pin.Body.AddImpulse(impulse, dt);
        }
    }

    private void UpdatePikePins(float dt)
    {
        for (var index = _pikePins.Count - 1; index >= 0; index--)
        {
            var pin = _pikePins[index];
            if (!IsDeployed || ArsenalVisualVariant != 9 ||
                pin.Body.IsGrabbed || pin.Body.PhysicalParticleCount <= 3)
            {
                _pikePins.RemoveAt(index);
                continue;
            }
            if (!pin.Body.IsPhysicalParticle(pin.ParticleIndex))
            {
                _pikePins.RemoveAt(index);
                continue;
            }
            ref var particle = ref pin.Body.Particles[pin.ParticleIndex];
            var pinnedPoint = particle.Position;
            var error = pin.StaticAnchor - pinnedPoint;
            if (error.LengthSquared() > 190f * 190f)
            {
                _pikePins.RemoveAt(index);
                continue;
            }
            var velocity = (particle.Position - particle.PreviousPosition) /
                           MathF.Max(dt, 0.0001f);
            var impulse = error * 18f - velocity * 0.78f;
            var length = impulse.Length();
            if (length > 360f) impulse *= 360f / length;
            particle.Position += error * 0.82f;
            particle.PreviousPosition = particle.Position;
            particle.Contacting = true;
            particle.ContactMemory = 8;
            pin.Body.AddLocalizedImpulse(particle.Position, 26f, impulse, dt);
            pin.Body.Wake();
        }
    }

    private void UpdateArsenalProjectiles(float dt, Vector2 gravity,
        IReadOnlyList<ConveyorBelt> conveyors, IReadOnlyList<SoftBody> bodies,
        float worldWidth, float worldHeight, OverheadTubeFeed? tubeFeed,
        DestructibleGrid? grid, GranularMaterialSystem? granular)
    {
        for (var index = _arsenalProjectiles.Count - 1; index >= 0; index--)
        {
            var projectile = _arsenalProjectiles[index];
            var remaining = projectile.RemainingSeconds - dt;
            if (projectile.Stuck)
            {
                var stuckPosition = projectile.Position;
                if (projectile.LastHitParentId >= 0)
                {
                    var attachedBody = FindClosestParentBody(
                        bodies, projectile.LastHitParentId, projectile.Position);
                    if (attachedBody is null)
                    {
                        _arsenalProjectiles.RemoveAt(index);
                        continue;
                    }
                    stuckPosition = attachedBody.Center + projectile.AttachmentOffset;
                }
                if (remaining <= 0f)
                {
                    _arsenalProjectiles.RemoveAt(index);
                    continue;
                }
                _arsenalProjectiles[index] = projectile with
                {
                    Position = stuckPosition,
                    Velocity = Vector2.Zero,
                    RemainingSeconds = remaining
                };
                continue;
            }

            var previous = projectile.Position;
            var velocity = projectile.Velocity;
            var isGrenade = projectile.Kind == ArsenalProjectileKind.Grenade;
            var isSaw = projectile.Kind == ArsenalProjectileKind.SawBlade;
            var isBlackHole = projectile.Kind == ArsenalProjectileKind.BlackHole;
            var isBaseball = projectile.Kind == ArsenalProjectileKind.Baseball;
            var isAcid = projectile.Kind == ArsenalProjectileKind.AcidGlob;
            var isRat = projectile.Kind == ArsenalProjectileKind.Rat;
            var isFlame = projectile.Kind == ArsenalProjectileKind.Flame;
            if (isFlame && _flameTrailCooldown <= 0f)
            {
                AddFlamePatch(previous, velocity * 0.08f, null, -1, 0.42f);
                _flameTrailCooldown = 0.026f;
            }
            if (isGrenade || isAcid || isBaseball || isRat)
                velocity += gravity * (dt * (isBaseball ? 0.82f : isRat ? 0.58f : 0.72f));
            if (isBlackHole)
            {
                velocity *= MathF.Exp(-0.72f * dt);
                ApplyBlackHole(projectile.Position, bodies, tubeFeed, granular, dt);
            }
            var position = projectile.Position + velocity * dt;
            var angle = isGrenade
                ? projectile.Angle + 7f * dt
                : velocity.LengthSquared() > 0.001f
                    ? MathF.Atan2(velocity.Y, velocity.X)
                    : projectile.Angle;
            var penetration = projectile.PenetrationRemaining;
            var lastHitParentId = projectile.LastHitParentId;
            var attachmentOffset = projectile.AttachmentOffset;
            var radius = ProjectileRadius(projectile.Kind);

            var restitution = isGrenade ? 0.46f :
                isSaw ? 0.34f : isBaseball ? 0.72f : isRat ? 0.38f : 0f;
            var hitEnvironment = ResolveProjectileEnvironment(previous, ref position, ref velocity,
                radius, restitution, dt,
                conveyors, worldWidth, worldHeight, grid, out _, out var environmentNormal);
            if (hitEnvironment && !isGrenade)
            {
                if (isBlackHole)
                {
                    velocity = Vector2.Zero;
                }
                else if (isBaseball)
                {
                    velocity *= 0.82f;
                }
                else if (isAcid)
                {
                    EmitAcidBurst(
                        granular,
                        position,
                        velocity,
                        environmentNormal,
                        dt,
                        projectile.Power);
                    _arsenalProjectiles.RemoveAt(index);
                    continue;
                }
                else if (isRat)
                {
                    SpawnRatAgent(position, velocity * 0.25f, bodies, tubeFeed);
                    _arsenalProjectiles.RemoveAt(index);
                    continue;
                }
                else if (isFlame)
                {
                    ConveyorBelt? surfaceConveyor = null;
                    foreach (var conveyor in conveyors)
                    {
                        if (position.X < conveyor.Position.X - radius ||
                            position.X > conveyor.Position.X + conveyor.Width + radius ||
                            MathF.Abs(position.Y - conveyor.Position.Y) > radius + 8f)
                            continue;
                        surfaceConveyor = conveyor;
                        break;
                    }
                    AddSurfaceFlame(position, environmentNormal, surfaceConveyor, 4.2f);
                    _arsenalProjectiles.RemoveAt(index);
                    continue;
                }
                else
                {
                if (projectile.Kind == ArsenalProjectileKind.Nail && lastHitParentId >= 0)
                {
                    var pinnedBody = FindClosestParentBody(bodies, lastHitParentId, position);
                    if (pinnedBody is not null)
                    {
                        if (_nailPins.Count >= 16) _nailPins.RemoveAt(0);
                        _nailPins.Add(new NailPin(
                            pinnedBody, attachmentOffset, position, null, Vector2.Zero));
                    }
                }
                else if (isSaw)
                {
                    _arsenalProjectiles[index] = projectile with
                    {
                        Position = position,
                        Velocity = Vector2.Zero,
                        Angle = angle,
                        RemainingSeconds = MathF.Max(remaining, 3f),
                        Stuck = true,
                        LastHitParentId = -1
                    };
                    continue;
                }
                _arsenalProjectiles.RemoveAt(index);
                continue;
                }
            }

            if (isSaw)
            {
                SoftBody? stuckBody = null;
                foreach (var body in bodies)
                {
                    if (body.ParentId == lastHitParentId || body.IsDetachedDebris ||
                        tubeFeed?.Contains(body) == true ||
                        DistanceToSegment(body.Center, previous, position) > body.Radius + 12f)
                        continue;
                    var impactPoint = ClosestPoint(body.Center, previous, position);
                    if (!body.ContainsVisiblePoint(impactPoint)) continue;
                    // A saw disc removes the narrow band its teeth physically
                    // traverse. Bond-only damage left intact material spanning
                    // the visible projectile path, so the blade appeared to phase
                    // through a blob without actually slicing it.
                    var removed = body.ExciseSweptBand(
                        previous - Vector2.Normalize(velocity) * 8f,
                        position + Vector2.Normalize(velocity) * 8f,
                        10.5f,
                        maximumParticles: 7);
                    var broken = body.DamageLine(previous, position, 12f,
                        6.2f, maximumBreaks: 26);
                    broken += body.DamageBonds(impactPoint, 17f, 6.8f);
                    if (velocity.LengthSquared() > 0.01f)
                        body.AddImpulse(Vector2.Normalize(velocity) * 42f - Vector2.UnitY * 12f, dt);
                    velocity *= 0.90f;
                    penetration--;
                    lastHitParentId = body.ParentId;
                    attachmentOffset = impactPoint - body.Center;
                    if (broken > 0 || removed > 0) PuncturedThisStep = true;
                    if (penetration <= 0)
                    {
                        stuckBody = body;
                        position = impactPoint;
                        break;
                    }
                }
                if (stuckBody is not null)
                {
                    _arsenalProjectiles[index] = projectile with
                    {
                        Position = position,
                        Velocity = Vector2.Zero,
                        Angle = angle,
                        RemainingSeconds = MathF.Max(remaining, 3f),
                        PenetrationRemaining = 0,
                        LastHitParentId = stuckBody.ParentId,
                        Stuck = true,
                        AttachmentOffset = attachmentOffset
                    };
                    continue;
                }
            }
            else if (isGrenade)
            {
                foreach (var body in bodies)
                {
                    if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                        DistanceToSegment(body.Center, previous, position) > body.Radius + radius)
                        continue;
                    var normal = position - body.Center;
                    if (normal.LengthSquared() < 0.001f) normal = -Vector2.UnitY;
                    else normal = Vector2.Normalize(normal);
                    var intoBody = Vector2.Dot(velocity, normal);
                    if (intoBody < 0f) velocity -= normal * intoBody * 1.55f;
                    velocity *= 0.84f;
                    position = previous;
                    body.AddImpulse(-normal * MathF.Min(12f, velocity.Length() * 0.018f), dt);
                    break;
                }
            }
            else if (isBlackHole)
            {
                // Its radial field is the damage mechanic; it passes through
                // ordinary bodies instead of degenerating into a bullet hit.
            }
            else if (projectile.Kind is
                     ArsenalProjectileKind.Rat or ArsenalProjectileKind.GrowthPulse or
                     ArsenalProjectileKind.Flame or ArsenalProjectileKind.IceBolt or
                     ArsenalProjectileKind.LightningSeed or ArsenalProjectileKind.AcidGlob or
                     ArsenalProjectileKind.WaterTear or ArsenalProjectileKind.Baseball)
            {
                var consumed = ResolveSpecialProjectileBody(
                    projectile.Kind, previous, position, ref velocity, projectile.Power,
                    bodies, tubeFeed, granular, dt);
                if (consumed)
                {
                    _arsenalProjectiles.RemoveAt(index);
                    continue;
                }
            }
            else
            {
                foreach (var body in bodies)
                {
                    if (body.ParentId == lastHitParentId || body.IsDetachedDebris ||
                        tubeFeed?.Contains(body) == true ||
                        DistanceToSegment(body.Center, previous, position) > body.Radius + radius + 5f)
                        continue;
                    var impactPoint = ClosestPoint(body.Center, previous, position);
                    if (!body.ContainsVisiblePoint(impactPoint)) continue;
                    var previousHitParentId = lastHitParentId;
                    var previousAttachmentOffset = attachmentOffset;
                    var (thickness, damage, _, impulse) = ProjectileDamage(projectile.Kind,
                        projectile.Power);
                    var broken = body.DamageBonds(impactPoint, thickness * 1.8f, damage);
                    if (velocity.LengthSquared() > 0.01f)
                    {
                        var direction = Vector2.Normalize(velocity);
                        var lift = projectile.Kind switch
                        {
                            ArsenalProjectileKind.Nail => 72f,
                            ArsenalProjectileKind.MagnumBullet => 38f,
                            ArsenalProjectileKind.ShotgunPellet => 14f,
                            _ => 5f
                        };
                        body.AddImpulse(direction * impulse - Vector2.UnitY * lift, dt);
                    }
                    if (broken > 0) PuncturedThisStep = true;
                    if (projectile.Kind == ArsenalProjectileKind.Nail &&
                        previousHitParentId >= 0 &&
                        previousHitParentId != body.ParentId)
                    {
                        var firstBody = FindClosestParentBody(
                            bodies, previousHitParentId, impactPoint);
                        if (firstBody is not null)
                        {
                            if (_nailPins.Count >= 16) _nailPins.RemoveAt(0);
                            _nailPins.Add(new NailPin(
                                firstBody,
                                previousAttachmentOffset,
                                Vector2.Zero,
                                body,
                                impactPoint - body.Center));
                        }
                    }
                    penetration--;
                    lastHitParentId = body.ParentId;
                    attachmentOffset = impactPoint - body.Center;
                    velocity *= projectile.Kind switch
                    {
                        ArsenalProjectileKind.MagnumBullet => 0.82f,
                        ArsenalProjectileKind.Nail => 0.78f,
                        _ => 0.68f
                    };
                    if (penetration <= 0) break;
                }
                if (penetration <= 0)
                {
                    _arsenalProjectiles.RemoveAt(index);
                    continue;
                }
            }

            if (remaining <= 0f)
            {
                if (isGrenade) ExplodeGrenade(position, bodies, tubeFeed, dt);
                if (isAcid)
                    EmitAcidBurst(
                        granular,
                        position,
                        velocity,
                        -Vector2.UnitY,
                        dt,
                        projectile.Power);
                if (isBaseball) _baseballInPlay = false;
                _arsenalProjectiles.RemoveAt(index);
                continue;
            }
            if (isBaseball &&
                (position.X < -40f || position.X > worldWidth + 40f ||
                 position.Y < -80f || position.Y > worldHeight + 80f))
            {
                _baseballInPlay = false;
                _arsenalProjectiles.RemoveAt(index);
                continue;
            }
            _arsenalProjectiles[index] = projectile with
            {
                Position = position,
                Velocity = velocity,
                Angle = angle,
                RemainingSeconds = remaining,
                PenetrationRemaining = penetration,
                LastHitParentId = lastHitParentId,
                AttachmentOffset = attachmentOffset
            };
        }
    }

    private static SoftBody? FindClosestParentBody(
        IReadOnlyList<SoftBody> bodies, int parentId, Vector2 position)
    {
        SoftBody? closest = null;
        var closestDistance = float.MaxValue;
        foreach (var body in bodies)
        {
            if (body.ParentId != parentId || body.IsDetachedDebris) continue;
            var distance = Vector2.DistanceSquared(body.Center, position);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closest = body;
        }
        return closest;
    }

    private static int FindNearestPhysicalParticle(SoftBody body, Vector2 position)
    {
        var nearest = -1;
        var best = float.MaxValue;
        for (var index = 0; index < body.Particles.Length; index++)
        {
            if (!body.IsPhysicalParticle(index)) continue;
            var distance = Vector2.DistanceSquared(body.Particles[index].Position, position);
            if (distance >= best) continue;
            best = distance;
            nearest = index;
        }
        return nearest;
    }

    private static SoftBody? FindNearestTarget(
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed,
        Vector2 origin,
        float maximumDistance)
    {
        SoftBody? target = null;
        var best = maximumDistance * maximumDistance;
        foreach (var body in bodies)
        {
            if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                body.PhysicalParticleCount <= 3)
                continue;
            var distance = Vector2.DistanceSquared(origin, body.Center);
            if (distance >= best) continue;
            best = distance;
            target = body;
        }
        return target;
    }

    private float NextArsenal01()
    {
        _arsenalRandom ^= _arsenalRandom << 13;
        _arsenalRandom ^= _arsenalRandom >> 17;
        _arsenalRandom ^= _arsenalRandom << 5;
        return (_arsenalRandom & 0x00FFFFFFu) / 16777216f;
    }

    private void LobBaseball()
    {
        var direction = LiveBarrelDirection;
        SpawnProjectile(ArsenalProjectileKind.Baseball,
            Position + direction * 20f,
            direction * 255f - Vector2.UnitY * 155f,
            20f, penetration: int.MaxValue);
        _baseballInPlay = true;
        _angularVelocity += direction.X >= 0f ? -7f : 7f;
    }

    private void SwingBaseballBat(
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed,
        float dt)
    {
        var ballIndex = -1;
        var best = 150f * 150f;
        for (var index = 0; index < _arsenalProjectiles.Count; index++)
        {
            var projectile = _arsenalProjectiles[index];
            if (projectile.Kind != ArsenalProjectileKind.Baseball) continue;
            var distance = Vector2.DistanceSquared(Position, projectile.Position);
            if (distance >= best) continue;
            best = distance;
            ballIndex = index;
        }
        _angularVelocity += LiveBarrelDirection.X >= 0f ? -19f : 19f;
        if (ballIndex < 0) return;
        var ball = _arsenalProjectiles[ballIndex];
        var target = FindNearestTarget(bodies, tubeFeed, ball.Position, 720f);
        var direction = target is null
            ? LiveBarrelDirection
            : Vector2.Normalize(target.Center - ball.Position);
        direction = Rotate(direction, (NextArsenal01() - 0.5f) * 0.30f);
        _arsenalProjectiles[ballIndex] = ball with
        {
            Velocity = direction * (980f + NextArsenal01() * 180f),
            RemainingSeconds = 20f,
            LastHitParentId = -1,
            Angle = MathF.Atan2(direction.Y, direction.X)
        };
        LastArsenalActionPosition = ball.Position;
        ArsenalShotSerial++;
    }

    public bool TryPickupBaseball(Vector2 point)
    {
        if (ArsenalVisualVariant != 21 || !_baseballInPlay) return false;
        for (var index = _arsenalProjectiles.Count - 1; index >= 0; index--)
        {
            var projectile = _arsenalProjectiles[index];
            if (projectile.Kind != ArsenalProjectileKind.Baseball ||
                Vector2.DistanceSquared(projectile.Position, point) > 54f * 54f)
                continue;
            _arsenalProjectiles.RemoveAt(index);
            _baseballInPlay = false;
            return true;
        }
        return false;
    }

    private void SpawnRatAgent(
        Vector2 position,
        Vector2 velocity,
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed)
    {
        if (_rats.Count >= 12) _rats.RemoveAt(0);
        _rats.Add(new RatAgent(
            position,
            velocity,
            FindNearestTarget(bodies, tubeFeed, position, 520f),
            Vector2.Zero,
            -1,
            false,
            false,
            -1,
            0.12f,
            18f,
            0));
    }

    private void UpdateRats(
        float dt,
        IReadOnlyList<SoftBody> bodies,
        IReadOnlyList<ConveyorBelt> conveyors,
        OverheadTubeFeed? tubeFeed,
        DestructibleGrid? grid,
        GranularMaterialSystem? granular)
    {
        for (var index = _rats.Count - 1; index >= 0; index--)
        {
            var rat = _rats[index];
            var remaining = rat.RemainingSeconds - dt;
            var target = rat.Target;
            var attachedOnce = rat.HasAttachedOnce;
            var targetParentId = rat.TargetParentId;
            if (target is not null && !bodies.Contains(target))
            {
                target = attachedOnce && targetParentId >= 0
                    ? FindClosestParentBody(bodies, targetParentId, rat.Position)
                    : FindNearestTarget(bodies, tubeFeed, rat.Position, 620f);
            }
            if (remaining <= 0f || rat.Position.X < -64f || rat.Position.X > 1344f)
            {
                _rats.RemoveAt(index);
                continue;
            }
            if (attachedOnce &&
                (target is null || target.PhysicalParticleCount <= 3 ||
                 target.Center.X < -48f || target.Center.X > 1328f))
            {
                EmitRatBurst(granular, rat.Position, dt);
                _rats.RemoveAt(index);
                continue;
            }
            if (!attachedOnce)
                target ??= FindNearestTarget(bodies, tubeFeed, rat.Position, 620f);
            var targetChanged = !ReferenceEquals(target, rat.Target);
            var position = rat.Position;
            var velocity = rat.Velocity;
            var bodyOffset = targetChanged ? Vector2.Zero : rat.BodyOffset;
            var targetParticleIndex = targetChanged ? -1 : rat.TargetParticleIndex;
            var attached = rat.Attached && target is not null;
            var chew = rat.ChewCooldown - dt;
            if (target is not null)
            {
                var toTarget = target.Center - position;
                if (!attached && !attachedOnce && toTarget.Length() <= target.Radius + 11f)
                {
                    targetParticleIndex = FindNearestPhysicalParticle(target, position);
                    if (targetParticleIndex >= 0)
                    {
                        bodyOffset = position -
                                     target.Particles[targetParticleIndex].Position;
                        var maximumOffset = 6f;
                        if (bodyOffset.LengthSquared() > maximumOffset * maximumOffset)
                            bodyOffset = Vector2.Normalize(bodyOffset) * maximumOffset;
                        attached = true;
                        attachedOnce = true;
                        targetParentId = target.ParentId;
                    }
                }
                if (attached)
                {
                    if ((uint)targetParticleIndex >= (uint)target.Particles.Length ||
                        !target.IsPhysicalParticle(targetParticleIndex))
                    {
                        targetParticleIndex = FindNearestPhysicalParticle(target, position);
                        if (targetParticleIndex < 0)
                        {
                            EmitRatBurst(granular, position, dt);
                            _rats.RemoveAt(index);
                            continue;
                        }
                        bodyOffset = position - target.Particles[targetParticleIndex].Position;
                    }
                    var hostParticle = target.Particles[targetParticleIndex];
                    position = hostParticle.Position + bodyOffset;
                    velocity = (hostParticle.Position - hostParticle.PreviousPosition) /
                               MathF.Max(dt, 0.0001f);
                }
                if (attached)
                {
                    if (chew <= 0f)
                    {
                        target.DamageLine(position, position, 7f, 1.7f, maximumBreaks: 1);
                        target.DamageBonds(position, 8f, 1.25f);
                        var biteDirection = bodyOffset.LengthSquared() > 0.001f
                            ? Vector2.Normalize(bodyOffset)
                            : Vector2.Normalize(position - target.Center);
                        if (!float.IsFinite(biteDirection.X) || !float.IsFinite(biteDirection.Y))
                            biteDirection = -Vector2.UnitY;
                        target.AddLocalizedImpulse(position, 14f,
                            biteDirection * 18f, dt);
                        chew = 0.24f;
                        PuncturedThisStep = true;
                    }
                }
                else
                {
                    if (TryFindRatSurface(position, conveyors, grid,
                            out var surfacePoint, out var surfaceNormal,
                            out var surfaceVelocity))
                    {
                        var desiredPosition = surfacePoint + surfaceNormal * 8f;
                        position = Vector2.Lerp(position, desiredPosition,
                            Math.Clamp(dt * 22f, 0f, 1f));
                        var tangent = new Vector2(-surfaceNormal.Y, surfaceNormal.X);
                        var tangentSign = MathF.Sign(Vector2.Dot(toTarget, tangent));
                        if (tangentSign == 0) tangentSign = 1;
                        var desired = tangent * (tangentSign * 150f) + surfaceVelocity;
                        velocity = Vector2.Lerp(velocity, desired,
                            Math.Clamp(dt * 12f, 0f, 1f));
                        position += velocity * dt;
                    }
                    else
                    {
                        velocity += new Vector2(0f, 720f) * dt;
                        velocity.X *= MathF.Exp(-2.2f * dt);
                        position += velocity * dt;
                    }
                }
            }
            else
            {
                velocity += new Vector2(0f, 420f) * dt;
                position += velocity * dt;
            }
            _rats[index] = rat with
            {
                Position = position,
                Velocity = velocity,
                Target = target,
                BodyOffset = bodyOffset,
                TargetParticleIndex = targetParticleIndex,
                Attached = attached,
                HasAttachedOnce = attachedOnce,
                TargetParentId = targetParentId,
                ChewCooldown = chew,
                RemainingSeconds = remaining,
                Frame = (byte)((Environment.TickCount64 / 110 + index) & 1)
            };
        }
    }

    private static bool TryFindRatSurface(
        Vector2 position,
        IReadOnlyList<ConveyorBelt> conveyors,
        DestructibleGrid? grid,
        out Vector2 surfacePoint,
        out Vector2 surfaceNormal,
        out Vector2 surfaceVelocity)
    {
        var bestPoint = Vector2.Zero;
        var bestNormal = -Vector2.UnitY;
        var bestVelocity = Vector2.Zero;
        var bestDistanceSquared = 22f * 22f;
        foreach (var conveyor in conveyors)
        {
            if (position.X < conveyor.Position.X - 12f ||
                position.X > conveyor.Position.X + conveyor.Width + 12f)
                continue;
            var candidate = new Vector2(
                Math.Clamp(position.X, conveyor.Position.X,
                    conveyor.Position.X + conveyor.Width),
                conveyor.Position.Y);
            var distanceSquared = Vector2.DistanceSquared(position, candidate);
            if (distanceSquared >= bestDistanceSquared) continue;
            bestDistanceSquared = distanceSquared;
            bestPoint = candidate;
            bestNormal = -Vector2.UnitY;
            bestVelocity = new Vector2(conveyor.Speed, 0f);
        }

        if (grid is null)
        {
            surfacePoint = bestPoint;
            surfaceNormal = bestNormal;
            surfaceVelocity = bestVelocity;
            return bestDistanceSquared < 22f * 22f;
        }
        var cellSize = grid.CellSize;
        var centerX = (int)MathF.Floor(position.X / cellSize);
        var centerY = (int)MathF.Floor(position.Y / cellSize);
        for (var cellY = Math.Max(0, centerY - 2);
             cellY <= Math.Min(grid.Rows - 1, centerY + 2);
             cellY++)
        for (var cellX = Math.Max(0, centerX - 2);
             cellX <= Math.Min(grid.Columns - 1, centerX + 2);
             cellX++)
        {
            if (!grid.Cell(cellX, cellY).IsSolid) continue;
            var left = cellX * (float)cellSize;
            var top = cellY * (float)cellSize;
            Consider(new Vector2(
                    Math.Clamp(position.X, left, left + cellSize), top),
                -Vector2.UnitY, !GridSolid(grid, cellX, cellY - 1));
            Consider(new Vector2(
                    Math.Clamp(position.X, left, left + cellSize), top + cellSize),
                Vector2.UnitY, !GridSolid(grid, cellX, cellY + 1));
            Consider(new Vector2(
                    left, Math.Clamp(position.Y, top, top + cellSize)),
                -Vector2.UnitX, !GridSolid(grid, cellX - 1, cellY));
            Consider(new Vector2(
                    left + cellSize, Math.Clamp(position.Y, top, top + cellSize)),
                Vector2.UnitX, !GridSolid(grid, cellX + 1, cellY));
        }
        surfacePoint = bestPoint;
        surfaceNormal = bestNormal;
        surfaceVelocity = bestVelocity;
        return bestDistanceSquared < 22f * 22f;

        void Consider(Vector2 candidate, Vector2 normal, bool exposed)
        {
            if (!exposed) return;
            var distanceSquared = Vector2.DistanceSquared(position, candidate);
            if (distanceSquared >= bestDistanceSquared) return;
            bestDistanceSquared = distanceSquared;
            bestPoint = candidate;
            bestNormal = normal;
            bestVelocity = Vector2.Zero;
        }
    }

    private static bool GridSolid(DestructibleGrid grid, int x, int y) =>
        x >= 0 && y >= 0 && x < grid.Columns && y < grid.Rows &&
        grid.Cell(x, y).IsSolid;

    private static void EmitRatBurst(
        GranularMaterialSystem? granular,
        Vector2 position,
        float dt)
    {
        if (granular is null) return;
        granular.EmitBlood(
            new WoundEvent(position, -Vector2.UnitY, 1.8f),
            dt,
            requestedCount: 6,
            speedScale: 1.35f);
    }

    private void AddFlamePatch(
        Vector2 position,
        Vector2 velocity,
        SoftBody? attachedBody,
        int bodyParticleIndex,
        float lifetime)
    {
        if (_flamePatches.Count >= 48) _flamePatches.RemoveAt(0);
        _flamePatches.Add(new FlamePatch(
            position,
            velocity,
            lifetime,
            attachedBody,
            bodyParticleIndex,
            Vector2.Zero,
            null,
            false,
            0f,
            (byte)(_arsenalRandom & 255)));
    }

    private void AddSmoke(
        Vector2 position,
        SmokeKind kind,
        float intensity = 1f)
    {
        if (_smokeParticles.Count >= 128) _smokeParticles.RemoveAt(0);
        intensity = Math.Clamp(intensity, 0.35f, 1.8f);
        var lifetime = (0.55f + NextArsenal01() * 0.85f) * intensity;
        _smokeParticles.Add(new SmokeParticle(
            position + new Vector2(-3f + NextArsenal01() * 6f, -2f),
            new Vector2(-17f + NextArsenal01() * 34f, -32f - NextArsenal01() * 38f),
            lifetime,
            lifetime,
            2f + NextArsenal01() * 3.4f * intensity,
            kind,
            (byte)(_arsenalRandom++ & 255)));
    }

    private void UpdateSmokeParticles(float dt)
    {
        for (var index = _smokeParticles.Count - 1; index >= 0; index--)
        {
            var smoke = _smokeParticles[index];
            var remaining = smoke.RemainingSeconds - dt;
            if (remaining <= 0f)
            {
                _smokeParticles.RemoveAt(index);
                continue;
            }
            var age01 = 1f - remaining / MathF.Max(0.001f, smoke.LifetimeSeconds);
            var velocity = smoke.Velocity + new Vector2(
                ((smoke.Variation & 1) == 0 ? -1f : 1f) *
                (5f + (smoke.Variation % 5)) * dt,
                -8f * dt);
            velocity *= MathF.Exp(-0.48f * dt);
            _smokeParticles[index] = smoke with
            {
                Position = smoke.Position + velocity * dt,
                Velocity = velocity,
                RemainingSeconds = remaining,
                Radius = smoke.Radius + dt * (2.2f + age01 * 3.8f)
            };
        }
    }

    private void AddSurfaceFlame(
        Vector2 position,
        Vector2 surfaceNormal,
        ConveyorBelt? conveyor,
        float lifetime,
        float spreadDelay = 0.28f)
    {
        if (surfaceNormal.LengthSquared() < 0.001f)
            surfaceNormal = -Vector2.UnitY;
        else
            surfaceNormal = Vector2.Normalize(surfaceNormal);
        foreach (var existing in _flamePatches)
        {
            if (!existing.SurfaceFire ||
                Vector2.DistanceSquared(existing.Position, position) > 16f * 16f)
                continue;
            return;
        }
        if (_flamePatches.Count >= 48) _flamePatches.RemoveAt(0);
        _flamePatches.Add(new FlamePatch(
            position,
            Vector2.Zero,
            lifetime,
            null,
            -1,
            surfaceNormal,
            conveyor,
            true,
            spreadDelay,
            (byte)(_arsenalRandom++ & 255)));
    }

    private void IgniteBody(SoftBody body, Vector2 impact)
    {
        var existing = _burningBlobs.FirstOrDefault(state =>
            ReferenceEquals(state.Body, body));
        if (existing is not null)
        {
            existing.RemainingSeconds = MathF.Min(6f, existing.RemainingSeconds + 1.4f);
            return;
        }
        if (_burningBlobs.Count >= 12) _burningBlobs.RemoveAt(0);
        _burningBlobs.Add(new BurningBlobState
        {
            Body = body,
            LastPosition = impact,
            RemainingSeconds = 4.2f,
            DamageCooldown = 0.08f,
            Variation = (byte)((body.ParentId * 47 + _arsenalRandom) & 255)
        });
        var particleIndex = FindNearestPhysicalParticle(body, impact);
        AddFlamePatch(impact, -Vector2.UnitY * 10f, body, particleIndex, 1.2f);
    }

    private void UpdateFlameEffects(
        float dt,
        IReadOnlyList<SoftBody> bodies,
        IReadOnlyList<ConveyorBelt> conveyors,
        OverheadTubeFeed? tubeFeed)
    {
        if (_flamePatches.Count > 0 && _smokeSpawnCooldown <= 0f)
        {
            var sampleCount = Math.Min(3, 1 + _flamePatches.Count / 14);
            for (var sample = 0; sample < sampleCount; sample++)
            {
                var flame = _flamePatches[
                    (int)((_arsenalRandom + (uint)(sample * 17)) %
                          (uint)_flamePatches.Count)];
                AddSmoke(flame.Position, SmokeKind.Fire,
                    flame.SurfaceFire ? 1.15f : 0.72f);
            }
            _smokeSpawnCooldown = 0.065f;
        }
        for (var index = _flamePatches.Count - 1; index >= 0; index--)
        {
            var flame = _flamePatches[index];
            var remaining = flame.RemainingSeconds - dt;
            if (remaining <= 0f)
            {
                _flamePatches.RemoveAt(index);
                continue;
            }
            var position = flame.Position;
            var velocity = flame.Velocity;
            var attached = flame.AttachedBody;
            var particleIndex = flame.BodyParticleIndex;
            var conveyor = flame.Conveyor;
            var spreadCooldown = flame.SpreadCooldown - dt;
            if (attached is not null && !bodies.Contains(attached))
            {
                attached = FindClosestParentBody(bodies, attached.ParentId, position);
                particleIndex = attached is null
                    ? -1
                    : FindNearestPhysicalParticle(attached, position);
            }
            if (attached is not null &&
                particleIndex >= 0 &&
                attached.IsPhysicalParticle(particleIndex))
            {
                position = attached.Particles[particleIndex].Position;
                velocity = Vector2.Zero;
            }
            else if (flame.SurfaceFire)
            {
                if (conveyor is not null)
                {
                    if (!conveyors.Contains(conveyor))
                        conveyor = null;
                    else
                        position += new Vector2(conveyor.Speed * dt, 0f);
                }
                velocity = Vector2.Zero;
                foreach (var body in bodies)
                {
                    if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                        Vector2.DistanceSquared(body.Center, position) >
                        (body.Radius + 24f) * (body.Radius + 24f))
                        continue;
                    IgniteBody(body, position);
                }
                if (spreadCooldown <= 0f && _flamePatches.Count < 44)
                {
                    var normal = flame.SurfaceNormal.LengthSquared() > 0.001f
                        ? Vector2.Normalize(flame.SurfaceNormal)
                        : -Vector2.UnitY;
                    var tangent = new Vector2(-normal.Y, normal.X);
                    var side = (flame.Variation & 1) == 0 ? -1f : 1f;
                    AddSurfaceFlame(
                        position + tangent * side * (13f + flame.Variation % 5),
                        normal,
                        conveyor,
                        2.8f + (flame.Variation % 3) * 0.35f,
                        0.42f);
                    spreadCooldown = 0.55f;
                }
            }
            else
            {
                velocity += new Vector2(
                    (flame.Variation % 3 - 1) * 18f,
                    -72f) * dt;
                velocity *= MathF.Exp(-2.8f * dt);
                position += velocity * dt;
            }
            _flamePatches[index] = flame with
            {
                Position = position,
                Velocity = velocity,
                RemainingSeconds = remaining,
                AttachedBody = attached,
                BodyParticleIndex = particleIndex,
                Conveyor = conveyor,
                SpreadCooldown = spreadCooldown
            };
        }

        for (var index = _burningBlobs.Count - 1; index >= 0; index--)
        {
            var burning = _burningBlobs[index];
            var body = burning.Body;
            if (!bodies.Contains(body))
            {
                var replacement = FindClosestParentBody(
                    bodies, body.ParentId, burning.LastPosition);
                if (replacement is null)
                {
                    _burningBlobs.RemoveAt(index);
                    continue;
                }
                body = replacement;
                burning.Body = replacement;
            }
            if (body.PhysicalParticleCount <= 3 || tubeFeed?.Contains(body) == true)
            {
                _burningBlobs.RemoveAt(index);
                continue;
            }
            burning.LastPosition = body.Center;
            burning.RemainingSeconds -= dt;
            burning.DamageCooldown -= dt;
            if (burning.RemainingSeconds <= 0f)
            {
                _burningBlobs.RemoveAt(index);
                continue;
            }
            if (burning.DamageCooldown <= 0f)
            {
                var sample = (burning.Variation + (int)(burning.RemainingSeconds * 17f)) %
                             body.Particles.Length;
                var particleIndex = sample;
                for (var attempt = 0;
                     attempt < body.Particles.Length && !body.IsPhysicalParticle(particleIndex);
                     attempt++)
                    particleIndex = (particleIndex + 1) % body.Particles.Length;
                if (body.IsPhysicalParticle(particleIndex))
                {
                    var point = body.Particles[particleIndex].Position;
                    body.DamageBonds(point, 10f, 1.45f);
                    body.DamageLine(point, point, 6f, 1.05f, maximumBreaks: 1);
                    body.RegisterHitReaction(0.35f, 0.07f);
                    if ((_arsenalRandom++ & 1) == 0)
                        AddFlamePatch(point, -Vector2.UnitY * 8f,
                            body, particleIndex, 0.72f);
                    PuncturedThisStep = true;
                }
                burning.DamageCooldown = 0.16f;
            }
        }
    }

    private void CreateAcidPool(
        Vector2 position,
        float radius,
        Vector2 surfaceNormal,
        SoftBody? attachedBody = null,
        ConveyorBelt? conveyor = null)
    {
        if (_acidPools.Count >= 8) _acidPools.RemoveAt(0);
        var particleIndex = attachedBody is null
            ? -1
            : FindNearestPhysicalParticle(attachedBody, position);
        _acidPools.Add(new AcidPool(
            position,
            radius,
            5.2f,
            0f,
            surfaceNormal.LengthSquared() > 0.001f
                ? Vector2.Normalize(surfaceNormal)
                : -Vector2.UnitY,
            attachedBody,
            particleIndex,
            conveyor,
            (byte)(_arsenalRandom++ & 255)));
    }

    private void EmitAcidBurst(
        GranularMaterialSystem? granular,
        Vector2 position,
        Vector2 impactVelocity,
        Vector2 surfaceNormal,
        float dt,
        float power)
    {
        if (granular is null) return;
        power = Math.Clamp(power, 0.25f, 1f);
        var normal = surfaceNormal.LengthSquared() > 0.001f
            ? Vector2.Normalize(surfaceNormal)
            : -Vector2.UnitY;
        var inheritedVelocity = impactVelocity * 0.16f;
        var count = 20 + (int)MathF.Round(power * 18f);
        var available = Math.Max(
            0,
            GranularMaterialSystem.ParticleCapacity - granular.Particles.Count);
        count = Math.Min(count, available);
        for (var index = 0; index < count; index++)
        {
            var angle = NextArsenal01() * MathF.Tau;
            var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            // Keep the impact surface from swallowing half the burst while
            // retaining a full splattery fan rather than a uniform cone.
            if (Vector2.Dot(radial, normal) < -0.22f)
                radial = Vector2.Normalize(radial - normal *
                    Vector2.Dot(radial, normal) * 1.35f);
            var speed = 72f + NextArsenal01() * (155f + power * 105f);
            var velocity = inheritedVelocity + radial * speed +
                           normal * (35f + NextArsenal01() * 85f);
            var spawn = position + normal * 3f +
                        radial * (2f + NextArsenal01() * 5f);
            granular.Particles.Add(new GranularParticle
            {
                Position = spawn,
                PreviousPosition = spawn - velocity * dt,
                Radius = 1.8f + NextArsenal01() * 1.35f,
                Lifetime = 6.2f + NextArsenal01() * 4.4f,
                Kind = GranularKind.Acid,
                SplatterOnImpact = false,
                BypassConveyors = false,
                CorrosionCooldown = NextArsenal01() * 0.055f
            });
        }
        ArsenalExplosionSerial++;
        LastArsenalActionPosition = position;
    }

    private void UpdateAcidPools(
        float dt,
        IReadOnlyList<SoftBody> bodies,
        IReadOnlyList<ConveyorBelt> conveyors,
        OverheadTubeFeed? tubeFeed)
    {
        if (_acidPools.Count > 0 && _smokeSpawnCooldown <= 0f)
        {
            var pool = _acidPools[(int)(_arsenalRandom % (uint)_acidPools.Count)];
            AddSmoke(pool.Position, SmokeKind.Acid, 0.78f);
            _smokeSpawnCooldown = 0.09f;
        }
        for (var index = _acidPools.Count - 1; index >= 0; index--)
        {
            var pool = _acidPools[index];
            var remaining = pool.RemainingSeconds - dt;
            var cooldown = pool.DamageCooldown - dt;
            if (remaining <= 0f)
            {
                _acidPools.RemoveAt(index);
                continue;
            }
            var position = pool.Position;
            var normal = pool.SurfaceNormal;
            var attachedBody = pool.AttachedBody;
            var conveyor = pool.Conveyor;
            var bodyParticleIndex = pool.BodyParticleIndex;
            if (attachedBody is not null)
            {
                if (!bodies.Contains(attachedBody))
                {
                    attachedBody = FindClosestParentBody(
                        bodies, attachedBody.ParentId, position);
                    bodyParticleIndex = attachedBody is null
                        ? -1
                        : FindNearestPhysicalParticle(attachedBody, position);
                    if (attachedBody is null)
                    {
                        // Coating belongs to material, not world space. If every
                        // descendant is gone, remove it instead of leaving a decal
                        // hovering at the topology split coordinate.
                        _acidPools.RemoveAt(index);
                        continue;
                    }
                }
                else
                {
                    if (bodyParticleIndex < 0 ||
                        !attachedBody.IsPhysicalParticle(bodyParticleIndex))
                        bodyParticleIndex = FindNearestPhysicalParticle(attachedBody, position);
                    if (bodyParticleIndex < 0)
                    {
                        attachedBody = null;
                        bodyParticleIndex = -1;
                    }
                }
                if (attachedBody is not null)
                {
                    var radialNormal =
                        attachedBody.Particles[bodyParticleIndex].Position -
                        attachedBody.Center;
                    if (radialNormal.LengthSquared() > 0.001f)
                        normal = Vector2.Normalize(Vector2.Lerp(
                            normal, Vector2.Normalize(radialNormal), 0.35f));
                    position = attachedBody.Particles[bodyParticleIndex].Position +
                               normal * 2f;
                    // Acid coating creeps down the material surface between damage
                    // ticks instead of hovering at its original impact coordinate.
                    var lower = bodyParticleIndex;
                    for (var candidate = 0; candidate < attachedBody.Particles.Length; candidate++)
                    {
                        if (!attachedBody.IsPhysicalParticle(candidate)) continue;
                        var candidatePosition = attachedBody.Particles[candidate].Position;
                        if (candidatePosition.Y <=
                            attachedBody.Particles[lower].Position.Y + 2f) continue;
                        if (MathF.Abs(candidatePosition.X - position.X) > pool.Radius * 0.72f)
                            continue;
                        lower = candidate;
                    }
                    if (lower != bodyParticleIndex &&
                        (pool.Variation + (int)(remaining * 20f)) % 7 == 0)
                    {
                        bodyParticleIndex = lower;
                        normal = Vector2.Normalize(
                            attachedBody.Particles[lower].Position - attachedBody.Center);
                    }
                }
            }
            else if (conveyor is not null)
            {
                if (!conveyors.Contains(conveyor))
                    conveyor = null;
                else
                {
                    position += new Vector2(conveyor.Speed * dt, 0f);
                    position.Y = conveyor.Position.Y - 2f;
                    normal = -Vector2.UnitY;
                    if (position.X < conveyor.Position.X - pool.Radius ||
                        position.X > conveyor.Position.X + conveyor.Width + pool.Radius)
                    {
                        _acidPools.RemoveAt(index);
                        continue;
                    }
                }
            }
            if (cooldown <= 0f)
            {
                foreach (var body in bodies)
                {
                    if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                        Vector2.Distance(body.Center, position) > body.Radius + pool.Radius)
                        continue;
                    var tangent = new Vector2(-normal.Y, normal.X);
                    var contact = ClosestPoint(body.Center,
                        position - tangent * pool.Radius,
                        position + tangent * pool.Radius);
                    body.ExciseSweptBand(contact, contact, 8f, maximumParticles: 1);
                    body.DamageBonds(contact, 12f, 2.2f);
                    body.RegisterHitReaction(0.4f, 0.08f);
                    PuncturedThisStep = true;
                }
                cooldown = 0.18f;
            }
            _acidPools[index] = pool with
            {
                Position = position,
                RemainingSeconds = remaining,
                DamageCooldown = cooldown,
                SurfaceNormal = normal,
                AttachedBody = attachedBody,
                BodyParticleIndex = bodyParticleIndex,
                Conveyor = conveyor
            };
        }
    }

    private void FreezeBody(
        SoftBody body,
        float remainingSeconds = 8f,
        int generation = 0)
    {
        var existing = _frozenBlobs.FirstOrDefault(state => ReferenceEquals(state.Body, body));
        if (existing is not null)
        {
            existing.RemainingSeconds = MathF.Max(existing.RemainingSeconds, remainingSeconds);
            existing.PendingSplitPropagation = false;
            return;
        }
        if (_frozenBlobs.Count >= 24) _frozenBlobs.RemoveAt(0);
        var center = body.Center;
        var offsets = new Vector2[body.Particles.Length];
        for (var index = 0; index < offsets.Length; index++)
            offsets[index] = body.Particles[index].Position - center;
        _frozenBlobs.Add(new FrozenBlobState
        {
            Body = body,
            Offsets = offsets,
            RemainingSeconds = remainingSeconds,
            ShatterCooldown = 0f,
            PendingSplitPropagation = false,
            Generation = generation
        });
    }

    private void UpdateFrozenBlobs(
        float dt,
        IReadOnlyList<SoftBody> bodies,
        GranularMaterialSystem? granular)
    {
        for (var stateIndex = _frozenBlobs.Count - 1; stateIndex >= 0; stateIndex--)
        {
            var state = _frozenBlobs[stateIndex];
            state.RemainingSeconds -= dt;
            state.ShatterCooldown = MathF.Max(0f, state.ShatterCooldown - dt);
            var body = state.Body;
            if (!bodies.Contains(body))
            {
                // Topology splitting replaces the original body object. Carry the
                // ice state to every real child with the same material lineage so
                // frozen chunks remain frozen and can shatter again.
                var descendants = bodies
                    .Where(candidate =>
                        candidate.ParentId == body.ParentId &&
                        candidate.PhysicalParticleCount > 3)
                    .ToArray();
                _frozenBlobs.RemoveAt(stateIndex);
                foreach (var descendant in descendants)
                    FreezeBody(
                        descendant,
                        MathF.Max(2.8f, state.RemainingSeconds),
                        state.Generation + 1);
                continue;
            }
            if (state.RemainingSeconds <= 0f || body.PhysicalParticleCount <= 3)
            {
                _frozenBlobs.RemoveAt(stateIndex);
                continue;
            }
            if (state.ShatterCooldown <= 0f &&
                state.RemainingSeconds < 7.72f &&
                (body.LastTerrainImpact > 250f || body.LastImpact > 420f))
            {
                var center = body.Center;
                var crackRadius = MathF.Max(3f, body.ParticleSpacing * 0.72f);
                if (state.Generation == 0)
                {
                    // First impact creates physical frozen descendants. Their
                    // launch is deliberately modest so they read as shattered
                    // matter instead of a second grenade.
                    body.AddRadialExplosion(center, 90f, 245f, dt);
                    const int crackCount = 3;
                    for (var crack = 0; crack < crackCount; crack++)
                    {
                        var angle = (crack + ((body.ParentId * 37) & 15) / 16f) *
                                    MathF.PI / crackCount;
                        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) *
                                        body.Radius;
                        body.DamageLine(
                            center - direction,
                            center + direction,
                            crackRadius * (0.48f + crack % 3 * 0.08f),
                            5.8f,
                            maximumBreaks: 24);
                    }
                    state.PendingSplitPropagation = true;
                    state.ShatterCooldown = 0.42f;
                    state.RemainingSeconds = MathF.Max(state.RemainingSeconds, 3.2f);
                }
                else
                {
                    // A frozen descendant's next hard impact is its terminal
                    // shatter: it ceases being ice and enters ordinary gore.
                    body.AddRadialExplosion(center, 65f, 190f, dt);
                    for (var crack = 0; crack < 3; crack++)
                    {
                        var angle = crack * MathF.PI / 3f +
                                    ((body.ParentId & 7) - 3) * 0.04f;
                        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) *
                                        body.Radius;
                        body.DamageLine(
                            center - direction,
                            center + direction,
                            crackRadius,
                            12f,
                            maximumBreaks: 48);
                    }
                    body.DamageBonds(center, body.Radius * 1.2f, 16f);
                    body.BeginCrumbling();
                    if (granular is not null)
                        EmitRadialArsenalGore(
                            granular, center, dt, 14, 45f, 180f);
                    _frozenBlobs.RemoveAt(stateIndex);
                }
                PuncturedThisStep = true;
                continue;
            }
            if (body.IsGrabbed) continue;
            var velocity = body.AverageVelocity(dt);
            var centerNow = body.Center;
            var count = Math.Min(body.Particles.Length, state.Offsets.Length);
            for (var index = 0; index < count; index++)
            {
                if (!body.IsPhysicalParticle(index)) continue;
                var target = centerNow + state.Offsets[index];
                body.Particles[index].Position =
                    Vector2.Lerp(body.Particles[index].Position, target, 0.78f);
                body.Particles[index].PreviousPosition =
                    body.Particles[index].Position - velocity * dt;
            }
        }
    }

    private void ApplyBlackHole(
        Vector2 position,
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed,
        GranularMaterialSystem? granular,
        float dt)
    {
        foreach (var body in bodies)
        {
            if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true) continue;
            var delta = position - body.Center;
            var distance = delta.Length();
            const float pullRadius = 124f;
            if (distance > pullRadius + body.Radius) continue;
            var direction = distance < 0.01f ? Vector2.UnitY : delta / distance;
            var falloff = 1f - Math.Clamp(distance / pullRadius, 0f, 1f);
            body.AddImpulse(direction * (32f + 210f * falloff), dt);
            if (distance > 34f + body.Radius * 0.25f || _specialEffectCooldown > 0f)
                continue;
            var removed = body.ExciseSweptBand(position, position, 18f,
                maximumParticles: 2);
            body.DamageBonds(position, 24f, 4.8f);
            body.AddLocalizedImpulse(position, 42f, -direction * 210f, dt);
            if (removed > 0 && granular is not null)
                EmitArsenalGore(granular, position, -direction, dt, 4);
            _specialEffectCooldown = 0.075f;
            PuncturedThisStep = true;
        }
    }

    private void EmitArsenalGore(
        GranularMaterialSystem granular,
        Vector2 position,
        Vector2 direction,
        float dt,
        int count)
    {
        if (direction.LengthSquared() < 0.001f) direction = -Vector2.UnitY;
        else direction = Vector2.Normalize(direction);
        for (var index = 0;
             index < count && granular.Particles.Count < GranularMaterialSystem.ParticleCapacity;
             index++)
        {
            var spread = Rotate(direction, (NextArsenal01() - 0.5f) * 1.1f);
            var velocity = spread * (90f + NextArsenal01() * 180f);
            granular.Particles.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - velocity * dt,
                Radius = 2f + NextArsenal01() * 1.3f,
                Lifetime = 12f,
                Kind = index % 3 == 0 ? GranularKind.Tissue : GranularKind.Blood,
                Appearance = GranularAppearance.Gore,
                SplatterOnImpact = true
            });
        }
    }

    private void EmitRadialArsenalGore(
        GranularMaterialSystem granular,
        Vector2 position,
        float dt,
        int count,
        float minimumSpeed,
        float maximumSpeed)
    {
        for (var index = 0;
             index < count && granular.Particles.Count < GranularMaterialSystem.ParticleCapacity;
             index++)
        {
            var angle = MathF.Tau * (index + NextArsenal01() * 0.72f) /
                        MathF.Max(1, count);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var speed = minimumSpeed +
                        (maximumSpeed - minimumSpeed) * NextArsenal01();
            var velocity = direction * speed;
            var spawnPosition = position + direction * (3f + NextArsenal01() * 8f);
            granular.Particles.Add(new GranularParticle
            {
                Position = spawnPosition,
                PreviousPosition = spawnPosition - velocity * dt,
                Radius = 2f + NextArsenal01() * 1.6f,
                Lifetime = 12f,
                Kind = index % 4 == 0 ? GranularKind.Tissue : GranularKind.Blood,
                Appearance = GranularAppearance.Gore,
                SplatterOnImpact = true
            });
        }
    }

    private bool ResolveSpecialProjectileBody(
        ArsenalProjectileKind kind,
        Vector2 previous,
        Vector2 position,
        ref Vector2 velocity,
        float power,
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed,
        GranularMaterialSystem? granular,
        float dt)
    {
        var radius = ProjectileRadius(kind);
        foreach (var body in bodies)
        {
            if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                DistanceToSegment(body.Center, previous, position) > body.Radius + radius + 5f)
                continue;
            var impact = ClosestPoint(body.Center, previous, position);
            var direction = velocity.LengthSquared() < 0.001f
                ? Vector2.UnitX
                : Vector2.Normalize(velocity);
            if (!body.ContainsVisiblePoint(impact))
                impact = body.Center - direction * body.Radius * 0.62f;
            switch (kind)
            {
                case ArsenalProjectileKind.Rat:
                    SpawnRatAgent(impact, velocity * 0.18f, bodies, tubeFeed);
                    return true;
                case ArsenalProjectileKind.GrowthPulse:
                {
                    var currentScale = _enlargementScales.GetValueOrDefault(body.ParentId, 1f);
                    var factor = MathF.Min(1.055f, 3f / currentScale);
                    body.ScaleMaterial(factor);
                    currentScale *= factor;
                    _enlargementScales[body.ParentId] = currentScale;
                    AddArsenalActionEffect(15, impact, body.Center, 0.08f, currentScale);
                    if (currentScale >= 2.98f)
                    {
                        var explosionCenter = body.Center;
                        body.AddRadialExplosion(
                            explosionCenter, 300f, 860f, dt);
                        body.DamageLine(
                            explosionCenter - Vector2.UnitX * body.Radius,
                            explosionCenter + Vector2.UnitX * body.Radius,
                            body.Radius * 0.38f, 18f, maximumBreaks: 72);
                        body.DamageLine(
                            explosionCenter - Vector2.UnitY * body.Radius,
                            explosionCenter + Vector2.UnitY * body.Radius,
                            body.Radius * 0.38f, 18f, maximumBreaks: 72);
                        var diagonal = Vector2.Normalize(new Vector2(1f, 1f)) *
                                       body.Radius;
                        body.DamageLine(
                            explosionCenter - diagonal,
                            explosionCenter + diagonal,
                            body.Radius * 0.28f, 16f, maximumBreaks: 64);
                        body.DamageLine(
                            explosionCenter - new Vector2(diagonal.X, -diagonal.Y),
                            explosionCenter + new Vector2(diagonal.X, -diagonal.Y),
                            body.Radius * 0.28f, 16f, maximumBreaks: 64);
                        body.DamageBonds(explosionCenter, body.Radius * 1.2f, 20f);
                        body.ExciseSweptBand(
                            explosionCenter,
                            explosionCenter,
                            MathF.Max(5f, body.Radius * 0.10f),
                            maximumParticles: 6);
                        if (granular is not null)
                            EmitRadialArsenalGore(
                                granular, explosionCenter, dt, 24, 180f, 520f);
                        ArsenalExplosionSerial++;
                        LastArsenalActionPosition = explosionCenter;
                        _enlargementScales.Remove(body.ParentId);
                    }
                    return true;
                }
                case ArsenalProjectileKind.Flame:
                    body.DamageBonds(impact, 8f, 0.55f);
                    body.AddLocalizedImpulse(impact, 15f, direction * 8f, dt);
                    body.RegisterHitReaction(0.55f, 0.09f);
                    IgniteBody(body, impact);
                    return true;
                case ArsenalProjectileKind.IceBolt:
                    FreezeBody(body);
                    body.AddImpulse(direction * 22f, dt);
                    return true;
                case ArsenalProjectileKind.LightningSeed:
                    ChainLightning(body, impact, bodies, tubeFeed, dt);
                    return true;
                case ArsenalProjectileKind.AcidGlob:
                {
                    var normal = impact - body.Center;
                    if (normal.LengthSquared() < 0.001f) normal = -Vector2.UnitY;
                    EmitAcidBurst(
                        granular,
                        impact,
                        velocity,
                        Vector2.Normalize(normal),
                        dt,
                        power);
                    body.DamageBonds(impact, 17f, 3.4f);
                    return true;
                }
                case ArsenalProjectileKind.WaterTear:
                {
                    body.AddLocalizedImpulse(impact, 42f,
                        direction * 290f - Vector2.UnitY * 45f, dt);
                    body.DamageBonds(impact, 12f, 0.82f);
                    body.RegisterHitReaction(1.25f, 0.18f);
                    var tearHits = _tearHitCounts.GetValueOrDefault(body.ParentId) + 1;
                    _tearHitCounts[body.ParentId] = tearHits;
                    if (tearHits >= 5)
                    {
                        var center = body.Center;
                        body.DamageLine(center - Vector2.UnitX * body.Radius,
                            center + Vector2.UnitX * body.Radius,
                            body.Radius * 0.82f, 18f, maximumBreaks: 72);
                        body.DamageLine(center - Vector2.UnitY * body.Radius,
                            center + Vector2.UnitY * body.Radius,
                            body.Radius * 0.82f, 18f, maximumBreaks: 72);
                        body.AddRadialExplosion(center, 240f, 720f, dt);
                        if (granular is not null)
                            EmitRadialArsenalGore(
                                granular, center, dt, 22, 150f, 470f);
                        _tearHitCounts.Remove(body.ParentId);
                        ArsenalExplosionSerial++;
                        LastArsenalActionPosition = center;
                    }
                    return true;
                }
                case ArsenalProjectileKind.Baseball:
                    if (_damageCooldowns.ContainsKey(body.ParentId)) return false;
                    body.DamageLine(impact - direction * 8f, impact + direction * 12f,
                        18f, 7.8f, maximumBreaks: 18);
                    body.DamageBonds(impact, 24f, 8.6f);
                    body.AddLocalizedImpulse(impact, 48f,
                        direction * Math.Clamp(velocity.Length() * 0.72f, 280f, 820f), dt);
                    velocity *= 0.64f;
                    _damageCooldowns[body.ParentId] = 0.10f;
                    PuncturedThisStep = true;
                    return false;
            }
        }
        return false;
    }

    private void ChainLightning(
        SoftBody first,
        Vector2 impact,
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed,
        float dt)
    {
        var current = first;
        var point = impact;
        var visited = new HashSet<int>();
        for (var chain = 0; chain < 4; chain++)
        {
            visited.Add(current.ParentId);
            current.DamageBonds(point, 21f, 4.2f - chain * 0.55f);
            current.AddLocalizedImpulse(point, 30f,
                new Vector2((chain % 2 == 0 ? 1f : -1f) * 65f, -150f), dt);
            SoftBody? next = null;
            var best = 155f * 155f;
            foreach (var candidate in bodies)
            {
                if (visited.Contains(candidate.ParentId) || candidate.IsDetachedDebris ||
                    tubeFeed?.Contains(candidate) == true)
                    continue;
                var distance = Vector2.DistanceSquared(current.Center, candidate.Center);
                if (distance >= best) continue;
                best = distance;
                next = candidate;
            }
            if (next is null) break;
            AddArsenalActionEffect(18, point, next.Center, 0.14f, chain);
            current = next;
            point = current.Center;
        }
        PuncturedThisStep = true;
    }

    private static float ProjectileRadius(ArsenalProjectileKind kind) => kind switch
    {
        ArsenalProjectileKind.Nail => 3.2f,
        ArsenalProjectileKind.ShotgunPellet => 2.0f,
        ArsenalProjectileKind.MagnumBullet => 2.7f,
        ArsenalProjectileKind.SmgBullet => 1.8f,
        ArsenalProjectileKind.SawBlade => 11f,
        ArsenalProjectileKind.Grenade => 4f,
        ArsenalProjectileKind.BlackHole => 12f,
        ArsenalProjectileKind.Rat => 9.5f,
        ArsenalProjectileKind.GrowthPulse => 5f,
        ArsenalProjectileKind.Flame => 6f,
        ArsenalProjectileKind.IceBolt => 8f,
        ArsenalProjectileKind.LightningSeed => 6f,
        ArsenalProjectileKind.AcidGlob => 10f,
        ArsenalProjectileKind.WaterTear => 11f,
        ArsenalProjectileKind.Baseball => 6f,
        _ => 2f
    };

    private static (float Thickness, float Damage, int MaximumBreaks, float Impulse)
        ProjectileDamage(ArsenalProjectileKind kind, float power) => kind switch
    {
        ArsenalProjectileKind.Nail => (5.4f, 2.5f * power, 4, 210f),
        ArsenalProjectileKind.ShotgunPellet => (4.2f, 1.8f * power, 3, 28f),
        ArsenalProjectileKind.MagnumBullet => (3.8f, 2.8f * power, 8, 110f),
        ArsenalProjectileKind.SmgBullet => (4.2f, 8f * power, 4, 12f),
        _ => (2f, 0.5f, 1, 2f)
    };

    private static bool ResolveProjectileEnvironment(Vector2 previous, ref Vector2 position,
        ref Vector2 velocity, float radius, float restitution, float dt,
        IReadOnlyList<ConveyorBelt> conveyors, float worldWidth, float worldHeight,
        DestructibleGrid? grid, out bool bounced, out Vector2 contactNormal)
    {
        var hit = false;
        var didBounce = false;
        var resolvedVelocity = velocity;
        var resolvedNormal = Vector2.Zero;
        if (grid is not null)
        {
            var particle = new Particle
            {
                Position = position,
                PreviousPosition = previous,
                Radius = radius,
                InverseMass = 1f
            };
            var collision = grid.ResolveParticle(ref particle, dt);
            if (collision.Hit)
            {
                position = particle.Position;
                Reflect(collision.Normal);
            }
        }

        foreach (var conveyor in conveyors)
        {
            if (position.X < conveyor.Position.X - radius ||
                position.X > conveyor.Position.X + conveyor.Width + radius ||
                position.Y < conveyor.Position.Y - radius - MathF.Abs(velocity.Y * dt) ||
                position.Y > conveyor.Position.Y + conveyor.Height + radius)
                continue;
            var particle = new Particle
            {
                Position = position,
                PreviousPosition = previous,
                Radius = radius,
                InverseMass = 1f
            };
            var contact = conveyor.ResolveParticle(ref particle, dt, applyBeltVelocity: false);
            if (!contact.Hit) continue;
            position = particle.Position;
            Reflect(contact.Normal);
        }

        if (position.X < radius || position.X > worldWidth - radius)
        {
            position.X = Math.Clamp(position.X, radius, worldWidth - radius);
            Reflect(position.X <= radius ? Vector2.UnitX : -Vector2.UnitX);
        }
        if (position.Y < radius || position.Y > worldHeight - radius)
        {
            position.Y = Math.Clamp(position.Y, radius, worldHeight - radius);
            Reflect(position.Y <= radius ? Vector2.UnitY : -Vector2.UnitY);
        }
        velocity = resolvedVelocity;
        bounced = didBounce;
        contactNormal = resolvedNormal;
        return hit;

        void Reflect(Vector2 normal)
        {
            hit = true;
            if (normal.LengthSquared() > 0.001f)
                resolvedNormal = Vector2.Normalize(normal);
            didBounce = restitution > 0f;
            if (normal.LengthSquared() < 0.001f || restitution <= 0f) return;
            normal = Vector2.Normalize(normal);
            var normalSpeed = Vector2.Dot(resolvedVelocity, normal);
            if (normalSpeed >= 0f) return;
            var tangent = resolvedVelocity - normal * normalSpeed;
            resolvedVelocity = tangent * 0.82f - normal * normalSpeed * restitution;
        }
    }

    private void BuildGrenadeTrajectory(Vector2 gravity, IReadOnlyList<ConveyorBelt> conveyors,
        float worldWidth, float worldHeight, DestructibleGrid? grid)
    {
        _grenadeTrajectory.Clear();
        const float previewDt = 1f / 120f;
        const float remaining = 1.8f;
        var position = Position + _grenadeThrowDirection * 18f;
        var velocity = _grenadeThrowDirection * _grenadeThrowSpeed + _gripVelocity * 0.18f;
        _grenadeTrajectory.Add(new GrenadeTrajectoryPoint(position, false, false));
        var steps = Math.Min(240, (int)MathF.Ceiling(remaining / previewDt));
        for (var step = 1; step <= steps; step++)
        {
            var previous = position;
            velocity += gravity * (previewDt * 0.72f);
            position += velocity * previewDt;
            ResolveProjectileEnvironment(previous, ref position, ref velocity, 4f, 0.46f,
                previewDt, conveyors, worldWidth, worldHeight, grid, out var bounced, out _);
            if ((step % 5 == 0 || bounced) && _grenadeTrajectory.Count < 48)
                _grenadeTrajectory.Add(new GrenadeTrajectoryPoint(position, bounced, false));
        }
        if (_grenadeTrajectory.Count == 0)
            _grenadeTrajectory.Add(new GrenadeTrajectoryPoint(position, false, true));
        else
        {
            var last = _grenadeTrajectory[^1];
            if (Vector2.DistanceSquared(last.Position, position) > 1f && _grenadeTrajectory.Count < 48)
                _grenadeTrajectory.Add(new GrenadeTrajectoryPoint(position, false, true));
            else
                _grenadeTrajectory[^1] = last with { Position = position, Final = true };
        }
    }

    private void ExplodeGrenade(Vector2 position, IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed, float dt)
    {
        const float radius = 138f;
        foreach (var body in bodies)
        {
            if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true) continue;
            var offset = body.Center - position;
            var distance = offset.Length();
            if (distance > radius + body.Radius) continue;
            if (IsBlastOccluded(position, body, bodies, tubeFeed)) continue;
            var direction = distance > 0.01f ? offset / distance : -Vector2.UnitY;
            var strength = Math.Clamp(1f - distance / radius, 0.12f, 1f);
            var face = body.Center - direction * body.Radius * 0.72f;
            var tangent = new Vector2(-direction.Y, direction.X);
            body.DamageLine(
                face - tangent * (10f + strength * 12f),
                face + tangent * (10f + strength * 12f),
                12f + strength * 11f,
                1.7f + strength * 4.2f,
                maximumBreaks: 8 + (int)MathF.Round(strength * 16f));
            body.DamageBonds(face, 18f + strength * 18f, 3.1f + strength * 6f);
            body.AddLocalizedImpulse(
                face,
                58f + strength * 34f,
                direction * (420f + strength * 720f) - Vector2.UnitY * (90f * strength),
                dt);
            body.AddRadialExplosion(
                position,
                130f + strength * 100f,
                420f + strength * 420f,
                dt);
            body.RegisterHitReaction(1.2f + strength, 0.22f);
        }
        LastArsenalActionPosition = position;
        AddArsenalActionEffect(11, position, position, 0.14f, radius);
        ArsenalExplosionSerial++;
    }

    private static bool IsBlastOccluded(
        Vector2 origin,
        SoftBody target,
        IReadOnlyList<SoftBody> bodies,
        OverheadTubeFeed? tubeFeed)
    {
        var ray = target.Center - origin;
        var rayLengthSquared = ray.LengthSquared();
        if (rayLengthSquared < 1f) return false;
        foreach (var blocker in bodies)
        {
            if (ReferenceEquals(blocker, target) || blocker.IsDetachedDebris ||
                tubeFeed?.Contains(blocker) == true)
                continue;
            var amount = Vector2.Dot(blocker.Center - origin, ray) / rayLengthSquared;
            if (amount <= 0.06f || amount >= 0.92f) continue;
            var closest = origin + ray * amount;
            var shieldRadius = blocker.Radius * 0.78f;
            if (Vector2.DistanceSquared(blocker.Center, closest) <=
                shieldRadius * shieldRadius)
                return true;
        }
        return false;
    }

    private void ResolveTubeGlass(OverheadTubeFeed? tubeFeed, float dt)
    {
        if (tubeFeed is null || IsHolstered || IsReturningToHolster) return;
        var uppermost = MathF.Min(
            MathF.Min(HandleStart.Y - 7f, HandleEnd.Y - 7f),
            MathF.Min(BladeCoreStart.Y - 12f, BladeCoreEnd.Y - 12f));
        if (uppermost >= OverheadTubeFeed.GlassBottom) return;
        var correction = OverheadTubeFeed.GlassBottom - uppermost;
        Position = new Vector2(Position.X, Position.Y + correction);
        _previousPosition = new Vector2(_previousPosition.X,
            MathF.Min(Position.Y, _previousPosition.Y + correction));
        if (IsGrabbed) _gripVelocity.Y = MathF.Max(0f, _gripVelocity.Y);
        else
        {
            var velocity = Position - _previousPosition;
            velocity.Y = MathF.Abs(velocity.Y) * 0.24f;
            _previousPosition = Position - velocity;
        }
    }

    private void ResolveBlobContacts(float dt, IReadOnlyList<SoftBody> bodies,
        Vector2 displacement, OverheadTubeFeed? tubeFeed)
    {
        var handleStart = HandleStart;
        var handleEnd = HandleEnd;
        var bladeCoreStart = BladeCoreStart;
        var bladeCoreEnd = BladeCoreEnd;
        var bladeEdgeStart = BladeEdgeStart;
        var bladeEdgeEnd = BladeEdgeEnd;
        var bladeCollisionStart = ArsenalVisualVariant < 0 ? bladeEdgeStart : bladeCoreStart;
        var bladeCollisionEnd = ArsenalVisualVariant < 0 ? bladeEdgeEnd : bladeCoreEnd;
        var bladeCollisionRadius = ArsenalVisualVariant == 7
            ? SledgeFaceCollisionRadius
            : 12f;
        var edgeTangent = bladeEdgeEnd - bladeEdgeStart;
        if (edgeTangent.LengthSquared() < 0.001f) edgeTangent = -Vector2.UnitX;
        else edgeTangent = Vector2.Normalize(edgeTangent);
        var toolDirection = new Vector2(-edgeTangent.Y, edgeTangent.X);
        if (Vector2.Dot(toolDirection, _chopDirection) < 0f) toolDirection = -toolDirection;
        var safeDt = MathF.Max(dt, 0.0001f);
        var edgeMidpoint = (bladeEdgeStart + bladeEdgeEnd) * 0.5f;
        var edgeOffset = edgeMidpoint - Position;
        var angularEdgeVelocity = new Vector2(-edgeOffset.Y, edgeOffset.X) * _angularVelocity;
        var worldVelocity = displacement / safeDt + angularEdgeVelocity;
        var forwardSpeed = Vector2.Dot(worldVelocity, toolDirection);
        var lateralSpeed = MathF.Abs(worldVelocity.X * toolDirection.Y - worldVelocity.Y * toolDirection.X);
        var validSwingDirection = Vector2.Dot(toolDirection, _chopDirection) >= 0.72f;
        var canBladeHit = IsGrabbed && ControlState == CleaverControlState.Swing &&
                          !_strongHitConsumed && _controlStateTime <= 0.42f &&
                          forwardSpeed >= 105f && validSwingDirection &&
                          lateralSpeed <= forwardSpeed * 1.35f &&
                          (ArsenalVisualVariant != 0 || _saberIgnited);
        var reaction = Vector2.Zero;
        var sledgeSwinging = ArsenalVisualVariant == 7 && IsGrabbed &&
                             ControlState == CleaverControlState.Swing;

        foreach (var body in bodies)
        {
            var nearCurrentTool = MathF.Min(
                DistanceToSegment(body.Center, handleStart, handleEnd),
                DistanceToSegment(body.Center, bladeCollisionStart, bladeCollisionEnd)) <=
                body.Radius + 20f;
            var nearSledgeSweep = sledgeSwinging &&
                                  Vector2.DistanceSquared(body.Center, Position) <=
                                  (body.Radius + 96f) * (body.Radius + 96f);
            if (body.IsDetachedDebris || tubeFeed?.Contains(body) == true ||
                (!nearCurrentTool && !nearSledgeSweep))
                continue;

            var bodyContact = false;
            var edgeContact = false;
            var edgeContactPoint = bladeEdgeEnd;
            var closestEdgeDistance = float.PositiveInfinity;
            var edgeRelativeSpeed = 0f;
            for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
            {
                if (!body.IsPhysicalParticle(particleIndex)) continue;
                ref var particle = ref body.Particles[particleIndex];
                var beforeBlade = particle.Position;
                if (sledgeSwinging &&
                    TrySledgeSweptHeadContact(beforeBlade, particle.Radius,
                        out var sweptDistanceSquared) &&
                    sweptDistanceSquared < closestEdgeDistance)
                {
                    // Swept contact prevents the accelerating head from tunnelling
                    // through thin or already-deformed tissue at 120 Hz.
                    closestEdgeDistance = sweptDistanceSquared;
                    edgeContactPoint = beforeBlade;
                    edgeContact = true;
                    bodyContact = true;
                }
                if (ResolveCapsuleContact(ref particle, handleStart, handleEnd, 7f,
                        displacement, ref reaction))
                {
                    bodyContact = true;
                }

                if (!(ArsenalVisualVariant == 0 && _saberIgnited) &&
                    ArsenalVisualVariant != 9 &&
                    ResolveCapsuleContact(ref particle, bladeCollisionStart, bladeCollisionEnd,
                        bladeCollisionRadius,
                        displacement, ref reaction))
                {
                    bodyContact = true;
                }
                if (ArsenalVisualVariant == 7)
                {
                    // A sledge damages with the whole broad hammer head, not the
                    // cleaver-era hairline edge embedded in the shared tool rig.
                    var headPoint = ClosestPoint(
                        beforeBlade, bladeCollisionStart, bladeCollisionEnd);
                    var headDistance = Vector2.DistanceSquared(beforeBlade, headPoint);
                    var headRadius = particle.Radius + bladeCollisionRadius;
                    if (headDistance <= headRadius * headRadius &&
                        headDistance < closestEdgeDistance)
                    {
                        closestEdgeDistance = headDistance;
                        // Damage at the contacted tissue node. The closest point on
                        // the hammer capsule can sit outside visible tissue after
                        // collision separation, which made valid blows look like
                        // they passed through without crushing anything.
                        edgeContactPoint = beforeBlade;
                        edgeContact = true;
                    }
                }
                if (ArsenalVisualVariant < 0 &&
                    ResolveCapsuleContact(ref particle, bladeEdgeStart, bladeEdgeEnd, 3.2f,
                        displacement, ref reaction))
                {
                    bodyContact = true;
                }

                var edgeDistance = Vector2.DistanceSquared(beforeBlade,
                    ClosestPoint(beforeBlade, bladeEdgeStart, bladeEdgeEnd));
                var edgeRadius = particle.Radius + 3.2f;
                if (edgeDistance > edgeRadius * edgeRadius || edgeDistance >= closestEdgeDistance) continue;
                closestEdgeDistance = edgeDistance;
                edgeContactPoint = beforeBlade;
                edgeContact = true;
                edgeRelativeSpeed = MathF.Max(edgeRelativeSpeed,
                    (beforeBlade - particle.PreviousPosition).Length() / safeDt);
            }

            if (ArsenalVisualVariant == 9)
            {
                // Pike tips can sit inside the filled lattice rather than touching a
                // perimeter contact node. Match the actual material spacing so an
                // interior tissue particle still registers a physical impalement.
                var materialRadius = body.ParticleSpacing * 1.6f;
                var materialRadiusSquared = materialRadius * materialRadius;
                for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
                {
                    var materialPoint = body.Particles[particleIndex].Position;
                    var closest = ClosestPoint(materialPoint, bladeEdgeStart, bladeEdgeEnd);
                    var distanceSquared = Vector2.DistanceSquared(materialPoint, closest);
                    if (distanceSquared > materialRadiusSquared) continue;
                    edgeContact = true;
                    bodyContact = true;
                    edgeContactPoint = closest;
                    edgeRelativeSpeed = MathF.Max(
                        edgeRelativeSpeed, body.AverageVelocity(dt).Length());
                    break;
                }
            }
            if (ArsenalVisualVariant == 0 && _saberIgnited && edgeContact)
                bodyContact = true;

            if (!bodyContact) continue;
            BlobContactsThisStep++;
            body.Wake();
            if (_damageCooldowns.ContainsKey(body.ParentId)) continue;

            var alreadyPikePinned = ArsenalVisualVariant == 9 &&
                                    _pikePins.Any(pin => pin.Body.ParentId == body.ParentId);
            var pikeImpact = ArsenalVisualVariant == 9 && edgeContact &&
                             edgeRelativeSpeed >= 8f && !alreadyPikePinned;
            var sledgeImpact = ArsenalVisualVariant == 7 && IsGrabbed &&
                               ControlState == CleaverControlState.Swing &&
                               _controlStateTime >= 0.035f &&
                               _controlStateTime <= SledgeSlamSeconds &&
                               edgeContact;
            var axeImpact = ArsenalVisualVariant == 12 && _arsenalPrimaryHeld && edgeContact &&
                            MathF.Abs(_angularVelocity) >= 5.5f;
            var saberImpact = ArsenalVisualVariant == 0 && _saberIgnited && edgeContact;
            if ((canBladeHit || pikeImpact || sledgeImpact || axeImpact || saberImpact) && edgeContact)
            {
                var impactStrength = _windupStrength;
                if (ArsenalVisualVariant == 0 && _saberIgnited)
                {
                    body.DamageLine(bladeEdgeStart, bladeEdgeEnd, 9f,
                        5.8f, maximumBreaks: 28);
                    body.DamageBonds(edgeContactPoint, 13f, 6.5f);
                }
                else if (ArsenalVisualVariant == 7)
                {
                    var smash = Math.Clamp(_windupStrength, 0.32f, 1f);
                    var slamProgress = Math.Clamp(
                        _controlStateTime / SledgeSlamSeconds, 0.18f, 1f);
                    var downwardImpulse = new Vector2(
                        Math.Clamp(edgeContactPoint.X - Position.X, -42f, 42f) *
                        (1.2f + smash),
                        760f + smash * 820f + slamProgress * 360f);
                    body.AddLocalizedImpulse(edgeContactPoint, 56f + smash * 34f,
                        downwardImpulse, dt);
                    body.AddImpulse(new Vector2(
                        downwardImpulse.X * 0.08f,
                        42f + smash * 68f), dt);
                }
                else if (pikeImpact)
                {
                    body.DamageLine(edgeContactPoint, edgeContactPoint, 4.5f,
                        1.25f, maximumBreaks: 1);
                    body.DamageBonds(edgeContactPoint, 5.5f, 1.2f);
                    body.AddImpulse(-toolDirection * 0.7f, dt);
                    if (IsDeployed)
                    {
                        var particleIndex = FindNearestPhysicalParticle(
                            body, edgeContactPoint);
                        if (particleIndex < 0) continue;
                        var anchor = ClosestPoint(
                            body.Particles[particleIndex].Position,
                            bladeEdgeStart,
                            bladeEdgeEnd);
                        if (_pikePins.Count >= 8) _pikePins.RemoveAt(0);
                        _pikePins.Add(new PikePin(
                            body,
                            particleIndex,
                            anchor));
                    }
                }
                else if (axeImpact)
                {
                    body.DamageLine(bladeEdgeStart, bladeEdgeEnd, 7.5f,
                        1.58f, maximumBreaks: 7);
                    body.AddImpulse(toolDirection * 11f, dt);
                }
                else
                {
                    body.DamageLine(edgeContactPoint, edgeContactPoint, 10.5f,
                        1.34f + _windupStrength * 0.42f, maximumBreaks: 5);
                    body.AddImpulse(toolDirection * Math.Clamp(forwardSpeed * 0.035f, 4f, 14f), dt);
                }
                DepositCuttingEdgeBlood(edgeContactPoint, 0.52f);
                _damageCooldowns[body.ParentId] = saberImpact ? 0.035f : axeImpact ? 0.075f : 0.12f;
                if (!axeImpact && !pikeImpact && !saberImpact &&
                    ArsenalVisualVariant != 7)
                {
                    _strongHitConsumed = true;
                    ControlState = CleaverControlState.Impact;
                    _controlStateTime = 0f;
                    _gripVelocity *= 0.22f;
                    _angularVelocity *= 0.30f;
                }
                PuncturedThisStep = true;
                if (_primaryActionSwing && impactStrength >= 0.72f)
                {
                    _heavyImpactAge = 0f;
                    _heavyImpactStrength = impactStrength;
                    HeavyImpactPosition = edgeContactPoint;
                    HeavyImpactAngle = MathF.Atan2(toolDirection.Y, toolDirection.X) - MathF.PI * 0.5f;
                }
            }
        }

        var reactionLength = reaction.Length();
        if (reactionLength > 8f) reaction *= 8f / reactionLength;
        if (!IsDeployed)
            Position += reaction;
    }

    private bool TrySledgeSweptHeadContact(
        Vector2 tissuePoint, float tissueRadius, out float closestDistanceSquared)
    {
        closestDistanceSquared = float.PositiveInfinity;
        const int sweepSamples = 7;
        for (var sample = 0; sample <= sweepSamples; sample++)
        {
            var amount = sample / (float)sweepSamples;
            var samplePosition = Vector2.Lerp(_previousPosition, Position, amount);
            var sampleAngle = _previousAngle + (Angle - _previousAngle) * amount;
            var headStart = samplePosition + Rotate(LocalEdgeStart, sampleAngle);
            var headEnd = samplePosition + Rotate(LocalEdgeEnd, sampleAngle);
            var closest = ClosestPoint(tissuePoint, headStart, headEnd);
            closestDistanceSquared = MathF.Min(
                closestDistanceSquared,
                Vector2.DistanceSquared(tissuePoint, closest));
        }

        var combinedRadius = tissueRadius + SledgeFaceCollisionRadius;
        return closestDistanceSquared <= combinedRadius * combinedRadius;
    }

    private static float ResolveSledgeGroundSurfaceY(
        Vector2 slamStart,
        IReadOnlyList<ConveyorBelt> conveyors,
        DestructibleGrid? grid,
        float worldHeight)
    {
        var surfaceY = worldHeight - 8f;
        foreach (var conveyor in conveyors)
        {
            if (slamStart.X < conveyor.Position.X - 20f ||
                slamStart.X > conveyor.Position.X + conveyor.Width + 20f ||
                conveyor.Position.Y < slamStart.Y + 24f ||
                conveyor.Position.Y >= surfaceY)
                continue;
            surfaceY = conveyor.Position.Y;
        }

        if (grid is null) return surfaceY;
        var cellX = Math.Clamp(
            (int)MathF.Floor(slamStart.X / grid.CellSize),
            0,
            grid.Columns - 1);
        var firstRow = Math.Clamp(
            (int)MathF.Floor((slamStart.Y + 24f) / grid.CellSize),
            0,
            grid.Rows - 1);
        for (var row = firstRow; row < grid.Rows; row++)
        {
            if (!grid.Cell(cellX, row).IsSolid) continue;
            surfaceY = MathF.Min(surfaceY, row * grid.CellSize);
            break;
        }
        return surfaceY;
    }

    private int CrushSledgeAtSurface(
        IReadOnlyList<SoftBody> bodies,
        float impactX,
        float surfaceY,
        float dt,
        float strength)
    {
        var smash = Math.Clamp(strength, 0.32f, 1f);
        _sledgeCrushedParentIds.Clear();
        var halfWidth = 36f + smash * 10f;
        // Remove a thick ground-side band, not the full body height. Repeated
        // heavy blows can finish a blob, but one ordinary smash leaves material
        // above the crushed face unless the blob was already very small.
        var crushDepth = 20f + smash * 16f;
        var crushed = 0;
        foreach (var body in bodies)
        {
            if (body.IsDetachedDebris ||
                MathF.Abs(body.Center.X - impactX) > body.Radius + halfWidth ||
                body.Center.Y - body.Radius > surfaceY + 8f ||
                body.Center.Y + body.Radius < surfaceY - crushDepth - 10f)
                continue;
            var removed = body.CrushAgainstSurface(
                new Vector2(impactX, surfaceY),
                halfWidth,
                crushDepth,
                surfaceY,
                3.8f + smash * 3.2f);
            if (removed <= 0) continue;
            crushed += removed;
            _sledgeCrushedParentIds.Add(body.ParentId);
            body.AddLocalizedImpulse(
                new Vector2(impactX, surfaceY - crushDepth * 0.55f),
                halfWidth + 20f,
                new Vector2(0f, 980f + smash * 820f),
                dt);
        }
        return crushed;
    }

    private void EnterSledgeImpact(
        Vector2 impactPoint,
        float strength,
        int crushedParticles,
        IReadOnlyList<SoftBody> bodies,
        GranularMaterialSystem? granular,
        float dt)
    {
        if (ArsenalVisualVariant != 7 ||
            ControlState != CleaverControlState.Swing)
            return;

        var smash = Math.Clamp(strength, 0.32f, 1f);
        _sledgeImpactAngle = _sledgeSwingRight ? MathF.PI : SledgeGroundAngle;
        var localFaceCenter = Rotate((LocalEdgeStart + LocalEdgeEnd) * 0.5f,
            _sledgeImpactAngle);
        var localFaceBottom = MathF.Max(
            Rotate(LocalEdgeStart, _sledgeImpactAngle).Y,
            Rotate(LocalEdgeEnd, _sledgeImpactAngle).Y);
        Position = new Vector2(
            impactPoint.X - localFaceCenter.X,
            impactPoint.Y - localFaceBottom);
        _previousPosition = Position;
        Angle = _sledgeImpactAngle;
        _previousAngle = Angle;
        _sledgeImpactGripPosition = Position;
        _strongHitConsumed = true;
        ControlState = CleaverControlState.Impact;
        _controlStateTime = 0f;
        _gripVelocity = Vector2.Zero;
        _angularVelocity = 0f;
        _heavyImpactAge = 0f;
        _heavyImpactStrength = 0.62f + smash * 0.38f;
        HeavyImpactPosition = impactPoint;
        HeavyImpactAngle = 0f;
        if (crushedParticles > 0)
            CreateHeavyBloodBridges(impactPoint, smash, crushedParticles);
        BounceSledgeSurfaceMatter(
            bodies,
            granular,
            impactPoint,
            dt,
            smash,
            crushedParticles > 0);
        _sledgeAftershockPoint = impactPoint;
        _sledgeAftershockStrength = smash;
        _sledgeGranularAftershockPending = crushedParticles > 0 ||
                                           smash >= 0.985f;
    }

    private void BounceSledgeSurfaceMatter(
        IReadOnlyList<SoftBody> bodies,
        GranularMaterialSystem? granular,
        Vector2 impactPoint,
        float dt,
        float strength,
        bool struckMatter)
    {
        var fullCharge = strength >= 0.985f;
        var localRadius = 74f + strength * 42f;
        foreach (var body in bodies)
        {
            var bottom = body.Center.Y + body.Radius;
            if (MathF.Abs(bottom - impactPoint.Y) > 54f) continue;
            if (!fullCharge &&
                (!struckMatter ||
                 !_sledgeCrushedParentIds.Contains(body.ParentId)))
                continue;

            var heightSample = NextArsenal01();
            var lateralSample = NextArsenal01() * 2f - 1f;
            var upwardSpeed = fullCharge
                ? 245f + heightSample * 195f
                : 165f + heightSample * 125f;
            var lateralSpeed = lateralSample * (fullCharge ? 58f : 30f);
            body.AddImpulse(new Vector2(lateralSpeed, -upwardSpeed), dt);
        }

        BounceSledgeGranular(
            granular,
            impactPoint,
            dt,
            strength,
            struckMatter);
    }

    private void BounceSledgeGranular(
        GranularMaterialSystem? granular,
        Vector2 impactPoint,
        float dt,
        float strength,
        bool struckMatter)
    {
        if (granular is null) return;
        var fullCharge = strength >= 0.985f;
        var localRadius = 74f + strength * 42f;
        for (var index = 0; index < granular.Particles.Count; index++)
        {
            var particle = granular.Particles[index];
            if (particle.InContinuousDrain ||
                particle.Position.Y < impactPoint.Y - 34f ||
                particle.Position.Y > impactPoint.Y + 12f ||
                !fullCharge &&
                (!struckMatter ||
                 MathF.Abs(particle.Position.X - impactPoint.X) > localRadius))
                continue;

            var velocity = (particle.Position - particle.PreviousPosition) /
                           MathF.Max(dt, 0.0001f);
            var heightSample = NextArsenal01();
            velocity.Y = -(fullCharge
                ? 235f + heightSample * 220f
                : 145f + heightSample * 145f);
            velocity.X += (NextArsenal01() * 2f - 1f) *
                          (fullCharge ? 64f : 34f);
            particle.PreviousPosition = particle.Position - velocity * dt;
            particle.RestFrames = 0;
            granular.Particles[index] = particle;
        }
    }

    private void CreateHeavyBloodBridges(
        Vector2 impactPoint, float strength, int crushedParticles)
    {
        var count = Math.Clamp(1 + crushedParticles / 7, 2, 3);
        for (var index = 0; index < count; index++)
        {
            if (_heavyBloodBridges.Count >= 8) _heavyBloodBridges.RemoveAt(0);
            var amount = count <= 1 ? 0.5f : index / (float)(count - 1);
            var localAnchor = Vector2.Lerp(LocalEdgeStart, LocalEdgeEnd, amount);
            var spread = (amount - 0.5f) * (50f + strength * 22f);
            var variation = (byte)(_stainSerial++ & 7);
            var groundAnchor = new Vector2(
                impactPoint.X + spread + ((variation & 1) == 0 ? -2f : 2f),
                impactPoint.Y - 1f);
            // The bridge survives the impact hold and only the first part of
            // lift-off. It snaps before the actual return-to-ready recovery.
            var lifetime = 0.24f + strength * 0.035f +
                           (variation % 2) * 0.012f;
            _heavyBloodBridges.Add(new HeavyBloodBridge(
                localAnchor,
                groundAnchor,
                lifetime,
                lifetime,
                4.0f + strength * 1.7f + (variation & 1) * 0.55f,
                variation));
        }
    }

    private void UpdateHeavyBloodBridges(float dt)
    {
        for (var index = _heavyBloodBridges.Count - 1; index >= 0; index--)
        {
            var bridge = _heavyBloodBridges[index];
            var remaining = bridge.RemainingSeconds - dt;
            if (remaining <= 0f)
                _heavyBloodBridges.RemoveAt(index);
            else
                _heavyBloodBridges[index] = bridge with { RemainingSeconds = remaining };
        }
    }

    public bool ResolveBloodContact(ref GranularParticle blood, float dt)
    {
        if (!Visible || blood.Kind != GranularKind.Blood) return false;
        if ((uint)ArsenalVisualVariant < ArsenalLocalBounds.Length)
            return ResolveArsenalBloodContact(ref blood, dt);

        var handleClosest = ClosestPoint(blood.Position, HandleStart, HandleEnd);
        var bladeClosest = ClosestPoint(blood.Position, BladeCoreStart, BladeCoreEnd);
        var edgeClosest = ClosestPoint(blood.Position, BladeEdgeStart, BladeEdgeEnd);
        var handleDistanceSq = Vector2.DistanceSquared(blood.Position, handleClosest);
        var bladeDistanceSq = Vector2.DistanceSquared(blood.Position, bladeClosest);
        var edgeDistanceSq = Vector2.DistanceSquared(blood.Position, edgeClosest);
        var touchesEdge = edgeDistanceSq <= bladeDistanceSq && edgeDistanceSq <= handleDistanceSq;
        var touchesBlade = touchesEdge || bladeDistanceSq <= handleDistanceSq;
        var closest = touchesEdge ? edgeClosest : touchesBlade ? bladeClosest : handleClosest;
        var toolRadius = touchesEdge ? 3.2f : touchesBlade ? 12f : 7f;
        var combinedRadius = toolRadius + blood.Radius;
        var delta = blood.Position - closest;
        var distanceSq = delta.LengthSquared();
        if (distanceSq >= combinedRadius * combinedRadius) return false;

        var normal = distanceSq > 0.0001f
            ? delta / MathF.Sqrt(distanceSq)
            : Vector2.UnitY;
        var surfacePoint = closest + normal * toolRadius;
        var previousVelocity = blood.Position - blood.PreviousPosition;
        blood.Position = closest + normal * combinedRadius;
        var normalTravel = Vector2.Dot(previousVelocity, normal);
        var reflected = normalTravel < 0f
            ? previousVelocity - normal * normalTravel * 1.08f
            : previousVelocity;
        reflected *= 0.34f;
        blood.PreviousPosition = blood.Position - reflected;
        blood.RestFrames = 0;

        var local = ToAuthoredLocal(surfacePoint - Position);
        local = touchesBlade
            ? new Vector2(Math.Clamp(local.X, -51f, -9f), Math.Clamp(local.Y, -6f, 20f))
            : new Vector2(Math.Clamp(local.X, -8f, 13f), Math.Clamp(local.Y, -6f, 7f));
        DepositBlood(local, 0.12f + Math.Clamp(previousVelocity.Length() / MathF.Max(dt, 0.0001f) * 0.0008f, 0f, 0.28f));
        return true;
    }

    private bool ResolveArsenalBloodContact(ref GranularParticle blood, float dt)
    {
        var local = ToAuthoredLocal(blood.Position - Position);
        var radius = blood.Radius;
        if (ArsenalVisualVariant == 0 && _saberIgnited)
        {
            var bladePoint = ClosestPoint(local, LocalEdgeStart, LocalEdgeEnd);
            var bladeRadius = radius + 3.2f;
            if (local.X <= -18f &&
                Vector2.DistanceSquared(local, bladePoint) <= bladeRadius * bladeRadius)
            {
                // Each real granular pixel is consumed independently on hot-blade
                // contact. Blob tissue is still cut by the material solver; this does
                // not vaporize wound blood wholesale or suppress normal bleeding.
                blood.Position = Position + Rotate(bladePoint, Angle);
                blood.PreviousPosition = blood.Position;
                blood.Lifetime = 0f;
                blood.RestFrames = 0;
                AddArsenalActionEffect(13, blood.Position,
                    blood.Position + new Vector2((_stainSerial & 1) == 0 ? -2f : 2f, -8f),
                    0.095f, radius);
                AddSmoke(blood.Position, SmokeKind.Saber, 0.46f);
                SaberSizzleSerial++;
                _stainSerial++;
                return true;
            }
        }

        // The inactive blade is not physically present, and an ignited blade burns
        // droplets away. Only the authored hilt/collar can retain a blood stain.
        var bounds = ArsenalVisualVariant == 0
            ? SaberHiltBounds
            : ArsenalLocalBounds[ArsenalVisualVariant];
        if (local.X < bounds.MinX - radius || local.X > bounds.MaxX + radius ||
            local.Y < bounds.MinY - radius || local.Y > bounds.MaxY + radius)
            return false;

        var clamped = new Vector2(
            Math.Clamp(local.X, bounds.MinX, bounds.MaxX),
            Math.Clamp(local.Y, bounds.MinY, bounds.MaxY));
        Vector2 localNormal;
        if (local.X >= bounds.MinX && local.X <= bounds.MaxX &&
            local.Y >= bounds.MinY && local.Y <= bounds.MaxY)
        {
            var left = local.X - bounds.MinX;
            var right = bounds.MaxX - local.X;
            var top = local.Y - bounds.MinY;
            var bottom = bounds.MaxY - local.Y;
            var nearest = MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));
            if (nearest == left)
            {
                clamped.X = bounds.MinX;
                localNormal = -Vector2.UnitX;
            }
            else if (nearest == right)
            {
                clamped.X = bounds.MaxX;
                localNormal = Vector2.UnitX;
            }
            else if (nearest == top)
            {
                clamped.Y = bounds.MinY;
                localNormal = -Vector2.UnitY;
            }
            else
            {
                clamped.Y = bounds.MaxY;
                localNormal = Vector2.UnitY;
            }
        }
        else
        {
            var delta = local - clamped;
            if (delta.LengthSquared() < 0.0001f) localNormal = -Vector2.UnitY;
            else localNormal = Vector2.Normalize(delta);
        }

        var normal = Rotate(localNormal, Angle);
        var surfacePoint = Position + Rotate(clamped, Angle);
        var previousVelocity = blood.Position - blood.PreviousPosition;
        blood.Position = surfacePoint + normal * radius;
        var normalTravel = Vector2.Dot(previousVelocity, normal);
        var reflected = normalTravel < 0f
            ? previousVelocity - normal * normalTravel * 1.08f
            : previousVelocity;
        reflected *= 0.34f;
        blood.PreviousPosition = blood.Position - reflected;
        blood.RestFrames = 0;
        DepositBlood(clamped, 0.12f + Math.Clamp(
            previousVelocity.Length() / MathF.Max(dt, 0.0001f) * 0.0008f, 0f, 0.28f));
        return true;
    }

    private void DepositCuttingEdgeBlood(Vector2 worldPoint, float amount)
    {
        if (ArsenalVisualVariant == 0 && _saberIgnited)
            return;
        var local = ToAuthoredLocal(worldPoint - Position);
        if ((uint)ArsenalVisualVariant < ArsenalLocalBounds.Length)
        {
            var bounds = ArsenalLocalBounds[ArsenalVisualVariant];
            local = new Vector2(
                Math.Clamp(local.X, bounds.MinX, bounds.MaxX),
                Math.Clamp(local.Y, bounds.MinY, bounds.MaxY));
        }
        else
            local = new Vector2(Math.Clamp(local.X, -51f, -9f), 19f);
        DepositBlood(local, amount);
    }

    private void DepositBlood(Vector2 localPosition, float amount)
    {
        var searchStart = Math.Max(0, _bloodStains.Count - 12);
        for (var i = _bloodStains.Count - 1; i >= searchStart; i--)
        {
            var existing = _bloodStains[i];
            if (Vector2.DistanceSquared(existing.LocalPosition, localPosition) > 30f) continue;
            _bloodStains[i] = existing with
            {
                LocalPosition = Vector2.Lerp(existing.LocalPosition, localPosition, 0.28f),
                Amount = MathF.Min(1f, existing.Amount + amount * 0.65f),
                Wetness = 1f
            };
            return;
        }

        var stain = new CleaverBloodStain(localPosition, Math.Clamp(amount, 0.08f, 1f), 1f, _stainSerial++);
        if (_bloodStains.Count < MaximumBloodStains)
        {
            _bloodStains.Add(stain);
            return;
        }

        var closestIndex = 0;
        var closestDistance = float.PositiveInfinity;
        for (var i = 0; i < _bloodStains.Count; i++)
        {
            var distance = Vector2.DistanceSquared(_bloodStains[i].LocalPosition, localPosition);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closestIndex = i;
        }
        var merged = _bloodStains[closestIndex];
        _bloodStains[closestIndex] = merged with
        {
            Amount = MathF.Min(1f, merged.Amount + amount * 0.45f),
            Wetness = 1f
        };
    }

    private void UpdateBloodStains(float dt)
    {
        for (var i = 0; i < _bloodStains.Count; i++)
        {
            var stain = _bloodStains[i];
            _bloodStains[i] = stain with { Wetness = MathF.Max(0f, stain.Wetness - dt * 0.055f) };
        }
    }

    private static bool ResolveCapsuleContact(ref Particle particle, Vector2 start, Vector2 end,
        float toolRadius, Vector2 knifeDisplacement, ref Vector2 reaction)
    {
        var closest = ClosestPoint(particle.Position, start, end);
        var delta = particle.Position - closest;
        var distanceSq = delta.LengthSquared();
        var combinedRadius = particle.Radius + toolRadius;
        if (distanceSq >= combinedRadius * combinedRadius) return false;

        Vector2 normal;
        float distance;
        if (distanceSq > 0.0001f)
        {
            distance = MathF.Sqrt(distanceSq);
            normal = delta / distance;
        }
        else
        {
            distance = 0f;
            normal = knifeDisplacement.LengthSquared() > 0.001f
                ? -Vector2.Normalize(knifeDisplacement)
                : -Vector2.UnitY;
        }
        var penetration = combinedRadius - distance;
        var particleCorrection = normal * penetration * 0.72f;
        particle.Position += particleCorrection;
        particle.Contacting = true;
        particle.ContactMemory = 6;
        reaction -= normal * penetration * 0.16f;
        return true;
    }

    private void BeginRespawn()
    {
        IsGrabbed = false;
        IsDeployed = false;
        HoldMode = ToolHoldMode.None;
        _primaryActionHeld = false;
        _primaryActionBuffered = false;
        _primaryActionSwing = false;
        IsHolstered = false;
        IsReturningToHolster = false;
        _heavyImpactAge = -1f;
        RespawnRemaining = 2.25f;
    }

    private void BeginAssistedSwing(bool primaryAction)
    {
        var sledge = ArsenalVisualVariant == 7;
        if (sledge)
        {
            // A sledge attack always commits from overhead toward the world floor.
            // Base rotation still controls its resting pose, but cannot turn the
            // attack into a sideways or incomplete cleaver-like swipe.
            _chopDirection = Vector2.UnitY;
            Angle = SledgeOverheadAngle;
            _previousAngle = Angle;
            _angularVelocity = 0f;
            _sledgeSlamStartPosition = Position;
            _sledgeGroundSurfaceY = 0f;
            _sledgeGroundInitialized = false;
        }
        else
        {
            var swingRotation = _baseRotationAngle - ReadyAngle;
            // The base orientation defines the complete swing plane. Cursor motion
            // moves the equipped grip but can never arm or steer a legacy gesture swing.
            _chopDirection = Rotate(Vector2.UnitY, swingRotation);
        }
        ControlState = CleaverControlState.Swing;
        _primaryActionSwing = primaryAction;
        _controlStateTime = 0f;
        _strongHitConsumed = false;
        if (sledge)
        {
            // Keep the hand planted. All visible attack power comes from the heavy
            // head accelerating around the grip, not the entire tool sliding down.
            _gripVelocity *= 0.16f;
            return;
        }
        var targetAngle = MathF.Atan2(_chopDirection.Y, _chopDirection.X) - MathF.PI * 0.5f;
        var turnImpulse = 18f + _windupStrength * 10f;
        const float maximumTurnImpulse = 26f;
        _angularVelocity += Math.Clamp(ShortestAngle(Angle, targetAngle) * turnImpulse,
            -maximumTurnImpulse, maximumTurnImpulse);
        _gripVelocity += _chopDirection * (300f + _windupStrength * 500f);
    }

    private void BeginRecovery()
    {
        if (ArsenalVisualVariant == 7)
            _sledgeRecoveryStartPosition = Position;
        ControlState = CleaverControlState.Recovery;
        _controlStateTime = 0f;
        _strongHitConsumed = true;
        if (_primaryActionSwing)
        {
            // Recover almost immediately, but through the same angular body used
            // for collision rather than teleporting only the rendered sprite.
            _angularVelocity += Math.Clamp(ShortestAngle(Angle, _baseRotationAngle) * 22f, -32f, 32f);
            _gripVelocity *= 0.42f;
        }
    }

    private void ReturnToHolster()
    {
        RespawnRemaining = 0f;
        Position = HolsterPosition;
        _previousPosition = Position;
        _grabTarget = Position;
        _lastGrabTarget = Position;
        _gripVelocity = Vector2.Zero;
        _windupDistance = 0f;
        _windupStrength = 0f;
        _controlStateTime = 0f;
        _strongHitConsumed = false;
        _primaryActionHeld = false;
        _primaryActionBuffered = false;
        _primaryActionSwing = false;
        _heavyImpactAge = -1f;
        _heavyImpactStrength = 0f;
        ResetArsenalPrimary();
        ControlState = CleaverControlState.Carry;
        Angle = 0f;
        _angularVelocity = 0f;
        IsGrabbed = false;
        IsDeployed = false;
        HoldMode = ToolHoldMode.None;
        IsHolstered = true;
        IsReturningToHolster = false;
    }

    private static Vector2 Rotate(Vector2 value, float angle)
    {
        var c = MathF.Cos(angle);
        var s = MathF.Sin(angle);
        return new Vector2(value.X * c - value.Y * s, value.X * s + value.Y * c);
    }

    private static Vector2 InverseRotate(Vector2 value, float angle) => Rotate(value, -angle);

    private static float SmoothStep01(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return amount * amount * (3f - 2f * amount);
    }

    private Vector2 RotateToolLocal(Vector2 value)
    {
        return Rotate(value, Angle);
    }

    private Vector2 ToAuthoredLocal(Vector2 worldOffset) => InverseRotate(worldOffset, Angle);

    private static Vector2 ClosestPoint(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSq = segment.LengthSquared();
        if (lengthSq < 0.0001f) return start;
        return start + segment * Math.Clamp(Vector2.Dot(point - start, segment) / lengthSq, 0f, 1f);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end) =>
        Vector2.Distance(point, ClosestPoint(point, start, end));

    private static float LerpAngle(float from, float to, float amount)
    {
        return from + ShortestAngle(from, to) * amount;
    }

    private static float ShortestAngle(float from, float to)
    {
        var delta = (to - from) % MathF.Tau;
        if (delta > MathF.PI) delta -= MathF.Tau;
        else if (delta < -MathF.PI) delta += MathF.Tau;
        return delta;
    }
}
