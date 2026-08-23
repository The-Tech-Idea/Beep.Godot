using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// `[E] Gather Wood` — a key/button glyph followed by the action it performs, with
    /// <b>chord support</b> (`L2 + ✛`).
    ///
    /// INDEX.md lists this as a new kit requirement found by the art pass, seen in three
    /// references, and chord support is called out explicitly: a hint that can only show ONE
    /// glyph cannot express the modifier combinations controllers rely on.
    ///
    /// `docs/hud/survival.md` element 11 wants exactly this as the interaction prompt, and the
    /// framework's existing `InteractionPromptComponent` is one of the components the HUD audit
    /// found had never been placed in any scene.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitInputHint : KitControl
    {
        /// <summary>A chip: takes the theme's chip corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Chip;

        /// <summary>Keys in the chord, joined by "+". One entry is the common case.</summary>
        [Export] public string[] Keys { get => _keys; set { _keys = value ?? System.Array.Empty<string>(); UpdateMinimumSize(); QueueRedraw(); } }
        private string[] _keys = { "E" };

        [Export] public string Action { get => _action; set { _action = value ?? ""; UpdateMinimumSize(); QueueRedraw(); } }
        private string _action = "Gather Wood";

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Ignore;
            if (CustomMinimumSize == Vector2.Zero)
                CustomMinimumSize = _GetMinimumSize();
        }

        public override Vector2 _GetMinimumSize()
        {
            int fs = UiSurface.FontSize(this);
            return new Vector2(fs * 9f, fs * 1.75f);
        }

        public override void _Draw()
        {
            if (Size.X < 12f || Size.Y < 8f) return;
            var font = KitFont();
            if (font == null) return;

            Color face = FaceColor();
            Color ink = InkColor();
            int fs = UiSurface.FontSize(this);
            float keyH = Mathf.Min(Size.Y * 0.82f, fs * 1.28f);
            float y = (Size.Y - keyH) * 0.5f;
            float x = 0f;

            // Key caps: light plates with a hard outline, so they read as physical buttons
            // against whatever the world behind them is doing.
            for (int i = 0; i < _keys.Length; i++)
            {
                string k = _keys[i] ?? "";
                int kfs = UiSurface.FitRole(this, UiSurface.TextRole.Value,
                                            new Vector2(keyH * 1.8f, keyH * 0.70f),
                                            k, font, min: 8);
                Vector2 km = font.GetStringSize(k, HorizontalAlignment.Left, -1, kfs);
                float kw = Mathf.Max(keyH, km.X + fs * 0.8f);
                var cap = new Rect2(x, y, kw, keyH);

                Color plate = new(Mathf.Lerp(face.R, 1f, 0.82f), Mathf.Lerp(face.G, 1f, 0.82f),
                                  Mathf.Lerp(face.B, 1f, 0.84f), 1f);
                // A keycap is Round in EVERY genre. ActiveShape gave RPG a Chamfer cap, which at
                // this size cuts every corner off a ~26px square and renders the key as a diamond
                // — unreadable as a keyboard key. This is the same exemption KitTree and KitChip
                // take: a widget that depicts a real-world object keeps that object's silhouette
                // rather than the genre's.
                DrawShape(cap, KitShape.Round, plate, ink, Mathf.Max(1.5f, Geo.Rim * 0.7f * (fs / 14f)));
                DrawText(font, new Vector2(cap.Position.X + (cap.Size.X - km.X) * 0.5f, cap.Position.Y + (cap.Size.Y + km.Y * 0.6f) * 0.5f),
                           k, kfs, new Color(0.10f, 0.09f, 0.08f));
                x += kw;

                // The chord separator sits BETWEEN caps and is not a cap itself.
                if (i < _keys.Length - 1)
                {
                    const string plus = "+";
                    Vector2 pm = font.GetStringSize(plus, HorizontalAlignment.Left, -1, fs);
                    DrawText(font, new Vector2(x + fs * 0.22f, y + (keyH + pm.Y * 0.6f) * 0.5f),
                               plus, fs, UiSurface.Text(this));
                    x += pm.X + fs * 0.44f;
                }
            }

            if (string.IsNullOrEmpty(_action)) return;
            float remaining = Mathf.Max(8f, Size.X - x - fs * 0.5f);
            int afs = UiSurface.FitRole(this, UiSurface.TextRole.Caption,
                                        new Vector2(remaining, keyH * 0.62f),
                                        _action, font, min: 8);
            Vector2 am = font.GetStringSize(_action, HorizontalAlignment.Left, -1, afs);
            DrawText(font, new Vector2(x + fs * 0.5f, y + (keyH + am.Y * 0.6f) * 0.5f),
                       _action, afs, UiSurface.Text(this));
        }
    }
}
