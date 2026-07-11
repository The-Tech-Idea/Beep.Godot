using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// A texture-based UI skin. When set on <see cref="GameApp"/> (or directly on
    /// <see cref="ThemePresetComponent"/>), the theme engine builds <see cref="StyleBoxTexture"/>
    /// (9-patch) instead of procedural <see cref="StyleBoxFlat"/> for every UI node that
    /// has a texture slot. Nodes without a matching texture fall back to the procedural box.
    ///
    /// Each texture is a res:// path to a PNG. The 9-patch margins (texture margins) define
    /// which part of the image stretches vs. stays fixed (corners). All values are in texture px.
    ///
    /// Usage: assign textures in the inspector (or set paths in GameApp), and the theme engine
    /// picks them up automatically. Leave a slot null to use the procedural box for that node.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class UISkin : Resource
    {
        // ── Button textures (per state) ──
        [ExportGroup("Button Textures")]
        [Export] public string? ButtonNormal { get; set; }
        [Export] public string? ButtonHover { get; set; }
        [Export] public string? ButtonPressed { get; set; }
        [Export] public string? ButtonDisabled { get; set; }
        [Export] public string? ButtonFocus { get; set; }

        // ── Panel textures ──
        [ExportGroup("Panel Textures")]
        [Export] public string? Panel { get; set; }

        // ── Input (LineEdit / TextEdit) textures ──
        [ExportGroup("Input Textures")]
        [Export] public string? InputNormal { get; set; }
        [Export] public string? InputFocus { get; set; }

        // ── ProgressBar textures ──
        [ExportGroup("ProgressBar Textures")]
        [Export] public string? ProgressBarBackground { get; set; }
        [Export] public string? ProgressBarFill { get; set; }

        // ── Slider grabber texture ──
        [ExportGroup("Slider Textures")]
        [Export] public string? SliderGrabber { get; set; }

        // ── ScrollBar grabber texture ──
        [ExportGroup("ScrollBar Textures")]
        [Export] public string? ScrollGrabber { get; set; }

        // ── Separator texture ──
        [ExportGroup("Separator Textures")]
        [Export] public string? Separator { get; set; }

        /// <summary>9-patch margin — how many px from each edge stay fixed when stretching.
        /// Applied uniformly to all textures. -1 = auto (texture's own border metadata).</summary>
        [Export] public int PatchMargin { get; set; } = 12;

        /// <summary>True if ANY texture slot is set (i.e. this skin is active).</summary>
        public bool HasTextures =>
            !string.IsNullOrEmpty(ButtonNormal) || !string.IsNullOrEmpty(ButtonHover)
            || !string.IsNullOrEmpty(ButtonPressed) || !string.IsNullOrEmpty(ButtonDisabled)
            || !string.IsNullOrEmpty(ButtonFocus) || !string.IsNullOrEmpty(Panel)
            || !string.IsNullOrEmpty(InputNormal) || !string.IsNullOrEmpty(InputFocus)
            || !string.IsNullOrEmpty(ProgressBarBackground) || !string.IsNullOrEmpty(ProgressBarFill)
            || !string.IsNullOrEmpty(SliderGrabber) || !string.IsNullOrEmpty(ScrollGrabber)
            || !string.IsNullOrEmpty(Separator);

        /// <summary>Load a texture by res:// path, or null if missing/unset.</summary>
        public Texture2D? LoadTexture(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!ResourceLoader.Exists(path)) return null;
            return ResourceLoader.Load<Texture2D>(path);
        }

        /// <summary>Build a 9-patch StyleBoxTexture from a texture path, or null if the
        /// texture is unset/unavailable. The caller falls back to StyleBoxFlat when null.</summary>
        public StyleBoxTexture? BuildStyleBox(string? path)
        {
            var tex = LoadTexture(path);
            if (tex == null) return null;
            var sb = new StyleBoxTexture { Texture = tex };
            if (PatchMargin >= 0)
            {
                sb.TextureMarginLeft = PatchMargin;
                sb.TextureMarginRight = PatchMargin;
                sb.TextureMarginTop = PatchMargin;
                sb.TextureMarginBottom = PatchMargin;
            }
            // Stretch the center, keep corners fixed.
            sb.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch;
            sb.AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch;
            return sb;
        }
    }
}
