using System.Text.Json;
using PixelForgeStudio.Core;
using PixelForgeStudio.Mcp;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "serve";
var dataRoot = Environment.GetEnvironmentVariable("PIXEL_FORGE_DATA")
    ?? FindDataRoot();
var store = new ProjectStore(dataRoot);

static string FindDataRoot()
{
    // Development builds and the checked-in published launcher both live somewhere
    // beneath PixelForgeStudio. Anchor project storage to that directory so starting
    // the app from PROCESS, .publish, or Explorer always opens the same art library.
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (directory.Name.Equals("PixelForgeStudio", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(directory.FullName, "PixelForgeData");
    }

    var workingData = Path.Combine(Directory.GetCurrentDirectory(), "PixelForgeData");
    return Directory.Exists(workingData)
        ? workingData
        : Path.Combine(AppContext.BaseDirectory, "PixelForgeData");
}

if (command == "mcp")
{
    await new McpServer(store).Run();
    return;
}

if (command == "self-test")
{
    var name = "self-test-" + Guid.NewGuid().ToString("N")[..8];
    try
    {
        var p = await store.Create(name, 8, 8, 2, category: ProjectCategory.Objects);
        if (p.Category != ProjectCategory.Objects) throw new Exception("Project category was not persisted.");
        var listed = store.List().Single(x => x.Name == name);
        if (listed.Category != ProjectCategory.Objects) throw new Exception("Project category was missing from the project list.");
        ProjectOps.Rect(p, p.Layers[0], 0, 1, 1, 6, 6, 8, false);
        ProjectOps.Line(p, p.Layers[0], 0, 1, 6, 6, 1, 13);
        var copied = ProjectOps.CopyRegion(p, p.Layers[0], 0, 1, 1, 6, 6);
        ProjectOps.PasteRegion(p, p.Layers[0], 1, 1, 1, 6, 6, copied);
        ProjectOps.TransformRegion(p, p.Layers[0], 1, 1, 1, 6, 6, "flip-horizontal");
        p.AttachmentPoints.Add(new AttachmentPoint { Name = "mount", X = 4, Y = 6 });
        p.AttachmentPoints.Add(new AttachmentPoint { Name = "tool-tip", X = 2, Y = 2, Frame = 0 });
        p.AttachmentPoints.Add(new AttachmentPoint { Name = "tool-tip", X = 5, Y = 2, Frame = 1 });
        AnimationOps.SetTag(p, "cycle", 0, 1, "pingpong", true);
        AnimationOps.SetDurations(p, 0, [80, 140]);
        AnimationOps.AddFrame(p, 1, 0, 60);
        if (p.Tags.Single().To != 2 || p.AttachmentPoints.Single(point => point.Name == "tool-tip" && point.X == 5).Frame != 2)
            throw new Exception("Frame insertion did not preserve animation metadata.");
        AnimationOps.DeleteFrame(p, 1);
        if (p.Tags.Single().To != 1 || p.AttachmentPoints.Single(point => point.Name == "tool-tip" && point.X == 5).Frame != 1)
            throw new Exception("Frame deletion did not preserve animation metadata.");
        p.Validation.Loop = true;
        p.Validation.AttachmentMotion = true;
        await store.Save(p);
        var exporter = new Exporter(store);
        var png = await exporter.Export(p, "png", 2);
        var sheet = await exporter.Export(p, "spritesheet", 1);
        var pack = await exporter.Export(p, "pack", 1);
        if (png.Data.Length < 100 || sheet.Data.Length < 100 || pack.Data.Length < 300) throw new Exception("Export output was unexpectedly small.");
        if (PngCodec.RenderContactSheet(p).Length < 100) throw new Exception("Contact sheet output was unexpectedly small.");
        var report = ProjectAnalyzer.Analyze(p);
        if (report.Frames.Count != 2 || report.AttachmentPoints.Count != 3 || report.Animation.Clips.Single().Name != "cycle" || report.Animation.AttachmentTracks.Single().Samples.Count != 2)
            throw new Exception("Project report omitted animation production metadata.");
        if (PngCodec.RenderContactSheet(p, 2, 0, report.Animation.Clips.Single().PlaybackFrames).Length < 100)
            throw new Exception("Animation contact sheet output was unexpectedly small.");
        p.Layers[0].FrameInvariant = true;
        report = ProjectAnalyzer.Analyze(p);
        if (!report.Issues.Any(issue => issue.Code == "frame-invariant-layer-changed"))
            throw new Exception("Static-layer drift was not reported.");
        p.Layers[0].FrameInvariant = false;
        p.Validation.FrameConsistency = true;
        if (ProjectAnalyzer.Analyze(p).FrameConsistency.DistinctComponentCounts < 1)
            throw new Exception("Cross-frame consistency report was missing.");
        p.PaletteLocked = true;
        await store.Save(p);
        try { ProjectOps.Color(p, "#010203"); throw new Exception("Locked palette accepted an unauthorized color."); }
        catch (InvalidDataException) { }
        p.Palette.Add("#010203");
        try { await store.Save(p); throw new Exception("General project save bypassed the locked palette."); }
        catch (InvalidDataException) { p.Palette.RemoveAt(p.Palette.Count - 1); }
        if (BlobForgeBridge.ResolveAssetName(new PixelProject { Name = "blobforge_vacuum_holster_v2" }) != "VacuumHolster.png")
            throw new Exception("BlobForge runtime asset naming failed.");
        Console.WriteLine($"PASS project={name} png={png.Data.Length} sheet={sheet.Data.Length} pack={pack.Data.Length}");
    }
    finally { store.Delete(name); }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
var app = builder.Build();
var exporterApi = new Exporter(store);
var blobForgeBridge = new BlobForgeBridge(store);

app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Task.CompletedTask;
    });
    try { await next(); }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = ex is FileNotFoundException ? 404 : ex is IOException ? 409 : 400;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", () => new { ok = true, app = "Pixel Forge Studio", mcp = "pixel_forge", dataRoot = store.Root });
