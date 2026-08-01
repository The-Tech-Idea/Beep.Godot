using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// Renders each genre's plate TWICE — with the rim and without — so measure_outline.py can
/// difference them. See KitRim for why differencing is the only honest way to find the rim.
public partial class OutlineProbe : Node
{
    private static readonly string[] Genres =
    {
        "rpg", "survival", "strategy", "citybuilder", "platformer",
        "puzzle", "cardgame", "topdown", "shooter", "racing",
    };

    public override void _Ready() => _ = Run();

    private async System.Threading.Tasks.Task Run()
    {
        DirAccess.MakeDirRecursiveAbsolute("res://tmp/outline");
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var bg = new ColorRect { Color = new Color(0.42f, 0.42f, 0.42f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);
        root.AddChild(new ThemePresetComponent());

        foreach (string genre in Genres)
        {
            SkinCatalog.SetActiveSkin(genre, "", "", "");
            var g = KitGeometry.ForGenre(genre);

            foreach (bool rim in new[] { true, false })
            {
                // SHADOW OFF for both: its edge shares the silhouette and would show up in the
                // difference as if it were rim. The rim toggle is the only variable.
                KitShadow.Enabled = false;
                KitRim.Enabled = rim;

                var plate = new KitButton { Text = "" };
                root.AddChild(plate);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                plate.Size = new Vector2(360, 220);
                plate.Position = ((root.Size - plate.Size) * 0.5f).Round();
                for (int i = 0; i < 3; i++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

                var err = GetViewport().GetTexture().GetImage()
                                       .SavePng($"res://tmp/outline/{genre}_{(rim ? "on" : "off")}.png");
                if (err != Error.Ok) GD.Print($"outline: SAVE FAILED {genre} {err}");
                plate.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            // WHICH declared value? This is the ambiguity that produced four wrong answers in a
            // row. `OutlineShade` drives the outermost band only when the register's stack asks
            // for it (`shade: -1`, the theme-decides sentinel) -- Casual and Technical do.
            // CARVED hardcodes 2.05 in its own first layer and never consults OutlineShade at
            // all, so rpg reports a "declared" 0.16 while actually drawing a bright 2.05 rim.
            // Reading the field alone is how you conclude the render is inverted when it is not.
            float effective = 1f;
            foreach (var layer in KitStacks.For(g.Register))
            {
                if (layer.Kind != KitLayerKind.Plate) continue;
                effective = layer.Shade < 0f ? g.OutlineShade : layer.Shade;
                break;                                   // the OUTERMOST band decides polarity
            }
            GD.Print($"outline: {genre,-12} register={g.Register,-9} field={g.OutlineShade:0.00} "
                   + $"effective={effective:0.00} ({(effective >= 1f ? "BRIGHT" : "DARK")})");
        }

        KitShadow.Enabled = true;
        KitRim.Enabled = true;
        GD.Print("outline: rendered");
        GetTree().Quit(0);
    }
}
