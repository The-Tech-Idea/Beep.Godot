using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A game button: the genre's material stack cut to the genre's silhouette, with sculpted
    /// states and an optional badge that OVERHANGS its corner.
    ///
    /// IT IS A GODOT <see cref="Button"/>.
    /// ------------------------------------
    /// It used to derive from KitControl, on the reasoning that Button "owns its own
    /// StyleBox-per-state drawing, which is exactly the model the kit replaces". That reasoning
    /// was wrong twice over.
    ///
    /// First, the base draw is not a fight: blanking each state's StyleBox with a
    /// <see cref="StyleBoxEmpty"/> suppresses it entirely (<see cref="KitChrome.Suppress"/>), and
    /// KitPushButton had already proved that.
    ///
    /// Second, the cost of NOT being a Button is severe and silent. A Control that merely looks
    /// like a button has no <c>Pressed</c> from BaseButton, no <c>Text</c>, no <c>Disabled</c>, no
    /// <c>ToggleMode</c>, no <c>ButtonGroup</c> — and every <c>GetNode&lt;Button&gt;</c>,
    /// <c>is Button</c> and <c>btn.Pressed +=</c> in a project fails against it. That is exactly
    /// the CS1503 class of error this addon has already shipped once, and it is invisible in a
    /// .tscn: Godot happily attaches a Control-derived script to a Button node, leaving a managed
    /// Control standing in for a native Button.
    ///
    /// So: <c>Text</c>, <c>Icon</c>, <c>Disabled</c>, <c>Pressed</c> and the whole BaseButton API
    /// are now the REAL ones, inherited, not shadowed copies. `tools/check_script_node_types.py`
    /// enforces that a node carrying this script is declared `type="Button"`.
    ///
    /// Use <see cref="KitPushButton"/> for a plain converted button. Use this one when you want
    /// the badge that straddles the corner, which Button's own layout cannot express.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitButton : Button
    {
        /// <summary>Which palette role the plate takes. Accent by default — every reference sheet
        /// puts a saturated accent button on a neutral panel.</summary>
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Accent;

        /// <summary>Badge text, e.g. a cost. Empty = no badge. Drawn straddling the top-right
        /// corner, which containers cannot do — see <see cref="KitAttach"/>.</summary>
        [Export]
        public string BadgeText
        {
            get => _badge;
            set { _badge = value ?? ""; QueueRedraw(); }
        }
        private string _badge = "";

        [Export] public UiSurface.Role BadgeRole { get; set; } = UiSurface.Role.Warning;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Suppress();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                float h = Mathf.Clamp(fs * 2.15f, 28f, 40f);
                CustomMinimumSize = new Vector2(Mathf.Max(78f, h * 3.15f), h);
            }
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Suppress();
            QueueRedraw();
        }

        /// <summary>Blank every state's StyleBox so the base class paints nothing and _Draw owns
        /// the look. The re-entry guard matters: AddThemeStyleboxOverride emits
        /// NotificationThemeChanged, which lands straight back here.</summary>
        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;

            int fs = UiSurface.FontSize(this);
            float pad = Mathf.Max(6f, fs * 0.7f);
            float frame = Geo.FramePx(Mathf.Max(Size.Y, fs * 2.4f));
            // The badge's room is added to the PAD, not passed as vpad -- Suppress's fifth
            // parameter is the vertical padding, and feeding a horizontal inset into it squashed
            // the label instead of moving it clear of the badge.
            KitChrome.Suppress(this, new[] { "normal", "hover", "pressed", "disabled", "focus" },
                               frame, pad + BadgeInset());

            _suppressing = false;
        }

        /// <summary>How far the plate is pulled in to leave the badge room. Fed into the content
        /// margins too, so the LABEL moves with the plate instead of drifting off it.</summary>
        private float BadgeInset()
            => string.IsNullOrEmpty(_badge) ? 0f : UiSurface.FontSize(this, 0.8f) * 0.5f;

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

            Color plate = UiSurface.Semantic(this, Accent);
            if (plate.A < 0.02f) plate = UiSurface.Of(this);   // no semantic palette: stay usable
            Color face = KitChrome.StateFace(plate, state);
            int fs = UiSurface.FontSize(this);

            // The badge overhangs, so the plate is inset to leave it room rather than being
            // clipped by the control's own rect.
            float inset = BadgeInset();
            var body = new Rect2(inset, inset, Size.X - inset * 2f, Size.Y - inset * 2f);
            if (body.Size.X <= 2f || body.Size.Y <= 2f) return;

            // One shared band walk (KitChrome), not a second copy. The register stack is the
            // kit's definition of what a plate IS; two implementations of it drift.
            KitChrome.DrawPlate(this, _genre, body, face, state, fs / 14f, KitWidgetClass.Button);

            // The label LAST, and drawn by us. A script's _Draw runs AFTER the base class's, so
            // the plate above paints straight over the text Button already drew.
            DrawLabel(body, state);
            DrawBadge(state);
        }

        private void DrawLabel(Rect2 body, KitState state)
        {
            if (string.IsNullOrEmpty(Text)) return;
            var font = KitFonts.Resolve(Geo.Font) ?? GetThemeDefaultFont();
            if (font == null) return;

            string text = Geo.UpperCase ? Text.ToUpperInvariant() : Text;
            int fs = UiSurface.FitText(this, body.Size - new Vector2(UiSurface.FontSize(this) * 0.7f, 0f),
                                       0.46f, text, font, min: 8, themeMax: 1.0f);
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            // Pressed text shifts with the plate, so the label looks pushed in with it.
            float dy = state == KitState.Pressed ? 1f : 0f;
            var at = new Vector2(body.Position.X + (body.Size.X - m.X) * 0.5f,
                                 body.Position.Y + (body.Size.Y + m.Y * 0.62f) * 0.5f + dy);
            Color ink = UiSurface.Text(this);
            if (state is KitState.Disabled or KitState.Locked) ink = ink with { A = 0.45f };
            KitChrome.DrawText(this, _genre, font, at, text, fs, ink);
        }

        /// <summary>The badge, straddling the top-right corner. Drawn directly rather than through
        /// KitControl's attachment list, which this class no longer inherits — the geometry is the
        /// same <see cref="KitAttach"/> resolve, so the two stay in step.</summary>
        private void DrawBadge(KitState state)
        {
            if (string.IsNullOrEmpty(_badge) || state == KitState.Disabled) return;

            int bfs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            var attach = new KitAttach
            {
                Anchor = KitAnchor.TopRight,
                Size = new Vector2(bfs * 2.2f, bfs * 1.6f),
                Shape = KitShape.Pill,
                Role = BadgeRole,
                Text = _badge,
                Overhang = 0.5f,
            };
            Rect2 r = attach.Resolve(Size);
            Color fill = UiSurface.Semantic(this, BadgeRole);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);

            var poly = KitChrome.Poly(KitShape.Pill, r, Geo);
            if (poly.Length >= 3 && Geometry2D.TriangulatePolygon(poly).Length > 0)
            {
                DrawColoredPolygon(poly, fill);
                var closed = new Vector2[poly.Length + 1];
                poly.CopyTo(closed, 0);
                closed[^1] = poly[0];
                DrawPolyline(closed, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim * 0.5f));
            }

            var font = KitFonts.Resolve(Geo.Font) ?? GetThemeDefaultFont();
            if (font == null) return;
            bfs = UiSurface.FitText(this, r.Size * 0.82f, 0.62f, _badge, font, min: 7, themeMax: 0.85f);
            Vector2 m = font.GetStringSize(_badge, HorizontalAlignment.Left, -1, bfs);
            KitChrome.DrawText(this, _genre, font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                       _badge, bfs, UiSurface.Ink(fill));
        }
    }
}
