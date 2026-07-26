using System.Numerics;

namespace BlobForge.World;

public enum BloodShipmentStage : byte
{
    RaisingBasin,
    DeployingFunnel,
    TruckArriving,
    LoadingTruck,
    TruckDeparting,
    FinalizingPayout,
    Complete
}

public readonly record struct ShipmentBloodPixel(
    Vector2 Position,
    float Volume,
    byte Variation,
    float Radius);

/// <summary>
/// A bounded, conserved end-of-day transfer. Every visible red pixel owns a
/// measured portion of blood removed from the basin, and that same portion is
/// credited to the current truck only when the pixel reaches its inlet.
/// </summary>
public sealed class BloodShipmentSequence
{
    public const int MaximumParticles = 240;
    public const int MaximumTrucks = 5;
    public const float TargetBasinTop = 130f;
    public const float SpriteScale = 2f;

    private const float RaiseDuration = 0.82f;
    private const float FunnelDeployDuration = 0.72f;
    private const float TruckSpeed = 1500f;
    private const float ParticleSpeed = 520f;
    private const float ParticleSpawnRate = 80f;
    private const float EarningsResponse = 28f;
    private const float PayoutFinalizeDuration = 0.62f;
    private const float TruckSpriteWidth = 128f * SpriteScale;

    private readonly BloodBasin _basin;
    private readonly List<ShipmentParticleState> _particles =
        new(MaximumParticles);
    private readonly List<ShipmentBloodPixel> _renderPixels =
        new(MaximumParticles);
    private readonly float _particleQuantum;
    private readonly float _standardTruckCapacity;
    private readonly int _plannedTruckCount;
    private float _stageTime;
    private float _spawnAccumulator;
    private float _emittedVolume;
    private float _loadedVolume;
    private float _currentTruckLoaded;
    private float _currentTruckCapacity;
    private float _truckX;
    private float _displayedBloodEarnings;
    private float _displayedTotalEarnings;
    private int _serial;

    public BloodShipmentSequence(
        BloodBasin basin,
        decimal bloodRatePerGallon = 0m,
        int processedBlobs = 0,
        decimal processedRate = 0m)
    {
        _basin = basin;
        InitialVolume = basin.PrepareForShipment();
        InitialGallons = basin.EstimatedStoredGallons;
        InitialLiters = basin.EstimatedStoredLiters;
        BloodRatePerGallon = Math.Max(0m, bloodRatePerGallon);
        ProcessedBonus = Math.Max(0, processedBlobs) * Math.Max(0m, processedRate);
        ProjectedBloodPayout = decimal.Round(
            (decimal)InitialGallons * BloodRatePerGallon,
            2,
            MidpointRounding.AwayFromZero);
        ProjectedTotalPayout = ProjectedBloodPayout + ProcessedBonus;

        var fill = InitialVolume / MathF.Max(0.001f, basin.FluidCapacity);
        var particleCount = InitialVolume <= 0f
            ? 0
            : Math.Clamp((int)MathF.Ceiling(fill * MaximumParticles), 18, MaximumParticles);
        _particleQuantum = particleCount > 0 ? InitialVolume / particleCount : 0f;
        _plannedTruckCount = InitialVolume <= 0f
            ? 0
            : Math.Clamp((int)MathF.Ceiling(fill * MaximumTrucks), 1, MaximumTrucks);
        _standardTruckCapacity = _plannedTruckCount > 0
            ? InitialVolume / _plannedTruckCount
            : 0f;
        _currentTruckCapacity = MathF.Min(_standardTruckCapacity, InitialVolume);
        _truckX = -TruckSpriteWidth - 24f;
        Stage = BloodShipmentStage.RaisingBasin;
    }

    public BloodShipmentStage Stage { get; private set; }
    public bool Active => Stage != BloodShipmentStage.Complete;
    public bool Complete => Stage == BloodShipmentStage.Complete;
    public float InitialVolume { get; }
    public float InitialGallons { get; }
    public float InitialLiters { get; }
    public decimal BloodRatePerGallon { get; }
    public decimal ProjectedBloodPayout { get; }
    public decimal ProcessedBonus { get; }
    public decimal ProjectedTotalPayout { get; }
    public decimal LoadedBloodPayout => decimal.Round(
        ProjectedBloodPayout * (decimal)LoadedFraction,
        2,
        MidpointRounding.AwayFromZero);
    public decimal DisplayedBloodEarnings => decimal.Round(
        (decimal)_displayedBloodEarnings,
        2,
        MidpointRounding.AwayFromZero);
    public decimal DisplayedTotalEarnings => decimal.Round(
        (decimal)_displayedTotalEarnings,
        2,
        MidpointRounding.AwayFromZero);
    public float LoadedFraction => InitialVolume <= 0f
        ? 1f
        : Math.Clamp(_loadedVolume / InitialVolume, 0f, 1f);
    public float ShippedVolume => _loadedVolume;
    public int PlannedTruckCount => _plannedTruckCount;
    public int DepartedTruckCount { get; private set; }
    public IReadOnlyList<ShipmentBloodPixel> Pixels => _renderPixels;
    public float CurrentTruckFill01 => _currentTruckCapacity <= 0f
        ? 0f
        : Math.Clamp(_currentTruckLoaded / _currentTruckCapacity, 0f, 1f);
    public int FunnelFrame => Stage switch
    {
        BloodShipmentStage.RaisingBasin => 0,
        BloodShipmentStage.DeployingFunnel => _stageTime switch
        {
            < FunnelDeployDuration / 3f => 0,
            < FunnelDeployDuration * 2f / 3f => 1,
            _ => 2
        },
        _ => 2
    };
    public int TruckFrame => ((int)(_stageTime * 10f) & 1);
    public float BasinOffsetY
    {
        get
        {
            var progress = Stage == BloodShipmentStage.RaisingBasin
                ? SmoothStep(Math.Clamp(_stageTime / RaiseDuration, 0f, 1f))
                : 1f;
            return (TargetBasinTop - _basin.Top) * progress;
        }
    }

