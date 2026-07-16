using System.Drawing;
using System.Numerics;
using BlobForge.Physics;

namespace BlobForge.World;

public enum CartCycleState : byte
{
    Docked,
    DoorOpening,
    Departing,
    Returning
}

public readonly record struct DoorwayBloodStain(
    Vector2 Position,
    float Amount,
    float Wetness,
    byte Variation,
    bool Vertical);

public sealed class BloodShopItem(string code, string label, float cost)
{
    public string Code { get; } = code;
    public string Label { get; } = label;
    public float Cost { get; } = cost;
    public bool Purchased { get; internal set; }
}

public sealed class ProcessingLine
{
    public const float OperatingSpeed = 120f;
    private const float TransferWidth = 112f;
    private const float BayWidth = 48f;
    private const float OutputTransferWidth = 64f;
    private const float BeltHeight = 26f;
    private const float FirstX = 256f;
    private const float CartSpeed = 205f;
    private const float BloodFluidConversion = 0.20f;
    private const float ReceivingTubLeft = 32f;
    private const float ReceivingTubLipLength = 16f;
    private const float ReceivingTubRampWidth = 32f;
    private const float ReceivingTubDepth = 26f;
    private const float BasinCaptureMargin = 18f;
    private static readonly float[] SpikeOffsets = { -36f, -18f, 0f, 18f, 36f };
    private static readonly float[] BayBloodYieldMultipliers = { 1f, 1.25f, 1.55f, 1.95f, 2.40f };
    private readonly HashSet<int> _bayOneEnteredParents = new();
    private readonly HashSet<int> _processedParents = new();
    private readonly HashSet<int> _drilledParents = new();
    private readonly HashSet<int> _pressedParents = new();
    private readonly HashSet<int> _vacuumedParents = new();
    private readonly HashSet<int> _filteredParents = new();
    private readonly HashSet<int> _cartPassengers = new();
    private readonly List<int> _dispatchedParents = new(4);
    private readonly bool[] _spikeContacts = new bool[5];
    private SoftBody? _lockedBody;
    private SoftBody? _drillLockedBody;
    private SoftBody? _pressLockedBody;
    private SoftBody? _vacuumLockedBody;
    private SoftBody? _filterLockedBody;
    private bool _buttonHeld;
    private bool _drillLeverHeld;
    private bool _cycleStarted;
    private bool _drillCycleStarted;
    private bool _returning;
    private bool _drillReturning;
    private float _drillDamageAccumulator;
    private float _drillSpin;
    private int _drillDamagePulses;
    private int _drillBrokenLinks;
    private int _pressBrokenLinks;
    private bool _drumWheelDragging;
    private bool _drumLoading;
    private bool _drumFinishing;
    private float _drumAngle;
    private float _drumAngularSpeed;
    private float _drumProgress;
    private float _drumLoadProgress;
    private Vector2 _drumLoadStartCenter;
    private float _drumDoorOpenness;
    private float _drumWheelAngle;
    private float _drumLastPointerAngle;
    private float _drumInputMotion;
    private float _drumInputHold;
    private float _drumDriveDirection = 1f;
    private float _drumFinishTarget;
    private float _drumReleaseDelay;
    private float _drumDamageAccumulator;
    private float _vacuumProgress;
    private float _vacuumDamageAccumulator;
    private float _vacuumDrainAccumulator;
    private float _vacuumReservoir;
    private float _vacuumFlowPhase;
    private int _vacuumExtractedLinks;
    private bool _vacuumContact;
    private float _vacuumReleaseBoost;
    private bool _filterDragging;
    private bool _filterReturning;
    private bool _filterCompleteOnReturn;
    private int _filterResiduePending;
    private float _filterKnob = 1f;
    private float _filterLastCutX;
    private float _filterStartKnob;
    private bool _filterTraversed;
    private int _filterCutCount;
    private int _filterBrokenLinks;
    private float _cartX;
    private float _cartDeltaX;
    private bool _powerEngaging;
    private bool _breakerLeverDragging;
    private Vector2 _breakerPosition;
    private readonly Vector2[] _receivingTubSurface;
    private readonly List<DoorwayBloodStain> _doorwayStains = new(24);
    private readonly BloodShopItem[] _bloodShopItems =
    [
        new("SLOT-A", "UPGRADE SOCKET A", 5_000f),
        new("SLOT-B", "UPGRADE SOCKET B", 12_000f),
        new("SLOT-C", "UPGRADE SOCKET C", 25_000f)
    ];

    public ProcessingLine(float deckY, bool powered = true, Vector2? breakerPosition = null)
    {
        DeckY = deckY;
        Powered = powered;
        BreakerLever = powered ? 1f : 0f;
        _breakerPosition = breakerPosition ?? new Vector2(42f, deckY - 150f);
        _receivingTubSurface =
        [
            new Vector2(ReceivingTubLeft, deckY),
            new Vector2(ReceivingTubLeft + ReceivingTubLipLength, deckY),
            new Vector2(ReceivingTubLeft + ReceivingTubLipLength + ReceivingTubRampWidth,
                deckY + ReceivingTubDepth),
            new Vector2(FirstX - ReceivingTubLipLength - ReceivingTubRampWidth,
                deckY + ReceivingTubDepth),
            new Vector2(FirstX - ReceivingTubLipLength, deckY),
            new Vector2(FirstX, deckY)
        ];
        var x = FirstX;
        for (var station = 0; station < 5; station++)
        {
            AddBelt(x, TransferWidth);
            x += TransferWidth;
            Bays.Add(new ProcessingBay(station, x, BayWidth));
            x += BayWidth;
        }
        AddBelt(x, OutputTransferWidth);
        CartDockBounds = new RectangleF(x + OutputTransferWidth + 4f, deckY + 32f, 106f, 64f);
        _cartX = CartDockBounds.X;
        WalkwayBounds = new RectangleF(1072f, deckY + 96f, 208f, 14f);
        DoorwayBounds = new RectangleF(1240f, deckY - 26f, 40f, 122f);
        VacuumHose = new VacuumHose(VacuumHoseAnchor, VacuumNozzleRest);
        Basin = new BloodBasin(250f, deckY + 91f, 866f, 101f);
    }

    public float DeckY { get; }
    public List<ConveyorBelt> Belts { get; } = new(6);
    public List<ProcessingBay> Bays { get; } = new(5);
    public RectangleF CartDockBounds { get; }
    public RectangleF ReceivingTubBounds => new(
        ReceivingTubLeft,
        DeckY - 4f,
        FirstX - ReceivingTubLeft,
        ReceivingTubDepth + 22f);
    public IReadOnlyList<Vector2> ReceivingTubSurface => _receivingTubSurface;
    public RectangleF OutputCartBounds => new(_cartX, CartDockBounds.Y, CartDockBounds.Width, CartDockBounds.Height);
    public float CartFloorY => OutputCartBounds.Bottom - 20f;
    public RectangleF WalkwayBounds { get; }
    public RectangleF DoorwayBounds { get; }
    public CartCycleState CartState { get; private set; }
    public float DoorOpenness { get; private set; }
    public bool IsCartLoaded { get; private set; }
    public bool Powered { get; private set; }
    public bool BreakerSelected { get; set; }
    public float BreakerLever { get; private set; }
    public float PowerPhase { get; private set; }
    public RectangleF BreakerBounds => new(_breakerPosition.X, _breakerPosition.Y, 96f, 128f);
    public Vector2 BreakerTrackTop => new(BreakerBounds.Left + BreakerBounds.Width * 0.5f, BreakerBounds.Top + 28f);
    public Vector2 BreakerTrackBottom => new(BreakerBounds.Left + BreakerBounds.Width * 0.5f, BreakerBounds.Bottom - 28f);
    public Vector2 BreakerLeverHandle => Vector2.Lerp(BreakerTrackTop, BreakerTrackBottom, BreakerLever);
    public SoftBody? LockedBody => _lockedBody;
    public float CrusherTravel { get; private set; }
    public bool CrusherButtonHeld => _buttonHeld;
    public Vector2 CrusherButtonCenter => new(Bays[0].CenterX + 68f, DeckY - 94f);
    public float CrusherHeadTop => DeckY - 150f + CrusherTravel * 92f;
    public SoftBody? DrillLockedBody => _drillLockedBody;
    public float DrillTravel { get; private set; }
    public float DrillSpin => _drillSpin;
    public int DrillDamagePulses => _drillDamagePulses;
    public int DrillBrokenLinks => _drillBrokenLinks;
    public bool DrillLeverHeld => _drillLeverHeld;
    public Vector2 DrillLeverCenter => new(Bays[1].CenterX + 63f, DeckY - 78f);
    public float DrillHeadTop => DeckY - 162f + DrillTravel * 72f;
    public Vector2 DrillTip => new(
        Bays[1].CenterX + MathF.Sin(_drillSpin) * 1.8f,
        DrillHeadTop + 78f);
    public SoftBody? PressLockedBody => _pressLockedBody;
    public float PressTravel { get; private set; }
    public float PressTimingMarker => 0.5f;
    public bool PressInTimingWindow => true;
    public Vector2 PressButtonCenter => new(Bays[2].CenterX + 63f, DeckY - 76f);
    public float PressHeadTop => DeckY - 155f + PressTravel * 104f;
    public int PressBrokenLinks => _pressBrokenLinks;
    public SoftBody? DrumLockedBody => _pressLockedBody;
    public const float DrumInteriorRadius = 50f;
    public const float DrumOuterRadius = 58f;
    public Vector2 DrumCenter => new(Bays[2].CenterX, DeckY - 72f);
    public Vector2 DrumWheelCenter => new(Bays[2].CenterX + 66f, DeckY - 76f);
    public float DrumAngle => _drumAngle;
    public float DrumAngularSpeed => _drumAngularSpeed;
    public float DrumProgress => _drumProgress;
    public bool DrumLoading => _drumLoading;
    public float DrumLoadProgress => _drumLoadProgress;
    public bool DrumBodyInside => !_drumLoading || _drumLoadProgress >= 0.74f;
    public float DrumLoadHatchOpenness
    {
        get
        {
            if (!_drumLoading) return 0f;
            if (_drumLoadProgress < 0.16f) return SmoothStep01(_drumLoadProgress / 0.16f);
            if (_drumLoadProgress < 0.82f) return 1f;
            return 1f - SmoothStep01((_drumLoadProgress - 0.82f) / 0.18f);
        }
    }
    public float DrumLiftPlatformY
    {
        get
        {
            if (_pressLockedBody is null || !_drumLoading) return DeckY + 6f;
            var carriedY = _pressLockedBody.Center.Y + _pressLockedBody.Radius + 3f;
            if (_drumLoadProgress <= 0.74f) return carriedY;
            var retract = SmoothStep01((_drumLoadProgress - 0.74f) / 0.26f);
            return carriedY + (DeckY + 6f - carriedY) * retract;
        }
    }
    public float DrumDoorOpenness => _drumDoorOpenness;
    public float DrumWheelAngle => _drumWheelAngle;
    public bool DrumWheelDragging => _drumWheelDragging;
    public bool DrumFinishing => _drumFinishing;
    public SoftBody? VacuumLockedBody => _vacuumLockedBody;
    public VacuumHose VacuumHose { get; }
    public BloodBasin Basin { get; }
    public Vector2 VacuumHoseAnchor => new(Bays[3].CenterX + 42f, DeckY - 98f);
    public Vector2 VacuumNozzleRest => new(Bays[3].CenterX + 64f, DeckY - 48f);
    public Vector2 VacuumHolsterCenter => VacuumNozzleRest;
    public float VacuumProgress => _vacuumProgress;
    public float VacuumFlowPhase => _vacuumFlowPhase;
    public bool VacuumContact => _vacuumContact;
    public int VacuumExtractedLinks => _vacuumExtractedLinks;
    public float VacuumDrainRemaining => _vacuumReservoir;
    public IReadOnlyList<DoorwayBloodStain> DoorwayStains => _doorwayStains;
    public IReadOnlyList<BloodShopItem> BloodShopItems => _bloodShopItems;
    public const float ReliefValveCost = 2_500f;
    public bool MachineryLockedByStorage => Powered && Basin.IsFull;
    public RectangleF BloodShopBounds => new(
        32f,
        DeckY,
        Basin.Left - 52f,
        Basin.Bottom - DeckY - 6f);
    public float BloodShopContentTop => DeckY + ReceivingTubDepth + 6f;
    public float BloodShopTopAt(float x)
    {
        x = Math.Clamp(x, BloodShopBounds.Left, BloodShopBounds.Right);
        for (var i = 0; i < _receivingTubSurface.Length - 1; i++)
        {
            var left = _receivingTubSurface[i];
            var right = _receivingTubSurface[i + 1];
            if (x < left.X || x > right.X) continue;
            var amount = (x - left.X) / MathF.Max(0.001f, right.X - left.X);
            return left.Y + (right.Y - left.Y) * amount;
        }
        return DeckY + ReceivingTubDepth;
    }
    public RectangleF BloodShopItemBounds(int index)
    {
        var bounds = BloodShopBounds;
        return new RectangleF(bounds.Left + 10f, BloodShopContentTop + 38f + index * 25f,
            bounds.Width - 20f, 21f);
    }
    public RectangleF BloodShopReliefBounds
    {
        get
        {
            var bounds = BloodShopBounds;
            return new RectangleF(bounds.Left + 10f, bounds.Bottom - 30f, bounds.Width - 20f, 21f);
        }
    }
    public SoftBody? FilterLockedBody => _filterLockedBody;
    public bool FilterDragging => _filterDragging;
    public bool FilterReturning => _filterReturning;
    public bool FilterLaserActive => _filterLockedBody is not null &&
                                     !_filterTraversed && !_filterCompleteOnReturn;
    public float FilterKnob => _filterKnob;
    public Vector2 FilterKnobCenter => new(Bays[4].CenterX + _filterKnob * 34f, DeckY - 170f);
    public float FilterLaserX => Bays[4].CenterX + _filterKnob * 28f;
    public int FilterBrokenLinks => _filterBrokenLinks;
    public float BloodYieldMultiplierForBay(int bayIndex) =>
        BayBloodYieldMultipliers[Math.Clamp(bayIndex, 0, BayBloodYieldMultipliers.Length - 1)];

