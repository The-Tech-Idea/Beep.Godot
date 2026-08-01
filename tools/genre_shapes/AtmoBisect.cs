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
/// CORRECTION to an earlier claim in this file: "not in any method it runs" OVERSTATED the
/// evidence. Only _Ready was stubbed. _Process, _EnterTree and _ExitTree were never tested, and
/// _EnterTree in particular runs on the base class. Ruling out one method is not ruling out all
/// of them, and writing it that way is how a wrong conclusion gets inherited by the next attempt.
///
/// NARROWED, still by removal:
///
///     ConfigureParticleEmitter() removed from _Ready ....... 5 errors  (not it)
///     _Ready body skipped entirely ........................ 5 errors  (not _Ready AT ALL)
///     ResourceLoader.Exists / GD.Load called directly ...... 0 errors  (not them)
///
///     script on a bare Node, empty scene ................. 5 errors  (it IS this script)
///     empty scene, no script at all ...................... 0 errors  (the control -- clean)
///     the five [Export] Texture2D properties un-exported .. 5 errors  (not them)
///     EVERY [Export] in the file stripped ................ 5 errors  (not exports AT ALL)
///     [Tool] / [GlobalClass] ............................. same as the components that emit 0
///
/// TWICE in this hunt a count coincidence looked decisive and was wrong: five particle textures
/// matched five errors, then five exported Texture2D properties matched five errors. Both were
/// ruled out by a two-minute test. A matching count is a hypothesis, never a finding.
///
/// STILL UNIDENTIFIED. What has NOT been tested: _Process, _EnterTree (which runs on the base
/// class), _ExitTree, and the constructor. That is where to go next -- stub each in turn and
/// re-count. The error is harmless at five occurrences on startup, so this is tidiness, not
/// urgency; it is recorded this precisely so the next attempt starts from the eliminations rather
/// than repeating them.
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
