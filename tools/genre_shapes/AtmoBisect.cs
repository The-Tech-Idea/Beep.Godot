using Godot;

/// Bisects a scene by REMOVING one child at a time, so a component can be identified by which
/// removal silences an engine message -- rather than by reading the candidates and picking one.
///
/// Written to chase `ERROR: This function should never be used outside the editor, it can
/// severely damage performance.`, which every genre main emits. Result:
///
///     without WeatherAmbient     5 errors      without Seasonal       5 errors
///     without AmbientController  5 errors      without WeatherAudio   5 errors
///     without DayNight           5 errors      without Fog            5 errors
///     without Weather            0 errors   <-- WeatherSystemComponent
///
/// STILL OPEN: which call inside it. ResourceLoader.Exists and GD.Load were the obvious suspects
/// (Bundled() runs once per particle texture, and five textures matches five errors) and BOTH ARE
/// RULED OUT -- calling them directly at runtime emits nothing. Reading the file for editor-only
/// APIs has found nothing either.
///
/// The next step is the same technique one level down: stub out sections of _Ready and
/// ConfigureParticleEmitter until the count drops. Do not guess; four wrong answers earlier in
/// this work all came from reading code and deciding.
public partial class AtmoBisect : Node
{
    [Export] public string ScenePath { get; set; } =
        "res://addons/beep_game_builder_cs/templates/scenes/atmosphere.tscn";

    /// <summary>Substring the engine message to attribute. Counted between the markers.</summary>
    [Export] public string Needle { get; set; } = "should never be used outside the editor";

    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        var scene = GD.Load<PackedScene>(ScenePath);
        if (scene == null) { GD.Print($"atmo:   FAIL no scene at {ScenePath}"); GetTree().Quit(1); return; }

        var probe = scene.Instantiate();
        var names = new System.Collections.Generic.List<string>();
        foreach (var c in probe.GetChildren()) names.Add(c.Name);
        probe.QueueFree();

        GD.Print($"atmo:   bisecting {names.Count} children for: {Needle}");
        foreach (string drop in names)
        {
            // The markers bracket each trial. The engine writes to the same stream, so whichever
            // pair the message falls between names a trial that still contained the cause; the
            // trial with ZERO is the one whose removal silenced it.
            GD.Print($"atmo:   --- BEGIN without {drop} ---");
            var inst = scene.Instantiate();
            var child = inst.GetNodeOrNull(drop);
            if (child != null) { inst.RemoveChild(child); child.QueueFree(); }
            AddChild(inst);
            for (int i = 0; i < 3; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"atmo:   --- END without {drop} ---");
            inst.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print("atmo:   done — pipe through: awk '/BEGIN without/{n=$5;c=0} /"
               + "should never/{c++} /END without/{printf \"  %-20s %d\n\", n, c}'");
        GetTree().Quit(0);
    }
}
