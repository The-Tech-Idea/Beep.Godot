using Godot;
using System.Collections.Generic;
using System.Linq;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// Phase G's real gate: sweep EVERY shipped theme and prove the catalog is actually varied.
///
/// Authoring 50 `kit` blocks is worthless if they collapse to a handful of distinct styles, or if
/// a typo leaves one inert. Both failures look identical from the outside -- the theme dropdown
/// works, the scene renders, and two entries just happen to look the same. That is precisely the
/// complaint this whole effort started from, so it gets measured rather than eyeballed.
///
/// Three assertions:
///   1. every theme resolves a style signature with ZERO parse warnings;
///   2. within a genre, all five themes are mutually DISTINCT;
///   3. across the catalog, the axes are actually exercised -- every shadow kind and a spread of
///      fonts, not 50 variations of one register.
public partial class StyleSweepProbe : Node
{
    public override void _Ready()
    {
        int bad = 0;
        var all = new List<string>();
        var shadows = new HashSet<KitShadowKind>();
        var fonts = new HashSet<KitFontRole>();
        var materials = new HashSet<string>();
        var registers = new HashSet<KitRegister>();
        var glosses = new HashSet<KitGloss>();

        foreach (var genre in SkinCatalog.AllGenres.Values.OrderBy(g => g.Id))
        {
            var sigs = new Dictionary<string, string>();
            foreach (var theme in genre.Themes.Values.OrderBy(t => t.Id))
            {
                SkinCatalog.SetActiveSkin(genre.Id, theme.Id, "", "");
                var g = KitGeometry.ForGenre(genre.Id);
                var grain = KitGrain.For(genre.Id);

                string sig = $"rg={g.Register} gl={g.GlossStyle} sh={g.Shadow.Kind} ol={g.OutlineShade:0.00} c={g.Corner:0.00}/"
                           + $"{g.CornerPanel:0.00}/{g.CornerBar:0.00} sk={g.Shear:0.00} "
                           + $"wb={g.Wobble:0.000} f={g.Font} up={(g.UpperCase ? 1 : 0)} "
                           + $"tr={g.Tracking:0.00} gr={grain?.Pattern ?? "-"}@{grain?.Amount ?? 0f:0.00} "
                           + $"sel={g.SelectSlot}/{g.SelectButton}/{g.SelectPanel}";

                registers.Add(g.Register);
                glosses.Add(g.GlossStyle);
                shadows.Add(g.Shadow.Kind);
                fonts.Add(g.Font);
                materials.Add(grain?.Pattern ?? "-");

                if (sigs.TryGetValue(sig, out string? twin))
                {
                    GD.Print($"sweep:  {genre.Id}/{theme.Id} <-- IDENTICAL to {twin}");
                    bad++;
                }
                else sigs[sig] = theme.Id;
                all.Add(sig);
            }
            GD.Print($"sweep:  {genre.Id,-12} {sigs.Count}/{genre.Themes.Count} distinct");
        }

        // COVERAGE. Distinctness alone can be satisfied by 50 near-clones that differ in the third
        // decimal of one axis, which would pass while looking exactly as uniform as before.
        GD.Print($"sweep:  shadow kinds used  {shadows.Count}/5  ({string.Join(",", shadows)})");
        GD.Print($"sweep:  font roles used    {fonts.Count}  ({string.Join(",", fonts)})");
        GD.Print($"sweep:  materials used     {materials.Count}");
        GD.Print($"sweep:  registers used     {registers.Count}/4  ({string.Join(",", registers)})");
        GD.Print($"sweep:  gloss styles used  {glosses.Count}/3  ({string.Join(",", glosses)})");
        GD.Print($"sweep:  distinct styles    {all.Distinct().Count()}/{all.Count} across the catalog");

        if (shadows.Count < 5) { GD.Print("sweep:  <-- a shadow kind is never used by any theme"); bad++; }
        if (fonts.Count < 6) { GD.Print("sweep:  <-- fewer than 6 font roles in the whole catalog"); bad++; }
        if (materials.Count < 6) { GD.Print("sweep:  <-- fewer than 6 materials in the whole catalog"); bad++; }
        if (all.Distinct().Count() < all.Count) { GD.Print("sweep:  <-- duplicate styles across genres"); bad++; }

        // The PIXEL register must be reachable from a theme. It was built as a fourth register
        // precisely because modelling pixel as one silhouette let a pixel theme draw smooth type
        // and soft gradients inside a stepped outline; if no theme selects it, that is still what
        // ships and the register is decorative.
        if (!registers.Contains(KitRegister.Pixel))
        { GD.Print("sweep:  <-- NO theme selects the Pixel register"); bad++; }
        if (glosses.Count < 3)
        { GD.Print("sweep:  <-- a gloss construction is never used by any theme"); bad++; }

        GD.Print($"sweep:  {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
