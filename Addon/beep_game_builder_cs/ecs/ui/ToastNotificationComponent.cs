using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Toast notification popup. Attach to any Node — spawns sliding toast messages.
    /// Blind — works for success, error, warning, info messages.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ToastNotificationComponent : EntityComponent
    {
        public enum ToastType { Info, Success, Warning, Error }

        [Export] public float Duration { get; set; } = 3f;
        [Export] public float SlideDistance { get; set; } = 80f;
        [Export] public Vector2 ToastSize { get; set; } = new(300, 48);
        [Export] public int MaxVisible { get; set; } = 3;

        [Signal] public delegate void ToastShownEventHandler(string message, ToastType type);
        [Signal] public delegate void ToastDismissedEventHandler();

        private readonly System.Collections.Generic.Queue<Control> _activeToasts = new();
        private static ToastNotificationComponent? _instance;

        public override void _Ready()
        {
            base._Ready();
            _instance = this;
        }

        public static void Show(string message, ToastType type = ToastType.Info)
        {
            _instance?.ShowToast(message, type);
        }

        public void ShowToast(string message, ToastType type = ToastType.Info)
        {
            if (!IsActive) return;

            var toast = new Panel();
            toast.Size = ToastSize;
            toast.Position = new Vector2((GetViewport().GetVisibleRect().Size.X - ToastSize.X) / 2f, -ToastSize.Y);

            Color bgColor = type switch
            {
                ToastType.Success => new Color(0.15f, 0.6f, 0.2f, 0.95f),
                ToastType.Warning => new Color(0.8f, 0.6f, 0.1f, 0.95f),
                ToastType.Error => new Color(0.8f, 0.15f, 0.15f, 0.95f),
                _ => new Color(0.15f, 0.2f, 0.3f, 0.95f)
            };

            var sb = new StyleBoxFlat { BgColor = bgColor };
            sb.SetCornerRadiusAll(8);
            toast.AddThemeStyleboxOverride("panel", sb);

            string icon = type switch
            { ToastType.Success => "✓", ToastType.Warning => "⚠", ToastType.Error => "✕", _ => "ℹ" };

            var label = new Label { Text = $"{icon}  {message}", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.AddThemeColorOverride("font_color", Colors.White);
            label.AddThemeFontSizeOverride("font_size", 13);
            toast.AddChild(label);

            // Stack existing toasts up
            float yOffset = ToastSize.Y + 8;
            foreach (var t in _activeToasts)
                t.Position += new Vector2(0, yOffset);

            GetParent()?.AddChild(toast);
            _activeToasts.Enqueue(toast);
            while (_activeToasts.Count > MaxVisible) { var old = _activeToasts.Dequeue(); old.QueueFree(); }

            var tween = toast.CreateTween();
            tween.TweenProperty(toast, "position:y", 12f, 0.4f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            tween.TweenInterval(Duration);
            tween.TweenProperty(toast, "modulate:a", 0f, 0.3f);
            tween.Finished += () => { toast.QueueFree(); EmitSignal(SignalName.ToastDismissed); };

            EmitSignal(SignalName.ToastShown, message, (int)type);
        }
    }
}
