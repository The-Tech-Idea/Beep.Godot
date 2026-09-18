using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>Where a resource renderer gets its icons.</summary>
    public enum ResourceIconSource
    {
        /// <summary>Use the bundled sheet for whatever set the generator is on.</summary>
        FollowGenerator = 0,

        /// <summary>Use the sheet, grid and order configured on this node.</summary>
        Custom = 1,
    }

    /// <summary>
    /// Draws terrain RESOURCES as icons on the map, the way a 4X shows what a
    /// tile is worth working.
    ///
    /// The generator has always assigned resources per tile and written them to
    /// the cell data, but the only thing that ever drew them was a debug overlay
    /// of coloured circles in the terrain lab. In a running game they were
    /// invisible: twenty-odd resource kinds decided every generation and thrown
    /// away.
    ///
    /// Icons come from a sheet of equal frames plus an ordered list naming which
    /// resource each frame is. The list is what lets one component serve any
    /// resource set - historical, oil and gas, off-world - without the renderer
    /// knowing anything about what the ids mean.
    ///
    /// A resource with no frame draws NOTHING. Substituting some other icon
    /// would tell the player a tile holds something it does not, which is worse
    /// than an honest gap.
    ///
    /// SHEETS SHIPPED WITH THE ADDON, and the IconOrder each one needs. The
    /// orders were read off the sheets rather than assumed: a generated sheet
    /// does not necessarily lay its icons out in the order it was asked for, and
    /// the space sheet in particular carries four extra icons interleaved with
    /// the twelve wanted. Blank entries skip a frame, which is what keeps the
    /// rest of the mapping aligned instead of shifting every icon after the gap.
    ///
    ///   Art/Icons/resources_historical_5x5.png   Columns 5, Rows 5
    ///     wheat, cattle, banana, deer, fish, whale, stone, gems, spices, wine,
    ///     furs, incense, ivory, silver, horses, iron, coal, oil, aluminium,
    ///     uranium
    ///
    ///   Art/Icons/resources_oil_and_gas.png      Columns 4, Rows 4
    ///     crude_oil, offshore_oil, natural_gas, offshore_gas, shale, oil_sands,
    ///     condensate, helium, sulphur, salt_dome, brine, coalbed_methane
    ///
    ///   Art/Icons/resources_space.png            Columns 4, Rows 4
    ///     water_ice, ammonia_ice, methane_ice, helium3, regolith, "", silicates,
    ///     iron_ore, "", titanium, rare_earths, platinum, "", "", thorium,
    ///     deuterium
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainResourceRendererComponent : TerrainRendererComponent, ISerializationListener
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");
        /// <summary>Optional subtree of live resource nodes; empty shows generated resources.</summary>
        [Export] public NodePath ResourceRootPath { get; set; } = new("");
        [Export] public NodePath GridPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(96, 60);
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export(PropertyHint.Range, "1,256,1")] public int TileSize { get; set; } = 64;

        [ExportGroup("Icons")]
        /// <summary>
        /// Where the icons come from. Following the generator is the default
        /// because the GENERATOR owns which resource set a map uses - the
        /// renderer only needs pictures for it. Configuring the set in one place
        /// and the icons in another is how they drift apart, and a drifted
        /// mapping does not fail loudly: it silently draws wheat for helium-3.
        /// </summary>
        [Export] public ResourceIconSource IconSource { get; set; } = ResourceIconSource.FollowGenerator;

        [Export(PropertyHint.File, "*.png,*.webp")] public string IconSheetPath { get; set; } = "";
        [Export(PropertyHint.Range, "1,16,1")] public int Columns { get; set; } = 4;
        [Export(PropertyHint.Range, "1,16,1")] public int Rows { get; set; } = 4;

        /// <summary>
        /// Resource ids in frame order, reading left to right then down. An id
        /// absent from this list is simply not drawn, so a sheet may cover part
        /// of a set without the rest turning into wrong icons.
        /// </summary>
        [Export] public string[] IconOrder { get; set; } = Array.Empty<string>();

        [ExportGroup("Look")]
        [Export(PropertyHint.Range, "0.1,2,0.05")] public float IconScale { get; set; } = 0.52f;
        /// <summary>Lifts the icon off the tile centre so ground detail stays readable.</summary>
        [Export(PropertyHint.Range, "-1,1,0.01")] public float VerticalOffset { get; set; } = -0.12f;
        [Export] public bool ShowBackplate { get; set; } = true;
        [Export] public Color BackplateColour { get; set; } = new(0.09f, 0.10f, 0.13f, 0.62f);
        // No z index export; the shared stack owns this. These are markers
        // rather than world objects, and they sit above the props so a forest
        // can never hide the thing the player is meant to click.

        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild.
        /// </summary>

        /// <summary>A bundled sheet and the frame order it needs.</summary>
        private readonly record struct IconPreset(string Path, int Columns, int Rows, string[] Order);

        private const string PresetRoot = "res://addons/beep_game_builder_cs/textures/resources/";

        /// <summary>
        /// The sheets that ship with the addon. Orders were read off the sheets
        /// rather than assumed - a generated sheet does not necessarily lay its
        /// icons out in the order it was asked for, and the space sheet carries
        /// four extra icons interleaved with the twelve wanted. Blank entries
        /// skip a frame, which keeps everything after the gap aligned.
        /// </summary>
        private static readonly Dictionary<ResourceSet, IconPreset> Presets = new()
        {
            [ResourceSet.Historical] = new(
                PresetRoot + "resources_historical_5x5.png", 5, 5, new[]
                {
                    "wheat", "cattle", "banana", "deer", "fish",
                    "whale", "stone", "gems", "spices", "wine",
                    "furs", "incense", "ivory", "silver", "horses",
                    "iron", "coal", "oil", "aluminium", "uranium",
                }),
            [ResourceSet.OilAndGas] = new(
                PresetRoot + "resources_oil_and_gas.png", 4, 4, new[]
                {
                    "crude_oil", "offshore_oil", "natural_gas", "offshore_gas",
                    "shale", "oil_sands", "condensate", "helium",
                    "sulphur", "salt_dome", "brine", "coalbed_methane",
                }),
            [ResourceSet.SpaceExploration] = new(
                PresetRoot + "resources_space.png", 4, 4, new[]
                {
                    "water_ice", "ammonia_ice", "methane_ice", "helium3",
                    "regolith", "", "silicates", "iron_ore",
                    "", "titanium", "rare_earths", "platinum",
                    "", "", "thorium", "deuterium",
                }),
        };

        private readonly record struct Icon(Rect2 Region, Rect2 Target, Vector2 Centre, float Radius);

        private TerrainGeneratorComponent? _generator;
        private Texture2D? _sheet;
        private string _sheetPath = "";
        private int _columns = 4;
        private int _rows = 4;
        private string[] _order = Array.Empty<string>();
        private string _loadedSheetPath = "";
        private TerrainResourceViewBinding? _liveResources;
        private GridProjectionComponent? _grid;
        private readonly Dictionary<string, int> _frames = new();
        private readonly List<Icon> _icons = new();
        public int IconCount => _icons.Count;

        public override void _Ready()
        {
            ResolveGenerator();
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override void _ExitTree()
        {
            _liveResources?.Dispose();
            if (GodotObject.IsInstanceValid(_grid)) _grid!.GeometryChanged -= QueueRebuild;
            _grid = null;
            ClearRebuildQueued();
        }

        // Release the plain managed helper's signal targets before Godot unloads
        // this assembly; scene exit alone does not cover editor hot reload.
        public void OnBeforeSerialize()
        {
            _liveResources?.Dispose();
            _liveResources = null;
            if (GodotObject.IsInstanceValid(_grid)) _grid!.GeometryChanged -= QueueRebuild;
            _grid = null;
            ClearRebuildQueued();
        }

        public void OnAfterDeserialize() => CallDeferred(nameof(RestoreResourceView));

        private void RestoreResourceView()
        {
            if (!IsInsideTree()) return;
            ResolveGenerator();
            QueueRebuild();
        }

        public override void _EnterTree()
        {
            if (HasRebuildAttempt && !Engine.IsEditorHint())
                Callable.From(() =>
                {
                    if (!IsInsideTree()) return;
                    ResolveGenerator();
                    QueueRebuild();
                }).CallDeferred();
        }
        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty && ResourceRootPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." }
                : Array.Empty<string>();

        /// <summary>Rebuilds every resource icon from the generator.</summary>
        public override void Rebuild()
        {
            HasRebuildAttempt = true;
            ClearRebuildQueued();
            ZIndex = TerrainLayers.ZForMarkers();
            ZAsRelative = false;
            // An icon is drawn at about half a tile from a sheet frame several times that,
            // so this view only minifies: without the mip chain a sheet of detailed icons
            // sparkles as the map moves. LINEAR above it, unlike the prop views, because
            // these icons were drawn with soft anti-aliased edges - keeping their texels
            // would only keep the jaggies the artist smoothed out.
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;

            ResolveGenerator();
            _icons.Clear();
            ApplyPreset();
            if ((_generator is null && ResourceRootPath.IsEmpty) || (!GridPath.IsEmpty && _grid is null) || !LoadSheet())
            {
                GD.PushWarning(
                    _generator is null
                        ? $"[{Name}] no generator at TerrainGeneratorPath; no resource icons were drawn."
                        : $"[{Name}] no resource icon sheet could be loaded, so no icons were drawn.");
                QueueRedraw();
                return;
            }
            if (_generator is not null && ResourceRootPath.IsEmpty)
                TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            // Resolved ONCE per rebuild rather than once per cell: every public
            // per-position accessor on the generator rebuilds and compares its
            // ~30-field settings record before returning this same cached field.
            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            float tile = Mathf.Max(1, TileSize);
            int columns = Mathf.Max(1, _columns);
            int rows = Mathf.Max(1, _rows);
            Vector2 sheetSize = _sheet!.GetSize();
            var frame = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));
            // Hoisted above the loop: a stackalloc in the body would grow the stack per cell.
            System.Span<Vector2> corners = stackalloc Vector2[4];

            foreach (var (cell, resource) in ResourceEntries(size))
            {
                if (resource.Length == 0 || !_frames.TryGetValue(resource, out int index)) continue;
                Vector2 centre = ((Vector2)cell + Vector2.One * 0.5f) * tile;
                float cellScale = tile;
                if (_grid is not null)
                {
                    centre = ToLocal(_grid.CellToWorld(cell));
                    if (!centre.IsFinite() || _grid.CellCorners(cell, corners) < 3) continue;
                    for (int i = 0; i < 4; i++) corners[i] = ToLocal(_grid.ToGlobal(corners[i]));
                    cellScale = Mathf.Min(corners[0].DistanceTo(corners[1]), corners[1].DistanceTo(corners[2]));
                }
                centre.Y += VerticalOffset * cellScale;
                var region = new Rect2(new Vector2(index % columns, index / columns) * frame, frame);
                float fit = cellScale / Mathf.Max(1, Mathf.Max(frame.X, frame.Y));
                Vector2 drawn = (Vector2)frame * fit * IconScale;
                _icons.Add(new Icon(region, new Rect2(centre - drawn * 0.5f, drawn),
                    centre, Mathf.Max(drawn.X, drawn.Y) * 0.58f));
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_sheet is null)
                return;

            // A dark disc behind each icon. Terrain art is high-contrast and a
            // small icon laid straight onto it disappears against rock or trees.
            if (ShowBackplate)
            {
                foreach (Icon icon in _icons)
                    DrawCircle(icon.Centre, icon.Radius, BackplateColour);
            }

            foreach (Icon icon in _icons)
                DrawTextureRectRegion(_sheet, icon.Target, icon.Region);
        }

        /// <summary>
        /// Chooses the sheet for this rebuild. Following the generator means the
        /// icons cannot disagree with the set the map was generated from.
        /// </summary>
        private void ApplyPreset()
        {
            if (IconSource == ResourceIconSource.Custom)
            {
                _sheetPath = IconSheetPath;
                _columns = Columns;
                _rows = Rows;
                _order = IconOrder ?? Array.Empty<string>();
                return;
            }

            ResourceSet set = _generator?.ResourceSet ?? ResourceSet.Historical;
            if (!Presets.TryGetValue(set, out IconPreset preset))
            {
                GD.PushWarning($"[{Name}] no bundled icon sheet for resource set {set}.");
                _sheetPath = "";
                return;
            }

            _sheetPath = preset.Path;
            _columns = preset.Columns;
            _rows = preset.Rows;
            _order = preset.Order;
        }

        private bool LoadSheet()
        {
            if (_loadedSheetPath != _sheetPath) _sheet = null;
            _loadedSheetPath = _sheetPath;
            if (_sheet is null && !string.IsNullOrWhiteSpace(_sheetPath))
                _sheet = TerrainTextures.Load(_sheetPath, Name, "the resource icon sheet");

            _frames.Clear();
            if (_order is { Length: > 0 })
            {
                for (int i = 0; i < _order.Length && i < Mathf.Max(1, _columns) * Mathf.Max(1, _rows); i++)
                {
                    string id = _order[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        _frames[id] = i;
                }
            }
            return _sheet is not null && _frames.Count > 0
                && _sheet.GetWidth() >= Mathf.Max(1, _columns) && _sheet.GetHeight() >= Mathf.Max(1, _rows);
        }

        private void ResolveGenerator()
        {
            _generator = TerrainGeneratorPath.IsEmpty ? null : GetNodeOrNull<TerrainGeneratorComponent>(TerrainGeneratorPath);
            _liveResources ??= new TerrainResourceViewBinding(QueueRebuild);
            _liveResources.Bind(ResourceRootPath.IsEmpty ? null : GetNodeOrNull<Node>(ResourceRootPath));
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            if (grid == _grid) return;
            if (GodotObject.IsInstanceValid(_grid)) _grid!.GeometryChanged -= QueueRebuild;
            _grid = grid;
            if (_grid is not null && !Engine.IsEditorHint()) _grid.GeometryChanged += QueueRebuild;
        }

        private IEnumerable<(Vector2I Cell, string Resource)> ResourceEntries(Vector2I size)
        {
            if (!ResourceRootPath.IsEmpty)
            {
                if (_liveResources is not null)
                    foreach (var entry in _liveResources.Entries())
                        if (new Rect2I(BoundsOrigin, size).HasPoint(entry.Cell)) yield return entry;
                yield break;
            }
            var field = _generator!.ResolveField();
            for (int y = 0; y < size.Y; y++)
                for (int x = 0; x < size.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    string resource = field.ResourceAtCell(cell);
                    if (resource.Length == 0) resource = field.LiquidResourceAtCell(cell);
                    if (resource.Length > 0) yield return (BoundsOrigin + cell, resource);
                }
        }

        /// <summary>Current drawn icon centers in this node's local coordinates.</summary>
        public Vector2[] GetIconCenters()
        {
            var centers = new Vector2[_icons.Count];
            for (int i = 0; i < centers.Length; i++) centers[i] = _icons[i].Centre;
            return centers;
        }
    }
}
