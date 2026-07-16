using System.Text.Json.Serialization;

namespace PixelForgeStudio.Core;

public sealed class PixelProject
{
    public int Version { get; set; } = 1;
    public required string Name { get; set; }
    public string Category { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public List<string> Palette { get; set; } = [];
    public List<PixelLayer> Layers { get; set; } = [];
    public List<int> FrameDurationsMs { get; set; } = [];
    public List<AnimationTag> Tags { get; set; } = [];
    public long Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public int FrameCount => FrameDurationsMs.Count;

    public static PixelProject Create(string name, int width, int height, int frames = 1,
        IEnumerable<string>? palette = null, string? category = null)
    {
        if (width is < 1 or > 512 || height is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(width), "Canvas dimensions must be 1-512 pixels.");
        if (frames is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(frames), "Frame count must be 1-256.");
        var colors = (palette ?? DefaultPalette).Select(ColorUtil.NormalizeHex).Distinct(StringComparer.OrdinalIgnoreCase).Take(256).ToList();
        if (colors.Count == 0) colors.Add("#000000");
        var project = new PixelProject
        {
            Name = ProjectStore.SanitizeName(name), Width = width, Height = height, Palette = colors,
            Category = ProjectCategory.Normalize(category, name),
            FrameDurationsMs = Enumerable.Repeat(100, frames).ToList()
        };
        project.Layers.Add(PixelLayer.Create("Artwork", frames, width * height));
        return project;
    }

    public void Validate()
    {
        Name = ProjectStore.SanitizeName(Name);
        Category = ProjectCategory.Normalize(Category, Name);
        if (Width is < 1 or > 512 || Height is < 1 or > 512) throw new InvalidDataException("Canvas dimensions must be 1-512 pixels.");
        if (FrameDurationsMs.Count is < 1 or > 256) throw new InvalidDataException("A project must contain 1-256 frames.");
        Palette = Palette.Select(ColorUtil.NormalizeHex).Distinct(StringComparer.OrdinalIgnoreCase).Take(256).ToList();
        if (Palette.Count == 0) Palette.Add("#000000");
        if (Layers.Count == 0) Layers.Add(PixelLayer.Create("Artwork", FrameCount, Width * Height));
        foreach (var layer in Layers)
        {
            layer.Id = string.IsNullOrWhiteSpace(layer.Id) ? Guid.NewGuid().ToString("N") : layer.Id;
            layer.Name = string.IsNullOrWhiteSpace(layer.Name) ? "Layer" : layer.Name.Trim();
            layer.Opacity = Math.Clamp(layer.Opacity, 0, 1);
            while (layer.Frames.Count < FrameCount) layer.Frames.Add(Enumerable.Repeat(-1, Width * Height).ToArray());
            if (layer.Frames.Count > FrameCount) layer.Frames.RemoveRange(FrameCount, layer.Frames.Count - FrameCount);
            for (var i = 0; i < layer.Frames.Count; i++)
            {
                if (layer.Frames[i].Length != Width * Height) throw new InvalidDataException($"Layer '{layer.Name}' frame {i} has an invalid pixel count.");
                for (var p = 0; p < layer.Frames[i].Length; p++)
                    if (layer.Frames[i][p] < -1 || layer.Frames[i][p] >= Palette.Count) layer.Frames[i][p] = -1;
            }
        }
        Tags = Tags.Where(t => t.From >= 0 && t.To >= t.From && t.To < FrameCount).ToList();
    }

    public static readonly string[] DefaultPalette =
    [
        "#0b1020", "#171d35", "#29335c", "#3f4f78", "#65749b", "#a3b0ce", "#f4f1de", "#f6bd60",
        "#f28482", "#d8576b", "#9e3b58", "#5c2946", "#76c893", "#40916c", "#1b6b61", "#48bfe3"
    ];
}

public static class ProjectCategory
{
    public const string Tilesets = "Tilesets";
    public const string Objects = "Objects";
    public const string MiscArt = "Misc Art";
    public static readonly string[] All = [Tilesets, Objects, MiscArt];

    public static string Normalize(string? category, string projectName)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            var canonical = All.FirstOrDefault(x =>
                x.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
                throw new InvalidDataException($"Unknown project category '{category}'.");
            return canonical;
        }

        var name = projectName.ToLowerInvariant();
        if (name.Contains("tileset") || name.Contains("tile-set") || name.Contains("_tiles"))
            return Tilesets;
        if (name.StartsWith("blobforge_") || name.Contains("sprite") ||
            name.Contains("hero") || name.Contains("plumber"))
            return Objects;
        return MiscArt;
    }
}

public sealed class PixelLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Layer";
    public bool Visible { get; set; } = true;
    public double Opacity { get; set; } = 1;
    public List<int[]> Frames { get; set; } = [];

    public static PixelLayer Create(string name, int frames, int pixels) => new()
    {
        Name = name,
        Frames = Enumerable.Range(0, frames).Select(_ => Enumerable.Repeat(-1, pixels).ToArray()).ToList()
    };
}

public sealed class AnimationTag
{
    public string Name { get; set; } = "animation";
    public int From { get; set; }
    public int To { get; set; }
    public string Direction { get; set; } = "forward";
}

public static class ColorUtil
{
    public static string NormalizeHex(string value)
    {
        value = (value ?? "").Trim();
        if (!value.StartsWith('#')) value = "#" + value;
        if (value.Length == 4) value = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";
        if (value.Length != 7 || !value.Skip(1).All(Uri.IsHexDigit)) throw new InvalidDataException($"Invalid color '{value}'. Use #RRGGBB.");
        return value.ToLowerInvariant();
    }

    public static (byte R, byte G, byte B) Parse(string hex) =>
        (Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));
}
