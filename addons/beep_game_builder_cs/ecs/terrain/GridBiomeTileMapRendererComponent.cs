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
    /// It builds and owns one <see cref="GridTerrainTransitionLayerComponent"/>
    /// and one TileMapLayer per configured biome, so a scene needs a single node
    /// instead of a hand-wired pair per biome.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridBiomeTileMapRendererComponent : Node2D
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

        [Export] public NodePath CellDataPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(48, 30);

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
        [Export] public bool RefreshOnReady { get; set; } = true;
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
        [Export] public NodePath GeneratorPath { get; set; } = new("");
        [Export(PropertyHint.File, "*.gdshader")] public string WaterShaderPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,24,0.5")] public float CoastRangeTiles { get; set; } = 5.0f;
        [Export(PropertyHint.Range, "1,8,1")] public int CoastDetail { get; set; } = 2;
        [Export(PropertyHint.Range, "0,1,0.01")] public float MaxOpacity { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float ShoreOpacity { get; set; } = 0.55f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float LakeOpacity { get; set; } = 0.42f;
        [Export(PropertyHint.Range, "0.1,12,0.1")] public float ClarityTiles { get; set; } = 3.0f;
        [Export(PropertyHint.Range, "0,2,0.01")] public float WaveIntensity { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0,2,0.01")] public float FoamStrength { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "1,64,0.5")] public float DeepTiles { get; set; } = 6.0f;
        [Export(PropertyHint.Range, "1,64,0.5")] public float ShallowTiles { get; set; } = 6.0f;
        [Export(PropertyHint.File, "*.png,*.webp")] public string ShallowTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DeepTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SandTexturePath { get; set; } = "";

        /// <summary>
        /// The authored surf, as the isometric sea takes. Without it the shader
        /// falls back to generated crests - which is a different sea from the
        /// one the other view draws off the same coastline.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string FoamSheetPath { get; set; } = "";

        private readonly List<GridTerrainTransitionLayerComponent> _layers = new();
        private GridTerrainGeneratorComponent? _generator;
        private Sprite2D? _water;
        private ImageTexture? _coastMap;
        private ShaderMaterial? _waterMaterial;
        /// <summary>What the current layers were built from; see Signature.</summary>
        private string _builtSignature = string.Empty;



        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to a GridCellDataComponent." };
            if (ConfiguredLayers().Count == 0)
                return new[] { "Assign at least one biome atlas, or nothing will be drawn." };
            return Array.Empty<string>();
        }

        /// <summary>Rebuilds every biome layer from the current cell data.</summary>
        public void Rebuild()
        {
            EnsureLayers();
            foreach (GridTerrainTransitionLayerComponent layer in _layers)
                layer.RefreshTransitions();

            EnsureWaterSurface();
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
                return;

            _generator ??= GetNodeOrNull<GridTerrainGeneratorComponent>(GeneratorPath);
            if (_generator is null)
            {
                GD.PushWarning(
                    $"[{Name}] no generator at GeneratorPath, so the sea has no coastline to read; "
                    + "the water tiles will draw on their own.");
                return;
            }

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            _coastMap = TerrainCoastField.Build(_generator, size, CoastDetail, CoastRangeTiles);

            _water ??= GetNodeOrNull<Sprite2D>("TileWater");
            if (_water is null || !GodotObject.IsInstanceValid(_water))
            {
                _water = new Sprite2D { Name = "TileWater" };
                AddChild(_water);
                if (Engine.IsEditorHint() && Owner is not null)
                    _water.Owner = Owner;
            }

            // One white texel stretched over the map. The shader never samples
            // it; a Sprite2D just needs something to give the quad its extent.
            if (_water.Texture is null)
            {
                Image dot = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
                dot.SetPixel(0, 0, Colors.White);
                _water.Texture = ImageTexture.CreateFromImage(dot);
            }

            _water.Centered = false;
            _water.Position = Vector2.Zero;
            _water.Scale = new Vector2(size.X * AtlasTileSize.X, size.Y * AtlasTileSize.Y);

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
                material.SetShaderParameter("coast_map", _coastMap);
            }

            if (_water is not null)
            {
                material.SetShaderParameter("quad_origin", _water.Position);
                material.SetShaderParameter("quad_size", _water.Scale);
            }

            material.SetShaderParameter("coast_range", CoastRangeTiles);
            material.SetShaderParameter("map_size", new Vector2(size.X, size.Y));
            material.SetShaderParameter(
                "cell_size", new Vector2(AtlasTileSize.X, AtlasTileSize.Y));
            material.SetShaderParameter("max_opacity", MaxOpacity);
            material.SetShaderParameter("clarity_tiles", ClarityTiles);
            material.SetShaderParameter("lake_opacity", LakeOpacity);
            material.SetShaderParameter("shore_opacity", ShoreOpacity);
            material.SetShaderParameter("wave_intensity", WaveIntensity);
            material.SetShaderParameter("foam_strength", FoamStrength);
            material.SetShaderParameter("deep_tiles", DeepTiles);
            material.SetShaderParameter("shallow_tiles", ShallowTiles);

            SetTexture(material, "tex_shallow", ShallowTexturePath);
            SetTexture(material, "tex_deep", DeepTexturePath);
            SetTexture(material, "tex_sand", SandTexturePath);
            // Only take the authored-surf path if the art actually loaded;
            // turning it on without the sheet draws no surf at all.
            material.SetShaderParameter(
                "use_foam_sheet", SetTexture(material, "foam_sheet", FoamSheetPath));
            return material;
        }

        /// <summary>
        /// Assigns a water texture, reporting whether the art actually loaded.
        /// </summary>
        private bool SetTexture(ShaderMaterial material, string parameter, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            // Through the shared loader. A bare GD.Load here could only ever
            // resolve res:// paths, so the same absolute path that works for the
            // isometric sea failed silently for this one.
            Texture2D? texture = TerrainTextures.Load(path, Name, $"the {parameter} material");
            if (texture is null)
                return false;

            material.SetShaderParameter(parameter, texture);
            return true;
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

            var configured = new List<BiomeLayer>();
            foreach (BiomeLayer candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate.AtlasPath))
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
            _builtSignature = signature;

            foreach (Node child in GetChildren())
                child.QueueFree();
            _layers.Clear();

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
            foreach (GridTerrainTransitionLayerComponent layer in _layers)
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
            foreach (GridTerrainTransitionLayerComponent layer in _layers)
            {
                layer.BoundsOrigin = BoundsOrigin;
                layer.BoundsSize = BoundsSize;
            }
        }

        private GridTerrainTransitionLayerComponent CreateLayer(
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
            var display = new TileMapLayer
            {
                Name = $"{name}Tiles",
                Position = new Vector2(AtlasTileSize.X * -0.5f, AtlasTileSize.Y * -0.5f),
            };
            AddChild(display);

            var component = new GridTerrainTransitionLayerComponent
            {
                Name = $"{name}Transitions",
                BoundsOrigin = BoundsOrigin,
                BoundsSize = BoundsSize,
                TransitionTerrainKind = terrainKind,
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

            // Paths must be assigned AFTER the node is in the tree and relative
            // to the component itself: it is a grandchild of this renderer, so a
            // path computed from the renderer does not resolve from there.
            Node? cells = CellDataPath.IsEmpty ? null : GetNodeOrNull(CellDataPath);
            if (cells is not null)
                component.CellDataPath = component.GetPathTo(cells);
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
