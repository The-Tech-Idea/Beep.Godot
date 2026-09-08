using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Renders the generated map isometrically using an AUTHORED TileSet whose
    /// terrain peering bits do the transition matching.
    ///
    /// Hard terrain borders are what make an isometric map read as a
    /// checkerboard; transition tiles are what make it look finished. This does
    /// not choose those tiles itself - it hands whole runs of cells to
    /// SetCellsTerrainConnect and lets Godot pick, which is the same thing
    /// TerrainTransitionLayerComponent does for the flat view.
    ///
    /// WHY NOT PICK THE TILES HERE. An earlier version mapped a four-bit corner
    /// mask onto tile indices directly. That works only against an atlas whose
    /// layout has been verified, and deriving the layout from the pixels of a
    /// textured sheet is not reliable: two shades of grass do not separate the
    /// way flat colours do, and the derived mapping agreed with the known one on
    /// barely a third of tiles. Peering bits are authored once, in the editor,
    /// by someone who can see the tiles - and then they are simply correct.
    ///
    /// The TileSet is a resource you author and point at. Its tile shape must be
    /// isometric, and every terrain named below must exist in it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainIsometricAutotileRendererComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
        /// <summary>Authoritative live map, when assigned. Uses the same terrain rules as navigation.</summary>
        [Export] public NodePath CellDataPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(48, 48);

        [ExportGroup("TileSet")]
        /// <summary>
        /// An authored TileSet: isometric tile shape, one terrain set, and
        /// peering bits painted on the transition tiles.
        /// </summary>
        [Export] public TileSet? Tiles { get; set; }

        /// <summary>Which terrain set in the TileSet carries the terrains below.</summary>
        [Export(PropertyHint.Range, "0,8,1")] public int TerrainSet { get; set; }

        /// <summary>Disable for complete terrain tiles without authored transition artwork.
        /// Tiles are then selected by their TileData terrain assignment, not guessed atlas indices.</summary>
        [Export] public bool UseTerrainConnections { get; set; } = true;

        /// <summary>
        /// Terrain kinds this renderer paints, in draw order, as
        /// "kind[,kind...]=terrainIndex" - for example "grass,dry_grass=0".
        /// Kinds absent from this list are not painted, which is deliberate: a
        /// silently substituted terrain misdescribes the map.
        /// </summary>
        [Export] public string[] TerrainBindings { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild.
        /// </summary>
        [Export] public bool RefreshOnReady { get; set; } = true;
        [Export(PropertyHint.Range, "64,4096,64")] public int CellsPerFrame { get; set; } = 512;
        public bool IsRebuilding => _build is not null;
        public ulong PublicationRevision { get; private set; }
        public int CellsProcessedLastFrame { get; private set; }
        private IEnumerator<int>? _build;
        private ulong _buildRevision;
        private string _buildConfiguration = "";
        private TileSet? _buildTiles;
        private bool _buildTilesChanged;

        private TerrainGeneratorComponent? _generator;
        private TileMapLayer? _layer;
        private GridCellDataComponent? _cells;
        private bool _rebuildQueued;
        private Godot.Collections.Dictionary _paintDiagnostics = new();

        public Godot.Collections.Dictionary GetPaintDiagnostics() => _paintDiagnostics.Duplicate(true);

        private string TileSetProblem(Dictionary<string, int> bindings)
        {
            if (Tiles is null) return "Tiles needs an authored isometric TileSet.";
            if (Tiles.TileShape != TileSet.TileShapeEnum.Isometric) return "Tiles must use the isometric tile shape.";
            if (TerrainSet < 0 || TerrainSet >= Tiles.GetTerrainSetsCount()) return $"Terrain set {TerrainSet} does not exist.";
            var assigned = new HashSet<int>();
            for (int sourceIndex = 0; sourceIndex < Tiles.GetSourceCount(); sourceIndex++)
            {
                if (Tiles.GetSource(Tiles.GetSourceId(sourceIndex)) is not TileSetAtlasSource atlas) continue;
                for (int tileIndex = 0; tileIndex < atlas.GetTilesCount(); tileIndex++)
                {
                    Vector2I coords = atlas.GetTileId(tileIndex);
                    for (int alternative = 0; alternative < atlas.GetAlternativeTilesCount(coords); alternative++)
                    {
                        var data = atlas.GetTileData(coords, atlas.GetAlternativeTileId(coords, alternative));
                        if (data.TerrainSet != TerrainSet || data.Terrain < 0) continue;
                        if (!UseTerrainConnections) { assigned.Add(data.Terrain); continue; }
                        for (int bit = 0; bit < 16; bit++)
                            if (data.IsValidTerrainPeeringBit((TileSet.CellNeighbor)bit)
                                && data.GetTerrainPeeringBit((TileSet.CellNeighbor)bit) >= 0)
                            { assigned.Add(data.Terrain); break; }
                    }
                }
            }
            if (assigned.Count == 0) return $"Terrain set {TerrainSet} has no usable assigned atlas tiles (connections: {UseTerrainConnections}).";
            foreach (int terrain in bindings.Values)
                if (!assigned.Contains(terrain)) return $"Terrain {terrain} has no usable authored atlas tiles (connections: {UseTerrainConnections}).";
            return string.Empty;
        }

        /// <summary>The native layer shared with GridProjectionComponent through TileMapLayerPath.</summary>
        public TileMapLayer GetTerrainLayer()
        {
            var layer = EnsureLayer();
            layer.TileSet = Tiles;
            return layer;
        }

        public Vector2 CellPosition(Vector2I cell)
        {
            var layer = GetTerrainLayer();
            return layer.Transform * layer.MapToLocal(cell);
        }

        /// <summary>Logical tile extent; sprite overhang is not included.</summary>
        public Rect2 GridExtent(Vector2I size)
        {
            if (Tiles == null || size.X <= 0 || size.Y <= 0) return new Rect2();
            var layer = GetTerrainLayer();
            Vector2 tile = Tiles.TileSize;
            Rect2 extent = new(layer.MapToLocal(BoundsOrigin) - tile * 0.5f, tile);
            void Include(int x, int y) => extent = extent.Merge(
                new Rect2(layer.MapToLocal(BoundsOrigin + new Vector2I(x, y)) - tile * 0.5f, tile));
            // Include alternating rows/columns for staggered TileSet layouts too.
            for (int x = 0; x < size.X; x++) { Include(x, 0); Include(x, size.Y - 1); }
            for (int y = 0; y < size.Y; y++) { Include(0, y); Include(size.X - 1, y); }
            return layer.Transform * extent;
        }

        public override void _Ready()
        {
            ResolveCells();
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override void _ExitTree()
        {
            CancelRebuild();
            DisconnectCells();
        }

        private bool _hasRebuildAttempt;

        public override void _Notification(int what)
        {
            if (what == NotificationVisibilityChanged && _hasRebuildAttempt && IsInsideTree() && IsVisibleInTree() && !Engine.IsEditorHint())
                QueueRebuild();
        }

        private void DisconnectCells()
        {
            if (_cells is not null && GodotObject.IsInstanceValid(_cells))
            {
                _cells.CellChanged -= OnCellChanged;
                _cells.CellsChanged -= QueueRebuild;
            }
            _cells = null;
        }

        private void ResolveCells()
        {
            var cells = CellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(CellDataPath);
            if (cells == _cells) return;
            DisconnectCells();
            _cells = cells;
            if (_cells is null || Engine.IsEditorHint()) return;
            _cells.CellChanged += OnCellChanged;
            _cells.CellsChanged += QueueRebuild;
        }

        private void OnCellChanged(int x, int y)
        {
            if (new Rect2I(BoundsOrigin, BoundsSize).HasPoint(new Vector2I(x, y))) QueueRebuild();
        }

        private void QueueRebuild()
        {
            if (_rebuildQueued || !IsInsideTree() || !IsVisibleInTree()) return;
            _rebuildQueued = true;
            Callable.From(() =>
            {
                if (!_rebuildQueued) return;
                _rebuildQueued = false;
                if (IsInsideTree() && IsVisibleInTree())
                {
                    if (IsRebuilding || (long)BoundsSize.X * BoundsSize.Y > 65536) RequestRebuild();
                    else Rebuild();
                }
            }).CallDeferred();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (TerrainGeneratorPath.IsEmpty && CellDataPath.IsEmpty)
                return new[] { "Assign CellDataPath for a live map or TerrainGeneratorPath for a generated preview." };
            if (!TryBindings(out var bindings, out string problem)) return new[] { problem };
            problem = TileSetProblem(bindings);
            if (problem.Length > 0) return new[] { problem };
            return Array.Empty<string>();
        }

        /// <summary>Repaints the whole map, letting Godot match the transitions.</summary>
        public void Rebuild()
        {
            CancelRebuild();
            using var build = RebuildSteps().GetEnumerator();
            while (build.MoveNext()) { }
            if (_paintDiagnostics.ContainsKey("reason")) _layer?.Clear();
        }

        public void RequestRebuild()
        {
            CancelRebuild();
            _rebuildQueued = false;
            ResolveCells();
            _buildRevision = _cells?.TerrainRevision ?? 0;
            _buildConfiguration = BuildConfiguration();
            _buildTiles = Tiles;
            _buildTilesChanged = false;
            if (_buildTiles is not null) _buildTiles.Changed += OnBuildTilesChanged;
            _build = RebuildSteps().GetEnumerator();
            SetProcess(true);
        }

        public void CancelRebuild()
        {
            _build?.Dispose();
            _build = null;
            if (GodotObject.IsInstanceValid(_buildTiles)) _buildTiles!.Changed -= OnBuildTilesChanged;
            _buildTiles = null;
            SetProcess(false);
        }

        private void OnBuildTilesChanged() => _buildTilesChanged = true;

        internal void ShowPrepared()
        {
            Visible = true;
            _rebuildQueued = false;
        }

        private string BuildConfiguration()
        {
            var generator = _cells is null && !TerrainGeneratorPath.IsEmpty
                ? GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath) : null;
            string recipe = generator is null ? "" : $"{generator.GetInstanceId()}:{Json.Stringify(generator.CaptureConfiguration())}";
            return $"{CellDataPath}|{TerrainGeneratorPath}|{BoundsOrigin}|{BoundsSize}|{Tiles?.GetInstanceId()}|{TerrainSet}|{UseTerrainConnections}|{string.Join(';', TerrainBindings)}|{recipe}";
        }

        public override void _Process(double delta)
        {
            CellsProcessedLastFrame = 0;
            if (_build is null) return;
            var source = CellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(CellDataPath);
            if (_buildTilesChanged || source != _cells || (_cells?.TerrainRevision ?? 0) != _buildRevision || BuildConfiguration() != _buildConfiguration)
            {
                RequestRebuild();
                return;
            }
            int budget = Mathf.Clamp(CellsPerFrame, 64, 4096);
            try
            {
                while (_build is not null && CellsProcessedLastFrame < budget)
                {
                    if (!_build.MoveNext()) { CancelRebuild(); break; }
                    CellsProcessedLastFrame += _build.Current;
                }
            }
            catch (Exception error)
            {
                CancelRebuild();
                _paintDiagnostics = new() { ["valid"] = false, ["reason"] = error.Message };
                GD.PushError($"[{Name}] terrain publication failed: {error.Message}");
            }
        }

        private IEnumerable<int> RebuildSteps()
        {
            _hasRebuildAttempt = true;
            _rebuildQueued = false;
            ResolveCells();
            ResolveGenerator();
            _paintDiagnostics = new() { ["valid"] = false, ["requested"] = 0, ["missing"] = 0, ["unmapped"] = 0 };
            // Authored tiles may exist before this instance has ever rebuilt.
            _layer = GetNodeOrNull<TileMapLayer>("IsoTerrain");
            if ((!CellDataPath.IsEmpty && _cells is null) || (_cells is null && _generator is null))
            {
                _paintDiagnostics["reason"] = "Configured terrain source is missing.";
                GD.PushWarning($"[{Name}] configured terrain source is missing; nothing was drawn.");
                yield break;
            }
            bool validBindings = TryBindings(out var bindings, out string problem);
            if (validBindings) problem = TileSetProblem(bindings);
            if (problem.Length > 0)
            {
                _paintDiagnostics["reason"] = problem;
                GD.PushWarning($"[{Name}] {problem} Nothing was drawn.");
                yield break;
            }
            if (_cells is null && _generator is not null)
                TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            var layer = new TileMapLayer
            {
                Name = "IsoTerrainPending",
                Visible = false,
                YSortEnabled = true,
                RenderingQuadrantSize = 1,
                ZIndex = TerrainLayers.ZFor(TerrainLayers.Ground),
                ZAsRelative = false,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps
            };
            AddChild(layer);
            try
            {
                layer.TileSet = Tiles;
                layer.Clear();
                var expected = new Dictionary<Vector2I, int>();

                Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));

                GeneratedTerrainField? field = _cells is null ? _generator!.ResolveField() : null;

                var groups = new Dictionary<int, Godot.Collections.Array<Vector2I>>();
                var drawOrder = new List<int>();
                foreach (int terrain in bindings.Values)
                {
                    if (groups.ContainsKey(terrain)) continue;
                    groups.Add(terrain, new());
                    drawOrder.Add(terrain);
                }
                for (int y = 0; y < size.Y; y++)
                {
                    for (int x = 0; x < size.X; x++)
                    {
                        var local = new Vector2I(x, y);
                        var cell = BoundsOrigin + local;
                        string kind = _cells is not null ? GridCellRules.TerrainKindAt(_cells, cell)
                            : GridTerrainRules.Normalize(field!.TerrainAtCell(local));
                        if (bindings.TryGetValue(kind, out int terrain))
                        {
                            groups[terrain].Add(cell);
                            expected.Add(cell, terrain);
                        }
                        yield return 1;
                    }
                }
                // Keep binding draw order, but join all aliases of one terrain in
                // one native batch. Each source cell is read only once above.
                foreach (int terrain in drawOrder)
                {
                    var cells = groups[terrain];
                    if (cells.Count > 0)
                    {
                        if (UseTerrainConnections)
                        {
                            for (int start = 0; start < cells.Count; start += 64)
                            {
                                var batch = new Godot.Collections.Array<Vector2I>();
                                for (int i = start; i < Math.Min(start + 64, cells.Count); i++) batch.Add(cells[i]);
                                layer.SetCellsTerrainConnect(batch, TerrainSet, terrain);
                                yield return batch.Count;
                            }
                        }
                        else
                            foreach (int painted in PaintAssignedTiles(layer, cells, terrain)) yield return painted;
                    }
                }

                int missing = 0;
                foreach (var pair in expected)
                {
                    var data = layer.GetCellTileData(pair.Key);
                    if (data is null || data.TerrainSet != TerrainSet || data.Terrain != pair.Value) missing++;
                    yield return 1;
                }
                int unmapped = size.X * size.Y - expected.Count;
                _paintDiagnostics = new()
                {
                    ["valid"] = missing == 0 && unmapped == 0,
                    ["requested"] = expected.Count,
                    ["missing"] = missing,
                    ["unmapped"] = unmapped
                };
                if (missing > 0)
                    GD.PushWarning($"[{Name}] {missing} of {expected.Count} requested terrain cells are absent or use the wrong terrain; check transition coverage.");
                // Keep the native layer identity used by gameplay projection and authored paths.
                var visible = EnsureLayer();
                visible.TileSet = Tiles;
                visible.TileMapData = layer.TileMapData;
                PublicationRevision++;
            }
            finally
            {
                if (GodotObject.IsInstanceValid(layer)) layer.Free();
            }
        }

        private IEnumerable<int> PaintAssignedTiles(TileMapLayer layer, Godot.Collections.Array<Vector2I> cells, int terrain)
        {
            var choices = new List<(int Source, Vector2I Atlas, int Alternative)>();
            for (int i = 0; i < Tiles!.GetSourceCount(); i++)
            {
                int source = Tiles.GetSourceId(i);
                if (Tiles.GetSource(source) is not TileSetAtlasSource atlas) continue;
                for (int t = 0; t < atlas.GetTilesCount(); t++)
                {
                    Vector2I coord = atlas.GetTileId(t);
                    for (int a = 0; a < atlas.GetAlternativeTilesCount(coord); a++)
                    {
                        int alternative = atlas.GetAlternativeTileId(coord, a);
                        TileData data = atlas.GetTileData(coord, alternative);
                        if (data.TerrainSet == TerrainSet && data.Terrain == terrain)
                            choices.Add((source, coord, alternative));
                    }
                }
            }
            foreach (Vector2I cell in cells)
            {
                uint hash = unchecked((uint)cell.X * 73856093u ^ (uint)cell.Y * 19349663u);
                var tile = choices[(int)(hash % (uint)choices.Count)];
                layer.SetCell(cell, tile.Source, tile.Atlas, tile.Alternative);
                yield return 1;
            }
        }

        /// <summary>
        /// Resolves one owner for each normalized kind before any tiles are painted.
        /// </summary>
        private bool TryBindings(out Dictionary<string, int> bindings, out string problem)
        {
            bindings = new();
            problem = string.Empty;
            foreach (string entry in TerrainBindings)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] halves = entry.Split('=', StringSplitOptions.TrimEntries);
                if (halves.Length != 2 || !int.TryParse(halves[1], out int terrain) || terrain < 0)
                {
                    problem = $"Terrain binding '{entry}' requires kind[,kind...]=terrainIndex with a non-negative index.";
                    return false;
                }

                var kinds = halves[0].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (kinds.Length == 0)
                {
                    problem = $"Terrain binding '{entry}' needs at least one terrain kind.";
                    return false;
                }
                foreach (string rawKind in kinds)
                {
                    string kind = GridTerrainRules.Normalize(rawKind);
                    if (bindings.TryGetValue(kind, out int previous) && previous != terrain)
                    {
                        problem = $"Terrain kind '{kind}' is bound to both {previous} and {terrain}.";
                        return false;
                    }
                    bindings[kind] = terrain;
                }
            }
            if (bindings.Count > 0) return true;
            problem = "TerrainBindings needs at least one kind=terrainIndex entry.";
            return false;
        }

        private TileMapLayer EnsureLayer()
        {
            _layer = TerrainAuthoring.EnsureLayer(this, "IsoTerrain");

            // Without Y sorting a tile drawn later covers one that should be in
            // front of it, and an isometric scene falls apart immediately.
            _layer.YSortEnabled = true;

            // Y sorting alone is not enough. Godot batches tiles into quadrants
            // and sorts whole QUADRANTS against each other, so two tiles in one
            // batch draw in atlas order however they overlap. This layer enabled
            // Y sorting and left the quadrant size at its default, which is the
            // half-fixed state that looks correct until two neighbouring tiles
            // differ in height. One tile per quadrant is what makes the sort
            // actually per-tile - the same thing TerrainIsometricRendererComponent
            // does, for the same reason.
            _layer.RenderingQuadrantSize = 1;

            // The shared stack, like every other view. This layer is the ground
            // it paints, and it drew at Node2D's default z of 0 - the slot the
            // stack gives the SEA - so anything else on the shared stack landed
            // on the wrong side of it.
            _layer.ZIndex = TerrainLayers.ZFor(TerrainLayers.Ground);
            _layer.ZAsRelative = false;

            // Authored isometric tiles are detailed art minified hard at map
            // zoom; without a mip-aware filter they alias into a shimmering grid.
            _layer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            return _layer;
        }

        private void ResolveGenerator()
        {
            _generator = TerrainGeneratorPath.IsEmpty ? null
                : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
        }
    }
}
