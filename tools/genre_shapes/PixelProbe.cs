using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// Render proof for <see cref="KitRegister.Pixel"/>.
///
/// The sweep proves a theme SELECTS the register. It cannot prove the register changes what is
/// drawn -- and this repo has shipped exactly that failure twice now (the font role reached only
/// the drop-ins; KitStyleJson was never called at all). Both looked correct from every angle
/// except the rendered pixels.
///
/// So: render the same widget under a pixel theme and a rounded casual theme of the SAME genre,
/// and let tools/genre_shapes/measure_pixel.py measure the corner construction. A staircase and an
/// arc are trivially separable by how many distinct edge positions the corner has -- an arc moves
/// every row, a staircase moves once per step.
public partial class PixelProbe : Node
{
    [Export] public string OutDir { get; set; } = "res://tmp/pixelproof";

    /// (genre, theme, label) — one pixel-register theme and one rounded control from each genre.
    private static readonly string[,] Cases =
    {
        { "platformer", "pixel8bit", "px_platformer" },
        // THREE ROLES, deliberately not two:
        //   px_ the pixel theme under test
        //   rr_ the ARC control for corner construction -- must have wobble 0
        //   sh_ the SHADED control for anti-aliasing -- must be a theme with real depth
        // One theme cannot be both: platformer/modern is the flat-translucent register (no
        // shadow, almost no grain) so it is the right arc control and a useless AA control, at 9
        // luminance levels against the pixel theme's 3.
        { "platformer", "modern",    "rr_platformer" },
        { "platformer", "cartoon",   "sh_platformer" },
        { "topdown",    "classic",   "px_topdown" },
        { "topdown",    "nature",    "sh_topdown" },

        // GLOSS construction, all three from ONE genre so the silhouette is held constant and
        // only the highlight differs. Comparing across genres would confound the band with the
        // shape it is clipped to -- the same mistake that made the topdown pixel pair useless.
        { "puzzle",     "modern",    "gl_linear" },
        { "puzzle",     "candy",     "gl_hard" },
        { "puzzle",     "sea",       "gl_curved" },
    };

    public override void _Ready() => _ = Guarded();

    /// <summary>
    /// `_ = Run()` swallows every exception: the Task faults, nobody awaits it, and Godot sits
    /// idle forever with an empty output directory. That is what this probe did on its first
    /// run -- ten minutes of nothing, and no error anywhere. An async void probe must report its
    /// own failure or it is indistinguishable from a hang.
    /// </summary>
    private async System.Threading.Tasks.Task Guarded()
    {
        try { await Run(); }
        catch (System.Exception e)
        {
            GD.Print($"pixel:  FAILED {e.GetType().Name}: {e.Message}");
            GD.PushError($"pixel: {e}");
            GetTree().Quit(3);
        }
    }

    private async System.Threading.Tasks.Task Run()
    {
        DirAccess.MakeDirRecursiveAbsolute(OutDir);

        var root = new Control { Name = "Canvas" };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var bg = new ColorRect { Color = new Color(0.42f, 0.42f, 0.42f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        for (int i = 0; i < Cases.GetLength(0); i++)
        {
            string genre = Cases[i, 0], theme = Cases[i, 1], label = Cases[i, 2];
            SkinCatalog.SetActiveSkin(genre, theme, "", "");

            // Large and unlabelled. A corner has to be big enough to tell an arc from a staircase:
            // at button size both are a handful of pixels and the measurement is noise, which is
            // the same mistake the material gate made before it grew its own proof pass.
            // GLOSS cases render with the shadow OFF. The measurement normalises against the
            // widget's bounding box, and a soft shadow puts 30px of penumbra outside the plate --
            // so the same 260px plate measured 260 under one theme and 293 under another, and
            // every depth was divided by a different number. Same paired-render trick the shadow
            // gate uses, for the same reason: cancel what you are not measuring.
            KitShadow.Enabled = !label.StartsWith("gl_");

            var plate = new KitButton { Text = "" };
            root.AddChild(plate);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            plate.Size = new Vector2(420, 260);
            plate.Position = ((root.Size - plate.Size) * 0.5f).Round();
            for (int f = 0; f < 3; f++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var g = KitGeometry.ForGenre(genre);
            GD.Print($"pixel:  {label,-14} register={g.Register} gloss={g.GlossStyle} "
                   + $"px={g.PixelSize:0} corner={g.Corner:0.00}");

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var err = GetViewport().GetTexture().GetImage().SavePng($"{OutDir}/{label}.png");
            if (err != Error.Ok) GD.Print($"pixel:  SAVE FAILED {label} {err}");

            plate.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        KitShadow.Enabled = true;
        GD.Print("pixel:  rendered");
        GetTree().Quit(0);
    }
}
