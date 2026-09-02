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
	public partial class TerrainPaintedRendererComponent : Node2D
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
			// The splat shader has no lava material slot; rock is the honest
			// stand-in until one exists. Unmapped, lava fell through to id 0
			// and a lava field painted as grass.
			["lava"] = 10,
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

		// No z index export here. Where a view sits in the stack belongs to
		// TerrainLayers, and a per-renderer dial beside it is a second owner of
		// the same fact - which is how the top-down feature renderer ended up
		// drawing its trees underneath the map.

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

		private TerrainGeneratorComponent? _generator;
		private TileMapLayer? _surface;
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
				? new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." }
				: System.Array.Empty<string>();

		/// <summary>Re-uploads the terrain grid and repaints the surface.</summary>
		public void Rebuild()
		{
			ResolveGenerator();
			if (_generator is null)
			{
				GD.PushWarning($"[{Name}] no generator at TerrainGeneratorPath; the painted surface was not repainted.");
				return;
			}
			TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

			// Resolved ONCE per rebuild rather than once per cell; see
			// TerrainGeneratorComponent.ResolveField.
			GeneratedTerrainField field = _generator.ResolveField();

			Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
			ImageTexture idMap = BuildIdMap(field, size, out ImageTexture shadeMap, out ImageTexture coastMap);

			EnsureSurface(size);
			if (_material is null)
				return;

			_material.SetShaderParameter("id_map", idMap);
			_material.SetShaderParameter("shade_map", shadeMap);
			_material.SetShaderParameter("coast_map", coastMap);
			_material.SetShaderParameter("coast_range", CoastRangeTiles);

			// The beach is as wide as the GENERATOR says, not as wide as this
			// shader happens to default to.
			//
			// This view composites its own sand at the shore from the coast
			// field, while the tile and isometric views draw the sand BIOME the
			// beach stage assigns. Two sources for one fact, and they drifted
			// exactly as that always does: with BeachWidth at 0.028 the
			// generator made no beach at all, the tile and isometric views
			// showed none, and this one carried on drawing the 1.15 tiles its
			// shader defaulted to. One map, and only one of three views telling
			// the truth about its coast.
			_material.SetShaderParameter("beach_tiles", _generator.BeachWidth);
			_material.SetShaderParameter("map_size", new Vector2(size.X, size.Y));
			_material.SetShaderParameter(
				"cell_size", new Vector2(Mathf.Max(1, TileSize), Mathf.Max(1, TileSize)));
			_material.SetShaderParameter("texture_tiles", Mathf.Max(1.0f, TextureTiles));
			_material.SetShaderParameter("blend_width", BlendWidth);
			_material.SetShaderParameter("edge_noise", EdgeNoise);
			_material.SetShaderParameter("noise_scale", NoiseScale);
			_material.SetShaderParameter("shade_strength", ShadeStrength);

			Texture2D? foam = TerrainTextures.Load(FoamSheetPath, Name, "foam sheet");
			if (foam is null && !string.IsNullOrWhiteSpace(FoamSheetPath))
				GD.PushWarning($"[{Name}] falling back to generated crests.");
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
		private ImageTexture BuildIdMap(GeneratedTerrainField field, Vector2I size, out ImageTexture shadeMap, out ImageTexture coastMap)
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
					string kind = field.TerrainAtCell(cell);
					int id = TerrainIds.TryGetValue(kind, out int mapped) ? mapped : 0;

					// Shade is 0.7..1.3 from the generator; halved so it fits a
					// colour channel, and doubled again in the shader.
					float lit = Mathf.Clamp(field.ShadeAtPosition(new Vector2(cell.X + 0.5f, cell.Y + 0.5f)) * 0.5f, 0.0f, 1.0f);
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
			=> TerrainCoastField.Build(_generator!, size, CoastDetail, CoastRangeTiles);

		private void EnsureSurface(Vector2I size)
		{
			_surface = TerrainAuthoring.EnsureLayer(this, "SplatSurface");

			// REAL TILES, not a quad stretched over the map.
			//
			// The look is unchanged - this is still one continuous blended surface
			// computed per pixel - but it is now part of the tile system: saved with
			// the scene, editable, and carrying the collision and navigation a
			// TileMapLayer offers. The shader finds out where a fragment is from
			// VERTEX rather than from a full-map quad's UV, which is what made the
			// surface interchangeable at all.
			int tile = Mathf.Max(1, TileSize);
			Vector2I cell = new(tile, tile);
			if (_surface.TileSet is null || _surface.TileSet.TileSize != cell)
				_surface.TileSet = TerrainShaderSurface.BuildTileSet(cell, isometric: false);

			TerrainShaderSurface.Fill(_surface, size);

			// The whole map - bed, sea and land composited in a single pass - so it
			// goes at the floor of the shared stack.
			_surface.ZIndex = TerrainLayers.ZForFloor();
			_surface.ZAsRelative = false;
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

			Texture2D? texture = TerrainTextures.Load(path, Name, $"the {parameter} material");
			if (texture is null)
				return;

			_material.SetShaderParameter(parameter, texture);
		}

		private void ResolveGenerator()
		{
			if (_generator is null || !GodotObject.IsInstanceValid(_generator))
				_generator = TerrainGeneratorPath.IsEmpty
					? null
					: GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
		}
	}
}
