using Godot;
using Beep.ECS.UI.Kit;

/// Every KitShape must produce a polygon Godot can triangulate, at several aspect ratios.
/// A shape that fails draws NOTHING, and the only previous signal was an error buried in a
/// render log -- after the silhouette had already shipped.
public partial class PolyProbe : Node
{
    public override void _Ready()
    {
        int bad = 0, n = 0;
        var sizes = new[] { new Vector2(140, 48), new Vector2(420, 260), new Vector2(90, 90),
                            new Vector2(300, 40), new Vector2(60, 200) };
        foreach (KitShape sh in System.Enum.GetValues<KitShape>())
        foreach (var sz in sizes)
        {
            var r = new Rect2(Vector2.Zero, sz);
            float cut = Mathf.Min(sz.X, sz.Y) * 0.18f;
            var poly = KitControl.OutlinePoly(sh, r, cut);
            n++;
            if (poly.Length < 3 || Geometry2D.TriangulatePolygon(poly).Length == 0)
            {
                bad++;
                GD.Print($"poly: FAIL {sh,-14} {sz.X:0}x{sz.Y:0} pts={poly.Length}");
            }
        }
        GD.Print($"poly: {n - bad}/{n} ok, {bad} FAIL");

        // CORNER PER WIDGET CLASS. One number per genre cannot express the references -- rpgui
        // uses chamfered plaques, rounded slots and square bars inside ONE theme. This asserts
        // the classes actually resolve to different radii where the theme says they should,
        // because a per-class value that silently falls back to the genre default looks
        // identical to not having the feature at all.
        int cornerBad = 0;
        foreach (string genre in new[] { "rpg", "platformer", "shooter", "citybuilder" })
        {
            var g = KitGeometry.ForGenre(genre);
            float btn = g.CornerFor(KitWidgetClass.Button);
            float pan = g.CornerFor(KitWidgetClass.Panel);
            float slot = g.CornerFor(KitWidgetClass.Slot);
            float bar = g.CornerFor(KitWidgetClass.Bar);
            bool distinct = !(Mathf.IsEqualApprox(btn, pan) && Mathf.IsEqualApprox(pan, slot)
                              && Mathf.IsEqualApprox(slot, bar));
            if (!distinct) cornerBad++;
            GD.Print($"corner: {genre,-12} button={btn:0.00} panel={pan:0.00} "
                   + $"slot={slot:0.00} bar={bar:0.00} {(distinct ? "" : " <-- ALL EQUAL")}");
        }
        GD.Print($"corner: {(cornerBad == 0 ? "PASS" : $"FAIL ({cornerBad} genre(s) undifferentiated)")}");

        // SHEAR and WOBBLE. Both are post-passes on the finished polygon, so the two things that
        // can go wrong are: they do nothing (the value never reaches the geometry), or they
        // produce a polygon Godot cannot triangulate -- which draws NOTHING and looks, from a
        // screenshot, exactly like "the effect is subtle".
        int modBad = 0;
        foreach (var (genre, want) in new[] { ("racing", "shear"), ("shooter", "shear"),
                                              ("puzzle", "wobble"), ("platformer", "wobble"),
                                              ("rpg", "none") })
        {
            var g = KitGeometry.ForGenre(genre);
            var rect = new Rect2(Vector2.Zero, new Vector2(300, 170));
            float cut = 170f * g.Corner;
            var plain = KitControl.OutlinePoly(KitShape.Round, rect, cut);
            var mod = KitControl.OutlinePoly(KitShape.Round, rect, cut, g.Shear, g.Wobble);

            float moved = 0f;
            for (int i = 0; i < Mathf.Min(plain.Length, mod.Length); i++)
                moved = Mathf.Max(moved, plain[i].DistanceTo(mod[i]));

            bool triangulates = Geometry2D.TriangulatePolygon(mod).Length > 0;
            bool expected = want != "none";
            bool ok = triangulates && (expected ? moved > 1.0f : moved < 0.01f);
            if (!ok) modBad++;
            GD.Print($"mod:    {genre,-12} shear={g.Shear:0.00} wobble={g.Wobble:0.000} "
                   + $"maxMove={moved:0.0}px tri={triangulates} want={want} {(ok ? "" : "<-- FAIL")}");
        }
        GD.Print($"mod:    {(modBad == 0 ? "PASS" : $"FAIL ({modBad})")}");
        bad += modBad;

        // FONTS. The failure this must catch is silent by construction: a declared family with no
        // shipped face falls back to the theme default and renders IDENTICALLY to a theme that
        // declares nothing. Report coverage per genre and name the gaps; do not fail the probe
        // for them, because Serif/Blackletter/Handwritten have no CC0 face and that is a stated
        // limitation, not a defect. DO fail if a role claims a file that is not on disk.
        int fontBad = 0, uncovered = 0;
        foreach (string genre in new[] { "rpg", "survival", "citybuilder", "strategy", "racing",
                                         "shooter", "platformer", "puzzle", "cardgame", "topdown" })
        {
            var g = KitGeometry.ForGenre(genre);
            bool has = KitFonts.HasFace(g.Font) || g.Font == KitFontRole.Default;
            string path = KitFonts.PathFor(g.Font) ?? "";
            bool onDisk = path.Length == 0 || ResourceLoader.Exists(path);
            if (!onDisk) fontBad++;
            if (!has) uncovered++;
            GD.Print($"font:   {genre,-12} role={g.Font,-12} caps={(g.UpperCase ? "Y" : "n")} "
                   + $"track={g.Tracking:0.00} "
                   + $"{(has ? (onDisk ? "shipped" : "MISSING FILE") : "NO CC0 FACE -> warns")}");
        }
        // ATTACHMENTS. The whole point of KitAttach is that a sub-element may cross its host's
        // edge -- the move containers cannot make. If Resolve() returns a rect wholly INSIDE the
        // host, the primitive has silently degraded to ordinary layout and every "overhanging"
        // medallion in the kit is just a child.
        int attBad = 0;
        var host = new Vector2(300, 170);
        foreach (var (anchor, label) in new[]
                 {
                     (KitAnchor.MiddleLeft, "CapLeft"), (KitAnchor.MiddleRight, "CapRight"),
                     (KitAnchor.TopCentre, "MedallionTop"), (KitAnchor.TopRight, "CornerFlag"),
                     (KitAnchor.Below, "Hanger"),
                 })
        {
            var a = new KitAttach { Anchor = anchor, Size = new Vector2(48, 48), Overhang = 0.5f };
            Rect2 r = a.Resolve(host);
            bool outside = r.Position.X < -0.5f || r.Position.Y < -0.5f
                        || r.End.X > host.X + 0.5f || r.End.Y > host.Y + 0.5f;
            if (!outside) attBad++;
            GD.Print($"attach: {label,-13} rect=({r.Position.X:0},{r.Position.Y:0})"
                   + $"-({r.End.X:0},{r.End.Y:0}) host={host.X:0}x{host.Y:0} "
                   + $"{(outside ? "overhangs" : "<-- INSIDE, not an attachment")}");
        }
        GD.Print($"attach: {(attBad == 0 ? "PASS" : $"FAIL ({attBad} anchor(s) do not overhang)")}");
        bad += attBad;

        GD.Print($"font:   {(fontBad == 0 ? "PASS" : $"FAIL ({fontBad} missing file(s))")}"
               + $"  ({uncovered} genre(s) declare a role with no shipped face -- expected, warns at runtime)");
        bad += fontBad;

        GetTree().Quit(bad == 0 && cornerBad == 0 ? 0 : 1);
    }
}
