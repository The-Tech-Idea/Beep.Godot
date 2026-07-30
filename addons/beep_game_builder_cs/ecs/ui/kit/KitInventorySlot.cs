using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// ONE inventory slot, populated from the inspector.
    ///
    /// WHY THIS EXISTS ALONGSIDE <see cref="KitSlotGrid"/>
    /// --------------------------------------------------
    /// KitSlotGrid holds a `List&lt;Slot&gt;` of plain C# objects, which means a slot's icon and
    /// count can only be set from CODE. That is right for a bag whose contents come from the
    /// game at runtime, and useless for laying out a screen: a developer building an inventory
    /// panel in the editor has no way to drop in a slot and give it a texture and a count.
    ///
    /// This is the drag-and-drop counterpart. Add it under any container, assign
    /// <see cref="Icon"/> and <see cref="Count"/> in the inspector, and it draws itself — the
    /// recessed well, the item, the count badge, the rarity rim, the locked state and its
    /// requirement — all in the active genre's material.
    ///
    /// The framework ships no item art, so <see cref="Icon"/> is deliberately allowed to be
    /// null: an empty slot is a legitimate state, not a misconfiguration, and it draws as an
    /// empty well rather than warning.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitInventorySlot : KitControl
    {
        private Texture2D? _icon;
        private int _count;
        private bool _locked;
        private string _requirement = "";
        private bool _selected;
        private UiSurface.Role _rarity = UiSurface.Role.Neutral;

        /// <summary>The item's art. Null = an empty slot, which is a normal state.</summary>
        [Export] public Texture2D? Icon
        {
            get => _icon;
            set { _icon = value; QueueRedraw(); }
        }

        /// <summary>Stack size. 0 or 1 draws no badge — a badge reading "1" on every slot is
        /// noise, and none of the reference sheets do it.</summary>
        [Export] public int Count
        {
            get => _count;
            set { _count = Mathf.Max(0, value); QueueRedraw(); }
        }

        /// <summary>Rarity, as a palette ROLE rather than a colour, so a slot reskins with the
        /// theme instead of pinning a literal into the scene.</summary>
        [Export] public UiSurface.Role Rarity
        {
            get => _rarity;
            set { _rarity = value; QueueRedraw(); }
        }

        /// <summary>Locked slots say WHY, in words — see <see cref="Requirement"/>. A padlock
        /// alone is the one thing the reference kits consistently do NOT do.</summary>
        [Export] public bool Locked
        {
            get => _locked;
            set { _locked = value; QueueRedraw(); }
        }

        [Export] public string Requirement
        {
            get => _requirement;
            set { _requirement = value ?? ""; QueueRedraw(); }
        }

        [Export] public bool Selected
        {
            get => _selected;
            set { _selected = value; QueueRedraw(); }
        }

        /// <summary>Emitted on click. The slot reports; the game decides what a click means.</summary>
        [Signal] public delegate void SlotPressedEventHandler();

        /// <summary>
        /// A slot's silhouette, with the EXOTIC genre shapes tamed.
        ///
        /// A slot is a container for someone else's art, drawn in a grid, and the shapes that
        /// give a button its identity make a terrible slot: rpg's `Spiked` hung triangular points
        /// off the bottom of every slot in the grid, and `Torn`/`Capsule`/`Shield`/`Ellipse` are
        /// no better in a tiled row. The genre still shows through the corner radius, frame,
        /// material and rim — just not through a silhouette that only reads as a one-off plate.
        ///
        /// `OverrideShape` still wins, so a developer who wants the spikes can have them.
        /// </summary>
        private KitShape SlotShape
        {
            get
            {
                if (OverrideShape) return Shape;
                return ActiveShape switch
                {
                    KitShape.Spiked or KitShape.Torn or KitShape.Capsule
                        or KitShape.Shield or KitShape.Ellipse or KitShape.Pill
                        or KitShape.Arch or KitShape.Arrow or KitShape.Chevron
                        or KitShape.Parallelogram or KitShape.Pentagon
                        or KitShape.Ribbon or KitShape.Speed => KitShape.Round,
                    _ => ActiveShape,
                };
            }
        }

        public override void _Ready()
        {
            base._Ready();
            // A slot is square by default and big enough for its own badge to be legible.
            int fs = UiSurface.FontSize(this);
            float side = Mathf.Max(48f, fs * 3.4f);
            if (CustomMinimumSize == Vector2.Zero)
                CustomMinimumSize = new Vector2(side, side);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (Locked) return;
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                EmitSignal(SignalName.SlotPressed);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            if (Size.X < 6f || Size.Y < 6f) return;

            var g = Geo;
            var font = GetThemeDefaultFont();
            Color surface = UiSurface.Of(this);
            Color ink = UiSurface.Ink(surface);
            var body = new Rect2(Vector2.Zero, Size);

            // The WELL is recessed: a slot is a hole you put a thing in, not a raised plate.
            // WellShade exists precisely because reusing the readout's Recessed shade (0.12) drew
            // slots as black holes.
            Color well = new(surface.R * g.WellShade,
                             surface.G * g.WellShade,
                             surface.B * g.WellShade, 1f);
            if (Locked) well = new Color(well.R * 0.82f, well.G * 0.82f, well.B * 0.86f, 1f);

            float rimPx = Mathf.Max(1f, g.Rim * 0.6f);
            KitChrome.Fill(this, SlotShape, body, g, well, ink, rimPx);

            // Rarity reads as a RIM, not a fill — the settled "palette goes on ONE element" rule,
            // and it keeps the item art readable against its own slot.
            if (_rarity != UiSurface.Role.Neutral && !Locked)
            {
                Color rc = UiSurface.Semantic(this, _rarity);
                KitChrome.Fill(this, SlotShape, KitChrome.Inset(body, rimPx),
                               g, new Color(0, 0, 0, 0), rc, Mathf.Max(2f, rimPx * 1.6f));
            }

            if (_icon != null)
            {
                float pad = Mathf.Max(3f, Mathf.Min(Size.X, Size.Y) * 0.16f);
                var box = new Rect2(pad, pad, Size.X - pad * 2f, Size.Y - pad * 2f);
                var mod = Locked ? new Color(1, 1, 1, 0.35f) : Colors.White;
                DrawTextureRect(_icon, box, false, mod);
            }

            if (Locked)
            {
                DrawPadlock(body, ink);
                if (!string.IsNullOrEmpty(_requirement) && font != null)
                {
                    // INSIDE the slot, at the bottom. Drawn below it, the text collided with the
                    // next slot in the grid -- a standalone widget must stay within its own rect.
                    int fs = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                               new Vector2(Size.X * 0.94f, Size.Y * 0.24f),
                                               _requirement, font, min: 7);
                    Vector2 m = font.GetStringSize(_requirement, HorizontalAlignment.Left, -1, fs);
                    if (m.X <= Size.X * 0.98f)
                        DrawString(font,
                                   new Vector2((Size.X - m.X) * 0.5f, Size.Y - fs * 0.45f),
                                   _requirement, HorizontalAlignment.Left, -1, fs,
                                   UiSurface.Text(this));
                }
            }
            else if (_count > 1 && font != null)
            {
                DrawCountBadge(body, font, ink);
            }

            // Selection LAST and OUTSIDE the well, so it reads as a frame around the slot rather
            // than a change to the slot itself.
            if (_selected)
            {
                var sel = new Rect2(body.Position - new Vector2(2f, 2f),
                                    body.Size + new Vector2(4f, 4f));
                KitChrome.Fill(this, SlotShape, sel, g, new Color(0, 0, 0, 0),
                               new Color(1f, 1f, 1f, 0.92f), Mathf.Max(2f, rimPx * 1.4f));
            }
        }

        /// <summary>Bottom-right, straddling the corner — where every reference sheet puts it.
        /// Sized off the SLOT so it stays legible at any slot size.</summary>
        private void DrawCountBadge(Rect2 r, Font font, Color ink)
        {
            string txt = _count > 999 ? "999+" : _count.ToString();
            int fs = UiSurface.FitRole(this, UiSurface.TextRole.Small,
                                       new Vector2(r.Size.X * 0.58f, r.Size.Y * 0.40f),
                                       txt, font, min: 9);
            Vector2 m = font.GetStringSize(txt, HorizontalAlignment.Left, -1, fs);
            float w = Mathf.Max(m.X + fs * 0.7f, fs * 1.4f), h = fs * 1.3f;
            // Straddle only slightly: at 0.62 most of the pill sat outside the control and
            // came out clipped. 0.92 keeps it inside while still reading as a corner badge.
            var b = new Rect2(r.End.X - w * 0.92f, r.End.Y - h * 0.92f, w, h);

            KitChrome.Fill(this, KitShape.Pill, b, Geo,
                           UiSurface.Semantic(this, UiSurface.Role.Warning), ink,
                           Mathf.Max(1.5f, fs * 0.10f));
            DrawString(font,
                       new Vector2(b.Position.X + (b.Size.X - m.X) * 0.5f,
                                   b.Position.Y + (b.Size.Y + m.Y * 0.62f) * 0.5f),
                       txt, HorizontalAlignment.Left, -1, fs,
                       new Color(0.10f, 0.09f, 0.08f, 1f));
        }

        private void DrawPadlock(Rect2 r, Color ink)
        {
            float s = Mathf.Min(r.Size.X, r.Size.Y) * 0.26f;
            var c = r.Position + r.Size * 0.5f;
            var bodyRect = new Rect2(c.X - s * 0.5f, c.Y - s * 0.1f, s, s * 0.72f);
            DrawRect(bodyRect, ink);
            DrawArc(new Vector2(c.X, c.Y - s * 0.1f), s * 0.30f,
                    Mathf.Pi, Mathf.Tau, 14, ink, Mathf.Max(1.5f, s * 0.16f));
        }
    }
}
