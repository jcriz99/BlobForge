namespace BlobForge.World;

/// <summary>
/// A bounded, single-buffer cellular liquid inspired by Noita's falling-material
/// rules. Incoming ballistic blood becomes basin-local cells; settled material
/// sleeps, so the pool has near-zero simulation cost when undisturbed.
/// </summary>
public sealed class BloodBasin
{
    // The current factory artwork uses 64 logical pixels per meter (one 32 px
    // wall tile is 0.5 m). The side-view tank is authored as a one-meter-deep
    // industrial basin. These dimensions turn the conserved 2D fill fraction
    // into an explicit estimated real-world quantity instead of relabeling
    // arbitrary simulation area as gallons.
    public const float WorldUnitsPerMeter = 64f;
    public const float EstimatedTankDepthMeters = 1f;
    public const float LitersPerCubicMeter = 1000f;
    public const float LitersPerUsGallon = 3.7854118f;
    // Retained as the compatibility projection used by diagnostics and the volume gauge.
    public const int ColumnCount = 80;
    public const int FluidGridWidth = 289;
    public const int FluidGridHeight = 30;
    private const int VisualSurfaceFilterRadius = 9;
    private const int MaximumInteriorStains = 20;
    private const int MaximumSuspendedDrops = 96;
    private const int MaximumSurfaceSplashes = 64;
    private const int MaximumSurfaceRipples = 18;
    private const int MaximumFrontOverflowStains = 10;
    private static readonly int[] NaturalSurfaceOrder = BuildNaturalSurfaceOrder();

    private readonly bool[] _cells = new bool[FluidGridWidth * FluidGridHeight];
    private readonly int[] _surfaceRows = new int[FluidGridWidth];
    private readonly int[] _columnMass = new int[FluidGridWidth];
    private readonly int[] _targetColumnMass = new int[FluidGridWidth];
    private readonly float[] _heights = new float[ColumnCount];
    private readonly List<BasinInteriorStain> _interiorStains = new(MaximumInteriorStains);
    private readonly List<BasinSuspendedDrop> _suspendedDrops = new(MaximumSuspendedDrops);
    private readonly List<BasinSurfaceSplash> _surfaceSplashes = new(MaximumSurfaceSplashes);
    private readonly List<BasinSurfaceRipple> _surfaceRipples = new(MaximumSurfaceRipples);
    private readonly List<BasinPipeStain> _pipeStains = new(8);
    private readonly List<BasinFrontOverflowStain> _frontOverflowStains =
        new(MaximumFrontOverflowStains);
    private float _availableFood;
    private float _availableDrinkVolume;
    private float _simulationAccumulator;
    private float _creatureDirection = 1f;
    private float _creatureFeedHold;
    private int _fluidCellCount;
    private int _settledTicks;
    private bool _fluidActive;
    private float _liquidCenterColumn = (FluidGridWidth - 1) * 0.5f;
    private int _fractionalColumn = -1;
    private int _fractionalRow = -1;
    private float _fractionalFill;
    private float _sloshAmplitude;
    private float _sloshPhase;
    private float _sloshDirection = 1f;
    private int _inflowVisualSerial;
    private int _overflowSideSerial;

    public BloodBasin(float left, float top, float width, float height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        CreatureX = left + width * 0.56f;
        Array.Fill(_surfaceRows, FluidGridHeight);
    }

    public float Left { get; }
    public float Top { get; }
    public float Width { get; }
    public float Height { get; }
    public float Right => Left + Width;
    public float Bottom => Top + Height;
    public float FluidTop => Top + 5f;
    public float FluidBottom => Bottom - 7f;
    public float FluidCellWidth => Width / FluidGridWidth;
    public float FluidCellHeight => (Height - 12f) / FluidGridHeight;
    public float FluidCellVolume => FluidCapacity / (FluidGridWidth * FluidGridHeight);
    public IReadOnlyList<float> Heights => _heights;
    public int FluidVisualRevision { get; private set; }
    public int FluidCellCount => _fluidCellCount;
    public bool FluidIsActive => _fluidActive;
    public float TotalDeposited { get; private set; }
    public float CurrentFluidVolume { get; private set; }
    public float PendingFluidVolume { get; private set; }
    public float TotalSpent { get; private set; }
    public float TotalOverflowed { get; private set; }
    public float FluidCapacity => Width * (Height - 12f);
    public float StoredVolume => Math.Clamp(CurrentFluidVolume + PendingFluidVolume, 0f, FluidCapacity);
    public float EstimatedCapacityLiters =>
        Width / WorldUnitsPerMeter *
        ((Height - 12f) / WorldUnitsPerMeter) *
        EstimatedTankDepthMeters *
        LitersPerCubicMeter;
    public float EstimatedStoredLiters => EstimatedCapacityLiters *
        Math.Clamp(StoredVolume / MathF.Max(0.001f, FluidCapacity), 0f, 1f);
    public float EstimatedStoredGallons => EstimatedStoredLiters / LitersPerUsGallon;
    public float SpendableBlood => CurrentFluidVolume;
    public float RemainingCapacity => MathF.Max(0f, FluidCapacity - StoredVolume);
    public bool IsFull => RemainingCapacity <= MathF.Max(0.01f, FluidCapacity * 0.000001f);
    public float AverageFluidHeight => Math.Clamp(CurrentFluidVolume / Width, 0f, Height - 12f);
    public float FluidLevel01 => Math.Clamp(CurrentFluidVolume / FluidCapacity, 0f, 1f);
    public float CreatureConsumed { get; private set; }
    public float TotalConsumedFluid { get; private set; }
    public float CreatureScale => Math.Clamp(0.48f + MathF.Sqrt(CreatureConsumed) * 0.105f, 0.48f, 3.15f);
    public float CreaturePhase { get; private set; }
    public float CreatureX { get; private set; }
    public float CreatureDirection => _creatureDirection;
    public bool CreatureIsFeeding => _creatureFeedHold > 0f;
    public float CreatureDrinkTime { get; private set; }
    public float CreatureFullness => 1f - MathF.Exp(-CreatureConsumed * 0.12f);
    public float BubblePhase { get; private set; }
    public static bool DiegoEnabled => false;
    public float SloshAmplitude => _sloshAmplitude;
    public IReadOnlyList<BasinInteriorStain> InteriorStains => _interiorStains;
    public IReadOnlyList<BasinSuspendedDrop> SuspendedDrops => _suspendedDrops;
    public IReadOnlyList<BasinSurfaceSplash> SurfaceSplashes => _surfaceSplashes;
    public IReadOnlyList<BasinSurfaceRipple> SurfaceRipples => _surfaceRipples;
    public IReadOnlyList<BasinPipeStain> PipeStains => _pipeStains;
    public IReadOnlyList<BasinFrontOverflowStain> FrontOverflowStains =>
        _frontOverflowStains;

