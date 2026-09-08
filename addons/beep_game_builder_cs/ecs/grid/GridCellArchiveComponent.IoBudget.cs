namespace Beep.ECS;

public partial class GridCellArchiveComponent
{
    public int SharedIoOperationLimit => GridArchiveIoBudget.Shared.OperationLimit;
    public long SharedIoByteLimit => GridArchiveIoBudget.Shared.ByteLimit;
    public int SharedIoActiveOperations => GridArchiveIoBudget.Shared.Active;
    public long SharedIoReservedBytes => GridArchiveIoBudget.Shared.Reserved;

    private bool TryReserveIo(out GridArchiveIoBudget.Lease? lease, int buffers = 1)
    {
        bool accepted = GridArchiveIoBudget.Shared.TryReserve(ByteLimit * buffers, out lease, out string error);
        if (!accepted) LastError = error;
        return accepted;
    }
}