    public float BloodYieldMultiplierAt(float x)
    {
        var closestBay = 0;
        var closestDistance = float.PositiveInfinity;
        for (var bayIndex = 0; bayIndex < Bays.Count; bayIndex++)
        {
            var distance = MathF.Abs(x - Bays[bayIndex].CenterX);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closestBay = bayIndex;
        }
        return BloodYieldMultiplierForBay(closestBay);
    }
    public bool IsBayInUse(int bayIndex) => MachineryLockedByStorage || Powered && bayIndex switch
    {
        0 => _lockedBody is not null,
        1 => _drillLockedBody is not null,
        2 => _pressLockedBody is not null,
        3 => _vacuumLockedBody is not null,
        4 => _filterLockedBody is not null,
        _ => false
    };
    public bool IsLocked(SoftBody body)
        => ReferenceEquals(body, _lockedBody) || ReferenceEquals(body, _drillLockedBody) ||
           ReferenceEquals(body, _pressLockedBody) || ReferenceEquals(body, _vacuumLockedBody) ||
           ReferenceEquals(body, _filterLockedBody) || IsInTransit(body);
    public bool HasEnteredBayOne(SoftBody body)
        => _bayOneEnteredParents.Contains(body.ParentId);
    public bool IsInTransit(SoftBody body)
        => (CartState is CartCycleState.DoorOpening or CartCycleState.Departing) &&
           _cartPassengers.Contains(body.ParentId);

    private ConveyorBelt AddBelt(float x, float width)
    {
        var belt = new ConveyorBelt(
            new Vector2(x, DeckY), width, BeltHeight, OperatingSpeed,
            minimumWidth: 24f, systemControlled: true);
        Belts.Add(belt);
        return belt;
    }

    public bool HitCrusherButton(Vector2 point)
        => Powered && !MachineryLockedByStorage &&
           Vector2.DistanceSquared(point, CrusherButtonCenter) <= 16f * 16f;

    public bool HitDrillLever(Vector2 point)
        => Powered && !MachineryLockedByStorage &&
           Vector2.DistanceSquared(point, DrillLeverCenter) <= 24f * 24f;

    public bool HitPressButton(Vector2 point) => false;

    public bool HitDrumWheel(Vector2 point)
        => Powered && !MachineryLockedByStorage && _pressLockedBody is not null &&
           !_drumLoading && !_drumFinishing &&
           Vector2.DistanceSquared(point, DrumWheelCenter) <= 25f * 25f;

    public bool HitVacuumNozzle(Vector2 point)
        => Powered && !MachineryLockedByStorage && VacuumHose.HitNozzle(point);

    public bool HitFilterKnob(Vector2 point)
        => Powered && !MachineryLockedByStorage && _filterLockedBody is not null &&
           Vector2.DistanceSquared(point, FilterKnobCenter) <= 22f * 22f;

    public bool HitBloodShop(Vector2 point)
        => Powered && BloodShopBounds.Contains(point.X, point.Y) && point.Y >= BloodShopTopAt(point.X);

    public bool TryActivateBloodShop(Vector2 point)
    {
        if (!HitBloodShop(point)) return false;
        for (var i = 0; i < _bloodShopItems.Length; i++)
        {
            var item = _bloodShopItems[i];
            if (item.Purchased || !BloodShopItemBounds(i).Contains(point.X, point.Y)) continue;
            if (!Basin.TrySpend(item.Cost)) return true;
            item.Purchased = true;
            return true;
        }
        if (BloodShopReliefBounds.Contains(point.X, point.Y))
            Basin.TrySpend(ReliefValveCost);
        return true;
    }

    public bool HitCart(Vector2 point)
        => Powered && CartState == CartCycleState.Docked && OutputCartBounds.Contains(point.X, point.Y);

    public bool HitBreaker(Vector2 point) => BreakerBounds.Contains(point.X, point.Y);

    public bool HitBreakerLever(Vector2 point)
        => !Powered && !_powerEngaging &&
           Vector2.DistanceSquared(point, BreakerLeverHandle) <= 17f * 17f;

    public void SetBreakerPosition(Vector2 position, float worldWidth, float worldHeight)
    {
        _breakerPosition = new Vector2(
            Math.Clamp(position.X, 0f, MathF.Max(0f, worldWidth - BreakerBounds.Width)),
            Math.Clamp(position.Y, 0f, MathF.Max(0f, worldHeight - BreakerBounds.Height)));
    }

    public bool BeginBreakerLeverDrag(Vector2 point)
    {
        if (!HitBreakerLever(point)) return false;
        _breakerLeverDragging = true;
        return true;
    }

    /// <summary>
    /// Tracks the player's physical downward pull. Returns true exactly once
    /// when the handle crosses the latching threshold and power engagement starts.
    /// </summary>
    public bool DragBreakerLever(Vector2 point)
    {
        if (!_breakerLeverDragging || Powered || _powerEngaging) return false;
        var top = BreakerTrackTop.Y;
        var bottom = BreakerTrackBottom.Y;
        BreakerLever = Math.Clamp((point.Y - top) / MathF.Max(1f, bottom - top), 0f, 1f);
        if (BreakerLever < 0.82f) return false;
        _breakerLeverDragging = false;
        _powerEngaging = true;
        return true;
    }

    public void EndBreakerLeverDrag() => _breakerLeverDragging = false;

    public bool TryDispatchCart(IReadOnlyList<SoftBody> bodies)
    {
        if (!Powered || CartState != CartCycleState.Docked || !IsCartLoaded) return false;
        _cartPassengers.Clear();
        for (var i = 0; i < bodies.Count; i++)
            if (IsInCartLoadZone(bodies[i].Center)) _cartPassengers.Add(bodies[i].ParentId);
        if (_cartPassengers.Count == 0) return false;
        CartState = CartCycleState.DoorOpening;
        return true;
    }

    public void DrainDispatchedParents(List<int> destination)
    {
        destination.AddRange(_dispatchedParents);
        _dispatchedParents.Clear();
    }

    public void SetCrusherButtonHeld(bool held)
    {
        if (held && MachineryLockedByStorage) return;
        _buttonHeld = held;
        if (held && _lockedBody is not null)
        {
            _cycleStarted = true;
            _returning = false;
        }
        else if (!held && _cycleStarted)
        {
            _returning = true;
        }
    }

    public void SetDrillLeverHeld(bool held)
    {
        if (held && MachineryLockedByStorage) return;
        _drillLeverHeld = held;
        if (held && _drillLockedBody is not null)
        {
            _drillCycleStarted = true;
            _drillReturning = false;
        }
        else if (!held && _drillCycleStarted)
        {
            _drillReturning = true;
        }
    }

    public bool ActivatePressButton()
    {
        // Compatibility hook for diagnostics and old saved input bindings. The
        // player-facing control is now the physical hand wheel below.
        if (!Powered || MachineryLockedByStorage || _pressLockedBody is null ||
            _drumLoading || _drumFinishing) return false;
        _drumProgress = 1f;
        BeginDrumFinish();
        return true;
    }

    public bool BeginDrumWheelDrag(Vector2 point)
    {
        if (!HitDrumWheel(point)) return false;
        var offset = point - DrumWheelCenter;
        if (offset.LengthSquared() < 8f * 8f) return false;
        _drumWheelDragging = true;
        _drumLastPointerAngle = MathF.Atan2(offset.Y, offset.X);
        return true;
    }

