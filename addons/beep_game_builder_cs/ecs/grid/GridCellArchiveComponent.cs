using Godot;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Beep.ECS;

/// <summary>Per-chunk cell snapshots on disk. Does not own a world or suspend its simulation.</summary>
[GlobalClass]
public partial class GridCellArchiveComponent : Node
{
    [Export] public NodePath CellDataPath { get; set; } = new("");
    [Export] public string ArchiveDirectory { get; set; } = "";
    [Export] public int MaximumChunkBytes { get; set; } = 8 * 1024 * 1024;
    public string LastError { get; private set; } = "";
    public bool IsBusy => IsLoading || IsSaving;
    private long _nextRequestId;

    public override void _Ready() => SetProcess(NeedsProcessing);
    public override void _Process(double delta)
    {
        ProcessChunkDemand();
        ProcessPendingLoad();
        ProcessPendingSave();
        ProcessChunkBudget();
    }

    public override void _ExitTree()
    {
        CancelLoadOnExit();
        DisconnectReadSources();
        _readCells = null;
        _notifyRead = false;
        CancelSaveOnExit();
        _demandRetryAt.Clear();
        _nextDemandScan = 0;
        RequestReady();
        // Keep cancelled tasks until drained; reattachment cannot start overlapping I/O.
    }

    public string GetChunkPath(Vector2I coordinate)
    {
        if (string.IsNullOrWhiteSpace(ArchiveDirectory)) throw new InvalidOperationException("Assign a world-specific archive directory.");
        string directory = Path.GetFullPath(ProjectSettings.GlobalizePath(ArchiveDirectory));
        return Path.Combine(directory, FormattableString.Invariant($"{coordinate.X}_{coordinate.Y}.chunk"));
    }

