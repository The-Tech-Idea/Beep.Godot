using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Writes an authored terrain pattern into cell data, then refreshes
    /// whatever draws it.
    ///
    /// A generated world comes from GridTerrainGeneratorComponent. This is the
    /// other case: a map whose shape is KNOWN because someone chose it - a test
    /// fixture exercising corners and concave joins, a tutorial level, a
    /// hand-built menu backdrop.
    ///
    /// It replaces a demo controller that hardcoded one lake, one desert and one
    /// volcano as inline ellipse arithmetic, and then named five layer nodes as
    /// strings to refresh. Both are data now, so a second fixture is a second
    /// configured node rather than a second script.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridCellPatternComponent : Node
    {
        [Export] public NodePath CellDataPath { get; set; } = new("");

        [Export] public Vector2I Size { get; set; } = new(18, 10);

        /// <summary>Filled everywhere first; regions are stamped over it in order.</summary>
        [Export] public string BaseTerrainKind { get; set; } = "grass";

        [Export] public Godot.Collections.Array<GridCellRegionDefinition> Regions { get; set; } = new();

        /// <summary>
        /// What to rebuild once the cells are written - transition layers, prop
        /// scatters. They cannot refresh themselves on ready, because at that
        /// point the cells they would read are still empty.
        /// </summary>
        [Export] public Godot.Collections.Array<NodePath> RefreshTargets { get; set; } = new();

        [Export] public bool SeedOnReady { get; set; } = true;

        private GridCellDataComponent? _cells;

        public override void _Ready()
        {
            if (SeedOnReady && !Engine.IsEditorHint())
                CallDeferred(nameof(Seed));
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to a GridCellDataComponent." };
            if (Regions.Count == 0)
                return new[] { "Add at least one region, or the pattern is a flat field of BaseTerrainKind." };
            return System.Array.Empty<string>();
        }

        /// <summary>Writes the pattern and refreshes everything that draws it.</summary>
        public void Seed()
        {
            _cells ??= CellDataPath.IsEmpty ? null : GetNodeOrNull<GridCellDataComponent>(CellDataPath);
            if (_cells is null)
            {
                GD.PushWarning($"[{Name}] no GridCellDataComponent at CellDataPath; nothing was seeded.");
                return;
            }

            _cells.ClearCells();
            Vector2I size = new(Mathf.Max(1, Size.X), Mathf.Max(1, Size.Y));

            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    string kind = BaseTerrainKind;

                    // Later regions win, so the array reads as a painter's stack.
                    foreach (GridCellRegionDefinition? region in Regions)
                    {
                        if (region is not null && region.Contains(cell))
                            kind = region.TerrainKind;
                    }

                    _cells.SetTerrainKind(cell, kind);
                }
            }

            Refresh();
        }

        /// <summary>
        /// Rebuilds each target by what it IS, not by what it is called. The
        /// controller this replaces looked five layers up by name, so renaming a
        /// node in the scene silently stopped it being drawn.
        /// </summary>
        private void Refresh()
        {
            foreach (NodePath path in RefreshTargets)
            {
                if (path.IsEmpty)
                    continue;

                Node? target = GetNodeOrNull(path);
                switch (target)
                {
                    case null:
                        GD.PushWarning($"[{Name}] refresh target '{path}' does not exist.");
                        break;
                    case GridTerrainTransitionLayerComponent transitions:
                        transitions.RefreshTransitions();
                        break;
                    case SeededTerrainPropScatterComponent scatter:
                        scatter.Rebuild();
                        break;
                    default:
                        GD.PushWarning(
                            $"[{Name}] refresh target '{path}' is a {target.GetType().Name}, which this cannot rebuild.");
                        break;
                }
            }
        }
    }
}
