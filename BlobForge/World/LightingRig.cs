using System.Drawing;
using System.Numerics;

namespace BlobForge.World;

/// <summary>
/// Performance-budgeted lighting state. Ambient/directional light changes are
/// rare and revision cached. Hanging lamps advance a 10 Hz shadow revision; their
/// fixtures still animate every step, while the expensive occlusion map updates at
/// a deliberately lower visual rate.
/// </summary>
public sealed class LightingRig
{
    public const int MaximumLights = 12;
    private readonly List<IndustrialLight> _lights = new();
    private float _time;
    private float _dynamicAccumulator;

    public float AmbientLevel { get; private set; } = 1f;
    public Color AmbientColor { get; private set; } = Color.FromArgb(10, 15, 22);
    public Vector2 DirectionalDirection { get; private set; } = Vector2.UnitY;
    public float DirectionalStrength { get; private set; }
    public Color DirectionalColor { get; private set; } = Color.FromArgb(112, 145, 166);
    public bool FactoryPowered { get; private set; } = true;
    public int PoweredLightCount { get; private set; } = MaximumLights;
    public IReadOnlyList<IndustrialLight> Lights => _lights;
    public int Revision { get; private set; }
    public int DynamicRevision { get; private set; }

    public void ConfigureAmbient(float level, Color color)
    {
        AmbientLevel = Math.Clamp(level, 0f, 1f);
        AmbientColor = color;
        Revision++;
    }

    public void ConfigureDirectional(Vector2 direction, float strength, Color color)
    {
        DirectionalDirection = direction.LengthSquared() < 0.001f
            ? Vector2.UnitY
            : Vector2.Normalize(direction);
        DirectionalStrength = Math.Clamp(strength, 0f, 1f);
        DirectionalColor = color;
        Revision++;
    }

    public void Step(float dt)
    {
        _time += dt;
        foreach (var light in _lights) light.Step(_time);
        _dynamicAccumulator += dt;
        if (_dynamicAccumulator < 1f / 10f) return;
        _dynamicAccumulator %= 1f / 10f;
        DynamicRevision++;
    }

    public void ClearLights()
    {
        if (_lights.Count == 0) return;
        _lights.Clear();
        DynamicRevision++;
    }

    public IndustrialLight? AddIndustrialLight(IndustrialLight light)
    {
        if (_lights.Count >= MaximumLights) return null;
        _lights.Add(light);
        DynamicRevision++;
        return light;
    }

    public bool RemoveLight(IndustrialLight light)
    {
        var removed = _lights.Remove(light);
        if (removed) DynamicRevision++;
        return removed;
    }

    public void NotifyEdited() => DynamicRevision++;

    public IndustrialLight? HitTest(Vector2 point)
    {
        for (var index = _lights.Count - 1; index >= 0; index--)
            if (_lights[index].ContainsPoint(point)) return _lights[index];
        return null;
    }

    public void ConfigureProcessingStation()
    {
        _lights.Clear();
        AmbientLevel = 0.78f;
        AmbientColor = Color.FromArgb(8, 13, 20);
        DirectionalDirection = Vector2.Normalize(new Vector2(0.22f, 1f));
        DirectionalStrength = 0.08f;
        DirectionalColor = Color.FromArgb(105, 143, 164);
        AddPresetLantern(466f, 104f, 430f, 132f, 0.42f, Color.FromArgb(255, 224, 144));
        AddPresetLantern(766f, 100f, 470f, 150f, 0.46f, Color.FromArgb(255, 215, 125));
        AddPresetLantern(1070f, 108f, 430f, 136f, 0.40f, Color.FromArgb(220, 238, 230));
        Revision++;
        DynamicRevision++;
    }

    public void SetFactoryPower(bool powered)
    {
        SetPoweredLightCount(powered ? _lights.Count : 0);
    }

    public void SetPoweredLightCount(int count)
    {
        count = Math.Clamp(count, 0, _lights.Count);
        var powered = count > 0;
        if (PoweredLightCount == count && FactoryPowered == powered) return;
        PoweredLightCount = count;
        FactoryPowered = powered;
        if (powered)
        {
            AmbientLevel = 0.78f;
            AmbientColor = Color.FromArgb(8, 13, 20);
            DirectionalDirection = Vector2.Normalize(new Vector2(0.22f, 1f));
            DirectionalStrength = 0.08f;
            DirectionalColor = Color.FromArgb(105, 143, 164);
        }
        else
        {
            AmbientLevel = 0.055f;
            AmbientColor = Color.FromArgb(3, 5, 8);
            DirectionalStrength = 0f;
        }
        Revision++;
        DynamicRevision++;
    }

    public bool IsLightPowered(int index) =>
        FactoryPowered && index >= 0 && index < PoweredLightCount;

