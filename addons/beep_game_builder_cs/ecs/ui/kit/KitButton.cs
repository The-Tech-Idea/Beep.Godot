using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A game button: the genre's material stack cut to the genre's silhouette, with sculpted
    /// states and an optional badge that OVERHANGS its corner.
    ///
    /// The phase-A proof for plans/game-ui-kit/PLAN.md — if this cannot be built from
    /// KitControl's primitives and reskinned across genres without touching this file, the
    /// architecture is wrong and should be reconsidered before phase B.
    ///
    /// Not a Godot Button subclass on purpose. Button owns its own StyleBox-per-state drawing,
    /// which is exactly the model the kit replaces; inheriting it would mean fighting the base
    /// class's draw on every frame.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitButton : KitControl
    {
        [Export] public string Text { get => _text; set { _text = value ?? ""; QueueRedraw(); } }
        private string _text = "Button";

        [Export] public Texture2D? Icon { get; set; }

        [Export] public bool Disabled
        {
            get => _disabled;
            set { _disabled = value; SetState(value ? KitState.Disabled : KitState.Normal); }
        }
        private bool _disabled;

        /// <summary>Badge text, e.g. a cost. Empty = no badge. Drawn straddling the top-right
        /// corner, which containers cannot do — see KitAttach.</summary>
        [Export] public string BadgeText { get => _badge; set { _badge = value ?? ""; Rebuild(); } }
        private string _badge = "";

        [Export] public UiSurface.Role BadgeRole { get; set; } = UiSurface.Role.Warning;

        [Signal] public delegate void PressedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                // Size from the GENRE's ratios, so a racing chip is lean and a platformer
                // plate is chunky without either restating a pixel size.
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * (5.5f + Geo.PadRatio * 1.6f),
                                                fs * Geo.HeightRatio);
            }
            Rebuild();
        }

        private void Rebuild()
        {
            Attachments.Clear();
            if (!string.IsNullOrEmpty(_badge))
            {
                int fs = UiSurface.FontSize(this, 0.8f);
                Attachments.Add(new KitAttach
                {
                    Anchor = KitAnchor.TopRight,
                    Size = new Vector2(fs * 2.2f, fs * 1.6f),
                    Shape = KitShape.Pill,
                    Role = BadgeRole,
                    Text = _badge,
                    Overhang = 0.5f,
                });
            }
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (_disabled) return;
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed) SetState(KitState.Pressed);
                else
                {
                    bool inside = new Rect2(Vector2.Zero, Size).HasPoint(mb.Position);
                    SetState(inside ? KitState.Hover : KitState.Normal);
                    if (inside) EmitSignal(SignalName.Pressed);
                }
                AcceptEvent();
            }
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (_disabled) return;
            if (what == NotificationMouseEnter) SetState(KitState.Hover);
            else if (what == NotificationMouseExit) SetState(KitState.Normal);
        }

        public override void _Draw()
        {
            if (Size.X <= 2 || Size.Y <= 2) return;

            // The badge overhangs, so the plate is inset to leave it room rather than being
            // clipped by the control's own rect.
            float inset = Attachments.Count > 0 ? UiSurface.FontSize(this, 0.8f) * 0.5f : 0f;
            var plate = new Rect2(inset, inset, Size.X - inset * 2f, Size.Y - inset * 2f);
            if (plate.Size.X <= 2 || plate.Size.Y <= 2) return;

            DrawMaterial(plate, ActiveShape);

            var font = KitFont();
            if (font != null && !string.IsNullOrEmpty(_text))
            {
                int fs = UiSurface.FontSize(this);
                Vector2 m = font.GetStringSize(_text, HorizontalAlignment.Left, -1, fs);
                // Pressed text shifts with the plate, so the label looks pushed in with it.
                float dy = State == KitState.Pressed ? 1f : 0f;
                var at = new Vector2(plate.Position.X + (plate.Size.X - m.X) * 0.5f,
                                     plate.Position.Y + (plate.Size.Y + m.Y * 0.62f) * 0.5f + dy);
                Color text = UiSurface.Text(this);
                if (State is KitState.Disabled or KitState.Locked) text = text with { A = 0.45f };
                DrawString(font, at, _text, HorizontalAlignment.Left, -1, fs, text);
            }

            DrawAttachments();
        }
    }
}
