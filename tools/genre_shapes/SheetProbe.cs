using Godot;
using System.Collections.Generic;

/// <summary>
/// Render EVERY template scene and write one PNG each, so a sweep can be checked by looking at
/// all of it instead of spot-checking one and hoping.
///
/// Written after a 108-button sweep was verified against a single rendered scene. The two bugs it
/// did contain -- blank plates and lost labels -- compiled cleanly and passed the scene validator,
/// so neither gate would have caught them; only looking would.
/// </summary>
public partial class SheetProbe : Node
{
    [Export] public string OutDir { get; set; } = "res://tmp/sheet";
    [Export] public string Root { get; set; } = "res://addons/beep_game_builder_cs/templates/scenes";

    public override async void _Ready()
    {
        try { await Run(); }
        catch (System.Exception e) { GD.Print($"sheet: FAILED {e.Message}"); GetTree().Quit(1); }
    }

    private async System.Threading.Tasks.Task Run()
    {
        DirAccess.MakeDirRecursiveAbsolute(OutDir);
        var scenes = new List<string>();
        Collect(Root, scenes);
        scenes.Sort();
        GD.Print($"sheet: {scenes.Count} scenes");

        foreach (string path in scenes)
        {
            // A scene that fails to instantiate must be REPORTED, not skipped silently -- that is
            // exactly the kind of breakage a bulk sweep introduces.
            PackedScene? packed = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
            if (packed == null) { GD.Print($"sheet: LOAD-FAIL {path}"); continue; }

            Node inst;
            try { inst = packed.Instantiate(); }
            catch (System.Exception e) { GD.Print($"sheet: INST-FAIL {path} {e.Message}"); continue; }

            string name = path.Substring(path.LastIndexOf('/') + 1).Replace(".tscn", "");
            AddChild(inst);
            try
            {
                for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng($"{OutDir}/{name}.png");
                GD.Print($"sheet: ok {name}");
            }
            catch (System.Exception e)
            {
                // One scene throwing must not end the sweep -- the whole point is to see ALL of
                // them, and the scenes that throw are the ones most worth knowing about.
                GD.Print($"sheet: DRAW-FAIL {name} {e.Message}");
            }

            if (GodotObject.IsInstanceValid(inst)) { RemoveChild(inst); inst.QueueFree(); }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print("sheet: done");
        GetTree().Quit();
    }

    private static void Collect(string dir, List<string> into)
    {
        using var d = DirAccess.Open(dir);
        if (d == null) return;
        d.ListDirBegin();
        for (string f = d.GetNext(); f != ""; f = d.GetNext())
        {
            if (d.CurrentIsDir()) Collect($"{dir}/{f}", into);
            else if (f.EndsWith(".tscn")) into.Add($"{dir}/{f}");
        }
    }
}
