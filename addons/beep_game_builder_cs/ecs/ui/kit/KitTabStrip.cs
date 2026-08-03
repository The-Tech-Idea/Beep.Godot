using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A row of tabs, welded to the panel below them.
    ///
    /// CATALOGUE-FROM-ART.md section A ranks this in the top build tier — it "appears in nearly
    /// every picture". The art pass measured SEVENTEEN distinct selection mechanisms across the
    /// folder and concluded the choice follows widget CLASS, with a convention per class:
    /// <b>tab strips use fill and elevation</b> (gameui8: "a filled pill appears behind the tab";
    /// gameui9: "raise the selected tab"), while card carousels use an outline. So
    /// <see cref="Selection"/> offers exactly the tab-appropriate mechanisms rather than a
    /// generic "selected" look shared with every other widget.
    ///
    /// The selected tab is painted in the PANEL's colour so it welds to the content area, and
    /// carries no bottom border — the lesson Stage 28 paid for on the settings screen, where a
    /// generic surface box gave every tab a drop shadow that fell across its neighbours.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitTabStrip : TabBar
    {
        public enum SelectionStyle
        {
            /// <summary>Selected tab takes the panel colour and welds to it (gameui8).</summary>
            Weld,
            /// <summary>A filled pill appears behind the selected tab (gameui8).</summary>
            Pill,
            /// <summary>The selected tab is raised above the strip (gameui9).</summary>
            Elevate,
        }

        public sealed class Tab
        {
            public string Text = "Tab";
            public Texture2D? Icon;
            /// <summary>Corner flash badge — section A names this on the tab strip specifically.
            /// 0 hides it.</summary>
            public int Badge;
        }

        public readonly List<Tab> Tabs = new();

        [Export] public SelectionStyle Selection { get; set; } = SelectionStyle.Weld;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;
        private int _hoverTab = -1;

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            if (Tabs.Count == 0 && GetTabCount() == 0)
                Tabs.AddRange(new[] { new Tab { Text = "One" }, new Tab { Text = "Two" },
                                      new Tab { Text = "Three" } });

            // Preserve tabs authored on the real TabBar. Only seed from the C# list when the
            // native TabBar has no tabs yet; clearing here breaks scene-authored tabs and their
            // selection/click behaviour.
            if (GetTabCount() == 0)
                foreach (var t in Tabs) AddTab(t.Text);
            Suppress();
            TabChanged += _ => QueueRedraw();
            MouseExited += () => { _hoverTab = -1; QueueRedraw(); };
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion motion)
            {
                int hit = HitTab(motion.Position);
                if (_hoverTab != hit)
                {
                    _hoverTab = hit;
                    QueueRedraw();
                }
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                int hit = HitTab(mb.Position);
                if (hit >= 0 && !IsTabDisabled(hit))
                {
                    CurrentTab = hit;
                    _hoverTab = hit;
                    AcceptEvent();
                    QueueRedraw();
                    return;
                }
            }

            base._GuiInput(@event);
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            QueueRedraw();
        }

        /// <summary>Blank TabBar's own tab plates, then restate the size they were providing —
        /// the same trap the Slider grabber set: a control whose theme art is blanked collapses
        /// and _Draw's size guard then makes it vanish in silence.</summary>
        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            foreach (string sb in new[] { "tab_selected", "tab_hovered", "tab_unselected",
                                          "tab_disabled", "tab_focus", "button_pressed",
                                          "button_highlight" })
                AddThemeStyleboxOverride(sb, new StyleBoxEmpty());
            int fs = UiSurface.FontSize(this);
            AddThemeColorOverride("font_selected_color", new Color(0, 0, 0, 0));
            AddThemeColorOverride("font_unselected_color", new Color(0, 0, 0, 0));
            AddThemeColorOverride("font_hovered_color", new Color(0, 0, 0, 0));
            int count = Mathf.Max(1, GetTabCount() > 0 ? GetTabCount() : Tabs.Count);
            CustomMinimumSize = new Vector2(Mathf.Max(CustomMinimumSize.X, 72f * count),
                                            Mathf.Clamp(fs * 1.75f, 26f, 34f));
            _suppressing = false;
        }

        private KitShape TabShape => Geo.Register == KitRegister.Pixel ? KitShape.Stepped : KitShape.Round;

        private Rect2 TabRect(int i)
        {
            int count = Mathf.Max(1, GetTabCount() > 0 ? GetTabCount() : Tabs.Count);
            float w = Size.X / count;
            // 6px separation at 14pt — tabs are near-touching, so the gap is deliberate and small.
            float sep = Mathf.Max(2f, UiSurface.FontSize(this) * 0.24f) * 0.5f;
            float raise = Selection == SelectionStyle.Elevate && i == CurrentTab ? 0f : Size.Y * 0.08f;
            return new Rect2(i * w + sep, raise, w - sep * 2f, Size.Y - raise);
        }

        private int HitTab(Vector2 p)
        {
            int count = GetTabCount();
            if (count <= 0) count = Tabs.Count;
            for (int i = 0; i < count; i++)
                if (TabRect(i).HasPoint(p)) return i;
            return -1;
        }

        private string TabText(int i)
            => i >= 0 && i < Tabs.Count && !string.IsNullOrEmpty(Tabs[i].Text)
                ? Tabs[i].Text
                : GetTabTitle(i);

        private int TabBadge(int i)
            => i >= 0 && i < Tabs.Count ? Tabs[i].Badge : 0;

        public override void _Draw()
        {
            int count = GetTabCount();
            if (count <= 0) count = Tabs.Count;
            if (Size.X <= 8 || Size.Y <= 6 || count == 0) return;

            var g = Geo;
            Color face = UiSurface.Of(this);
            Color ink = UiSurface.Ink(UiSurface.Of(this));
            var font = KitChrome.Font(this, _genre);
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * 0.7f * (fs / 14f));

            for (int i = 0; i < count; i++)
            {
                Rect2 r = TabRect(i);
                if (r.Size.X < 3f) continue;
                bool sel = i == CurrentTab;
                bool hover = i == _hoverTab && !sel && !IsTabDisabled(i);

                // Unselected is the pressed surface, selected the panel's own colour: the pair
                // must be clearly different, not two near-identical greys.
                Color plate = sel
                    ? face
                    : new Color(face.R * 0.72f, face.G * 0.72f, face.B * 0.76f, 1f);
                if (hover)
                    plate = KitChrome.StateFace(plate, KitState.Hover);

                if (sel && Selection == SelectionStyle.Pill)
                {
                    // The pill sits BEHIND the tab and is the only accented element.
                    Color acc = UiSurface.Semantic(this, UiSurface.Role.Accent);
                    KitChrome.DrawShape(this, _genre, r, KitShape.Pill, acc, ink, rimPx);
                    plate = new Color(acc.R, acc.G, acc.B, 1f);
                }
                else
                {
                    KitChrome.DrawShape(this, _genre, r, TabShape, plate, sel ? KitChrome.Rim(UiSurface.Of(this), Geo) : ink, rimPx);
                }

                if (sel || hover)
                {
                    Color acc = UiSurface.Semantic(this, UiSurface.Role.Accent);
                    float y = r.End.Y - Mathf.Max(2f, fs * 0.18f);
                    DrawLine(new Vector2(r.Position.X + r.Size.X * 0.18f, y),
                             new Vector2(r.End.X - r.Size.X * 0.18f, y),
                             acc with { A = sel ? 0.90f : 0.48f },
                             Mathf.Max(2f, fs * 0.16f));
                }

                string text = TabText(i);
                if (font != null && !string.IsNullOrEmpty(text))
                {
                    // An unselected tab is a place you CAN go: normal text at reduced alpha, not
                    // the disabled colour. Reading it as unavailable was a real Stage 28 defect.
                    Color txt = UiSurface.Text(this);
                    if (!sel) txt = txt with { A = 0.78f };
                    // A tab's width is the strip divided by the tab count, so a long title has
                    // to shrink to its own tab rather than run into the next one.
                    int tf = UiSurface.FitRole(this, UiSurface.TextRole.Body,
                                               new Vector2(r.Size.X * 0.86f, r.Size.Y * 0.62f),
                                               text, font);
                    Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, tf);
                    KitChrome.DrawText(this, _genre, font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                               text, tf, txt);
                }

                // Corner flash badge, straddling the tab's top-right — the attention anchor the
                // art pass measured eight independent times.
                int badge = TabBadge(i);
                if (badge > 0 && font != null)
                {
                    string b = badge.ToString();
                    int small = Mathf.Max(8, Mathf.RoundToInt(fs * 0.7f));
                    Vector2 m = font.GetStringSize(b, HorizontalAlignment.Left, -1, small);
                    float bw = Mathf.Max(m.X + small * 0.7f, small * 1.4f), bh = small * 1.2f;
                    // Straddle the corner, but stay inside the STRIP: at -bh*0.35 the badge was
                    // drawn above y=0 and got cut off by the control's own top edge, and a 0.6
                    // overhang pushed it into the next tab. Sit it just inside the top and
                    // overhang less, so it still reads as a corner flash without being clipped
                    // or colliding with its neighbour.
                    var br = new Rect2(r.End.X - bw * 0.78f,
                                       Mathf.Max(0f, r.Position.Y - bh * 0.12f), bw, bh);
                    KitChrome.DrawShape(this, _genre, br, KitShape.Pill, UiSurface.Semantic(this, UiSurface.Role.Danger), ink, 1.5f);
                    KitChrome.DrawText(this, _genre, font, new Vector2(br.Position.X + (br.Size.X - m.X) * 0.5f, br.Position.Y + (br.Size.Y + m.Y * 0.6f) * 0.5f),
                               b, small, new Color(0.98f, 0.96f, 0.92f));
                }
            }

            
        }
    }
}
