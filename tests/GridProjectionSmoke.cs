using Beep.ECS;
using Godot;
using System;

// ENH-08: GridProjectionComponent.CellCorners must have an allocation-free span overload
// for the hot per-cell callers (overlay per stored cell per redraw, collision per cell per
// chunk build). This probe proves the span path allocates nothing over ten thousand calls,
// that it agrees with the array convenience overload, and - as a positive control so the
// measurement itself cannot silently pass - that the array overload really does allocate.
[GlobalClass]
public partial class GridProjectionSmoke : Node
{
    public bool Run()
    {
        var grid = new GridProjectionComponent
        {
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(48, 32),
            Origin = new Vector2(10, 20),
        };
        AddChild(grid);

        var cell = new Vector2I(3, -2);

        // Correctness: the span overload writes four corners identical to the array overload.
        Vector2[] fromArray = grid.CellCorners(cell);
        Span<Vector2> fromSpan = stackalloc Vector2[4];
        int n = grid.CellCorners(cell, fromSpan);
        if (n != 4 || fromArray.Length != 4)
            return Fail($"Top-down should yield 4 corners; span {n}, array {fromArray.Length}");
        for (int i = 0; i < 4; i++)
            if (fromArray[i] != fromSpan[i])
                return Fail($"Top-down corner {i} differs: array {fromArray[i]} vs span {fromSpan[i]}");

        // Isometric agrees across the other branch too.
        grid.Projection = GridProjectionComponent.GridProjection.Isometric;
        Vector2[] isoArray = grid.CellCorners(cell);
        Span<Vector2> isoSpan = stackalloc Vector2[4];
        if (grid.CellCorners(cell, isoSpan) != 4 || isoArray.Length != 4)
            return Fail("Isometric should yield 4 corners on both overloads");
        for (int i = 0; i < 4; i++)
            if (isoArray[i] != isoSpan[i])
                return Fail($"Isometric corner {i} differs: array {isoArray[i]} vs span {isoSpan[i]}");
        grid.Projection = GridProjectionComponent.GridProjection.TopDown;

        // A buffer shorter than four is refused, not overrun.
        Span<Vector2> tooSmall = stackalloc Vector2[3];
        if (grid.CellCorners(cell, tooSmall) != 0)
            return Fail("CellCorners must return 0 for a span shorter than four");

        // Allocation: ten thousand span fills allocate essentially nothing.
        Span<Vector2> buf = stackalloc Vector2[4];
        for (int i = 0; i < 200; i++) grid.CellCorners(new Vector2I(i, i), buf); // warm the JIT
        long spanBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            if (grid.CellCorners(new Vector2I(i & 63, i >> 6), buf) != 4)
                return Fail("Span fill lost a corner mid-loop");
        long spanAllocated = GC.GetAllocatedBytesForCurrentThread() - spanBefore;
        if (spanAllocated > 4096)
            return Fail($"CellCorners(span) allocated {spanAllocated} bytes over 10000 calls");

        // Positive control: the array overload DOES allocate, so a broken measurement
        // (always reading zero) would fail this check instead of masking a regression.
        for (int i = 0; i < 200; i++) _ = grid.CellCorners(new Vector2I(i, i));
        long arrayBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) _ = grid.CellCorners(new Vector2I(i & 63, i >> 6));
        long arrayAllocated = GC.GetAllocatedBytesForCurrentThread() - arrayBefore;
        if (arrayAllocated <= spanAllocated)
            return Fail($"Array overload should allocate more than the span; span {spanAllocated}, array {arrayAllocated}");

        // Surface caching (ENH-08 part 3): a native layer resolves once and is cached, but a
        // freed layer must NOT be served from cache. This exercises the validity guard in
        // NativeLayer - without it, CellToWorld would touch a freed node instead of re-resolving.
        var tileSet = new TileSet { TileShape = TileSet.TileShapeEnum.Square, TileSize = new Vector2I(32, 32) };
        var layer = new TileMapLayer { Name = "NativeLayer", TileSet = tileSet };
        grid.AddChild(layer);
        grid.TileMapLayerPath = "NativeLayer";
        if (!grid.CellToWorld(new Vector2I(2, 1)).IsFinite())
            return Fail("A native layer projection should resolve to a finite world position");
        _ = grid.CellToWorld(Vector2I.Zero); // populate the cache
        layer.Free();
        if (grid.CellToWorld(new Vector2I(2, 1)).IsFinite())
            return Fail("A freed native layer must re-resolve to none, not be served from cache");
        grid.TileMapLayerPath = new NodePath("");

        grid.Free();
        GD.Print($"[grid-projection] CellCorners span/array agree (top-down + isometric); span 10000 calls allocated {spanAllocated} bytes, array {arrayAllocated} bytes; freed native layer re-resolves");
        return true;
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
