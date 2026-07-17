using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Squash-and-stretch visual juice. Automatically deforms the parent Node2D
    /// (or Control) on jump and land events for bouncy, alive feel.
    /// Listens to sibling JumpComponent signals.
    ///
    /// Attach as a child of the same node that has JumpComponent.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SquashAndStretchComponent : ControllerComponent
    {
        [Export] public float SquashAmount { get; set; } = 0.15f;
        [Export] public float StretchAmount { get; set; } = 0.2f;
        [Export] public float Duration { get; set; } = 0.12f;

        private Node2D? _target2D;
        private Godot.Control? _targetControl;
        private Vector2 _originalScale;
        private Tween? _tween;
        private PlatformerController? _platformer;

        public override void _Ready()
        {
            base._Ready();
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            _target2D = GetParent() as Node2D;
            _targetControl = GetParent() as Godot.Control;
            _originalScale = _target2D?.Scale ?? _targetControl?.Scale ?? Vector2.One;

            var jump = GetSiblingComponent<JumpComponent>();
            if (jump != null)
            {
                jump.Jumped += OnJumped;
                jump.DoubleJumped += OnDoubleJumped;
            }

            // Land squash: JumpComponent has no land event, so the squash half never fired.
            // The sibling PlatformerController tracks floor contact and emits Landed.
            _platformer = GetSiblingComponent<PlatformerController>();
            if (_platformer != null)
                _platformer.Landed += OnLand;
        }

        private void OnJumped(int jumpsRemaining) => Stretch();
        private void OnDoubleJumped() => Stretch();
        public void OnLand() => Squash();

        private void Stretch()
        {
            if (!IsActive) return;
            _tween?.Kill();
            var target = _target2D ?? (Node)_targetControl;
            if (target == null) return;
            _tween = CreateTween();
            _tween.TweenProperty(target, "scale",
                new Vector2(_originalScale.X * (1f - StretchAmount), _originalScale.Y * (1f + StretchAmount)),
                Duration * 0.5f).SetEase(Tween.EaseType.Out);
            _tween.TweenProperty(target, "scale", _originalScale, Duration * 0.5f).SetEase(Tween.EaseType.In);
        }

        private void Squash()
        {
            if (!IsActive) return;
            _tween?.Kill();
            var target = _target2D ?? (Node)_targetControl;
            if (target == null) return;
            _tween = CreateTween();
            _tween.TweenProperty(target, "scale",
                new Vector2(_originalScale.X * (1f + SquashAmount), _originalScale.Y * (1f - SquashAmount)),
                Duration * 0.4f).SetEase(Tween.EaseType.Out);
            _tween.TweenProperty(target, "scale", _originalScale, Duration * 0.6f).SetEase(Tween.EaseType.In);
        }

        public override void _ExitTree()
        {
            _tween?.Kill();
            var jump = GetSiblingComponent<JumpComponent>();
            if (jump != null)
            {
                jump.Jumped -= OnJumped;
                jump.DoubleJumped -= OnDoubleJumped;
            }
            if (_platformer != null)
                _platformer.Landed -= OnLand;
        }
    }
}
