using Godot;
using Beep.ECS.UI.Kit;
using Beep.ECS.UI;

/// Prove (a) text scales with the box it is drawn in, and (b) KitInventorySlot populates from
/// exported values. Rendered at THREE slot sizes, because the whole defect being fixed is that
/// type did not change with size -- one size would prove nothing.
public partial class SlotProbe : Node
{
    public override async void _Ready()
    {
        var root = new Control { Name = "Root" };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var bg = new ColorRect { Color = new Color(0.42f, 0.42f, 0.42f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        SkinCatalog.SetActiveSkin("rpg", "", "", "");
        DirAccess.MakeDirRecursiveAbsolute("res://tmp/slots");

        var icon = MakeIcon();
        float x = 40f;
        foreach (float side in new[] { 44f, 80f, 132f })
        {
            float y = 40f;
            // count badge at three sizes
            foreach (int count in new[] { 0, 7, 128 })
            {
                var s = new KitInventorySlot { Icon = icon, Count = count };
                s.Position = new Vector2(x, y);
                s.Size = new Vector2(side, side);
                root.AddChild(s);
                y += side + 24f;
            }
            // locked + rarity
            var locked = new KitInventorySlot { Locked = true, Requirement = "Needs Lv 8" };
            locked.Position = new Vector2(x, y); locked.Size = new Vector2(side, side);
            root.AddChild(locked);
            y += side + 40f;

            var rare = new KitInventorySlot { Icon = icon, Count = 3, Rarity = UiSurface.Role.Info, Selected = true };
            rare.Position = new Vector2(x, y); rare.Size = new Vector2(side, side);
            root.AddChild(rare);

            x += side + 60f;
        }

        // Type-scale strip: the SAME card at two sizes, so the title must differ.
        float cx = x + 30f;
        foreach (var sz in new[] { new Vector2(110, 130), new Vector2(210, 250) })
        {
            var card = new KitNodeCard
            {
                Title = "Iron Axe", Footer = KitNodeCard.FooterKind.Status, FooterText = "OWNED",
            };
            card.Position = new Vector2(cx, 40f);
            card.Size = sz;
            root.AddChild(card);
            cx += sz.X + 30f;
        }

        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var err = GetViewport().GetTexture().GetImage().SavePng("res://tmp/slots/slots.png");
        GD.Print(err == Error.Ok ? "slotprobe: ok" : $"slotprobe: SAVE FAILED {err}");
        GetTree().Quit();
    }

    /// A generated stand-in item icon. The addon ships no item art on purpose.
    private static Texture2D MakeIcon()
    {
        var img = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        for (int y = 0; y < 32; y++)
        for (int t = 0; t < 32; t++)
            if (Mathf.Abs(t - 16) + Mathf.Abs(y - 16) < 13)
                img.SetPixel(t, y, new Color(0.72f, 0.55f, 0.25f));
        return ImageTexture.CreateFromImage(img);
    }
}
