using System.Numerics;
using System.Buffers;

namespace BlobForge.Physics;

internal static class BlobHullCollision
{
    private const float PassiveAllowedOverlap = 0.85f;
    internal const float GrabCompressionFraction = 0.78f;

    public static int ResolveAll(IReadOnlyList<SoftBody> bodies, float dt, float correctionBudgetScale = 1f)
    {
        // Hull construction is intentionally lazy. Most factory blobs are far
        // apart, so eagerly rebuilding every contour during every XPBD solver
        // iteration spent substantial time and allocations on pairs that could
        // never touch. The same narrow-phase hull is still used whenever the
        // cheap center-radius broad phase says a contact is possible.
        var hulls = new Vector2[bodies.Count][];

        var contacts = 0;
        for (var i = 0; i < bodies.Count; i++)
        for (var j = i + 1; j < bodies.Count; j++)
        {
            var a = bodies[i];
            var b = bodies[j];
            if (a.IsSleeping && b.IsSleeping) continue;
            var broadphaseRadius = (a.Radius + b.Radius) * 1.25f;
            if (Vector2.DistanceSquared(a.Center, b.Center) > broadphaseRadius * broadphaseRadius) continue;
            var hullA = hulls[i] ??= BuildHull(a);
            var hullB = hulls[j] ??= BuildHull(b);
            if (!TryGetPenetration(hullA, hullB, b.Center - a.Center, out var axis, out var depth)) continue;
            // The hull guard is an anti-intertwining backstop, not a rigid
            // collider. During deliberate manipulation, allow one controlled
            // layer of shell compression so local particles and tissue areas
            // can visibly mush before the deep-overlap guard engages.
            var allowedOverlap = a.IsGrabbed || b.IsGrabbed
                ? MathF.Min(a.ParticleSpacing, b.ParticleSpacing) * GrabCompressionFraction
                : PassiveAllowedOverlap;
            if (depth <= allowedOverlap) continue;

            var approachSpeed = -Vector2.Dot(b.AverageVelocity(dt) - a.AverageVelocity(dt), axis);
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
            TranslateHull(hullA, correctionA);
            TranslateHull(hullB, correctionB);
            a.MarkSupportedByBody(axis);
            b.MarkSupportedByBody(-axis);
            if (a.IsGrabbed && !b.IsGrabbed) b.ApplyPressureDamping(axis, dt);
            else if (b.IsGrabbed && !a.IsGrabbed) a.ApplyPressureDamping(-axis, dt);
            contacts++;
        }
        return contacts;
    }

    public static Vector2[] BuildHull(SoftBody body)
    {
        var points = BlobContourBuilder.BuildShell(body).Points;
        if (points.Length < 3)
        {
            var physicalCount = 0;
            for (var i = 0; i < body.Particles.Length; i++)
                if (body.IsPhysicalParticle(i)) physicalCount++;
            points = new Vector2[physicalCount];
            var pointIndex = 0;
            for (var i = 0; i < body.Particles.Length; i++)
                if (body.IsPhysicalParticle(i)) points[pointIndex++] = body.Particles[i].Position;
        }
        if (points.Length <= 2) return points;
        Array.Sort(points, static (a, b) =>
        {
            var x = a.X.CompareTo(b.X);
            return x != 0 ? x : a.Y.CompareTo(b.Y);
        });

        static float Cross(Vector2 origin, Vector2 a, Vector2 b)
            => (a.X - origin.X) * (b.Y - origin.Y) - (a.Y - origin.Y) * (b.X - origin.X);

        var hull = ArrayPool<Vector2>.Shared.Rent(points.Length * 2);
        var hullCount = 0;
        foreach (var point in points)
        {
            while (hullCount >= 2 && Cross(hull[hullCount - 2], hull[hullCount - 1], point) <= 0f) hullCount--;
            hull[hullCount++] = point;
        }
        var lowerCount = hullCount;
        for (var i = points.Length - 2; i >= 0; i--)
        {
            var point = points[i];
            while (hullCount > lowerCount && Cross(hull[hullCount - 2], hull[hullCount - 1], point) <= 0f) hullCount--;
            hull[hullCount++] = point;
        }
        hullCount--;
        var result = new Vector2[hullCount];
        Array.Copy(hull, result, hullCount);
        ArrayPool<Vector2>.Shared.Return(hull);
        return result;
    }

    public static bool TryGetPenetration(SoftBody a, SoftBody b, out Vector2 axis, out float depth)
        => TryGetPenetration(BuildHull(a), BuildHull(b), b.Center - a.Center, out axis, out depth);

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

    private static void TranslateHull(Vector2[] hull, Vector2 correction)
    {
        for (var i = 0; i < hull.Length; i++) hull[i] += correction;
    }
}
