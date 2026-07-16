using System.Numerics;
using BlobForge.Physics;

namespace BlobForge.World;

public sealed class DestructibleGrid
{
    public const int ProcessingDeckRow = 15;
    private readonly MaterialCell[] _cells;
    private readonly HashSet<int> _damagedThisStep = new();
    private readonly List<BloodSurfaceMark> _bloodStains = new(256);
    private readonly List<BloodSurfaceMark> _pendingBloodFlows = new(8);
    private readonly List<BloodDripEmission> _pendingBloodDrops = new(3);
    private uint _paintRandomState = 0x91E10DA5u;
    private float _dripPlannerTime;
    private readonly Dictionary<long, DripPoolBucket> _dripPoolBuckets = new(128);
    private readonly List<DripPoolCandidate> _dripPoolCandidates = new(128);
    // This is a coalescing threshold, not an eviction limit. Once reached,
    // additional paint on an already represented surface face is folded into
    // that face. Paint on a genuinely new face is still retained so pigment
    // never disappears merely because some unrelated area was stained later.
    public const int PersistentStainSoftLimit = 640;
    private const float PersistentPigmentFloor = 0.01f;
    private const int BloodFlowSpawnBudgetPerStep = 2;
    private const int BloodDropSpawnBudgetPerStep = 2;
    private const int RecentMergeSearchLimit = 160;

    public DestructibleGrid(int columns, int rows, int cellSize)
    {
        Columns = columns;
        Rows = rows;
        CellSize = cellSize;
        _cells = new MaterialCell[columns * rows];
    }

    public int Columns { get; }
    public int Rows { get; }
    public int CellSize { get; }
    public int DestroyedCellCount { get; private set; }
    public int SurfaceRevision { get; private set; }
    public int SolidCellCount => _cells.Count(c => c.IsSolid);
    public int StainedCellCount => _bloodStains.Count;
    public IReadOnlyList<BloodSurfaceMark> BloodStains => _bloodStains;

    public ref MaterialCell Cell(int x, int y) => ref _cells[y * Columns + x];

    public void Set(int x, int y, CellMaterial material)
    {
        if (!InBounds(x, y)) return;
        ref var cell = ref Cell(x, y);
        var wasSolid = cell.IsSolid;
        cell.Material = material;
        cell.Health = material switch
        {
            CellMaterial.Steel => float.PositiveInfinity,
            CellMaterial.Concrete => 70f,
            CellMaterial.Glass => 28f,
            _ => 0f
        };
        if (wasSolid != cell.IsSolid) SurfaceRevision++;
    }

    public void BeginStep() => _damagedThisStep.Clear();

    public void BeginStep(float dt)
    {
        _damagedThisStep.Clear();
        UpdateBloodStains(dt);
    }

    public void DrainBloodDrops(List<BloodDripEmission> destination)
    {
        destination.AddRange(_pendingBloodDrops);
        _pendingBloodDrops.Clear();
    }

    public void DepositBlood(int x, int y, Vector2 position, Vector2 surfaceNormal, float amount)
    {
        if (!InBounds(x, y) || amount <= 0f) return;
        if (!Cell(x, y).IsSolid) return;
        if (surfaceNormal.LengthSquared() < 0.001f) surfaceNormal = -Vector2.UnitY;
        surfaceNormal = CanonicalSurfaceNormal(x, y, position, Vector2.Normalize(surfaceNormal));
        AddOrMergeBloodMark(new BloodSurfaceMark
        {
            Position = position,
            SurfaceNormal = Vector2.Normalize(surfaceNormal),
            Amount = Math.Clamp(amount, 0.01f, 0.24f),
            Wetness = 1f,
            Radius = 2.2f + MathF.Sqrt(amount) * 5.5f,
            CellX = x,
            CellY = y
            ,Variation = NextPaintVariation()
        });
    }

    private Vector2 CanonicalSurfaceNormal(int cellX, int cellY, Vector2 position, Vector2 suppliedNormal)
    {
        var bestNormal = suppliedNormal;
        var bestScore = float.NegativeInfinity;
        var cellLeft = cellX * (float)CellSize;
        var cellTop = cellY * (float)CellSize;

        Consider(!IsSolid(cellX - 1, cellY), -Vector2.UnitX, MathF.Abs(position.X - cellLeft));
        Consider(!IsSolid(cellX + 1, cellY), Vector2.UnitX, MathF.Abs(position.X - (cellLeft + CellSize)));
        Consider(!IsSolid(cellX, cellY - 1), -Vector2.UnitY, MathF.Abs(position.Y - cellTop));
        Consider(!IsSolid(cellX, cellY + 1), Vector2.UnitY, MathF.Abs(position.Y - (cellTop + CellSize)));
        return bestNormal;

        void Consider(bool exposed, Vector2 candidate, float faceDistance)
        {
            if (!exposed) return;
            var alignment = Vector2.Dot(suppliedNormal, candidate);
            if (alignment < 0.10f) return;
            var score = alignment * 4f - MathF.Min(CellSize, faceDistance) / CellSize;
            if (score <= bestScore) return;
            bestScore = score;
            bestNormal = candidate;
        }
    }

