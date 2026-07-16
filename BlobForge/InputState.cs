using System.Diagnostics;
using System.Numerics;

namespace BlobForge;

public sealed class InputState
{
    private readonly Queue<(Vector2 Position, long Timestamp)> _mouseHistory = new();

    public Vector2 MousePosition { get; private set; }
    public bool LeftDown { get; private set; }
    public bool RightDown { get; private set; }

    public void SetMouse(Vector2 position)
    {
        MousePosition = position;
        var now = Stopwatch.GetTimestamp();
        _mouseHistory.Enqueue((position, now));
        var cutoff = now - (long)(Stopwatch.Frequency * 0.09);
        while (_mouseHistory.Count > 2 && _mouseHistory.Peek().Timestamp < cutoff) _mouseHistory.Dequeue();
    }

    public void SetLeft(bool down) => LeftDown = down;
    public void SetRight(bool down) => RightDown = down;

    public Vector2 GetMouseVelocity()
    {
        if (_mouseHistory.Count < 2) return Vector2.Zero;
        var first = _mouseHistory.Peek();
        var last = _mouseHistory.Last();
        var seconds = (last.Timestamp - first.Timestamp) / (double)Stopwatch.Frequency;
        if (seconds <= 0.0001) return Vector2.Zero;
        var velocity = (last.Position - first.Position) / (float)seconds;
        var speed = velocity.Length();
        return speed > 2400f ? velocity * (2400f / speed) : velocity;
    }
}