    public bool ContainsHorizontal(float x) => x >= Left + 8f && x <= Right - 8f;

    public float FluidFillAt(int x, int y)
    {
        if ((uint)x >= FluidGridWidth || (uint)y >= FluidGridHeight) return 0f;
        if (_cells[Index(x, y)]) return 1f;
        return x == _fractionalColumn && y == _fractionalRow ? _fractionalFill : 0f;
    }

    public float SurfaceYAt(float x)
    {
        var column = GridColumnAt(x);
        return FluidBottom - VisualFluidDepthAt(column) * FluidCellHeight;
    }

    /// <summary>
    /// Returns the visually filtered depth in fluid cells. The authoritative
    /// cellular state remains untouched; a cyclic triangular filter merely
    /// turns adjacent one-cell pressure bands into a restrained liquid wave.
    /// Because every source column contributes the same total weight, the
    /// filter preserves the represented volume exactly.
    /// </summary>
    public float VisualFluidDepthAt(int x)
    {
        if ((uint)x >= FluidGridWidth) return 0f;
        float baseDepth;
        if (_fluidCellCount == 0)
            baseDepth = x == _fractionalColumn && _fractionalRow >= 0 ? _fractionalFill : 0f;
        else if (_fluidCellCount < FluidGridWidth)
        {
            baseDepth = _columnMass[x];
            if (x == _fractionalColumn && _fractionalRow >= 0)
                baseDepth += _fractionalFill;
        }
        else
        {
            var weightedMass = 0f;
            var totalWeight = 0;
            for (var offset = -VisualSurfaceFilterRadius; offset <= VisualSurfaceFilterRadius; offset++)
            {
                var weight = VisualSurfaceFilterRadius + 1 - Math.Abs(offset);
                var sample = (x + offset + FluidGridWidth) % FluidGridWidth;
                weightedMass += _columnMass[sample] * weight;
                totalWeight += weight;
            }
            // A sub-cell remainder is pressure, not a solitary visible spike.
            baseDepth = weightedMass / totalWeight + _fractionalFill / FluidGridWidth;
        }

        if (baseDepth <= 0.02f) return baseDepth;
        // A deterministic sub-cell ripple keeps a settled pool from becoming a
        // ruler-straight strip. It has no angle of repose and does not touch the
        // conserved cell state, so it reads as liquid texture rather than sand.
        var roughPhase = MathF.Tau * (x + 0.5f) / FluidGridWidth;
        var roughness = (MathF.Sin(roughPhase * 7f + 0.41f) * 0.52f +
                         MathF.Sin(roughPhase * 13f + 1.73f) * 0.31f +
                         MathF.Sin(roughPhase * 23f + 0.16f) * 0.17f) * 0.34f;
        if (_sloshAmplitude <= 0.001f)
            return Math.Clamp(baseDepth + roughness, 0f, FluidGridHeight);
        var phase = MathF.Tau * (x + 0.5f) / FluidGridWidth;
        var wave = MathF.Sin(phase * 2f + _sloshPhase) * 0.72f +
                   MathF.Sin(phase * 3f - _sloshPhase * 0.63f + 1.17f) * 0.28f;
        return Math.Clamp(baseDepth + roughness + wave * _sloshAmplitude, 0f, FluidGridHeight);
    }

    public float VisualFluidFillAt(int x, int visualY, int verticalScale)
    {
        if ((uint)x >= FluidGridWidth || verticalScale <= 0 ||
            (uint)visualY >= FluidGridHeight * verticalScale)
            return 0f;
        var depthFromBottom = FluidGridHeight - (visualY + 1f) / verticalScale;
        return Math.Clamp((VisualFluidDepthAt(x) - depthFromBottom) * verticalScale, 0f, 1f);
    }

