using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Renders terrain as one continuous surface, blended in a shader, rather
    /// than as one sprite per tile.
    ///
    /// This follows how Factorio draws ground (FFF-214): a large seamless
    /// material texture is sampled in WORLD space and terrain shapes are cut out
    /// of it, so a field of grass is one continuous texture instead of the same
    /// stamp repeated. Transitions are blended at runtime rather than being
    /// prerendered per terrain pair, which is what keeps the art requirement to
    /// one texture per terrain instead of one sprite per pairing.
    ///
    /// A fragment shader cannot read a TileMapLayer, so the terrain grid is
    /// uploaded as a texture: one texel per tile, terrain id in red and
    /// hillshade in green. Neighbour lookups in the shader are then samples at
    /// one texel offset, which is what makes edge blending possible.
    ///
    /// The gameplay grid is untouched - this draws what the generator already
    /// decided, and TileMapLayer still owns features, objects and collision.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridSplatTerrainRendererComponent : Node2D
    {
        /// <summary>
        /// Terrain kind to shader id. The shader indexes materials by this, so
        /// the order is part of the contract with terrain_splat.gdshader.
        /// </summary>
        private static readonly Dictionary<string, int> TerrainIds = new()
        {
            ["grass"] = 0,
            ["dry_grass"] = 1,
            ["desert"] = 2,
            ["sand"] = 3,
            ["tundra"] = 4,
            ["snow"] = 5,
            ["ice"] = 6,
            ["jungle"] = 7,
            ["swamp"] = 8,
            ["mud"] = 8,
            ["gravel"] = 9,
            ["rock"] = 10,
            ["shallow_water"] = 11,
            ["deep_water"] = 12,
        };

        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(96, 60);
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;

        [ExportGroup("Look")]
        [Export(PropertyHint.Range, "1,32,0.5")] public float TextureTiles { get; set; } = 6.0f;
        [Export(PropertyHint.Range, "0,0.9,0.01")] public float BlendWidth { get; set; } = 0.42f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float EdgeNoise { get; set; } = 0.55f;
        [Export(PropertyHint.Range, "0.5,24,0.5")] public float NoiseScale { get; set; } = 5.0f;
        [Export(PropertyHint.Range, "0,2,0.05")] public float ShadeStrength { get; set; } = 1.0f;
        /// <summary>How many tiles of coast distance the shader can see.</summary>
        [Export(PropertyHint.Range, "1,16,0.5")] public float CoastRangeTiles { get; set; } = 5.0f;
        /// <summary>
        /// Sub-tile resolution of the coast distance field. At 1 the field has
        /// one value per tile and its interpolated contours are square, so surf
        /// drawn along them looks like survey lines rather than waves.
        /// </summary>
        [Export(PropertyHint.Range, "1,8,1")] public int CoastDetail { get; set; } = 4;

        [Export] public int RenderZIndex { get; set; } = -95;

        [ExportGroup("Surf")]
        /// <summary>
        /// A strip of equal frames of authored foam, sampled by distance from the
        /// waterline rather than stamped per tile, so the coastline stays the
        /// smooth one the distance field describes.
        ///
        /// It needs a SOFT fringe - foam fading out at its edges. A flat cutout
        /// silhouette carries no falloff to read and collapses to one solid band.
        /// Leave empty for generated crests.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string FoamSheetPath { get; set; } = "";
        /// <summary>
        /// How heavy the sea is: 0 a millpond, 1 an ordinary day, 2 a storm.
        ///
        /// One dial rather than several, because big waves are not just brighter
        /// foam - the surf reaches further out, the crests broaden, and the wash
        /// runs further up the sand. Those move together or the result reads as
        /// small waves turned up.
        /// </summary>
        [Export(PropertyHint.Range, "0,2,0.05")] public float WaveIntensity { get; set; } = 1.0f;

        /// <summary>Tiles covered by one repeat of the foam texture ALONG the shore.</summary>
        [Export(PropertyHint.Range, "1,48,0.5")] public float FoamTilesAlong { get; set; } = 11.0f;

        /// <summary>
        /// Tiles covered by one repeat ACROSS the shore. Short: the surf band is
        /// about a tile deep, so scaling both axes alike leaves one stretched
        /// ribbon rather than crests sitting inside the band.
        /// </summary>
        [Export(PropertyHint.Range, "0.3,8,0.1")] public float FoamTilesAcross { get; set; } = 7.0f;

        /// <summary>How fast the authored crests advance onto the beach.</summary>
        [Export(PropertyHint.Range, "0,4,0.01")] public float FoamScroll { get; set; } = 0.055f;

        /// <summary>How strongly the surf pulses as crests arrive, 0 for a steady band.</summary>
        [Export(PropertyHint.Range, "0,1,0.05")] public float FoamPulse { get; set; } = 0.34f;

        /// <summary>How fast arriving crests follow one another.</summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float FoamArrivalRate { get; set; } = 0.9f;


        [ExportGroup("Material Textures")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string GrassTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DryGrassTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SandTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DirtTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SnowTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string MudTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string GravelTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string RockTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string ShallowWaterTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DeepWaterTexturePath { get; set; } = "";

        private const string ShaderPath = "res://addons/beep_game_builder_cs/shaders/terrain_splat.gdshader";

        private GridTerrainGeneratorComponent? _generator;
        private Sprite2D? _surface;
        private ShaderMaterial? _material;



        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild,
        /// so the map is not built twice.
        /// </summary>
        [Export] public bool RefreshOnReady { get; set; } = true;

        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a GridTerrainGeneratorComponent." }
                : System.Array.Empty<string>();

        /// <summary>Re-uploads the terrain grid and repaints the surface.</summary>
        public void Rebuild()
        {
            ResolveGenerator();
            if (_generator is null)
                return;

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            ImageTexture idMap = BuildIdMap(size, out ImageTexture shadeMap, out ImageTexture coastMap);

            EnsureSurface(size);
            if (_material is null)
                return;

            _material.SetShaderParameter("id_map", idMap);
            _material.SetShaderParameter("shade_map", shadeMap);
            _material.SetShaderParameter("coast_map", coastMap);
            _material.SetShaderParameter("coast_range", CoastRangeTiles);
            _material.SetShaderParameter("map_size", new Vector2(size.X, size.Y));
            _material.SetShaderParameter("texture_tiles", Mathf.Max(1.0f, TextureTiles));
            _material.SetShaderParameter("blend_width", BlendWidth);
            _material.SetShaderParameter("edge_noise", EdgeNoise);
            _material.SetShaderParameter("noise_scale", NoiseScale);
            _material.SetShaderParameter("shade_strength", ShadeStrength);

            Texture2D? foam = string.IsNullOrWhiteSpace(FoamSheetPath) ? null : LoadTexture(FoamSheetPath);
            if (foam is null && !string.IsNullOrWhiteSpace(FoamSheetPath))
                GD.PushWarning($"[{Name}] could not load foam sheet '{FoamSheetPath}'; using generated crests.");
            _material.SetShaderParameter("use_foam_sheet", foam is not null);
            if (foam is not null)
                _material.SetShaderParameter("foam_sheet", foam);
            _material.SetShaderParameter("wave_intensity", Mathf.Clamp(WaveIntensity, 0.0f, 2.0f));
            _material.SetShaderParameter("foam_tiles_along", Mathf.Max(1.0f, FoamTilesAlong));
            _material.SetShaderParameter("foam_tiles_across", Mathf.Max(0.3f, FoamTilesAcross));
            _material.SetShaderParameter("foam_scroll", Mathf.Max(0.0f, FoamScroll));
            _material.SetShaderParameter("foam_pulse", Mathf.Clamp(FoamPulse, 0.0f, 1.0f));
            _material.SetShaderParameter("foam_arrival_rate", Mathf.Max(0.0f, FoamArrivalRate));
        }

        /// <summary>
        /// One texel per tile: red is the terrain id, green the hillshade. This
        /// is the only way the shader can know what its neighbours are.
        /// </summary>
        private ImageTexture BuildIdMap(Vector2I size, out ImageTexture shadeMap, out ImageTexture coastMap)
        {
            var image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
            // Shade lives in its own image so it can be sampled with linear
            // filtering while ids stay nearest.
            var shade = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    string kind = _generator!.TerrainKindAt(cell);
                    int id = TerrainIds.TryGetValue(kind, out int mapped) ? mapped : 0;

                    // Shade is 0.7..1.3 from the generator; halved so it fits a
                    // colour channel, and doubled again in the shader.
                    float lit = Mathf.Clamp(_generator.ShadeAtCell(cell) * 0.5f, 0.0f, 1.0f);
                    image.SetPixel(x, y, new Color(id / 255.0f, lit, 0.0f, 1.0f));
                    shade.SetPixel(x, y, new Color(lit, lit, lit, 1.0f));
                }
            }
            shadeMap = ImageTexture.CreateFromImage(shade);
            coastMap = BuildCoastMap(size);
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>
        /// Signed distance to the coastline, in tiles: positive out to sea,
        /// negative inland, zero at the waterline.
        ///
        /// The shader needs this because a weighted land/water FRACTION ramps
        /// from 0 to 1 across barely a pixel, so beach and surf bands expressed
        /// in it collapse to nothing. A real distance lets a band be specified
        /// in tiles and actually be that wide on screen.
        /// </summary>
        private ImageTexture BuildCoastMap(Vector2I size)
        {
            // Sampled finer than the tile grid: the generator knows where water
            // is at sub-tile resolution, and using that is what keeps the
            // contours curved instead of following bilinear tile patches.
            int detail = Mathf.Clamp(CoastDetail, 1, 8);
            var fine = new Vector2I(size.X * detail, size.Y * detail);
            int count = fine.X * fine.Y;

            var water = new bool[count];
            for (int y = 0; y < fine.Y; y++)
            {
                for (int x = 0; x < fine.X; x++)
                {
                    var at = new Vector2((x + 0.5f) / detail, (y + 0.5f) / detail);
                    water[(y * fine.X) + x] = _generator!.IsWaterAtPosition(at);
                }
            }

            int[] toWater = Distance(water, fine, seedOnWater: true);
            int[] toLand = Distance(water, fine, seedOnWater: false);

            // Which water is OPEN SEA. Surf belongs to a coast with a fetch
            // behind it; a lake or a river has no swell running onto it, and
            // drawing breakers around a pond reads as wrong immediately.
            bool[] ocean = OceanCells(size);

            float range = Mathf.Max(1.0f, CoastRangeTiles);
            var image = Image.CreateEmpty(fine.X, fine.Y, false, Image.Format.Rgba8);
            for (int y = 0; y < fine.Y; y++)
            {
                for (int x = 0; x < fine.X; x++)
                {
                    int index = (y * fine.X) + x;
                    // Distances come back in chamfer units of a sub-cell; convert
                    // to tiles.
                    float signed = (water[index] ? toLand[index] : -toWater[index])
                        / (float)(detail * ChamferStep);
                    float encoded = Mathf.Clamp((signed / range * 0.5f) + 0.5f, 0.0f, 1.0f);
                    // Green carries whether this is open sea, so the shader can
                    // put surf on a coast and leave lakes still.
                    float open = ocean[((y / detail) * size.X) + (x / detail)] ? 1.0f : 0.0f;
                    image.SetPixel(x, y, new Color(encoded, open, encoded, 1.0f));
                }
            }
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>
        /// Cells that are open sea, grown by one cell.
        ///
        /// The growth matters: the coast map is sampled below the tile grid and
        /// filtered linearly, so without it the land-side fringe of a coastal
        /// tile reads as "not sea" and the surf is cut off exactly where it
        /// should be strongest.
        /// </summary>
        private bool[] OceanCells(Vector2I size)
        {
            GeneratedTerrainField field = _generator!.ResolveField();
            int count = size.X * size.Y;
            var seed = new bool[count];
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                    seed[(y * size.X) + x] = field.WaterSourceAtCell(new Vector2I(x, y)) == "ocean";
            }

            var grown = new bool[count];
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !near; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= size.X || ny >= size.Y)
                                continue;
                            near = seed[(ny * size.X) + nx];
                        }
                    }
                    grown[(y * size.X) + x] = near;
                }
            }
            return grown;
        }

        /// <summary>
        /// One orthogonal step, in the units <see cref="Distance"/> returns.
        /// Distances come back scaled by this so the chamfer weights can be
        /// whole numbers.
        /// </summary>
        private const int ChamferStep = 3;

        /// <summary>
        /// Chamfer distance to the nearest seed, scaled by <see cref="ChamferStep"/>.
        ///
        /// A four-neighbour flood measures MANHATTAN distance, whose contours are
        /// diamonds. That is invisible in a band a fraction of a tile wide, but
        /// the shallow-water shelf is a couple of tiles deep and drew those
        /// diamonds as angular polygons out in the sea. Weighting diagonal steps
        /// at 4 against 3 puts the contours within a few percent of circular,
        /// for two sweeps and no queue.
        /// </summary>
        private static int[] Distance(bool[] water, Vector2I size, bool seedOnWater)
        {
            const int diagonal = 4;
            // Headroom so a saturated cell plus one step cannot overflow.
            int far = int.MaxValue - (diagonal * 2);

            var distance = new int[water.Length];
            for (int index = 0; index < water.Length; index++)
                distance[index] = water[index] == seedOnWater ? 0 : far;

            // Forward sweep reads the half of the neighbourhood already settled.
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    int index = (y * size.X) + x;
                    int best = distance[index];
                    if (x > 0)
                        best = Math.Min(best, distance[index - 1] + ChamferStep);
                    if (y > 0)
                    {
                        best = Math.Min(best, distance[index - size.X] + ChamferStep);
                        if (x > 0)
                            best = Math.Min(best, distance[index - size.X - 1] + diagonal);
                        if (x < size.X - 1)
                            best = Math.Min(best, distance[index - size.X + 1] + diagonal);
                    }
                    distance[index] = best;
                }
            }

            // Backward sweep covers the other half.
            for (int y = size.Y - 1; y >= 0; y--)
            {
                for (int x = size.X - 1; x >= 0; x--)
                {
                    int index = (y * size.X) + x;
                    int best = distance[index];
                    if (x < size.X - 1)
                        best = Math.Min(best, distance[index + 1] + ChamferStep);
                    if (y < size.Y - 1)
                    {
                        best = Math.Min(best, distance[index + size.X] + ChamferStep);
                        if (x > 0)
                            best = Math.Min(best, distance[index + size.X - 1] + diagonal);
                        if (x < size.X - 1)
                            best = Math.Min(best, distance[index + size.X + 1] + diagonal);
                    }
                    distance[index] = best;
                }
            }
            return distance;
        }

        private void EnsureSurface(Vector2I size)
        {
            _surface ??= GetNodeOrNull<Sprite2D>("SplatSurface");
            if (_surface is null || !GodotObject.IsInstanceValid(_surface))
            {
                _surface = new Sprite2D { Name = "SplatSurface", Centered = false };
                AddChild(_surface);
                if (Engine.IsEditorHint())
                    _surface.Owner = GetTree()?.EditedSceneRoot;
            }

            // A single white pixel stretched over the map: the shader paints it,
            // so the texture itself only has to provide UVs.
            if (_surface.Texture is null)
            {
                var pixel = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
                pixel.SetPixel(0, 0, Colors.White);
                _surface.Texture = ImageTexture.CreateFromImage(pixel);
            }

            int tile = Mathf.Max(1, TileSize);
            _surface.Scale = new Vector2(size.X * tile, size.Y * tile);
            _surface.ZIndex = RenderZIndex;
            _surface.TextureFilter = TextureFilterEnum.Linear;

            if (_material is null)
            {
                var shader = GD.Load<Shader>(ShaderPath);
                if (shader is null)
                {
                    GD.PushWarning($"[{Name}] terrain splat shader missing at {ShaderPath}; nothing will be drawn.");
                    return;
                }
                _material = new ShaderMaterial { Shader = shader };
                _surface.Material = _material;
                AssignMaterialTextures();
            }
        }

        private void AssignMaterialTextures()
        {
            Assign("tex_grass", GrassTexturePath);
            Assign("tex_dry_grass", DryGrassTexturePath);
            Assign("tex_sand", SandTexturePath);
            Assign("tex_dirt", DirtTexturePath);
            Assign("tex_snow", SnowTexturePath);
            Assign("tex_mud", MudTexturePath);
            Assign("tex_gravel", GravelTexturePath);
            Assign("tex_rock", RockTexturePath);
            Assign("tex_shallow", ShallowWaterTexturePath);
            Assign("tex_deep", DeepWaterTexturePath);
        }

        private void Assign(string parameter, string path)
        {
            if (_material is null || string.IsNullOrWhiteSpace(path))
                return;

            Texture2D? texture = LoadTexture(path);
            if (texture is null)
            {
                GD.PushWarning($"[{Name}] could not load terrain material texture '{path}' for {parameter}.");
                return;
            }
            _material.SetShaderParameter(parameter, texture);
        }

        private static Texture2D? LoadTexture(string path)
        {
            if (path.StartsWith("res://", System.StringComparison.Ordinal))
                return GD.Load<Texture2D>(path);

            // Absolute paths are supported so art can live outside the project,
            // which is how the existing terrain material textures are wired.
            Image image = Image.LoadFromFile(path);
            if (image.IsEmpty())
                return null;

            // The shader declares these samplers filter_linear_mipmap, but that
            // hint does nothing unless the texture actually carries a mip chain -
            // it silently falls back to plain linear and the ground aliases and
            // shimmers as soon as the map is zoomed out. Only the material
            // textures get this; the id, shade and coast maps are data and are
            // built elsewhere.
            image.GenerateMipmaps();
            return ImageTexture.CreateFromImage(image);
        }

        private void ResolveGenerator()
        {
            if (_generator is null || !GodotObject.IsInstanceValid(_generator))
                _generator = TerrainGeneratorPath.IsEmpty
                    ? null
                    : GetNodeOrNull<GridTerrainGeneratorComponent>(TerrainGeneratorPath);
        }
    }
}
