using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

public partial class GridCellDataComponent
{
    private readonly Dictionary<Vector2I, long> _chunkRevisions = new();
    private long _revisionClock, _replacementRevision;
    internal long ReplacementRevision => _replacementRevision;

    /// <summary>Process-local content token, not a persisted version or navigation revision.</summary>
    public long GetChunkRevision(Vector2I chunk)
        => _chunkRevisions.TryGetValue(chunk, out long revision) ? revision : _replacementRevision;

    private void MarkChunkChanged(Vector2I chunk) => _chunkRevisions[chunk] = ++_revisionClock;
    private void MarkCellChanged(Vector2I cell) => MarkChunkChanged(ChunkedCellStore<CellRecord>.ChunkFor(cell));
    private void ResetChunkRevisions()
    {
        _chunkRevisions.Clear();
        _replacementRevision = ++_revisionClock;
    }

    private void NotifyCellChanged(Vector2I cell)
    {
        MarkCellChanged(cell);
        EmitSignal(SignalName.CellChanged, cell.X, cell.Y);
    }

    private static Variant CopyMetadataValue(Variant value) => value.VariantType switch
    {
        Variant.Type.Dictionary => value.AsGodotDictionary().Duplicate(deep: true),
        Variant.Type.Array => value.AsGodotArray().Duplicate(deep: true),
        _ => value
    };
}
