using System.Numerics;

namespace BlobForge.World;

public enum CellMaterial : byte
{
    Air,
    Steel,
    Concrete,
    Glass
}

public struct MaterialCell
{
    public CellMaterial Material;
    public float Health;

    public readonly bool IsSolid => Material != CellMaterial.Air;
    public readonly bool IsDestructible => Material is CellMaterial.Concrete or CellMaterial.Glass;
}

public struct BloodSurfaceMark
{
    public Vector2 Position;
    public Vector2 SurfaceNormal;
    public float Amount;
    public float Wetness;
    public float Radius;
    public float FlowAccumulator;
    public float RunoffLoad;
    public int CellX;
    public int CellY;
    public bool IsDrip;
    public bool IsRunoffLeader;
    public float LoopCoordinate;
    public byte Variation;

    public readonly float TrailLength
    {
        get
        {
            var retainedPigmentLength = 6f + Amount * 30f + Radius * 2.4f;
            var pooledRunoffLength = 4f + RunoffLoad * 22f;
            return Math.Clamp(MathF.Max(retainedPigmentLength, pooledRunoffLength), 7f, 120f);
        }
    }

    public readonly float VisibleTrailLength
    {
        get
        {
            var variationBand = ((Variation >> 3) & 7) / 7f;
            var variation = IsRunoffLeader
                ? 0.95f + variationBand * 0.10f
                : 0.82f + variationBand * 0.28f;
            return TrailLength * variation;
        }
    }
}

public readonly record struct BloodDripEmission(
    Vector2 Position,
    Vector2 Velocity,
    float Radius,
    byte Variation);
