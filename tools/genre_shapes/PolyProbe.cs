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

        GetTree().Quit(bad == 0 && cornerBad == 0 ? 0 : 1);
    }
}
