using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Drag-and-drop component. Attach to any Control to make it draggable.
    /// Blind — works for windows, cards, inventory items, UI panels.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DragComponent : UIComponent
    {
        [Export] public bool DragHorizontal { get; set; } = true;
        [Export] public bool DragVertical { get; set; } = true;
        [Export] public bool SnapBack { get; set; } = false;
        [Export] public float SnapDuration { get; set; } = 0.3f;
        [Export] public bool ConstrainToParent { get; set; } = true;
        [Export] public bool BringToFrontOnDrag { get; set; } = true;

        [Signal] public delegate void DragStartedEventHandler();
        [Signal] public delegate void DragEndedEventHandler(Vector2 finalPosition);
        [Signal] public delegate void DroppedEventHandler(Vector2 position);

        private Godot.Control? _control;
        private Vector2 _dragOffset;
        private Vector2 _startPosition;
        private bool _isDragging;
        private Tween? _snapTween;

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent() as Godot.Control;
            if (_control != null)
            {
                _control.GuiInput += OnGuiInput;
                _control.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
                // A layout Container re-sorts its children's Position every layout pass and will
                // fight the drag (which moves _control.Position directly). Warn once; a real drag
                // genuinely moves the node, so we don't switch to offset_transform — reparent it.
                if (!Engine.IsEditorHint() && _control.GetParent() is Container)
                    GD.PushWarning($"[{Name}] DragComponent's target '{_control.Name}' is inside a {_control.GetParent().GetType().Name} — the Container will re-sort it each layout pass and fight the drag. Parent the draggable Control to a non-Container (a plain Control/CanvasLayer).");
            }
            else
            {
                GD.PushWarning($"[{Name}] DragComponent needs a Control parent to drag; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the draggable Control.");
            }
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (_control == null || !IsActive) return;

            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                {
                    _isDragging = true;
                    _snapTween?.Kill();
                    _startPosition = _control.Position;
                    _dragOffset = _control.GetGlobalMousePosition() - _control.GlobalPosition;
                    if (BringToFrontOnDrag && _control.GetParent() is Godot.Control parent)
                        parent.MoveChild(_control, -1);
                    EmitSignal(SignalName.DragStarted);
                    _control.AcceptEvent();
                }
                else if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed && _isDragging)
                {
                    _isDragging = false;
                    EmitSignal(SignalName.DragEnded, _control.Position);
                    EmitSignal(SignalName.Dropped, _control.Position);

                    if (SnapBack)
                    {
                        _snapTween = _control.CreateTween();
                        _snapTween.TweenProperty(_control, "position", _startPosition, SnapDuration)
                            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
                    }
                }
            }
            else if (@event is InputEventMouseMotion mm && _isDragging)
            {
                Vector2 newPos = _control.GetGlobalMousePosition() - _dragOffset;
                if (!DragHorizontal) newPos.X = _control.GlobalPosition.X;
                if (!DragVertical) newPos.Y = _control.GlobalPosition.Y;

                if (ConstrainToParent && _control.GetParent() is Godot.Control p)
                {
                    newPos.X = Mathf.Clamp(newPos.X, 0, p.Size.X - _control.Size.X);
                    newPos.Y = Mathf.Clamp(newPos.Y, 0, p.Size.Y - _control.Size.Y);
                }

                _control.Position = newPos;
                _control.AcceptEvent();
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            _snapTween?.Kill();
            if (_control != null && GodotObject.IsInstanceValid(_control))
                _control.GuiInput -= OnGuiInput;
        }
    }
}
