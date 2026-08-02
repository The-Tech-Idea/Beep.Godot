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
        [Export] public UiSurface.Role FallbackRole { get; set; } = UiSurface.Role.Neutral;

        public override void _Ready() => ApplyFallback();

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged) ApplyFallback();
        }

        private void ApplyFallback()
        {
            if (Color.A > 0.02f) return;
            Color c = FallbackRole == UiSurface.Role.Neutral ? UiSurface.Of(this) : UiSurface.Semantic(this, FallbackRole);
            if (c.A > 0.02f) Color = c;
        }
    }
}
