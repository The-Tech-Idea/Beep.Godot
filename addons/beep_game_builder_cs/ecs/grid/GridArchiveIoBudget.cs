using Godot;
using System;
using System.Threading;

namespace Beep.ECS;

/// <summary>Process-wide admission for archive workers and their unpublished payloads.</summary>
internal sealed class GridArchiveIoBudget
{
    private static readonly Lazy<GridArchiveIoBudget> Instance = new(() => new());
    internal static GridArchiveIoBudget Shared => Instance.Value;
    private readonly object _gate = new();
    internal int OperationLimit { get; }
    internal long ByteLimit { get; }
    private int _active;
    private long _reserved;
    internal int Active { get { lock (_gate) return _active; } }
    internal long Reserved { get { lock (_gate) return _reserved; } }

    private GridArchiveIoBudget()
    {
        OperationLimit = (int)Math.Clamp(ProjectSettings.GetSetting("beep/streaming/archive_max_operations", 2).AsInt64(), 1, 64);
        ByteLimit = Math.Clamp(ProjectSettings.GetSetting("beep/streaming/archive_max_inflight_bytes", 32L * 1024 * 1024).AsInt64(), 1024, 1024L * 1024 * 1024);
    }

    internal bool TryReserve(int bytes, out Lease? lease, out string error)
    {
        lease = null;
        lock (_gate)
        {
            if (bytes > ByteLimit) { error = "archive_io_request_too_large"; return false; }
            if (_active >= OperationLimit || bytes > ByteLimit - _reserved)
            { error = "archive_io_busy"; return false; }
            _active++;
            _reserved += bytes;
            lease = new(this, bytes);
        }
        error = "";
        return true;
    }

    internal sealed class Lease : IDisposable
    {
        private GridArchiveIoBudget? _owner;
        private readonly int _bytes;
        internal Lease(GridArchiveIoBudget owner, int bytes) { _owner = owner; _bytes = bytes; }
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null) return;
            lock (owner._gate) { owner._active--; owner._reserved -= _bytes; }
        }
    }
}
