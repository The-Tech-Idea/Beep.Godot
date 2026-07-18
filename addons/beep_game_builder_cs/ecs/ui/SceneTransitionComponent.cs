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

        /// <summary>True when the fade can actually run (active and its rect exists). SceneNav
        /// checks this before routing navigation through the fade, so a not-yet-ready transition
        /// never stalls a scene change.</summary>
        public bool CanPlay => IsActive && _rect != null;

        public override void _Ready()
        {
            base._Ready();
            // EnsureRect spawns a ColorRect. This is [Tool] and lives in the menu scenes,
            // so without the guard opening one in the editor adds a runtime-only node.
            if (Engine.IsEditorHint()) return;
            // Defer rect creation — adding children during _Ready can fail with
            // "Parent node is busy setting up children" when the parent scene
            // is still instantiating its own children.
            CallDeferred(nameof(EnsureRect));
        }

        private void EnsureRect()
        {
            if (_rect != null) return; // already created
            if (GetParent() is not Node parent) return;
            if (!IsInsideTree()) return;

            _rect = new ColorRect
            {
                Name = "TransitionRect",
                Color = new Color(FadeColor.R, FadeColor.G, FadeColor.B, 0),
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };
            _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _rect.OffsetLeft = _rect.OffsetTop = _rect.OffsetRight = _rect.OffsetBottom = 0;
            parent.AddChild(_rect);
            // Only set owner if parent is in the scene tree (avoids "Invalid owner" error
            // during editor tool mode when the node isn't fully in the tree yet).
            if (parent.IsInsideTree())
                _rect.Owner = parent.Owner;
        }

        public void TransitionIn()
        {
            if (!IsActive || _rect == null) return;
            _tween?.Kill();
            _rect.Color = FadeColor with { A = 0 };
            _tween = CreateTween();
            _tween.TweenProperty(_rect, "color:a", 1f, Duration);
            _tween.Finished += OnTransitionFinished;
        }

        public void TransitionOut()
        {
            if (!IsActive || _rect == null) return;
            _tween?.Kill();
            _rect.Color = FadeColor with { A = 1 };
            _tween = CreateTween();
            _tween.TweenProperty(_rect, "color:a", 0f, Duration);
            _tween.Finished += OnTransitionFinished;
        }

        private void OnTransitionFinished() => EmitSignal(SignalName.Finished);

        public override void _ExitTree()
        {
            _tween?.Kill();
        }
    }
}
