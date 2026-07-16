using System.Numerics;

namespace BlobForge.Physics;

public struct AreaConstraint
{
    public int A;
    public int B;
    public int C;
    public float RestArea;
    public float Compliance;
    public float Lambda;
    public bool Broken;

    public AreaConstraint(int a, int b, int c, float restArea, float compliance)
    {
        A = a;
        B = b;
        C = c;
        RestArea = restArea;
        Compliance = compliance;
        Lambda = 0f;
        Broken = false;
    }

    public readonly bool IncludesEdge(int first, int second)
    {
        return Has(first) && Has(second);
    }

    public readonly bool Has(int particle) => A == particle || B == particle || C == particle;

    public static float SignedArea(Vector2 a, Vector2 b, Vector2 c)
        => 0.5f * ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X));
}
