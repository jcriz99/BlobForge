using System.Numerics;
using System.Runtime.CompilerServices;

namespace BlobForge.Physics;

/// <summary>
/// Builds the single authoritative outer material contour used by rendering and
/// broad-phase blob collision. A body can temporarily contain point-touching cell
/// islands after damage; only edge-connected tissue is allowed to define one skin.
/// </summary>
internal static class BlobContourBuilder
{
    private const float SkinExpansion = 0.48f;
    private static readonly ConditionalWeakTable<SoftBody, ContourPolicy> Policies = new();
    internal static int TopologyPlanBuildCount { get; private set; }

    internal static void ResetDiagnostics() => TopologyPlanBuildCount = 0;

    internal readonly record struct Contour(Vector2[] Points, bool[] WoundPoints, int[] ParticleIndices)
    {
        public static Contour Empty => new(Array.Empty<Vector2>(), Array.Empty<bool>(), Array.Empty<int>());
    }

    public static Contour BuildShell(SoftBody body)
    {
        var cohesiveDamage = !body.IsCrumbling && (body.HasLocalDamage || body.IsDetachedDebris);
        var healthyConvexHull = !body.IsCrumbling && !cohesiveDamage;
        var contour = body.IsCrumbling
            ? BuildActiveHull(body)
            : cohesiveDamage
                ? BuildCohesiveContour(body)
                : BuildHealthyHull(body);
        if (contour.Points.Length < 3 && cohesiveDamage)
        {
            var policy = Policies.GetOrCreateValue(body);
            policy.ForceCohesiveHull = true;
            contour = BuildCohesiveContour(body);
        }
        if (contour.Points.Length < 3) contour = BuildActiveHull(body);
        if (contour.Points.Length < 3 ||
            (healthyConvexHull ? !HasFiniteArea(contour.Points) : !IsValid(contour.Points)))
            return Contour.Empty;

        var center = body.Center;
        // Every builder below returns an owned point array. Expand that array in
        // place instead of cloning one more contour-sized buffer on every render
        // and collision query.
        var shell = contour.Points;
        for (var i = 0; i < shell.Length; i++)
        {
            if (contour.WoundPoints.Length == shell.Length && contour.WoundPoints[i]) continue;
            var direction = shell[i] - center;
            if (direction.LengthSquared() > 0.0001f)
                shell[i] += Vector2.Normalize(direction) * body.ParticleSpacing * SkinExpansion;
        }
        // BuildHealthyHull is a convex hull whose center lies inside it. Moving
        // its vertices outward along their center rays preserves angular order,
        // cannot introduce a crossing, and continues to contain the unexpanded
        // material. The general O(n^2) intersection and containment repair below
        // is required for wound seams and crumbling components, but only repeats
        // already-proven work for the common intact-body path used by rendering
        // and every hull-collision pass.
        if (healthyConvexHull)
            return HasFiniteArea(shell)
                ? new Contour(shell, contour.WoundPoints, contour.ParticleIndices)
                : Contour.Empty;
        if (IsValid(shell) && RepairPhysicalContainment(body, shell, contour.ParticleIndices))
            return new Contour(shell, contour.WoundPoints, contour.ParticleIndices);
        if (cohesiveDamage)
        {
            var policy = Policies.GetOrCreateValue(body);
            if (!policy.ForceCohesiveHull)
            {
                policy.ForceCohesiveHull = true;
                return BuildShell(body);
            }
        }
        return Contour.Empty;
    }

