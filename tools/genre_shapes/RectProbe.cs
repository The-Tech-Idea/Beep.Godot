using Godot;

/// TEMPORARY: prints the laid-out rect of every node in a scene, so a layout can be measured
/// instead of guessed at.
public partial class RectProbe : Node
{
    [Export] public string ScenePath { get; set; } = "";

    public override async void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        var inst = packed.Instantiate();
        AddChild(inst);
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Dump(inst, 0);
        GetTree().Quit();
    }

    private void Dump(Node n, int depth)
    {
        string pad = new string(' ', depth * 2);
        if (n is Control c)
            GD.Print($"rect: {pad}{n.Name,-14} pos=({c.Position.X:0},{c.Position.Y:0}) size=({c.Size.X:0}x{c.Size.Y:0}) min=({c.CustomMinimumSize.X:0}x{c.CustomMinimumSize.Y:0}) mf={c.MouseFilter}");
        else GD.Print($"rect: {pad}{n.Name} [{n.GetType().Name}]");
        foreach (var ch in n.GetChildren()) Dump(ch, depth + 1);
    }
}
