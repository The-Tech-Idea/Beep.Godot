using Godot;
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Beep.ECS;

public partial class GridCellArchiveComponent
{
    [Signal] public delegate void ChunkSaveFinishedEventHandler(long requestId, bool success, string error);
    public bool IsSaving => _writeTask is not null;
    public long PendingSaveId { get; private set; }
    private Task<WriteResult>? _writeTask;
    private GridCellDataComponent.ChunkSnapshotCapture? _writeCapture;
    private GridArchiveIoBudget.Lease? _writeIoLease;
    private CancellationTokenSource? _writeCancellation;
    private GridCellDataComponent? _writeCells;
    private Vector2I _writeCoordinate;
    private long _writeRevision;
    private string _writeDirectory = "", _writeDestination = "", _writeDefault = "";
    private bool _writeCancelled, _notifyWrite;
    private sealed record WriteResult(string? Temporary, byte[]? Hash, string Error);

    /// <summary>
    /// Copies the chunk's request-time records on the main thread - that copy is the main thread's
    /// whole share - then a worker encodes, hashes and writes them; completion publishes the file
    /// and remembers the hash that eviction later verifies against.
    /// </summary>
    public long RequestSaveChunk(Vector2I coordinate)
    {
        if (IsBusy) { LastError = "archive_busy"; return 0; }
        if (!IsInsideTree() || Engine.IsEditorHint()) { LastError = "archive_not_running"; return 0; }
        if (!TryReserveIo(out var reservation)) return 0;
        LastError = "";
        try
        {
            var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                ?? throw new InvalidOperationException("CellDataPath must resolve to GridCellDataComponent.");
            string destination = GetChunkPath(coordinate);
            _writeCapture = cells.BeginChunkSnapshot(coordinate);
            _writeCells = cells;
            _writeCoordinate = coordinate;
            _writeRevision = cells.GetChunkRevision(coordinate);
            _writeDirectory = ArchiveDirectory;
            _writeDestination = destination;
            _writeDefault = cells.DefaultTerrainKind;
            _writeCancelled = false;
            _notifyWrite = true;
            _writeCancellation = new();
            PendingSaveId = ++_nextRequestId;
            _writeIoLease = reservation;
            reservation = null;
            // The worker receives the private copies, a path, a limit and a token - never a node,
            // the live store, or this component. It owns the capture until its task completes.
            var capture = _writeCapture;
            var cancellation = _writeCancellation.Token;
            int limit = ByteLimit;
            _writeTask = Task.Run(() => EncodeAndPrepare(capture, destination, limit, cancellation));
            SetProcess(true);
            return PendingSaveId;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or FormatException or System.Security.SecurityException)
        {
            _writeCancellation?.Dispose();
            _writeCancellation = null;
            _writeCapture?.Dispose();
            _writeCapture = null;
            _writeIoLease?.Dispose();
            _writeIoLease = null;
            _writeCells = null;
            PendingSaveId = 0;
            LastError = exception.Message;
            return 0;
        }
        finally { reservation?.Dispose(); }
    }

    private static WriteResult EncodeAndPrepare(GridCellDataComponent.ChunkSnapshotCapture capture, string destination,
        int byteLimit, CancellationToken cancellation)
    {
        try
        {
            byte[] cells = capture.Encode(cancellation);
            var (buffer, length) = EncodeChunkDocument(capture.Coordinate, cells, byteLimit);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                var payload = buffer.AsSpan(0, length);
                return new(PrepareWrite(destination, payload, cancellation), SHA256.HashData(payload), "");
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
        catch (OperationCanceledException) { return new(null, null, "cancelled"); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or FormatException or System.Security.SecurityException)
        { return new(null, null, exception.Message); }
    }

    private string SaveTargetError() => _writeCancelled ? "cancelled"
        : !GodotObject.IsInstanceValid(_writeCells) || _writeCells!.IsQueuedForDeletion() || !_writeCells.IsInsideTree()
            || GetNodeOrNull<GridCellDataComponent>(CellDataPath) != _writeCells || ArchiveDirectory != _writeDirectory
            || _writeCells.DefaultTerrainKind != _writeDefault ? "archive_target_changed" : "";

    public void CancelSave()
    {
        if (!IsSaving) return;
        _writeCancelled = true;
        _writeCancellation?.Cancel();
    }

    public void ProcessPendingSave()
    {
        if (_writeTask is not { IsCompleted: true } task) return;
        var reservation = _writeIoLease;
        _writeIoLease = null;
        long id = PendingSaveId;
        bool notify = _notifyWrite;
        var cells = _writeCells;
        var coordinate = _writeCoordinate;
        long revision = _writeRevision;
        string destination = _writeDestination;
        string error = SaveTargetError();
        _writeTask = null;
        _writeCells = null;
        // The task has completed, so the worker is done with the copies.
        _writeCapture?.Dispose();
        _writeCapture = null;
        _writeCancellation?.Dispose();
        _writeCancellation = null;
        PendingSaveId = 0;
        SetProcess(NeedsProcessing);
        string? temporary = null;
        bool success = false;
        try
        {
            var result = task.GetAwaiter().GetResult();
            temporary = result.Temporary;
            if (error.Length == 0) error = result.Error;
            if (error.Length == 0)
            {
                File.Move(temporary!, destination, true);
                temporary = null;
                RememberSavedChunk(coordinate, cells!, revision, destination, result.Hash!);
                success = true;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or System.Security.SecurityException)
        { error = exception.Message; }
        finally { DeleteTemporary(temporary); reservation?.Dispose(); }
        LastError = error;
        FinishBudgetSave(id, success);
        if (notify && IsInsideTree() && !IsQueuedForDeletion()) EmitSignal(SignalName.ChunkSaveFinished, id, success, error);
    }

    private void CancelSaveOnExit()
    {
        CancelSave();
        _writeCells = null;
        _notifyWrite = false;
        _writeCancellation?.Dispose();
        _writeCancellation = null;
        if (_writeTask is not null) _writeTask = DiscardDetachedWrite(_writeTask, _writeIoLease, _writeCapture);
        _writeIoLease = null;
        _writeCapture = null;
    }

    private static async Task<WriteResult> DiscardDetachedWrite(Task<WriteResult> task, GridArchiveIoBudget.Lease? reservation,
        GridCellDataComponent.ChunkSnapshotCapture? capture)
    {
        // A permanently freed node cannot pump completion; cleanup must not depend on reattachment.
        try
        {
            var result = await task.ConfigureAwait(false);
            DeleteTemporary(result.Temporary);
            return new(null, null, "cancelled");
        }
        finally
        {
            reservation?.Dispose();
            capture?.Dispose();
        }
    }
}
