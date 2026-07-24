using System.Text.Json;
using PixelForgeStudio.Core;

namespace PixelForgeStudio.Mcp;

public sealed class McpServer(ProjectStore store)
{
    private readonly Exporter _exporter = new(store);
    private readonly BlobForgeBridge _blobForge = new(store);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public async Task Run()
    {
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        string? line;
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement; var method = root.GetProperty("method").GetString() ?? "";
                if (!root.TryGetProperty("id", out var id)) continue;
                var result = await Dispatch(method, root.TryGetProperty("params", out var ps) ? ps : default);
                Write(new { jsonrpc = "2.0", id = JsonSerializer.Deserialize<object>(id.GetRawText()), result });
            }
            catch (Exception ex)
            {
                try
                {
                    using var failed = JsonDocument.Parse(line);
                    var id = failed.RootElement.TryGetProperty("id", out var i) ? JsonSerializer.Deserialize<object>(i.GetRawText()) : null;
                    Write(new { jsonrpc = "2.0", id, error = new { code = -32603, message = ex.Message } });
                }
                catch { Console.Error.WriteLine(ex); }
            }
        }
    }

    private async Task<object> Dispatch(string method, JsonElement ps) => method switch
    {
        "initialize" => new
        {
            protocolVersion = ps.ValueKind == JsonValueKind.Object && ps.TryGetProperty("protocolVersion", out var pv) ? pv.GetString() : "2025-06-18",
            capabilities = new { tools = new { listChanged = false }, resources = new { subscribe = false, listChanged = true } },
            serverInfo = new { name = "pixel-forge-studio", version = "1.0.0" },
            instructions = "Create and edit indexed-color pixel art projects, preview frames, and export PNG sprites or engine packs. Coordinates are zero-based from the top-left."
        },
        "ping" => new { },
        "tools/list" => new { tools = ToolDefinitions() },
        "tools/call" => await CallTool(ps.GetProperty("name").GetString()!, ps.TryGetProperty("arguments", out var a) ? a : Empty()),
        "resources/list" => new { resources = store.List().Select(p => ResourceFor(p)).ToArray() },
        "resources/read" => await ReadResource(ps.GetProperty("uri").GetString()!),
        _ => throw new InvalidOperationException($"Unsupported MCP method '{method}'.")
    };

    private static JsonElement Empty() => JsonDocument.Parse("{}").RootElement.Clone();
    private static object ResourceFor(object item)
    {
        var el = JsonSerializer.SerializeToElement(item);
        var name = (el.TryGetProperty("name", out var lower) ? lower : el.GetProperty("Name")).GetString()!;
        return new { uri = $"pixelforge://projects/{name}", name, mimeType = "application/json", description = $"Pixel Forge project {name}" };
    }

    private async Task<object> ReadResource(string uri)
    {
        const string prefix = "pixelforge://projects/";
        if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unknown resource URI.");
        var p = await store.Load(Uri.UnescapeDataString(uri[prefix.Length..]));
        return new { contents = new[] { new { uri, mimeType = "application/json", text = JsonSerializer.Serialize(p, _json) } } };
    }

    private async Task<object> CallTool(string name, JsonElement a)
    {
        object payload;
        switch (name)
        {
            case "project_list": payload = store.List(); break;
            case "project_create":
                payload = await store.Create(Str(a, "name"), Int(a, "width"), Int(a, "height"), Int(a, "frames", 1),
                    a.TryGetProperty("palette", out var pal) ? pal.EnumerateArray().Select(x => x.GetString()!) : null,
                    Bool(a, "overwrite"), a.TryGetProperty("category", out var category) ? category.GetString() : null); break;
            case "project_get":
            {
                var p = await store.Load(Str(a, "name")); var frame = Int(a, "frame", 0);
                payload = Bool(a, "includePixels") ? p : new { p.Name, p.Category, p.Width, p.Height, p.Palette, p.PaletteLocked, p.Reference, p.AttachmentPoints, p.Validation, p.RuntimeAssetName, Layers = p.Layers.Select((l, i) => new { index = i, l.Id, l.Name, l.Visible, l.Opacity, l.FrameInvariant }), p.FrameDurationsMs, p.Tags, p.Revision, p.UpdatedAt, ascii = ProjectOps.Ascii(p, frame) };
                break;
            }
            case "project_delete": payload = new { deleted = store.Delete(Str(a, "name")) }; break;
            case "project_preview":
            {
                var p = await store.Load(Str(a, "name")); var frame = Int(a, "frame", 0); var png = PngCodec.RenderFrame(p, frame, Int(a, "scale", 8));
                return new { content = new object[] { TextContent($"{p.Name}, frame {frame}, {p.Width}x{p.Height}, revision {p.Revision}"), new { type = "image", data = Convert.ToBase64String(png), mimeType = "image/png" } } };
            }
            case "project_contact_sheet":
            {
                var p = await store.Load(Str(a, "name")); var png = PngCodec.RenderContactSheet(p, Int(a, "scale", 4), Int(a, "columns", 0));
                return new { content = new object[] { TextContent($"{p.Name} contact sheet, {p.FrameCount} frames"), new { type = "image", data = Convert.ToBase64String(png), mimeType = "image/png" } } };
            }
            case "project_comparison":
            {
                var p = await store.Load(Str(a, "name"));
                var referenceName = a.TryGetProperty("reference", out var rn) ? rn.GetString() : p.Reference?.ProjectName;
                if (string.IsNullOrWhiteSpace(referenceName)) throw new InvalidDataException("Provide reference or configure the project's approved reference.");
                var reference = await store.Load(referenceName); var frame = Int(a, "frame", 0);
                var referenceFrame = Int(a, "referenceFrame", p.Reference?.Frame ?? 0);
                var png = PngCodec.RenderComparison(p, frame, reference, referenceFrame, Int(a, "scale", 4));
                return new { content = new object[] { TextContent($"{p.Name} beside approved reference {reference.Name}"), new { type = "image", data = Convert.ToBase64String(png), mimeType = "image/png" } } };
            }
            case "project_report":
                payload = ProjectAnalyzer.Analyze(await store.Load(Str(a, "name"))); break;
            case "animation_report":
                payload = ProjectAnalyzer.Analyze(await store.Load(Str(a, "name"))).Animation; break;
            case "animation_contact_sheet":
            {
                var p = await store.Load(Str(a, "name")); var tagName = Str(a, "tag");
                var clip = ProjectAnalyzer.Analyze(p).Animation.Clips.FirstOrDefault(clip => clip.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"Animation tag '{tagName}' does not exist.");
                var png = PngCodec.RenderContactSheet(p, Int(a, "scale", 4), Int(a, "columns", 0), clip.PlaybackFrames);
                return new { content = new object[] { TextContent($"{p.Name} animation '{clip.Name}', {clip.PlaybackFrames.Count} playback frames, {clip.TotalDurationMs}ms"), new { type = "image", data = Convert.ToBase64String(png), mimeType = "image/png" } } };
            }
            case "project_reference_set":
            {
                var p = await store.Load(Str(a, "name")); var referenceName = Str(a, "reference", "");
                if (string.IsNullOrWhiteSpace(referenceName)) p.Reference = null;
                else
                {
                    var reference = await store.Load(referenceName);
                    p.Reference = new ArtReference { ProjectName = reference.Name, Frame = Math.Clamp(Int(a, "frame", 0), 0, reference.FrameCount - 1), Opacity = Math.Clamp(Double(a, "opacity", .35), 0, 1) };
                }
                await store.Save(p); payload = new { p.Reference, p.Revision }; break;
            }
            case "palette_add":
            {
                var p = await store.Load(Str(a, "name")); var index = ProjectOps.Color(p, Str(a, "color")); await store.Save(p); payload = new { index, color = p.Palette[index], p.Revision }; break;
            }
            case "palette_set":
            {
                var p = await store.Load(Str(a, "name")); var index = Int(a, "index"); if (index < 0 || index >= p.Palette.Count) throw new ArgumentOutOfRangeException("index");
                p.Palette[index] = ColorUtil.NormalizeHex(Str(a, "color")); await store.Save(p); payload = new { index, color = p.Palette[index], p.Revision }; break;
            }
            case "palette_lock":
            {
                var p = await store.Load(Str(a, "name")); p.PaletteLocked = Bool(a, "locked", true); await store.Save(p);
                payload = new { p.PaletteLocked, colors = p.Palette.Count, p.Revision }; break;
            }
            case "validation_set":
            {
                var p = await store.Load(Str(a, "name"));
                if (a.TryGetProperty("tileX", out var tx)) p.Validation.TileX = tx.GetBoolean();
                if (a.TryGetProperty("tileY", out var ty)) p.Validation.TileY = ty.GetBoolean();
                if (a.TryGetProperty("loop", out var loop)) p.Validation.Loop = loop.GetBoolean();
                if (a.TryGetProperty("frameConsistency", out var fc)) p.Validation.FrameConsistency = fc.GetBoolean();
                if (a.TryGetProperty("maxOccupancyDriftPercent", out var occupancy)) p.Validation.MaxOccupancyDriftPercent = occupancy.GetDouble();
                if (a.TryGetProperty("maxBoundsDriftPixels", out var bounds)) p.Validation.MaxBoundsDriftPixels = bounds.GetInt32();
                if (a.TryGetProperty("attachmentMotion", out var attachmentMotion)) p.Validation.AttachmentMotion = attachmentMotion.GetBoolean();
                if (a.TryGetProperty("maxAttachmentStepPixels", out var attachmentStep)) p.Validation.MaxAttachmentStepPixels = attachmentStep.GetDouble();
                await store.Save(p); payload = new { p.Validation, report = ProjectAnalyzer.Analyze(p), p.Revision }; break;
            }
            case "attachment_set":
            {
                var p = await store.Load(Str(a, "name")); var pointName = Str(a, "pointName").Trim();
                int? attachmentFrame = a.TryGetProperty("frame", out var af) ? af.GetInt32() : null;
                p.AttachmentPoints.RemoveAll(point => point.Name.Equals(pointName, StringComparison.OrdinalIgnoreCase) && point.Frame == attachmentFrame);
                p.AttachmentPoints.Add(new AttachmentPoint { Name = pointName, X = Int(a, "x"), Y = Int(a, "y"), Frame = attachmentFrame });
                await store.Save(p); payload = new { p.AttachmentPoints, p.Revision }; break;
            }
            case "attachment_delete":
            {
                var p = await store.Load(Str(a, "name")); var pointName = Str(a, "pointName");
                var removed = a.TryGetProperty("frame", out var attachmentFrame)
                    ? p.AttachmentPoints.RemoveAll(point => point.Name.Equals(pointName, StringComparison.OrdinalIgnoreCase) && point.Frame == attachmentFrame.GetInt32())
                    : p.AttachmentPoints.RemoveAll(point => point.Name.Equals(pointName, StringComparison.OrdinalIgnoreCase));
                await store.Save(p); payload = new { removed, p.AttachmentPoints, p.Revision }; break;
            }
            case "layer_add":
            {
                var p = await store.Load(Str(a, "name")); var l = PixelLayer.Create(Str(a, "layerName", "Layer"), p.FrameCount, p.Width * p.Height);
                var index = Math.Clamp(Int(a, "index", p.Layers.Count), 0, p.Layers.Count); p.Layers.Insert(index, l); await store.Save(p); payload = new { index, l.Id, l.Name, p.Revision }; break;
            }
            case "layer_update":
            {
                var p = await store.Load(Str(a, "name")); var l = ProjectOps.Layer(p, Str(a, "layer", ""));
                if (a.TryGetProperty("newName", out var nn)) l.Name = nn.GetString()?.Trim() ?? l.Name;
                if (a.TryGetProperty("visible", out var v)) l.Visible = v.GetBoolean();
                if (a.TryGetProperty("opacity", out var o)) l.Opacity = Math.Clamp(o.GetDouble(), 0, 1);
                if (a.TryGetProperty("frameInvariant", out var invariant)) l.FrameInvariant = invariant.GetBoolean();
                await store.Save(p); payload = new { l.Id, l.Name, l.Visible, l.Opacity, l.FrameInvariant, p.Revision }; break;
            }
            case "layer_delete":
            {
                var p = await store.Load(Str(a, "name")); if (p.Layers.Count == 1) throw new InvalidOperationException("Cannot delete the final layer.");
                var l = ProjectOps.Layer(p, Str(a, "layer", "")); p.Layers.Remove(l); await store.Save(p); payload = new { deleted = l.Name, p.Revision }; break;
            }
            case "frame_add":
            {
                var p = await store.Load(Str(a, "name"));
                var index = AnimationOps.AddFrame(p, Int(a, "index", p.FrameCount), Int(a, "copyFrom", -1), Int(a, "durationMs", 100));
                await store.Save(p); payload = new { index, frames = p.FrameCount, p.Revision }; break;
            }
            case "frame_delete":
            {
                var p = await store.Load(Str(a, "name")); AnimationOps.DeleteFrame(p, Int(a, "frame"));
                await store.Save(p); payload = new { frames = p.FrameCount, p.Tags, p.AttachmentPoints, p.Revision }; break;
            }
            case "frame_duration":
            {
                var p = await store.Load(Str(a, "name")); var frame = Int(a, "frame"); p.FrameDurationsMs[frame] = Math.Clamp(Int(a, "durationMs"), 16, 60000); await store.Save(p); payload = new { frame, durationMs = p.FrameDurationsMs[frame], p.Revision }; break;
            }
            case "frame_durations_set":
            {
                var p = await store.Load(Str(a, "name")); var durations = a.GetProperty("durationsMs").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                AnimationOps.SetDurations(p, Int(a, "from", 0), durations); await store.Save(p); payload = new { p.FrameDurationsMs, p.Revision }; break;
            }
            case "animation_tag_set":
            {
                var p = await store.Load(Str(a, "name")); var tag = AnimationOps.SetTag(p, Str(a, "tag"), Int(a, "from"), Int(a, "to"), Str(a, "direction", "forward"), Bool(a, "loop", true));
                await store.Save(p); payload = new { tag, report = ProjectAnalyzer.Analyze(p).Animation, p.Revision }; break;
            }
            case "animation_tag_delete":
            {
                var p = await store.Load(Str(a, "name")); var tagName = Str(a, "tag");
                var removed = p.Tags.RemoveAll(tag => tag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)); await store.Save(p); payload = new { removed, p.Tags, p.Revision }; break;
            }
            case "pixels_set":
            {
                var p = await store.Load(Str(a, "name")); var l = ProjectOps.Layer(p, Str(a, "layer", "")); var frame = Int(a, "frame", 0); var count = 0;
                foreach (var point in a.GetProperty("pixels").EnumerateArray())
                {
                    var color = ProjectOps.Color(p, point.TryGetProperty("color", out var c) ? c.GetString() : (a.TryGetProperty("color", out var gc) ? gc.GetString() : null),
                        point.TryGetProperty("paletteIndex", out var pi) ? pi.GetInt32() : (a.TryGetProperty("paletteIndex", out var gpi) ? gpi.GetInt32() : null), true);
                    ProjectOps.SetPixel(p, l, frame, point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32(), color); count++;
                }
                await store.Save(p); payload = new { changed = count, p.Revision }; break;
            }
            case "draw_line": case "draw_rect": case "draw_ellipse": case "flood_fill":
            {
                var p = await store.Load(Str(a, "name")); var l = ProjectOps.Layer(p, Str(a, "layer", "")); var frame = Int(a, "frame", 0);
                var color = ProjectOps.Color(p, a.TryGetProperty("color", out var c) ? c.GetString() : null, a.TryGetProperty("paletteIndex", out var pi) ? pi.GetInt32() : null, true);
                if (name == "draw_line") ProjectOps.Line(p, l, frame, Int(a, "x1"), Int(a, "y1"), Int(a, "x2"), Int(a, "y2"), color);
                if (name == "draw_rect") ProjectOps.Rect(p, l, frame, Int(a, "x"), Int(a, "y"), Int(a, "width"), Int(a, "height"), color, Bool(a, "filled"));
                if (name == "draw_ellipse") ProjectOps.Ellipse(p, l, frame, Int(a, "cx"), Int(a, "cy"), Int(a, "rx"), Int(a, "ry"), color, Bool(a, "filled"));
                var filled = name == "flood_fill" ? ProjectOps.Fill(p, l, frame, Int(a, "x"), Int(a, "y"), color) : 0;
                await store.Save(p); payload = new { changed = name == "flood_fill" ? filled : -1, p.Revision }; break;
            }
            case "canvas_clear":
            {
                var p = await store.Load(Str(a, "name")); var l = ProjectOps.Layer(p, Str(a, "layer", "")); var frame = Int(a, "frame", 0);
                Array.Fill(l.Frames[frame], -1); await store.Save(p); payload = new { cleared = true, p.Revision }; break;
            }
            case "transform":
            {
                var p = await store.Load(Str(a, "name")); var l = ProjectOps.Layer(p, Str(a, "layer", "")); ProjectOps.Transform(p, l, Int(a, "frame", 0), Str(a, "operation"), Int(a, "amount", 1));
                await store.Save(p); payload = new { transformed = true, p.Revision }; break;
            }
            case "region_copy":
            {
                var p = await store.Load(Str(a, "name")); var l = ProjectOps.Layer(p, Str(a, "layer", ""));
                var width = Int(a, "width"); var height = Int(a, "height");
                payload = new { width, height, pixels = ProjectOps.CopyRegion(p, l, Int(a, "frame", 0), Int(a, "x"), Int(a, "y"), width, height) }; break;
            }
            case "region_paste":
            {
                var p = await store.Load(Str(a, "name")); var l = ProjectOps.Layer(p, Str(a, "layer", ""));
                var pixels = a.GetProperty("pixels").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                ProjectOps.PasteRegion(p, l, Int(a, "frame", 0), Int(a, "x"), Int(a, "y"), Int(a, "width"), Int(a, "height"), pixels, Bool(a, "includeTransparent", true));
                await store.Save(p); payload = new { pasted = pixels.Length, p.Revision }; break;
            }
            case "region_transform":
            {
                var p = await store.Load(Str(a, "name")); var l = ProjectOps.Layer(p, Str(a, "layer", ""));
                ProjectOps.TransformRegion(p, l, Int(a, "frame", 0), Int(a, "x"), Int(a, "y"), Int(a, "width"), Int(a, "height"), Str(a, "operation"), Int(a, "amount", 1));
                await store.Save(p); payload = new { transformed = true, p.Revision }; break;
            }
            case "operation_batch":
                payload = await ApplyOperationBatch(a); break;
            case "export_asset":
            {
                var p = await store.Load(Str(a, "name")); var result = await _exporter.Export(p, Str(a, "format", "png"), Int(a, "scale", 1), Int(a, "frame", 0));
                payload = new { result.FileName, path = Path.Combine(store.ExportRoot, p.Name, result.FileName), bytes = result.Data.Length, result.ContentType }; break;
            }
            case "preview_in_blobforge":
                payload = await _blobForge.Export(await store.Load(Str(a, "name")), Bool(a, "launch")); break;
            default: throw new InvalidOperationException($"Unknown tool '{name}'.");
        }
        return new { content = new[] { TextContent(JsonSerializer.Serialize(payload, _json)) }, structuredContent = payload };
    }

    private static object TextContent(string text) => new { type = "text", text };
    private static string Str(JsonElement a, string name, string? fallback = null) => a.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString()! : fallback ?? throw new InvalidDataException($"Missing '{name}'.");
    private static int Int(JsonElement a, string name, int fallback = int.MinValue) => a.TryGetProperty(name, out var v) ? v.GetInt32() : fallback != int.MinValue ? fallback : throw new InvalidDataException($"Missing '{name}'.");
    private static bool Bool(JsonElement a, string name, bool fallback = false) => a.TryGetProperty(name, out var v) ? v.GetBoolean() : fallback;
    private static double Double(JsonElement a, string name, double fallback) => a.TryGetProperty(name, out var v) ? v.GetDouble() : fallback;

    private async Task<object> ApplyOperationBatch(JsonElement arguments)
    {
        var project = await store.Load(Str(arguments, "name"));
        var revisionBefore = project.Revision;
        if (arguments.TryGetProperty("expectedRevision", out var expected) && expected.GetInt64() != revisionBefore)
            throw new InvalidOperationException($"Stale edit: expected revision {expected.GetInt64()}, but project is revision {revisionBefore}. Reload before editing.");

        var snapshots = project.Layers.SelectMany((layer, layerIndex) =>
            layer.Frames.Select((pixels, frame) => new SurfaceSnapshot(layerIndex, frame, (int[])pixels.Clone()))).ToArray();
        var operations = arguments.GetProperty("operations").EnumerateArray().ToArray();
        foreach (var operation in operations)
        {
            var type = Str(operation, "type").ToLowerInvariant();
            var layer = ProjectOps.Layer(project, Str(operation, "layer", ""));
            var frame = Int(operation, "frame", 0);
            var color = type is "clear" or "region-transform" ? -1 : ProjectOps.Color(project,
                operation.TryGetProperty("color", out var colorValue) ? colorValue.GetString() : null,
                operation.TryGetProperty("paletteIndex", out var paletteIndex) ? paletteIndex.GetInt32() : null, true);
            switch (type)
            {
                case "pixel": ProjectOps.SetPixel(project, layer, frame, Int(operation, "x"), Int(operation, "y"), color); break;
                case "line": ProjectOps.Line(project, layer, frame, Int(operation, "x1"), Int(operation, "y1"), Int(operation, "x2"), Int(operation, "y2"), color); break;
                case "rect": ProjectOps.Rect(project, layer, frame, Int(operation, "x"), Int(operation, "y"), Int(operation, "width"), Int(operation, "height"), color, Bool(operation, "filled")); break;
                case "ellipse": ProjectOps.Ellipse(project, layer, frame, Int(operation, "cx"), Int(operation, "cy"), Int(operation, "rx"), Int(operation, "ry"), color, Bool(operation, "filled")); break;
                case "fill": ProjectOps.Fill(project, layer, frame, Int(operation, "x"), Int(operation, "y"), color); break;
                case "clear": Array.Fill(layer.Frames[frame], -1); break;
                case "region-transform": ProjectOps.TransformRegion(project, layer, frame, Int(operation, "x"), Int(operation, "y"), Int(operation, "width"), Int(operation, "height"), Str(operation, "operation"), Int(operation, "amount", 1)); break;
                default: throw new InvalidDataException($"Unsupported batch operation '{type}'.");
            }
        }

        var surfaces = new List<object>();
        var totalChanged = 0;
        foreach (var snapshot in snapshots)
        {
            var current = project.Layers[snapshot.LayerIndex].Frames[snapshot.Frame];
            var changed = 0; var minX = project.Width; var minY = project.Height; var maxX = -1; var maxY = -1;
            for (var index = 0; index < current.Length; index++)
            {
                if (current[index] == snapshot.Pixels[index]) continue;
                changed++; var x = index % project.Width; var y = index / project.Width;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            if (changed == 0) continue;
            totalChanged += changed;
            surfaces.Add(new { layer = project.Layers[snapshot.LayerIndex].Name, frame = snapshot.Frame, changedPixels = changed,
                bounds = new PixelBounds(minX, minY, maxX - minX + 1, maxY - minY + 1) });
        }

        var dryRun = Bool(arguments, "dryRun", true);
        if (!dryRun) await store.Save(project);
        return new { dryRun, operationCount = operations.Length, changedPixels = totalChanged, surfaces,
            revisionBefore, revisionAfter = dryRun ? revisionBefore : project.Revision, report = ProjectAnalyzer.Analyze(project) };
    }

    private sealed record SurfaceSnapshot(int LayerIndex, int Frame, int[] Pixels);

    private static object[] ToolDefinitions()
    {
        static object T(string name, string description, object properties, string[]? required = null) => new { name, description, inputSchema = new { type = "object", properties, required = required ?? [] } };
        var project = new { name = new { type = "string", description = "Project name" } };
        var target = new { name = project.name, layer = new { type = "string", description = "Layer name, ID, or zero-based index; defaults to top layer" }, frame = new { type = "integer", minimum = 0, description = "Zero-based frame" } };
        var color = new { color = new { type = "string", description = "#RRGGBB or transparent" }, paletteIndex = new { type = "integer", minimum = -1, description = "Palette index; -1 is transparent" } };
        return
        [
            T("project_list", "List all local pixel-art projects.", new { }),
            T("project_create", "Create a new indexed-color project.", new { name = project.name, category = new { type="string", @enum=ProjectCategory.All, @default=ProjectCategory.MiscArt }, width = new { type="integer", minimum=1, maximum=512 }, height = new { type="integer", minimum=1, maximum=512 }, frames = new { type="integer", minimum=1, maximum=256, @default=1 }, palette = new { type="array", items=new { type="string" } }, overwrite = new { type="boolean", @default=false } }, ["name","width","height"]),
            T("project_get", "Read project metadata and an ASCII composite; optionally include all indexed pixels.", new { name=project.name, frame=target.frame, includePixels=new { type="boolean", @default=false } }, ["name"]),
            T("project_preview", "Return a rendered PNG preview directly to the MCP client.", new { name=project.name, frame=target.frame, scale=new { type="integer", minimum=1, maximum=64, @default=8 } }, ["name"]),
            T("project_contact_sheet", "Return every animation frame in one nearest-neighbor contact-sheet preview.", new { name=project.name, scale=new { type="integer", minimum=1, maximum=32, @default=4 }, columns=new { type="integer", minimum=0, @default=0 } }, ["name"]),
            T("project_comparison", "Render the project beside an approved reference project.", new { name=project.name, reference=new { type="string" }, frame=target.frame, referenceFrame=new { type="integer", minimum=0, @default=0 }, scale=new { type="integer", minimum=1, maximum=32, @default=4 } }, ["name"]),
            T("project_report", "Report dimensions, silhouettes, transparency, palette usage, timings, tile seams, loop endpoints, cross-frame drift, static-layer violations, attachments, and validation issues.", project, ["name"]),
            T("animation_report", "Report named clips, playback order, total timing, per-frame pixel transitions, duplicate holds, loop transitions, and attachment motion tracks.", project, ["name"]),
            T("animation_contact_sheet", "Preview one named animation tag in its actual forward, reverse, or ping-pong playback order.", new { name=project.name, tag=new {type="string"}, scale=new {type="integer",minimum=1,maximum=32,@default=4}, columns=new {type="integer",minimum=0,@default=0} }, ["name","tag"]),
            T("project_reference_set", "Set or clear the approved Pixel Forge reference used for overlays and comparisons.", new { name=project.name, reference=new { type="string", description="Project name; empty clears the reference" }, frame=new { type="integer", minimum=0, @default=0 }, opacity=new { type="number", minimum=0, maximum=1, @default=.35 } }, ["name","reference"]),
            T("project_delete", "Permanently delete a pixel-art project.", project, ["name"]),
            T("palette_add", "Add a color if needed and return its palette index.", new { name=project.name, color=color.color }, ["name","color"]),
            T("palette_set", "Replace a palette color, updating every pixel that uses that index.", new { name=project.name, index=new { type="integer", minimum=0 }, color=color.color }, ["name","index","color"]),
            T("palette_lock", "Lock or unlock the palette. Locked projects reject colors not already approved.", new { name=project.name, locked=new { type="boolean", @default=true } }, ["name"]),
            T("validation_set", "Configure tile, loop, cross-frame, and attachment-motion validation for a project.", new { name=project.name, tileX=new { type="boolean" }, tileY=new { type="boolean" }, loop=new { type="boolean" }, frameConsistency=new { type="boolean" }, maxOccupancyDriftPercent=new { type="number", minimum=0 }, maxBoundsDriftPixels=new { type="integer", minimum=0 }, attachmentMotion=new {type="boolean"}, maxAttachmentStepPixels=new {type="number",minimum=0} }, ["name"]),
            T("attachment_set", "Create or update a named machinery attachment point.", new { name=project.name, pointName=new { type="string" }, x=new { type="integer", minimum=0 }, y=new { type="integer", minimum=0 }, frame=new { type="integer", minimum=0 } }, ["name","pointName","x","y"]),
            T("attachment_delete", "Delete a named attachment point across all frames, or only the specified frame.", new { name=project.name, pointName=new { type="string" }, frame=new {type="integer",minimum=0} }, ["name","pointName"]),
            T("layer_add", "Add a transparent layer.", new { name=project.name, layerName=new { type="string" }, index=new { type="integer", minimum=0 } }, ["name"]),
            T("layer_update", "Rename a layer, change visibility/opacity, or mark it frame-invariant so animation drift becomes an error.", new { name=project.name, layer=target.layer, newName=new { type="string" }, visible=new { type="boolean" }, opacity=new { type="number", minimum=0, maximum=1 }, frameInvariant=new { type="boolean" } }, ["name","layer"]),
            T("layer_delete", "Delete a layer.", new { name=project.name, layer=target.layer }, ["name","layer"]),
            T("frame_add", "Insert a blank frame or duplicate an existing one.", new { name=project.name, index=new { type="integer", minimum=0 }, copyFrom=new { type="integer", minimum=-1 }, durationMs=new { type="integer", minimum=16, @default=100 } }, ["name"]),
            T("frame_delete", "Delete an animation frame.", new { name=project.name, frame=target.frame }, ["name","frame"]),
            T("frame_duration", "Set animation frame duration in milliseconds.", new { name=project.name, frame=target.frame, durationMs=new { type="integer", minimum=16, maximum=60000 } }, ["name","frame","durationMs"]),
            T("frame_durations_set", "Set an atomic range of per-frame durations for deliberate holds, anticipation, impacts, and recovery timing.", new { name=project.name, from=new {type="integer",minimum=0,@default=0}, durationsMs=new {type="array",minItems=1,items=new {type="integer",minimum=16,maximum=60000}} }, ["name","durationsMs"]),
            T("animation_tag_set", "Create or update a named animation clip with a frame range, playback direction, and loop mode.", new { name=project.name, tag=new {type="string"}, from=new {type="integer",minimum=0}, to=new {type="integer",minimum=0}, direction=new {type="string",@enum=new[]{"forward","reverse","pingpong"},@default="forward"}, loop=new {type="boolean",@default=true} }, ["name","tag","from","to"]),
            T("animation_tag_delete", "Delete a named animation clip tag without deleting its frames.", new { name=project.name, tag=new {type="string"} }, ["name","tag"]),
            T("pixels_set", "Set many individual pixels in one atomic edit. Each point may override the shared color.", new { name=project.name, layer=target.layer, frame=target.frame, color=color.color, paletteIndex=color.paletteIndex, pixels=new { type="array", minItems=1, items=new { type="object", properties=new { x=new { type="integer" }, y=new { type="integer" }, color=color.color, paletteIndex=color.paletteIndex }, required=new[]{"x","y"} } } }, ["name","pixels"]),
            T("draw_line", "Draw a one-pixel Bresenham line.", new { name=project.name, layer=target.layer, frame=target.frame, x1=new {type="integer"}, y1=new {type="integer"}, x2=new {type="integer"}, y2=new {type="integer"}, color=color.color, paletteIndex=color.paletteIndex }, ["name","x1","y1","x2","y2"]),
            T("draw_rect", "Draw an outline or filled pixel rectangle.", new { name=project.name, layer=target.layer, frame=target.frame, x=new {type="integer"}, y=new {type="integer"}, width=new {type="integer",minimum=1}, height=new {type="integer",minimum=1}, filled=new {type="boolean",@default=false}, color=color.color, paletteIndex=color.paletteIndex }, ["name","x","y","width","height"]),
            T("draw_ellipse", "Draw an outline or filled pixel ellipse.", new { name=project.name, layer=target.layer, frame=target.frame, cx=new {type="integer"}, cy=new {type="integer"}, rx=new {type="integer",minimum=0}, ry=new {type="integer",minimum=0}, filled=new {type="boolean",@default=false}, color=color.color, paletteIndex=color.paletteIndex }, ["name","cx","cy","rx","ry"]),
            T("flood_fill", "Flood-fill a contiguous area on one layer.", new { name=project.name, layer=target.layer, frame=target.frame, x=new {type="integer"}, y=new {type="integer"}, color=color.color, paletteIndex=color.paletteIndex }, ["name","x","y"]),
            T("canvas_clear", "Clear one layer frame to transparency.", new { name=project.name, layer=target.layer, frame=target.frame }, ["name"]),
            T("transform", "Flip or wrap-shift one layer frame.", new { name=project.name, layer=target.layer, frame=target.frame, operation=new {type="string", @enum=new[]{"flip-horizontal","flip-vertical","shift-x","shift-y"}}, amount=new {type="integer",@default=1} }, ["name","operation"]),
            T("region_copy", "Copy an indexed rectangular selection/mask and return its palette indices.", new { name=project.name, layer=target.layer, frame=target.frame, x=new {type="integer",minimum=0}, y=new {type="integer",minimum=0}, width=new {type="integer",minimum=1}, height=new {type="integer",minimum=1} }, ["name","x","y","width","height"]),
            T("region_paste", "Paste indexed pixels into a rectangular destination.", new { name=project.name, layer=target.layer, frame=target.frame, x=new {type="integer"}, y=new {type="integer"}, width=new {type="integer",minimum=1}, height=new {type="integer",minimum=1}, pixels=new {type="array",items=new {type="integer",minimum=-1}}, includeTransparent=new {type="boolean",@default=true} }, ["name","x","y","width","height","pixels"]),
            T("region_transform", "Transform pixels only inside a rectangular selection/mask.", new { name=project.name, layer=target.layer, frame=target.frame, x=new {type="integer",minimum=0}, y=new {type="integer",minimum=0}, width=new {type="integer",minimum=1}, height=new {type="integer",minimum=1}, operation=new {type="string",@enum=new[]{"flip-horizontal","flip-vertical","rotate-180","shift-x","shift-y"}}, amount=new {type="integer",@default=1} }, ["name","x","y","width","height","operation"]),
            T("operation_batch", "Atomically dry-run or commit a mixed structured edit. Returns exact changed-pixel counts and bounds; expectedRevision prevents stale edits.", new { name=project.name, expectedRevision=new {type="integer",minimum=0}, dryRun=new {type="boolean",@default=true}, operations=new {type="array",minItems=1,items=new {type="object",properties=new { type=new {type="string",@enum=new[]{"pixel","line","rect","ellipse","fill","clear","region-transform"}}, layer=target.layer, frame=target.frame, x=new {type="integer"}, y=new {type="integer"}, x1=new {type="integer"}, y1=new {type="integer"}, x2=new {type="integer"}, y2=new {type="integer"}, width=new {type="integer",minimum=1}, height=new {type="integer",minimum=1}, cx=new {type="integer"}, cy=new {type="integer"}, rx=new {type="integer",minimum=0}, ry=new {type="integer",minimum=0}, filled=new {type="boolean"}, color=color.color, paletteIndex=color.paletteIndex, operation=new {type="string"}, amount=new {type="integer"}},required=new[]{"type"}}} }, ["name","operations"]),
            T("export_asset", "Export frame PNG, spritesheet PNG, JSON, or a Godot/Unity/MonoGame engine pack ZIP.", new { name=project.name, format=new {type="string",@enum=new[]{"png","spritesheet","pack","json"}}, scale=new {type="integer",minimum=1,maximum=64,@default=1}, frame=target.frame }, ["name","format"]),
            T("preview_in_blobforge", "Export to BlobForge source/current-build Assets and optionally launch the current playable build.", new { name=project.name, launch=new {type="boolean",@default=false} }, ["name"])
        ];
    }

    private static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
}
