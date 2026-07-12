using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// A texture-based UI skin. When set on <see cref="GameApp"/> (or directly on
    /// <see cref="ThemePresetComponent"/>), the theme engine builds <see cref="StyleBoxTexture"/>
    /// (9-patch) instead of procedural <see cref="StyleBoxFlat"/> for every UI node that
    /// has a texture slot. Nodes without a matching texture fall back to the procedural box.
    ///
    /// Drag and drop .png textures into the inspector slots. The 9-patch margins define
    /// which part of the image stretches vs. stays fixed (corners). All values are in texture px.
    /// Leave a slot null to use the procedural box for that node.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class UISkin : Resource
    {
        // ── Button textures (per state) ──
        [ExportGroup("Button Textures")]
        [Export] public Texture2D? ButtonNormal { get; set; }
        [Export] public Texture2D? ButtonHover { get; set; }
        [Export] public Texture2D? ButtonPressed { get; set; }
        [Export] public Texture2D? ButtonDisabled { get; set; }
        [Export] public Texture2D? ButtonFocus { get; set; }

        // ── Panel textures ──
        [ExportGroup("Panel Textures")]
        [Export] public Texture2D? Panel { get; set; }

        // ── Input (LineEdit / TextEdit) textures ──
        [ExportGroup("Input Textures")]
        [Export] public Texture2D? InputNormal { get; set; }
        [Export] public Texture2D? InputFocus { get; set; }

        // ── ProgressBar textures ──
        [ExportGroup("ProgressBar Textures")]
        [Export] public Texture2D? ProgressBarBackground { get; set; }
        [Export] public Texture2D? ProgressBarFill { get; set; }

        // ── Slider grabber texture ──
        [ExportGroup("Slider Textures")]
        [Export] public Texture2D? SliderGrabber { get; set; }

        // ── ScrollBar grabber texture ──
        [ExportGroup("ScrollBar Textures")]
        [Export] public Texture2D? ScrollGrabber { get; set; }

        // ── Separator texture ──
        [ExportGroup("Separator Textures")]
        [Export] public Texture2D? Separator { get; set; }

        /// <summary>9-patch margin — how many px from each edge stay fixed when stretching.
        /// Applied uniformly to all textures. -1 = auto (texture's own border metadata).</summary>
        [Export] public int PatchMargin { get; set; } = 12;

        /// <summary>True if ANY texture slot is set (i.e. this skin is active).</summary>
        public bool HasTextures =>
            ButtonNormal != null || ButtonHover != null
            || ButtonPressed != null || ButtonDisabled != null
            || ButtonFocus != null || Panel != null
            || InputNormal != null || InputFocus != null
            || ProgressBarBackground != null || ProgressBarFill != null
            || SliderGrabber != null || ScrollGrabber != null
            || Separator != null;

        /// <summary>Build a 9-patch StyleBoxTexture from a texture, or null if unset.
        /// The caller falls back to StyleBoxFlat when null.</summary>
        public StyleBoxTexture? BuildStyleBox(Texture2D? texture)
        {
            if (texture == null) return null;
            var sb = new StyleBoxTexture { Texture = texture };
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
