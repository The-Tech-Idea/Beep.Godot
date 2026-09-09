using Beep.ECS;
using Godot;
using System.Collections.Generic;

// ENH-09: GridCellOverlayComponent used to draw every stored cell each redraw. It now culls to the
// camera's visible cell window (unless DrawAll is set). This probe seeds one flagged cell inside the
// window and two far outside, and asserts the overlay would paint only the inside one - and all three
// when DrawAll is on. The mutation the guard catches: dropping the window filter draws all three.
[GlobalClass]
public partial class GridCellOverlaySmoke : Node
{
    public bool Run()
    {
        var proj = new GridProjectionComponent
        {
            Name = "Proj",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(64, 64),
        };
        AddChild(proj);

        var cells = new GridCellDataComponent { Name = "Cells" };
        AddChild(cells);

        var overlay = new GridCellOverlayComponent
        {
            Name = "Overlay",
            GridPath = "../Proj",
            CellDataPath = "../Cells",
            DrawCells = true,
            DrawAll = false,
        };
        AddChild(overlay);

        if (!proj.TryGetVisibleCellRect(out Rect2I visible))
            return Fail("The projection reported no visible cell rect (no viewport?)");

        var inside = new Vector2I(visible.Position.X + visible.Size.X / 2, visible.Position.Y + visible.Size.Y / 2);
        var outsideLow = visible.Position - new Vector2I(1000, 1000);
        var outsideHigh = visible.Position + visible.Size + new Vector2I(1000, 1000);
        if (!visible.HasPoint(inside) || visible.HasPoint(outsideLow) || visible.HasPoint(outsideHigh))
            return Fail("Test setup: inside/outside cells are not on the expected side of the window");

        cells.AddFlag(inside, GridCellDataComponent.CellFlags.Tilled);
        cells.AddFlag(outsideLow, GridCellDataComponent.CellFlags.Tilled);
        cells.AddFlag(outsideHigh, GridCellDataComponent.CellFlags.Tilled);

        int culled = Count(overlay.VisibleCells());
        if (culled != 1)
            return Fail($"Culled overlay should paint only the 1 in-window cell, painted {culled}");

        var painted = new List<Vector2I>();
        foreach ((Vector2I cell, GridCellDataComponent.CellFlags _) in overlay.VisibleCells())
            painted.Add(cell);
        if (painted.Count != 1 || painted[0] != inside)
            return Fail("Culled overlay painted the wrong cell");

        overlay.DrawAll = true;
        int all = Count(overlay.VisibleCells());
        if (all != 3)
            return Fail($"DrawAll should paint all 3 flagged cells, painted {all}");

        overlay.Free();
        cells.Free();
        proj.Free();
        GD.Print($"[grid-overlay] cull paints 1 of 3 flagged cells inside the {visible.Size.X}x{visible.Size.Y} window; DrawAll paints 3");
        return true;
    }

    private static int Count(IEnumerable<(Vector2I, GridCellDataComponent.CellFlags)> cells)
    {
        int n = 0;
        foreach (var _ in cells) n++;
        return n;
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
