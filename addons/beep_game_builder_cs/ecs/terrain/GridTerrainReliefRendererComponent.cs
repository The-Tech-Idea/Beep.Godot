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
    public partial class GridTerrainReliefRendererComponent : Node2D
    {
        /// <summary>Matches TerrainRelief, which is internal to the generation layer.</summary>
        private const int ReliefHills = 1;
        private const int ReliefMountains = 2;

        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(96, 60);
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;
        [Export] public int Seed { get; set; } = 20261;

        [ExportGroup("Sheets")]
        /// <summary>
        /// Sheets are grids of equal frames on transparent background, in the
        /// same shape as the feature renderer's tree sheets. Leave one empty and
        /// that relief level simply is not drawn - deliberately, so a project can
        /// ship hills without mountains, rather than half-drawing both.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string HillsSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int HillsColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int HillsRows { get; set; } = 4;
        [Export(PropertyHint.File, "*.png,*.webp")] public string MountainsSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int MountainsColumns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int MountainsRows { get; set; } = 4;

        [ExportGroup("Look")]
        [Export(PropertyHint.Range, "0.2,4,0.05")] public float HillsScale { get; set; } = 0.72f;
        [Export(PropertyHint.Range, "0.2,4,0.05")] public float MountainsScale { get; set; } = 1.25f;
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
        [Export] public bool RefreshOnReady { get; set; } = true;

        /// <summary>One drawn sprite: sheet region, where, and how big.</summary>
        private readonly record struct Stamp(Texture2D Sheet, Rect2 Region, Rect2 Target, float SortY);

        private GridTerrainGeneratorComponent? _generator;
        private Texture2D? _hills;
        private Texture2D? _mountains;
        private readonly List<Stamp> _stamps = new();

        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a GridTerrainGeneratorComponent." }
                : Array.Empty<string>();

        /// <summary>Rebuilds every relief stamp from the generator.</summary>
        public void Rebuild()
        {
            ZIndex = TerrainLayers.ZForProps(TerrainLayers.Mountains);
            ZAsRelative = false;
            // The sheets are mipmapped; without asking for them a peak drawn a
            // few pixels across aliases into noise at map zoom.
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;

            ResolveGenerator();
            _stamps.Clear();
            if (_generator is null)
            {
                QueueRedraw();
                return;
            }

            LoadSheets();
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            float tile = Mathf.Max(1, TileSize);

            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    int relief = _generator.ReliefAt(new Vector2I(x, y));
                    Texture2D? sheet = relief switch
                    {
                        ReliefMountains => _mountains,
                        ReliefHills => _hills,
                        _ => null,
                    };
                    if (sheet is null)
                        continue;

                    bool mountain = relief == ReliefMountains;
                    int columns = Mathf.Max(1, mountain ? MountainsColumns : HillsColumns);
                    int rows = Mathf.Max(1, mountain ? MountainsRows : HillsRows);
                    int count = Mathf.Max(1, mountain ? MountainsPerTile : HillsPerTile);
                    float scale = mountain ? MountainsScale : HillsScale;

                    for (int slot = 0; slot < count; slot++)
                        AddStamp(sheet, columns, rows, x, y, tile, slot, scale);
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

        private void AddStamp(
            Texture2D sheet, int columns, int rows, int x, int y, float tile, int slot, float scale)
        {
            Vector2 sheetSize = sheet.GetSize();
            var frame = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));
            int frames = columns * rows;
            int index = Mathf.FloorToInt(Hash01(x, y, Seed + 4111 + (slot * 83)) * frames) % frames;
            var region = new Rect2(new Vector2(index % columns, index / columns) * frame, frame);

            float fit = tile / Mathf.Max(1, Mathf.Max(frame.X, frame.Y));
            float jitter = 1.0f + ((Hash01(x, y, Seed + 4231 + (slot * 79)) - 0.5f) * 2.0f * ScaleJitter);
            Vector2 drawn = (Vector2)frame * fit * scale * jitter;

            var centre = new Vector2(
                (x + 0.5f + ((Hash01(x, y, Seed + 4357 + (slot * 71)) - 0.5f) * PositionJitter)) * tile,
                // Nudged up so the base sits at the tile centre and the mass
                // rises above it, which is how the eye reads elevation.
                (y + 0.40f + ((Hash01(x, y, Seed + 4463 + (slot * 67)) - 0.5f) * PositionJitter)) * tile);

            _stamps.Add(new Stamp(sheet, region, new Rect2(centre - (drawn * 0.5f), drawn), centre.Y));
        }

        private void LoadSheets()
        {
            _hills ??= Load(HillsSheetPath);
            _mountains ??= Load(MountainsSheetPath);
        }

        private Texture2D? Load(string path)
            => TerrainTextures.Load(path, Name, "relief sheet");

        private void ResolveGenerator()
        {
            if (_generator is null || !GodotObject.IsInstanceValid(_generator))
                _generator = TerrainGeneratorPath.IsEmpty
                    ? null
                    : GetNodeOrNull<GridTerrainGeneratorComponent>(TerrainGeneratorPath);
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
