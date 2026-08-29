using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// <summary>
/// Phase-A proof harness: renders ONE <see cref="KitButton"/> under each of the ten genres and
/// writes gs_&lt;genre&gt;.png, which is the input tools/genre_shapes/verify_greyscale.py grades.
///
/// COLOUR IS HELD CONSTANT ON PURPOSE. No theme is applied, so every genre resolves the same
/// fallback surface out of <see cref="UiSurface"/> and the same 14pt font. The gate asks whether
/// genres are tellable apart with colour removed (PLAN.md 4.1); letting each genre bring its own
/// theme would let a lighter palette masquerade as a different material and the gate would pass
/// on exactly the thing it exists to reject. Varying ONE input — the genre — means any separation
/// the gate measures is provably geometry and material.
///
/// Each genre gets a FRESH button because KitControl caches the genre in _Ready; reusing one
/// instance would render all ten at whichever genre was set first.
/// </summary>
public partial class KitProofProbe : Node
{
    [Export] public string OutDir { get; set; } = "res://tmp/kitproof";

    private static readonly string[] Genres =
    {
        "rpg", "survival", "strategy", "shooter", "racing",
        "citybuilder", "cardgame", "platformer", "puzzle", "topdown",
    };

    public override async void _Ready()
    {
        try { await Run(); }
        catch (System.Exception e)
        {
            // _Ready is async void, so an exception here would otherwise vanish and the probe
            // would look like it simply produced nothing.
            GD.PushError($"kitproof: {e}");
            GD.Print($"kitproof: FAILED {e.Message}");
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task Run()
    {
        GD.Print("kitproof: start");
        DirAccess.MakeDirRecursiveAbsolute(OutDir);

        var root = new Control { Name = "Canvas" };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // A flat, constant field so the gate can separate widget from background by difference
        // from the corner pixel. Mid-grey keeps both the bright rim and the dark ink in range.
        var bg = new ColorRect { Color = new Color(0.42f, 0.42f, 0.42f), Name = "Bg" };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        foreach (string genre in Genres)
        {
            SkinCatalog.SetActiveSkin(genre, "", "", "");

            var btn = new KitButton { Text = "PLAY" };
            root.AddChild(btn);

            // _Ready sizes from the genre's ratios; it has run by the next frame.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            btn.Size = btn.CustomMinimumSize;
            btn.Position = ((root.Size - btn.Size) * 0.5f).Round();

            for (int i = 0; i < 3; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await Shoot($"{OutDir}/gs_{genre}.png");
            GD.Print($"kitproof: {genre,-12} {btn.Size.X:0}x{btn.Size.Y:0}");

            btn.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            // SECOND PASS: the same widget with NO LABEL and a large face, for the MATERIAL
            // gate (tools/genre_shapes/measure_material.py --proof).
            //
            // The material axis could not be gated off gs_*.png at all: at 130x45 the inset
            // crop is ~73x25 and mostly glyph, so flat-filled plates scored ABOVE diamond
            // plate and the measurement was worthless. Text is not material. A big, empty
            // face is the only honest input, so the probe renders one rather than asking the
            // measurement to subtract a letterform it cannot see.
            var plate = new KitButton { Text = "" };
            root.AddChild(plate);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            plate.Size = new Vector2(420, 260);
            plate.Position = ((root.Size - plate.Size) * 0.5f).Round();

            for (int i = 0; i < 3; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await Shoot($"{OutDir}/gm_{genre}.png");
            plate.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        await ShootWidgets(root);

        GD.Print("kitproof: done");
        GetTree().Quit();
    }

    /// <summary>
    /// One sheet per genre showing the kit's widgets together, so they can be compared against
    /// the reference sheet they were measured from instead of being trusted.
    /// </summary>
    private async System.Threading.Tasks.Task ShootWidgets(Control root)
    {
        foreach (string genre in new[] { "rpg", "platformer", "citybuilder" })
        {
            SkinCatalog.SetActiveSkin(genre, "", "", "");
            var host = new Control { Name = "Widgets" };
            host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            // Unlike the greyscale sheets, the widget sheet is for LOOKING at, so it carries a
            // real surface and real semantic colours -- a widget whose palette resolves to black
            // tells you nothing about whether it was built correctly.
            host.Theme = SheetTheme();
            root.AddChild(host);

            var stats = new[] { ("ATTACK", "7"), ("DEFENSE", "12"), ("COMBO", "x3") };
            for (int i = 0; i < stats.Length; i++)
            {
                var lv = new KitLabelValue { Label = stats[i].Item1, Value = stats[i].Item2 };
                host.AddChild(lv);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                lv.Size = lv.CustomMinimumSize;
                lv.Position = new Vector2(60, 60 + i * (lv.Size.Y + 10));
            }

            var meterSeg = new KitMeter { Value = 0.62f, Segments = 10 };
            var meterCont = new KitMeter { Value = 0.62f, Segments = 0, Fill = UiSurface.Role.Danger };
            host.AddChild(meterSeg);
            host.AddChild(meterCont);

            var panel = new KitCollapsiblePanel { Title = "INVENTORY" };
            var panelC = new KitCollapsiblePanel { Title = "SHUT", Collapsed = true };
            host.AddChild(panel);
            host.AddChild(panelC);

            var kp = new KitPanel { Title = "EQUIPMENT" };
            host.AddChild(kp);

            var grid = new KitSlotGrid { Columns = 4, Rows = 3, Selected = 1 };
            grid.Slots.AddRange(new[]
            {
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Filled, Count = 12, Tint = UiSurface.Role.Info },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Filled, Count = 3 },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Invite },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Blank },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Locked, Requirement = "Lv 12" },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Blank },
            });
            host.AddChild(grid);

            // Icon buttons: the four states gameui3 names, plus a locked one.
            var ibs = new[]
            {
                new KitIconButton { Glyph = "+" },
                new KitIconButton { Glyph = "*", Accent = UiSurface.Role.Info },
                new KitIconButton { Glyph = "x", Disabled = true },
                new KitIconButton { Glyph = "?", Locked = true, Requirement = "Lv 5" },
            };
            foreach (var b in ibs) host.AddChild(b);

            var tree = new KitTree { Columns = 4, Tiers = 3, Selected = 1 };
            tree.Nodes.AddRange(new[]
            {
                new KitTree.Node { Column = 1, Tier = 0, Branch = 0, State = KitTree.NodeState.Owned, Cost = 1 },
                new KitTree.Node { Column = 0, Tier = 1, Branch = 0, State = KitTree.NodeState.Available, Cost = 2 },
                new KitTree.Node { Column = 2, Tier = 1, Branch = 1, State = KitTree.NodeState.Available, Cost = 2 },
                new KitTree.Node { Column = 1, Tier = 2, Branch = 2, State = KitTree.NodeState.Locked },
                new KitTree.Node { Column = 3, Tier = 2, Branch = 3, State = KitTree.NodeState.Locked },
            });
            tree.Nodes[1].Parents.Add(0);
            tree.Nodes[2].Parents.Add(0);
            tree.Nodes[3].Parents.Add(1);
            tree.Nodes[4].Parents.Add(2);
            host.AddChild(tree);

            var cur = new KitCurrencyBar();
            cur.Entries.Clear();
            cur.Entries.AddRange(new[]
            {
                new KitCurrencyBar.Entry { Value = "12,480", Glyph = "$", Accent = UiSurface.Role.Warning },
                new KitCurrencyBar.Entry { Value = "340", Glyph = "*", Accent = UiSurface.Role.Info },
            });
            host.AddChild(cur);

            var tabs = new KitTabStrip();
            tabs.Tabs.Clear();
            tabs.Tabs.AddRange(new[]
            {
                new KitTabStrip.Tab { Text = "GEAR" },
                new KitTabStrip.Tab { Text = "MAP", Badge = 3 },
                new KitTabStrip.Tab { Text = "QUESTS" },
            });
            host.AddChild(tabs);

            var card1 = new KitNodeCard { Title = "Iron Axe", Footer = KitNodeCard.FooterKind.Status, FooterText = "OWNED" };
            var card2 = new KitNodeCard { Title = "Steel Axe", Footer = KitNodeCard.FooterKind.Action, FooterText = "BUY 40", FooterRole = UiSurface.Role.Warning };
            var card3 = new KitNodeCard { Title = "Rune Axe", Footer = KitNodeCard.FooterKind.Status, FooterText = "LOCKED", Locked = true, Requirement = "Needs Lv 8" };
            host.AddChild(card1); host.AddChild(card2); host.AddChild(card3);




            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            meterSeg.Size = meterSeg.CustomMinimumSize;
            meterSeg.Position = new Vector2(320, 60);
            meterCont.Size = meterCont.CustomMinimumSize;
            meterCont.Position = new Vector2(320, 60 + meterSeg.Size.Y + 14);
            panel.Size = panel.CustomMinimumSize;
            panel.Position = new Vector2(320, 150);
            panelC.Size = new Vector2(panelC.CustomMinimumSize.X, 40);
            panelC.Position = new Vector2(320 + panel.Size.X + 40, 150);
            kp.Size = kp.CustomMinimumSize;
            kp.Position = new Vector2(60, 330);
            grid.Size = grid.CustomMinimumSize;
            grid.Position = new Vector2(60 + kp.Size.X + 60, 330);
            for (int i = 0; i < ibs.Length; i++)
            {
                ibs[i].Size = ibs[i].CustomMinimumSize;
                ibs[i].Position = new Vector2(620 + i * (ibs[i].Size.X + 14), 60);
            }
            tree.Size = tree.CustomMinimumSize;
            tree.Position = new Vector2(620, 330);

            cur.Size = new Vector2(360, 44); cur.Position = new Vector2(620, 130);
            tabs.Size = new Vector2(300, 38); tabs.Position = new Vector2(620, 190);
            card1.Size = card1.CustomMinimumSize; card1.Position = new Vector2(660, 430);
            card2.Size = card2.CustomMinimumSize; card2.Position = new Vector2(660 + card1.Size.X + 16, 430);
            card3.Size = card3.CustomMinimumSize; card3.Position = new Vector2(660 + (card1.Size.X + 16) * 2, 430);


            for (int i = 0; i < 3; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await Shoot($"{OutDir}/widgets_{genre}.png");
            GD.Print($"kitproof: widgets {genre}");
            host.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await ShootFormKit(root, genre);
        }
    }


