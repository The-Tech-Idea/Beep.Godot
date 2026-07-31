using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// Render one widget per genre on a FLAT field at a KNOWN rect, and write that rect out.
///
/// measure_shadow.py measures the ring OUTSIDE the widget, so it has to know exactly where the
/// widget is. Inferring it does not work (a shadow also differs from the background) and
/// ASSUMING it is worse: assuming 260x150 against a 420x260 render put the measuring ring inside
/// the plate and reported a confident "hard" shadow for ten genres that have none at all.
/// The probe knows; the probe says.
public partial class ShadowProbe : Node
{
    private static readonly string[] Genres =
    {
        "platformer", "topdown", "shooter", "puzzle", "rpg",
        "survival", "racing", "citybuilder", "strategy", "cardgame",
    };

    private const string OutDir = "res://tmp/shadow";
    private static readonly Vector2 Size = new(300, 170);

    public override async void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(OutDir);
        // Flip to true to print each layer's resolved shade and drawn luminance.
        KitControl.DebugOutline = false;
        var root = new Control { Name = "Root" };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // A flat, light field. Light on purpose: a shadow must read as DARKER than its ground,
        // and a mid-grey field leaves too little headroom below it to measure a soft falloff.
        var bg = new ColorRect { Color = new Color(0.78f, 0.78f, 0.78f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Vector2 pos = ((root.Size - Size) * 0.5f).Round();

        using var rects = FileAccess.Open($"{OutDir}/rects.txt", FileAccess.ModeFlags.Write);

        foreach (string genre in Genres)
        {
            SkinCatalog.SetActiveSkin(genre, "", "", "");
            var btn = new KitButton { Text = "" };
            root.AddChild(btn);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            btn.Size = Size;
            btn.Position = pos;

            for (int i = 0; i < 4; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            var err = GetViewport().GetTexture().GetImage().SavePng($"{OutDir}/sh_{genre}.png");
            if (err != Error.Ok) GD.Print($"shadow: SAVE FAILED {genre} {err}");

            // The same widget again with shadows off. The gate analyses the DIFFERENCE, so the
            // silhouette -- overhanging or inset -- cancels exactly and only the shadow remains.
            KitShadow.Enabled = false;
            btn.QueueRedraw();
            for (int i = 0; i < 3; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{OutDir}/nos_{genre}.png");
            // POLARITY PASS: shadows still off, but on an opaque DARK field.
            //
            // The light field is required for the shadow measurement (a shadow must read darker
            // than its ground), and it is exactly what makes the polarity reading suspect: a
            // dark outline band lifts far more against a light ground than the plate does if any
            // alpha is involved. Rendering the same widget on a dark field settles it -- if the
            // apparent polarity flips, the pixels are being blended, not drawn.
            bg.Color = new Color(0.08f, 0.08f, 0.09f);
            btn.QueueRedraw();
            for (int i = 0; i < 3; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{OutDir}/pol_{genre}.png");
            bg.Color = new Color(0.78f, 0.78f, 0.78f);

            KitShadow.Enabled = true;
            rects?.StoreLine($"{genre} {pos.X:0} {pos.Y:0} {Size.X:0} {Size.Y:0}");
            GD.Print($"shadow: ok {genre} at {pos.X:0},{pos.Y:0} {Size.X:0}x{Size.Y:0}");

            btn.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print("shadow: done");
        GetTree().Quit();
    }
}