    public Vector2 FunnelOrigin =>
        new(_basin.Left + _basin.Width * 0.5f - 96f,
            _basin.Bottom + BasinOffsetY - 5f);

    public Vector2 FunnelOutlet => FunnelOrigin + new Vector2(12f, 328f);
    public Vector2 TruckPosition => new(_truckX, FunnelOutlet.Y - 56f);
    public int InFlightParticleCount => _particles.Count;

    public void Update(float dt)
    {
        if (Complete || !float.IsFinite(dt) || dt <= 0f) return;
        dt = Math.Clamp(dt, 0f, 1f / 20f);
        _stageTime += dt;

        switch (Stage)
        {
            case BloodShipmentStage.RaisingBasin:
                if (_stageTime >= RaiseDuration)
                    EnterStage(BloodShipmentStage.DeployingFunnel);
                break;

            case BloodShipmentStage.DeployingFunnel:
                if (_stageTime >= FunnelDeployDuration)
                {
                    if (InitialVolume > 0f)
                        EnterStage(BloodShipmentStage.TruckArriving);
                    else
                        Finish();
                }
                break;

            case BloodShipmentStage.TruckArriving:
                _truckX = MathF.Min(DockedTruckX, _truckX + TruckSpeed * dt);
                if (_truckX >= DockedTruckX - 0.01f)
                {
                    _truckX = DockedTruckX;
                    EnterStage(BloodShipmentStage.LoadingTruck);
                }
                break;

            case BloodShipmentStage.LoadingTruck:
                SpawnShipmentPixels(dt);
                StepParticles(dt);
                if (CurrentTruckIsReadyToDepart())
                    EnterStage(BloodShipmentStage.TruckDeparting);
                break;

            case BloodShipmentStage.TruckDeparting:
                StepParticles(dt);
                _truckX += TruckSpeed * dt;
                if (_truckX <= 1280f + 32f) break;
                DepartedTruckCount++;
                if (_loadedVolume + 0.001f >= InitialVolume ||
                    (_basin.StoredVolume <= 0.001f &&
                     _particles.Count == 0 &&
                     _emittedVolume >= InitialVolume - 0.05f))
                {
                    Finish();
                    break;
                }
                _currentTruckLoaded = 0f;
                _currentTruckCapacity = MathF.Min(
                    _standardTruckCapacity,
                    InitialVolume - _loadedVolume);
                _truckX = -TruckSpriteWidth - 24f;
                EnterStage(BloodShipmentStage.TruckArriving);
                break;

            case BloodShipmentStage.FinalizingPayout:
            {
                _displayedBloodEarnings = (float)ProjectedBloodPayout;
                var progress = SmoothStep(Math.Clamp(
                    _stageTime / PayoutFinalizeDuration,
                    0f,
                    1f));
                var bloodPayout = (float)ProjectedBloodPayout;
                _displayedTotalEarnings = bloodPayout +
                    ((float)ProjectedTotalPayout - bloodPayout) * progress;
                if (_stageTime >= PayoutFinalizeDuration)
                {
                    _displayedTotalEarnings = (float)ProjectedTotalPayout;
                    EnterStage(BloodShipmentStage.Complete);
                }
                break;
            }
        }

        UpdateDisplayedEarnings(dt);
        RebuildRenderPixels();
    }

    private float DockedTruckX => FunnelOutlet.X - 12f;

    private void SpawnShipmentPixels(float dt)
    {
        var remainingToEmit = InitialVolume - _emittedVolume;
        var remainingTruckCapacity = _currentTruckCapacity -
            _currentTruckLoaded - InFlightVolume();
        if (remainingToEmit <= 0.0001f || remainingTruckCapacity <= 0.0001f) return;

        _spawnAccumulator += dt * ParticleSpawnRate;
        while (_spawnAccumulator >= 1f && _particles.Count < MaximumParticles)
        {
            _spawnAccumulator -= 1f;
            remainingToEmit = InitialVolume - _emittedVolume;
            remainingTruckCapacity = _currentTruckCapacity -
                _currentTruckLoaded - InFlightVolume();
            var requested = MathF.Min(_particleQuantum,
                MathF.Min(remainingToEmit, remainingTruckCapacity));
            if (requested <= 0.0001f) break;
            var volume = _basin.ExtractForShipment(requested);
            if (volume <= 0f) break;

            var variation = (byte)(_serial++ & 7);
            _particles.Add(new ShipmentParticleState
            {
                Distance = -variation * 1.8f,
                Volume = volume,
                Variation = variation,
                Radius = 2f + variation % 3
            });
            _emittedVolume += volume;
        }
    }

