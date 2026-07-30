using Godot;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// An <see cref="OptionButton"/> that draws the kit's chrome. Migration drop-in — see
    /// <see cref="KitChrome"/>.
    ///
    /// Because it IS an OptionButton, `Find&lt;OptionButton&gt;`, `.AddItem`, `.Selected`,
    /// `.ItemSelected +=` and the popup all keep working. `SettingsMenu.cs` binds
    /// `ResolutionOption` and `LanguageOption` this way, and `ThemeGallery.cs` three more.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitOptionButton : OptionButton
    {
        [Export] public UiSurface.Role Accent { get; set; } = UiSurface.Role.Neutral;

        private string _genre = "";
        private bool _suppressing;

        public override void _Ready()
        {
            _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
            Suppress();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationThemeChanged)
            {
                _genre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : "";
                Suppress();
                QueueRedraw();
            }
        }

        private void Suppress()
        {
            if (_suppressing) return;
            _suppressing = true;
            int fs = UiSurface.FontSize(this);
            float pad = Mathf.Max(6f, fs * 0.7f);
            float frame = KitGeometry.ForGenre(_genre).FramePx(Mathf.Max(Size.Y, fs * 2.4f));
            // The RIGHT margin is widened to reserve room for the arrow this class draws. Without
            // it a long item label runs straight under the chevron.
            foreach (string s in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                AddThemeStyleboxOverride(s, new StyleBoxEmpty
                {
                    ContentMarginLeft = frame + pad,
                    ContentMarginRight = frame + pad + fs * 1.6f,
                    ContentMarginTop = frame * 0.5f + pad * 0.4f,
                    ContentMarginBottom = frame * 0.5f + pad * 0.4f,
                });
            AddThemeIconOverride("arrow", KitChrome.Blank);
            _suppressing = false;
        }

        public override void _Draw()
        {
            if (Size.X <= 4f || Size.Y <= 4f) return;
            KitState state = Disabled ? KitState.Disabled
                : IsHovered() ? KitState.Hover : KitState.Normal;

            Color plate = UiSurface.Semantic(this, Accent);
            if (plate.A < 0.02f) plate = UiSurface.Of(this);
            Color face = KitChrome.StateFace(plate, state);
            var body = new Rect2(Vector2.Zero, Size);

            KitChrome.DrawPlate(this, _genre, body, face, state,
                                UiSurface.FontSize(this) / 14f);

            // Label and chevron LAST: a script's _Draw runs AFTER the base class's, so the plate
            // above paints straight over anything OptionButton already drew.
            int fs = UiSurface.FontSize(this);
            Color ink = UiSurface.Text(this);
            if (state == KitState.Disabled) ink = ink with { A = 0.45f };
            float frame = KitGeometry.ForGenre(_genre).FramePx(Size.Y);
            float pad = Mathf.Max(6f, fs * 0.7f);

            var textBox = new Rect2(frame + pad, 0,
                                    Mathf.Max(4f, Size.X - (frame + pad) * 2f - fs * 1.6f), Size.Y);
            KitChrome.DrawLabel(this, this, Text, textBox, ink, 0f, HorizontalAlignment.Left);

            float ax = Size.X - frame - pad - fs * 0.55f;
            float ay = Size.Y * 0.5f;
            float s = fs * 0.34f;
            DrawColoredPolygon(new[]
            {
                new Vector2(ax - s, ay - s * 0.55f), new Vector2(ax + s, ay - s * 0.55f),
                new Vector2(ax, ay + s * 0.7f),
            }, ink);
        }
    }
}
