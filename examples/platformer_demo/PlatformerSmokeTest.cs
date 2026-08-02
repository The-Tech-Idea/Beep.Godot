using Godot;

namespace Beep.Examples;

/// <summary>
/// Loads the platformer example and drives the addon's PlatformerController through
/// the same input actions a generated platformer project uses.
/// </summary>
public partial class PlatformerSmokeTest : Node
{
    private const string ReportPath = "user://platformer_smoke_report.txt";
    private readonly System.Text.StringBuilder _report = new();

    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        int bad = 0;
        try
        {
            var scene = GD.Load<PackedScene>("res://examples/platformer_demo/platformer_demo.tscn");
            var demo = scene.Instantiate<PlatformerDemo>();
            AddChild(demo);
            await ToSignal(demo, PlatformerDemo.SignalName.DemoReady);
            await Frames(10);

            var player = demo.Player;
            var levelHost = demo.LevelHost;
            if (player == null || levelHost == null)
            {
                Log("platformer: FAIL missing player or level host");
                WriteReport();
                GetTree().Quit(1);
                return;
            }

            bool hasTerrain = levelHost.FindChild("DemoTerrain", true, false) != null;
            bool hasHud = demo.GetNodeOrNull("PlatformerMain/HUD/Root/GenreHud") != null;
            bool hasWeather = demo.GetNodeOrNull("PlatformerMain/Atmosphere/Weather") != null;
            bool hasWeatherControls = demo.GetNodeOrNull("PlatformerMain/DemoWeatherControls") != null;
            Log($"platformer: built terrain={hasTerrain} hud={hasHud} weather={hasWeather} weather_controls={hasWeatherControls}");
            if (!hasTerrain || !hasHud || !hasWeather || !hasWeatherControls) bad++;

            Vector2 start = player.GlobalPosition;
            Input.ActionPress("move_right");
            await Frames(45);
            Input.ActionRelease("move_right");
            float moved = player.GlobalPosition.X - start.X;
            bool ok = moved > 80f;
            Log($"platformer: {(ok ? "ok  " : "FAIL")} moved {moved:0.0}px while move_right held");
            if (!ok) bad++;

            player.GlobalPosition = new Vector2(280, 360);
            player.Velocity = Vector2.Zero;
            await Frames(70);
            float beforeJumpY = player.GlobalPosition.Y;
            SendKey(Key.Space, true);
            await Frames(2);
            SendKey(Key.Space, false);
            await Frames(16);
            float jumpDelta = beforeJumpY - player.GlobalPosition.Y;
            ok = jumpDelta > 24f;
            Log($"platformer: {(ok ? "ok  " : "FAIL")} jumped {jumpDelta:0.0}px upward");
            if (!ok) bad++;

            Log($"platformer: {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
            WriteReport();
            GetTree().Quit(bad == 0 ? 0 : 1);
        }
        catch (System.Exception e)
        {
            Log($"platformer: FAILED {e.GetType().Name}: {e.Message}");
            WriteReport();
            GetTree().Quit(1);
        }
    }

    private void Log(string line)
    {
        GD.Print(line);
        _report.AppendLine(line);
    }

    private void WriteReport()
    {
        using var file = FileAccess.Open(ReportPath, FileAccess.ModeFlags.Write);
        file?.StoreString(_report.ToString());
    }

    private async System.Threading.Tasks.Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void SendKey(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = key,
            Pressed = pressed,
        });
    }
}