    private static bool RepairPhysicalContainment(SoftBody body, Vector2[] shell, int[] boundaryParticles)
    {
        // Usually every center is already covered and this is just one bounded
        // containment check. Damage smoothing/cut projection can rarely place a
        // boundary a few pixels inward; move only the nearest edge back over that
        // center instead of inflating or rebuilding the whole shape.
        var margin = body.ParticleSpacing * 0.04f;
        for (var pass = 0; pass < 5; pass++)
        {
            var repairedAny = false;
            for (var candidateIndex = 0; candidateIndex < boundaryParticles.Length; candidateIndex++)
            {
                var particleIndex = boundaryParticles[candidateIndex];
                if (!body.IsSurfaceParticle(particleIndex)) continue;
                var point = body.Particles[particleIndex].Position;
                if (ContainsPoint(shell, point)) continue;

                var closestEdge = -1;
                var closestPoint = Vector2.Zero;
                var closestDistanceSquared = float.MaxValue;
                for (var edgeIndex = 0; edgeIndex < shell.Length; edgeIndex++)
                {
                    var a = shell[edgeIndex];
                    var b = shell[(edgeIndex + 1) % shell.Length];
                    var edge = b - a;
                    var lengthSquared = edge.LengthSquared();
                    if (lengthSquared < 0.0001f) continue;
                    var t = Math.Clamp(Vector2.Dot(point - a, edge) / lengthSquared, 0f, 1f);
                    var candidate = a + edge * t;
                    var distanceSquared = Vector2.DistanceSquared(point, candidate);
                    if (distanceSquared >= closestDistanceSquared) continue;
                    closestDistanceSquared = distanceSquared;
                    closestPoint = candidate;
                    closestEdge = edgeIndex;
                }
                if (closestEdge < 0) return false;

                var correction = point - closestPoint;
                var correctionLength = correction.Length();
                if (correctionLength > 0.0001f)
                    correction *= (correctionLength + margin) / correctionLength;
                shell[closestEdge] += correction;
                shell[(closestEdge + 1) % shell.Length] += correction;
                repairedAny = true;
            }
            if (!repairedAny) return true;
            if (!IsValid(shell)) return false;
        }

        for (var candidateIndex = 0; candidateIndex < boundaryParticles.Length; candidateIndex++)
        {
            var particleIndex = boundaryParticles[candidateIndex];
            if (body.IsSurfaceParticle(particleIndex) &&
                !ContainsPoint(shell, body.Particles[particleIndex].Position)) return false;
        }
        return true;
    }

    internal static bool ContainsPoint(ReadOnlySpan<Vector2> polygon, Vector2 point, float tolerance = 0f)
    {
        var toleranceSquared = tolerance * tolerance;
        var inside = false;
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            var edge = b - a;
            var lengthSquared = edge.LengthSquared();
            if (lengthSquared > 0.0001f)
            {
                var t = Math.Clamp(Vector2.Dot(point - a, edge) / lengthSquared, 0f, 1f);
                if (Vector2.DistanceSquared(point, a + edge * t) <= toleranceSquared) return true;
            }
            if ((a.Y > point.Y) == (b.Y > point.Y)) continue;
            var crossingX = (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (point.X < crossingX) inside = !inside;
        }
        return inside;
    }

    private static Contour BuildHealthyHull(SoftBody body)
    {
        var policy = Policies.GetOrCreateValue(body);
        if (policy.SurfaceTopologyRevision != body.TopologyRevision)
        {
            var count = 0;
            for (var index = 0; index < body.Particles.Length; index++)
                if (body.IsSurfaceParticle(index)) count++;
            policy.SurfaceParticles = new int[count];
            count = 0;
            for (var index = 0; index < body.Particles.Length; index++)
                if (body.IsSurfaceParticle(index)) policy.SurfaceParticles[count++] = index;
            policy.SurfaceTopologyRevision = body.TopologyRevision;
        }
        return BuildHull(body, policy.SurfaceParticles, policy.SurfaceParticles.Length, policy);
    }

    private static Contour BuildActiveHull(SoftBody body)
    {
        var policy = Policies.GetOrCreateValue(body);
        if (policy.ActiveParticleScratch.Length < body.Particles.Length)
            policy.ActiveParticleScratch = new int[body.Particles.Length];
        var count = 0;
        for (var index = 0; index < body.Particles.Length; index++)
            if (body.IsPhysicalParticle(index)) policy.ActiveParticleScratch[count++] = index;
        return BuildHull(body, policy.ActiveParticleScratch, count, policy);
    }

