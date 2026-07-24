using System.Buffers;
using System.Numerics;
using BlobForge.World;

namespace BlobForge.Physics;

/// <summary>
/// Builds conservative center/radius body-pair candidates without allocating
/// per simulation pass. Candidate keys are returned in the same body-index
/// order as the former nested loops so contact resolution remains deterministic.
/// </summary>
internal static class BlobBodyBroadPhase
{
    private static readonly IComparer<SweepEntry> SweepEntryComparer =
        Comparer<SweepEntry>.Create(static (a, b) =>
        {
            var minimumX = a.MinimumX.CompareTo(b.MinimumX);
            return minimumX != 0 ? minimumX : a.BodyIndex.CompareTo(b.BodyIndex);
        });

    public static ulong[] RentCandidatePairs(
        IReadOnlyList<SoftBody> bodies,
        ReadOnlySpan<Vector2> centers,
        float radiusScale,
        bool excludeDetachedDebris,
        OverheadTubeFeed? tubeFeed,
        out int candidateCount)
    {
        var bodyCount = bodies.Count;
        var entries = ArrayPool<SweepEntry>.Shared.Rent(Math.Max(1, bodyCount));
        var candidates = ArrayPool<ulong>.Shared.Rent(Math.Max(1, bodyCount * 2));
        candidateCount = 0;

        try
        {
            var entryCount = 0;
            for (var bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
            {
                if (excludeDetachedDebris && bodies[bodyIndex].IsDetachedDebris) continue;
                entries[entryCount++] = new SweepEntry(
                    bodyIndex,
                    centers[bodyIndex].X - bodies[bodyIndex].Radius * radiusScale);
            }
            Array.Sort(entries, 0, entryCount, SweepEntryComparer);

            for (var entryIndex = 0; entryIndex < entryCount; entryIndex++)
            {
                var bodyIndex = entries[entryIndex].BodyIndex;
                var body = bodies[bodyIndex];
                var center = centers[bodyIndex];
                var radius = body.Radius * radiusScale;
                var maximumX = center.X + radius;

                for (var otherEntryIndex = entryIndex + 1;
                     otherEntryIndex < entryCount;
                     otherEntryIndex++)
                {
                    var otherEntry = entries[otherEntryIndex];
                    if (otherEntry.MinimumX > maximumX) break;

                    var otherBodyIndex = otherEntry.BodyIndex;
                    var otherBody = bodies[otherBodyIndex];
                    if (body.IsSleeping && otherBody.IsSleeping) continue;
                    if (tubeFeed is not null &&
                        tubeFeed.Contains(body) != tubeFeed.Contains(otherBody)) continue;
                    var pairRadius = radius + otherBody.Radius * radiusScale;
                    if (MathF.Abs(centers[otherBodyIndex].Y - center.Y) > pairRadius) continue;

                    if (candidateCount == candidates.Length)
                    {
                        var replacement = ArrayPool<ulong>.Shared.Rent(
                            candidates.Length <= int.MaxValue / 2
                                ? candidates.Length * 2
                                : int.MaxValue);
                        candidates.AsSpan(0, candidateCount).CopyTo(replacement);
                        ArrayPool<ulong>.Shared.Return(candidates);
                        candidates = replacement;
                    }

                    var firstIndex = Math.Min(bodyIndex, otherBodyIndex);
                    var secondIndex = Math.Max(bodyIndex, otherBodyIndex);
                    candidates[candidateCount++] =
                        ((ulong)(uint)firstIndex << 32) | (uint)secondIndex;
                }
            }

            // Sweep order is spatial. Restoring index order preserves the exact
            // narrow-phase/correction sequence used by the previous nested loops.
            if (candidateCount > 1) Array.Sort(candidates, 0, candidateCount);
            return candidates;
        }
        catch
        {
            ArrayPool<ulong>.Shared.Return(candidates);
            throw;
        }
        finally
        {
            ArrayPool<SweepEntry>.Shared.Return(entries);
        }
    }

    public static int FirstBodyIndex(ulong candidate) => (int)(candidate >> 32);

    public static int SecondBodyIndex(ulong candidate) => (int)(uint)candidate;

    private readonly record struct SweepEntry(int BodyIndex, float MinimumX);
}
