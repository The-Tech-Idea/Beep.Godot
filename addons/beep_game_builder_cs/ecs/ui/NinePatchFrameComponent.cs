using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Paints a 9-patch frame behind its parent Control, using a texture from the skin catalog.
    ///
    /// WHEN TO USE THIS — and when not to:
    ///  • A Button, Panel, PanelContainer, LineEdit, Window — anything a <c>Theme</c> styles —
    ///    must use a <b>StyleBoxTexture</b>, which is what <see cref="ThemePresetComponent"/>
    ///    already builds from each theme.json's <c>textures{}</c> block. A NinePatchRect is a
    ///    Node and CANNOT be a theme StyleBox, so reaching for it there is simply wrong.
    ///  • This component is for the case a Theme cannot reach: a standalone decorative frame —
    ///    a HUD banner, a portrait border, a callout, a minimap surround — around content that
    ///    is not a themed widget.
    ///
    /// Drop it under the Control you want framed. It inserts one full-rect NinePatchRect as that
    /// Control's first child, so the frame draws behind the content and the layout is untouched.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class NinePatchFrameComponent : UIComponent
    {
        /// <summary>Catalog slot to draw: "panel" (default), "dialog", "button_normal", …
        /// Any key from a theme.json <c>textures{}</c> block.</summary>
        [Export] public string Slot { get; set; } = "panel";

        /// <summary>Genre whose skin to read. Same meaning as ThemePresetComponent.GenreName.</summary>
        [Export] public string GenreName { get; set; } = "platformer";

        /// <summary>Theme within that genre. Empty = the genre's default_theme.</summary>
        [Export] public string PresetName { get; set; } = "";

        /// <summary>Explicit art, which always wins over the catalog. Use this for a frame the
        /// skin has no slot for.</summary>
        [Export] public Texture2D? OverrideTexture { get; set; }

        /// <summary>9-patch margin in px. 0 = use the margins the catalog slot declares, which
        /// is what keeps a baked texture's corners crisp. Only set this for OverrideTexture.</summary>
        [Export] public int PatchMargin { get; set; }

        /// <summary>Tint applied over the frame.</summary>
        [Export] public Color Modulate { get; set; } = new(1, 1, 1, 1);

        private NinePatchRect? _rect;

        public override void _Ready()
        {
            base._Ready();
            Apply();
        }

        public override void _ExitTree()
        {
            if (_rect != null && GodotObject.IsInstanceValid(_rect)) _rect.QueueFree();
            _rect = null;
            base._ExitTree();
        }

        /// <summary>Build or refresh the frame. Public and idempotent — safe to call again after
        /// changing Slot/GenreName at runtime; it reuses the one child rather than stacking.</summary>
        public void Apply()
        {
            if (GetParent() is not Godot.Control parent)
            {
                // Say so. Parented to a plain Node this draws nothing, and an invisible frame is
                // indistinguishable from a broken one — the failure mode this repo keeps re-learning.
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] NinePatchFrameComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Control — no frame is drawn. Move it under the Control you want framed.");
                return;
            }

            var (texture, margin) = Resolve();
            if (texture == null)
            {
                if (!Engine.IsEditorHint())
                    GD.PushWarning($"[{Name}] no texture for slot '{Slot}' in {GenreName}/{(string.IsNullOrEmpty(PresetName) ? "<default>" : PresetName)} and no OverrideTexture — no frame is drawn. Bake the skin (dock → Bake Textures) or assign OverrideTexture.");
                if (_rect != null && GodotObject.IsInstanceValid(_rect)) _rect.QueueFree();
                _rect = null;
                return;
            }

            if (_rect == null || !GodotObject.IsInstanceValid(_rect))
            {
                _rect = new NinePatchRect { Name = "BeepFrame", MouseFilter = Control.MouseFilterEnum.Ignore };
                parent.AddChild(_rect);
                parent.MoveChild(_rect, 0);          // behind the framed content
                _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            }

            _rect.Texture = texture;
            _rect.PatchMarginLeft = margin.Item1;
            _rect.PatchMarginTop = margin.Item2;
            _rect.PatchMarginRight = margin.Item3;
            _rect.PatchMarginBottom = margin.Item4;
            _rect.SelfModulate = Modulate;
        }

        /// <summary>Resolve texture + 9-patch margins. OverrideTexture wins; otherwise the
        /// catalog slot supplies both the art and the margins it was authored for.</summary>
        private (Texture2D?, (int, int, int, int)) Resolve()
        {
            int m = Mathf.Max(0, PatchMargin);
            if (OverrideTexture != null)
                return (OverrideTexture, (m, m, m, m));

            var theme = SkinCatalog.GetTheme(GenreName, PresetName);
            if (theme == null)
            {
                var genre = SkinCatalog.GetGenre(GenreName);
                if (genre != null && genre.Themes.TryGetValue(genre.DefaultTheme, out var dt)) theme = dt;
            }
            var slot = SlotDef(theme?.Textures);
            if (slot?.Path is not { } path || string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
                return (null, (m, m, m, m));

            var tex = ResourceLoader.Load<Texture2D>(path);
            // PatchMargin = 0 means "trust the slot", which is what makes a baked texture correct.
            var declared = PatchMargin > 0
                ? (m, m, m, m)
                : ((int)slot.MarginLeft, (int)slot.MarginTop, (int)slot.MarginRight, (int)slot.MarginBottom);
            return (tex, declared);
        }

        private TextureSlotDef? SlotDef(ThemeTextureSlots? t) => t == null ? null : Slot.ToLowerInvariant() switch
        {
            "button_normal" => t.ButtonNormal,
            "button_hover" => t.ButtonHover,
            "button_pressed" => t.ButtonPressed,
            "button_disabled" => t.ButtonDisabled,
            "button_focus" => t.ButtonFocus,
            "dialog" => t.Dialog,
            "input_normal" => t.InputNormal,
            "input_focus" => t.InputFocus,
            "progress_bg" => t.ProgressBg,
            "progress_fill" => t.ProgressFill,
            "separator" => t.Separator,
            _ => t.Panel,
        };

        /// <summary>Inspector dropdowns, sourced from the skin catalog — same treatment
        /// ThemePresetComponent gives its genre/theme pickers.</summary>
        public override void _ValidateProperty(Godot.Collections.Dictionary property)
        {
            base._ValidateProperty(property);
            switch ((string)property["name"])
            {
                case nameof(GenreName):
                    SkinPropertyHints.ApplyEnum(property, SkinPropertyHints.GenreHint(GenreName));
                    break;
                case nameof(PresetName):
                    SkinPropertyHints.ApplyEnum(property, SkinPropertyHints.ThemeHint(GenreName, PresetName));
                    break;
                case nameof(Slot):
                    SkinPropertyHints.ApplyEnum(property,
                        "panel,dialog,button_normal,button_hover,button_pressed,button_disabled,button_focus,input_normal,input_focus,progress_bg,progress_fill,separator");
                    break;
            }
        }
    }
}
