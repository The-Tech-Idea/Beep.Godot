using Godot;
using Beep.GameBuilder;

/// Generates a project the way the dock does, then reports what it wrote.
///
/// BeepGenreGenerator.CreateProject is public and static with no editor dependency — the dock
/// calls exactly this. Running it headlessly is the closest thing to the editor pass that nothing
/// else in this repo substitutes for, and it is the one verification that has been missing all
/// along. SkipExisting so nothing already present is touched.
public partial class GenProbe : Node
{
    public override void _Ready()
    {
        var info = new GameInfo { GameName = "GenProbe", Version = "0.0.1" };
        var log = BeepGenreGenerator.CreateProject("topdown", info,
                                                   BeepGenreGenerator.RegenMode.SkipExisting);
        GD.Print($"gen:    CreateProject returned {log.Count} log lines");
        foreach (var line in log) GD.Print($"gen:      {line}");
        GetTree().Quit(0);
    }
}
