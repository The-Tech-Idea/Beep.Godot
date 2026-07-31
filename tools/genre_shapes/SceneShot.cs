using Godot;

/// Screenshot any scene. `godot --path . tools/genre_shapes/scene_shot.tscn -- <res-path> <out>`
public partial class SceneShot : Node
{
    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        var args = OS.GetCmdlineUserArgs();
        string scene = args.Length > 0 ? args[0] : "";
        string outPath = args.Length > 1 ? args[1] : "res://tmp/scene_shot.png";
        if (!ResourceLoader.Exists(scene))
        {
            GD.Print($"shot:   FAIL no scene at {scene}");
            GetTree().Quit(1);
            return;
        }
        AddChild(GD.Load<PackedScene>(scene).Instantiate());
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(outPath.GetBaseDir());
        var err = GetViewport().GetTexture().GetImage().SavePng(outPath);
        GD.Print($"shot:   {(err == Error.Ok ? $"ok {outPath}" : $"FAIL {err}")}");
        GetTree().Quit(err == Error.Ok ? 0 : 1);
    }
}
