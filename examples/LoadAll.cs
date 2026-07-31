using Godot;
public partial class LoadAll : Node
{
    public override void _Ready()
    {
        string[] scenes = {
            "res://examples/style_showcase/showcase.tscn",
            "res://examples/topdown_arena/arena.tscn",
            "res://examples/topdown_arena/ui/main_menu.tscn",
            "res://examples/topdown_arena/ui/hud.tscn",
            "res://examples/topdown_arena/ui/pause.tscn",
            "res://examples/topdown_arena/ui/result.tscn",
            "res://examples/topdown_arena/entities/player.tscn",
            "res://examples/topdown_arena/entities/enemy.tscn",
            "res://examples/topdown_arena/entities/coin.tscn",
        };
        int bad = 0;
        foreach (var p in scenes)
        {
            var ps = ResourceLoader.Exists(p) ? GD.Load<PackedScene>(p) : null;
            Node? n = null;
            try { n = ps?.Instantiate(); } catch (System.Exception e)
            { GD.Print($"load:   FAIL {p} -> {e.Message}"); bad++; continue; }
            if (n == null) { GD.Print($"load:   FAIL {p} did not instantiate"); bad++; continue; }
            GD.Print($"load:   ok  {p,-58} {n.GetChildCount()} children");
            n.QueueFree();
        }
        GD.Print($"load:   {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
