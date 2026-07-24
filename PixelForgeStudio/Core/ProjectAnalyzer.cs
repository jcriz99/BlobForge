namespace PixelForgeStudio.Core;

public static class ProjectAnalyzer
{
    public static ProjectReport Analyze(PixelProject project)
    {
        var paletteUse = new long[project.Palette.Count];
        foreach (var layer in project.Layers)
        foreach (var frame in layer.Frames)
        foreach (var index in frame)
            if (index >= 0 && index < paletteUse.Length) paletteUse[index]++;

        var frames = new List<FrameReport>(project.FrameCount);
        for (var frame = 0; frame < project.FrameCount; frame++)
        {
            var rgba = PngCodec.Composite(project, frame);
            var occupied = 0; var minX = project.Width; var minY = project.Height; var maxX = -1; var maxY = -1;
            for (var y = 0; y < project.Height; y++) for (var x = 0; x < project.Width; x++)
            {
                if (rgba[(y * project.Width + x) * 4 + 3] == 0) continue;
                occupied++; minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            frames.Add(new FrameReport(frame, project.FrameDurationsMs[frame], occupied,
                project.Width * project.Height - occupied, occupied == 0 ? null : new PixelBounds(minX, minY, maxX - minX + 1, maxY - minY + 1),
                CountComponents(rgba, project.Width, project.Height)));
        }

        var first = PngCodec.Composite(project, 0);
        var last = PngCodec.Composite(project, project.FrameCount - 1);
        var loopDelta = project.FrameCount == 1 ? 0 : PixelDifference(first, last);
        var seamX = CountSeamMismatch(first, project.Width, project.Height, horizontal: true);
        var seamY = CountSeamMismatch(first, project.Width, project.Height, horizontal: false);
        var issues = new List<ValidationIssue>();
        foreach (var frame in frames.Where(f => f.OccupiedPixels == 0)) issues.Add(new("warning", "empty-frame", $"Frame {frame.Frame + 1} is empty."));
        if (project.Validation.TileX && seamX > 0) issues.Add(new("warning", "tile-x-seam", $"Left/right tile seam has {seamX} mismatched rows."));
        if (project.Validation.TileY && seamY > 0) issues.Add(new("warning", "tile-y-seam", $"Top/bottom tile seam has {seamY} mismatched columns."));
        if (project.Validation.Loop && project.FrameCount < 2) issues.Add(new("warning", "single-frame-loop", "Loop validation needs at least two frames."));
        if (project.Validation.Loop && loopDelta > 0) issues.Add(new("info", "loop-endpoint-delta", $"First and last frames differ at {loopDelta} pixels; inspect the loop transition in the contact sheet."));
        if (project.Reference is not null && project.Reference.ProjectName.Equals(project.Name, StringComparison.OrdinalIgnoreCase))
            issues.Add(new("warning", "self-reference", "The approved reference points to this same project."));
        foreach (var point in project.AttachmentPoints.Where(a => a.X < 0 || a.X >= project.Width || a.Y < 0 || a.Y >= project.Height))
            issues.Add(new("error", "attachment-outside", $"Attachment '{point.Name}' is outside the canvas."));

        var consistency = AnalyzeFrameConsistency(project, frames, issues);
        var animation = AnalyzeAnimation(project, issues);

        return new ProjectReport(project.Name, project.Width, project.Height, project.FrameCount,
            project.PaletteLocked, paletteUse.Select((count, index) => new PaletteUse(index, project.Palette[index], count)).ToArray(),
            frames, seamX, seamY, loopDelta, consistency, animation, project.AttachmentPoints, issues);
    }

    private static AnimationReport AnalyzeAnimation(PixelProject project, List<ValidationIssue> issues)
    {
        var composites = Enumerable.Range(0, project.FrameCount).Select(frame => PngCodec.Composite(project, frame)).ToArray();
        var transitions = new List<FrameTransitionReport>();
        for (var frame = 0; frame < project.FrameCount - 1; frame++)
            transitions.Add(new(frame, frame + 1, PixelDifference(composites[frame], composites[frame + 1])));
        if (project.FrameCount > 1) transitions.Add(new(project.FrameCount - 1, 0, PixelDifference(composites[^1], composites[0])));
        var duplicates = transitions.Count(transition => transition.To != 0 && transition.ChangedPixels == 0);
        if (duplicates > 0) issues.Add(new("info", "duplicate-adjacent-frames",
            $"{duplicates} adjacent frame transition(s) are visually identical; use a longer frame duration unless separate frames are required by gameplay."));

        var tags = project.Tags.Count > 0 ? project.Tags :
            [new AnimationTag { Name = "all", From = 0, To = project.FrameCount - 1, Direction = "forward", Loop = project.Validation.Loop }];
        var clips = tags.Select(tag =>
        {
            var forward = Enumerable.Range(tag.From, tag.To - tag.From + 1).ToList();
            List<int> playback = tag.Direction switch
            {
                "reverse" => forward.AsEnumerable().Reverse().ToList(),
                "pingpong" when forward.Count > 1 => forward.Concat(forward.Skip(1).SkipLast(1).Reverse()).ToList(),
                _ => forward
            };
            var loopTransition = tag.Loop && playback.Count > 1 ? PixelDifference(composites[playback[^1]], composites[playback[0]]) : 0;
            return new AnimationClipReport(tag.Name, tag.From, tag.To, tag.Direction, tag.Loop, playback,
                playback.Sum(frame => project.FrameDurationsMs[frame]), loopTransition);
        }).ToArray();

        var tracks = project.AttachmentPoints.Where(point => point.Frame.HasValue)
            .GroupBy(point => point.Name, StringComparer.OrdinalIgnoreCase).Select(group =>
            {
                var points = group.OrderBy(point => point.Frame).ToArray();
                var maxStep = 0d;
                for (var i = 1; i < points.Length; i++) maxStep = Math.Max(maxStep, Distance(points[i - 1], points[i]));
                var loopStep = points.Length > 1 ? Distance(points[^1], points[0]) : 0;
                var missing = project.FrameCount - points.Select(point => point.Frame!.Value).Distinct().Count();
                if (project.Validation.AttachmentMotion && missing > 0)
                    issues.Add(new("warning", "attachment-track-incomplete", $"Attachment track '{group.Key}' is missing {missing} frame(s)."));
                if (project.Validation.AttachmentMotion && maxStep > project.Validation.MaxAttachmentStepPixels)
                    issues.Add(new("warning", "attachment-step", $"Attachment track '{group.Key}' moves {maxStep:0.##} pixels in one sampled step; configured limit is {project.Validation.MaxAttachmentStepPixels:0.##}."));
                return new AttachmentTrackReport(group.Key, points.Select(point => new AttachmentSample(point.Frame!.Value, point.X, point.Y)).ToArray(), missing, maxStep, loopStep);
            }).ToArray();

        return new(transitions, clips, tracks, duplicates);

        static double Distance(AttachmentPoint a, AttachmentPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }

    private static FrameConsistencyReport AnalyzeFrameConsistency(PixelProject project, IReadOnlyList<FrameReport> frames, List<ValidationIssue> issues)
    {
        var baseline = frames[0];
        var maxOccupancyDrift = 0d;
        var maxBoundsDrift = 0;
        var componentCounts = frames.Select(frame => frame.SilhouetteComponents).Distinct().Count();
        foreach (var frame in frames.Skip(1))
        {
            maxOccupancyDrift = Math.Max(maxOccupancyDrift,
                Math.Abs(frame.OccupiedPixels - baseline.OccupiedPixels) * 100d / Math.Max(1, baseline.OccupiedPixels));
            if (baseline.Bounds is not null && frame.Bounds is not null)
            {
                maxBoundsDrift = Math.Max(maxBoundsDrift, new[]
                {
                    Math.Abs(frame.Bounds.X - baseline.Bounds.X), Math.Abs(frame.Bounds.Y - baseline.Bounds.Y),
                    Math.Abs(frame.Bounds.X + frame.Bounds.Width - baseline.Bounds.X - baseline.Bounds.Width),
                    Math.Abs(frame.Bounds.Y + frame.Bounds.Height - baseline.Bounds.Y - baseline.Bounds.Height)
                }.Max());
            }
        }

        var invariantLayers = new List<InvariantLayerReport>();
        foreach (var layer in project.Layers.Where(layer => layer.FrameInvariant))
        {
            var changed = 0;
            for (var frame = 1; frame < project.FrameCount; frame++)
                for (var pixel = 0; pixel < layer.Frames[0].Length; pixel++)
                    if (layer.Frames[0][pixel] != layer.Frames[frame][pixel]) changed++;
            invariantLayers.Add(new(layer.Name, changed));
            if (changed > 0) issues.Add(new("error", "frame-invariant-layer-changed",
                $"Static layer '{layer.Name}' differs by {changed} pixel instances across animation frames."));
        }

        if (project.Validation.FrameConsistency)
        {
            if (maxOccupancyDrift > project.Validation.MaxOccupancyDriftPercent)
                issues.Add(new("warning", "occupancy-drift", $"Animation occupancy drifts {maxOccupancyDrift:0.#}% from frame 1; configured limit is {project.Validation.MaxOccupancyDriftPercent:0.#}%."));
            if (maxBoundsDrift > project.Validation.MaxBoundsDriftPixels)
                issues.Add(new("warning", "bounds-drift", $"Animation bounds drift {maxBoundsDrift} pixels from frame 1; configured limit is {project.Validation.MaxBoundsDriftPixels}."));
            if (componentCounts > 1)
                issues.Add(new("info", "component-count-drift", "Silhouette component count changes between frames; verify that parts do not detach unintentionally."));
        }

        return new(maxOccupancyDrift, maxBoundsDrift, componentCounts, invariantLayers);
    }

    private static int CountComponents(byte[] rgba, int width, int height)
    {
        var visited = new bool[width * height]; var components = 0; var queue = new Queue<int>();
        for (var start = 0; start < visited.Length; start++)
        {
            if (visited[start] || rgba[start * 4 + 3] == 0) continue;
            components++; visited[start] = true; queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var index = queue.Dequeue(); var x = index % width; var y = index / width;
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
            }
        }
        return components;

        void Visit(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            var index = y * width + x;
            if (visited[index] || rgba[index * 4 + 3] == 0) return;
            visited[index] = true; queue.Enqueue(index);
        }
    }

    private static int CountSeamMismatch(byte[] rgba, int width, int height, bool horizontal)
    {
        var mismatches = 0; var length = horizontal ? height : width;
        for (var i = 0; i < length; i++)
        {
            var a = horizontal ? i * width : i;
            var b = horizontal ? i * width + width - 1 : (height - 1) * width + i;
            if (!PixelEquals(rgba, a, rgba, b)) mismatches++;
        }
        return mismatches;
    }

    private static int PixelDifference(byte[] left, byte[] right)
    {
        var changed = 0;
        for (var i = 0; i < left.Length / 4; i++) if (!PixelEquals(left, i, right, i)) changed++;
        return changed;
    }

    private static bool PixelEquals(byte[] left, int leftIndex, byte[] right, int rightIndex)
    {
        var a = leftIndex * 4; var b = rightIndex * 4;
        return left[a] == right[b] && left[a + 1] == right[b + 1] && left[a + 2] == right[b + 2] && left[a + 3] == right[b + 3];
    }
}

public sealed record ProjectReport(string Name, int Width, int Height, int FrameCount, bool PaletteLocked,
    IReadOnlyList<PaletteUse> Palette, IReadOnlyList<FrameReport> Frames, int TileXMismatches, int TileYMismatches,
    int LoopEndpointDeltaPixels, FrameConsistencyReport FrameConsistency, AnimationReport Animation,
    IReadOnlyList<AttachmentPoint> AttachmentPoints, IReadOnlyList<ValidationIssue> Issues);
public sealed record PaletteUse(int Index, string Color, long Uses);
public sealed record FrameReport(int Frame, int DurationMs, int OccupiedPixels, int TransparentPixels, PixelBounds? Bounds, int SilhouetteComponents);
public sealed record PixelBounds(int X, int Y, int Width, int Height);
public sealed record ValidationIssue(string Severity, string Code, string Message);
public sealed record FrameConsistencyReport(double MaxOccupancyDriftPercent, int MaxBoundsDriftPixels,
    int DistinctComponentCounts, IReadOnlyList<InvariantLayerReport> InvariantLayers);
public sealed record InvariantLayerReport(string Layer, int ChangedPixelInstances);
public sealed record AnimationReport(IReadOnlyList<FrameTransitionReport> Transitions, IReadOnlyList<AnimationClipReport> Clips,
    IReadOnlyList<AttachmentTrackReport> AttachmentTracks, int AdjacentDuplicateFrames);
public sealed record FrameTransitionReport(int From, int To, int ChangedPixels);
public sealed record AnimationClipReport(string Name, int From, int To, string Direction, bool Loop,
    IReadOnlyList<int> PlaybackFrames, int TotalDurationMs, int LoopTransitionPixels);
public sealed record AttachmentTrackReport(string Name, IReadOnlyList<AttachmentSample> Samples, int MissingFrames,
    double MaxStepPixels, double LoopStepPixels);
public sealed record AttachmentSample(int Frame, int X, int Y);
