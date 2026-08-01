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

        // ── KitMeter IS a ProgressBar (a Range) ──
        var mt = new KitMeter { Name = "HP" };
        root.AddChild(mt);
        var pb = root.GetNodeOrNull<ProgressBar>("HP");
        ok = pb != null && Mathf.IsEqualApprox((float)pb.MaxValue, 1f);
        GD.Print($"mt:     {(ok ? "ok  " : "FAIL")} GetNode<ProgressBar>(\"HP\") -> "
               + $"{(pb == null ? "NULL" : $"range {pb.MinValue:0.0}..{pb.MaxValue:0.0}")} "
               + "(0..1 kept so shipped scenes still mean percent)");
        if (!ok) bad++;

        // ── KitIconButton IS a Button ──
        var ib = new KitIconButton { Name = "Hammer", Glyph = "H" };
        root.AddChild(ib);
        var ibb = root.GetNodeOrNull<Button>("Hammer");
        bool ibFired = false;
        if (ibb != null) ibb.Pressed += () => ibFired = true;
        ibb?.EmitSignal(BaseButton.SignalName.Pressed);
        ok = ibb != null && ibFired;
        GD.Print($"ib:     {(ok ? "ok  " : "FAIL")} GetNode<Button>(\"Hammer\") + Pressed");
        if (!ok) bad++;

        // ── KitKnob IS an HSlider ──
        var kn = new KitKnob { Name = "Dial" };
        root.AddChild(kn);
        var kh = root.GetNodeOrNull<HSlider>("Dial");
        ok = kh != null && kh.CustomMinimumSize.X > 20f;
        GD.Print($"kn:     {(ok ? "ok  " : "FAIL")} GetNode<HSlider>(\"Dial\") min "
               + $"{kh?.CustomMinimumSize.X:0}x{kh?.CustomMinimumSize.Y:0}");
        if (!ok) bad++;

        // ── KitTabStrip IS a TabBar ──
        var ts = new KitTabStrip { Name = "Tabs" };
        root.AddChild(ts);
        var tb = root.GetNodeOrNull<TabBar>("Tabs");
        ok = tb != null && tb.TabCount == 3;
        GD.Print($"ts:     {(ok ? "ok  " : "FAIL")} GetNode<TabBar>(\"Tabs\") -> "
               + $"{tb?.TabCount ?? -1} tabs pushed into TabBar");
        if (!ok) bad++;
        int picked = -1;
        if (tb != null) { tb.TabChanged += i => picked = (int)i; tb.CurrentTab = 2; }
        ok = picked == 2;
        GD.Print($"ts:     {(ok ? "ok  " : "FAIL")} TabChanged fired with {picked} via CurrentTab");
        if (!ok) bad++;
        ok = tb != null && tb.CustomMinimumSize.Y >= 26f;
        GD.Print($"ts:     {(ok ? "ok  " : "FAIL")} min height {tb?.CustomMinimumSize.Y:0} "
               + "(blanking the tab styleboxes must not collapse it)");
        if (!ok) bad++;

        // ── KitStarRating IS a Range ──
        var sr = new KitStarRating { Name = "Stars", Total = 5, Earned = 3 };
        root.AddChild(sr);
        var rg = root.GetNodeOrNull<Range>("Stars");
        double heard = -1;
        if (rg != null) { rg.ValueChanged += v => heard = v; rg.Value = 4; }
        ok = rg != null && Mathf.IsEqualApprox((float)heard, 4f)
             && Mathf.IsEqualApprox((float)rg.MaxValue, 5f) && sr.Earned == 4;
        GD.Print($"sr:     {(ok ? "ok  " : "FAIL")} GetNode<Range>(\"Stars\") "
               + $"max={rg?.MaxValue:0} ValueChanged={heard:0} Earned={sr.Earned}");
        if (!ok) bad++;

        GD.Print($"kb:     {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
