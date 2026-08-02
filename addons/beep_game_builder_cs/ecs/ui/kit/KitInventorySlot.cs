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
        /// <summary>A slot: takes the theme's slot corner, which the
        /// references vary independently of the button corner.</summary>
        protected override KitWidgetClass WidgetClass => KitWidgetClass.Slot;

        private Texture2D? _icon;
        private int _count;
        private bool _locked;
        private string _requirement = "";
        private bool _selected;
        private bool _hover;
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

        /// <summary>
        /// A GHOST of what belongs here — art-pass file 53's fourth empty state.
        ///
        /// The kit had three: blank, invite-`+`, and locked-with-requirement. The references have
        /// a fourth and it is the most useful one: an equipment slot draws a faded silhouette of
        /// the item type it accepts, so an empty helm slot and an empty boot slot are not the
        /// same grey square. Set this and leave Icon null.
        /// </summary>
        [Export] public Texture2D? GhostIcon
        {
            get => _ghost;
            set { _ghost = value; QueueRedraw(); }
        }
        private Texture2D? _ghost;

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
            MouseFilter = MouseFilterEnum.Stop;
            MouseEntered += () => { _hover = true; QueueRedraw(); };
            MouseExited += () => { _hover = false; QueueRedraw(); };
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
            var font = KitFont();
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
            DrawInset(body, well);

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
            else if (_ghost != null && !Locked)
            {
                // The GHOST: only when the slot is genuinely empty, and never under a padlock --
                // a locked slot already says why it is unavailable, and showing what would go in
                // it as well reads as a filled slot that has been greyed out.
                float pad = Mathf.Max(3f, Mathf.Min(Size.X, Size.Y) * 0.22f);
                var box = new Rect2(pad, pad, Size.X - pad * 2f, Size.Y - pad * 2f);
                DrawTextureRect(_ghost, box, false, new Color(1, 1, 1, 0.16f));
            }
            else if (!Locked)
            {
                DrawEmptyMark(body, ink);
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
                        DrawText(font, new Vector2((Size.X - m.X) * 0.5f, Size.Y - fs * 0.45f),
                                   _requirement, fs, UiSurface.Text(this));
                }
            }
            else if (_count > 1 && font != null)
            {
                DrawCountBadge(body, font, ink);
            }

            // Selection LAST and OUTSIDE the well, so it reads as a frame around the slot rather
            // than a change to it — and drawn from the THEME's declared cues, not a hardcoded
            // white frame. citybuilder and strategy add a glow, cardgame lifts, topdown keeps a
            // plain border; racing3 proves a single cue per widget cannot be right.
            if (_selected)
                KitSelect.Draw(this, g.SelectFor(WidgetClass), KitChrome.Poly(SlotShape, body, g),
                               body, UiSurface.Semantic(this, UiSurface.Role.Accent), rimPx);
            else if (_hover && !Locked)
                KitSelect.Draw(this, g.SelectFor(WidgetClass), KitChrome.Poly(SlotShape, body, g),
                               body, UiSurface.Semantic(this, UiSurface.Role.Info), Mathf.Max(1.5f, rimPx));
        }

        private void DrawInset(Rect2 r, Color well)
        {
            float w = Mathf.Max(1f, Mathf.Min(r.Size.X, r.Size.Y) * 0.035f);
            Color light = new(Mathf.Min(1f, well.R * 1.35f), Mathf.Min(1f, well.G * 1.35f), Mathf.Min(1f, well.B * 1.35f), 0.38f);
            Color shade = new(well.R * 0.45f, well.G * 0.45f, well.B * 0.45f, 0.42f);
            DrawLine(r.Position + new Vector2(w, w), new Vector2(r.End.X - w, r.Position.Y + w), light, w);
            DrawLine(r.Position + new Vector2(w, w), new Vector2(r.Position.X + w, r.End.Y - w), light, w);
            DrawLine(new Vector2(r.Position.X + w, r.End.Y - w), r.End - new Vector2(w, w), shade, w);
            DrawLine(new Vector2(r.End.X - w, r.Position.Y + w), r.End - new Vector2(w, w), shade, w);
        }

        private void DrawEmptyMark(Rect2 r, Color ink)
        {
            Vector2 c = r.Position + r.Size * 0.5f;
            float a = Mathf.Min(r.Size.X, r.Size.Y) * 0.18f;
            Color col = new(ink.R, ink.G, ink.B, 0.24f);
            DrawArc(c, a, 0f, Mathf.Tau, 24, col, Mathf.Max(1.5f, a * 0.18f));
            DrawLine(c - new Vector2(a * 0.55f, 0f), c + new Vector2(a * 0.55f, 0f), col, Mathf.Max(1.5f, a * 0.16f));
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
            DrawText(font, new Vector2(b.Position.X + (b.Size.X - m.X) * 0.5f, b.Position.Y + (b.Size.Y + m.Y * 0.62f) * 0.5f),
                       txt, fs, new Color(0.10f, 0.09f, 0.08f, 1f));
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
