using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitBuildTile : Button
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Export]
        public UiSurface.Role Accent
        {
            get => _accent;
            set { if (_accent == value) return; _accent = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _accent = UiSurface.Role.Neutral;

        [Export]
        public Texture2D? TileIcon
        {
            get => _tileIcon;
            set { if (_tileIcon == value) return; _tileIcon = value; RefreshVisualAndRedraw(); }
        }
        private Texture2D? _tileIcon;

        [Export]
        public Vector2 FixedSize
        {
            get => _fixedSize;
            set
            {
                Vector2 next = new(Mathf.Max(0f, value.X), Mathf.Max(0f, value.Y));
                if (_fixedSize == next) return;
                Vector2 previous = _fixedSize;
                _fixedSize = next;
                ApplyFixedSize(previous);
            }
        }
        private Vector2 _fixedSize = Vector2.Zero;

        [Export]
        public string Caption
        {
            get => _caption;
            set
            {
                string next = value ?? "";
                if (_caption == next) return;
                _caption = next;
                RefreshMinimumAndRedraw();
            }
        }

        [Export]
        public string CostText
        {
            get => _costText;
            set
            {
                string next = value ?? "";
                if (_costText == next) return;
                _costText = next;
                RefreshMinimumAndRedraw();
            }
        }

        [Export]
        public string OwnedText
        {
            get => _ownedText;
            set
            {
                string next = value ?? "";
                if (_ownedText == next) return;
                _ownedText = next;
                RefreshMinimumAndRedraw();
            }
        }

        private string _caption = "";
        private string _costText = "";
        private string _ownedText = "";
        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            Text = "";
            ApplyFixedSize(Vector2.Zero);
            Suppress();
            KitChrome.HookButtonChromeRedraw(this, RefreshVisualAndRedraw, ref _eventsHooked);
        }

        public override Vector2 _GetMinimumSize()
        {
            if (FixedSize != Vector2.Zero)
                return FixedSize;

            int fs = UiSurface.FontSize(this);
            Vector2 min = new(Mathf.Max(84f, fs * 6.5f), Mathf.Max(96f, fs * 7.25f));

            if (KitChrome.Font(this, _genre) is { } font)
            {
                float textWidth = 0f;
                foreach (string raw in new[] { _caption, _costText, _ownedText })
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    string text = KitChrome.Case(raw, _genre);
                    int textFs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
                    textWidth = Mathf.Max(textWidth, font.GetStringSize(text, HorizontalAlignment.Left, -1, textFs).X);
                }
                min.X = Mathf.Max(min.X, textWidth + fs * 1.8f);
            }

            Vector2 native = base._GetMinimumSize();
            return new Vector2(Mathf.Max(min.X, native.X), Mathf.Max(min.Y, native.Y));
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            if (FixedSize == Vector2.Zero)
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            RefreshVisualAndRedraw();
        }

        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            float fs = UiSurface.FontSize(this);
            KitChrome.Suppress(this, new[] { "normal", "hover", "pressed", "disabled", "focus" },
                               Geo.FramePx(Mathf.Max(Size.Y, fs * 4f)), fs * 0.35f, fs * 0.25f);
            _suppressing = false;
        }

        private void ApplyFixedSize(Vector2 previous)
        {
            if (_fixedSize != Vector2.Zero)
            {
                CustomMinimumSize = _fixedSize;
            }
            else
            {
                if (CustomMinimumSize == previous)
                    CustomMinimumSize = Vector2.Zero;
                if (IsInsideTree())
                    KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            }
            if (IsInsideTree())
                Suppress();
            UpdateMinimumSize();
            RefreshVisualAndRedraw();
        }

        private void RefreshMinimumAndRedraw()
        {
            if (IsInsideTree() && FixedSize == Vector2.Zero)
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
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

            KitState state = CurrentState();
            Color plate = UiSurface.SemanticOrDerived(this, Accent);
            if (plate.A < 0.02f) plate = UiSurface.Of(this);
            plate = KitChrome.StateFace(plate, state);
            int fs = UiSurface.FontSize(this);
            var body = new Rect2(Vector2.Zero, Size);
            KitChrome.DrawPlate(this, _genre, body, plate, state, fs / 14f);
            KitChrome.DrawFocusRing(this, _genre, body, KitChrome.Shape(_genre), 0.8f);

            var font = KitChrome.Font(this, _genre);
            if (font == null) return;

            float pad = Mathf.Max(4f, fs * 0.35f);
            float iconH = Size.Y * 0.42f;
            var iconRect = new Rect2(pad, pad, Size.X - pad * 2f, iconH);
            if (TileIcon != null)
                DrawTextureRect(TileIcon, iconRect, false, new Color(1, 1, 1, Disabled ? 0.42f : 1f));
            else
                DrawGlyph(font, iconRect, string.IsNullOrEmpty(_caption) ? "?" : _caption[..1]);

            Color ink = UiSurface.Text(this);
            if (Disabled) ink = ink with { A = 0.45f };
            DrawLine(font, _caption, new Rect2(pad, Size.Y * 0.55f, Size.X - pad * 2f, fs * 1.25f),
                     UiSurface.TextRole.Caption, ink);
            DrawLine(font, _costText, new Rect2(pad, Size.Y * 0.76f, Size.X - pad * 2f, fs * 1.05f),
                     UiSurface.TextRole.Small, ink with { A = Disabled ? 0.42f : 0.78f });

            if (!string.IsNullOrEmpty(_ownedText))
                DrawBadge(font);
        }

        private void DrawGlyph(Font font, Rect2 r, string glyph)
        {
            int fs = UiSurface.FitText(this, r.Size, 0.62f, glyph, font, min: 8, themeMax: 1.8f);
            Vector2 m = font.GetStringSize(glyph, HorizontalAlignment.Left, -1, fs);
            KitChrome.DrawText(this, _genre, font,
                new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                glyph, fs, UiSurface.Text(this) with { A = Disabled ? 0.35f : 0.88f });
        }

        private void DrawLine(Font font, string text, Rect2 r, UiSurface.TextRole role, Color ink)
        {
            if (string.IsNullOrEmpty(text)) return;
            string draw = KitChrome.Case(text, _genre);
            int fs = UiSurface.FitRole(this, role, r.Size, draw, font);
            draw = KitChrome.EllipsizeText(font, draw, fs, r.Size.X);
            if (string.IsNullOrEmpty(draw)) return;
            Vector2 m = font.GetStringSize(draw, HorizontalAlignment.Left, -1, fs);
            KitChrome.DrawText(this, _genre, font,
                new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                draw, fs, ink);
        }

        private void DrawBadge(Font font)
        {
            float fs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            var r = new Rect2(Size.X - fs * 2.25f, fs * 0.25f, fs * 1.9f, fs * 1.35f);
            Color fill = UiSurface.SemanticOrDerived(this, UiSurface.Role.Success);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);
            KitChrome.DrawShape(this, _genre, r, KitShape.Pill, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim * 0.45f));
            string owned = KitChrome.Case(_ownedText, _genre);
            float textWidth = Mathf.Max(1f, r.Size.X * 0.78f);
            int bfs = UiSurface.FitText(this, new Vector2(textWidth, r.Size.Y * 0.78f), 0.58f, owned, font, min: 7, themeMax: 0.85f);
            owned = KitChrome.EllipsizeText(font, owned, bfs, textWidth);
            if (string.IsNullOrEmpty(owned)) return;
            Vector2 m = font.GetStringSize(owned, HorizontalAlignment.Left, -1, bfs);
            KitChrome.DrawText(this, _genre, font,
                new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                owned, bfs, UiSurface.Ink(fill));
        }
    }
}
