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
	public partial class TerrainPaintedRendererComponent : TerrainRendererComponent
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
			["lava"] = 13,
			["shallow_water"] = 11,
			["deep_water"] = 12,
			["water"] = 12,
			["sea"] = 12,
			["ocean"] = 12,
		};

		[Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
		[Export] public NodePath CellDataPath { get; set; } = new("");

		[ExportGroup("Map")]
		[Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
		[Export] public Vector2I BoundsSize { get; set; } = new(96, 60);
		[Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;

		[ExportGroup("Look")]
		/// <summary>Tiles per ground-texture repeat, including beach and submerged sand.</summary>
		[Export(PropertyHint.Range, "1,32,0.5")] public float GroundTextureTiles { get; set; } = 12.0f;
		/// <summary>Tiles per animated water-texture repeat, independent of ground detail.</summary>
		[Export(PropertyHint.Range, "1,32,0.5")] public float WaterTextureTiles { get; set; } = 6.0f;
		/// <summary>Optional per-ground-texture repeat sizes; unassigned slots use GroundTextureTiles.</summary>
		[Export] public TerrainMaterialTiling? MaterialTiling { get; set; }
		[Export] public TerrainMapArt? MapArt { get; set; }
		[Export(PropertyHint.Range, "0,0.9,0.01")] public float BlendWidth { get; set; } = 0.42f;
		/// <summary>Concentrates material transitions without changing their coverage or texture scale.</summary>
		[Export(PropertyHint.Range, "1,8,0.25")] public float BlendSharpness { get; set; } = 4.0f;
		/// <summary>Texture-detail influence at material transitions; zero uses geometric blending only.</summary>
		[Export(PropertyHint.Range, "0,1,0.05")] public float MaterialEdgeDetail { get; set; } = 0.75f;
		[Export(PropertyHint.Range, "0,1,0.01")] public float EdgeNoise { get; set; } = 0.55f;
		[Export(PropertyHint.Range, "0.5,24,0.5")] public float NoiseScale { get; set; } = 5.0f;
		[Export(PropertyHint.Range, "0,2,0.05")] public float ShadeStrength { get; set; } = 0.35f;
		/// <summary>How many tiles of coast distance the shader can see.</summary>
		[Export(PropertyHint.Range, "5,16,0.5")] public float CoastRangeTiles { get; set; } = TerrainCoastField.DefaultRangeTiles;
		/// <summary>
		/// Sub-tile resolution of the coast distance field. At 1 the field has
		/// one value per tile and its interpolated contours are square, so surf
		/// drawn along them looks like survey lines rather than waves.
		/// </summary>
		[Export(PropertyHint.Range, "1,16,1")] public int CoastDetail { get; set; } = 12;

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
		/// under a tile deep, and a repeat spread over many tiles parks the
		/// sheet's crest bands outside it - the old default of 7 left the beach
		/// with no foam at all for most of each scroll cycle.
		/// </summary>
		[Export(PropertyHint.Range, "0.3,8,0.1")] public float FoamTilesAcross { get; set; } = 1.6f;

		/// <summary>How fast the authored crests advance onto the beach.</summary>
		[Export(PropertyHint.Range, "0,4,0.01")] public float FoamScroll { get; set; } = 0.055f;

		/// <summary>How strongly the surf pulses as crests arrive, 0 for a steady band.</summary>
		[Export(PropertyHint.Range, "0,1,0.05")] public float FoamPulse { get; set; } = 0.34f;

		/// <summary>How fast arriving crests follow one another.</summary>
		[Export(PropertyHint.Range, "0,4,0.05")] public float FoamArrivalRate { get; set; } = 0.9f;

		// The three water dials the isometric renderer exposes, feeding the same
		// shader uniforms. The two views share one water on purpose; each view
		// exposing a different half of its dials was the drift the shared shader
		// exists to prevent, moved up into the components.
		/// <summary>How bright the surf paints, 0 for none.</summary>
		[Export(PropertyHint.Range, "0,1,0.01")] public float FoamStrength { get; set; } = 0.50f;
		/// <summary>Tiles from the shore at which the water reaches full depth colour.</summary>
		[Export(PropertyHint.Range, "0.5,12,0.1")] public float DeepTiles { get; set; } = 4.5f;
		/// <summary>Tiles from the shore over which the sandy bottom shows through.</summary>
		[Export(PropertyHint.Range, "0,8,0.1")] public float ShallowTiles { get; set; } = 1.8f;
		/// <summary>Direction the swell travels, in degrees, y-down screen space.</summary>
		[Export(PropertyHint.Range, "0,360,1")] public float SwellDirectionDegrees { get; set; } = 210.0f;
		/// <summary>How strongly surf favours coasts facing the swell. 0 puts surf on every shore alike.</summary>
		[Export(PropertyHint.Range, "0,1,0.01")] public float SwellDirectionality { get; set; } = 0.65f;


		[ExportGroup("Material Textures")]
		[Export(PropertyHint.File, "*.png,*.webp")] public string GrassTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string DryGrassTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string SandTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string DirtTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string SnowTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string MudTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string GravelTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string RockTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string LavaTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string ShallowWaterTexturePath { get; set; } = "";
		[Export(PropertyHint.File, "*.png,*.webp")] public string DeepWaterTexturePath { get; set; } = "";

		private const string ShaderPath = "res://addons/beep_game_builder_cs/shaders/terrain_splat.gdshader";

		private TerrainGeneratorComponent? _generator;
		private GridCellDataComponent? _cells;
		private TileMapLayer? _surface;
		private ShaderMaterial? _material;
		private readonly TerrainCoastField.LiveCache _liveCoast = new();
		private TerrainVisualSnapshot _visualSnapshot = new();
		private TerrainVisualSnapshot.Preparation? _snapshotPreparation;
		private TerrainPaintedCoastJob? _coastPreparation;
		private ImageTexture? _preparedCoast, _preparedLake;
		private TerrainCoastField.Pixels? _preparedCoastPixels, _preparedLakePixels;
		private (GridCellDataComponent? Cells, ulong Revision, string DefaultKind,
			Vector2I Origin, Vector2I Size, int Detail, float Range)? _preparedKey;
		public bool IsPreparingSnapshot => _snapshotPreparation is not null;
		public int PreparedSnapshotCells => _snapshotPreparation?.Loaded ?? 0;
		public string PreparationStage => _coastPreparation is null ? "Preparing painted terrain" : "Computing painted coast";

		internal bool BeginSnapshotPreparation()
		{
			CancelSnapshotPreparation();
			ResolveCells();
			if (_cells is null) return false;
			ClearRebuildQueued();
			_snapshotPreparation = new(_cells, BoundsOrigin, BoundsSize);
			return true;
		}

		internal bool StepSnapshotPreparation(int budget)
		{
			if (_snapshotPreparation is null) throw new InvalidOperationException("Painted snapshot preparation was interrupted.");
			var cells = CellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(CellDataPath);
			if (!_snapshotPreparation.Matches(cells, BoundsOrigin, BoundsSize))
				throw new InvalidOperationException("Painted snapshot source changed during preparation.");
			if (!_snapshotPreparation.Step(budget)) return false;
			if (_coastPreparation is null)
			{
				_coastPreparation = new(_snapshotPreparation.Snapshot, BoundsSize, CoastDetail, CoastRangeTiles);
				return false;
			}
			if (_coastPreparation.Detail != CoastDetail || _coastPreparation.Range != CoastRangeTiles)
				throw new InvalidOperationException("Painted coast settings changed during preparation.");
			if (!_coastPreparation.Completion.IsCompleted) return false;
			var pixels = _coastPreparation.Completion.GetAwaiter().GetResult();
			var coast = pixels.Coast.Upload();
			var lake = pixels.Lake.Upload();
			_renderCoast.Prepare(coast, BoundsSize, pixels.SmoothCoast);
			_renderLake.Prepare(lake, BoundsSize, pixels.SmoothLake);
			_preparedCoast = coast;
			_preparedLake = lake;
			// The raw fields too, so the coast caches adopt them and the first edit is a window, not a rebuild.
			_preparedCoastPixels = pixels.Coast;
			_preparedLakePixels = pixels.Lake;
			_preparedKey = (_cells, _cells!.TerrainRevision, _cells.DefaultTerrainKind,
				BoundsOrigin, BoundsSize, CoastDetail, CoastRangeTiles);
			_coastPreparation.Dispose();
			_coastPreparation = null;
			_visualSnapshot = _snapshotPreparation.Snapshot;
			_snapshotPreparation = null;
			_mapKey = null;
			return true;
		}

		internal void CancelSnapshotPreparation()
		{
			_snapshotPreparation = null;
			_coastPreparation?.Dispose();
			_coastPreparation = null;
		}
		private readonly TerrainCoastField.RenderCache _renderCoast = new();
		private readonly TerrainCoastField.RenderCache _renderLake = new();
		private ImageTexture? _lakeMap, _lakeWidthMap, _emptyLakeMap;
		private ImageTexture? _idMap, _shadeMap, _coastMap;
		// The id-map inputs and pixels, retained so a changed chunk rewrites only its texels. The
		// whole-map pass they replace cost 742 ms for one inland cell at 320x256.
		private byte[]? _idPixels, _shadePixels, _lakeWidthPixels;
		private bool[]? _waterMask, _lakeBank;
		private float[]? _elevationField;
		private GridTerrainWaterPatch?[]? _waterPatches, _lakePatches;
		private Image? _idImage, _shadeImage, _lakeWidthImage;
		private int _lakeBankCells;
		private (GridCellDataComponent? Cells, ulong Revision, string DefaultKind,
			GeneratedTerrainField? Field, Vector2I Origin, Vector2I Size, int Detail, float Range)? _mapKey;



		/// <summary>
		/// Whether this renderer builds itself once the scene is ready. Turn it
		/// off where a controller generates the world first and drives Rebuild,
		/// so the map is not built twice.
		/// </summary>

		// The painted view declines a rebuild while it is preparing a visual
		// snapshot off-thread; the queued build would race that preparation.
		protected override bool CanQueueRebuild() => _snapshotPreparation is null;

		public override void _Ready()
		{
			ResolveCells();
			if (RefreshOnReady && !Engine.IsEditorHint())
				CallDeferred(nameof(Rebuild));
		}

		public override void _EnterTree()
		{
			if (HasRebuildAttempt && !Engine.IsEditorHint())
				Callable.From(() =>
				{
					if (!IsInsideTree()) return;
					ResolveCells();
					QueueRebuild();
				}).CallDeferred();
		}

		public override void _ExitTree()
		{
			CancelSnapshotPreparation();
			DisconnectCells();
			ClearRebuildQueued();
		}
		private void DisconnectCells()
		{
			if (_cells is not null && GodotObject.IsInstanceValid(_cells))
			{
				_cells.CellChanged -= OnCellChanged;
				_cells.CellsChanged -= OnCellsChangedSignal;
			}
			_cells = null;
			_mapKey = null;
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

		private void OnCellChanged(int x, int y)
		{
			if (new Rect2I(BoundsOrigin, BoundsSize).HasPoint(new Vector2I(x, y))) QueueRebuild();
		}
		public override string[] _GetConfigurationWarnings()
			=> TerrainGeneratorPath.IsEmpty && CellDataPath.IsEmpty
				? new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." }
				: System.Array.Empty<string>();

		/// <summary>Re-uploads the terrain grid and repaints the surface.</summary>
		public override void Rebuild()
		{
			CancelSnapshotPreparation();
			HasRebuildAttempt = true;
			ClearRebuildQueued();
			ResolveCells();
			ResolveGenerator();
			if ((!CellDataPath.IsEmpty && _cells is null) || (_cells is null && _generator is null))
			{
				_surface = GetNodeOrNull<TileMapLayer>("SplatSurface");
				_surface?.Clear();
				GD.PushWarning($"[{Name}] terrain source could not be resolved; the painted surface was cleared.");
				return;
			}
			if (_cells is null && _generator is not null)
				TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

			// Resolved ONCE per rebuild rather than once per cell; see
			// TerrainGeneratorComponent.ResolveField.
			GeneratedTerrainField? field = _cells is null ? _generator!.ResolveField() : null;

			Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
			if (_preparedKey != (_cells, _cells?.TerrainRevision ?? 0, _cells?.DefaultTerrainKind ?? "",
				BoundsOrigin, size, CoastDetail, CoastRangeTiles))
			{
				_preparedCoast = _preparedLake = null;
				_preparedCoastPixels = _preparedLakePixels = null;
				_preparedKey = null;
			}
			if (_cells is not null && !_visualSnapshot.Update(_cells, BoundsOrigin, size))
			{
				if (GodotObject.IsInstanceValid(_surface)) _surface!.Visible = false;
				return;
			}
			var mapKey = (_cells, _cells is not null ? _visualSnapshot.Revision : 0, _cells?.DefaultTerrainKind ?? "",
				field, BoundsOrigin, size, CoastDetail, CoastRangeTiles);
			if (_mapKey != mapKey || !GodotObject.IsInstanceValid(_idMap)
				|| !GodotObject.IsInstanceValid(_shadeMap) || !GodotObject.IsInstanceValid(_coastMap))
			{
				// Same source, bounds and coast settings, only the samples moved: rewrite the changed
				// chunks' texels in place. Anything else is a whole build.
				bool incremental = _cells is not null && _idPixels is not null && !_visualSnapshot.Reset
					&& _mapKey is { } previous && previous.Cells == _cells && previous.DefaultKind == _cells.DefaultTerrainKind
					&& previous.Field == field && previous.Origin == BoundsOrigin && previous.Size == size
					&& previous.Detail == CoastDetail && previous.Range == CoastRangeTiles
					&& GodotObject.IsInstanceValid(_idMap) && GodotObject.IsInstanceValid(_shadeMap)
					&& GodotObject.IsInstanceValid(_coastMap) && GodotObject.IsInstanceValid(_lakeWidthMap);
				if (incremental) UpdateMaps(size, _visualSnapshot.ChangedChunks);
				else BuildMaps(field, size);
				_mapKey = mapKey;
			}

			EnsureSurface(size);
			_surface!.Visible = true;
			if (_material is null)
				return;

			_material.SetShaderParameter("id_map", _idMap!);
			_material.SetShaderParameter("shade_map", _shadeMap!);
			// The live caches hand the render caches their raw fields and the window an update
			// touched; a generator-built field has neither and takes the whole pass.
			bool liveCoast = _waterPatches is not null;
			_material.SetShaderParameter("coast_map", _renderCoast.Resolve(_coastMap!, size, _liveCoast.CoastRevision,
				liveCoast ? _liveCoast.CoastField : null, liveCoast ? _liveCoast.CoastDirty : null));
			_material.SetShaderParameter("lake_map", _renderLake.Resolve(_lakeMap!, size, _liveCoast.LakeRevision,
				liveCoast ? _liveCoast.LakeField : null, liveCoast ? _liveCoast.LakeDirty : null));
			_material.SetShaderParameter("lake_width_map", _lakeWidthMap!);

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
			// Live sand is explicit cell data; do not invent a generated beach over player edits.
			// Width and underlying biome are carried in each live ID texel.
			_material.SetShaderParameter(
				"cell_size", new Vector2(Mathf.Max(1, TileSize), Mathf.Max(1, TileSize)));
			TerrainMaterialTiling.Apply(_material, MaterialTiling);
			_material.SetShaderParameter("blend_width", BlendWidth);
			_material.SetShaderParameter("blend_sharpness", Mathf.Clamp(BlendSharpness, 1.0f, 8.0f));
			_material.SetShaderParameter("material_edge_detail", Mathf.Clamp(MaterialEdgeDetail, 0.0f, 1.0f));
			_material.SetShaderParameter("edge_noise", EdgeNoise);
			_material.SetShaderParameter("noise_scale", NoiseScale);
			_material.SetShaderParameter("shade_strength", ShadeStrength);

			// Everything water_common.gdshaderinc declares, through its one writer.
			// The coast range keeps this view's own floor: it must agree with the range
			// TerrainPaintedCoastJob BUILT the field with, not with the export alone.
			TerrainWaterMaterial.Apply(_material, new TerrainWaterMaterial.Settings(
				Size: size,
				Origin: BoundsOrigin,
				CoastRange: Mathf.Max(5f, CoastRangeTiles),
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
			TerrainWaterMaterial.BindFoamSheet(_material, FoamSheetPath, Name);
			_material.SetShaderParameter("art_style", 0);
			MapArt?.ApplyGround(_material);
		}

		/// <summary>
		/// One texel per tile: red is the terrain id, green the hillshade. This
		/// is the only way the shader can know what its neighbours are.
		/// </summary>
		private void BuildMaps(GeneratedTerrainField? field, Vector2I size)
		{
			int count = checked(size.X * size.Y);
			_idPixels = new byte[checked(count * 4)];
			_shadePixels = new byte[_idPixels.Length];
			_lakeWidthPixels = new byte[_idPixels.Length];
			_waterMask = new bool[count];
			_lakeBank = new bool[count];
			_lakeBankCells = 0;
			_elevationField = _cells is null ? null : new float[count];
			_waterPatches = _cells is null ? null : new GridTerrainWaterPatch?[count];
			_lakePatches = _cells is null ? null : new GridTerrainWaterPatch?[count];
			// Sample each live elevation once; neighbouring slopes read this snapshot.
			for (int y = 0; y < size.Y; y++)
				for (int x = 0; x < size.X; x++) WriteCellTexels(field, size, x, y);
			for (int y = 0; y < size.Y; y++)
				for (int x = 0; x < size.X; x++) WriteShadeTexel(field, size, x, y);
			_idImage = Image.CreateFromData(size.X, size.Y, false, Image.Format.Rgba8, _idPixels);
			// Shade is filtered linearly while terrain IDs remain nearest-filtered.
			_shadeImage = Image.CreateFromData(size.X, size.Y, false, Image.Format.Rgba8, _shadePixels);
			_lakeWidthImage = Image.CreateFromData(size.X, size.Y, false, Image.Format.Rgba8, _lakeWidthPixels);
			_idMap = ImageTexture.CreateFromImage(_idImage);
			_shadeMap = ImageTexture.CreateFromImage(_shadeImage);
			_lakeWidthMap = ImageTexture.CreateFromImage(_lakeWidthImage);
			_lakeMap = ResolveLakeMap(field, size);
			_coastMap = ResolveCoastMap(size);
		}

		/// <summary>Rewrites the texels of the chunks whose samples changed, plus a one-cell shade halo, in place.</summary>
		private void UpdateMaps(Vector2I size, IReadOnlyList<Vector2I> chunks)
		{
			var bounds = new Rect2I(Vector2I.Zero, size);
			foreach (Vector2I chunk in chunks)
			{
				// Snapshot chunks are absolute; the maps are local to BoundsOrigin.
				var rect = new Rect2I(chunk * 32 - BoundsOrigin, new Vector2I(32, 32)).Intersection(bounds);
				if (rect.Size.X <= 0 || rect.Size.Y <= 0) continue;
				for (int y = rect.Position.Y; y < rect.End.Y; y++)
					for (int x = rect.Position.X; x < rect.End.X; x++) WriteCellTexels(null, size, x, y);
				var shade = rect.Grow(1).Intersection(bounds);
				for (int y = shade.Position.Y; y < shade.End.Y; y++)
					for (int x = shade.Position.X; x < shade.End.X; x++) WriteShadeTexel(null, size, x, y);
			}
			// New textures rather than ImageTexture.Update: the headless renderer never applies an
			// Update, and the probes read texture data back. The upload is the same either way.
			_idImage!.SetData(size.X, size.Y, false, Image.Format.Rgba8, _idPixels!);
			_idMap = ImageTexture.CreateFromImage(_idImage);
			_shadeImage!.SetData(size.X, size.Y, false, Image.Format.Rgba8, _shadePixels!);
			_shadeMap = ImageTexture.CreateFromImage(_shadeImage);
			_lakeWidthImage!.SetData(size.X, size.Y, false, Image.Format.Rgba8, _lakeWidthPixels!);
			_lakeWidthMap = ImageTexture.CreateFromImage(_lakeWidthImage);
			_lakeMap = ResolveLakeMap(null, size);
			_coastMap = ResolveCoastMap(size);
		}

		private void WriteCellTexels(GeneratedTerrainField? field, Vector2I size, int x, int y)
		{
			var cell = new Vector2I(x, y);
			int index = y * size.X + x;
			int pixel = index * 4;
			string kind = _cells is not null ? _visualSnapshot[index].Kind : field!.TerrainAtCell(cell);
			int id = TerrainIds.TryGetValue(kind, out int mapped) ? mapped : 0;
			_idPixels![pixel] = (byte)id;
			var shore = _cells is not null ? _visualSnapshot[index].Shore
				: (Inland: field!.InlandTerrainAtCell(cell), Width: field.BeachWidth,
					LakeWidth: field.ReliefAtCell(cell) == TerrainRelief.Flat ? field.LakeShoreWidth : 0f);
			_lakeWidthPixels![pixel] = (byte)Mathf.RoundToInt(shore.LakeWidth / 3f * 255f);
			bool bank = shore.LakeWidth > 0f;
			if (_lakeBank![index] != bank)
			{
				_lakeBank[index] = bank;
				_lakeBankCells += bank ? 1 : -1;
			}
			_idPixels[pixel + 2] = (byte)(TerrainIds.TryGetValue(shore.Inland, out int inlandId) ? inlandId : id);
			_idPixels[pixel + 3] = (byte)Mathf.RoundToInt(Mathf.Clamp(shore.Width / 4f, 0f, 1f) * 255f);
			bool water = TerrainTileSets.IsWaterKind(kind);
			_waterMask![index] = water;
			if (_elevationField is not null) _elevationField[index] = water ? 0f : _visualSnapshot[index].Elevation;
			if (_waterPatches is not null)
			{
				_waterPatches[index] = _visualSnapshot[index].Water;
				_lakePatches![index] = _visualSnapshot[index].Lake;
			}
		}

		private void WriteShadeTexel(GeneratedTerrainField? field, Vector2I size, int x, int y)
		{
			var cell = new Vector2I(x, y);
			int index = y * size.X + x;
			// Shade is 0.7..1.3 from the generator; halved so it fits a
			// colour channel, and doubled again in the shader.
			float lighting = _elevationField is not null
				? (_waterMask![index] ? 1f : TerrainShadingStage.AtCell(_elevationField, size, cell))
				: field!.ShadeAtPosition(new Vector2(cell.X + 0.5f, cell.Y + 0.5f));
			float lit = Mathf.Clamp(lighting * 0.5f, 0.0f, 1.0f);
			byte shadeByte = (byte)(lit * 255f);
			int pixel = index * 4;
			_idPixels![pixel + 1] = shadeByte;
			_shadePixels![pixel] = _shadePixels[pixel + 1] = _shadePixels[pixel + 2] = shadeByte;
			_shadePixels[pixel + 3] = 255;
		}

		private ImageTexture ResolveLakeMap(GeneratedTerrainField? field, Vector2I size)
		{
			int detail = Mathf.Clamp(CoastDetail, 1, 16);
			float range = Mathf.Max(5f, CoastRangeTiles);
			ImageTexture lake;
			if (_lakeBankCells == 0)
			{
				if (!GodotObject.IsInstanceValid(_emptyLakeMap))
				{
					using var empty = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
					empty.Fill(new Color(0f, 0f, 0f, 1f));
					_emptyLakeMap = ImageTexture.CreateFromImage(empty);
				}
				lake = _emptyLakeMap!;
			}
			else if (_lakePatches is null)
				lake = TerrainCoastField.BuildLake(_cells, field, BoundsOrigin, size, CoastDetail, range);
			else if (_preparedLakePixels is { Format: Image.Format.Rgbaf } prepared && _preparedLake is not null)
				lake = _liveCoast.AdoptLake(_lakePatches, prepared, _preparedLake, size, detail, range);
			else
				lake = _liveCoast.ResolveLakeSamples(_lakePatches, size, detail, range);
			_preparedLake = null;
			_preparedLakePixels = null;
			return lake;
		}

		private ImageTexture ResolveCoastMap(Vector2I size)
		{
			int detail = Mathf.Clamp(CoastDetail, 1, 16);
			float range = Mathf.Max(5f, CoastRangeTiles);
			ImageTexture coast;
			if (_waterPatches is null)
				coast = BuildCoastMap(size);
			else if (_preparedCoastPixels is { } prepared && _preparedCoast is not null)
				coast = _liveCoast.AdoptCoast(_waterMask!, _waterPatches, prepared, _preparedCoast, size, detail, range);
			else
				coast = _liveCoast.ResolveSamples(_waterMask!, _waterPatches, size, detail, range);
			_preparedCoast = null;
			_preparedCoastPixels = null;
			_preparedKey = null;
			return coast;
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
			=> _cells is not null
				? _liveCoast.Resolve(_cells, BoundsOrigin, size, CoastDetail, Mathf.Max(5f, CoastRangeTiles))
				: TerrainCoastField.Build(_generator!, size, CoastDetail, Mathf.Max(5f, CoastRangeTiles));

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
			_surface.Position = new Vector2(BoundsOrigin.X * tile, BoundsOrigin.Y * tile);
			GetTerrainLayer();

			// The whole map - bed, sea and land composited in a single pass - so it
			// goes at the floor of the shared stack.
			_surface.ZIndex = TerrainLayers.ZForFloor();
			_surface.ZAsRelative = false;
			_surface.TextureFilter = TextureFilterEnum.Linear;

			if (_material is null || _surface.Material != _material)
			{
				// Keep authored uniforms, but never write map data into a shared scene resource.
				_material = (_surface.Material as ShaderMaterial)?.Duplicate() as ShaderMaterial;
				if (_material is not null && _material.Shader is null)
					_material.Shader = GD.Load<Shader>(ShaderPath);
				if (_material is not null) _surface.Material = _material;
			}
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
			}
			AssignMaterialTextures();
		}

		/// <summary>Absolute logical coordinates; shader tiles stay local to one rendering quadrant.</summary>
		public TileMapLayer GetTerrainLayer()
		{
			var layer = TerrainAuthoring.EnsureLayer(this, "LogicalGrid");
			var size = Vector2I.One * Mathf.Max(1, TileSize);
			if (layer.TileSet is null || layer.TileSet.TileSize != size)
				layer.TileSet = new TileSet { TileSize = size };
			return layer;
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
			Assign("tex_lava", LavaTexturePath);
			Assign("tex_shallow", ShallowWaterTexturePath);
			Assign("tex_deep", DeepWaterTexturePath);
		}

		private void Assign(string parameter, string path)
		{
			// One texture binder for every renderer, and it reports what loaded;
			// see TerrainTextures.Bind.
			if (_material is not null)
				TerrainTextures.Bind(_material, parameter, path, Name);
		}

		private void ResolveGenerator()
		{
			_generator = TerrainGeneratorPath.IsEmpty ? null
				: GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
		}
	}
}
