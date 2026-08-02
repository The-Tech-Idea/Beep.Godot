using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitSwitchVisual : Control
    {
        [Export] public bool IsOn { get => _isOn; set { _isOn = value; QueueRedraw(); } }
        [Export] public UiSurface.Role OnRole { get; set; } = UiSurface.Role.Success;

        private bool _isOn;
        private string _genre = "";

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            Color surface = UiSurface.Of(this);
            Color on = UiSurface.Semantic(this, OnRole);
            if (on.A < 0.02f) on = surface;
            Color trackCol = _isOn
                ? on
                : new Color(surface.R * 0.42f, surface.G * 0.40f, surface.B * 0.46f, 1f);

            var track = new Rect2(Vector2.Zero, Size);
            KitChrome.Fill(this, KitShape.Pill, track, KitGeometry.ForGenre(_genre),
                           trackCol, UiSurface.Ink(surface), Mathf.Max(1f, Size.Y * 0.09f));

            float kr = Size.Y * 0.38f;
            float kx = _isOn ? Size.X - kr - Size.Y * 0.14f : kr + Size.Y * 0.14f;
            var kc = new Vector2(kx, Size.Y * 0.5f);
            Color knobCol = UiSurface.Luminance(trackCol) < 0.62f
                ? new Color(0.96f, 0.96f, 0.94f, 1f)
                : new Color(surface.R * 0.30f, surface.G * 0.29f, surface.B * 0.33f, 1f);
            DrawCircle(kc, kr, knobCol);
            DrawArc(kc, kr, 0f, Mathf.Tau, 20, UiSurface.Ink(trackCol), Mathf.Max(1f, kr * 0.18f));
        }
    }
}
