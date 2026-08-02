using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Toast notification popup. Attach to any Node — spawns sliding toast messages.
    /// Blind — works for success, error, warning, info messages.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ToastNotificationComponent : UIScreenComponent
    {
        public enum ToastType { Info, Success, Warning, Error }

        [Export] public float Duration { get; set; } = 3f;
        [Export] public float SlideDistance { get; set; } = 80f;
        [Export] public Vector2 ToastSize { get; set; } = new(300, 48);
        [Export] public int MaxVisible { get; set; } = 3;

        [Signal] public delegate void ToastShownEventHandler(string message, ToastType type);
        [Signal] public delegate void ToastDismissedEventHandler();

        // A List (not Queue) so a self-dismissed toast can Remove itself in its Finished lambda —
        // otherwise freed toasts lingered here and the next ShowText's stacking loop touched a
        // disposed node (ObjectDisposedException), and the stale entries evicted real toasts early.
        private readonly System.Collections.Generic.List<Godot.Control> _activeToasts = new();
        // Pending dismiss tweens, so _ExitTree can kill them — their Finished lambda captures this
        // and EmitSignals; toasts are parented to GetParent() (which outlives this component), so
        // without the kill a still-animating toast fires on a freed component.
        private readonly System.Collections.Generic.List<Tween> _toastTweens = new();
        private static ToastNotificationComponent? _instance;

        public override void _Ready()
        {
            base._Ready();
            _instance = this;
        }

        public override void _ExitTree()
        {
            // Clear the static so Show() doesn't call a freed node after a scene change. Guard on
            // identity: a later instance may already own it.
            if (_instance == this) _instance = null;
            // Kill in-flight dismiss tweens (their Finished lambda would EmitSignal on this freed
            // component) and free the toasts they were animating.
            foreach (var t in _toastTweens) if (GodotObject.IsInstanceValid(t)) t.Kill();
            _toastTweens.Clear();
            foreach (var toast in _activeToasts) if (GodotObject.IsInstanceValid(toast)) toast.QueueFree();
            _activeToasts.Clear();
            base._ExitTree();
        }

        public static void Show(string message, ToastType type = ToastType.Info)
        {
            if (GodotObject.IsInstanceValid(_instance)) _instance!.ShowToast(message, type);
        }

        public void ShowToast(string message, ToastType type = ToastType.Info)
        {
            if (!IsActive) return;
            int fs = UiSurface.FontSize(this);
            Vector2 toastSize = ToastSize;
            if (toastSize == new Vector2(300, 48))
                toastSize = new Vector2(fs * 21f, fs * 3.4f);

            // TopLevel so the absolutely-positioned toast isn't re-laid-out when this component is
            // parented under a Container.
            var toast = new KitToast { TopLevel = true };
            toast.Size = toastSize;
            toast.Position = new Vector2((GetViewport().GetVisibleRect().Size.X - toastSize.X) / 2f, -toastSize.Y);
            toast.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;

            string icon = type switch
            { ToastType.Success => "✓", ToastType.Warning => "⚠", ToastType.Error => "✕", _ => "ℹ" };
            toast.IconGlyph = icon;
            toast.Message = message;
            toast.Role = type switch
            {
                ToastType.Success => UiSurface.Role.Success,
                ToastType.Warning => UiSurface.Role.Warning,
                ToastType.Error => UiSurface.Role.Danger,
                _ => UiSurface.Role.Info
            };

            // Stack existing toasts up (guard: entries are pruned on dismiss, but stay defensive).
            float yOffset = toastSize.Y + 8;
            foreach (var t in _activeToasts)
                if (GodotObject.IsInstanceValid(t)) t.Position += new Vector2(0, yOffset);

            GetParent()?.AddChild(toast);
            _activeToasts.Add(toast);
            while (_activeToasts.Count > MaxVisible)
            {
                var old = _activeToasts[0];
                _activeToasts.RemoveAt(0);
                if (GodotObject.IsInstanceValid(old)) old.QueueFree();
            }

            var tween = toast.CreateTween();
            _toastTweens.Add(tween);
            tween.TweenProperty(toast, "position:y", 12f, 0.4f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenInterval(Duration);
            tween.TweenProperty(toast, "modulate:a", 0f, 0.3f);
            tween.Finished += () => { _toastTweens.Remove(tween); _activeToasts.Remove(toast); toast.QueueFree(); EmitSignal(SignalName.ToastDismissed); };

            EmitSignal(SignalName.ToastShown, message, (int)type);
        }
    }
}
