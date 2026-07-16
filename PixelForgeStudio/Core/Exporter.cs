using System.IO.Compression;
using System.Text.Json;

namespace PixelForgeStudio.Core;

public sealed class Exporter(ProjectStore store)
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<(byte[] Data, string FileName, string ContentType)> Export(PixelProject p, string format, int scale = 1, int frame = 0)
    {
        format = format.ToLowerInvariant();
        byte[] data; string ext; string content;
        switch (format)
        {
            case "png": data = PngCodec.RenderFrame(p, frame, scale); ext = $"frame-{frame}.png"; content = "image/png"; break;
            case "spritesheet": data = PngCodec.RenderSheet(p, scale); ext = "sheet.png"; content = "image/png"; break;
            case "pack": data = BuildPack(p, scale); ext = "engine-pack.zip"; content = "application/zip"; break;
            case "json": data = JsonSerializer.SerializeToUtf8Bytes(p, _json); ext = "project.json"; content = "application/json"; break;
            default: throw new InvalidDataException("Format must be png, spritesheet, pack, or json.");
        }
        var dir = Path.Combine(store.ExportRoot, p.Name); Directory.CreateDirectory(dir);
        var fileName = $"{p.Name}-{ext}"; await File.WriteAllBytesAsync(Path.Combine(dir, fileName), data);
        return (data, fileName, content);
    }

    private byte[] BuildPack(PixelProject p, int scale)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Add(zip, $"{p.Name}.png", PngCodec.RenderSheet(p, scale));
            var atlas = new
            {
                image = $"{p.Name}.png", frameWidth = p.Width * scale, frameHeight = p.Height * scale,
                frames = Enumerable.Range(0, p.FrameCount).Select(i => new { index = i, x = i * p.Width * scale, y = 0, width = p.Width * scale, height = p.Height * scale, durationMs = p.FrameDurationsMs[i] }),
                animations = p.Tags
            };
            AddText(zip, $"{p.Name}.atlas.json", JsonSerializer.Serialize(atlas, _json));
            AddText(zip, "godot/import.md", $"Import {p.Name}.png, disable Filter, create Sprite2D/AnimatedSprite2D with Hframes={p.FrameCount}, Vframes=1. Pixel size: {p.Width}x{p.Height}.");
            AddText(zip, "unity/import.md", $"Set Texture Type=Sprite, Sprite Mode=Multiple, Filter Mode=Point, Compression=None, Pixels Per Unit={p.Height}. Slice grid {p.Width * scale}x{p.Height * scale}.");
            AddText(zip, "monogame/import.md", $"Load {p.Name}.png as Texture2D. Each source rectangle is new Rectangle(frame * {p.Width * scale}, 0, {p.Width * scale}, {p.Height * scale}).");
            AddText(zip, "README.txt", "Pixel Forge Studio engine pack. Coordinates use a top-left origin. Transparency is preserved. See the atlas JSON for timing and tags.");
        }
        return output.ToArray();
    }

    private static void Add(ZipArchive zip, string name, byte[] data)
    { using var stream = zip.CreateEntry(name, CompressionLevel.SmallestSize).Open(); stream.Write(data); }
    private static void AddText(ZipArchive zip, string name, string text)
    { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(text); }
}
