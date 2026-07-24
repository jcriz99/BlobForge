using System.Numerics;

namespace BlobForge.World;

public enum FactoryWorkerActivity : byte
{
    Forming,
    Climbing,
    Idle,
    Walking,
    Descending,
    Operating,
    Ascending
}

public sealed class FactoryWorker(int id, Vector2 position)
{
    public int Id { get; } = id;
    public Vector2 Position { get; internal set; } = position;
    public FactoryWorkerActivity Activity { get; internal set; } = FactoryWorkerActivity.Forming;
    public int AssignedBay { get; internal set; } = -1;
    public float Phase { get; internal set; }
    public float Formation { get; internal set; }
    public float OperationTime { get; internal set; }
    public bool FacingRight { get; internal set; }
}
