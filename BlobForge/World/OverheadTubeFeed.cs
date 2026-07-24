using System.Numerics;
using BlobForge.Physics;

namespace BlobForge.World;

/// <summary>Moves premium soft bodies through a sealed, air-driven ceiling tube.</summary>
public sealed class OverheadTubeFeed
{
    private const float TubeCenterY = 92f;
    private const float TubeInteriorTop = 57f;
    public const float GlassBottom = 132f;
    private const float TubeInteriorBottom = 127f;
    private const float HiddenReturnSeconds = 0.72f;
    private readonly List<Entry> _entries = new(6);
    private readonly Dictionary<SoftBody, Entry> _entryByBody = new(6);
    private float _spawnCountdown = 0.35f;

    public OverheadTubeFeed(float deckY = 480f)
    {
        DeckY = deckY;
    }

    public Vector2 Inlet => new(1232f, TubeCenterY);
    public float DeckY { get; }
    public float OverheadExitX => -72f;
    public float ConveyorReleaseX => 76f;
    public float SpawnInterval { get; set; } = 2.65f;
    public int MaximumBodiesInFactory { get; set; } = 16;
    public int BodiesInTube => _entries.Count;
    public int BodiesInVisibleTube => _entries.Count(entry => entry.Stage == FeedStage.Overhead);

