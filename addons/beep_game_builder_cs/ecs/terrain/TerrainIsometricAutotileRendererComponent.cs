using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Renders the generated map isometrically using an AUTHORED TileSet whose
    /// terrain peering bits do the transition matching.
    ///
    /// Hard terrain borders are what make an isometric map read as a
    /// checkerboard; transition tiles are what make it look finished. This does
    /// not choose those tiles itself - it hands whole runs of cells to
    /// SetCellsTerrainConnect and lets Godot pick, which is the same thing
    /// TerrainTransitionLayerComponent does for the flat view.
    ///
    /// WHY NOT PICK THE TILES HERE. An earlier version mapped a four-bit corner
    /// mask onto tile indices directly. That works only against an atlas whose
    /// layout has been verified, and deriving the layout from the pixels of a
    /// textured sheet is not reliable: two shades of grass do not separate the
    /// way flat colours do, and the derived mapping agreed with the known one on
    /// barely a third of tiles. Peering bits are authored once, in the editor,
    /// by someone who can see the tiles - and then they are simply correct.
    ///
    /// The TileSet is a resource you author and point at. Its tile shape must be
    /// isometric, and every terrain named below must exist in it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainIsometricAutotileRendererComponent : Node2D
    {
        [Export] public NodePath TerrainGeneratorPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsSize { get; set; } = new(48, 48);

        [ExportGroup("TileSet")]
        /// <summary>
        /// An authored TileSet: isometric tile shape, one terrain set, and
        /// peering bits painted on the transition tiles.
        /// </summary>
        [Export] public TileSet? Tiles { get; set; }

        /// <summary>Which terrain set in the TileSet carries the terrains below.</summary>
        [Export(PropertyHint.Range, "0,8,1")] public int TerrainSet { get; set; }

        /// <summary>
        /// Terrain kinds this renderer paints, in draw order, as
        /// "kind[,kind...]=terrainIndex" - for example "grass,dry_grass=0".
        /// Kinds absent from this list are not painted, which is deliberate: a
        /// silently substituted terrain misdescribes the map.
        /// </summary>
        [Export] public string[] TerrainBindings { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether this renderer builds itself once the scene is ready. Turn it
        /// off where a controller generates the world first and drives Rebuild.
        /// </summary>
        [Export] public bool RefreshOnReady { get; set; } = true;

        private TerrainGeneratorComponent? _generator;
        private TileMapLayer? _layer;

        public override void _Ready()
        {
            if (RefreshOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Rebuild));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (TerrainGeneratorPath.IsEmpty)
                return new[] { "TerrainGeneratorPath should point to a TerrainGeneratorComponent." };
            if (Tiles is null)
                return new[] { "Tiles needs an authored isometric TileSet with terrain peering bits." };
            if (TerrainBindings.Length == 0)
                return new[] { "TerrainBindings needs at least one \"kind=terrainIndex\" entry." };
            return Array.Empty<string>();
        }

        /// <summary>Repaints the whole map, letting Godot match the transitions.</summary>
        public void Rebuild()
        {
            ResolveGenerator();
            if (_generator is null)
            {
                GD.PushWarning($"[{Name}] no generator at TerrainGeneratorPath; nothing was drawn.");
                return;
            }
            if (Tiles is null)
            {
                GD.PushWarning($"[{Name}] no TileSet assigned to Tiles, so nothing was drawn.");
                return;
            }
            TerrainBoundsCheck.WarnIfMismatched(Name, BoundsSize, _generator.BoundsSize);

            TileMapLayer layer = EnsureLayer();
            layer.TileSet = Tiles;
            layer.Clear();
            int requested = 0;

            Vector2I size = new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));

            // Resolved ONCE per rebuild - this renderer rescans the entire grid
            // once per terrain binding, so the per-cell generator wrapper's
            // settings rebuild would otherwise pay off once per binding too.
            GeneratedTerrainField field = _generator.ResolveField();

            // Cells are gathered per terrain and painted in one call each:
            // SetCellsTerrainConnect resolves the joins across the whole run, so
            // painting cell by cell would make each tile decide before its
            // neighbours exist and leave the seams it is meant to remove.
            foreach ((HashSet<string> kinds, int terrain) in Bindings())
            {
                var cells = new Godot.Collections.Array<Vector2I>();
                for (int y = 0; y < size.Y; y++)
                {
                    for (int x = 0; x < size.X; x++)
                    {
                        var cell = new Vector2I(x, y);
                        if (kinds.Contains(field.TerrainAtCell(cell)))
                            cells.Add(cell);
                    }
                }

                if (cells.Count > 0)
                {
                    requested += cells.Count;
                    layer.SetCellsTerrainConnect(cells, TerrainSet, terrain);
                }
            }

            // SetCellsTerrainConnect matches against PEERING BITS, so a TileSet
            // whose tiles carry none matches nothing and paints nothing - with no
            // error, which is the worst way for this to fail: the projection is
            // selectable, the layer exists, and the map is simply invisible.
            if (requested > 0 && layer.GetUsedCells().Count == 0)
            {
                GD.PushWarning(
                    $"[{Name}] painted no tiles for {requested} cells. The TileSet has no terrain "
                    + $"peering bits in terrain set {TerrainSet}, so Godot has nothing to match. "
                    + "Paint the peering bits on the transition tiles in the TileSet editor.");
            }
        }

        /// <summary>
        /// Parses the bindings once per rebuild. Malformed entries are reported
        /// rather than skipped quietly: a terrain that silently stops painting
        /// looks like a generation bug, and it is a typo.
        /// </summary>
        private IEnumerable<(HashSet<string> Kinds, int Terrain)> Bindings()
        {
            foreach (string entry in TerrainBindings)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] halves = entry.Split('=', StringSplitOptions.TrimEntries);
                if (halves.Length != 2 || !int.TryParse(halves[1], out int terrain))
                {
                    GD.PushWarning($"[{Name}] terrain binding '{entry}' is not \"kind[,kind...]=terrainIndex\".");
                    continue;
                }

                var kinds = new HashSet<string>();
                foreach (string kind in halves[0].Split(',', StringSplitOptions.RemoveEmptyEntries))
                    kinds.Add(kind.Trim());

                if (kinds.Count > 0)
                    yield return (kinds, terrain);
            }
        }

        private TileMapLayer EnsureLayer()
        {
            _layer = TerrainAuthoring.EnsureLayer(this, "IsoTerrain");

            // Without Y sorting a tile drawn later covers one that should be in
            // front of it, and an isometric scene falls apart immediately.
            _layer.YSortEnabled = true;

            // Y sorting alone is not enough. Godot batches tiles into quadrants
            // and sorts whole QUADRANTS against each other, so two tiles in one
            // batch draw in atlas order however they overlap. This layer enabled
            // Y sorting and left the quadrant size at its default, which is the
            // half-fixed state that looks correct until two neighbouring tiles
            // differ in height. One tile per quadrant is what makes the sort
            // actually per-tile - the same thing TerrainIsometricRendererComponent
            // does, for the same reason.
            _layer.RenderingQuadrantSize = 1;

            // The shared stack, like every other view. This layer is the ground
            // it paints, and it drew at Node2D's default z of 0 - the slot the
            // stack gives the SEA - so anything else on the shared stack landed
            // on the wrong side of it.
            _layer.ZIndex = TerrainLayers.ZFor(TerrainLayers.Ground);
            _layer.ZAsRelative = false;

            // Authored isometric tiles are detailed art minified hard at map
            // zoom; without a mip-aware filter they alias into a shimmering grid.
            _layer.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            return _layer;
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
