namespace PixelForgeStudio.Core;

public static class ProjectOps
{
    public static PixelLayer Layer(PixelProject project, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return project.Layers[^1];
        if (int.TryParse(selector, out var index) && index >= 0 && index < project.Layers.Count) return project.Layers[index];
        return project.Layers.FirstOrDefault(l => l.Id.Equals(selector, StringComparison.OrdinalIgnoreCase) || l.Name.Equals(selector, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Layer '{selector}' was not found.");
    }

    public static int Color(PixelProject project, string? color, int? paletteIndex = null, bool allowTransparent = false)
    {
        if (paletteIndex.HasValue)
        {
            if (allowTransparent && paletteIndex == -1) return -1;
            if (paletteIndex < 0 || paletteIndex >= project.Palette.Count) throw new ArgumentOutOfRangeException(nameof(paletteIndex));
            return paletteIndex.Value;
        }
        if (allowTransparent && (string.IsNullOrWhiteSpace(color) || color.Equals("transparent", StringComparison.OrdinalIgnoreCase))) return -1;
        var hex = ColorUtil.NormalizeHex(color ?? throw new InvalidDataException("Provide color or paletteIndex."));
        var found = project.Palette.FindIndex(c => c.Equals(hex, StringComparison.OrdinalIgnoreCase));
        if (found >= 0) return found;
        if (project.PaletteLocked) throw new InvalidDataException($"Palette is locked; '{hex}' is not approved for this project.");
        if (project.Palette.Count >= 256) throw new InvalidDataException("Palette already contains 256 colors.");
        project.Palette.Add(hex);
        return project.Palette.Count - 1;
    }

    public static void SetPixel(PixelProject project, PixelLayer layer, int frame, int x, int y, int color)
    {
        CheckFrame(project, frame);
        if (x < 0 || x >= project.Width || y < 0 || y >= project.Height) return;
        layer.Frames[frame][y * project.Width + x] = color;
    }

    public static void Line(PixelProject p, PixelLayer l, int frame, int x0, int y0, int x1, int y1, int color)
    {
        var dx = Math.Abs(x1 - x0); var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0); var sy = y0 < y1 ? 1 : -1; var err = dx + dy;
        while (true)
        {
            SetPixel(p, l, frame, x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public static void Rect(PixelProject p, PixelLayer l, int frame, int x, int y, int width, int height, int color, bool filled)
    {
        if (filled)
            for (var py = y; py < y + height; py++) for (var px = x; px < x + width; px++) SetPixel(p, l, frame, px, py, color);
        else
        {
            Line(p, l, frame, x, y, x + width - 1, y, color); Line(p, l, frame, x, y + height - 1, x + width - 1, y + height - 1, color);
            Line(p, l, frame, x, y, x, y + height - 1, color); Line(p, l, frame, x + width - 1, y, x + width - 1, y + height - 1, color);
        }
    }

    public static void Ellipse(PixelProject p, PixelLayer l, int frame, int cx, int cy, int rx, int ry, int color, bool filled)
    {
        if (rx < 0 || ry < 0) return;
        if (filled)
        {
            for (var y = -ry; y <= ry; y++)
            {
                var span = ry == 0 ? rx : (int)Math.Floor(rx * Math.Sqrt(Math.Max(0, 1 - y * y / (double)(ry * ry))));
                for (var x = -span; x <= span; x++) SetPixel(p, l, frame, cx + x, cy + y, color);
            }
            return;
        }
        var steps = Math.Max(16, (int)(2 * Math.PI * Math.Max(rx, ry) * 2));
        for (var i = 0; i < steps; i++)
        {
            var a = i * 2 * Math.PI / steps;
            SetPixel(p, l, frame, cx + (int)Math.Round(rx * Math.Cos(a)), cy + (int)Math.Round(ry * Math.Sin(a)), color);
        }
    }

    public static int Fill(PixelProject p, PixelLayer l, int frame, int x, int y, int color)
    {
        CheckFrame(p, frame);
        if (x < 0 || x >= p.Width || y < 0 || y >= p.Height) return 0;
        var pixels = l.Frames[frame]; var target = pixels[y * p.Width + x];
        if (target == color) return 0;
        var queue = new Queue<(int X, int Y)>(); queue.Enqueue((x, y)); var count = 0;
        while (queue.Count > 0)
        {
            var q = queue.Dequeue(); var idx = q.Y * p.Width + q.X;
            if (pixels[idx] != target) continue;
            pixels[idx] = color; count++;
            if (q.X > 0) queue.Enqueue((q.X - 1, q.Y)); if (q.X + 1 < p.Width) queue.Enqueue((q.X + 1, q.Y));
            if (q.Y > 0) queue.Enqueue((q.X, q.Y - 1)); if (q.Y + 1 < p.Height) queue.Enqueue((q.X, q.Y + 1));
        }
        return count;
    }

    public static void Transform(PixelProject p, PixelLayer l, int frame, string operation, int amount = 1)
    {
        CheckFrame(p, frame); var source = l.Frames[frame]; var dest = Enumerable.Repeat(-1, source.Length).ToArray();
        for (var y = 0; y < p.Height; y++) for (var x = 0; x < p.Width; x++)
        {
            var nx = x; var ny = y;
            switch (operation.ToLowerInvariant())
            {
                case "flip-horizontal": nx = p.Width - 1 - x; break;
                case "flip-vertical": ny = p.Height - 1 - y; break;
                case "shift-x": nx = ((x + amount) % p.Width + p.Width) % p.Width; break;
                case "shift-y": ny = ((y + amount) % p.Height + p.Height) % p.Height; break;
                default: throw new InvalidDataException("Operation must be flip-horizontal, flip-vertical, shift-x, or shift-y.");
            }
            dest[ny * p.Width + nx] = source[y * p.Width + x];
        }
        l.Frames[frame] = dest;
    }

    public static int[] CopyRegion(PixelProject p, PixelLayer l, int frame, int x, int y, int width, int height)
    {
        CheckRegion(p, frame, x, y, width, height);
        var result = new int[width * height];
        for (var py = 0; py < height; py++)
            Array.Copy(l.Frames[frame], (y + py) * p.Width + x, result, py * width, width);
        return result;
    }

    public static void PasteRegion(PixelProject p, PixelLayer l, int frame, int x, int y, int width, int height,
        IReadOnlyList<int> pixels, bool includeTransparent = true)
    {
        CheckFrame(p, frame);
        if (width < 1 || height < 1 || pixels.Count != width * height) throw new InvalidDataException("Region dimensions do not match pixel data.");
        for (var py = 0; py < height; py++) for (var px = 0; px < width; px++)
        {
            var value = pixels[py * width + px];
            if (value < -1 || value >= p.Palette.Count) throw new InvalidDataException($"Region contains invalid palette index {value}.");
            if (value < 0 && !includeTransparent) continue;
            SetPixel(p, l, frame, x + px, y + py, value);
        }
    }

    public static void TransformRegion(PixelProject p, PixelLayer l, int frame, int x, int y, int width, int height,
        string operation, int amount = 1)
    {
        CheckRegion(p, frame, x, y, width, height);
        var source = CopyRegion(p, l, frame, x, y, width, height);
        var dest = Enumerable.Repeat(-1, source.Length).ToArray();
        for (var py = 0; py < height; py++) for (var px = 0; px < width; px++)
        {
            var nx = px; var ny = py;
            switch (operation.ToLowerInvariant())
            {
                case "flip-horizontal": nx = width - 1 - px; break;
                case "flip-vertical": ny = height - 1 - py; break;
                case "rotate-180": nx = width - 1 - px; ny = height - 1 - py; break;
                case "shift-x": nx = ((px + amount) % width + width) % width; break;
                case "shift-y": ny = ((py + amount) % height + height) % height; break;
                default: throw new InvalidDataException("Region operation must be flip-horizontal, flip-vertical, rotate-180, shift-x, or shift-y.");
            }
            dest[ny * width + nx] = source[py * width + px];
        }
        PasteRegion(p, l, frame, x, y, width, height, dest);
    }

    public static string Ascii(PixelProject p, int frame)
    {
        CheckFrame(p, frame); const string symbols = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var rgba = PngCodec.Composite(p, frame); var lines = new List<string>();
        for (var y = 0; y < p.Height; y++)
        {
            var line = new char[p.Width];
            for (var x = 0; x < p.Width; x++)
            {
                var i = (y * p.Width + x) * 4;
                if (rgba[i + 3] == 0) { line[x] = '.'; continue; }
                var pi = p.Palette.FindIndex(c => { var rgb = ColorUtil.Parse(c); return rgb.R == rgba[i] && rgb.G == rgba[i + 1] && rgb.B == rgba[i + 2]; });
                line[x] = pi >= 0 && pi < symbols.Length ? symbols[pi] : '#';
            }
            lines.Add(new string(line));
        }
        return string.Join('\n', lines);
    }

    private static void CheckFrame(PixelProject p, int frame)
    { if (frame < 0 || frame >= p.FrameCount) throw new ArgumentOutOfRangeException(nameof(frame)); }

    private static void CheckRegion(PixelProject p, int frame, int x, int y, int width, int height)
    {
        CheckFrame(p, frame);
        if (width < 1 || height < 1 || x < 0 || y < 0 || x + width > p.Width || y + height > p.Height)
            throw new InvalidDataException("Region must be a positive rectangle inside the canvas.");
    }
}