    public SoftBody? Update(List<SoftBody> bodies, float dt, Func<Vector2, SoftBody> factory)
    {
        _spawnCountdown -= dt;
        SoftBody? spawned = null;
        if (_spawnCountdown <= 0f && bodies.Count(body => !body.IsDetachedDebris) < MaximumBodiesInFactory)
        {
            spawned = factory(Inlet);
            bodies.Add(spawned);
            var direction = (spawned.ParentId & 1) == 0 ? 1f : -1f;
            var entry = new Entry(spawned, Inlet,
                direction * (0.78f + (spawned.ParentId % 5) * 0.12f),
                (spawned.ParentId % 11) * 0.57f);
            _entries.Add(entry);
            _entryByBody.Add(spawned, entry);
            _spawnCountdown = SpawnInterval;
        }

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            entry.AirPhase += dt * (2.7f + entry.Seed * 0.07f);
            switch (entry.Stage)
            {
                case FeedStage.Overhead:
                    ApplyAirflow(entry, dt);
                    if (entry.Body.Center.X - entry.Body.Radius > OverheadExitX) break;

                    // Do not run the body down the left edge. It first clears the
                    // viewport completely, then travels through unseen return plumbing.
                    entry.Stage = FeedStage.HiddenReturn;
                    entry.StageTime = 0f;
                    entry.Target = new Vector2(-entry.Body.Radius - 38f, entry.Body.Center.Y);
                    break;

                case FeedStage.HiddenReturn:
                    entry.StageTime += dt;
                    var hiddenAmount = Math.Clamp(entry.StageTime / HiddenReturnSeconds, 0f, 1f);
                    entry.Target = new Vector2(-entry.Body.Radius - 38f,
                        Lerp(TubeCenterY, DeckY - entry.Body.Radius - 4f,
                            SmoothStep(hiddenAmount)));
                    if (entry.StageTime < HiddenReturnSeconds) break;
                    entry.Stage = FeedStage.ConveyorEntry;
                    entry.TravelX = -entry.Body.Radius - 38f;
                    entry.Target = new Vector2(entry.TravelX, DeckY - entry.Body.Radius - 4f);
                    break;

                case FeedStage.ConveyorEntry:
                    entry.TravelX += ProcessingLine.OperatingSpeed * dt;
                    entry.Target = new Vector2(entry.TravelX, entry.Target.Y);
                    if (entry.Target.X < ConveyorReleaseX) break;

                    entry.Body.ApplyTranslation(entry.Target - entry.Body.Center, preserveVelocity: false);
                    entry.Body.AddImpulse(new Vector2(ProcessingLine.OperatingSpeed, 0f), dt);
                    entry.Body.Wake();
                    _entries.RemoveAt(i);
                    _entryByBody.Remove(entry.Body);
                    break;
            }
        }
        return spawned;
    }

    private static void ApplyAirflow(Entry entry, float dt)
    {
        var body = entry.Body;
        var average = body.AverageVelocity(dt);
        var speedPulse = MathF.Sin(entry.AirPhase * 0.73f + entry.Seed) * 24f;
        var verticalTarget = MathF.Sin(entry.AirPhase * 1.41f + entry.Seed * 0.4f) * 42f;
        var desired = new Vector2(-154f + speedPulse, verticalTarget);
        body.AddImpulse(new Vector2(
            (desired.X - average.X) * 0.105f,
            (desired.Y - average.Y) * 0.075f), dt);

        // Distributed gusts create real deformation and angular momentum. The
        // wall contacts below, rather than a scripted rotation, make the blob
        // tumble and bump around the glass tunnel.
        var center = body.Center;
        for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
        {
            if (!body.IsPhysicalParticle(particleIndex)) continue;
            ref var particle = ref body.Particles[particleIndex];
            var radial = particle.Position - center;
            var length = radial.Length();
            var tangent = length > 0.001f
                ? new Vector2(-radial.Y, radial.X) / length
                : Vector2.UnitY;
            var unevenGust = MathF.Sin(entry.AirPhase * 2.3f + particleIndex * 1.37f + entry.Seed) * 3.8f;
            var spinGust = entry.SpinBias *
                           (5.4f + MathF.Sin(entry.AirPhase * 0.91f + particleIndex * 0.29f) * 2.2f);
            particle.PreviousPosition -= (new Vector2(0f, unevenGust) + tangent * spinGust) * dt;
        }
        body.Wake();
    }

    public bool ConstrainBody(SoftBody body, float dt)
    {
        if (_entryByBody.TryGetValue(body, out var entry))
        {
            if (entry.Stage == FeedStage.Overhead)
            {
                ContainInsideTube(body, dt);
            }
            else
            {
                body.ApplyTranslation(entry.Target - body.Center, preserveVelocity: false);
                body.AddImpulse(-body.AverageVelocity(dt), dt);
                body.Wake();
            }
            return true;
        }
        return false;
    }

    private static void ContainInsideTube(SoftBody body, float dt)
    {
        for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
        {
            if (!body.IsPhysicalParticle(particleIndex)) continue;
            ref var particle = ref body.Particles[particleIndex];
            var minimumY = TubeInteriorTop + particle.Radius;
            var maximumY = TubeInteriorBottom - particle.Radius;
            if (particle.Position.Y < minimumY)
                _ = ResolveHorizontalGlass(ref particle, minimumY, dt, ceiling: true);
            else if (particle.Position.Y > maximumY)
                _ = ResolveHorizontalGlass(ref particle, maximumY, dt, ceiling: false);
        }
        body.Wake();
    }

    /// <summary>Seals ordinary factory matter below the tube's lower glass face.</summary>
    public int ResolveExteriorBody(SoftBody body, float dt)
    {
        if (Contains(body)) return 0;
        var contacts = 0;
        for (var particleIndex = 0; particleIndex < body.Particles.Length; particleIndex++)
        {
            if (!body.IsPhysicalParticle(particleIndex)) continue;
            ref var particle = ref body.Particles[particleIndex];
            var minimumY = GlassBottom + particle.Radius;
            if (particle.Position.Y >= minimumY) continue;
            // From the factory side this is a ceiling collision: negative Y is
            // motion into the glass. It used to be resolved as a floor, which
            // corrected overlap but failed to reflect or report the real impact.
            var impact = ResolveHorizontalGlass(
                ref particle, minimumY, dt, ceiling: true);
            var contactPoint = new Vector2(particle.Position.X, GlassBottom);
            if (impact >= body.LastImpact)
                body.LastImpactPoint = contactPoint;
            if (impact >= body.LastTerrainImpact)
                body.LastTerrainImpactPoint = contactPoint;
            body.LastImpact = MathF.Max(body.LastImpact, impact);
            body.LastTerrainImpact = MathF.Max(body.LastTerrainImpact, impact);
            body.RecordBreakupImpact(contactPoint, impact);
            contacts++;
        }
        if (contacts > 0) body.Wake();
        return contacts;
    }

    private static float ResolveHorizontalGlass(
        ref Particle particle,
        float surfaceY,
        float dt,
        bool ceiling)
    {
        var velocity = (particle.Position - particle.PreviousPosition) / MathF.Max(dt, 0.0001f);
        var impact = ceiling
            ? MathF.Max(0f, -velocity.Y)
            : MathF.Max(0f, velocity.Y);
        particle.Position.Y = surfaceY;
        var movingIntoSurface = ceiling ? velocity.Y < 0f : velocity.Y > 0f;
        if (movingIntoSurface) velocity.Y *= -0.34f;
        velocity.X *= 0.985f;
        particle.PreviousPosition = particle.Position - velocity * dt;
        particle.Contacting = true;
        particle.ContactMemory = 6;
        return impact;
    }

    public bool Contains(SoftBody body) => _entryByBody.ContainsKey(body);

    public bool IsInHiddenReturn(SoftBody body) =>
        _entryByBody.TryGetValue(body, out var entry) && entry.Stage == FeedStage.HiddenReturn;

    public bool IsEnteringConveyor(SoftBody body) =>
        _entryByBody.TryGetValue(body, out var entry) && entry.Stage == FeedStage.ConveyorEntry;

    private static float SmoothStep(float amount) => amount * amount * (3f - 2f * amount);
    private static float Lerp(float a, float b, float amount) => a + (b - a) * amount;

    private sealed class Entry(SoftBody body, Vector2 target, float spinBias, float airPhase)
    {
        public SoftBody Body { get; } = body;
        public Vector2 Target { get; set; } = target;
        public float TravelX { get; set; } = target.X;
        public float SpinBias { get; } = spinBias;
        public float Seed { get; } = (body.ParentId % 17) * 0.31f;
        public float AirPhase { get; set; } = airPhase;
        public float StageTime { get; set; }
        public FeedStage Stage { get; set; }
    }

    private enum FeedStage : byte
    {
        Overhead,
        HiddenReturn,
        ConveyorEntry
    }
}
