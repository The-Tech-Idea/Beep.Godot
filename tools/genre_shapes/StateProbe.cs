using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// Renders the three states added from the art pass, so they can be LOOKED at rather than
/// inferred from a clean build.
public partial class StateProbe : Node
{
    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        SkinCatalog.SetActiveSkin("rpg", "fantasy", "", "");
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var bg = new ColorRect { Color = new Color(0.13f, 0.14f, 0.17f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        // A ThemePresetComponent is what REGISTERS the semantic palette. Without it
        // UiSurface.Semantic returns the same neutral for Success and Danger -- opaque, so the
        // widget's own alpha fallback never fires -- and a delta chip renders grey whatever its
        // sign. The chip was right; the fixture was unthemed, which is its own lesson: a probe
        // that skips the theming step is not showing what a real scene shows.
        root.AddChild(new ThemePresetComponent());

        var col = new VBoxContainer { Position = new Vector2(40, 30) };
        col.AddThemeConstantOverride("separation", 18);
        root.AddChild(col);

        // ── meter end caps, tiered vs plain ──
        foreach (var (tier, caps, label) in new[] { (0, false, "plain"), (0, true, "caps"),
                                                    (3, true, "caps + tier 3") })
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 20);
            col.AddChild(row);
            var l = new Label { Text = label, CustomMinimumSize = new Vector2(140, 0) };
            row.AddChild(l);
            row.AddChild(new KitMeter
            {
                CustomMinimumSize = new Vector2(320, 26),
                Value = 0.68, Tier = tier, EndCaps = caps,
            });
        }

        // ── inventory slot: filled / empty / GHOSTED / locked ──
        var slots = new HBoxContainer();
        slots.AddThemeConstantOverride("separation", 22);
        col.AddChild(slots);
        var tex = MakeGlyph();
        slots.AddChild(new KitInventorySlot { CustomMinimumSize = new Vector2(76, 76), Icon = tex });
        slots.AddChild(new KitInventorySlot { CustomMinimumSize = new Vector2(76, 76) });
        slots.AddChild(new KitInventorySlot { CustomMinimumSize = new Vector2(76, 76), GhostIcon = tex });
        slots.AddChild(new KitInventorySlot
        {
            CustomMinimumSize = new Vector2(76, 76), GhostIcon = tex,
            Locked = true, Requirement = "Lv 12",
        });

        // ── delta chips ──
        var chips = new HBoxContainer();
        chips.AddThemeConstantOverride("separation", 18);
        col.AddChild(chips);
        foreach (float d in new[] { 3f, -2.5f, 12f })
            chips.AddChild(new KitChip { Kind = KitChip.ChipKind.Delta, Delta = d });

        var probe = new KitChip();
        root.AddChild(probe);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"state:  Success={UiSurface.Semantic(probe, UiSurface.Role.Success)} "
               + $"Danger={UiSurface.Semantic(probe, UiSurface.Role.Danger)}");
        probe.QueueFree();

        // ── ARCHETYPES: the same plate, told apart by ornament alone ──
        var arch = new HBoxContainer { Position = new Vector2(40, 330) };
        arch.AddThemeConstantOverride("separation", 34);
        root.AddChild(arch);
        foreach (var a in new[] { KitArchetype.Victory, KitArchetype.Defeat,
                                  KitArchetype.Settings, KitArchetype.LevelUp,
                                  KitArchetype.Inventory })
        {
            var panel = new KitPanel
            {
                Title = a.ToString().ToUpperInvariant(),
                Archetype = a,
                CustomMinimumSize = new Vector2(140, 110),
            };
            arch.AddChild(panel);
        }

        // IDEMPOTENCE: applying twice must not stack a second crown on the first.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var first = arch.GetChild<KitPanel>(0);
        int before = first.GetChildCount();
        first.Archetype = KitArchetype.Victory;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"state:  archetype idempotent: {before} -> {first.GetChildCount()} children "
               + $"{(before == first.GetChildCount() ? "ok" : "FAIL -- ornaments stacked")}");

        for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://tmp/states");
        var err = GetViewport().GetTexture().GetImage().SavePng("res://tmp/states/states.png");
        GD.Print($"state:  {(err == Error.Ok ? "ok res://tmp/states/states.png" : $"FAIL {err}")}");
        GetTree().Quit(err == Error.Ok ? 0 : 1);
    }

    /// A simple shape to stand in for an item icon — the addon ships no art.
    private static Texture2D MakeGlyph()
    {
        var img = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
            if (Mathf.Abs(x - 16) + Mathf.Abs(y - 16) < 13)
                img.SetPixel(x, y, new Color(0.95f, 0.85f, 0.45f));
        return ImageTexture.CreateFromImage(img);
    }
}
