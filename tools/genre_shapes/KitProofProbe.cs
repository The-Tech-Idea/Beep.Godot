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

            for (int i = 0; i < 3; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            await Shoot($"{OutDir}/widgets_{genre}.png");
            GD.Print($"kitproof: widgets {genre}");
            host.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
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
