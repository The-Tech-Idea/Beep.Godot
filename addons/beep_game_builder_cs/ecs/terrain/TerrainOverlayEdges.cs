using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The borders of labelled regions on a cell grid: every side where a cell's id differs
    /// from its +x or +y neighbour's, reported once per non-zero side. A region drawn from
    /// these is outlined in its own colour without knowing the projection - the caller turns
    /// each (cell, neighbour) pair into the shared side of their two cell polygons.
    ///
    /// Cells outside <c>bounds</c> read as 0, so a region touching the map edge is closed there.
    /// Written once for start areas; a territory border is the same walk over another id.
    /// </summary>
    internal static class TerrainOverlayEdges
    {
        /// <summary>One side of region <c>Id</c>: between its <c>Cell</c> and the outside <c>Neighbour</c>.</summary>
        internal readonly record struct Edge(Vector2I Cell, Vector2I Neighbour, int Id);

        internal static List<Edge> Collect(Rect2I bounds, Func<Vector2I, int> idAt)
        {
            var edges = new List<Edge>();
            int IdAt(Vector2I cell) => bounds.HasPoint(cell) ? idAt(cell) : 0;

            // From one row and column before the bounds, so the first row's and column's outer
            // sides are pairs too.
            for (int y = bounds.Position.Y - 1; y < bounds.End.Y; y++)
            {
                for (int x = bounds.Position.X - 1; x < bounds.End.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    int id = IdAt(cell);
                    Pair(cell, id, cell + Vector2I.Right);
                    Pair(cell, id, cell + Vector2I.Down);
                }
            }
            return edges;

            void Pair(Vector2I cell, int id, Vector2I neighbour)
            {
                int other = IdAt(neighbour);
                if (id == other) return;
                if (id != 0) edges.Add(new Edge(cell, neighbour, id));
                if (other != 0) edges.Add(new Edge(neighbour, cell, other));
            }
        }

        /// <summary>
        /// The side two neighbouring cell polygons share, or false when they share fewer than two
        /// corners (a projection whose neighbours touch at a point only).
        /// </summary>
        internal static bool SharedSide(Vector2[] cell, Vector2[] neighbour, out Vector2 from, out Vector2 to)
        {
            from = to = Vector2.Zero;
            if (cell.Length < 2) return false;
            // A hundredth of the side, so float noise in a projected corner still matches.
            float tolerance = cell[0].DistanceSquaredTo(cell[1]) * 0.0001f;
            int found = 0;
            foreach (Vector2 corner in cell)
            {
                foreach (Vector2 other in neighbour)
                {
                    if (corner.DistanceSquaredTo(other) > tolerance) continue;
                    if (found == 0) from = corner; else to = corner;
                    found++;
                    break;
                }
                if (found == 2) return true;
            }
            return false;
        }
    }
}