    private void UpdateBloodStains(float dt)
    {
        _pendingBloodFlows.Clear();
        _pendingBloodDrops.Clear();
        for (var markIndex = _bloodStains.Count - 1; markIndex >= 0; markIndex--)
        {
            var mark = _bloodStains[markIndex];
            if (!InBounds(mark.CellX, mark.CellY) || !Cell(mark.CellX, mark.CellY).IsSolid)
            {
                _bloodStains.RemoveAt(markIndex);
                continue;
            }

            var wallAttached = MathF.Abs(mark.SurfaceNormal.X) > 0.55f;
            mark.Wetness = MathF.Max(0f, mark.Wetness - dt * 0.085f);

            if (!mark.IsDrip && wallAttached &&
                mark.Wetness > 0.12f && mark.Amount > 0.032f && _pendingBloodFlows.Count < 2)
            {
                var wallCadence = 0.82f + (mark.Variation & 7) * 0.08f;
                mark.FlowAccumulator += dt * (0.72f + mark.Amount * 1.4f) * wallCadence;
                if (mark.FlowAccumulator >= 1f)
                {
                    mark.FlowAccumulator -= 1f;
                    var transfer = MathF.Min(0.055f, mark.Amount * 0.16f);
                    mark.Amount -= transfer;
                    var wallVariation = NextPaintVariation();
                    _pendingBloodFlows.Add(new BloodSurfaceMark
                    {
                        Position = new Vector2(mark.Position.X, mark.Position.Y + 1f),
                        SurfaceNormal = mark.SurfaceNormal,
                        Amount = MathF.Max(0.065f, transfer),
                        Wetness = mark.Wetness,
                        Radius = 1.8f + (wallVariation & 7) * 0.42f,
                        FlowAccumulator = ((wallVariation >> 3) & 7) * 0.045f,
                        RunoffLoad = mark.Amount,
                        CellX = mark.CellX,
                        CellY = mark.CellY,
                        IsDrip = true,
                        IsRunoffLeader = true,
                        Variation = wallVariation
                    });
                }
            }

            if (mark.IsDrip && mark.Wetness > 0.12f && mark.Amount > 0.09f &&
                _pendingBloodDrops.Count < BloodDropSpawnBudgetPerStep)
            {
                var cadenceVariation = 0.62f + (mark.Variation & 7) * 0.085f;
                mark.FlowAccumulator += dt * (0.12f + mark.Amount * 0.24f) * cadenceVariation;
                if (mark.FlowAccumulator >= 1f)
                {
                    mark.FlowAccumulator -= 1f;
                    var dropVariation = NextPaintVariation();
                    var tipLength = mark.VisibleTrailLength;
                    var horizontalJitter = ((dropVariation & 15) / 15f - 0.5f) * MathF.Max(3f, mark.Radius);
                    _pendingBloodDrops.Add(new BloodDripEmission(
                        new Vector2(mark.Position.X + horizontalJitter, mark.Position.Y + tipLength),
                        new Vector2(((dropVariation >> 4) & 7) - 3.5f, 18f + (dropVariation & 7) * 3.2f),
                        1.35f + ((dropVariation >> 2) & 3) * 0.32f,
                        dropVariation));
                    mark.Amount = MathF.Max(0f, mark.Amount - 0.008f);
                }
            }

            // Drying changes the rendered palette through Wetness; it does not
            // erase pigment. Only destruction of the supporting surface (and,
            // later, an explicit cleaning mechanic) may remove this mark.
            mark.Amount = MathF.Max(PersistentPigmentFloor, mark.Amount);
            _bloodStains[markIndex] = mark;
        }
        PlanProceduralDrips(dt);
        foreach (var flow in _pendingBloodFlows) AddOrMergeBloodMark(flow);
    }

