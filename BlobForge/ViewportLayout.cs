using System.Numerics;

namespace BlobForge;

public static class ViewportLayout
{
    public static Rectangle Fit(Size available, Size logical)
    {
        if (available.Width <= 0 || available.Height <= 0 || logical.Width <= 0 || logical.Height <= 0)
            return Rectangle.Empty;
        var scale = MathF.Min(
            available.Width / (float)logical.Width,
            available.Height / (float)logical.Height);
        var width = Math.Max(1, (int)MathF.Floor(logical.Width * scale));
        var height = Math.Max(1, (int)MathF.Floor(logical.Height * scale));
        return new Rectangle(
            (available.Width - width) / 2,
            (available.Height - height) / 2,
            width,
            height);
    }

    public static Vector2 ToWorld(Point point, Rectangle viewport, Size logical, bool clamp)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return Vector2.Zero;
        var localX = point.X - viewport.Left;
        var localY = point.Y - viewport.Top;
        if (clamp)
        {
            localX = Math.Clamp(localX, 0, viewport.Width);
            localY = Math.Clamp(localY, 0, viewport.Height);
        }
        return new Vector2(
            localX * logical.Width / (float)viewport.Width,
            localY * logical.Height / (float)viewport.Height);
    }
}
