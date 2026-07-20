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
            Callable.From(Setup).CallDeferred();
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

        private void Stretch() => Play(1f - StretchAmount, 1f + StretchAmount, Duration * 0.5f, Duration * 0.5f);
        private void Squash() => Play(1f + SquashAmount, 1f - SquashAmount, Duration * 0.4f, Duration * 0.6f);

        /// <summary>Deform then settle. For a Node2D target, scale is animated directly (relative to
        /// the captured original scale). For a Control target, the render-only offset_transform_scale
        /// layer is used instead — a Control inside a Container has its raw scale overwritten every
        /// layout pass (CLAUDE.md), so the juice would silently never play. Offset-transform neutral
        /// is Vector2.One, so the multipliers apply directly.</summary>
        private void Play(float xMul, float yMul, float upDur, float downDur)
        {
            if (!IsActive) return;
            _tween?.Kill();
            if (_targetControl != null)
            {
                _targetControl.OffsetTransformEnabled = true;
                _tween = CreateTween();
                _tween.TweenProperty(_targetControl, "offset_transform_scale", new Vector2(xMul, yMul), upDur).SetEase(Tween.EaseType.Out);
                _tween.TweenProperty(_targetControl, "offset_transform_scale", Vector2.One, downDur).SetEase(Tween.EaseType.In);
            }
            else if (_target2D != null)
            {
                _tween = CreateTween();
                _tween.TweenProperty(_target2D, "scale", new Vector2(_originalScale.X * xMul, _originalScale.Y * yMul), upDur).SetEase(Tween.EaseType.Out);
                _tween.TweenProperty(_target2D, "scale", _originalScale, downDur).SetEase(Tween.EaseType.In);
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _tween?.Kill();
            // Siblings can free before this node — guard the -= against a disposed collaborator.
            var jump = GetSiblingComponent<JumpComponent>();
            if (jump != null && GodotObject.IsInstanceValid(jump))
            {
                jump.Jumped -= OnJumped;
                jump.DoubleJumped -= OnDoubleJumped;
            }
            if (_platformer != null && GodotObject.IsInstanceValid(_platformer))
                _platformer.Landed -= OnLand;
        }
    }
}
