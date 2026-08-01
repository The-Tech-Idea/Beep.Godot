using Godot;
using System.Linq;
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
        // EVERY genre, not just one. A genre.json naming a scene that does not exist, or a
        // nav_wiring key pointing nowhere, only shows up when that genre is actually stamped --
        // and until now no genre had ever been stamped outside the editor.
        int bad = 0;
        foreach (var genre in Beep.ECS.UI.SkinCatalog.AllGenres.Values
                                  .OrderBy(g => g.Id).ToArray())
        {
            var info = new GameInfo { GameName = "GenProbe", Version = "0.0.1" };
            var log = BeepGenreGenerator.CreateProject(genre.Id, info,
                                                       BeepGenreGenerator.RegenMode.OverwriteAll);
            var trouble = log.Where(l => l.Contains("Failed") || l.Contains("ERROR")
                                      || l.Contains("missing") || l.Contains("not found")).ToList();
            GD.Print($"gen:    {genre.Id,-12} {log.Count,3} steps, "
                   + $"{(trouble.Count == 0 ? "no failures" : $"{trouble.Count} FAILURE(S)")}");
            foreach (var t in trouble) { GD.Print($"gen:      {t}"); bad++; }
        }
        GD.Print($"gen:    {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(0);
    }
}
