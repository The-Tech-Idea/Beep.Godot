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
        [Export] public Texture2D? Portrait { get => _art; set { if (_art == value) return; _art = value; RefreshVisualAndRedraw(); } }
        private Texture2D? _art;

        /// <summary>Shown in the badge. Empty hides it.</summary>
        [Export] public string BadgeText { get => _badge; set { SetText(ref _badge, value); } }
        private string _badge = "12";

        [Export]
        public UiSurface.Role BadgeRole
        {
            get => _badgeRole;
            set { if (_badgeRole == value) return; _badgeRole = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _badgeRole = UiSurface.Role.Warning;

        /// <summary>Round is the portrait convention; a square frame suits roster grids.</summary>
        [Export]
        public bool Round
        {
            get => _round;
            set { if (_round == value) return; _round = value; RefreshVisualAndRedraw(); }
        }
        private bool _round = true;

        /// <summary>Ring in a palette role — rarity, team, online state.</summary>
        [Export]
        public UiSurface.Role RimRole
        {
            get => _rimRole;
            set { if (_rimRole == value) return; _rimRole = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _rimRole = UiSurface.Role.Accent;

        public override void _Ready()
        {
            base._Ready();
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        private void SetText(ref string target, string? value)
        {
            string next = value ?? "";
            if (target == next) return;
            target = next;
            RefreshContentAndRedraw();
        }

        private void RefreshContentAndRedraw()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 4f, fs * 4f);
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
            else
                DrawPortraitPlaceholder(frame.Grow(-rw * 1.8f), rim, ink);

            if (string.IsNullOrEmpty(_badge)) return;
            var font = KitFont();
            if (font == null) return;

            // Bottom-right, straddling the rim. Badge is always a circle so every avatar uses
            // the same visual language whether the text is "3" or "12".
            float dia = Mathf.Clamp(d * 0.32f, 18f, 30f);
            string badge = KitCase(_badge);
            float badgeWidth = dia * 0.68f;
            int bs = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                       new Vector2(badgeWidth, dia * 0.58f),
                                       badge, font, min: 8);
            badge = KitChrome.EllipsizeText(font, badge, bs, badgeWidth);
            Vector2 m = font.GetStringSize(badge, HorizontalAlignment.Left, -1, bs);
            var b = new Rect2(frame.End.X - dia * 0.60f, frame.End.Y - dia * 0.66f, dia, dia);
            b.Position = new Vector2(Mathf.Clamp(b.Position.X, 0f, Mathf.Max(0f, Size.X - dia)),
                                     Mathf.Clamp(b.Position.Y, 0f, Mathf.Max(0f, Size.Y - dia)));
            Color bc = UiSurface.Semantic(this, BadgeRole);
            Vector2 centre = b.Position + b.Size * 0.5f;
            DrawCircle(centre, dia * 0.5f, bc);
            DrawArc(centre, dia * 0.5f, 0f, Mathf.Tau, 32, ink, Mathf.Max(1.5f, rw * 0.45f));
            float baseline = b.Position.Y + (b.Size.Y - font.GetHeight(bs)) * 0.5f + font.GetAscent(bs);
            DrawText(font, new Vector2(b.Position.X + (b.Size.X - m.X) * 0.5f, baseline),
                       badge, bs, UiSurface.Luminance(bc) > 0.5f
                           ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
        }

        private void DrawPortraitPlaceholder(Rect2 r, Color rim, Color ink)
        {
            Color bg = new(Mathf.Lerp(FaceColor().R, rim.R, 0.24f),
                           Mathf.Lerp(FaceColor().G, rim.G, 0.24f),
                           Mathf.Lerp(FaceColor().B, rim.B, 0.24f), 1f);
            DrawShape(r, Round ? KitShape.Pill : ActiveShape, bg, new Color(0, 0, 0, 0), 0f);
            Vector2 c = r.Position + r.Size * 0.5f;
            float d = Mathf.Min(r.Size.X, r.Size.Y);
            DrawCircle(c + new Vector2(0f, -d * 0.12f), d * 0.16f, new Color(ink.R, ink.G, ink.B, 0.45f));
            DrawArc(c + new Vector2(0f, d * 0.30f), d * 0.30f, Mathf.Pi * 1.08f, Mathf.Pi * 1.92f,
                    24, new Color(ink.R, ink.G, ink.B, 0.45f), Mathf.Max(2f, d * 0.08f));
        }
    }
}
