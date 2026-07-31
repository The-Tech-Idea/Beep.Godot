using Godot;
using System.Linq;
using Beep.ECS.UI;

namespace Beep.Examples;

/// <summary>
/// The arena demo's front screen. Layout is `ui/main_menu.tscn`.
///
/// The skin picker is the point: it writes the chosen theme into the arena scene's exported
/// `Theme` before changing scene, so the whole game restyles from one dropdown.
/// </summary>
public partial class MainMenu : Control
{
    private const string ArenaPath = "res://examples/topdown_arena/arena.tscn";
    private OptionButton? _themes;

    public override void _Ready()
    {
        SkinCatalog.SetActiveSkin("topdown", "fantasy", "", "");

        _themes = FindChild("ThemePicker", true, false) as OptionButton;
        if (_themes != null)
        {
            foreach (var t in Themes()) _themes.AddItem(t.DisplayName);
            _themes.Selected = 0;
        }
        else GD.PushWarning("[MainMenu] main_menu.tscn has no ThemePicker — skin is fixed.");

        Wire("PlayButton", Play);
        Wire("QuitButton", () => GetTree().Quit());
    }

    private static ThemeDef[] Themes()
    {
        var g = SkinCatalog.GetGenre("topdown");
        return g == null ? System.Array.Empty<ThemeDef>()
                         : g.Themes.Values.OrderBy(t => t.Id).ToArray();
    }

    private void Wire(string name, System.Action onPressed)
    {
        if (FindChild(name, true, false) is Button b) b.Pressed += onPressed;
        else GD.PushWarning($"[MainMenu] main_menu.tscn has no {name} — that action is unreachable.");
    }

    private void Play()
    {
        var themes = Themes();
        string theme = _themes != null && themes.Length > 0
            ? themes[Mathf.Clamp(_themes.Selected, 0, themes.Length - 1)].Id
            : "fantasy";

        // Instantiated rather than ChangeSceneToFile so the chosen theme can be set on the root
        // BEFORE _Ready runs -- setting it after would apply the skin twice and flash.
        var scene = GD.Load<PackedScene>(ArenaPath);
        var arena = scene.Instantiate<ArenaGame>();
        arena.Theme = theme;
        GetTree().Root.AddChild(arena);
        GetTree().CurrentScene = arena;
        QueueFree();
    }
}
