using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One authored patch of terrain, written into cell data as an ellipse.
    ///
    /// Hand-authored regions are how a test or a tutorial map gets a KNOWN
    /// shape - a lake with concave joins, a desert with a solid interior, a
    /// small crater - rather than whatever a seed happened to produce. The
    /// shapes used to be arithmetic inside a demo controller, so the only way to
    /// move a lake was to edit C#.
    /// </summary>
    [GlobalClass]
    public partial class GridCellRegionDefinition : Resource
    {
        [Export] public string TerrainKind { get; set; } = "water";

        /// <summary>Centre of the ellipse, in tiles.</summary>
        [Export] public Vector2 Centre { get; set; } = Vector2.Zero;

        /// <summary>Semi-axes of the ellipse, in tiles. Equal radii give a circle.</summary>
        [Export] public Vector2 Radii { get; set; } = Vector2.One;

        /// <summary>
        /// Tiles this region may write to. Left at zero size it may write
        /// anywhere in the pattern's bounds; a real rectangle clips it, which is
        /// what keeps a lake off the map edge.
        /// </summary>
        [Export] public Rect2I Clip { get; set; }

        /// <summary>Whether a tile centre falls inside this region.</summary>
        public bool Contains(Vector2I cell)
        {
            if (Clip.Size.X > 0 && Clip.Size.Y > 0 && !Clip.HasPoint(cell))
                return false;

            float rx = Mathf.Max(0.0001f, Radii.X);
            float ry = Mathf.Max(0.0001f, Radii.Y);
            float dx = (cell.X - Centre.X) / rx;
            float dy = (cell.Y - Centre.Y) / ry;
            return (dx * dx) + (dy * dy) < 1.0f;
        }
    }
}
