using System.Collections.Generic;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// HUD toast/notification queue. Each Push() spawns a small Panel+Label that
    /// slides in, holds, then fades and frees itself. Older toasts stack upward
    /// (LIFO at bottom). Use for "Saved!", "Quest updated", pickup logs, etc.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class NotificationStackComponent : UIComponent
    {
        public enum NoteType { Info, Success, Warning, Error }

        [Export] public float Duration { get; set; } = 3.0f;
        [Export] public Vector2 ToastSize { get; set; } = new(260, 36);
        [Export] public float Slide { get; set; } = 0.4f;
        [Export] public int MaxVisible { get; set; } = 4;

        private readonly List<PanelContainer> _active = new();

        public void Push(string message, NoteType type = NoteType.Info)
        {
            if (!IsActive) return;
            if (Engine.IsEditorHint()) return;

            var toast = new PanelContainer { CustomMinimumSize = ToastSize };
            var sb = new StyleBoxFlat();
            sb.BgColor = ColorFor(type);
            sb.SetCornerRadiusAll(6);
            sb.SetContentMarginAll(8);
            toast.AddThemeStyleboxOverride("panel", sb);

            var lbl = new Label { Text = message, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            lbl.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
            toast.AddChild(lbl);

            // Position at the bottom of the parent, stacking upward.
            var parent = GetParent() as Godot.Control;
            if (parent == null) return;
            float y = parent.Size.Y - ToastSize.Y;
            toast.Position = new Vector2(12, y);

            // Shift existing toasts up.
            float step = ToastSize.Y + 6;
            foreach (var t in _active)
                if (GodotObject.IsInstanceValid(t)) t.Position -= new Vector2(0, step);

            parent.AddChild(toast);
            _active.Add(toast);

            while (_active.Count > MaxVisible)
            {
                var old = _active[0];
                _active.RemoveAt(0);
                if (GodotObject.IsInstanceValid(old)) old.QueueFree();
            }

            // Slide-in + hold + fade.
            float startY = toast.Position.Y + 20;
            toast.Modulate = new Color(1, 1, 1, 0);
            var tw = toast.CreateTween();
            tw.TweenProperty(toast, "modulate:a", 1f, Slide);
            tw.TweenInterval(Duration);
            tw.TweenProperty(toast, "modulate:a", 0f, Slide * 0.6f);
            tw.Finished += () => { _active.Remove(toast); toast.QueueFree(); };
        }

        private static Color ColorFor(NoteType type) => type switch
        {
            NoteType.Success => new Color(0.15f, 0.6f, 0.2f, 0.95f),
            NoteType.Warning => new Color(0.8f, 0.6f, 0.1f, 0.95f),
            NoteType.Error => new Color(0.8f, 0.15f, 0.15f, 0.95f),
            _ => new Color(0.15f, 0.2f, 0.3f, 0.95f)
        };
    }
}