    public void AddMaterial(float x, float fluidVolume, float downwardSpeed, float nutrition)
    {
        fluidVolume = Math.Clamp(fluidVolume, 0f, RemainingCapacity);
        if (fluidVolume <= 0f) return;
        var volumeBeforeDeposit = CurrentFluidVolume;
        var depositColumn = GridColumnAt(x);
        _liquidCenterColumn = volumeBeforeDeposit <= 0.0001f
            ? depositColumn
            : (_liquidCenterColumn * volumeBeforeDeposit + depositColumn * fluidVolume) /
              (volumeBeforeDeposit + fluidVolume);
        CurrentFluidVolume += fluidVolume;
        TotalDeposited += fluidVolume;
        if (DiegoEnabled)
        {
            _availableFood += nutrition;
            _availableDrinkVolume += fluidVolume;
        }
        SynchronizeCellsToVolume(x);
        RegisterInflowVisual(x, downwardSpeed, fluidVolume);
    }

    /// <summary>
    /// Keeps captured material visible inside the tank before it joins the
    /// cellular pool. Drops bob at the surface and dissolve gradually instead
    /// of popping out of existence on contact.
    /// </summary>
    public void AddSuspendedMaterial(
        float x,
        float y,
        float fluidVolume,
        float downwardSpeed,
        float nutrition,
        float radius)
    {
        fluidVolume = Math.Clamp(fluidVolume, 0f, RemainingCapacity);
        if (fluidVolume <= 0f) return;

        if (_suspendedDrops.Count >= MaximumSuspendedDrops)
            DissolveSuspendedDrop(0, 1f);

        var serial = ++_inflowVisualSerial;
        var drift = (((serial * 37) & 15) - 7.5f) * 0.22f;
        _suspendedDrops.Add(new BasinSuspendedDrop
        {
            X = Math.Clamp(x, Left + 8f, Right - 8f),
            Y = Math.Clamp(y, FluidTop + radius, FluidBottom - radius),
            VelocityX = drift,
            VelocityY = Math.Clamp(downwardSpeed * 0.12f, 0f, 42f),
            Radius = Math.Clamp(radius, 1.4f, 4.6f),
            RemainingVolume = fluidVolume,
            InitialVolume = fluidVolume,
            RemainingNutrition = nutrition,
            Age = 0f,
            Variation = (byte)(serial * 73)
        });
        PendingFluidVolume += fluidVolume;
        TotalDeposited += fluidVolume;
        RegisterSurfaceImpact(x, downwardSpeed, radius, fluidVolume);
        RegisterInflowVisual(x, downwardSpeed, fluidVolume);
    }

    /// <summary>
    /// Records the displacement caused by matter striking a completely full
    /// pool. The displaced amount is deliberately excluded from stored and
    /// spendable blood; the original granular particle remains physical and is
    /// emitted over a lip by ProcessingLine.
    /// </summary>
    public bool RegisterOverflowImpact(float x, float downwardSpeed, float radius, float displacedVolume)
    {
        displacedVolume = MathF.Max(0f, displacedVolume);
        TotalOverflowed += displacedVolume;
        RegisterSurfaceImpact(x, downwardSpeed, radius, displacedVolume);
        RegisterInflowVisual(x, downwardSpeed, displacedVolume, stainPipe: false);
        _overflowSideSerial++;
        // Front-glass overflow trails are deliberately disabled until their
        // presentation is redesigned. Clear any legacy marks retained by a
        // long-running session while preserving physical lip overflow.
        _frontOverflowStains.Clear();
        // Drain position must not bias the exterior spill. Strict alternation keeps
        // long full-basin runs evenly distributed across both lips.
        return (_overflowSideSerial & 1) != 0;
    }

