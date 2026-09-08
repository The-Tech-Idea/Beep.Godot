using Beep.ECS;
using Godot;
using System;
using System.Diagnostics;

public partial class NavigationQuerySmoke : Node
{
    public bool Run()
    {
        var cells = new GridCellDataComponent { Name = "Cells" };
        AddChild(cells);
        var nav = new GridNavigationComponent { CellDataPath = new("../Cells"), BoundsSize = new(8, 8) };
        AddChild(nav);
        var from = new Vector2I(1, 1);
        var to = new Vector2I(2, 1);
        cells.SetTerrainKind(from, "grass");
        cells.SetTerrainKind(to, "grass");
        for (int i = 0; i < 1000; i++) nav.CanTraverse(from, to);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        bool traversable = true;
        for (int i = 0; i < 10000; i++) traversable &= nav.CanTraverse(from, to);
        timer.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GD.Print($"[navigation-query] 10000 live edge checks: {allocated} managed bytes, {timer.Elapsed.TotalMilliseconds:F3} ms");
        bool ok = traversable;
        cells.SetTerrainKind(to, "water");
        ok &= !nav.CanTraverse(from, to);
        cells.SetTerrainKind(to, "grass");
        ok &= nav.CanTraverse(from, to);
        cells.SetMetadata(to, "terrain_relief", 1);
        ok &= !nav.CanTraverse(from, to);
        cells.SetMetadata(from, "terrain_ramp_direction", Vector2I.Right);
        ok &= nav.CanTraverse(from, to);
        cells.SetMetadata(from, "terrain_ramp_direction", Vector2I.Left);
        ok &= !nav.CanTraverse(from, to);
        cells.SetMetadata(to, "terrain_relief", 0);
        ok &= nav.CanTraverse(from, to);
        nav.SetBlocked(to, true);
        ok &= !nav.CanTraverse(from, to);
        nav.ClearBlocked();
        ok &= nav.CanTraverse(from, to);
        var replacement = new GridCellDataComponent { Name = "Replacement" };
        AddChild(replacement);
        replacement.SetTerrainKind(to, "water");
        nav.CellDataPath = new("../Replacement");
        ok &= !nav.CanTraverse(from, to);
        nav.CellDataPath = new("../Cells");
        ok &= nav.CanTraverse(from, to);
        if (!ok) GD.PushError("Live edge query retained stale terrain, ramp, blocked or source data");
        return ok;
    }
}
