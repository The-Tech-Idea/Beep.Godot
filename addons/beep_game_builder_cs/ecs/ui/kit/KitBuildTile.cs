using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitBuildTile : Button
    {
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Neutral;
        [Export] public Texture2D? TileIcon { get; set; }

        [Export]
        public string Caption
        {
            get => _caption;
            set { _caption = value ?? ""; QueueRedraw(); }
        }

        [Export]
        public string CostText
        {
            get => _costText;
            set { _costText = value ?? ""; QueueRedraw(); }
        }

        [Export]
        public string OwnedText
        {
            get => _ownedText;
            set { _ownedText = value ?? ""; QueueRedraw(); }
        }

        private string _caption = "";
        private string _costText = "";
        private string _ownedText = "";
        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Text = "";
            Suppress();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Suppress();
            QueueRedraw();
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
            Color plate = UiSurface.Semantic(this, Accent);
            if (plate.A < 0.02f) plate = UiSurface.Of(this);
            plate = KitChrome.StateFace(plate, state);
            int fs = UiSurface.FontSize(this);
            var body = new Rect2(Vector2.Zero, Size);
            KitChrome.DrawPlate(this, _genre, body, plate, state, fs / 14f);

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
            Vector2 m = font.GetStringSize(draw, HorizontalAlignment.Left, -1, fs);
            KitChrome.DrawText(this, _genre, font,
                new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                draw, fs, ink);
        }

        private void DrawBadge(Font font)
        {
            float fs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            var r = new Rect2(Size.X - fs * 2.25f, fs * 0.25f, fs * 1.9f, fs * 1.35f);
            Color fill = UiSurface.Semantic(this, UiSurface.Role.Success);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);
            KitChrome.DrawShape(this, _genre, r, KitShape.Pill, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim * 0.45f));
            int bfs = UiSurface.FitText(this, r.Size * 0.78f, 0.58f, _ownedText, font, min: 7, themeMax: 0.85f);
            Vector2 m = font.GetStringSize(_ownedText, HorizontalAlignment.Left, -1, bfs);
            KitChrome.DrawText(this, _genre, font,
                new Vector2(r.Position.X + (r.Size.X - m.X) * 0.5f, r.Position.Y + (r.Size.Y + m.Y * 0.62f) * 0.5f),
                _ownedText, bfs, UiSurface.Ink(fill));
        }
    }
}
