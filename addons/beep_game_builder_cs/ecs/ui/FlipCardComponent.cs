using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Card flip component. Attach to a Container with 2 children (front/back).
    /// First child = front face, second child = back face.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class FlipCardComponent : UIComponent
    {
        [Export] public float Duration { get; set; } = 0.5f;
        [Export] public bool StartFaceUp { get; set; } = true;

        [Signal] public delegate void FlippedEventHandler(bool showingFront);

        private Container? _container;
        private Godot.Control? _front;
        private Godot.Control? _back;
        private bool _showingFront;
        private Tween? _flipTween;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _container = GetParent() as Container;
            if (_container == null) return;
            var children = _container.GetChildren();
            if (children.Count >= 2) { _front = children[0] as Godot.Control; _back = children[1] as Godot.Control; }
            if (!StartFaceUp) { _showingFront = false; if (_front != null) _front.Visible = false; }
            else _showingFront = true;
        }

        public void Flip()
        {
            if (!IsActive || _container == null) return;
            _flipTween?.Kill();

            _showingFront = !_showingFront;
            var show = _showingFront ? _front : _back;
            var hide = _showingFront ? _back : _front;

            // Scale X to 0, swap visibility, scale back
            _flipTween = _container.CreateTween();

            _flipTween.TweenProperty(_container, "scale:x", 0f, Duration * 0.5f);
            _flipTween.TweenCallback(Callable.From(() => OnFlipMidpoint(show, hide)));
            _flipTween.TweenProperty(_container, "scale:x", 1f, Duration * 0.5f);
            _flipTween.Finished += OnFlipFinished;
        }

        private void OnFlipMidpoint(Godot.Control? show, Godot.Control? hide)
        {
            if (hide != null) hide.Visible = false;
            if (show != null)
            {
                show.Visible = true;
                show.Scale = new Vector2(-show.Scale.X, show.Scale.Y);
            }
        }

        private void OnFlipFinished() => EmitSignal(SignalName.Flipped, _showingFront);

        public void ShowFront() { if (!_showingFront) Flip(); }
        public void ShowBack() { if (_showingFront) Flip(); }

        public override void _ExitTree()
        {
            _flipTween?.Kill();
        }
    }
}
