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
        // Ranges match water_common.gdshaderinc's own hint_range, because
        // TerrainWaterMaterial now holds these values to it. They used to be wider
        // here than the shader accepts - DeepTiles ran to 64 against a shader
        // declaring 0.5 to 12 - so the Inspector offered numbers that would now be
        // silently clamped. No scene in the tree authors one; narrowing the slider
        // is what keeps that true.
        //
        // The DEFAULTS below are still this view's own and differ from the
        // isometric sea's (0.50 / 4.5 / 1.8). That is a real divergence - one map
        // drawn twice with two different seas - but changing a default changes how
        // terrain_tilemap_demo.tscn looks, which is an appearance decision rather
        // than a consolidation, so it is left to the owner.
        [Export(PropertyHint.Range, "0,2,0.01")] public float WaveIntensity { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float FoamStrength { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0.5,12,0.1")] public float DeepTiles { get; set; } = 6.0f;
        [Export(PropertyHint.Range, "0,8,0.1")] public float ShallowTiles { get; set; } = 6.0f;
        /// <summary>Tiles per sandy-seabed texture repeat, under the shallows.</summary>
        [Export(PropertyHint.Range, "1,32,0.5")] public float GroundTextureTiles { get; set; } = 12.0f;
        /// <summary>Tiles per animated water-texture repeat.</summary>
        [Export(PropertyHint.Range, "1,32,0.5")] public float WaterTextureTiles { get; set; } = 6.0f;
        [Export(PropertyHint.File, "*.png,*.webp")] public string ShallowTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DeepTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SandTexturePath { get; set; } = "";

        /// <summary>
        /// The authored surf, as the isometric sea takes. Without it the shader
        /// falls back to generated crests - which is a different sea from the
        /// one the other view draws off the same coastline.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string FoamSheetPath { get; set; } = "";

        // The dials that make the sheet above behave. This view set use_foam_sheet
        // from FoamSheetPath and then set NONE of these, so an authored sheet ran on
        // whatever the shader defaulted to while the isometric view of the same map
        // ran the values its author tuned. The defaults here are the shader's own, so
        // adding them changes nothing that draws today and gives this view the dial.
        /// <summary>Tiles covered by one repeat of the foam texture ALONG the shore.</summary>
        [Export(PropertyHint.Range, "1,48,0.5")] public float FoamTilesAlong { get; set; } = 11.0f;
        /// <summary>Tiles covered by one repeat ACROSS the shore - short on purpose; see the painted renderer.</summary>
        [Export(PropertyHint.Range, "0.3,8,0.1")] public float FoamTilesAcross { get; set; } = 1.6f;
        /// <summary>How fast the authored crests advance onto the beach.</summary>
        [Export(PropertyHint.Range, "0,4,0.01")] public float FoamScroll { get; set; } = 0.055f;
        /// <summary>How strongly the surf pulses as crests arrive, 0 for a steady band.</summary>
        [Export(PropertyHint.Range, "0,1,0.05")] public float FoamPulse { get; set; } = 0.34f;
        /// <summary>How fast arriving crests follow one another.</summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float FoamArrivalRate { get; set; } = 0.9f;
        /// <summary>Direction the swell travels, in degrees, y-down screen space.</summary>
        [Export(PropertyHint.Range, "0,360,1")] public float SwellDirectionDegrees { get; set; } = 210.0f;
        /// <summary>How strongly surf favours coasts facing the swell. 0 puts surf on every shore alike.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float SwellDirectionality { get; set; } = 0.65f;

        private readonly List<TerrainTransitionLayerComponent> _layers = new();
        private const string BiomeDisplayMetadata = "_terrain_tile_biome_display";
        private TerrainGeneratorComponent? _generator;
        private GridCellDataComponent? _cells;
        private bool _coastQueued;
        private TileMapLayer? _water;
        private ImageTexture? _coastMap;
        private readonly TerrainCoastField.RenderCache _renderCoast = new();
        private readonly TerrainCoastField.LiveCache _liveCoast = new();
        private ShaderMaterial? _waterMaterial;
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
            if (((TerrainChangeKind)kind & (TerrainChangeKind.Terrain | TerrainChangeKind.Navigation)) != 0) QueueCoast();
        }

        // A content change requeues only the coast (not the whole tile map); a
        // residency move is ignored.
        protected override void OnCellsChangedSignal(int kind, Godot.Collections.Array<Vector2I> chunks)
        {
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
            if (TerrainGeneratorPath.IsEmpty && CellDataPath.IsEmpty)
                return new[] { "TerrainGeneratorPath should point to the TerrainGeneratorComponent this view draws." };
            if (ConfiguredLayers().Count == 0)
                return new[] { "Assign at least one biome atlas, or nothing will be drawn." };
            return Array.Empty<string>();
        }

        /// <summary>Rebuilds every biome layer from the current cell data.</summary>
        public override void Rebuild()
        {
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
                GD.PushWarning($"[{Name}] no generator at TerrainGeneratorPath; no terrain tiles were drawn.");
                return;
            }
            if (_cells is null && _generator is not null)
                TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            EnsureLayers();

            // The editor already warns that an unconfigured renderer draws
            // nothing, but a scene loaded at runtime never sees that. Without it
            // an empty biome list is indistinguishable from an empty world.
            if (_layers.Count == 0)
            {
                GD.PushWarning(
                    $"[{Name}] no biome atlas is configured, so no terrain tiles were drawn.");
            }

            foreach (TerrainTransitionLayerComponent layer in _layers)
            {
                layer.RefreshTransitions();
                ApplyGroundDetail(layer);
            }

            EnsureWaterSurface();
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
            _coastMap = null;
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
            _coastMap = _cells is not null
                ? _liveCoast.Resolve(_cells, BoundsOrigin, size, CoastDetail, CoastRangeTiles)
                : TerrainCoastField.Build(_generator!, size, CoastDetail, CoastRangeTiles);

            _water = TerrainAuthoring.EnsureLayer(this, "TileWater");

            // The sea is tiles like everything else. Every cell is filled, not
            // just the wet ones: the shader decides where the water stops, and
            // it needs to be able to draw the shore fade and the foam slightly
            // INLAND of the waterline. Filling only water cells would clip both
            // at a cell boundary and put a straight edge along every beach.
            Vector2I cell = new(Mathf.Max(1, AtlasTileSize.X), Mathf.Max(1, AtlasTileSize.Y));
            if (_water.TileSet is null || _water.TileSet.TileSize != cell)
                _water.TileSet = TerrainShaderSurface.BuildTileSet(cell, isometric: false);

            TerrainShaderSurface.Fill(_water, size);

            _water.Position = new Vector2(BoundsOrigin.X * cell.X, BoundsOrigin.Y * cell.Y);

            // The shared sea level: over the water tiles that are its bed, and
            // under the land. Not another biome competing for the same ground.
            _water.ZIndex = TerrainLayers.ZFor(TerrainLayers.Sea);
            _water.ZAsRelative = false;

            _waterMaterial = BuildWaterMaterial(size);
            _water.Material = _waterMaterial;
        }

        private ShaderMaterial? BuildWaterMaterial(Vector2I size)
        {
            ShaderMaterial material = _waterMaterial ?? new ShaderMaterial();
            material.SetShaderParameter("tile_batch", true);
            if (material.Shader is null)
            {
                var shader = GD.Load<Shader>(WaterShaderPath);
                if (shader is null)
                {
                    GD.PushWarning(
                        $"[{Name}] could not load water shader '{WaterShaderPath}'; there will be no sea.");
                    return null;
                }
                material.Shader = shader;
            }

            // TOP-DOWN. The one thing that differs from the isometric view.
            material.SetShaderParameter("flat_projection", 1.0f);

            if (_coastMap is null)
            {
                GD.PushWarning(
                    $"[{Name}] the coast field is missing; the sea will draw without shallows.");
            }
            else
            {
                material.SetShaderParameter("coast_map", _renderCoast.Resolve(_coastMap, BoundsSize, _liveCoast.CoastRevision));
            }

            // This surface's OWN uniforms - the ones iso_water.gdshader declares for
            // a transparent sheet of water floating over seabed. They mean nothing to
            // the painted view's opaque composite, so they stay here rather than
            // moving into the shared block below.
            material.SetShaderParameter(
                "cell_size", new Vector2(AtlasTileSize.X, AtlasTileSize.Y));
            material.SetShaderParameter("max_opacity", MaxOpacity);
            material.SetShaderParameter("clarity_tiles", ClarityTiles);
            material.SetShaderParameter("lake_opacity", LakeOpacity);
            material.SetShaderParameter("shore_opacity", ShoreOpacity);

            // Everything water_common.gdshaderinc declares, through its one writer.
            TerrainWaterMaterial.Apply(material, new TerrainWaterMaterial.Settings(
                Size: size,
                Origin: BoundsOrigin,
                CoastRange: CoastRangeTiles,
                GroundTextureTiles: GroundTextureTiles,
                WaterTextureTiles: WaterTextureTiles,
                WaveIntensity: WaveIntensity,
                FoamStrength: FoamStrength,
                ShallowTiles: ShallowTiles,
                DeepTiles: DeepTiles,
                FoamTilesAlong: FoamTilesAlong,
                FoamTilesAcross: FoamTilesAcross,
                FoamScroll: FoamScroll,
                FoamPulse: FoamPulse,
                FoamArrivalRate: FoamArrivalRate,
                SwellDirectionDegrees: SwellDirectionDegrees,
                SwellDirectionality: SwellDirectionality));

            TerrainWaterMaterial.ApplyTextures(
                material, Name, ShallowTexturePath, DeepTexturePath, SandTexturePath, FoamSheetPath);
            return material;
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
