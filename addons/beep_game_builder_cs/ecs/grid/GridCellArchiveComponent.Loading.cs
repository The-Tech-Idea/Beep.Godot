using Godot;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Beep.ECS;

public partial class GridCellArchiveComponent
{
    [Signal] public delegate void ChunkLoadFinishedEventHandler(long requestId, bool success, string error);
    public bool IsLoading => _readTask is not null;
    public long PendingLoadId { get; private set; }
    private Task<ReadResult>? _readTask;
    private GridArchiveIoBudget.Lease? _readIoLease;
    private CancellationTokenSource? _readCancellation;
    private GridCellDataComponent? _readCells;
    private Vector2I _readCoordinate;
    private string _readDirectory = "", _readDefault = "";
    private long _readRevision;
    private bool _readChanged, _readCancelled, _notifyRead;
    private sealed record ReadResult(GridCellDataComponent.ParsedChunk? Records, string Error);

    /// <summary>One bounded read at a time. Returns zero when rejected, otherwise a completion ID.</summary>
    public long RequestLoadChunk(Vector2I coordinate)
    {
        if (IsBusy) { LastError = "archive_busy"; return 0; }
        if (!IsInsideTree() || Engine.IsEditorHint()) { LastError = "archive_not_running"; return 0; }
        if (!TryReserveIo(out var reservation)) return 0;
        LastError = "";
        try
        {
            var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                ?? throw new InvalidOperationException("CellDataPath must resolve to GridCellDataComponent.");
            string path = GetChunkPath(coordinate);
            int limit = ByteLimit;
            _readCells = cells;
            _readCoordinate = coordinate;
            _readDirectory = ArchiveDirectory;
            _readDefault = cells.DefaultTerrainKind;
            _readRevision = cells.GetChunkRevision(coordinate);
            _readChanged = _readCancelled = false;
            _notifyRead = true;
            cells.CellChanged += OnReadCellChanged;
            cells.CellsChanged += OnReadCellsChanged;
            cells.DayAdvanced += OnReadDayAdvanced;
            _readCancellation = new();
            var cancellation = _readCancellation.Token;
            PendingLoadId = ++_nextRequestId;
            // The worker reads and decodes the whole chunk - file, envelope, cell array, records -
            // and hands back records that reference nothing live; publication stays on the main
            // thread. Decoding 1,024 records used to be the main thread's per-reload cost, on top
            // of three large allocations per file.
            string defaultKind = _readDefault;
            _readTask = Task.Run(() => ReadForRequest(path, limit, coordinate, defaultKind, cancellation));
            _readIoLease = reservation;
            reservation = null;
            SetProcess(true);
            return PendingLoadId;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or System.Security.SecurityException)
        {
            DisconnectReadSources();
            _readCancellation?.Dispose();
            _readCancellation = null;
            _readCells = null;
            PendingLoadId = 0;
            LastError = exception.Message;
            return 0;
        }
        finally { reservation?.Dispose(); }
    }

    private static ReadResult ReadForRequest(string path, int limit, Vector2I coordinate, string defaultTerrainKind, CancellationToken cancellation)
    {
        try
        {
            var (buffer, length) = ReadBytes(path, limit, cancellation);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                return new(DecodeChunk(buffer.AsSpan(0, length), coordinate, defaultTerrainKind), "");
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
        catch (OperationCanceledException) { return new(null, "cancelled"); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or FormatException or InvalidOperationException or System.Security.SecurityException)
        { return new(null, exception.Message); }
    }

    public void CancelLoad()
    {
        if (!IsLoading) return;
        _readCancelled = true;
        _readCancellation?.Cancel();
    }

    public void ProcessPendingLoad()
    {
        if (_readTask is not { IsCompleted: true } task) return;
        var reservation = _readIoLease;
        _readIoLease = null;
        long id = PendingLoadId;
        var cells = _readCells;
        var coordinate = _readCoordinate;
        bool notify = _notifyRead;
        string error = _readCancelled || !AutomaticReadStillWanted() ? "cancelled"
            : _readChanged ? "cell_data_changed"
            : !GodotObject.IsInstanceValid(cells) || cells!.IsQueuedForDeletion() || !cells.IsInsideTree()
                || GetNodeOrNull<GridCellDataComponent>(CellDataPath) != cells || ArchiveDirectory != _readDirectory
                || cells.DefaultTerrainKind != _readDefault || cells.GetChunkRevision(coordinate) != _readRevision ? "archive_target_changed" : "";
        DisconnectReadSources();
        _readTask = null;
        _readCells = null;
        _readCancellation?.Dispose();
        _readCancellation = null;
        PendingLoadId = 0;
        SetProcess(NeedsProcessing);
        bool success = false;
        try
        {
            var result = task.GetAwaiter().GetResult();
            if (error.Length == 0) error = result.Error;
            if (error.Length == 0)
            {
                cells!.PublishParsedChunk(coordinate, result.Records!);
                success = true;
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException
            or InvalidOperationException or ArgumentException)
        { error = exception.Message; }
        finally { reservation?.Dispose(); }
        LastError = error;
        FinishAutomaticRead(id, success);
        if (notify && IsInsideTree() && !IsQueuedForDeletion()) EmitSignal(SignalName.ChunkLoadFinished, id, success, error);
    }

    private void OnReadCellChanged(int x, int y, int kind)
    {
        if (new Vector2I(x >> 5, y >> 5) == _readCoordinate) _readChanged = true;
    }
    private void OnReadCellsChanged(int kind, Godot.Collections.Array<Vector2I> chunks) => _readChanged = true;
    private void OnReadDayAdvanced(int days) => _readChanged = true;
    private void DisconnectReadSources()
    {
        if (!GodotObject.IsInstanceValid(_readCells)) return;
        _readCells!.CellChanged -= OnReadCellChanged;
        _readCells.CellsChanged -= OnReadCellsChanged;
        _readCells.DayAdvanced -= OnReadDayAdvanced;
    }

    private void CancelLoadOnExit()
    {
        CancelLoad();
        _readCancellation?.Dispose();
        _readCancellation = null;
        if (_readTask is not null) _readTask = DiscardDetachedRead(_readTask, _readIoLease);
        _readIoLease = null;
    }

    private static async Task<ReadResult> DiscardDetachedRead(Task<ReadResult> task, GridArchiveIoBudget.Lease? reservation)
    {
        try { await task.ConfigureAwait(false); return new(null, "cancelled"); }
        finally { reservation?.Dispose(); }
    }

}
