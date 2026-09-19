using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Draws terrain features - woods, jungle, marsh, oasis - as sprites
    /// standing on the ground, the way Civilization shows them.
    ///
    /// This is the difference between a tile that *is* dark green and a tile
    /// that has a canopy on it. Colouring the ground cannot read as forest at
    /// any zoom; an object can.
    ///
    /// Sprites come from a sheet, and which one a tile gets is chosen by a
    /// seeded hash of its coordinates, so a wood is not the same tree repeated
    /// and the same map always produces the same trees.
    ///
    /// Everything is drawn from ONE node rather than as a Sprite2D per tree. A
    /// wooded map is thousands of sprites, and that many nodes costs real time
    /// to build and walk every frame; drawing them directly keeps the scene
    /// tree flat and the cost proportional to what is actually visible.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainFeatureRendererComponent : TerrainRendererComponent
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public TerrainMapArt? MapArt { get; set; }
        [Export] public TerrainPropSizing? PropSizing { get; set; }
        private TerrainPropSizing Sizing => PropSizing ?? TerrainPropSizing.Standard;

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(96, 60);
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;
        [Export] public int Seed { get; set; } = 31415;

        [ExportGroup("Sheets")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string WoodsSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int WoodsColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int WoodsRows { get; set; } = 4;
        /// <summary>Terrain-to-frame choices, e.g. "grass,dry_grass=0,1,4". Empty uses the whole sheet.</summary>
        [Export] public string[] WoodsFrameBindings { get; set; } = System.Array.Empty<string>();
        [Export(PropertyHint.File, "*.png,*.webp")] public string JungleSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int JungleColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int JungleRows { get; set; } = 4;
        [Export(PropertyHint.File, "*.png,*.webp")] public string OasisSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int OasisColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int OasisRows { get; set; } = 4;
        [Export(PropertyHint.File, "*.png,*.webp")] public string MarshSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int MarshColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int MarshRows { get; set; } = 4;

        /// <summary>
        /// Bushes standing among the trees of woods and forest - the understory. Every frame is used.
        /// Empty draws no bushes unless the MapArt supplies its own. Sized by TerrainPropSizing.Bushes.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string BushesSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int BushesColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int BushesRows { get; set; } = 4;

        [ExportGroup("Look")]
        [Export] public Vector2 SpriteAnchor { get; set; } = new(0.5f, 0.92f);
        [Export(PropertyHint.Range, "1,8,1")] public int SpritesPerTile { get; set; } = 1;

        /// <summary>
        /// Bushes in each woods or forest tile, placed in the clearings between its trees. They are
        /// drawn only where bush art exists - the bushes sheet above, or the MapArt's Bushes.
        /// </summary>
        [Export(PropertyHint.Range, "0,8,1")] public int BushesPerWoodsTile { get; set; } = 1;

        /// <summary>
        /// Extra canopies on a dense stand. Closed forest and open woodland come
        /// from the same sheet; what separates them is how much of the tile is
        /// covered, so drawing both at one density would throw away the
        /// distinction the generator just made.
        /// </summary>
        [Export(PropertyHint.Range, "0,8,1")] public int ForestExtraSprites { get; set; } = 1;
        [Export(PropertyHint.Range, "0,1,0.01")] public float PositionJitter { get; set; } = 0.85f;
        [Export(PropertyHint.Range, "0,0.6,0.01")] public float ScaleJitter { get; set; } = 0.18f;
        // No z index export. This one was the reason the trees were missing
        // from the tile view: it was declared, set to -84 in three scenes, and
        // NEVER ASSIGNED TO ANYTHING - so the node kept Node2D's default z of
        // 0. That happened to look right over the painted view, whose surface
        // sits far below, and put every tree under the tile view's ground the
        // moment its layers moved to the shared stack. An accepted setting that
        // enforces nothing is worse than no setting: the scenes said where the
        // trees went, and nothing read it.
        //
        // Props stand ON the ground, so the level is the stack's, not this
        // renderer's.

        /// <summary>
        /// One drawn sprite: sheet region, where, how big, and its kind - the feature it stands for,
        /// or "bush" - which is the size category it was drawn at.
        /// </summary>
        private readonly record struct Stamp(Texture2D Sheet, Rect2 Region, Rect2 Target, float SortY, Vector2 Anchor, string Kind);

        /// <summary>
        /// Above this many cells a rebuild is spread over frames instead of blocking one.
        ///
        /// DERIVED FROM COST, not from a round number. Measured on this renderer, 2026-09-19: the
        /// largest map the lab offers - Huge, 128x80, wet and young, the heaviest it can make -
        /// builds 2,083 stamps in 48.3 ms. That is 4.7 microseconds a cell, so a 16.7 ms frame is
        /// about 3,500 cells, and past that one rebuild visibly drops frames.
        ///
        /// It was 65,536 - a 256x256 map. The size ladder stops at Huge's 10,240, so NOTHING this
        /// game can generate ever reached it: the streaming path, its residency, its per-frame
        /// budget and its preload were configurable, guarded, and unreachable, while the biggest
        /// map blocked the main thread for three frames doing the work they exist to spread. The
        /// owner found it by looking for the chunking and not finding it (2026-09-19).
        /// </summary>
        private const int StreamAboveCells = 3500;

        /// <summary>Salt that keeps a cell's bush draws off the hash its trees drew from.</summary>
        private const int UnderstorySeedSalt = 6151;

        private TerrainGeneratorComponent? _generator;
        private GridCellDataComponent? _cells;
        private GridProjectionComponent? _grid;
        public int StampCount => _stamps.Count;
        private readonly TerrainFeatureSheets _sheets = new();
        private readonly List<Stamp> _stamps = new();



        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild,
        /// so the map is not built twice.
        /// </summary>

        public override void _Ready()
        {
            ResolveCells();
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override void _ExitTree()
        {
            ResetStreaming();
            _stamps.Clear();
            DisconnectCells();
            if (GodotObject.IsInstanceValid(_grid)) _grid!.GeometryChanged -= QueueRebuild;
            _grid = null;
            ClearRebuildQueued();
        }

        public override void _EnterTree()
        {
            if (HasRebuildAttempt && !Engine.IsEditorHint())
                Callable.From(() =>
                {
                    if (!IsInsideTree()) return;
                    ResolveCells();
                    ResolveGrid();
                    QueueRebuild();
                }).CallDeferred();
        }
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
            if (((TerrainChangeKind)kind & (TerrainChangeKind.Terrain | TerrainChangeKind.Navigation)) == 0) return;
            var cell = new Vector2I(x, y);
            if (new Rect2I(BoundsOrigin, BoundsSize).HasPoint(cell) && !InvalidateFeatureCell(cell)) QueueRebuild();
        }
        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty && CellDataPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." }
                : System.Array.Empty<string>();

        /// <summary>Rebuilds every feature sprite from the generator.</summary>
        public override void Rebuild()
        {
            ResetStreaming();
            HasRebuildAttempt = true;
            ClearRebuildQueued();
            ResolveCells();
            ResolveGrid();
            // Trees and bushes are authored sprite frames, and TerrainPropSizing.DrawnPixels
            // refuses to draw one larger than its art - so this view only ever MINIFIES, and
            // the mip chain the sheets are loaded with is what keeps a tree drawn a few pixels
            // across from aliasing at map zoom.
            //
            // NEAREST above that chain: the only magnification left is the player's own zoom,
            // and a linear filter there interpolates between the artist's pixels, turning a
            // hard cartoon edge into a smear. Nearest keeps the drawn pixels the drawn pixels.
            TextureFilter = TextureFilterEnum.NearestWithMipmaps;

            // Above all terrain, below the markers. Everything is drawn from
            // this one node in painter's order, so the whole batch shares the
            // level it stands on.
            ZIndex = TerrainLayers.ZForProps(TerrainLayers.Ground);
            ZAsRelative = false;
            ResolveGenerator();
            _stamps.Clear();
            if ((!CellDataPath.IsEmpty && _cells is null) || (_cells is null && _generator is null)
                || (!GridPath.IsEmpty && _grid is null))
            {
                GD.PushWarning($"[{Name}] configured terrain or grid source is missing; no features were drawn.");
                QueueRedraw();
                return;
            }
            if (_cells is null && _generator is not null)
                TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            _sheets.Load(Name,
                new TerrainFeatureSheets.Layout(WoodsSheetPath, WoodsColumns, WoodsRows),
                new TerrainFeatureSheets.Layout(JungleSheetPath, JungleColumns, JungleRows),
                new TerrainFeatureSheets.Layout(OasisSheetPath, OasisColumns, OasisRows),
                new TerrainFeatureSheets.Layout(MarshSheetPath, MarshColumns, MarshRows),
                WoodsFrameBindings);
            _sheets.LoadUnderstory(Name, new TerrainFeatureSheets.Layout(BushesSheetPath, BushesColumns, BushesRows));
            if (_sheets.Count == 0 && !_sheets.TryGetUnderstory(out _) && (MapArt is null ||
                MapArt.Trees.Count + MapArt.Oasis.Count + MapArt.Marsh.Count + MapArt.Bushes.Count == 0))
            {
                GD.PushWarning($"[{Name}] no feature sheets loaded, so no features were drawn.");
                QueueRedraw();
                return;
            }

            // Resolved ONCE per rebuild rather than once per cell; see
            // TerrainGeneratorComponent.ResolveField.
            ITerrainSurfaceData source = _cells is null ? _generator!.ResolveField() : new LiveTerrainSurfaceData(_cells);
            Vector2I sourceOrigin = _cells is null ? Vector2I.Zero : BoundsOrigin;
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            float tile = Mathf.Max(1, TileSize);
            if (StreamLargeMaps && !Engine.IsEditorHint() && IsInsideTree() && (long)size.X * size.Y > StreamAboveCells)
            {
                BeginStreaming(source, sourceOrigin, size, tile);
                QueueRedraw();
                return;
            }
            System.Func<Vector2, bool> waterAt = _cells is not null
                ? TerrainCoastField.CreateLiveWaterSampler(_cells, BoundsOrigin, size)
                : ((GeneratedTerrainField)source).IsWaterAtPosition;
            bool Dry(Vector2 at) => new Rect2(Vector2.Zero, (Vector2)size).HasPoint(at) && !waterAt(at);
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    BuildCell(source, sourceOrigin, tile, x, y, Dry, _stamps);
                }
            }

            _stamps.Sort(ByLayerThenDepth);
            QueueRedraw();
        }

        /// <summary>
        /// The order this batch is drawn in: UNDERSTORY FIRST, then depth.
        ///
        /// Painter's order by itself - further up the map drawn first - is what makes a nearer tree
        /// overlap one behind it, and that is still the second key. But it is the wrong ONLY key
        /// across kinds: a bush is ground cover a few tenths of a tile high, and sorting it against
        /// a tree by centre alone put any bush slightly down-screen of a trunk over the whole
        /// canopy. The owner asked for the low things to sit under the tall ones (2026-09-18).
        ///
        /// A layer key rather than a z index because both kinds are drawn from this ONE node, in
        /// list order; small rocks are a different node and take TerrainLayers.ZForClutter instead.
        /// One comparator, used by the full rebuild and by the streaming merge alike - two copies
        /// would let a streamed map and a built one stack their props differently.
        /// </summary>
        private static int ByLayerThenDepth(Stamp left, Stamp right)
        {
            int layer = Understory(left).CompareTo(Understory(right));
            return layer != 0 ? layer : left.SortY.CompareTo(right.SortY);
        }

        private static int Understory(Stamp stamp) => stamp.Kind == "bush" ? 0 : 1;

        public override void _Draw()
        {
            foreach (Stamp stamp in _stamps)
                DrawTextureRectRegion(stamp.Sheet, stamp.Target, stamp.Region);
        }

        private void BuildCell(ITerrainSurfaceData source, Vector2I sourceOrigin, float tile,
            int x, int y, System.Func<Vector2, bool> dry, List<Stamp> stamps)
        {
            var cell = new Vector2I(x, y);
            string feature = source.FeatureAtCell(sourceOrigin + cell);
            if (feature.Length == 0) return;
            System.Span<Vector2> offsets = stackalloc Vector2[TerrainFeatureScatter.MaximumCount];
            int trees = 0;
            var art = MapArt?.FeatureTextures(feature);
            bool individual = art is { Count: > 0 };
            bool hasSheet = _sheets.TryGet(feature, out TerrainFeatureSheets.Sheet described);
            Texture2D? sheet = hasSheet ? described.Texture : null;
            if (individual || (hasSheet && sheet is not null))
            {
                int columns = described.Columns, rows = described.Rows;
                int[]? frames = _sheets.FramesFor(described, source.TerrainAtCell(sourceOrigin + cell));
                int clump = Mathf.Clamp(SpritesPerTile, 1, 8)
                    + (feature is TerrainFeatureStage.Forest or TerrainFeatureStage.Jungle ? Mathf.Clamp(ForestExtraSprites, 0, 8) : 0);
                trees = TerrainFeatureScatter.Fill(offsets[..clump], BoundsOrigin + cell, Seed,
                    PositionJitter, (Vector2)cell + Vector2.One * 0.5f, dry);
                for (int i = 0; i < trees; i++)
                {
                    Texture2D selected = individual ? art![Mathf.FloorToInt(TerrainGeometry.Hash01(
                        BoundsOrigin.X + x, BoundsOrigin.Y + y, Seed + 811 + i * 97) * art.Count) % art.Count] : sheet!;
                    if (!GodotObject.IsInstanceValid(selected)) continue;
                    AddStamp(selected, individual ? 1 : columns, individual ? 1 : rows, individual ? null : frames,
                        x, y, tile, i, offsets[i], feature, stamps);
                }
            }
            if (feature is TerrainFeatureStage.Woods or TerrainFeatureStage.Forest)
                AddUnderstory(x, y, tile, dry, offsets, trees, stamps);
        }

        /// <summary>
        /// The bushes of a woods or forest tile, after its trees and in the clearings between them:
        /// the shared scatter keeps each bush as far from the trunks as from the other bushes. Drawn
        /// from the MapArt's Bushes when it has them, else from the bushes sheet, and sized as "bush".
        /// Jungle, oasis and marsh carry none - a jungle is canopy, and reeds and palms are their own art.
        /// </summary>
        private void AddUnderstory(int x, int y, float tile, System.Func<Vector2, bool> dry,
            System.Span<Vector2> offsets, int trees, List<Stamp> stamps)
        {
            int wanted = Mathf.Clamp(BushesPerWoodsTile, 0, 8);
            if (wanted == 0) return;
            var art = MapArt?.Bushes;
            bool individual = art is { Count: > 0 };
            bool hasSheet = _sheets.TryGetUnderstory(out TerrainFeatureSheets.Sheet sheet);
            if (!individual && !hasSheet) return;
            var cell = new Vector2I(x, y);
            int total = TerrainFeatureScatter.Append(offsets[..Mathf.Min(trees + wanted, TerrainFeatureScatter.MaximumCount)],
                trees, BoundsOrigin + cell, Seed + UnderstorySeedSalt, PositionJitter, (Vector2)cell + Vector2.One * 0.5f, dry);
            for (int i = trees; i < total; i++)
            {
                Texture2D selected = individual ? art![Mathf.FloorToInt(TerrainGeometry.Hash01(
                    BoundsOrigin.X + x, BoundsOrigin.Y + y, Seed + UnderstorySeedSalt + i * 97) * art.Count) % art.Count] : sheet.Texture;
                if (!GodotObject.IsInstanceValid(selected)) continue;
                AddStamp(selected, individual ? 1 : sheet.Columns, individual ? 1 : sheet.Rows, null,
                    x, y, tile, i, offsets[i], "bush", stamps);
            }
        }

        private void AddStamp(Texture2D sheet, int columns, int rows, int[]? frames, int x, int y, float tile, int slot,
            Vector2 jitter, string feature, List<Stamp> stamps)
        {
            // Texture2D.GetSize returns floats, so the frame is computed and
            // then floored to whole pixels for the atlas region.
            Vector2 sheetSize = sheet.GetSize();
            var frame = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));
            if (frame.X <= 0 || frame.Y <= 0) return;
            Vector2I cell = BoundsOrigin + new Vector2I(x, y);
            int count = frames?.Length ?? (columns * rows);
            int roll = Mathf.FloorToInt(TerrainGeometry.Hash01(cell.X, cell.Y, Seed + 811 + (slot * 97)) * count) % count;
            int index = frames is null ? roll : frames[roll];
            var region = Sizing.VisibleRegion(sheet, columns, rows, index);
            frame = (Vector2I)region.Size;
            if (frame.X <= 0 || frame.Y <= 0) return;

            Vector2 centre = ((Vector2)cell + Vector2.One * 0.5f) * tile;
            Vector2 across = Vector2.Right * tile, down = Vector2.Down * tile;
            if (_grid is not null)
            {
                centre = ToLocal(_grid.CellToWorld(cell));
                System.Span<Vector2> corners = stackalloc Vector2[4];
                if (!centre.IsFinite() || _grid.CellCorners(cell, corners) != 4) return;
                for (int i = 0; i < 4; i++) corners[i] = ToLocal(_grid.ToGlobal(corners[i]));
                across = corners[1] - corners[0];
                down = corners[3] - corners[0];
                tile = Mathf.Min(across.Length(), down.Length());
            }

            // The size the prop covers in cells, capped at the art's own resolution: TerrainPropSizing
            // owns both halves of that rule, so every view draws a sheet at the same size.
            float jitterScale = 1.0f + ((TerrainGeometry.Hash01(cell.X, cell.Y, Seed + 907 + (slot * 89)) - 0.5f) * 2.0f * ScaleJitter);
            Vector2 drawn = Sizing.DrawnPixels((Vector2)frame, tile, feature, jitterScale);

            centre += across * jitter.X + down * jitter.Y;

            stamps.Add(new Stamp(
                sheet,
                region,
                new Rect2(centre - (drawn * SpriteAnchor), drawn),
                centre.Y, centre, feature));
        }

        private void ResolveGenerator()
        {
            _generator = TerrainGeneratorPath.IsEmpty ? null
                : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
        }

        private void ResolveGrid()
        {
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            if (_grid == grid) return;
            if (GodotObject.IsInstanceValid(_grid)) _grid!.GeometryChanged -= QueueRebuild;
            _grid = grid;
            if (_grid is not null && !Engine.IsEditorHint()) _grid.GeometryChanged += QueueRebuild;
        }

        /// <summary>Actual drawn centers, in local coordinates, including deterministic jitter.</summary>
        public Vector2[] GetStampCenters()
        {
            var centers = new Vector2[_stamps.Count];
            for (int i = 0; i < centers.Length; i++) centers[i] = _stamps[i].Target.GetCenter();
            return centers;
        }

        /// <summary>Actual ground anchors, independent of the artwork's SpriteAnchor.</summary>
        public Vector2[] GetStampAnchors()
        {
            var anchors = new Vector2[_stamps.Count];
            for (int i = 0; i < anchors.Length; i++) anchors[i] = _stamps[i].Anchor;
            return anchors;
        }

        /// <summary>Actual sprite-frame bounds in renderer-local units, including size jitter.</summary>
        public Godot.Collections.Array<Rect2> GetStampBounds()
        {
            var bounds = new Godot.Collections.Array<Rect2>();
            foreach (var stamp in _stamps) bounds.Add(stamp.Target);
            return bounds;
        }

        /// <summary>
        /// The bounds of the stamps of one kind - a feature ("woods", "forest", "jungle", "oasis",
        /// "marsh") or "bush" - so each size category can be measured against its own range.
        /// </summary>
        public Godot.Collections.Array<Rect2> GetStampBoundsOfKind(string kind)
        {
            var bounds = new Godot.Collections.Array<Rect2>();
            foreach (var stamp in _stamps)
                if (stamp.Kind == kind) bounds.Add(stamp.Target);
            return bounds;
        }

    }
}
