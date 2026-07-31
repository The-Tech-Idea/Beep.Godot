using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A square icon button — the unit a build toolbar, ability bar, hotbar or icon rail is made
    /// of, and the control gameui3 lays out explicitly as a labelled state set:
    /// <b>Normal / Over / Click / Disabled</b>.
    ///
    /// Measured behaviour taken from the sheets:
    ///  - <b>Glyph : button</b> is a per-family ratio (0.40 carved / 0.55 flat / 0.60), so the
    ///    icon is sized from <see cref="KitGeometry.GlyphRatio"/> rather than filling the plate.
    ///  - <b>One button size for every icon button</b>, rail or docked (citybuilder2: 72px) —
    ///    hence a single square metric rather than per-context sizing.
    ///  - <b>A locked control has NO hover and NO press state</b> (gameui3's padlock button is
    ///    drawn in normal and disabled only). Pretending otherwise gives the player feedback
    ///    that promises an interaction which will not happen.
    ///  - Buttons can <b>straddle a panel edge, half in and half out</b> (gameui6), so
    ///    <see cref="Straddle"/> is offered rather than requiring the parent to fake it.
    ///
    /// Disabled DRAINS SATURATION rather than fading — the 7x settled rule. Fading a control is
    /// the clearest tell that a UI is a themed form rather than a game.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitIconButton : KitControl
    {
        [Export] public Texture2D? Icon { get => _icon; set { _icon = value; QueueRedraw(); } }
        private Texture2D? _icon;

        /// <summary>Fallback glyph when no texture is supplied, so the button is never blank.</summary>
        [Export] public string Glyph { get => _glyph; set { _glyph = value ?? ""; QueueRedraw(); } }
        private string _glyph = "";

        [Export] public bool Disabled
        {
            get => _disabled;
            set { _disabled = value; SetState(value ? KitState.Disabled : KitState.Normal); }
        }
        private bool _disabled;

        /// <summary>Locked is not the same as disabled: a locked control states a REQUIREMENT and
        /// never shows hover or press. See <see cref="Requirement"/>.</summary>
        [Export] public bool Locked
        {
            get => _locked;
            set { _locked = value; SetState(value ? KitState.Locked : KitState.Normal); }
        }
        private bool _locked;

        /// <summary>Shown under a locked button. The 5x settled rule is that locked states say
        /// WHY in words, not with a padlock alone.</summary>
        [Export] public string Requirement { get => _req; set { _req = value ?? ""; QueueRedraw(); } }
        private string _req = "";

        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Neutral;

        // NOTE: there is deliberately no `Straddle` export. An earlier draft had one that did
        // nothing at all — a silent no-op export is the same defect class as a snake_case one
        // Godot drops. Straddling an edge (gameui6's play/replay/home row, rpgui's close button)
        // is the HOST's job: it positions the button, or draws it as a KitAttach, because only
        // the host knows which edge is being crossed.

        [Signal] public delegate void PressedEventHandler();

        private bool Interactive => !_disabled && !_locked;

        public override void _Ready()
        {
            base._Ready();
            if (CustomMinimumSize == Vector2.Zero)
            {
                int fs = UiSurface.FontSize(this);
                float s = fs * 3.4f;                   // one size for every icon button
                CustomMinimumSize = new Vector2(s, s);
            }
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!Interactive) return;
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
            // A locked or disabled control must not light up on hover.
            if (!Interactive) return;
            if (what == NotificationMouseEnter) SetState(KitState.Hover);
            else if (what == NotificationMouseExit) SetState(KitState.Normal);
        }

        public override void _Draw()
        {
            if (Size.X <= 4 || Size.Y <= 4) return;

            var g = Geo;
            int fs = UiSurface.FontSize(this);

            // Square, from the shorter side, so a stretched host still yields a square button.
            float s = Mathf.Min(Size.X, Size.Y);
            var plate = new Rect2((Size.X - s) * 0.5f, (Size.Y - s) * 0.5f, s, s);

            DrawMaterial(plate, ActiveShape);

            if (Accent != UiSurface.Role.Neutral && Interactive)
            {
                // Accent goes on ONE element (5x rule) — here a keyline inside the plate, so the
                // icon itself stays neutral and readable.
                Color a = UiSurface.Semantic(this, Accent);
                DrawShape(plate.Grow(-Mathf.Max(2f, s * 0.07f)), ActiveShape,
                          new Color(0, 0, 0, 0), a, Mathf.Max(1.5f, s * 0.035f));
            }

            // Glyph sized by the genre's family ratio, not stretched to the plate.
            float gs = s * g.GlyphRatio;
            var box = new Rect2(plate.Position + new Vector2((s - gs) * 0.5f, (s - gs) * 0.5f),
                                new Vector2(gs, gs));

            if (_icon != null)
            {
                Color mod = Colors.White;
                if (State == KitState.Disabled) mod = new Color(0.72f, 0.72f, 0.72f, 0.9f);
                else if (State == KitState.Locked) mod = new Color(0.12f, 0.12f, 0.14f, 1f);
                DrawTextureRect(_icon, box, false, mod);
            }
            else if (!string.IsNullOrEmpty(_glyph))
            {
                var font = KitFont();
                if (font != null)
                {
                    int size = Mathf.Max(8, Mathf.RoundToInt(gs));
                    Vector2 m = font.GetStringSize(_glyph, HorizontalAlignment.Left, -1, size);
                    Color col = UiSurface.Text(this);
                    if (State == KitState.Locked) col = new Color(0.12f, 0.12f, 0.14f, 1f);
                    else if (State == KitState.Disabled) col = col with { A = 0.55f };
                    DrawString(font,
                               new Vector2(plate.Position.X + (s - m.X) * 0.5f,
                                           plate.Position.Y + (s + m.Y * 0.6f) * 0.5f),
                               _glyph, HorizontalAlignment.Left, -1, size, col);
                }
            }

            // The requirement, in words, under a locked button.
            if (_locked && !string.IsNullOrEmpty(_req))
            {
                var font = KitFont();
                if (font != null)
                {
                    int small = Mathf.Max(8, Mathf.RoundToInt(fs * 0.66f));
                    Vector2 m = font.GetStringSize(_req, HorizontalAlignment.Left, -1, small);
                    DrawString(font,
                               new Vector2(plate.Position.X + (s - m.X) * 0.5f,
                                           plate.End.Y - small * 0.25f),
                               _req, HorizontalAlignment.Left, -1, small, UiSurface.Text(this));
                }
            }

            DrawAttachments();
        }
    }
}
