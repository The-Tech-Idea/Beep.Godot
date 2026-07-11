using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Anchors the parent Control to the OS safe-area rect on _Ready and whenever
    /// the viewport resizes. Use on HUD roots / mobile UIs so notches and rounded
    /// corners don't clip content.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SafeAreaComponent : UIComponent
    {
        [Export] public bool ApplyOnReady { get; set; } = true;
        [Export] public bool TrackResize { get; set; } = true;

        private Godot.Control? _control;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent() as Godot.Control;
            if (ApplyOnReady) Apply();
            if (TrackResize) GetViewport().SizeChanged += Apply;
        }

        public void Apply()
        {
            if (_control == null) return;
            // GetDisplaySafeArea() returns the usable rect in physical screen coords;
            // relative to the viewport's top-left it gives the inset for notches etc.
            Rect2I safe = DisplayServer.GetDisplaySafeArea();
            Vector2I view = (Vector2I)GetViewport().GetVisibleRect().Size;
            _control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            // No usable safe area (some platforms) → fill everything.
            if (safe.Size.X <= 0 || safe.Size.Y <= 0)
            {
                _control.OffsetLeft = _control.OffsetTop = _control.OffsetRight = _control.OffsetBottom = 0;
                return;
            }
            _control.OffsetLeft = safe.Position.X;
            _control.OffsetTop = safe.Position.Y;
            _control.OffsetRight = safe.End.X - view.X;
            _control.OffsetBottom = safe.End.Y - view.Y;
        }
    }
}
