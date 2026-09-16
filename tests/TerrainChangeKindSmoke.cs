using Beep.ECS;
using Godot;
using System.Collections.Generic;

/// <summary>
/// The CellsChanged contract: an edit says Terrain and bumps the content revision;
/// an eviction says Residency, names its one chunk, and leaves TerrainRevision and
/// PinnedNavigationRevision where they were. That is the point of ENH-01 - eviction is
/// where the rebuild storm came from, and a renderer or a demand search can only skip
/// it if the signal and the revisions they gate on agree that nothing changed.
///
/// NavigationRevision is the one that moves (corrected 2026-09-16). ENH-01 first held
/// that an eviction "changes no search input", which is true only of a search that pins
/// what it reads. A search with LoadMissingTerrain off pins nothing and reads
/// IsCellAvailable, so for it the evicted cells went from open to closed - and the
/// search_eviction probe, which had asserted exactly that since before ENH-01, failed.
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
        int cellKind = -1;
        int cellEmits = 0;
        cells.CellChanged += (x, y, kind) => { cellEmits++; cellKind = kind; };

        // Per-cell classification, and the no-op early-outs: an edit that changes
        // nothing returns false and emits nothing.
        var here = new Vector2I(6, 6);
        cells.SetTerrainKind(here, "grass");
        cellEmits = 0;
        if (cells.SetTerrainKind(here, "grass")) return Fail("SetTerrainKind to the same kind reported a change");
        if (cellEmits != 0) return Fail("SetTerrainKind no-op still emitted CellChanged");
        if (!cells.Till(here)) return Fail("Till of an untilled cell reported no change");
        if ((TerrainChangeKind)cellKind != TerrainChangeKind.Gameplay) return Fail($"Till kind was {cellKind}, expected Gameplay");
        cellEmits = 0;
        if (cells.Till(here)) return Fail("Till of an already-tilled cell reported a change");
        if (cellEmits != 0) return Fail("Till no-op still emitted CellChanged");
        if (!cells.Water(here)) return Fail("Water of a dry cell reported no change");
        if ((TerrainChangeKind)cellKind != TerrainChangeKind.Gameplay) return Fail($"Water kind was {cellKind}, expected Gameplay");
        cellEmits = 0;
        if (cells.Water(here)) return Fail("Water of an already-watered cell reported a change");
        if (cellEmits != 0) return Fail("Water no-op still emitted CellChanged");
        cells.SetFlags(here, 0);
        cells.RemoveCrop(here, clearTilled: true);

        // The daily index: AdvanceDay ages crops and evaporates standing water without
        // scanning every cell. A planted crop and a watered cell must be found; a cell
        // that is neither must be left alone. A missed index-maintenance site shows up
        // here as a crop that never grows or water that never dries.
        var crop = new Vector2I(20, 20);
        var puddle = new Vector2I(21, 21);
        cells.Till(crop);
        if (!cells.PlantCrop(crop, "wheat", 3)) return Fail("PlantCrop on tilled ground failed");
        cells.Water(puddle);
        cells.AdvanceDay(1);
        if (cells.GetCropAgeDays(crop) != 1) return Fail("AdvanceDay did not age a planted crop; the daily index missed it");
        if (cells.HasFlag(puddle, GridCellDataComponent.CellFlags.Watered)) return Fail("AdvanceDay did not evaporate standing water; the daily index missed it");
        cells.RemoveCrop(crop, clearTilled: true);
        cells.SetFlags(puddle, 0);

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

        // Evict that chunk. Residency only; its one chunk named; the renderers' revision and the
        // demand searches' revision hold, and the unpinned searches' revision moves.
        var chunk = new Vector2I(0, 0);
        long chunkRevision = cells.GetChunkRevision(chunk);
        ulong terrainMark = cells.TerrainRevision;
        ulong navigationMark = cells.NavigationRevision;
        ulong pinnedMark = cells.PinnedNavigationRevision;
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
        if (cells.PinnedNavigationRevision != pinnedMark)
            return Fail("Eviction bumped PinnedNavigationRevision; a demand search pins what it reads, so an eviction changes nothing it can see");
        if (cells.NavigationRevision == navigationMark)
            return Fail("Eviction left NavigationRevision alone; a search that pins nothing reads the evicted cells as unavailable now");

        // A gameplay first-touch of never-generated ground is NOT a terrain change:
        // GetOrCreate must not bump the global terrain revision, only the chunk token.
        var virginCell = new Vector2I(40, 40);                       // untouched chunk (ChunkSize 32 -> chunk (1,1))
        var virginChunk = GridCellDataComponent.ChunkOf(virginCell);
        ulong terrainMark2 = cells.TerrainRevision;
        long chunkMark2 = cells.GetChunkRevision(virginChunk);
        if (!cells.Till(virginCell)) return Fail("Till of a never-touched cell reported no change");
        if (cells.TerrainRevision != terrainMark2)
            return Fail("Till of a virgin cell bumped TerrainRevision; a gameplay first-touch is not a terrain change");
        if (cells.GetChunkRevision(virginChunk) == chunkMark2)
            return Fail("Till of a virgin cell did not advance the chunk content token");

        cells.Free();
        GD.Print("[terrain-change-kind] per-cell kinds, no-op early-outs, daily-index crop/water, bulk edit=Terrain(+Navigation), eviction=Residency (unpinned navigation revision only) OK");
        return true;
    }

    private static bool Fail(string message)
    {
        GD.PushError("[terrain-change-kind] " + message);
        return false;
    }
}
