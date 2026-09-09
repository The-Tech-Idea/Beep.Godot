using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridCellDataComponent
{
    public Godot.Collections.Dictionary CaptureChunkState()
    {
        if (_unavailableChunks.Count > 0)
            throw new InvalidOperationException("A full cell snapshot requires every chunk to be available.");
        var chunks = new Godot.Collections.Array();
        foreach (var coordinate in _cells.ChunkCoordinates) chunks.Add(CaptureChunkEntry(coordinate));
        return new() { ["version"] = 1, ["chunk_size"] = 32, ["chunks"] = chunks };
    }

    public Godot.Collections.Dictionary CaptureSingleChunkState(Vector2I coordinate)
        => new() { ["version"] = 1, ["chunk_size"] = 32,
            ["chunks"] = new Godot.Collections.Array { CaptureChunkEntry(coordinate) } };

    private Godot.Collections.Dictionary CaptureChunkEntry(Vector2I coordinate)
        => new() { ["x"] = coordinate.X, ["y"] = coordinate.Y,
            ["data"] = Convert.ToBase64String(EncodeChunkCells(coordinate)) };

    /// <summary>
    /// The chunk's live records as one VarToBytes payload - the "data" of a snapshot entry before
    /// Base64. The archive writes it into its document directly; game saves wrap it in a Dictionary.
    /// </summary>
    internal byte[] EncodeChunkCells(Vector2I coordinate)
    {
        if (!IsChunkAvailable(coordinate)) throw new InvalidOperationException("Cannot snapshot an unavailable cell chunk.");
        return EncodeCells(_cells.InChunk(coordinate), default);
    }

    public void RestoreChunkState(Godot.Collections.Dictionary state)
    {
        _cells = ParseChunkState(state);
        RebuildDailyIndex();
        _unavailableChunks.Clear();
        _evictedChunks.Clear();
        ResetChunkRevisions();
        TerrainRevision++;
        MarkNavigationChanged();
        EmitCellsChanged(TerrainChangeKind.Terrain | TerrainChangeKind.Navigation, new Godot.Collections.Array<Vector2I>());
    }

    /// <summary>Replace exactly one chunk, leaving all neighboring records intact.</summary>
    public void RestoreSingleChunkState(Vector2I expectedCoordinate, Godot.Collections.Dictionary state)
    {
        if (!state.TryGetValue("chunks", out var encoded) || encoded.VariantType != Variant.Type.Array
            || encoded.AsGodotArray().Count != 1) throw new FormatException("Expected exactly one cell chunk.");
        var parsed = ParseChunkState(state);
        var entries = encoded.AsGodotArray();
        var entry = entries[0].AsGodotDictionary();
        if (new Vector2I(ReadChunkInteger(entry["x"]), ReadChunkInteger(entry["y"])) != expectedCoordinate)
            throw new FormatException("Cell archive coordinate does not match the requested chunk.");
        PublishRecords(expectedCoordinate, parsed);
    }

    /// <summary>
    /// Records parsed for one chunk - on a worker, usually - awaiting publication on the main
    /// thread. Opaque to the archive: the record type stays private to the cell service.
    /// </summary>
    internal sealed class ParsedChunk
    {
        private readonly ChunkedCellStore<CellRecord> _records = new();

        internal ParsedChunk(Godot.Collections.Array cells, Vector2I coordinate, string defaultTerrainKind)
            => ParseCellArray(cells, coordinate, defaultTerrainKind, _records);

        internal void PublishTo(GridCellDataComponent target, Vector2I coordinate) => target.PublishRecords(coordinate, _records);
    }

    /// <summary>
    /// Parses one chunk's decoded cell array. Static and free of node state, so the archive runs
    /// it on its worker: the records it builds reference nothing but themselves until
    /// <see cref="PublishParsedChunk"/> hands them to the live store on the main thread.
    /// </summary>
    internal static ParsedChunk ParseChunk(Godot.Collections.Array cells, Vector2I coordinate, string defaultTerrainKind)
        => new(cells, coordinate, defaultTerrainKind);

    /// <summary>The main-thread tail of a chunk restore: replaces the chunk and publishes the change.</summary>
    internal void PublishParsedChunk(Vector2I coordinate, ParsedChunk parsed) => parsed.PublishTo(this, coordinate);

    private void PublishRecords(Vector2I coordinate, ChunkedCellStore<CellRecord> records)
    {
        _cells.ReplaceChunk(coordinate, records);
        RefreshDailyChunk(coordinate);
        _unavailableChunks.Remove(coordinate);
        _evictedChunks.Remove(coordinate);
        MarkChunkChanged(coordinate);
        TerrainRevision++;
        MarkNavigationChanged();
        // A chunk reloaded from the archive: its content reappears, so listeners redraw it.
        EmitCellsChanged(TerrainChangeKind.Terrain | TerrainChangeKind.Navigation, new Godot.Collections.Array<Vector2I> { coordinate });
    }

    private ChunkedCellStore<CellRecord> ParseChunkState(Godot.Collections.Dictionary state)
    {
        if (GridVariantReader.Int(state, "version") != 1 || GridVariantReader.Int(state, "chunk_size") != 32
            || !state.TryGetValue("chunks", out var encoded) || encoded.VariantType != Variant.Type.Array)
            throw new FormatException("Invalid cell chunk snapshot header.");
        var parsed = new ChunkedCellStore<CellRecord>();
        var seen = new HashSet<Vector2I>();
        foreach (var value in encoded.AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) throw new FormatException("Invalid cell chunk entry.");
            using var entry = value.AsGodotDictionary();
            if (!entry.ContainsKey("x") || !entry.ContainsKey("y") || !entry.ContainsKey("data")
                || entry["data"].VariantType != Variant.Type.String)
                throw new FormatException("Incomplete cell chunk entry.");
            var coordinate = new Vector2I(ReadChunkInteger(entry["x"]), ReadChunkInteger(entry["y"]));
            if (!seen.Add(coordinate)) throw new FormatException("Duplicate cell chunk.");
            using Variant decoded = GD.BytesToVar(Convert.FromBase64String(entry["data"].AsString()));
            if (decoded.VariantType != Variant.Type.Array) throw new FormatException("Invalid encoded cell chunk.");
            using var cells = decoded.AsGodotArray();
            ParseCellArray(cells, coordinate, DefaultTerrainKind, parsed);
        }
        return parsed;
    }

    /// <summary>
    /// One chunk's records from its decoded cell array, validated: at most 32x32 records, each with
    /// an exact coordinate inside the chunk, none duplicated, and only portable metadata - checked
    /// value by value inside FromDictionary rather than by a second walk over every record. Every
    /// wrapper and Variant it touches is disposed as it goes; see CellRecord.Set for why that matters.
    /// </summary>
    private static void ParseCellArray(Godot.Collections.Array cells, Vector2I coordinate, string defaultTerrainKind,
        ChunkedCellStore<CellRecord> into)
    {
        if (cells.Count > 1024) throw new FormatException("Cell chunk exceeds 32x32 bounds.");
        foreach (var cellValue in cells)
        {
            using var owned = cellValue;
            if (owned.VariantType != Variant.Type.Dictionary) throw new FormatException("Invalid cell record.");
            using var cell = owned.AsGodotDictionary();
            using var key = (Variant)"cell";
            if (!cell.TryGetValue(key, out var position) || position.VariantType != Variant.Type.Vector2I)
                throw new FormatException("Cell record lacks an exact integer coordinate.");
            var at = position.AsVector2I();
            if (ChunkedCellStore<CellRecord>.ChunkFor(at) != coordinate || into.ContainsKey(at))
                throw new FormatException("Cell record is duplicated or outside its chunk.");
            into[at] = CellRecord.FromDictionary(cell, defaultTerrainKind);
        }
    }

    private static int ReadChunkInteger(Variant value)
    {
        // JSON numbers arrive as floats; only exact, in-range integers are coordinates.
        double number = value.VariantType switch
        {
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            _ => double.NaN
        };
        if (!double.IsFinite(number) || number != Math.Truncate(number)
            || number < int.MinValue || number > int.MaxValue)
            throw new FormatException("Invalid cell chunk coordinate.");
        return (int)number;
    }

    private static void ValidatePortable(Variant value, int depth)
    {
        if (depth > 64) throw new FormatException("Cell metadata is cyclic or exceeds the nesting limit.");
        if (value.VariantType is Variant.Type.Object or Variant.Type.Callable or Variant.Type.Signal or Variant.Type.Rid)
            throw new FormatException("Cell snapshots cannot contain live objects, callables, signals or RIDs.");
        // The wrappers and the Variants an enumeration yields each own native memory; disposed
        // here rather than left to finalizers, for the same reason as CellRecord.Set.
        if (value.VariantType == Variant.Type.Array)
        {
            using var array = value.AsGodotArray();
            foreach (var child in array)
            {
                using var owned = child;
                ValidatePortable(owned, depth + 1);
            }
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            using var dictionary = value.AsGodotDictionary();
            foreach (var pair in dictionary)
            {
                using var key = pair.Key;
                using var item = pair.Value;
                ValidatePortable(key, depth + 1);
                ValidatePortable(item, depth + 1);
            }
        }
    }
}
