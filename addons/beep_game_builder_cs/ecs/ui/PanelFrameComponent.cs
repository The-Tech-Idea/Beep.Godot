using Godot;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// A game panel: outer frame, recessed inner well, and a title banner that OVERHANGS the
    /// top edge.
    ///
    /// This is the most repeated element across every kit in `Example_Art/gameui1..7.png`, and
    /// the one our UI got most consistently wrong. Not one of the seven puts a title inside the
    /// box — the header is always a separate piece sitting on top of the frame and crossing its
    /// border, and the body is always a recessed well rather than the frame's own face. That
    /// overlap is what makes a panel read as an assembled object instead of a div with a
    /// heading. See docs/GAME_UI_KIT_SPEC.md.
    ///
    /// Drawn with <see cref="StyleBoxFlat"/> for the same reason as
    /// <see cref="ResourceBadgeComponent"/>: it gives corner radius, border width and drop
    /// shadow natively, which is exactly the heavy-outlined look the kits share.
    ///
    /// Place it as the background Control of a panel. If its first child is a
    /// <see cref="MarginContainer"/>, its margins are driven automatically so content lands
    /// inside the well and clears the banner — otherwise read <see cref="WellRect"/> yourself.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PanelFrameComponent : Godot.Control
    {
        public enum BannerShape
        {
            /// <summary>Rounded plaque. The default, and the most common across the kits.</summary>
            Plaque,
            /// <summary>Ribbon with folded ends that extend past the panel (gameui6).</summary>
            Ribbon,
            /// <summary>Ellipse (gameui7).</summary>
            Ellipse,
            /// <summary>No banner — a plain framed well.</summary>
            None,
        }

        [Export] public string Title { get => _title; set { string next = value ?? ""; if (_title == next) return; _title = next; RefreshFrameChrome(); } }
        private string _title = "";

        [Export] public BannerShape Banner { get => _shape; set { if (_shape == value) return; _shape = value; RefreshFrameChrome(); } }
        private BannerShape _shape = BannerShape.Plaque;

        [Export] public Texture2D? TitleIcon { get => _titleIcon; set { if (_titleIcon == value) return; _titleIcon = value; RefreshFrameChrome(); } }
        private Texture2D? _titleIcon;

        /// <summary>Outline thickness. The kits are consistently heavy — a hairline reads as a
        /// document, this reads as an object.</summary>
        [Export] public int OutlineWidth { get => _outlineWidth; set { int next = Mathf.Max(0, value); if (_outlineWidth == next) return; _outlineWidth = next; RefreshFrameChrome(); } }
        private int _outlineWidth = 3;

        [Export] public int CornerRadius { get => _cornerRadius; set { int next = Mathf.Max(0, value); if (_cornerRadius == next) return; _cornerRadius = next; RefreshFrameChrome(); } }
        private int _cornerRadius = 10;
        /// <summary>Title size as a multiple of the theme's body font, and a banner tall enough
        /// to hold it. Both were fixed (34px around 17pt) — on a 24pt theme the title overflowed
        /// its own banner.</summary>
        [Export(PropertyHint.Range, "0.5,3.0,0.05")]
        public float TitleFontScale
        {
            get => _titleFontScale;
            set
            {
                float next = Mathf.Clamp(value, 0.5f, 3.0f);
                if (Mathf.IsEqualApprox(_titleFontScale, next)) return;
                _titleFontScale = next;
                RefreshFrameChrome();
            }
        }
        private float _titleFontScale = 1.2f;

        private int TitleFontSize => UiSurface.FontSize(this, TitleFontScale);
        private int BannerHeight => Mathf.RoundToInt(TitleFontSize * 2.0f);

        /// <summary>How much vertical space the banner occupies at the top of this frame, or 0
        /// when there is none. Public because LAYOUT code has to know: the banner is drawn from
        /// y=0 downward, so any content placed above this overlaps it. BeepDialogLayout.ApplyShell
        /// was stamping a uniform 24px margin on all four sides and clobbering the 46 that
        /// settings_menu.tscn set for exactly this reason, which put the tab row 10px inside the
        /// "Settings" plaque.</summary>
        public int BannerRoom =>
            _shape != BannerShape.None && !string.IsNullOrEmpty(_title) ? BannerHeight : 0;
        /// <summary>Gap between the frame's inner edge and the well.</summary>
        [Export] public int FramePadding { get => _framePadding; set { int next = Mathf.Max(0, value); if (_framePadding == next) return; _framePadding = next; RefreshFrameChrome(); } }
        private int _framePadding = 8;

        /// <summary>Draw the recessed inner well.
        ///
        /// Off gives a FLUSH frame — outer frame and banner only, with content sitting on the
        /// frame's own face. Needed for screens whose content is a loose column rather than a
        /// filled block: main_menu and game_over stack buttons in a VBox with separation and a
        /// spacer, so the recess showed through every gap as dark banding between them. A well
        /// only reads as a recess when something actually fills it.
        ///
        /// WellRect is still computed either way, so content placement is unchanged.</summary>
        [Export] public bool DrawWell { get => _drawWell; set { if (_drawWell == value) return; _drawWell = value; RefreshFrameChrome(); } }
        private bool _drawWell = true;

        /// <summary>The recessed content area, in local coordinates. Lay content out inside it.</summary>
        public Rect2 WellRect { get; private set; }

        /// <summary>Outline actually used this draw — resolved (theme) or exported. Held in a
        /// field because Box() is called from several places that would otherwise each need it
        /// threaded through.</summary>
        private Color _drawOutline = new(0.13f, 0.08f, 0.05f, 1f);

        // There is no "use theme colours" switch any more, and no exported colours for it to
        // fall back to. Hand-setting colours per scene never scaled — 10 genres x 5 themes x
        // every panel, all wrong the moment a theme changed — and the off path only existed to
        // give those literals somewhere to be used. The register (which shape, how heavy) is
        // per genre; the palette is per theme, and the theme already knows it.

        /// <summary>Banner shape per genre, so a genre reads as its own family without every
        /// scene restating it. Registers taken from docs/GAME_UI_KIT_SPEC.md.</summary>
        public static BannerShape ShapeForGenre(string genre) => genre?.ToLowerInvariant() switch
        {
            "rpg" or "survival" or "topdown" or "cardgame" => BannerShape.Ribbon,   // wood/adventure
            "puzzle" or "platformer" => BannerShape.Ellipse,                        // candy/cartoon
            _ => BannerShape.Plaque,                                                // civic/tech
        };

        /// <summary>Control this frame wraps. When set, the frame tracks that node's rect every
        /// frame instead of using its own offsets.
        ///
        /// A fixed offset cannot work here: the cluster inside grows with its content (a fourth
        /// stat label, a longer quest name) and a hardcoded frame height simply lets the extra
        /// row spill out below the well. The frame has to be a consequence of what it contains.
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
                UpdateProcessing();
                if (!Engine.IsEditorHint() && IsInsideTree() && !_targetPath.IsEmpty)
                    CallDeferred(nameof(ResolveTarget));
            }
        }
        private NodePath _targetPath = new("");

        /// <summary>A MarginContainer holding this panel's content, when it is a SIBLING rather
        /// than a child of the frame.
        ///
        /// The frame already drives a MarginContainer that is its own child, but the common
        /// layout puts both under a shared PanelContainer, so the frame never reached it and the
        /// scene hardcoded a top margin instead — settings_menu used 46px. The banner is now
        /// sized from the theme font, so on a larger-type genre it grew past that fixed margin
        /// and printed over the tab row. Driving the margin from the ACTUAL banner height is the
        /// only version of this that survives a font change.</summary>
        [Export] public NodePath ContentMarginPath { get => _contentMarginPath; set { if (_contentMarginPath == value) return; _contentMarginPath = value ?? new NodePath(""); RefreshFrameChrome(); } }
        private NodePath _contentMarginPath = new("");

        /// <summary>Slack around the target: how far the frame extends past the content on each
        /// side. Top is separate because the banner needs the room.</summary>
        [Export] public Vector2 TargetPadding { get => _targetPadding; set { if (_targetPadding == value) return; _targetPadding = value; RefreshFrameChrome(); } }
        private Vector2 _targetPadding = new(14, 12);

        private Godot.Control? _target;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;   // background art, never a click target
            UpdateProcessing();
            if (!Engine.IsEditorHint() && !TargetPath.IsEmpty)
                CallDeferred(nameof(ResolveTarget));
        }

        private void UpdateProcessing() => SetProcess(!TargetPath.IsEmpty);

        private void ResolveTarget() => _target = GetNodeOrNull<Godot.Control>(TargetPath);

        public override void _Process(double delta)
        {
            DriveSiblingMargin();
            if (TargetPath.IsEmpty) return;
            if (_target == null || !GodotObject.IsInstanceValid(_target)) { ResolveTarget(); return; }
            if (GetParent() is not Godot.Control) return;

            // CombinedMinimumSize, never Size: the cluster is usually an anchored container that
            // stretches to the full screen width while its CONTENT is narrow, so sizing from
            // Size produced a 1348px frame around a 200px stat list. Minimum size is the content.
            Vector2 need = _target.GetCombinedMinimumSize();

            // The banner occupies the top of the frame, so the WELL is what must be `need` tall
            // — not the frame. Sizing the frame to the content alone left the last row of a
            // cluster hanging out of the bottom, because the banner had eaten that much of it.
            bool hasBanner = _shape != BannerShape.None && !string.IsNullOrEmpty(_title);
            float bannerRoom = hasBanner ? BannerHeight : 0f;
            Vector2 size = new(need.X + TargetPadding.X,
                               need.Y + TargetPadding.Y + bannerRoom);

            // Size only. Position stays wherever the scene placed the frame — deriving it from an
            // anchored target's Position lands at the parent origin, not at the visible content.
            if (Size != size)
            {
                Size = size;
                CustomMinimumSize = size;
                QueueRedraw();
            }
        }

        /// <summary>Resolve the palette once per draw. Derived from the theme's own Label and
        /// Panel colours so all 50 skins stay distinguishable, then pushed to the contrast the
        /// kits use — heavy dark outline, frame lighter than the well it contains.</summary>
        private void ResolveThemeColors(out Color frame, out Color well, out Color banner,
                                        out Color outline, out Color title)
        {
            // GetThemeColor falls back to a sane default when the type is absent, so this is safe
            // in a bare scene as well as under a fully generated theme.
            Color accent = GetThemeColor("font_color", "Label");
            // Through UiSurface, not `as StyleBoxFlat`: the panel resolves to a TEXTURE under a
            // skinned theme and to StyleBoxEmpty on screens that let PanelFrame draw the plate,
            // and the old cast returned null for both — so it fell through to a hardcoded brown
            // and every framed screen drew a wood frame regardless of the active palette.
            Color surface = UiSurface.Of(this);

            static Color Mul(Color c, float k) => new(c.R * k, c.G * k, c.B * k, 1f);
            frame = Mul(surface, 1.0f) with { A = 1f };
            well = Mul(surface, 0.62f);
            banner = Mul(surface, 1.25f);
            outline = UiSurface.Ink(surface);
            title = accent;
        }

        public override void _Notification(int what)
        {
            if (what == NotificationThemeChanged || what == NotificationResized) RefreshFrameChrome();
        }

        private void RefreshFrameChrome()
        {
            if (IsInsideTree())
                DriveSiblingMargin();
            UpdateMinimumSize();
            QueueRedraw();
        }

        private StyleBoxFlat Box(Color bg, int radius, bool shadow, int border = -1)
        {
            var b = new StyleBoxFlat { BgColor = bg, BorderColor = _drawOutline };
            b.SetBorderWidthAll(border < 0 ? OutlineWidth : border);
            b.SetCornerRadiusAll(radius);
            if (shadow)
            {
                b.ShadowColor = new Color(0, 0, 0, 0.42f);
                b.ShadowSize = 5;
                b.ShadowOffset = new Vector2(0, 3);
            }
            return b;
        }

        public override void _Draw()
        {
            Vector2 s = Size;
            if (s.X <= 0 || s.Y <= 0) return;

            bool hasBanner = _shape != BannerShape.None && !string.IsNullOrEmpty(_title);
            // The frame starts BELOW the banner's midline, so the banner crosses its top border
            // rather than sitting above an untouched edge. The overlap is the whole point.
            float top = hasBanner ? BannerHeight * 0.5f : 0f;

            ResolveThemeColors(out var cFrame, out var cWell, out var cBanner,
                               out var cOutline, out var cTitle);
            _drawOutline = cOutline;

            var frameRect = new Rect2(0, top, s.X, s.Y - top);
            DrawStyleBox(Box(cFrame, CornerRadius, true), frameRect);

            int wellPad = FramePadding + OutlineWidth;
            var well = new Rect2(frameRect.Position.X + wellPad,
                                 frameRect.Position.Y + wellPad + (hasBanner ? BannerHeight * 0.5f : 0f),
                                 Mathf.Max(0f, frameRect.Size.X - wellPad * 2),
                                 Mathf.Max(0f, frameRect.Size.Y - wellPad * 2 - (hasBanner ? BannerHeight * 0.5f : 0f)));
            WellRect = well;
            if (DrawWell && well.Size.X > 2 && well.Size.Y > 2)
                DrawStyleBox(Box(cWell, Mathf.Max(2, CornerRadius - 4), false, Mathf.Max(2, OutlineWidth - 1)), well);

            if (hasBanner) DrawBanner(s, cBanner, cTitle);
            DriveChildMargins();
        }

        private void DrawBanner(Vector2 s, Color cBanner, Color cTitle)
        {
            var font = GetThemeDefaultFont();
            float textW = font?.GetStringSize(_title, HorizontalAlignment.Left, -1, TitleFontSize).X ?? 60f;
            float iconW = TitleIcon != null ? BannerHeight * 0.8f : 0f;
            float w = Mathf.Min(s.X - 16f, textW + iconW + BannerHeight * 1.6f);
            float x = (s.X - w) * 0.5f;

            var rect = new Rect2(x, 0, w, BannerHeight);

            switch (_shape)
            {
                case BannerShape.Ellipse:
                    DrawStyleBox(Box(cBanner, Mathf.RoundToInt(BannerHeight * 0.5f), true), rect);
                    break;
                case BannerShape.Ribbon:
                    // Folded ends extending past the plaque, as in gameui6 — drawn first so the
                    // plaque overlaps them and the fold reads as behind.
                    float tail = BannerHeight * 0.45f;
                    var dark = new StyleBoxFlat { BgColor = _drawOutline };
                    dark.SetCornerRadiusAll(2);
                    DrawStyleBox(dark, new Rect2(x - tail, BannerHeight * 0.55f, tail + 6, BannerHeight * 0.4f));
                    DrawStyleBox(dark, new Rect2(x + w - 6, BannerHeight * 0.55f, tail + 6, BannerHeight * 0.4f));
                    DrawStyleBox(Box(cBanner, 4, true), rect);
                    break;
                default:
                    DrawStyleBox(Box(cBanner, Mathf.Max(4, CornerRadius - 2), true), rect);
                    break;
            }

            float cx = rect.Position.X + (rect.Size.X - textW - iconW) * 0.5f;
            if (TitleIcon != null)
            {
                float p = BannerHeight * 0.1f;
                DrawTextureRect(TitleIcon, new Rect2(cx, p, iconW - p, BannerHeight - p * 2), false);
                cx += iconW;
            }
            if (font != null)
            {
                float y = BannerHeight * 0.5f + TitleFontSize * 0.36f;
                DrawString(font, new Vector2(cx + 1, y + 1), _title, HorizontalAlignment.Left, -1,
                           TitleFontSize, new Color(0, 0, 0, 0.6f));
                DrawString(font, new Vector2(cx, y), _title, HorizontalAlignment.Left, -1,
                           TitleFontSize, cTitle);
            }
        }

        /// <summary>Drive a sibling MarginContainer's top margin from the real banner height.</summary>
        private void DriveSiblingMargin()
        {
            if (ContentMarginPath.IsEmpty) return;
            if (GetNodeOrNull<MarginContainer>(ContentMarginPath) is not { } mc) return;
            bool hasBanner = _shape != BannerShape.None && !string.IsNullOrEmpty(_title);
            int top = FramePadding + OutlineWidth + (hasBanner ? BannerHeight : 0);
            int side = FramePadding + OutlineWidth + 8;
            if (mc.GetThemeConstant("margin_top") == top) return;   // no churn per frame
            KitChrome.SetConstantOverrideIfChanged(mc, "margin_top", top);
            KitChrome.SetConstantOverrideIfChanged(mc, "margin_left", side);
            KitChrome.SetConstantOverrideIfChanged(mc, "margin_right", side);
            KitChrome.SetConstantOverrideIfChanged(mc, "margin_bottom", side);
        }

        /// <summary>Push the well's geometry into a child MarginContainer so content lands inside
        /// the recess without every scene hand-tuning four margins against art it cannot see.</summary>
        private void DriveChildMargins()
        {
            foreach (var child in GetChildren())
            {
                if (child is not MarginContainer mc) continue;
                KitChrome.SetConstantOverrideIfChanged(mc, "margin_left", Mathf.RoundToInt(WellRect.Position.X) + 4);
                KitChrome.SetConstantOverrideIfChanged(mc, "margin_top", Mathf.RoundToInt(WellRect.Position.Y) + 4);
                KitChrome.SetConstantOverrideIfChanged(mc, "margin_right",
                    Mathf.RoundToInt(Size.X - (WellRect.Position.X + WellRect.Size.X)) + 4);
                KitChrome.SetConstantOverrideIfChanged(mc, "margin_bottom",
                    Mathf.RoundToInt(Size.Y - (WellRect.Position.Y + WellRect.Size.Y)) + 4);
                break;
            }
        }
    }
}