    private void PlanProceduralDrips(float dt)
    {
        _dripPlannerTime += dt;
        if (_dripPlannerTime < 0.12f) return;
        _dripPlannerTime = 0f;
        _dripPoolBuckets.Clear();
        _dripPoolCandidates.Clear();
        const float horizontalBinSize = 18f;
        const float verticalBinSize = 8f;
        for (var i = 0; i < _bloodStains.Count; i++)
        {
            var mark = _bloodStains[i];
            if (mark.IsDrip || mark.Wetness <= 0.12f || mark.SurfaceNormal.Y > -0.45f) continue;
            var binX = (int)MathF.Floor(mark.Position.X / horizontalBinSize);
            var binY = (int)MathF.Floor(mark.Position.Y / verticalBinSize);
            var key = PoolKey(binX, binY);
            _dripPoolBuckets.TryGetValue(key, out var bucket);
            bucket.Amount += mark.Amount;
            bucket.WeightedX += mark.Position.X * mark.Amount;
            bucket.SurfaceY += mark.Position.Y * mark.Amount;
            if (bucket.SourceIndex < 0 || mark.Amount > bucket.LargestSourceAmount)
            {
                bucket.SourceIndex = i;
                bucket.LargestSourceAmount = mark.Amount;
            }
            _dripPoolBuckets[key] = bucket;
        }

        foreach (var pair in _dripPoolBuckets)
        {
            var binX = (int)(pair.Key >> 32);
            var binY = (int)(uint)pair.Key;
            var pooledAmount = 0f;
            var weightedX = 0f;
            var weightedY = 0f;
            var sourceIndex = pair.Value.SourceIndex;
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (!_dripPoolBuckets.TryGetValue(PoolKey(binX + offsetX, binY), out var neighbor)) continue;
                pooledAmount += neighbor.Amount;
                weightedX += neighbor.WeightedX;
                weightedY += neighbor.SurfaceY;
            }
            if (pooledAmount < 0.30f || sourceIndex < 0) continue;
            _dripPoolCandidates.Add(new DripPoolCandidate(
                pooledAmount,
                weightedX / pooledAmount,
                weightedY / pooledAmount,
                sourceIndex));
        }
        _dripPoolCandidates.Sort(static (a, b) => b.PooledAmount.CompareTo(a.PooledAmount));