app.MapGet("/api/projects", () => store.List());
app.MapGet("/api/projects/{name}", async (string name) => await store.Load(name));
app.MapPost("/api/projects", async (CreateRequest r) => Results.Json(await store.Create(
    r.Name, r.Width, r.Height, r.Frames, r.Palette, r.Overwrite, r.Category)));
app.MapPut("/api/projects/{name}", async (string name, PixelProject project) =>
{
    if (!ProjectStore.SanitizeName(name).Equals(ProjectStore.SanitizeName(project.Name), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Project route and body names differ.");
    await store.Save(project); return Results.Json(project);
});
app.MapDelete("/api/projects/{name}", (string name) => new { deleted = store.Delete(name) });
app.MapGet("/api/projects/{name}/preview/{frame:int}", async (string name, int frame, int? scale) =>
{
    var p = await store.Load(name); return Results.File(PngCodec.RenderFrame(p, frame, scale ?? 1), "image/png", $"{p.Name}-frame-{frame}.png");
});
app.MapGet("/api/projects/{name}/contact-sheet", async (string name, int? scale, int? columns) =>
{
    var p = await store.Load(name);
    return Results.File(PngCodec.RenderContactSheet(p, scale ?? 4, columns ?? 0), "image/png", $"{p.Name}-contact-sheet.png");
});
app.MapGet("/api/projects/{name}/animation-contact-sheet", async (string name, string tag, int? scale, int? columns) =>
{
    var p = await store.Load(name);
    var clip = ProjectAnalyzer.Analyze(p).Animation.Clips.FirstOrDefault(clip => clip.Name.Equals(tag, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidDataException($"Animation tag '{tag}' does not exist.");
    return Results.File(PngCodec.RenderContactSheet(p, scale ?? 4, columns ?? 0, clip.PlaybackFrames), "image/png", $"{p.Name}-{clip.Name}-contact-sheet.png");
});
app.MapGet("/api/projects/{name}/comparison", async (string name, int? frame, int? scale) =>
{
    var p = await store.Load(name);
    if (p.Reference is null) throw new InvalidDataException("Choose an approved reference project first.");
    var reference = await store.Load(p.Reference.ProjectName);
    var referenceFrame = Math.Clamp(p.Reference.Frame, 0, reference.FrameCount - 1);
    return Results.File(PngCodec.RenderComparison(p, Math.Clamp(frame ?? 0, 0, p.FrameCount - 1), reference, referenceFrame, scale ?? 4),
        "image/png", $"{p.Name}-comparison.png");
});
app.MapGet("/api/projects/{name}/report", async (string name) => ProjectAnalyzer.Analyze(await store.Load(name)));
app.MapPost("/api/projects/{name}/preview-in-blobforge", async (string name, PreviewInBlobForgeRequest request) =>
    await blobForgeBridge.Export(await store.Load(name), request.Launch));
app.MapGet("/api/projects/{name}/export/{format}", async (string name, string format, int? scale, int? frame) =>
{
    var result = await exporterApi.Export(await store.Load(name), format, scale ?? 1, frame ?? 0);
    return Results.File(result.Data, result.ContentType, result.FileName);
});
app.MapFallbackToFile("index.html");

var port = 4876;
var portArg = args.FirstOrDefault(x => x.StartsWith("--port="));
if (portArg is not null && int.TryParse(portArg[7..], out var parsed)) port = parsed;
app.Urls.Add($"http://127.0.0.1:{port}");
Console.WriteLine($"Pixel Forge Studio: http://127.0.0.1:{port}");
Console.WriteLine($"Projects: {store.Root}");
await app.RunAsync();

public sealed record CreateRequest(string Name, int Width, int Height, int Frames = 1,
    string[]? Palette = null, bool Overwrite = false, string? Category = null);
public sealed record PreviewInBlobForgeRequest(bool Launch = false);
