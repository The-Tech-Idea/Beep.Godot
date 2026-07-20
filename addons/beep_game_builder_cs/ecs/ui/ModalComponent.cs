using Godot;
using System;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Modal dialog overlay component. Attach to any Control to make it a modal popup.
    /// Blind — works for dialogs, confirmations, settings, forms.
    /// Creates dark overlay behind the dialog, blocks input to background.
    ///
    /// The open/close pop animates the dialog's `scale`, which assumes the dialog is a
    /// free-positioned Control (an overlay centered by anchors) — NOT a child of a layout
    /// Container that would re-sort and overwrite the scale each pass. Modals are normally
    /// absolute overlays, so this holds; if you must host one in a Container, animate the
    /// offset_transform layer instead (see UIEffectComponent).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ModalComponent : UIComponent
    {
        [Export] public bool StartVisible { get; set; } = false;
        [Export] public Color OverlayColor { get; set; } = new(0, 0, 0, 0.5f);
        [Export] public bool CloseOnOverlayClick { get; set; } = true;
        [Export] public bool CloseOnCancel { get; set; } = true;   // ui_cancel (Esc / gamepad B) closes
        [Export] public float AnimationDuration { get; set; } = 0.25f;

        [Signal] public delegate void OpenedEventHandler();
        [Signal] public delegate void ClosedEventHandler();

        private Godot.Control? _dialog;
        private ColorRect? _overlay;
        private Tween? _tween;
        private bool _containerWarned;   // one-shot guard for the Container-host warning in Open()

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _dialog = GetParent() as Godot.Control;
            if (_dialog == null)
            {
                GD.PushWarning($"[{Name}] ModalComponent needs a Control parent to show/hide; got '{GetParent()?.GetType().Name ?? "null"}'. Parent it to the dialog Control.");
                return;
            }

            // Hide first, then let Open() reveal a StartVisible modal — Open() builds the dark
            // overlay and animates in. Previously StartVisible left the dialog visible with NO
            // overlay (and Open() then early-returned because it was already visible), so
            // CloseOnOverlayClick was dead until the modal had been Closed once.
            _dialog.Visible = false;
            if (StartVisible) Callable.From(Open).CallDeferred();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // The overlay is parented to the dialog's PARENT, not to this node, so freeing
            // the component does not take it along. Left behind while open, it keeps its
            // MouseFilter.Stop and blocks all input to whatever loads next.
            _tween?.Kill();
            _tween = null;
            if (_overlay != null && GodotObject.IsInstanceValid(_overlay))
                _overlay.QueueFree();
            _overlay = null;
        }

        public void Open()
        {
            if (_dialog == null || !IsActive || _dialog.Visible) return;

            // The pop tweens _dialog.Scale, which a layout Container host would overwrite each
            // layout pass. Warn once — modals are normally free overlays, so this is a misconfig.
            if (!_containerWarned && _dialog.GetParent() is Container)
            {
                GD.PushWarning($"[{Name}] ModalComponent's dialog is inside a {_dialog.GetParent().GetType().Name} — the Container will re-sort it and overwrite the open/close scale animation. Host the modal as a free overlay (CanvasLayer/anchored Control).");
                _containerWarned = true;
            }

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

            // Focus the first focusable descendant so a keyboard/gamepad player can act; falls back
            // to making the dialog itself focusable.
            Callable.From(GrabDialogFocus).CallDeferred();
        }

        private void GrabDialogFocus()
        {
            if (_dialog == null || !GodotObject.IsInstanceValid(_dialog) || !_dialog.Visible) return;
            if (FindFocusable(_dialog) is { } target) { target.GrabFocus(); return; }
            _dialog.FocusMode = Godot.Control.FocusModeEnum.All;
            _dialog.GrabFocus();
        }

        private static Godot.Control? FindFocusable(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is Godot.Control c && c.FocusMode != Godot.Control.FocusModeEnum.None && c.Visible)
                    return c;
                if (FindFocusable(child) is { } nested) return nested;
            }
            return null;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // ui_cancel (Esc / gamepad B) closes an open modal. Built-in action, always present.
            if (!CloseOnCancel || _dialog is null || !_dialog.Visible) return;
            if (@event.IsActionPressed("ui_cancel"))
            {
                Close();
                GetViewport()?.SetInputAsHandled();
            }
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
