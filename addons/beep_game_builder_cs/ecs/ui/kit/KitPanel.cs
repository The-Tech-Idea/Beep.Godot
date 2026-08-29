using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// A panel: frame, a RECESSED inner well, and an optional overhanging title banner.
    ///
    /// The structure is the one PLAN.md 4.2a extracted from the reference sheets — "a game
    /// control is a FRAME around an INNER PLATE, two nested shapes, not one plate with a bevel"
    /// — plus the banner, which the art pass counts as the most repeated element in the folder.
    /// A Godot `PanelContainer` can express none of it: one StyleBox, one rectangle, and a title
    /// that must sit inside the box rather than across its edge.
    ///
    /// The well is inset to <b>0.79-0.80 x</b> the host, a ratio two unrelated families produced
    /// independently (citybuilder3's tiles and gameui1's parchment slots), and is drawn at the
    /// RECESSED plate shade so it reads as carved into the frame rather than laid on top of it.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitPanel : Panel
    {
        /// <summary>A panel: takes the theme's panel corner, which the
        /// references vary independently of the button corner.</summary>
        private const KitWidgetClass Class = KitWidgetClass.Panel;

        private string _genre = "";
        private KitGeometry Geo => KitGeometry.ForGenre(_genre);
        private bool _suppressing;
        private bool _eventsHooked;
        private readonly System.Collections.Generic.List<KitAttach> Attachments = new();

        /// <summary>State is fixed for a panel -- it is not an interactive control.</summary>
        private const KitState State = KitState.Normal;

        /// <summary>
        /// What this screen IS. Declaring it places the archetype's ornaments — a crown on a
        /// victory panel, a gear on settings — so the screen is recognisable before it is read.
        /// See <see cref="KitArchetypes"/>.
        /// </summary>
        [Export]
        public KitArchetype Archetype
        {
            get => _archetype;
            set { _archetype = value; if (IsNodeReady()) ApplyArchetypeOrnaments(); }
        }
        private KitArchetype _archetype = KitArchetype.None;

        /// <summary>
        /// Legacy fallback for old scenes that expected KitPanel to create ornament children.
        /// Disabled by default so ornaments remain real design-time nodes that can be selected,
        /// renamed, positioned, and styled in the Godot scene tree.
        /// </summary>
        [Export]
        public bool GenerateOrnamentsWhenMissing
        {
            get => _generateOrnamentsWhenMissing;
            set { if (_generateOrnamentsWhenMissing == value) return; _generateOrnamentsWhenMissing = value; if (IsNodeReady()) ApplyArchetypeOrnaments(); }
        }
        private bool _generateOrnamentsWhenMissing;

        [Export] public string Title { get => _title; set { SetText(ref _title, value); } }
        private string _title = "";
        [Export]
        public KitPanelIntent Intent
        {
            get => _intent;
            set { if (_intent == value) return; _intent = value; RefreshPanelChrome(); }
        }
        private KitPanelIntent _intent = KitPanelIntent.Sheet;
        [Export] public KitPanelHeaderStyle HeaderStyle { get => _headerStyle; set { if (_headerStyle == value) return; _headerStyle = value; RefreshPanelChrome(); } }
        private KitPanelHeaderStyle _headerStyle = KitPanelHeaderStyle.UtilityStrip;
        [Export(PropertyHint.Range, "0.45,1.4,0.01")]
        public float TitleFontScale { get => _titleFontScale; set { if (Mathf.IsEqualApprox(_titleFontScale, value)) return; _titleFontScale = value; RefreshPanelChrome(); } }
        private float _titleFontScale = 0.90f;

        /// <summary>Banner silhouette. Plaque/Ribbon/Shield/Ellipse are the four the reference
        /// kits use; the genre picks one unless this is overridden.</summary>
        [Export]
        public bool OverrideBannerShape
        {
            get => _overrideBannerShape;
            set { if (_overrideBannerShape == value) return; _overrideBannerShape = value; RefreshVisualAndRedraw(); }
        }
        private bool _overrideBannerShape;

        [Export]
        public KitShape BannerShape
        {
            get => _bannerShape;
            set { if (_bannerShape == value) return; _bannerShape = value; RefreshVisualAndRedraw(); }
        }
        private KitShape _bannerShape = KitShape.Rect;

        /// <summary>Banner lightness as a multiple of the frame. 0.44 (gameui2) reads recessed;
        /// values above 1 give gameui4's white plate. Polarity is per-family, so it is exposed.</summary>
        [Export(PropertyHint.Range, "0.1,1.6,0.01")]
        public float BannerShade
        {
            get => _bannerShade;
            set
            {
                if (Mathf.Abs(_bannerShade - value) < 0.001f) return;
                _bannerShade = value;
                RefreshVisualAndRedraw();
            }
        }
        private float _bannerShade = 0.44f;

        /// <summary>Draw the inner well. Off gives a plain framed plate.</summary>
        [Export]
        public bool ShowWell
        {
            get => _showWell;
            set { if (_showWell == value) return; _showWell = value; RefreshVisualAndRedraw(); }
        }
        private bool _showWell = true;

        /// <summary>
        /// Size the frame from the cluster it wraps, instead of filling its parent.
        ///
        /// Ported from <see cref="PanelFrameComponent"/>, whose comments record why each part is
        /// the way it is. Without this a KitPanel dropped into a PanelContainer stretched to the
        /// full container and its banner drifted to the screen edge — which is exactly what
        /// happened the first time this was swapped in on puzzle/level_map.
        /// </summary>
        [Export]
        public NodePath TargetPath
        {
            get => _targetPath;
            set
            {
                if (_targetPath == value) return;
                _targetPath = value ?? new NodePath("");
                _target = null;
                UpdateTargetFitProcessing();
                if (IsInsideTree() && !Engine.IsEditorHint() && !_targetPath.IsEmpty)
                    CallDeferred(nameof(ResolveTarget));
                RefreshVisualAndRedraw();
            }
        }
        private NodePath _targetPath = new("");

        /// <summary>Slack around the target. Top is separate because the banner needs the room.</summary>
        [Export]
        public Vector2 TargetPadding
        {
            get => _targetPadding;
            set { if (_targetPadding == value) return; _targetPadding = value; RefreshVisualAndRedraw(); }
        }
        private Vector2 _targetPadding = new(14, 12);

        private Godot.Control? _target;

        private void ResolveTarget() => _target = GetNodeOrNull<Godot.Control>(TargetPath);

        private bool ShouldFitTarget()
            => !_targetPath.IsEmpty && IsVisibleInTree();

        private void UpdateTargetFitProcessing()
        {
            if (IsInsideTree())
                SetProcess(ShouldFitTarget());
        }

        private void ApplyArchetypeOrnaments()
            => KitArchetypes.Apply(this, _archetype, GenerateOrnamentsWhenMissing);

        private void ApplyMouseFilterDefault()
        {
            if (!AutoMouseFilter)
                return;

            MouseFilter = ShowClose ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        }

        private void ApplyCloseFocusDefault()
        {
            if (!AutoFocusDefault)
                return;

            FocusMode = ShowClose ? FocusModeEnum.All : FocusModeEnum.None;
        }

        private void SetText(ref string target, string? value)
        {
            string next = value ?? "";
            if (target == next) return;
            target = next;
            RefreshPanelChrome();
        }

        private void RefreshPanelChrome()
        {
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            QueueRedraw();
        }

        private void RefreshVisualAndRedraw()
        {
            QueueRedraw();
        }

        public override void _Process(double delta)
        {
            if (!ShouldFitTarget())
            {
                UpdateTargetFitProcessing();
                return;
            }
            if (_target == null || !GodotObject.IsInstanceValid(_target)) { ResolveTarget(); return; }
            if (GetParent() is not Godot.Control) return;

            // CombinedMinimumSize, never Size: the cluster is usually an anchored container that
            // stretches to the full screen width while its CONTENT is narrow, so sizing from Size
            // produced a 1348px frame around a 200px stat list.
            Vector2 need = _target.GetCombinedMinimumSize();

            // The header occupies space before the content well. Banner headers overhang the
            // frame; utility headers sit inside it. Both still need to be part of the target-fit
            // ask, or the last content row ends up under the chrome.
            float headerRoom = HeaderRoom();
            Vector2 size = new(need.X + TargetPadding.X, need.Y + TargetPadding.Y + headerRoom);

            // Size ONLY. Position stays where the scene put it — deriving it from an anchored
            // target lands at the parent origin rather than at the visible content.
            if (Size != size)
            {
                Size = size;
                KitChrome.RefreshAutoMinimumSize(this, size);
                QueueRedraw();
            }
        }

        /// <summary>Section D's `TornPanel` — an irregular torn edge along the bottom, for the
        /// paper/parchment register. A panel VARIANT rather than its own class: the structure is
        /// identical and only the bottom edge changes.</summary>
        [Export]
        public bool TornEdge
        {
            get => _tornEdge;
            set { if (_tornEdge == value) return; _tornEdge = value; RefreshVisualAndRedraw(); }
        }
        private bool _tornEdge;

        /// <summary>Section D's `CornerClose` — an X straddling the frame's top-right corner
        /// (rpgui's title bar has "a close button attached at the right end"). Drawn by the HOST
        /// so it can cross the frame's edge, which is the whole reason it is not a child button.</summary>
        [Export]
        public bool ShowClose
        {
            get => _showClose;
            set
            {
                if (_showClose == value) return;
                _showClose = value;
                ApplyMouseFilterDefault();
                ApplyCloseFocusDefault();
                RefreshVisualAndRedraw();
            }
        }
        private bool _showClose;

        /// <summary>
        /// Apply the kit's default panel mouse behavior: ignore clicks for background chrome,
        /// stop clicks only when the drawn close button is enabled. Turn this off when the scene
        /// deliberately authors MouseFilter for a custom hit target or pass-through region.
        /// </summary>
        [Export]
        public bool AutoMouseFilter
        {
            get => _autoMouseFilter;
            set { if (_autoMouseFilter == value) return; _autoMouseFilter = value; ApplyMouseFilterDefault(); }
        }
        private bool _autoMouseFilter = true;

        [Export]
        public bool AutoFocusDefault
        {
            get => _autoFocusDefault;
            set { if (_autoFocusDefault == value) return; _autoFocusDefault = value; ApplyCloseFocusDefault(); }
        }
        private bool _autoFocusDefault = true;

        [Signal] public delegate void CloseRequestedEventHandler();

        private Rect2 CloseRect()
        {
            float s = Mathf.Max(16f, UiSurface.FontSize(this) * 1.7f);
            Rect2 b = BodyRect();
            return new Rect2(b.End.X - s * 0.55f, b.Position.Y - s * 0.40f, s, s);
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!ShowClose) return;
            if (@event is InputEventKey key && (KitChrome.IsConfirmKey(key) || KitChrome.IsCancelKey(key)))
            {
                EmitSignal(SignalName.CloseRequested);
                AcceptEvent();
                return;
            }

            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb
                && CloseRect().HasPoint(mb.Position))
            {
                GrabFocus();
                EmitSignal(SignalName.CloseRequested);
                AcceptEvent();
            }
        }

        /// <summary>Panel paints a `panel` StyleBox of its own; ours must REPLACE it, not sit on
        /// top of it, or every kit panel carries Godot's default grey plate underneath.</summary>
        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            KitChrome.SetEmptyStyleboxOverride(this, "panel");
            _suppressing = false;
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationVisibilityChanged)
                UpdateTargetFitProcessing();
            if (what != NotificationThemeChanged) return;
            _genre = KitChrome.GenreOf(this);
            Suppress();
            KitChrome.RefreshAutoMinimumSize(this, _GetMinimumSize());
            UpdateMinimumSize();
            RefreshVisualAndRedraw();
        }

        public override void _Ready()
        {
            base._Ready();
            _genre = KitChrome.GenreOf(this);
            Suppress();
            // Placed after the panel has a size -- the ornaments are sized from its short edge,
            // and at _Ready on a container-laid-out panel that is still zero.
            if (!_eventsHooked)
            {
                Resized += ApplyArchetypeOrnaments;
                _eventsHooked = true;
            }
            ApplyArchetypeOrnaments();

            // A KitPanel is BACKGROUND ART. Used the way PanelFrameComponent is -- dropped inside
            // a PanelContainer whose own stylebox is blanked, so the container still lays out the
            // content and the kit draws the chrome -- it sits UNDER that content, and a Control
            // defaults to MouseFilter.Stop. Left alone it would swallow every click meant for the
            // buttons on top of it. PanelFrameComponent has carried this line since it was
            // written ("background art, never a click target"); the kit version needs it too.
            //
            // The exception is ShowClose: that draws an interactive X straddling the frame, so
            // the panel must be able to receive that one click.
            ApplyMouseFilterDefault();
            ApplyCloseFocusDefault();
            UpdateTargetFitProcessing();

            if (!Engine.IsEditorHint() && !TargetPath.IsEmpty)
                CallDeferred(nameof(ResolveTarget));

            KitChrome.SetAutoMinimumSize(this, _GetMinimumSize());
        }

        /// <summary>Ribbon for the wood/adventure genres, ellipse for the candy ones, plaque
        /// otherwise — the same register mapping PanelFrameComponent settled on in Stage 32c,
        /// kept identical so a kit panel and a framed legacy screen agree.</summary>
        private KitShape ResolvedBannerShape()
        {
            if (OverrideBannerShape) return BannerShape;
            return Geo.Register switch
            {
                KitRegister.Carved => KitShape.Ribbon,
                KitRegister.Casual => KitShape.Ellipse,
                _ => KitShape.Rect,
            };
        }

        /// <summary>The content rect a caller should lay children out inside — the well, minus
        /// the banner's intrusion. Public so a screen does not have to re-derive the insets and
        /// drift from them.</summary>
        /// <summary>An irregular saw-tooth along the bottom, seeded from the panel's own width so
        /// it is stable across redraws — a torn edge that reshuffles every frame reads as noise.</summary>
        private void DrawTornEdge(Rect2 body, Color face, Color ink)
        {
            int teeth = Mathf.Max(6, Mathf.RoundToInt(body.Size.X / 18f));
            float h = Mathf.Max(4f, body.Size.Y * 0.045f);
            uint seed = (uint)Mathf.RoundToInt(body.Size.X * 7f + teeth);
            var pts = new System.Collections.Generic.List<Vector2>
            {
                new(body.Position.X, body.End.Y - h),
            };
            for (int i = 0; i <= teeth; i++)
            {
                seed = seed * 1664525u + 1013904223u;              // stable LCG, no Math.Random
                float jitter = ((seed >> 16) & 0xFF) / 255f;
                float x = Mathf.Lerp(body.Position.X, body.End.X, i / (float)teeth);
                pts.Add(new Vector2(x, body.End.Y - h + (i % 2 == 0 ? h * jitter : h * (1f + jitter * 0.4f))));
            }
            pts.Add(new Vector2(body.End.X, body.End.Y - h));
            DrawColoredPolygon(pts.ToArray(), face);
            DrawPolyline(pts.ToArray(), ink, 1.5f);
        }

        private void DrawClose(Color ink, float rimPx)
        {
            Rect2 r = CloseRect();
            Color c = UiSurface.SemanticOrDerived(this, UiSurface.Role.Danger);
            KitChrome.DrawShape(this, _genre, r, KitShape.Round, c, ink, Mathf.Max(1.5f, rimPx * 0.8f));
            var ctr = r.Position + r.Size * 0.5f;
            float a = r.Size.X * 0.22f, w = Mathf.Max(2f, r.Size.X * 0.09f);
            var on = UiSurface.Luminance(c) > 0.5f
                ? new Color(0.10f, 0.09f, 0.08f) : new Color(0.98f, 0.96f, 0.92f);
            DrawLine(ctr - new Vector2(a, a), ctr + new Vector2(a, a), on, w);
            DrawLine(ctr - new Vector2(a, -a), ctr + new Vector2(a, -a), on, w);
        }

        private void DrawWellInset(Rect2 r, Color sunk)
        {
            float w = Mathf.Max(1f, Mathf.Min(r.Size.X, r.Size.Y) * 0.012f);
            Color light = new(Mathf.Min(1f, sunk.R * 1.35f), Mathf.Min(1f, sunk.G * 1.35f), Mathf.Min(1f, sunk.B * 1.35f), 0.28f);
            Color shade = new(sunk.R * 0.45f, sunk.G * 0.45f, sunk.B * 0.45f, 0.35f);
            DrawLine(r.Position + new Vector2(w, w), new Vector2(r.End.X - w, r.Position.Y + w), light, w);
            DrawLine(r.Position + new Vector2(w, w), new Vector2(r.Position.X + w, r.End.Y - w), light, w);
            DrawLine(new Vector2(r.Position.X + w, r.End.Y - w), r.End - new Vector2(w, w), shade, w);
            DrawLine(new Vector2(r.End.X - w, r.Position.Y + w), r.End - new Vector2(w, w), shade, w);
        }

        public Rect2 ContentRect()
        {
            Rect2 body = BodyRect();
            float ft = Geo.FramePx(body.Size.Y);
            float headerRoom = HeaderStyle == KitPanelHeaderStyle.UtilityStrip
                ? KitChrome.PanelHeaderRoom(this, _genre, _title, HeaderStyle, TitleFontScale, Size.Y)
                : 0f;
            return new Rect2(body.Position + new Vector2(ft, ft + headerRoom),
                             new Vector2(Mathf.Max(0f, body.Size.X - ft * 2f),
                                         Mathf.Max(0f, body.Size.Y - ft * 2f - headerRoom)));
        }

        private float HeaderRoom()
            => KitChrome.PanelHeaderRoom(this, _genre, _title, HeaderStyle, TitleFontScale, Size.Y);

        /// <summary>Amount the body is pushed down to keep an overhanging banner inside bounds.</summary>
        private float HeaderOverhang()
            => Intent == KitPanelIntent.Hud
                ? 0f
                : KitChrome.PanelHeaderOverhang(this, _genre, _title, HeaderStyle,
                                                TitleFontScale, Size.Y);

        /// <summary>
        /// The frame, inset from the top by the banner's overhang.
        ///
        /// The banner straddles the FRAME's edge — the measured behaviour — but the whole widget
        /// stays inside its own rect, because a Container reserves space from the control's size
        /// and knows nothing about anything drawn outside it. Drawing the banner at a negative y
        /// instead put it on top of whatever sat above: in kit_gallery.tscn the EQUIPMENT banner
        /// covered the COMBO stat row in the HBox above it.
        /// </summary>
        private Rect2 BodyRect()
        {
            float o = HeaderOverhang();
            return new Rect2(0f, o, Size.X, Mathf.Max(4f, Size.Y - o));
        }

        /// <summary>Containers size from here, so the banner's headroom is part of the ask
        /// rather than something the panel silently borrows from its neighbour.</summary>
        public override Vector2 _GetMinimumSize()
        {
            var b = base._GetMinimumSize();
            return new Vector2(b.X, b.Y + HeaderRoom());
        }

        public override void _Draw()
        {
            if (Size.X <= 8 || Size.Y <= 8) return;

            var g = Geo;
            Rect2 body = BodyRect();
            Color face = UiSurface.Of(this);
            Color ink = UiSurface.Ink(UiSurface.Of(this));
            float fs = UiSurface.FontSize(this);
            float rimPx = Mathf.Max(1f, g.Rim * (fs / 14f));

            if (Intent == KitPanelIntent.Hud)
            {
                var shape = KitMaterial.PanelShapeForGenre(_genre, Intent);
                Color plate = new Color(face.R * 0.82f, face.G * 0.82f, face.B * 0.84f, Mathf.Min(0.90f, face.A));
                KitChrome.DrawShape(this, _genre, body, shape, plate, ink with { A = 0.50f },
                                    Mathf.Clamp(rimPx * 0.42f, 1f, 2.5f));
                KitChrome.DrawAttachments(this, _genre, Attachments);
                return;
            }

            // Frame.
            KitChrome.DrawShape(this, _genre, body, KitChrome.Shape(_genre, KitWidgetClass.Panel), face,
                                KitChrome.Rim(UiSurface.Of(this), Geo), rimPx, KitWidgetClass.Panel);

            if (ShowWell)
            {
                // Well inset. The measured 0.79-0.80 x host came off large reference panels; at
                // the sizes the kit actually gets used it ate the interior, leaving a thick band
                // of frame around a small well. Now ~0.88-0.90 x: the frame still reads as a
                // frame, the content gets the room. Still derived from the frame thickness so a
                // carved genre's well clears its own frame.
                float ft = Mathf.Max(g.FramePx(body.Size.Y) * 0.55f,
                                     Mathf.Min(body.Size.X, body.Size.Y) * 0.05f);
                var well = new Rect2(body.Position + new Vector2(ft, ft),
                                     body.Size - new Vector2(ft * 2f, ft * 2f));
                if (well.Size.X > 4 && well.Size.Y > 4)
                {
                    float ps = g.WellShade;
                    var sunk = new Color(face.R * ps, face.G * ps, face.B * ps, face.A);
                    KitChrome.DrawShape(this, _genre, well, KitChrome.Shape(_genre, KitWidgetClass.Panel), sunk,
                                        ink, Mathf.Max(1f, rimPx * 0.5f), KitWidgetClass.Panel);
                    DrawWellInset(well, sunk);
                }
            }

            if (TornEdge) DrawTornEdge(body, face, ink);

            KitChrome.DrawPanelHeader(this, _genre, body, _title, HeaderStyle,
                                      ResolvedBannerShape(), BannerShade, TitleFontScale);

            if (ShowClose) DrawClose(ink, rimPx);
            if (ShowClose) KitChrome.DrawFocusRing(this, _genre, CloseRect(), KitShape.Round, 0.8f);

            KitChrome.DrawAttachments(this, _genre, Attachments);
        }
    }
}