    private void StepParticles(float dt)
    {
        if (_particles.Count == 0) return;
        var pathLength = ShipmentPathLength();
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Distance += ParticleSpeed * dt * (0.92f + particle.Variation * 0.018f);
            if (particle.Distance >= pathLength)
            {
                _currentTruckLoaded += particle.Volume;
                _loadedVolume += particle.Volume;
                _particles.RemoveAt(i);
                continue;
            }
            _particles[i] = particle;
        }
    }

    private bool CurrentTruckIsReadyToDepart()
    {
        if (_particles.Count == 0 &&
            _basin.StoredVolume <= 0.001f &&
            _emittedVolume >= InitialVolume - 0.05f)
        {
            // The final IEEE-754 remainder can be smaller than the normal packet
            // quantum. Treat the physically empty basin as authoritative and
            // close the last truck at its exact received amount.
            _currentTruckCapacity = _currentTruckLoaded;
            return true;
        }

        var assigned = _currentTruckLoaded + InFlightVolume();
        var noMoreForTruck = assigned >= _currentTruckCapacity - 0.001f ||
                             _emittedVolume >= InitialVolume - 0.001f;
        return noMoreForTruck && _particles.Count == 0 &&
               _currentTruckLoaded >= _currentTruckCapacity - 0.001f;
    }

    private float InFlightVolume()
    {
        var volume = 0f;
        for (var i = 0; i < _particles.Count; i++) volume += _particles[i].Volume;
        return volume;
    }

    private void RebuildRenderPixels()
    {
        _renderPixels.Clear();
        for (var i = 0; i < _particles.Count; i++)
        {
            var particle = _particles[i];
            var position = PositionAlongPath(MathF.Max(0f, particle.Distance));
            var jitter = ((particle.Variation * 5 + i * 3) % 7 - 3) * 0.55f;
            position.X += jitter;
            _renderPixels.Add(new ShipmentBloodPixel(
                position,
                particle.Volume,
                particle.Variation,
                particle.Radius));
        }
    }

    private Vector2 PositionAlongPath(float distance)
    {
        Span<Vector2> points =
        [
            FunnelOrigin + new Vector2(96f, 76f),
            FunnelOrigin + new Vector2(98f, 238f),
            FunnelOrigin + new Vector2(82f, 258f),
            FunnelOrigin + new Vector2(66f, 278f),
            FunnelOrigin + new Vector2(50f, 298f),
            FunnelOrigin + new Vector2(34f, 318f),
            FunnelOutlet
        ];
        for (var i = 1; i < points.Length; i++)
        {
            var segment = points[i] - points[i - 1];
            var length = segment.Length();
            if (distance <= length)
                return points[i - 1] + segment * (distance / MathF.Max(0.001f, length));
            distance -= length;
        }
        return points[^1];
    }

    private float ShipmentPathLength()
    {
        Span<Vector2> points =
        [
            FunnelOrigin + new Vector2(96f, 76f),
            FunnelOrigin + new Vector2(98f, 238f),
            FunnelOrigin + new Vector2(82f, 258f),
            FunnelOrigin + new Vector2(66f, 278f),
            FunnelOrigin + new Vector2(50f, 298f),
            FunnelOrigin + new Vector2(34f, 318f),
            FunnelOutlet
        ];
        var length = 0f;
        for (var i = 1; i < points.Length; i++) length += Vector2.Distance(points[i - 1], points[i]);
        return length;
    }

    private void EnterStage(BloodShipmentStage stage)
    {
        Stage = stage;
        _stageTime = 0f;
    }

    private void Finish()
    {
        _loadedVolume = InitialVolume;
        _currentTruckLoaded = _currentTruckCapacity;
        _displayedBloodEarnings = (float)ProjectedBloodPayout;
        _displayedTotalEarnings = _displayedBloodEarnings;
        EnterStage(BloodShipmentStage.FinalizingPayout);
        RebuildRenderPixels();
    }

    private void UpdateDisplayedEarnings(float dt)
    {
        if (Stage is BloodShipmentStage.FinalizingPayout or BloodShipmentStage.Complete)
            return;
        var target = (float)ProjectedBloodPayout * LoadedFraction;
        var response = 1f - MathF.Exp(-EarningsResponse * dt);
        _displayedBloodEarnings += (target - _displayedBloodEarnings) * response;
        if (MathF.Abs(target - _displayedBloodEarnings) < 0.005f)
            _displayedBloodEarnings = target;
        _displayedTotalEarnings = _displayedBloodEarnings;
    }

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);

    private struct ShipmentParticleState
    {
        public float Distance;
        public float Volume;
        public byte Variation;
        public float Radius;
    }
}
