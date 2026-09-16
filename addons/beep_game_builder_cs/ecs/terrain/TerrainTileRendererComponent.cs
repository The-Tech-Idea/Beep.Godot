using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Renders generated terrain as real Godot tiles, one autotiled layer per
    /// biome, stacked in a fixed order.
    ///
    /// This is the renderer to use when a game has tileset art. Borders come
    /// from 15-piece TRANSITION TILES, which is how a 2D game gets a smooth
    /// coastline while every tile stays a discrete gameplay tile - as opposed to
    /// blurring a painted image, which only hides the tile grid rather than
    /// respecting it.
    ///
    /// It builds and owns one <see cref="TerrainTransitionLayerComponent"/>
    /// and one TileMapLayer per configured biome, so a scene needs a single node
    /// instead of a hand-wired pair per biome.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainTileRendererComponent : TerrainRendererComponent
    {
        /// <summary>
        /// A biome and the atlas that draws it. WHICH LEVEL it belongs to is not
        /// stored here: TerrainLayers.LevelForKind answers that from the kind,
        /// and the transition layer places its own node from the same call. A
        /// level recorded here as well would be a second copy of that mapping,
        /// which is how gravel and rock came to be classified in two places.
        /// </summary>
        private readonly record struct BiomeLayer(
            string TerrainKind, string AtlasPath, string DetailAtlasPath);


        [ExportGroup("Map")]
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(48, 30);
        [Export] public TerrainLibraryPack? LibraryPack { get; set; }
        private Godot.Collections.Dictionary _paintDiagnostics = new();
        /// <summary>Whether the last build drew this view, and the reason when it did not.</summary>
        public Godot.Collections.Dictionary GetPaintDiagnostics() => _paintDiagnostics.Duplicate(true);
        public int LibraryCellsUpdated { get; private set; }
        private readonly HashSet<Vector2I> _libraryDirty = new();
        private TerrainLibraryPack? _publishedPack;
        private string _publishedPackKey = "";
        private Rect2I _publishedPackBounds;

        /// <summary>Logical cells, not the half-cell-offset corner display grid.</summary>
        public TileMapLayer GetTerrainLayer()
        {
            var layer = TerrainAuthoring.EnsureLayer(this, "LogicalGrid");
            var size = new Vector2I(Mathf.Max(1, AtlasTileSize.X), Mathf.Max(1, AtlasTileSize.Y));
            if (layer.TileSet is null || layer.TileSet.TileSize != size)
                layer.TileSet = new TileSet { TileSize = size };
            return layer;
        }

        [ExportGroup("Atlas Layout")]
        [Export] public Vector2I AtlasTileSize { get; set; } = new(64, 64);
        [Export(PropertyHint.Range, "1,16,1")] public int AtlasColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int AtlasTileRows { get; set; } = 4;

        [ExportGroup("Base")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string BaseAtlasPath { get; set; } = "";

        [ExportGroup("Biome Atlases")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string GrassAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string GrassDetailAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DryGrassAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SandAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DesertAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DesertDetailAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string JungleAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SwampAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string TundraAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string RockAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string GravelAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SnowAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string IceAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string WaterAtlasPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string WaterDetailAtlasPath { get; set; } = "";

        [ExportGroup("Rendering")]
        /// <summary>Optional repeating surface textures keyed by exact biome name.
        /// The transition atlas continues to own coverage and borders.</summary>
        [Export] public Godot.Collections.Dictionary<string, string> GroundTexturePaths { get; set; } = new();
        [Export(PropertyHint.Range, "1,32,0.5")] public float GroundRepeatCells { get; set; } = 6f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float GroundDetailStrength { get; set; } = 0.3f;
        /// <summary>
        /// The sea, drawn by the SAME shader the isometric view uses.
        ///
        /// Water tiles alone give a flat blue field: no depth, no shore, no
        /// swell. The shader that draws the sea elsewhere was tuned until it was
        /// right, and it takes a projection switch precisely so a second view
        /// does not need a second sea to keep in step with the first.
        ///
        /// Leave the path empty and the tiles are all that draws, which is what
        /// a game wanting a flat stylised sea would ask for.
        /// </summary>
        [ExportGroup("Water Surface")]
        /// <summary>
        /// The generator this view draws - not just for the sea, despite the
        /// group above: ConfiguredLayers also reads it to know which biomes the
        /// map actually contains. Named to match every sibling renderer's
        /// TerrainGeneratorPath rather than the bare GeneratorPath this used to
        /// be, which was this renderer's own one-off spelling of the same
        /// export.
        /// </summary>
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export(PropertyHint.File, "*.gdshader")] public string WaterShaderPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,24,0.5")] public float CoastRangeTiles { get; set; } = TerrainCoastField.DefaultRangeTiles;
        [Export(PropertyHint.Range, "1,16,1")] public int CoastDetail { get; set; } = TerrainCoastField.DefaultDetail;
        [Export(PropertyHint.Range, "0,1,0.01")] public float MaxOpacity { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float ShoreOpacity { get; set; } = 0.55f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float LakeOpacity { get; set; } = 0.42f;
        [Export(PropertyHint.Range, "0.1,12,0.1")] public float ClarityTiles { get; set; } = 3.0f;
        /// <summary>
        /// How the sea LOOKS - the thirteen dials and four textures every view of this world
        /// shares (VIEW-04). This view used to export its own copies, defaulted differently from
        /// the other two views, so one map drawn twice grew two seas. Unassigned, the shipped
        /// defaults are used, which are still the same sea the other views draw.
        /// </summary>
        [Export] public TerrainWaterLook? WaterLook { get; set; }

        private readonly List<TerrainTransitionLayerComponent> _layers = new();
        private const string BiomeDisplayMetadata = "_terrain_tile_biome_display";
        private TerrainGeneratorComponent? _generator;
        private GridCellDataComponent? _cells;
        private bool _coastQueued;
        private TileMapLayer? _water;
        private readonly TerrainSeaSurface _sea = new();
        /// <summary>What the current layers were built from; see Signature.</summary>
        private string _builtSignature = string.Empty;



        public override void _Ready()
        {
            ResolveCells();
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override void _ExitTree() => DisconnectCells();
        private void DisconnectCells()
        {
            if (_cells is not null && GodotObject.IsInstanceValid(_cells))
            {
                _cells.CellChanged -= OnCellChanged;
                _cells.CellsChanged -= OnCellsChangedSignal;
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
            _cells.CellsChanged += OnCellsChangedSignal;
        }

        private void OnCellChanged(int x, int y, int kind)
        {
            if (LibraryPack is not null)
            {
                if (((TerrainChangeKind)kind & TerrainChangeKind.Content) != 0)
                {
                    _libraryDirty.Add(new Vector2I(x, y));
                    QueueRebuild();
                }
                return;
            }
            if (((TerrainChangeKind)kind & (TerrainChangeKind.Terrain | TerrainChangeKind.Navigation)) != 0) QueueCoast();
        }

        // A content change requeues only the coast (not the whole tile map); a
        // residency move is ignored.
        protected override void OnCellsChangedSignal(int kind, Godot.Collections.Array<Vector2I> chunks)
        {
            if (LibraryPack is not null)
            {
                if (((TerrainChangeKind)kind & TerrainChangeKind.Content) != 0) _publishedPack = null;
                base.OnCellsChangedSignal(kind, chunks);
                return;
            }
            if (((TerrainChangeKind)kind & TerrainChangeKind.Content) != 0) QueueCoast();
        }

        private void QueueCoast()
        {
            if (_coastQueued || !IsInsideTree() || !IsVisibleInTree() || string.IsNullOrWhiteSpace(WaterShaderPath)) return;
            _coastQueued = true;
            Callable.From(() =>
            {
                if (!_coastQueued) return;
                _coastQueued = false;
                if (IsInsideTree() && IsVisibleInTree()) EnsureWaterSurface();
            }).CallDeferred();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (LibraryPack is not null)
            {
                string problem = LibraryPack.Validate(TerrainProjection.Tiles);
                if (problem.Length > 0) return new[] { problem };
            }
            if (TerrainGeneratorPath.IsEmpty && CellDataPath.IsEmpty)
                return new[] { "TerrainGeneratorPath should point to the TerrainGeneratorComponent this view draws." };
            if (LibraryPack is null && ConfiguredLayers().Count == 0)
                return new[] { "Assign at least one biome atlas, or nothing will be drawn." };
            return Array.Empty<string>();
        }

        /// <summary>Rebuilds every biome layer from the current cell data.</summary>
        public override void Rebuild()
        {
            if (TerrainLibraryEditSession.Blocks(this)) return;
            if (LibraryPack is not null) { RebuildLibrary(); return; }
            var oldLibrary = GetNodeOrNull<TileMapLayer>("LibraryTerrain");
            if (oldLibrary is not null) oldLibrary.Visible = false;
            HasRebuildAttempt = true;
            ClearRebuildQueued();
            _coastQueued = false;
            // Checked FIRST, before anything else runs - the same shape every
            // sibling renderer uses. Without this, a scene with atlases
            // configured but TerrainGeneratorPath unwired drew nothing and said
            // nothing: ConfiguredLayers' present-filter is only a filter when a
            // generator resolved, so an atlas-only config still produced a
            // non-empty layer list and never reached the "no biome atlas"
            // warning below, which is the one case that warning exists for.
            ResolveGenerator();
            ResolveCells();
            if ((!CellDataPath.IsEmpty && _cells is null) || (_cells is null && _generator is null))
            {
                ClearSurface();
                RecordBuildFailure("no generator at TerrainGeneratorPath; no terrain tiles were drawn.");
                return;
            }
            if (_cells is null && _generator is not null)
                TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            EnsureLayers();

            // The editor already warns that an unconfigured renderer draws
            // nothing, but a scene loaded at runtime never sees that. Without it
            // an empty biome list is indistinguishable from an empty world.
            if (_layers.Count == 0)
                RecordBuildFailure("no biome atlas is configured, so no terrain tiles were drawn.");

            foreach (TerrainTransitionLayerComponent layer in _layers)
            {
                layer.RefreshTransitions();
                ApplyGroundDetail(layer);
            }

            EnsureWaterSurface();
            if (_layers.Count > 0)
                _paintDiagnostics = new() { ["valid"] = true, ["requested"] = BoundsSize.X * BoundsSize.Y };
        }

        private void RebuildLibrary()
        {
            HasRebuildAttempt = true;
            ClearRebuildQueued();
            _libraryDirty.Clear();
            string problem = LibraryPack!.Validate(TerrainProjection.Tiles);
            if (problem.Length > 0) { RecordBuildFailure(problem); return; }
            ResolveCells();
            ResolveGenerator();
            if ((!CellDataPath.IsEmpty && _cells is null) || (_cells is null && _generator is null))
            {
                RecordBuildFailure("Library rendering requires a valid terrain source.");
                return;
            }
            var display = TerrainAuthoring.EnsureLayer(this, "LibraryTerrain");
            var field = _cells is null ? _generator!.ResolveField() : null;
            string KindAt(Vector2I cell) => _cells is not null ? GridCellRules.TerrainKindAt(_cells, cell)
                : field!.TerrainAtCell(cell - BoundsOrigin);
            try
            {
                foreach (int _ in TerrainLibraryPainter.Build(display, LibraryPack,
                    new Rect2I(BoundsOrigin, BoundsSize), KindAt, cell => LibraryPack.ElevationAt(_cells, cell))) { }
            }
            // The painter's failure for a pack that cannot draw this map.
            catch (InvalidOperationException error)
            {
                RecordBuildFailure(error.Message);
                return;
            }
            ClearBiomeLayers();
            _water?.Clear();
            display.Position = Vector2.Zero;
            display.ZIndex = TerrainLayers.ZFor(TerrainLayers.Ground);
            display.ZAsRelative = false;
            display.Visible = true;
            _publishedPack = LibraryPack;
            _publishedPackKey = LibraryPack.RenderKey();
            _publishedPackBounds = new Rect2I(BoundsOrigin, BoundsSize);
            LibraryCellsUpdated = BoundsSize.X * BoundsSize.Y;
            TerrainLibraryEditSession.RestoreVisuals(this, display);
            _paintDiagnostics = new() { ["valid"] = true, ["requested"] = BoundsSize.X * BoundsSize.Y };
        }

        /// <summary>Records why a build did not publish; what is on screen is left as it was.</summary>
        private void RecordBuildFailure(string reason)
        {
            _paintDiagnostics = new() { ["valid"] = false, ["reason"] = reason };
            GD.PushWarning($"[{Name}] {reason}");
        }

        protected override void PerformQueuedRebuild()
        {
            if (TerrainLibraryEditSession.Blocks(this)) return;
            var display = GetNodeOrNull<TileMapLayer>("LibraryTerrain");
            if (LibraryPack is null || _publishedPack != LibraryPack || _libraryDirty.Count == 0
                || _publishedPackKey != LibraryPack.RenderKey() || _publishedPackBounds != new Rect2I(BoundsOrigin, BoundsSize)
                || _cells is null || display is null || display.TileSet != LibraryPack.Tiles)
            {
                Rebuild();
                return;
            }
            string problem = LibraryPack.Validate(TerrainProjection.Tiles);
            if (problem.Length == 0)
            {
                try
                {
                    LibraryCellsUpdated = TerrainLibraryPainter.Update(display, LibraryPack, new Rect2I(BoundsOrigin, BoundsSize),
                        _libraryDirty, cell => GridCellRules.TerrainKindAt(_cells, cell), cell => LibraryPack.ElevationAt(_cells, cell));
                    TerrainLibraryEditSession.RestoreVisuals(this, display);
                }
                // The painter's failure for a pack that cannot draw these cells.
                catch (InvalidOperationException error) { problem = error.Message; }
            }
            _libraryDirty.Clear();
            if (problem.Length > 0)
            {
                _publishedPack = null;
                RecordBuildFailure(problem);
            }
        }

        private void ApplyGroundDetail(TerrainTransitionLayerComponent layer)
        {
            var display = (TileMapLayer)layer.GetParent();
            if (layer.RenderFilledBase || !GroundTexturePaths.TryGetValue(layer.TransitionTerrainKind, out string? path)
                || string.IsNullOrWhiteSpace(path))
            {
                display.Material = null;
                return;
            }
            Texture2D? texture = TerrainTextures.Load(path, Name, "repeating tile ground detail");
            if (texture is null) { display.Material = null; return; }
            var material = display.Material as ShaderMaterial ?? new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://addons/beep_game_builder_cs/shaders/terrain_tile_detail.gdshader")
            };
            material.SetShaderParameter("ground_texture", texture);
            material.SetShaderParameter("cell_size", new Vector2(AtlasTileSize.X, AtlasTileSize.Y));
            material.SetShaderParameter("repeat_cells", Mathf.Clamp(GroundRepeatCells, 1f, 32f));
            material.SetShaderParameter("detail_strength", Mathf.Clamp(GroundDetailStrength, 0f, 1f));
            display.Material = material;
        }

        /// <summary>
        /// Resolve the current generator path, including replacement of a still-live node.
        /// </summary>
        private void ResolveGenerator()
        {
            _generator = TerrainGeneratorPath.IsEmpty ? null
                : GetNodeOrNull<Node>(TerrainGeneratorPath) as TerrainGeneratorComponent;
        }

        private void ClearSurface()
        {
            ClearBiomeLayers();
            _water?.Clear();
            _sea.ForgetCoast();
        }

        private void ClearBiomeLayers()
        {
            // Metadata survives PackedScene serialization; the runtime component list does not.
            foreach (Node child in GetChildren())
                if (child is TileMapLayer display && display.GetMeta(BiomeDisplayMetadata, false).AsBool())
                {
                    RemoveChild(display);
                    display.QueueFree();
                }
            _layers.Clear();
            _builtSignature = string.Empty;
        }

        /// <summary>
        /// Lays the shader sea over the water tiles.
        ///
        /// The tiles stay: they are the bed, the same job the seabed layers do
        /// in the isometric view, and the surface is transparent in the shallows
        /// so they show through it.
        ///
        /// The quad covers the whole map because the shader decides for itself
        /// what is water - and drawn top-down it stops at the shore, where drawn
        /// isometrically it deliberately runs on under the land. Same field,
        /// same shoreline; only the compositing differs.
        /// </summary>
        private void EnsureWaterSurface()
        {
            if (string.IsNullOrWhiteSpace(WaterShaderPath))
            {
                _water?.Clear();
                return;
            }

            ResolveGenerator();
            ResolveCells();
            if ((!CellDataPath.IsEmpty && _cells is null) || (_cells is null && _generator is null))
            {
                ClearSurface();
                GD.PushWarning(
                    $"[{Name}] no generator at TerrainGeneratorPath, so the sea has no coastline to read; "
                    + "the water tiles will draw on their own.");
                return;
            }

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            _sea.ResolveCoast(_cells, _generator, BoundsOrigin, size, CoastDetail, CoastRangeTiles);

            Vector2I cell = new(Mathf.Max(1, AtlasTileSize.X), Mathf.Max(1, AtlasTileSize.Y));
            _water = _sea.TileBatched(this, "TileWater", cell, size,
                new Vector2(BoundsOrigin.X * cell.X, BoundsOrigin.Y * cell.Y), isometric: false);

            // TOP-DOWN, batched tiles: the two things that differ from the block view's quad.
            _water.Material = _sea.BuildMaterial(
                this, WaterShaderPath, WaterLook ?? TerrainWaterLook.Shared,
                new TerrainSeaSurface.Sheet(MaxOpacity, ClarityTiles, LakeOpacity, ShoreOpacity),
                BoundsOrigin, size, cell, CoastRangeTiles, flatProjection: true, tileBatch: true);
        }

        /// <summary>
        /// Paint order, taken from the SHARED stack in TerrainLayers: sea, then
        /// ground, then hills, then mountains.
        ///
        /// This view used to order its layers by biome alone and draw water
        /// LAST, so the sea's transition tiles resolved the coastline by
        /// covering the land at the shore - which meant they covered the BEACH.
        /// The sand layer was there, 436 tiles of it, drawn and then buried
        /// under deep water. A view that stacks its own way will eventually
        /// contradict the others, and this is what that looks like.
        ///
        /// Water below, land above: the coast is now resolved by the LAND's
        /// transition tiles meeting the sea, which is what a beach is.
        /// </summary>
        private List<BiomeLayer> ConfiguredLayers()
        {
            // Declaration order is DRAW order within a level. TerrainLayers
            // decides which level each kind lands on; this list only decides who
            // draws over whom among equals - which is why the beach comes last
            // of the ground biomes, so it meets the sea rather than being buried
            // by the biome behind it.
            var candidates = new List<BiomeLayer>
            {
                // SEA, beneath everything.
                new("deep_water", WaterAtlasPath, WaterDetailAtlasPath),
                new("shallow_water", WaterAtlasPath, WaterDetailAtlasPath),
                new("water", WaterAtlasPath, WaterDetailAtlasPath),
                new("sea", WaterAtlasPath, WaterDetailAtlasPath),
                new("ocean", WaterAtlasPath, WaterDetailAtlasPath),

                // GROUND.
                new("swamp", SwampAtlasPath, string.Empty),
                new("jungle", JungleAtlasPath, string.Empty),
                new("grass", GrassAtlasPath, GrassDetailAtlasPath),
                new("dry_grass", DryGrassAtlasPath, string.Empty),
                new("desert", DesertAtlasPath, DesertDetailAtlasPath),
                new("tundra", TundraAtlasPath, string.Empty),
                new("snow", SnowAtlasPath, string.Empty),
                new("ice", IceAtlasPath, string.Empty),
                new("sand", SandAtlasPath, string.Empty),

                // HILLS and MOUNTAINS, which TerrainLayers raises above the
                // ground they rise from.
                new("gravel", GravelAtlasPath, string.Empty),
                new("rock", RockAtlasPath, string.Empty),
            };

            // WHICH of these the map actually needs is the ENGINE's answer, not
            // this list's. The list above says what art exists and in what order
            // it draws; the generator says what the world contains. Building a
            // layer for a biome the map has none of made the layer count a
            // property of the renderer's configuration rather than of the world,
            // so two views of one map could not be compared.
            ResolveGenerator();
            // Keep every authored live biome ready: an edit may introduce a kind absent at generation.
            Godot.Collections.Array<string>? present = CellDataPath.IsEmpty ? _generator?.TerrainKindsPresent() : null;

            var configured = new List<BiomeLayer>();
            foreach (BiomeLayer candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.AtlasPath))
                    continue;
                if (present is not null && !present.Contains(candidate.TerrainKind))
                    continue;
                configured.Add(candidate);
            }
            return configured;
        }

        private void EnsureLayers()
        {
            List<BiomeLayer> configured = ConfiguredLayers();

            // Reuse the layers only when the configuration behind them has not
            // changed. Comparing the COUNT alone was wrong twice over: _layers
            // holds the filled base as well, so with a base atlas set the counts
            // could never match and every rebuild tore down and recreated every
            // layer; and swapping one atlas path for another keeps the count
            // identical while changing what must be drawn. A reused layer cannot
            // pick a new atlas up - its TileSet is built once and kept - so a
            // changed configuration has to rebuild.
            string signature = Signature(configured);
            if (signature == _builtSignature && AllValid())
            {
                Configure();
                return;
            }
            // Names are not ownership: authored roads/details may also end in "Tiles".
            ClearBiomeLayers();
            _builtSignature = signature;

            // A filled base at the floor of the stack, so a gap between biome
            // layers shows water rather than a hole.
            //
            // No z is passed to CreateLayer any more. The transition component
            // on each layer places its own node from the terrain kind it paints,
            // so this renderer setting one too would be a second write of the
            // same decision - agreeing today and diverging the first time either
            // side is edited. Layers sharing a level are ordered among
            // themselves by creation order, which is the order declared above.
            if (!string.IsNullOrWhiteSpace(BaseAtlasPath))
                _layers.Add(CreateLayer("Base", "grass", BaseAtlasPath, string.Empty, filledBase: true));

            foreach (BiomeLayer layer in configured)
            {
                _layers.Add(CreateLayer(
                    NodeNameFor(layer.TerrainKind),
                    layer.TerrainKind,
                    layer.AtlasPath,
                    layer.DetailAtlasPath,
                    filledBase: false));
            }
        }

        private bool AllValid()
        {
            foreach (TerrainTransitionLayerComponent layer in _layers)
            {
                if (!GodotObject.IsInstanceValid(layer))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// What the current exports would build. Two runs with the same
        /// signature produce the same layers, so the existing ones can be kept
        /// and simply repainted.
        /// </summary>
        private string Signature(List<BiomeLayer> configured)
        {
            var text = new System.Text.StringBuilder();
            text.Append(BaseAtlasPath).Append('|')
                .Append(CellDataPath).Append('|').Append(TerrainGeneratorPath).Append('|')
                .Append(_cells?.GetInstanceId() ?? _generator?.GetInstanceId() ?? 0).Append('|')
                .Append(AtlasTileSize).Append('|')
                .Append(AtlasColumns).Append('x').Append(AtlasTileRows);
            foreach (BiomeLayer layer in configured)
            {
                text.Append('|').Append(layer.TerrainKind)
                    .Append('=').Append(layer.AtlasPath)
                    .Append('+').Append(layer.DetailAtlasPath);
            }
            return text.ToString();
        }

        /// <summary>
        /// Re-applies what a KEPT layer can actually act on.
        ///
        /// The atlas is deliberately not re-assigned: a layer's TileSet is built
        /// once and kept, so writing a new path here would look like
        /// configuration and change nothing - the failure this method used to
        /// embody. The z index is not set here either, and for the opposite
        /// reason: the transition component re-places its own layer on every
        /// refresh, so it is already correct.
        /// </summary>
        private void Configure()
        {
            foreach (TerrainTransitionLayerComponent layer in _layers)
            {
                layer.BoundsOrigin = BoundsOrigin;
                layer.BoundsSize = BoundsSize;
            }
        }

        private TerrainTransitionLayerComponent CreateLayer(
            string name,
            string terrainKind,
            string atlasPath,
            string detailAtlasPath,
            bool filledBase)
        {
            // A dual-grid renderer paints one MORE row and column than the map
            // has, because each tile straddles the corner between four cells.
            // Shifting the layer back half a tile is what lines that grid up
            // with the cells; without it the extra ring shows as a border frame
            // around the whole map.
            //
            // Neither the z index nor the texture filter is set here: the
            // transition component below places the layer from the terrain kind
            // it paints, and sets the filter to match the atlas it just built
            // with a mip chain. Both used to be written twice.
            // Do not take ownership of an authored layer with the requested display name.
            var display = new TileMapLayer { Name = $"{name}Tiles" };
            display.SetMeta(BiomeDisplayMetadata, true);
            AddChild(display, forceReadableName: true);
            TerrainAuthoring.Adopt(display, this);
            display.Position = new Vector2(AtlasTileSize.X * -0.5f, AtlasTileSize.Y * -0.5f);

            var component = new TerrainTransitionLayerComponent
            {
                Name = $"{name}Transitions",
                BoundsOrigin = BoundsOrigin,
                BoundsSize = BoundsSize,
                TransitionTerrainKind = terrainKind,
                MatchTerrainAliases = false,
                RenderFilledBase = filledBase,
                // The atlases here are hand-authored 15-piece sheets, not Godot
                // TileSet terrain sets, so connection selection uses the
                // canonical 15-piece layout rather than TileSet terrains.
                UseTileSetTerrains = false,
                UseCanonical15PieceLayout = true,
                AtlasTexturePath = atlasPath,
                BuildTileSetFromAtlasPath = true,
                AtlasTileSize = AtlasTileSize,
                AtlasColumns = AtlasColumns,
                AtlasTileRows = AtlasTileRows,
                RefreshOnReady = false,
            };

            display.AddChild(component);
            TerrainAuthoring.Adopt(component, this);

            // Paths must be assigned AFTER the node is in the tree and relative
            // to the component itself: it is a grandchild of this renderer, so a
            // path computed from the renderer does not resolve from there.
            // Live cells own edited maps; generated fields are for recipe-only previews.
            Node? source = CellDataPath.IsEmpty
                ? (TerrainGeneratorPath.IsEmpty ? null : GetNodeOrNull(TerrainGeneratorPath))
                : GetNodeOrNull(CellDataPath);
            if (source is not null)
            {
                if (CellDataPath.IsEmpty) component.TerrainGeneratorPath = component.GetPathTo(source);
                else component.CellDataPath = component.GetPathTo(source);
            }
            component.DisplayLayerPath = component.GetPathTo(display);

            if (!string.IsNullOrWhiteSpace(detailAtlasPath))
            {
                component.DetailAtlasTexturePath = detailAtlasPath;
                component.BuildDetailTileSetFromAtlasPath = true;
                component.DetailSourceId = 1;
                component.DetailDisplayLayerPath = component.GetPathTo(display);
            }

            return component;
        }

        private static string NodeNameFor(string terrainKind)
        {
            Span<char> buffer = stackalloc char[terrainKind.Length];
            bool upper = true;
            int length = 0;
            foreach (char character in terrainKind)
            {
                if (character == '_')
                {
                    upper = true;
                    continue;
                }
                buffer[length++] = upper ? char.ToUpperInvariant(character) : character;
                upper = false;
            }
            return new string(buffer[..length]);
        }
    }
}
