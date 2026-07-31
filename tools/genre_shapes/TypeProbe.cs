using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// KitButton must now BE a Button: typed lookup, inherited Text/Disabled, and a real Pressed.
public partial class TypeProbe : Node
{
    public override void _Ready()
    {
        int bad = 0;
        SkinCatalog.SetActiveSkin("rpg", "fantasy", "", "");

        var root = new Control { Name = "Root" };
        root.Size = new Vector2(400, 300);
        AddChild(root);

        var kb = new KitButton { Name = "Buy", Text = "BUY", BadgeText = "25" };
        kb.Size = new Vector2(200, 56);
        root.AddChild(kb);

        // 1. it IS a Button, so a typed lookup works -- the whole point of the change.
        var found = root.GetNodeOrNull<Button>("Buy");
        bool ok = found != null;
        GD.Print($"kb:     {(ok ? "ok  " : "FAIL")} GetNode<Button>(\"Buy\") -> {(ok ? "found" : "NULL")}");
        if (!ok) bad++;

        // 2. Text is BaseButton's, not a shadowing copy.
        ok = found != null && found.Text == "BUY";
        GD.Print($"kb:     {(ok ? "ok  " : "FAIL")} inherited Text = '{found?.Text}'");
        if (!ok) bad++;

        // 3. Pressed is BaseButton's signal and reaches a plain Button handler.
        bool fired = false;
        if (found != null) found.Pressed += () => fired = true;
        found?.EmitSignal(BaseButton.SignalName.Pressed);
        GD.Print($"kb:     {(fired ? "ok  " : "FAIL")} Pressed fired through the Button API");
        if (!fired) bad++;

        // 4. Disabled is BaseButton's and changes the sculpt rather than just fading.
        if (found != null) found.Disabled = true;
        ok = found is { Disabled: true };
        GD.Print($"kb:     {(ok ? "ok  " : "FAIL")} inherited Disabled");
        if (!ok) bad++;

        // ── KitToggle IS a CheckButton ──
        var tg = new KitToggle { Name = "Music" };
        root.AddChild(tg);
        var cb = root.GetNodeOrNull<CheckButton>("Music");
        ok = cb != null;
        GD.Print($"tg:     {(ok ? "ok  " : "FAIL")} GetNode<CheckButton>(\"Music\") -> {(ok ? "found" : "NULL")}");
        if (!ok) bad++;
        bool toggled = false;
        if (cb != null) cb.Toggled += _ => toggled = true;
        if (cb != null) cb.ButtonPressed = !cb.ButtonPressed;
        GD.Print($"tg:     {(toggled ? "ok  " : "FAIL")} Toggled fired via ButtonPressed");
        if (!toggled) bad++;

        // ── KitSlider IS an HSlider (a Range) ──
        var sl = new KitSlider { Name = "Volume" };
        root.AddChild(sl);
        var hs = root.GetNodeOrNull<HSlider>("Volume");
        ok = hs != null;
        GD.Print($"sl:     {(ok ? "ok  " : "FAIL")} GetNode<HSlider>(\"Volume\") -> {(ok ? "found" : "NULL")}");
        if (!ok) bad++;
        double got = -1;
        if (hs != null) { hs.ValueChanged += v => got = v; hs.Value = 0.62; }
        ok = hs != null && Mathf.IsEqualApprox((float)got, 0.62f);
        GD.Print($"sl:     {(ok ? "ok  " : "FAIL")} ValueChanged fired with {got:0.00} "
               + $"(range {hs?.MinValue:0.0}..{hs?.MaxValue:0.0})");
        if (!ok) bad++;
        ok = hs != null && hs.CustomMinimumSize.Y >= 20f;
        GD.Print($"sl:     {(ok ? "ok  " : "FAIL")} min height {hs?.CustomMinimumSize.Y:0} "
               + "(blanking the grabber icon must not collapse it)");
        if (!ok) bad++;

        GD.Print($"kb:     {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
