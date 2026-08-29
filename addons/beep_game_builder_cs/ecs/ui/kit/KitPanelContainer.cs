using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A PanelContainer that draws the kit's chrome instead of a StyleBox.
    ///
    /// This is the drop-in replacement for a bare <c>PanelContainer</c>: change the type and add
    /// this script, and the panel keeps laying out its children exactly as before while rendering
    /// a real game frame. Nothing reparents, and every <c>GetNode&lt;PanelContainer&gt;</c> or
    /// <c>is PanelContainer</c> lookup keeps working — which matters, because a kit widget that is
    /// NOT the Godot type it replaces silently breaks those, as KitButton did to ConnectButton.
    ///
    /// WHY IT DERIVES FROM PanelContainer RATHER THAN KitControl
    /// --------------------------------------------------------
    /// PanelContainer is a CONTAINER: it sets its children's rect every layout pass. KitPanel is a
    /// plain Control and lays out nothing, so swapping one for the other collapses every child to
    /// its minimum size at the origin — invisible in the scene file, obvious on screen, across
    /// 121 panels. Inheriting the container is what makes replacement safe. The cost is that this
    /// cannot also inherit KitControl (C# has single inheritance), so it shares the kit's chrome
    /// through <see cref="KitChrome"/> rather than by subclassing.
    ///
    /// CONTENT MARGINS ARE THE SUBTLE PART
    /// -----------------------------------
    /// A PanelContainer insets its children by its panel StyleBox's CONTENT MARGINS. Blanking the
    /// stylebox sets those to zero, so the kit frame would draw straight over the content. This
    /// therefore installs a StyleBoxEmpty whose content margins are driven by the kit's own frame
    /// thickness (plus banner room), so the container insets children by exactly the amount the
    /// frame occupies. PanelFrameComponent needed a whole ContentMarginPath export to solve this
    /// from outside; owning the stylebox solves it from inside.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitPanelContainer : PanelContainer
    {
        public enum HeaderStyle
        {
            Banner,
            UtilityStrip,
            None,
        }

        [Export] public string Title { get => _title; set { string next = value ?? ""; if (_title == next) return; _title = next; Refresh(); } }
        private string _title = "";

        /// <summary>Banner lightness as a multiple of the frame. 0.44 (gameui2) reads recessed;
        /// above 1 gives gameui4's white plate.</summary>
        [Export(PropertyHint.Range, "0.1,1.6,0.01")]
        public float BannerShade { get => _bannerShade; set { if (Mathf.Abs(_bannerShade - value) < 0.001f) return; _bannerShade = value; Refresh(); } }
        private float _bannerShade = 0.44f;
        /// <summary>Title scale relative to the current UI font. City-builder/status panels use
        /// compact utility headers; RPG/dialog panels can leave the larger default.</summary>
        [Export(PropertyHint.Range, "0.45,1.4,0.01")]
        public float TitleFontScale { get => _titleFontScale; set { if (Mathf.Abs(_titleFontScale - value) < 0.001f) return; _titleFontScale = value; Refresh(); } }
        private float _titleFontScale = 0.90f;
        [Export] public HeaderStyle TitleStyle { get => _titleStyle; set { if (_titleStyle == value) return; _titleStyle = value; Refresh(); } }
        private HeaderStyle _titleStyle = HeaderStyle.UtilityStrip;
        [Export] public KitPanelIntent Intent { get => _intent; set { if (_intent == value) return; _intent = value; Refresh(); } }
        private KitPanelIntent _intent = KitPanelIntent.Sheet;

        [Export] public bool ShowWell { get => _showWell; set { if (_showWell == value) return; _showWell = value; Refresh(); } }
        private bool _showWell = true;

        /// <summary>Extra inset for children, on top of the frame. Use when content needs to sit
        /// further inside the well than the frame alone requires.</summary>
        [Export] public Vector2 ExtraPadding { get => _extraPadding; set { if (_extraPadding == value) return; _extraPadding = value; Refresh(); } }
        // Was (6,6) ON TOP of the frame thickness, which on a small container doubled the inset
        // and left the content squeezed into the middle. The frame already provides the visual
        // breathing room; this is only the gap between frame and content.
        private Vector2 _extraPadding = new(2, 2);

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private KitShape ActiveShape => KitMaterial.PanelShapeForGenre(_genre, Intent);

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            Refresh();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = KitChrome.GenreOf(this);
                Refresh();
            }
            else if (what == NotificationResized) Refresh();
        }

        /// <summary>Own the panel stylebox so it contributes LAYOUT but no paint: empty, with
        /// content margins equal to the frame the kit is about to draw.</summary>
        private bool _refreshing;

        private void Refresh()
        {
            if (_refreshing) return;
            _refreshing = true;

            float h = Mathf.Max(Size.Y, 1f);
            float frame = FramePx(h);
            float banner = HeaderRoom();

            bool marginsChanged = KitChrome.SetEmptyStyleboxOverride(
                this,
                "panel",
                frame + ExtraPadding.X,
                frame + ExtraPadding.X,
                frame + ExtraPadding.Y + banner,
                frame + ExtraPadding.Y);

            _refreshing = false;
            if (marginsChanged)
                UpdateMinimumSize();
            QueueRedraw();
        }

        private float HeaderRoom()
            => KitChrome.PanelHeaderRoom(this, _genre, _title, SharedHeaderStyle(),
                                         TitleFontScale, Size.Y);

        private float BodyOverhang()
            => KitChrome.PanelHeaderOverhang(this, _genre, _title, SharedHeaderStyle(),
                                             TitleFontScale, Size.Y);

        private KitPanelHeaderStyle SharedHeaderStyle() => TitleStyle switch
        {
            HeaderStyle.UtilityStrip => KitPanelHeaderStyle.UtilityStrip,
            HeaderStyle.None => KitPanelHeaderStyle.None,
            _ => KitPanelHeaderStyle.Banner,
        };

        private float FramePx(float height)
        {
            float f = Geo.FramePx(height);
            return Intent == KitPanelIntent.Hud ? Mathf.Clamp(f * 0.42f, 1f, 3f) : f;
        }

        public override void _Draw()
        {
            if (Size.X <= 8f || Size.Y <= 8f) return;

            var g = Geo;
            Color face = UiSurface.Of(this);
            Color ink = UiSurface.Ink(face);
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * (fs / 14f));

            if (Intent == KitPanelIntent.Hud)
            {
                DrawHudPanel(face, ink, fs);
                return;
            }

            // The body is inset from the top by the banner's overhang, so the banner straddles the
            // FRAME's edge while the whole widget stays inside its own rect.
            float over = BodyOverhang();
            var body = new Rect2(0f, over, Size.X, Mathf.Max(4f, Size.Y - over));

            KitChrome.DrawPlate(this, _genre, body, face, KitState.Normal, fs / 14f,
                                KitWidgetClass.Panel);

            if (ShowWell)
            {
                float ft = Mathf.Max(g.FramePx(body.Size.Y) * 0.55f,
                                     Mathf.Min(body.Size.X, body.Size.Y) * 0.05f);
                var well = Inset(body, ft);
                if (well.Size.X > 4 && well.Size.Y > 4)
                    KitChrome.DrawShape(this, _genre, well, ActiveShape, Tint(face, g.WellShade),
                                        ink, Mathf.Max(1f, rimPx * 0.5f));
            }

            DrawBanner(body, fs, face, ink);
        }

        private void DrawHudPanel(Color face, Color ink, int fs)
        {
            var body = new Rect2(Vector2.Zero, Size);
            float rimPx = Mathf.Clamp(Geo.Rim * 0.42f * (fs / 14f), 1f, 2.5f);
            Color panel = Tint(face, 0.82f) with { A = Mathf.Min(0.92f, face.A) };
            KitChrome.DrawShape(this, _genre, body, ActiveShape, panel, ink with { A = 0.54f },
                                rimPx);

            if (ShowWell)
            {
                float inset = Mathf.Max(2f, Mathf.Min(Size.X, Size.Y) * 0.045f);
                var well = Inset(body, inset);
                if (well.Size.X > 4f && well.Size.Y > 4f)
                    KitChrome.DrawShape(this, _genre, well, ActiveShape,
                                        Tint(face, Geo.WellShade) with { A = panel.A * 0.75f },
                                        ink with { A = 0.24f }, 1f);
            }

            if (!string.IsNullOrEmpty(_title) && TitleStyle != HeaderStyle.None)
            {
                KitChrome.DrawPanelHeader(this, _genre, body, _title, KitPanelHeaderStyle.UtilityStrip,
                                          KitShape.Rect, BannerShade, TitleFontScale);
            }
        }

        private static Rect2 Inset(Rect2 r, float by)
            => new(r.Position + new Vector2(by, by), r.Size - new Vector2(by * 2f, by * 2f));

        /// <summary>Shade may exceed 1.0 (the measured carved rim is 2.05x), so brightening lifts
        /// toward white rather than clipping each channel, which would shift hue.</summary>
        private static Color Tint(Color face, float shade)
        {
            if (shade <= 1f) return new Color(face.R * shade, face.G * shade, face.B * shade, face.A);
            float lum = UiSurface.Luminance(face);
            float want = Mathf.Min(1f, lum * shade);
            float t = Mathf.Clamp((want - lum) / Mathf.Max(0.001f, 1f - lum), 0f, 1f);
            return new Color(Mathf.Lerp(face.R, 1f, t), Mathf.Lerp(face.G, 1f, t),
                             Mathf.Lerp(face.B, 1f, t), face.A);
        }

        private void DrawBanner(Rect2 host, int fs, Color face, Color ink)
        {
            if (TitleStyle == HeaderStyle.None) return;
            KitChrome.DrawPanelHeader(this, _genre, host, _title, SharedHeaderStyle(),
                                      KitChrome.PanelHeaderShape(_genre), BannerShade,
                                      TitleFontScale);
        }
    }
}