    private static Contour BuildHull(SoftBody body, int[] indices)
        => BuildHull(body, indices, indices.Length, Policies.GetOrCreateValue(body));

    private static Contour BuildHull(
        SoftBody body,
        int[] indices,
        int indexCount,
        ContourPolicy policy)
    {
        if (indexCount < 3) return Contour.Empty;
        // Boundary populations are small. Allocation-free insertion sorting is
        // faster here than constructing a captured comparer for Array.Sort.
        for (var index = 1; index < indexCount; index++)
        {
            var value = indices[index];
            var cursor = index - 1;
            while (cursor >= 0 && CompareParticlePosition(body, indices[cursor], value) > 0)
            {
                indices[cursor + 1] = indices[cursor];
                cursor--;
            }
            indices[cursor + 1] = value;
        }

        var required = indexCount * 2;
        if (policy.HullIndexScratch.Length < required)
            policy.HullIndexScratch = new int[required];
        var hull = policy.HullIndexScratch;
        var hullCount = 0;
        for (var indexPosition = 0; indexPosition < indexCount; indexPosition++)
        {
            var particleIndex = indices[indexPosition];
            while (hullCount >= 2 && Cross(body, hull[hullCount - 2], hull[hullCount - 1], particleIndex) <= 0f)
                hullCount--;
            hull[hullCount++] = particleIndex;
        }
        var lowerCount = hullCount;
        for (var i = indexCount - 2; i >= 0; i--)
        {
            var particleIndex = indices[i];
            while (hullCount > lowerCount &&
                   Cross(body, hull[hullCount - 2], hull[hullCount - 1], particleIndex) <= 0f)
                hullCount--;
            hull[hullCount++] = particleIndex;
        }
        if (hullCount > 1) hullCount--;
        if (policy.HullPointOutput.Length != hullCount)
        {
            policy.HullPointOutput = new Vector2[hullCount];
            policy.HullParticleOutput = new int[hullCount];
        }
        var points = policy.HullPointOutput;
        var particleIndices = policy.HullParticleOutput;
        for (var i = 0; i < hullCount; i++)
        {
            particleIndices[i] = hull[i];
            points[i] = body.Particles[hull[i]].Position;
        }
        return points.Length >= 3
            ? new Contour(points, Array.Empty<bool>(), particleIndices)
            : Contour.Empty;
    }

    private static int CompareParticlePosition(SoftBody body, int a, int b)
    {
        var x = body.Particles[a].Position.X.CompareTo(body.Particles[b].Position.X);
        return x != 0 ? x : body.Particles[a].Position.Y.CompareTo(body.Particles[b].Position.Y);
    }

    private static float Cross(SoftBody body, int origin, int a, int b)
    {
        var o = body.Particles[origin].Position;
        var first = body.Particles[a].Position;
        var second = body.Particles[b].Position;
        return (first.X - o.X) * (second.Y - o.Y) - (first.Y - o.Y) * (second.X - o.X);
    }

