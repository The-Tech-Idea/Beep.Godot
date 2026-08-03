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
    /// cannot also inherit KitControl (C# has single inheritance), so it shares the kit's geometry
    /// through <see cref="KitControl.Outline"/>, <see cref="KitStacks"/> and
    /// <see cref="KitGeometry"/> rather than by subclassing.
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

        [Export] public string Title { get => _title; set { _title = value ?? ""; Refresh(); } }
        private string _title = "";

        /// <summary>Banner lightness as a multiple of the frame. 0.44 (gameui2) reads recessed;
        /// above 1 gives gameui4's white plate.</summary>
        [Export(PropertyHint.Range, "0.1,1.6,0.01")]
        public float BannerShade { get => _bannerShade; set { _bannerShade = value; Refresh(); } }
        private float _bannerShade = 0.44f;
        /// <summary>Title scale relative to the current UI font. City-builder/status panels use
        /// compact utility headers; RPG/dialog panels can leave the larger default.</summary>
        [Export(PropertyHint.Range, "0.45,1.4,0.01")]
        public float TitleFontScale { get => _titleFontScale; set { _titleFontScale = value; Refresh(); } }
        private float _titleFontScale = 1.0f;
        [Export] public HeaderStyle TitleStyle { get => _titleStyle; set { _titleStyle = value; Refresh(); } }
        private HeaderStyle _titleStyle = HeaderStyle.Banner;
        [Export] public KitPanelIntent Intent { get => _intent; set { _intent = value; Refresh(); } }
        private KitPanelIntent _intent = KitPanelIntent.Sheet;

        [Export] public bool ShowWell { get => _showWell; set { _showWell = value; Refresh(); } }
        private bool _showWell = true;

        /// <summary>Extra inset for children, on top of the frame. Use when content needs to sit
        /// further inside the well than the frame alone requires.</summary>
        [Export] public Vector2 ExtraPadding { get => _extraPadding; set { _extraPadding = value; Refresh(); } }
        private Vector2 _extraPadding = new(6, 6);

        private string _genre = "";
        private StyleBoxEmpty? _spacer;

        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private KitShape ActiveShape => KitMaterial.PanelShapeForGenre(_genre, Intent);

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Refresh();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
                Refresh();
            }
            else if (what == NotificationResized) Refresh();
        }

        /// <summary>Own the panel stylebox so it contributes LAYOUT but no paint: empty, with
        /// content margins equal to the frame the kit is about to draw.</summary>
        private bool _refreshing;

        private void Refresh()
        {
            // Re-entry guard. AddThemeStyleboxOverride below emits NotificationThemeChanged, which
            // calls straight back into Refresh -- unbounded recursion that crashed the scene on
            // load with a stack overflow inside InvokeGodotClassMethod. Anything that writes a
            // theme override from a theme-changed handler needs this.
            if (_refreshing) return;
            _refreshing = true;

            float h = Mathf.Max(Size.Y, 1f);
            float frame = FramePx(h);
            float banner = HeaderRoom();

            _spacer ??= new StyleBoxEmpty();
            _spacer.ContentMarginLeft = frame + ExtraPadding.X;
            _spacer.ContentMarginRight = frame + ExtraPadding.X;
            _spacer.ContentMarginTop = frame + ExtraPadding.Y + banner;
            _spacer.ContentMarginBottom = frame + ExtraPadding.Y;
            AddThemeStyleboxOverride("panel", _spacer);

            _refreshing = false;
            QueueRedraw();
        }

        private float HeaderRoom()
            => string.IsNullOrEmpty(_title)
                ? 0f
                : TitleStyle == HeaderStyle.None
                    ? 0f
                : TitleStyle == HeaderStyle.UtilityStrip
                    ? Mathf.Max(UiSurface.FontSize(this, TitleFontScale) * 1.65f, 18f)
                    : Mathf.Max(UiSurface.FontSize(this, TitleFontScale) * 1.5f, Size.Y * 0.14f) * 0.5f;

        private float BodyOverhang()
            => TitleStyle == HeaderStyle.Banner ? HeaderRoom() : 0f;

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

            // Walk the register's stack, exactly as KitControl does, so a panel and a button in
            // the same genre are built from the same bands.
            float frame = FramePx(body.Size.Y);
            Rect2 cur = body;
            foreach (var layer in KitStacks.For(g.Register))
            {
                if (layer.Kind != KitLayerKind.Plate && layer.Kind != KitLayerKind.Keyline) continue;
                float inset = layer.Inset >= 0f ? body.Size.Y * layer.Inset : frame;
                Rect2 box = (layer.Kind == KitLayerKind.Plate && layer.Inset == 0f)
                    ? body : Inset(cur, inset);
                if (box.Size.X < 2f || box.Size.Y < 2f) continue;

                Color c = Tint(face, layer.Shade);
                if (layer.Kind == KitLayerKind.Keyline)
                    Cut(box, ActiveShape, new Color(0, 0, 0, 0), c with { A = layer.Amount },
                        Mathf.Max(1f, rimPx * 0.5f));
                else
                {
                    Cut(box, ActiveShape, c, ink, layer.Rim > 0f ? Mathf.Max(1f, rimPx * layer.Rim) : 0f);
                    cur = box;
                }
            }

            if (ShowWell)
            {
                float ft = Mathf.Max(frame, Mathf.Min(body.Size.X, body.Size.Y) * 0.10f);
                var well = Inset(cur, ft * 0.35f);
                if (well.Size.X > 4 && well.Size.Y > 4)
                    Cut(well, ActiveShape, Tint(face, g.WellShade), ink, Mathf.Max(1f, rimPx * 0.5f));
            }

            DrawBanner(body, fs, face, ink);
        }

        private void DrawHudPanel(Color face, Color ink, int fs)
        {
            var body = new Rect2(Vector2.Zero, Size);
            float rimPx = Mathf.Clamp(Geo.Rim * 0.42f * (fs / 14f), 1f, 2.5f);
            Color panel = Tint(face, 0.82f) with { A = Mathf.Min(0.92f, face.A) };
            Cut(body, ActiveShape, panel, ink with { A = 0.54f }, rimPx);

            if (ShowWell)
            {
                float inset = Mathf.Max(2f, Mathf.Min(Size.X, Size.Y) * 0.045f);
                var well = Inset(body, inset);
                if (well.Size.X > 4f && well.Size.Y > 4f)
                    Cut(well, ActiveShape, Tint(face, Geo.WellShade) with { A = panel.A * 0.75f },
                        ink with { A = 0.24f }, 1f);
            }

            if (!string.IsNullOrEmpty(_title) && TitleStyle != HeaderStyle.None)
            {
                var font = GetThemeDefaultFont();
                if (font != null) DrawUtilityHeader(body, fs, face, ink, font);
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

        /// <summary>Fill + rim cut to a kit silhouette, sharing KitControl's outline table.</summary>
        private void Cut(Rect2 r, KitShape shape, Color fill, Color rim, float rimWidth)
        {
            if (r.Size.X < 1f || r.Size.Y < 1f) return;
            float cut = Mathf.Min(r.Size.X, r.Size.Y) * Geo.Corner;
            var poly = KitControl.Outline(shape, r, cut);
            if (poly != null)
            {
                if (fill.A > 0f) DrawColoredPolygon(poly, fill);
                if (rimWidth > 0f)
                {
                    var closed = new Vector2[poly.Length + 1];
                    poly.CopyTo(closed, 0);
                    closed[^1] = poly[0];
                    DrawPolyline(closed, rim, rimWidth);
                }
                return;
            }
            float radius = shape switch
            {
                KitShape.Rect => 0f,
                KitShape.Pill or KitShape.Ellipse => Mathf.Min(r.Size.X, r.Size.Y) * 0.5f,
                _ => cut,
            };
            if (fill.A > 0f)
            {
                var sb = new StyleBoxFlat { BgColor = fill };
                sb.SetCornerRadiusAll(Mathf.RoundToInt(radius));
                DrawStyleBox(sb, r);
            }
            if (rimWidth > 0f)
            {
                var sb = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = rim, DrawCenter = false };
                sb.SetCornerRadiusAll(Mathf.RoundToInt(radius));
                sb.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(rimWidth)));
                DrawStyleBox(sb, r);
            }
        }

        private void DrawBanner(Rect2 host, int fs, Color face, Color ink)
        {
            if (string.IsNullOrEmpty(_title)) return;
            var font = GetThemeDefaultFont();
            if (font == null) return;
            if (TitleStyle == HeaderStyle.None) return;
            if (TitleStyle == HeaderStyle.UtilityStrip)
            {
                DrawUtilityHeader(host, fs, face, ink, font);
                return;
            }

            float titleFs = UiSurface.FontSize(this, TitleFontScale);
            float h = Mathf.Max(titleFs * 1.5f, host.Size.Y * 0.14f);
            int tf = UiSurface.FitText(this, new Vector2(host.Size.X * 0.88f, h * 0.68f),
                                       0.72f, _title, font, min: 8, themeMax: TitleFontScale);
            float need = font.GetStringSize(_title, HorizontalAlignment.Left, -1, tf).X + tf * 2f;
            float w = Mathf.Max(host.Size.X * 0.62f, Mathf.Min(need, host.Size.X * 1.08f));
            var r = new Rect2(host.Position.X + (host.Size.X - w) * 0.5f,
                              host.Position.Y - h * 0.5f, w, h);

            KitShape shape = Geo.Register switch
            {
                KitRegister.Carved => KitShape.Ribbon,
                KitRegister.Casual => KitShape.Ellipse,
                _ => KitShape.Rect,
            };
            Color plate = Tint(face, BannerShade);
            Cut(r, shape, plate, ink, Mathf.Max(1f, Geo.Rim * 0.7f * (fs / 14f)));

            Vector2 m = font.GetStringSize(_title, HorizontalAlignment.Left, -1, tf);
            Color txt = UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f);
            KitChrome.DrawText(this, _genre, font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                       _title, tf, txt);
        }

        private void DrawUtilityHeader(Rect2 host, int fs, Color face, Color ink, Font font)
        {
            float titleFs = UiSurface.FontSize(this, TitleFontScale);
            float frame = Geo.FramePx(host.Size.Y);
            float h = Mathf.Max(titleFs * 1.45f, 17f);
            float padX = Mathf.Max(8f, fs * 0.55f);
            var r = new Rect2(host.Position.X + frame, host.Position.Y + frame,
                              Mathf.Max(4f, host.Size.X - frame * 2f), h);
            int tf = UiSurface.FitText(this, new Vector2(r.Size.X - padX * 2f, h * 0.72f),
                                       0.68f, _title, font, min: 8, themeMax: TitleFontScale);
            Color plate = Tint(face, Mathf.Max(0.48f, BannerShade));
            DrawRect(r, plate with { A = Mathf.Min(0.92f, plate.A) });
            DrawLine(new Vector2(r.Position.X, r.End.Y), new Vector2(r.End.X, r.End.Y),
                     ink with { A = 0.52f }, Mathf.Max(1f, Geo.Rim * 0.35f * (fs / 14f)));

            Vector2 m = font.GetStringSize(_title, HorizontalAlignment.Left, -1, tf);
            Color txt = UiSurface.Luminance(plate) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f);
            KitChrome.DrawText(this, _genre, font,
                new Vector2(r.Position.X + padX,
                            r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                _title, tf, txt);
        }
    }
}
