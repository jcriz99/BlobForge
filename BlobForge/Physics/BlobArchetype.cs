using System.Numerics;

namespace BlobForge.Physics;

public readonly record struct BlobArchetype(
    float Radius,
    int TargetTissueParticles,
    SimulationMode InitialMode)
{
    public static BlobArchetype Standard { get; } = new(
        Radius: 78f,
        TargetTissueParticles: 61,
        InitialMode: SimulationMode.ReducedTissue);

    public static BlobArchetype ProcessingUnit { get; } = new(
        Radius: 36f,
        TargetTissueParticles: 61,
        InitialMode: SimulationMode.ReducedTissue);

    public SoftBody Create(Vector2 position)
    {
        var body = new SoftBody(position, Radius, TargetTissueParticles);
        body.SetMode(InitialMode);
        return body;
    }
}
