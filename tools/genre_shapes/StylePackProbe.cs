using Godot;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

/// Prove two themes of ONE genre produce different styles, authored purely in theme.json.
///
/// This is the end-to-end test of the whole effort: the original complaint was that genres looked
/// alike, and the answer turned out to be that genre never determined the look -- the THEME does,
/// and the theme had nothing to say with. If these two citybuilder themes resolve to the same
/// geometry, the `kit` block is inert and every stage since Phase A is decorative.
public partial class StylePackProbe : Node
{
    public override void _Ready()
    {
        int bad = 0;
        SkinCatalog.SetActiveSkin("citybuilder", "urban", "", "");
        var a = KitGeometry.ForGenre("citybuilder");
        string sa = $"shadow={a.Shadow.Kind} outline={a.OutlineShade:0.00} bar={a.CornerBar:0.00} "
                  + $"font={a.Font} caps={a.UpperCase} slot={a.SelectSlot}";

        SkinCatalog.SetActiveSkin("citybuilder", "blueprint", "", "");
        var b = KitGeometry.ForGenre("citybuilder");
        string sb = $"shadow={b.Shadow.Kind} outline={b.OutlineShade:0.00} bar={b.CornerBar:0.00} "
                  + $"font={b.Font} caps={b.UpperCase} slot={b.SelectSlot}";

        GD.Print($"pack:   urban      {sa}");
        GD.Print($"pack:   blueprint  {sb}");

        if (sa == sb) { bad++; GD.Print("pack:   <-- IDENTICAL, the kit block is inert"); }

        // A theme with NO kit block must fall back to the genre's built-in style, not inherit
        // whatever the previous theme published.
        SkinCatalog.SetActiveSkin("citybuilder", "eco", "", "");
        var c = KitGeometry.ForGenre("citybuilder");
        bool reverted = c.Shadow.Kind == KitShadowKind.Hard && !KitStyleJson.Has("citybuilder");
        GD.Print($"pack:   eco (no kit block) -> shadow={c.Shadow.Kind} "
               + $"{(reverted ? "built-in restored" : "<-- LEAKED from previous theme")}");
        if (!reverted) bad++;

        // MATERIAL. The last axis to stay C#-only, and the loudest difference between two
        // themes of one genre in the references -- stone versus drafting paper.
        SkinCatalog.SetActiveSkin("citybuilder", "urban", "", "");
        var ga = KitGrain.For("citybuilder");
        SkinCatalog.SetActiveSkin("citybuilder", "blueprint", "", "");
        var gb = KitGrain.For("citybuilder");
        GD.Print($"grain:  urban      {ga?.Pattern} x{ga?.Tiles} @{ga?.Amount:0.000} ({ga?.Material})");
        GD.Print($"grain:  blueprint  {gb?.Pattern} x{gb?.Tiles} @{gb?.Amount:0.000}");
        if (ga is not { } va || gb is not { } vb || va.Pattern == vb.Pattern)
        { bad++; GD.Print("grain:  <-- SAME MATERIAL, the grain keys are inert"); }

        // And an untouched genre must keep its generated assignment.
        SkinCatalog.SetActiveSkin("rpg", "", "", "");
        var gr = KitGrain.For("rpg");
        bool kept = gr is { } vr && vr.Pattern == "pattern_50" && vr.Tiles == 3;
        GD.Print($"grain:  rpg (no override) -> {gr?.Pattern} x{gr?.Tiles} "
               + $"{(kept ? "table intact" : "<-- OVERRIDE LEAKED ACROSS GENRES")}");
        if (!kept) bad++;

        // A genre with no table entry has NO grain. KitGrainDef is a struct, so a failed
        // TryGetValue leaves a DEFAULT rather than null -- returning that would hand an unknown
        // genre a grain with an empty pattern, which resolves to "grain_.png", warns once, and
        // renders flat. Asserted rather than reasoned about, because that is exactly the bug the
        // struct semantics produced the first time.
        SkinCatalog.SetActiveSkin("nosuchgenre", "", "", "");
        var gu = KitGrain.For("nosuchgenre");
        GD.Print($"grain:  unknown genre -> {(gu is null ? "null (correct)" : $"{gu.Value.Pattern} <-- PHANTOM GRAIN")}");
        if (gu is not null) bad++;

        GD.Print($"pack:   {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
