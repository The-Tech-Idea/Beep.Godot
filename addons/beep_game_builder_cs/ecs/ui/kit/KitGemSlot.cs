using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A socket — CATALOGUE-FROM-ART.md section E's `GemSlot`. The circular counterpart to
    /// <see cref="KitSlotGrid"/>'s square cell: rune sockets, gear inserts, augment slots.
    ///
    /// Kept separate because a socket is not a small inventory cell. It is CUT INTO its host
    /// rather than laid on it, so it uses the recessed readout shade (the 0.12 measured on
    /// citybuilder5's sunken capsule) rather than the 0.79 content-well shade a grid cell takes —
    /// the distinction that rendered a whole slot grid as black holes when it was got wrong.
    ///
    /// `KitState.Empty`'s three meanings apply here as they do to the grid (ui3.png shows all
    /// three on one screen): blank, an invite, or locked WITH a requirement in words.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitGemSlot : KitControl
    {
        /// <summary>A slot: takes the theme's slot corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Slot;

        public enum SocketState { Empty, Filled, Invite, Locked }

        [Export] public SocketState State_ { get => _state; set { _state = value; QueueRedraw(); } }
        private SocketState _state = SocketState.Filled;

        [Export] public Texture2D? Gem { get => _gem; set { _gem = value; QueueRedraw(); } }
        private Texture2D? _gem;

        /// <summary>Rarity / element colour of the inserted gem.</summary>
        [Export] public UiSurface.Role Role { get; set; } = UiSurface.Role.Info;

        [Export] public string Requirement { get => _req; set { _req = value ?? ""; QueueRedraw(); } }
        private string _req = "";

        [Signal] public delegate void ActivatedEventHandler();

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                CustomMinimumSize = new Vector2(fs * 2.8f, fs * 2.8f);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (_state == SocketState.Locked) return;
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                EmitSignal(SignalName.Activated);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            float d = Mathf.Min(Size.X, Size.Y);
            if (d < 10f) return;

            var c = Size * 0.5f;
            Color face = FaceColor();
            Color ink = InkColor();
            int fs = UiSurface.FontSize(this);

            // Cut IN: the recessed readout shade, not the content-well shade.
            float ps = Geo.PlateShadeFor(KitElevation.Recessed);
            float r = d * 0.42f;
            DrawCircle(c, r, new Color(face.R * ps, face.G * ps, face.B * ps, 1f));
            // Bezel: bright above, dark below, so the socket reads as a hole rather than a disc.
            DrawArc(c, r, Mathf.Pi, Mathf.Tau, 24, new Color(0, 0, 0, 0.35f), Mathf.Max(2f, d * 0.06f));
            DrawArc(c, r, 0f, Mathf.Pi, 24, new Color(1, 1, 1, 0.22f), Mathf.Max(2f, d * 0.06f));
            DrawArc(c, r, 0f, Mathf.Tau, 32, ink, Mathf.Max(1.5f, d * 0.035f));

            switch (_state)
            {
                case SocketState.Filled:
                {
                    Color g = UiSurface.Semantic(this, Role);
                    if (_gem != null)
                        DrawTextureRect(_gem, new Rect2(c - new Vector2(r, r) * 0.68f,
                                                        new Vector2(r, r) * 1.36f), false);
                    else
                    {
                        // A faceted gem: a small polygon with a highlight, not a flat dot.
                        var pts = new Vector2[6];
                        for (int i = 0; i < 6; i++)
                        {
                            float a = -Mathf.Pi * 0.5f + i * Mathf.Tau / 6f;
                            pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.62f;
                        }
                        DrawColoredPolygon(pts, g);
                        var closed = new Vector2[7];
                        pts.CopyTo(closed, 0);
                        closed[6] = pts[0];
                        DrawPolyline(closed, ink, Mathf.Max(1.5f, d * 0.03f));
                        DrawCircle(c - new Vector2(r * 0.22f, r * 0.26f), r * 0.14f,
                                   new Color(1, 1, 1, 0.55f));
                    }
                    break;
                }
                case SocketState.Invite:
                {
                    float a = r * 0.4f, w = Mathf.Max(2f, d * 0.06f);
                    var col = new Color(ink.R, ink.G, ink.B, 0.75f);
                    DrawLine(c - new Vector2(a, 0f), c + new Vector2(a, 0f), col, w);
                    DrawLine(c - new Vector2(0f, a), c + new Vector2(0f, a), col, w);
                    break;
                }
                case SocketState.Locked:
                {
                    float a = r * 0.34f, w = Mathf.Max(2f, d * 0.055f);
                    var col = new Color(0.85f, 0.85f, 0.87f, 0.5f);
                    DrawLine(c - new Vector2(a, a), c + new Vector2(a, a), col, w);
                    DrawLine(c - new Vector2(a, -a), c + new Vector2(a, -a), col, w);
                    if (!string.IsNullOrEmpty(_req) && GetThemeDefaultFont() is { } font)
                    {
                        int s = Mathf.Max(8, Mathf.RoundToInt(fs * 0.6f));
                        Vector2 m = font.GetStringSize(_req, HorizontalAlignment.Left, -1, s);
                        DrawString(font, new Vector2(c.X - m.X * 0.5f, Size.Y - s * 0.1f),
                                   _req, HorizontalAlignment.Left, -1, s, UiSurface.Text(this));
                    }
                    break;
                }
            }
        }
    }
}
