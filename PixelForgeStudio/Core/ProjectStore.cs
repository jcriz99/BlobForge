using System.Text.Json;

namespace PixelForgeStudio.Core;

public sealed class ProjectStore
{
    private readonly string _root;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ProjectStore(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(ExportRoot);
    }

    public string Root => _root;
    public string ExportRoot => Path.Combine(_root, "exports");
    private string PathFor(string name) => Path.Combine(_root, SanitizeName(name) + ".pixelforge.json");

    public static string SanitizeName(string name)
    {
        var cleaned = new string((name ?? "").Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(cleaned)) throw new InvalidDataException("Project name must contain letters or numbers.");
        return cleaned.Length > 64 ? cleaned[..64] : cleaned;
    }

    public IEnumerable<object> List() => Directory.EnumerateFiles(_root, "*.pixelforge.json")
        .Select(path =>
        {
            try
            {
                var p = JsonSerializer.Deserialize<PixelProject>(File.ReadAllText(path), _json)!;
                p.Category = ProjectCategory.Normalize(p.Category, p.Name);
                return new { p.Name, p.Category, p.Width, p.Height, Frames = p.FrameCount, Layers = p.Layers.Count, p.Revision, p.UpdatedAt };
            }
            catch { return null; }
        }).Where(x => x is not null)!;

    public async Task<PixelProject> Load(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) throw new FileNotFoundException($"Pixel project '{SanitizeName(name)}' does not exist.");
        await _gate.WaitAsync();
        try
        {
            var project = JsonSerializer.Deserialize<PixelProject>(await File.ReadAllTextAsync(path), _json)
                ?? throw new InvalidDataException("Project file is empty.");
            project.Validate();
            return project;
        }
        finally { _gate.Release(); }
    }

    public async Task Save(PixelProject project, bool bumpRevision = true)
    {
        project.Validate();
        if (bumpRevision) project.Revision++;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _gate.WaitAsync();
        try
        {
            var path = PathFor(project.Name);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(project, _json));
            File.Move(temp, path, true);
        }
        finally { _gate.Release(); }
    }

    public async Task<PixelProject> Create(string name, int width, int height, int frames = 1,
        IEnumerable<string>? palette = null, bool overwrite = false, string? category = null)
    {
        var project = PixelProject.Create(name, width, height, frames, palette, category);
        if (!overwrite && File.Exists(PathFor(project.Name))) throw new IOException($"Project '{project.Name}' already exists.");
        await Save(project, false);
        return project;
    }

    public bool Delete(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }
}
