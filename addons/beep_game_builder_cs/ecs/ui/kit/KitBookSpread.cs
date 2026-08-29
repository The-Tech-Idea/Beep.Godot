using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A two-page book — CATALOGUE-FROM-ART.md F.2's `BookSpread`, the journal / codex / quest-log
    /// set piece in the rpg and survival families.
    ///
    /// The spine is the whole idea: two pages that meet at a shaded gutter read as ONE object a
    /// player has opened, where two panels side by side read as two panels. So this owns the
    /// spine shading and the page edges, and exposes <see cref="LeftRect"/> / <see cref="RightRect"/>
    /// for a screen to lay its content into — the same contract <see cref="KitPanel.ContentRect"/>
    /// offers, and for the same reason: a screen that re-derives the insets will drift from them.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitBookSpread : KitControl
    {
        /// <summary>A panel: takes the theme's panel corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

        [Export] public string LeftTitle { get => _lt; set { string next = value ?? ""; if (_lt == next) return; _lt = next; RefreshMinimumAndRedraw(); } }
        private string _lt = "Quests";

        [Export] public string RightTitle { get => _rt; set { string next = value ?? ""; if (_rt == next) return; _rt = next; RefreshMinimumAndRedraw(); } }
        private string _rt = "Rewards";

        [Export] public string[] LeftPageTitles { get => _leftPages; set { if (SetStringArray(ref _leftPages, value)) RefreshPageList(); } }
        private string[] _leftPages = { "Inventory", "Active Quests", "World Map" };

        [Export] public string[] RightPageTitles { get => _rightPages; set { if (SetStringArray(ref _rightPages, value)) RefreshPageList(); } }
        private string[] _rightPages = { "Equipment", "Rewards", "Notes" };

        [Export] public bool ShowPageCorners { get => _showPageCorners; set { if (_showPageCorners == value) return; _showPageCorners = value; RefreshVisualAndRedraw(); } }
        private bool _showPageCorners = true;

        /// <summary>Ribbon bookmark hanging over the top edge. Empty hides it.</summary>
        [Export] public bool ShowRibbon { get => _showRibbon; set { if (_showRibbon == value) return; _showRibbon = value; RefreshVisualAndRedraw(); } }
        private bool _showRibbon;

        [Export] public bool ShowCover { get => _showCover; set { if (_showCover == value) return; _showCover = value; RefreshVisualAndRedraw(); } }
        private bool _showCover = true;

        [Export] public bool ShowTabs { get => _showTabs; set { if (_showTabs == value) return; _showTabs = value; RefreshPageList(); } }
        private bool _showTabs = true;

        [Export] public string[] Tabs { get => _tabs; set { if (SetStringArray(ref _tabs, value)) RefreshPageList(); } }
        private string[] _tabs = { "Bag", "Quest", "Map" };

        [Export(PropertyHint.Range, "0,8,1")]
        public int SelectedTab
        {
            get => _selectedTab;
            set
            {
                int next = Mathf.Max(0, value);
                TurnTo(next);
            }
        }
        private int _selectedTab;
        private int _turnFrom;
        private int _turnTo;
        private float _turnTime = 1f;
        private const float TurnDuration = 0.28f;

        [Signal] public delegate void TabSelectedEventHandler(int index);
        [Signal] public delegate void PageTurnedEventHandler(int index);

        public override void _Ready()
        {
            base._Ready();
            ApplyInputDefaults(MouseFilterEnum.Stop, FocusModeEnum.All);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
            UpdateProcessing();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationVisibilityChanged)
                UpdateProcessing();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float width = Mathf.Clamp(fs * 24f + TabOutset * 0.78f, 300f, 340f);
            float height = Mathf.Max(fs * 14f, 190f);
            if (ShowTabs && _tabs.Length > 0)
            {
                float tabStack = _tabs.Length * TabHeight(fs) + Mathf.Max(0, _tabs.Length - 1) * 3f;
                height = Mathf.Max(height, tabStack + fs * 5f);
            }
            return new Vector2(width, height);
        }

        public void SetLeftPageTitles(string[]? titles) => LeftPageTitles = titles ?? System.Array.Empty<string>();

        public void AddLeftPageTitle(string title) => LeftPageTitles = WithAdded(_leftPages, title);

        public void ClearLeftPageTitles() => LeftPageTitles = System.Array.Empty<string>();

        public void SetRightPageTitles(string[]? titles) => RightPageTitles = titles ?? System.Array.Empty<string>();

        public void AddRightPageTitle(string title) => RightPageTitles = WithAdded(_rightPages, title);

        public void ClearRightPageTitles() => RightPageTitles = System.Array.Empty<string>();

        public void SetTabs(string[]? tabs) => Tabs = tabs ?? System.Array.Empty<string>();

        public void AddTab(string tab) => Tabs = WithAdded(_tabs, tab);

        public void ClearTabs() => Tabs = System.Array.Empty<string>();

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventKey key)
            {
                Vector2I dir = KitChrome.DirectionFromKey(key);
                if (dir.X <= -9999) { TurnTo(0); EmitSignal(SignalName.TabSelected, _selectedTab); AcceptEvent(); return; }
                if (dir.X >= 9999) { TurnTo(PageCount() - 1); EmitSignal(SignalName.TabSelected, _selectedTab); AcceptEvent(); return; }
                if (dir.X < 0) { TurnTo(_selectedTab - 1); EmitSignal(SignalName.TabSelected, _selectedTab); AcceptEvent(); return; }
                if (dir.X > 0) { TurnTo(_selectedTab + 1); EmitSignal(SignalName.TabSelected, _selectedTab); AcceptEvent(); return; }
                if (KitChrome.IsConfirmKey(key)) { EmitSignal(SignalName.TabSelected, _selectedTab); AcceptEvent(); return; }
            }

            if (!ShowTabs || _tabs.Length == 0) return;
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
                return;

            var (lp, rp) = PageRects();
            int pages = PageCount();
            for (int i = 0; i < Mathf.Min(_tabs.Length, pages); i++)
            {
                if (!TabRect(rp, i).HasPoint(mb.Position)) continue;
                GrabFocus();
                TurnTo(i);
                EmitSignal(SignalName.TabSelected, i);
                AcceptEvent();
                return;
            }

            if (ShowPageCorners && PrevRect(lp).HasPoint(mb.Position))
            {
                GrabFocus();
                TurnTo(_selectedTab - 1);
                AcceptEvent();
                return;
            }

            if (ShowPageCorners && NextRect(rp).HasPoint(mb.Position))
            {
                GrabFocus();
                TurnTo(_selectedTab + 1);
                AcceptEvent();
                return;
            }
        }

        public override void _Process(double delta)
        {
            if (_turnTime >= 1f)
            {
                UpdateProcessing();
                return;
            }

            _turnTime = Mathf.Min(1f, _turnTime + (float)delta / TurnDuration);
            QueueRedraw();
            if (_turnTime >= 1f)
            {
                UpdateProcessing();
                EmitSignal(SignalName.PageTurned, _selectedTab);
            }
        }

        private void TurnTo(int page)
        {
            int max = Mathf.Max(0, PageCount() - 1);
            int next = Mathf.Clamp(page, 0, max);
            if (next == _selectedTab && _turnTime >= 1f) return;
            _turnFrom = _selectedTab;
            _turnTo = next;
            _selectedTab = next;
            _turnTime = _turnFrom == _turnTo ? 1f : 0f;
            UpdateProcessing();
            RefreshVisualAndRedraw();
        }

        private void RefreshPageList()
        {
            int max = Mathf.Max(0, PageCount() - 1);
            int next = Mathf.Clamp(_selectedTab, 0, max);
            _turnFrom = Mathf.Clamp(_turnFrom, 0, max);
            _turnTo = Mathf.Clamp(_turnTo, 0, max);
            if (next != _selectedTab)
            {
                _selectedTab = next;
                _turnFrom = next;
                _turnTo = next;
                _turnTime = 1f;
                UpdateProcessing();
            }
            RefreshMinimumAndRedraw();
        }

        private void RefreshMinimumAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        private bool ShouldAnimate() => _turnTime < 1f;

        private void UpdateProcessing()
        {
            SetProcess(IsVisibleInTree() && ShouldAnimate());
        }

        private static bool SetStringArray(ref string[] target, string[]? value)
        {
            string[] next = NormalizeStrings(value);
            if (SameStrings(target, next)) return false;
            target = next;
            return true;
        }

        private static string[] NormalizeStrings(string[]? values)
        {
            if (values == null || values.Length == 0)
                return System.Array.Empty<string>();

            var next = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                next[i] = values[i] ?? "";
            return next;
        }

        private static bool SameStrings(string[] a, string[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if ((a[i] ?? "") != (b[i] ?? ""))
                    return false;
            return true;
        }

        private float TabOutset
            => ShowTabs && _tabs.Length > 0
                ? Mathf.Clamp(UiSurface.FontSize(this) * 4.9f, 62f, 90f)
                : 0f;

        private static float TabHeight(int fs) => Mathf.Clamp(fs * 1.45f, 18f, 28f);

        private int PageCount()
            => Mathf.Max(1, Mathf.Max(_tabs.Length, Mathf.Max(_leftPages.Length, _rightPages.Length)));

        private static string[] WithAdded(string[] values, string value)
        {
            var next = new string[values.Length + 1];
            System.Array.Copy(values, next, values.Length);
            next[^1] = value ?? "";
            return next;
        }

        private Rect2 BookBounds()
        {
            float right = TabOutset * 0.78f;
            return new Rect2(0f, 0f, Mathf.Max(40f, Size.X - right), Size.Y);
        }

        private float Gutter => Mathf.Max(6f, BookBounds().Size.X * 0.035f);

        private (Rect2 Left, Rect2 Right) PageRects()
        {
            Rect2 b = BookBounds();
            float inset = ShowCover ? Mathf.Clamp(b.Size.Y * 0.095f, 11f, 18f) : Mathf.Max(3f, b.Size.Y * 0.035f);
            float gut = Gutter;
            var lp = new Rect2(b.Position.X + inset, b.Position.Y + inset,
                               b.Size.X * 0.5f - gut - inset, b.Size.Y - inset * 2f);
            var rp = new Rect2(b.Position.X + b.Size.X * 0.5f + gut, b.Position.Y + inset,
                               b.Size.X * 0.5f - gut - inset, b.Size.Y - inset * 2f);
            return (lp, rp);
        }

        /// <summary>Content area of the left page.</summary>
        public Rect2 LeftRect()
        {
            var (lp, _) = PageRects();
            float pad = lp.Size.Y * 0.075f;
            float title = string.IsNullOrEmpty(PageLeftTitle(_selectedTab)) ? 0f : UiSurface.FontSize(this) * 2.0f;
            return new Rect2(lp.Position.X + pad, lp.Position.Y + pad + title,
                             lp.Size.X - pad * 2f, lp.Size.Y - pad * 2f - title);
        }

        /// <summary>Content area of the right page.</summary>
        public Rect2 RightRect()
        {
            var (_, rp) = PageRects();
            float pad = rp.Size.Y * 0.075f;
            float title = string.IsNullOrEmpty(PageRightTitle(_selectedTab)) ? 0f : UiSurface.FontSize(this) * 2.0f;
            return new Rect2(rp.Position.X + pad, rp.Position.Y + pad + title,
                             rp.Size.X - pad * 2f, rp.Size.Y - pad * 2f - title);
        }

        public override void _Draw()
        {
            if (Size.X < 60f || Size.Y < 40f) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = KitFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1.5f, g.Rim * (fs / 14f));

            Rect2 book = BookBounds();
            if (ShowCover)
                DrawCover(book, face, ink, rimPx);
            else
                DrawShape(book, ActiveShape, face, RimColor(), rimPx);

            float inset = Mathf.Max(3f, book.Size.Y * 0.035f);
            float gut = Gutter;
            // Pages take the raised plate shade — they sit ON the cover, not sunk into it.
            float ps = g.PlateShadeFor(KitElevation.Raised);
            var page = new Color(Mathf.Lerp(face.R, 1f, 0.72f) * ps,
                                 Mathf.Lerp(face.G, 1f, 0.70f) * ps,
                                 Mathf.Lerp(face.B, 1f, 0.62f) * ps, 1f);

            var (lp, rp) = PageRects();
            DrawShape(lp, ActiveShape, page, ink, Mathf.Max(1f, rimPx * 0.6f));
            DrawShape(rp, ActiveShape, page, ink, Mathf.Max(1f, rimPx * 0.6f));
            DrawPageBorder(lp, ink);
            DrawPageBorder(rp, ink);
            DrawPageLines(lp, page);
            DrawPageLines(rp, page);

            // The spine: a shaded gutter, darkest at the centre, which is what makes the two
            // pages read as one opened object.
            int bands = 6;
            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                float a = (1f - Mathf.Abs(t - 0.5f) * 2f) * 0.34f;
                float x = book.Position.X + book.Size.X * 0.5f - gut + (gut * 2f) * t;
                DrawLine(new Vector2(x, book.Position.Y + inset), new Vector2(x, book.End.Y - inset),
                         new Color(0, 0, 0, a), Mathf.Max(1.5f, gut * 0.4f));
            }

            if (ShowRibbon)
            {
                float rw = Mathf.Max(6f, book.Size.X * 0.022f);
                float rx = book.Position.X + book.Size.X * 0.72f;
                Color rc = UiSurface.Semantic(this, UiSurface.Role.Danger);
                DrawRect(new Rect2(rx, book.Position.Y - book.Size.Y * 0.05f, rw, book.Size.Y * 0.30f), rc);
                DrawColoredPolygon(new[]
                {
                    new Vector2(rx, book.Position.Y + book.Size.Y * 0.25f),
                    new Vector2(rx + rw, book.Position.Y + book.Size.Y * 0.25f),
                    new Vector2(rx + rw * 0.5f, book.Position.Y + book.Size.Y * 0.32f),
                }, rc);
            }

            if (font == null) return;
            void Title(string t, Rect2 p)
            {
                if (string.IsNullOrEmpty(t)) return;
                string text = KitCase(t);
                float textWidth = p.Size.X * 0.82f;
                int tf = UiSurface.FitRole(this, UiSurface.TextRole.Subtitle,
                                           new Vector2(textWidth, p.Size.Y * 0.12f),
                                           text, font, min: 9);
                text = KitChrome.EllipsizeText(font, text, tf, textWidth);
                if (string.IsNullOrEmpty(text)) return;
                Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, tf);
                DrawText(font, new Vector2(p.Position.X + (p.Size.X - m.X) * 0.5f, p.Position.Y + tf * 1.45f),
                           text, tf, new Color(0.16f, 0.13f, 0.10f));
            }

            if (ShowTabs)
                DrawTabs(lp, rp, page, ink);
            DrawTurnAnimation(lp, rp, page, ink);
            DrawPageControls(lp, rp, page, ink);
            int titlePage = VisiblePageIndex();
            Title(PageLeftTitle(titlePage), lp);
            Title(PageRightTitle(titlePage), rp);
            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), book, ActiveShape, 0.8f);
        }

        private void DrawCover(Rect2 book, Color face, Color ink, float rimPx)
        {
            Color cover = face;
            Color coverDark = new(cover.R * 0.58f, cover.G * 0.54f, cover.B * 0.50f, 1f);
            Color coverLight = new(Mathf.Lerp(cover.R, 1f, 0.12f),
                                   Mathf.Lerp(cover.G, 1f, 0.10f),
                                   Mathf.Lerp(cover.B, 1f, 0.08f), 1f);

            DrawShape(book, ActiveShape, coverDark, RimColor(), rimPx * 1.25f);

            float board = Mathf.Clamp(book.Size.Y * 0.075f, 9f, 16f);
            var top = new Rect2(book.Position.X + board, book.Position.Y + board,
                                book.Size.X - board * 2f, book.Size.Y - board * 2f);
            DrawShape(top, ActiveShape, cover, ink with { A = 0.72f }, Mathf.Max(1.5f, rimPx * 0.75f));

            Color exposed = new(Mathf.Lerp(cover.R, coverDark.R, 0.42f),
                                Mathf.Lerp(cover.G, coverDark.G, 0.42f),
                                Mathf.Lerp(cover.B, coverDark.B, 0.42f), 1f);
            DrawRect(new Rect2(book.Position.X + board * 0.45f, book.Position.Y + board * 1.15f,
                               board * 0.72f, book.Size.Y - board * 2.30f), exposed);
            DrawRect(new Rect2(book.End.X - board * 1.17f, book.Position.Y + board * 1.15f,
                               board * 0.72f, book.Size.Y - board * 2.30f), exposed);
            DrawLine(new Vector2(book.Position.X + board * 1.22f, book.Position.Y + board * 1.25f),
                     new Vector2(book.Position.X + board * 1.22f, book.End.Y - board * 1.25f),
                     coverLight with { A = 0.26f }, Mathf.Max(1f, board * 0.16f));
            DrawLine(new Vector2(book.End.X - board * 1.22f, book.Position.Y + board * 1.25f),
                     new Vector2(book.End.X - board * 1.22f, book.End.Y - board * 1.25f),
                     new Color(0, 0, 0, 0.25f), Mathf.Max(1f, board * 0.16f));

            DrawLine(new Vector2(top.Position.X + board, top.Position.Y + board * 0.8f),
                     new Vector2(top.End.X - board, top.Position.Y + board * 0.8f),
                     coverLight with { A = 0.32f }, Mathf.Max(1.2f, board * 0.28f));
            DrawLine(new Vector2(top.Position.X + board, top.End.Y - board * 0.8f),
                     new Vector2(top.End.X - board, top.End.Y - board * 0.8f),
                     new Color(0, 0, 0, 0.20f), Mathf.Max(1.2f, board * 0.25f));

            float hinge = Mathf.Clamp(book.Size.X * 0.025f, 7f, 14f);
            float cx = book.Position.X + book.Size.X * 0.5f;
            DrawRect(new Rect2(cx - hinge * 0.5f, top.Position.Y + board * 0.5f,
                               hinge, top.Size.Y - board), new Color(0, 0, 0, 0.18f));
            DrawLine(new Vector2(cx - hinge * 0.32f, top.Position.Y + board),
                     new Vector2(cx - hinge * 0.32f, top.End.Y - board),
                     coverLight with { A = 0.18f }, Mathf.Max(1f, hinge * 0.14f));
            DrawLine(new Vector2(cx + hinge * 0.32f, top.Position.Y + board),
                     new Vector2(cx + hinge * 0.32f, top.End.Y - board),
                     new Color(0, 0, 0, 0.22f), Mathf.Max(1f, hinge * 0.14f));
        }

        private int VisiblePageIndex()
            => _turnTime < 0.48f ? _turnFrom : _selectedTab;

        private string PageLeftTitle(int i)
        {
            if (_leftPages.Length > 0)
                return _leftPages[Mathf.Clamp(i, 0, _leftPages.Length - 1)] ?? "";
            if (_tabs.Length > 0)
                return _tabs[Mathf.Clamp(i, 0, _tabs.Length - 1)] ?? "";
            return _lt;
        }

        private string PageRightTitle(int i)
        {
            if (_rightPages.Length > 0)
                return _rightPages[Mathf.Clamp(i, 0, _rightPages.Length - 1)] ?? "";
            return _rt;
        }

        private void DrawPageBorder(Rect2 p, Color ink)
        {
            float inset = Mathf.Max(5f, p.Size.Y * 0.035f);
            var r = p.Grow(-inset);
            DrawRect(r, new Color(ink.R, ink.G, ink.B, 0.24f), false, Mathf.Max(1f, p.Size.Y * 0.006f));
        }

        private void DrawTabs(Rect2 lp, Rect2 rp, Color page, Color ink)
        {
            int count = Mathf.Min(_tabs.Length, PageCount());
            if (count == 0) return;
            Font? font = KitFont();
            for (int i = 0; i < count; i++)
            {
                var r = TabRect(rp, i);
                bool sel = i == Mathf.Min(_selectedTab, _tabs.Length - 1);
                Color fill = sel ? UiSurface.Semantic(this, UiSurface.Role.Warning)
                                 : new Color(page.R * 0.86f, page.G * 0.82f, page.B * 0.74f, 1f);
                DrawShape(r, KitShape.Round, fill, ink, Mathf.Max(1f, Geo.Rim * 0.45f));
                DrawLine(new Vector2(r.Position.X + 2f, r.Position.Y + r.Size.Y * 0.18f),
                         new Vector2(r.Position.X + 2f, r.End.Y - r.Size.Y * 0.18f),
                         new Color(ink.R, ink.G, ink.B, 0.34f), Mathf.Max(1f, r.Size.Y * 0.08f));
                if (font != null)
                {
                    string text = KitCase(_tabs[i] ?? "");
                    float textWidth = r.Size.X * 0.74f;
                    int tf = UiSurface.FitRole(this, UiSurface.TextRole.Small, new Vector2(textWidth, r.Size.Y * 0.74f), text, font, min: 7);
                    text = KitChrome.EllipsizeText(font, text, tf, textWidth);
                    if (string.IsNullOrEmpty(text)) continue;
                    Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, tf);
                    Color tc = UiSurface.Luminance(fill) > 0.52f ? new Color(0.14f, 0.10f, 0.07f) : new Color(0.98f, 0.95f, 0.88f);
                    DrawText(font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f,
                                               r.Position.Y + (r.Size.Y + m.Y * 0.60f) * 0.5f),
                             text, tf, tc);
                }
            }
        }

        private Rect2 TabRect(Rect2 rp, int i)
        {
            int fs = UiSurface.FontSize(this);
            float h = TabHeight(fs);
            float w = TabOutset;
            float overlap = Mathf.Clamp(fs * 0.85f, 10f, 16f);
            float x = BookBounds().End.X - overlap;
            float y = rp.Position.Y + rp.Size.Y * 0.14f;
            return new Rect2(x, y + i * (h + 3f), w, h);
        }

        private Rect2 PrevRect(Rect2 lp)
        {
            float s = Mathf.Clamp(lp.Size.Y * 0.16f, 18f, 30f);
            return new Rect2(lp.Position.X + lp.Size.X * 0.08f, lp.End.Y - s - lp.Size.Y * 0.06f, s, s);
        }

        private Rect2 NextRect(Rect2 rp)
        {
            float s = Mathf.Clamp(rp.Size.Y * 0.16f, 18f, 30f);
            return new Rect2(rp.End.X - s - rp.Size.X * 0.08f, rp.End.Y - s - rp.Size.Y * 0.06f, s, s);
        }

        private void DrawPageControls(Rect2 lp, Rect2 rp, Color page, Color ink)
        {
            if (!ShowPageCorners) return;
            Font? font = KitFont();
            int pages = PageCount();
            Color muted = new Color(ink.R, ink.G, ink.B, 0.38f);

            void Corner(Rect2 r, bool next, bool enabled)
            {
                Color fill = enabled
                    ? new Color(page.R * 0.88f, page.G * 0.84f, page.B * 0.76f, 1f)
                    : new Color(page.R * 0.78f, page.G * 0.76f, page.B * 0.72f, 0.75f);
                DrawShape(r, KitShape.Round, fill, enabled ? ink with { A = 0.55f } : muted, Mathf.Max(1f, r.Size.Y * 0.05f));
                Vector2 c = r.Position + r.Size * 0.5f;
                float w = Mathf.Max(1.5f, r.Size.Y * 0.08f);
                float a = r.Size.X * 0.22f;
                Color arrow = enabled ? ink : muted;
                if (next)
                {
                    DrawLine(c + new Vector2(-a * 0.45f, -a), c + new Vector2(a * 0.55f, 0), arrow, w);
                    DrawLine(c + new Vector2(a * 0.55f, 0), c + new Vector2(-a * 0.45f, a), arrow, w);
                }
                else
                {
                    DrawLine(c + new Vector2(a * 0.45f, -a), c + new Vector2(-a * 0.55f, 0), arrow, w);
                    DrawLine(c + new Vector2(-a * 0.55f, 0), c + new Vector2(a * 0.45f, a), arrow, w);
                }
            }

            Corner(PrevRect(lp), false, _selectedTab > 0);
            Corner(NextRect(rp), true, _selectedTab < pages - 1);

            if (font == null) return;
            string label = $"{_selectedTab + 1}/{pages}";
            float labelWidth = rp.Size.X * 0.28f;
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Small, new Vector2(labelWidth, rp.Size.Y * 0.10f), label, font, min: 7);
            label = KitChrome.EllipsizeText(font, label, fs, labelWidth);
            if (string.IsNullOrEmpty(label)) return;
            Vector2 m = font.GetStringSize(label, HorizontalAlignment.Left, -1, fs);
            Vector2 at = new Vector2(rp.Position.X + (rp.Size.X - m.X) * 0.5f, rp.End.Y - rp.Size.Y * 0.08f);
            DrawText(font, at, label, fs, muted);
        }

        private void DrawTurnAnimation(Rect2 lp, Rect2 rp, Color page, Color ink)
        {
            if (_turnTime >= 1f) return;
            float t = 1f - Mathf.Pow(1f - _turnTime, 3f);
            bool forward = _turnTo >= _turnFrom;
            bool firstHalf = t < 0.5f;
            float k = firstHalf ? t * 2f : (t - 0.5f) * 2f;

            Rect2 fold;
            float shadowX;
            if (forward)
            {
                if (firstHalf)
                {
                    float x = Mathf.Lerp(rp.Position.X, rp.End.X, k);
                    fold = new Rect2(x, rp.Position.Y, rp.End.X - x, rp.Size.Y);
                    shadowX = x;
                }
                else
                {
                    float x = Mathf.Lerp(lp.End.X, lp.Position.X, k);
                    fold = new Rect2(x, lp.Position.Y, lp.End.X - x, lp.Size.Y);
                    shadowX = fold.End.X;
                }
            }
            else
            {
                if (firstHalf)
                {
                    float w = Mathf.Lerp(lp.Size.X, 0f, k);
                    fold = new Rect2(lp.Position.X, lp.Position.Y, w, lp.Size.Y);
                    shadowX = fold.End.X;
                }
                else
                {
                    float w = Mathf.Lerp(0f, rp.Size.X, k);
                    fold = new Rect2(rp.Position.X, rp.Position.Y, w, rp.Size.Y);
                    shadowX = fold.Position.X;
                }
            }

            if (fold.Size.X <= 1f) return;
            Color shadow = new(0, 0, 0, Mathf.Lerp(0.30f, 0.04f, t));
            Color sheet = new(Mathf.Lerp(page.R, 1f, 0.22f), Mathf.Lerp(page.G, 1f, 0.18f),
                              Mathf.Lerp(page.B, 1f, 0.12f), Mathf.Lerp(0.96f, 0.22f, t));
            DrawShape(fold, ActiveShape, sheet, ink with { A = Mathf.Lerp(0.55f, 0.12f, t) }, Mathf.Max(1f, Geo.Rim * 0.35f));
            DrawLine(new Vector2(shadowX, fold.Position.Y + 4f),
                     new Vector2(shadowX, fold.End.Y - 4f),
                     shadow, Mathf.Max(2f, rp.Size.X * 0.025f));
        }

        private void DrawPageLines(Rect2 p, Color page)
        {
            Color line = new(page.R * 0.70f, page.G * 0.70f, page.B * 0.72f, 0.45f);
            float start = p.Position.Y + p.Size.Y * 0.28f;
            float step = Mathf.Max(8f, p.Size.Y * 0.095f);
            for (float y = start; y < p.End.Y - p.Size.Y * 0.12f; y += step)
                DrawLine(new Vector2(p.Position.X + p.Size.X * 0.16f, y),
                         new Vector2(p.End.X - p.Size.X * 0.14f, y), line, Mathf.Max(1f, p.Size.Y * 0.006f));
        }
    }
}
