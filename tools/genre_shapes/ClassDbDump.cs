using Godot;
using System.Collections.Generic;

/// Dump every built-in property name Godot's ClassDB knows, so validate_scenes.sh can tell a
/// legitimate built-in (snake_case, correct) from a C# [Export] written snake_case (silently
/// dropped). Guessing that list would make the check weaker; this makes it factual.
///
/// Needed once the kit's drop-ins began attaching C# scripts to real Godot types: a KitSliderBar
/// IS an HSlider, so `min_value` is both a legitimate built-in AND a name whose PascalCase form
/// collides with a [Export] elsewhere in the addon.
public partial class ClassDbDump : Node
{
    public override void _Ready()
    {
        var names = new HashSet<string>();
        foreach (StringName cls in ClassDB.GetClassList())
            foreach (var prop in ClassDB.ClassGetPropertyList(cls, true))
                if (prop.TryGetValue("name", out var n))
                {
                    string s = n.AsString();
                    if (s.Length > 0 && !s.Contains('/')) names.Add(s);
                }

        var sorted = new List<string>(names);
        sorted.Sort(System.StringComparer.Ordinal);
        using var f = FileAccess.Open("res://tools/genre_shapes/godot_builtin_props.txt",
                                      FileAccess.ModeFlags.Write);
        foreach (string s in sorted) f.StoreLine(s);
        GD.Print($"classdb: {sorted.Count} built-in property names");
        GetTree().Quit();
    }
}
