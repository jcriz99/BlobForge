using System.Numerics;

namespace BlobForge.Physics;

public readonly record struct WoundEvent(Vector2 Position, Vector2 Normal, float Severity);
