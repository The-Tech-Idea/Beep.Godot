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
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
