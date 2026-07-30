using Godot;

/// Print the real laid-out rects of a scene's key nodes. Pixel-measuring a screenshot to infer
/// layout is guesswork; this is the layout.
public partial class RectProbe2 : Node
{
    public override async void _Ready()
    {
        var packed = GD.Load<PackedScene>(
            "res://addons/beep_game_builder_cs/templates/scenes/settings_menu.tscn");
        var inst = packed.Instantiate();
        AddChild(inst);
        for (int i = 0; i < 8; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        foreach (string path in new[]
                 {
                     "Center/Panel", "Center/Panel/Frame", "Center/Panel/Margin",
                     "Center/Panel/Margin/ContentVBox",
                     "Center/Panel/Margin/ContentVBox/TitleLabel",
                     "Center/Panel/Margin/ContentVBox/TitleRule",
                     "Center/Panel/Margin/ContentVBox/Tabs",
                 })
        {
            var c = inst.GetNodeOrNull<Control>(path);
            if (c == null) { GD.Print($"rect: {path,-46} MISSING"); continue; }
            var r = c.GetGlobalRect();
            GD.Print($"rect: {path,-46} x={r.Position.X,6:0} y={r.Position.Y,6:0} "
                     + $"w={r.Size.X,5:0} h={r.Size.Y,5:0}  vis={c.Visible}");
        }

        var fr = inst.GetNodeOrNull<Control>("Center/Panel/Frame");
        if (fr != null)
        {
            GD.Print($"rect: Frame children = {fr.GetChildCount()}  "
                     + "(DriveChildMargins only drives a MarginContainer CHILD)");
            foreach (var ch in fr.GetChildren()) GD.Print($"        child: {ch.Name} ({ch.GetType().Name})");
        }
        var mg = inst.GetNodeOrNull<MarginContainer>("Center/Panel/Margin");
        if (mg != null)
            GD.Print($"rect: Margin top constant = {mg.GetThemeConstant("margin_top")}");
        GetTree().Quit();
    }
}
