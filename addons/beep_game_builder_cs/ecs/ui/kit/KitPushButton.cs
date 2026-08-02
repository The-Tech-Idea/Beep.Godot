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
        /// <summary>Which palette role this button's plate takes. Accent is the default because
        /// that is what every reference sheet does; set Success/Danger for a confirm or a
        /// destructive action, or Neutral to fall back to the panel surface for a quiet button.</summary>
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Accent;

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

            // A BUTTON TAKES THE ACCENT, NOT THE SURFACE.
            //
            // This is the most consistent thing in Example_Art and the kit had it backwards:
            // ui1's yellow Claim, rpgui's gold PLAY, store's green BUY, ui2's orange Select,
            // gameui4/5's red and green actions — every reference button is a SATURATED accent
            // plate sitting on a neutral panel. Drawing buttons in the surface tone made all ten
            // genres read as the same drab plate no matter what their palette said, which is
            // independent of silhouette and was the loudest difference from the reference sheets.
            //
            // The art pass's own settled rule says it: "the palette goes on ONE element, the
            // other stays neutral" (5 references). The panel is the neutral one; this is the one.
            Color plate = UiSurface.Semantic(this, Accent);
            if (plate.A < 0.02f) plate = UiSurface.Of(this);   // no semantic palette: stay usable
            Color face = StateFace(plate, state);
            Color ink = UiSurface.Ink(face);
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * (fs / 14f));
            var body = new Rect2(Vector2.Zero, Size);

            // One shared band walk (KitChrome), not a second copy. The register stack is
            // the kit's definition of what a plate IS; two implementations of it drift.
            KitChrome.DrawPlate(this, _genre, body, face, state, fs / 14f);

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
            var font = KitFonts.Resolve(Geo.Font) ?? GetThemeDefaultFont();
            if (font == null) return;

            string[] lines = Text.Split('\n');
            string longest = "";
            foreach (string line in lines)
                if (line.Length > longest.Length) longest = line;

            int fs = UiSurface.FitText(this,
                                       Size - new Vector2(UiSurface.FontSize(this) * 1.4f,
                                                          UiSurface.FontSize(this) * 0.35f),
                                       lines.Length > 1 ? 0.38f : 0.50f,
                                       longest, font, min: 8, themeMax: 1.08f);
            Color col = UiSurface.Text(this);
            if (state == KitState.Disabled) col = col with { A = 0.45f };
            // Pressed text shifts with the plate, so the label looks pushed in with it.
            float dy = state == KitState.Pressed ? 1f : 0f;

            float lh = fs * 1.15f;
            float top = (Size.Y - lh * lines.Length) * 0.5f + fs * 0.82f + dy;
            for (int i = 0; i < lines.Length; i++)
            {
                Vector2 m = font.GetStringSize(lines[i], HorizontalAlignment.Left, -1, fs);
                KitChrome.DrawText(this, _genre, font, new Vector2((Size.X - m.X) * 0.5f, top + lh * i),
                           lines[i], fs, col);
            }
        }

        /// <summary>State sculpt, shared with every other drop-in so a converted Button and a
        /// converted CheckButton respond to hover and disable identically.</summary>
        private static Color StateFace(Color s, KitState st) => KitChrome.StateFace(s, st);

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
