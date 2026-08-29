using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A game button: the genre's material stack cut to the genre's silhouette, with sculpted
    /// states and an optional badge pinned into its corner.
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
    /// the contained badge/cost treatment, which Button's own layout cannot express.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitButton : Button
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        /// <summary>Which palette role the plate takes. Accent by default — every reference sheet
        /// puts a saturated accent button on a neutral panel.</summary>
        [Export]
        public UiSurface.Role Accent
        {
            get => _accent;
            set { if (_accent == value) return; _accent = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _accent = UiSurface.Role.Accent;

        /// <summary>Badge text, e.g. a cost. Empty = no badge. Drawn inside the top-right
        /// corner so ordinary Godot containers can place buttons without overlap.</summary>
        [Export]
        public string BadgeText
        {
            get => _badge;
            set
            {
                string next = value ?? "";
                if (_badge == next) return;
                _badge = next;
                if (IsInsideTree())
                {
                    Suppress();
                    UpdateMinimumSize();
                }
                RefreshVisualAndRedraw();
            }
        }
        private string _badge = "";

        [Export]
        public UiSurface.Role BadgeRole
        {
            get => _badgeRole;
            set { if (_badgeRole == value) return; _badgeRole = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _badgeRole = UiSurface.Role.Warning;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            Suppress();
            KitChrome.HookButtonChromeRedraw(this, RefreshVisualAndRedraw, ref _eventsHooked);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            float h = Mathf.Clamp(fs * 2.15f, 28f, 40f);
            float frame = Geo.FramePx(Mathf.Max(Size.Y, fs * 2.4f));
            float pad = Mathf.Max(6f, fs * 0.7f);
            float width = Mathf.Max(78f, h * 3.15f);

            Font? font = KitChrome.Font(this, _genre);
            if (font != null && !string.IsNullOrEmpty(Text))
            {
                string text = Geo.UpperCase ? Text.ToUpperInvariant() : Text;
                int textFs = UiSurface.FontSize(this, UiSurface.TextRole.Body);
                float textWidth = font.GetStringSize(text, HorizontalAlignment.Left, -1, textFs).X;
                width = Mathf.Max(width, frame * 2f + pad * 2f + textWidth + BadgeLabelReserve());
            }

            Vector2 badge = BadgeSize();
            if (badge.X > 0f)
            {
                width = Mathf.Max(width + badge.X * 0.45f, 78f + badge.X * 0.75f);
                h = Mathf.Max(h, badge.Y + Mathf.Max(4f, frame * 0.5f + pad * 0.35f));
            }

            Vector2 native = base._GetMinimumSize();
            return new Vector2(Mathf.Max(width, native.X), Mathf.Max(h, native.Y));
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            RefreshVisualAndRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        /// <summary>Blank every state's StyleBox so the base class paints nothing and _Draw owns
        /// the look. Suppression goes through KitChrome so unchanged overrides are not recreated
        /// during Godot's theme-change notifications.</summary>
        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;

            int fs = UiSurface.FontSize(this);
            float pad = Mathf.Max(6f, fs * 0.7f);
            float frame = Geo.FramePx(Mathf.Max(Size.Y, fs * 2.4f));
            float right = frame + pad + BadgeLabelReserve();
            float vertical = frame * 0.5f + pad * 0.4f;
            foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                KitChrome.SetEmptyStyleboxOverride(this, state, frame + pad, right, vertical, vertical);

            _suppressing = false;
        }

        /// <summary>How much label room the contained badge reserves on the right side.</summary>
        private float BadgeLabelReserve()
            => string.IsNullOrEmpty(_badge) ? 0f : BadgeSize().X * 0.68f;

        private Vector2 BadgeSize()
        {
            if (string.IsNullOrEmpty(_badge)) return Vector2.Zero;

            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            string badge = KitChrome.Case(_badge, _genre);
            Font? font = KitChrome.Font(this, _genre);
            float textWidth = font?.GetStringSize(badge, HorizontalAlignment.Left, -1, fs).X
                           ?? badge.Length * fs * 0.56f;
            float height = Mathf.Max(fs * 1.45f, 18f);
            float width = Mathf.Clamp(textWidth + fs * 0.95f, height, fs * 5.2f);
            return new Vector2(width, height);
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

            Color plate = UiSurface.SemanticOrDerived(this, Accent);
            if (plate.A < 0.02f) plate = UiSurface.Of(this);   // no semantic palette: stay usable
            Color face = KitChrome.StateFace(plate, state);
            int fs = UiSurface.FontSize(this);

            var body = new Rect2(Vector2.Zero, Size);
            if (body.Size.X <= 2f || body.Size.Y <= 2f) return;

            // One shared band walk (KitChrome), not a second copy. The register stack is the
            // kit's definition of what a plate IS; two implementations of it drift.
            KitChrome.DrawPlate(this, _genre, body, face, state, fs / 14f, KitWidgetClass.Button);

            // The label LAST, and drawn by us. A script's _Draw runs AFTER the base class's, so
            // the plate above paints straight over the text Button already drew.
            DrawLabel(body, state, face);
            DrawBadge(state);
            KitChrome.DrawFocusRing(this, _genre, body, KitChrome.Shape(_genre), 0.8f);
        }

        private void DrawLabel(Rect2 body, KitState state, Color face)
        {
            if (string.IsNullOrEmpty(Text)) return;
            var font = KitChrome.Font(this, _genre);
            if (font == null) return;

            float reserve = BadgeLabelReserve();
            body = new Rect2(body.Position, new Vector2(Mathf.Max(1f, body.Size.X - reserve), body.Size.Y));
            string text = Geo.UpperCase ? Text.ToUpperInvariant() : Text;
            float textWidth = Mathf.Max(1f, body.Size.X - UiSurface.FontSize(this) * 0.7f);
            int fs = UiSurface.FitText(this, body.Size - new Vector2(UiSurface.FontSize(this) * 0.7f, 0f),
                                       0.46f, text, font, min: 8, themeMax: 1.0f);
            text = KitChrome.EllipsizeText(font, text, fs, textWidth);
            if (string.IsNullOrEmpty(text)) return;
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            // Pressed text shifts with the plate, so the label looks pushed in with it.
            float dy = state == KitState.Pressed ? 1f : 0f;
            var at = new Vector2(body.Position.X + (body.Size.X - m.X) * 0.5f,
                                 body.Position.Y + (body.Size.Y + m.Y * 0.62f) * 0.5f + dy);
            Color ink = UiSurface.Ink(face);
            if (state is KitState.Disabled or KitState.Locked) ink = ink with { A = 0.45f };
            KitChrome.DrawText(this, _genre, font, at, text, fs, ink);
        }

        /// <summary>The badge, pinned inside the top-right corner so layout remains container-safe.</summary>
        private void DrawBadge(KitState state)
        {
            if (string.IsNullOrEmpty(_badge) || state == KitState.Disabled) return;

            Vector2 size = BadgeSize();
            if (size.X <= 1f || size.Y <= 1f) return;

            float edge = Mathf.Max(1f, Geo.Rim * 0.35f);
            Rect2 r = new(Size.X - size.X - edge, edge, size.X, size.Y);
            if (state == KitState.Pressed)
                r.Position += new Vector2(0f, 1f);
            Color fill = UiSurface.SemanticOrDerived(this, BadgeRole);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);

            var poly = KitChrome.Poly(KitShape.Pill, r, Geo);
            if (poly.Length >= 3)
            {
                DrawColoredPolygon(poly, fill);
                var closed = new Vector2[poly.Length + 1];
                poly.CopyTo(closed, 0);
                closed[^1] = poly[0];
                DrawPolyline(closed, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim * 0.5f));
            }

            var font = KitChrome.Font(this, _genre);
            if (font == null) return;
            string badge = KitChrome.Case(_badge, _genre);
            int bfs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            bfs = UiSurface.FitText(this, r.Size * 0.82f, 0.62f, badge, font, min: 7, themeMax: 0.85f);
            badge = KitChrome.EllipsizeText(font, badge, bfs, r.Size.X * 0.82f);
            if (string.IsNullOrEmpty(badge)) return;
            Vector2 m = font.GetStringSize(badge, HorizontalAlignment.Left, -1, bfs);
            KitChrome.DrawText(this, _genre, font, new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.6f) * 0.5f),
                       badge, bfs, UiSurface.Ink(fill));
        }
    }
}