    /// <summary>
    /// The form / small-part / radial families on their own sheet. They were first laid out on
    /// the main widget sheet and collided with the panel and slot grid -- a proof sheet that
    /// overlaps itself proves nothing.
    /// </summary>
    private async System.Threading.Tasks.Task ShootFormKit(Control root, string genre)
    {
        var host = new Control { Name = "FormKit" };
        host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.Theme = SheetTheme();
        root.AddChild(host);

        var chips = new Godot.Control[]
        {
            new KitChip { Kind = KitChip.ChipKind.Rarity, Text = "RARE", Role = UiSurface.Role.Accent2 },
            new KitChip { Kind = KitChip.ChipKind.Count, Text = "9" },
            new KitChip { Kind = KitChip.ChipKind.Dot, Role = UiSurface.Role.Danger },
            new KitChip { Kind = KitChip.ChipKind.Status, Positive = true, Role = UiSurface.Role.Success },
            new KitChip { Kind = KitChip.ChipKind.Status, Positive = false, Role = UiSurface.Role.Danger },
        };
        foreach (var c in chips) host.AddChild(c);

        var sld = new KitSlider { Value = 0.62f };
        // ButtonPressed, not Pressed: KitToggle is a CheckButton now, so the latch is
        // BaseButton's property and `Pressed` is BaseButton's SIGNAL.
        var tg1 = new KitToggle { ButtonPressed = true };
        var tg2 = new KitToggle { ButtonPressed = false };
        var tg3 = new KitToggle { ButtonPressed = true, Style = KitToggle.ToggleStyle.Box };
        var sel = new KitArrowSelector();
        var rad = new KitRadialMeter { Value = 0.68f, CentreText = "68" };
        var rad2 = new KitRadialMeter { Value = 0.4f, Segments = 0, Fill = UiSurface.Role.Danger, GapDegrees = 0f };
        var star = new KitStarRating { Total = 3, Earned = 2 };
        // Godot.Control, not KitControl: the widgets with a real Godot equivalent now derive
        // from it, so the only type they still share is Control -- which is the point.
        var all = new Godot.Control[] { sld, tg1, tg2, tg3, sel, rad, rad2, star };
        foreach (var w in all) host.AddChild(w);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        float cx = 60f;
        foreach (var c in chips) { c.Size = c.CustomMinimumSize; c.Position = new Vector2(cx, 60); cx += c.Size.X + 14; }
        sld.Size = new Vector2(220, 28); sld.Position = new Vector2(60, 120);
        tg1.Size = tg1.CustomMinimumSize; tg1.Position = new Vector2(60, 180);
        tg2.Size = tg2.CustomMinimumSize; tg2.Position = new Vector2(150, 180);
        tg3.Size = tg3.CustomMinimumSize; tg3.Position = new Vector2(240, 180);
        sel.Size = new Vector2(220, 34); sel.Position = new Vector2(60, 230);
        rad.Size = new Vector2(90, 90); rad.Position = new Vector2(60, 290);
        rad2.Size = new Vector2(90, 90); rad2.Position = new Vector2(175, 290);
        star.Size = new Vector2(130, 40); star.Position = new Vector2(60, 400);

        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // Second page of the form sheet: hangers, ornaments and the comparison primitives.
        foreach (var w in all) w.Visible = false;
        foreach (var c2 in chips) c2.Visible = false;

        var hangers = new KitPanelHanger[6];
        var kinds = new[] { KitPanelHanger.HangerKind.Chain, KitPanelHanger.HangerKind.Rope,
                            KitPanelHanger.HangerKind.Nail, KitPanelHanger.HangerKind.Tape,
                            KitPanelHanger.HangerKind.ScrollRoll, KitPanelHanger.HangerKind.Vine };
        for (int i = 0; i < 6; i++)
        {
            hangers[i] = new KitPanelHanger { Kind = kinds[i] };
            host.AddChild(hangers[i]);
        }
        var orns = new KitOrnament[6];
        var okinds = new[] { KitOrnament.OrnamentKind.Crown, KitOrnament.OrnamentKind.Wings,
                             KitOrnament.OrnamentKind.Laurel, KitOrnament.OrnamentKind.Trophy,
                             KitOrnament.OrnamentKind.Starburst, KitOrnament.OrnamentKind.RibbonTail };
        for (int i = 0; i < 6; i++)
        {
            orns[i] = new KitOrnament { Kind = okinds[i] };
            host.AddChild(orns[i]);
        }
        var tip = new KitTooltip { Text = "Costs 40 gold" };
        var hint = new KitInputHint { Keys = new[] { "L2", "X" }, Action = "Boost" };
        var radar = new KitRadarChart();
        host.AddChild(tip); host.AddChild(hint); host.AddChild(radar);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (int i = 0; i < 6; i++)
        {
            hangers[i].Size = new Vector2(110, 46);
            hangers[i].Position = new Vector2(50 + i * 124, 60);
            orns[i].Size = new Vector2(64, 46);
            orns[i].Position = new Vector2(64 + i * 124, 160);
        }
        tip.Size = new Vector2(190, 52); tip.Position = new Vector2(60, 250);
        hint.Size = new Vector2(230, 34); hint.Position = new Vector2(290, 258);
        radar.Size = new Vector2(190, 190); radar.Position = new Vector2(60, 330);

        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Shoot($"{OutDir}/formkit2_{genre}.png");

        // Page 3: rows, avatar, pager, segmented group, spinners.
        foreach (var h in hangers) h.Visible = false;
        foreach (var o in orns) o.Visible = false;
        tip.Visible = false; hint.Visible = false; radar.Visible = false;

        var rows = new[]
        {
            new KitRow { Rank = "1", Title = "Recover the Cargo", Subtitle = "Sector 4", Value = "1,240", State_ = "NEW" },
            new KitRow { Rank = "2", Title = "Escort the Convoy", Subtitle = "Dust Flats", Value = "980", State_ = "DONE", StateRole = UiSurface.Role.Info, Alternate = true },
            new KitRow { Rank = "3", Title = "Hold the Ridge", Subtitle = "Locked", Value = "--", State_ = "LOCKED", StateRole = UiSurface.Role.Danger, Selected = true },
        };
        foreach (var w in rows) host.AddChild(w);
        var av = new KitAvatarFrame { BadgeText = "12" };
        var pg = new KitPager { PageCount = 5, Page = 2 };
        var pg2 = new KitPager { PageCount = 40, Page = 12 };
        var seg = new KitSegmentedIconGroup();
        var sp1 = new KitSpinner { Kind = KitSpinner.SpinnerKind.Ring };
        var sp2 = new KitSpinner { Kind = KitSpinner.SpinnerKind.Dots };
        var sp3 = new KitSpinner { Kind = KitSpinner.SpinnerKind.Bar, Progress = 0.45f };
        foreach (var w in new Godot.Control[] { av, pg, pg2, seg, sp1, sp2, sp3 }) host.AddChild(w);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (int i = 0; i < rows.Length; i++)
        { rows[i].Size = new Vector2(430, 46); rows[i].Position = new Vector2(50, 50 + i * 52); }
        av.Size = new Vector2(96, 96); av.Position = new Vector2(520, 50);
        pg.Size = new Vector2(210, 34); pg.Position = new Vector2(50, 225);
        pg2.Size = new Vector2(210, 34); pg2.Position = new Vector2(280, 225);
        seg.Size = new Vector2(150, 38); seg.Position = new Vector2(50, 285);
        sp1.Size = new Vector2(52, 52); sp1.Position = new Vector2(230, 280);
        sp2.Size = new Vector2(70, 52); sp2.Position = new Vector2(300, 280);
        sp3.Size = new Vector2(180, 16); sp3.Position = new Vector2(390, 298);

        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Shoot($"{OutDir}/formkit3_{genre}.png");

        // Page 4: the final set pieces.
        foreach (var w in rows) w.Visible = false;
        av.Visible = false; pg.Visible = false; pg2.Visible = false; seg.Visible = false;
        sp1.Visible = false; sp2.Visible = false; sp3.Visible = false;

        var knob = new KitKnob { Value = 0.35f };
        var gem1 = new KitGemSlot { State_ = KitGemSlot.SocketState.Filled };
        var gem2 = new KitGemSlot { State_ = KitGemSlot.SocketState.Invite };
        var gem3 = new KitGemSlot { State_ = KitGemSlot.SocketState.Locked, Requirement = "Lv 9" };
        var path = new KitLevelPath();
        var wheel = new KitSpinWheel();
        var book = new KitBookSpread { LeftTitle = "Quests", RightTitle = "Bestiary" };
        var torn = new KitPanel { Title = "NOTES", TornEdge = true, ShowClose = true };
        foreach (var w in new Godot.Control[] { knob, gem1, gem2, gem3, path, wheel, book, torn })
            host.AddChild(w);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        knob.Size = new Vector2(76, 76); knob.Position = new Vector2(50, 50);
        gem1.Size = new Vector2(54, 54); gem1.Position = new Vector2(150, 60);
        gem2.Size = new Vector2(54, 54); gem2.Position = new Vector2(212, 60);
        gem3.Size = new Vector2(54, 54); gem3.Position = new Vector2(274, 60);
        path.Size = new Vector2(300, 210); path.Position = new Vector2(50, 160);
        wheel.Size = new Vector2(190, 190); wheel.Position = new Vector2(380, 50);
        book.Size = new Vector2(360, 200); book.Position = new Vector2(600, 50);
        torn.Size = new Vector2(230, 150); torn.Position = new Vector2(380, 280);

        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Shoot($"{OutDir}/formkit4_{genre}.png");



        await Shoot($"{OutDir}/formkit_{genre}.png");
        GD.Print($"kitproof: formkit {genre}");
        host.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>A minimal stand-in for what ThemePresetComponent publishes: a panel surface and
    /// the BeepSemantic role colours the kit widgets read.</summary>
    private static Theme SheetTheme()
    {
        var t = new Theme { DefaultFontSize = 15 };
        var panel = new StyleBoxFlat { BgColor = new Color(0.29f, 0.31f, 0.38f) };
        t.SetStylebox("panel", "PanelContainer", panel);
        t.SetColor("font_color", "Label", new Color(0.93f, 0.94f, 0.97f));
        t.SetColor("accent", UiSurface.SemanticType, new Color(0.35f, 0.62f, 0.95f));
        t.SetColor("accent2", UiSurface.SemanticType, new Color(0.62f, 0.48f, 0.90f));
        t.SetColor("success", UiSurface.SemanticType, new Color(0.36f, 0.78f, 0.44f));
        t.SetColor("warning", UiSurface.SemanticType, new Color(0.95f, 0.72f, 0.24f));
        t.SetColor("danger", UiSurface.SemanticType, new Color(0.90f, 0.34f, 0.32f));
        t.SetColor("info", UiSurface.SemanticType, new Color(0.32f, 0.74f, 0.86f));
        t.SetColor("neutral", UiSurface.SemanticType, new Color(0.62f, 0.65f, 0.72f));
        return t;
    }

    private async System.Threading.Tasks.Task Shoot(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var err = GetViewport().GetTexture().GetImage().SavePng(path);
        if (err != Error.Ok) GD.PushError($"kitproof: save failed {err} for {path}");
    }
}
