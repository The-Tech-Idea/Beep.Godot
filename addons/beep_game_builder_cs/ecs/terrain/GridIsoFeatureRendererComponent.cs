using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Draws terrain FEATURES - woods, forest, jungle, marsh, oasis - standing on
    /// an isometric map.
    ///
    /// The same features the flat renderer draws, in the other projection. It
    /// asks GridIsoTileMapRendererComponent where a cell's top face is rather
    /// than recomputing the isometric transform: that component owns the
    /// projection, the elevation rule and the layer offsets, and a second copy of
    /// that arithmetic drifts the moment any of them changes. A tree standing
    /// beside its own hill instead of on it is the usual symptom.
    ///
    /// One node per ELEVATION LEVEL drawing everything on that level - a wooded
    /// map is thousands of sprites, and that many nodes costs real time to build
    /// and walk every frame, but a single node for all of them loses which level
    /// a tree stands on. The stack is sea, ground, upper, then ground props and
    /// upper props above all of it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridIsoFeatureRendererComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        /// <summary>The isometric renderer that owns the projection.</summary>
        [Export] public NodePath IsoRendererPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(48, 48);
        [Export] public int Seed { get; set; } = 40961;

        [ExportGroup("Sheets")]
        [Export(PropertyHint.File, "*.png,*.webp")] public string WoodsSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int WoodsColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int WoodsRows { get; set; } = 4;

        /// <summary>
        /// Which frames of the woods sheet suit which terrain, as
        /// "kind[,kind...]=frame[,frame...]" - for example "tundra,snow=7".
        ///
        /// A tree sheet is a mix of climates: cherry blossom, autumn and snow
        /// sit beside the plain greens. Picking uniformly across the whole
        /// sheet scatters all of them everywhere, which is what makes a
        /// temperate wood read as confetti. The component cannot tell which
        /// frame is which - only the sheet's author can - so the mapping is
        /// data. Leave it empty to use the whole sheet.
        /// </summary>
        [Export] public string[] WoodsFrameBindings { get; set; } = Array.Empty<string>();
        [Export(PropertyHint.File, "*.png,*.webp")] public string JungleSheetPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string MarshSheetPath { get; set; } = "";
        [Export(PropertyHint.File, "*.png,*.webp")] public string OasisSheetPath { get; set; } = "";

        [ExportGroup("Look")]
        /// <summary>Sprite width as a fraction of one diamond's width.</summary>
        [Export(PropertyHint.Range, "0.1,2,0.01")] public float SpriteScale { get; set; } = 0.62f;
        [Export(PropertyHint.Range, "1,8,1")] public int SpritesPerTile { get; set; } = 2;
        [Export(PropertyHint.Range, "0,8,1")] public int ForestExtraSprites { get; set; } = 2;
        [Export(PropertyHint.Range, "0,1,0.01")] public float PositionJitter { get; set; } = 0.30f;
        [Export(PropertyHint.Range, "0,0.6,0.01")] public float ScaleJitter { get; set; } = 0.16f;

        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild.
        /// </summary>
        [Export] public bool RefreshOnReady { get; set; } = true;

        private readonly record struct Stamp(Texture2D Sheet, Rect2 Region, Rect2 Target, float SortY);

        /// <summary>
        /// Draws the props belonging to ONE elevation level, at the z just above
        /// that level's terrain. Without this every prop lands on top of every
        /// tile, and a tree on low ground floats over the cliff in front of it.
        /// </summary>
        private partial class LevelProps : Node2D
        {
            public readonly List<Stamp> Stamps = new();

            public override void _Draw()
            {
                foreach (Stamp stamp in Stamps)
                    DrawTextureRectRegion(stamp.Sheet, stamp.Target, stamp.Region);
            }
        }

        /// <summary>
        /// Lowest level that can carry props. The sea carries none - features
        /// are land-only - so no node is made for it, and the stack is exactly
        /// the five layers it should be: sea, ground, ground props, upper,
        /// upper props.
        /// </summary>
        private const int FirstPropLevel = 1;

        /// <summary>Prop nodes for levels FirstPropLevel upward.</summary>
        private readonly List<LevelProps> _levels = new();

        /// <summary>Terrain kind to the frames of the woods sheet it may use.</summary>
        private readonly Dictionary<string, int[]> _woodsFrames = new();
        private readonly HashSet<string> _unbound = new();

        private GridTerrainGeneratorComponent? _generator;
        private GridIsoTileMapRendererComponent? _iso;
        private readonly Dictionary<string, Texture2D> _sheets = new();

        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
            => IsoRendererPath.IsEmpty
                ? new[] { "IsoRendererPath should point to a GridIsoTileMapRendererComponent." }
                : Array.Empty<string>();

        /// <summary>Rebuilds every feature stamp from the generator.</summary>
        public void Rebuild()
        {
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;

            Resolve();
            EnsureLevels();
            foreach (LevelProps level in _levels)
                level.Stamps.Clear();

            if (_generator is null || _iso is null)
            {
                Redraw();
                return;
            }

            LoadSheets();
            LoadWoodsFrames();
            if (_sheets.Count == 0)
            {
                Redraw();
                return;
            }

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            float diamond = Mathf.Max(8, _iso.CellSize.X);

            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    string feature = _generator.FeatureAt(cell);
                    if (feature.Length == 0 || !_iso.IsLandCell(cell))
                        continue;

                    if (!TryDescribe(feature, out Texture2D? sheet, out int columns, out int rows) || sheet is null)
                        continue;

                    // Only the woods sheet is a climate mix; the others are one
                    // subject each, so they use every frame they have.
                    int[]? frames = feature is TerrainFeatureStage.Woods or TerrainFeatureStage.Forest
                        ? FramesFor(_generator.TerrainKindAt(cell), columns * rows)
                        : null;

                    Vector2 top = _iso.SurfacePosition(cell);
                    // A prop belongs to the level it stands on, so the terrain
                    // above can cover it.
                    int level = Mathf.Clamp(
                        TerrainLayers.LevelFor(
                            _generator.TerrainKindAt(cell), _generator.ReliefAt(cell)),
                        FirstPropLevel, TerrainLayers.Count - 1)
                        - FirstPropLevel;
                    int clump = Mathf.Max(1, SpritesPerTile)
                        + (feature is TerrainFeatureStage.Forest or TerrainFeatureStage.Jungle
                            ? Mathf.Max(0, ForestExtraSprites) : 0);

                    for (int slot = 0; slot < clump; slot++)
                        AddStamp(_levels[level].Stamps, sheet, columns, rows, frames, cell, top, diamond, slot);
                }
            }

            // Painter's order down the screen, which in isometric is also order
            // away from the viewer.
            foreach (LevelProps level in _levels)
                level.Stamps.Sort((left, right) => left.SortY.CompareTo(right.SortY));
            Redraw();
        }

        /// <summary>
        /// One entry per prop level - its z index and how many sprites it holds.
        /// Pairs with the terrain renderer's report so a guard can assert the
        /// whole stack interleaves.
        /// </summary>
        public Godot.Collections.Array<Godot.Collections.Dictionary> GetLayerDiagnostics()
        {
            var report = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            for (int index = 0; index < _levels.Count; index++)
            {
                report.Add(new Godot.Collections.Dictionary
                {
                    { "kind", "props" },
                    { "level", index + FirstPropLevel },
                    { "z", _levels[index].ZIndex },
                    { "relative_z", _levels[index].ZAsRelative },
                    { "cells", _levels[index].Stamps.Count },
                });
            }
            return report;
        }

        private void Redraw()
        {
            foreach (LevelProps level in _levels)
                level.QueueRedraw();
        }

        /// <summary>
        /// One node per elevation level, each sitting at the odd z directly above
        /// that level's terrain: sea, ground, ground props, upper, upper props.
        /// </summary>
        private void EnsureLevels()
        {
            int wanted = TerrainLayers.Count - FirstPropLevel;
            if (_levels.Count == wanted)
                return;

            _levels.Clear();
            for (int level = FirstPropLevel; level < TerrainLayers.Count; level++)
            {
                string name = $"Props{level}";
                var node = GetNodeOrNull<LevelProps>(name);
                if (node is null || !GodotObject.IsInstanceValid(node))
                {
                    node = new LevelProps { Name = name };
                    AddChild(node);
                    if (Engine.IsEditorHint() && Owner is not null)
                        node.Owner = Owner;
                }
                // Above ALL terrain, ordered among themselves by level. The
                // renderer owns this rule - a single shared z for every prop is
                // what used to put a tree on low ground over the cliff in front
                // of it - so there is deliberately no export to override it.
                node.ZIndex = TerrainLayers.ZForProps(level);
                node.ZAsRelative = false;
                node.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
                _levels.Add(node);
            }
        }

        private void AddStamp(
            List<Stamp> stamps,
            Texture2D sheet, int columns, int rows, int[]? frames,
            Vector2I cell, Vector2 top, float diamond, int slot)
        {
            Vector2 sheetSize = sheet.GetSize();
            var frame = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));
            int count = frames?.Length ?? (columns * rows);
            int roll = Mathf.FloorToInt(Hash01(cell.X, cell.Y, Seed + 5101 + (slot * 83)) * count) % count;
            int index = frames is null ? roll : frames[roll];
            var region = new Rect2(new Vector2(index % columns, index / columns) * frame, frame);

            float fit = diamond / Mathf.Max(1, Mathf.Max(frame.X, frame.Y));
            float jitter = 1.0f + ((Hash01(cell.X, cell.Y, Seed + 5227 + (slot * 79)) - 0.5f) * 2.0f * ScaleJitter);
            Vector2 drawn = (Vector2)frame * fit * SpriteScale * jitter;

            // Scatter within the diamond, not a square: an offset that ignores
            // the projection puts trees over the edge of their own tile.
            float u = (Hash01(cell.X, cell.Y, Seed + 5333 + (slot * 71)) - 0.5f) * PositionJitter;
            float v = (Hash01(cell.X, cell.Y, Seed + 5449 + (slot * 67)) - 0.5f) * PositionJitter;
            var offset = new Vector2(
                (u - v) * _iso!.CellSize.X * 0.5f,
                (u + v) * _iso.CellSize.Y * 0.5f);

            // The trunk sits on the tile's top face; the canopy rises above it.
            Vector2 basePoint = top + offset;
            var target = new Rect2(
                basePoint - new Vector2(drawn.X * 0.5f, drawn.Y * 0.86f), drawn);
            stamps.Add(new Stamp(sheet, region, target, basePoint.Y));
        }

        private bool TryDescribe(string feature, out Texture2D? sheet, out int columns, out int rows)
        {
            columns = Mathf.Max(1, WoodsColumns);
            rows = Mathf.Max(1, WoodsRows);
            sheet = null;

            string key = feature switch
            {
                TerrainFeatureStage.Woods or TerrainFeatureStage.Forest => "woods",
                TerrainFeatureStage.Jungle => _sheets.ContainsKey("jungle") ? "jungle" : "woods",
                TerrainFeatureStage.Oasis => _sheets.ContainsKey("oasis") ? "oasis" : "woods",
                // No fallback to woods: reeds are not trees, and a canopy in a
                // bog would misdescribe the ground.
                TerrainFeatureStage.Marsh => _sheets.ContainsKey("marsh") ? "marsh" : string.Empty,
                _ => string.Empty,
            };
            return key.Length > 0 && _sheets.TryGetValue(key, out sheet);
        }

        /// <summary>
        /// The frames a terrain may use, or null for the whole sheet. An
        /// out-of-range frame is dropped rather than clamped: clamping would
        /// quietly draw the wrong tree, and this is a typo in the binding.
        /// </summary>
        private int[]? FramesFor(string terrain, int total)
        {
            if (_woodsFrames.Count == 0)
                return null;
            if (_woodsFrames.TryGetValue(terrain, out int[]? frames))
                return frames;

            // Bindings exist but this terrain is not in them - the author meant
            // to control every climate and missed one, so say which.
            if (_unbound.Add(terrain))
                GD.PushWarning($"[{Name}] no WoodsFrameBindings entry for terrain '{terrain}'; using the whole sheet.");
            _ = total;
            return null;
        }

        private void LoadWoodsFrames()
        {
            _woodsFrames.Clear();
            _unbound.Clear();
            int total = Mathf.Max(1, WoodsColumns) * Mathf.Max(1, WoodsRows);

            foreach (string entry in WoodsFrameBindings)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] halves = entry.Split('=', StringSplitOptions.TrimEntries);
                if (halves.Length != 2)
                {
                    GD.PushWarning($"[{Name}] woods binding '{entry}' is not \"kind[,kind...]=frame[,frame...]\".");
                    continue;
                }

                var frames = new List<int>();
                foreach (string piece in halves[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(piece.Trim(), out int frame) || frame < 0 || frame >= total)
                        GD.PushWarning($"[{Name}] woods binding '{entry}' names frame '{piece}', outside 0..{total - 1}.");
                    else
                        frames.Add(frame);
                }

                if (frames.Count == 0)
                    continue;

                foreach (string kind in halves[0].Split(',', StringSplitOptions.RemoveEmptyEntries))
                    _woodsFrames[kind.Trim()] = frames.ToArray();
            }
        }

        private void LoadSheets()
        {
            if (_sheets.Count > 0)
                return;

            Add("woods", WoodsSheetPath);
            Add("jungle", JungleSheetPath);
            Add("marsh", MarshSheetPath);
            Add("oasis", OasisSheetPath);
        }

        private void Add(string key, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            Texture2D? texture = TerrainTextures.Load(path, Name, $"the {key} feature sheet");
            if (texture is not null)
                _sheets[key] = texture;
        }

        private void Resolve()
        {
            if (_generator is null || !GodotObject.IsInstanceValid(_generator))
                _generator = TerrainGeneratorPath.IsEmpty
                    ? null
                    : GetNodeOrNull<GridTerrainGeneratorComponent>(TerrainGeneratorPath);

            if (_iso is null || !GodotObject.IsInstanceValid(_iso))
                _iso = IsoRendererPath.IsEmpty
                    ? null
                    : GetNodeOrNull<GridIsoTileMapRendererComponent>(IsoRendererPath);
        }

        private static float Hash01(int x, int y, int seed)
        {
            uint value = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215.0f;
        }
    }
}
