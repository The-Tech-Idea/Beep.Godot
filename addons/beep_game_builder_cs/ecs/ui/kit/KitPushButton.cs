using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A Godot <see cref="Button"/> that draws the kit's chrome instead of a StyleBox.
    ///
    /// The migration drop-in, and the exact counterpart of <see cref="KitPanelContainer"/>: change
    /// nothing but the script and a generic Button becomes a game button. Because it IS a Button,
    /// every <c>Find&lt;Button&gt;</c>, <c>GetNode&lt;Button&gt;</c>, <c>is Button</c> and
    /// <c>btn.Pressed +=</c> in the codebase keeps working — all 48 typed lookups, untouched.
    ///
    /// THE ONE KIT BUTTON
    /// ------------------
    /// There were two. `KitButton` was also `: Button`, also drew the kit's chrome, and differed
    /// only by carrying a badge — while this one carried the corner studs. Two classes on one
    /// Godot base, each holding a feature the other lacked, is a split with nothing behind it:
    /// a scene author had to know which name had which capability, and neither could be chosen
    /// on the merits.
    ///
    /// This class survived because the adoption was here — 98 references across 66 files and
    /// roughly 50 shipped scenes, against 42 in 21 — and the badge came across to it. That
    /// direction cost 22 files to repoint instead of 66, and left every shipped scene untouched.
    ///
    /// A note for anyone reading the old comment in a diff: it claimed the other class derived
    /// from KitControl and so was "NOT a Button". That had stopped being true — both derived from
    /// Button — and the stale claim is exactly what kept the pair looking justified.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitPushButton : Button
    {
        [Export]
        public bool AutoInputDefaults
        {
            get => _autoInputDefaults;
            set { if (_autoInputDefaults == value) return; _autoInputDefaults = value; }
        }
        private bool _autoInputDefaults = true;

        /// <summary>Which palette role this button's plate takes. Accent is the default because
        /// that is what every reference sheet does; set Success/Danger for a confirm or a
        /// destructive action, or Neutral to fall back to the panel surface for a quiet button.</summary>
        [Export]
        public UiSurface.Role Accent
        {
            get => _accent;
            set { if (_accent == value) return; _accent = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _accent = UiSurface.Role.Accent;

        /// <summary>Badge text, e.g. a cost. Empty = no badge. Drawn inside the top-right corner
        /// so ordinary Godot containers can place buttons without overlap.
        ///
        /// Absorbed from KitPushButton, which was a second Button-derived kit button differing from
        /// this one only by carrying a badge. Two classes on one Godot base, one adding nothing
        /// the other could not, is the duplicate shape; the capability moved here, where the
        /// adoption already was.</summary>
        [Export]
        public string BadgeText
        {
            get => _badge;
            set
            {
                string next = value ?? "";
                if (_badge == next) return;
                _badge = next;
                if (IsInsideTree())
                {
                    SuppressBaseChrome();
                    UpdateMinimumSize();
                }
                RefreshVisualAndRedraw();
            }
        }
        private string _badge = "";

        [Export]
        public UiSurface.Role BadgeRole
        {
            get => _badgeRole;
            set { if (_badgeRole == value) return; _badgeRole = value; RefreshVisualAndRedraw(); }
        }
        private UiSurface.Role _badgeRole = UiSurface.Role.Warning;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private KitShape ActiveShape => KitMaterial.ShapeForGenre(_genre);

        private bool _suppressing;
        private bool _eventsHooked;

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            KitChrome.ApplyInputDefaults(this, AutoInputDefaults, focusMode: FocusModeEnum.All);
            SuppressBaseChrome();
            KitChrome.HookButtonChromeRedraw(this, RefreshVisualAndRedraw, ref _eventsHooked);
            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = KitChrome.GenreOf(this);
                SuppressBaseChrome();
                KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
                UpdateMinimumSize();
                RefreshVisualAndRedraw();
            }
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override Vector2 _GetMinimumSize()
        {
            int themeFs = UiSurface.FontSize(this);
            float pad = Mathf.Max(6f, themeFs * 0.7f);
            // Godot calls this to decide the size; reading Size makes the answer depend on itself.
            float frame = Geo.FramePx(themeFs * 2.4f);
            float horizontal = (frame + pad) * 2f;
            float vertical = (frame * 0.5f + pad * 0.4f) * 2f;
            float width = horizontal + themeFs * 4.4f;
            float height = Mathf.Max(themeFs * 2.35f, 30f);

            Font? font = KitChrome.Font(this, _genre);
            if (font != null && !string.IsNullOrEmpty(Text))
            {
                string[] lines = KitChrome.Case(Text, _genre).Split('\n');
                int textFs = UiSurface.FontSize(this, UiSurface.TextRole.Body);
                float longest = 0f;
                foreach (string line in lines)
                    longest = Mathf.Max(longest, font.GetStringSize(line, HorizontalAlignment.Left, -1, textFs).X);

                width = Mathf.Max(width, horizontal + longest);
                height = Mathf.Max(height, vertical + Mathf.Max(1, lines.Length) * textFs * 1.15f);
            }

            Vector2 badge = BadgeSize();
            if (badge.X > 0f)
            {
                width = Mathf.Max(width + badge.X * 0.45f, horizontal + badge.X * 1.35f);
                height = Mathf.Max(height, badge.Y + Mathf.Max(4f, frame * 0.5f + pad * 0.35f));
            }

            Vector2 native = base._GetMinimumSize();
            return new Vector2(Mathf.Max(width, native.X), Mathf.Max(height, native.Y));
        }

        /// <summary>
        /// Blank every state's StyleBox so the base class paints nothing and _Draw owns the look.
        ///
        /// The content margins are kept, because Button sizes its own text from them — zeroing
        /// them collapses the button onto its label. Suppression goes through KitChrome so
        /// unchanged theme overrides are not recreated during Godot's theme-change notifications.
        /// </summary>
        private void SuppressBaseChrome()
        {
            if (_suppressing) return;
            _suppressing = true;

            try
            {
                int fs = UiSurface.FontSize(this);
                float pad = Mathf.Max(6f, fs * 0.7f);
                // Font-derived, never Size: this frame becomes the content margin the minimum size
                // is computed from, so reading Size feeds the button's height back into itself.
                float frame = Geo.FramePx(fs * 2.4f);
                foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                    KitChrome.SetEmptyStyleboxOverride(
                        this,
                        state,
                        frame + pad,
                        frame + pad,
                        // The badge hangs over the plate's TOP corner now, not into the label's
                        // right side, so the room it needs is vertical.
                        frame * 0.5f + pad * 0.4f + (string.IsNullOrEmpty(_badge)
                            ? 0f : KitChrome.BadgeOverhang(this)),
                        frame * 0.5f + pad * 0.4f);
            }
            finally
            {
                _suppressing = false;
            }
            UpdateMinimumSize();
        }

        private KitState CurrentState()
        {
            if (Disabled) return KitState.Disabled;
            if (ButtonPressed || IsPressed()) return KitState.Pressed;
            if (IsHovered()) return KitState.Hover;
            return KitState.Normal;
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;

            var g = Geo;
            KitState state = CurrentState();

            // A BUTTON TAKES THE ACCENT, NOT THE SURFACE.
            //
            // This is the most consistent thing in Example_Art and the kit had it backwards:
            // ui1's yellow Claim, rpgui's gold PLAY, store's green BUY, ui2's orange Select,
            // gameui4/5's red and green actions — every reference button is a SATURATED accent
            // plate sitting on a neutral panel. Drawing buttons in the surface tone made all ten
            // genres read as the same drab plate no matter what their palette said, which is
            // independent of silhouette and was the loudest difference from the reference sheets.
            //
            // The art pass's own settled rule says it: "the palette goes on ONE element, the
            // other stays neutral" (5 references). The panel is the neutral one; this is the one.
            Color plate = UiSurface.SemanticOrDerived(this, Accent);
            if (plate.A < 0.02f) plate = UiSurface.Of(this);   // no semantic palette: stay usable
            Color face = StateFace(plate, state);
            int fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * (fs / 14f));
            var body = new Rect2(Vector2.Zero, Size);

            // The plate gives up the room the badge overhangs into, so the badge can straddle the
            // corner without leaving this control's rect and upsetting a container.
            if (!string.IsNullOrEmpty(_badge))
            {
                float over = KitChrome.BadgeOverhang(this);
                body = new Rect2(body.Position.X, body.Position.Y + over,
                                 Mathf.Max(1f, body.Size.X - over), Mathf.Max(1f, body.Size.Y - over));
            }

            // One shared band walk (KitChrome), not a second copy. The register stack is
            // the kit's definition of what a plate IS; two implementations of it drift.
            KitChrome.DrawPlate(this, _genre, body, face, state, fs / 14f);

            if (g.Studs > 0 && state != KitState.Disabled) Studs(body, g, UiSurface.Ink(face));

            // The label LAST, and drawn by us. A script's _Draw runs AFTER the base class's, so
            // the plate above paints straight over the text Button already drew — every swept
            // button rendered as a blank plate until this was added. Re-drawing it here is the
            // price of owning the chrome on a Button subclass.
            DrawLabel(body, state, face);
            if (state != KitState.Disabled)
                KitChrome.DrawCornerBadge(this, _genre, body, _badge, BadgeRole);
            KitChrome.DrawFocusRing(this, _genre, body, ActiveShape, 0.8f);
        }

        /// <summary>Multi-line aware: several template buttons carry two lines ("Hammer\nx2",
        /// "5\n★★"), and drawing only the first would silently lose half of every one of them.</summary>
        private void DrawLabel(Rect2 body, KitState state, Color face)
        {
            if (string.IsNullOrEmpty(Text)) return;
            var font = KitChrome.Font(this, _genre);
            if (font == null) return;

            string[] lines = KitChrome.Case(Text, _genre).Split('\n');
            string longest = "";
            foreach (string line in lines)
                if (line.Length > longest.Length) longest = line;

            int fs = UiSurface.FitText(this,
                                       body.Size - new Vector2(UiSurface.FontSize(this) * 1.4f,
                                                               UiSurface.FontSize(this) * 0.35f),
                                       lines.Length > 1 ? 0.38f : 0.50f,
                                       longest, font, min: 8, themeMax: 1.08f);
            Color col = UiSurface.Ink(face);
            if (state == KitState.Disabled) col = col with { A = 0.45f };
            // Pressed text shifts with the plate, so the label looks pushed in with it.
            float dy = state == KitState.Pressed ? 1f : 0f;

            float lh = fs * 1.15f;
            float top = body.Position.Y + (body.Size.Y - lh * lines.Length) * 0.5f + fs * 0.82f + dy;
            float textWidth = Mathf.Max(1f, body.Size.X - UiSurface.FontSize(this) * 1.4f);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = KitChrome.EllipsizeText(font, lines[i], fs, textWidth);
                if (string.IsNullOrEmpty(line)) continue;
                Vector2 m = font.GetStringSize(line, HorizontalAlignment.Left, -1, fs);
                KitChrome.DrawText(this, _genre, font,
                           new Vector2(body.Position.X + (body.Size.X - m.X) * 0.5f, top + lh * i),
                           line, fs, col);
            }
        }

        /// <summary>State sculpt, shared with every other drop-in so a converted Button and a
        /// converted CheckButton respond to hover and disable identically.</summary>
        private static Color StateFace(Color s, KitState st) => KitChrome.StateFace(s, st);

        private Vector2 BadgeSize()
        {
            if (string.IsNullOrEmpty(_badge)) return Vector2.Zero;

            int fs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            string badge = KitChrome.Case(_badge, _genre);
            Font? font = KitChrome.Font(this, _genre);
            float textWidth = font?.GetStringSize(badge, HorizontalAlignment.Left, -1, fs).X
                           ?? badge.Length * fs * 0.56f;
            float height = Mathf.Max(fs * 1.45f, 18f);
            float width = Mathf.Clamp(textWidth + fs * 0.95f, height, fs * 5.2f);
            return new Vector2(width, height);
        }


        private void Studs(Rect2 r, KitGeometry g, Color ink)
        {
            float sr = Mathf.Max(1.5f, r.Size.Y * 0.06f);
            float off = Mathf.Max(sr * 1.8f, g.FramePx(r.Size.Y) * 0.55f);
            foreach (var c in new[]
            {
                r.Position + new Vector2(off, off),
                r.Position + new Vector2(r.Size.X - off, off),
                r.Position + new Vector2(off, r.Size.Y - off),
                r.Position + new Vector2(r.Size.X - off, r.Size.Y - off),
            })
            {
                DrawCircle(c, sr, new Color(1, 1, 1, 0.30f));
                DrawArc(c, sr, 0, Mathf.Tau, 12, ink, Mathf.Max(1f, sr * 0.35f));
            }
        }
    }
}
