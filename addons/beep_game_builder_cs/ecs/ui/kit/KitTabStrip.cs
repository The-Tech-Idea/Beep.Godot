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
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

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

        [Export]
        public string[] TabLabels
        {
            get
            {
                var labels = new string[Tabs.Count];
                for (int i = 0; i < Tabs.Count; i++)
                    labels[i] = Tabs[i].Text;
                return labels;
            }
            set => SetTabLabels(value);
        }

        [Export]
        public Texture2D[] TabIcons
        {
            get
            {
                var icons = new Texture2D[Tabs.Count];
                for (int i = 0; i < Tabs.Count; i++)
                    icons[i] = Tabs[i].Icon!;
                return icons;
            }
            set => SetTabIcons(value);
        }

        [Export]
        public int[] TabBadges
        {
            get
            {
                var badges = new int[Tabs.Count];
                for (int i = 0; i < Tabs.Count; i++)
                    badges[i] = Tabs[i].Badge;
                return badges;
            }
            set => SetTabBadges(value);
        }

        public void SetTabs(IEnumerable<Tab>? tabs)
        {
            List<Tab> next = NormalizeTabs(tabs);
            if (SameTabs(Tabs, next)) return;
            Tabs.Clear();
            Tabs.AddRange(next);
            RebuildTabsFromList();
        }

        public void SetTabLabels(string[]? labels)
        {
            int count = labels?.Length ?? 0;
            bool changed = Tabs.Count != count;
            while (Tabs.Count > count)
                Tabs.RemoveAt(Tabs.Count - 1);
            for (int i = 0; i < count; i++)
            {
                EnsureTab(i);
                string next = labels![i] ?? "";
                if (Tabs[i].Text == next) continue;
                Tabs[i].Text = next;
                changed = true;
            }
            if (!changed) return;
            RebuildTabsFromList();
        }

        public void SetTabIcons(Texture2D[]? icons)
        {
            if (icons == null)
            {
                bool changed = false;
                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].Icon == null) continue;
                    Tabs[i].Icon = null;
                    changed = true;
                }
                if (!changed) return;
                RebuildTabsFromList();
                return;
            }

            bool updated = false;
            for (int i = 0; i < icons.Length; i++)
            {
                EnsureTab(i);
                if (Tabs[i].Icon == icons[i]) continue;
                Tabs[i].Icon = icons[i];
                updated = true;
            }
            for (int i = icons.Length; i < Tabs.Count; i++)
            {
                if (Tabs[i].Icon == null) continue;
                Tabs[i].Icon = null;
                updated = true;
            }
            if (!updated) return;
            RebuildTabsFromList();
        }

        public void SetTabBadges(int[]? badges)
        {
            if (badges == null)
            {
                bool changed = false;
                for (int i = 0; i < Tabs.Count; i++)
                {
                    if (Tabs[i].Badge == 0) continue;
                    Tabs[i].Badge = 0;
                    changed = true;
                }
                if (!changed) return;
                RebuildTabsFromList();
                return;
            }

            bool updated = false;
            for (int i = 0; i < badges.Length; i++)
            {
                EnsureTab(i);
                int next = Mathf.Max(0, badges[i]);
                if (Tabs[i].Badge == next) continue;
                Tabs[i].Badge = next;
                updated = true;
            }
            for (int i = badges.Length; i < Tabs.Count; i++)
            {
                if (Tabs[i].Badge == 0) continue;
                Tabs[i].Badge = 0;
                updated = true;
            }
            if (!updated) return;
            RebuildTabsFromList();
        }

        public void AddKitTab(string text, Texture2D? icon = null, int badge = 0)
        {
            Tabs.Add(new Tab { Text = text ?? "", Icon = icon, Badge = Mathf.Max(0, badge) });
            RebuildTabsFromList();
        }

        public bool RemoveKitTab(int index)
        {
            if (index < 0 || index >= Tabs.Count)
                return false;

            Tabs.RemoveAt(index);
            RebuildTabsFromList();
            return true;
        }

        public void ClearKitTabs()
        {
            if (Tabs.Count == 0 && GetTabCount() == 0)
                return;

            Tabs.Clear();
            RebuildTabsFromList();
        }

        public void RefreshTabs()
        {
            RebuildTabsFromList();
        }

        [Export] public SelectionStyle Selection { get => _selection; set { if (_selection == value) return; _selection = value; RefreshVisualAndRedraw(); } }
        private SelectionStyle _selection = SelectionStyle.Weld;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;
        private int _hoverTab = -1;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);

            // Preserve tabs authored on the real TabBar. Only seed from the C# list when the
            // native TabBar has no tabs yet; clearing here breaks scene-authored tabs and their
            // selection/click behaviour.
            if (GetTabCount() == 0)
                AddTabsToNative();
            Suppress();
            if (!_eventsHooked)
            {
                TabChanged += _ => QueueRedraw();
                MouseExited += ClearHover;
                _eventsHooked = true;
            }
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            int count = Mathf.Max(1, GetTabCount() > 0 ? GetTabCount() : Tabs.Count);
            return new Vector2(72f * count, Mathf.Clamp(fs * 1.75f, 26f, 34f));
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                int count = GetTabCount();
                if (dir.X <= -9999 && count > 0) { SelectKeyboardTab(FindEnabledTab(0, 1)); AcceptEvent(); return; }
                if (dir.X >= 9999 && count > 0) { SelectKeyboardTab(FindEnabledTab(count - 1, -1)); AcceptEvent(); return; }
                if (dir.X < 0 && count > 0) { SelectKeyboardTab(FindEnabledTab(CurrentTab - 1, -1)); AcceptEvent(); return; }
                if (dir.X > 0 && count > 0) { SelectKeyboardTab(FindEnabledTab(CurrentTab + 1, 1)); AcceptEvent(); return; }
            }

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
                    GrabFocus();
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
            if (KitChrome.ShouldClearPointerState(this, what))
                ClearHover();
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            RefreshVisualAndRedraw();
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
                KitChrome.SetEmptyStyleboxOverride(this, sb);
            int fs = UiSurface.FontSize(this);
            KitChrome.SetColorOverrideIfChanged(this, "font_selected_color", new Color(0, 0, 0, 0));
            KitChrome.SetColorOverrideIfChanged(this, "font_unselected_color", new Color(0, 0, 0, 0));
            KitChrome.SetColorOverrideIfChanged(this, "font_hovered_color", new Color(0, 0, 0, 0));
            int count = Mathf.Max(1, GetTabCount() > 0 ? GetTabCount() : Tabs.Count);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            _suppressing = false;
        }

        private void RebuildTabsFromList()
        {
            int previous = CurrentTab;
            if (IsInsideTree())
            {
                ClearTabs();
                AddTabsToNative();
                if (GetTabCount() > 0)
                    CurrentTab = Mathf.Clamp(previous, 0, GetTabCount() - 1);
                _hoverTab = -1;
                Suppress();
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
                UpdateMinimumSize();
            }
            RefreshVisualAndRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private void ClearHover()
        {
            if (_hoverTab < 0) return;
            _hoverTab = -1;
            QueueRedraw();
        }

        private void EnsureTab(int index)
        {
            while (Tabs.Count <= index)
                Tabs.Add(new Tab());
        }

        private static List<Tab> NormalizeTabs(IEnumerable<Tab>? tabs)
        {
            var next = new List<Tab>();
            if (tabs == null)
                return next;

            foreach (Tab? tab in tabs)
            {
                next.Add(new Tab
                {
                    Text = tab?.Text ?? "",
                    Icon = tab?.Icon,
                    Badge = Mathf.Max(0, tab?.Badge ?? 0),
                });
            }
            return next;
        }

        private static bool SameTabs(IReadOnlyList<Tab> left, IReadOnlyList<Tab> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
            {
                if ((left[i].Text ?? "") != right[i].Text) return false;
                if (!ReferenceEquals(left[i].Icon, right[i].Icon)) return false;
                if (Mathf.Max(0, left[i].Badge) != right[i].Badge) return false;
            }
            return true;
        }

        private void AddTabsToNative()
        {
            foreach (var tab in Tabs)
            {
                int index = GetTabCount();
                AddTab(tab.Text);
                if (tab.Icon != null)
                    SetTabIcon(index, tab.Icon);
            }
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

        private int FindEnabledTab(int start, int step)
        {
            int count = GetTabCount();
            if (count <= 0 || step == 0) return -1;
            int i = Mathf.Clamp(start, 0, count - 1);
            for (; i >= 0 && i < count; i += step)
            {
                if (!IsTabDisabled(i))
                    return i;
            }
            return -1;
        }

        private bool SelectKeyboardTab(int index)
        {
            if (index < 0 || index >= GetTabCount() || IsTabDisabled(index))
                return false;
            CurrentTab = index;
            _hoverTab = index;
            QueueRedraw();
            return true;
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
            if (Size.X <= 8 || Size.Y <= 6) return;
            if (count == 0)
            {
                KitChrome.DrawEmptyPreview(this, _genre, new Rect2(Vector2.Zero, Size),
                                           TabShape, "Tabs");
                return;
            }

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
                bool disabled = IsTabDisabled(i);
                bool hover = i == _hoverTab && !sel && !disabled;

                // Unselected is the pressed surface, selected the panel's own colour: the pair
                // must be clearly different, not two near-identical greys.
                Color plate = sel
                    ? face
                    : new Color(face.R * 0.72f, face.G * 0.72f, face.B * 0.76f, 1f);
                if (disabled)
                    plate = new Color(face.R * 0.58f, face.G * 0.58f, face.B * 0.62f, 0.68f);
                if (hover)
                    plate = KitChrome.StateFace(plate, KitState.Hover);

                if (sel && Selection == SelectionStyle.Pill)
                {
                    // The pill sits BEHIND the tab and is the only accented element.
                    Color acc = UiSurface.SemanticOrDerived(this, UiSurface.Role.Accent);
                    KitChrome.DrawShape(this, _genre, r, KitShape.Pill, acc, ink, rimPx);
                    plate = new Color(acc.R, acc.G, acc.B, 1f);
                }
                else
                {
                    KitChrome.DrawShape(this, _genre, r, TabShape, plate, sel ? KitChrome.Rim(UiSurface.Of(this), Geo) : ink, rimPx);
                }

                if (sel || hover)
                {
                    Color acc = UiSurface.SemanticOrDerived(this, UiSurface.Role.Accent);
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
                    if (disabled) txt = txt with { A = 0.38f };
                    else if (!sel) txt = txt with { A = 0.78f };
                    // A tab's width is the strip divided by the tab count, so a long title has
                    // to shrink to its own tab rather than run into the next one.
                    int tf = UiSurface.FitRole(this, UiSurface.TextRole.Body,
                                               new Vector2(r.Size.X * 0.86f, r.Size.Y * 0.62f),
                                               text, font);
                    text = KitChrome.EllipsizeText(font, text, tf, r.Size.X * 0.86f);
                    if (string.IsNullOrEmpty(text)) continue;
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
                    b = KitChrome.EllipsizeText(font, b, small, r.Size.X * 0.45f);
                    if (string.IsNullOrEmpty(b)) continue;
                    Vector2 m = font.GetStringSize(b, HorizontalAlignment.Left, -1, small);
                    float bw = Mathf.Max(m.X + small * 0.7f, small * 1.4f), bh = small * 1.2f;
                    // Straddle the corner, but stay inside the STRIP: at -bh*0.35 the badge was
                    // drawn above y=0 and got cut off by the control's own top edge, and a 0.6
                    // overhang pushed it into the next tab. Sit it just inside the top and
                    // overhang less, so it still reads as a corner flash without being clipped
                    // or colliding with its neighbour.
                    var br = new Rect2(r.End.X - bw * 0.78f,
                                       Mathf.Max(0f, r.Position.Y - bh * 0.12f), bw, bh);
                    KitChrome.DrawShape(this, _genre, br, KitShape.Pill, UiSurface.SemanticOrDerived(this, UiSurface.Role.Danger), ink, 1.5f);
                    KitChrome.DrawText(this, _genre, font, new Vector2(br.Position.X + (br.Size.X - m.X) * 0.5f, br.Position.Y + (br.Size.Y + m.Y * 0.6f) * 0.5f),
                               b, small, new Color(0.98f, 0.96f, 0.92f));
                }
            }

            KitChrome.DrawFocusRing(this, _genre, new Rect2(Vector2.Zero, Size), TabShape, 0.8f);
        }
    }
}
