using System.Numerics;

namespace BlobForge.Physics;

public struct Particle
{
    public Vector2 Position;
    public Vector2 PreviousPosition;
    public Vector2 Acceleration;
    public float InverseMass;
    public float Radius;
    public bool Supported;
    public byte SupportMemory;
    public bool Contacting;
    public byte ContactMemory;

    public readonly Vector2 Velocity(float inverseDt) => (Position - PreviousPosition) * inverseDt;
}
