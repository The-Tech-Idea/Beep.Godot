using Godot;
using Beep.ECS;

/// Renders the weather system across types and camera axes so it can be LOOKED at.
public partial class WeatherShots : Node2D
{
    private static readonly (WeatherSystemComponent.WeatherType type, bool top, string name)[] Shots =
    {
        (WeatherSystemComponent.WeatherType.Cloudy, false, "cloudy_side"),
        (WeatherSystemComponent.WeatherType.Cloudy, true,  "cloudy_top"),
        (WeatherSystemComponent.WeatherType.Rain,   false, "rain_side"),
        (WeatherSystemComponent.WeatherType.Rain,   true,  "rain_top"),
        (WeatherSystemComponent.WeatherType.Storm,  true,  "storm_top"),
        (WeatherSystemComponent.WeatherType.Snow,   false, "snow_side"),
    };

    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        DirAccess.MakeDirRecursiveAbsolute("res://tmp/weather");

        // A plain ground so cloud SHADOWS have something to fall on -- the whole point of the
        // top-down treatment, and invisible against an empty viewport.
        var ground = new ColorRect { Color = new Color(0.42f, 0.46f, 0.36f), Size = new Vector2(1280, 720) };
        AddChild(ground);
        var cam = new Camera2D { Position = new Vector2(640, 360) };
        AddChild(cam);
        cam.MakeCurrent();

        var scene = GD.Load<PackedScene>(
            "res://addons/beep_game_builder_cs/templates/scenes/atmosphere.tscn");

        foreach (var (type, top, name) in Shots)
        {
            var atmo = scene.Instantiate();
            AddChild(atmo);
            var w = atmo.GetNodeOrNull<WeatherSystemComponent>("Weather");
            if (w == null) { GD.Print("wx:     FAIL no Weather node"); GetTree().Quit(1); return; }

            w.TopDownView = top;
            w.SetWeather(type);
            // Let the cross-fade and the particle stream actually establish.
            for (int i = 0; i < 90; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var err = GetViewport().GetTexture().GetImage().SavePng($"res://tmp/weather/{name}.png");
            GD.Print($"wx:     {name,-12} {(err == Error.Ok ? "ok" : $"FAILED {err}")}");

            atmo.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print("wx:     done");
        GetTree().Quit(0);
    }
}
