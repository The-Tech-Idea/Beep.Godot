using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace Beep.ECS
{
	/// <summary>
	/// Renders an opaque terrain base plus explicit ground-detail, water, and
	/// gameplay-overlay Sprite2D layers. Use this behind TileMapLayer collision,
	/// transition, and authored-detail maps to reduce broad terrain tile count
	/// without merging unrelated visual state into one texture.
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

		public readonly record struct PaintSample(
			Color Colour,
			TerrainPaintEffect Effect = TerrainPaintEffect.None,
			float EdgeAmount = 0.0f,
			string TerrainKind = "");

		/// <summary>
		/// Typed terrain data consumed by the explicit render layers. Unlike
		/// <see cref="PaintSample"/>, this never encodes roads, farming state, or
		/// water into a single final colour.
		/// </summary>
		public readonly record struct TerrainPaintSample(
			string TerrainKind,
			Color BaseColour,
			TerrainPaintEffect Effect = TerrainPaintEffect.None,
			float WaterAmount = 0.0f,
			float WaterEdgeAmount = 0.0f,
			float GroundDetailMask = 0.0f,
			float PropScatterMask = 0.0f,
			int CellFlags = 0,
			string RoadKind = "");

		public enum DetailRegionMode
		{
			SampleMask,
			ProceduralPatches,
			SampleAndProceduralPatches,
		}

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

		private readonly record struct BiomeOverlayDescriptor(string Key, string TexturePath);
		private readonly record struct BiomeTextureOverlay(string Key, Texture2D MaskTexture, Texture2D Texture);
		private readonly record struct MaterialTexturePaths(
			string Grass,
			string DryGrass,
			string Sand,
			string Mud,
			string Rock,
			string WaterShallow,
			string WaterDeep,
			string SnowIce)
		{
			public static MaterialTexturePaths From(PainterlyTerrainComponent terrain) => new(
				terrain.GrassMaterialTexturePath,
				terrain.DryGrassMaterialTexturePath,
				terrain.SandMaterialTexturePath,
				terrain.MudMaterialTexturePath,
				terrain.RockMaterialTexturePath,
				terrain.ShallowWaterMaterialTexturePath,
				terrain.DeepWaterMaterialTexturePath,
				terrain.SnowIceMaterialTexturePath);
		}

		[ExportGroup("Generation")]
		[Export] public TerrainMode Mode { get; set; } = TerrainMode.ProceduralNoise;
		[Export] public TerrainPreset Preset { get; set; } = TerrainPreset.Grassland;
		[Export] public int WidthTiles { get; set; } = 96;
		[Export] public int HeightTiles { get; set; } = 64;
		[Export] public int TileSize { get; set; } = 64;
		[Export] public int PixelsPerTile { get; set; } = 64;
		[Export] public int Seed { get; set; } = 12345;
		[Export] public bool GenerateOnReady { get; set; } = true;
		[Export] public bool GenerateInEditor { get; set; } = false;

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
		[Export(PropertyHint.Range, "0,1,0.01")] public float GrainStrength { get; set; } = 0.0f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float HeightTintStrength { get; set; } = 0.10f;
		[Export(PropertyHint.Range, "0,3,1")] public int SmoothingPasses { get; set; } = 0;
		[Export] public bool UseBundledMaterialTextures { get; set; } = false;
		[Export(PropertyHint.Dir)] public string MaterialTextureRoot { get; set; } = DefaultMaterialTextureRoot;
		[Export(PropertyHint.Range, "0,1,0.01")] public float MaterialTextureStrength { get; set; } = 0.35f;
		[Export(PropertyHint.Range, "1,32,0.5")] public float MaterialTextureTilesPerRepeat { get; set; } = 7.0f;
		[ExportGroup("Material Texture Sources")]
		[Export(PropertyHint.File, "*.png,*.webp")] public string GrassMaterialTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string DryGrassMaterialTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string SandMaterialTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string MudMaterialTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string RockMaterialTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string ShallowWaterMaterialTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string DeepWaterMaterialTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string SnowIceMaterialTexturePath { get; set; } = "";
		[Export(PropertyHint.Range, "0,33554432,1024")] public int MaxGeneratedPixels { get; set; } = 16777216;
		[Export(PropertyHint.Range, "0,1,0.01")] public float PostSharpenStrength { get; set; } = 0.16f;
		[Export] public int RenderZIndex { get; set; } = -90;
		[Export] public bool RenderDetailAsOverlay { get; set; } = true;
		[Export] public int DetailLayerZOffset { get; set; } = 1;
		[Export] public Vector2 RenderOffset { get; set; } = Vector2.Zero;
		[Export] public bool Centered { get; set; } = false;
		[Export] public CanvasItem.TextureFilterEnum TerrainTextureFilter { get; set; } = CanvasItem.TextureFilterEnum.Linear;

		[ExportGroup("Repeated Grass Overlay")]
		[Export] public bool UseRepeatedGrassOverlay { get; set; } = false;
		[Export(PropertyHint.File, "*.png,*.webp")] public string GrassOverlayTexturePath { get; set; } = "";
		[Export(PropertyHint.Range, "0,1,0.01")] public float GrassOverlayStrength { get; set; } = 0.28f;
		[Export(PropertyHint.Range, "32,4096,1")] public float GrassOverlayTextureSize { get; set; } = 1024.0f;

		[ExportGroup("Repeated Biome Overlays")]
		[Export] public bool UseRepeatedBiomeOverlays { get; set; } = false;
		[Export(PropertyHint.File, "*.png,*.webp")] public string DryGrassOverlayTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string SandOverlayTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string EarthOverlayTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string RockOverlayTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string SnowIceOverlayTexturePath { get; set; } = "";
		[Export(PropertyHint.Range, "0,1,0.01")] public float BiomeOverlayStrength { get; set; } = 0.18f;
		[Export(PropertyHint.Range, "32,4096,1")] public float BiomeOverlayTextureSize { get; set; } = 1024.0f;

		[ExportGroup("Biome Detail Layers")]
		[Export] public bool EnableBiomeDetailLayers { get; set; } = false;
		[Export] public DetailRegionMode GroundDetailRegionMode { get; set; } = DetailRegionMode.SampleMask;
		[Export(PropertyHint.Range, "0,1,0.01")] public float DefaultGroundDetailMask { get; set; } = 0.0f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float BiomeDetailStrength { get; set; } = 0.52f;
		[Export(PropertyHint.Range, "0,2,0.01")] public float BiomeDetailDensity { get; set; } = 1.0f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float BiomeDetailCoverage { get; set; } = 0.30f;
		[Export(PropertyHint.Range, "0.02,2,0.01")] public float BiomeDetailPatchScale { get; set; } = 0.18f;
		[Export(PropertyHint.Range, "0.01,0.5,0.01")] public float BiomeDetailPatchSoftness { get; set; } = 0.12f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float DuneLayerStrength { get; set; } = 0.34f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float PebbleLayerStrength { get; set; } = 0.24f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float VegetationLayerStrength { get; set; } = 0.32f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float FlowerLayerStrength { get; set; } = 0.10f;

		[ExportGroup("Base Colours")]
		[Export] public Color GrassBaseColour { get; set; } = new(0.34f, 0.62f, 0.12f);
		[Export] public Color DryGrassBaseColour { get; set; } = new(0.38f, 0.56f, 0.18f);
		[Export] public Color DesertBaseColour { get; set; } = new(0.68f, 0.47f, 0.22f);
		[Export] public Color SandBaseColour { get; set; } = new(0.68f, 0.58f, 0.36f);
		[Export] public Color RockBaseColour { get; set; } = new(0.34f, 0.35f, 0.33f);
		[Export] public Color SwampBaseColour { get; set; } = new(0.16f, 0.28f, 0.18f);
		[Export] public Color SnowBaseColour { get; set; } = new(0.78f, 0.82f, 0.80f);
		[Export] public Color IceBaseColour { get; set; } = new(0.66f, 0.82f, 0.88f);
		[Export] public Color WaterBaseColour { get; set; } = new(0.04f, 0.34f, 0.50f);

		public int LastGeneratedPixelsPerTile { get; private set; }
		public int LastGeneratedPixelCount { get; private set; }
		public bool LastGenerationWasCapped { get; private set; }
		public Vector2 LastAppliedTerrainScale { get; private set; } = Vector2.One;
		public bool LastGeneratedSeparateDetailLayer { get; private set; }
		public int LastGroundDetailPixelCount { get; private set; }
		public int LastWaterPixelCount { get; private set; }
		public int LastFoamPixelCount { get; private set; }
		public int LastGameplayOverlayPixelCount { get; private set; }

		[ExportGroup("Water Effects")]
		[Export(PropertyHint.Range, "0,1,0.01")] public float WaterAlpha { get; set; } = 0.78f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float ShallowWaterAlpha { get; set; } = 0.62f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float WaterFoamStrength { get; set; } = 0.22f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float WaterRippleStrength { get; set; } = 0.12f;
		[Export] public bool AnimateWater { get; set; } = true;
		[Export] public Vector2 WaterScrollSpeed { get; set; } = new(0.018f, 0.011f);
		[Export] public bool UseRepeatedWaterOverlay { get; set; } = false;
		[Export(PropertyHint.File, "*.png,*.webp")] public string WaterOverlayTexturePath { get; set; } = "";
		[Export(PropertyHint.Range, "0,1,0.01")] public float WaterOverlayStrength { get; set; } = 0.20f;
		[Export(PropertyHint.Range, "32,4096,1")] public float WaterOverlayTextureSize { get; set; } = 1024.0f;
		[Export] public bool UseAnimatedFoamEdges { get; set; } = false;
		[Export(PropertyHint.File, "*.png,*.webp")] public string FoamTexturePath { get; set; } = "";
		[Export(PropertyHint.Range, "0,1,0.01")] public float FoamOpacity { get; set; } = 0.72f;
		[Export(PropertyHint.Range, "1,16,1")] public int FoamEdgeWidthPixels { get; set; } = 2;
		[Export(PropertyHint.Range, "1,60,0.1")] public float FoamFramesPerSecond { get; set; } = 8.0f;
		[Export(PropertyHint.Range, "1,64,1")] public int FoamFrameCount { get; set; } = 16;
		[Export(PropertyHint.Range, "0.1,32,0.1")] public float FoamTilesPerRepeat { get; set; } = 6.0f;

		private Sprite2D? _sprite;
		private Sprite2D? _grassOverlaySprite;
		private Sprite2D? _detailSprite;
		private Sprite2D? _waterSprite;
		private Sprite2D? _foamSprite;
		private Sprite2D? _gameplaySprite;
		private readonly Dictionary<string, Sprite2D> _biomeOverlaySprites = new(StringComparer.Ordinal);
		private readonly Dictionary<string, ShaderMaterial> _biomeOverlayMaterials = new(StringComparer.Ordinal);
		private const string DefaultMaterialTextureRoot = "res://addons/beep_game_builder_cs/textures/terrain";

		public override void _Ready()
		{
			base._Ready();

			if (!GenerateOnReady)
				return;

			if (Engine.IsEditorHint() && !GenerateInEditor)
				return;

			CallDeferred(nameof(Rebuild));
		}

		/// <summary>Rebuild from the exported mode, preset and noise settings.</summary>
		public void Rebuild()
		{
			int width = Mathf.Max(1, WidthTiles);
			int height = Mathf.Max(1, HeightTiles);
			MaterialTextureSet? textures = UseBundledMaterialTextures
				? MaterialTextureSet.Load(MaterialTextureRoot, MaterialTexturePaths.From(this))
				: null;

			if (Mode == TerrainMode.Plain)
			{
				RenderFromTerrainPaintContinuousSampler(
					width,
					height,
					at =>
					{
						Color colour = ApplyMaterialTexture(textures, Preset, 0.5f, 0.5f, at, BaseColour(Preset));
						TerrainPaintEffect effect = EffectFor(Preset);
						return new TerrainPaintSample(
							TerrainKindFor(Preset, 0.5f, 0.5f),
							colour,
							effect,
							effect == TerrainPaintEffect.Water ? 1.0f : 0.0f,
							GroundDetailMask: DefaultGroundDetailMask);
					},
					Mathf.Max(1, TileSize));
				return;
			}

			FastNoiseLite heightNoise = Noise(Seed, Frequency);
			FastNoiseLite moistureNoise = Noise(Seed + 9719, Frequency * 1.35f);

			RenderFromTerrainPaintContinuousSampler(width, height, at =>
			{
				float h = Smooth(Normalized(heightNoise.GetNoise2D(at.X, at.Y)));
				float m = Smooth(Normalized(moistureNoise.GetNoise2D(at.X, at.Y)));

				TerrainPaintEffect effect = EffectFor(Preset, h);
				string terrainKind = TerrainKindFor(Preset, h, m);
				Color colour = ColourFor(Preset, h, m);
				if (!KeepsPlainBaseColour(terrainKind))
				{
					colour = h >= 0.5f
						? colour.Lightened((h - 0.5f) * HeightTintStrength)
						: colour.Darkened((0.5f - h) * HeightTintStrength);
				}
				colour = ApplyMaterialTexture(textures, Preset, h, m, at, colour);

				return new TerrainPaintSample(
					terrainKind,
					colour,
					effect,
					effect == TerrainPaintEffect.Water ? 1.0f : 0.0f,
					WaterEdgeForHeight(effect, h),
					DefaultGroundDetailMask);
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
			float strength = Mathf.Clamp(MaterialTextureStrength, 0.0f, 1.0f);
			if (textures is null || strength <= 0.0f)
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

			return colour.Lerp(sampled, strength);
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
			RenderFromTerrainPaintSampler(
				widthTiles,
				heightTiles,
				cell => ToTerrainPaintSample(sample(cell)),
				tileSize);
		}

		/// <summary>
		/// Renders a logical grid without blending discrete cell state into the
		/// terrain base. Water, ground marks, and gameplay data are emitted to
		/// their own buffers.
		/// </summary>
		public void RenderFromTerrainPaintSampler(int widthTiles, int heightTiles, Func<Vector2I, TerrainPaintSample> sample, int tileSize)
		{
			ArgumentNullException.ThrowIfNull(sample);

			int width = Mathf.Max(1, widthTiles);
			int height = Mathf.Max(1, heightTiles);
			TerrainPaintSample[] cells = new TerrainPaintSample[width * height];
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
					cells[CellIndex(x, y, width)] = sample(new Vector2I(x, y));
			}

			RenderFromTerrainPaintContinuousSampler(
				width,
				height,
				at =>
				{
					int x = Mathf.Clamp(Mathf.FloorToInt(at.X), 0, width - 1);
					int y = Mathf.Clamp(Mathf.FloorToInt(at.Y), 0, height - 1);
					TerrainPaintSample paint = cells[CellIndex(x, y, width)];
					if (paint.Effect != TerrainPaintEffect.Water && paint.WaterAmount <= 0.0f)
						return paint;

					float edge = WaterEdgeAmount(
						EffectAt(cells, x, y, width, height),
						EffectAt(cells, x - 1, y, width, height),
						EffectAt(cells, x + 1, y, width, height),
						EffectAt(cells, x, y - 1, width, height),
						EffectAt(cells, x, y + 1, width, height));

					return edge > paint.WaterEdgeAmount ? paint with { WaterEdgeAmount = edge } : paint;
				},
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
			RenderFromTerrainPaintContinuousSampler(widthTiles, heightTiles, at => ToTerrainPaintSample(sample(at)), tileSize);
		}

		/// <summary>Renders typed samples into independent terrain layers.</summary>
		public void RenderFromTerrainPaintContinuousSampler(int widthTiles, int heightTiles, Func<Vector2, TerrainPaintSample> sample, int tileSize)
		{
			ArgumentNullException.ThrowIfNull(sample);

			int ppt = EffectivePixelsPerTile(widthTiles, heightTiles);
			int imageWidth = Mathf.Max(1, widthTiles) * ppt;
			int imageHeight = Mathf.Max(1, heightTiles) * ppt;
			LastGeneratedPixelsPerTile = ppt;
			LastGeneratedPixelCount = imageWidth * imageHeight;
			LastGenerationWasCapped = ppt < Mathf.Clamp(PixelsPerTile, 1, 64);
			var baseImage = Image.CreateEmpty(imageWidth, imageHeight, false, Image.Format.Rgba8);
			byte[] pixels = new byte[imageWidth * imageHeight * 4];
			byte[] grassMaskPixels = new byte[imageWidth * imageHeight * 4];
			Dictionary<string, byte[]> biomeMaskPixels = CreateBiomeOverlayMasks(imageWidth, imageHeight);
			var detailImage = Image.CreateEmpty(imageWidth, imageHeight, false, Image.Format.Rgba8);
			var waterImage = Image.CreateEmpty(imageWidth, imageHeight, false, Image.Format.Rgba8);
			var gameplayImage = Image.CreateEmpty(imageWidth, imageHeight, false, Image.Format.Rgba8);
			byte[] detailPixels = new byte[imageWidth * imageHeight * 4];
			byte[] waterPixels = new byte[imageWidth * imageHeight * 4];
			byte[] gameplayPixels = new byte[imageWidth * imageHeight * 4];
			LastGeneratedSeparateDetailLayer = true;
			LastGroundDetailPixelCount = 0;
			LastWaterPixelCount = 0;
			LastFoamPixelCount = 0;
			LastGameplayOverlayPixelCount = 0;

			for (int y = 0; y < imageHeight; y++)
			{
				for (int x = 0; x < imageWidth; x++)
				{
					Vector2 at = new(
						Mathf.Clamp((x + 0.5f) / ppt, 0.0f, Mathf.Max(0.0f, widthTiles - 0.001f)),
						Mathf.Clamp((y + 0.5f) / ppt, 0.0f, Mathf.Max(0.0f, heightTiles - 0.001f)));

					TerrainPaintSample paint = sample(at);
					// Water is an overlay. Keep its ground colour in the base layer so
					// alpha, foam, and future shore tiles never turn into a solid blue slab.
					Color baseColour = paint.Effect == TerrainPaintEffect.Water
						? paint.BaseColour
						: PlainBaseColourFor(paint.TerrainKind, paint.BaseColour);
					float grain = Grain(x, y, Seed);
					baseColour = baseColour.Lightened(Mathf.Max(0.0f, grain) * GrainStrength);
					baseColour = baseColour.Darkened(Mathf.Max(0.0f, -grain) * GrainStrength * 0.8f);
					baseColour.A = 1.0f;
					WritePixel(pixels, x, y, imageWidth, baseColour);
					if (IsGrassTerrain(paint.TerrainKind))
					{
						int offset = Offset(x, y, imageWidth);
						grassMaskPixels[offset] = 255;
						grassMaskPixels[offset + 3] = 255;
					}
					WriteBiomeOverlayMask(biomeMaskPixels, paint.TerrainKind, x, y, imageWidth);

					Color detail = GroundDetailPixel(paint, at, x, y);
					WritePixel(detailPixels, x, y, imageWidth, detail);
					if (detail.A > 0.0f)
						LastGroundDetailPixelCount++;

					Color water = WaterLayerPixel(paint, x, y);
					WritePixel(waterPixels, x, y, imageWidth, water);
					if (water.A > 0.0f)
						LastWaterPixelCount++;

					Color gameplay = GameplayOverlayPixel(paint, at);
					WritePixel(gameplayPixels, x, y, imageWidth, gameplay);
					if (gameplay.A > 0.0f)
						LastGameplayOverlayPixelCount++;
				}
			}

			baseImage.SetData(imageWidth, imageHeight, false, Image.Format.Rgba8, pixels);
			detailImage.SetData(imageWidth, imageHeight, false, Image.Format.Rgba8, detailPixels);
			waterImage.SetData(imageWidth, imageHeight, false, Image.Format.Rgba8, waterPixels);
			gameplayImage.SetData(imageWidth, imageHeight, false, Image.Format.Rgba8, gameplayPixels);
			Texture2D? foamMask = BuildFoamEdgeMask(waterPixels, imageWidth, imageHeight);
			Texture2D? foamTexture = foamMask is null ? null : LoadTexture2D(FoamTexturePath);
			Texture2D? grassMask = BuildGrassOverlayMask(grassMaskPixels, imageWidth, imageHeight);
			Texture2D? grassTexture = grassMask is null ? null : LoadTexture2D(GrassOverlayTexturePath);
			IReadOnlyList<BiomeTextureOverlay> biomeOverlays = BuildBiomeTextureOverlays(biomeMaskPixels, imageWidth, imageHeight);

			ApplyLayerTextures(
				ImageTexture.CreateFromImage(baseImage),
				grassMask,
				grassTexture,
				biomeOverlays,
				ImageTexture.CreateFromImage(detailImage),
				ImageTexture.CreateFromImage(waterImage),
				foamMask,
				foamTexture,
				ImageTexture.CreateFromImage(gameplayImage),
				tileSize,
				ppt);
		}

		private Texture2D? BuildGrassOverlayMask(byte[] maskPixels, int width, int height)
		{
			if (!UseRepeatedGrassOverlay || string.IsNullOrWhiteSpace(GrassOverlayTexturePath) || GrassOverlayStrength <= 0.0f)
				return null;

			bool hasGrass = false;
			for (int i = 3; i < maskPixels.Length; i += 4)
			{
				if (maskPixels[i] != 0)
				{
					hasGrass = true;
					break;
				}
			}
			if (!hasGrass)
				return null;

			var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
			image.SetData(width, height, false, Image.Format.Rgba8, maskPixels);
			return ImageTexture.CreateFromImage(image);
		}

		private static bool IsGrassTerrain(string terrainKind)
			=> NormalizeTerrainKind(terrainKind) is "grass" or "grassland" or "dry_grass";

		private Dictionary<string, byte[]> CreateBiomeOverlayMasks(int width, int height)
		{
			var masks = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			if (!UseRepeatedBiomeOverlays || BiomeOverlayStrength <= 0.0f)
				return masks;

			foreach (BiomeOverlayDescriptor descriptor in BiomeOverlayDescriptors())
			{
				if (!string.IsNullOrWhiteSpace(descriptor.TexturePath))
					masks[descriptor.Key] = new byte[width * height * 4];
			}
			return masks;
		}

		private static void WriteBiomeOverlayMask(Dictionary<string, byte[]> masks, string terrainKind, int x, int y, int width)
		{
			if (masks.Count == 0)
				return;

			string key = BiomeOverlayKeyFor(terrainKind);
			if (!masks.TryGetValue(key, out byte[]? pixels))
				return;

			int offset = Offset(x, y, width);
			pixels[offset] = 255;
			pixels[offset + 3] = 255;
		}

		private IReadOnlyList<BiomeTextureOverlay> BuildBiomeTextureOverlays(Dictionary<string, byte[]> masks, int width, int height)
		{
			var overlays = new List<BiomeTextureOverlay>();
			foreach (BiomeOverlayDescriptor descriptor in BiomeOverlayDescriptors())
			{
				if (!masks.TryGetValue(descriptor.Key, out byte[]? pixels) || !HasVisiblePixels(pixels))
					continue;

				Texture2D? texture = LoadTexture2D(descriptor.TexturePath);
				if (texture is null)
					continue;

				var mask = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
				mask.SetData(width, height, false, Image.Format.Rgba8, pixels);
				overlays.Add(new BiomeTextureOverlay(descriptor.Key, ImageTexture.CreateFromImage(mask), texture));
			}
			return overlays;
		}

		private IReadOnlyList<BiomeOverlayDescriptor> BiomeOverlayDescriptors() => new[]
		{
			new BiomeOverlayDescriptor("dry_grass", DryGrassOverlayTexturePath),
			new BiomeOverlayDescriptor("sand", SandOverlayTexturePath),
			new BiomeOverlayDescriptor("earth", EarthOverlayTexturePath),
			new BiomeOverlayDescriptor("rock", RockOverlayTexturePath),
			new BiomeOverlayDescriptor("snow_ice", SnowIceOverlayTexturePath),
		};

		private static bool HasVisiblePixels(byte[] pixels)
		{
			for (int offset = 3; offset < pixels.Length; offset += 4)
			{
				if (pixels[offset] != 0)
					return true;
			}
			return false;
		}

		private Texture2D? BuildFoamEdgeMask(byte[] waterPixels, int width, int height)
		{
			if (!UseAnimatedFoamEdges || string.IsNullOrWhiteSpace(FoamTexturePath) || FoamOpacity <= 0.0f)
				return null;

			int radius = Mathf.Clamp(FoamEdgeWidthPixels, 1, 16);
			byte[] foamPixels = new byte[waterPixels.Length];
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int offset = Offset(x, y, width);
					if (waterPixels[offset + 3] == 0)
						continue;

					bool shore = false;
					for (int distance = 1; distance <= radius && !shore; distance++)
					{
						shore = IsLandPixel(waterPixels, x - distance, y, width, height) ||
							IsLandPixel(waterPixels, x + distance, y, width, height) ||
							IsLandPixel(waterPixels, x, y - distance, width, height) ||
							IsLandPixel(waterPixels, x, y + distance, width, height) ||
							IsLandPixel(waterPixels, x - distance, y - distance, width, height) ||
							IsLandPixel(waterPixels, x + distance, y - distance, width, height) ||
							IsLandPixel(waterPixels, x - distance, y + distance, width, height) ||
							IsLandPixel(waterPixels, x + distance, y + distance, width, height);
					}

					if (!shore)
						continue;

					foamPixels[offset] = 255;
					foamPixels[offset + 1] = 255;
					foamPixels[offset + 2] = 255;
					foamPixels[offset + 3] = waterPixels[offset + 3];
					LastFoamPixelCount++;
				}
			}

			if (LastFoamPixelCount == 0)
				return null;

			var foamImage = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
			foamImage.SetData(width, height, false, Image.Format.Rgba8, foamPixels);
			return ImageTexture.CreateFromImage(foamImage);
		}

		private static bool IsLandPixel(byte[] waterPixels, int x, int y, int width, int height)
			=> x < 0 || y < 0 || x >= width || y >= height || waterPixels[Offset(x, y, width) + 3] == 0;

		private int EffectivePixelsPerTile(int widthTiles, int heightTiles)
		{
			int ppt = Mathf.Clamp(PixelsPerTile, 1, 64);
			if (MaxGeneratedPixels <= 0)
				return ppt;

			long cells = (long)Mathf.Max(1, widthTiles) * Mathf.Max(1, heightTiles);
			long requestedPixels = cells * ppt * ppt;
			if (requestedPixels <= MaxGeneratedPixels)
				return ppt;

			int capped = Mathf.FloorToInt(Mathf.Sqrt(MaxGeneratedPixels / (float)cells));
			return Mathf.Clamp(capped, 1, ppt);
		}

		private static void WritePixel(byte[] pixels, int x, int y, int width, Color colour)
		{
			int i = Offset(x, y, width);
			pixels[i] = ToByte(colour.R);
			pixels[i + 1] = ToByte(colour.G);
			pixels[i + 2] = ToByte(colour.B);
			pixels[i + 3] = ToByte(colour.A);
		}

		private static byte ToByte(float value)
			=> (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255.0f), 0, 255);

		private void ApplySmoothing(Image image)
		{
			int passes = Mathf.Clamp(SmoothingPasses, 0, 3);

			for (int pass = 0; pass < passes; pass++)
				SmoothImage(image);
		}

		private void ApplySharpen(Image image)
		{
			float amount = Mathf.Clamp(PostSharpenStrength, 0.0f, 1.0f);
			if (amount <= 0.0f)
				return;

			SharpenImage(image, amount);
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

		private static void SharpenImage(Image image, float amount)
		{
			int width = image.GetWidth();
			int height = image.GetHeight();
			if (width < 3 || height < 3)
				return;

			byte[] source = image.GetData();
			byte[] target = new byte[source.Length];
			Array.Copy(source, target, source.Length);

			for (int y = 1; y < height - 1; y++)
			{
				for (int x = 1; x < width - 1; x++)
				{
					int centre = Offset(x, y, width);
					int left = Offset(x - 1, y, width);
					int right = Offset(x + 1, y, width);
					int up = Offset(x, y - 1, width);
					int down = Offset(x, y + 1, width);

					for (int channel = 0; channel < 3; channel++)
					{
						float blur = (source[left + channel] + source[right + channel] + source[up + channel] + source[down + channel]) * 0.25f;
						float sharpened = source[centre + channel] + ((source[centre + channel] - blur) * amount);
						target[centre + channel] = (byte)Mathf.Clamp(Mathf.RoundToInt(sharpened), 0, 255);
					}
				}
			}

			image.SetData(width, height, false, Image.Format.Rgba8, target);
		}

		private static int Offset(int x, int y, int width) => ((y * width) + x) * 4;

		private void ApplyTexture(Texture2D texture, Texture2D? detailTexture, int tileSize, int pixelsPerTile)
		{
			_sprite ??= EnsureSprite();
			_sprite.Texture = texture;
			_sprite.TextureFilter = TerrainTextureFilter;
			_sprite.Centered = Centered;
			_sprite.Position = RenderOffset;
			_sprite.ZIndex = RenderZIndex;
			LastAppliedTerrainScale = new Vector2((float)tileSize / pixelsPerTile, (float)tileSize / pixelsPerTile);
			_sprite.Scale = LastAppliedTerrainScale;
			_sprite.Material = null;

			if (detailTexture is null)
			{
				if (_detailSprite is not null)
					_detailSprite.Visible = false;

				_sprite.Material = AnimateWater ? WaterMaterial() : null;
				return;
			}

			_detailSprite ??= EnsureDetailSprite();
			_detailSprite.Texture = detailTexture;
			_detailSprite.TextureFilter = TerrainTextureFilter;
			_detailSprite.Centered = Centered;
			_detailSprite.Position = RenderOffset;
			_detailSprite.ZIndex = RenderZIndex + DetailLayerZOffset;
			_detailSprite.Scale = LastAppliedTerrainScale;
			_detailSprite.Material = AnimateWater ? WaterMaterial() : null;
			_detailSprite.Visible = true;
		}

		private void ApplyLayerTextures(
			Texture2D baseTexture,
			Texture2D? grassMaskTexture,
			Texture2D? grassTexture,
			IReadOnlyList<BiomeTextureOverlay> biomeOverlays,
			Texture2D detailTexture,
			Texture2D waterTexture,
			Texture2D? foamMaskTexture,
			Texture2D? foamTexture,
			Texture2D gameplayTexture,
			int tileSize,
			int pixelsPerTile)
		{
			LastAppliedTerrainScale = new Vector2((float)tileSize / pixelsPerTile, (float)tileSize / pixelsPerTile);
			ConfigureLayer(_sprite ??= EnsureSprite(), baseTexture, RenderZIndex, false);
			ConfigureGrassOverlayLayer(grassMaskTexture, grassTexture);
			ConfigureBiomeOverlayLayers(biomeOverlays);
			ConfigureLayer(_detailSprite ??= EnsureDetailSprite(), detailTexture, RenderZIndex + DetailLayerZOffset + 2, false);
			ConfigureLayer(_waterSprite ??= EnsureWaterSprite(), waterTexture, RenderZIndex + DetailLayerZOffset + 3, AnimateWater);
			ConfigureFoamLayer(foamMaskTexture, foamTexture);
			ConfigureLayer(_gameplaySprite ??= EnsureGameplaySprite(), gameplayTexture, RenderZIndex + DetailLayerZOffset + 5, false);
		}

		private void ConfigureGrassOverlayLayer(Texture2D? grassMaskTexture, Texture2D? grassTexture)
		{
			if (grassMaskTexture is null || grassTexture is null)
			{
				if (_grassOverlaySprite is not null)
					_grassOverlaySprite.Visible = false;
				return;
			}

			Sprite2D sprite = _grassOverlaySprite ??= EnsureGrassOverlaySprite();
			sprite.Texture = grassMaskTexture;
			sprite.TextureFilter = TerrainTextureFilter;
			sprite.Centered = Centered;
			sprite.Position = RenderOffset;
			sprite.ZIndex = RenderZIndex + DetailLayerZOffset;
			sprite.Scale = LastAppliedTerrainScale;
			sprite.Material = GrassOverlayMaterial(grassTexture);
			sprite.Visible = true;
		}

		private void ConfigureBiomeOverlayLayers(IReadOnlyList<BiomeTextureOverlay> overlays)
		{
			var active = new HashSet<string>(StringComparer.Ordinal);
			foreach (BiomeTextureOverlay overlay in overlays)
			{
				active.Add(overlay.Key);
				if (!_biomeOverlaySprites.TryGetValue(overlay.Key, out Sprite2D? sprite) || !GodotObject.IsInstanceValid(sprite))
				{
					sprite = EnsureBiomeOverlaySprite(overlay.Key);
					_biomeOverlaySprites[overlay.Key] = sprite;
				}

				sprite.Texture = overlay.MaskTexture;
				sprite.TextureFilter = TerrainTextureFilter;
				sprite.Centered = Centered;
				sprite.Position = RenderOffset;
				sprite.ZIndex = RenderZIndex + DetailLayerZOffset + 1;
				sprite.Scale = LastAppliedTerrainScale;
				sprite.Material = BiomeOverlayMaterial(overlay.Key, overlay.Texture);
				sprite.Visible = true;
			}

			foreach ((string key, Sprite2D sprite) in _biomeOverlaySprites)
			{
				if (GodotObject.IsInstanceValid(sprite) && !active.Contains(key))
					sprite.Visible = false;
			}
		}

		private void ConfigureFoamLayer(Texture2D? foamMaskTexture, Texture2D? foamTexture)
		{
			if (foamMaskTexture is null || foamTexture is null)
			{
				if (_foamSprite is not null)
					_foamSprite.Visible = false;
				return;
			}

			Sprite2D sprite = _foamSprite ??= EnsureFoamSprite();
			sprite.Texture = foamMaskTexture;
			sprite.TextureFilter = TerrainTextureFilter;
			sprite.Centered = Centered;
			sprite.Position = RenderOffset;
			sprite.ZIndex = RenderZIndex + DetailLayerZOffset + 4;
			sprite.Scale = LastAppliedTerrainScale;
			sprite.Material = FoamMaterial(foamTexture);
			sprite.Visible = true;
		}

		private void ConfigureLayer(Sprite2D sprite, Texture2D texture, int zIndex, bool animate)
		{
			sprite.Texture = texture;
			sprite.TextureFilter = TerrainTextureFilter;
			sprite.Centered = Centered;
			sprite.Position = RenderOffset;
			sprite.ZIndex = zIndex;
			sprite.Scale = LastAppliedTerrainScale;
			sprite.Material = animate ? WaterMaterial() : null;
			sprite.Visible = true;
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

		private Sprite2D EnsureDetailSprite()
		{
			if (GetNodeOrNull<Sprite2D>("PainterlyTerrainDetailSprite") is { } existing)
				return existing;

			var sprite = new Sprite2D
			{
				Name = "PainterlyTerrainDetailSprite",
				Centered = Centered,
				ZIndex = RenderZIndex + DetailLayerZOffset + 2,
			};
			AddChild(sprite);

			if (Engine.IsEditorHint())
				sprite.Owner = GetTree()?.EditedSceneRoot;

			return sprite;
		}

		private Sprite2D EnsureWaterSprite()
		{
			if (GetNodeOrNull<Sprite2D>("PainterlyTerrainWaterSprite") is { } existing)
				return existing;

			var sprite = new Sprite2D
			{
				Name = "PainterlyTerrainWaterSprite",
				Centered = Centered,
				ZIndex = RenderZIndex + DetailLayerZOffset + 3,
			};
			AddChild(sprite);
			if (Engine.IsEditorHint())
				sprite.Owner = GetTree()?.EditedSceneRoot;
			return sprite;
		}

		private Sprite2D EnsureGameplaySprite()
		{
			if (GetNodeOrNull<Sprite2D>("PainterlyTerrainGameplaySprite") is { } existing)
				return existing;

			var sprite = new Sprite2D
			{
				Name = "PainterlyTerrainGameplaySprite",
				Centered = Centered,
				ZIndex = RenderZIndex + DetailLayerZOffset + 5,
			};
			AddChild(sprite);
			if (Engine.IsEditorHint())
				sprite.Owner = GetTree()?.EditedSceneRoot;
			return sprite;
		}

		private Sprite2D EnsureGrassOverlaySprite()
		{
			if (GetNodeOrNull<Sprite2D>("PainterlyTerrainGrassOverlaySprite") is { } existing)
				return existing;

			var sprite = new Sprite2D
			{
				Name = "PainterlyTerrainGrassOverlaySprite",
				Centered = Centered,
				ZIndex = RenderZIndex + DetailLayerZOffset,
			};
			AddChild(sprite);
			if (Engine.IsEditorHint())
				sprite.Owner = GetTree()?.EditedSceneRoot;
			return sprite;
		}

		private Sprite2D EnsureFoamSprite()
		{
			if (GetNodeOrNull<Sprite2D>("PainterlyTerrainFoamSprite") is { } existing)
				return existing;

			var sprite = new Sprite2D
			{
				Name = "PainterlyTerrainFoamSprite",
				Centered = Centered,
				ZIndex = RenderZIndex + DetailLayerZOffset + 2,
			};
			AddChild(sprite);
			if (Engine.IsEditorHint())
				sprite.Owner = GetTree()?.EditedSceneRoot;
			return sprite;
		}

		private Sprite2D EnsureBiomeOverlaySprite(string key)
		{
			string nodeName = $"PainterlyTerrain{BiomeOverlayNodeSuffix(key)}OverlaySprite";
			if (GetNodeOrNull<Sprite2D>(nodeName) is { } existing)
				return existing;

			var sprite = new Sprite2D
			{
				Name = nodeName,
				Centered = Centered,
				ZIndex = RenderZIndex + DetailLayerZOffset + 1,
			};
			AddChild(sprite);
			if (Engine.IsEditorHint())
				sprite.Owner = GetTree()?.EditedSceneRoot;
			return sprite;
		}

		private static string BiomeOverlayNodeSuffix(string key) => key switch
		{
			"dry_grass" => "DryGrass",
			"sand" => "Sand",
			"earth" => "Earth",
			"rock" => "Rock",
			"snow_ice" => "SnowIce",
			_ => "Biome",
		};

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
				_ when moisture < Dryness => DryGrassBaseColour,
				_ => GrassBaseColour,
			};
		}

		private Color BaseColour(TerrainPreset preset) => preset switch
		{
			TerrainPreset.Desert => DesertBaseColour,
			TerrainPreset.Sand => SandBaseColour,
			TerrainPreset.Ice => IceBaseColour,
			TerrainPreset.Sea => WaterBaseColour,
			TerrainPreset.Rock => RockBaseColour,
			TerrainPreset.Lava => new Color(0.24f, 0.08f, 0.05f),
			TerrainPreset.Swamp => SwampBaseColour,
			TerrainPreset.Snow => SnowBaseColour,
			_ => GrassBaseColour,
		};

		private static bool KeepsPlainBaseColour(string terrainKind)
		{
			string kind = NormalizeTerrainKind(terrainKind);
			return kind is "grass" or "grassland" or "dry_grass";
		}

		private static int TerrainKindSeedOffset(string normalizedKind) => normalizedKind switch
		{
			"grass" or "grassland" => 101,
			"dry_grass" => 127,
			"forest" or "woods" => 149,
			"desert" => 173,
			"sand" or "beach" => 191,
			"dirt" or "soil" or "earth" => 211,
			"mud" or "swamp" => 229,
			"rock" or "stone" or "gravel" => 251,
			"snow" => 277,
			"ice" => 293,
			"lava" => 307,
			_ => 331,
		};

		private Color PlainBaseColourFor(string terrainKind, Color fallback)
		{
			return NormalizeTerrainKind(terrainKind) switch
			{
				"grass" or "grassland" => GrassBaseColour,
				"dry_grass" => DryGrassBaseColour,
				"desert" => DesertBaseColour,
				"sand" or "beach" => SandBaseColour,
				"dirt" or "soil" or "earth" => new Color(0.42f, 0.29f, 0.16f),
				"mud" or "swamp" => SwampBaseColour,
				"rock" or "stone" or "gravel" => RockBaseColour,
				"snow" => SnowBaseColour,
				"ice" => IceBaseColour,
				"water" or "sea" or "ocean" or "shallow_water" or "deep_water" => WaterBaseColour,
				_ => fallback,
			};
		}

		private static float StateOverlayStrength(string terrainKind, TerrainPaintEffect effect)
		{
			if (effect == TerrainPaintEffect.Water)
				return 1.0f;

			return KeepsPlainBaseColour(terrainKind) ? 0.96f : 0.72f;
		}

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

		private string TerrainKindFor(TerrainPreset preset, float height, float moisture)
		{
			if (preset == TerrainPreset.Sea)
				return height < 0.35f ? "deep_water" : "water";
			if (preset == TerrainPreset.Ice)
				return "ice";
			if (preset == TerrainPreset.Lava)
				return "lava";
			if (height < WaterLevel)
				return "water";
			if (height < WaterLevel + BeachWidth)
				return preset == TerrainPreset.Desert ? "sand" : "beach";
			if (height >= RockLevel)
				return preset == TerrainPreset.Snow ? "snow" : "rock";

			return preset switch
			{
				TerrainPreset.Desert => "desert",
				TerrainPreset.Sand => "sand",
				TerrainPreset.Rock => "rock",
				TerrainPreset.Swamp => moisture > 0.62f ? "swamp" : "mud",
				TerrainPreset.Snow => "snow",
				_ when moisture < Dryness => "dry_grass",
				_ => "grass",
			};
		}

		private static TerrainPaintSample ToTerrainPaintSample(PaintSample sample)
		{
			string terrainKind = NormalizeTerrainKind(sample.TerrainKind);
			return new TerrainPaintSample(
				terrainKind,
				sample.Colour,
				sample.Effect,
				sample.Effect == TerrainPaintEffect.Water ? 1.0f : 0.0f,
				sample.EdgeAmount);
		}

		private float GroundDetailRegionMask(TerrainPaintSample paint, Vector2 at)
		{
			float sampleMask = Mathf.Clamp(paint.GroundDetailMask, 0.0f, 1.0f);
			float proceduralMask = BiomeDetailPatchMask(NormalizeTerrainKind(paint.TerrainKind), at);
			return GroundDetailRegionMode switch
			{
				DetailRegionMode.ProceduralPatches => proceduralMask,
				DetailRegionMode.SampleAndProceduralPatches => sampleMask * proceduralMask,
				_ => sampleMask,
			};
		}

		private Color GroundDetailPixel(TerrainPaintSample paint, Vector2 at, int x, int y)
		{
			if (!EnableBiomeDetailLayers || paint.Effect == TerrainPaintEffect.Water || paint.WaterAmount > 0.0f)
				return Colors.Transparent;

			float region = GroundDetailRegionMask(paint, at);
			if (region <= 0.0f || Hash01(x, y, Seed + 13001) > region)
				return Colors.Transparent;

			float strength = Mathf.Clamp(BiomeDetailStrength, 0.0f, 1.0f);
			float density = Mathf.Clamp(BiomeDetailDensity, 0.0f, 2.0f);
			if (strength <= 0.0f || density <= 0.0f)
				return Colors.Transparent;

			string kind = NormalizeTerrainKind(paint.TerrainKind);
			Color colour;
			bool mark;
			switch (kind)
			{
				case "grass":
				case "grassland":
				case "dry_grass":
					float grassTuft = SpotMask(at, Seed + 14011, 7.0f, 0.075f * density);
					float broadPatch = SpotMask(at + new Vector2(3.2f, 1.8f), Seed + 14023, 0.75f, 0.50f * density);
					float flower = SpotMask(at + new Vector2(1.3f, 2.7f), Seed + 14029, 11.0f, 0.012f * density);
					bool flowerMark = flower > 0.94f && Hash01(x, y, Seed + 14031) < FlowerLayerStrength;
					mark = grassTuft > 0.84f || broadPatch > 0.66f || flowerMark;
					if (flowerMark)
					{
						float roll = Hash01(x / 2, y / 2, Seed + 14037);
						colour = roll > 0.66f ? new Color(0.92f, 0.94f, 0.72f) : roll > 0.33f ? new Color(0.32f, 0.70f, 0.92f) : new Color(0.96f, 0.80f, 0.30f);
					}
					else
					{
						colour = kind == "dry_grass" ? new Color(0.31f, 0.42f, 0.13f) : new Color(0.22f, 0.47f, 0.08f);
					}
					break;

				case "desert":
				case "sand":
				case "beach":
					float dune = Mathf.Abs(Mathf.Sin((at.X * 2.1f) + (at.Y * 0.7f) + (Seed * 0.013f)));
					float pebble = SpotMask(at, Seed + 14101, 5.2f, 0.045f * density);
					mark = (dune > 0.993f && ValueNoise(at.X * 0.48f, at.Y * 0.48f, Seed + 14107) > 0.75f) || pebble > 0.80f;
					colour = pebble > 0.80f ? new Color(0.43f, 0.33f, 0.21f) : new Color(0.78f, 0.57f, 0.30f);
					break;

				case "rock":
				case "stone":
				case "gravel":
					float rock = SpotMask(at, Seed + 14201, 5.8f, 0.06f * density);
					mark = rock > 0.76f;
					colour = new Color(0.22f, 0.23f, 0.20f);
					break;

				default:
					float earth = SpotMask(at, Seed + 14301, 6.4f, 0.05f * density);
					mark = earth > 0.78f;
					colour = new Color(0.28f, 0.20f, 0.11f);
					break;
			}

			if (!mark || Hash01(x, y, Seed + 14399) > strength)
				return Colors.Transparent;

			colour.A = 1.0f;
			return colour;
		}

		private Color WaterLayerPixel(TerrainPaintSample paint, int x, int y)
		{
			if (paint.Effect != TerrainPaintEffect.Water && paint.WaterAmount <= 0.0f)
				return Colors.Transparent;

			Color water = ApplyWater(
				PlainBaseColourFor(paint.TerrainKind, paint.BaseColour),
				Mathf.Clamp(paint.WaterEdgeAmount, 0.0f, 1.0f),
				x,
				y);
			water.A *= Mathf.Clamp(paint.WaterAmount <= 0.0f ? 1.0f : paint.WaterAmount, 0.0f, 1.0f);
			return water;
		}

		private static Color GameplayOverlayPixel(TerrainPaintSample paint, Vector2 at)
		{
			Vector2 local = new(Repeat(at.X), Repeat(at.Y));
			float dx = Mathf.Abs(local.X - 0.5f);
			float dy = Mathf.Abs(local.Y - 0.5f);
			bool inner = local.X > 0.12f && local.X < 0.88f && local.Y > 0.12f && local.Y < 0.88f;

			if (!string.IsNullOrWhiteSpace(paint.RoadKind))
				return dx < 0.16f || dy < 0.16f ? RoadOverlayColour(paint.RoadKind) : Colors.Transparent;

			var flags = (GridCellDataComponent.CellFlags)paint.CellFlags;
			if ((flags & GridCellDataComponent.CellFlags.Blocked) != 0)
				return inner && (Mathf.Abs(local.X - local.Y) < 0.045f || Mathf.Abs((local.X + local.Y) - 1.0f) < 0.045f)
					? new Color(0.17f, 0.17f, 0.16f, 1.0f)
					: Colors.Transparent;
			if ((flags & GridCellDataComponent.CellFlags.HarvestReady) != 0)
				return inner && Repeat(local.X * 5.0f) < 0.24f ? new Color(0.77f, 0.63f, 0.20f, 1.0f) : Colors.Transparent;
			if ((flags & GridCellDataComponent.CellFlags.Planted) != 0)
				return inner && Repeat(local.X * 4.0f) < 0.28f ? new Color(0.18f, 0.42f, 0.16f, 1.0f) : Colors.Transparent;
			if ((flags & GridCellDataComponent.CellFlags.Watered) != 0)
				return inner && Repeat((local.X + local.Y) * 5.0f) < 0.16f ? new Color(0.16f, 0.34f, 0.46f, 1.0f) : Colors.Transparent;
			if ((flags & GridCellDataComponent.CellFlags.Tilled) != 0)
				return inner && Repeat(local.Y * 5.0f) < 0.18f ? new Color(0.35f, 0.22f, 0.12f, 1.0f) : Colors.Transparent;
			if ((flags & GridCellDataComponent.CellFlags.Cleared) != 0)
				return inner && Repeat((local.X * 3.0f) + (local.Y * 2.0f)) < 0.11f ? new Color(0.50f, 0.38f, 0.22f, 1.0f) : Colors.Transparent;
			return Colors.Transparent;
		}

		private static Color RoadOverlayColour(string roadKind)
		{
			return NormalizeTerrainKind(roadKind) switch
			{
				"stone" or "stone_path" or "paved" => new Color(0.43f, 0.40f, 0.34f, 1.0f),
				"sand" or "sand_path" => new Color(0.60f, 0.49f, 0.30f, 1.0f),
				"asphalt" => new Color(0.18f, 0.18f, 0.17f, 1.0f),
				_ => new Color(0.48f, 0.34f, 0.19f, 1.0f),
			};
		}

		private Color ApplyBiomeDetail(Color colour, string terrainKind, Vector2 at, int x, int y, TerrainPaintEffect effect)
		{
			float strength = Mathf.Clamp(BiomeDetailStrength, 0.0f, 1.0f);
			if (!EnableBiomeDetailLayers || strength <= 0.0f)
				return colour;

			string kind = NormalizeTerrainKind(terrainKind);
			float mask = BiomeDetailPatchMask(kind, at);
			if (mask <= 0.0f)
				return colour;

			strength *= mask;
			float density = Mathf.Clamp(BiomeDetailDensity, 0.0f, 2.0f);
			return kind switch
			{
				"desert" or "sand" or "beach" => ApplySandDetail(colour, at, x, y, strength, density),
				"grass" or "grassland" or "dry_grass" => ApplyGrassDetail(colour, at, x, y, strength, density, kind == "dry_grass"),
				"forest" or "woods" => ApplyForestDetail(colour, at, x, y, strength, density),
				"dirt" or "soil" or "earth" or "mud" or "swamp" => ApplyEarthDetail(colour, at, x, y, strength, density, kind == "swamp"),
				"rock" or "stone" or "gravel" => ApplyRockDetail(colour, at, x, y, strength, density),
				"snow" or "ice" => ApplyColdDetail(colour, at, x, y, strength, density, kind == "ice"),
				"lava" => ApplyLavaDetail(colour, at, x, y, strength),
				_ when effect == TerrainPaintEffect.Water => colour,
				_ => ApplyGrassDetail(colour, at, x, y, strength * 0.8f, density, dry: false),
			};
		}

		private float BiomeDetailPatchMask(string normalizedKind, Vector2 at)
		{
			float coverage = Mathf.Clamp(BiomeDetailCoverage, 0.0f, 1.0f);
			if (coverage <= 0.0f)
				return 0.0f;
			if (coverage >= 0.999f)
				return 1.0f;

			float scale = Mathf.Max(0.02f, BiomeDetailPatchScale);
			int seedOffset = TerrainKindSeedOffset(normalizedKind);
			float primary = SpotMask(at, Seed + 12007 + seedOffset, scale, coverage);
			float secondary = SpotMask(at + new Vector2(11.3f, -7.9f), Seed + 12119 + seedOffset, scale * 0.73f, coverage * 0.65f);
			float mask = Mathf.Max(primary, secondary * 0.85f);
			float softness = Mathf.Clamp(BiomeDetailPatchSoftness, 0.01f, 0.5f);
			return SmoothStep(softness * 0.25f, 1.0f, mask);
		}

		private Color ApplySandDetail(Color colour, Vector2 at, int x, int y, float strength, float density)
		{
			float duneStrength = Mathf.Clamp(DuneLayerStrength, 0.0f, 1.0f);
			float pebbleStrength = Mathf.Clamp(PebbleLayerStrength, 0.0f, 1.0f);
			float wave = Mathf.Sin((at.X * 1.75f) + (at.Y * 0.85f) + (Seed * 0.013f));
			float broken = SmoothStep(0.24f, 0.86f, ValueNoise(at.X * 0.45f, at.Y * 0.45f, Seed + 9101));
			float ridge = Mathf.Pow(Mathf.Max(0.0f, 1.0f - Mathf.Abs(wave) * 1.75f), 1.75f) * broken;
			float trough = SmoothStep(0.70f, 1.0f, Mathf.Abs(wave)) * (1.0f - (broken * 0.45f));

			colour = colour.Lightened(ridge * strength * duneStrength * 0.18f);
			colour = colour.Darkened(trough * strength * duneStrength * 0.10f);

			float pebble = SpotMask(at, Seed + 9301, 3.75f, 0.22f * density);
			float darkPebble = pebble * Hash01(x / 2, y / 2, Seed + 9307);
			colour = MixPreserveAlpha(colour, new Color(0.39f, 0.31f, 0.20f), darkPebble * strength * pebbleStrength * 0.55f);

			float paleDust = SpotMask(at + new Vector2(7.1f, 2.3f), Seed + 9311, 2.5f, 0.16f * density);
			return MixPreserveAlpha(colour, new Color(0.84f, 0.70f, 0.45f), paleDust * strength * duneStrength * 0.18f);
		}

		private Color ApplyGrassDetail(Color colour, Vector2 at, int x, int y, float strength, float density, bool dry)
		{
			float vegetation = Mathf.Clamp(VegetationLayerStrength, 0.0f, 1.0f);
			float fineClump = SpotMask(at, Seed + 9401, dry ? 4.2f : 5.4f, (dry ? 0.14f : 0.16f) * density);
			float broadClump = SmoothStep(0.58f, 0.92f, ValueNoise(at.X * 0.85f, at.Y * 0.85f, Seed + 9409));
			Color dark = dry ? new Color(0.31f, 0.36f, 0.16f) : new Color(0.28f, 0.49f, 0.12f);
			Color light = dry ? new Color(0.48f, 0.54f, 0.20f) : new Color(0.42f, 0.68f, 0.13f);

			colour = MixPreserveAlpha(colour, dark, broadClump * strength * 0.32f);
			colour = MixPreserveAlpha(colour, dark.Darkened(0.18f), fineClump * strength * vegetation * 0.48f);
			colour = MixPreserveAlpha(colour, light, SmoothStep(0.78f, 0.96f, ValueNoise(at.X * 0.55f, at.Y * 0.55f, Seed + 9413)) * strength * 0.025f);

			float flower = SpotMask(at + new Vector2(1.7f, 4.9f), Seed + 9419, 7.25f, (dry ? 0.02f : 0.052f) * density);
			float flowerRoll = Hash01(x / 3, y / 3, Seed + 9421);
			Color flowerColour = flowerRoll > 0.66f
				? new Color(0.86f, 0.92f, 0.72f)
				: flowerRoll > 0.34f
					? new Color(0.36f, 0.72f, 0.92f)
					: new Color(0.90f, 0.80f, 0.30f);
			return MixPreserveAlpha(colour, flowerColour, flower * strength * Mathf.Clamp(FlowerLayerStrength, 0.0f, 1.0f) * 1.35f);
		}

		private Color ApplyForestDetail(Color colour, Vector2 at, int x, int y, float strength, float density)
		{
			float shadowMass = SmoothStep(0.36f, 0.82f, ValueNoise(at.X * 1.15f, at.Y * 1.15f, Seed + 9501));
			float leafFleck = SpotMask(at, Seed + 9509, 5.0f, 0.42f * density);
			colour = MixPreserveAlpha(colour, new Color(0.09f, 0.24f, 0.10f), shadowMass * strength * 0.25f);
			return MixPreserveAlpha(colour, new Color(0.34f, 0.54f, 0.19f), leafFleck * strength * 0.25f);
		}

		private Color ApplyEarthDetail(Color colour, Vector2 at, int x, int y, float strength, float density, bool swamp)
		{
			float patch = SpotMask(at, Seed + 9601, swamp ? 3.2f : 4.4f, (swamp ? 0.36f : 0.25f) * density);
			float damp = SmoothStep(0.56f, 0.90f, ValueNoise(at.X * 1.8f, at.Y * 1.8f, Seed + 9609));
			colour = MixPreserveAlpha(colour, swamp ? new Color(0.07f, 0.20f, 0.13f) : new Color(0.24f, 0.16f, 0.09f), patch * strength * 0.32f);
			return MixPreserveAlpha(colour, new Color(0.46f, 0.35f, 0.20f), damp * strength * 0.14f);
		}

		private Color ApplyRockDetail(Color colour, Vector2 at, int x, int y, float strength, float density)
		{
			float pebble = SpotMask(at, Seed + 9701, 4.7f, 0.45f * density);
			float crackWave = Mathf.Sin((at.X * 4.7f) - (at.Y * 2.3f) + (Seed * 0.021f));
			float crack = Mathf.Pow(Mathf.Max(0.0f, 1.0f - Mathf.Abs(crackWave) * 6.0f), 2.0f);
			crack *= SmoothStep(0.42f, 0.86f, ValueNoise(at.X * 1.4f, at.Y * 1.4f, Seed + 9709));

			colour = MixPreserveAlpha(colour, new Color(0.21f, 0.22f, 0.20f), crack * strength * 0.42f);
			return MixPreserveAlpha(colour, new Color(0.49f, 0.50f, 0.45f), pebble * strength * Mathf.Clamp(PebbleLayerStrength, 0.0f, 1.0f) * 0.32f);
		}

		private Color ApplyColdDetail(Color colour, Vector2 at, int x, int y, float strength, float density, bool ice)
		{
			float scratchWave = Mathf.Sin((at.X * 3.8f) + (at.Y * 5.6f) + (Seed * 0.017f));
			float scratch = Mathf.Pow(Mathf.Max(0.0f, 1.0f - Mathf.Abs(scratchWave) * 5.5f), 2.0f);
			float drift = SmoothStep(0.54f, 0.92f, ValueNoise(at.X * 0.9f, at.Y * 0.9f, Seed + 9801));
			colour = MixPreserveAlpha(colour, ice ? new Color(0.43f, 0.72f, 0.82f) : new Color(0.62f, 0.69f, 0.72f), scratch * strength * 0.22f * density);
			return MixPreserveAlpha(colour, Colors.White, drift * strength * 0.16f);
		}

		private Color ApplyLavaDetail(Color colour, Vector2 at, int x, int y, float strength)
		{
			float veinWave = Mathf.Sin((at.X * 5.8f) + (at.Y * 1.7f) + (Seed * 0.03f));
			float vein = Mathf.Pow(Mathf.Max(0.0f, 1.0f - Mathf.Abs(veinWave) * 4.0f), 2.4f);
			vein *= SmoothStep(0.38f, 0.80f, ValueNoise(at.X * 1.1f, at.Y * 1.1f, Seed + 9901));
			return MixPreserveAlpha(colour, new Color(0.92f, 0.31f, 0.08f), vein * strength * 0.55f);
		}

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

		private float WaterEdgeForHeight(TerrainPaintEffect effect, float height)
		{
			if (effect != TerrainPaintEffect.Water || Preset == TerrainPreset.Sea)
				return 0.0f;

			float shoreWidth = Mathf.Max(0.001f, BeachWidth);
			return 1.0f - SmoothStep(0.0f, shoreWidth, Mathf.Abs(height - WaterLevel));
		}

		private static float WaterEdgeAmount(
			TerrainPaintEffect centre,
			TerrainPaintEffect left,
			TerrainPaintEffect right,
			TerrainPaintEffect up,
			TerrainPaintEffect down)
		{
			if (centre != TerrainPaintEffect.Water)
				return 0.0f;

			int solidNeighbours = 0;
			if (left != TerrainPaintEffect.Water) solidNeighbours++;
			if (right != TerrainPaintEffect.Water) solidNeighbours++;
			if (up != TerrainPaintEffect.Water) solidNeighbours++;
			if (down != TerrainPaintEffect.Water) solidNeighbours++;

			return solidNeighbours / 4.0f;
		}

		private static int CellIndex(int x, int y, int width) => (y * width) + x;

		private static TerrainPaintEffect EffectAt(PaintSample[] cells, int x, int y, int width, int height)
		{
			x = Mathf.Clamp(x, 0, width - 1);
			y = Mathf.Clamp(y, 0, height - 1);
			return cells[CellIndex(x, y, width)].Effect;
		}

		private static TerrainPaintEffect EffectAt(TerrainPaintSample[] cells, int x, int y, int width, int height)
		{
			x = Mathf.Clamp(x, 0, width - 1);
			y = Mathf.Clamp(y, 0, height - 1);
			return cells[CellIndex(x, y, width)].Effect;
		}

		private static PaintSample BlendPaintCells(PaintSample[] cells, Vector2 at, int x, int y, int width, int height)
		{
			PaintSample centre = cells[CellIndex(x, y, width)];
			int right = Mathf.Min(width - 1, x + 1);
			int down = Mathf.Min(height - 1, y + 1);

			float tx = SmoothStep(0.0f, 1.0f, at.X - Mathf.Floor(at.X));
			float ty = SmoothStep(0.0f, 1.0f, at.Y - Mathf.Floor(at.Y));

			Color top = centre.Colour.Lerp(cells[CellIndex(right, y, width)].Colour, tx);
			Color bottom = cells[CellIndex(x, down, width)].Colour.Lerp(cells[CellIndex(right, down, width)].Colour, tx);
			return centre with { Colour = top.Lerp(bottom, ty) };
		}

		private ShaderMaterial WaterMaterial()
		{
			_waterMaterial ??= new ShaderMaterial
			{
				Shader = new Shader { Code = WaterShaderCode },
			};

			_waterMaterial.SetShaderParameter("water_scroll_speed", WaterScrollSpeed);
			_waterMaterial.SetShaderParameter("water_ripple_strength", WaterRippleStrength);
			Texture2D? overlay = UseRepeatedWaterOverlay ? LoadTexture2D(WaterOverlayTexturePath) : null;
			if (overlay is not null)
				_waterMaterial.SetShaderParameter("water_overlay_texture", overlay);
			_waterMaterial.SetShaderParameter("water_overlay_strength", overlay is null ? 0.0f : Mathf.Clamp(WaterOverlayStrength, 0.0f, 1.0f));
			_waterMaterial.SetShaderParameter("water_overlay_texture_size", Mathf.Max(32.0f, WaterOverlayTextureSize));

			return _waterMaterial;
		}

		private ShaderMaterial? _waterMaterial;

		private ShaderMaterial FoamMaterial(Texture2D foamTexture)
		{
			_foamMaterial ??= new ShaderMaterial
			{
				Shader = new Shader { Code = FoamShaderCode },
			};

			_foamMaterial.SetShaderParameter("foam_texture", foamTexture);
			_foamMaterial.SetShaderParameter("foam_opacity", Mathf.Clamp(FoamOpacity, 0.0f, 1.0f));
			_foamMaterial.SetShaderParameter("foam_frames_per_second", Mathf.Max(0.1f, FoamFramesPerSecond));
			_foamMaterial.SetShaderParameter("foam_frame_count", Mathf.Max(1, FoamFrameCount));
			_foamMaterial.SetShaderParameter("foam_tiles_per_repeat", Mathf.Max(0.1f, FoamTilesPerRepeat));
			return _foamMaterial;
		}

		private static Texture2D? LoadTexture2D(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return null;

			if (path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("uid://", StringComparison.Ordinal))
				return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

			Image image = Image.LoadFromFile(path);
			return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
		}

		private ShaderMaterial? _foamMaterial;

		private ShaderMaterial GrassOverlayMaterial(Texture2D grassTexture)
		{
			_grassOverlayMaterial ??= new ShaderMaterial
			{
				Shader = new Shader { Code = GrassOverlayShaderCode },
			};

			_grassOverlayMaterial.SetShaderParameter("overlay_tex", grassTexture);
			_grassOverlayMaterial.SetShaderParameter("overlay_strength", Mathf.Clamp(GrassOverlayStrength, 0.0f, 1.0f));
			_grassOverlayMaterial.SetShaderParameter("overlay_texture_size", Mathf.Max(32.0f, GrassOverlayTextureSize));
			return _grassOverlayMaterial;
		}

		private ShaderMaterial BiomeOverlayMaterial(string key, Texture2D texture)
		{
			if (!_biomeOverlayMaterials.TryGetValue(key, out ShaderMaterial? material))
			{
				material = new ShaderMaterial { Shader = new Shader { Code = GrassOverlayShaderCode } };
				_biomeOverlayMaterials[key] = material;
			}

			material.SetShaderParameter("overlay_tex", texture);
			material.SetShaderParameter("overlay_strength", Mathf.Clamp(BiomeOverlayStrength, 0.0f, 1.0f));
			material.SetShaderParameter("overlay_texture_size", Mathf.Max(32.0f, BiomeOverlayTextureSize));
			return material;
		}

		private ShaderMaterial? _grassOverlayMaterial;

		private const string WaterShaderCode = @"
