using Beep.ECS;
using Godot;
using System.Collections.Generic;

/// <summary>
/// The CellsChanged contract: an edit says Terrain and bumps the content revision;
/// an eviction says Residency, names its one chunk, and moves NEITHER global revision.
/// The last is the whole point of ENH-01 - eviction is where the rebuild storm came
/// from, and a listener can only skip it if the signal, and the revisions renderers
/// gate on, agree that nothing changed.
/// </summary>
public partial class TerrainChangeKindSmoke : Node
{
    public bool Run()
    {
        var cells = new GridCellDataComponent { Name = "Cells" };
        AddChild(cells);

        int lastKind = -1;
        var lastChunks = new List<Vector2I>();
        int emits = 0;
        cells.CellsChanged += (kind, chunks) =>
        {
            emits++;
            lastKind = kind;
            lastChunks = new List<Vector2I>(chunks);
        };

        // An edit inside one chunk: Terrain, that chunk named, content revision up.
        ulong terrainBefore = cells.TerrainRevision;
        cells.FillTerrain(new Rect2I(2, 2, 3, 3), "desert");
        if (emits != 1) return Fail($"FillTerrain emitted {emits} times, expected 1");
        if (((TerrainChangeKind)lastKind & TerrainChangeKind.Terrain) == 0)
            return Fail($"FillTerrain kind {lastKind} did not include Terrain");
        if (cells.TerrainRevision == terrainBefore) return Fail("A terrain edit did not bump TerrainRevision");
        if (lastChunks.Count != 1 || lastChunks[0] != new Vector2I(0, 0))
            return Fail($"FillTerrain named {lastChunks.Count} chunks, expected only (0,0)");

        // A land->water edit also carries Navigation.
        cells.FillTerrain(new Rect2I(2, 2, 1, 1), "deep_water");
        if (((TerrainChangeKind)lastKind & TerrainChangeKind.Navigation) == 0)
            return Fail($"A land-to-water edit kind {lastKind} did not include Navigation");
        // Put it back to land so the chunk is evictable and land-typed.
        cells.FillTerrain(new Rect2I(2, 2, 1, 1), "desert");

        // Evict that chunk. Residency only; its one chunk named; neither revision moves.
        var chunk = new Vector2I(0, 0);
        long chunkRevision = cells.GetChunkRevision(chunk);
        ulong terrainMark = cells.TerrainRevision;
        ulong navigationMark = cells.NavigationRevision;
        emits = 0;
        if (!cells.CanEvictChunk(chunk)) return Fail("Freshly filled land chunk was not evictable");
        if (!cells.TryEvictChunk(chunk, chunkRevision)) return Fail("TryEvictChunk refused a current, evictable chunk");
        if (emits != 1) return Fail($"Eviction emitted {emits} times, expected 1");
        if ((TerrainChangeKind)lastKind != TerrainChangeKind.Residency)
            return Fail($"Eviction kind was {lastKind}, expected Residency ({(int)TerrainChangeKind.Residency}) alone");
        if (lastChunks.Count != 1 || lastChunks[0] != chunk)
            return Fail($"Eviction named {lastChunks.Count} chunks, expected only {chunk}");
        if (cells.TerrainRevision != terrainMark)
            return Fail("Eviction bumped TerrainRevision; a residency move must not read as content");
        if (cells.NavigationRevision != navigationMark)
            return Fail("Eviction bumped NavigationRevision; an evicted unpinned chunk changes no search input");

        cells.Free();
        GD.Print("[terrain-change-kind] edit=Terrain(+Navigation), eviction=Residency with revisions held OK");
        return true;
    }

    private static bool Fail(string message)
    {
        GD.PushError("[terrain-change-kind] " + message);
        return false;
    }
}