    public void DragDrumWheel(Vector2 point)
    {
        if (!_drumWheelDragging || _pressLockedBody is null || _drumFinishing) return;
        var offset = point - DrumWheelCenter;
        if (offset.LengthSquared() < 8f * 8f) return;
        var angle = MathF.Atan2(offset.Y, offset.X);
        var delta = WrapAngle(angle - _drumLastPointerAngle);
        _drumLastPointerAngle = angle;
        // Ignore impossible cursor teleports while preserving fast, deliberate circles.
        delta = Math.Clamp(delta, -0.72f, 0.72f);
        _drumInputMotion += delta;
        _drumWheelAngle += delta;
        if (MathF.Abs(delta) > 0.002f)
        {
            _drumDriveDirection = MathF.Sign(delta);
            _drumInputHold = 0.10f;
        }
    }

    public void EndDrumWheelDrag() => _drumWheelDragging = false;

    public bool BeginVacuumDrag(Vector2 point)
    {
        if (!Powered || MachineryLockedByStorage || _vacuumLockedBody is null ||
            !VacuumHose.BeginDrag(point)) return false;
        VacuumHose.DragTo(point, DeckY);
        return true;
    }

    public void DragVacuumNozzle(Vector2 point) => VacuumHose.DragTo(point, DeckY);

    public void EndVacuumDrag(Vector2? releasePoint = null)
    {
        VacuumHose.EndDrag();
        _vacuumContact = false;
    }

    public bool BeginFilterDrag(Vector2 point)
    {
        if (!Powered || MachineryLockedByStorage || _filterReturning || !HitFilterKnob(point)) return false;
        _filterDragging = true;
        _filterStartKnob = _filterKnob;
        _filterTraversed = false;
        _filterLastCutX = FilterLaserX;
        return true;
    }

    public void DragFilterKnob(float pointerX)
    {
        if (!_filterDragging || _filterLockedBody is null) return;
        var bay = Bays[4];
        var requested = Math.Clamp((pointerX - bay.CenterX) / 34f, -1f, 1f);
        _filterKnob = MathF.Min(_filterKnob, requested);
        var laserX = FilterLaserX;
        if (laserX < _filterLastCutX - 1f && _filterCutCount < 18)
        {
            // Mouse-move events are not guaranteed to arrive at uniform intervals. Sweep
            // every crossed slice so one fast right-to-left gesture has exactly the same
            // gameplay result as a slow drag with many intermediate events.
            DamageFilterSweep(_filterLastCutX, laserX);
            _filterLastCutX = laserX;
        }
        if (_filterStartKnob - _filterKnob >= 1.55f)
            _filterTraversed = true;
    }

    public void EndFilterDrag()
    {
        if (!_filterDragging) return;
        _filterDragging = false;

        // A completed right-to-left cut is the whole interaction for this blob.
        // Snapping the carriage home and releasing here avoids making the player
        // wait through a second, non-interactive one-second reset animation.
        if (_filterTraversed)
        {
            _filterKnob = 1f;
            CompleteFilterCycle();
            return;
        }

        _filterReturning = true;
        _filterCompleteOnReturn = false;
    }

    public void PreStep(List<SoftBody> bodies, List<GranularParticle> granular, float dt)
    {
        UpdatePower(dt);
        if (!Powered) return;
        VacuumHose.Step(dt, VacuumHoseAnchor, VacuumNozzleRest, DeckY,
            VacuumHose.IsDragging ? _vacuumLockedBody?.Center : null);
        Basin.Step(dt);
        CollectBasinInflows(granular, dt);
        if (MachineryLockedByStorage)
        {
            foreach (var belt in Belts) belt.SetAutomationSpeed(0f);
            _buttonHeld = false;
            _drillLeverHeld = false;
            _drumWheelDragging = false;
            _filterDragging = false;
            _vacuumContact = false;
            VacuumHose.EndDrag();
            HoldBodyInMachine(dt);
            RefreshCartLoad(bodies, granular);
            return;
        }
        UpdateCart(bodies, granular, dt);
        RebindOrCapture(bodies);
        RebindOrCaptureDrill(bodies);
        RebindOrCapturePress(bodies);
        RebindOrCaptureVacuum(bodies);
        RebindOrCaptureFilter(bodies);
        UpdateCrusher(dt);
        UpdateDrill(dt);
        UpdatePress(granular, dt);
        UpdateVacuum(granular, dt);
        UpdateFilter(dt);
        EmitFilterResidue(granular);
        UpdateBeltAutomation(bodies);
        BoostVacuumQueue(bodies, dt);
        HoldBodyInMachine(dt);
        PropelAcrossTables(bodies, dt);
        RefreshCartLoad(bodies, granular);
    }

    private void UpdatePower(float dt)
    {
        if (!_powerEngaging)
        {
            if (!Powered && !_breakerLeverDragging)
                BreakerLever = MoveTowards(BreakerLever, 0f, dt * 5.5f);
            if (Powered) PowerPhase = (PowerPhase + dt * 2.5f) % 1f;
            return;
        }

        BreakerLever = MoveTowards(BreakerLever, 1f, dt * 2.8f);
        if (BreakerLever < 0.72f) return;
        BreakerLever = 1f;
        _powerEngaging = false;
        Powered = true;
    }

    private void UpdateCart(List<SoftBody> bodies, List<GranularParticle> granular, float dt)
    {
        _cartDeltaX = 0f;
        switch (CartState)
        {
            case CartCycleState.Docked:
                DoorOpenness = MoveTowards(DoorOpenness, 0f, 2.8f * dt);
                return;
            case CartCycleState.DoorOpening:
                DoorOpenness = MoveTowards(DoorOpenness, 1f, 2.4f * dt);
                if (DoorOpenness >= 0.999f) CartState = CartCycleState.Departing;
                return;
            case CartCycleState.Departing:
                _cartDeltaX = CartSpeed * dt;
                _cartX += _cartDeltaX;
                CarryContents(bodies, granular, _cartDeltaX);
                if (_cartX < 1270f) return;
                RemoveDispatchedContents(bodies, granular);
                _cartX = 1320f;
                _cartPassengers.Clear();
                IsCartLoaded = false;
                CartState = CartCycleState.Returning;
                return;
            case CartCycleState.Returning:
                _cartDeltaX = -CartSpeed * 1.12f * dt;
                _cartX += _cartDeltaX;
                if (_cartX > CartDockBounds.X) return;
                _cartX = CartDockBounds.X;
                _cartDeltaX = 0f;
                CartState = CartCycleState.Docked;
                return;
        }
    }

