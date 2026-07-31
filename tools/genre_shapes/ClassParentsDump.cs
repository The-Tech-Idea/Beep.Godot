using Godot;

/// Dumps Godot's class inheritance to tools/godot_class_parents.txt so a plain Python checker can
/// answer "is this node type allowed to carry this script?" without embedding a hand-written and
/// therefore wrong copy of Godot's hierarchy.
public partial class ClassParentsDump : Node
{
    public override void _Ready()
    {
        using var f = FileAccess.Open("res://tools/godot_class_parents.txt", FileAccess.ModeFlags.Write);
        if (f == null) { GD.Print("classdb: FAILED to open output"); GetTree().Quit(1); return; }
        int n = 0;
        foreach (var name in ClassDB.GetClassList())
        {
            f.StoreLine($"{name}\t{ClassDB.GetParentClass(name)}");
            n++;
        }
        GD.Print($"classdb: wrote {n} classes");
        GetTree().Quit(0);
    }
}
