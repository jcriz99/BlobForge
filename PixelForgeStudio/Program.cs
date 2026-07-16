using System.Text.Json;
using PixelForgeStudio.Core;
using PixelForgeStudio.Mcp;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "serve";
var dataRoot = Environment.GetEnvironmentVariable("PIXEL_FORGE_DATA")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "PixelForgeData");
var store = new ProjectStore(dataRoot);

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
        ProjectOps.Rect(p, p.Layers[0], 0, 1, 1, 6, 6, 8, false);
        ProjectOps.Line(p, p.Layers[0], 0, 1, 6, 6, 1, 13);
        await store.Save(p);
        var exporter = new Exporter(store);
        var png = await exporter.Export(p, "png", 2);
        var sheet = await exporter.Export(p, "spritesheet", 1);
        var pack = await exporter.Export(p, "pack", 1);
        if (png.Data.Length < 100 || sheet.Data.Length < 100 || pack.Data.Length < 300) throw new Exception("Export output was unexpectedly small.");
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

app.Use(async (ctx, next) =>
{
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
