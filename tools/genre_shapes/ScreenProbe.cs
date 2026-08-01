using Godot;
public partial class ScreenProbe : Node
{
    public override void _Ready()
    {
        int bad = 0;
        foreach (var (path, root) in new[]
                 { ("res://addons/beep_game_builder_cs/templates/scenes/load_game_menu.tscn", "LoadGameMenu"),
                   ("res://addons/beep_game_builder_cs/templates/scenes/save_game_menu.tscn", "SaveGameMenu") })
        {
            var n = GD.Load<PackedScene>(path).Instantiate();
            // THE thing that was broken: the managed object was a Node standing in for a Control.
            bool isControl = n is Control;
            var c = n as Control;
            GD.Print($"screen: {root,-14} is Control={isControl,-6} "
                   + $"anchors reachable={(c != null ? $"{c.AnchorRight:0.0}" : "NO")}");
            if (!isControl) bad++;
            n.QueueFree();
        }
        GD.Print($"screen: {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
