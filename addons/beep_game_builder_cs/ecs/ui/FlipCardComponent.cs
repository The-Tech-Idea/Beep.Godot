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
            if (_container == null)
            {
                GD.PushWarning($"[{Name}] FlipCardComponent needs a Container parent holding the front/back faces; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the card container.");
                return;
            }
            // Flip on the offset_transform layer so a card-grid/HBox parent can't overwrite the
            // scale mid-flip (the same container-stomp the other effects were migrated off of).
            _container.OffsetTransformEnabled = true;
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

            // Pivot at the card's horizontal center so the flip hinges in the middle, not the
            // left edge. Size is settled by the time a flip is triggered at runtime.
            _container.PivotOffset = new Vector2(_container.Size.X / 2f, _container.Size.Y / 2f);

            // Scale X to 0 on the offset layer, swap visibility, scale back.
            _flipTween = _container.CreateTween();

            _flipTween.TweenProperty(_container, "offset_transform_scale:x", 0f, Duration * 0.5f);
            _flipTween.TweenCallback(Callable.From(() => OnFlipMidpoint(show, hide)));
            _flipTween.TweenProperty(_container, "offset_transform_scale:x", 1f, Duration * 0.5f);
            _flipTween.Finished += OnFlipFinished;
        }

        private void OnFlipMidpoint(Godot.Control? show, Godot.Control? hide)
        {
            if (hide != null) hide.Visible = false;
            if (show != null)
            {
                show.Visible = true;
                // No scale negation: the container's own scale:x tween (1→0→1) already produces the
                // flip. Negating each face's X here mirrored the revealed face every other flip.
            }
        }

        private void OnFlipFinished() => EmitSignal(SignalName.Flipped, _showingFront);

        public void ShowFront() { if (!_showingFront) Flip(); }
        public void ShowBack() { if (_showingFront) Flip(); }

        public override void _ExitTree()
        {
            base._ExitTree();
            _flipTween?.Kill();
        }
    }
}
