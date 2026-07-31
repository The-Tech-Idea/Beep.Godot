using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A portrait in a frame, with a badge OVERHANGING its rim — CATALOGUE-FROM-ART.md section E
    /// (`AvatarFrame`, "overhanging its rim"), and the element `ui8`'s FriendCard hangs a level
    /// star on ("a star at the card's bottom-right, straddling the corner").
    ///
    /// The overhang is the reason this is a widget and not a TextureRect with a border: a child
    /// cannot cross its parent's edge under a Container, which is the same constraint
    /// <see cref="KitAttach"/> exists for.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitAvatarFrame : KitControl
    {
        [Export] public Texture2D? Portrait { get => _art; set { _art = value; QueueRedraw(); } }
        private Texture2D? _art;

        /// <summary>Shown in the badge. Empty hides it.</summary>
        [Export] public string BadgeText { get => _badge; set { _badge = value ?? ""; QueueRedraw(); } }
        private string _badge = "12";

        [Export] public UiSurface.Role BadgeRole { get; set; } = UiSurface.Role.Warning;

        /// <summary>Round is the portrait convention; a square frame suits roster grids.</summary>
        [Export] public bool Round { get; set; } = true;

        /// <summary>Ring in a palette role — rarity, team, online state.</summary>
        [Export] public UiSurface.Role RimRole { get; set; } = UiSurface.Role.Accent;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 4f, fs * 4f);
            }
        }

        public override void _Draw()
        {
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 10f) return;

            Color face = FaceColor();
            Color ink = InkColor();
            Color rim = UiSurface.Semantic(this, RimRole);
            int fs = UiSurface.FontSize(this);

            // Inset so the badge can straddle the rim without leaving our own rect.
            float pad = d * 0.12f;
            var frame = new Rect2(pad, pad, d - pad * 2f, d - pad * 2f);
            KitShape shape = Round ? KitShape.Pill : ActiveShape;

            float rw = Mathf.Max(2.5f, d * 0.07f);
            DrawShape(frame, shape, face, ink, rw);
            // The ring sits inside the ink edge, so the frame reads as metal around a plate.
            DrawShape(frame.Grow(-rw * 0.8f), shape, new Color(0, 0, 0, 0), rim, rw * 0.9f);

            if (_art != null)
                DrawTextureRect(_art, frame.Grow(-rw * 1.8f), false);

            if (string.IsNullOrEmpty(_badge)) return;
            var font = KitFont();
            if (font == null) return;

            // Bottom-right, straddling the rim — the attention anchor measured 8x.
            int bs = Mathf.Max(8, Mathf.RoundToInt(fs * 0.72f));
            Vector2 m = font.GetStringSize(_badge, HorizontalAlignment.Left, -1, bs);
            float bw = Mathf.Max(m.X + bs * 0.7f, bs * 1.5f), bh = bs * 1.3f;
            var b = new Rect2(frame.End.X - bw * 0.55f, frame.End.Y - bh * 0.65f, bw, bh);
            Color bc = UiSurface.Semantic(this, BadgeRole);
            DrawShape(b, KitShape.Pill, bc, ink, Mathf.Max(1.5f, rw * 0.6f));
            DrawString(font, new Vector2(b.Position.X + (b.Size.X - m.X) * 0.5f,
                                         b.Position.Y + (b.Size.Y + m.Y * 0.6f) * 0.5f),
                       _badge, HorizontalAlignment.Left, -1, bs,
                       UiSurface.Luminance(bc) > 0.5f
                           ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
        }
    }
}
