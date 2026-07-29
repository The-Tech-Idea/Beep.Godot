using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A Godot <see cref="Button"/> that draws the kit's chrome instead of a StyleBox.
    ///
    /// The migration drop-in, and the exact counterpart of <see cref="KitPanelContainer"/>: change
    /// nothing but the script and a generic Button becomes a game button. Because it IS a Button,
    /// every <c>Find&lt;Button&gt;</c>, <c>GetNode&lt;Button&gt;</c>, <c>is Button</c> and
    /// <c>btn.Pressed +=</c> in the codebase keeps working — all 48 typed lookups, untouched.
    ///
    /// WHY THIS EXISTS ALONGSIDE <see cref="KitButton"/>
    /// -------------------------------------------------
    /// KitButton derives from KitControl, which buys the full layer/attachment model but makes it
    /// NOT a Button — so swapping a scene onto it silently breaks every typed lookup and every
    /// `Pressed +=`, and each scene has to be repaired by hand. That cost is why 126 buttons sat
    /// unconverted across 35 files. PLAN.md rejected subclassing Button ("fighting the base
    /// class's draw"), but the base draw is trivially suppressed — see below — and the migration
    /// cost of NOT subclassing turned out to be far higher than the drawing cost of doing it.
    ///
    /// Use this to convert existing screens. Use KitButton when you want attachments that overhang
    /// the control (a cost badge straddling the corner), which Button's own layout cannot express.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitPushButton : Button
    {
        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private KitShape ActiveShape => KitMaterial.ShapeForGenre(_genre);

        private bool _suppressing;

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            SuppressBaseChrome();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
                SuppressBaseChrome();
                QueueRedraw();
            }
        }

        /// <summary>
        /// Blank every state's StyleBox so the base class paints nothing and _Draw owns the look.
        ///
        /// The content margins are kept, because Button sizes its own text from them — zeroing
        /// them collapses the button onto its label. The re-entry guard is required for the same
        /// reason KitPanelContainer needs one: AddThemeStyleboxOverride emits
        /// NotificationThemeChanged, which lands straight back here.
        /// </summary>
        private void SuppressBaseChrome()
        {
            if (_suppressing) return;
            _suppressing = true;

            int fs = UiSurface.FontSize(this);
            float pad = Mathf.Max(6f, fs * 0.7f);
            float frame = Geo.FramePx(Mathf.Max(Size.Y, fs * 2.4f));
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            {
                var sb = new StyleBoxEmpty();
                sb.ContentMarginLeft = frame + pad;
                sb.ContentMarginRight = frame + pad;
                sb.ContentMarginTop = frame * 0.5f + pad * 0.4f;
                sb.ContentMarginBottom = frame * 0.5f + pad * 0.4f;
                AddThemeStyleboxOverride(state, sb);
            }

            _suppressing = false;
        }

        private KitState CurrentState()
        {
            if (Disabled) return KitState.Disabled;
            if (ButtonPressed || IsPressed()) return KitState.Pressed;
            if (IsHovered()) return KitState.Hover;
            return KitState.Normal;
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;

            var g = Geo;
            KitState state = CurrentState();
            Color face = StateFace(UiSurface.Of(this), state);
            Color ink = UiSurface.Ink(face);
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * (fs / 14f));
            var body = new Rect2(Vector2.Zero, Size);

            // Walk the register's stack, so a converted Button and a KitButton in the same genre
            // are built from the same bands rather than two lookalike implementations.
            float frame = g.FramePx(body.Size.Y);
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
                    Cut(box, new Color(0, 0, 0, 0), c with { A = layer.Amount }, Mathf.Max(1f, rimPx * 0.5f));
                else
                {
                    Cut(box, c, ink, layer.Rim > 0f ? Mathf.Max(1f, rimPx * layer.Rim) : 0f);
                    cur = box;
                }
            }

            if (g.Studs > 0 && state != KitState.Disabled) Studs(body, g, ink);

            // The label LAST, and drawn by us. A script's _Draw runs AFTER the base class's, so
            // the plate above paints straight over the text Button already drew — every swept
            // button rendered as a blank plate until this was added. Re-drawing it here is the
            // price of owning the chrome on a Button subclass.
            DrawLabel(state);
        }

        /// <summary>Multi-line aware: several template buttons carry two lines ("Hammer\nx2",
        /// "5\n★★"), and drawing only the first would silently lose half of every one of them.</summary>
        private void DrawLabel(KitState state)
        {
            if (string.IsNullOrEmpty(Text)) return;
            var font = GetThemeDefaultFont();
            if (font == null) return;

            int fs = UiSurface.FontSize(this);
            Color col = UiSurface.Text(this);
            if (state == KitState.Disabled) col = col with { A = 0.45f };
            // Pressed text shifts with the plate, so the label looks pushed in with it.
            float dy = state == KitState.Pressed ? 1f : 0f;

            string[] lines = Text.Split('\n');
            float lh = fs * 1.15f;
            float top = (Size.Y - lh * lines.Length) * 0.5f + fs * 0.82f + dy;
            for (int i = 0; i < lines.Length; i++)
            {
                Vector2 m = font.GetStringSize(lines[i], HorizontalAlignment.Left, -1, fs);
                DrawString(font, new Vector2((Size.X - m.X) * 0.5f, top + lh * i),
                           lines[i], HorizontalAlignment.Left, -1, fs, col);
            }
        }

        private static Rect2 Inset(Rect2 r, float by)
            => new(r.Position + new Vector2(by, by), r.Size - new Vector2(by * 2f, by * 2f));

        /// <summary>State is a SCULPT, not an alpha change — fading a control is the clearest tell
        /// that a UI is a themed form rather than a game.</summary>
        private static Color StateFace(Color s, KitState st)
        {
            float k = st switch
            {
                KitState.Hover => 1.12f,
                KitState.Pressed => 0.84f,
                KitState.Disabled => 0.88f,
                _ => 1f,
            };
            var c = new Color(Mathf.Min(1f, s.R * k), Mathf.Min(1f, s.G * k), Mathf.Min(1f, s.B * k), s.A);
            if (st != KitState.Disabled) return c;
            // Disabled DRAINS SATURATION rather than dimming (the 7x settled rule).
            float l = UiSurface.Luminance(c);
            return new Color(Mathf.Lerp(c.R, l, 0.9f), Mathf.Lerp(c.G, l, 0.9f), Mathf.Lerp(c.B, l, 0.9f), c.A);
        }

        private static Color Tint(Color face, float shade)
        {
            if (shade <= 1f) return new Color(face.R * shade, face.G * shade, face.B * shade, face.A);
            float lum = UiSurface.Luminance(face);
            float want = Mathf.Min(1f, lum * shade);
            float t = Mathf.Clamp((want - lum) / Mathf.Max(0.001f, 1f - lum), 0f, 1f);
            return new Color(Mathf.Lerp(face.R, 1f, t), Mathf.Lerp(face.G, 1f, t),
                             Mathf.Lerp(face.B, 1f, t), face.A);
        }

        private void Cut(Rect2 r, Color fill, Color rim, float rimWidth)
        {
            if (r.Size.X < 1f || r.Size.Y < 1f) return;
            KitShape shape = ActiveShape;
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

        private void Studs(Rect2 r, KitGeometry g, Color ink)
        {
            float sr = Mathf.Max(1.5f, r.Size.Y * 0.06f);
            float off = Mathf.Max(sr * 1.8f, g.FramePx(r.Size.Y) * 0.55f);
            foreach (var c in new[]
            {
                r.Position + new Vector2(off, off),
                r.Position + new Vector2(r.Size.X - off, off),
                r.Position + new Vector2(off, r.Size.Y - off),
                r.Position + new Vector2(r.Size.X - off, r.Size.Y - off),
            })
            {
                DrawCircle(c, sr, new Color(1, 1, 1, 0.30f));
                DrawArc(c, sr, 0, Mathf.Tau, 12, ink, Mathf.Max(1f, sr * 0.35f));
            }
        }
    }
}
