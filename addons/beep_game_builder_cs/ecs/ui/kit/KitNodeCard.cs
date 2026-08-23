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
        /// <summary>A panel: takes the theme's panel corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Panel;

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
        private bool _hover;

        [Signal] public delegate void PressedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            FocusMode = FocusModeEnum.All;
            MouseEntered += () => { _hover = true; QueueRedraw(); };
            MouseExited += () => { _hover = false; QueueRedraw(); };
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                // Upgrade/shop cards need a stable compact footprint. The art does not use
                // screen-title text inside the card; the icon and welded footer carry the read.
                CustomMinimumSize = new Vector2(Mathf.Clamp(fs * 7.4f, 104f, 132f),
                                                Mathf.Clamp(fs * 10.4f, 146f, 188f));
            }
        }

        private float FooterHeight() => Footer switch
        {
            FooterKind.Status => Mathf.Clamp(Size.Y * 0.16f, 22f, 30f),
            FooterKind.Action => Mathf.Clamp(Size.Y * 0.13f, 20f, 28f),
            _ => 0f,
        };

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(Mathf.Clamp(fs * 7.4f, 104f, 132f),
                               Mathf.Clamp(fs * 10.4f, 146f, 188f));
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (_locked) return;
            if (@event is InputEventKey key && KitChrome.IsConfirmKey(key))
            {
                EmitSignal(SignalName.Pressed);
                AcceptEvent();
                return;
            }
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                GrabFocus();
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
            var font = KitFont();
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
            KitChrome.DrawFocusRing(this, KitChrome.GenreOf(this), body, ActiveShape, 0.8f);
            if (_hover && !_locked)
                KitSelect.Draw(this, Geo.SelectFor(WidgetClass),
                               KitChrome.Poly(ActiveShape, body, Geo), body,
                               UiSurface.Semantic(this, UiSurface.Role.Info),
                               Mathf.Max(1.5f, rimPx * 0.75f));

            float pad = Mathf.Clamp(Mathf.Min(body.Size.X, body.Size.Y) * 0.10f, 8f, 13f);
            float titleH = Mathf.Clamp(body.Size.Y * 0.18f, 22f, 32f);
            float reqH = _locked && !string.IsNullOrEmpty(_req) ? Mathf.Clamp(body.Size.Y * 0.12f, 14f, 22f) : 0f;
            float artBottom = body.End.Y - pad - titleH - reqH;

            // Art fills the upper portion, but it is boxed by named bands so it cannot collide
            // with title/requirement text on short cards.
            var art = new Rect2(body.Position + new Vector2(pad, pad),
                                new Vector2(body.Size.X - pad * 2f,
                                            Mathf.Max(26f, artBottom - body.Position.Y - pad)));
            if (_art != null)
            {
                DrawTextureRect(_art, art, false,
                                _locked ? new Color(0.55f, 0.55f, 0.58f, 1f) : Colors.White);
            }
            else
            {
                DrawArtPlaceholder(art, font, fs, face, ink);
            }

            if (font != null && !string.IsNullOrEmpty(_title))
            {
                Rect2 titleBox = new(body.Position.X + pad, art.End.Y + pad * 0.35f,
                                     body.Size.X - pad * 2f, titleH);
                DrawFittedText(font, titleBox, _title, UiSurface.TextRole.Caption,
                               UiSurface.Text(this), HorizontalAlignment.Center, 8);
            }

            // Requirement, in words, for a locked card.
            if (_locked && !string.IsNullOrEmpty(_req) && font != null)
            {
                Rect2 reqBox = new(body.Position.X + pad, body.End.Y - pad - reqH,
                                   body.Size.X - pad * 2f, reqH);
                DrawFittedText(font, reqBox, _req, UiSurface.TextRole.Small,
                               UiSurface.Text(this) with { A = 0.78f }, HorizontalAlignment.Center, 7);
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
            DrawFittedText(font, foot.Grow(-Mathf.Max(3f, foot.Size.Y * 0.14f)), _footer,
                           UiSurface.TextRole.Small,
                           UiSurface.Luminance(fc) > 0.5f
                               ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f),
                           HorizontalAlignment.Center, 8);
        }

        private void DrawArtPlaceholder(Rect2 r, Font? font, int fs, Color face, Color ink)
        {
            Color accent = _locked
                ? new Color(face.R * 0.60f, face.G * 0.60f, face.B * 0.62f, 1f)
                : UiSurface.Semantic(this, FooterRole);
            Color well = new Color(Mathf.Lerp(face.R, accent.R, 0.22f),
                                   Mathf.Lerp(face.G, accent.G, 0.22f),
                                   Mathf.Lerp(face.B, accent.B, 0.22f), 1f);
            DrawShape(r, ActiveShape, well, RimColor(), Mathf.Max(1f, r.Size.X * 0.025f));

            Vector2 c = r.Position + r.Size * 0.5f;
            float rr = Mathf.Min(r.Size.X, r.Size.Y) * 0.24f;
            DrawCircle(c, rr, accent);
            DrawArc(c, rr, 0f, Mathf.Tau, 24, ink, Mathf.Max(1.5f, rr * 0.10f));

            if (font == null || string.IsNullOrEmpty(_title)) return;
            string mark = KitCase(_title[..1]);
            int mf = UiSurface.FitRole(this, UiSurface.TextRole.Title,
                                       new Vector2(rr * 1.35f, rr * 1.35f), mark, font, min: 9);
            Vector2 m = font.GetStringSize(mark, HorizontalAlignment.Left, -1, mf);
            DrawText(font, new Vector2(c.X - m.X * 0.5f, c.Y + m.Y * 0.32f),
                     mark, mf, UiSurface.Luminance(accent) > 0.5f
                         ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f));
        }

        private void DrawFittedText(Font font, Rect2 r, string text, UiSurface.TextRole role,
                                    Color color, HorizontalAlignment align, int min)
        {
            if (string.IsNullOrEmpty(text) || r.Size.X <= 1f || r.Size.Y <= 1f) return;
            int fs = UiSurface.FitRole(this, role, r.Size, text, font, min: min);
            Vector2 m = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
            float x = align == HorizontalAlignment.Center
                ? r.Position.X + (r.Size.X - m.X) * 0.5f
                : align == HorizontalAlignment.Right
                    ? r.End.X - m.X
                    : r.Position.X;
            float y = r.Position.Y + (r.Size.Y - font.GetHeight(fs)) * 0.5f + font.GetAscent(fs);
            DrawText(font, new Vector2(x, y), text, fs, color);
        }
    }
}
