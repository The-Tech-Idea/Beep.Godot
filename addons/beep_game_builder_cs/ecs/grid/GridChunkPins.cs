using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// One pin owner's chunk demand against a <see cref="GridCellDataComponent"/>. Each
    /// refresh the owner declares the chunks it needs with <see cref="Want"/>/<see cref="WantCell"/>/
    /// <see cref="WantCells"/>/<see cref="WantRect"/>, then <see cref="Commit"/> hands the
    /// accumulated set to the store, which diffs it against this owner's previous set and
    /// pins/unpins only the delta — the refcount and the <c>TreeExiting</c> release live in
    /// <see cref="GridCellDataComponent.ReplaceChunkPins"/>, not here.
    ///
    /// <para>This replaces the resolve-the-store / rebind-on-change / allocate-a-HashSet /
    /// shift-every-cell-to-its-chunk block that was copied into each pin owner. The wanted
    /// set is reused across commits, so a steady-state refresh allocates nothing; the chunk
    /// rule itself is <see cref="GridCellDataComponent.ChunkOf"/>.</para>
    ///
    /// <para>Owners with a genuinely different pin policy — an incrementally retired route set,
    /// a per-actor radius with one token per actor, a per-frame viewport rectangle with its own
    /// change cache — keep their own logic and only borrow <see cref="GridCellDataComponent.ChunkOf"/>
    /// for the arithmetic.</para>
    /// </summary>
    public sealed class GridChunkPins
    {
        private readonly Node _owner;
        private GridCellDataComponent? _cells;
        private readonly HashSet<Vector2I> _wanted = new();

        public GridChunkPins(Node owner) => _owner = owner;

        /// <summary>The store currently pinned against, or null.</summary>
        public GridCellDataComponent? Cells => _cells;

        /// <summary>Chunks accumulated for the next <see cref="Commit"/>.</summary>
        public int WantedCount => _wanted.Count;

        /// <summary>
        /// Point the pins at a store, or at null. When the store changes, the pins held on the
        /// previous store are released, but any pending wanted set is kept — chunk coordinates are
        /// store-independent, so a set accumulated before the rebind is committed onto the new
        /// store. Returns true when the binding changed.
        /// </summary>
        public bool Bind(GridCellDataComponent? cells)
        {
            if (ReferenceEquals(cells, _cells)) return false;
            if (GodotObject.IsInstanceValid(_cells)) _cells!.ReleaseChunkPins(_owner);
            _cells = cells;
            return true;
        }

        /// <summary>Accumulate one chunk coordinate directly.</summary>
        public void Want(Vector2I chunk) => _wanted.Add(chunk);

        /// <summary>Accumulate the chunk a cell falls in.</summary>
        public void WantCell(Vector2I cell) => _wanted.Add(GridCellDataComponent.ChunkOf(cell));

        /// <summary>Accumulate the chunk each cell falls in.</summary>
        public void WantCells(IEnumerable<Vector2I> cells)
        {
            foreach (var cell in cells) _wanted.Add(GridCellDataComponent.ChunkOf(cell));
        }

        /// <summary>
        /// Accumulate every chunk a cell rectangle at <paramref name="origin"/> of the given
        /// <paramref name="size"/> touches (size floored to 1x1). Widens before shifting so a
        /// rectangle near the coordinate limit never wraps into another region. Returns false
        /// WITHOUT touching the wanted set when the rectangle would exceed the coordinate limit
        /// or span more than 65536 chunks; the caller then skips <see cref="Commit"/> so the
        /// previous pins are retained rather than released by an empty commit.
        /// </summary>
        public bool WantRect(Vector2I origin, Vector2I size)
        {
            long lastX = (long)origin.X + Math.Max(1, size.X) - 1;
            long lastY = (long)origin.Y + Math.Max(1, size.Y) - 1;
            if (lastX > int.MaxValue || lastY > int.MaxValue)
            {
                GD.PushError("GridChunkPins rectangle exceeds supported coordinate range; previous pins retained.");
                return false;
            }
            Vector2I min = GridCellDataComponent.ChunkOf(origin);
            Vector2I max = GridCellDataComponent.ChunkOf(new Vector2I((int)lastX, (int)lastY));
            if ((long)(max.X - min.X + 1) * (max.Y - min.Y + 1) > 65536)
            {
                GD.PushError("GridChunkPins rectangle exceeds supported chunk demand; previous pins retained.");
                return false;
            }
            for (int y = min.Y; y <= max.Y; y++)
                for (int x = min.X; x <= max.X; x++)
                    _wanted.Add(new Vector2I(x, y));
            return true;
        }

        /// <summary>
        /// Diff the accumulated wanted set against this owner's last commit, pin/unpin the delta,
        /// then reset the accumulator. Returns the store's result (false when there is no store or
        /// the owner is not in the tree). An empty wanted set releases every pin this owner holds.
        /// </summary>
        public bool Commit()
        {
            bool ok = _cells is not null && _cells.ReplaceChunkPins(_owner, _wanted);
            _wanted.Clear();
            return ok;
        }

        /// <summary>Unpin everything this owner holds and clear the accumulator. The store binding is kept.</summary>
        public void Release()
        {
            if (GodotObject.IsInstanceValid(_cells)) _cells!.ReleaseChunkPins(_owner);
            _wanted.Clear();
        }
    }
}
