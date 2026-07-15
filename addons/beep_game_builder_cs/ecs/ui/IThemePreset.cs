using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Full semantic color palette for a theme preset.
    /// Every preset defines its own complete schema — no shared parameters.
    /// </summary>
    public struct ColorSchema
    {
        // ── Surface ──
        public Color SurfacePrimary;      // Default button face
        public Color SurfaceHover;        // Button on mouse hover
        public Color SurfacePressed;      // Button when pressed/clicked
        public Color SurfaceDisabled;     // Button when disabled

        // ── Text ──
        public Color TextPrimary;         // Default button label / text
        public Color TextHover;           // Text on hover
        public Color TextDisabled;        // Text when disabled
        public Color TextOnDark;          // Light text for dark surfaces

        // ── Accent ──
        public Color AccentPrimary;       // Focus rings, primary highlights
        public Color AccentSecondary;     // Secondary accent, hover hints

        // ── Border ──
        public Color BorderNormal;        // Default border color
        public Color BorderHover;         // Border on hover
        public Color BorderFocus;         // Focus ring border
        public Color BorderBevelLight;    // Top-left bevel edge (Classic / 3D styles)
        public Color BorderBevelDark;     // Bottom-right bevel edge

        // ── Shadow ──
        public Color ShadowColor;         // Drop shadow tint

        // ── Background ──
        public Color BgPanel;             // Panel / container background
        public Color BgCanvas;            // Root / page background

        // ── Semantic ──
        public Color SemanticSuccess;     // Green — confirm, health, positive
        public Color SemanticDanger;      // Red — delete, damage, negative
        public Color SemanticWarning;     // Yellow / orange — caution
        public Color SemanticInfo;        // Blue — informational
    }

    /// <summary>
    /// Animation behaviour for hover, press, and focus transitions.
    /// Each preset chooses its own feel — bouncy, snappy, smooth, or none.
    /// </summary>
    public struct AnimationConfig
    {
        /// <summary>Target scale on hover (e.g. 1.04 = 4% larger).</summary>
        public float HoverScaleAmount;

        /// <summary>Duration of the hover scale tween in seconds.</summary>
        public float HoverScaleDuration;

        /// <summary>Target scale on press (e.g. 0.96 = 4% smaller).</summary>
        public float PressScaleAmount;

        /// <summary>Duration of the press scale tween in seconds.</summary>
        public float PressScaleDuration;

        /// <summary>Whether the button lifts slightly on hover (shadow depth change).</summary>
        public bool EnableShadowLift;

        /// <summary>Whether a focus glow ring appears on keyboard/gamepad focus.</summary>
        public bool EnableFocusGlow;
        // Note: all animation values come from theme.json's "animation" block.
        // No hardcoded defaults — if a theme.json is missing the block, the struct
        // zero-initializes (all 0/false), which disables animation. Every shipped
        // theme.json includes a complete animation block.
    }

    /// <summary>
    /// Contract for a theme preset.
    /// Each preset is a COMPLETE visual package — it owns all geometry,
    /// all colors (ColorSchema), and all animation behaviour (AnimationConfig).
    ///
    /// Themes are FILE-BASED now. To add a new preset:
    /// 1. Create skins/&lt;genre&gt;/themes/&lt;mytheme&gt;/theme.json
    /// 2. Add palette .json files in the same folder
    /// Zero C# changes — SkinCatalog auto-discovers it on editor restart.
    /// </summary>
    public interface IThemePreset
    {
        /// <summary>Human-readable name shown in debug / tooltips.</summary>
        string PresetName { get; }

        /// <summary>Theme id (lowercase string key, e.g. "cartoon").</summary>
        string PresetType { get; }

        /// <summary>Complete color schema for all surface/text/accent/border/semantic slots.</summary>
        ColorSchema Colors { get; }

        /// <summary>Animation behaviour for hover / press / focus.</summary>
        AnimationConfig Animation { get; }

        // ── Button state StyleBoxes ──
        // Each method returns a COMPLETE StyleBox with the preset's own
        // corner geometry, border widths, shadow settings, and content margins.

        StyleBox GetButtonNormal();
        StyleBox GetButtonHover();
        StyleBox GetButtonPressed();
        StyleBox GetButtonDisabled();
        StyleBox GetButtonFocus();

        // ── Semantic button variants (Godot Theme Type Variations) ──

        /// <summary>Primary / emphasised button (e.g. "Play", "Submit").</summary>
        StyleBox GetPrimaryButtonNormal();

        /// <summary>Danger / destructive action button (e.g. "Delete", "Quit").</summary>
        StyleBox GetDangerButtonNormal();

        /// <summary>Success / positive action button (e.g. "Save", "Confirm").</summary>
        StyleBox GetSuccessButtonNormal();

        // ── Other control types ──

        /// <summary>Panel / PanelContainer background.</summary>
        StyleBox GetPanelBackground();

        /// <summary>LineEdit / text input background.</summary>
        StyleBox GetLineEditNormal();

        // ── Texture mode ──

        /// <summary>Whether this preset uses image textures instead of procedural StyleBoxFlat.</summary>
        bool UsesTextures { get; }

        /// <summary>Path to the normal-state button texture (res://...). Null if procedural.</summary>
        string? TexturePathNormal { get; }

        /// <summary>Path to the hover-state button texture. Null if procedural.</summary>
        string? TexturePathHover { get; }

        /// <summary>Path to the pressed-state button texture. Null if procedural.</summary>
        string? TexturePathPressed { get; }

        // ── Per-slot StyleBoxTexture getters (Phase C — JSON-driven 9-patch textures).
        // Returns a built StyleBoxTexture if the slot is set, null otherwise.
        // ThemePresetComponent consults these BEFORE falling back to the inspector UISkin.

        StyleBox? GetButtonNormalTexture();
        StyleBox? GetButtonHoverTexture();
        StyleBox? GetButtonPressedTexture();
        StyleBox? GetButtonDisabledTexture();
        StyleBox? GetButtonFocusTexture();
        StyleBox? GetPanelTexture();
        StyleBox? GetDialogTexture();
        StyleBox? GetInputNormalTexture();
        StyleBox? GetInputFocusTexture();
        StyleBox? GetProgressBgTexture();
        StyleBox? GetProgressFillTexture();
        StyleBox? GetSliderGrabberTexture();
        StyleBox? GetScrollGrabberTexture();
        StyleBox? GetSeparatorTexture();
    }
}
