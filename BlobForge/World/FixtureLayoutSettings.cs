using System.Numerics;
using System.Text.Json;

namespace BlobForge.World;

public sealed class FixtureLayoutSettings
{
    private readonly string _path;

    private FixtureLayoutSettings(string path)
    {
        _path = path;
    }

    public float? BlobCounterX { get; set; }
    public float? BlobCounterY { get; set; }
    public float? BreakerBoxX { get; set; }
    public float? BreakerBoxY { get; set; }

    public Vector2? BlobCounterPosition => Position(BlobCounterX, BlobCounterY);
    public Vector2? BreakerBoxPosition => Position(BreakerBoxX, BreakerBoxY);

    public static FixtureLayoutSettings Load()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlobForge");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "fixture-layout.json");
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<FixtureLayoutSettingsData>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    return new FixtureLayoutSettings(path)
                    {
                        BlobCounterX = loaded.BlobCounterX,
                        BlobCounterY = loaded.BlobCounterY,
                        BreakerBoxX = loaded.BreakerBoxX,
                        BreakerBoxY = loaded.BreakerBoxY
                    };
                }
            }
        }
        catch
        {
            // An invalid layout should fall back to the authored defaults.
        }
        return new FixtureLayoutSettings(path);
    }

    public void Capture(HoldingChamber? chamber, ProcessingLine? line)
    {
        if (chamber is not null)
        {
            BlobCounterX = chamber.CounterBounds.X;
            BlobCounterY = chamber.CounterBounds.Y;
        }
        if (line is not null)
        {
            BreakerBoxX = line.BreakerBounds.X;
            BreakerBoxY = line.BreakerBounds.Y;
        }
    }

    public void Save()
    {
        try
        {
            var data = new FixtureLayoutSettingsData
            {
                BlobCounterX = BlobCounterX,
                BlobCounterY = BlobCounterY,
                BreakerBoxX = BreakerBoxX,
                BreakerBoxY = BreakerBoxY
            };
            File.WriteAllText(_path,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Placement editing must remain usable even if persistence is unavailable.
        }
    }

    private static Vector2? Position(float? x, float? y)
    {
        if (x is null || y is null || !float.IsFinite(x.Value) || !float.IsFinite(y.Value)) return null;
        return new Vector2(x.Value, y.Value);
    }

    private sealed class FixtureLayoutSettingsData
    {
        public float? BlobCounterX { get; set; }
        public float? BlobCounterY { get; set; }
        public float? BreakerBoxX { get; set; }
        public float? BreakerBoxY { get; set; }
    }
}