        var spawnBudget = BloodFlowSpawnBudgetPerStep;
        var processed = 0;
        Span<int> nearbyDrips = stackalloc int[4];
        foreach (var candidate in _dripPoolCandidates)
        {
            if (processed++ >= 16) break;
            var source = _bloodStains[candidate.SourceIndex];
            var pooledAmount = candidate.PooledAmount;
            var anchorX = candidate.AnchorX;

            var nearbyDripCount = 0;
            var activeNearbyCount = 0;
            var driedNearbyCount = 0;
            var largestNearby = -1;
            var largestNearbyScore = float.MinValue;
            var largestDriedNearby = -1;
            var largestDriedScore = float.MinValue;
            const float clusterRadius = 24f;
            for (var j = 0; j < _bloodStains.Count; j++)
            {
                var drip = _bloodStains[j];
                if (!drip.IsDrip || MathF.Abs(drip.Position.X - anchorX) > clusterRadius ||
                    MathF.Abs(drip.Position.Y - candidate.SurfaceY) > 10f) continue;
                if (nearbyDripCount < nearbyDrips.Length) nearbyDrips[nearbyDripCount] = j;
                nearbyDripCount++;
                var dried = drip.Wetness <= 0.12f;
                if (dried)
                {
                    driedNearbyCount++;
                    var driedScore = drip.Amount + drip.Radius * 0.1f +
                                     (drip.IsRunoffLeader ? 100f : 0f);
                    if (driedScore > largestDriedScore)
                    {
                        largestDriedScore = driedScore;
                        largestDriedNearby = j;
                    }
                }
                else activeNearbyCount++;
                // Prefer feeding a currently active lane. A dried leader is
                // considered separately so fresh blood can route around it.
                var leadershipScore = drip.Amount + (drip.IsRunoffLeader ? 100f : 0f) +
                                      (dried ? 0f : 200f);
                if (leadershipScore <= largestNearbyScore) continue;
                largestNearbyScore = leadershipScore;
                largestNearby = j;
            }
            var transferVariation = NextPaintVariation();
            var transferScale = 0.055f + (transferVariation & 7) * 0.009f;
            var transfer = MathF.Min(0.065f, source.Amount * transferScale);
            if (transfer <= 0.004f) continue;
            source.Amount -= transfer;
            _bloodStains[candidate.SourceIndex] = source;

            var storedNearbyCount = Math.Min(nearbyDripCount, nearbyDrips.Length);
            var driedOnlyNearby = activeNearbyCount == 0 && driedNearbyCount > 0;
            // Most fresh pools beside fully dried runoff establish a new lane.
            // The remaining cases revive the old lane with an explicit width
            // increase, so reactivation never looks like the old pixels simply
            // switching back on unchanged.
            var routeAroundDried = driedOnlyNearby && spawnBudget > 0 && NextPaintVariation() < 178;
            var satelliteChance = pooledAmount <= 1.25f
                ? 0f
                : Math.Clamp(0.045f + (pooledAmount - 1.25f) * 0.075f, 0.045f, 0.34f);
            var spawnSatellite = storedNearbyCount == 0 ||
                                 routeAroundDried ||
                                 storedNearbyCount < 3 && spawnBudget > 0 &&
                                 NextPaintVariation() / 255f < satelliteChance;
            byte spawnVariation = 0;
            var spawnX = anchorX;
            if (spawnSatellite && spawnBudget > 0)
            {
                spawnVariation = NextPaintVariation();
                if (routeAroundDried && largestDriedNearby >= 0)
                {
                    var driedLane = _bloodStains[largestDriedNearby];
                    var direction = (spawnVariation & 1) == 0 ? -1f : 1f;
                    var routeDistance = Math.Clamp(
                        driedLane.Radius * 0.72f + 5.5f + ((spawnVariation >> 1) & 3) * 1.35f,
                        7f,
                        15f);
                    spawnX = driedLane.Position.X + direction * routeDistance;
                }
                else if (storedNearbyCount == 0)
                {
                    spawnX += ((spawnVariation & 15) / 15f - 0.5f) * 5f;
                }
                else
                {
                    var direction = (spawnVariation & 1) == 0 ? -1f : 1f;
                    var distance = 6f + ((spawnVariation >> 1) & 7) * 1.35f;
                    spawnX += direction * distance;
                }
                for (var nearby = 0; nearby < storedNearbyCount; nearby++)
                {
                    if (MathF.Abs(_bloodStains[nearbyDrips[nearby]].Position.X - spawnX) >= 5f) continue;
                    spawnSatellite = false;
                    break;
                }
                if (_pendingBloodFlows.Any(drip => MathF.Abs(drip.Position.X - spawnX) < 5f))
                    spawnSatellite = false;
            }

            if (!spawnSatellite && storedNearbyCount > 0)
            {
                var selectionVariation = NextPaintVariation();
                var targetIndex = driedOnlyNearby && largestDriedNearby >= 0
                    ? largestDriedNearby
                    : selectionVariation < 182 && largestNearby >= 0
                        ? largestNearby
                        : nearbyDrips[selectionVariation % storedNearbyCount];
                var drip = _bloodStains[targetIndex];
                var reactivatingDriedLane = drip.Wetness <= 0.12f;
                var amountCap = 0.72f + ((drip.Variation >> 3) & 7) * 0.24f;
                var growthVariation = 0.65f + (selectionVariation & 7) * 0.11f;
                drip.Amount = MathF.Min(amountCap, drip.Amount + transfer * growthVariation);
                drip.Wetness = MathF.Max(drip.Wetness, source.Wetness);
                if (drip.IsRunoffLeader)
                {
                    // Keep one stable continuation lane for the surrounding
                    // pool. This lane carries pooled pressure through tile
                    // seams; satellites retain their individual short scale.
                    drip.RunoffLoad = MathF.Max(drip.RunoffLoad, MathF.Min(5.3f, pooledAmount));
                }
                if (reactivatingDriedLane)
                {
                    var widthGain = 1.15f + (selectionVariation & 3) * 0.32f + transfer * 8f;
                    drip.Radius = Math.Clamp(drip.Radius + widthGain, 2.4f, 11.4f);
                    drip.RunoffLoad = MathF.Max(
                        drip.RunoffLoad,
                        MathF.Min(5.3f, pooledAmount * 1.12f));
                }
                else
                {
                    var radiusCap = 4.2f + (drip.Variation & 7) * 0.95f;
                    drip.Radius = Math.Clamp(
                        drip.Radius + transfer * (2.2f + (selectionVariation & 15) * 0.34f),
                        1.8f,
                        radiusCap);
                }
                TryBindBloodMarkToSurface(ref drip);
                _bloodStains[targetIndex] = drip;
            }
            else if (spawnSatellite && spawnBudget > 0)
            {
                var markVariation = NextPaintVariation();
                _pendingBloodFlows.Add(new BloodSurfaceMark
                {
                    Position = new Vector2(spawnX, source.Position.Y),
                    SurfaceNormal = source.SurfaceNormal,
                    Amount = MathF.Max(0.055f, transfer * (0.75f + (markVariation & 7) * 0.09f)),
                    Wetness = source.Wetness,
                    Radius = Math.Clamp(
                        1.7f + MathF.Sqrt(pooledAmount) * (0.72f + (markVariation & 7) * 0.12f),
                        1.8f,
                        6.4f),
                    FlowAccumulator = ((markVariation >> 3) & 7) * 0.055f,
                    RunoffLoad = storedNearbyCount == 0
                        ? MathF.Min(5.3f, pooledAmount)
                        : routeAroundDried
                            ? MathF.Min(4.2f, pooledAmount * 0.82f)
                        : MathF.Min(0.78f, pooledAmount * 0.32f),
                    CellX = source.CellX,
                    CellY = source.CellY,
                    IsDrip = true,
                    IsRunoffLeader = storedNearbyCount == 0 || routeAroundDried,
                    Variation = markVariation
                });
                spawnBudget--;
            }
        }
    }

    private static long PoolKey(int x, int y) => ((long)x << 32) ^ (uint)y;

    private struct DripPoolBucket
    {
        public float Amount;
        public float WeightedX;
        public float SurfaceY;
        public int SourceIndex;
        public float LargestSourceAmount;
    }

    private readonly record struct DripPoolCandidate(
        float PooledAmount,
        float AnchorX,
        float SurfaceY,
        int SourceIndex);

    private byte NextPaintVariation()
    {
        _paintRandomState ^= _paintRandomState << 13;
        _paintRandomState ^= _paintRandomState >> 17;
        _paintRandomState ^= _paintRandomState << 5;
        return (byte)(_paintRandomState >> 24);
    }

    private void AddOrMergeBloodMark(BloodSurfaceMark incoming)
    {
        if (!TryBindBloodMarkToSurface(ref incoming)) return;
        var sideWallPaint = !incoming.IsDrip && MathF.Abs(incoming.SurfaceNormal.X) > 0.65f;
        var mergeDistance = sideWallPaint
            ? MathF.Max(1.5f, incoming.Radius * 0.48f)
            : MathF.Max(3f, incoming.Radius * 0.85f);
        var searchStart = Math.Max(0, _bloodStains.Count - RecentMergeSearchLimit);
        if (TryMergeBloodMark(incoming, sideWallPaint, mergeDistance, searchStart, _bloodStains.Count))
            return;
        // The capped layer is normally searched only in its recent tail. At
        // capacity, also find an older mark at this exact surface location so
        // later spills can visibly re-wet it and restart its runoff instead of
        // being consumed by unrelated layer churn.
        if (_bloodStains.Count >= PersistentStainSoftLimit && searchStart > 0 &&
            TryMergeBloodMark(incoming, sideWallPaint, mergeDistance, 0, searchStart))
            return;
        if (_bloodStains.Count >= PersistentStainSoftLimit &&
            TryMergeBloodMark(
                incoming,
                sideWallPaint,
                CellSize * 1.5f,
                0,
                _bloodStains.Count,
                forcePersistentCoalesce: true))
            return;
        _bloodStains.Add(incoming);
    }

    private bool TryMergeBloodMark(
        BloodSurfaceMark incoming,
        bool sideWallPaint,
        float mergeDistance,
        int startInclusive,
        int endExclusive,
        bool forcePersistentCoalesce = false)
    {
        for (var i = endExclusive - 1; i >= startInclusive; i--)
        {
            var existing = _bloodStains[i];
            if (existing.CellX != incoming.CellX || existing.CellY != incoming.CellY ||
                existing.IsDrip != incoming.IsDrip ||
                Vector2.Dot(existing.SurfaceNormal, incoming.SurfaceNormal) < 0.35f ||
                Vector2.DistanceSquared(existing.Position, incoming.Position) > mergeDistance * mergeDistance) continue;
            if (!forcePersistentCoalesce && !incoming.IsDrip &&
                existing.Wetness <= 0.08f && incoming.Wetness > 0.5f)
                continue;
            // A wall receives paint along a narrow vertical tangent. Reusing
            // one saturated mark made every later spray disappear into the
            // first impact. Stop merging saturated wall pigment so subsequent
            // bursts can establish nearby marks and visibly build coverage.
            if (!forcePersistentCoalesce && sideWallPaint && existing.Amount >= 0.30f) continue;
            var total = MathF.Min(sideWallPaint ? 0.34f : 2.4f, existing.Amount + incoming.Amount);
            var incomingWeight = incoming.Amount / MathF.Max(0.001f, existing.Amount + incoming.Amount);
            existing.Position = Vector2.Lerp(existing.Position, incoming.Position, incomingWeight * 0.35f);
            existing.Amount = total;
            existing.Wetness = MathF.Max(existing.Wetness, incoming.Wetness);
            if (sideWallPaint)
                existing.FlowAccumulator = MathF.Min(
                    0.92f,
                    existing.FlowAccumulator + incoming.Amount * 0.65f);
            existing.RunoffLoad = MathF.Max(existing.RunoffLoad, incoming.RunoffLoad);
            existing.IsRunoffLeader |= incoming.IsRunoffLeader;
            existing.Radius = Math.Clamp(2.1f + MathF.Sqrt(total) * 5.4f, 2f, 10.5f);
            TryBindBloodMarkToSurface(ref existing);
            _bloodStains[i] = existing;
            return true;
        }
        return false;
    }

    private bool TryBindBloodMarkToSurface(ref BloodSurfaceMark mark)
    {
        if (mark.SurfaceNormal.LengthSquared() < 0.001f) mark.SurfaceNormal = -Vector2.UnitY;
        mark.SurfaceNormal = Vector2.Normalize(mark.SurfaceNormal);
        var supportingPoint = mark.Position - mark.SurfaceNormal * 0.8f;
        var supportX = (int)MathF.Floor(supportingPoint.X / CellSize);
        var supportY = (int)MathF.Floor(supportingPoint.Y / CellSize);
        if (!IsSolid(supportX, supportY))
        {
            // Exact face/corner coordinates can quantize into the air cell on
            // the other side. Retain the supplied impact cell only while the
            // painted coordinate actually lies on that cell's face.
            if (!IsSolid(mark.CellX, mark.CellY) ||
                mark.Position.X < mark.CellX * CellSize - 0.01f ||
                mark.Position.X > (mark.CellX + 1) * CellSize + 0.01f ||
                mark.Position.Y < mark.CellY * CellSize - 0.01f ||
                mark.Position.Y > (mark.CellY + 1) * CellSize + 0.01f)
                return false;
            supportX = mark.CellX;
            supportY = mark.CellY;
        }

        var horizontalFace = MathF.Abs(mark.SurfaceNormal.Y) >= MathF.Abs(mark.SurfaceNormal.X);
        if (horizontalFace)
        {
            var outwardY = mark.SurfaceNormal.Y < 0f ? supportY - 1 : supportY + 1;
            if (IsSolid(supportX, outwardY)) return false;
            var minimumX = supportX * (float)CellSize;
            var maximumX = (supportX + 1) * (float)CellSize;
            var edgeMargin = MathF.Min(mark.IsDrip ? 6.5f : 5.5f, MathF.Max(1f, mark.Radius * 0.75f));
            if (!IsSolid(supportX - 1, supportY)) minimumX += edgeMargin;
            if (!IsSolid(supportX + 1, supportY)) maximumX -= edgeMargin;
            if (minimumX > maximumX) minimumX = maximumX = (supportX + 0.5f) * CellSize;
            mark.Position = new Vector2(
                Math.Clamp(mark.Position.X, minimumX, maximumX),
                mark.SurfaceNormal.Y < 0f ? supportY * CellSize : (supportY + 1) * CellSize);
        }
        else
        {
            var outwardX = mark.SurfaceNormal.X < 0f ? supportX - 1 : supportX + 1;
            if (IsSolid(outwardX, supportY)) return false;
            var minimumY = supportY * (float)CellSize;
            var maximumY = (supportY + 1) * (float)CellSize;
            var edgeMargin = MathF.Min(5.5f, MathF.Max(1f, mark.Radius * 0.75f));
            if (!IsSolid(supportX, supportY - 1)) minimumY += edgeMargin;
            if (!IsSolid(supportX, supportY + 1)) maximumY -= edgeMargin;
            if (minimumY > maximumY) minimumY = maximumY = (supportY + 0.5f) * CellSize;
            mark.Position = new Vector2(
                mark.SurfaceNormal.X < 0f ? supportX * CellSize : (supportX + 1) * CellSize,
                Math.Clamp(mark.Position.Y, minimumY, maximumY));
        }
        mark.CellX = supportX;
        mark.CellY = supportY;
        return true;
    }

    private bool IsSolid(int x, int y) => InBounds(x, y) && Cell(x, y).IsSolid;

    public CollisionResult ResolveParticle(ref Particle particle, float dt)
    {
        var minX = Math.Clamp((int)((particle.Position.X - particle.Radius) / CellSize), 0, Columns - 1);
        var maxX = Math.Clamp((int)((particle.Position.X + particle.Radius) / CellSize), 0, Columns - 1);
        var minY = Math.Clamp((int)((particle.Position.Y - particle.Radius) / CellSize), 0, Rows - 1);
        var maxY = Math.Clamp((int)((particle.Position.Y + particle.Radius) / CellSize), 0, Rows - 1);
        var strongestImpact = 0f;
        var hit = false;
        var hitX = -1;
        var hitY = -1;
        var hitNormal = Vector2.Zero;
        var hitPoint = Vector2.Zero;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!Cell(x, y).IsSolid) continue;
                var rectMin = new Vector2(x * CellSize, y * CellSize);
                var rectMax = rectMin + new Vector2(CellSize);
                if (!TryCircleAabb(
                        particle.Position,
                        particle.PreviousPosition,
                        particle.Radius,
                        rectMin,
                        rectMax,
                        x > 0 && !Cell(x - 1, y).IsSolid,
                        x + 1 < Columns && !Cell(x + 1, y).IsSolid,
                        y > 0 && !Cell(x, y - 1).IsSolid,
                        y + 1 < Rows && !Cell(x, y + 1).IsSolid,
                        out var normal,
                        out var depth)) continue;

                var velocity = (particle.Position - particle.PreviousPosition) / dt;
                var normalSpeed = MathF.Max(0f, -Vector2.Dot(velocity, normal));
                strongestImpact = MathF.Max(strongestImpact, normalSpeed);
                particle.Position += normal * depth;
                particle.Contacting = true;
                particle.ContactMemory = 6;

                var tangent = velocity - Vector2.Dot(velocity, normal) * normal;
                var rebound = normalSpeed > 110f ? normal * normalSpeed * 0.12f : Vector2.Zero;
                // Soft tissue keeps meaningful tangential momentum on terrain; the
                // body's own damping and deformation provide gradual rolling friction.
                var correctedVelocity = tangent * 0.94f + rebound;
                particle.PreviousPosition = particle.Position - correctedVelocity * dt;
                if (normal.Y < -0.55f)
                {
                    particle.Supported = true;
                    particle.SupportMemory = 10;
                }
                hit = true;
                hitX = x;
                hitY = y;
                hitNormal = normal;
                hitPoint = particle.Position - normal * particle.Radius;
            }
        }

        return new CollisionResult(hit, hitX, hitY, strongestImpact, hitPoint, hitNormal);
    }

    public bool ApplyImpactDamage(int x, int y, float impact)
    {
        if (!InBounds(x, y)) return false;
        var index = y * Columns + x;
        if (!_damagedThisStep.Add(index)) return false;
        ref var cell = ref _cells[index];
        if (!cell.IsDestructible || impact < 150f) return false;

        var materialScale = cell.Material == CellMaterial.Glass ? 0.34f : 0.17f;
        cell.Health -= (impact - 120f) * materialScale;
        if (cell.Health > 0f) return false;
        cell = default;
        DestroyedCellCount++;
        SurfaceRevision++;
        return true;
    }

    public void CarveCircle(Vector2 center, float radius, float damage)
    {
        var minX = Math.Clamp((int)((center.X - radius) / CellSize), 0, Columns - 1);
        var maxX = Math.Clamp((int)((center.X + radius) / CellSize), 0, Columns - 1);
        var minY = Math.Clamp((int)((center.Y - radius) / CellSize), 0, Rows - 1);
        var maxY = Math.Clamp((int)((center.Y + radius) / CellSize), 0, Rows - 1);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                ref var cell = ref Cell(x, y);
                if (!cell.IsDestructible) continue;
                var cellCenter = new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);
                if (Vector2.DistanceSquared(cellCenter, center) > radius * radius) continue;
                cell.Health -= damage;
                if (cell.Health <= 0f)
                {
                    cell = default;
                    DestroyedCellCount++;
                    SurfaceRevision++;
                }
            }
        }
    }

    public void BuildSampleArena()
    {
        Array.Clear(_cells);
        SurfaceRevision++;
        _bloodStains.Clear();
        _pendingBloodFlows.Clear();
        DestroyedCellCount = 0;

        for (var x = 0; x < Columns; x++)
        {
            Set(x, Rows - 1, CellMaterial.Steel);
            if (x % 3 != 1) Set(x, Rows - 2, CellMaterial.Steel);
        }
        for (var y = 0; y < Rows; y++)
        {
            Set(0, y, CellMaterial.Steel);
            Set(Columns - 1, y, CellMaterial.Steel);
        }

        for (var x = 20; x <= 22; x++)
        for (var y = 7; y < Rows - 2; y++)
            Set(x, y, CellMaterial.Concrete);

        for (var x = 28; x <= 29; x++)
        for (var y = 3; y < 12; y++)
            Set(x, y, CellMaterial.Glass);

        for (var x = 5; x < 12; x++)
            Set(x, 13 + (x % 2), CellMaterial.Concrete);
    }

    public void BuildProcessingStation()
    {
        Array.Clear(_cells);
        SurfaceRevision++;
        _bloodStains.Clear();
        _pendingBloodFlows.Clear();
        DestroyedCellCount = 0;

        for (var x = 0; x < Columns; x++) Set(x, Rows - 1, CellMaterial.Steel);
        for (var y = 0; y < Rows; y++)
        {
            Set(0, y, CellMaterial.Steel);
            Set(Columns - 1, y, CellMaterial.Steel);
        }

        // The chamber now empties into ProcessingLine's shallow powered receiving
        // tub. Keep its whole former tower footprint empty: the tub supplies its
        // own matching angled collision and needs no terrain pedestal beneath it.

        // The cart has a dedicated lower service lane and an opening through
        // the right-hand factory wall. The central volume stays empty for the
        // future blood collection tank beneath the processing line.
        for (var x = 34; x < Columns; x++) Set(x, 18, CellMaterial.Steel);
        // The cart route is a closed service chase. Tile the otherwise unused
        // lower-right void so the route reads as factory structure, not a room.
        for (var x = 35; x < Columns - 1; x++)
        for (var y = 19; y < Rows - 1; y++)
            Set(x, y, CellMaterial.Steel);
        for (var y = 14; y <= 17; y++) Set(Columns - 1, y, CellMaterial.Air);
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows;

    private static bool TryCircleAabb(
        Vector2 center,
        Vector2 previous,
        float radius,
        Vector2 min,
        Vector2 max,
        bool exposedLeft,
        bool exposedRight,
        bool exposedTop,
        bool exposedBottom,
        out Vector2 normal,
        out float depth)
    {
        var closest = Vector2.Clamp(center, min, max);
        var delta = center - closest;
        var distanceSq = delta.LengthSquared();
        if (distanceSq > radius * radius)
        {
            normal = Vector2.Zero;
            depth = 0f;
            return false;
        }

        if (distanceSq > 0.0001f)
        {
            var distance = MathF.Sqrt(distanceSq);
            normal = delta / distance;
            depth = radius - distance;
            return depth > 0f;
        }

        // The center is inside the cell. Never choose an internal tile seam or
        // the outside of the arena as an escape route. Prefer the exposed face
        // crossed by the previous position/motion; this remains stable even when
        // a blob separation correction moved both Verlet positions deep inside.
        var left = center.X - min.X;
        var right = max.X - center.X;
        var top = center.Y - min.Y;
        var bottom = max.Y - center.Y;
        if (exposedTop && previous.Y <= min.Y)
        {
            normal = -Vector2.UnitY;
            depth = radius + top;
            return true;
        }
        if (exposedBottom && previous.Y >= max.Y)
        {
            normal = Vector2.UnitY;
            depth = radius + bottom;
            return true;
        }
        if (exposedLeft && previous.X <= min.X)
        {
            normal = -Vector2.UnitX;
            depth = radius + left;
            return true;
        }
        if (exposedRight && previous.X >= max.X)
        {
            normal = Vector2.UnitX;
            depth = radius + right;
            return true;
        }

        var movement = center - previous;
        if (exposedTop && movement.Y > MathF.Abs(movement.X) * 0.35f)
        {
            normal = -Vector2.UnitY;
            depth = radius + top;
            return true;
        }
        if (exposedBottom && -movement.Y > MathF.Abs(movement.X) * 0.35f)
        {
            normal = Vector2.UnitY;
            depth = radius + bottom;
            return true;
        }
        if (exposedLeft && movement.X > MathF.Abs(movement.Y) * 0.35f)
        {
            normal = -Vector2.UnitX;
            depth = radius + left;
            return true;
        }
        if (exposedRight && -movement.X > MathF.Abs(movement.Y) * 0.35f)
        {
            normal = Vector2.UnitX;
            depth = radius + right;
            return true;
        }

        var nearest = float.MaxValue;
        normal = Vector2.Zero;
        if (exposedLeft && left < nearest) (nearest, normal) = (left, -Vector2.UnitX);
        if (exposedRight && right < nearest) (nearest, normal) = (right, Vector2.UnitX);
        if (exposedTop && top < nearest) (nearest, normal) = (top, -Vector2.UnitY);
        if (exposedBottom && bottom < nearest) (nearest, normal) = (bottom, Vector2.UnitY);
        if (normal == Vector2.Zero)
        {
            // Fully enclosed solid region: reject toward the closest outer cell
            // face as a conservative last resort.
            nearest = MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));
            if (nearest == left) normal = -Vector2.UnitX;
            else if (nearest == right) normal = Vector2.UnitX;
            else if (nearest == top) normal = -Vector2.UnitY;
            else normal = Vector2.UnitY;
        }
        depth = radius + nearest;
        return true;
    }
}

public readonly record struct CollisionResult(
    bool Hit,
    int CellX,
    int CellY,
    float Impact,
    Vector2 ContactPoint,
    Vector2 Normal);
