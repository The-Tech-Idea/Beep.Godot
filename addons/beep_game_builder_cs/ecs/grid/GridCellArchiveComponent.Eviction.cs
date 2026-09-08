using Godot;
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace Beep.ECS;

public partial class GridCellArchiveComponent
{
    /// <summary>
    /// Synchronously releases an unpinned, non-ticking cell chunk whose current content this
    /// archive has already written. "Current" is the chunk's revision still being the one the save
    /// captured; "written" is the file on disk hashing to the bytes that save produced. This used to
    /// re-encode the whole chunk and compare bytes - a second full Variant encode on the main
    /// thread per eviction, 40-60 ms on a 1,024-cell chunk and the largest spike in retirement.
    /// The hash check relies on the cell service marking its chunk revision on every mutation,
    /// which is what makes revision equality mean content equality.
    /// </summary>
    public bool EvictSavedChunk(Vector2I coordinate)
    {
        LastError = "";
        if (IsBusy) { LastError = "archive_busy"; return false; }
        if (!_savedChunks.TryGetValue(coordinate, out var saved) || !IsChunkSaveCurrent(coordinate))
        {
            LastError = "chunk_not_saved_current";
            return false;
        }
        if (!TryReserveIo(out var lease)) return false;
        using var reservation = lease;
        try
        {
            var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath)!;
            var (buffer, length) = ReadBytes(GetChunkPath(coordinate), ByteLimit, default);
            bool matches;
            try { matches = SHA256.HashData(buffer.AsSpan(0, length)).AsSpan().SequenceEqual(saved.Hash); }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
            if (!matches)
            {
                LastError = "archive_content_changed";
                return false;
            }
            if (!cells.TryEvictChunk(coordinate, saved.Revision))
            {
                LastError = "chunk_required_or_changed";
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or FormatException or System.Security.SecurityException)
        {
            LastError = exception.Message;
            return false;
        }
    }
}
