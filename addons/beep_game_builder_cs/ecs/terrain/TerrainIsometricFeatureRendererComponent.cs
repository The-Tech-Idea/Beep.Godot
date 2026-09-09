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
    /// asks TerrainIsometricRendererComponent where a cell's top face is rather
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
    public partial class TerrainIsometricFeatureRendererComponent : TerrainRendererComponent
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
        [Export] public TerrainPropSizing? PropSizing { get; set; }
        private TerrainPropSizing Sizing => PropSizing ?? TerrainPropSizing.Standard;

        /// <summary>The isometric renderer that owns the projection.</summary>
        [Export] public NodePath IsometricRendererPath { get; set; } = new("");

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
        // Each sheet's own grid. This view used to cut ALL FOUR sheets on
        // WoodsColumns/WoodsRows while exposing the other three paths and no layout
        // for them, so a marsh or oasis sheet authored on a different grid was sliced
        // correctly in the flat view and wrongly here, off the same map.
        //
        // Zero inherits the woods layout, which is exactly what every sheet used to
        // get - so scenes that already cut their sheets on one grid, like
        // terrain_iso_demo.tscn's 8x1 marsh against WoodsColumns = 8, keep working
        // untouched.
        [Export(PropertyHint.File, "*.png,*.webp")] public string JungleSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "0,16,1")] public int JungleColumns { get; set; }
        [Export(PropertyHint.Range, "0,16,1")] public int JungleRows { get; set; }
        [Export(PropertyHint.File, "*.png,*.webp")] public string MarshSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "0,16,1")] public int MarshColumns { get; set; }
        [Export(PropertyHint.Range, "0,16,1")] public int MarshRows { get; set; }
        [Export(PropertyHint.File, "*.png,*.webp")] public string OasisSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "0,16,1")] public int OasisColumns { get; set; }
        [Export(PropertyHint.Range, "0,16,1")] public int OasisRows { get; set; }

        [ExportGroup("Look")]
        [Export(PropertyHint.Range, "1,8,1")] public int SpritesPerTile { get; set; } = 2;
        [Export(PropertyHint.Range, "0,8,1")] public int ForestExtraSprites { get; set; } = 2;
        [Export(PropertyHint.Range, "0,1,0.01")] public float PositionJitter { get; set; } = 0.30f;
        [Export(PropertyHint.Range, "0,0.6,0.01")] public float ScaleJitter { get; set; } = 0.16f;

        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild.
        /// </summary>

        private readonly record struct Stamp(Texture2D Sheet, Rect2 Region, Rect2 Target, Vector2 Anchor, int Level);

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

        private TerrainGeneratorComponent? _generator;
        private TerrainIsometricRendererComponent? _iso;
        private TerrainIsometricRendererComponent? _connectedIso;

        /// <summary>The sheets, their grids and the woods frame bindings; see TerrainFeatureSheets.</summary>
        private readonly TerrainFeatureSheets _sheets = new();

        public override void _Ready()
        {
            Resolve();
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override void _ExitTree()
        {
            _residency = null;
            _residentStamps.Clear();
            SetProcess(false);
            if (_connectedIso is not null && GodotObject.IsInstanceValid(_connectedIso))
                _connectedIso.SurfaceRebuilt -= OnSurfaceRebuilt;
            _connectedIso = null;
            ClearRebuildQueued();
        }

        public override void _EnterTree()
        {
            if (HasRebuildAttempt && !Engine.IsEditorHint())
                Callable.From(() =>
                {
                    if (!IsInsideTree()) return;
                    Resolve();
                    QueueRebuild();
                }).CallDeferred();
        }
        private void OnSurfaceRebuilt()
        {
            HasRebuildAttempt = true;
            if (IsInsideTree() && IsVisibleInTree()) Rebuild();
        }
        public override string[] _GetConfigurationWarnings()
            => IsometricRendererPath.IsEmpty
                ? new[] { "IsometricRendererPath should point to a TerrainIsometricRendererComponent." }
                : Array.Empty<string>();

        /// <summary>Rebuilds every feature stamp from the generator.</summary>
        public override void Rebuild()
        {
            _residency = null;
            _residentStamps.Clear();
            SetProcess(false);
            HasRebuildAttempt = true;
            ClearRebuildQueued();
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;

            Resolve();
            EnsureLevels();
            foreach (LevelProps level in _levels)
                level.Stamps.Clear();

            ITerrainSurfaceData? field = _iso?.ResolveSurface();
            if (field is null || _iso is null || !_iso.HasSurface)
            {
                GD.PushWarning(
                    _generator is null
                        ? $"[{Name}] no generator at TerrainGeneratorPath; no features were drawn."
                        : $"[{Name}] no isometric renderer to place against, so no features were drawn.");
                Redraw();
                return;
            }

            _sheets.Load(Name,
                new TerrainFeatureSheets.Layout(WoodsSheetPath, WoodsColumns, WoodsRows),
                new TerrainFeatureSheets.Layout(JungleSheetPath, JungleColumns, JungleRows),
                new TerrainFeatureSheets.Layout(OasisSheetPath, OasisColumns, OasisRows),
                new TerrainFeatureSheets.Layout(MarshSheetPath, MarshColumns, MarshRows),
                WoodsFrameBindings);
            if (_sheets.Count == 0)
            {
                GD.PushWarning($"[{Name}] no feature sheets loaded, so no features were drawn.");
                Redraw();
                return;
            }
            TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _iso.BoundsSize);

            // Resolved ONCE per rebuild rather than once per cell; see
            // TerrainGeneratorComponent.ResolveField.
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            bool stream = StreamLargeMaps && !Engine.IsEditorHint() && (long)size.X * size.Y > 65536;
            var waterAt = _iso.CreateWaterSampler(stream);
            bool Dry(Vector2 at) => new Rect2(Vector2.Zero, (Vector2)size).HasPoint(at) && !waterAt(at);

            void BuildCell(int x, int y, List<Stamp> stamps)
            {
                    Span<Vector2> offsets = stackalloc Vector2[TerrainFeatureScatter.MaximumCount];
                    var cell = new Vector2I(x, y);
                    string feature = field.FeatureAtCell(cell);
                    // The field-taking overloads: the public per-cell wrappers
                    // would pay the generator's settings rebuild once per call,
                    // per wooded tile, per stamp.
                    if (feature.Length == 0 || !TerrainIsometricRendererComponent.IsLandCell(field, cell))
                        return;

                    if (!_sheets.TryGet(feature, out TerrainFeatureSheets.Sheet described) || described.Texture is null)
                        return;

                    Texture2D sheet = described.Texture;
                    int columns = described.Columns, rows = described.Rows;
                    int[]? frames = _sheets.FramesFor(described, field.TerrainAtCell(cell));

                    Vector2 top = ToLocal(_iso.ToGlobal(_iso.SurfacePosition(field, cell)));
                    System.Span<Vector2> corners = stackalloc Vector2[4];
                    if (_iso.SurfaceCorners(field, cell, corners) != 4) return;
                    for (int i = 0; i < 4; i++)
                        corners[i] = ToLocal(_iso.ToGlobal(corners[i]));
                    Vector2 across = corners[1] - corners[0];
                    Vector2 down = corners[2] - corners[1];
                    float diamond = corners[1].DistanceTo(corners[3]);
                    // A prop belongs to the level it stands on, so the terrain
                    // above can cover it.
                    int level = Mathf.Clamp(
                        _iso.SurfaceLevel(field, cell),
                        FirstPropLevel, TerrainLayers.Count - 1)
                        - FirstPropLevel;
                    int clump = Mathf.Clamp(SpritesPerTile, 1, 8)
                        + (feature is TerrainFeatureStage.Forest or TerrainFeatureStage.Jungle
                            ? Mathf.Clamp(ForestExtraSprites, 0, 8) : 0);

                    Vector2I identity = _iso.BoundsOrigin + cell;
                    int count = TerrainFeatureScatter.Fill(offsets[..clump], identity, Seed,
                        PositionJitter, (Vector2)cell + Vector2.One * 0.5f, Dry);
                    for (int slot = 0; slot < count; slot++)
                        AddStamp(stamps, sheet, columns, rows, frames, identity,
                            top, across, down, diamond, slot, offsets[slot], feature, level);
            }

            if (stream)
            {
                _residency = new TerrainPropResidency<Stamp>(this, null, _iso.BoundsOrigin, size, 1,
                    FeatureChunkSize, BuildCell, _iso.VisiblePropCells, () =>
                    {
                        var transform = _iso.GetGlobalTransformWithCanvas();
                        return Mathf.Max((transform.X * _iso.CellSize.X).Length(), (transform.Y * _iso.CellSize.Y).Length());
                    });
                SetProcess(true);
                Redraw();
                return;
            }
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++) BuildCell(x, y, _residentStamps);
            _residentStamps.Sort((a, b) => a.Anchor.Y.CompareTo(b.Anchor.Y));
            DistributeStamps();
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

        /// <summary>Actual drawn trunk anchors, in this renderer's local coordinates.</summary>
        public Godot.Collections.Array<Vector2> GetStampAnchors()
        {
            var anchors = new Godot.Collections.Array<Vector2>();
            foreach (var level in _levels)
                foreach (var stamp in level.Stamps) anchors.Add(stamp.Anchor);
            return anchors;
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
                    TerrainAuthoring.Adopt(node, this);
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
            Vector2I cell, Vector2 top, Vector2 across, Vector2 down, float diamond, int slot, Vector2 jitterOffset, string feature, int level)
        {
            Vector2 sheetSize = sheet.GetSize();
            var frame = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));
            if (frame.X <= 0 || frame.Y <= 0 || diamond <= 0) return;
            int count = frames?.Length ?? (columns * rows);
            int roll = Mathf.FloorToInt(TerrainGeometry.Hash01(cell.X, cell.Y, Seed + 5101 + (slot * 83)) * count) % count;
            int index = frames is null ? roll : frames[roll];
            var region = Sizing.VisibleRegion(sheet, columns, rows, index);
            frame = (Vector2I)region.Size;
            if (frame.X <= 0 || frame.Y <= 0) return;

            // A cell edge, not the diamond's diagonal: same unit as the flat renderer.
            float fit = Mathf.Min(across.Length(), down.Length()) / Mathf.Max(1, Mathf.Max(frame.X, frame.Y));
            float jitter = 1.0f + ((TerrainGeometry.Hash01(cell.X, cell.Y, Seed + 5227 + (slot * 79)) - 0.5f) * 2.0f * ScaleJitter);
            Vector2 drawn = (Vector2)frame * fit * Sizing.SizeInCells(feature, jitter);

            // Scatter within the diamond, not a square: an offset that ignores
            // the projection puts trees over the edge of their own tile.
            Vector2 offset = across * jitterOffset.X + down * jitterOffset.Y;

            // The trunk sits on the tile's top face; the canopy rises above it.
            Vector2 basePoint = top + offset;
            var target = new Rect2(
                basePoint - new Vector2(drawn.X * 0.5f, drawn.Y * 0.92f), drawn);
            stamps.Add(new Stamp(sheet, region, target, basePoint, level));
        }

        internal bool FollowsSurface(TerrainIsometricRendererComponent renderer)
        {
            Resolve();
            return !Engine.IsEditorHint() && _connectedIso == renderer;
        }

        public Godot.Collections.Array<Rect2> GetStampBounds()
        {
            var bounds = new Godot.Collections.Array<Rect2>();
            foreach (var level in _levels)
                foreach (var stamp in level.Stamps) bounds.Add(stamp.Target);
            return bounds;
        }

        private void Resolve()
        {
            _generator = TerrainGeneratorPath.IsEmpty
                ? null
                : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);

            _iso = IsometricRendererPath.IsEmpty
                ? null
                : GetNodeOrNull<TerrainIsometricRendererComponent>(IsometricRendererPath);
            if (_connectedIso != _iso)
            {
                if (_connectedIso is not null && GodotObject.IsInstanceValid(_connectedIso))
                    _connectedIso.SurfaceRebuilt -= OnSurfaceRebuilt;
                _connectedIso = _iso;
                if (_connectedIso is not null && !Engine.IsEditorHint()) _connectedIso.SurfaceRebuilt += OnSurfaceRebuilt;
            }
        }

    }
}
