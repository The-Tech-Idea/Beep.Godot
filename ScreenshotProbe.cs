using Godot;

// TEMPORARY verification harness — renders a scene and writes a PNG so the layout can be
// looked at instead of guessed at. Delete after use.
public partial class ScreenshotProbe : Node
{
    [Export] public string ScenePath { get; set; } = "";
    [Export] public string OutPath { get; set; } = "res://probe_out.png";
    /// <summary>Shoot every page of this TabContainer, suffixing the file with the tab index.</summary>
    [Export] public string TabPath { get; set; } = "";

    public override async void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed == null) { GD.PushError($"probe: cannot load {ScenePath}"); GetTree().Quit(1); return; }
        var inst = packed.Instantiate();
        AddChild(inst);

        // Several frames: containers sort on the frame after they are added, and the
        // deferred focus/normalise passes land a frame after that.
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var tabs = string.IsNullOrEmpty(TabPath)
            ? null
            : inst.FindChild(TabPath, recursive: true, owned: false) as TabContainer;

        if (tabs == null) { await Shoot(OutPath); GetTree().Quit(); return; }

        for (int t = 0; t < tabs.GetTabCount(); t++)
        {
            tabs.CurrentTab = t;
            for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Shoot(OutPath.Replace(".png", $"_{t}_{tabs.GetTabTitle(t)}.png"));
        }
        GetTree().Quit();
    }

    private async System.Threading.Tasks.Task Shoot(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var err = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print(err == Error.Ok ? $"probe: wrote {path}" : $"probe: save failed {err}");
    }
}