shader_type canvas_item;

uniform vec2 water_scroll_speed = vec2(0.018, 0.011);
uniform float water_ripple_strength : hint_range(0.0, 1.0) = 0.12;
uniform sampler2D water_overlay_texture : repeat_enable, filter_linear;
uniform float water_overlay_strength : hint_range(0.0, 1.0) = 0.0;
uniform float water_overlay_texture_size : hint_range(32.0, 4096.0) = 1024.0;
varying vec2 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
	if (tex.a <= 0.0) {
		discard;
	}
    if (tex.a < 0.99) {
        vec2 moved = UV + TIME * water_scroll_speed;
        float wave =
            sin((moved.x * 36.0) + (moved.y * 18.0)) * 0.45 +
            sin((moved.x * -20.0) + (moved.y * 42.0)) * 0.35 +
            sin((moved.x * 70.0) + TIME * 0.6) * 0.20;
        tex.rgb += wave * water_ripple_strength * 0.055;
    }
	if (water_overlay_strength > 0.0) {
		vec3 overlay = texture(water_overlay_texture, world_position / water_overlay_texture_size).rgb;
		tex.rgb = mix(tex.rgb, overlay, water_overlay_strength);
	}
    COLOR = tex;
}
";

		private const string FoamShaderCode = @"
shader_type canvas_item;

