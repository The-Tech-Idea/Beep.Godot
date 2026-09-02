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
    public partial class TerrainFeatureRendererComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(96, 60);
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;
        [Export] public int Seed { get; set; } = 31415;

        [ExportGroup("Sheets")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string WoodsSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int WoodsColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int WoodsRows { get; set; } = 4;
        [Export(PropertyHint.File, "*.png,*.webp")] public string JungleSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int JungleColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int JungleRows { get; set; } = 4;
        [Export(PropertyHint.File, "*.png,*.webp")] public string OasisSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int OasisColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int OasisRows { get; set; } = 4;
        [Export(PropertyHint.File, "*.png,*.webp")] public string MarshSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int MarshColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int MarshRows { get; set; } = 4;

        [ExportGroup("Look")]
        [Export(PropertyHint.Range, "0.2,3,0.05")] public float SpriteScale { get; set; } = 0.62f;
        [Export(PropertyHint.Range, "1,8,1")] public int SpritesPerTile { get; set; } = 4;

        /// <summary>
        /// Extra canopies on a dense stand. Closed forest and open woodland come
        /// from the same sheet; what separates them is how much of the tile is
        /// covered, so drawing both at one density would throw away the
        /// distinction the generator just made.
        /// </summary>
        [Export(PropertyHint.Range, "0,8,1")] public int ForestExtraSprites { get; set; } = 3;
        [Export(PropertyHint.Range, "0,1,0.01")] public float PositionJitter { get; set; } = 0.18f;
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

        /// <summary>One drawn sprite: sheet region, where, and how big.</summary>
        private readonly record struct Stamp(Texture2D Sheet, Rect2 Region, Rect2 Target, float SortY);

        private TerrainGeneratorComponent? _generator;
        private readonly Dictionary<string, Texture2D> _sheets = new();
        private readonly List<Stamp> _stamps = new();



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

        /// <summary>Rebuilds every feature sprite from the generator.</summary>
        public void Rebuild()
        {
            // The mipmaps built above are only used if the node asks for them.
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;

            // Above all terrain, below the markers. Everything is drawn from
            // this one node in painter's order, so the whole batch shares the
            // level it stands on.
            ZIndex = TerrainLayers.ZForProps(TerrainLayers.Ground);
            ZAsRelative = false;
            ResolveGenerator();
            _stamps.Clear();
            if (_generator is null)
            {
                GD.PushWarning($"[{Name}] no generator at TerrainGeneratorPath; no features were drawn.");
                QueueRedraw();
                return;
            }
            TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            LoadSheets();
            if (_sheets.Count == 0)
            {
                GD.PushWarning($"[{Name}] no feature sheets loaded, so no features were drawn.");
                QueueRedraw();
                return;
            }

            // Resolved ONCE per rebuild rather than once per cell; see
            // TerrainGeneratorComponent.ResolveField.
            GeneratedTerrainField field = _generator.ResolveField();
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            float tile = Mathf.Max(1, TileSize);

            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    string feature = field.FeatureAtCell(new Vector2I(x, y));
                    if (feature.Length == 0)
                        continue;

                    if (!TryDescribe(feature, out Texture2D? sheet, out int columns, out int rows) || sheet is null)
                        continue;

                    int clump = Mathf.Max(1, SpritesPerTile)
                        + (feature is TerrainFeatureStage.Forest or TerrainFeatureStage.Jungle
                            ? Mathf.Max(0, ForestExtraSprites) : 0);
                    for (int i = 0; i < clump; i++)
                        AddStamp(sheet, columns, rows, x, y, tile, i);
                }
            }

            // Painter's order: further up the map is drawn first, so a nearer
            // tree overlaps one behind it.
            _stamps.Sort((left, right) => left.SortY.CompareTo(right.SortY));
            QueueRedraw();
        }

        public override void _Draw()
        {
            foreach (Stamp stamp in _stamps)
                DrawTextureRectRegion(stamp.Sheet, stamp.Target, stamp.Region);
        }

        private bool TryDescribe(string feature, out Texture2D? sheet, out int columns, out int rows)
        {
            columns = 4;
            rows = 4;
            sheet = null;

            string key = feature switch
            {
                TerrainFeatureStage.Woods => "woods",
                // Dense forest is the same art, drawn thicker.
                TerrainFeatureStage.Forest => "woods",
                // Jungle falls back to the woods sheet when none is assigned, so
                // a missing sheet still shows vegetation rather than nothing.
                TerrainFeatureStage.Jungle => _sheets.ContainsKey("jungle") ? "jungle" : "woods",
                TerrainFeatureStage.Oasis => _sheets.ContainsKey("oasis") ? "oasis" : "woods",
                // No fallback to woods: reeds are not trees, and standing a
                // forest canopy in a bog would misdescribe the ground. Without a
                // marsh sheet the feature is simply not drawn.
                TerrainFeatureStage.Marsh => _sheets.ContainsKey("marsh") ? "marsh" : string.Empty,
                _ => string.Empty,
            };
            if (key.Length == 0 || !_sheets.TryGetValue(key, out sheet))
                return false;

            (columns, rows) = key switch
            {
                "jungle" => (Mathf.Max(1, JungleColumns), Mathf.Max(1, JungleRows)),
                "oasis" => (Mathf.Max(1, OasisColumns), Mathf.Max(1, OasisRows)),
                "marsh" => (Mathf.Max(1, MarshColumns), Mathf.Max(1, MarshRows)),
                _ => (Mathf.Max(1, WoodsColumns), Mathf.Max(1, WoodsRows)),
            };
            return true;
        }

        private void AddStamp(Texture2D sheet, int columns, int rows, int x, int y, float tile, int slot)
        {
            // Texture2D.GetSize returns floats, so the frame is computed and
            // then floored to whole pixels for the atlas region.
            Vector2 sheetSize = sheet.GetSize();
            var frame = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));
            int index = Mathf.FloorToInt(TerrainGeometry.Hash01(x, y, Seed + 811 + (slot * 97)) * columns * rows) % (columns * rows);
            var region = new Rect2(new Vector2(index % columns, index / columns) * frame, frame);

            // Scale so the sprite covers roughly one tile regardless of how
            // large the source art is.
            float fit = tile / Mathf.Max(1, Mathf.Max(frame.X, frame.Y));
            float jitterScale = 1.0f + ((TerrainGeometry.Hash01(x, y, Seed + 907 + (slot * 89)) - 0.5f) * 2.0f * ScaleJitter);
            Vector2 drawn = (Vector2)frame * fit * SpriteScale * jitterScale;

            var centre = new Vector2(
                (x + 0.5f + ((TerrainGeometry.Hash01(x, y, Seed + 1013 + (slot * 71)) - 0.5f) * PositionJitter)) * tile,
                // Nudged up so the trunk sits at the tile centre and the canopy
                // overhangs upward, which is how the eye reads depth.
                (y + 0.42f + ((TerrainGeometry.Hash01(x, y, Seed + 1117 + (slot * 67)) - 0.5f) * PositionJitter)) * tile);

            _stamps.Add(new Stamp(
                sheet,
                region,
                new Rect2(centre - (drawn * 0.5f), drawn),
                centre.Y));
        }

        private void LoadSheets()
        {
            if (_sheets.Count > 0)
                return;

            Add("woods", WoodsSheetPath);
            Add("jungle", JungleSheetPath);
            Add("oasis", OasisSheetPath);
            Add("marsh", MarshSheetPath);
        }

        private void Add(string key, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            // Mipmaps matter more here than anywhere else in the renderer: a
            // tree frame is around 310 pixels and is drawn about ten across with
            // the whole map in view, a minification of thirty to one. The shared
            // loader is what guarantees the chain exists.
            Texture2D? texture = TerrainTextures.Load(path, Name, $"the {key} feature sheet");
            if (texture is null)
                return;

            _sheets[key] = texture;
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
