using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Draws terrain RELIEF - hills and mountains - as objects standing on the
    /// ground, the way the feature renderer draws woods.
    ///
    /// The generator already decides relief per tile and writes it to the cell
    /// data, but until now the only trace of it in a rendered map was the ground
    /// material turning grey. Height cannot read as height from a flat colour at
    /// any zoom; an object with a lit face and a shadow can. This surfaces data
    /// the pipeline was already computing and throwing away.
    ///
    /// Everything is drawn from ONE node rather than a Sprite2D per rock. A
    /// mountainous map is thousands of stamps, and that many nodes costs real
    /// time to build and walk every frame.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainReliefRendererComponent : TerrainRendererComponent
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
        /// <summary>Authoritative live terrain. Empty uses the generated field.</summary>
        [Export] public NodePath CellDataPath { get; set; } = new("");
        /// <summary>Optional gameplay grid for native cell positions, including parent transforms.</summary>
        [Export] public NodePath GridPath { get; set; } = new("");
        [Export] public TerrainMapArt? MapArt { get; set; }
        [Export] public TerrainPropSizing? PropSizing { get; set; }
        private TerrainPropSizing Sizing => PropSizing ?? TerrainPropSizing.Standard;

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(96, 60);
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;
        [Export] public int Seed { get; set; } = 20261;

        [ExportGroup("Art")]
        /// <summary>Individual transparent sprites; when assigned, used instead of the hill sheet.</summary>
        [Export] public Godot.Collections.Array<Texture2D> HillsTextures { get; set; } = new();
        /// <summary>Individual transparent sprites; when assigned, used instead of the mountain sheet.</summary>
        [Export] public Godot.Collections.Array<Texture2D> MountainsTextures { get; set; } = new();
        /// <summary>
        /// Sheets are grids of equal frames on transparent background, in the
        /// same shape as the feature renderer's tree sheets. A relief level is
        /// hidden when both its individual textures and sheet path are empty.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string HillsSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int HillsColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int HillsRows { get; set; } = 4;
        [Export(PropertyHint.File, "*.png,*.webp")] public string MountainsSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int MountainsColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int MountainsRows { get; set; } = 4;

        [ExportGroup("Look")]
        [Export(PropertyHint.Range, "0,1,0.01")] public float HillsCoverage { get; set; } = 1f;
        [Export(PropertyHint.Range, "0,1,0.01")] public float MountainsCoverage { get; set; } = 1f;
        [Export(PropertyHint.Range, "1,8,1")] public int HillsPerTile { get; set; } = 2;
        [Export(PropertyHint.Range, "1,8,1")] public int MountainsPerTile { get; set; } = 1;
        [Export(PropertyHint.Range, "0,1,0.01")] public float PositionJitter { get; set; } = 0.22f;
        [Export(PropertyHint.Range, "0,0.6,0.01")] public float ScaleJitter { get; set; } = 0.16f;
        // No z index export; the shared stack owns this. Relief draws the
        // hills and mountains standing on the ground, so it takes the prop slot
        // of the highest level it draws - above the trees, which is what lets a
        // peak occlude a tree standing in front of it.

        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild,
        /// so the map is not built twice.
        /// </summary>

        /// <summary>One drawn sprite: sheet region, where, and how big.</summary>
        private readonly record struct Stamp(Texture2D Sheet, Rect2 Region, Rect2 Target, float SortY);

        private TerrainGeneratorComponent? _generator;
        private GridCellDataComponent? _cells;
        private GridProjectionComponent? _grid;
        private Texture2D? _hills;
        private Texture2D? _mountains;
        private string _loadedHillsPath = "";
        private string _loadedMountainsPath = "";
        private readonly List<Stamp> _stamps = new();
        public int StampCount => _stamps.Count;

        public override void _Ready()
        {
            ResolveSources();
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override void _ExitTree()
        {
            ResetStreaming();
            _stamps.Clear();
            DisconnectSources();
            ClearRebuildQueued();
        }

        public override void _EnterTree()
        {
            if (HasRebuildAttempt && !Engine.IsEditorHint())
                Callable.From(() =>
                {
                    if (!IsInsideTree()) return;
                    ResolveSources();
                    QueueRebuild();
                }).CallDeferred();
        }
        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty && CellDataPath.IsEmpty
                ? new[] { "Set CellDataPath for live terrain or TerrainGeneratorPath for generated terrain." }
                : Array.Empty<string>();

        /// <summary>Rebuilds relief from live cells, or the field when no live source is configured.</summary>
        public override void Rebuild()
        {
            ResetStreaming();
            HasRebuildAttempt = true;
            ClearRebuildQueued();
            ZIndex = TerrainLayers.ZForProps(TerrainLayers.Mountains);
            ZAsRelative = false;
            // The sheets are mipmapped; without asking for them a peak drawn a
            // few pixels across aliases into noise at map zoom.
            TextureFilter = MapArt?.PixelArt == true ? TextureFilterEnum.NearestWithMipmaps : TextureFilterEnum.LinearWithMipmaps;

            ResolveSources();
            _stamps.Clear();
            if ((!CellDataPath.IsEmpty && _cells is null)
                || (_cells is null && _generator is null)
                || (!GridPath.IsEmpty && _grid is null))
            {
                GD.PushWarning($"[{Name}] configured terrain or grid source is missing; no relief was drawn.");
                QueueRedraw();
                return;
            }
            if (_cells is null && _generator is not null)
                TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            LoadSheets();
            // Resolved ONCE per rebuild rather than once per cell; see
            // TerrainGeneratorComponent.ResolveField.
            ITerrainSurfaceData field = _cells is not null
                ? new LiveTerrainSurfaceData(_cells) : _generator!.ResolveField();
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            float tile = Mathf.Max(1, TileSize);
            if (StreamLargeMaps && !Engine.IsEditorHint() && IsInsideTree() && (long)size.X * size.Y > 65536)
            {
                BeginStreaming(field, size, tile);
                QueueRedraw();
                return;
            }
            Func<Vector2, bool> waterAt = _cells is not null
                ? TerrainCoastField.CreateLiveWaterSampler(_cells, BoundsOrigin, size)
                : ((GeneratedTerrainField)field).IsWaterAtPosition;
            bool Dry(Vector2 at) => new Rect2(Vector2.Zero, (Vector2)size).HasPoint(at) && !waterAt(at);
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    BuildCell(field, tile, x, y, Dry, _stamps);
                }
            }

            // Painter's order: a nearer peak overlaps one behind it, which is
            // most of what makes a range read as having depth.
            _stamps.Sort((left, right) => left.SortY.CompareTo(right.SortY));
            QueueRedraw();
        }

        public override void _Draw()
        {
            foreach (Stamp stamp in _stamps)
                DrawTextureRectRegion(stamp.Sheet, stamp.Target, stamp.Region);
        }

        private void BuildCell(ITerrainSurfaceData field, float tile, int x, int y, Func<Vector2, bool> dry, List<Stamp> stamps)
        {
            var localCell = new Vector2I(x, y);
            var cell = BoundsOrigin + localCell;
            Vector2I sample = _cells is null ? localCell : cell;
            if (TerrainTileSets.IsWaterKind(field.TerrainAtCell(sample))) return;
            TerrainRelief relief = field.ReliefAtCell(sample);
            if (relief == TerrainRelief.Flat) return;
            bool mountain = relief == TerrainRelief.Mountains;
            float coverage = mountain ? MountainsCoverage : HillsCoverage;
            if (!float.IsFinite(coverage) || TerrainGeometry.Hash01(cell.X, cell.Y, Seed + 4051) >= Mathf.Clamp(coverage, 0f, 1f)) return;
            Texture2D? sheet = mountain ? _mountains : _hills;
            var textures = mountain ? MountainsTextures : HillsTextures;
            var styled = mountain ? MapArt?.LargeRocks : MapArt?.SmallRocks;
            if (styled is { Count: > 0 }) textures = styled;
            if (sheet is null && textures.Count == 0) return;
            int columns = Mathf.Max(1, mountain ? MountainsColumns : HillsColumns);
            int rows = Mathf.Max(1, mountain ? MountainsRows : HillsRows);
            int count = Mathf.Clamp(mountain ? MountainsPerTile : HillsPerTile, 1, 8);
            Span<Vector2> offsets = stackalloc Vector2[TerrainFeatureScatter.MaximumCount];
            count = TerrainFeatureScatter.Fill(offsets[..count], cell, Seed, PositionJitter,
                (Vector2)localCell + Vector2.One * 0.5f, dry);
            for (int slot = 0; slot < count; slot++)
            {
                Texture2D? texture = textures.Count == 0 ? sheet : textures[
                    Mathf.FloorToInt(TerrainGeometry.Hash01(cell.X, cell.Y, Seed + 4177 + slot * 83) * textures.Count) % textures.Count];
                if (!GodotObject.IsInstanceValid(texture)) continue;
                AddStamp(texture!, textures.Count == 0 ? columns : 1, textures.Count == 0 ? rows : 1,
                    cell.X, cell.Y, tile, slot, offsets[slot], mountain, stamps);
            }
        }

        /// <summary>Actual sprite-frame bounds in renderer-local units, including size jitter.</summary>
        public Godot.Collections.Array<Rect2> GetStampBounds()
        {
            var bounds = new Godot.Collections.Array<Rect2>();
            foreach (var stamp in _stamps) bounds.Add(stamp.Target);
            return bounds;
        }

        private void AddStamp(
            Texture2D sheet, int columns, int rows, int x, int y, float tile, int slot, Vector2 offset, bool mountain, List<Stamp> stamps)
        {
            var cell = new Vector2I(x, y);
            Vector2 centre = CellPosition(cell, _grid);
            Vector2 across = Vector2.Right * tile, down = Vector2.Down * tile;
            if (_grid is not null)
            {
                // Use this cell's top face, not neighbours that may be absent or at another height.
                Vector2[] corners = _grid.CellCorners(cell);
                if (corners.Length != 4) return;
                for (int i = 0; i < corners.Length; i++) corners[i] = ToLocal(_grid.ToGlobal(corners[i]));
                across = corners[1] - corners[0];
                down = corners[3] - corners[0];
            }
            if (!centre.IsFinite() || !across.IsFinite() || !down.IsFinite()) return;
            if (_grid is not null) tile = Mathf.Min(across.Length(), down.Length());
            Vector2 sheetSize = sheet.GetSize();
            var frame = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));
            int frames = columns * rows;
            int index = Mathf.FloorToInt(TerrainGeometry.Hash01(x, y, Seed + 4111 + (slot * 83)) * frames) % frames;
            var region = Sizing.VisibleRegion(sheet, columns, rows, index);
            frame = (Vector2I)region.Size;
            if (frame.X <= 0 || frame.Y <= 0) return;

            float fit = tile / Mathf.Max(1, Mathf.Max(frame.X, frame.Y));
            float jitter = 1.0f + ((TerrainGeometry.Hash01(x, y, Seed + 4231 + (slot * 79)) - 0.5f) * 2.0f * ScaleJitter);
            float limited = Sizing.SizeInCells(mountain ? "large_rock" : "small_rock", jitter);
            Vector2 drawn = (Vector2)frame * fit * limited;

            centre += across * offset.X + down * offset.Y;

            stamps.Add(new Stamp(sheet, region, new Rect2(centre - (drawn * 0.5f), drawn), centre.Y));
        }

        private void LoadSheets()
        {
            if (_loadedHillsPath != HillsSheetPath) _hills = null;
            if (_loadedMountainsPath != MountainsSheetPath) _mountains = null;
            _loadedHillsPath = HillsSheetPath;
            _loadedMountainsPath = MountainsSheetPath;
            _hills ??= Load(HillsSheetPath);
            _mountains ??= Load(MountainsSheetPath);
        }

        private Texture2D? Load(string path)
            => TerrainTextures.Load(path, Name, "relief sheet");

        /// <summary>Relief anchor in this renderer's coordinates, from the same grid used by gameplay.</summary>
        public Vector2 CellPosition(Vector2I cell)
        {
            if (!GridPath.IsEmpty)
            {
                var grid = GetNodeOrNull<GridProjectionComponent>(GridPath);
                return grid is null ? new Vector2(float.NaN, float.NaN) : CellPosition(cell, grid);
            }
            return CellPosition(cell, null);
        }

        private Vector2 CellPosition(Vector2I cell, GridProjectionComponent? grid)
            => grid is not null ? ToLocal(grid.CellToWorld(cell))
                : ((Vector2)cell + Vector2.One * 0.5f) * Mathf.Max(1, TileSize);

        private void ResolveSources()
        {
            _generator = TerrainGeneratorPath.IsEmpty ? null : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
            var cells = CellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(CellDataPath);
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            if (_cells == cells && _grid == grid) return;
            DisconnectSources();
            _cells = cells;
            _grid = grid;
            if (Engine.IsEditorHint()) return;
            if (_cells is not null)
            {
                _cells.CellChanged += OnCellChanged;
                _cells.CellsChanged += OnCellsChangedSignal;
            }
            if (_grid is not null) _grid.GeometryChanged += QueueRebuild;
        }

        private void DisconnectSources()
        {
            if (GodotObject.IsInstanceValid(_cells))
            {
                _cells!.CellChanged -= OnCellChanged;
                _cells.CellsChanged -= OnCellsChangedSignal;
            }
            if (GodotObject.IsInstanceValid(_grid)) _grid!.GeometryChanged -= QueueRebuild;
            _cells = null;
            _grid = null;
        }

        private void OnCellChanged(int x, int y, int kind)
        {
            if (((TerrainChangeKind)kind & (TerrainChangeKind.Terrain | TerrainChangeKind.Navigation)) == 0) return;
            var cell = new Vector2I(x, y);
            if (!new Rect2I(BoundsOrigin, BoundsSize).HasPoint(cell)) return;
            if (_residency is not null) _residency.InvalidateCell(cell);
            else QueueRebuild();
        }
    }
}
