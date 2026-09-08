using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A chip the player can take off: a pill with a label and a close affordance at its trailing
    /// edge. Filters, equipped tags, party members, applied modifiers.
    ///
    /// It IS a <see cref="Button"/>, so pressing it is an ordinary press — <c>ui_accept</c> and a
    /// click both work, inherited from BaseButton. Removal is a SEPARATE gesture, not a second
    /// meaning for the same press: clicking the close region, or Delete/Backspace while focused,
    /// emits <c>RemovePressed</c>. Conflating the two would make selecting a chip destroy it.
    ///
    /// Known limitation: Godot defines no built-in action for "remove", so the removal gesture is
    /// keyboard and mouse only. A controller can focus and press the chip but not take it off; a
    /// screen that needs that should offer a remove control of its own.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitRemovableChip : Button
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        [Signal] public delegate void RemovePressedEventHandler();

        [Export] public string ChipText { get => _text; set { string next = value ?? ""; if (_text == next) return; _text = next; RefreshMinimumAndRedraw(); } }
        [Export] public bool Removable { get => _removable; set { if (_removable == value) return; _removable = value; RefreshMinimumAndRedraw(); } }
        [Export] public UiSurface.Role Role { get => _role; set { if (_role == value) return; _role = value; RefreshVisualAndRedraw(); } }

        private string _text = "";
        private bool _removable = true;
        private UiSurface.Role _role = UiSurface.Role.Accent;
        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            Suppress();
            KitChrome.HookButtonChromeRedraw(this, RefreshVisualAndRedraw, ref _eventsHooked);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            float h = Mathf.Max(fs * 1.55f, 24f);
            string text = KitChrome.Case(_text, _genre);
            Font? font = KitChrome.Font(this, _genre);
            float textWidth = string.IsNullOrEmpty(text)
                ? fs * 1.4f
                : font?.GetStringSize(text, HorizontalAlignment.Left, -1, fs).X ?? text.Length * fs * 0.56f;
            float closeRoom = Removable ? h * 0.85f : 0f;
            return new Vector2(Mathf.Max(h * 2.1f, textWidth + h * 0.9f + closeRoom), h);
        }

        // Derives from a native Godot type, so it cannot inherit KitControl's copy; it forwards to
        // the one shared body instead of restating it.
        private void RefreshMinimumAndRedraw()
            => KitChrome.RefreshMinimumAndRedraw(this, _GetMinimumSize());

        private void RefreshVisualAndRedraw()
        {
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
            if (Removable && @event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode is Key.Delete or Key.Backspace)
            {
                EmitSignal(SignalName.RemovePressed);
                AcceptEvent();
                return;
            }
            if (!Removable || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                base._GuiInput(@event);
                return;
            }
            if (mb.Position.X >= Size.X - Size.Y * 0.95f)
            {
                GrabFocus();
                EmitSignal(SignalName.RemovePressed);
                AcceptEvent();
                return;
            }
            base._GuiInput(@event);
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            KitState state = Disabled ? KitState.Disabled : IsPressed() ? KitState.Pressed : IsHovered() ? KitState.Hover : KitState.Normal;
            Color fill = KitChrome.StateFace(UiSurface.SemanticOrDerived(this, Role), state);
            if (fill.A < 0.02f) fill = UiSurface.Of(this);
            var r = new Rect2(Vector2.Zero, Size);
            KitChrome.DrawShape(this, _genre, r, KitShape.Pill, fill, UiSurface.Ink(fill), Mathf.Max(1f, Geo.Rim * 0.6f));

            var font = KitChrome.Font(this, _genre);
            if (font == null) return;
            string text = KitChrome.Case(_text, _genre);
            float closeRoom = Removable ? Size.Y * 0.72f : 0f;
            var textBox = new Rect2(Size.Y * 0.45f, 0, Mathf.Max(1f, Size.X - Size.Y * 0.9f - closeRoom), Size.Y);
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Small, textBox.Size, text, font);
            text = KitChrome.EllipsizeText(font, text, fs, textBox.Size.X);
            if (string.IsNullOrEmpty(text)) return;
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            Color ink = UiSurface.Luminance(fill) > 0.5f ? new Color(0.1f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f);
            KitChrome.DrawText(this, _genre, font, new Vector2(textBox.Position.X, (Size.Y + m.Y * 0.62f) * 0.5f), text, fs, ink);

            if (Removable)
            {
                float c = Size.Y * 0.5f;
                var center = new Vector2(Size.X - c, c);
                float a = Size.Y * 0.16f;
                DrawLine(center + new Vector2(-a, -a), center + new Vector2(a, a), ink, Mathf.Max(1.5f, Size.Y * 0.08f));
                DrawLine(center + new Vector2(-a, a), center + new Vector2(a, -a), ink, Mathf.Max(1.5f, Size.Y * 0.08f));
            }
            KitChrome.DrawFocusRing(this, _genre, r, KitShape.Pill, 0.8f);
        }
    }
}
