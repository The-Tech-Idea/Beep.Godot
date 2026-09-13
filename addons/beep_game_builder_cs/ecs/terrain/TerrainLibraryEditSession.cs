using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>Persistent native-brush working copy. Live cells change only through a validated transaction.</summary>
[Tool, GlobalClass]
public partial class TerrainLibraryEditSession : Node
{
    [Export] public bool Active { get; set; }
    [Export] public NodePath CellDataPath { get; set; } = new("");
    [Export] public NodePath DisplayPath { get; set; } = new("");
    [Export] public TerrainLibraryPack? Pack { get; set; }
    [Export] public Vector2I BoundsOrigin { get; set; }
    [Export] public Vector2I BoundsSize { get; set; }
    [Export] public string BaselinePackKey { get; set; } = "";
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Baseline { get; set; } = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> VisualOverrides { get; set; } = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> EditorCellSeed { get; set; } = new();
    public string Problem { get; private set; } = "";

    public static TerrainLibraryEditSession? Find(Node renderer) => renderer.GetNodeOrNull<TerrainLibraryEditSession>("TerrainEditSession");
    public static bool Blocks(Node renderer) => Find(renderer)?.Active == true;
    public TileMapLayer? WorkingLayer => GetNodeOrNull<TileMapLayer>("WorkingTerrain");

    public static TerrainLibraryEditSession GetOrCreate(Node renderer)
    {
        var session = Find(renderer);
        if (session is not null) return session;
        if (renderer.HasNode("TerrainEditSession")) throw new InvalidOperationException("TerrainEditSession name is already in use.");
        session = new() { Name = "TerrainEditSession" };
        renderer.AddChild(session);
        TerrainAuthoring.Adopt(session, renderer);
        return session;
    }

