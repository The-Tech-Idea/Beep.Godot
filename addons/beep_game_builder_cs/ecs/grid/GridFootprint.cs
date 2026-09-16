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
        /// <summary>
        /// Every cell of an origin+size rectangle, row by row. Size is taken as given.
        ///
        /// A struct enumerable, so a foreach over it allocates nothing. Start selection asks
        /// this once per candidate cell of the whole map; as an iterator method it allocated a
        /// state machine per call - 1.5 MB on a Huge build - for a rectangle walk.
        /// </summary>
        public static Rectangle Cells(Vector2I origin, Vector2I size) => new(origin, size);

        /// <summary>
        /// A foreach binds the struct <see cref="GetEnumerator"/> and allocates nothing; the
        /// IEnumerable implementation (boxed) is for callers that hand the cells on as a sequence.
        /// </summary>
        internal readonly struct Rectangle : IEnumerable<Vector2I>
        {
            private readonly Vector2I _origin, _size;
            public Rectangle(Vector2I origin, Vector2I size) { _origin = origin; _size = size; }
            public Enumerator GetEnumerator() => new(_origin, _size);
            IEnumerator<Vector2I> IEnumerable<Vector2I>.GetEnumerator() => GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal struct Enumerator : IEnumerator<Vector2I>
        {
            private readonly Vector2I _origin, _size;
            private int _x, _y;

            public Enumerator(Vector2I origin, Vector2I size)
            {
                _origin = origin;
                _size = size;
                _x = -1;
                _y = size.X > 0 && size.Y > 0 ? 0 : size.Y;
            }

            public Vector2I Current => new(_origin.X + _x, _origin.Y + _y);

            public bool MoveNext()
            {
                if (_y >= _size.Y) return false;
                if (++_x < _size.X) return true;
                _x = 0;
                return ++_y < _size.Y;
            }

            object System.Collections.IEnumerator.Current => Current;

            public void Reset()
            {
                _x = -1;
                _y = _size.X > 0 && _size.Y > 0 ? 0 : _size.Y;
            }

            public void Dispose() { }
        }

        /// <summary>The single cell under a body, for a component that has no grid object of its own.</summary>
        public static IEnumerable<Vector2I> SingleUnder(Node? parent, GridProjectionComponent? grid)
        {
            if (grid != null && parent is Node2D body)
                yield return grid.WorldToCell(body.GlobalPosition);
        }
    }
}
