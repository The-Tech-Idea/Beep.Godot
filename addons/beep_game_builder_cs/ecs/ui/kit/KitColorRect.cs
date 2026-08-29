using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Drop-in ColorRect for game UI scene backplates and fades.
    ///
    /// It preserves the authored ColorRect colour, but derives a fallback from the active skin so
    /// template backgrounds are not plain editor rectangles when a scene omits a colour.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitColorRect : ColorRect
    {
        [Export]
        public bool AutoFallback
        {
            get => _autoFallback;
            set
            {
                if (_autoFallback == value) return;
                _autoFallback = value;
                ApplyFallback();
            }
        }
        private bool _autoFallback = true;

        [Export]
        public UiSurface.Role FallbackRole
        {
            get => _fallbackRole;
            set
            {
                if (_fallbackRole == value) return;
                _fallbackRole = value;
                ApplyFallback();
            }
        }
        private UiSurface.Role _fallbackRole = UiSurface.Role.Neutral;
        private Color _lastFallback = new(0, 0, 0, 0);
        private bool _appliedFallback;

        public override void _Ready()
        {
            base._Ready();
            ApplyFallback();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged) ApplyFallback();
        }

        private void ApplyFallback()
        {
            if (!AutoFallback) return;
            if (Color.A > 0.02f && (!_appliedFallback || !SameColor(Color, _lastFallback))) return;
            Color c = FallbackRole == UiSurface.Role.Neutral ? UiSurface.Of(this) : UiSurface.SemanticOrDerived(this, FallbackRole);
            if (c.A <= 0.02f) return;
            if (_appliedFallback && SameColor(Color, c)) return;
            Color = c;
            _lastFallback = c;
            _appliedFallback = true;
        }

        private static bool SameColor(Color a, Color b)
            => Mathf.Abs(a.R - b.R) < 0.001f
            && Mathf.Abs(a.G - b.G) < 0.001f
            && Mathf.Abs(a.B - b.B) < 0.001f
            && Mathf.Abs(a.A - b.A) < 0.001f;
    }
}