    private void AddPresetLantern(
        float x, float lampY, float range, float halfWidth, float strength, Color color)
    {
        _lights.Add(IndustrialLight.CreateHanging(
            new Vector2(x, 0f), lampY, range, halfWidth, strength, color));
    }
}

public sealed class IndustrialLight
{
    private static int _nextId;
    private float _swingAngle;

    // Compatibility constructor: position is the initial lamp position.
    public IndustrialLight(
        Vector2 position,
        Vector2 direction,
        float range,
        float halfWidth,
        float strength,
        Color color)
        : this(
            new Vector2(position.X, MathF.Max(0f, position.Y - 90f)),
            Math.Clamp(90f, 30f, 210f),
            range,
            halfWidth,
            strength,
            color)
    {
        _swingAngle = MathF.Atan2(direction.X, direction.Y) * 0.2f;
    }

    private IndustrialLight(
        Vector2 anchor,
        float cableLength,
        float range,
        float halfWidth,
        float strength,
        Color color)
    {
        Id = Interlocked.Increment(ref _nextId);
        Anchor = anchor;
        CableLength = Math.Clamp(cableLength, 28f, 210f);
        Range = Math.Clamp(range, 160f, 620f);
        HalfWidth = Math.Clamp(halfWidth, 54f, 230f);
        Strength = Math.Clamp(strength, 0.12f, 0.72f);
        Color = color;
        Phase = Id * 1.713f;
    }

    public static IndustrialLight CreateHanging(
        Vector2 anchor,
        float cableLength,
        float range,
        float halfWidth,
        float strength,
        Color color) => new(anchor, cableLength, range, halfWidth, strength, color);

    public int Id { get; }
    public Vector2 Anchor { get; private set; }
    public float CableLength { get; private set; }
    public float Range { get; private set; }
    public float HalfWidth { get; private set; }
    public float Strength { get; private set; }
    public Color Color { get; }
    public float Phase { get; }
    public bool IsSelected { get; set; }
    public float SwingAngle => _swingAngle;
    public Vector2 Direction => Vector2.Normalize(new Vector2(MathF.Sin(_swingAngle), MathF.Cos(_swingAngle)));
    public Vector2 Position => Anchor + Direction * CableLength;
    public Vector2 Tangent => new(-Direction.Y, Direction.X);
    public Vector2 CableHandle => Anchor + Direction * (CableLength * 0.52f);
    public Vector2 RangeHandle => Position + Tangent * (44f + (Range - 240f) * 0.08f);

    internal void Step(float time)
    {
        // Two very low-frequency components avoid synchronized metronome motion.
        _swingAngle = MathF.Sin(time * 0.72f + Phase) * 0.040f +
                      MathF.Sin(time * 0.29f + Phase * 0.61f) * 0.014f;
    }

    public bool ContainsPoint(Vector2 point)
    {
        if (Vector2.DistanceSquared(point, Position) <= 36f * 36f) return true;
        if (Vector2.DistanceSquared(point, Anchor) <= 11f * 11f) return true;
        if (IsSelected && Vector2.DistanceSquared(point, RangeHandle) <= 13f * 13f) return true;
        return DistanceToSegmentSquared(point, Anchor, Position) <= 7f * 7f;
    }

    public LightEditHandle HitEditHandle(Vector2 point)
    {
        if (Vector2.DistanceSquared(point, RangeHandle) <= 13f * 13f) return LightEditHandle.Range;
        if (Vector2.DistanceSquared(point, CableHandle) <= 11f * 11f) return LightEditHandle.CableLength;
        return ContainsPoint(point) ? LightEditHandle.Move : LightEditHandle.None;
    }

    public void Move(Vector2 delta, float arenaWidth)
    {
        Anchor = new Vector2(Math.Clamp(Anchor.X + delta.X, 42f, arenaWidth - 42f), 0f);
    }

    public void SetCableFromPointer(Vector2 point)
    {
        CableLength = Math.Clamp(Vector2.Distance(Anchor, point), 28f, 210f);
    }

    public void SetRangeFromPointer(Vector2 point)
    {
        var offset = Vector2.Dot(point - Position, Tangent);
        Range = Math.Clamp(240f + (MathF.Abs(offset) - 44f) / 0.08f, 160f, 620f);
        HalfWidth = Math.Clamp(Range * 0.31f, 54f, 230f);
    }

    public void AdjustCable(float delta) => CableLength = Math.Clamp(CableLength + delta, 28f, 210f);
    public void AdjustStrength(float delta) => Strength = Math.Clamp(Strength + delta, 0.12f, 0.72f);
    public void AdjustRange(float delta)
    {
        Range = Math.Clamp(Range + delta, 160f, 620f);
        HalfWidth = Math.Clamp(Range * 0.31f, 54f, 230f);
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared < 0.001f) return Vector2.DistanceSquared(point, start);
        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.DistanceSquared(point, start + segment * t);
    }
}

public enum LightEditHandle
{
    None,
    Move,
    CableLength,
    Range
}