    private static Contour BuildCohesiveContour(SoftBody body)
    {
        var policy = Policies.GetOrCreateValue(body);
        if (policy.TopologyRevision != body.TopologyRevision)
        {
            RebuildPolicy(body, policy);
        }
        if (policy.CohesiveParticles.Length < 3) return Contour.Empty;
        // Repeated point bites can leave several intact cell islands touching at
        // one lattice node. They are still one physical body even when the area
        // policy identifies only the largest edge-connected island. Never render
        // just that island while retaining collision nodes from the others.
        if (policy.CohesiveParticles.Length < body.PhysicalParticleCount)
            return BuildActiveHull(body);
        if (policy.ForceCohesiveHull || policy.BoundaryLoop.Length < 3)
            return BuildHull(body, policy.CohesiveParticles);

        var best = policy.BoundaryLoop;
        if (policy.MaterialPointScratch.Length < best.Length)
            policy.MaterialPointScratch = new Vector2[best.Length];
        if (policy.CohesivePointOutput.Length != best.Length)
            policy.CohesivePointOutput = new Vector2[best.Length];
        var points = policy.CohesivePointOutput;
        for (var index = 0; index < best.Length; index++)
            points[index] = policy.MaterialPointScratch[index] = body.Particles[best[index]].Position;
        Array.Clear(policy.ProjectionSum);
        Array.Clear(policy.ProjectionCount);
        foreach (var group in policy.SegmentGroups)
        {
            var segment = body.CurrentWorldCutSegment(group.SegmentIndex);
            var segmentVector = segment.End - segment.Start;
            var segmentLength = segmentVector.Length();
            if (segmentLength < 0.001f) continue;
            var tangent = segmentVector / segmentLength;
            var normal = new Vector2(-tangent.Y, tangent.X);
            var maximumProjection = body.ParticleSpacing * 0.52f;
            var lowerShift = float.MinValue;
            var upperShift = float.MaxValue;
            var minimumSignedDistance = float.MaxValue;
            var maximumSignedDistance = float.MinValue;
            foreach (var vertex in group.Vertices)
            {
                var materialPoint = points[vertex];
                var t = Math.Clamp(Vector2.Dot(materialPoint - segment.Start, tangent), 0f, segmentLength);
                var basePoint = segment.Start + tangent * t;
                var signedDistance = Vector2.Dot(materialPoint - basePoint, normal);
                lowerShift = MathF.Max(lowerShift, signedDistance - maximumProjection);
                upperShift = MathF.Min(upperShift, signedDistance + maximumProjection);
                minimumSignedDistance = MathF.Min(minimumSignedDistance, signedDistance);
                maximumSignedDistance = MathF.Max(maximumSignedDistance, signedDistance);
            }
            var centerT = Math.Clamp(Vector2.Dot(body.Center - segment.Start, tangent), 0f, segmentLength);
            var centerBase = segment.Start + tangent * centerT;
            var centerSide = Vector2.Dot(body.Center - centerBase, normal);
            // Put the straight cut face on the outside of every particle that it
            // represents. Choosing zero here could slice through those contact
            // centers even though the topology and collision nodes were valid.
            var outwardShift = centerSide >= 0f ? minimumSignedDistance : maximumSignedDistance;
            var shift = lowerShift <= upperShift
                ? Math.Clamp(outwardShift, lowerShift, upperShift)
                : (lowerShift + upperShift) * 0.5f;
            foreach (var vertex in group.Vertices)
            {
                var materialPoint = points[vertex];
                var t = Math.Clamp(Vector2.Dot(materialPoint - segment.Start, tangent), 0f, segmentLength);
                var requested = segment.Start + tangent * t + normal * shift;
                var displacement = requested - materialPoint;
                var length = displacement.Length();
                if (length > maximumProjection && length > 0.0001f)
                    requested = materialPoint + displacement * (maximumProjection / length);
                policy.ProjectionSum[vertex] += requested;
                policy.ProjectionCount[vertex]++;
            }
        }
        for (var i = 0; i < points.Length; i++)
            if (policy.ProjectionCount[i] > 0)
                points[i] = policy.ProjectionSum[i] / policy.ProjectionCount[i];

        for (var pass = 0; pass < 4; pass++)
        {
            Array.Copy(points, policy.SmoothingScratch, points.Length);
            for (var i = 0; i < points.Length; i++)
            {
                if (policy.WoundPoints[i]) continue;
                var previous = (i + points.Length - 1) % points.Length;
                var next = (i + 1) % points.Length;
                var strength = policy.WoundPoints[previous] || policy.WoundPoints[next] ? 0.18f : 0.32f;
                points[i] = Vector2.Lerp(
                    policy.SmoothingScratch[i],
                    (policy.SmoothingScratch[previous] + policy.SmoothingScratch[next]) * 0.5f,
                    strength);
            }
        }

        // Smoothing is visual only. Never let it travel farther inward than the
        // later skin expansion can cover, otherwise a real boundary/contact node
        // can end up visibly outside the blob on a sharply damaged corner.
        var maximumSmoothingTravel = body.ParticleSpacing * (SkinExpansion - 0.12f);
        for (var i = 0; i < points.Length; i++)
        {
            if (policy.WoundPoints[i]) continue;
            var displacement = points[i] - policy.MaterialPointScratch[i];
            var length = displacement.Length();
            if (length > maximumSmoothingTravel)
                points[i] = policy.MaterialPointScratch[i] + displacement * (maximumSmoothingTravel / length);
        }

        if (IsValid(points)) return new Contour(points, policy.WoundPoints, policy.BoundaryLoop);
        policy.ForceCohesiveHull = true;
        return BuildHull(body, policy.CohesiveParticles);
    }