    public bool SaveChunk(Vector2I coordinate)
    {
        if (IsBusy) { LastError = "archive_busy"; return false; }
        if (!TryReserveIo(out var lease)) return false;
        using var reservation = lease;
        string? temporary = null;
        LastError = "";
        try
        {
            var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                ?? throw new InvalidOperationException("CellDataPath must resolve to GridCellDataComponent.");
            long revision = cells.GetChunkRevision(coordinate);
            var (buffer, length) = EncodeChunkDocument(coordinate, cells.EncodeChunkCells(coordinate), ByteLimit);
            try
            {
                var payload = buffer.AsSpan(0, length);
                string destination = GetChunkPath(coordinate);
                temporary = PrepareWrite(destination, payload, default);
                // Same-directory replacement publishes only a complete file; previous data survives failed writes.
                File.Move(temporary, destination, true);
                temporary = null;
                RememberSavedChunk(coordinate, cells, revision, destination, SHA256.HashData(payload));
                return true;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or FormatException or System.Security.SecurityException)
        {
            LastError = exception.Message;
            return false;
        }
        finally
        {
            DeleteTemporary(temporary);
        }
    }

    public bool LoadChunk(Vector2I coordinate)
    {
        if (IsBusy) { LastError = "archive_busy"; return false; }
        if (!TryReserveIo(out var lease)) return false;
        using var reservation = lease;
        LastError = "";
        try
        {
            var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                ?? throw new InvalidOperationException("CellDataPath must resolve to GridCellDataComponent.");
            var (buffer, length) = ReadBytes(GetChunkPath(coordinate), ByteLimit, default);
            try { cells.PublishParsedChunk(coordinate, DecodeChunk(buffer.AsSpan(0, length), coordinate, cells.DefaultTerrainKind)); }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or FormatException or System.Security.SecurityException)
        {
            LastError = exception.Message;
            return false;
        }
    }

    private int ByteLimit => Math.Clamp(MaximumChunkBytes, 1024, 64 * 1024 * 1024);

    /// <summary>
    /// The archive document: the fixed JSON envelope of a single-chunk snapshot around one Base64
    /// payload, written straight into a pooled buffer. The caller returns the buffer to
    /// <see cref="ArrayPool{T}.Shared"/> when done. Static, with the limit passed in: the worker
    /// calls it and must not read this node.
    ///
    /// The generic path - Godot Dictionary, Base64 string, Json.Stringify string, UTF-8 bytes -
    /// allocated about 800 KB of large objects per chunk, and the gen2 collections those forced
    /// over a 200 MB live world were the 100-200 ms main-thread stalls in the retirement harness
    /// (raising the runtime's large-object threshold alone took the worst frame from 214 ms to
    /// 69 ms). Godot's JSON parser reads this document exactly as it read the generic writer's.
    /// </summary>
    private static (byte[] Buffer, int Length) EncodeChunkDocument(Vector2I coordinate, ReadOnlySpan<byte> cells, int byteLimit)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(96 + Base64.GetMaxEncodedToUtf8Length(cells.Length));
        try
        {
            Span<byte> target = buffer;
            int length = 0;
            ReadOnlySpan<byte> head = "{\"version\":1,\"chunk_size\":32,\"chunks\":[{\"x\":"u8;
            head.CopyTo(target); length += head.Length;
            Utf8Formatter.TryFormat(coordinate.X, target[length..], out int written); length += written;
            ReadOnlySpan<byte> y = ",\"y\":"u8;
            y.CopyTo(target[length..]); length += y.Length;
            Utf8Formatter.TryFormat(coordinate.Y, target[length..], out written); length += written;
            ReadOnlySpan<byte> data = ",\"data\":\""u8;
            data.CopyTo(target[length..]); length += data.Length;
            Base64.EncodeToUtf8(cells, target[length..], out _, out int encoded); length += encoded;
            ReadOnlySpan<byte> tail = "\"}]}"u8;
            tail.CopyTo(target[length..]); length += tail.Length;
            if (length > byteLimit) throw new InvalidDataException("Cell chunk exceeds the archive byte limit.");
            return (buffer, length);
        }
        catch
        {
            // Rethrown as-is: the caller's catch decides; this only hands the buffer back.
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    private static string PrepareWrite(string destination, ReadOnlySpan<byte> bytes, System.Threading.CancellationToken cancellation)
    {
        string? temporary = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None))
            {
                for (int offset = 0; offset < bytes.Length;)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int count = Math.Min(65536, bytes.Length - offset);
                    stream.Write(bytes.Slice(offset, count));
                    offset += count;
                }
                stream.Flush(true);
            }
            cancellation.ThrowIfCancellationRequested();
            string prepared = temporary;
            temporary = null;
            return prepared;
        }
        finally { DeleteTemporary(temporary); }
    }

    private static void DeleteTemporary(string? path)
    {
        if (path is null) return;
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The whole file into a pooled buffer; the caller returns the buffer. A chunk file is large enough for the LOH otherwise.</summary>
    private static (byte[] Buffer, int Length) ReadBytes(string path, int limit, System.Threading.CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        if (stream.Length > limit) throw new InvalidDataException("Cell chunk exceeds the archive byte limit.");
        int length = checked((int)stream.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, length));
        try
        {
            for (int offset = 0; offset < length;)
            {
                cancellation.ThrowIfCancellationRequested();
                int count = Math.Min(65536, length - offset);
                stream.ReadExactly(buffer.AsSpan(offset, count));
                offset += count;
            }
            cancellation.ThrowIfCancellationRequested();
            return (buffer, length);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);
            throw;
        }
    }

    /// <summary>
    /// Decodes one archive document into records, without touching a node: the worker runs this,
    /// and the synchronous LoadChunk runs the same method on the main thread.
    /// </summary>
    private static GridCellDataComponent.ParsedChunk DecodeChunk(ReadOnlySpan<byte> document, Vector2I coordinate, string defaultTerrainKind)
    {
        byte[] payload = DecodeChunkDocument(document, coordinate);
        using Variant decoded = GD.BytesToVar(payload);
        if (decoded.VariantType != Variant.Type.Array) throw new FormatException("Invalid encoded cell chunk.");
        using var cells = decoded.AsGodotArray();
        return GridCellDataComponent.ParseChunk(cells, coordinate, defaultTerrainKind);
    }

    /// <summary>
    /// Reads the envelope <see cref="EncodeChunkDocument"/> writes - or any JSON of that shape -
    /// straight from its UTF-8 bytes, and returns the one chunk's cell payload. The generic path
    /// made a 200 KB string of the document and another of the Base64 before decoding either.
    /// </summary>
    private static byte[] DecodeChunkDocument(ReadOnlySpan<byte> document, Vector2I expected)
    {
        try
        {
            var reader = new Utf8JsonReader(document, new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new FormatException("Invalid cell archive JSON.");
            int? version = null, chunkSize = null, x = null, y = null;
            byte[]? data = null;
            int chunks = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) throw new FormatException("Invalid cell archive JSON.");
                if (reader.ValueTextEquals("version"u8)) { reader.Read(); version = ReadInteger(ref reader); }
                else if (reader.ValueTextEquals("chunk_size"u8)) { reader.Read(); chunkSize = ReadInteger(ref reader); }
                else if (reader.ValueTextEquals("chunks"u8))
                {
                    if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray) throw new FormatException("Invalid cell chunk snapshot header.");
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType != JsonTokenType.StartObject) throw new FormatException("Invalid cell chunk entry.");
                        chunks++;
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                        {
                            if (reader.TokenType != JsonTokenType.PropertyName) throw new FormatException("Invalid cell chunk entry.");
                            if (reader.ValueTextEquals("x"u8)) { reader.Read(); x = ReadInteger(ref reader); }
                            else if (reader.ValueTextEquals("y"u8)) { reader.Read(); y = ReadInteger(ref reader); }
                            else if (reader.ValueTextEquals("data"u8))
                            {
                                if (!reader.Read() || reader.TokenType != JsonTokenType.String) throw new FormatException("Incomplete cell chunk entry.");
                                data = reader.GetBytesFromBase64();
                            }
                            else { reader.Read(); reader.Skip(); }
                        }
                    }
                }
                else { reader.Read(); reader.Skip(); }
            }
            if (version != 1 || chunkSize != 32) throw new FormatException("Invalid cell chunk snapshot header.");
            if (chunks != 1) throw new FormatException("Expected exactly one cell chunk.");
            if (x is null || y is null || data is null) throw new FormatException("Incomplete cell chunk entry.");
            if (new Vector2I(x.Value, y.Value) != expected) throw new FormatException("Cell archive coordinate does not match the requested chunk.");
            return data;
        }
        catch (JsonException exception)
        {
            throw new FormatException("Invalid cell archive JSON.", exception);
        }
    }

    private static int ReadInteger(ref Utf8JsonReader reader)
    {
        // JSON numbers may arrive as floats; only exact, in-range integers are headers or coordinates.
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetDouble(out double number) || !double.IsFinite(number)
            || number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue)
            throw new FormatException("Invalid cell chunk coordinate.");
        return (int)number;
    }
}
