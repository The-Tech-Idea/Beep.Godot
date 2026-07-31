using Godot;
using System.Linq;
using Beep.ECS.UI;

namespace Beep.Examples;

/// <summary>Renders the showcase under several genre/theme pairs so the switch can be LOOKED at.
/// A green log line does not prove anything was visible.</summary>
public partial class ShowcaseShot : Node
{
    private static readonly (string genre, string theme)[] Shots =
    {
        ("topdown", "classic"), ("topdown", "fantasy"),
        ("rpg", "fantasy"), ("shooter", "cyberpunk"), ("citybuilder", "blueprint"),
    };

    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        int bad = 0;
        try
        {
            DirAccess.MakeDirRecursiveAbsolute("res://tmp/showcase");
            var scene = GD.Load<PackedScene>("res://examples/style_showcase/showcase.tscn");
            var ui = scene.Instantiate<StyleShowcase>();
            AddChild(ui);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var genrePicker = ui.FindChild("GenrePicker", true, false) as OptionButton;
            var themePicker = ui.FindChild("ThemePicker", true, false) as OptionButton;
            if (genrePicker == null || themePicker == null)
            {
                GD.Print("shot:   FAIL pickers missing"); bad++;
            }
            else
            {
                var ids = SkinCatalog.AllGenres.Values.OrderBy(g => g.Id).Select(g => g.Id).ToArray();
                foreach (var (genre, theme) in Shots)
                {
                    int gi = System.Array.IndexOf(ids, genre);
                    if (gi < 0) { GD.Print($"shot:   FAIL unknown genre {genre}"); bad++; continue; }
                    genrePicker.Selected = gi;
                    genrePicker.EmitSignal(OptionButton.SignalName.ItemSelected, gi);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                    var tids = SkinCatalog.GetGenre(genre)!.Themes.Values
                                          .OrderBy(t => t.Id).Select(t => t.Id).ToArray();
                    int ti = System.Array.IndexOf(tids, theme);
                    if (ti < 0) { GD.Print($"shot:   FAIL {genre} has no theme {theme}"); bad++; continue; }
                    themePicker.Selected = ti;
                    themePicker.EmitSignal(OptionButton.SignalName.ItemSelected, ti);

                    for (int i = 0; i < 5; i++)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

                    string path = $"res://tmp/showcase/{genre}_{theme}.png";
                    var err = GetViewport().GetTexture().GetImage().SavePng(path);
                    GD.Print($"shot:   {genre}/{theme,-10} {(err == Error.Ok ? "ok" : $"FAILED {err}")}");
                    if (err != Error.Ok) bad++;
                }
            }
        }
        catch (System.Exception e) { GD.Print($"shot:   FAILED {e.Message}"); bad++; }

        GD.Print($"shot:   {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