    public bool TrySpend(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f || CurrentFluidVolume + 0.001f < amount)
            return false;
        CurrentFluidVolume = MathF.Max(0f, CurrentFluidVolume - amount);
        TotalSpent += amount;
        _availableDrinkVolume = MathF.Min(_availableDrinkVolume, CurrentFluidVolume);
        SynchronizeCellsToVolume(Left + Width * 0.5f);
        return true;
    }

    public float SellAllStoredBlood()
    {
        var sold = StoredVolume;
        if (sold <= 0f) return 0f;
        CurrentFluidVolume = 0f;
        PendingFluidVolume = 0f;
        TotalSpent += sold;
        _availableFood = 0f;
        _availableDrinkVolume = 0f;
        _suspendedDrops.Clear();
        _surfaceSplashes.Clear();
        _surfaceRipples.Clear();
        _sloshAmplitude = 0f;
        SynchronizeCellsToVolume(Left + Width * 0.5f);
        return sold;
    }

    public void Step(float dt)
    {
        BubblePhase = (BubblePhase + dt) % 4096f;
        if (DiegoEnabled) StepCreature(dt);

        _simulationAccumulator += dt;
        if (_simulationAccumulator < 1f / 60f) return;
        _simulationAccumulator %= 1f / 60f;
        StepSuspendedDrops(1f / 60f);
        StepSurfaceImpacts(1f / 60f);
        StepInteriorEffects(1f / 60f);
        if (!_fluidActive || _fluidCellCount == 0) return;
        StepFluidCells();
    }

    private void StepSuspendedDrops(float dt)
    {
        if (_suspendedDrops.Count == 0) return;
        var dissolvedVolume = 0f;
        var dissolvedNutrition = 0f;
        var weightedX = 0f;

        for (var i = _suspendedDrops.Count - 1; i >= 0; i--)
        {
            var drop = _suspendedDrops[i];
            drop.Age += dt;
            var surface = Math.Clamp(SurfaceYAt(drop.X), FluidTop + 3f, FluidBottom);
            var targetY = Math.Clamp(surface + drop.Radius * 0.18f, FluidTop + drop.Radius, FluidBottom - drop.Radius);
            var buoyancy = (targetY - drop.Y) * 12f;
            drop.VelocityY = (drop.VelocityY + buoyancy * dt) * MathF.Exp(-dt * 4.6f);
            drop.VelocityX *= MathF.Exp(-dt * 1.8f);
            drop.X = Math.Clamp(drop.X + drop.VelocityX * dt, Left + 8f, Right - 8f);
            drop.Y = Math.Clamp(drop.Y + drop.VelocityY * dt, FluidTop + drop.Radius, FluidBottom - drop.Radius);

            var dissolveFraction = drop.Age < 0.18f
                ? 0f
                : Math.Clamp(dt / (0.72f + (drop.Variation & 7) * 0.055f), 0f, 1f);
            if (dissolveFraction > 0f)
            {
                var volume = drop.RemainingVolume * dissolveFraction;
                var nutrition = drop.RemainingNutrition * dissolveFraction;
                drop.RemainingVolume -= volume;
                drop.RemainingNutrition -= nutrition;
                dissolvedVolume += volume;
                dissolvedNutrition += nutrition;
                weightedX += drop.X * volume;
            }

            if (drop.RemainingVolume <= MathF.Max(0.006f, drop.InitialVolume * 0.025f))
            {
                dissolvedVolume += drop.RemainingVolume;
                dissolvedNutrition += drop.RemainingNutrition;
                weightedX += drop.X * drop.RemainingVolume;
                _suspendedDrops.RemoveAt(i);
            }
            else
            {
                _suspendedDrops[i] = drop;
            }
        }

        if (dissolvedVolume <= 0f) return;
        PendingFluidVolume = MathF.Max(0f, PendingFluidVolume - dissolvedVolume);
        var focusX = weightedX / dissolvedVolume;
        var before = CurrentFluidVolume;
        CurrentFluidVolume = Math.Clamp(CurrentFluidVolume + dissolvedVolume, 0f, FluidCapacity);
        var accepted = CurrentFluidVolume - before;
        if (accepted <= 0f) return;
        var column = GridColumnAt(focusX);
        _liquidCenterColumn = before <= 0.0001f
            ? column
            : (_liquidCenterColumn * before + column * accepted) / (before + accepted);
        if (DiegoEnabled)
        {
            _availableFood += dissolvedNutrition;
            _availableDrinkVolume += accepted;
        }
        SynchronizeCellsToVolume(focusX);
    }

    private void DissolveSuspendedDrop(int index, float fraction)
    {
        if ((uint)index >= _suspendedDrops.Count) return;
        var drop = _suspendedDrops[index];
        fraction = Math.Clamp(fraction, 0f, 1f);
        var volume = drop.RemainingVolume * fraction;
        var nutrition = drop.RemainingNutrition * fraction;
        PendingFluidVolume = MathF.Max(0f, PendingFluidVolume - volume);
        CurrentFluidVolume = Math.Clamp(CurrentFluidVolume + volume, 0f, FluidCapacity);
        if (DiegoEnabled)
        {
            _availableFood += nutrition;
            _availableDrinkVolume += volume;
        }
        SynchronizeCellsToVolume(drop.X);
        _suspendedDrops.RemoveAt(index);
    }

    private void RegisterSurfaceImpact(float x, float downwardSpeed, float radius, float fluidVolume)
    {
        if (CurrentFluidVolume < FluidCellVolume * 4f || downwardSpeed < 18f) return;
        var surface = Math.Clamp(SurfaceYAt(x), FluidTop + 3f, FluidBottom - 2f);
        var strength = Math.Clamp(
            downwardSpeed / 150f + radius * 0.12f + fluidVolume / FluidCellVolume * 0.018f,
            0.22f,
            1.35f);
        var count = Math.Clamp(2 + (int)(strength * 4.2f), 2, 8);
        var serial = _inflowVisualSerial + 1;
        for (var splash = 0; splash < count; splash++)
        {
            if (_surfaceSplashes.Count >= MaximumSurfaceSplashes)
                _surfaceSplashes.RemoveAt(0);
            var centered = count <= 1 ? 0f : splash / (float)(count - 1) * 2f - 1f;
            var variation = (byte)(serial * 59 + splash * 83);
            var jitter = ((variation & 7) - 3f) * 0.34f;
            var velocityX = centered * (22f + strength * 34f) + jitter;
            var upwardSpeed = (34f + strength * 66f) * (0.78f + ((variation >> 3) & 3) * 0.10f);
            _surfaceSplashes.Add(new BasinSurfaceSplash
            {
                X = Math.Clamp(x + centered * (2f + radius * 0.45f), Left + 8f, Right - 8f),
                Y = surface - 2f,
                VelocityX = velocityX,
                VelocityY = -upwardSpeed,
                Radius = 1f + (variation & 1) * 0.65f + strength * 0.28f,
                Age = 0f,
                Lifetime = 0.28f + ((variation >> 5) & 3) * 0.055f,
                Variation = variation
            });
        }

        if (_surfaceRipples.Count >= MaximumSurfaceRipples) _surfaceRipples.RemoveAt(0);
        _surfaceRipples.Add(new BasinSurfaceRipple
        {
            X = Math.Clamp(x, Left + 10f, Right - 10f),
            Age = 0f,
            Lifetime = 0.34f + MathF.Min(0.16f, strength * 0.08f),
            Strength = strength,
            Variation = (byte)(serial * 71)
        });
    }

    private void StepSurfaceImpacts(float dt)
    {
        for (var i = _surfaceSplashes.Count - 1; i >= 0; i--)
        {
            var splash = _surfaceSplashes[i];
            splash.Age += dt;
            splash.VelocityY += 285f * dt;
            splash.VelocityX *= MathF.Exp(-dt * 0.55f);
            splash.X = Math.Clamp(splash.X + splash.VelocityX * dt, Left + 7f, Right - 7f);
            splash.Y += splash.VelocityY * dt;
            var returnedToSurface = splash.Age > 0.09f &&
                                    splash.Y >= SurfaceYAt(splash.X) - splash.Radius * 0.15f;
            if (splash.Age >= splash.Lifetime || returnedToSurface)
                _surfaceSplashes.RemoveAt(i);
            else
                _surfaceSplashes[i] = splash;
        }
        for (var i = _surfaceRipples.Count - 1; i >= 0; i--)
        {
            var ripple = _surfaceRipples[i];
            ripple.Age += dt;
            if (ripple.Age >= ripple.Lifetime) _surfaceRipples.RemoveAt(i);
            else _surfaceRipples[i] = ripple;
        }
    }

    private void StepCreature(float dt)
    {
        CreaturePhase = (CreaturePhase + dt * (1.2f + CreatureScale * 0.18f)) % (MathF.PI * 2f);
        var appetite = (0.38f + CreatureScale * 0.22f) * dt;
        var foodBeforeEating = _availableFood;
        var eaten = MathF.Min(_availableFood, appetite);
        _availableFood -= eaten;
        CreatureConsumed += eaten;
        if (eaten > 0f && foodBeforeEating > 0.0001f && _availableDrinkVolume > 0f)
        {
            var swallowed = MathF.Min(
                CurrentFluidVolume,
                _availableDrinkVolume * Math.Clamp(eaten / foodBeforeEating, 0f, 1f));
            _availableDrinkVolume = MathF.Max(0f, _availableDrinkVolume - swallowed);
            CurrentFluidVolume = MathF.Max(0f, CurrentFluidVolume - swallowed);
            TotalConsumedFluid += swallowed;
            SynchronizeCellsToVolume(CreatureX);
        }

        if (eaten > 0.0001f)
        {
            if (_creatureFeedHold <= 0f) CreatureDrinkTime = 0f;
            _creatureFeedHold = 0.42f;
            CreatureDrinkTime += dt;
        }
        else
        {
            _creatureFeedHold = MathF.Max(0f, _creatureFeedHold - dt);
            if (_creatureFeedHold <= 0f)
            {
                var margin = 74f + CreatureFullness * 18f;
                CreatureX += _creatureDirection * (15f + CreatureScale * 4f) * dt;
                if (CreatureX >= Right - margin)
                {
                    CreatureX = Right - margin;
                    _creatureDirection = -1f;
                }
                else if (CreatureX <= Left + margin)
                {
                    CreatureX = Left + margin;
                    _creatureDirection = 1f;
                }
            }
        }

    }

    private void RegisterInflowVisual(float x, float downwardSpeed, float fluidVolume, bool stainPipe = true)
    {
        _inflowVisualSerial++;
        var normalizedX = Math.Clamp((x - Left) / Width, 0f, 1f);
        var impulse = Math.Clamp(
            0.08f + downwardSpeed / 360f + fluidVolume / MathF.Max(FluidCellVolume, 0.001f) * 0.025f,
            0.08f,
            0.92f);
        _sloshAmplitude = Math.Clamp(
            MathF.Max(_sloshAmplitude, impulse * 0.72f) + impulse * 0.14f,
            0f,
            1.18f);
        _sloshDirection = normalizedX < 0.5f ? 1f : -1f;
        _sloshPhase += (normalizedX - 0.5f) * 0.42f;
        FluidVisualRevision++;
        if (stainPipe) RegisterPipeStain(x, downwardSpeed, fluidVolume);

        var variation = (byte)((_inflowVisualSerial * 73 + (int)(normalizedX * 127f)) & 255);
        if (_inflowVisualSerial % 5 == 0 || downwardSpeed >= 110f)
        {
            var surface = SurfaceYAt(x);
            var stainX = Math.Clamp(x + ((variation & 7) - 3f) * 1.4f, Left + 8f, Right - 8f);
            var stainY = Math.Clamp(
                surface - 14f - ((variation >> 3) & 7) * 2.4f,
                Top + 8f,
                Bottom - 18f);
            AddOrRefreshInteriorStain(new BasinInteriorStain(
                stainX,
                stainY,
                2f + (variation & 1) * 2f,
                8f + ((variation >> 4) & 3) * 4f,
                1f,
                0f,
                variation,
                false));
        }

        var nearSide = normalizedX < 0.14f || normalizedX > 0.86f;
        if (nearSide || _sloshAmplitude > 0.72f && _inflowVisualSerial % 7 == 0)
        {
            var leftSide = normalizedX < 0.5f;
            var sideX = leftSide ? Left + 7f : Right - 7f;
            var sideY = Math.Clamp(SurfaceYAt(sideX) - 4f, Top + 10f, Bottom - 20f);
            AddOrRefreshInteriorStain(new BasinInteriorStain(
                sideX,
                sideY,
                4f,
                7f + ((variation >> 5) & 3) * 2f,
                1f,
                0f,
                (byte)(variation ^ 0x5A),
                true));
        }
    }

    private void RegisterPipeStain(float x, float downwardSpeed, float fluidVolume)
    {
        for (var i = 0; i < _pipeStains.Count; i++)
        {
            var stain = _pipeStains[i];
            if (MathF.Abs(stain.X - x) > 18f) continue;
            _pipeStains[i] = stain with
            {
                Amount = MathF.Min(1f, stain.Amount + 0.10f + fluidVolume / MathF.Max(1f, FluidCellVolume) * 0.025f),
                Wetness = 1f,
                Length = MathF.Min(20f, MathF.Max(stain.Length, 5f) + downwardSpeed * 0.006f)
            };
            return;
        }
        if (_pipeStains.Count >= 8) _pipeStains.RemoveAt(0);
        _pipeStains.Add(new BasinPipeStain(
            x,
            Math.Clamp(0.20f + fluidVolume / MathF.Max(1f, FluidCellVolume) * 0.035f, 0.2f, 0.65f),
            1f,
            Math.Clamp(5f + downwardSpeed * 0.018f, 5f, 16f),
            (byte)(_inflowVisualSerial * 47)));
    }

    private void RegisterFrontOverflowStain(float downwardSpeed, float displacedVolume)
    {
        var hash = unchecked((uint)(_overflowSideSerial * 747796405 + 2891336453));
        hash ^= hash >> 16;
        var variation = (byte)(hash >> 24);
        var width = 2.5f + ((hash >> 8) & 7) * 0.65f;
        var length = Math.Clamp(
            17f + downwardSpeed * 0.045f +
            displacedVolume / MathF.Max(FluidCellVolume, 0.001f) * 2.25f,
            20f,
            64f);
        var maximumLength = MathF.Max(Height + 190f, 760f - Top);
        if (_frontOverflowStains.Count < MaximumFrontOverflowStains)
        {
            var span = MathF.Max(24f, Width - 96f);
            var x = Left + 48f + (hash & 0xFFFF) / 65535f * span;
            var nearestIndex = -1;
            var nearestDistance = float.MaxValue;
            for (var attempt = 0; attempt < 7; attempt++)
            {
                nearestIndex = -1;
                nearestDistance = float.MaxValue;
                for (var index = 0; index < _frontOverflowStains.Count; index++)
                {
                    var distance = MathF.Abs(_frontOverflowStains[index].X - x);
                    if (distance >= nearestDistance) continue;
                    nearestDistance = distance;
                    nearestIndex = index;
                }
                if (nearestDistance >= 19f) break;
                hash = unchecked(hash * 1664525u + 1013904223u);
                x = Left + 48f + (hash & 0xFFFF) / 65535f * span;
            }
            if (nearestIndex < 0 || nearestDistance >= 14f)
            {
                _frontOverflowStains.Add(new BasinFrontOverflowStain(
                    x, width, length, 1f, variation));
                return;
            }
        }
        var selected = (int)((hash >> 12) % (uint)_frontOverflowStains.Count);
        var existing = _frontOverflowStains[selected];
        var widthChange = ((hash >> 20) & 3) switch
        {
            0 => -0.45f,
            3 => 0.8f,
            _ => 0.2f
        };
        _frontOverflowStains[selected] = existing with
        {
            Width = Math.Clamp(
                MathF.Max(existing.Width, width) + widthChange,
                2.5f,
                9.5f),
            Length = MathF.Min(maximumLength,
                MathF.Max(existing.Length, length) +
                29f + (hash & 15) * 1.4f +
                displacedVolume / MathF.Max(FluidCellVolume, 0.001f)),
            Wetness = MathF.Min(1f, existing.Wetness + 0.24f)
        };
    }

    public BasinPipeStain? PipeStainNear(float x)
    {
        BasinPipeStain? closest = null;
        var closestDistance = 18f;
        for (var i = 0; i < _pipeStains.Count; i++)
        {
            var distance = MathF.Abs(_pipeStains[i].X - x);
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closest = _pipeStains[i];
        }
        return closest;
    }

    private void AddOrRefreshInteriorStain(BasinInteriorStain incoming)
    {
        var searchStart = Math.Max(0, _interiorStains.Count - 12);
        for (var i = _interiorStains.Count - 1; i >= searchStart; i--)
        {
            var existing = _interiorStains[i];
            if (existing.IsSide != incoming.IsSide ||
                MathF.Abs(existing.X - incoming.X) > 7f ||
                MathF.Abs(existing.Y - incoming.Y) > 10f)
                continue;
            _interiorStains[i] = existing with
            {
                Length = MathF.Min(42f, MathF.Max(existing.Length, incoming.Length) + 2f),
                Wetness = 1f,
                Age = 0f
            };
            return;
        }
        if (_interiorStains.Count >= MaximumInteriorStains)
            _interiorStains.RemoveAt(0);
        _interiorStains.Add(incoming);
    }

    private void StepInteriorEffects(float dt)
    {
        if (_sloshAmplitude > 0f)
        {
            _sloshPhase = (_sloshPhase + _sloshDirection * dt * (3.2f + _sloshAmplitude * 1.4f)) %
                          MathF.Tau;
            _sloshAmplitude *= MathF.Exp(-dt * 0.95f);
            if (_sloshAmplitude < 0.016f) _sloshAmplitude = 0f;
            FluidVisualRevision++;
        }

        for (var i = 0; i < _interiorStains.Count; i++)
        {
            var stain = _interiorStains[i];
            var nextWetness = MathF.Max(0f, stain.Wetness - dt * 0.075f);
            var nextLength = MathF.Min(
                stain.IsSide ? 48f : 38f,
                stain.Length + dt * (nextWetness > 0.2f ? 5.5f : 0.7f));
            _interiorStains[i] = stain with
            {
                Length = nextLength,
                Wetness = nextWetness,
                Age = stain.Age + dt
            };
        }
        for (var i = 0; i < _frontOverflowStains.Count; i++)
        {
            var stain = _frontOverflowStains[i];
            _frontOverflowStains[i] = stain with
            {
                Wetness = MathF.Max(0.14f, stain.Wetness - dt * 0.022f),
                Length = MathF.Min(MathF.Max(Height + 190f, 760f - Top),
                    stain.Length + dt *
                    (24f + stain.Wetness * 44f + stain.Variation % 5 * 2.5f))
            };
        }
        for (var i = 0; i < _pipeStains.Count; i++)
        {
            var stain = _pipeStains[i];
            _pipeStains[i] = stain with
            {
                Wetness = MathF.Max(0.08f, stain.Wetness - dt * 0.045f),
                Length = MathF.Min(24f, stain.Length + dt * stain.Wetness * 0.8f)
            };
        }
    }

    private void StepFluidCells()
    {
        RebuildColumnMass();
        BuildHydrostaticTarget();
        var moved = 0;
        var donor = 0;
        var receiver = 0;

        // Liquid has no angle of repose. A height-field pressure pass moves cells
        // from columns above the hydrostatic target to columns below it. Moving up
        // to half the fluid per tick makes a newly delivered mound settle in at
        // most a few 60 Hz frames, without scanning once the surface is level.
        var transferBudget = Math.Min(_fluidCellCount, Math.Max(64, (_fluidCellCount + 1) / 2));
        while (moved < transferBudget)
        {
            while (donor < FluidGridWidth && _columnMass[donor] <= _targetColumnMass[donor])
                donor++;
            while (receiver < FluidGridWidth && _columnMass[receiver] >= _targetColumnMass[receiver])
                receiver++;
            if (donor >= FluidGridWidth || receiver >= FluidGridWidth) break;

            var sourceRow = FirstOccupiedRow(donor);
            var destinationRow = DepositRow(receiver);
            if (sourceRow >= FluidGridHeight || destinationRow < 0) break;
            _cells[Index(donor, sourceRow)] = false;
            _cells[Index(receiver, destinationRow)] = true;
            _columnMass[donor]--;
            _columnMass[receiver]++;
            moved++;
        }

        if (moved == 0)
        {
            if (++_settledTicks >= 2) _fluidActive = false;
            return;
        }
        _settledTicks = 0;
        FluidVisualRevision++;
        RebuildSurfaceAndProjection(
            Left + (_liquidCenterColumn + 0.5f) * FluidCellWidth);
    }

    private void BuildHydrostaticTarget()
    {
        var level = _fluidCellCount / FluidGridWidth;
        var remainder = _fluidCellCount % FluidGridWidth;
        Array.Fill(_targetColumnMass, level);
        if (remainder == 0) return;

        if (level == 0)
        {
            // At sub-pixel depth, keep one connected puddle instead of drawing a
            // dotted row across the whole floor.
            var start = Math.Clamp(
                (int)MathF.Round(_liquidCenterColumn - (remainder - 1) * 0.5f),
                0,
                FluidGridWidth - remainder);
            for (var x = start; x < start + remainder; x++)
                _targetColumnMass[x] = 1;
            return;
        }

        // Whole cells cannot express a fractional top row. Place them in broad,
        // deterministic low-frequency bands instead of an evenly spaced checker.
        // Pressure still constrains every column to within one cell of every
        // other; only the sub-cell surface character changes.
        for (var i = 0; i < remainder; i++)
            _targetColumnMass[NaturalSurfaceOrder[i]]++;
    }

    private void SynchronizeCellsToVolume(float focusX)
    {
        var totalCells = FluidGridWidth * FluidGridHeight;
        var targetCells = CurrentFluidVolume >= FluidCapacity - 0.001f
            ? totalCells
            : Math.Clamp((int)MathF.Floor(CurrentFluidVolume / FluidCellVolume), 0, totalCells);
        var addOrdinal = 0;
        while (_fluidCellCount < targetCells && AddCellNear(focusX, addOrdinal++)) _fluidCellCount++;
        while (_fluidCellCount > targetCells && RemoveSurfaceCellNear(focusX)) _fluidCellCount--;
        _fluidActive = _fluidCellCount > 0;
        _settledTicks = 0;
        FluidVisualRevision++;
        RebuildSurfaceAndProjection(focusX);
    }

    private bool AddCellNear(float x, int ordinal)
    {
        const int depositHalfWidth = 3;
        var center = GridColumnAt(x);
        for (var attempt = 0; attempt < FluidGridWidth; attempt++)
        {
            var serial = ordinal + attempt;
            var radius = (serial / 2) % (depositHalfWidth + 1);
            var direction = (serial & 1) == 0 ? 1 : -1;
            var column = center + radius * direction;
            if ((uint)column >= FluidGridWidth) continue;
            var row = DepositRow(column);
            if (row < 0) continue;
            _cells[Index(column, row)] = true;
            return true;
        }
        for (var column = 0; column < FluidGridWidth; column++)
        {
            var row = DepositRow(column);
            if (row < 0) continue;
            _cells[Index(column, row)] = true;
            return true;
        }
        return false;
    }

    private bool RemoveSurfaceCellNear(float x)
    {
        var center = GridColumnAt(x);
        for (var radius = 0; radius < FluidGridWidth; radius++)
        for (var side = 0; side < (radius == 0 ? 1 : 2); side++)
        {
            var column = center + (side == 0 ? radius : -radius);
            if ((uint)column >= FluidGridWidth) continue;
            var row = FirstOccupiedRow(column);
            if (row >= FluidGridHeight) continue;
            _cells[Index(column, row)] = false;
            return true;
        }
        return false;
    }

    private int DepositRow(int column)
    {
        var surface = FirstOccupiedRow(column);
        if (surface >= FluidGridHeight) return FluidGridHeight - 1;
        return surface - 1;
    }

    private int FirstOccupiedRow(int column)
    {
        for (var row = 0; row < FluidGridHeight; row++)
            if (_cells[Index(column, row)]) return row;
        return FluidGridHeight;
    }

    private void RebuildColumnMass()
    {
        Array.Clear(_columnMass);
        for (var y = 0; y < FluidGridHeight; y++)
        for (var x = 0; x < FluidGridWidth; x++)
            if (_cells[Index(x, y)]) _columnMass[x]++;
    }

    private void RebuildSurfaceAndProjection(float focusX)
    {
        Array.Fill(_surfaceRows, FluidGridHeight);
        Array.Clear(_columnMass);
        Array.Clear(_heights);
        var legacyColumnWidth = Width / ColumnCount;
        for (var x = 0; x < FluidGridWidth; x++)
        for (var y = 0; y < FluidGridHeight; y++)
        {
            if (!_cells[Index(x, y)]) continue;
            _columnMass[x]++;
            if (y < _surfaceRows[x]) _surfaceRows[x] = y;
            var worldX = Left + (x + 0.5f) * FluidCellWidth;
            var legacy = Math.Clamp((int)((worldX - Left) / legacyColumnWidth), 0, ColumnCount - 1);
            _heights[legacy] += FluidCellVolume / legacyColumnWidth;
        }

        var represented = _fluidCellCount * FluidCellVolume;
        var remainder = Math.Clamp(CurrentFluidVolume - represented, 0f, FluidCellVolume);
        _fractionalFill = remainder / FluidCellVolume;
        _fractionalColumn = _fractionalFill > 0.0001f ? LowestColumnNear(focusX) : -1;
        _fractionalRow = _fractionalColumn >= 0 ? DepositRow(_fractionalColumn) : -1;
        if (_fractionalColumn >= 0 && _fractionalRow >= 0)
        {
            var fractionalWorldX = Left + (_fractionalColumn + 0.5f) * FluidCellWidth;
            var legacy = Math.Clamp((int)(((fractionalWorldX - Left) / Width) * ColumnCount), 0, ColumnCount - 1);
            _heights[legacy] += remainder / legacyColumnWidth;
        }
        FluidVisualRevision++;
    }

    private int LowestColumnNear(float focusX)
    {
        var center = GridColumnAt(focusX);
        var lowestMass = _columnMass.Min();
        for (var radius = 0; radius < FluidGridWidth; radius++)
        {
            var right = center + radius;
            if (right < FluidGridWidth && _columnMass[right] == lowestMass) return right;
            var left = center - radius;
            if (radius > 0 && left >= 0 && _columnMass[left] == lowestMass) return left;
        }
        return center;
    }

    private int GridColumnAt(float x)
    {
        var normalized = Math.Clamp((x - Left) / Width, 0f, 0.999999f);
        return Math.Clamp((int)(normalized * FluidGridWidth), 0, FluidGridWidth - 1);
    }

    private static int Index(int x, int y) => y * FluidGridWidth + x;

    private static int[] BuildNaturalSurfaceOrder()
    {
        var columns = Enumerable.Range(0, FluidGridWidth).ToArray();
        Array.Sort(columns, (left, right) =>
        {
            var scoreOrder = NaturalSurfaceScore(right).CompareTo(NaturalSurfaceScore(left));
            return scoreOrder != 0 ? scoreOrder : left.CompareTo(right);
        });
        return columns;
    }

    private static float NaturalSurfaceScore(int x)
    {
        var phase = MathF.Tau * (x + 0.5f) / FluidGridWidth;
        return MathF.Sin(phase * 2f + 0.37f) * 0.68f +
               MathF.Sin(phase * 5f + 1.91f) * 0.24f +
               MathF.Sin(phase * 9f + 0.73f) * 0.08f;
    }
}

