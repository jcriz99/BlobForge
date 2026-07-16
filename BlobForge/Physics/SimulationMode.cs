namespace BlobForge.Physics;

public enum SimulationMode
{
    FullTissue,
    ReducedTissue,
    ShapeProxy,
    Asleep,
    LooseFragment
}

public readonly record struct ModeSettings(int SolverIterations, float LinearDamping)
{
    public static ModeSettings For(SimulationMode mode) => mode switch
    {
        SimulationMode.FullTissue => new(8, 0.9985f),
        SimulationMode.ReducedTissue => new(5, 0.9975f),
        SimulationMode.ShapeProxy => new(3, 0.996f),
        SimulationMode.Asleep => new(0, 0f),
        SimulationMode.LooseFragment => new(2, 0.994f),
        _ => new(6, 0.997f)
    };
}
