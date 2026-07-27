using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PixelForgeStudio.Core;

public static class PngCodec
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] RenderFrame(PixelProject project, int frame, int scale = 1)
    {
        if (frame < 0 || frame >= project.FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        scale = Math.Clamp(scale, 1, 64);
        var rgba = Composite(project, frame);
        if (scale > 1) rgba = ScaleNearest(rgba, project.Width, project.Height, scale);
        return EncodeRgba(project.Width * scale, project.Height * scale, rgba);
    }

    public static byte[] RenderSheet(PixelProject project, int scale = 1)
    {
        scale = Math.Clamp(scale, 1, 64);
        var w = project.Width * project.FrameCount;
        var rgba = new byte[w * project.Height * 4];
        for (var f = 0; f < project.FrameCount; f++)
        {
            var frame = Composite(project, f);
            for (var y = 0; y < project.Height; y++)
                Buffer.BlockCopy(frame, y * project.Width * 4, rgba, (y * w + f * project.Width) * 4, project.Width * 4);
        }
        if (scale > 1) rgba = ScaleNearest(rgba, w, project.Height, scale);
        return EncodeRgba(w * scale, project.Height * scale, rgba);
    }

    public static byte[] RenderContactSheet(PixelProject project, int scale = 4, int columns = 0, IReadOnlyList<int>? frames = null)
    {
        scale = Math.Clamp(scale, 1, 32);
        frames ??= Enumerable.Range(0, project.FrameCount).ToArray();
        if (frames.Count == 0 || frames.Any(frame => frame < 0 || frame >= project.FrameCount)) throw new InvalidDataException("Contact-sheet frames are outside the project.");
        columns = columns <= 0 ? Math.Max(1, (int)Math.Ceiling(Math.Sqrt(frames.Count))) : Math.Clamp(columns, 1, frames.Count);
        var rows = (frames.Count + columns - 1) / columns;
        const int gap = 2;
        var width = columns * project.Width + (columns - 1) * gap;
        var height = rows * project.Height + (rows - 1) * gap;
        var rgba = new byte[width * height * 4];
        for (var index = 0; index < frames.Count; index++)
        {
            var source = Composite(project, frames[index]); var ox = (index % columns) * (project.Width + gap); var oy = (index / columns) * (project.Height + gap);
            Blit(source, project.Width, project.Height, rgba, width, ox, oy);
        }
        if (scale > 1) rgba = ScaleNearest(rgba, width, height, scale);
        return EncodeRgba(width * scale, height * scale, rgba);
    }

    public static byte[] RenderComparison(PixelProject project, int frame, PixelProject reference, int referenceFrame, int scale = 4)
    {
        scale = Math.Clamp(scale, 1, 32); const int gap = 4;
        var width = project.Width + gap + reference.Width; var height = Math.Max(project.Height, reference.Height);
        var rgba = new byte[width * height * 4];
        Blit(Composite(project, frame), project.Width, project.Height, rgba, width, 0, 0);
        Blit(Composite(reference, referenceFrame), reference.Width, reference.Height, rgba, width, project.Width + gap, 0);
        if (scale > 1) rgba = ScaleNearest(rgba, width, height, scale);
        return EncodeRgba(width * scale, height * scale, rgba);
    }

    public static byte[] Composite(PixelProject project, int frame)
    {
        var output = new byte[project.Width * project.Height * 4];
        foreach (var layer in project.Layers.Where(l => l.Visible))
        {
            var pixels = layer.Frames[frame];
            var layerAlpha = Math.Clamp(layer.Opacity, 0, 1);
            for (var i = 0; i < pixels.Length; i++)
            {
                var pi = pixels[i];
                if (pi < 0 || pi >= project.Palette.Count) continue;
                var (r, g, b) = ColorUtil.Parse(project.Palette[pi]);
                var a = layerAlpha;
                var dstA = output[i * 4 + 3] / 255d;
                var outA = a + dstA * (1 - a);
                if (outA <= 0) continue;
                output[i * 4] = (byte)Math.Round((r * a + output[i * 4] * dstA * (1 - a)) / outA);
                output[i * 4 + 1] = (byte)Math.Round((g * a + output[i * 4 + 1] * dstA * (1 - a)) / outA);
                output[i * 4 + 2] = (byte)Math.Round((b * a + output[i * 4 + 2] * dstA * (1 - a)) / outA);
                output[i * 4 + 3] = (byte)Math.Round(outA * 255);
            }
        }
        return output;
    }

    private static byte[] ScaleNearest(byte[] source, int width, int height, int scale)
    {
        var output = new byte[width * scale * height * scale * 4];
        var outW = width * scale;
        for (var y = 0; y < height * scale; y++)
        for (var x = 0; x < outW; x++)
        {
            var si = ((y / scale) * width + x / scale) * 4;
            var di = (y * outW + x) * 4;
            Buffer.BlockCopy(source, si, output, di, 4);
        }
        return output;
    }

    private static void Blit(byte[] source, int sourceWidth, int sourceHeight, byte[] destination, int destinationWidth, int x, int y)
    {
        for (var row = 0; row < sourceHeight; row++)
            Buffer.BlockCopy(source, row * sourceWidth * 4, destination, ((y + row) * destinationWidth + x) * 4, sourceWidth * 4);
    }

    public static byte[] EncodeRgba(int width, int height, byte[] rgba)
    {
        using var output = new MemoryStream();
        output.Write(Signature);
        var ihdr = new byte[13];
        WriteBig(ihdr, 0, width); WriteBig(ihdr, 4, height);
        ihdr[8] = 8; ihdr[9] = 6;
        WriteChunk(output, "IHDR", ihdr);
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgba, y * width * 4, width * 4);
        }
        using var compressed = new MemoryStream();
        raw.Position = 0;
        using (var z = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)) raw.CopyTo(z);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4]; WriteBig(len, 0, data.Length); output.Write(len);
        var typeBytes = Encoding.ASCII.GetBytes(type); output.Write(typeBytes); output.Write(data);
        var crcData = new byte[typeBytes.Length + data.Length]; typeBytes.CopyTo(crcData, 0); data.CopyTo(crcData, typeBytes.Length);
        Span<byte> crc = stackalloc byte[4]; WriteBig(crc, 0, unchecked((int)Crc32(crcData))); output.Write(crc);
    }

    private static void WriteBig(Span<byte> bytes, int offset, int value)
    { bytes[offset] = (byte)(value >> 24); bytes[offset + 1] = (byte)(value >> 16); bytes[offset + 2] = (byte)(value >> 8); bytes[offset + 3] = (byte)value; }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xffffffffu;
        foreach (var b in bytes) { crc ^= b; for (var k = 0; k < 8; k++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1)); }
        return ~crc;
    }
}
