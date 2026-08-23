using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Renders a large, filtered terrain base as one Sprite2D instead of many
    /// visible tile cells. Use this behind TileMapLayer collision/detail maps to
    /// reduce tile count and avoid a pixel-art base when the game wants painted
    /// terrain.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PainterlyTerrainComponent : WorldComponent
    {
        public enum TerrainPaintEffect
        {
            None,
            Water,
            Ice,
            Lava,
        }

        public readonly record struct PaintSample(Color Colour, TerrainPaintEffect Effect = TerrainPaintEffect.None);

        public enum TerrainMode
        {
            Plain,
            ProceduralNoise,
        }

        public enum TerrainPreset
        {
            Grassland,
            Desert,
            Sand,
            Ice,
            Sea,
            Rock,
            Lava,
            Swamp,
            Snow,
        }

        [ExportGroup("Generation")]
        [Export] public TerrainMode Mode { get; set; } = TerrainMode.ProceduralNoise;
        [Export] public TerrainPreset Preset { get; set; } = TerrainPreset.Grassland;
        [Export] public int WidthTiles { get; set; } = 96;
        [Export] public int HeightTiles { get; set; } = 64;
        [Export] public int TileSize { get; set; } = 64;
        [Export] public int PixelsPerTile { get; set; } = 8;
        [Export] public int Seed { get; set; } = 12345;
        [Export] public bool GenerateOnReady { get; set; } = true;
        [Export] public bool GenerateInEditor { get; set; } = true;

        [ExportGroup("Noise")]
        [Export] public FastNoiseLite.NoiseTypeEnum NoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.Perlin;
        [Export] public FastNoiseLite.FractalTypeEnum FractalType { get; set; } = FastNoiseLite.FractalTypeEnum.Fbm;
        [Export] public float Frequency { get; set; } = 0.012f;
        [Export] public int Octaves { get; set; } = 5;
        [Export] public float Lacunarity { get; set; } = 2.0f;
        [Export] public float Gain { get; set; } = 0.48f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float WaterLevel { get; set; } = 0.28f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float BeachWidth { get; set; } = 0.06f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float RockLevel { get; set; } = 0.82f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Dryness { get; set; } = 0.25f;

        [ExportGroup("Look")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float BlendStrength { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float GrainStrength { get; set; } = 0.10f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float HeightTintStrength { get; set; } = 0.10f;
        [Export(PropertyHint.Range, "0,3,1")] public int SmoothingPasses { get; set; } = 0;
        [Export] public bool UseBundledMaterialTextures { get; set; } = true;
        [Export(PropertyHint.Range, "0,1,0.01")] public float MaterialTextureStrength { get; set; } = 0.55f;
        [Export(PropertyHint.Range, "1,32,0.5")] public float MaterialTextureTilesPerRepeat { get; set; } = 7.0f;
        [Export] public int RenderZIndex { get; set; } = -90;
        [Export] public bool Centered { get; set; } = false;

        [ExportGroup("Water Effects")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float WaterAlpha { get; set; } = 0.78f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float ShallowWaterAlpha { get; set; } = 0.62f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float WaterFoamStrength { get; set; } = 0.22f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float WaterRippleStrength { get; set; } = 0.12f;
        [Export] public bool AnimateWater { get; set; } = true;
        [Export] public Vector2 WaterScrollSpeed { get; set; } = new(0.018f, 0.011f);

        private Sprite2D? _sprite;

        public override void _Ready()
        {
            base._Ready();

            if (!GenerateOnReady)
                return;

            if (Engine.IsEditorHint() && !GenerateInEditor)
                return;

            Rebuild();
        }

        /// <summary>Rebuild from the exported mode, preset and noise settings.</summary>
        public void Rebuild()
        {
            int width = Mathf.Max(1, WidthTiles);
            int height = Mathf.Max(1, HeightTiles);
            MaterialTextureSet? textures = UseBundledMaterialTextures ? MaterialTextureSet.Load() : null;

            if (Mode == TerrainMode.Plain)
            {
                RenderFromContinuousSampler(
                    width,
                    height,
                    at => new PaintSample(ApplyMaterialTexture(textures, Preset, 0.5f, 0.5f, at, BaseColour(Preset)), EffectFor(Preset)),
                    Mathf.Max(1, TileSize));
                return;
            }

            FastNoiseLite heightNoise = Noise(Seed, Frequency);
            FastNoiseLite moistureNoise = Noise(Seed + 9719, Frequency * 1.35f);

            RenderFromContinuousSampler(width, height, at =>
            {
                float h = Normalized(heightNoise.GetNoise2D(at.X, at.Y));
                float m = Normalized(moistureNoise.GetNoise2D(at.X, at.Y));

                TerrainPaintEffect effect = EffectFor(Preset, h);
                Color colour = ColourFor(Preset, h, m);
                colour = h >= 0.5f
                    ? colour.Lightened((h - 0.5f) * HeightTintStrength)
                    : colour.Darkened((0.5f - h) * HeightTintStrength);
                colour = ApplyMaterialTexture(textures, Preset, h, m, at, colour);

                return new PaintSample(colour, effect);
            }, Mathf.Max(1, TileSize));
        }

        private Color ApplyMaterialTexture(
            MaterialTextureSet? textures,
            TerrainPreset preset,
            float height,
            float moisture,
            Vector2 at,
            Color colour)
        {
            if (textures is null)
                return colour;

            Color sampled = preset switch
            {
                TerrainPreset.Sea => WaterTexture(textures, height, at),
                TerrainPreset.Ice or TerrainPreset.Snow => textures.SnowIce.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.27f, 0.11f)),
                TerrainPreset.Desert or TerrainPreset.Sand => textures.Sand.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.19f, 0.47f)),
                TerrainPreset.Rock => textures.Rock.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.29f, 0.61f)),
                TerrainPreset.Swamp => textures.Mud.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.64f, 0.08f)),
                _ when height < WaterLevel => WaterTexture(textures, height, at),
                _ when height < WaterLevel + BeachWidth => ShoreTexture(textures, height, at),
                _ when height >= RockLevel => textures.Rock.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.29f, 0.61f)),
                _ when moisture < Dryness => textures.DryGrass.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.73f, 0.17f)),
                _ => textures.Grass.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.11f, 0.37f)),
            };

            return colour.Lerp(sampled, Mathf.Clamp(MaterialTextureStrength, 0.0f, 1.0f));
        }

        private Color ShoreTexture(MaterialTextureSet textures, float height, Vector2 at)
        {
            float wet = 1.0f - SmoothStep(WaterLevel, WaterLevel + BeachWidth, height);
            Color sand = textures.Sand.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.19f, 0.47f));
            Color mud = textures.Mud.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.64f, 0.08f));
            return sand.Lerp(mud, wet * 0.45f);
        }

        private Color WaterTexture(MaterialTextureSet textures, float height, Vector2 at)
        {
            float shallow = 1.0f - SmoothStep(WaterLevel - 0.08f, WaterLevel + BeachWidth, height);
            Color deep = textures.WaterDeep.Sample(at, MaterialTextureTilesPerRepeat + 1.5f, new Vector2(0.05f, 0.42f));
            Color shallowColour = textures.WaterShallow.Sample(at, MaterialTextureTilesPerRepeat, new Vector2(0.31f, 0.76f));
            return deep.Lerp(shallowColour, shallow * 0.72f);
        }

        /// <summary>
        /// Render from an existing logical map. The sampler receives tile
        /// coordinates and returns the colour for that logical tile.
        /// </summary>
        public void RenderFromSampler(int widthTiles, int heightTiles, Func<Vector2I, Color> sample, int tileSize)
        {
            ArgumentNullException.ThrowIfNull(sample);
            RenderFromPaintSampler(widthTiles, heightTiles, cell => new PaintSample(sample(cell)), tileSize);
        }

        /// <summary>
        /// Render from an existing logical map with material effects. Water
        /// samples can be transparent, foamed at shorelines, and animated.
        /// </summary>
        public void RenderFromPaintSampler(int widthTiles, int heightTiles, Func<Vector2I, PaintSample> sample, int tileSize)
        {
            ArgumentNullException.ThrowIfNull(sample);

            RenderFromContinuousSampler(
                widthTiles,
                heightTiles,
                at => sample(new Vector2I(
                    Mathf.Clamp(Mathf.FloorToInt(at.X), 0, widthTiles - 1),
                    Mathf.Clamp(Mathf.FloorToInt(at.Y), 0, heightTiles - 1))),
                tileSize);
        }

        /// <summary>
        /// Render from a continuous tile-space sampler. This is the preferred
        /// path for painted terrain because coastlines, beaches and biome
        /// transitions can come from noise values instead of square tile cells.
        /// </summary>
        public void RenderFromContinuousSampler(int widthTiles, int heightTiles, Func<Vector2, PaintSample> sample, int tileSize)
        {
            ArgumentNullException.ThrowIfNull(sample);

            int ppt = Mathf.Clamp(PixelsPerTile, 4, 64);
            int imageWidth = Mathf.Max(1, widthTiles) * ppt;
            int imageHeight = Mathf.Max(1, heightTiles) * ppt;
            var image = Image.CreateEmpty(imageWidth, imageHeight, false, Image.Format.Rgba8);

            for (int y = 0; y < imageHeight; y++)
            {
                for (int x = 0; x < imageWidth; x++)
                {
                    Vector2 at = new(
                        Mathf.Clamp((x + 0.5f) / ppt, 0.0f, Mathf.Max(0.0f, widthTiles - 0.001f)),
                        Mathf.Clamp((y + 0.5f) / ppt, 0.0f, Mathf.Max(0.0f, heightTiles - 0.001f)));

                    PaintSample paint = sample(at);
                    Color colour = paint.Colour;

                    float grain = Grain(x, y, Seed);
                    colour = colour.Lightened(Mathf.Max(0.0f, grain) * GrainStrength);
                    colour = colour.Darkened(Mathf.Max(0.0f, -grain) * GrainStrength * 0.8f);
                    colour = ApplyEffect(colour, paint.Effect, 0.0f, x, y);

                    image.SetPixel(x, y, colour);
                }
            }

            ApplySmoothing(image);
            ApplyTexture(ImageTexture.CreateFromImage(image), tileSize, ppt);
        }

        private void ApplySmoothing(Image image)
        {
            int passes = Mathf.Clamp(SmoothingPasses, 0, 3);

            for (int pass = 0; pass < passes; pass++)
                SmoothImage(image);
        }

        private static void SmoothImage(Image image)
        {
            int width = image.GetWidth();
            int height = image.GetHeight();
            byte[] source = image.GetData();
            byte[] target = new byte[source.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int centre = Offset(x, y, width);
                    int left = Offset(Mathf.Max(0, x - 1), y, width);
                    int right = Offset(Mathf.Min(width - 1, x + 1), y, width);
                    int up = Offset(x, Mathf.Max(0, y - 1), width);
                    int down = Offset(x, Mathf.Min(height - 1, y + 1), width);

                    for (int channel = 0; channel < 4; channel++)
                    {
                        int value =
                            (source[centre + channel] * 4) +
                            source[left + channel] +
                            source[right + channel] +
                            source[up + channel] +
                            source[down + channel];
                        target[centre + channel] = (byte)(value / 8);
                    }
                }
            }

            image.SetData(width, height, false, Image.Format.Rgba8, target);
        }

        private static int Offset(int x, int y, int width) => ((y * width) + x) * 4;

        private void ApplyTexture(Texture2D texture, int tileSize, int pixelsPerTile)
        {
            _sprite ??= EnsureSprite();
            _sprite.Texture = texture;
            _sprite.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
            _sprite.Centered = Centered;
            _sprite.ZIndex = RenderZIndex;
            _sprite.Scale = new Vector2((float)tileSize / pixelsPerTile, (float)tileSize / pixelsPerTile);
            _sprite.Material = AnimateWater ? WaterMaterial() : null;
        }

        private Sprite2D EnsureSprite()
        {
            if (GetNodeOrNull<Sprite2D>("PainterlyTerrainSprite") is { } existing)
                return existing;

            var sprite = new Sprite2D { Name = "PainterlyTerrainSprite", Centered = Centered, ZIndex = RenderZIndex };
            AddChild(sprite);

            if (Engine.IsEditorHint())
                sprite.Owner = GetTree()?.EditedSceneRoot;

            return sprite;
        }

        private FastNoiseLite Noise(int seed, float frequency) => new()
        {
            Seed = seed,
            NoiseType = NoiseType,
            FractalType = FractalType,
            Frequency = Mathf.Max(0.0001f, frequency),
            FractalOctaves = Mathf.Clamp(Octaves, 1, 10),
            FractalLacunarity = Mathf.Max(1.0f, Lacunarity),
            FractalGain = Mathf.Clamp(Gain, 0.0f, 1.0f),
        };

        private Color ColourFor(TerrainPreset preset, float height, float moisture)
        {
            if (preset is TerrainPreset.Sea or TerrainPreset.Ice or TerrainPreset.Lava)
                return BaseColour(preset);

            if (height < WaterLevel)
                return new Color(0.05f, 0.37f, 0.52f);

            if (height < WaterLevel + BeachWidth)
                return preset == TerrainPreset.Desert ? new Color(0.66f, 0.52f, 0.30f) : new Color(0.60f, 0.53f, 0.34f);

            if (height >= RockLevel)
                return preset == TerrainPreset.Snow ? new Color(0.70f, 0.73f, 0.72f) : new Color(0.34f, 0.38f, 0.35f);

            return preset switch
            {
                TerrainPreset.Desert => new Color(0.58f, 0.42f, 0.20f).Lerp(new Color(0.78f, 0.60f, 0.32f), moisture * 0.35f),
                TerrainPreset.Sand => new Color(0.66f, 0.56f, 0.34f),
                TerrainPreset.Rock => new Color(0.36f, 0.37f, 0.34f),
                TerrainPreset.Swamp => new Color(0.16f, 0.28f, 0.18f).Lerp(new Color(0.25f, 0.35f, 0.20f), moisture),
                TerrainPreset.Snow => new Color(0.78f, 0.82f, 0.80f).Lerp(new Color(0.62f, 0.70f, 0.74f), moisture * 0.35f),
                _ when moisture < Dryness => new Color(0.42f, 0.48f, 0.28f),
                _ => new Color(0.25f, 0.48f, 0.20f).Lerp(new Color(0.18f, 0.38f, 0.17f), moisture * 0.45f),
            };
        }

        private static Color BaseColour(TerrainPreset preset) => preset switch
        {
            TerrainPreset.Desert => new Color(0.68f, 0.47f, 0.22f),
            TerrainPreset.Sand => new Color(0.68f, 0.58f, 0.36f),
            TerrainPreset.Ice => new Color(0.66f, 0.82f, 0.88f),
            TerrainPreset.Sea => new Color(0.04f, 0.34f, 0.50f),
            TerrainPreset.Rock => new Color(0.34f, 0.35f, 0.33f),
            TerrainPreset.Lava => new Color(0.24f, 0.08f, 0.05f),
            TerrainPreset.Swamp => new Color(0.16f, 0.28f, 0.18f),
            TerrainPreset.Snow => new Color(0.78f, 0.82f, 0.80f),
            _ => new Color(0.25f, 0.48f, 0.20f),
        };

        private static TerrainPaintEffect EffectFor(TerrainPreset preset) => preset switch
        {
            TerrainPreset.Sea => TerrainPaintEffect.Water,
            TerrainPreset.Ice => TerrainPaintEffect.Ice,
            TerrainPreset.Lava => TerrainPaintEffect.Lava,
            _ => TerrainPaintEffect.None,
        };

        private TerrainPaintEffect EffectFor(TerrainPreset preset, float height) => preset switch
        {
            TerrainPreset.Sea => TerrainPaintEffect.Water,
            TerrainPreset.Ice => TerrainPaintEffect.Ice,
            TerrainPreset.Lava => TerrainPaintEffect.Lava,
            _ when height < WaterLevel => TerrainPaintEffect.Water,
            _ => TerrainPaintEffect.None,
        };

        private Color ApplyEffect(Color colour, TerrainPaintEffect effect, float edgeAmount, int x, int y)
        {
            return effect switch
            {
                TerrainPaintEffect.Water => ApplyWater(colour, edgeAmount, x, y),
                TerrainPaintEffect.Ice => colour.Lightened(0.10f),
                TerrainPaintEffect.Lava => colour.Lightened(Grain(x, y, Seed + 4001) * 0.18f),
                _ => colour,
            };
        }

        private Color ApplyWater(Color colour, float edgeAmount, int x, int y)
        {
            float ripple = Grain(x * 3, y * 2, Seed + 1777) * WaterRippleStrength;
            Color water = colour.Lightened(Mathf.Max(0.0f, ripple));

            if (edgeAmount > 0.0f && WaterFoamStrength > 0.0f)
                water = water.Lerp(Colors.White, edgeAmount * WaterFoamStrength);

            water.A = Mathf.Lerp(WaterAlpha, ShallowWaterAlpha, edgeAmount);
            return water;
        }

        private static TerrainPaintEffect DominantEffect(
            TerrainPaintEffect a, TerrainPaintEffect b, TerrainPaintEffect c, TerrainPaintEffect d)
        {
            if (a == TerrainPaintEffect.Water || b == TerrainPaintEffect.Water ||
                c == TerrainPaintEffect.Water || d == TerrainPaintEffect.Water)
                return TerrainPaintEffect.Water;

            if (a == TerrainPaintEffect.Lava || b == TerrainPaintEffect.Lava ||
                c == TerrainPaintEffect.Lava || d == TerrainPaintEffect.Lava)
                return TerrainPaintEffect.Lava;

            if (a == TerrainPaintEffect.Ice || b == TerrainPaintEffect.Ice ||
                c == TerrainPaintEffect.Ice || d == TerrainPaintEffect.Ice)
                return TerrainPaintEffect.Ice;

            return TerrainPaintEffect.None;
        }

        private static float WaterEdgeAmount(
            TerrainPaintEffect a, TerrainPaintEffect b, TerrainPaintEffect c, TerrainPaintEffect d)
        {
            int water = 0;
            if (a == TerrainPaintEffect.Water) water++;
            if (b == TerrainPaintEffect.Water) water++;
            if (c == TerrainPaintEffect.Water) water++;
            if (d == TerrainPaintEffect.Water) water++;

            return water is > 0 and < 4 ? 1.0f - (water / 4.0f) : 0.0f;
        }

        private ShaderMaterial WaterMaterial()
        {
            _waterMaterial ??= new ShaderMaterial
            {
                Shader = new Shader { Code = WaterShaderCode },
            };

            _waterMaterial.SetShaderParameter("water_scroll_speed", WaterScrollSpeed);
            _waterMaterial.SetShaderParameter("water_ripple_strength", WaterRippleStrength);

            return _waterMaterial;
        }

        private ShaderMaterial? _waterMaterial;

        private const string WaterShaderCode = @"