    public override void _Ready() => CallDeferred(nameof(RestoreEditorState));

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsString() is nameof(Active) or nameof(CellDataPath) or nameof(DisplayPath) or nameof(Pack)
            or nameof(BoundsOrigin) or nameof(BoundsSize) or nameof(BaselinePackKey) or nameof(Baseline)
            or nameof(VisualOverrides) or nameof(EditorCellSeed)) property["usage"] = (long)PropertyUsageFlags.Storage;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationEditorPreSave && Engine.IsEditorHint() && !CellDataPath.IsEmpty
            && GetNodeOrNull<GridCellDataComponent>(CellDataPath) is { } cells) CaptureEditorSeed(cells);
    }

    public void RestoreEditorState()
    {
        // Scene-only editing has no runtime save loader. Seed only an empty editor store,
        // never a loaded game's live cells or an already-populated editor map.
        if (Engine.IsEditorHint() && !CellDataPath.IsEmpty && EditorCellSeed.Count > 0
            && GetNodeOrNull<GridCellDataComponent>(CellDataPath) is { } cells && cells.GetStoredChunks().Count == 0)
        {
            try { cells.LoadCells(EditorCellSeed, clearExisting: false); }
            catch (Exception error) { Problem = error.Message; GD.PushWarning(Problem); }
        }
        UpdateVisibility();
    }

    private void CaptureEditorSeed(GridCellDataComponent cells)
    {
        var seed = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var state in Baseline) seed.Add(cells.GetCell(state["cell"].AsVector2I()).Duplicate(true));
        EditorCellSeed = seed;
    }

    private bool GenerationIsRunning()
    {
        var nodes = new Stack<Node>();
        nodes.Push(GetTree().Root);
        var renderer = GetParent();
        while (nodes.Count > 0)
        {
            var node = nodes.Pop();
            if (node is TerrainWorldComponent world && world.IsGenerating)
                foreach (var path in new[] { world.TileRendererPath, world.IsometricAutotileRendererPath })
                    if (!path.IsEmpty && world.GetNodeOrNull<Node>(path) == renderer) return true;
            foreach (Node child in node.GetChildren()) nodes.Push(child);
        }
        return false;
    }

    public string Begin()
    {
        if (Active) return Problem = "An edit session is already active.";
        if (GenerationIsRunning()) return Problem = "Wait for world generation to finish before editing.";
        var previousPack = Pack;
        var renderer = GetParent();
        NodePath sourcePath;
        NodePath displayPath;
        TerrainLibraryPack? nextPack;
        Vector2I origin, size;
        if (renderer is TerrainTileRendererComponent flat)
        {
            nextPack = flat.LibraryPack; origin = flat.BoundsOrigin; size = flat.BoundsSize;
            sourcePath = flat.CellDataPath;
            displayPath = new("../LibraryTerrain");
        }
        else if (renderer is TerrainIsometricAutotileRendererComponent iso)
        {
            iso.CancelRebuild();
            nextPack = iso.LibraryPack; origin = iso.BoundsOrigin; size = iso.BoundsSize;
            sourcePath = iso.CellDataPath;
            displayPath = new("../IsoTerrain");
        }
        else return Problem = "Select a supported terrain renderer.";
        if (nextPack is null) return Problem = "Assign and render a library pack first.";
        var cells = sourcePath.IsEmpty ? null : renderer.GetNodeOrNull<GridCellDataComponent>(sourcePath);
        var display = GetNodeOrNull<TileMapLayer>(displayPath);
        if (cells is null || display is null || display.TileSet != nextPack.Tiles) return Problem = "Render the pack from live cell data before editing.";
        Problem = nextPack.Validate(renderer is TerrainTileRendererComponent ? TerrainProjection.Tiles : TerrainProjection.IsometricAutotile);
        if (Problem.Length > 0) return Problem;
        if (size.X <= 0 || size.Y <= 0) return Problem = "Invalid map bounds.";
        var baseline = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int y = origin.Y; y < origin.Y + size.Y; y++)
            for (int x = origin.X; x < origin.X + size.X; x++) baseline.Add(cells.CaptureTerrainEditCell(new(x, y)));
        Problem = cells.ValidateTerrainEditPatch(baseline, baseline);
        if (Problem.Length > 0) return Problem;
        // A rejected Begin must not replace the previous session's saved bindings.
        Pack = nextPack;
        BoundsOrigin = origin;
        BoundsSize = size;
        DisplayPath = displayPath;
        CellDataPath = GetPathTo(cells);
        Baseline = baseline;
        if (previousPack != Pack) VisualOverrides = new();
        CaptureEditorSeed(cells);
        BaselinePackKey = StablePackKey();
        var working = TerrainAuthoring.EnsureLayer(this, "WorkingTerrain");
        working.TileSet = display.TileSet;
        working.TileMapData = (byte[])display.TileMapData.Clone();
        working.Transform = display.Transform;
        working.ZIndex = display.ZIndex;
        working.ZAsRelative = display.ZAsRelative;
        working.YSortEnabled = display.YSortEnabled;
        working.RenderingQuadrantSize = display.RenderingQuadrantSize;
        working.TextureFilter = display.TextureFilter;
        working.CollisionEnabled = false;
        working.NavigationEnabled = false;
        Active = true;
        UpdateVisibility();
        return Problem = "";
    }

    private string StablePackKey()
    {
        if (Pack?.Tiles is null) return "";
        var result = new System.Text.StringBuilder();
        result.Append(Pack.PackId).Append('|').Append(Pack.Version).Append('|').Append(Pack.Projection)
            .Append('|').Append(Pack.TerrainSet).Append('|').Append(Pack.Tiles.TileSize).Append('|').Append(Pack.Tiles.TileShape)
            .Append('|').Append(Pack.Tiles.TileLayout).Append('|').Append(Pack.Tiles.TileOffsetAxis)
            .Append('|').Append(Pack.BackgroundSource).Append('|').Append(Pack.BackgroundAtlas).Append('|').Append(Pack.BackgroundAlternative);
        foreach (var pair in new SortedDictionary<string, int>(Pack.TerrainBindings)) result.Append('|').Append(pair.Key).Append('=').Append(pair.Value);
        Pack.AppendElevationContract(result);
        for (int s = 0; s < Pack.Tiles.GetSourceCount(); s++)
        {
            int id = Pack.Tiles.GetSourceId(s);
            if (Pack.Tiles.GetSource(id) is not TileSetAtlasSource atlas) continue;
            for (int t = 0; t < atlas.GetTilesCount(); t++)
            {
                var coord = atlas.GetTileId(t);
                for (int a = 0; a < atlas.GetAlternativeTilesCount(coord); a++)
                {
                    int alt = atlas.GetAlternativeTileId(coord, a);
                    var data = atlas.GetTileData(coord, alt);
                    result.Append('|').Append(id).Append(':').Append(coord).Append(':').Append(alt).Append(':').Append(data.TerrainSet).Append(':').Append(data.Terrain);
                    result.Append(':').Append(Pack.TileElevationId(data));
                    for (int b = 0; b < 16; b++)
                        if (data.IsValidTerrainPeeringBit((TileSet.CellNeighbor)b)) result.Append(',').Append(data.GetTerrainPeeringBit((TileSet.CellNeighbor)b));
                }
            }
        }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(result.ToString())));
    }

    public Godot.Collections.Dictionary PrepareApply()
    {
        Problem = "";
        var empty = new Godot.Collections.Dictionary();
        var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath);
        var working = WorkingLayer;
        var currentPack = GetParent() is TerrainTileRendererComponent flat ? flat.LibraryPack
            : (GetParent() as TerrainIsometricAutotileRendererComponent)?.LibraryPack;
        if (!Active || cells is null || Pack is null || working is null || currentPack != Pack || working.TileSet != Pack.Tiles)
        { Problem = "The active session, cell source or pack has changed."; return empty; }
        Vector2I origin = GetParent() is TerrainTileRendererComponent f ? f.BoundsOrigin : ((TerrainIsometricAutotileRendererComponent)GetParent()).BoundsOrigin;
        Vector2I size = GetParent() is TerrainTileRendererComponent f2 ? f2.BoundsSize : ((TerrainIsometricAutotileRendererComponent)GetParent()).BoundsSize;
        NodePath path = GetParent() is TerrainTileRendererComponent f3 ? f3.CellDataPath : ((TerrainIsometricAutotileRendererComponent)GetParent()).CellDataPath;
        if (origin != BoundsOrigin || size != BoundsSize || path.IsEmpty || GetParent().GetNodeOrNull<GridCellDataComponent>(path) != cells)
        { Problem = "Map bounds or live cell source changed during editing."; return empty; }
        Problem = Pack.Validate(Pack.Projection);
        if (Problem.Length > 0) return empty;
        if (BaselinePackKey != StablePackKey()) { Problem = "Pack configuration changed; preserve this copy and restart editing."; return empty; }
        Problem = cells.ValidateTerrainEditPatch(Baseline, Baseline);
        if (Problem.Length > 0) return empty;
        var bounds = new Rect2I(BoundsOrigin, BoundsSize);
        foreach (var cell in working.GetUsedCells())
            if (!bounds.HasPoint(cell)) { Problem = $"Painted cell {cell} is outside the map."; return empty; }
        var before = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var after = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var kinds = new Dictionary<Vector2I, string>();
        var profiles = new Dictionary<Vector2I, string>();
        foreach (var state in Baseline)
        {
            var cell = state["cell"].AsVector2I();
            TileData? tile = working.GetCellTileData(cell);
            int terrain = tile?.Terrain ?? -1;
            if (tile is not null && tile.TerrainSet != Pack.TerrainSet) { Problem = $"Unmapped tile at {cell}."; return empty; }
            string oldKind = state["terrain"].AsString();
            string kind = "";
            if (Pack.TerrainBindings.TryGetValue(oldKind, out int oldId) && oldId == terrain) kind = oldKind;
            else foreach (var pair in Pack.TerrainBindings)
                if (pair.Value == terrain)
                {
                    if (kind.Length > 0) { Problem = $"Terrain {terrain} has multiple logical kinds; author an unambiguous pack."; return empty; }
                    kind = pair.Key;
                }
            if (kind.Length == 0) { Problem = $"No logical kind for tile/erasure at {cell}."; return empty; }
            kinds[cell] = kind;
            string oldProfile = Pack.ElevationAt(cells, cell);
            string profile = Pack.TileElevationId(tile);
            if (profile.Length == 0) profile = oldProfile;
            profiles[cell] = profile;
            if (kind == oldKind && profile == oldProfile) continue;
            var replacement = profile != oldProfile
                ? cells.PreviewTerrainElevation(cell, kind, profile, Pack.ElevationValues[profile])
                : cells.PreviewTerrainEdit(cell, kind);
            before.Add(state.Duplicate(true)); after.Add(replacement);
        }
        var canonical = new TileMapLayer { CollisionEnabled = false, NavigationEnabled = false };
        AddChild(canonical);
        canonical.Visible = false;
        var overrides = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        try
        {
            foreach (int _ in TerrainLibraryPainter.Build(canonical, Pack, bounds, cell => kinds[cell], cell => profiles[cell])) { }
            foreach (var pair in kinds)
            {
                var cell = pair.Key;
                var authored = working.GetCellTileData(cell);
                var expected = canonical.GetCellTileData(cell);
                if (authored is null)
                {
                    // Erasing means the pack's explicit background, never an undefined logical hole.
                    continue;
                }
                if (!SameConnections(authored, expected)) { Problem = $"Connections at {cell} do not match neighboring terrain. Use the terrain brush."; return empty; }
                if (Pack.TileElevationId(authored) == profiles[cell] && (working.GetCellSourceId(cell) != canonical.GetCellSourceId(cell)
                    || working.GetCellAtlasCoords(cell) != canonical.GetCellAtlasCoords(cell)
                    || working.GetCellAlternativeTile(cell) != canonical.GetCellAlternativeTile(cell)))
                    overrides.Add(new() { ["cell"] = cell, ["kind"] = pair.Value, ["source"] = working.GetCellSourceId(cell),
                        ["atlas"] = working.GetCellAtlasCoords(cell), ["alternative"] = working.GetCellAlternativeTile(cell) });
            }
        }
        catch (Exception error) { Problem = error.Message; return empty; }
        finally { canonical.Free(); }
        return new() { ["before"] = before, ["after"] = after, ["old_visuals"] = VisualOverrides.Duplicate(true),
            ["new_visuals"] = overrides, ["baseline"] = Baseline.Duplicate(true), ["working"] = (byte[])working.TileMapData.Clone(),
            ["pack_key"] = BaselinePackKey };
    }

    internal static bool SameConnections(TileData? a, TileData? b)
    {
        if (a is null || b is null || a.TerrainSet != b.TerrainSet || a.Terrain != b.Terrain) return false;
        for (int i = 0; i < 16; i++)
            if (a.IsValidTerrainPeeringBit((TileSet.CellNeighbor)i)
                && a.GetTerrainPeeringBit((TileSet.CellNeighbor)i) != b.GetTerrainPeeringBit((TileSet.CellNeighbor)i)) return false;
        return true;
    }

    public void Commit(Godot.Collections.Dictionary transaction, bool forward)
    {
        var cells = GetNodeOrNull<GridCellDataComponent>(CellDataPath);
        var renderer = GetParent();
        bool sameSource = renderer is TerrainTileRendererComponent flat
            ? flat.LibraryPack == Pack && flat.BoundsOrigin == BoundsOrigin && flat.BoundsSize == BoundsSize
                && !flat.CellDataPath.IsEmpty && flat.GetNodeOrNull<GridCellDataComponent>(flat.CellDataPath) == cells
            : renderer is TerrainIsometricAutotileRendererComponent iso
                && iso.LibraryPack == Pack && iso.BoundsOrigin == BoundsOrigin && iso.BoundsSize == BoundsSize
                && !iso.CellDataPath.IsEmpty && iso.GetNodeOrNull<GridCellDataComponent>(iso.CellDataPath) == cells;
        if (cells is null || !sameSource || transaction["pack_key"].AsString() != StablePackKey())
        { Problem = "Cannot apply history: cell source or pack changed."; GD.PushWarning(Problem); return; }
        var expected = new Godot.Collections.Array<Godot.Collections.Dictionary>(transaction[forward ? "before" : "after"].AsGodotArray());
        var next = new Godot.Collections.Array<Godot.Collections.Dictionary>(transaction[forward ? "after" : "before"].AsGodotArray());
        Problem = cells.ApplyTerrainEditPatch(expected, next);
        if (Problem.Length > 0) { GD.PushWarning(Problem); return; }
        VisualOverrides = new(transaction[forward ? "new_visuals" : "old_visuals"].AsGodotArray());
        Active = !forward;
        if (!forward)
        {
            Baseline = new(transaction["baseline"].AsGodotArray());
            if (WorkingLayer is { } working) working.TileMapData = transaction["working"].AsByteArray();
        }
        CaptureEditorSeed(cells);
        UpdateVisibility();
        if (forward && renderer is TerrainRendererComponent view) view.Rebuild();
    }

    public void Discard()
    {
        Problem = "";
        if (!CellDataPath.IsEmpty && GetNodeOrNull<GridCellDataComponent>(CellDataPath) is { } cells) CaptureEditorSeed(cells);
        Active = false;
        UpdateVisibility();
        if (GetParent() is TerrainRendererComponent renderer) renderer.Rebuild();
    }

    public void UpdateVisibility()
    {
        if (WorkingLayer is { } working) working.Visible = Active;
        if (!DisplayPath.IsEmpty && GetNodeOrNull<TileMapLayer>(DisplayPath) is { } display) display.Visible = !Active;
    }

    internal static void RestoreVisuals(Node renderer, TileMapLayer layer)
    {
        var session = Find(renderer);
        if (session is null || session.Active || session.Pack?.Tiles != layer.TileSet) return;
        var cells = session.GetNodeOrNull<GridCellDataComponent>(session.CellDataPath);
        if (cells is null) return;
        foreach (var entry in session.VisualOverrides)
        {
            Vector2I cell = entry["cell"].AsVector2I();
            int source = entry["source"].AsInt32();
            var atlasCoord = entry["atlas"].AsVector2I();
            int alternative = entry["alternative"].AsInt32();
            if (cells.GetTerrainKind(cell) != entry["kind"].AsString() || !layer.TileSet.HasSource(source)
                || layer.TileSet.GetSource(source) is not TileSetAtlasSource atlas || !atlas.HasTile(atlasCoord)
                || !atlas.HasAlternativeTile(atlasCoord, alternative)) continue;
            var candidate = atlas.GetTileData(atlasCoord, alternative);
            var current = layer.GetCellTileData(cell);
            if (session.Pack.TileElevationId(candidate) == session.Pack.TileElevationId(current) && SameConnections(candidate, current))
                layer.SetCell(cell, source, atlasCoord, alternative);
        }
    }
}
