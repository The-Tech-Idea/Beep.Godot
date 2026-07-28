using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A progress/resource meter that is SEGMENTED by default.
    ///
    /// "**Segmented progress is the default, continuous is the exception**" is one of the art
    /// pass's settled rules, measured across seven independent references (gameui1-4, rpg1, rpg2,
    /// rpgui1). Every meter this framework shipped before was a continuous bar, which is the
    /// exception being used as the rule.
    ///
    /// Two more settled rules are built in rather than left to the caller:
    ///  - "**Empty/track = a dark tint of the surface's own HUE, never grey**" (4 references).
    ///    A grey track is the single clearest tell of a themed form; the track must carry the
    ///    same hue as the fill so the meter reads as one object.
    ///  - "**The palette goes on ONE element**, the other stays neutral" (5 references) — so the
    ///    fill takes the role colour and the track is derived from it, not separately themed.
    ///
    /// The optional end CAP comes from gameui6 and rpgui.md's finding that on that sheet
    /// "variation lives in the END CAPS, not the body — six bars, one track". The cap is a
    /// KitAttach so it can overhang the bar, which is how every reference draws it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitMeter : KitControl
    {
        [Export(PropertyHint.Range, "0.0,1.0,0.001")]
        public float Value { get => _value; set { _value = Mathf.Clamp(value, 0f, 1f); QueueRedraw(); } }
        private float _value = 0.62f;

        /// <summary>Number of segments. 0 makes the meter continuous — deliberately available,
        /// but deliberately NOT the default.</summary>
        [Export(PropertyHint.Range, "0,40,1")]
        public int Segments { get => _segments; set { _segments = Mathf.Max(0, value); QueueRedraw(); } }
        private int _segments = 10;

        [Export] public UiSurface.Role Fill { get; set; } = UiSurface.Role.Success;

        /// <summary>Icon pinned over the bar's leading end, overhanging it. Optional.</summary>
        [Export] public Texture2D? CapIcon { get => _cap; set { _cap = value; Rebuild(); } }
        private Texture2D? _cap;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                // HUD rail height : text cap-height = 2.6 (citybuilder1), and the rail itself is
                // ~3% of screen height across five references. Sized off the type so it holds.
                CustomMinimumSize = new Vector2(fs * 10f, fs * 1.15f);
            }
            Rebuild();
        }

        private void Rebuild()
        {
            Attachments.Clear();
            if (_cap != null)
            {
                int fs = UiSurface.FontSize(this);
                Attachments.Add(new KitAttach
                {
                    Anchor = KitAnchor.MiddleLeft,
                    Size = new Vector2(fs * 1.6f, fs * 1.6f),
                    Shape = KitShape.Round,
                    Role = Fill,
                    Icon = _cap,
                    Overhang = 0.5f,
                });
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (Size.X <= 4 || Size.Y <= 4) return;

            var g = Geo;
            Color ink = InkColor();
            Color fill = UiSurface.Semantic(this, Fill);

            // The track is the fill's own hue driven dark — never a neutral grey.
            Color track = new Color(fill.R * 0.26f, fill.G * 0.26f, fill.B * 0.30f, 1f);

            // The cap overhangs, so the bar is inset to leave it room.
            float inset = _cap != null ? UiSurface.FontSize(this) * 0.55f : 0f;
            var bar = new Rect2(inset, 0, Size.X - inset, Size.Y);
            if (bar.Size.X <= 2) return;

            float rimPx = Mathf.Max(1f, g.Rim * 0.6f * (UiSurface.FontSize(this) / 14f));
            DrawShape(bar, ActiveShape, track, ink, rimPx);

            if (_value > 0f)
            {
                if (_segments <= 0)
                {
                    var f = new Rect2(bar.Position, new Vector2(bar.Size.X * _value, bar.Size.Y));
                    if (f.Size.X > 1) DrawShape(f, ActiveShape, fill, ink, 0f);
                }
                else
                {
                    // Gap scales with the bar so segments stay legible at any size; a fixed pixel
                    // gap disappears on a HUD rail and gapes on a full-width bar.
                    float gap = Mathf.Max(1f, bar.Size.Y * 0.14f);
                    float segW = (bar.Size.X - gap * (_segments - 1)) / _segments;
                    if (segW > 0.5f)
                    {
                        float lit = _value * _segments;
                        for (int i = 0; i < _segments; i++)
                        {
                            float amount = Mathf.Clamp(lit - i, 0f, 1f);
                            if (amount <= 0.001f) break;
                            var s = new Rect2(bar.Position.X + i * (segW + gap), bar.Position.Y,
                                              segW * amount, bar.Size.Y);
                            if (s.Size.X > 0.5f) DrawShape(s, ActiveShape, fill, ink, 0f);
                        }
                    }
                }
            }

            DrawAttachments();
        }
    }
}
