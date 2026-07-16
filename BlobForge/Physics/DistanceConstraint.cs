namespace BlobForge.Physics;

public struct DistanceConstraint
{
    public int A;
    public int B;
    public float RestLength;
    public float Compliance;
    public float Health;
    public bool Broken;
    public float Lambda;

    public DistanceConstraint(int a, int b, float restLength, float compliance, float health = 1f)
    {
        A = a;
        B = b;
        RestLength = restLength;
        Compliance = compliance;
        Health = health;
        Broken = false;
        Lambda = 0f;
    }
}
