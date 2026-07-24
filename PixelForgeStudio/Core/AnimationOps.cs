namespace PixelForgeStudio.Core;

public static class AnimationOps
{
    public static int AddFrame(PixelProject project, int index, int copyFrom = -1, int durationMs = 100)
    {
        if (project.FrameCount >= 256) throw new InvalidOperationException("Frame limit reached.");
        var oldCount = project.FrameCount;
        index = Math.Clamp(index, 0, oldCount);
        project.FrameDurationsMs.Insert(index, Math.Clamp(durationMs, 16, 60000));
        foreach (var layer in project.Layers)
            layer.Frames.Insert(index, copyFrom >= 0 && copyFrom < oldCount
                ? (int[])layer.Frames[copyFrom].Clone()
                : Enumerable.Repeat(-1, project.Width * project.Height).ToArray());
        foreach (var point in project.AttachmentPoints.Where(point => point.Frame >= index)) point.Frame++;
        foreach (var tag in project.Tags)
        {
            if (index < tag.From) { tag.From++; tag.To++; }
            else if (index <= tag.To) tag.To++;
        }
        return index;
    }

    public static void DeleteFrame(PixelProject project, int frame)
    {
        if (project.FrameCount == 1) throw new InvalidOperationException("Cannot delete the final frame.");
        if (frame < 0 || frame >= project.FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        project.FrameDurationsMs.RemoveAt(frame);
        foreach (var layer in project.Layers) layer.Frames.RemoveAt(frame);
        project.AttachmentPoints.RemoveAll(point => point.Frame == frame);
        foreach (var point in project.AttachmentPoints.Where(point => point.Frame > frame)) point.Frame--;
        foreach (var tag in project.Tags.ToArray())
        {
            if (frame < tag.From) { tag.From--; tag.To--; }
            else if (frame <= tag.To) tag.To--;
            if (tag.To < tag.From) project.Tags.Remove(tag);
        }
    }

    public static AnimationTag SetTag(PixelProject project, string name, int from, int to, string direction, bool loop)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Animation name is required.");
        if (from < 0 || to < from || to >= project.FrameCount) throw new InvalidDataException("Animation range is outside the project frames.");
        direction = direction.ToLowerInvariant();
        if (direction is not ("forward" or "reverse" or "pingpong")) throw new InvalidDataException("Direction must be forward, reverse, or pingpong.");
        project.Tags.RemoveAll(tag => tag.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var tag = new AnimationTag { Name = name, From = from, To = to, Direction = direction, Loop = loop };
        project.Tags.Add(tag);
        return tag;
    }

    public static void SetDurations(PixelProject project, int from, IReadOnlyList<int> durationsMs)
    {
        if (durationsMs.Count == 0 || from < 0 || from + durationsMs.Count > project.FrameCount)
            throw new InvalidDataException("Duration range is outside the project frames.");
        for (var i = 0; i < durationsMs.Count; i++) project.FrameDurationsMs[from + i] = Math.Clamp(durationsMs[i], 16, 60000);
    }
}
