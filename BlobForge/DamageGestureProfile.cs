using System.Numerics;
using BlobForge.Physics;

namespace BlobForge;

internal static class DamageGestureProfile
{
    public const float DragThreshold = 7f;
    public const float SliceThickness = 3.25f;
    public const float SliceDamage = 2.2f;
    public const float SliceTerrainRadius = 11f;
    public const float BiteRadius = 14f;
    public const float BiteDamage = 1.8f;
    public const float BiteTerrainRadius = 26f;

    public static int Slice(SoftBody body, Vector2 start, Vector2 end)
        => body.DamageLine(start, end, SliceThickness, SliceDamage);

    public static int SlicePath(SoftBody body, IReadOnlyList<Vector2> path)
        => body.DamagePath(path, SliceThickness, SliceDamage);

    public static int Bite(SoftBody body, Vector2 point)
        => body.DamageLine(point, point, BiteRadius, BiteDamage);
}
