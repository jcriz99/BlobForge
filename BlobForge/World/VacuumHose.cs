using System.Numerics;

namespace BlobForge.World;

public sealed class VacuumHose
{
    private const int NodeCount = 11;
    private const int ConstraintIterations = 5;
    private const float SegmentLength = 10.5f;
    private readonly Vector2[] _positions = new Vector2[NodeCount];
    private readonly Vector2[] _previous = new Vector2[NodeCount];
    private Vector2 _anchor;
    private Vector2 _restPosition;
    private Vector2 _dragTarget;
    private Vector2 _nozzleFacing = Vector2.UnitX;

    public VacuumHose(Vector2 anchor, Vector2 restPosition)
    {
        Reset(anchor, restPosition);
    }

    public IReadOnlyList<Vector2> Nodes => _positions;
    public Vector2 NozzlePosition => _positions[^1];
    public Vector2 NozzleFacing => _nozzleFacing;
    public bool IsDragging { get; private set; }

    public bool HitNozzle(Vector2 point)
        => Vector2.DistanceSquared(point, NozzlePosition) <= 20f * 20f;

    public bool BeginDrag(Vector2 point)
    {
        if (!HitNozzle(point)) return false;
        IsDragging = true;
        _dragTarget = point;
        return true;
    }

    public void DragTo(Vector2 point, float deckY)
    {
        if (!IsDragging) return;
        _dragTarget = new Vector2(
            Math.Clamp(point.X, _anchor.X - 135f, _anchor.X + 70f),
            Math.Clamp(point.Y, deckY - 145f, deckY - 4f));
    }

    public void EndDrag() => IsDragging = false;

    public void SnapNozzleTo(Vector2 position)
    {
        _positions[^1] = position;
        _previous[^1] = position;
        _dragTarget = position;
    }

    public void Step(float dt, Vector2 anchor, Vector2 restPosition, float deckY, Vector2? aimTarget = null)
    {
        _anchor = anchor;
        _restPosition = restPosition;
        _positions[0] = _previous[0] = anchor;
        for (var i = 1; i < NodeCount; i++)
        {
            if (IsDragging && i == NodeCount - 1) continue;
            var position = _positions[i];
            var velocity = (position - _previous[i]) * 0.982f;
            _previous[i] = position;
            _positions[i] = position + velocity + new Vector2(0f, 235f * dt * dt);
        }

        if (!IsDragging)
        {
            var nozzle = _positions[^1];
            var spring = (_restPosition - nozzle) * MathF.Min(1f, dt * 7.5f);
            _positions[^1] += spring;
        }

        for (var iteration = 0; iteration < ConstraintIterations; iteration++)
        {
            _positions[0] = anchor;
            if (IsDragging) _positions[^1] = _dragTarget;
            for (var i = 0; i < NodeCount - 1; i++)
            {
                var delta = _positions[i + 1] - _positions[i];
                var distance = MathF.Max(0.001f, delta.Length());
                var correction = delta * ((distance - SegmentLength) / distance);
                if (i == 0)
                    _positions[i + 1] -= correction;
                else if (IsDragging && i + 1 == NodeCount - 1)
                    _positions[i] += correction;
                else
                {
                    _positions[i] += correction * 0.5f;
                    _positions[i + 1] -= correction * 0.5f;
                }
            }
            for (var i = 1; i < NodeCount; i++)
                _positions[i].Y = MathF.Min(_positions[i].Y, deckY - 3f);
        }
        _positions[0] = anchor;
        if (IsDragging) _positions[^1] = _dragTarget;

        var fallback = _positions[^1] - _positions[^2];
        var targetDirection = aimTarget.HasValue
            ? aimTarget.Value - _positions[^1]
            : IsDragging
                ? fallback
                // In the rack the suction mouth points upward, opposite the
                // previous down/sideways rest pose. This keeps the nozzle
                // visibly vertical while the hose hangs from its lower end.
                : -Vector2.UnitY;
        if (targetDirection.LengthSquared() > 0.0001f)
        {
            targetDirection = Vector2.Normalize(targetDirection);
            var currentAngle = MathF.Atan2(_nozzleFacing.Y, _nozzleFacing.X);
            var targetAngle = MathF.Atan2(targetDirection.Y, targetDirection.X);
            var delta = MathF.Atan2(
                MathF.Sin(targetAngle - currentAngle),
                MathF.Cos(targetAngle - currentAngle));
            var turn = Math.Clamp(dt * (aimTarget.HasValue ? 18f : 6f), 0f, 1f);
            currentAngle += delta * turn;
            _nozzleFacing = new Vector2(MathF.Cos(currentAngle), MathF.Sin(currentAngle));
        }
    }

    private void Reset(Vector2 anchor, Vector2 restPosition)
    {
        _anchor = anchor;
        _restPosition = restPosition;
        _dragTarget = restPosition;
        _nozzleFacing = -Vector2.UnitY;
        for (var i = 0; i < NodeCount; i++)
        {
            var t = i / (float)(NodeCount - 1);
            _positions[i] = Vector2.Lerp(anchor, restPosition, t);
            _previous[i] = _positions[i];
        }
    }
}
