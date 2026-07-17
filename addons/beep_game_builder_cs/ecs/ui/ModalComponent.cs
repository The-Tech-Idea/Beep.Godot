using Godot;
using System;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Modal dialog overlay component. Attach to any Control to make it a modal popup.
    /// Blind — works for dialogs, confirmations, settings, forms.
    /// Creates dark overlay behind the dialog, blocks input to background.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ModalComponent : UIComponent
    {
        [Export] public bool StartVisible { get; set; } = false;
        [Export] public Color OverlayColor { get; set; } = new(0, 0, 0, 0.5f);
        [Export] public bool CloseOnOverlayClick { get; set; } = true;
        [Export] public float AnimationDuration { get; set; } = 0.25f;

        [Signal] public delegate void OpenedEventHandler();
        [Signal] public delegate void ClosedEventHandler();

        private Godot.Control? _dialog;
        private ColorRect? _overlay;
        private Tween? _tween;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _dialog = GetParent() as Godot.Control;
            if (_dialog == null) return;

            if (!StartVisible) _dialog.Visible = false;
        }

        public void Open()
        {
            if (_dialog == null || !IsActive || _dialog.Visible) return;

            if (_overlay != null && GodotObject.IsInstanceValid(_overlay))
                _overlay.QueueFree();

            // Create overlay
            _overlay = new ColorRect { Color = OverlayColor, MouseFilter = Godot.Control.MouseFilterEnum.Stop };
            _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            if (CloseOnOverlayClick) _overlay.GuiInput += OnOverlayClicked;

            var root = _dialog.GetParent();
            int dialogIndex = _dialog.GetIndex();
            root?.AddChild(_overlay);
            root?.MoveChild(_overlay, dialogIndex); // Behind dialog

            _dialog.Visible = true;
            _dialog.Modulate = new Color(1, 1, 1, 0);
            _dialog.Scale = new Vector2(0.9f, 0.9f);

            _tween?.Kill();
            _tween = _dialog.CreateTween().SetParallel(true);
            _tween.TweenProperty(_dialog, "modulate:a", 1f, AnimationDuration);
            _tween.TweenProperty(_dialog, "scale", Vector2.One, AnimationDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            _tween.Finished += OnOpenFinished;
        }

        public void Close()
        {
            if (_dialog == null || !IsActive || !_dialog.Visible) return;

            _tween?.Kill();
            _tween = _dialog.CreateTween().SetParallel(true);
            _tween.TweenProperty(_dialog, "modulate:a", 0f, AnimationDuration * 0.6f);
            _tween.TweenProperty(_dialog, "scale", new Vector2(0.95f, 0.95f), AnimationDuration * 0.6f);
            _tween.Finished += OnCloseFinished;
        }

        private void OnOverlayClicked(InputEvent e)
        {
            if (CloseOnOverlayClick && e is InputEventMouseButton mb && mb.Pressed)
            {
                Close();
                GetViewport()?.SetInputAsHandled();
            }
        }

        private void OnOpenFinished() => EmitSignal(SignalName.Opened);

        private void OnCloseFinished()
        {
            _dialog.Visible = false;
            _dialog.Scale = Vector2.One;
            _dialog.Modulate = Colors.White;
            if (_overlay != null && GodotObject.IsInstanceValid(_overlay))
                _overlay.QueueFree();
            _overlay = null;
            EmitSignal(SignalName.Closed);
        }
    }
}
