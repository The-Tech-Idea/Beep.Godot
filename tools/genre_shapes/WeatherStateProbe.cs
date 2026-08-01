using Godot;
using Beep.ECS;

/// Asserts each genre main's Weather node actually CARRIES the genre's weather.
///
/// The override is written as `[node name="Weather" parent="Atmosphere" index="3"]`, which
/// resolves positionally against the instanced scene — if the atmosphere ever reorders its
/// children, the override silently lands on a different node. So it is read back, not assumed.
public partial class WeatherStateProbe : Node
{
    private static readonly (string genre, WeatherSystemComponent.WeatherType w, bool top)[] Want =
    {
        ("survival",   WeatherSystemComponent.WeatherType.Cloudy, true),
        ("rpg",        WeatherSystemComponent.WeatherType.Cloudy, true),
        ("topdown",    WeatherSystemComponent.WeatherType.Cloudy, true),
        ("platformer", WeatherSystemComponent.WeatherType.Cloudy, false),
        ("racing",     WeatherSystemComponent.WeatherType.Clear,  false),
        ("shooter",    WeatherSystemComponent.WeatherType.Clear,  false),
    };

    public override void _Ready()
    {
        int bad = 0;
        foreach (var (genre, want, top) in Want)
        {
            var path = $"res://addons/beep_game_builder_cs/templates/scenes/{genre}_main.tscn";
            var root = GD.Load<PackedScene>(path).Instantiate();
            var w = root.GetNodeOrNull<WeatherSystemComponent>("Atmosphere/Weather");
            if (w == null)
            {
                GD.Print($"ws:     FAIL {genre}: no Atmosphere/Weather node");
                bad++; root.QueueFree(); continue;
            }
            bool ok = w.CurrentWeather == want && w.TopDownView == top;
            GD.Print($"ws:     {(ok ? "ok  " : "FAIL")} {genre,-11} {w.CurrentWeather,-6} "
                   + $"topdown={w.TopDownView,-5} (want {want}, {top})");
            if (!ok) bad++;
            root.QueueFree();
        }
        GD.Print($"ws:     {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
