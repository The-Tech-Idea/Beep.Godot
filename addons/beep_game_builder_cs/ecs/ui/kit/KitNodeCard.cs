using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A card with a WELDED FOOTER BAR underneath — the upgrade/shop/skill card.
    ///
    /// CATALOGUE-FROM-ART.md calls the card-plus-footer "the single most repeated compound
    /// element across three pictures", and the art pass counted the welded footer **8 times**
    /// across unrelated sheets (store, skilltree1, Upgrades, ui5, gameui7, gameui8, rpg2, rpgui2).
    /// It is the highest-frequency compound in the whole folder and the kit had nothing for it.
    ///
    /// The correction that matters, recorded in INDEX.md: <b>the welded footer is TWO widgets,
    /// not one.</b> A <b>status band at 0.19 x card height</b> (skilltree1: 50px on a 262px card;
    /// store1 agrees) and an <b>action button at 0.10 x</b>. Modelling them as one would have
    /// produced a BUY button at twice its correct height — so <see cref="FooterKind"/> makes the
    /// caller say which it is, and the height follows from that rather than from a guess.
    ///
    /// Card state follows the settled rules: <b>locked drains saturation and states its
    /// requirement in words</b> (5x), rather than dimming behind a padlock.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitNodeCard : KitControl
    {
        public enum FooterKind
        {
            None,
            /// <summary>A status band — "OWNED", "TIER 3", a price. 0.19 x card height.</summary>
            Status,
            /// <summary>An action the player can press — BUY, EQUIP. 0.10 x card height.</summary>
            Action,
        }

        [Export] public string Title { get => _title; set { _title = value ?? ""; QueueRedraw(); } }
        private string _title = "";

        [Export] public Texture2D? Art { get => _art; set { _art = value; QueueRedraw(); } }
        private Texture2D? _art;

        [Export] public FooterKind Footer { get; set; } = FooterKind.Status;

        [Export] public string FooterText { get => _footer; set { _footer = value ?? ""; QueueRedraw(); } }
        private string _footer = "OWNED";

        [Export] public UiSurface.Role FooterRole { get; set; } = UiSurface.Role.Success;

        /// <summary>Locked cards state WHY, in words — the 5x settled rule.</summary>
        [Export] public bool Locked { get => _locked; set { _locked = value; SetState(value ? KitState.Locked : KitState.Normal); } }
        private bool _locked;

        [Export] public string Requirement { get => _req; set { _req = value ?? ""; QueueRedraw(); } }
        private string _req = "";

        [Signal] public delegate void PressedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                // Cards are markedly taller than wide in every reference.
                CustomMinimumSize = new Vector2(fs * 8f, fs * 13f);
            }
        }

        private float FooterHeight() => Footer switch
        {
            FooterKind.Status => Size.Y * 0.19f,
            FooterKind.Action => Size.Y * 0.10f,
            _ => 0f,
        };

        public override void _GuiInput(InputEvent @event)
        {
            if (_locked) return;
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                EmitSignal(SignalName.Pressed);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;

            var g = Geo;
            Color face = FaceColor();
            Color ink = InkColor();
            var font = GetThemeDefaultFont();
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * (fs / 14f));

            float fh = FooterHeight();
            var body = new Rect2(0, 0, Size.X, Size.Y - fh);

            Color plate = face;
            if (_locked)
            {
                // Drain saturation; do not simply dim. Lightness may even rise.
                float l = UiSurface.Luminance(face);
                plate = new Color(Mathf.Lerp(face.R, l, 0.93f), Mathf.Lerp(face.G, l, 0.93f),
                                  Mathf.Lerp(face.B, l, 0.93f), 1f);
            }

            DrawShape(body, ActiveShape, plate, _locked ? ink : RimColor(), rimPx);

            // Art fills the upper portion, inset into the card the way every reference does.
            if (_art != null)
            {
                var art = new Rect2(body.Position + new Vector2(body.Size.X * 0.12f, body.Size.Y * 0.10f),
                                    new Vector2(body.Size.X * 0.76f, body.Size.Y * 0.55f));
                DrawTextureRect(_art, art, false,
                                _locked ? new Color(0.55f, 0.55f, 0.58f, 1f) : Colors.White);
            }

            if (font != null && !string.IsNullOrEmpty(_title))
            {
                // A card's name is its TITLE. Drawn at body size it read as a caption on a large
                // card ("Iron Axe", "Rune Axe" on the widget sheet), and the role now scales it
                // with the card while still shrinking to fit a narrow one.
                int tf = UiSurface.FitRole(this, UiSurface.TextRole.Title,
                                           new Vector2(body.Size.X * 0.88f, body.Size.Y * 0.22f),
                                           _title, font);
                Vector2 m = font.GetStringSize(_title, HorizontalAlignment.Left, -1, tf);
                DrawString(font,
                           new Vector2(body.Position.X + (body.Size.X - m.X) * 0.5f,
                                       body.Position.Y + body.Size.Y * 0.78f),
                           _title, HorizontalAlignment.Left, -1, tf, UiSurface.Text(this));
            }

            // Requirement, in words, for a locked card.
            if (_locked && !string.IsNullOrEmpty(_req) && font != null)
            {
                int small = UiSurface.FitRole(this, UiSurface.TextRole.Caption,
                                              new Vector2(body.Size.X * 0.92f, body.Size.Y * 0.12f),
                                              _req, font, min: 8);
                Vector2 m = font.GetStringSize(_req, HorizontalAlignment.Left, -1, small);
                DrawString(font,
                           new Vector2(body.Position.X + (body.Size.X - m.X) * 0.5f,
                                       body.Position.Y + body.Size.Y * 0.93f),
                           _req, HorizontalAlignment.Left, -1, small, UiSurface.Text(this));
            }

            // ── the welded footer ──
            if (fh <= 1f) return;
            var foot = new Rect2(0, Size.Y - fh, Size.X, fh);

            // Welded: it shares the card's width and butts against it with no gap. The palette
            // goes on the footer and the card body stays neutral (the "palette on ONE element"
            // rule), which is what makes the footer read as the card's call to action.
            Color fc = _locked
                ? new Color(plate.R * 0.7f, plate.G * 0.7f, plate.B * 0.72f, 1f)
                : UiSurface.Semantic(this, FooterRole);
            DrawShape(foot, ActiveShape, fc, ink, Mathf.Max(1f, rimPx * 0.7f));

            if (font == null || string.IsNullOrEmpty(_footer)) return;
            int ffs = Footer == FooterKind.Action
                ? Mathf.Max(8, Mathf.RoundToInt(fs * 0.85f))
                : Mathf.Max(8, Mathf.RoundToInt(fs * 0.8f));
            Vector2 fm = font.GetStringSize(_footer, HorizontalAlignment.Left, -1, ffs);
            DrawString(font,
                       new Vector2(foot.Position.X + (foot.Size.X - fm.X) * 0.5f,
                                   foot.Position.Y + (foot.Size.Y + fm.Y * 0.6f) * 0.5f),
                       _footer, HorizontalAlignment.Left, -1, ffs,
                       UiSurface.Luminance(fc) > 0.5f
                           ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
        }
    }
}
