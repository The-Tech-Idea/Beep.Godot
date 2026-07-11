using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Full-screen fade transition. Creates a child ColorRect covering the screen
    /// (on the parent CanvasLayer). TransitionIn() fades the rect in then hides
    /// the scene; TransitionOut() fades it out to reveal the scene. Connect a
    /// NavigationComponent.BeforeNavigate signal → TransitionIn, then change_scene
    /// in the Finished handler.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SceneTransitionComponent : UIComponent
    {
        public enum TransitionStyle { Fade, Slide }

        [Export] public TransitionStyle Style { get; set; } = TransitionStyle.Fade;
        [Export] public Color FadeColor { get; set; } = new(0, 0, 0, 1);
        [Export] public float Duration { get; set; } = 0.4f;

        [Signal] public delegate void FinishedEventHandler();

        private ColorRect? _rect;
        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            EnsureRect();
        }

        private void EnsureRect()
        {
            if (GetParent() is not Node parent) return;
            _rect = new ColorRect
            {
                Name = "TransitionRect",
                Color = new Color(FadeColor.R, FadeColor.G, FadeColor.B, 0),
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _rect.OffsetLeft = _rect.OffsetTop = _rect.OffsetRight = _rect.OffsetBottom = 0;
            parent.AddChild(_rect);
            _rect.Owner = parent;
        }

        public void TransitionIn()
        {
            if (!IsActive || _rect == null) return;
            _tween?.Kill();
            _rect.Color = new Color(FadeColor.R, FadeColor.G, FadeColor.B, 0);
            _tween = CreateTween();
            _tween.TweenProperty(_rect, "color:a", 1f, Duration);
            _tween.Finished += () => EmitSignal(SignalName.Finished);
        }

        public void TransitionOut()
        {
            if (!IsActive || _rect == null) return;
            _tween?.Kill();
            _rect.Color = new Color(FadeColor.R, FadeColor.G, FadeColor.B, 1);
            _tween = CreateTween();
            _tween.TweenProperty(_rect, "color:a", 0f, Duration);
            _tween.Finished += () => EmitSignal(SignalName.Finished);
        }
    }
}