uniform sampler2D foam_texture : source_color;
uniform float foam_opacity : hint_range(0.0, 1.0) = 0.72;
uniform float foam_frames_per_second : hint_range(0.1, 60.0) = 8.0;
uniform int foam_frame_count = 16;
uniform float foam_tiles_per_repeat : hint_range(0.1, 32.0) = 6.0;

void fragment() {
    vec4 shore_mask = texture(TEXTURE, UV);
    if (shore_mask.a <= 0.0) {
        discard;
    }

    float frames = float(max(foam_frame_count, 1));
    float frame = floor(mod(TIME * foam_frames_per_second, frames));
    vec2 repeated = fract(UV * foam_tiles_per_repeat);
    vec2 foam_uv = vec2((repeated.x + frame) / frames, repeated.y);
    vec4 foam = texture(foam_texture, foam_uv);
    foam.a *= shore_mask.a * foam_opacity;
    COLOR = foam;
}
";

		private const string GrassOverlayShaderCode = @"
shader_type canvas_item;

uniform sampler2D overlay_tex : repeat_enable, filter_linear;
uniform float overlay_strength : hint_range(0.0, 1.0) = 0.28;
uniform float overlay_texture_size : hint_range(32.0, 4096.0) = 1024.0;
varying vec2 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
    vec4 grass_mask = texture(TEXTURE, UV);
    if (grass_mask.a <= 0.0) {
        discard;
    }

    vec4 overlay = texture(overlay_tex, world_position / overlay_texture_size);
    overlay.a *= grass_mask.a * overlay_strength;
    COLOR = overlay;
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

		private static string NormalizeTerrainKind(string value)
			=> string.IsNullOrWhiteSpace(value) ? "grass" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

		private static string BiomeOverlayKeyFor(string terrainKind) => NormalizeTerrainKind(terrainKind) switch
		{
			"dry_grass" => "dry_grass",
			"desert" or "sand" or "beach" => "sand",
			"dirt" or "soil" or "earth" or "mud" or "swamp" => "earth",
			"rock" or "stone" or "gravel" => "rock",
			"snow" or "ice" => "snow_ice",
			_ => string.Empty,
		};

		private static Color MixPreserveAlpha(Color colour, Color overlay, float amount)
		{
			float alpha = colour.A;
			Color mixed = colour.Lerp(overlay, Mathf.Clamp(amount, 0.0f, 1.0f));
			mixed.A = alpha;
			return mixed;
		}

		private static float SpotMask(Vector2 at, int seed, float cellsPerTile, float probability)
		{
			float scale = Mathf.Max(0.001f, cellsPerTile);
			float sx = at.X * scale;
			float sy = at.Y * scale;
			int cellX = Mathf.FloorToInt(sx);
			int cellY = Mathf.FloorToInt(sy);
			float chance = Hash01(cellX, cellY, seed);
			float clampedProbability = Mathf.Clamp(probability, 0.0f, 0.95f);
			if (chance < 1.0f - clampedProbability)
				return 0.0f;

			float localX = Repeat(sx) - 0.5f;
			float localY = Repeat(sy) - 0.5f;
			float radius = Mathf.Lerp(0.14f, 0.36f, Hash01(cellX, cellY, seed + 17));
			float distance = Mathf.Sqrt((localX * localX) + (localY * localY));
			return 1.0f - SmoothStep(radius * 0.35f, radius, distance);
		}

		private static float ValueNoise(float x, float y, int seed)
		{
			int x0 = Mathf.FloorToInt(x);
			int y0 = Mathf.FloorToInt(y);
			int x1 = x0 + 1;
			int y1 = y0 + 1;
			float tx = SmoothStep(0.0f, 1.0f, x - x0);
			float ty = SmoothStep(0.0f, 1.0f, y - y0);

			float a = Hash01(x0, y0, seed);
			float b = Hash01(x1, y0, seed);
			float c = Hash01(x0, y1, seed);
			float d = Hash01(x1, y1, seed);
			return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
		}

		private static float Hash01(int x, int y, int seed)
			=> (Grain(x, y, seed) + 1.0f) * 0.5f;

		private static float Repeat(float value) => value - Mathf.Floor(value);

		private static float Grain(int x, int y, int seed)
		{
			uint n = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
			n = (n ^ (n >> 13)) * 1274126177u;
			n ^= n >> 16;

			return ((n & 255u) / 127.5f) - 1.0f;
		}

		private sealed class MaterialTextureSet
		{
			private static readonly Dictionary<string, MaterialTextureSet> Cache = new();

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

			public static MaterialTextureSet Load(string root, MaterialTexturePaths paths)
			{
				string normalizedRoot = NormalizeRoot(root);
				string cacheKey = string.Join("|", normalizedRoot, paths.Grass, paths.DryGrass, paths.Sand, paths.Mud, paths.Rock, paths.WaterShallow, paths.WaterDeep, paths.SnowIce);
				if (Cache.TryGetValue(cacheKey, out MaterialTextureSet? cached))
					return cached;

				MaterialTextureSet loaded = new(
					SampledMaterialTexture.Load(ResolvePath(paths.Grass, normalizedRoot + "grass.png"), new Color(0.25f, 0.48f, 0.20f)),
					SampledMaterialTexture.Load(ResolvePath(paths.DryGrass, normalizedRoot + "dry_grass.png"), new Color(0.42f, 0.48f, 0.28f)),
					SampledMaterialTexture.Load(ResolvePath(paths.Sand, normalizedRoot + "sand.png"), new Color(0.58f, 0.50f, 0.30f)),
					SampledMaterialTexture.Load(ResolvePath(paths.Mud, normalizedRoot + "mud.png"), new Color(0.35f, 0.32f, 0.22f)),
					SampledMaterialTexture.Load(ResolvePath(paths.Rock, normalizedRoot + "rock.png"), new Color(0.34f, 0.38f, 0.35f)),
					SampledMaterialTexture.Load(ResolvePath(paths.WaterShallow, normalizedRoot + "water_shallow.png"), new Color(0.10f, 0.50f, 0.58f)),
					SampledMaterialTexture.Load(ResolvePath(paths.WaterDeep, normalizedRoot + "water_deep.png"), new Color(0.05f, 0.40f, 0.54f)),
					SampledMaterialTexture.Load(ResolvePath(paths.SnowIce, normalizedRoot + "snow_ice.png"), new Color(0.78f, 0.82f, 0.80f)));

				Cache[cacheKey] = loaded;
				return loaded;
			}

			private static string ResolvePath(string configuredPath, string fallbackPath)
				=> string.IsNullOrWhiteSpace(configuredPath) ? fallbackPath : configuredPath;

			private static string NormalizeRoot(string root)
			{
				string value = string.IsNullOrWhiteSpace(root) ? DefaultMaterialTextureRoot : root;
				return value.Replace('\\', '/').TrimEnd('/') + "/";
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
					Image? image = LoadImage(path);
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

			private static Image? LoadImage(string path)
			{
				if (path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("uid://", StringComparison.Ordinal))
				{
					if (ResourceLoader.Exists(path))
						return GD.Load<Texture2D>(path)?.GetImage();

					string diskPath = ProjectSettings.GlobalizePath(path);
					return File.Exists(diskPath) ? Image.LoadFromFile(diskPath) : null;
				}

				string localPath = path.StartsWith("user://", StringComparison.Ordinal)
					? ProjectSettings.GlobalizePath(path)
					: path;

				return File.Exists(localPath) ? Image.LoadFromFile(localPath) : null;
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
