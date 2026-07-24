using System.Numerics;
using System.Buffers;
using BlobForge.World;

namespace BlobForge.Physics;

internal sealed class BlobParticleSpatialHash
{
    private const float CellSize = 34f;
    private readonly Dictionary<long, List<ParticleHandle>> _buckets = new();
    private readonly List<List<ParticleHandle>> _activeBuckets = new(64);

    public int BuildAndResolve(IReadOnlyList<SoftBody> bodies, float dt,
        OverheadTubeFeed? tubeFeed = null)
    {
        foreach (var bucket in _activeBuckets) bucket.Clear();
        _activeBuckets.Clear();
        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            var body = bodies[bodyIndex];
            for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
            {
                if (!body.IsSurfaceParticle(particleIndex)) continue;
                var cell = Cell(body.Particles[particleIndex].Position);
                var key = Key(cell.X, cell.Y);
                if (!_buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<ParticleHandle>(12);
                    _buckets[key] = bucket;
                }
                if (bucket.Count == 0) _activeBuckets.Add(bucket);
                bucket.Add(new ParticleHandle(bodyIndex, particleIndex));
            }
        }

        var contacts = 0;
        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            var bodyA = bodies[bodyIndex];
            for (var particleIndex = 0; particleIndex < bodyA.Particles.Length; particleIndex++)
            {
                if (!bodyA.IsSurfaceParticle(particleIndex)) continue;
                ref var a = ref bodyA.Particles[particleIndex];
                var cell = Cell(a.Position);
                for (var y = cell.Y - 1; y <= cell.Y + 1; y++)
                for (var x = cell.X - 1; x <= cell.X + 1; x++)
                {
                    if (!_buckets.TryGetValue(Key(x, y), out var bucket)) continue;
                    foreach (var handle in bucket)
                    {
                        if (handle.BodyIndex <= bodyIndex) continue;
                        var bodyB = bodies[handle.BodyIndex];
                        if (tubeFeed is not null && tubeFeed.Contains(bodyA) != tubeFeed.Contains(bodyB)) continue;
                        ref var b = ref bodyB.Particles[handle.ParticleIndex];
                        if (!ResolvePair(bodyA, ref a, bodyB, ref b, dt)) continue;
                        contacts++;
                    }
                }
            }
        }
        return contacts + ResolveBodyGuards(bodies, dt, tubeFeed);
    }

    private static int ResolveBodyGuards(IReadOnlyList<SoftBody> bodies, float dt,
        OverheadTubeFeed? tubeFeed)
    {
        var centers = ArrayPool<Vector2>.Shared.Rent(Math.Max(1, bodies.Count));
        var averageVelocities = ArrayPool<Vector2>.Shared.Rent(Math.Max(1, bodies.Count));
        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            centers[bodyIndex] = bodies[bodyIndex].Center;
            averageVelocities[bodyIndex] = bodies[bodyIndex].AverageVelocity(dt);
        }
        var contacts = 0;
        ulong[]? candidates = null;
        try
        {
        candidates = BlobBodyBroadPhase.RentCandidatePairs(
            bodies, centers.AsSpan(0, bodies.Count), 0.68f,
            excludeDetachedDebris: true, tubeFeed: tubeFeed,
            candidateCount: out var candidateCount);
        for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            var i = BlobBodyBroadPhase.FirstBodyIndex(candidate);
            var j = BlobBodyBroadPhase.SecondBodyIndex(candidate);
            var a = bodies[i];
            var b = bodies[j];
            if (tubeFeed is not null && tubeFeed.Contains(a) != tubeFeed.Contains(b)) continue;
            if (a.IsDetachedDebris || b.IsDetachedDebris) continue;
            if (a.IsSleeping && b.IsSleeping) continue;
            var delta = centers[j] - centers[i];
            var minimumDistance = (a.Radius + b.Radius) * 0.68f;
            var distanceSq = delta.LengthSquared();
            if (distanceSq >= minimumDistance * minimumDistance) continue;

            var distance = MathF.Sqrt(MathF.Max(0.0001f, distanceSq));
            var normal = distanceSq < 0.0001f ? Vector2.UnitX : delta / distance;
            var approachSpeed = -Vector2.Dot(averageVelocities[j] - averageVelocities[i], normal);
            var penetration = minimumDistance - distance;
            if ((a.IsSleeping || b.IsSleeping) && (approachSpeed > 52f || penetration > 4f))
            {
                a.Wake();
                b.Wake();
            }

            var inverseMassA = a.IsSleeping ? 0f : 1f / Math.Max(1, a.Particles.Length);
            var inverseMassB = b.IsSleeping ? 0f : 1f / Math.Max(1, b.Particles.Length);
            if (-normal.Y > 0.18f && a.HasGroundSupport) inverseMassA *= 0.35f;
            if (normal.Y > 0.18f && b.HasGroundSupport) inverseMassB *= 0.35f;
            var inverseMass = inverseMassA + inverseMassB;
            if (inverseMass <= 0f) continue;
            var amount = MathF.Min(penetration * 0.18f, 2f);
            var correctionA = -normal * amount * (inverseMassA / inverseMass);
            var correctionB = normal * amount * (inverseMassB / inverseMass);
            a.ApplyTranslation(correctionA, preserveVelocity: true);
            b.ApplyTranslation(correctionB, preserveVelocity: true);
            a.YieldGrabTargetToSeparation(correctionA);
            b.YieldGrabTargetToSeparation(correctionB);
            centers[i] += correctionA;
            centers[j] += correctionB;
            a.MarkSupportedByBody(normal);
            b.MarkSupportedByBody(-normal);
            contacts++;
        }
        return contacts;
        }
        finally
        {
            if (candidates is not null) ArrayPool<ulong>.Shared.Return(candidates);
            ArrayPool<Vector2>.Shared.Return(centers);
            ArrayPool<Vector2>.Shared.Return(averageVelocities);
        }
    }

    private static bool ResolvePair(SoftBody bodyA, ref Particle a, SoftBody bodyB, ref Particle b, float dt)
    {
        if (bodyA.IsSleeping && bodyB.IsSleeping) return false;
        var delta = b.Position - a.Position;
        var minDistance = (a.Radius + b.Radius) * 0.92f;
        var distanceSq = delta.LengthSquared();
        if (distanceSq >= minDistance * minDistance) return false;

        var distance = MathF.Sqrt(MathF.Max(0.0001f, distanceSq));
        var normal = distanceSq < 0.0001f ? Vector2.UnitX : delta / distance;
        var penetration = minDistance - distance;
        var velocityA = (a.Position - a.PreviousPosition) / dt;
        var velocityB = (b.Position - b.PreviousPosition) / dt;
        var relative = velocityB - velocityA;
        var approachSpeed = -Vector2.Dot(relative, normal);
        if (approachSpeed > 0f)
        {
            if (approachSpeed >= bodyA.LastImpact) bodyA.LastImpactPoint = a.Position;
            if (approachSpeed >= bodyB.LastImpact) bodyB.LastImpactPoint = b.Position;
            bodyA.LastImpact = MathF.Max(bodyA.LastImpact, approachSpeed);
            bodyB.LastImpact = MathF.Max(bodyB.LastImpact, approachSpeed);
            if (bodyA.ParentId != bodyB.ParentId)
            {
                bodyA.RecordBreakupImpact(a.Position, approachSpeed);
                bodyB.RecordBreakupImpact(b.Position, approachSpeed);
            }
        }

        if ((bodyA.IsSleeping || bodyB.IsSleeping) && (approachSpeed > 52f || penetration > 4f))
        {
            bodyA.Wake();
            bodyB.Wake();
        }

        var inverseMassA = bodyA.IsSleeping ? 0f : a.InverseMass;
        var inverseMassB = bodyB.IsSleeping ? 0f : b.InverseMass;
        if (-normal.Y > 0.18f && bodyA.HasGroundSupport) inverseMassA *= 0.35f;
        if (normal.Y > 0.18f && bodyB.HasGroundSupport) inverseMassB *= 0.35f;
        var inverseMass = inverseMassA + inverseMassB;
        if (inverseMass <= 0f) return false;

        var correctionMagnitude = MathF.Min(penetration * 0.24f, minDistance * 0.065f);
        var correction = normal * correctionMagnitude;
        var correctionA = correction * (inverseMassA / inverseMass);
        var correctionB = correction * (inverseMassB / inverseMass);
        a.Position -= correctionA;
        a.PreviousPosition -= correctionA;
        b.Position += correctionB;
        b.PreviousPosition += correctionB;
        a.Contacting = true;
        a.ContactMemory = 6;
        b.Contacting = true;
        b.ContactMemory = 6;

        if (normal.Y > 0.5f)
        {
            a.Supported = true;
            a.SupportMemory = 10;
        }
        else if (normal.Y < -0.5f)
        {
            b.Supported = true;
            b.SupportMemory = 10;
        }

        if (approachSpeed > 0f)
        {
            // Blob-on-blob contact is deliberately near-inelastic. Removing most of
            // the closing speed prevents constraint recovery from becoming a springy
            // launch while still leaving tangential squish and sliding intact.
            var impulseScale = bodyA.IsGrabbed ^ bodyB.IsGrabbed ? 0.08f : 0.42f;
            var impulse = normal * MathF.Min(approachSpeed, 180f) * impulseScale;
            a.PreviousPosition += impulse * dt;
            b.PreviousPosition -= impulse * dt;
        }
        if (bodyA.IsGrabbed && !bodyB.IsGrabbed)
            bodyB.ApplyPressureDamping(bodyB.Center - bodyA.Center, dt);
        else if (bodyB.IsGrabbed && !bodyA.IsGrabbed)
            bodyA.ApplyPressureDamping(bodyA.Center - bodyB.Center, dt);
        return true;
    }

    private static (int X, int Y) Cell(Vector2 position)
        => ((int)MathF.Floor(position.X / CellSize), (int)MathF.Floor(position.Y / CellSize));

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

    private readonly record struct ParticleHandle(int BodyIndex, int ParticleIndex);
}
