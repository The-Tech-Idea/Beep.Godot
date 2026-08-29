using Godot;
using System.Linq;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

namespace Beep.Examples;

/// <summary>
/// THE ADDON, DEMONSTRATED — pick any of the 10 genres and any of its 5 themes, and watch every
/// widget in the panel restyle itself.
///
/// This is the point of the work in Stages 47–52, made visible. The original complaint about this
/// repo was that the genres all looked alike, and the answer turned out to be that **genre never
/// determined the look — the theme does, and the theme had nothing to say with.** So the right
/// example is not a game: it is a switch and a readout.
///
/// The left panel is ordinary kit widgets. Not one of them names a colour, a corner radius, an
/// outline weight or a font. The right panel prints what the selected theme actually resolved to,
/// straight out of `KitGeometry.ForGenre` — so you can see the declaration and its consequence
/// side by side.
///
/// Every value on the right is settable from `catalogs/skins/&lt;genre&gt;/themes/&lt;name&gt;/theme.json`
/// in a `kit` block, with **no C# at all**.
/// </summary>
public partial class StyleShowcase : Control
{
    private OptionButton _genre = null!, _theme = null!, _difficulty = null!;
    private VBoxContainer _axes = null!;
    private Label _sub = null!;

    public override void _Ready()
    {
        // Resolved BY NAME, scene-wide -- a path would hard-code the layout, which is exactly how
        // the addon's own menus broke when a Margin container was inserted.
        _genre = Need<OptionButton>("GenrePicker");
        _theme = Need<OptionButton>("ThemePicker");
        _difficulty = Need<OptionButton>("DifficultyPicker");
        _axes = Need<VBoxContainer>("AxisList");
        _sub = Need<Label>("SubLabel");

        _difficulty.AddItem("Casual");
        _difficulty.AddItem("Normal");
        _difficulty.AddItem("Brutal");
        _difficulty.Selected = 1;

        foreach (var g in SkinCatalog.AllGenres.Values.OrderBy(g => g.Id))
            _genre.AddItem($"{g.Icon}  {g.DisplayName}");

        _genre.ItemSelected += _ => { FillThemes(); Apply(); };
        _theme.ItemSelected += _ => Apply();

        // Start on topdown so the first thing shown is the pixel register -- the axis that is
        // hardest to believe is theme-driven until you watch it switch.
        var ids = GenreIds();
        int start = System.Array.IndexOf(ids, "topdown");
        _genre.Selected = start >= 0 ? start : 0;
        FillThemes();
        Apply();
    }

    private T Need<T>(string name) where T : Node
        => FindChild(name, true, false) as T
           ?? throw new System.InvalidOperationException(
               $"showcase.tscn has no {name} of type {typeof(T).Name}");

    private static string[] GenreIds()
        => SkinCatalog.AllGenres.Values.OrderBy(g => g.Id).Select(g => g.Id).ToArray();

    private string[] ThemeIds()
    {
        string id = GenreIds()[Mathf.Max(0, _genre.Selected)];
        var g = SkinCatalog.GetGenre(id);
        return g == null ? System.Array.Empty<string>()
                         : g.Themes.Values.OrderBy(t => t.Id).Select(t => t.Id).ToArray();
    }

    private void FillThemes()
    {
        _theme.Clear();
        string gid = GenreIds()[Mathf.Max(0, _genre.Selected)];
        var g = SkinCatalog.GetGenre(gid);
        if (g == null) return;
        foreach (var t in g.Themes.Values.OrderBy(t => t.Id)) _theme.AddItem(t.DisplayName);
        _theme.Selected = 0;
    }

    private void Apply()
    {
        var genres = GenreIds();
        var themes = ThemeIds();
        if (genres.Length == 0 || themes.Length == 0) return;

        string gid = genres[Mathf.Max(0, _genre.Selected)];
        string tid = themes[Mathf.Clamp(_theme.Selected, 0, themes.Length - 1)];
        SkinCatalog.SetActiveSkin(gid, tid, "", "");

        // KitControl caches its genre in _Ready and refreshes on NotificationThemeChanged, so the
        // switch has to be announced. Without this the panel keeps drawing the previous theme and
        // it looks like the skin did not apply -- which sends you hunting in the wrong file.
        Refresh(this);

        var geo = KitGeometry.ForGenre(gid);
        bool authored = KitStyleJson.Has(gid);

        _sub.Text = authored
            ? $"{gid}/{tid} — declared by a `kit` block in theme.json."
            : $"{gid}/{tid} — no `kit` block; showing the genre's built-in style.";

        foreach (var child in _axes.GetChildren()) child.QueueFree();

        Row("register", geo.Register.ToString(),
            "decides outline weight, AA, corner construction, font and shadow together");
        Row("shadow", geo.Shadow.Kind.ToString(), "None · Hard · Soft · Glow · Extrude");
        Row("gloss_style", geo.GlossStyle.ToString(), "soft sheen, discrete band, or curved glass");
        Row("outline_shade", $"{geo.OutlineShade:0.00}",
            geo.OutlineShade >= 1f ? "> 1 = a BRIGHT carved rim" : "< 1 = a thick DARK outline");
        Row("font", geo.Font.ToString(),
            KitFonts.HasFace(geo.Font) ? "shipped CC0 face" : "NO CC0 face — warns and falls back");
        Row("upper_case / tracking", $"{geo.UpperCase} / {geo.Tracking:0.00}", "text treatment");
        Row("corner  btn/panel/slot/bar/chip",
            $"{geo.Corner:0.00} / {geo.CornerPanel:0.00} / {geo.CornerSlot:0.00} / "
            + $"{geo.CornerBar:0.00} / {geo.CornerChip:0.00}",
            "radius per widget CLASS, in theme units — not per widget size");
        Row("shear / wobble", $"{geo.Shear:0.00} / {geo.Wobble:0.000}", "silhouette modifiers");
        Row("edge_run", geo.EdgeRun is { } er
                ? $"{er.SegmentCount} segments, {er.DrawnCount} drawn" : "none",
            "constructed frame: weight changes, gaps, blocks, hatch, ticks");
        Row("select  btn/panel/slot",
            $"{geo.SelectButton} / {geo.SelectPanel} / {geo.SelectSlot}",
            "selection cues as a SET, per widget class");
        Row("pixel_size", $"{geo.PixelSize:0}px",
            geo.Register == KitRegister.Pixel ? "art-pixel grid — corners and rims quantise to it"
                                              : "(only used by the Pixel register)");
    }

    /// <summary>One axis: its JSON key, the resolved value, and what it means.</summary>
    private void Row(string key, string value, string note)
    {
        var row = new VBoxContainer();
        _axes.AddChild(row);

        var top = new HBoxContainer();
        row.AddChild(top);

        var k = new Label { Text = key, CustomMinimumSize = new Vector2(220, 0) };
        k.ThemeTypeVariation = "BeepCaption";
        top.AddChild(k);

        var v = new Label { Text = value };
        // BeepValue is one of the four Label variations ThemePresetComponent registers -- it takes
        // the accent colour, so the resolved value is what the eye lands on.
        v.ThemeTypeVariation = "BeepValue";
        top.AddChild(v);

        var n = new Label { Text = "    " + note };
        n.ThemeTypeVariation = "BeepCaption";
        row.AddChild(n);
    }

    /// <summary>Tell every Control in the subtree the theme changed, depth-first.</summary>
    private static void Refresh(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            Refresh(child);
            if (child is Control c)
            {
                c.Notification((int)NotificationThemeChanged);
                c.QueueRedraw();
            }
        }
        if (node is ThemePresetComponent tp) tp.ApplyTheme();
    }
}
