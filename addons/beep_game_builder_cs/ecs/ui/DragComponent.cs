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

        public override void _Ready()
        {
            base._Ready();
            _control = GetParent() as Godot.Control;
            if (_control != null)
            {
                _control.GuiInput += OnGuiInput;
                _control.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
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
                    _startPosition = _control.Position;
                    _dragOffset = _control.GetGlobalMousePosition() - _control.Position;
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
                        var t = _control.CreateTween();
                        t.TweenProperty(_control, "position", _startPosition, SnapDuration)
                            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
                    }
                }
            }
            else if (@event is InputEventMouseMotion mm && _isDragging)
            {
                Vector2 newPos = _control.GetGlobalMousePosition() - _dragOffset;
                if (!DragHorizontal) newPos.X = _control.Position.X;
                if (!DragVertical) newPos.Y = _control.Position.Y;

                if (ConstrainToParent && _control.GetParent() is Godot.Control p)
                {
                    newPos.X = Mathf.Clamp(newPos.X, 0, p.Size.X - _control.Size.X);
                    newPos.Y = Mathf.Clamp(newPos.Y, 0, p.Size.Y - _control.Size.Y);
                }

                _control.Position = newPos;
                _control.AcceptEvent();
            }
        }
    }
}
