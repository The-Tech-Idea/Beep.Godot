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
    public partial class GridTerrainResourceRendererComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(96, 60);
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
        [Export] public int RenderZIndex { get; set; } = -40;

        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild.
        /// </summary>
        [Export] public bool RefreshOnReady { get; set; } = true;

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

        private GridTerrainGeneratorComponent? _generator;
        private Texture2D? _sheet;
        private string _sheetPath = "";
        private int _columns = 4;
        private int _rows = 4;
        private string[] _order = Array.Empty<string>();
        private ResourceSet? _presetApplied;
        private readonly Dictionary<string, int> _frames = new();
        private readonly List<Icon> _icons = new();

        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
            => TerrainGeneratorPath.IsEmpty
                ? new[] { "TerrainGeneratorPath should point to a GridTerrainGeneratorComponent." }
                : Array.Empty<string>();

        /// <summary>Rebuilds every resource icon from the generator.</summary>
        public void Rebuild()
        {
            ZIndex = RenderZIndex;
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;

            ResolveGenerator();
            _icons.Clear();
            ApplyPreset();
            if (_generator is null || !LoadSheet())
            {
                QueueRedraw();
                return;
            }

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));
            float tile = Mathf.Max(1, TileSize);
            int columns = Mathf.Max(1, _columns);
            int rows = Mathf.Max(1, _rows);
            Vector2 sheetSize = _sheet!.GetSize();
            var frame = new Vector2I(
                Mathf.FloorToInt(sheetSize.X / columns),
                Mathf.FloorToInt(sheetSize.Y / rows));

            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    string resource = _generator.ResourceAt(new Vector2I(x, y));
                    if (resource.Length == 0 || !_frames.TryGetValue(resource, out int index))
                        continue;

                    var region = new Rect2(
                        new Vector2(index % columns, index / columns) * frame, frame);
                    float fit = tile / Mathf.Max(1, Mathf.Max(frame.X, frame.Y));
                    Vector2 drawn = (Vector2)frame * fit * IconScale;
                    var centre = new Vector2(
                        (x + 0.5f) * tile,
                        (y + 0.5f + VerticalOffset) * tile);

                    _icons.Add(new Icon(
                        region,
                        new Rect2(centre - (drawn * 0.5f), drawn),
                        centre,
                        Mathf.Max(drawn.X, drawn.Y) * 0.58f));
                }
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

            // Only drop the cached texture when the set actually changed;
            // reloading a 1K sheet on every rebuild is pure waste.
            if (_presetApplied != set)
            {
                _sheet = null;
                _frames.Clear();
                _presetApplied = set;
            }
            _sheetPath = preset.Path;
            _columns = preset.Columns;
            _rows = preset.Rows;
            _order = preset.Order;
        }

        private bool LoadSheet()
        {
            if (_sheet is null && !string.IsNullOrWhiteSpace(_sheetPath))
            {
                if (_sheetPath.StartsWith("res://", StringComparison.Ordinal))
                {
                    _sheet = GD.Load<Texture2D>(_sheetPath);
                }
                else
                {
                    Image image = Image.LoadFromFile(_sheetPath);
                    if (image.IsEmpty())
                        GD.PushWarning($"[{Name}] could not load resource icon sheet '{_sheetPath}'.");
                    else
                    {
                        image.GenerateMipmaps();
                        _sheet = ImageTexture.CreateFromImage(image);
                    }
                }
            }

            if (_frames.Count == 0 && _order is { Length: > 0 })
            {
                for (int i = 0; i < _order.Length; i++)
                {
                    string id = _order[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        _frames[id] = i;
                }
            }
            return _sheet is not null && _frames.Count > 0;
        }

        private void ResolveGenerator()
        {
            if (_generator is null || !GodotObject.IsInstanceValid(_generator))
                _generator = TerrainGeneratorPath.IsEmpty
                    ? null
                    : GetNodeOrNull<GridTerrainGeneratorComponent>(TerrainGeneratorPath);
        }
    }
}
