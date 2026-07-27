using System.Numerics;
using System.Diagnostics;
using BlobForge.World;

namespace BlobForge.Physics;

public enum GranularKind : byte
{
    Tissue,
    Blood,
    Acid
}

public enum GranularAppearance : byte
{
    Gore,
    BlobMint,
    BlobTeal
}

public struct GranularParticle
{
    public Vector2 Position;
    public Vector2 PreviousPosition;
    public float Radius;
    public float Lifetime;
    public GranularKind Kind;
    public GranularAppearance Appearance;
    public byte RestFrames;
    public bool SplatterOnImpact;
    public bool BypassConveyors;
    // Enclosed drain transit remains visible, but it must not be pushed back
    // into the collector by generic pile/blob contacts.
    public bool InContinuousDrain;
    public float CorrosionCooldown;
    // Foreground depth-fall is a support/pile behavior. Airborne wound spray
    // and freshly granulated detached tissue must stay in normal 2D physics.
    public byte ForegroundSupportFrames;
}

public struct ForegroundGranularSpill
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Lifetime;
    public GranularKind Kind;
    public GranularAppearance Appearance;
    public byte Variation;
}

public sealed class GranularMaterialSystem
{
    public const int ParticleCapacity = 5000;
    public const int ForegroundSpillCapacity = 256;
    public const int BloodSpawnBudgetPerStep = 28;
    public const int TissueSpawnBudgetPerStep = 256;
    private const float GranularHashCellSize = 7f;
    private const float BlobHashCellSize = 34f;
    private const float DensePileCellWidth = 32f;
    private const float DensePileCellHeight = 24f;
    private const float DensePileMaximumSpeed = 150f;
    private const int DensePileThreshold = 20;
    private const int DensePileScanStride = 6;
    private const int DensePileSpillBaseBudget = 8;
    private const int DensePileSpillMaximumBudget = 32;
    private const int DensePileMaximumColumns = 64;
    private const int DensePileMaximumRows = 40;

    private readonly Dictionary<long, List<int>> _granularBuckets = new();
    private readonly Dictionary<long, List<BlobParticleHandle>> _blobBuckets = new();
    private readonly Dictionary<long, List<int>> _blobBodyBuckets = new();
    private readonly List<List<int>> _activeGranularBuckets = new(256);
    private readonly List<List<BlobParticleHandle>> _activeBlobBuckets = new(64);
    private readonly List<List<int>> _activeBlobBodyBuckets = new(64);
    private readonly List<ContainmentContourCache> _blobContainmentContours = new();
    private readonly int[] _densePileCounts =
        new int[DensePileMaximumColumns * DensePileMaximumRows];
    private int _bloodBudgetRemaining;
    private int _tissueBudgetRemaining;
    private uint _randomState = 0xA341316Cu;
    private int _stepSerial;

    public List<GranularParticle> Particles { get; } = new(ParticleCapacity);
    public List<ForegroundGranularSpill> ForegroundSpills { get; } =
        new(ForegroundSpillCapacity);
    public int BloodCount => Particles.Count(p => p.Kind == GranularKind.Blood);
    public int TissuePixelCount => Particles.Count(p => p.Kind == GranularKind.Tissue);
    public int AcidCount => Particles.Count(p => p.Kind == GranularKind.Acid);
    public int SourceTissueConvertedTotal { get; private set; }
    public int ForegroundSpillConvertedTotal { get; private set; }
    public int ForegroundSpillExpiredTotal { get; private set; }
    public int ForegroundSpillCollectedTotal { get; private set; }
    public int ForegroundSpillReemittedTotal { get; private set; }
    public int SpawnedThisStep { get; private set; }
    public int BloodSpawnedThisStep { get; private set; }
    public int BloodSplatteredThisStep { get; private set; }
    public double LastBucketBuildMs { get; private set; }
    public double LastIntegrationMs { get; private set; }
    public double LastContactSolveMs { get; private set; }

    public void BeginStep()
    {
        SpawnedThisStep = 0;
        BloodSpawnedThisStep = 0;
        BloodSplatteredThisStep = 0;
        _bloodBudgetRemaining = BloodSpawnBudgetPerStep;
        _tissueBudgetRemaining = TissueSpawnBudgetPerStep;
    }

