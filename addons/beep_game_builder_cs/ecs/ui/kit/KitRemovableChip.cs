using Godot;

namespace Beep.ECS.UI.Kit
{
    [Tool]
    [GlobalClass]
    public partial class KitRemovableChip : Button
    {
        [Signal] public delegate void RemovePressedEventHandler();

        [Export] public string ChipText { get => _text; set { _text = value ?? ""; QueueRedraw(); } }
        [Export] public bool Removable { get; set; } = true;
        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Accent;

        private string _text = "";
        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;

        public override void _Ready()
        {
            _genre = KitChrome.GenreOf(this);
            Suppress();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            QueueRedraw();
        }

        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            int fs = UiSurface.FontSize(this);
            KitChrome.Suppress(this, new[] { "normal", "hover", "pressed", "disabled", "focus" }, 0f, fs * 0.8f);
            _suppressing = false;
        }

        public override void _GuiInput(InputEvent @event)
        {
            base._GuiInput(@event);
            if (!Removable || @event is not InputEventMouseButton mb || !mb.Pressed) return;
            if (mb.Position.X >= Size.X - Size.Y * 0.95f)
            {
                EmitSignal(SignalName.RemovePressed);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            KitState state = Disabled ? KitState.Disabled : IsPressed() ? KitState.Pressed : IsHovered() ? KitState.Hover : KitState.Normal;
            Color fill = KitChrome.StateFace(UiSurface.Semantic(this, Role), state);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);
            var r = new Rect2(Vector2.Zero, Size);
            KitChrome.DrawShape(this, _genre, r, KitShape.Pill, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim * 0.6f));

            var font = KitChrome.Font(this, _genre);
            if (font == null) return;
            string text = KitChrome.Case(_text, _genre);
            float closeRoom = Removable ? Size.Y * 0.72f : 0f;
            var textBox = new Rect2(Size.Y * 0.45f, 0, Mathf.Max(1f, Size.X - Size.Y * 0.9f - closeRoom), Size.Y);
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Small, textBox.Size, text, font);
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            Color ink = UiSurface.Luminance(fill) > 0.5f ? new Color(0.1f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f);
            KitChrome.DrawText(this, _genre, font, new Vector2(textBox.Position.X, (Size.Y + m.Y * 0.62f) * 0.5f), text, fs, ink);

            if (!Removable) return;
            float c = Size.Y * 0.5f;
            var center = new Vector2(Size.X - c, c);
            float a = Size.Y * 0.16f;
            DrawLine(center + new Vector2(-a, -a), center + new Vector2(a, a), ink, Mathf.Max(1.5f, Size.Y * 0.08f));
            DrawLine(center + new Vector2(-a, a), center + new Vector2(a, -a), ink, Mathf.Max(1.5f, Size.Y * 0.08f));
        }
    }
}
