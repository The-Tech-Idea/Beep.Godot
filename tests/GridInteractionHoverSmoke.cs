using Beep.ECS;
using Godot;

// ENH-08 part 4: the interaction router (GridInteractionModeComponent) already converts the
// mouse to a cell once per frame; it must feed that cell to the selection component rather than
// making selection run a SECOND WorldToCell on the same position. This probe wires the router to
// a projection and selection, ticks the router, and asserts exactly one WorldToCell per frame -
// and that selection was actually fed. The mutation the guard catches: the router calling
// UpdateHoverFromWorld (recompute) instead of SetHoverCell, which doubles the count.
[GlobalClass]
public partial class GridInteractionHoverSmoke : Node
{
    private static readonly Vector2I InvalidCell = new(int.MinValue, int.MinValue);

    public bool Run()
    {
        var proj = new GridProjectionComponent
        {
            Name = "Proj",
            Projection = GridProjectionComponent.GridProjection.TopDown,
            TileSize = new Vector2(48, 32),
            TrackMouseCell = false, // the router owns the per-frame conversion, not the projection
        };
        AddChild(proj);

        var selection = new GridSelectionComponent { Name = "Sel", GridPath = "../Proj" };
        AddChild(selection);

        var router = new GridInteractionModeComponent
        {
            Name = "Router",
            GridPath = "../Proj",
            SelectionPath = "../Sel",
            CurrentMode = GridInteractionModeComponent.InteractionMode.Select,
            UseMouseInput = true,
            ManageChildMouseInput = true, // takes input ownership: selection.UseMouseInput -> false
        };
        AddChild(router);

        // Only manual _Process ticks should count; keep the engine from ticking mid-run.
        proj.SetProcess(false);
        selection.SetProcess(false);
        router.SetProcess(false);

        // The router took input ownership, so selection does not run its own per-frame hover.
        if (selection.UseMouseInput)
            return Fail("Router with ManageChildMouseInput should disable selection's own mouse input");

        if (selection.HoverCell != InvalidCell)
            return Fail("Selection should start with no hovered cell");

        // One router frame must run exactly one WorldToCell, and it must have fed selection.
        long before = proj.WorldToCellCalls;
        router._Process(1.0 / 60.0);
        long firstFrame = proj.WorldToCellCalls - before;
        if (firstFrame != 1)
            return Fail($"One router frame should run exactly 1 WorldToCell, ran {firstFrame}");
        if (selection.HoverCell == InvalidCell)
            return Fail("Router did not feed the hovered cell into selection");

        // Sustained: five more frames add exactly one conversion each (the router's; selection reuses it).
        long mid = proj.WorldToCellCalls;
        for (int i = 0; i < 5; i++) router._Process(1.0 / 60.0);
        long fiveFrames = proj.WorldToCellCalls - mid;
        if (fiveFrames != 5)
            return Fail($"Five router frames should run exactly 5 WorldToCell, ran {fiveFrames}");

        // SetHoverCell itself runs no conversion (the direct-feed path).
        long beforeSet = proj.WorldToCellCalls;
        selection.SetHoverCell(new Vector2I(9, 9));
        if (proj.WorldToCellCalls != beforeSet)
            return Fail("SetHoverCell must not run a WorldToCell");
        if (selection.HoverCell != new Vector2I(9, 9))
            return Fail("SetHoverCell should update the hovered cell");

        router.Free();
        selection.Free();
        proj.Free();
        GD.Print($"[grid-hover] one WorldToCell per router frame ({fiveFrames} over 5 frames); selection fed without a second conversion");
        return true;
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