    public int EmitBlood(WoundEvent wound, float dt, int requestedCount, float speedScale)
    {
        var count = Math.Min(Math.Max(0, requestedCount), _bloodBudgetRemaining);
        if (count == 0) return 0;
        var normal = wound.Normal.LengthSquared() < 0.001f ? Vector2.UnitY : Vector2.Normalize(wound.Normal);
        var tangent = new Vector2(-normal.Y, normal.X);
        for (var i = 0; i < count; i++)
        {
            var velocity = normal * NextFloat(10f, 48f) * speedScale +
                           tangent * NextFloat(-38f, 38f) * speedScale +
                           new Vector2(0f, NextFloat(12f, 48f));
            velocity.Y = MathF.Max(velocity.Y, -72f);
            var position = wound.Position + RandomUnit() * NextFloat(0f, 3.5f);
            Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - velocity * dt,
                Radius = NextFloat(1.6f, 2.7f),
                Lifetime = NextFloat(12f, 22f),
                Kind = GranularKind.Blood,
                SplatterOnImpact = Next01() < 0.30f,
                BypassConveyors = Next01() < 0.14f
            });
        }
        _bloodBudgetRemaining -= count;
        BloodSpawnedThisStep += count;
        return count;
    }

    public bool TryEmitBloodDrip(BloodDripEmission emission, float dt)
    {
        if (_bloodBudgetRemaining <= 0 || Particles.Count >= ParticleCapacity) return false;
        Add(new GranularParticle
        {
            Position = emission.Position,
            PreviousPosition = emission.Position - emission.Velocity * dt,
            Radius = emission.Radius,
            Lifetime = 8f + (emission.Variation & 7) * 0.55f,
            Kind = GranularKind.Blood,
            SplatterOnImpact = false,
            BypassConveyors = (emission.Variation & 7) == 0
        });
        _bloodBudgetRemaining--;
        BloodSpawnedThisStep++;
        return true;
    }

    public bool TryEmitDetached(SoftBody detached, float dt)
    {
        var requiredPixels = 0;
        foreach (var tissue in detached.Particles)
            requiredPixels += Math.Clamp((int)(tissue.Radius * 0.38f), 3, 6);
        if (requiredPixels > _tissueBudgetRemaining) return false;

        SourceTissueConvertedTotal += detached.Particles.Length;
        _tissueBudgetRemaining -= requiredPixels;
        foreach (var tissue in detached.Particles)
        {
            var baseVelocity = (tissue.Position - tissue.PreviousPosition) / dt;
            var pixelCount = Math.Clamp((int)(tissue.Radius * 0.38f), 3, 6);
            for (var i = 0; i < pixelCount; i++)
            {
                var offset = RandomUnit() * NextFloat(0f, tissue.Radius * 0.72f);
                var position = tissue.Position + offset;
                var velocity = baseVelocity * 0.72f + RandomUnit() * NextFloat(8f, 48f);
                Add(new GranularParticle
                {
                    Position = position,
                    PreviousPosition = position - velocity * dt,
                    Radius = NextFloat(2.1f, 3.4f),
                    Lifetime = NextFloat(26f, 44f),
                    Kind = GranularKind.Tissue,
                    Appearance = NextTissueAppearance(),
                    BypassConveyors = Next01() < 0.10f
                });
            }
        }
        return true;
    }

    public bool TryEmitDetachedParticle(SoftBody detached, int particleIndex, float dt)
    {
        if ((uint)particleIndex >= (uint)detached.Particles.Length || detached.IsConvertedParticle(particleIndex)) return false;
        var tissue = detached.Particles[particleIndex];
        var pixelCount = Math.Clamp((int)(tissue.Radius * 0.38f), 3, 6);
        if (pixelCount > _tissueBudgetRemaining) return false;

        SourceTissueConvertedTotal++;
        _tissueBudgetRemaining -= pixelCount;
        var baseVelocity = (tissue.Position - tissue.PreviousPosition) / dt;
        for (var i = 0; i < pixelCount; i++)
        {
            var offset = RandomUnit() * NextFloat(0f, tissue.Radius * 0.72f);
            var position = tissue.Position + offset;
            var velocity = baseVelocity * 0.72f + RandomUnit() * NextFloat(8f, 42f);
            Add(new GranularParticle
            {
                Position = position,
                PreviousPosition = position - velocity * dt,
                Radius = NextFloat(2.1f, 3.4f),
                Lifetime = NextFloat(26f, 44f),
                Kind = GranularKind.Tissue,
                Appearance = NextTissueAppearance(),
                BypassConveyors = Next01() < 0.10f
            });
        }
        return true;
    }

    public bool TryEmitDetachedPixel(SoftBody detached, int particleIndex, float dt)
    {
        if ((uint)particleIndex >= (uint)detached.Particles.Length ||
            detached.IsConvertedParticle(particleIndex) || _tissueBudgetRemaining <= 0) return false;
        var tissue = detached.Particles[particleIndex];
        var baseVelocity = (tissue.Position - tissue.PreviousPosition) / dt;
        var offset = RandomUnit() * NextFloat(0f, tissue.Radius * 0.78f);
        var position = tissue.Position + offset;
        var velocity = baseVelocity * 0.72f + RandomUnit() * NextFloat(7f, 36f);
        Add(new GranularParticle
        {
            Position = position,
            PreviousPosition = position - velocity * dt,
            Radius = NextFloat(2.1f, 3.4f),
            Lifetime = NextFloat(26f, 44f),
            Kind = GranularKind.Tissue,
            Appearance = NextTissueAppearance(),
            BypassConveyors = Next01() < 0.10f
        });
        _tissueBudgetRemaining--;
        return true;
    }

    public void RecordDetachedSourceConverted() => SourceTissueConvertedTotal++;

    public void Step(
        float dt,
        Vector2 gravity,
        DestructibleGrid grid,
        IReadOnlyList<SoftBody> bodies,
        IReadOnlyList<ConveyorBelt>? conveyors = null,
        HoldingChamber? holdingChamber = null,
        ProcessingLine? processingLine = null,
        PhysicalKnife? knife = null)
    {
        _stepSerial++;
        var worldWidth = grid.Columns * grid.CellSize;
        var worldHeight = grid.Rows * grid.CellSize;
        UpdateForegroundSpills(dt, worldWidth, worldHeight, processingLine);
        var bucketStart = Stopwatch.GetTimestamp();
        BuildBlobBuckets(bodies);
        var integrationStart = Stopwatch.GetTimestamp();
        LastBucketBuildMs = Stopwatch.GetElapsedTime(bucketStart, integrationStart).TotalMilliseconds;
        var dt2 = dt * dt;
        for (var i = Particles.Count - 1; i >= 0; i--)
        {
            var granular = Particles[i];
            granular.Lifetime -= dt;
            granular.CorrosionCooldown =
                MathF.Max(0f, granular.CorrosionCooldown - dt);
            if (granular.ForegroundSupportFrames > 0)
                granular.ForegroundSupportFrames--;
            if (granular.Lifetime <= 0f ||
                granular.Position.X < -64f || granular.Position.X > grid.Columns * grid.CellSize + 64f ||
                granular.Position.Y > grid.Rows * grid.CellSize + 96f)
            {
                RemoveParticleAtSwapBack(i);
                continue;
            }

            // Fully settled material is visually and physically static until
            // support changes or another active pixel pushes it. Recheck one
            // quarter of dormant pixels each 120 Hz step (every pixel at 30 Hz)
            // so destroyed terrain wakes them within 25 ms without spending the
            // full grid/blob collision cost on dense motionless pools.
            if (!granular.InContinuousDrain &&
                granular.RestFrames > 24 && ((i + _stepSerial) & 3) != 0)
            {
                Particles[i] = granular;
                continue;
            }

            var damping = granular.Kind is GranularKind.Blood or GranularKind.Acid
                ? 0.991f
                : 0.977f;
            var velocity = (granular.Position - granular.PreviousPosition) * damping;
            var incomingSpeed = velocity.Length() / dt;
            granular.PreviousPosition = granular.Position;
            granular.Position += velocity + gravity * dt2;

            if (granular.Kind == GranularKind.Blood &&
                knife?.ResolveBloodContact(ref granular, dt) == true &&
                granular.Lifetime <= 0f)
            {
                RemoveParticleAtSwapBack(i);
                continue;
            }

            var routedThroughContinuousDrain =
                granular.Kind != GranularKind.Acid &&
                processingLine?.RouteThroughContinuousEndDrain(ref granular, dt) == true;

            // Basin conversion must happen in the same granular integration pass.
            // Waiting for the next 120 Hz world pre-step allowed a fast droplet to
            // cross the tank and paint the structural floor underneath it first.
            if (granular.Kind != GranularKind.Acid &&
                processingLine?.TryCollectBasinInflow(ref granular, dt) == true)
            {
                RemoveParticleAtSwapBack(i);
                continue;
            }

            var proxy = new Particle
            {
                Position = granular.Position,
                PreviousPosition = granular.PreviousPosition,
                InverseMass = 1f,
                Radius = granular.Radius
            };
            var cartContainment = SurfaceContact.None;
            if (processingLine is not null)
            {
                cartContainment = processingLine.ResolveGranularCartOnly(ref proxy, dt);
                granular.Position = proxy.Position;
                granular.PreviousPosition = proxy.PreviousPosition;
            }
            var collision = routedThroughContinuousDrain
                ? default
                : grid.ResolveParticle(ref proxy, dt);
            granular.Position = proxy.Position;
            granular.PreviousPosition = proxy.PreviousPosition;
            var protectedByBasin = collision.Hit &&
                                   processingLine?.IsBasinProtectedFloor(collision.ContactPoint) == true;
            if (!protectedByBasin && ShouldSplatterOnGridImpact(granular, collision, incomingSpeed))
            {
                PaintGridSplatter(grid, collision, MathF.Max(incomingSpeed, collision.Impact));
                BloodSplatteredThisStep++;
                RemoveParticleAtSwapBack(i);
                continue;
            }
            // A visible pool of settled blood must keep feeding the surface
            // beneath it. Previously each pixel painted only on contact frames
            // 0 and 12, so a large physical pool could sit over a completely
            // dry tile forever. The cadence aligns with the existing dormant
            // support recheck, adding no extra collision pass.
            var settledBloodSeep = granular.Kind == GranularKind.Blood &&
                                   granular.RestFrames > 24 &&
                                   ((_stepSerial + i) & 63) == 0;
            if (!protectedByBasin && granular.Kind == GranularKind.Blood && collision.Hit &&
                (granular.RestFrames == 0 || granular.RestFrames == 12 || settledBloodSeep))
            {
                var stainAmount = settledBloodSeep
                    ? 0.012f
                    : 0.018f + Math.Clamp(incomingSpeed * 0.00012f, 0f, 0.065f);
                grid.DepositBlood(
                    collision.CellX,
                    collision.CellY,
                    collision.ContactPoint,
                    collision.Normal,
                    stainAmount);
            }
            var conveyorSupportHit = false;
            if (!routedThroughContinuousDrain && !granular.BypassConveyors && conveyors is not null)
            {
                var splatteredOnConveyor = false;
                foreach (var conveyor in conveyors)
                {
                    if (!conveyor.ContainsPoint(proxy.Position, proxy.Radius)) continue;
                    var conveyorContact = conveyor.ResolveParticle(ref proxy, dt, true);
                    if (!conveyorContact.Hit) continue;
                    conveyorSupportHit = true;
                    granular.Position = proxy.Position;
                    granular.PreviousPosition = proxy.PreviousPosition;
                    if (granular.Kind == GranularKind.Blood && granular.SplatterOnImpact && incomingSpeed >= 45f)
                    {
                        PaintConveyorSplatter(conveyor, conveyorContact, incomingSpeed);
                        BloodSplatteredThisStep++;
                        splatteredOnConveyor = true;
                        break;
                    }
                    if (granular.Kind == GranularKind.Blood &&
                        (granular.RestFrames == 0 || granular.RestFrames == 12))
                    {
                        var stainAmount = 0.018f + Math.Clamp(incomingSpeed * 0.00012f, 0f, 0.065f);
                        conveyor.DepositBlood(
                            conveyorContact.ContactPoint,
                            conveyorContact.Normal,
                            stainAmount);
                    }
                }
                if (splatteredOnConveyor)
                {
                    RemoveParticleAtSwapBack(i);
                    continue;
                }
            }

            if (!granular.InContinuousDrain)
                ResolveBlobContact(ref granular, bodies, dt);
            var chamberContact = SurfaceContact.None;
            if (!granular.InContinuousDrain &&
                granular.Kind == GranularKind.Blood && holdingChamber is not null)
            {
                proxy.Position = granular.Position;
                proxy.PreviousPosition = granular.PreviousPosition;
                chamberContact = holdingChamber.ResolveGranularExterior(ref proxy, dt);
                granular.Position = proxy.Position;
                granular.PreviousPosition = proxy.PreviousPosition;
            }
            var machineContact = SurfaceContact.None;
            if (!granular.InContinuousDrain && processingLine is not null)
            {
                if (granular.Kind == GranularKind.Blood)
                    processingLine.RegisterDoorwayBlood(
                        granular.Position,
                        granular.PreviousPosition,
                        granular.Radius,
                        incomingSpeed);
                proxy.Position = granular.Position;
                proxy.PreviousPosition = granular.PreviousPosition;
                machineContact = processingLine.ResolveGranular(ref proxy, dt, granular.Kind);
                granular.Position = proxy.Position;
                granular.PreviousPosition = proxy.PreviousPosition;
            }
            var speedSq = Vector2.DistanceSquared(granular.Position, granular.PreviousPosition) / dt2;
            // Sub-pixel drift under roughly 14 px/s should settle instead of
            // keeping thousands of pooled pixels microscopically active forever.
            if ((collision.Hit || chamberContact.Hit || machineContact.Hit || cartContainment.Hit) && speedSq < 196f)
            {
                if (granular.RestFrames < byte.MaxValue) granular.RestFrames++;
                if (granular.RestFrames > 12) granular.PreviousPosition = granular.Position;
            }
            else
            {
                granular.RestFrames = 0;
            }
            if (collision.Hit ||
                chamberContact.Hit ||
                machineContact.Hit ||
                cartContainment.Hit ||
                conveyorSupportHit)
                granular.ForegroundSupportFrames = 18;
            Particles[i] = granular;
        }

        var contactStart = Stopwatch.GetTimestamp();
        LastIntegrationMs = Stopwatch.GetElapsedTime(integrationStart, contactStart).TotalMilliseconds;
        ResolveGranularContacts();
        // Granular/granular separation can push a contained pixel through a thin
        // cart wall after the normal cart pass. Re-run containment after the
        // solver so the cart is the final authority on its contents.
        if (processingLine is not null)
        {
            for (var i = 0; i < Particles.Count; i++)
            {
                var granular = Particles[i];
                if (granular.InContinuousDrain) continue;
                if (!processingLine.CouldNeedCartContainment(
                        granular.Position,
                        granular.PreviousPosition,
                        granular.Radius))
                    continue;
                var proxy = new Particle
                {
                    Position = granular.Position,
                    PreviousPosition = granular.PreviousPosition,
                    InverseMass = 1f,
                    Radius = granular.Radius
                };
                if (!processingLine.ResolveGranularCartOnly(ref proxy, dt).Hit) continue;
                granular.Position = proxy.Position;
                granular.PreviousPosition = proxy.PreviousPosition;
                Particles[i] = granular;
            }
        }
        if (holdingChamber is not null)
        {
            for (var i = 0; i < Particles.Count; i++)
            {
                var granular = Particles[i];
                if (granular.InContinuousDrain) continue;
                if (granular.Kind != GranularKind.Blood) continue;
                var proxy = new Particle
                {
                    Position = granular.Position,
                    PreviousPosition = granular.PreviousPosition,
                    InverseMass = 1f,
                    Radius = granular.Radius
                };
                if (!holdingChamber.ResolveGranularExterior(ref proxy, dt).Hit) continue;
                granular.Position = proxy.Position;
                granular.PreviousPosition = proxy.PreviousPosition;
                Particles[i] = granular;
            }
        }
        if (_stepSerial % DensePileScanStride == 0)
            ConvertDensePilesToForegroundSpills(dt, worldWidth, worldHeight);
        LastContactSolveMs = Stopwatch.GetElapsedTime(contactStart).TotalMilliseconds;
    }

    private void UpdateForegroundSpills(
        float dt,
        float worldWidth,
        float worldHeight,
        ProcessingLine? processingLine)
    {
        for (var i = ForegroundSpills.Count - 1; i >= 0; i--)
        {
            var spill = ForegroundSpills[i];
            spill.Lifetime -= dt;
            spill.Velocity.Y = MathF.Min(540f, spill.Velocity.Y + 135f * dt);
            spill.Position += spill.Velocity * dt;
            var basinContact = processingLine?.TryCollectForegroundSpill(
                ref spill,
                dt) ?? ForegroundBasinContact.None;
            if (basinContact == ForegroundBasinContact.Collected)
            {
                RemoveForegroundSpillAtSwapBack(i);
                ForegroundSpillCollectedTotal++;
                continue;
            }
            if (basinContact == ForegroundBasinContact.ReemitPhysical &&
                TryReemitForegroundSpill(spill, dt))
            {
                RemoveForegroundSpillAtSwapBack(i);
                ForegroundSpillReemittedTotal++;
                continue;
            }
            if (spill.Lifetime <= 0f ||
                spill.Position.Y - spill.Radius > worldHeight + 80f ||
                spill.Position.X < -48f || spill.Position.X > worldWidth + 48f)
            {
                RemoveForegroundSpillAtSwapBack(i);
                ForegroundSpillExpiredTotal++;
                continue;
            }
            ForegroundSpills[i] = spill;
        }
    }

    private bool TryReemitForegroundSpill(
        ForegroundGranularSpill spill,
        float dt)
    {
        if (Particles.Count >= ParticleCapacity) return false;
        Particles.Add(new GranularParticle
        {
            Position = spill.Position,
            PreviousPosition = spill.Position - spill.Velocity * dt,
            Radius = spill.Radius,
            Lifetime = MathF.Max(4f, spill.Lifetime),
            Kind = spill.Kind,
            Appearance = spill.Appearance,
            SplatterOnImpact = spill.Kind == GranularKind.Blood,
            ForegroundSupportFrames = 0
        });
        return true;
    }

    private void ConvertDensePilesToForegroundSpills(
        float dt,
        float worldWidth,
        float worldHeight)
    {
        if (ForegroundSpills.Count >= ForegroundSpillCapacity ||
            Particles.Count == 0)
            return;

        var columns = Math.Clamp(
            (int)MathF.Ceiling(worldWidth / DensePileCellWidth),
            1,
            DensePileMaximumColumns);
        var rows = Math.Clamp(
            (int)MathF.Ceiling(worldHeight / DensePileCellHeight),
            1,
            DensePileMaximumRows);
        Array.Clear(_densePileCounts, 0, columns * rows);
        var maximumSpeedSquared = DensePileMaximumSpeed * DensePileMaximumSpeed;
        var maximumLocalDensity = 0;
        for (var i = 0; i < Particles.Count; i++)
        {
            var particle = Particles[i];
            if (!IsDensePileCandidate(particle, dt, maximumSpeedSquared)) continue;
            if (!TryDensePileIndex(particle.Position, columns, rows, out var densityIndex))
                continue;
            var density = ++_densePileCounts[densityIndex];
            maximumLocalDensity = Math.Max(maximumLocalDensity, density);
        }

        var pressureBudget = DensePileSpillBaseBudget +
                             Math.Max(0, maximumLocalDensity - DensePileThreshold) / 2;
        var remainingBudget = Math.Min(
            Math.Min(DensePileSpillMaximumBudget, pressureBudget),
            ForegroundSpillCapacity - ForegroundSpills.Count);
        for (var i = Particles.Count - 1; i >= 0 && remainingBudget > 0; i--)
        {
            var particle = Particles[i];
            if (!IsDensePileCandidate(particle, dt, maximumSpeedSquared)) continue;
            if (!TryDensePileIndex(particle.Position, columns, rows, out var densityIndex))
                continue;
            var localDensity = _densePileCounts[densityIndex];
            if (Next01() >= ForegroundTransitionChanceForDensity(localDensity)) continue;

            var physicalVelocity = (particle.Position - particle.PreviousPosition) / dt;
            ForegroundSpills.Add(new ForegroundGranularSpill
            {
                Position = particle.Position + new Vector2(NextFloat(-1.5f, 1.5f), 0f),
                Velocity = new Vector2(
                    Math.Clamp(physicalVelocity.X * 0.08f, -12f, 12f) + NextFloat(-7f, 7f),
                    NextFloat(270f, 390f)),
                Radius = particle.Radius,
                Lifetime = NextFloat(2.6f, 3.5f),
                Kind = particle.Kind,
                Appearance = particle.Appearance,
                Variation = (byte)(Next01() * byte.MaxValue)
            });
            _densePileCounts[densityIndex]--;
            RemoveParticleAtSwapBack(i);
            ForegroundSpillConvertedTotal++;
            remainingBudget--;
        }
    }

    internal static float ForegroundTransitionChanceForDensity(int localDensity)
    {
        if (localDensity <= 0) return 0f;
        if (localDensity <= 4)
            return 0.00045f + (localDensity - 1) * 0.00035f;
        if (localDensity <= 12)
            return 0.0015f + (localDensity - 4) * 0.00145f;
        if (localDensity <= DensePileThreshold)
            return 0.0131f + (localDensity - 12) * 0.0052f;
        return Math.Clamp(
            0.065f + (localDensity - DensePileThreshold) * 0.025f,
            0.065f,
            0.90f);
    }

    private static bool IsDensePileCandidate(
        GranularParticle particle,
        float dt,
        float maximumSpeedSquared)
    {
        if (particle.InContinuousDrain ||
            particle.ForegroundSupportFrames == 0 ||
            particle.Kind is not (GranularKind.Blood or GranularKind.Tissue))
            return false;
        var velocity = (particle.Position - particle.PreviousPosition) / dt;
        return velocity.LengthSquared() <= maximumSpeedSquared;
    }

    private static bool TryDensePileIndex(
        Vector2 position,
        int columns,
        int rows,
        out int index)
    {
        var x = (int)MathF.Floor(position.X / DensePileCellWidth);
        var y = (int)MathF.Floor(position.Y / DensePileCellHeight);
        if ((uint)x >= (uint)columns || (uint)y >= (uint)rows)
        {
            index = -1;
            return false;
        }
        index = y * columns + x;
        return true;
    }

    private void PaintGridSplatter(DestructibleGrid grid, CollisionResult collision, float speed)
    {
        var tangent = new Vector2(-collision.Normal.Y, collision.Normal.X);
        var count = 9 + (int)(Next01() * 6f);
        var spread = Math.Clamp(speed * 0.095f, 18f, 36f);
        for (var splat = 0; splat < count; splat++)
        {
            var tangentOffset = NextFloat(-spread, spread);
            var radialFalloff = 1f - MathF.Abs(tangentOffset) / spread;
            var offset = tangent * tangentOffset - collision.Normal * NextFloat(0f, 1.8f);
            grid.DepositBlood(
                collision.CellX,
                collision.CellY,
                collision.ContactPoint + offset,
                collision.Normal,
                NextFloat(0.022f, 0.050f) + radialFalloff * 0.050f);
        }
    }

    private bool ShouldSplatterOnGridImpact(
        GranularParticle granular,
        CollisionResult collision,
        float incomingSpeed)
    {
        if (granular.Kind != GranularKind.Blood || !collision.Hit) return false;
        var sideWall = MathF.Abs(collision.Normal.X) > 0.65f;
        if (!sideWall) return granular.SplatterOnImpact && incomingSpeed >= 45f;

        // Horizontal impacts lose their normal velocity immediately when they
        // resolve against a wall, so total-speed gating made almost every wall
        // droplet bounce without painting. The preselected splatter minority
        // uses the actual wall-normal impact and a lower threshold. A small
        // fraction of exceptionally hard ordinary droplets can also burst;
        // most wall blood still bounces and leaves only its localized contact.
        if (granular.SplatterOnImpact) return collision.Impact >= 22f;
        return collision.Impact >= 90f && Next01() < 0.10f;
    }

    private void PaintConveyorSplatter(ConveyorBelt conveyor, SurfaceContact contact, float speed)
    {
        var tangent = new Vector2(-contact.Normal.Y, contact.Normal.X);
        var count = 9 + (int)(Next01() * 6f);
        var spread = Math.Clamp(speed * 0.095f, 18f, 36f);
        for (var splat = 0; splat < count; splat++)
        {
            var tangentOffset = NextFloat(-spread, spread);
            var radialFalloff = 1f - MathF.Abs(tangentOffset) / spread;
            conveyor.DepositBlood(
                contact.ContactPoint + tangent * tangentOffset,
                contact.Normal,
                NextFloat(0.022f, 0.050f) + radialFalloff * 0.050f);
        }
    }

    private void BuildBlobBuckets(IReadOnlyList<SoftBody> bodies)
    {
        foreach (var bucket in _activeBlobBuckets) bucket.Clear();
        foreach (var bucket in _activeBlobBodyBuckets) bucket.Clear();
        _activeBlobBuckets.Clear();
        _activeBlobBodyBuckets.Clear();
        while (_blobContainmentContours.Count < bodies.Count)
            _blobContainmentContours.Add(default);
        if (_blobContainmentContours.Count > bodies.Count)
            _blobContainmentContours.RemoveRange(bodies.Count, _blobContainmentContours.Count - bodies.Count);
        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            var body = bodies[bodyIndex];
            var contourCache = _blobContainmentContours[bodyIndex];
            if (!ReferenceEquals(contourCache.Body, body))
                _blobContainmentContours[bodyIndex] = new ContainmentContourCache { Body = body };
            var minimum = new Vector2(float.MaxValue, float.MaxValue);
            var maximum = new Vector2(float.MinValue, float.MinValue);
            for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
            {
                if (body.IsPhysicalParticle(particleIndex))
                {
                    var particle = body.Particles[particleIndex];
                    minimum = Vector2.Min(minimum, particle.Position - new Vector2(particle.Radius));
                    maximum = Vector2.Max(maximum, particle.Position + new Vector2(particle.Radius));
                }
                if (!body.IsSurfaceParticle(particleIndex)) continue;
                var cell = Cell(body.Particles[particleIndex].Position, BlobHashCellSize);
                var key = Key(cell.X, cell.Y);
                if (!_blobBuckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<BlobParticleHandle>(10);
                    _blobBuckets[key] = bucket;
                }
                if (bucket.Count == 0) _activeBlobBuckets.Add(bucket);
                bucket.Add(new BlobParticleHandle(bodyIndex, particleIndex));
            }

            if (minimum.X > maximum.X) continue;
            var minimumCell = Cell(minimum, BlobHashCellSize);
            var maximumCell = Cell(maximum, BlobHashCellSize);
            for (var y = minimumCell.Y; y <= maximumCell.Y; y++)
            for (var x = minimumCell.X; x <= maximumCell.X; x++)
            {
                var key = Key(x, y);
                if (!_blobBodyBuckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<int>(4);
                    _blobBodyBuckets[key] = bucket;
                }
                if (bucket.Count == 0) _activeBlobBodyBuckets.Add(bucket);
                bucket.Add(bodyIndex);
            }
        }
    }

    private void ResolveBlobContact(ref GranularParticle granular, IReadOnlyList<SoftBody> bodies, float dt)
    {
        var cell = Cell(granular.Position, BlobHashCellSize);
        var depositedBlood = false;
        for (var y = cell.Y - 1; y <= cell.Y + 1; y++)
        for (var x = cell.X - 1; x <= cell.X + 1; x++)
        {
            if (!_blobBuckets.TryGetValue(Key(x, y), out var bucket)) continue;
            foreach (var handle in bucket)
            {
                var body = bodies[handle.BodyIndex];
                ref var tissue = ref body.Particles[handle.ParticleIndex];
                var delta = granular.Position - tissue.Position;
                var minDistance = granular.Radius + tissue.Radius;
                var distanceSq = delta.LengthSquared();
                if (distanceSq >= minDistance * minDistance) continue;

                var distance = MathF.Sqrt(MathF.Max(0.0001f, distanceSq));
                var normal = distanceSq < 0.0001f ? -Vector2.UnitY : delta / distance;
                var penetration = minDistance - distance;
                var velocity = (granular.Position - granular.PreviousPosition) / dt;
                var normalSpeed = Vector2.Dot(velocity, normal);
                // Dense, slowly settling blood must not keep an otherwise stable
                // blob awake forever. It is still displaced by the blob shell;
                // only a meaningful impact wakes full soft-body simulation.
                var incomingImpact = -normalSpeed;
                var wakeSpeed = granular.Kind == GranularKind.Tissue ? 38f : 72f;
                if (body.IsSleeping &&
                    (incomingImpact > wakeSpeed || penetration > 4.5f && incomingImpact > wakeSpeed * 0.35f))
                    body.Wake();
                granular.Position += normal * penetration * 0.94f;

                if (!depositedBlood && granular.Kind == GranularKind.Blood &&
                    (incomingImpact > 10f || penetration > 1.4f))
                {
                    body.DepositBloodStain(
                        handle.ParticleIndex,
                        tissue.Position + normal * tissue.Radius,
                        0.08f + Math.Clamp(incomingImpact * 0.0022f, 0f, 0.30f));
                    depositedBlood = true;
                }

                if (granular.Kind == GranularKind.Acid &&
                    granular.CorrosionCooldown <= 0f)
                {
                    var corrosionPoint =
                        tissue.Position + normal * tissue.Radius;
                    body.DamageLine(
                        corrosionPoint,
                        corrosionPoint,
                        granular.Radius * 1.65f + 2f,
                        1.55f,
                        maximumBreaks: 1);
                    body.DamageBonds(
                        corrosionPoint,
                        granular.Radius * 2.4f + 3f,
                        1.25f);
                    body.RegisterHitReaction(0.72f, 0.10f);
                    granular.CorrosionCooldown = 0.105f;
                    granular.Lifetime =
                        MathF.Max(0.4f, granular.Lifetime - 0.055f);
                }

                if (normalSpeed < 0f)
                {
                    var tangent = velocity - normal * normalSpeed;
                    var restitution = granular.Kind is
                        GranularKind.Blood or GranularKind.Acid
                        ? 0.07f
                        : 0.14f;
                    var bounced = tangent * 0.74f - normal * normalSpeed * restitution;
                    granular.PreviousPosition = granular.Position - bounced * dt;
                }
            }
        }

        if (granular.Kind == GranularKind.Blood)
            EjectBloodFromBlobInterior(ref granular, bodies, dt);
    }

    private GranularAppearance NextTissueAppearance()
    {
        var sample = Next01();
        if (sample < 0.30f) return GranularAppearance.BlobMint;
        if (sample < 0.42f) return GranularAppearance.BlobTeal;
        return GranularAppearance.Gore;
    }

    private void EjectBloodFromBlobInterior(
        ref GranularParticle granular,
        IReadOnlyList<SoftBody> bodies,
        float dt)
    {
        var cell = Cell(granular.Position, BlobHashCellSize);
        if (!_blobBodyBuckets.TryGetValue(Key(cell.X, cell.Y), out var nearbyBodies)) return;
        foreach (var bodyIndex in nearbyBodies)
        {
            var body = bodies[bodyIndex];
            var cache = _blobContainmentContours[bodyIndex];
            if (cache.LastStepSerial != _stepSerial)
            {
                var rebuildCadence = body.IsGrabbed ? 1 : body.IsSleeping ? 12 : 4;
                var rebuild = cache.Points is null ||
                              cache.TopologyRevision != body.TopologyRevision ||
                              cache.Age >= rebuildCadence;
                if (rebuild)
                {
                    cache.Points = BlobContourBuilder.BuildShell(body).Points;
                    cache.TopologyRevision = body.TopologyRevision;
                    cache.Center = body.Center;
                    cache.Age = 0;
                }
                else
                {
                    var translation = body.Center - cache.Center;
                    if (translation.LengthSquared() > 0.000001f)
                        for (var pointIndex = 0; pointIndex < cache.Points!.Length; pointIndex++)
                            cache.Points[pointIndex] += translation;
                    cache.Center = body.Center;
                    cache.Age++;
                }
                cache.LastStepSerial = _stepSerial;
                _blobContainmentContours[bodyIndex] = cache;
            }
            var contour = cache.Points;
            if (contour is null) continue;
            if (contour.Length < 3 || !BlobContourBuilder.ContainsPoint(contour, granular.Position)) continue;

            var closestPoint = Vector2.Zero;
            var closestDistanceSquared = float.MaxValue;
            for (var edgeIndex = 0; edgeIndex < contour.Length; edgeIndex++)
            {
                var start = contour[edgeIndex];
                var end = contour[(edgeIndex + 1) % contour.Length];
                var edge = end - start;
                var edgeLengthSquared = edge.LengthSquared();
                if (edgeLengthSquared < 0.0001f) continue;
                var edgeT = Math.Clamp(
                    Vector2.Dot(granular.Position - start, edge) / edgeLengthSquared,
                    0f,
                    1f);
                var candidate = start + edge * edgeT;
                var distanceSquared = Vector2.DistanceSquared(granular.Position, candidate);
                if (distanceSquared >= closestDistanceSquared) continue;
                closestDistanceSquared = distanceSquared;
                closestPoint = candidate;
            }
            if (closestDistanceSquared == float.MaxValue) continue;

            var outward = closestPoint - granular.Position;
            if (outward.LengthSquared() < 0.0001f)
                outward = granular.Position - body.Center;
            if (outward.LengthSquared() < 0.0001f) outward = -Vector2.UnitY;
            outward = Vector2.Normalize(outward);
            var velocity = (granular.Position - granular.PreviousPosition) / dt;
            var tangent = velocity - outward * Vector2.Dot(velocity, outward);
            var outwardSpeed = MathF.Max(12f, Vector2.Dot(velocity, outward) * 0.20f);
            granular.Position = closestPoint + outward * (granular.Radius + 0.6f);
            granular.PreviousPosition = granular.Position - (tangent * 0.68f + outward * outwardSpeed) * dt;
            granular.RestFrames = 0;
        }
    }

    private void ResolveGranularContacts()
    {
        foreach (var bucket in _activeGranularBuckets) bucket.Clear();
        _activeGranularBuckets.Clear();
        for (var i = 0; i < Particles.Count; i++)
        {
            if (Particles[i].InContinuousDrain) continue;
            var cell = Cell(Particles[i].Position, GranularHashCellSize);
            var key = Key(cell.X, cell.Y);
            if (!_granularBuckets.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>(8);
                _granularBuckets[key] = bucket;
            }
            if (bucket.Count == 0) _activeGranularBuckets.Add(bucket);
            bucket.Add(i);
        }

        for (var i = 0; i < Particles.Count; i++)
        {
            var a = Particles[i];
            if (a.InContinuousDrain) continue;
            var cell = Cell(a.Position, GranularHashCellSize);
            // Visit each unordered neighboring cell pair exactly once. The old
            // 3x3 scan traversed both A->B and B->A, then discarded half using
            // the particle index. These five forward cells preserve every pair
            // while eliminating the redundant dictionary and list traversal.
            ResolveBucket(ref a, i, cell.X, cell.Y, true);
            ResolveBucket(ref a, i, cell.X + 1, cell.Y, false);
            ResolveBucket(ref a, i, cell.X - 1, cell.Y + 1, false);
            ResolveBucket(ref a, i, cell.X, cell.Y + 1, false);
            ResolveBucket(ref a, i, cell.X + 1, cell.Y + 1, false);
            Particles[i] = a;
        }
    }

    private void ResolveBucket(
        ref GranularParticle a,
        int particleIndex,
        int cellX,
        int cellY,
        bool sameCell)
    {
        if (!_granularBuckets.TryGetValue(Key(cellX, cellY), out var bucket)) return;
        foreach (var otherIndex in bucket)
        {
            if (sameCell && otherIndex <= particleIndex) continue;
            var b = Particles[otherIndex];
            if (b.InContinuousDrain) continue;
            if (a.RestFrames > 12 && b.RestFrames > 12) continue;
            var delta = b.Position - a.Position;
            var minDistance = a.Radius + b.Radius;
            var distanceSq = delta.LengthSquared();
            if (distanceSq >= minDistance * minDistance) continue;
            var distance = MathF.Sqrt(MathF.Max(0.0001f, distanceSq));
            var normal = distanceSq < 0.0001f ? Vector2.UnitX : delta / distance;
            var correction = normal * ((minDistance - distance) * 0.48f);
            a.Position -= correction;
            a.PreviousPosition -= correction;
            b.Position += correction;
            b.PreviousPosition += correction;
            Particles[otherIndex] = b;
        }
    }

    private void Add(GranularParticle particle)
    {
        if (Particles.Count >= ParticleCapacity) Particles.RemoveRange(0, Math.Min(64, Particles.Count));
        Particles.Add(particle);
        SpawnedThisStep++;
    }

    private void RemoveParticleAtSwapBack(int index)
    {
        var last = Particles.Count - 1;
        if ((uint)index >= (uint)Particles.Count) return;
        if (index != last) Particles[index] = Particles[last];
        Particles.RemoveAt(last);
    }

    private void RemoveForegroundSpillAtSwapBack(int index)
    {
        var last = ForegroundSpills.Count - 1;
        if ((uint)index >= (uint)ForegroundSpills.Count) return;
        if (index != last) ForegroundSpills[index] = ForegroundSpills[last];
        ForegroundSpills.RemoveAt(last);
    }

    private static (int X, int Y) Cell(Vector2 position, float cellSize)
        => ((int)MathF.Floor(position.X / cellSize), (int)MathF.Floor(position.Y / cellSize));

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

    private float NextFloat(float min, float max) => min + (max - min) * Next01();

    private Vector2 RandomUnit()
    {
        var angle = Next01() * MathF.Tau;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private float Next01()
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 17;
        _randomState ^= _randomState << 5;
        return (_randomState & 0x00FFFFFFu) / 16777216f;
    }

    private readonly record struct BlobParticleHandle(int BodyIndex, int ParticleIndex);

    private struct ContainmentContourCache
    {
        public SoftBody? Body;
        public Vector2[]? Points;
        public Vector2 Center;
        public int TopologyRevision;
        public int LastStepSerial;
        public int Age;
    }
}
