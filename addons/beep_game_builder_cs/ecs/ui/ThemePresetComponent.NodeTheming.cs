using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// PARTIAL: dedicated per-node-type theming methods. Each UI node type gets its
    /// OWN method that sets EVERYTHING for that type — all its color properties, all
    /// its StyleBox background states (built from the theme schema + geometry), and
    /// font size. No shared generic builders, no loops calling the same code for
    /// different types. One method = one node type = complete treatment.
    ///
    /// ApplyToSubtree() (in the main partial) just calls each method in sequence.
    /// </summary>
    public partial class ThemePresetComponent
    {
        /// <summary>Font size: geometry.json override takes priority, then the loaded
        /// theme's own font_size from theme.json, then 14 as last-resort fallback.</summary>
        private int Fs => _geometry != null && _geometry.FontSize >= 0
            ? _geometry.FontSize
            : (_loadedThemeGeometry.FontSize > 0 ? _loadedThemeGeometry.FontSize : 14);

        /// <summary>Try to build a texture-based StyleBox for a node-type slot. Resolution
        /// order (Phase C — JSON wins per slot):
        ///   1. JSON-driven texture from theme.json's textures{} block (jsonTex)
        ///   2. Inspector UISkin's matching slot (skinPath)
        ///   3. Procedural StyleBoxFlat (procedural)
        /// Returns the procedural box (with geometry stamped) if every texture
        /// source is unavailable. Skips texture resolution entirely when
        /// <paramref name="nodeTypeEnabled"/> is false (per-node toggle) or the
        /// master <see cref="UseTextures"/> is off.</summary>
        private StyleBox SkinOr(bool nodeTypeEnabled, StyleBox? jsonTex, Texture2D? skinTex, StyleBoxFlat procedural)
        {
            if (UseTextures && nodeTypeEnabled)
            {
                // 1. JSON-driven texture wins when set (per-slot override).
                if (jsonTex != null) return jsonTex;
                // 2. Inspector UISkin texture (drag-and-drop Texture2D).
                if (skinTex != null && _skin != null && _skin.HasTextures)
                {
                    var sb = _skin.BuildStyleBox(skinTex);
                    if (sb != null) return sb;
                }
            }
            return StampGeometry(procedural);
        }

        private StyleBox StampGeometry(StyleBox sb)
        {
            if (_geometry != null && sb is StyleBoxFlat flat) _geometry.ApplyTo(flat);
            return sb;
        }

        // ═══════════════════════════════════════════════════════════════
        // BUTTON — 5 StyleBox states + 6 color properties
        // ═══════════════════════════════════════════════════════════════
        private void ThemeButton()
        {
            var c = _presetInstance!.Colors;
            string t = "Button";
            var p = _presetInstance!;
            Sb("normal", t, SkinOr(UseButtonTextures, p.GetButtonNormalTexture(),   _skin?.ButtonNormal,   Box(c.SurfacePrimary,  c.BorderNormal,         c.ShadowColor, _shadowSize)));
            Sb("hover", t, SkinOr(UseButtonTextures, p.GetButtonHoverTexture(),    _skin?.ButtonHover,    Box(c.SurfaceHover,    c.BorderHover,          c.ShadowColor, _shadowSize + 4)));
            Sb("pressed", t, SkinOr(UseButtonTextures, p.GetButtonPressedTexture(),  _skin?.ButtonPressed,  Box(c.SurfacePressed,  c.BorderNormal,         c.ShadowColor, Mathf.Max(0, _shadowSize - 6))));
            Sb("disabled", t, SkinOr(UseButtonTextures, p.GetButtonDisabledTexture(), _skin?.ButtonDisabled, Box(c.SurfaceDisabled, Fade(c.BorderNormal, 0.4f), Clear, 0)));
            Sb("focus", t, SkinOr(UseButtonTextures, p.GetButtonFocusTexture(),    _skin?.ButtonFocus,    Box(c.SurfacePrimary,  c.BorderFocus,          c.ShadowColor, _shadowSize)));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_hover_color", c.TextHover);
            Col(t, "font_pressed_color", c.TextHover); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "font_focus_color", c.AccentSecondary); Col(t, "font_outline_color", c.BorderBevelDark);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // CHECKBUTTON / CHECKBOX — 5 StyleBox states + colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemeCheckButton()
        {
            var c = _presetInstance!.Colors;
            string t = "CheckButton";
            Sb("normal", t, Box(c.SurfacePrimary, Clear, Clear, 0));
            Sb("hover", t, Box(c.SurfaceHover, Clear, Clear, 0));
            Sb("pressed", t, Box(c.SurfacePressed, Clear, Clear, 0));
            Sb("disabled", t, Box(c.SurfaceDisabled, Clear, Clear, 0));
            Sb("focus", t, Box(c.SurfacePrimary, c.BorderFocus, Clear, 0));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_hover_color", c.TextHover);
            Col(t, "font_pressed_color", c.TextHover); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "font_focus_color", c.AccentSecondary); Col(t, "font_outline_color", c.ShadowColor);
            FontSz(t);
        }

        private void ThemeCheckBox()
        {
            var c = _presetInstance!.Colors;
            string t = "CheckBox";
            Sb("normal", t, Box(c.SurfacePrimary, Clear, Clear, 0));
            Sb("hover", t, Box(c.SurfaceHover, Clear, Clear, 0));
            Sb("pressed", t, Box(c.SurfacePressed, Clear, Clear, 0));
            Sb("disabled", t, Box(c.SurfaceDisabled, Clear, Clear, 0));
            Sb("focus", t, Box(c.SurfacePrimary, c.BorderFocus, Clear, 0));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_hover_color", c.TextHover);
            Col(t, "font_pressed_color", c.TextHover); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "font_focus_color", c.AccentSecondary); Col(t, "font_outline_color", c.ShadowColor);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // OPTIONBUTTON / MENUBUTTON / COLORPICKERBUTTON
        // ═══════════════════════════════════════════════════════════════
        private void ThemeOptionButton()
        {
            var c = _presetInstance!.Colors;
            string t = "OptionButton";
            Sb("normal", t, Box(c.SurfacePrimary, c.BorderNormal, c.ShadowColor, _shadowSize));
            Sb("hover", t, Box(c.SurfaceHover, c.BorderHover, c.ShadowColor, _shadowSize + 4));
            Sb("pressed", t, Box(c.SurfacePressed, c.BorderNormal, c.ShadowColor, Mathf.Max(0, _shadowSize - 6)));
            Sb("disabled", t, Box(c.SurfaceDisabled, Fade(c.BorderNormal, 0.4f), Clear, 0));
            Sb("focus", t, Box(c.SurfacePrimary, c.BorderFocus, c.ShadowColor, _shadowSize));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_hover_color", c.TextHover);
            Col(t, "font_pressed_color", c.TextHover); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "font_focus_color", c.AccentSecondary); Col(t, "font_outline_color", c.ShadowColor);
            FontSz(t);
        }

        private void ThemeMenuButton()
        {
            var c = _presetInstance!.Colors;
            string t = "MenuButton";
            Sb("normal", t, Box(c.SurfacePrimary, c.BorderNormal, c.ShadowColor, _shadowSize));
            Sb("hover", t, Box(c.SurfaceHover, c.BorderHover, c.ShadowColor, _shadowSize + 4));
            Sb("pressed", t, Box(c.SurfacePressed, c.BorderNormal, c.ShadowColor, Mathf.Max(0, _shadowSize - 6)));
            Sb("disabled", t, Box(c.SurfaceDisabled, Fade(c.BorderNormal, 0.4f), Clear, 0));
            Sb("focus", t, Box(c.SurfacePrimary, c.BorderFocus, c.ShadowColor, _shadowSize));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_hover_color", c.TextHover);
            Col(t, "font_pressed_color", c.TextHover); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "font_focus_color", c.AccentSecondary); Col(t, "font_outline_color", c.ShadowColor);
            FontSz(t);
        }

        private void ThemeColorPickerButton()
        {
            var c = _presetInstance!.Colors;
            string t = "ColorPickerButton";
            Sb("normal", t, Box(c.SurfacePrimary, c.BorderNormal, c.ShadowColor, _shadowSize));
            Sb("hover", t, Box(c.SurfaceHover, c.BorderHover, c.ShadowColor, _shadowSize + 4));
            Sb("pressed", t, Box(c.SurfacePressed, c.BorderNormal, c.ShadowColor, Mathf.Max(0, _shadowSize - 6)));
            Sb("disabled", t, Box(c.SurfaceDisabled, Fade(c.BorderNormal, 0.4f), Clear, 0));
            Sb("focus", t, Box(c.SurfacePrimary, c.BorderFocus, c.ShadowColor, _shadowSize));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_hover_color", c.TextHover);
            Col(t, "font_pressed_color", c.TextHover); Col(t, "font_disabled_color", c.TextDisabled);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // LABEL / RICHTEXTLABEL — colors only
        // ═══════════════════════════════════════════════════════════════
        private void ThemeLabel()
        {
            var c = _presetInstance!.Colors;
            string t = "Label";
            Col(t, "font_color", c.TextPrimary); Col(t, "font_outline_color", c.ShadowColor);
            Col(t, "shadow_color", c.ShadowColor);
            FontSz(t);
        }

        private void ThemeRichTextLabel()
        {
            var c = _presetInstance!.Colors;
            string t = "RichTextLabel";
            Col(t, "font_color", c.TextPrimary); Col(t, "font_outline_color", c.ShadowColor);
            Col(t, "shadow_color", c.ShadowColor);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // LINEEDIT / TEXTEDIT / SPINBOX — 3 StyleBox states + 6 colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemeLineEdit()
        {
            var c = _presetInstance!.Colors;
            var p = _presetInstance!;
            string t = "LineEdit";
            Sb("normal", t, SkinOr(UseInputTextures, p.GetInputNormalTexture(), _skin?.InputNormal, InputBox(c.SurfacePressed, c.BorderNormal)));
            Sb("focus", t, SkinOr(UseInputTextures, p.GetInputFocusTexture(),  _skin?.InputFocus,  InputBox(c.SurfacePressed, c.BorderFocus, focus: true)));
            Sb("read_only", t, InputBox(Fade(c.SurfaceDisabled, 0.6f), Fade(c.BorderNormal, 0.4f)));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_selected_color", c.TextOnDark);
            Col(t, "selection_color", c.AccentPrimary); Col(t, "caret_color", c.AccentSecondary);
            Col(t, "clear_button_color", c.TextDisabled); Col(t, "clear_button_color_pressed", c.SemanticDanger);
            FontSz(t);
        }

        private void ThemeTextEdit()
        {
            var c = _presetInstance!.Colors;
            string t = "TextEdit";
            Sb("normal", t, InputBox(c.SurfacePressed, c.BorderNormal));
            Sb("focus", t, InputBox(c.SurfacePressed, c.BorderFocus, focus: true));
            Sb("read_only", t, InputBox(Fade(c.SurfaceDisabled, 0.6f), Fade(c.BorderNormal, 0.4f)));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_selected_color", c.TextOnDark);
            Col(t, "selection_color", c.AccentPrimary); Col(t, "caret_color", c.AccentSecondary);
            FontSz(t);
        }

        private void ThemeSpinBox()
        {
            var c = _presetInstance!.Colors;
            string t = "SpinBox";
            Sb("normal", t, InputBox(c.SurfacePressed, c.BorderNormal));
            Sb("focus", t, InputBox(c.SurfacePressed, c.BorderFocus, focus: true));
            Sb("read_only", t, InputBox(Fade(c.SurfaceDisabled, 0.6f), Fade(c.BorderNormal, 0.4f)));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_selected_color", c.TextOnDark);
            Col(t, "selection_color", c.AccentPrimary); Col(t, "caret_color", c.AccentSecondary);
            Col(t, "updown_color", c.TextDisabled); Col(t, "updown_hover_color", c.TextHover);
            Col(t, "updown_pressed_color", c.AccentPrimary);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // PROGRESSBAR — bg/fill StyleBoxes + colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemeProgressBar()
        {
            var c = _presetInstance!.Colors;
            var p = _presetInstance!;
            string t = "ProgressBar";
            int inset = ActiveShapes.Progress.CornerInset;
            Sb("background", t, SkinOr(UseProgressBarTextures, p.GetProgressBgTexture(),   _skin?.ProgressBarBackground, RoundBox(c.SurfaceDisabled, inset)));
            Sb("fill", t, SkinOr(UseProgressBarTextures, p.GetProgressFillTexture(), _skin?.ProgressBarFill,        RoundBox(c.AccentPrimary,    inset)));
            Col(t, "font_color", c.TextOnDark); Col(t, "font_outline_color", c.ShadowColor);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // SLIDER (H/V) — grabber + track + tick color
        // ═══════════════════════════════════════════════════════════════
        private void ThemeSlider(string typeName)
        {
            var c = _presetInstance!.Colors;
            int r = (_gTL + _gTR) / 2;
            Sb("grabber_area", typeName, CircleBox(c.AccentPrimary, r));
            Sb("grabber_area_highlight", typeName, CircleBox(c.AccentSecondary, r, ActiveShapes.Slider.GrabberHoverShadow));
            Sb("slider", typeName, RoundBox(c.SurfaceDisabled, Mathf.Max(2, _gTL / 2)));
            Col(typeName, "tick_color", c.BorderNormal);
        }

        // ═══════════════════════════════════════════════════════════════
        // SCROLLBAR (H/V) — grabber + track
        // ═══════════════════════════════════════════════════════════════
        private void ThemeScrollBar(string typeName)
        {
            var c = _presetInstance!.Colors;
            int r = Mathf.Max(ActiveShapes.Scrollbar.GrabberMin, (_gTL + _gTR) / ActiveShapes.Scrollbar.GrabberDivisor);
            Sb("grabber", typeName, CircleBox(Fade(c.TextDisabled, 0.5f), r));
            Sb("grabber_highlight", typeName, CircleBox(Fade(c.TextDisabled, 0.8f), r));
            Sb("scroll", typeName, RoundBox(Fade(c.BgCanvas, 0.7f), 0));
        }

        // ═══════════════════════════════════════════════════════════════
        // TREE — 6 StyleBox states + 7 colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemeTree()
        {
            var c = _presetInstance!.Colors;
            string t = "Tree";
            var panelSb = PanelBox(c);
            Sb("panel", t, panelSb);
            Sb("selected", t, SelectedBox(c, 0.25f));
            Sb("selected_focus", t, SelectedBox(c, 0.40f, focus: true));
            Sb("cursor", t, SelectedBox(c, 0.25f));
            Sb("hover", t, SelectedBox(c, 0.25f));
            Sb("custom_button", t, SurfaceBox(c, c.SurfacePressed));
            Sb("custom_button_hover", t, SurfaceBox(c, c.SurfaceHover));
            Sb("custom_button_pressed", t, SurfaceBox(c, c.SurfacePressed));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_selected_color", c.TextOnDark);
            Col(t, "font_hover_color", c.TextHover); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "guide_color", c.BorderNormal); Col(t, "drop_position_color", c.AccentPrimary);
            Col(t, "relationship_line_color", c.BorderNormal);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // ITEMLIST — 6 StyleBox states + 6 colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemeItemList()
        {
            var c = _presetInstance!.Colors;
            string t = "ItemList";
            var panelSb = PanelBox(c);
            Sb("panel", t, panelSb);
            Sb("selected", t, SelectedBox(c, 0.25f));
            Sb("selected_focus", t, SelectedBox(c, 0.40f, focus: true));
            Sb("cursor", t, SelectedBox(c, 0.25f));
            Sb("hover", t, SelectedBox(c, 0.25f));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_selected_color", c.TextOnDark);
            Col(t, "font_hover_color", c.TextHover); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "guide_color", c.BorderNormal); Col(t, "drop_position_color", c.AccentPrimary);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // POPUPMENU — 3 StyleBox states + 7 colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemePopupMenu()
        {
            var c = _presetInstance!.Colors;
            string t = "PopupMenu";
            Sb("panel", t, PanelBox(c));
            Sb("hover", t, SelectedBox(c, 0.25f));
            Sb("separator", t, SeparatorBox(c));
            Col(t, "font_color", c.TextPrimary); Col(t, "font_hover_color", c.TextHover);
            Col(t, "font_disabled_color", c.TextDisabled); Col(t, "font_separator_color", c.BorderNormal);
            Col(t, "font_accelerator_color", c.TextDisabled); Col(t, "font_outline_color", c.ShadowColor);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // TABBAR / TABCONTAINER — 4 tab StyleBox states + 5 colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemeTabBar()
        {
            var c = _presetInstance!.Colors;
            string t = "TabBar";
            Sb("tab_unselected", t, SurfaceBox(c, c.SurfacePrimary));
            Sb("tab_selected", t, SurfaceBox(c, c.SurfaceHover));
            Sb("tab_disabled", t, SurfaceBox(c, c.SurfaceDisabled));
            Sb("tab_hovered", t, SurfaceBox(c, c.SurfaceHover));
            Sb("panel", t, PanelBox(c));
            Col(t, "font_color", c.TextDisabled); Col(t, "font_hovered_color", c.TextHover);
            Col(t, "font_selected_color", c.TextPrimary); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "font_outline_color", c.ShadowColor);
            FontSz(t);
        }

        private void ThemeTabContainer()
        {
            var c = _presetInstance!.Colors;
            string t = "TabContainer";
            Sb("panel", t, PanelBox(c));
            Sb("tab_selected", t, SurfaceBox(c, c.SurfaceHover));
            Sb("tab_unselected", t, SurfaceBox(c, c.SurfacePrimary));
            Sb("tab_disabled", t, SurfaceBox(c, c.SurfaceDisabled));
            Sb("tab_hovered", t, SurfaceBox(c, c.SurfaceHover));
            Col(t, "font_color", c.TextDisabled); Col(t, "font_hovered_color", c.TextHover);
            Col(t, "font_selected_color", c.TextPrimary); Col(t, "font_disabled_color", c.TextDisabled);
            Col(t, "font_outline_color", c.ShadowColor);
            FontSz(t);
        }

        // ═══════════════════════════════════════════════════════════════
        // PANEL / PANELCONTAINER — 1 StyleBox
        // ═══════════════════════════════════════════════════════════════
        private void ThemePanel()
        {
            var c = _presetInstance!.Colors;
            var p = _presetInstance!;
            Sb("panel", "Panel", SkinOr(UsePanelTextures, p.GetPanelTexture(), _skin?.Panel, PanelBox(c)));
        }

        private void ThemePanelContainer()
        {
            var c = _presetInstance!.Colors;
            var p = _presetInstance!;
            Sb("panel", "PanelContainer", SkinOr(UsePanelTextures, p.GetPanelTexture(), _skin?.Panel, PanelBox(c)));
        }

        // ═══════════════════════════════════════════════════════════════
        // SEPARATOR (H/V) — 1 StyleBox + constant
        // ═══════════════════════════════════════════════════════════════
        private void ThemeSeparator()
        {
            var c = _presetInstance!.Colors;
            int sep = ActiveShapes.Separator.Separation;
            var sb = StampGeometry(SeparatorBox(c));
            _generatedTheme!.SetStylebox("separator", "HSeparator", sb);
            _generatedTheme.SetStylebox("separator", "VSeparator", (StyleBoxFlat)sb.Duplicate());
            _generatedTheme.SetConstant("separation", "HSeparator", sep);
            _generatedTheme.SetConstant("separation", "VSeparator", sep);
        }

        // ═══════════════════════════════════════════════════════════════
        // WINDOW / DIALOG — border StyleBoxes + 5 colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemeWindow()
        {
            var c = _presetInstance!.Colors;
            string t = "Window";
            Sb("embedded_border", t, PanelBox(c));
            Sb("embedded_unfocused_border", t, PanelBox(c));
            Col(t, "title_color", c.TextPrimary); Col(t, "title_outline_color", c.ShadowColor);
            Col(t, "close_color", c.TextPrimary); Col(t, "close_hover_color", c.SemanticDanger);
            Col(t, "close_pressed_color", c.SemanticDanger);
        }

        // ═══════════════════════════════════════════════════════════════
        // SHARED PRIMITIVES (used by the per-node methods above — these are
        // low-level StyleBox constructors, NOT shared per-node themers)
        // ═══════════════════════════════════════════════════════════════

        private static readonly Color Clear = new(0, 0, 0, 0);

        private void Col(string type, string prop, Color val) => _generatedTheme!.SetColor(prop, type, val);
        private void FontSz(string type) => _generatedTheme!.SetFontSize("font_size", type, Fs);

        private StyleBoxFlat Box(Color bg, Color border, Color shadow, int shadowSize)
        {
            var sb = NewBox();
            sb.BgColor = bg; sb.BorderColor = border; sb.ShadowColor = shadow; sb.ShadowSize = shadowSize;
            return sb;
        }

        private StyleBoxFlat InputBox(Color bg, Color border, bool focus = false)
        {
            var s = ActiveShapes.Input;
            var sb = NewBox();
            sb.BgColor = bg; sb.BorderColor = border; sb.ShadowSize = 0;
            sb.ContentMarginLeft = Mathf.Max(s.MinX, _padL - s.InsetX);
            sb.ContentMarginRight = Mathf.Max(s.MinX, _padR - s.InsetX);
            sb.ContentMarginTop = Mathf.Max(s.MinY, _padT - s.InsetY);
            sb.ContentMarginBottom = Mathf.Max(s.MinY, _padB - s.InsetY);
            if (focus) { sb.BorderWidthLeft = Mathf.Max(s.FocusBorderMin, _bL); sb.BorderWidthRight = Mathf.Max(s.FocusBorderMin, _bR);
                         sb.BorderWidthTop = Mathf.Max(s.FocusBorderMin, _bT); sb.BorderWidthBottom = Mathf.Max(s.FocusBorderMin, _bB); }
            return sb;
        }

        private StyleBoxFlat PanelBox(ColorSchema c)
        {
            var sb = NewBox();
            sb.BgColor = c.BgPanel; sb.BorderColor = c.BorderNormal;
            sb.ShadowColor = c.ShadowColor; sb.ShadowSize = Mathf.Max(0, _shadowSize - ActiveShapes.Panel.ShadowReduction);
            return sb;
        }

        private StyleBoxFlat SurfaceBox(ColorSchema c, Color surface)
        {
            var sb = NewBox(); sb.BgColor = surface; sb.BorderColor = c.BorderNormal; return sb;
        }

        private StyleBoxFlat RoundBox(Color bg, int radius, int margin = -1)
        {
            int m = margin >= 0 ? margin : ActiveShapes.Progress.Margin;
            var sb = new StyleBoxFlat(); sb.BgColor = bg;
            sb.CornerRadiusTopLeft = radius; sb.CornerRadiusTopRight = radius;
            sb.CornerRadiusBottomRight = radius; sb.CornerRadiusBottomLeft = radius;
            sb.ContentMarginLeft = m; sb.ContentMarginRight = m;
            sb.ContentMarginTop = m; sb.ContentMarginBottom = m;
            return sb;
        }

        private StyleBoxFlat CircleBox(Color bg, int radius, int shadowSize = -1)
        {
            var sb = new StyleBoxFlat(); sb.BgColor = bg;
            sb.CornerRadiusTopLeft = radius; sb.CornerRadiusTopRight = radius;
            sb.CornerRadiusBottomRight = radius; sb.CornerRadiusBottomLeft = radius;
            sb.ShadowSize = shadowSize >= 0 ? shadowSize : ActiveShapes.Slider.GrabberShadow;
            sb.ShadowColor = _shadowColor;
            return sb;
        }

        private StyleBoxFlat SelectedBox(ColorSchema c, float alpha, bool focus = false)
        {
            var s = ActiveShapes.Selection;
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(c.AccentPrimary.R, c.AccentPrimary.G, c.AccentPrimary.B, alpha);
            int r = Mathf.Max(s.CornerMin, _gTL / s.CornerDivisor);
            sb.CornerRadiusTopLeft = r; sb.CornerRadiusTopRight = r;
            sb.CornerRadiusBottomRight = r; sb.CornerRadiusBottomLeft = r;
            sb.ContentMarginLeft = s.MarginX; sb.ContentMarginRight = s.MarginX;
            if (focus) { sb.BorderWidthLeft = s.FocusBorder; sb.BorderWidthRight = s.FocusBorder;
                         sb.BorderWidthTop = s.FocusBorder; sb.BorderWidthBottom = s.FocusBorder;
                         sb.BorderColor = c.BorderFocus; }
            return sb;
        }

        private StyleBoxFlat SeparatorBox(ColorSchema c)
        {
            var sb = new StyleBoxFlat(); sb.BgColor = c.BorderNormal; return sb;
        }

        private static Color Fade(Color c, float a) => new(c.R, c.G, c.B, a);
    }
}