    private static void RebuildPolicy(SoftBody body, ContourPolicy policy)
    {
        TopologyPlanBuildCount++;
        policy.TopologyRevision = body.TopologyRevision;
        policy.ForceCohesiveHull = false;
        policy.CohesiveParticles = Array.Empty<int>();
        policy.BoundaryLoop = Array.Empty<int>();
        policy.WoundPoints = Array.Empty<bool>();
        policy.SegmentGroups = Array.Empty<SegmentGroup>();

        var intactAreas = Enumerable.Range(0, body.AreaConstraints.Count)
            .Where(index => !body.AreaConstraints[index].Broken &&
                            body.IsPhysicalParticle(body.AreaConstraints[index].A) &&
                            body.IsPhysicalParticle(body.AreaConstraints[index].B) &&
                            body.IsPhysicalParticle(body.AreaConstraints[index].C))
            .ToArray();
        if (intactAreas.Length == 0) return;

        var parent = Enumerable.Range(0, body.AreaConstraints.Count).ToArray();
        int Find(int value)
        {
            while (parent[value] != value)
            {
                parent[value] = parent[parent[value]];
                value = parent[value];
            }
            return value;
        }
        void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a != b) parent[b] = a;
        }

        var edgeOwner = new Dictionary<MeshEdge, int>();
        foreach (var areaIndex in intactAreas)
        {
            var area = body.AreaConstraints[areaIndex];
            Connect(area.A, area.B);
            Connect(area.B, area.C);
            Connect(area.C, area.A);

            void Connect(int a, int b)
            {
                var edge = new MeshEdge(a, b);
                if (edgeOwner.TryGetValue(edge, out var other)) Union(areaIndex, other);
                else edgeOwner[edge] = areaIndex;
            }
        }

        var component = intactAreas
            .GroupBy(Find)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Min())
            .First()
            .ToArray();
        policy.CohesiveParticles = component
            .SelectMany(index =>
            {
                var area = body.AreaConstraints[index];
                return new[] { area.A, area.B, area.C };
            })
            .Distinct()
            .ToArray();

        var edges = new Dictionary<MeshEdge, BoundaryEdge>();
        foreach (var areaIndex in component)
        {
            var area = body.AreaConstraints[areaIndex];
            if (area.RestArea >= 0f)
            {
                AddEdge(area.A, area.B);
                AddEdge(area.B, area.C);
                AddEdge(area.C, area.A);
            }
            else
            {
                AddEdge(area.A, area.C);
                AddEdge(area.C, area.B);
                AddEdge(area.B, area.A);
            }
        }

        void AddEdge(int from, int to)
        {
            var key = new MeshEdge(from, to);
            if (edges.TryGetValue(key, out var existing)) edges[key] = existing with { Count = existing.Count + 1 };
            else edges[key] = new BoundaryEdge(from, to, 1);
        }

        var boundaryEdges = edges.Values.Where(edge => edge.Count == 1)
            .OrderBy(edge => edge.From)
            .ThenBy(edge => edge.To)
            .ToArray();
        if (boundaryEdges.Length < 3)
        {
            policy.ForceCohesiveHull = true;
            return;
        }

        var outgoing = new Dictionary<int, List<BoundaryEdge>>();
        foreach (var edge in boundaryEdges)
        {
            if (!outgoing.TryGetValue(edge.From, out var list)) outgoing[edge.From] = list = new List<BoundaryEdge>(2);
            list.Add(edge);
        }
        foreach (var list in outgoing.Values) list.Sort((a, b) => a.To.CompareTo(b.To));
        var branchedBoundary = outgoing.Values.Any(list => list.Count != 1) ||
                               boundaryEdges.GroupBy(edge => edge.To).Any(group => group.Count() != 1);
        if (branchedBoundary)
        {
            policy.ForceCohesiveHull = true;
            return;
        }

        var unused = boundaryEdges.Select(edge => (edge.From, edge.To)).ToHashSet();
        var loops = new List<int[]>();
        while (unused.Count > 0)
        {
            var firstPair = unused.OrderBy(edge => edge.From).ThenBy(edge => edge.To).First();
            var start = firstPair.From;
            var previous = start;
            var current = firstPair.To;
            var loop = new List<int> { start };
            unused.Remove(firstPair);
            var closed = false;
            for (var guard = 0; guard <= boundaryEdges.Length + 1; guard++)
            {
                loop.Add(current);
                if (current == start)
                {
                    loop.RemoveAt(loop.Count - 1);
                    closed = true;
                    break;
                }
                if (!outgoing.TryGetValue(current, out var candidates)) break;
                var available = candidates.Where(edge => unused.Contains((edge.From, edge.To))).ToArray();
                if (available.Length == 0) break;
                var next = available.Length == 1
                    ? available[0]
                    : ChooseFaceContinuation(body, previous, current, available);
                unused.Remove((next.From, next.To));
                previous = current;
                current = next.To;
            }
            if (closed && loop.Count >= 3) loops.Add(loop.ToArray());
        }
        if (loops.Count == 0)
        {
            policy.ForceCohesiveHull = true;
            return;
        }

        policy.BoundaryLoop = loops
            .OrderByDescending(loop => loop.Length)
            .ThenBy(loop => loop.Min())
            .First();
        policy.WoundPoints = new bool[policy.BoundaryLoop.Length];
        policy.ProjectionSum = new Vector2[policy.BoundaryLoop.Length];
        policy.ProjectionCount = new int[policy.BoundaryLoop.Length];
        policy.SmoothingScratch = new Vector2[policy.BoundaryLoop.Length];
        var groupedVertices = new Dictionary<int, HashSet<int>>();
        for (var i = 0; i < policy.BoundaryLoop.Length; i++)
        {
            var next = (i + 1) % policy.BoundaryLoop.Length;
            if (!body.TryGetWoundBoundaryBinding(
                    policy.BoundaryLoop[i],
                    policy.BoundaryLoop[next],
                    out var binding)) continue;
            policy.WoundPoints[i] = policy.WoundPoints[next] = true;
            if (!groupedVertices.TryGetValue(binding.SegmentIndex, out var vertices))
                groupedVertices[binding.SegmentIndex] = vertices = new HashSet<int>();
            vertices.Add(i);
            vertices.Add(next);
        }
        policy.SegmentGroups = groupedVertices
            .OrderBy(pair => pair.Key)
            .Select(pair => new SegmentGroup(pair.Key, pair.Value.OrderBy(vertex => vertex).ToArray()))
            .ToArray();
    }

    private static BoundaryEdge ChooseFaceContinuation(
        SoftBody body,
        int previous,
        int current,
        IReadOnlyList<BoundaryEdge> candidates)
    {
        var incomingReverse = body.Particles[previous].Position - body.Particles[current].Position;
        var reverseAngle = MathF.Atan2(incomingReverse.Y, incomingReverse.X);
        var best = candidates[0];
        var bestClockwise = float.MaxValue;
        foreach (var candidate in candidates)
        {
            var outgoing = body.Particles[candidate.To].Position - body.Particles[current].Position;
            var angle = MathF.Atan2(outgoing.Y, outgoing.X);
            var clockwise = reverseAngle - angle;
            while (clockwise < 0f) clockwise += MathF.Tau;
            while (clockwise >= MathF.Tau) clockwise -= MathF.Tau;
            if (clockwise >= bestClockwise) continue;
            bestClockwise = clockwise;
            best = candidate;
        }
        return best;
    }

    internal static bool IsValid(ReadOnlySpan<Vector2> points)
    {
        if (!HasFiniteArea(points)) return false;
        for (var i = 0; i < points.Length; i++)
        {
            var a0 = points[i];
            var a1 = points[(i + 1) % points.Length];
            for (var j = i + 1; j < points.Length; j++)
            {
                if (j == i || j == (i + 1) % points.Length || (j + 1) % points.Length == i) continue;
                if (SegmentsProperlyIntersect(a0, a1, points[j], points[(j + 1) % points.Length])) return false;
            }
        }
        return true;
    }

    private static bool HasFiniteArea(ReadOnlySpan<Vector2> points)
    {
        if (points.Length < 3 || MathF.Abs(PolygonArea(points)) < 1f) return false;
        for (var i = 0; i < points.Length; i++)
            if (!float.IsFinite(points[i].X) || !float.IsFinite(points[i].Y)) return false;
        return true;
    }

    internal static float PolygonArea(ReadOnlySpan<Vector2> points)
    {
        var area = 0f;
        for (var i = 0; i < points.Length; i++)
        {
            var next = (i + 1) % points.Length;
            area += points[i].X * points[next].Y - points[next].X * points[i].Y;
        }
        return area * 0.5f;
    }

    private static bool SegmentsProperlyIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Side(Vector2 p, Vector2 q, Vector2 r)
            => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        var abC = Side(a, b, c);
        var abD = Side(a, b, d);
        var cdA = Side(c, d, a);
        var cdB = Side(c, d, b);
        const float epsilon = 0.001f;
        return abC * abD < -epsilon && cdA * cdB < -epsilon;
    }

    private readonly record struct BoundaryEdge(int From, int To, int Count);
    private readonly record struct SegmentGroup(int SegmentIndex, int[] Vertices);

    private sealed class ContourPolicy
    {
        public int TopologyRevision { get; set; } = -1;
        public int SurfaceTopologyRevision { get; set; } = -1;
        public bool ForceCohesiveHull { get; set; }
        public int[] SurfaceParticles { get; set; } = Array.Empty<int>();
        public int[] ActiveParticleScratch { get; set; } = Array.Empty<int>();
        public int[] HullIndexScratch { get; set; } = Array.Empty<int>();
        public Vector2[] HullPointOutput { get; set; } = Array.Empty<Vector2>();
        public int[] HullParticleOutput { get; set; } = Array.Empty<int>();
        public int[] CohesiveParticles { get; set; } = Array.Empty<int>();
        public int[] BoundaryLoop { get; set; } = Array.Empty<int>();
        public bool[] WoundPoints { get; set; } = Array.Empty<bool>();
        public SegmentGroup[] SegmentGroups { get; set; } = Array.Empty<SegmentGroup>();
        public Vector2[] ProjectionSum { get; set; } = Array.Empty<Vector2>();
        public int[] ProjectionCount { get; set; } = Array.Empty<int>();
        public Vector2[] SmoothingScratch { get; set; } = Array.Empty<Vector2>();
        public Vector2[] MaterialPointScratch { get; set; } = Array.Empty<Vector2>();
        public Vector2[] CohesivePointOutput { get; set; } = Array.Empty<Vector2>();
    }

    private readonly record struct MeshEdge
    {
        public MeshEdge(int a, int b)
        {
            A = Math.Min(a, b);
            B = Math.Max(a, b);
        }

        public int A { get; }
        public int B { get; }
    }
}
