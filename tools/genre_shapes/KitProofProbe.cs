using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// <summary>
/// Phase-A proof harness: renders ONE <see cref="KitButton"/> under each of the ten genres and
/// writes gs_&lt;genre&gt;.png, which is the input tools/genre_shapes/verify_greyscale.py grades.
///
/// COLOUR IS HELD CONSTANT ON PURPOSE. No theme is applied, so every genre resolves the same
/// fallback surface out of <see cref="UiSurface"/> and the same 14pt font. The gate asks whether
/// genres are tellable apart with colour removed (PLAN.md 4.1); letting each genre bring its own
/// theme would let a lighter palette masquerade as a different material and the gate would pass
/// on exactly the thing it exists to reject. Varying ONE input — the genre — means any separation
/// the gate measures is provably geometry and material.
///
/// Each genre gets a FRESH button because KitControl caches the genre in _Ready; reusing one
/// instance would render all ten at whichever genre was set first.
/// </summary>
public partial class KitProofProbe : Node
{
    [Export] public string OutDir { get; set; } = "res://tmp/kitproof";

    private static readonly string[] Genres =
    {
        "rpg", "survival", "strategy", "shooter", "racing",
        "citybuilder", "cardgame", "platformer", "puzzle", "topdown",
    };

    public override async void _Ready()
    {
        try { await Run(); }
        catch (System.Exception e)
        {
            // _Ready is async void, so an exception here would otherwise vanish and the probe
            // would look like it simply produced nothing.
            GD.PushError($"kitproof: {e}");
            GD.Print($"kitproof: FAILED {e.Message}");
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task Run()
    {
        GD.Print("kitproof: start");
        DirAccess.MakeDirRecursiveAbsolute(OutDir);

        var root = new Control { Name = "Canvas" };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // A flat, constant field so the gate can separate widget from background by difference
        // from the corner pixel. Mid-grey keeps both the bright rim and the dark ink in range.
        var bg = new ColorRect { Color = new Color(0.42f, 0.42f, 0.42f), Name = "Bg" };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        foreach (string genre in Genres)
        {
            SkinCatalog.SetActiveSkin(genre, "", "", "");

            var btn = new KitButton { Text = "PLAY" };
            root.AddChild(btn);

            // _Ready sizes from the genre's ratios; it has run by the next frame.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            btn.Size = btn.CustomMinimumSize;
            btn.Position = ((root.Size - btn.Size) * 0.5f).Round();

            for (int i = 0; i < 3; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await Shoot($"{OutDir}/gs_{genre}.png");
            GD.Print($"kitproof: {genre,-12} {btn.Size.X:0}x{btn.Size.Y:0}");

            btn.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GD.Print("kitproof: done");
        GetTree().Quit();
    }

    private async System.Threading.Tasks.Task Shoot(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var err = GetViewport().GetTexture().GetImage().SavePng(path);
        if (err != Error.Ok) GD.PushError($"kitproof: save failed {err} for {path}");
    }
}