    private void CarryContents(List<SoftBody> bodies, List<GranularParticle> granular, float deltaX)
    {
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (!_cartPassengers.Contains(body.ParentId)) continue;
            body.ApplyTranslation(new Vector2(deltaX, 0f), preserveVelocity: true);
            body.Wake();
        }
        var zone = CartContentZone();
        for (var i = 0; i < granular.Count; i++)
        {
            var particle = granular[i];
            if (!zone.Contains(particle.Position.X, particle.Position.Y)) continue;
            particle.Position.X += deltaX;
            particle.PreviousPosition.X += deltaX;
            granular[i] = particle;
        }
    }

    private void RemoveDispatchedContents(List<SoftBody> bodies, List<GranularParticle> granular)
    {
        foreach (var parentId in _cartPassengers) _dispatchedParents.Add(parentId);
        var zone = CartContentZone();
        for (var i = granular.Count - 1; i >= 0; i--)
            if (zone.Contains(granular[i].Position.X, granular[i].Position.Y)) granular.RemoveAt(i);
    }

    private void RefreshCartLoad(IReadOnlyList<SoftBody> bodies, IReadOnlyList<GranularParticle> granular)
    {
        if (CartState != CartCycleState.Docked)
        {
            IsCartLoaded = false;
            return;
        }
        IsCartLoaded = false;
        for (var i = 0; i < bodies.Count; i++)
        {
            if (!IsInCartLoadZone(bodies[i].Center)) continue;
            IsCartLoaded = true;
            return;
        }
        var zone = CartContentZone();
        var granularCount = 0;
        for (var i = 0; i < granular.Count; i++)
            if (zone.Contains(granular[i].Position.X, granular[i].Position.Y) && ++granularCount >= 8)
            {
                IsCartLoaded = true;
                return;
            }
    }

    private RectangleF CartContentZone()
    {
        var cart = OutputCartBounds;
        return new RectangleF(cart.Left + 5f, cart.Top - 38f, cart.Width - 10f, cart.Height + 34f);
    }

    private bool IsInCartLoadZone(Vector2 center) => CartContentZone().Contains(center.X, center.Y);

    private void RebindOrCapture(IReadOnlyList<SoftBody> bodies)
    {
        if (_lockedBody is not null && bodies.Contains(_lockedBody)) return;
        if (_lockedBody is not null)
        {
            var parent = _lockedBody.ParentId;
            SoftBody? replacement = null;
            for (var i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body.ParentId != parent || !IsInsideCrusher(body.Center)) continue;
                if (replacement is null || body.Particles.Length > replacement.Particles.Length)
                    replacement = body;
            }
            _lockedBody = replacement;
            if (_lockedBody is not null) return;
            CompleteCycle();
        }

        if (_cycleStarted || _returning) return;
        var closestDistance = float.MaxValue;
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (!body.IsPickable || body.IsGrabbed || body.IsDetachedDebris ||
                _processedParents.Contains(body.ParentId) || !IsInsideCrusher(body.Center)) continue;
            var distance = MathF.Abs(body.Center.X - Bays[0].CenterX);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            _lockedBody = body;
        }
        if (_lockedBody is not null)
        {
            // Touching or riding a conveyor remains reversible. Crossing into Bay 1
            // and being captured by the crusher is the permanent handoff boundary.
            _bayOneEnteredParents.Add(_lockedBody.ParentId);
            Array.Clear(_spikeContacts);
        }
    }

    private bool IsInsideCrusher(Vector2 center)
        => center.X >= Bays[0].Left - 4f && center.X <= Bays[0].Right + 4f &&
           center.Y >= DeckY - 82f && center.Y <= DeckY + 8f;

    private void RebindOrCaptureDrill(IReadOnlyList<SoftBody> bodies)
    {
        if (_drillLockedBody is not null && bodies.Contains(_drillLockedBody)) return;
        if (_drillLockedBody is not null)
        {
            var parent = _drillLockedBody.ParentId;
            SoftBody? replacement = null;
            for (var i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body.ParentId != parent || !IsInsideDrill(body.Center)) continue;
                if (replacement is null || body.Particles.Length > replacement.Particles.Length)
                    replacement = body;
            }
            _drillLockedBody = replacement;
            if (_drillLockedBody is not null) return;
            CompleteDrillCycle();
        }

        if (_drillCycleStarted || _drillReturning) return;
        var closestDistance = float.MaxValue;
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (!body.IsPickable || body.IsGrabbed || body.IsDetachedDebris ||
                !_processedParents.Contains(body.ParentId) || _drilledParents.Contains(body.ParentId) ||
                !IsInsideDrill(body.Center)) continue;
            var distance = MathF.Abs(body.Center.X - Bays[1].CenterX);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            _drillLockedBody = body;
        }
        if (_drillLockedBody is null) return;
        _drillDamageAccumulator = 0f;
        _drillDamagePulses = 0;
        _drillBrokenLinks = 0;
    }

    private bool IsInsideDrill(Vector2 center)
        => center.X >= Bays[1].Left - 4f && center.X <= Bays[1].Right + 4f &&
           center.Y >= DeckY - 82f && center.Y <= DeckY + 8f;

    private void RebindOrCapturePress(IReadOnlyList<SoftBody> bodies)
    {
        if (_pressLockedBody is not null && bodies.Contains(_pressLockedBody)) return;
        if (_pressLockedBody is not null)
        {
            var parent = _pressLockedBody.ParentId;
            _pressLockedBody = FindParentInBay(bodies, parent, 2);
            if (_pressLockedBody is not null) return;
            _pressedParents.Add(parent);
            ResetPress();
        }
        if (_drumFinishing) return;
        _pressLockedBody = FindAvailableInBay(bodies, 2, _drilledParents, _pressedParents);
        if (_pressLockedBody is not null)
        {
            _pressBrokenLinks = 0;
            _drumProgress = 0f;
            _drumLoading = true;
            _drumLoadProgress = 0f;
            _drumLoadStartCenter = _pressLockedBody.Center;
            _drumDoorOpenness = 0f;
            _drumAngularSpeed = 0f;
            _drumInputMotion = 0f;
            _drumInputHold = 0f;
            _drumFinishing = false;
            _drumReleaseDelay = 0f;
            _drumDamageAccumulator = 0f;
            _pressLockedBody.Wake();
        }
    }

    private void RebindOrCaptureVacuum(IReadOnlyList<SoftBody> bodies)
    {
        if (_vacuumLockedBody is not null && bodies.Contains(_vacuumLockedBody)) return;
        if (_vacuumLockedBody is not null)
        {
            var parent = _vacuumLockedBody.ParentId;
            _vacuumLockedBody = FindParentInBay(bodies, parent, 3);
            if (_vacuumLockedBody is not null) return;
            _vacuumedParents.Add(parent);
            ResetVacuum();
        }
        _vacuumLockedBody = FindAvailableInBay(bodies, 3, _pressedParents, _vacuumedParents);
        if (_vacuumLockedBody is not null)
        {
            _vacuumProgress = 0f;
            _vacuumExtractedLinks = 0;
            _vacuumDamageAccumulator = 0f;
            _vacuumFlowPhase = 0f;
        }
    }

    private void RebindOrCaptureFilter(IReadOnlyList<SoftBody> bodies)
    {
        if (_filterLockedBody is not null && bodies.Contains(_filterLockedBody)) return;
        if (_filterLockedBody is not null)
        {
            var parent = _filterLockedBody.ParentId;
            _filterLockedBody = FindParentInBay(bodies, parent, 4);
            if (_filterLockedBody is not null) return;
            _filteredParents.Add(parent);
            _filterResiduePending = Math.Max(_filterResiduePending, 10);
            ResetFilter();
        }
        if (_filterDragging || _filterReturning) return;
        _filterLockedBody = FindAvailableInBay(bodies, 4, _vacuumedParents, _filteredParents);
        if (_filterLockedBody is null) return;
        _filterKnob = 1f;
        _filterLastCutX = FilterLaserX;
        _filterCutCount = 0;
        _filterBrokenLinks = 0;
        _filterTraversed = false;
    }

    private SoftBody? FindParentInBay(IReadOnlyList<SoftBody> bodies, int parentId, int bayIndex)
    {
        SoftBody? replacement = null;
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.ParentId != parentId || !IsInsideBay(body.Center, bayIndex)) continue;
            if (replacement is null || body.Particles.Length > replacement.Particles.Length)
                replacement = body;
        }
        return replacement;
    }

    private SoftBody? FindAvailableInBay(
        IReadOnlyList<SoftBody> bodies,
        int bayIndex,
        HashSet<int> required,
        HashSet<int> completed)
    {
        SoftBody? closest = null;
        var closestDistance = float.MaxValue;
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (!body.IsPickable || body.IsGrabbed || body.IsDetachedDebris ||
                !required.Contains(body.ParentId) || completed.Contains(body.ParentId) ||
                !IsInsideBay(body.Center, bayIndex)) continue;
            var distance = MathF.Abs(body.Center.X - Bays[bayIndex].CenterX);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closest = body;
        }
        return closest;
    }

    private bool IsInsideBay(Vector2 center, int bayIndex)
        => center.X >= Bays[bayIndex].Left - 4f && center.X <= Bays[bayIndex].Right + 4f &&
           center.Y >= DeckY - 82f && center.Y <= DeckY + 8f;

    private void UpdateCrusher(float dt)
    {
        var target = _buttonHeld && _lockedBody is not null && !_returning ? 1f : 0f;
        var speed = target > CrusherTravel ? 1.65f : 2.35f;
        CrusherTravel = MoveTowards(CrusherTravel, target, speed * dt);
        if (_lockedBody is not null && CrusherTravel > 0.25f) DamageAtSpikeContacts();
        if (_returning && CrusherTravel <= 0.001f) CompleteCycle();
    }

    private void DamageAtSpikeContacts()
    {
        if (_lockedBody is null) return;
        var tipY = CrusherHeadTop + 48f;
        for (var i = 0; i < SpikeOffsets.Length; i++)
        {
            if (_spikeContacts[i]) continue;
            var tip = new Vector2(Bays[0].CenterX + SpikeOffsets[i], tipY);
            if (!_lockedBody.ContainsVisiblePoint(tip)) continue;
            _lockedBody.DamageBonds(tip, 5.2f, 1.12f);
            _spikeContacts[i] = true;
        }
    }

    private void UpdateDrill(float dt)
    {
        var target = _drillLeverHeld && _drillLockedBody is not null && !_drillReturning ? 1f : 0f;
        var speed = target > DrillTravel ? 1.35f : 2.2f;
        DrillTravel = MoveTowards(DrillTravel, target, speed * dt);
        if (_drillLeverHeld && _drillLockedBody is not null)
        {
            _drillSpin = (_drillSpin + 24f * dt) % (MathF.PI * 2f);
            DamageAtDrillContact(dt);
        }
        if (_drillReturning && DrillTravel <= 0.001f) CompleteDrillCycle();
    }

    private void DamageAtDrillContact(float dt)
    {
        if (_drillLockedBody is null || _drillDamagePulses >= 10) return;
        var tip = DrillTip;
        var contactPoint = tip;
        var closestDistanceSq = 20f * 20f;
        var foundParticle = false;
        for (var i = 0; i < _drillLockedBody.Particles.Length; i++)
        {
            if (!_drillLockedBody.IsPhysicalParticle(i)) continue;
            var distanceSq = Vector2.DistanceSquared(tip, _drillLockedBody.Particles[i].Position);
            if (distanceSq >= closestDistanceSq) continue;
            closestDistanceSq = distanceSq;
            contactPoint = _drillLockedBody.Particles[i].Position;
            foundParticle = true;
        }
        if (!foundParticle && !_drillLockedBody.ContainsVisiblePoint(contactPoint))
        {
            _drillDamageAccumulator = 0f;
            return;
        }
        _drillDamageAccumulator += dt;
        if (_drillDamageAccumulator < 0.06f) return;
        _drillDamageAccumulator -= 0.06f;
        _drillBrokenLinks += _drillLockedBody.DamageBonds(contactPoint, 5.2f, 1.08f);
        _drillDamagePulses++;
    }

    private void UpdatePress(List<GranularParticle> granular, float dt)
    {
        if (_pressLockedBody is null)
        {
            _drumAngularSpeed = MoveTowards(_drumAngularSpeed, 0f, 12f * dt);
            _drumInputMotion = 0f;
            return;
        }

        _pressLockedBody.Wake();
        if (_drumLoading)
        {
            _drumWheelDragging = false;
            _drumAngularSpeed = MoveTowards(_drumAngularSpeed, 0f, 28f * dt);
            _drumLoadProgress = Math.Clamp(_drumLoadProgress + dt / 1.05f, 0f, 1f);
            PositionBodyOnDrumLift(dt);
            if (_drumLoadProgress >= 0.999f)
            {
                _drumLoading = false;
                _drumLoadProgress = 1f;
                PositionBodyAtDrumRest(dt);
            }
            return;
        }
        if (_drumFinishing)
        {
            _drumWheelDragging = false;
            _drumAngularSpeed = MoveTowards(_drumAngularSpeed, 0f, 28f * dt);
            _drumAngle = MoveTowards(_drumAngle, _drumFinishTarget, 7.2f * dt);
            if (MathF.Abs(_drumAngle - _drumFinishTarget) > 0.012f) return;

            _drumDoorOpenness = MoveTowards(_drumDoorOpenness, 1f, 5.5f * dt);
            PressTravel = _drumDoorOpenness;
            if (_drumDoorOpenness < 0.999f) return;
            _drumReleaseDelay += dt;
            if (_drumReleaseDelay < 0.12f) return;
            CompletePressCycle(granular, dt);
            return;
        }

        var inputSpeed = _drumInputMotion / MathF.Max(dt, 0.0001f);
        _drumInputMotion = 0f;
        const float baseDrivenSpeed = 13.5f; // 129 RPM: immediate, legible startup torque.
        if (MathF.Abs(inputSpeed) > 0.15f)
        {
            _drumDriveDirection = MathF.Sign(inputSpeed);
            _drumInputHold = 0.10f;
        }
        else
        {
            _drumInputHold = MathF.Max(0f, _drumInputHold - dt);
        }
        var requestedSpeed = Math.Clamp(MathF.Abs(inputSpeed) * 1.12f, baseDrivenSpeed, 30f);
        var targetSpeed = _drumWheelDragging && _drumInputHold > 0f
            ? _drumDriveDirection * requestedSpeed
            : 0f;
        _drumAngularSpeed = MoveTowards(
            _drumAngularSpeed,
            targetSpeed,
            (_drumWheelDragging && _drumInputHold > 0f ? 190f : 18f) * dt);
        var rotation = _drumAngularSpeed * dt;
        _drumAngle += rotation;

        var usefulRotation = MathF.Abs(rotation);
        if (usefulRotation > 0.0001f)
        {
            _drumProgress = Math.Clamp(_drumProgress + usefulRotation / (MathF.Tau * 6f), 0f, 1f);
            _drumDamageAccumulator += usefulRotation;
            if (_drumDamageAccumulator >= 0.52f)
            {
                _drumDamageAccumulator %= 0.52f;
                // Tumbling extracts blood through repeated compression without
                // cutting the unit into detached chunks that cannot advance to Bay 4.
                _pressBrokenLinks++;
            }
        }
        if (_drumProgress >= 0.999f) BeginDrumFinish();
    }

    private void BeginDrumFinish()
    {
        if (_drumFinishing) return;
        _pressBrokenLinks = Math.Max(1, _pressBrokenLinks);
        _drumFinishing = true;
        _drumWheelDragging = false;
        // The hatch marker is authored at the rotor's positive-Y orientation.
        // Finish at the nearest equivalent downward angle instead of snapping.
        _drumFinishTarget = MathF.Round((_drumAngle - MathF.PI * 0.5f) / MathF.Tau) * MathF.Tau +
                            MathF.PI * 0.5f;
        _drumReleaseDelay = 0f;
    }

    private void UpdateVacuum(List<GranularParticle> granular, float dt)
    {
        EmitVacuumDrain(granular, dt);
        _vacuumContact = false;
        if (_vacuumLockedBody is null || !VacuumHose.IsDragging)
        {
            _vacuumProgress = MathF.Max(0f, _vacuumProgress - dt * 0.12f);
            return;
        }

        var nozzle = VacuumHose.NozzlePosition;
        if (_vacuumLockedBody.DistanceToPointSquared(nozzle) > 15f * 15f)
        {
            _vacuumProgress = MathF.Max(0f, _vacuumProgress - dt * 0.08f);
            PullLooseMaterial(granular, nozzle, dt, false);
            return;
        }

        _vacuumContact = true;
        _vacuumFlowPhase = (_vacuumFlowPhase + dt * 1.85f) % 1f;
        _vacuumProgress = MathF.Min(1f, _vacuumProgress + dt / 2.35f);
        _vacuumDamageAccumulator += dt;
        if (_vacuumDamageAccumulator >= 0.19f)
        {
            _vacuumDamageAccumulator -= 0.19f;
            var contact = ClosestPhysicalPoint(_vacuumLockedBody, nozzle, 24f);
            var broken = _vacuumLockedBody.DamageBonds(contact, 5.2f, 1.06f);
            _vacuumExtractedLinks += broken;
            _vacuumReservoir += 0.45f + broken * 0.75f;
        }
        PullLooseMaterial(granular, nozzle, dt, true);
        if (_vacuumProgress >= 0.999f)
        {
            _vacuumProgress = 1f;
            CompleteVacuumCycle();
        }
    }

    private static Vector2 ClosestPhysicalPoint(SoftBody body, Vector2 point, float maximumDistance)
    {
        var closest = point;
        var closestDistanceSq = maximumDistance * maximumDistance;
        for (var i = 0; i < body.Particles.Length; i++)
        {
            if (!body.IsPhysicalParticle(i)) continue;
            var distanceSq = Vector2.DistanceSquared(point, body.Particles[i].Position);
            if (distanceSq >= closestDistanceSq) continue;
            closestDistanceSq = distanceSq;
            closest = body.Particles[i].Position;
        }
        return closest;
    }

    private void PullLooseMaterial(List<GranularParticle> granular, Vector2 nozzle, float dt, bool capture)
    {
        const float pullRadius = 92f;
        var pullRadiusSq = pullRadius * pullRadius;
        for (var i = granular.Count - 1; i >= 0; i--)
        {
            var particle = granular[i];
            var delta = nozzle - particle.Position;
            var distanceSq = delta.LengthSquared();
            if (distanceSq > pullRadiusSq || distanceSq < 0.0001f) continue;
            var distance = MathF.Sqrt(distanceSq);
            var direction = delta / distance;
            var strength = (1f - distance / pullRadius) * 940f;
            particle.PreviousPosition -= direction * strength * dt * dt;
            particle.RestFrames = 0;
            if (capture && distance <= 12f)
            {
                _vacuumReservoir += particle.Kind == GranularKind.Tissue ? 0.7f : 0.32f;
                granular.RemoveAt(i);
                continue;
            }
            granular[i] = particle;
        }
    }

    private void EmitVacuumDrain(List<GranularParticle> granular, float dt)
    {
        if (_vacuumReservoir <= 0.05f || granular.Count >= GranularMaterialSystem.ParticleCapacity) return;
        _vacuumDrainAccumulator += dt * MathF.Min(18f, 5f + _vacuumReservoir * 0.35f);
        var emitCount = Math.Min(3, (int)_vacuumDrainAccumulator);
        if (emitCount <= 0) return;
        _vacuumDrainAccumulator -= emitCount;
        for (var i = 0; i < emitCount && _vacuumReservoir > 0.05f; i++)
        {
            var position = new Vector2(Bays[3].CenterX + (i - 1) * 1.5f, DeckY + 14f);
            granular.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - new Vector2(0f, 3.5f),
                Radius = 2.2f,
                Lifetime = 38f,
                Kind = GranularKind.Blood
            });
            _vacuumReservoir -= 0.28f;
        }
    }

    private void BoostVacuumQueue(IReadOnlyList<SoftBody> bodies, float dt)
    {
        if (_vacuumReleaseBoost <= 0f) return;
        _vacuumReleaseBoost = MathF.Max(0f, _vacuumReleaseBoost - dt);
        var feed = Belts[3];
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.IsGrabbed || body.IsDetachedDebris ||
                !_pressedParents.Contains(body.ParentId) || _vacuumedParents.Contains(body.ParentId) ||
                body.Center.X < feed.Position.X - 24f || body.Center.X > Bays[3].Right + 8f)
                continue;
            var velocity = body.AverageVelocity(dt);
            if (velocity.X < 105f)
                body.AddImpulse(new Vector2((105f - velocity.X) * 0.11f, -1.5f), dt);
        }
    }

    public void RegisterDoorwayBlood(Vector2 current, Vector2 previous, float radius, float speed)
    {
        var door = DoorwayBounds;
        var nearJamb = current.X + radius >= door.Left - 7f && current.X - radius <= door.Left + 3f &&
                       current.Y >= door.Top - 8f && current.Y <= door.Bottom;
        var nearHeader = current.Y + radius >= door.Top - 7f && current.Y - radius <= door.Top + 2f &&
                         current.X >= door.Left - 7f && current.X <= door.Right;
        var nearThreshold = current.Y + radius >= door.Bottom - 5f && current.Y - radius <= door.Bottom + 3f &&
                            current.X >= door.Left - 7f && current.X <= door.Right;
        if (!nearJamb && !nearHeader && !nearThreshold) return;

        var variation = (byte)((_doorwayStains.Count * 71 + (int)(current.X + current.Y)) & 255);
        var point = nearJamb
            ? new Vector2(door.Left - 5f, Math.Clamp(current.Y, door.Top, door.Bottom))
            : nearHeader
                ? new Vector2(Math.Clamp(current.X, door.Left, door.Right), door.Top - 5f)
                : new Vector2(Math.Clamp(current.X, door.Left, door.Right), door.Bottom - 2f);
        for (var i = _doorwayStains.Count - 1; i >= Math.Max(0, _doorwayStains.Count - 10); i--)
        {
            var stain = _doorwayStains[i];
            if (Vector2.DistanceSquared(stain.Position, point) > 42f) continue;
            _doorwayStains[i] = stain with
            {
                Amount = MathF.Min(1f, stain.Amount + 0.08f + speed * 0.0003f),
                Wetness = 1f
            };
            return;
        }
        if (_doorwayStains.Count >= 24) _doorwayStains.RemoveAt(0);
        _doorwayStains.Add(new DoorwayBloodStain(point, 0.16f, 1f, variation, nearJamb));
    }

    private void CollectBasinInflows(List<GranularParticle> granular, float dt)
    {
        for (var i = granular.Count - 1; i >= 0; i--)
        {
            if (!TryCollectBasinInflow(granular[i], dt)) continue;
            granular.RemoveAt(i);
        }
    }

    public bool TryCollectBasinInflow(GranularParticle particle, float dt)
    {
        // Capture against the complete tank footprint, including its solid endcaps.
        // This check also runs immediately after granular integration, before terrain
        // collision can turn contained blood into stains on the factory floor below.
        if (particle.Position.X + particle.Radius < Basin.Left - BasinCaptureMargin ||
            particle.Position.X - particle.Radius > Basin.Right + BasinCaptureMargin ||
            particle.Position.Y + particle.Radius < Basin.Top)
            return false;
        var depositX = Math.Clamp(particle.Position.X, Basin.Left + 1f, Basin.Right - 1f);
        var surfaceY = Basin.SurfaceYAt(depositX);
        if (particle.Position.Y + particle.Radius < surfaceY) return false;

        var safeDt = MathF.Max(dt, 0.0001f);
        var downwardSpeed = MathF.Max(
            0f,
            (particle.Position.Y - particle.PreviousPosition.Y) / safeDt);
        var pixelArea = MathF.PI * particle.Radius * particle.Radius;
        // Later machinery performs progressively more complete extraction. The physical
        // pixel remains the same size, but blood entering under each successive bay has
        // a higher processed yield in the basin's authoritative volume and nutrition.
        var fluidVolume = particle.Kind == GranularKind.Blood
            ? pixelArea * BloodFluidConversion * BloodYieldMultiplierAt(depositX)
            : pixelArea * 0.045f;
        var nutrition = particle.Kind == GranularKind.Blood
            ? fluidVolume * 0.42f
            : pixelArea * 0.90f;
        Basin.AddSuspendedMaterial(
            depositX,
            particle.Position.Y,
            fluidVolume,
            downwardSpeed,
            nutrition,
            particle.Radius);
        return true;
    }

    public bool IsBasinProtectedFloor(Vector2 point)
        => point.X >= Basin.Left - BasinCaptureMargin &&
           point.X <= Basin.Right + BasinCaptureMargin &&
           point.Y >= Basin.Bottom - 3f;

    private void DamageFilterSweep(float previousX, float currentX)
    {
        if (_filterLockedBody is null || _filterLockedBody.PhysicalParticleCount <= 7) return;
        var distance = MathF.Abs(currentX - previousX);
        var slices = Math.Clamp((int)MathF.Ceiling(distance / 3.5f), 1, 18 - _filterCutCount);
        for (var slice = 1; slice <= slices; slice++)
        {
            var x = previousX + (currentX - previousX) * (slice / (float)slices);
            _filterBrokenLinks += _filterLockedBody.DamageLine(
                new Vector2(x, DeckY - 70f),
                new Vector2(x, DeckY - 2f),
                2.6f,
                1.08f);
            _filterCutCount++;
        }
    }

    private void UpdateFilter(float dt)
    {
        if (!_filterReturning) return;
        _filterKnob = MoveTowards(_filterKnob, 1f, 1.86f * dt);
        if (_filterKnob < 0.999f) return;
        _filterKnob = 1f;
        _filterReturning = false;
        if (_filterCompleteOnReturn)
        {
            CompleteFilterCycle();
            return;
        }
        _filterCompleteOnReturn = false;
        _filterStartKnob = 1f;
        _filterLastCutX = FilterLaserX;
        _filterTraversed = false;
        _filterCutCount = 0;
    }

    private void EmitFilterResidue(List<GranularParticle> granular)
    {
        if (_filterResiduePending <= 0 || granular.Count >= GranularMaterialSystem.ParticleCapacity) return;
        var count = Math.Min(_filterResiduePending,
            GranularMaterialSystem.ParticleCapacity - granular.Count);
        for (var i = 0; i < count; i++)
        {
            var position = new Vector2(
                Bays[4].Right + 30f + (i % 4) * 2.6f,
                DeckY - 9f - (i / 4) * 3f);
            granular.Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - new Vector2(1.8f + i % 3 * 0.35f, -0.25f),
                Radius = 2.1f + i % 2 * 0.25f,
                Lifetime = 42f,
                Kind = GranularKind.Tissue
            });
        }
        _filterResiduePending -= count;
    }

    private void HoldBodyInMachine(float dt)
    {
        HoldBodyAtBay(_lockedBody, Bays[0], dt, 1.05f, 0.055f);
        HoldBodyAtBay(_drillLockedBody, Bays[1], dt, 2.4f, 0.11f);
        if (_pressLockedBody is not null && _drumLoading) PositionBodyOnDrumLift(dt);
        HoldBodyAtBay(_vacuumLockedBody, Bays[3], dt, 2.1f, 0.10f);
        HoldBodyAtBay(_filterLockedBody, Bays[4], dt, 2.4f, 0.12f);
    }

    private void PositionBodyOnDrumLift(float dt)
    {
        if (_pressLockedBody is null) return;
        var liftAmount = SmoothStep01((_drumLoadProgress - 0.16f) / 0.58f);
        var target = DrumRestPosition(_pressLockedBody);
        var desired = Vector2.Lerp(_drumLoadStartCenter, target, liftAmount);
        _pressLockedBody.ApplyTranslation(desired - _pressLockedBody.Center, preserveVelocity: true);
        _pressLockedBody.AddImpulse(-_pressLockedBody.AverageVelocity(dt), dt);
        _pressLockedBody.Wake();
    }

    private void PositionBodyAtDrumRest(float dt)
    {
        if (_pressLockedBody is null) return;
        var target = DrumRestPosition(_pressLockedBody);
        _pressLockedBody.ApplyTranslation(target - _pressLockedBody.Center, preserveVelocity: true);
        _pressLockedBody.AddImpulse(-_pressLockedBody.AverageVelocity(dt), dt);
        _pressLockedBody.Wake();
    }

    private Vector2 DrumRestPosition(SoftBody body)
    {
        var radialAllowance = MathF.Max(0f, DrumInteriorRadius - body.Radius - 2f);
        return DrumCenter + new Vector2(0f, MathF.Min(6f, radialAllowance));
    }

    private static void HoldBodyAtBay(
        SoftBody? body,
        ProcessingBay bay,
        float dt,
        float maximumCorrection,
        float horizontalDamping)
    {
        if (body is null) return;
        var error = bay.CenterX - body.Center.X;
        var correction = Math.Clamp(error * 0.2f, -maximumCorrection, maximumCorrection);
        body.ApplyTranslation(new Vector2(correction, 0f), preserveVelocity: true);
        var velocity = body.AverageVelocity(dt);
        body.AddImpulse(new Vector2(-velocity.X * horizontalDamping, 0f), dt);
        body.Wake();
    }

    private void CompleteCycle()
    {
        if (_lockedBody is not null)
        {
            _processedParents.Add(_lockedBody.ParentId);
            _lockedBody.AddImpulse(new Vector2(92f, -8f), 1f / 120f);
        }
        _lockedBody = null;
        _cycleStarted = false;
        _returning = false;
        _buttonHeld = false;
        Array.Clear(_spikeContacts);
    }

    private void CompleteDrillCycle()
    {
        if (_drillLockedBody is not null)
        {
            _drilledParents.Add(_drillLockedBody.ParentId);
            _drillLockedBody.AddImpulse(new Vector2(92f, -8f), 1f / 120f);
        }
        _drillLockedBody = null;
        _drillCycleStarted = false;
        _drillReturning = false;
        _drillLeverHeld = false;
        _drillDamageAccumulator = 0f;
        _drillDamagePulses = 0;
    }

    private void CompletePressCycle(List<GranularParticle> granular, float dt)
    {
        if (_pressLockedBody is not null)
        {
            // The downward-facing hatch opens above the production line. Emit a
            // short, bounded discharge together with the processed blob.
            var available = Math.Min(18, GranularMaterialSystem.ParticleCapacity - granular.Count);
            for (var i = 0; i < available; i++)
            {
                var position = DrumCenter + new Vector2((i % 6 - 2.5f) * 3f, 31f + i / 6 * 2f);
                var velocity = new Vector2((i % 5 - 2f) * 7f, 58f + (i % 4) * 8f);
                granular.Add(new GranularParticle
                {
                    Position = position,
                    PreviousPosition = position - velocity * dt,
                    Radius = 1.8f + (i & 1) * 0.35f,
                    Lifetime = 32f,
                    Kind = GranularKind.Blood
                });
            }
            _pressedParents.Add(_pressLockedBody.ParentId);
            var outgoingBelt = Belts[3];
            var dropTarget = new Vector2(
                outgoingBelt.Position.X + MathF.Max(34f, _pressLockedBody.Radius * 0.82f),
                DeckY - _pressLockedBody.Radius - 5f);
            _pressLockedBody.ApplyTranslation(dropTarget - _pressLockedBody.Center, preserveVelocity: true);
            _pressLockedBody.AddImpulse(-_pressLockedBody.AverageVelocity(dt), dt);
            _pressLockedBody.AddImpulse(new Vector2(78f, -3f), dt);
        }
        ResetPress();
    }

    private void ResetPress()
    {
        _pressLockedBody = null;
        PressTravel = 0f;
        _drumWheelDragging = false;
        _drumLoading = false;
        _drumFinishing = false;
        _drumAngularSpeed = 0f;
        _drumProgress = 0f;
        _drumLoadProgress = 0f;
        _drumDoorOpenness = 0f;
        _drumInputMotion = 0f;
        _drumInputHold = 0f;
        _drumReleaseDelay = 0f;
        _drumDamageAccumulator = 0f;
    }

    private void CompleteVacuumCycle()
    {
        if (_vacuumLockedBody is not null)
        {
            _vacuumedParents.Add(_vacuumLockedBody.ParentId);
            _vacuumLockedBody.AddImpulse(new Vector2(92f, -6f), 1f / 120f);
        }
        VacuumHose.EndDrag();
        _vacuumLockedBody = null;
        _vacuumContact = false;
        _vacuumDamageAccumulator = 0f;
        _vacuumFlowPhase = 0f;
        // Extraction has already drained for the entire interaction. Retain at
        // most a tiny final gulp so Bay 4 cannot keep pouring after its blob is gone.
        _vacuumReservoir = MathF.Min(_vacuumReservoir, 1.12f);
        _vacuumDrainAccumulator = MathF.Max(_vacuumDrainAccumulator, 0.9f);
        _vacuumReleaseBoost = 0.72f;
    }

    private void ResetVacuum()
    {
        _vacuumLockedBody = null;
        VacuumHose.EndDrag();
        _vacuumProgress = 0f;
        _vacuumDamageAccumulator = 0f;
        _vacuumContact = false;
        _vacuumFlowPhase = 0f;
        _vacuumReservoir = 0f;
        _vacuumDrainAccumulator = 0f;
    }

    private void CompleteFilterCycle()
    {
        if (_filterLockedBody is not null)
        {
            _filteredParents.Add(_filterLockedBody.ParentId);
            _filterLockedBody.AddImpulse(new Vector2(98f, -5f), 1f / 120f);
        }
        _filterResiduePending = Math.Max(_filterResiduePending, 10);
        ResetFilter();
    }

    private void ResetFilter()
    {
        _filterLockedBody = null;
        _filterDragging = false;
        _filterReturning = false;
        _filterCompleteOnReturn = false;
        _filterKnob = 1f;
        _filterLastCutX = 0f;
        _filterStartKnob = 1f;
        _filterTraversed = false;
        _filterCutCount = 0;
    }

    private void PropelAcrossTables(IReadOnlyList<SoftBody> bodies, float dt)
    {
        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            var body = bodies[bodyIndex];
            if (ReferenceEquals(body, _lockedBody) || ReferenceEquals(body, _drillLockedBody) ||
                ReferenceEquals(body, _pressLockedBody) || ReferenceEquals(body, _vacuumLockedBody) ||
                ReferenceEquals(body, _filterLockedBody) ||
                body.IsGrabbed || IsInTransit(body)) continue;
            for (var bayIndex = 0; bayIndex < Bays.Count; bayIndex++)
            {
                var bay = Bays[bayIndex];
                if (body.Center.X < bay.Left - 8f || body.Center.X > bay.Right + 8f ||
                    body.Center.Y < DeckY - 72f || body.Center.Y > DeckY + 8f) continue;
                if (bayIndex == 0 && !_processedParents.Contains(body.ParentId)) break;
                if (bayIndex == 1 && !_drilledParents.Contains(body.ParentId)) break;
                if (bayIndex == 2 && !_pressedParents.Contains(body.ParentId)) break;
                if (bayIndex == 3 && !_vacuumedParents.Contains(body.ParentId)) break;
                if (bayIndex == 4 && !_filteredParents.Contains(body.ParentId)) break;
                var velocity = body.AverageVelocity(dt);
                if (velocity.X < 78f)
                    body.AddImpulse(new Vector2((78f - velocity.X) * 0.075f, 0f), dt);
                break;
            }
        }
    }

    private void UpdateBeltAutomation(IReadOnlyList<SoftBody> bodies)
    {
        foreach (var belt in Belts) belt.SetAutomationSpeed(OperatingSpeed);
        if (_lockedBody is not null) StopFeedWhenQueued(Belts[0], Bays[0].Left, bodies, _lockedBody);
        if (_drillLockedBody is not null) StopFeedWhenQueued(Belts[1], Bays[1].Left, bodies, _drillLockedBody);
        if (_pressLockedBody is not null) StopFeedWhenQueued(Belts[2], Bays[2].Left, bodies, _pressLockedBody);
        if (_vacuumLockedBody is not null) StopFeedWhenQueued(Belts[3], Bays[3].Left, bodies, _vacuumLockedBody);
        if (_filterLockedBody is not null) StopFeedWhenQueued(Belts[4], Bays[4].Left, bodies, _filterLockedBody);
        // Loose laser residue can arrive first and mark the cart loaded while the
        // actual blob is still crossing the very short final belt. Stopping on the
        // scalar IsCartLoaded flag deadlocked that blob at the belt's right roller.
        // Keep feeding until a soft body is physically inside the cart; then normal
        // back-pressure protects it from the next unit.
        var cartContainsBody = false;
        for (var i = 0; i < bodies.Count; i++)
            if (IsInCartLoadZone(bodies[i].Center))
            {
                cartContainsBody = true;
                break;
            }
        if (CartState != CartCycleState.Docked || IsCartLoaded && cartContainsBody)
            StopFeedWhenQueued(Belts[^1], Belts[^1].Position.X + Belts[^1].Width, bodies, null);
    }

    private static void StopFeedWhenQueued(
        ConveyorBelt feed,
        float limitX,
        IReadOnlyList<SoftBody> bodies,
        SoftBody? excluded)
    {
        var stopX = feed.Position.X + feed.Width - 40f;
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (ReferenceEquals(body, excluded) || !body.IsPickable ||
                body.Center.X < feed.Position.X - 30f || body.Center.X > limitX) continue;
            if (body.Center.X >= stopX)
            {
                feed.SetAutomationSpeed(0f);
                return;
            }
        }
    }

    public SurfaceContact ResolveParticle(SoftBody body, ref Particle particle, float dt)
    {
        if (ReferenceEquals(body, _pressLockedBody) && DrumBodyInside)
        {
            var drumContact = ResolveDrumWall(ref particle, dt);
            if (drumContact.Hit) return drumContact;
        }
        if (ReferenceEquals(body, _lockedBody) && CrusherTravel > 0.02f)
        {
            var contact = ResolveCrusherHead(ref particle, dt);
            if (contact.Hit) return contact;
        }
        if (ReferenceEquals(body, _drillLockedBody) && DrillTravel > 0.02f)
        {
            var bit = new RectangleF(Bays[1].CenterX - 7f, DrillHeadTop + 20f, 14f, 57f);
            var contact = ResolveBox(ref particle, bit, dt);
            if (contact.Hit) return contact;
        }
        var tubContact = ResolveReceivingTub(ref particle, dt);
        if (tubContact.Hit) return tubContact;
        var tableContact = ResolveTables(ref particle, dt);
        if (tableContact.Hit) return tableContact;
        var cartContact = ResolveCart(ref particle, dt);
        if (cartContact.Hit) return cartContact;
        return ResolveBox(ref particle, WalkwayBounds, dt);
    }

    private SurfaceContact ResolveDrumWall(ref Particle particle, float dt)
    {
        var offset = particle.Position - DrumCenter;
        var distanceSq = offset.LengthSquared();
        if (distanceSq < 0.0001f) return SurfaceContact.None;
        var distance = MathF.Sqrt(distanceSq);
        var outward = offset / distance;

        // The separating hatch leaves create an actual bottom opening. Every
        // other point remains a rotating circular wall.
        var apertureHalfWidth = MathF.Max(
            0f,
            _drumDoorOpenness * (DrumInteriorRadius + 6f) - particle.Radius);
        if (_drumDoorOpenness > 0.08f && outward.Y > 0.32f &&
            MathF.Abs(particle.Position.X - DrumCenter.X) <= apertureHalfWidth)
            return SurfaceContact.None;

        var penetration = distance + particle.Radius - DrumInteriorRadius;
        if (penetration <= 0f) return SurfaceContact.None;
        var safeDt = MathF.Max(dt, 0.0001f);
        var velocity = (particle.Position - particle.PreviousPosition) / safeDt;
        particle.Position -= outward * penetration;

        var tangent = new Vector2(-outward.Y, outward.X);
        var tangentSpeed = Vector2.Dot(velocity, tangent);
        var wallSpeed = _drumAngularSpeed * MathF.Max(1f, DrumInteriorRadius - particle.Radius);
        // Repeated contact friction approaches the moving wall velocity. The
        // tissue tumbles through collision; it is never explicitly rotated.
        tangentSpeed += (wallSpeed - tangentSpeed) * 0.10f;
        var outwardSpeed = Vector2.Dot(velocity, outward);
        var retainedNormalSpeed = outwardSpeed > 0f ? -outwardSpeed * 0.04f : outwardSpeed * 0.32f;
        var correctedVelocity = tangent * tangentSpeed + outward * retainedNormalSpeed;
        particle.PreviousPosition = particle.Position - correctedVelocity * safeDt;
        particle.Contacting = true;
        particle.ContactMemory = 6;
        var supported = outward.Y > 0.55f;
        if (supported)
        {
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        var inwardNormal = -outward;
        return new SurfaceContact(
            true,
            particle.Position - inwardNormal * particle.Radius,
            inwardNormal,
            MathF.Max(0f, outwardSpeed),
            supported);
    }

    private SurfaceContact ResolveCrusherHead(ref Particle particle, float dt)
    {
        // The old collider was one flat slab, so the blob could never visibly
        // wrap around the five drawn teeth. Keep the same travel and damage,
        // but make the contact geometry match the artwork: a backing plate and
        // five narrow downward triangles ending exactly at the damage points.
        var plate = new RectangleF(Bays[0].CenterX - 48f, CrusherHeadTop, 96f, 25f);
        var contact = ResolveBox(ref particle, plate, dt);
        if (contact.Hit) return contact;
        for (var i = 0; i < SpikeOffsets.Length; i++)
        {
            var centerX = Bays[0].CenterX + SpikeOffsets[i];
            contact = ResolveTriangle(
                ref particle,
                new Vector2(centerX - 7f, CrusherHeadTop + 23f),
                new Vector2(centerX + 7f, CrusherHeadTop + 23f),
                new Vector2(centerX, CrusherHeadTop + 48f),
                dt);
            if (contact.Hit) return contact;
        }
        return SurfaceContact.None;
    }

    public SurfaceContact ResolveGranularCartOnly(ref Particle particle, float dt)
    {
        if (!CouldNeedCartContainment(particle.Position, particle.PreviousPosition, particle.Radius))
            return SurfaceContact.None;
        var contact = ResolveCart(ref particle, dt);
        return contact.Hit ? contact : ResolveSweptCartContainment(ref particle, dt);
    }

    public bool CouldNeedCartContainment(Vector2 current, Vector2 previous, float radius)
    {
        var cart = OutputCartBounds;
        var minimumX = MathF.Min(current.X, previous.X) - radius;
        var maximumX = MathF.Max(current.X, previous.X) + radius;
        var minimumY = MathF.Min(current.Y, previous.Y) - radius;
        var maximumY = MathF.Max(current.Y, previous.Y) + radius;
        return maximumX >= cart.Left - 3f && minimumX <= cart.Right + 3f &&
               maximumY >= cart.Top + 2f && minimumY <= cart.Bottom + 4f;
    }

    public SurfaceContact ResolveGranular(ref Particle particle, float dt, GranularKind kind)
    {
        var tubContact = ResolveReceivingTub(ref particle, dt);
        if (tubContact.Hit) return tubContact;
        if (kind is GranularKind.Blood or GranularKind.Tissue && RouteGranularIntoDrain(ref particle))
            return SurfaceContact.None;
        var tableContact = ResolveTables(ref particle, dt);
        if (tableContact.Hit) return tableContact;
        var cartContact = ResolveCart(ref particle, dt);
        return cartContact.Hit ? cartContact : ResolveBox(ref particle, WalkwayBounds, dt);
    }

    private SurfaceContact ResolveReceivingTub(ref Particle particle, float dt)
    {
        var bounds = ReceivingTubBounds;
        if (particle.Position.X + particle.Radius < bounds.Left ||
            particle.Position.X - particle.Radius > bounds.Right ||
            particle.Position.Y + particle.Radius < DeckY - 2f ||
            particle.Position.Y - particle.Radius > bounds.Bottom + 8f)
            return SurfaceContact.None;

        var strongest = SurfaceContact.None;
        for (var segmentIndex = 0; segmentIndex < _receivingTubSurface.Length - 1; segmentIndex++)
        {
            var contact = ResolveReceivingTubSegment(
                ref particle,
                dt,
                _receivingTubSurface[segmentIndex],
                _receivingTubSurface[segmentIndex + 1],
                segmentIndex == _receivingTubSurface.Length - 2);
            if (contact.Hit && (!strongest.Hit || contact.Impact >= strongest.Impact))
                strongest = contact;
        }
        return strongest;
    }

    private static SurfaceContact ResolveReceivingTubSegment(
        ref Particle particle,
        float dt,
        Vector2 start,
        Vector2 end,
        bool isExitRamp)
    {
        var segment = end - start;
        var lengthSq = segment.LengthSquared();
        if (lengthSq < 0.001f) return SurfaceContact.None;
        var length = MathF.Sqrt(lengthSq);
        var tangent = segment / length;
        var normal = new Vector2(tangent.Y, -tangent.X);
        var rawT = Vector2.Dot(particle.Position - start, segment) / lengthSq;
        var endpointPadding = particle.Radius / length;
        if (rawT < -endpointPadding || rawT > 1f + endpointPadding || isExitRamp && rawT > 1f)
            return SurfaceContact.None;

        var t = Math.Clamp(rawT, 0f, 1f);
        var closest = start + segment * t;
        var delta = particle.Position - closest;
        var signedDistance = Vector2.Dot(delta, normal);
        var previousRawT = Vector2.Dot(particle.PreviousPosition - start, segment) / lengthSq;
        var previousClosest = start + segment * Math.Clamp(previousRawT, 0f, 1f);
        var previousSignedDistance = Vector2.Dot(particle.PreviousPosition - previousClosest, normal);
        var overlaps = delta.LengthSquared() <= particle.Radius * particle.Radius &&
                       (signedDistance >= -particle.Radius * 0.45f || previousSignedDistance >= 0f);
        var crossedFromAbove = previousSignedDistance >= particle.Radius * 0.20f &&
                               signedDistance < particle.Radius &&
                               rawT >= 0f && rawT <= 1f;
        if (!overlaps && !crossedFromAbove) return SurfaceContact.None;

        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var impact = MathF.Max(0f, -Vector2.Dot(velocity, normal));
        particle.Position += normal * MathF.Max(0f, particle.Radius - signedDistance);
        particle.Contacting = true;
        particle.ContactMemory = 6;
        particle.Supported = true;
        particle.SupportMemory = 10;

        // The tub is passive geometry. It absorbs a little sliding energy but never
        // adds conveyor-like motion of its own.
        var tangentSpeed = Vector2.Dot(velocity, tangent) * 0.90f;
        var outwardSpeed = MathF.Max(0f, Vector2.Dot(velocity, normal)) * 0.20f;
        var correctedVelocity = tangent * tangentSpeed + normal * outwardSpeed;
        particle.PreviousPosition = particle.Position - correctedVelocity * dt;
        return new SurfaceContact(
            true,
            particle.Position - normal * particle.Radius,
            normal,
            impact,
            normal.Y < -0.55f);
    }

    private bool RouteGranularIntoDrain(ref Particle particle)
    {
        if (particle.Position.Y < DeckY - 76f || particle.Position.Y > Basin.Top + 8f) return false;
        var closestBay = -1;
        var closestDistance = 64f;
        for (var i = 0; i < Bays.Count; i++)
        {
            var distance = MathF.Abs(particle.Position.X - Bays[i].CenterX);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closestBay = i;
        }
        if (closestBay < 0) return false;
        var centerX = Bays[closestBay].CenterX;
        var verticalDistance = MathF.Abs((DeckY - 4f) - particle.Position.Y);
        if (particle.Position.Y < DeckY && verticalDistance > 62f) return false;
        var attraction = Math.Clamp(1f - closestDistance / 64f, 0.12f, 1f);
        var shift = Math.Clamp((centerX - particle.Position.X) * (0.055f + attraction * 0.055f), -2.4f, 2.4f);
        particle.Position.X += shift;
        particle.PreviousPosition.X += shift;
        if (particle.Position.Y < DeckY + 4f)
            particle.PreviousPosition.Y = MathF.Min(particle.PreviousPosition.Y,
                particle.Position.Y - (0.45f + attraction * 0.85f));
        var enteringAperture = particle.Position.Y + particle.Radius >= DeckY - 1f &&
                               MathF.Abs(particle.Position.X - centerX) <= 11f;
        if (particle.Position.Y < DeckY - 2f && !enteringAperture) return false;
        var clampedX = Math.Clamp(particle.Position.X, centerX - 5f, centerX + 5f);
        var correction = clampedX - particle.Position.X;
        particle.Position.X = clampedX;
        particle.PreviousPosition.X += correction;
        return true;
    }

    private SurfaceContact ResolveTables(ref Particle particle, float dt)
    {
        for (var i = 0; i < Bays.Count; i++)
        {
            var bay = Bays[i];
            var contact = ResolveBox(ref particle, new RectangleF(bay.Left, DeckY, bay.Width, 10f), dt);
            if (contact.Hit) return contact;
        }
        return SurfaceContact.None;
    }

    private SurfaceContact ResolveCart(ref Particle particle, float dt)
    {
        var b = OutputCartBounds;
        var left = new RectangleF(b.Left, b.Top + 7f, 7f, b.Height - 7f);
        var right = new RectangleF(b.Right - 7f, b.Top + 7f, 7f, b.Height - 7f);
        // The physical bin floor follows the top edge of the cart's solid
        // lower panel, not the wheel baseline. This keeps the soft contour
        // contained behind the foreground cart art instead of leaking below it.
        var contact = ResolveCartFloor(ref particle, b, dt);
        if (contact.Hit) return contact;
        contact = ResolveBox(ref particle, left, dt);
        if (contact.Hit) return contact;
        contact = ResolveBox(ref particle, right, dt);
        return contact;
    }

    private SurfaceContact ResolveSweptCartContainment(ref Particle particle, float dt)
    {
        var cart = OutputCartBounds;
        var innerLeft = cart.Left + 7f + particle.Radius;
        var innerRight = cart.Right - 7f - particle.Radius;
        var rimY = cart.Top + 7f;
        var floorY = CartFloorY;
        var previous = particle.PreviousPosition;
        var current = particle.Position;

        var previousInside = previous.X >= innerLeft && previous.X <= innerRight &&
                             previous.Y + particle.Radius >= rimY &&
                             previous.Y - particle.Radius <= floorY + 2f;
        var currentInside = current.X >= innerLeft && current.X <= innerRight &&
                            current.Y + particle.Radius >= rimY &&
                            current.Y - particle.Radius <= floorY + 2f;
        var enteredThroughOpenTop = false;
        if (previous.Y + particle.Radius < rimY && current.Y + particle.Radius >= rimY)
        {
            var denominator = current.Y - previous.Y;
            var t = MathF.Abs(denominator) < 0.0001f
                ? 1f
                : Math.Clamp((rimY - particle.Radius - previous.Y) / denominator, 0f, 1f);
            var crossingX = previous.X + (current.X - previous.X) * t;
            enteredThroughOpenTop = crossingX >= innerLeft && crossingX <= innerRight;
        }
        if (!previousInside && !currentInside && !enteredThroughOpenTop)
            return SurfaceContact.None;

        var velocity = (current - previous) / MathF.Max(dt, 0.0001f);
        var correctedVelocity = velocity;
        var normalSum = Vector2.Zero;
        var hit = false;
        if (particle.Position.X < innerLeft)
        {
            particle.Position.X = innerLeft;
            normalSum += Vector2.UnitX;
            correctedVelocity.X = MathF.Max(0f, correctedVelocity.X) * 0.16f;
            hit = true;
        }
        else if (particle.Position.X > innerRight)
        {
            particle.Position.X = innerRight;
            normalSum -= Vector2.UnitX;
            correctedVelocity.X = MathF.Min(0f, correctedVelocity.X) * 0.16f;
            hit = true;
        }
        if (particle.Position.Y + particle.Radius > floorY)
        {
            particle.Position.Y = floorY - particle.Radius;
            normalSum -= Vector2.UnitY;
            correctedVelocity.Y = MathF.Min(0f, correctedVelocity.Y) * 0.12f;
            hit = true;
        }
        if (!hit) return SurfaceContact.None;

        correctedVelocity *= 0.88f;
        particle.PreviousPosition = particle.Position - correctedVelocity * dt;
        particle.Contacting = true;
        particle.ContactMemory = 6;
        if (normalSum.Y < -0.1f)
        {
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        var normal = normalSum.LengthSquared() > 0.001f
            ? Vector2.Normalize(normalSum)
            : -Vector2.UnitY;
        return new SurfaceContact(
            true,
            particle.Position - normal * particle.Radius,
            normal,
            velocity.Length(),
            normal.Y < -0.55f);
    }

    private static SurfaceContact ResolveCartFloor(ref Particle particle, RectangleF cart, float dt)
    {
        var floorY = cart.Bottom - 20f;
        if (particle.Position.X + particle.Radius < cart.Left + 4f ||
            particle.Position.X - particle.Radius > cart.Right - 4f ||
            particle.Position.Y + particle.Radius <= floorY ||
            particle.Position.Y - particle.Radius >= cart.Bottom + 2f)
            return SurfaceContact.None;

        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var impact = MathF.Max(0f, velocity.Y);
        particle.Position.Y = floorY - particle.Radius;
        // A bin floor is one-way containment: downward velocity is absorbed,
        // while legitimate upward recoil remains possible inside the cart.
        var correctedY = MathF.Min(0f, velocity.Y);
        particle.PreviousPosition = new Vector2(
            particle.Position.X - velocity.X * 0.91f * dt,
            particle.Position.Y - correctedY * dt);
        particle.Contacting = true;
        particle.ContactMemory = 6;
        particle.Supported = true;
        particle.SupportMemory = 10;
        return new SurfaceContact(
            true,
            new Vector2(particle.Position.X, floorY),
            -Vector2.UnitY,
            impact,
            true);
    }

    private static SurfaceContact ResolveBox(ref Particle particle, RectangleF box, float dt)
    {
        var min = new Vector2(box.Left, box.Top);
        var max = new Vector2(box.Right, box.Bottom);
        var closest = Vector2.Clamp(particle.Position, min, max);
        var delta = particle.Position - closest;
        var distanceSq = delta.LengthSquared();
        if (distanceSq > particle.Radius * particle.Radius) return SurfaceContact.None;
        Vector2 normal;
        float depth;
        if (distanceSq > 0.0001f)
        {
            var distance = MathF.Sqrt(distanceSq);
            normal = delta / distance;
            depth = particle.Radius - distance;
        }
        else
        {
            var l = particle.Position.X - min.X;
            var r = max.X - particle.Position.X;
            var t = particle.Position.Y - min.Y;
            var b = max.Y - particle.Position.Y;
            var nearest = MathF.Min(MathF.Min(l, r), MathF.Min(t, b));
            normal = nearest == l ? -Vector2.UnitX : nearest == r ? Vector2.UnitX : nearest == t ? -Vector2.UnitY : Vector2.UnitY;
            depth = particle.Radius + nearest;
        }
        if (depth <= 0f) return SurfaceContact.None;
        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var impact = MathF.Max(0f, -Vector2.Dot(velocity, normal));
        particle.Position += normal * depth;
        var tangent = velocity - normal * Vector2.Dot(velocity, normal);
        particle.PreviousPosition = particle.Position - tangent * 0.91f * dt;
        particle.Contacting = true;
        if (normal.Y < -0.55f)
        {
            particle.Supported = true;
            particle.SupportMemory = 10;
        }
        return new SurfaceContact(true, particle.Position - normal * particle.Radius, normal, impact, normal.Y < -0.55f);
    }

    private static SurfaceContact ResolveTriangle(
        ref Particle particle,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        float dt)
    {
        var ab = ClosestPointOnSegment(particle.Position, a, b);
        var bc = ClosestPointOnSegment(particle.Position, b, c);
        var ca = ClosestPointOnSegment(particle.Position, c, a);
        var closest = ab;
        var distanceSq = Vector2.DistanceSquared(particle.Position, ab);
        var candidateDistance = Vector2.DistanceSquared(particle.Position, bc);
        if (candidateDistance < distanceSq)
        {
            distanceSq = candidateDistance;
            closest = bc;
        }
        candidateDistance = Vector2.DistanceSquared(particle.Position, ca);
        if (candidateDistance < distanceSq)
        {
            distanceSq = candidateDistance;
            closest = ca;
        }

        var inside = PointInTriangle(particle.Position, a, b, c);
        if (!inside && distanceSq > particle.Radius * particle.Radius) return SurfaceContact.None;
        var distance = MathF.Sqrt(MathF.Max(distanceSq, 0.0001f));
        var normal = inside
            ? (closest - particle.Position) / distance
            : (particle.Position - closest) / distance;
        if (!float.IsFinite(normal.X) || !float.IsFinite(normal.Y)) normal = -Vector2.UnitY;
        var depth = inside ? particle.Radius + distance : particle.Radius - distance;
        if (depth <= 0f) return SurfaceContact.None;

        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        var impact = MathF.Max(0f, -Vector2.Dot(velocity, normal));
        particle.Position += normal * depth;
        var tangent = velocity - normal * Vector2.Dot(velocity, normal);
        particle.PreviousPosition = particle.Position - tangent * 0.91f * dt;
        particle.Contacting = true;
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

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSq = segment.LengthSquared();
        if (lengthSq < 0.0001f) return start;
        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSq, 0f, 1f);
        return start + segment * t;
    }

    private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
            (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        var d1 = Sign(point, a, b);
        var d2 = Sign(point, b, c);
        var d3 = Sign(point, c, a);
        var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
        => current < target ? MathF.Min(current + maxDelta, target) : MathF.Max(current - maxDelta, target);

    private static float SmoothStep01(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return amount * amount * (3f - 2f * amount);
    }

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle < -MathF.PI) angle += MathF.Tau;
        return angle;
    }
}

public sealed record ProcessingBay(int Index, float Left, float Width)
{
    public float Right => Left + Width;
    public float CenterX => Left + Width * 0.5f;
}
