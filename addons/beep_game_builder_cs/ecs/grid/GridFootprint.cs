using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The cells a footprint of a given size stands on, for callers that have a size
    /// but not yet a placed object - the placement preview, before anything exists to
    /// ask. An object with a real footprint answers for itself through
    /// <see cref="GridObjectComponent.FootprintCells"/> and
    /// <see cref="GridObjectComponent.Covers"/>; the four hand-rolled copies of this
    /// double loop are gone.
    /// </summary>
    internal static class GridFootprint
    {
        /// <summary>Every cell of an origin+size rectangle, row by row. Size is taken as given.</summary>
        public static IEnumerable<Vector2I> Cells(Vector2I origin, Vector2I size)
        {
            for (int y = 0; y < size.Y; y++)
                for (int x = 0; x < size.X; x++)
                    yield return new Vector2I(origin.X + x, origin.Y + y);
        }

        /// <summary>The single cell under a body, for a component that has no grid object of its own.</summary>
        public static IEnumerable<Vector2I> SingleUnder(Node? parent, GridProjectionComponent? grid)
        {
            if (grid != null && parent is Node2D body)
                yield return grid.WorldToCell(body.GlobalPosition);
        }
    }
}