public readonly record struct BasinInteriorStain(
    float X,
    float Y,
    float Width,
    float Length,
    float Wetness,
    float Age,
    byte Variation,
    bool IsSide);

public struct BasinSuspendedDrop
{
    public float X { get; internal set; }
    public float Y { get; internal set; }
    public float VelocityX { get; internal set; }
    public float VelocityY { get; internal set; }
    public float Radius { get; internal set; }
    public float RemainingVolume { get; internal set; }
    public float InitialVolume { get; internal set; }
    public float RemainingNutrition { get; internal set; }
    public float Age { get; internal set; }
    public byte Variation { get; internal set; }
}

public struct BasinSurfaceSplash
{
    public float X { get; internal set; }
    public float Y { get; internal set; }
    public float VelocityX { get; internal set; }
    public float VelocityY { get; internal set; }
    public float Radius { get; internal set; }
    public float Age { get; internal set; }
    public float Lifetime { get; internal set; }
    public byte Variation { get; internal set; }
}

public struct BasinSurfaceRipple
{
    public float X { get; internal set; }
    public float Age { get; internal set; }
    public float Lifetime { get; internal set; }
    public float Strength { get; internal set; }
    public byte Variation { get; internal set; }
}

public readonly record struct BasinPipeStain(
    float X,
    float Amount,
    float Wetness,
    float Length,
    byte Variation);

public readonly record struct BasinFrontOverflowStain(
    float X,
    float Width,
    float Length,
    float Wetness,
    byte Variation);
