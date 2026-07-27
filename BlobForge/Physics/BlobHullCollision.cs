using System.Numerics;
using System.Buffers;
using System.Runtime.CompilerServices;
using BlobForge.World;

namespace BlobForge.Physics;

internal static class BlobHullCollision
{
    private const float PassiveAllowedOverlap = 0.85f;
    internal const float GrabCompressionFraction = 0.78f;
    private static readonly ConditionalWeakTable<SoftBody, HullScratch> HullScratchByBody = new();
    private static readonly IComparer<Vector2> PointComparer = Comparer<Vector2>.Create(static (a, b) =>
    {
        var x = a.X.CompareTo(b.X);
        return x != 0 ? x : a.Y.CompareTo(b.Y);
    });

    public static int ResolveAll(IReadOnlyList<SoftBody> bodies, float dt,
        float correctionBudgetScale = 1f, OverheadTubeFeed? tubeFeed = null)
    {
        var anyAwake = false;
        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            if (bodies[bodyIndex].IsSleeping) continue;
            anyAwake = true;
            break;
        }
        if (!anyAwake) return 0;

        // Hull construction is intentionally lazy. Most factory blobs are far
        // apart, so eagerly rebuilding every contour during every XPBD solver
        // iteration spent substantial time and allocations on pairs that could
        // never touch. The same narrow-phase hull is still used whenever the
        // cheap center-radius broad phase says a contact is possible.
        var hulls = ArrayPool<HullView>.Shared.Rent(Math.Max(1, bodies.Count));
        var centers = ArrayPool<Vector2>.Shared.Rent(Math.Max(1, bodies.Count));
        var averageVelocities = ArrayPool<Vector2>.Shared.Rent(Math.Max(1, bodies.Count));
        Array.Clear(hulls, 0, bodies.Count);
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
            bodies, centers.AsSpan(0, bodies.Count), 1.25f,
            excludeDetachedDebris: false, tubeFeed: tubeFeed,
            candidateCount: out var candidateCount);
        for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            var i = BlobBodyBroadPhase.FirstBodyIndex(candidate);
            var j = BlobBodyBroadPhase.SecondBodyIndex(candidate);
            var a = bodies[i];
            var b = bodies[j];
            if (tubeFeed is not null && tubeFeed.Contains(a) != tubeFeed.Contains(b)) continue;
            if (a.IsSleeping && b.IsSleeping) continue;
            var centerDelta = centers[j] - centers[i];
            var broadphaseRadius = (a.Radius + b.Radius) * 1.25f;
            if (centerDelta.LengthSquared() > broadphaseRadius * broadphaseRadius) continue;
            if (hulls[i].Count == 0) hulls[i] = BuildHullView(a);
            if (hulls[j].Count == 0) hulls[j] = BuildHullView(b);
            var hullA = hulls[i];
            var hullB = hulls[j];
            if (!TryGetPenetration(hullA.Span, hullB.Span, centerDelta, out var axis, out var depth)) continue;
            // The hull guard is an anti-intertwining backstop, not a rigid
            // collider. During deliberate manipulation, allow one controlled
            // layer of shell compression so local particles and tissue areas
            // can visibly mush before the deep-overlap guard engages.
            var allowedOverlap = a.IsGrabbed || b.IsGrabbed
                ? MathF.Min(a.ParticleSpacing, b.ParticleSpacing) * GrabCompressionFraction
                : PassiveAllowedOverlap;
            if (depth <= allowedOverlap) continue;

            var approachSpeed = -Vector2.Dot(averageVelocities[j] - averageVelocities[i], axis);
            if ((a.IsSleeping || b.IsSleeping) && (approachSpeed > 46f || depth > 3.5f))
            {
                a.Wake();
                b.Wake();
            }

            var inverseMassA = a.IsSleeping ? 0f : (a.IsGrabbed ? 2f : 1f) / Math.Max(1, a.Particles.Length);
            var inverseMassB = b.IsSleeping ? 0f : (b.IsGrabbed ? 2f : 1f) / Math.Max(1, b.Particles.Length);
            if (-axis.Y > 0.18f && a.HasGroundSupport) inverseMassA *= 0.35f;
            if (axis.Y > 0.18f && b.HasGroundSupport) inverseMassB *= 0.35f;
            var inverseMass = inverseMassA + inverseMassB;
            if (inverseMass <= 0f) continue;

            var maximumCorrection = MathF.Min(a.ParticleSpacing, b.ParticleSpacing) *
                                    (a.IsGrabbed || b.IsGrabbed ? 0.28f : 0.42f) *
                                    Math.Clamp(correctionBudgetScale, 0.02f, 1f);
            var correctionMagnitude = MathF.Min(depth - allowedOverlap, maximumCorrection);
            var correctionA = -axis * correctionMagnitude * (inverseMassA / inverseMass);
            var correctionB = axis * correctionMagnitude * (inverseMassB / inverseMass);
            a.ApplyTranslation(correctionA, preserveVelocity: true);
            b.ApplyTranslation(correctionB, preserveVelocity: true);
            a.YieldGrabTargetToSeparation(correctionA);
            b.YieldGrabTargetToSeparation(correctionB);
            centers[i] += correctionA;
            centers[j] += correctionB;
            TranslateHull(hullA.Span, correctionA);
            TranslateHull(hullB.Span, correctionB);
            a.MarkSupportedByBody(axis);
            b.MarkSupportedByBody(-axis);
            if (a.IsGrabbed && !b.IsGrabbed) b.ApplyPressureDamping(axis, dt);
            else if (b.IsGrabbed && !a.IsGrabbed) a.ApplyPressureDamping(-axis, dt);
            contacts++;
        }
        return contacts;
        }
        finally
        {
            if (candidates is not null) ArrayPool<ulong>.Shared.Return(candidates);
            Array.Clear(hulls, 0, bodies.Count);
            ArrayPool<HullView>.Shared.Return(hulls);
            ArrayPool<Vector2>.Shared.Return(centers);
            ArrayPool<Vector2>.Shared.Return(averageVelocities);
        }
    }

    public static Vector2[] BuildHull(SoftBody body)
    {
        var view = BuildHullView(body);
        var result = new Vector2[view.Count];
        view.Span.CopyTo(result);
        return result;
    }

    private static HullView BuildHullView(SoftBody body)
    {
        var scratch = HullScratchByBody.GetValue(body, static _ => new HullScratch());
        var shell = BlobContourBuilder.BuildShell(body).Points;
        var pointCount = shell.Length;
        if (pointCount < 3)
        {
            pointCount = 0;
            for (var i = 0; i < body.Particles.Length; i++)
                if (body.IsPhysicalParticle(i)) pointCount++;
            scratch.EnsurePointCapacity(pointCount);
            var pointIndex = 0;
            for (var i = 0; i < body.Particles.Length; i++)
                if (body.IsPhysicalParticle(i)) scratch.Points[pointIndex++] = body.Particles[i].Position;
        }
        else
        {
            scratch.EnsurePointCapacity(pointCount);
            shell.CopyTo(scratch.Points, 0);
        }
        if (pointCount <= 2) return new HullView(scratch.Points, pointCount);
        Array.Sort(scratch.Points, 0, pointCount, PointComparer);

        static float Cross(Vector2 origin, Vector2 a, Vector2 b)
            => (a.X - origin.X) * (b.Y - origin.Y) - (a.Y - origin.Y) * (b.X - origin.X);

        scratch.EnsureHullCapacity(pointCount * 2);
        var hull = scratch.Hull;
        var hullCount = 0;
        for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            var point = scratch.Points[pointIndex];
            while (hullCount >= 2 && Cross(hull[hullCount - 2], hull[hullCount - 1], point) <= 0f) hullCount--;
            hull[hullCount++] = point;
        }
        var lowerCount = hullCount;
        for (var i = pointCount - 2; i >= 0; i--)
        {
            var point = scratch.Points[i];
            while (hullCount > lowerCount && Cross(hull[hullCount - 2], hull[hullCount - 1], point) <= 0f) hullCount--;
            hull[hullCount++] = point;
        }
        hullCount--;
        return new HullView(hull, hullCount);
    }

    public static bool TryGetPenetration(SoftBody a, SoftBody b, out Vector2 axis, out float depth)
    {
        var hullA = BuildHullView(a);
        var hullB = BuildHullView(b);
        return TryGetPenetration(hullA.Span, hullB.Span, b.Center - a.Center, out axis, out depth);
    }

    private static bool TryGetPenetration(
        ReadOnlySpan<Vector2> a,
        ReadOnlySpan<Vector2> b,
        Vector2 centerDelta,
        out Vector2 minimumAxis,
        out float minimumDepth)
    {
        minimumAxis = Vector2.Zero;
        minimumDepth = float.MaxValue;
        if (a.Length < 3 || b.Length < 3) return false;
        if (!TestAxes(a, a, b, ref minimumAxis, ref minimumDepth) ||
            !TestAxes(b, a, b, ref minimumAxis, ref minimumDepth)) return false;
        if (Vector2.Dot(minimumAxis, centerDelta) < 0f) minimumAxis = -minimumAxis;
        return true;
    }

    private static bool TestAxes(
        ReadOnlySpan<Vector2> source,
        ReadOnlySpan<Vector2> a,
        ReadOnlySpan<Vector2> b,
        ref Vector2 minimumAxis,
        ref float minimumDepth)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var edge = source[(i + 1) % source.Length] - source[i];
            if (edge.LengthSquared() < 0.0001f) continue;
            var axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));
            Project(a, axis, out var minA, out var maxA);
            Project(b, axis, out var minB, out var maxB);
            var overlap = MathF.Min(maxA, maxB) - MathF.Max(minA, minB);
            if (overlap <= 0f) return false;

            var aContainsB = minA <= minB && maxA >= maxB;
            var bContainsA = minB <= minA && maxB >= maxA;
            if (aContainsB || bContainsA)
            {
                overlap += MathF.Min(MathF.Abs(minA - minB), MathF.Abs(maxA - maxB));
            }
            if (overlap >= minimumDepth) continue;
            minimumDepth = overlap;
            minimumAxis = axis;
        }
        return true;
    }

    private static void Project(ReadOnlySpan<Vector2> polygon, Vector2 axis, out float min, out float max)
    {
        min = max = Vector2.Dot(polygon[0], axis);
        for (var i = 1; i < polygon.Length; i++)
        {
            var projection = Vector2.Dot(polygon[i], axis);
            min = MathF.Min(min, projection);
            max = MathF.Max(max, projection);
        }
    }

    private static void TranslateHull(Span<Vector2> hull, Vector2 correction)
    {
        for (var i = 0; i < hull.Length; i++) hull[i] += correction;
    }

    private readonly struct HullView
    {
        public HullView(Vector2[] points, int count)
        {
            Points = points;
            Count = count;
        }

        public Vector2[] Points { get; }
        public int Count { get; }
        public Span<Vector2> Span => Points.AsSpan(0, Count);
    }

    private sealed class HullScratch
    {
        public Vector2[] Points = Array.Empty<Vector2>();
        public Vector2[] Hull = Array.Empty<Vector2>();

        public void EnsurePointCapacity(int count)
        {
            if (Points.Length < count) Array.Resize(ref Points, count);
        }

        public void EnsureHullCapacity(int count)
        {
            if (Hull.Length < count) Array.Resize(ref Hull, count);
        }
    }
}
