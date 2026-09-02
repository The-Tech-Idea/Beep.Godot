using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Renders the generated map as an ISOMETRIC TileMapLayer of stacked blocks.
    ///
    /// Same generator, same cell data, a different projection: this is a third
    /// view of the world the pipeline already decided, alongside the flat splat
    /// surface and the orthogonal tile map. Nothing here decides terrain - it
    /// draws what TerrainGeneratorComponent wrote to the cells, so a map is
    /// the same world whichever renderer is pointed at it.
    ///
    /// A real TileMapLayer rather than hand-drawn sprites, because in isometric
    /// the things a game needs - picking the tile under the cursor, per-tile
    /// data, collision, Y-sorted overlap - are exactly what Godot's tile system
    /// already solves.
    ///
    /// ONE ATLAS, one frame per terrain kind. Loading a separate image per kind
    /// and mapping names to file numbers is how the palette drifts away from the
    /// terrain: the numbers say nothing about what they draw, so a wrong one
    /// looks deliberate. A single sheet whose frame order IS the terrain order
    /// cannot disagree with itself.
    ///
    /// Blocks are TALLER than their footprint, so the cell is the DIAMOND and
    /// each tile's texture origin lifts the block onto it. Getting that offset
    /// wrong is the classic isometric bug where terrain floats above its grid.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainIsometricRendererComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(48, 48);

        [ExportGroup("Blocks")]
        /// <summary>Sheet of equal cells, one isometric block per cell.</summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string BlockSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int SheetColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int SheetRows { get; set; } = 4;

        /// <summary>
        /// The diamond footprint one block occupies, in sheet pixels. This is
        /// the CELL size, not the block image - the image is taller because the
        /// block has sides.
        /// </summary>
        [Export] public Vector2I CellSize { get; set; } = new(462, 308);

        /// <summary>
        /// Lifts each block so its top diamond lands on the cell rather than the
        /// image's midpoint: the distance from the image centre up to the centre
        /// of the diamond.
        /// </summary>
        [Export] public int BlockLift { get; set; } = 79;

        /// <summary>
        /// Flat tops - the same tiles without their sides, on the same grid.
        ///
        /// Level ground has no visible sides, so drawing a full cube for every
        /// land cell puts a shaded face between every pair of tiles and the
        /// ground reads as a heap of loose boxes instead of a surface. Sides
        /// belong to drops - a coast, a cliff - and nowhere else.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string TopSheetPath { get; set; } = "";

        /// <summary>Lift for the flat-top sheet, which is a diamond and not a cube.</summary>
        [Export] public int TopLift { get; set; }

        /// <summary>
        /// How far one elevation step raises a block. This is the height of a
        /// block's visible SIDE: less and the terraces overlap into mush, more
        /// and the column pulls apart into floating slabs.
        /// </summary>
        [Export(PropertyHint.Range, "4,512,1")] public int LevelHeight { get; set; } = 158;

        [ExportGroup("Terrain Frames")]
        /// <summary>Frame index in the sheet, reading left to right then down.</summary>
        [Export] public int GrassFrame { get; set; } = 0;
        [Export] public int DryGrassFrame { get; set; } = 1;
        [Export] public int DesertFrame { get; set; } = 2;
        [Export] public int SandFrame { get; set; } = 3;
        [Export] public int TundraFrame { get; set; } = 4;
        [Export] public int SnowFrame { get; set; } = 5;
        [Export] public int IceFrame { get; set; } = 6;
        [Export] public int JungleFrame { get; set; } = 7;
        [Export] public int SwampFrame { get; set; } = 8;
        [Export] public int GravelFrame { get; set; } = 9;
        [Export] public int RockFrame { get; set; } = 10;
        [Export] public int ShallowWaterFrame { get; set; } = 11;
        [Export] public int DeepWaterFrame { get; set; } = 12;

        /// <summary>
        /// Interchangeable frames per terrain, as "kind=frame[,frame...]".
        ///
        /// One frame per terrain means a stretch of coast running along a grid
        /// axis is the SAME block five times over, and their side faces sit at
        /// identical screen heights - so their bottom edges merge into one hard
        /// unbroken line. Nothing is wrong with the coastline; measured across
        /// map sizes these runs happen at a steady rate of about two per hundred
        /// coastline tiles and cannot be generated away. What can change is that
        /// they are drawn identically.
        ///
        /// Repeat a frame to weight it: "grass=54,54,54,9" leaves grass mostly
        /// itself and breaks the run every fourth tile or so.
        /// </summary>
        [Export] public string[] TerrainVariants { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild.
        /// </summary>
        [Export] public bool RefreshOnReady { get; set; } = true;

        [ExportGroup("Water")]
        /// <summary>
        /// How far from shore the seabed is drawn, in tiles, and how many
        /// material bands it uses across that distance - sand near the beach,
        /// then gravel, then rock. Beyond it the water is opaque and a bed
        /// would never be seen.
        /// </summary>
        [Export(PropertyHint.Range, "1,8,1")] public int SeabedDepth { get; set; } = 5;

        /// <summary>
        /// How far the bed sits below the surface. Small: it only has to be
        /// under the water, and any more drops the whole map further down.
        /// </summary>
        [Export(PropertyHint.Range, "1,64,1")] public int SeabedStep { get; set; } = 12;

        /// <summary>The sea surface shader. Without it there is no water at all.</summary>
        [Export(PropertyHint.File, "*.gdshader")] public string WaterShaderPath { get; set; } = "";

        /// <summary>
        /// Water materials, shared with the flat renderer so both views draw the
        /// same sea. Left unset the shader falls back to white and the tints
        /// alone colour the water, which is flatter but not broken.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string ShallowTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string DeepTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string SandTexturePath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string FoamSheetPath { get; set; } = "";

        /// <summary>Sub-tile samples per tile edge when measuring the coastline.</summary>
        [Export(PropertyHint.Range, "1,8,1")] public int CoastDetail { get; set; } = 4;

        /// <summary>Distance, in tiles, at which the coast field saturates.</summary>
        [Export(PropertyHint.Range, "1,24,0.5")] public float CoastRangeTiles { get; set; } = 5.0f;

        /// <summary>
        /// How opaque deep water gets. With ClarityTiles below, this is what
        /// lets the seabed show: at the waterline the surface is clear, and it
        /// closes over as the bottom drops away.
        ///
        /// Deep water wants to be fully opaque. Anything less leaks the seabed
        /// through, and since the bed only exists inside the map that leak draws
        /// the map's own boundary as a faint diamond out in open water.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float MaxOpacity { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0.1,12,0.1")] public float ClarityTiles { get; set; } = 3.0f;

        /// <summary>
        /// How opaque a LAKE gets. Lower than the sea on purpose: a lake is
        /// shallow and still, and at open-sea opacity a pond reads as ocean.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float LakeOpacity { get; set; } = 0.42f;

        /// <summary>
        /// Water opacity at and inland of the waterline. Above zero because an
        /// isometric block overhangs the tile below it, and if the sea stops at
        /// the waterline that overhang leaves an undrawn strip along every
        /// beach.
        /// </summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float ShoreOpacity { get; set; } = 0.55f;

        /// <summary>
        /// How far the sea runs past the map, as a multiple of the map's own
        /// extent. The surface is a quad, so wherever it stops there is a hard
        /// rectangle in the middle of the ocean; open water has to reach beyond
        /// anywhere the camera can see. Costs nothing off-screen - the quad is
        /// clipped to the viewport.
        /// </summary>
        [Export(PropertyHint.Range, "0,8,0.25")] public float WaterOverscan { get; set; } = 2.5f;

        [Export(PropertyHint.Range, "0,2,0.05")] public float WaveIntensity { get; set; } = 1.0f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float FoamStrength { get; set; } = 0.50f;
        [Export(PropertyHint.Range, "0.5,12,0.1")] public float DeepTiles { get; set; } = 4.5f;
        [Export(PropertyHint.Range, "0,8,0.1")] public float ShallowTiles { get; set; } = 1.8f;

        // The same five foam-sheet dials the painted renderer exposes, feeding
        // the same shader uniforms. The two views deliberately share one water
        // shader so one map has one sea; leaving these authorable in only one
        // view was the same drift, one layer up - a foam sheet tuned in the
        // painted view silently reverted to defaults here.
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

        /// <summary>
        /// The stack this renderer draws into lives in TerrainLayers, shared
        /// with every other view. These forward to it so a scene or a guard can
        /// still ask the renderer, without the renderer owning the answer.
        /// </summary>
        public const int LevelCount = TerrainLayers.Count;

        public static int ZIndexForLevel(int level) => TerrainLayers.ZFor(level);

        public static int ZIndexForProps(int level) => TerrainLayers.ZForProps(level);

        /// <summary>
        /// How far the sea may overscan the map, in CELLS.
        ///
        /// A quad could be scaled arbitrarily for free; tiles have to be filled,
        /// so the margin needs a ceiling. Well past the distance at which the
        /// water goes opaque, which is what the overscan is hiding in the first
        /// place.
        /// </summary>
        private const int MaxWaterMarginCells = 72;

        private const int SourceId = 0;
        /// <summary>Flat tops, for ground with nothing lower beside it.</summary>
        private const int TopSourceId = 1;
        private const int SeaLevel = TerrainLayers.Sea;
        private const int GroundLevel = TerrainLayers.Ground;
        private const int UpperLevel = TerrainLayers.Hills;
        private const int PeakLevel = TerrainLayers.Mountains;
        private const int SummitLevel = TerrainLayers.Summits;

        /// <summary>
        /// Land levels have a tile layer; the sea does not. The sea is drawn by
        /// its surface shader, so a tile layer for it was an empty node holding
        /// one of the five slots open for nothing.
        /// </summary>
        private static int LayerFor(int level) => level - GroundLevel;

        /// <summary>The top share of mountain tiles drawn as summits.</summary>
        private const float SummitShare = 0.45f;

        private float _summitFloor = float.MaxValue;
        private TerrainGeneratorComponent? _generator;
        private readonly List<TileMapLayer> _layers = new();

        /// <summary>
        /// The seabed, as ONE layer. It was five, stacked at descending offsets
        /// to read as terraces - but the water above already shades by depth,
        /// so the terraces were saying a second time what the surface says
        /// better, at the cost of four extra layers and a stack three tile
        /// heights deep. Depth is the shader's job; the bed only has to be
        /// there to be seen through.
        /// </summary>
        private TileMapLayer? _seabed;
        /// <summary>
        /// The sea, as ONE surface rather than tiles. The shader is the flat
        /// renderer's, and it works from a coast distance field per pixel - a
        /// per-tile material cannot give it that, and the atlas UVs it would
        /// have to sample by belong to the tile, not the map.
        /// </summary>
        private TileMapLayer? _water;
        private ShaderMaterial? _waterMaterial;
        /// <summary>How far the sea layer was shifted, in tiles; see EnsureWaterSurface.</summary>
        private Vector2 _waterTileOffset = Vector2.Zero;
        private ImageTexture? _coastMap;

        /// <summary>Steps from the nearest land, per water cell; 0 on land.</summary>
        private int[] _depth = Array.Empty<int>();
        private TileSet? _tileSet;
        /// <summary>Terrain kind to its cell in the atlas.</summary>
        private readonly Dictionary<string, Vector2I> _frames = new();

        /// <summary>Terrain kind to every frame it may use; index 0 is the primary.</summary>
        private readonly Dictionary<string, Vector2I[]> _variants = new();

        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (TerrainGeneratorPath.IsEmpty)
                return new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." };
            if (string.IsNullOrWhiteSpace(BlockSheetPath))
                return new[] { "BlockSheetPath should point to a sheet of isometric blocks." };
            return Array.Empty<string>();
        }

        /// <summary>Rebuilds the whole isometric map from the generator.</summary>
        public void Rebuild()
        {
            ResolveGenerator();
            if (_generator is null)
            {
                GD.PushWarning($"[{Name}] no generator at TerrainGeneratorPath; no blocks were drawn.");
                return;
            }
            if (!EnsureTileSet())
            {
                GD.PushWarning($"[{Name}] the block TileSet could not be built, so no blocks were drawn.");
                return;
            }
            TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            // Resolved ONCE per rebuild rather than once per cell: this
            // renderer's own per-cell cost already compounds hardest of any
            // renderer in the addon (separate full-grid passes for water depth
            // and summit floor, plus ShowsSide re-checking terrain and relief
            // for every elevation step of every raised cell) - see
            // TerrainGeneratorComponent.ResolveField.
            GeneratedTerrainField field = _generator.ResolveField();

            // The coastline both views measure from. Built here rather than
            // copied, so the sea cannot break in one projection and not the
            // other.
            Vector2I bounds = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            _coastMap = TerrainCoastField.Build(_generator, bounds, CoastDetail, CoastRangeTiles);

            EnsureLayers();
            foreach (TileMapLayer existing in _layers)
                existing.Clear();
            _seabed?.Clear();

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            MeasureWaterDepth(field, size);
            MeasureSummitFloor(field, size);
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    string terrain = field.TerrainAtCell(cell);
                    if (!_frames.TryGetValue(terrain, out Vector2I frame))
                        continue;

                    frame = VariantFor(terrain, cell, frame);

                    bool land = !TerrainTileSets.IsWaterKind(terrain);
                    if (!land)
                    {
                        // The bed under open water, dropping away from the shore.
                        //
                        // Only where the surface is see-through. Clamping every
                        // water cell to the deepest step instead put a bed under
                        // the whole ocean - hundreds of tiles that opaque water
                        // can never reveal, whose only visible effect was its
                        // own edge: the bed stops at the map border, the sea now
                        // runs well past it, and that mismatch drew a dead
                        // straight line across the shallows.
                        int depth = _depth[(y * size.X) + x];
                        if (depth < 1 || depth > SeabedDepth || _seabed is null)
                            continue;

                        Vector2I bedFrame = SeabedFrameFor(depth - 1);
                        if (bedFrame.X >= 0)
                            _seabed.SetCell(cell, SourceId, bedFrame);

                        // A RIVER also gets a tile at ground level.
                        //
                        // Every other water cell is deliberately a hole in the
                        // ground layer - bed below, water surface over it - and
                        // that works for the sea and for lakes because they are
                        // wide enough to have an interior the front row does not
                        // cover. A river is ONE TILE WIDE, and a one-tile hole is
                        // hidden completely behind the block sprite of the tile
                        // in front of it. Measured at verified river positions:
                        // 52 to 62 of 81 pixels were grass. The drainage network
                        // was correct the whole time and simply could not be
                        // seen.
                        //
                        // The flat-top source rather than the block one, because
                        // a river sits at ground level and must not read as a
                        // raised cube.
                        if (field.WaterSourceAtCell(cell) == "river")
                            _layers[LayerFor(GroundLevel)].SetCell(cell, TopSourceId, frame);

                        continue;
                    }

                    // Layer 1 - ground, over the sea. Raised cells get ground
                    // too: it is the body of the cliff, and leaving it out is
                    // what made the levels a partition instead of a stack.
                    //
                    // One block per cell per layer. Writing a block into every
                    // level BELOW a cell as well was the earlier mistake: it
                    // stacked three overlapping tiles on one cell and sprawled
                    // them across the neighbours.
                    // Every level from the ground up to this tile's own, so the
                    // cell is a STACK rather than one block floating at its
                    // height. Writing only the top level was the original bug -
                    // it made the levels a partition instead of a stack, and the
                    // ground vanished from under every raised cell.
                    //
                    // Written as a loop rather than a case per level so that
                    // adding a step to the stack is a change to LevelFor alone.
                    int level = LevelFor(terrain, (int)field.ReliefAtCell(cell));

                    // A mountain TAPERS. Every raised cell drawn at one height
                    // makes a range a flat-topped mesa - correct as a stack, and
                    // still not a mountain, because a mountain has a middle that
                    // stands above its own edge. The cells deep inside a massif
                    // take one more step than the cells around its rim, so the
                    // range steps up to its summits instead of shearing off.
                    if (level >= PeakLevel && field.ElevationAtCell(cell) >= _summitFloor)
                        level = SummitLevel;
                    for (int step = GroundLevel; step <= level && step < LevelCount; step++)
                    {
                        _layers[LayerFor(step)].SetCell(
                            cell, ShowsSide(field, cell, step, size) ? SourceId : TopSourceId, frame);
                    }
                }
            }
        }

        /// <summary>
        /// Parses TerrainVariants. Every terrain gets an entry - the primary
        /// alone where none is configured - so the lookup never has to decide
        /// between "no variants" and "not a terrain".
        /// </summary>
        private void BuildVariants(int columns, int rows)
        {
            _variants.Clear();
            foreach ((string terrain, Vector2I coords) in _frames)
                _variants[terrain] = new[] { coords };

            foreach (string entry in TerrainVariants)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] halves = entry.Split('=', StringSplitOptions.TrimEntries);
                if (halves.Length != 2)
                {
                    GD.PushWarning($"[{Name}] terrain variant '{entry}' is not \"kind=frame[,frame...]\".");
                    continue;
                }

                if (!_frames.ContainsKey(halves[0]))
                {
                    GD.PushWarning($"[{Name}] terrain variant names '{halves[0]}', which is not a mapped terrain.");
                    continue;
                }

                var choices = new List<Vector2I>();
                foreach (string piece in halves[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(piece.Trim(), out int frame))
                        continue;

                    Vector2I coords = Coords(frame, columns, rows);
                    if (coords.X < 0)
                        GD.PushWarning($"[{Name}] terrain variant '{entry}' names frame {frame}, outside the sheet.");
                    else
                        choices.Add(coords);
                }

                if (choices.Count > 0)
                    _variants[halves[0]] = choices.ToArray();
            }
        }

        /// <summary>
        /// Which of a terrain's interchangeable frames this cell uses. Stable
        /// for a given cell, so the map does not shimmer when it is rebuilt.
        /// </summary>
        private Vector2I VariantFor(string terrain, Vector2I cell, Vector2I fallback)
        {
            if (!_variants.TryGetValue(terrain, out Vector2I[]? choices) || choices.Length <= 1)
                return fallback;

            uint value = (uint)(cell.X * 374761393) + (uint)(cell.Y * 668265263) + 2166136261u;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return choices[value % (uint)choices.Length];
        }

        /// <summary>
        /// Whether a cell at this level has a visible side, and so needs the
        /// full block rather than a flat top.
        ///
        /// Only the two FRONT faces of an isometric cube are ever seen - the
        /// ones toward +x and +y on screen - so a side shows exactly when one of
        /// those neighbours sits lower, or when there is no neighbour at all.
        /// Everywhere else the flat top tiles seamlessly with its neighbours.
        /// </summary>
        private bool ShowsSide(GeneratedTerrainField field, Vector2I cell, int level, Vector2I size)
        {
            return Lower(cell + Vector2I.Right) || Lower(cell + Vector2I.Down);

            bool Lower(Vector2I at)
            {
                if (at.X >= size.X || at.Y >= size.Y)
                    return true;

                string kind = field.TerrainAtCell(at);
                if (kind.Length == 0)
                    return true;

                return LevelFor(kind, (int)field.ReliefAtCell(at)) < level;
            }
        }

        /// <summary>
        /// The height above which a mountain tile is drawn as a SUMMIT.
        ///
        /// Taken over the mountain tiles of this map rather than as a fixed
        /// number, so a low range and a high one each get a crest instead of one
        /// being uniformly summit and the other uniformly flank.
        ///
        /// This replaced a walk that measured how deep inside its massif a tile
        /// sat. That works on a broad massif and does nothing on a narrow one -
        /// every tile of a two-wide ridge is a rim tile - and these ranges are
        /// mostly narrow ridges, so only 11 tiles of 57 ever reached a summit.
        /// Height is the field that actually says which part of a range is its
        /// crest, and a ridge one tile wide still has one.
        /// </summary>
        private void MeasureSummitFloor(GeneratedTerrainField field, Vector2I size)
        {
            var heights = new List<float>();
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    var at = new Vector2I(x, y);
                    if ((int)field.ReliefAtCell(at) >= 2)
                        heights.Add(field.ElevationAtCell(at));
                }
            }

            if (heights.Count == 0)
            {
                // Nothing is a summit when nothing is a mountain. Above every
                // possible height rather than below, so the test cannot pass.
                _summitFloor = float.MaxValue;
                return;
            }

            heights.Sort();
            int index = Mathf.Clamp(
                Mathf.RoundToInt(heights.Count * (1.0f - SummitShare)), 0, heights.Count - 1);
            _summitFloor = heights[index];
        }

        /// <summary>
        /// Steps from the nearest land for every water cell, by breadth-first
        /// sweep out from the coast. Depth is not something the generator
        /// records, and taking it from the deep/shallow kind alone gives two
        /// flat terraces instead of a slope.
        /// </summary>
        private void MeasureWaterDepth(GeneratedTerrainField field, Vector2I size)
        {
            int count = size.X * size.Y;
            if (_depth.Length != count)
                _depth = new int[count];
            Array.Clear(_depth);

            var queue = new Queue<int>();
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    string kind = field.TerrainAtCell(new Vector2I(x, y));
                    if (!TerrainTileSets.IsLandKind(kind))
                        continue;

                    // Land is the source: its water neighbours are one step deep.
                    queue.Enqueue((y * size.X) + x);
                }
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int cx = index % size.X;
                int cy = index / size.X;
                for (int side = 0; side < 4; side++)
                {
                    int nx = cx + (side == 0 ? 1 : side == 1 ? -1 : 0);
                    int ny = cy + (side == 2 ? 1 : side == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= size.X || ny >= size.Y)
                        continue;

                    int next = (ny * size.X) + nx;
                    if (_depth[next] != 0)
                        continue;

                    string kind = field.TerrainAtCell(new Vector2I(nx, ny));
                    if (!TerrainTileSets.IsWaterKind(kind))
                        continue;

                    _depth[next] = _depth[index] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        /// <summary>
        /// What the bed is made of at a given depth: sand by the shore, then
        /// gravel, then bare rock. Reuses the terrain frames rather than adding
        /// three more, so a project that restyles its sand restyles its shallows
        /// with it.
        /// </summary>
        private Vector2I SeabedFrameFor(int step)
        {
            int bands = Mathf.Max(1, SeabedDepth);
            string kind = step < bands / 3 ? "sand"
                : step < (bands * 2) / 3 ? "gravel"
                : "rock";
            return _frames.TryGetValue(kind, out Vector2I frame) ? frame : new Vector2I(-1, -1);
        }

        /// <summary>
        /// Where a cell's top face sits on screen, elevation included.
        ///
        /// Anything drawn ON the map - trees, resources, units - needs this, and
        /// it must come from here rather than be recomputed: the projection, the
        /// level rule and the layer offsets are all owned by this component, and
        /// a second copy of that arithmetic would drift the moment any of them
        /// changed. The TileMapLayer itself does the grid-to-screen part, so
        /// even that is not reimplemented.
        /// </summary>
        public Vector2 SurfacePosition(Vector2I cell)
        {
            ResolveGenerator();
            if (_generator is null || _layers.Count == 0)
                return Vector2.Zero;

            return SurfacePosition(_generator.ResolveField(), cell);
        }

        /// <summary>
        /// The hot-path overload: a caller looping many cells resolves the
        /// field once and passes it, instead of each call paying the
        /// generator's settings rebuild through the public per-cell wrappers.
        /// </summary>
        internal Vector2 SurfacePosition(GeneratedTerrainField field, Vector2I cell)
        {
            if (_layers.Count == 0)
                return Vector2.Zero;

            // Every layer shares one grid, so the projection comes from any of
            // them and the height comes from the level. That is also what lets
            // the sea answer without owning a layer of its own.
            int level = LevelFor(field.TerrainAtCell(cell), (int)field.ReliefAtCell(cell));
            return _layers[0].MapToLocal(cell) + new Vector2(0.0f, -level * LevelHeight);
        }

        /// <summary>
        /// One entry per terrain level - its z index and how many tiles it
        /// actually painted. The interleaved stack is the thing most likely to
        /// regress silently, because a wrong z still renders a full map; this
        /// is what lets a guard read the order back instead of eyeballing it.
        /// </summary>
        public Godot.Collections.Array<Godot.Collections.Dictionary> GetLayerDiagnostics()
        {
            var report = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            if (_seabed is not null)
            {
                report.Add(new Godot.Collections.Dictionary
                {
                    { "kind", "seabed" },
                    { "level", -1 },
                    { "z", _seabed.ZIndex },
                    { "relative_z", _seabed.ZAsRelative },
                    { "cells", _seabed.GetUsedCells().Count },
                });
            }

            if (_water is not null)
            {
                report.Add(new Godot.Collections.Dictionary
                {
                    { "kind", "water" },
                    { "level", SeaLevel },
                    { "z", _water.ZIndex },
                    { "relative_z", _water.ZAsRelative },
                    { "cells", 0 },
                    { "surface", SurfaceReport(_waterMaterial) },
                });
            }

            for (int level = GroundLevel; level < LevelCount && LayerFor(level) < _layers.Count; level++)
            {
                TileMapLayer layer = _layers[LayerFor(level)];
                report.Add(new Godot.Collections.Dictionary
                {
                    { "kind", "terrain" },
                    { "level", level },
                    { "z", layer.ZIndex },
                    { "relative_z", layer.ZAsRelative },
                    { "cells", layer.GetUsedCells().Count },
                });
            }
            return report;
        }

        /// <summary>
        /// What a water layer's surface actually ended up as. A shader that
        /// fails to load leaves flat colour behind and nothing else complains,
        /// which is precisely the state this renderer exists to get out of.
        /// </summary>
        private static Godot.Collections.Dictionary SurfaceReport(ShaderMaterial? material)
            => new()
            {
                { "shaded", material?.Shader is not null },
                { "opacity", material?.GetShaderParameter("max_opacity").AsSingle() ?? 1.0f },
                { "lake_opacity", material?.GetShaderParameter("lake_opacity").AsSingle() ?? 1.0f },
            };

        /// <summary>True where the cell is land, so callers can skip the sea.</summary>
        public bool IsLandCell(Vector2I cell)
        {
            ResolveGenerator();
            return _generator is not null && IsLandCell(_generator.ResolveField(), cell);
        }

        /// <summary>Hot-path overload; see SurfacePosition(field, cell).</summary>
        internal static bool IsLandCell(GeneratedTerrainField field, Vector2I cell)
            => TerrainTileSets.IsLandKind(field.TerrainAtCell(cell));

        /// <summary>Which level a tile is drawn at. Owned by TerrainLayers.</summary>
        public static int LevelFor(string terrain, int relief)
            => TerrainLayers.LevelFor(terrain, relief);

        private void EnsureLayers()
        {
            EnsureWater();
            if (_layers.Count != LevelCount - GroundLevel)
            {
                _layers.Clear();
                for (int level = GroundLevel; level < LevelCount; level++)
                    _layers.Add(MakeLayer($"IsoLevel{level}"));
            }

            for (int level = GroundLevel; level < LevelCount; level++)
            {
                TileMapLayer layer = _layers[LayerFor(level)];
                layer.TileSet = _tileSet;
                // The same grid, drawn higher up the screen for each step.
                layer.Position = new Vector2(0.0f, -level * LevelHeight);
                layer.ZIndex = ZIndexForLevel(level);
            }

        }

        /// <summary>
        /// The seabed steps and the lake surface, plus the material that makes
        /// water move. Rebuilt whenever the depth changes so the stack always
        /// matches SeabedDepth.
        /// </summary>
        private void EnsureWater()
        {
            _seabed ??= MakeLayer("IsoSeabed");
            // Just under the surface: enough for the bed to sit below the water
            // rather than in it, and no more. The old stack dropped a further
            // step per depth band, which is what made the whole map three tile
            // heights deep.
            _seabed.Position = new Vector2(0.0f, SeabedStep);
            _seabed.ZIndex = TerrainLayers.ZForSeabed(0);
            _seabed.TileSet = _tileSet;

            EnsureWaterSurface();
        }

        /// <summary>
        /// The sea surface: one quad over the whole map, running the flat
        /// renderer's water shader.
        ///
        /// A quad rather than tiles because that shader reads a coast distance
        /// field per pixel to decide depth, clarity and where the surf breaks. A
        /// tile can only offer its own atlas UV, which says nothing about where
        /// the coastline is.
        /// </summary>
        private void EnsureWaterSurface()
        {
            if (string.IsNullOrWhiteSpace(WaterShaderPath))
                return;

            // EnsureLayer finds the existing layer or creates, parents and
            // adopts a new one. A second AddChild here used to fire Godot's
            // "already has a parent" error on every first build.
            if (_water is null || !GodotObject.IsInstanceValid(_water))
                _water = TerrainAuthoring.EnsureLayer(this, "IsoWater");

            _water.ZIndex = ZIndexForLevel(SeaLevel);
            _water.ZAsRelative = false;

            // The sea is tiles, on the SAME isometric grid as the terrain, so a
            // water cell sits exactly where its land neighbour does.
            //
            // The blank tile has to be a diamond rather than a rectangle.
            // Isometric cells overlap, and since this surface is transparent, a
            // rectangular tile would blend with its neighbours' halves twice
            // over and draw a brighter lattice across the whole ocean.
            Vector2I cell = new(Mathf.Max(2, CellSize.X), Mathf.Max(2, CellSize.Y));
            if (_water.TileSet is null || _water.TileSet.TileSize != cell)
                _water.TileSet = TerrainShaderSurface.BuildTileSet(cell, isometric: true);

            // Overscanned so the surface's own edge is never in frame: beyond the
            // map the shader draws plain open sea, so there is nothing to give
            // the boundary away. As a quad that was a scale multiplier; as cells
            // it is a ring of extra tiles, and it has to be BOUNDED. The old 2.5
            // multiplier on a 64-tile map means 160 cells of margin per side -
            // 147,000 tiles to fill for a sea that is opaque out there anyway.
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            int margin = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Max(size.X, size.Y) * Mathf.Max(0.0f, WaterOverscan)),
                0,
                MaxWaterMarginCells);

            TerrainShaderSurface.Fill(
                _water, new Vector2I(size.X + (margin * 2), size.Y + (margin * 2)));

            // Shift the layer so its filled cell (margin, margin) lands where map
            // cell (0, 0) belongs. In a diamond grid, moving by (-m, -m) cells is
            // a pure VERTICAL shift - the x terms cancel, since (-m) - (-m) = 0 -
            // which is why this is one number and not a diagonal.
            _water.Position = new Vector2(0.0f, -margin * Mathf.Max(1, CellSize.Y));
            _waterTileOffset = new Vector2(margin, margin);

            _waterMaterial = BuildWaterMaterial();
            _water.Material = _waterMaterial;
        }

        /// <summary>
        /// The water material, fed the same coast field and dials the flat
        /// renderer uses. One shader, one coast field, two projections.
        /// </summary>
        private ShaderMaterial? BuildWaterMaterial()
        {
            // Adopt the material saved with the scene before building a fresh
            // one: replacing it wiped every uniform hand-tuned in the
            // Inspector on each reload. Exported dials are still written below
            // and win; only the uniforms no export covers survive by this.
            ShaderMaterial material = _waterMaterial
                ?? _water?.Material as ShaderMaterial
                ?? new ShaderMaterial();
            if (material.Shader is null)
            {
                var shader = GD.Load<Shader>(WaterShaderPath);
                if (shader is null)
                {
                    GD.PushWarning($"[{Name}] could not load water shader '{WaterShaderPath}'; there will be no sea.");
                    return null;
                }
                material.Shader = shader;
            }

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));

            // The coast field is what tells the shader where the shore is, and
            // how far from it each pixel lies. Without it the water has no
            // shallows: it would draw at open-sea opacity right up to the beach.
            // Handing the shader a null texture does that SILENTLY, so say so
            // instead - the caller can see a warning, where it cannot see a
            // uniform that was never set.
            if (_coastMap is null)
            {
                GD.PushWarning(
                    $"[{Name}] the coast field is missing; the sea will draw without shallows.");
            }
            else
            {
                material.SetShaderParameter("coast_map", _coastMap);
            }

            // The quad's rectangle in THIS renderer's space. The shader resolves
            // both projections from it and the fragment UV rather than from a
            // world position, which carries any parent scaling with it.
            material.SetShaderParameter("coast_range", CoastRangeTiles);
            material.SetShaderParameter("map_size", new Vector2(size.X, size.Y));
            material.SetShaderParameter("cell_size", new Vector2(CellSize.X, CellSize.Y));
            material.SetShaderParameter("tile_offset", _waterTileOffset);
            material.SetShaderParameter("max_opacity", MaxOpacity);
            material.SetShaderParameter("clarity_tiles", ClarityTiles);
            material.SetShaderParameter("lake_opacity", LakeOpacity);
            material.SetShaderParameter("shore_opacity", ShoreOpacity);
            material.SetShaderParameter("wave_intensity", WaveIntensity);
            material.SetShaderParameter("foam_strength", FoamStrength);
            material.SetShaderParameter("deep_tiles", DeepTiles);
            material.SetShaderParameter("shallow_tiles", ShallowTiles);

            material.SetShaderParameter("foam_tiles_along", Mathf.Max(1.0f, FoamTilesAlong));
            material.SetShaderParameter("foam_tiles_across", Mathf.Max(0.3f, FoamTilesAcross));
            material.SetShaderParameter("foam_scroll", Mathf.Max(0.0f, FoamScroll));
            material.SetShaderParameter("foam_pulse", Mathf.Clamp(FoamPulse, 0.0f, 1.0f));
            material.SetShaderParameter("foam_arrival_rate", Mathf.Max(0.0f, FoamArrivalRate));
            material.SetShaderParameter("swell_direction_degrees", SwellDirectionDegrees);
            material.SetShaderParameter("swell_directionality", Mathf.Clamp(SwellDirectionality, 0.0f, 1.0f));

            SetTexture(material, "tex_shallow", ShallowTexturePath);
            SetTexture(material, "tex_deep", DeepTexturePath);
            SetTexture(material, "tex_sand", SandTexturePath);
            // Only switch to the authored-surf path if the art actually loaded.
            material.SetShaderParameter("use_foam_sheet", SetTexture(material, "foam_sheet", FoamSheetPath));
            return material;
        }

        /// <summary>
        /// Assigns a water material texture. Reports true so the caller can tell
        /// a sheet that loaded from one that did not - the foam sheet decides
        /// which surf path the shader takes, and turning that on without the art
        /// behind it draws no surf at all.
        /// </summary>
        private bool SetTexture(ShaderMaterial material, string parameter, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            Texture2D? texture = LoadTexture(path, parameter);
            if (texture is null)
                return false;

            material.SetShaderParameter(parameter, texture);
            return true;
        }

        private TileMapLayer MakeLayer(string name)
        {
            TileMapLayer layer = TerrainAuthoring.EnsureLayer(this, name);

            // Within a level, Y sorting puts nearer tiles in front.
            layer.YSortEnabled = true;
            // Godot batches tiles into quadrants and Y sorts whole quadrants
            // against each other. With blocks twice as tall as their cell, a
            // tile in front of its neighbour lands in the same batch and draws
            // in atlas order instead of depth order. One tile per quadrant is
            // what makes Y sorting actually per-tile.
            layer.RenderingQuadrantSize = 1;
            // Absolute, not relative to this node: the prop layers that
            // interleave with these live under a DIFFERENT parent, and relative
            // z would put every prop above every tile the moment either parent
            // moved off zero.
            layer.ZAsRelative = false;
            // Detailed art at map zoom is minified several times over, and
            // without a mip chain that aliases into a shimmering grid. The
            // sheets are imported with mipmaps for this reason - asking for the
            // filter without them falls back silently.
            layer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            return layer;
        }

        private bool EnsureTileSet()
        {
            if (_tileSet is not null && _frames.Count > 0)
                return true;

            Texture2D? sheet = LoadSheet();
            if (sheet is null)
                return false;

            int columns = Mathf.Max(1, SheetColumns);
            int rows = Mathf.Max(1, SheetRows);
            Vector2 sheetSize = sheet.GetSize();
            var region = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));

            var source = new TileSetAtlasSource { Texture = sheet, TextureRegionSize = region };
            var tileSet = new TileSet
            {
                TileShape = TileSet.TileShapeEnum.Isometric,
                TileLayout = TileSet.TileLayoutEnum.DiamondDown,
                TileOffsetAxis = TileSet.TileOffsetAxisEnum.Horizontal,
                TileSize = new Vector2I(Mathf.Max(2, CellSize.X), Mathf.Max(2, CellSize.Y)),
            };

            _frames.Clear();
            foreach ((string terrain, int frame) in TerrainFrames())
            {
                Vector2I coords = Coords(frame, columns, rows);
                if (coords.X < 0)
                    continue;

                // A frame may serve more than one terrain kind, and a tile can
                // only be created once.
                if (source.GetTileAtCoords(coords) == new Vector2I(-1, -1))
                {
                    source.CreateTile(coords);
                    if (source.GetTileData(coords, 0) is { } data)
                        data.TextureOrigin = new Vector2I(0, -BlockLift);
                }
                _frames[terrain] = coords;
            }

            BuildVariants(columns, rows);

            // Every frame any terrain may use, primary and variants alike. A
            // variant that never gets a tile created silently falls back to the
            // primary, which is the whole effect quietly not happening.
            var wanted = new HashSet<Vector2I>();
            foreach (Vector2I[] choices in _variants.Values)
            {
                foreach (Vector2I coords in choices)
                    wanted.Add(coords);
            }

            foreach (Vector2I coords in wanted)
            {
                if (coords.X >= columns || coords.Y >= rows)
                    continue;
                if (source.GetTileAtCoords(coords) != new Vector2I(-1, -1))
                    continue;

                source.CreateTile(coords);
                if (source.GetTileData(coords, 0) is { } extra)
                    extra.TextureOrigin = new Vector2I(0, -BlockLift);
            }

            // The flat-top sheet, on the same grid so a terrain's top is at the
            // frame its block is at. Without it every cell is a cube and level
            // ground grows a shaded seam between every pair of tiles.
            if (!string.IsNullOrWhiteSpace(TopSheetPath))
            {
                Texture2D? tops = LoadTexture(TopSheetPath, "flat-top sheet");
                if (tops is not null)
                {
                    Vector2 topSize = tops.GetSize();
                    var topSource = new TileSetAtlasSource
                    {
                        Texture = tops,
                        TextureRegionSize = new Vector2I(
                            Mathf.FloorToInt(topSize.X / columns),
                            Mathf.FloorToInt(topSize.Y / rows)),
                    };

                    var made = new HashSet<Vector2I>();
                    foreach (Vector2I coords in wanted)
                    {
                        if (coords.X >= columns || coords.Y >= rows || !made.Add(coords))
                            continue;

                        topSource.CreateTile(coords);
                        if (topSource.GetTileData(coords, 0) is { } top)
                            top.TextureOrigin = new Vector2I(0, -TopLift);
                    }

                    if (made.Count > 0)
                        tileSet.AddSource(topSource, TopSourceId);
                    else
                        GD.PushWarning($"[{Name}] '{TopSheetPath}' has no top for any mapped terrain.");
                }
            }

            if (_frames.Count == 0)
            {
                GD.PushWarning($"[{Name}] no isometric blocks were mapped from '{BlockSheetPath}'.");
                return false;
            }

            tileSet.AddSource(source, SourceId);
            _tileSet = tileSet;
            return true;
        }

        private static Vector2I Coords(int frame, int columns, int rows)
            => frame < 0 || frame >= columns * rows
                ? new Vector2I(-1, -1)
                : new Vector2I(frame % columns, frame / columns);

        /// <summary>
        /// Terrain kinds paired with the frame that draws them. One list, so a
        /// kind can never be painted without also being registered - an
        /// unregistered kind silently vanishes from the map.
        /// </summary>
        private IEnumerable<(string Terrain, int Frame)> TerrainFrames()
        {
            yield return ("grass", GrassFrame);
            yield return ("dry_grass", DryGrassFrame);
            yield return ("desert", DesertFrame);
            yield return ("sand", SandFrame);
            yield return ("tundra", TundraFrame);
            yield return ("snow", SnowFrame);
            yield return ("ice", IceFrame);
            yield return ("jungle", JungleFrame);
            yield return ("swamp", SwampFrame);
            yield return ("mud", SwampFrame);
            yield return ("gravel", GravelFrame);
            yield return ("rock", RockFrame);
            // No lava block art ships; the rock block stands in. Unregistered,
            // a lava cell was skipped entirely and left a hole in the map.
            yield return ("lava", RockFrame);
            yield return ("shallow_water", ShallowWaterFrame);
            yield return ("deep_water", DeepWaterFrame);
        }

        private Texture2D? LoadSheet()
        {
            if (string.IsNullOrWhiteSpace(BlockSheetPath))
            {
                GD.PushWarning($"[{Name}] no block sheet set; nothing to draw.");
                return null;
            }
            return LoadTexture(BlockSheetPath, "block sheet");
        }

        /// <summary>Named art, through the shared loader.</summary>
        private Texture2D? LoadTexture(string path, string what)
            => TerrainTextures.Load(path, Name, what);

        private void ResolveGenerator()
        {
            if (_generator is null || !GodotObject.IsInstanceValid(_generator))
                _generator = TerrainGeneratorPath.IsEmpty
                    ? null
                    : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
        }
    }
}
