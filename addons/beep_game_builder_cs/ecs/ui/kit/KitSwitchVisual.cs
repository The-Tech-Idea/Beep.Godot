using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitSwitchVisual : Control
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Export] public bool IsOn { get => _isOn; set { if (_isOn == value) return; _isOn = value; RefreshVisualAndRedraw(); } }
        [Export]
        public UiSurface.Role OnRole
        {
            get => _onRole;
            set { if (_onRole == value) return; _onRole = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _onRole = UiSurface.Role.Success;

        private bool _isOn;
        private string _genre = "";

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, MouseFilterEnum.Ignore);
            RefreshMinimumAndRedraw();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            RefreshMinimumAndRedraw();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(Mathf.Clamp(fs * 3.7f, 44f, 64f),
                               Mathf.Clamp(fs * 2.0f, 24f, 34f));
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

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            Color surface = UiSurface.Of(this);
            Color on = UiSurface.SemanticOrDerived(this, OnRole);
            if (on.A < 0.02f) on = surface;
            var track = new Rect2(Vector2.Zero, Size);
            Color trackCol = _isOn
                ? new Color(on.R * 0.54f + surface.R * 0.20f,
                            on.G * 0.54f + surface.G * 0.20f,
                            on.B * 0.54f + surface.B * 0.20f,
                            1f)
                : KitChrome.WellFace(surface);
            KitShape trackShape = KitChrome.Shape(_genre, KitWidgetClass.Bar);
            KitChrome.DrawShape(this, _genre, track, trackShape, trackCol, UiSurface.Ink(surface),
                                Mathf.Max(1f, Size.Y * 0.07f), KitWidgetClass.Bar);

            float inset = Mathf.Max(2f, Size.Y * 0.14f);
            float kh = Mathf.Max(8f, Size.Y - inset * 2f);
            float kw = Mathf.Max(kh * 1.16f, Size.X * 0.34f);
            float x0 = track.Position.X + inset;
            float x1 = track.End.X - inset - kw;
            var knob = new Rect2(new Vector2(_isOn ? x1 : x0, track.Position.Y + inset),
                                 new Vector2(kw, kh));
            Color knobFace = _isOn
                ? new Color(Mathf.Lerp(on.R, 1f, 0.12f),
                            Mathf.Lerp(on.G, 1f, 0.12f),
                            Mathf.Lerp(on.B, 1f, 0.12f),
                            1f)
                : new Color(surface.R * 0.92f, surface.G * 0.92f, surface.B * 0.95f, 1f);
            KitChrome.DrawShape(this, _genre, knob, KitShape.Pill, knobFace,
                                UiSurface.Ink(knobFace) with { A = 0.34f },
                                Mathf.Max(1f, Size.Y * 0.035f), KitWidgetClass.Bar);
            Color mark = UiSurface.Ink(knobFace) with { A = 0.58f };
            float mx = knob.Position.X + knob.Size.X * 0.5f;
            DrawLine(new Vector2(mx, knob.Position.Y + kh * 0.28f),
                     new Vector2(mx, knob.End.Y - kh * 0.28f), mark, Mathf.Max(1f, kh * 0.10f));
        }
    }
}
