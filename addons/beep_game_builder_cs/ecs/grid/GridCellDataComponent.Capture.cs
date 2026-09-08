using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Beep.ECS;

public partial class GridCellDataComponent
{
    internal ChunkSnapshotCapture BeginChunkSnapshot(Vector2I coordinate) => new(this, coordinate);

    /// <summary>
    /// One record encoder for the live store and for retained copies: each record becomes the
    /// portable Dictionary <see cref="CellRecord.ToDictionary"/> defines, and the array of them is
    /// one VarToBytes payload. Every wrapper is disposed as it goes; see CellRecord.Set for why.
    /// </summary>
    private static byte[] EncodeCells(IEnumerable<KeyValuePair<Vector2I, CellRecord>> records, CancellationToken cancellation)
    {
        // Typed, exactly as the packet has always been written: VarToBytes encodes a typed array's
        // element type, so a plain Array here would change every archive's bytes.
        var encoded = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        using var owned = (Godot.Collections.Array)encoded;
        foreach (var (cell, record) in records)
        {
            cancellation.ThrowIfCancellationRequested();
            using var snapshot = record.ToDictionary(cell, validatePortableMetadata: true);
            encoded.Add(snapshot);
        }
        return GD.VarToBytes(encoded);
    }

    // Retains request-time copies of one chunk's records so the encode can run on a worker while
    // the live store keeps changing. The copies are private to the capture - metadata is a deep
    // duplicate, nothing references the live store, a node, or another thread's Variant - which is
    // the single-owner use Godot's thread-safety rules allow for Variant containers. The encode
    // used to advance on the main thread at 32 cells a frame; measured on a 1024x1024 world that
    // was ~75 ms of main-thread work per chunk and the reason retirement managed 38 chunks in
    // 15 seconds. Disposal stays with whichever thread finishes with the records.
    internal sealed class ChunkSnapshotCapture : IDisposable
    {
        private readonly Vector2I _coordinate;
        private readonly List<KeyValuePair<Vector2I, CellRecord>> _records = new();
        private bool _disposed;
        internal Vector2I Coordinate => _coordinate;
        internal int CellCount => _records.Count;

        internal ChunkSnapshotCapture(GridCellDataComponent source, Vector2I coordinate)
        {
            _coordinate = coordinate;
            if (!source.IsChunkAvailable(coordinate)) throw new InvalidOperationException("Cannot snapshot an unavailable cell chunk.");
            try
            {
                foreach (var (cell, record) in source._cells.InChunk(coordinate))
                    _records.Add(new(cell, record.CopyForSnapshot()));
            }
            catch { Dispose(); throw; }
        }

        /// <summary>The retained records as the VarToBytes payload a snapshot entry carries. Safe to call from a worker.</summary>
        internal byte[] Encode(CancellationToken cancellation)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return EncodeCells(_records, cancellation);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var (_, record) in _records) record.DisposeSnapshotMetadata();
            _records.Clear();
        }
    }
}