shader_type canvas_item;

uniform vec2 water_scroll_speed = vec2(0.018, 0.011);
uniform float water_ripple_strength : hint_range(0.0, 1.0) = 0.12;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    if (tex.a < 0.99) {
        vec2 moved = UV + TIME * water_scroll_speed;
        float wave =
            sin((moved.x * 36.0) + (moved.y * 18.0)) * 0.45 +
            sin((moved.x * -20.0) + (moved.y * 42.0)) * 0.35 +
            sin((moved.x * 70.0) + TIME * 0.6) * 0.20;
        tex.rgb += wave * water_ripple_strength * 0.055;
    }
    COLOR = tex;
}
";

        private static float Normalized(float value) => (value + 1.0f) * 0.5f;

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0), 0.0f, 1.0f);
            return t * t * (3.0f - (2.0f * t));
        }

        private float Smooth(float t)
        {
            t = Mathf.Clamp(t, 0.0f, 1.0f);
            float smooth = t * t * (3.0f - (2.0f * t));
            return Mathf.Lerp(t, smooth, BlendStrength);
        }

        private static float Grain(int x, int y, int seed)
        {
            uint n = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
            n = (n ^ (n >> 13)) * 1274126177u;
            n ^= n >> 16;

            return ((n & 255u) / 127.5f) - 1.0f;
        }

        private sealed class MaterialTextureSet
        {
            private static MaterialTextureSet? _cached;

            private MaterialTextureSet(
                SampledMaterialTexture grass,
                SampledMaterialTexture dryGrass,
                SampledMaterialTexture sand,
                SampledMaterialTexture mud,
                SampledMaterialTexture rock,
                SampledMaterialTexture waterShallow,
                SampledMaterialTexture waterDeep,
                SampledMaterialTexture snowIce)
            {
                Grass = grass;
                DryGrass = dryGrass;
                Sand = sand;
                Mud = mud;
                Rock = rock;
                WaterShallow = waterShallow;
                WaterDeep = waterDeep;
                SnowIce = snowIce;
            }

            public SampledMaterialTexture Grass { get; }
            public SampledMaterialTexture DryGrass { get; }
            public SampledMaterialTexture Sand { get; }
            public SampledMaterialTexture Mud { get; }
            public SampledMaterialTexture Rock { get; }
            public SampledMaterialTexture WaterShallow { get; }
            public SampledMaterialTexture WaterDeep { get; }
            public SampledMaterialTexture SnowIce { get; }

            public static MaterialTextureSet Load()
            {
                if (_cached is not null)
                    return _cached;

                const string Root = "res://addons/beep_game_builder_cs/textures/terrain/";
                _cached = new MaterialTextureSet(
                    SampledMaterialTexture.Load(Root + "grass.png", new Color(0.25f, 0.48f, 0.20f)),
                    SampledMaterialTexture.Load(Root + "dry_grass.png", new Color(0.42f, 0.48f, 0.28f)),
                    SampledMaterialTexture.Load(Root + "sand.png", new Color(0.58f, 0.50f, 0.30f)),
                    SampledMaterialTexture.Load(Root + "mud.png", new Color(0.35f, 0.32f, 0.22f)),
                    SampledMaterialTexture.Load(Root + "rock.png", new Color(0.34f, 0.38f, 0.35f)),
                    SampledMaterialTexture.Load(Root + "water_shallow.png", new Color(0.10f, 0.50f, 0.58f)),
                    SampledMaterialTexture.Load(Root + "water_deep.png", new Color(0.05f, 0.40f, 0.54f)),
                    SampledMaterialTexture.Load(Root + "snow_ice.png", new Color(0.78f, 0.82f, 0.80f)));
                return _cached;
            }
        }

        private sealed class SampledMaterialTexture
        {
            private readonly byte[] _data;
            private readonly Color _fallback;

            private SampledMaterialTexture(byte[] data, int width, int height, Color fallback)
            {
                _data = data;
                Width = width;
                Height = height;
                _fallback = fallback;
            }

            private int Width { get; }
            private int Height { get; }

            public static SampledMaterialTexture Load(string path, Color fallback)
            {
                try
                {
                    Texture2D? texture = GD.Load<Texture2D>(path);
                    Image? image = texture?.GetImage();
                    if (image is null || image.IsEmpty())
                        return Solid(fallback);

                    if (image.GetFormat() != Image.Format.Rgba8)
                        image.Convert(Image.Format.Rgba8);

                    return new SampledMaterialTexture(image.GetData(), image.GetWidth(), image.GetHeight(), fallback);
                }
                catch (Exception ex)
                {
                    GD.PushWarning($"Painterly terrain texture '{path}' could not be loaded: {ex.Message}");
                    return Solid(fallback);
                }
            }

            public Color Sample(Vector2 tile, float tilesPerRepeat, Vector2 offset)
            {
                if (_data.Length == 0 || Width <= 0 || Height <= 0)
                    return _fallback;

                float u = Repeat((tile.X / Mathf.Max(0.001f, tilesPerRepeat)) + offset.X);
                float v = Repeat((tile.Y / Mathf.Max(0.001f, tilesPerRepeat)) + offset.Y);
                float x = u * (Width - 1);
                float y = v * (Height - 1);
                int x0 = Mathf.FloorToInt(x);
                int y0 = Mathf.FloorToInt(y);
                int x1 = (x0 + 1) % Width;
                int y1 = (y0 + 1) % Height;
                float tx = x - x0;
                float ty = y - y0;

                Color a = Pixel(x0, y0);
                Color b = Pixel(x1, y0);
                Color c = Pixel(x0, y1);
                Color d = Pixel(x1, y1);

                return a.Lerp(b, tx).Lerp(c.Lerp(d, tx), ty);
            }

            private static SampledMaterialTexture Solid(Color fallback) => new(Array.Empty<byte>(), 0, 0, fallback);

            private Color Pixel(int x, int y)
            {
                int i = ((y * Width) + x) * 4;
                return new Color(
                    _data[i] / 255.0f,
                    _data[i + 1] / 255.0f,
                    _data[i + 2] / 255.0f,
                    _data[i + 3] / 255.0f);
            }

            private static float Repeat(float value) => value - Mathf.Floor(value);
        }
    }
}
