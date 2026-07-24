namespace BlobForge.Physics;

public sealed class RepresentationScheduler
{
    public int FullTissueBudget { get; set; } = 8;
    public int ReducedTissueBudget { get; set; } = 12;

    public void Apply(IReadOnlyList<SoftBody> bodies)
    {
        var fullBodies = 0;
        var reducedBodies = 0;
        for (var i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.IsSleeping)
            {
                body.SetMode(SimulationMode.Asleep);
                continue;
            }

            if (body.IsDetachedDebris)
            {
                body.SetMode(body.PhysicalParticleCount >= 4
                    ? SimulationMode.ShapeProxy
                    : SimulationMode.LooseFragment);
                continue;
            }

            var needsFullTissue = body.IsGrabbed || body.TopologyDirty || body.LastImpact > 130f;
            if (needsFullTissue && fullBodies < FullTissueBudget)
            {
                body.SetMode(SimulationMode.FullTissue);
                fullBodies++;
            }
            else if (body.Particles.Length <= 3)
            {
                body.SetMode(SimulationMode.LooseFragment);
            }
            else if (reducedBodies < Math.Max(0, ReducedTissueBudget))
            {
                body.SetMode(SimulationMode.ReducedTissue);
                reducedBodies++;
            }
            else
            {
                body.SetMode(SimulationMode.ShapeProxy);
            }
        }
    }
}
