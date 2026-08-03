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
        /// theme's own font_size from theme.json, then 14 as last-resort fallback.
        ///
        /// The override test is <c>&gt; 0</c>, not <c>&gt;= 0</c>. The unset sentinel is -1, so
        /// <c>&gt;= 0</c> let a literal 0 through as if it were a real size — and a font size of 0
        /// reaches Godot's text server, which then rejects it once PER GLYPH PER FRAME
        /// (<c>p_size.x &lt;= 0</c>) rather than failing loudly. No shipped geometry.json declares
        /// 0, so this is defensive; the cost of it being wrong is a scene that cannot finish a
        /// frame, which is not a failure mode worth leaving reachable.</summary>
        private int Fs => _geometry != null && _geometry.FontSize > 0
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
                if (jsonTex != null) return WithThemePadding(jsonTex);
                // 2. Inspector UISkin texture (drag-and-drop Texture2D).
                if (skinTex != null && _skin != null && _skin.HasTextures)
                {
                    var sb = _skin.BuildStyleBox(skinTex);
                    if (sb != null) return WithThemePadding(sb);
                }
            }
            // No StampGeometry here: Sb() stamps EVERY box it is handed, textured or flat.
            // Stamping only this branch is what made textured and procedural controls two
            // different visual registers.
            return procedural;
        }

        /// <summary>Give a texture box the theme's own content padding when it declares none.
        ///
        /// A <see cref="StyleBoxTexture"/> with unset (-1) content margins falls back to its
        /// 9-PATCH margins. Those exist to stop corner artwork stretching and are routinely
        /// 18-28px, so a control took 36-56px of padding purely because of how its art was
        /// sliced: 64px buttons and 52px inputs. 49 of the 50 shipped themes leave content
        /// margins unset, and this went unnoticed only because textured buttons never rendered
        /// at all until ApplyButtonOverrides was fixed — the fat controls arrived with the fix.
        ///
        /// Artwork geometry and type padding are unrelated concerns; padding belongs to the
        /// geometry profile. An explicit content_margin_* in theme.json still wins.</summary>
        private StyleBox WithThemePadding(StyleBox sb)
        {
            if (sb is not StyleBoxTexture tex) return sb;
            if (tex.ContentMarginLeft < 0) tex.ContentMarginLeft = _padL;
            if (tex.ContentMarginRight < 0) tex.ContentMarginRight = _padR;
            if (tex.ContentMarginTop < 0) tex.ContentMarginTop = _padT;
            if (tex.ContentMarginBottom < 0) tex.ContentMarginBottom = _padB;
            return sb;
        }

        private StyleBox StampGeometry(StyleBox sb) => StampGeometry(sb, "", "");

        /// <summary>Put a box into the shared register. <paramref name="slot"/> is the theme
        /// item name ("normal", "hover", "panel", "fill"…) and <paramref name="type"/> the
        /// control type, so a TEXTURED box can be tinted with the exact palette colour its
        /// procedural twin would have been filled with.</summary>
        private StyleBox StampGeometry(StyleBox sb, string slot, string type)
        {
            if (_geometry != null && sb is StyleBoxFlat flat) _geometry.ApplyTo(flat);
            ApplyGameArtRegister(sb, slot, type);
            return sb;
        }

        /// <summary>Push every generated box into the register the reference kits share.
        ///
        /// Deliberately at the CHOKE POINT rather than in each Theme*() method: every component
        /// type — Button, Panel, ProgressBar, TabBar, LineEdit, Tree, PopupMenu — routes its
        /// stylebox through StampGeometry, so applying the register here is what makes the whole
        /// UI consistent, instead of only whichever controls someone remembered to patch.
        /// Patching individual scenes was the wrong layer for exactly this reason.
        ///
        /// What every kit in Example_Art/gameui1..7 has and a hairline-bordered box does not: an
        /// outline distinctly DARKER than the fill, and a real drop shadow. Both are derived from
        /// the ACTIVE palette, so all 50 themes stay distinguishable, and the genre's own
        /// geometry profile still owns radius, padding and weight.</summary>
        private void ApplyGameArtRegister(StyleBox sb, string slot, string type)
        {
            if (!GameArtChrome || _presetInstance == null) return;

            var c = _presetInstance.Colors;

            switch (sb)
            {
                case StyleBoxFlat flat: FlatRegister(flat, c); break;
                case StyleBoxTexture tex: TextureRegister(tex, SurfaceForSlot(slot, type, c)); break;
            }
        }

        private void FlatRegister(StyleBoxFlat flat, ColorSchema c)
        {
            // Ink derived from the palette, not a fixed black: a parchment theme wants a brown
            // outline and a sci-fi theme a blue-black one. One hardcoded colour would flatten
            // the difference between all 50 skins into the same drawn-on border.
            static float Lum(Color x) => 0.2126f * x.R + 0.7152f * x.G + 0.0722f * x.B;
            Color basis = Lum(c.SurfacePrimary) > 0.5f ? c.SurfacePrimary : c.BorderNormal;
            var ink = new Color(basis.R * 0.22f, basis.G * 0.24f, basis.B * 0.28f, 1f);

            flat.BorderColor = ink;
            int w = Mathf.Max(GameArtOutline, flat.BorderWidthTop);
            flat.BorderWidthLeft = flat.BorderWidthRight = w;
            flat.BorderWidthTop = flat.BorderWidthBottom = w;

            // Only where the geometry profile did not already ask for one, so a genre that runs
            // deliberately flat (the pixel-art registers) keeps its own choice.
            if (flat.ShadowSize <= 0 && GameArtShadow > 0)
            {
                flat.ShadowColor = new Color(0, 0, 0, 0.38f);
                flat.ShadowSize = GameArtShadow;
                flat.ShadowOffset = new Vector2(0, Mathf.Max(1, GameArtShadow / 2));
            }
        }

        /// <summary>Shared with the components that DRAW rather than hand Godot a StyleBox, so
        /// the tint applied here and the colour they read back out cannot drift apart.</summary>
        private const float ArtNominalLuminance = UiSurface.ArtNominalLuminance;

        /// <summary>Tint a textured box with the SAME palette colour its procedural twin uses.
        ///
        /// Every theme inside a genre ships a BYTE-IDENTICAL button/panel texture — all five
        /// citybuilder skins hash to e692a3da, all five rpg skins to 43d6427f. So until now the
        /// theme and palette reached only the flat controls; every Button, PanelContainer,
        /// LineEdit, ProgressBar, Slider and ScrollBar stayed the same pale grey-blue in all 50
        /// skins. That is why switching skin appeared to do nothing to a menu.
        ///
        /// modulate MULTIPLIES, so the art's baked outline, bevel and inner shadow survive — only
        /// the flat mid-tone is recoloured, which is exactly how a greyscale UI kit is meant to be
        /// re-skinned. The colour is scaled up by the art's own mid-tone so the result LANDS on
        /// the palette colour instead of 0.8x of it, and the scale is applied uniformly across
        /// RGB and capped at the peak channel, so hue is preserved and no channel ever clips.</summary>
        private static void TextureRegister(StyleBoxTexture tex, Color surface)
        {
            float peak = Mathf.Max(surface.R, Mathf.Max(surface.G, surface.B));
            float k = 1f / ArtNominalLuminance;
            if (peak > 0.001f && peak * k > 1f) k = 1f / peak;
            // Alpha carried through, not forced to 1: menu surfaces are opaque so nothing
            // changes there, but a HUD plate is deliberately translucent so the world reads
            // through it, and clamping it opaque would black out the game behind the HUD.
            tex.ModulateColor = new Color(surface.R * k, surface.G * k, surface.B * k, surface.A);
        }

        /// <summary>The palette colour a given theme slot is filled with. Mirrors the fills the
        /// procedural boxes above use, so a textured control and a flat one on the same screen
        /// are the same colour in the same state — one register, not two.</summary>
        private static Color SurfaceForSlot(string slot, string type, ColorSchema c)
        {
            // Inputs are a recessed well, not a raised face — InputBox() fills them with
            // SurfacePressed, so their art has to be tinted to match or a textured LineEdit
            // reads as a button.
            if (type is "LineEdit" or "TextEdit" or "SpinBox")
                return slot == "read_only" ? Fade(c.SurfaceDisabled, 0.6f) : c.SurfacePressed;

            return slot switch
            {
                "hover" or "tab_hovered" or "grabber_highlight" or "custom_button_hover" => c.SurfaceHover,
                "pressed" or "grabber_pressed" or "custom_button_pressed" => c.SurfacePressed,
                "disabled" or "tab_disabled" => c.SurfaceDisabled,
                "panel" or "tab_selected" or "embedded_border" or "embedded_unfocused_border" => c.BgPanel,
                "background" or "slider" or "scroll" or "read_only" => c.SurfaceDisabled,
                "fill" => c.AccentPrimary,
                "separator" => c.BorderNormal,
                _ => c.SurfacePrimary,
            };
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
            ButtonFontColors(t, c, c.BorderBevelDark);
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
            ButtonFontColors(t, c, c.ShadowColor);
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
            ButtonFontColors(t, c, c.ShadowColor);
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
            ButtonFontColors(t, c, c.ShadowColor);
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
            ButtonFontColors(t, c, c.ShadowColor);
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
            ButtonFontColors(t, c, c.ShadowColor);
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
            var p = _presetInstance!;
            int r = (_gTL + _gTR) / 2;
            Sb("grabber_area", typeName, SkinOr(UseSliderTextures, p.GetSliderGrabberTexture(), _skin?.SliderGrabber, CircleBox(c.AccentPrimary, r)));
            // Highlight is a BRIGHTER primary, not the secondary accent. Using AccentSecondary
            // changed the filled portion's HUE on hover/focus — on urban the focused Master
            // slider went green while the two beside it stayed blue, which reads as three
            // unrelated controls rather than one focused.
            Sb("grabber_area_highlight", typeName, CircleBox(c.AccentPrimary.Lightened(0.22f), r, ActiveShapes.Slider.GrabberHoverShadow));
            Sb("slider", typeName, RoundBox(c.SurfaceDisabled, Mathf.Max(2, _gTL / 2)));
            Col(typeName, "tick_color", c.BorderNormal);
        }

        // ═══════════════════════════════════════════════════════════════
        // SCROLLBAR (H/V) — grabber + track
        // ═══════════════════════════════════════════════════════════════
        /// <summary>Scrollbar thickness in px. A scrollbar is chrome, not content, so it must
        /// NOT inherit the theme's content padding — but it did: <see cref="StampGeometry"/>
        /// stamps the geometry profile's ContentPadding onto every StyleBoxFlat, so a 14px
        /// padding produced a 28px-wide bar, and the 9-patch grabber texture forced 22px more
        /// on top. Every scrollable list had a fat grey column down its side. A scrollbar is a
        /// pill of fixed thickness; it does not scale with type padding.</summary>
        private const int ScrollBarThickness = 10;

        private void ThemeScrollBar(string typeName)
        {
            var c = _presetInstance!.Colors;
            var p = _presetInstance!;
            int r = ScrollBarThickness / 2;
            var grabber = SkinOr(UseScrollBarTextures, p.GetScrollGrabberTexture(), _skin?.ScrollGrabber,
                                 CircleBox(Fade(c.TextDisabled, 0.55f), r, shadowSize: 0));
            var hover = CircleBox(Fade(c.TextDisabled, 0.85f), r, shadowSize: 0);
            var pressed = CircleBox(Fade(c.AccentPrimary, 0.85f), r, shadowSize: 0);
            // The track was BgCanvas at 0.7 alpha — a near-solid gutter on a light theme. A
            // hairline tint reads as a groove without competing with the list beside it.
            var track = RoundBox(Fade(c.TextDisabled, 0.12f), r, 0);

            Sb("grabber", typeName, grabber);
            Sb("grabber_highlight", typeName, hover);
            Sb("grabber_pressed", typeName, pressed);
            Sb("scroll", typeName, track);

            // AFTER Sb: Sb -> StampGeometry is what injects the content padding, so the
            // thickness has to be restamped once the geometry profile has had its say.
            foreach (var box in new StyleBox[] { grabber, hover, pressed, track })
                SetPadding(box, r, r);
        }

        /// <summary>Set a StyleBox's content padding. Works for flat AND texture boxes — a
        /// StyleBoxTexture with unset (-1) content margins falls back to its 9-patch texture
        /// margins, which exist to protect corner artwork and have nothing to do with the
        /// padding a control needs around its text.</summary>
        private static void SetPadding(StyleBox sb, float x, float y)
        {
            sb.ContentMarginLeft = x; sb.ContentMarginRight = x;
            sb.ContentMarginTop = y; sb.ContentMarginBottom = y;
        }

        // ═══════════════════════════════════════════════════════════════
        // Readable text — contrast against the surface a label actually sits on
        // ═══════════════════════════════════════════════════════════════

        /// <summary>WCAG AA for normal text. A label below this is not "low contrast", it is
        /// unreadable.</summary>
        private const float MinTextContrast = 4.5f;

        /// <summary>WCAG relative luminance.</summary>
        private static float Luminance(Color c)
        {
            static float Ch(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Ch(c.R) + 0.7152f * Ch(c.G) + 0.0722f * Ch(c.B);
        }

        /// <summary>WCAG contrast ratio, 1.0 (identical) to 21.0 (black on white).</summary>
        private static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        /// <summary>The text colour to use on <paramref name="surface"/>.
        ///
        /// Honours the theme author's <paramref name="preferred"/> colour whenever it actually
        /// reads, and otherwise falls back to whichever of the theme's own two text colours
        /// contrasts better. Nothing is invented — the result is always a colour the theme
        /// already defines, so a palette tint still carries through.
        ///
        /// This exists because <c>font_hover_color</c> and <c>font_pressed_color</c> were wired
        /// straight to <c>TextHover</c>, which nearly every light theme defines as pure white
        /// while ALSO defining a near-white hover surface. urban put #FFFFFF text on #F5F8FA:
        /// a contrast ratio of 1.07, i.e. the button's label vanished the moment you hovered it.
        /// 11 of the 50 shipped themes failed this way, and pressed was worse because it reused
        /// the same colour against an even lighter surface.</summary>
        private static Color ReadableOn(ColorSchema c, Color surface, Color preferred,
                                        float min = MinTextContrast)
        {
            if (Contrast(surface, preferred) >= min) return preferred;

            Color best = Contrast(surface, c.TextPrimary) >= Contrast(surface, c.TextOnDark)
                ? c.TextPrimary : c.TextOnDark;
            if (Contrast(surface, best) >= min) return best;

            // Neither of the theme's two text colours reads on this surface, so picking the
            // least-bad one still leaves it unreadable. That is what left 8 of 350
            // theme x palette pairs below AA — worst 3.36 on puzzle/candy/dark, where a dark
            // palette pulls a pink surface and both text candidates toward each other.
            //
            // Derive instead: keep the chosen colour's HUE, so the theme's identity survives,
            // and push its value away from the surface until it clears. Returned only if it
            // actually beats the candidate, so this can never make a case worse.
            // Try BOTH directions and keep whichever actually reads. Choosing the direction
            // from the surface's luminance alone was wrong three different ways:
            //   - dark text on a mid surface got LIGHTENED toward the surface (candy/pastel)
            //   - text already at pure white cannot lighten further; going dark clears easily
            //   - a fully saturated hue (scifi's #0067ff) cannot brighten by value at all,
            //     so once value maxes the push has to relieve SATURATION toward white
            Color up = Push(best, surface, true, min);
            Color down = Push(best, surface, false, min);
            Color pick = Contrast(surface, up) >= Contrast(surface, down) ? up : down;
            return Contrast(surface, pick) > Contrast(surface, best) ? pick : best;
        }

        /// <summary>Walk a colour away from a surface until it reads, keeping its hue.</summary>
        private static Color Push(Color from, Color surface, bool lighten, float min)
        {
            from.ToHsv(out float h, out float sat, out float v);
            Color cur = from;
            for (int i = 0; i < 30 && Contrast(surface, cur) < min; i++)
            {
                if (lighten)
                {
                    if (v < 1f) v = Mathf.Min(1f, v + 0.04f);
                    else if (sat > 0f) sat = Mathf.Max(0f, sat - 0.06f);
                    else break;                       // already white
                }
                else if (v > 0f) v = Mathf.Max(0f, v - 0.04f);
                else break;                           // already black
                cur = Color.FromHsv(h, sat, v, from.A);
            }
            return cur;
        }

        /// <summary>Floor for DISABLED text. Deliberately far below AA: muted-to-the-point-of-
        /// unreadable IS the signal that a control is unavailable. Holding disabled labels to
        /// the same 4.5 as live ones repainted them in full-strength body colour, so a disabled
        /// Save button looked perfectly clickable. This only catches text that has vanished
        /// outright into its own surface.</summary>
        private const float MinDisabledContrast = 1.6f;

        /// <summary>Every font colour for one button-like type, each checked against the surface
        /// of the state it belongs to. Was six duplicated three-line blocks that all shared the
        /// same defect.</summary>
        /// <summary>The colour a textured control ACTUALLY renders, given the art register.
        ///
        /// TextureRegister sets modulate = surface / 0.8, but caps the boost at 1/peak so no
        /// channel clips. On a surface with a channel at 255 the cap binds, the boost becomes
        /// 1.0, and the control renders art(0.8) x surface — up to 20% DARKER than the schema
        /// colour. puzzle/candy is the clean example: modulate #ff69b5, rendered #cc5491.
        ///
        /// Font colours were being contrast-checked against the schema colour, so every check
        /// on a bright theme was optimistic by that margin. This returns what the player sees.</summary>
        private Color EffectiveSurface(Color surface)
        {
            // A procedural box paints the schema colour exactly; only the textured path shifts.
            if (!UseTextures) return surface;
            float peak = Mathf.Max(surface.R, Mathf.Max(surface.G, surface.B));
            float k = 1f / ArtNominalLuminance;
            if (peak > 0.001f && peak * k > 1f) k = 1f / peak;
            float f = ArtNominalLuminance * k;      // 1.0 when unclamped, < 1 when the cap binds
            return new Color(surface.R * f, surface.G * f, surface.B * f, surface.A);
        }

        private void ButtonFontColors(string t, ColorSchema c, Color outline)
        {
            Col(t, "font_color", ReadableOn(c, EffectiveSurface(c.SurfacePrimary), c.TextPrimary));
            Col(t, "font_hover_color", ReadableOn(c, EffectiveSurface(c.SurfaceHover), c.TextHover));
            Col(t, "font_pressed_color", ReadableOn(c, EffectiveSurface(c.SurfacePressed), c.TextHover));
            Col(t, "font_disabled_color", ReadableOn(c, EffectiveSurface(c.SurfaceDisabled), c.TextDisabled, MinDisabledContrast));
            // Focus keeps its accent tint only while that is legible on the focused face.
            Col(t, "font_focus_color", ReadableOn(c, EffectiveSurface(c.SurfacePrimary), c.AccentSecondary));
            Col(t, "font_outline_color", outline);
        }

        /// <summary>Gap between adjacent tabs.</summary>
        private const int TabGap = 6;

        /// <summary>The four tab-state boxes, built as TABS rather than as generic surfaces.
        ///
        /// They were plain SurfaceBoxes, which meant they inherited the geometry profile's drop
        /// shadow — 6px at a +3 y-offset on citybuilder. A tab strip is a row of touching
        /// controls with only a few px between them, so every tab cast its shadow across the gap
        /// and onto its neighbours: the tabs looked like they were overlapping each other.
        /// A tab is attached to the panel below it; it has nothing to cast a shadow onto.
        ///
        /// Also squares off the BOTTOM corners and rounds only the top, and paints the selected
        /// tab in the panel's own colour so it merges into the content area beneath — the
        /// selected/unselected pair used to be SurfaceHover vs SurfacePrimary, two nearly
        /// identical light greys, so which tab was active was barely legible.</summary>
        private void TabStyles(string t, ColorSchema c)
        {
            var sel = TabBox(c, c.BgPanel, c.BorderNormal);
            var uns = TabBox(c, c.SurfacePressed, Fade(c.BorderNormal, 0.55f));
            var hov = TabBox(c, c.SurfaceHover, c.BorderHover);
            var dis = TabBox(c, c.SurfaceDisabled, Fade(c.BorderNormal, 0.35f));
            Sb("tab_selected", t, sel);
            Sb("tab_unselected", t, uns);
            Sb("tab_hovered", t, hov);
            Sb("tab_disabled", t, dis);
            // AFTER Sb, because Sb -> StampGeometry re-applies the geometry profile's shadow,
            // corner radius and border widths over whatever the box was built with. Shaping the
            // box before that call silently achieved nothing — the shadow came straight back.
            foreach (var b in new[] { sel, uns, hov, dis }) ShapeAsTab(b);
        }

        private StyleBoxFlat TabBox(ColorSchema c, Color bg, Color border)
        {
            var sb = NewBox();
            sb.BgColor = bg;
            sb.BorderColor = border;
            return ShapeAsTab(sb);
        }

        /// <summary>Make a box behave like a tab: no drop shadow, rounded top, square bottom,
        /// no bottom border. A tab is welded to the panel beneath it — it neither floats nor
        /// casts a shadow on the tab beside it.</summary>
        private static StyleBoxFlat ShapeAsTab(StyleBoxFlat sb)
        {
            sb.ShadowSize = 0;
            sb.ShadowOffset = Vector2.Zero;
            sb.CornerRadiusBottomLeft = 0;
            sb.CornerRadiusBottomRight = 0;
            sb.BorderWidthBottom = 0;
            return sb;
        }

        /// <summary>Font colours for a tab strip, contrast-checked like every other control.
        ///
        /// An UNSELECTED tab was painted with <c>TextDisabled</c> — but unselected is not
        /// disabled, and using the disabled colour made "Display", "Game" and "Controls" read
        /// as greyed-out/unavailable next to the active tab. It is now the normal text colour
        /// held back to 78% alpha: clearly recessive, still legible. Hover went straight to
        /// <c>TextHover</c>, which is the same white-on-near-white trap that made button
        /// labels vanish, so it routes through ReadableOn too.</summary>
        private void TabFontColors(string t, ColorSchema c)
        {
            // "font_color" DOES NOT EXIST on TabBar/TabContainer. Godot names the idle tab
            // colour "font_unselected_color" (verified against ThemeDB.get_default_theme()),
            // so every value written to "font_color" here was silently discarded and the idle
            // tabs kept Godot's own dim grey — which is why "Display"/"Game"/"Controls" read
            // as greyed-out next to the active tab regardless of the theme.
            Color idle = ReadableOn(c, c.SurfacePrimary, c.TextPrimary);
            Color selected = ReadableOn(c, c.SurfaceHover, c.TextPrimary);
            Color hovered = ReadableOn(c, c.SurfaceHover, c.TextHover);
            Color disabled = ReadableOn(c, c.SurfaceDisabled, c.TextDisabled, MinDisabledContrast);

            // Idle tabs are recessive but legible — 78% alpha, not the disabled colour. An
            // unselected tab is a place you can go, not one you cannot.
            Col(t, "font_unselected_color", new Color(idle, 0.78f));
            Col(t, "font_selected_color", selected);
            Col(t, "font_hovered_color", hovered);
            Col(t, "font_disabled_color", disabled);
            Col(t, "font_outline_color", c.ShadowColor);

            // Icons follow the text, or a tab with an icon shows it at full strength while its
            // label is dimmed.
            Col(t, "icon_unselected_color", new Color(idle, 0.78f));
            Col(t, "icon_selected_color", selected);
            Col(t, "icon_hovered_color", hovered);
            Col(t, "icon_disabled_color", disabled);
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
            TabStyles(t, c);
            Sb("panel", t, PanelBox(c));
            TabFontColors(t, c);
            // TabBar's own gap between tabs. Left at Godot's default 4 the tabs sat almost
            // flush, which is what let their drop shadows run into each other.
            _generatedTheme!.SetConstant("h_separation", t, TabGap);
            FontSz(t);
        }

        private void ThemeTabContainer()
        {
            var c = _presetInstance!.Colors;
            string t = "TabContainer";
            Sb("panel", t, PanelBox(c));
            TabStyles(t, c);
            TabFontColors(t, c);
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
            var p = _presetInstance!;
            int sep = ActiveShapes.Separator.Separation;
            var sb = SkinOr(UseSeparatorTextures, p.GetSeparatorTexture(), _skin?.Separator, SeparatorBox(c));
            // Through Sb like every other type, rather than straight to SetStylebox: the direct
            // call skipped the register, so separators were the one control the theme never
            // reached. Duplicated BEFORE stamping so each copy is stamped exactly once.
            Sb("separator", "HSeparator", (StyleBox)sb.Duplicate());
            Sb("separator", "VSeparator", (StyleBox)sb.Duplicate());
            _generatedTheme!.SetConstant("separation", "HSeparator", sep);
            _generatedTheme.SetConstant("separation", "VSeparator", sep);
        }

        // ═══════════════════════════════════════════════════════════════
        // WINDOW / DIALOG — border StyleBoxes + 5 colors
        // ═══════════════════════════════════════════════════════════════
        private void ThemeWindow()
        {
            var c = _presetInstance!.Colors;
            var p = _presetInstance!;
            string t = "Window";
            Sb("embedded_border", t, SkinOr(UseDialogTextures, p.GetDialogTexture(), _skin?.Dialog, PanelBox(c)));
            Sb("embedded_unfocused_border", t, SkinOr(UseDialogTextures, p.GetDialogTexture(), _skin?.Dialog, PanelBox(c)));
            Col(t, "title_color", c.TextPrimary); Col(t, "title_outline_color", c.ShadowColor);
            Col(t, "close_color", c.TextPrimary); Col(t, "close_hover_color", c.SemanticDanger);
            Col(t, "close_pressed_color", c.SemanticDanger);
        }

        // ═══════════════════════════════════════════════════════════════
        // SEMANTIC ROLES — the palette's meaning colours, published so that
        // components can ask for a ROLE instead of carrying a literal
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Publish the palette's meaning colours under a type any Control can query.
        ///
        /// All 50 themes define semantic_success/warning/danger/info and NOTHING read them, so
        /// every component that needed "the green one" carried its own literal — five badges in
        /// citybuilder_main.tscn each hardcoded a Color(), which is a palette living in a scene
        /// file where no skin can reach it. A scene should declare the ROLE and the theme should
        /// supply the colour; this is what makes that possible.</summary>
        private void ThemeSemantics()
        {
            var c = _presetInstance!.Colors;
            string t = UiSurface.SemanticType;
            Col(t, "success", c.SemanticSuccess);
            Col(t, "warning", c.SemanticWarning);
            Col(t, "danger", c.SemanticDanger);
            Col(t, "info", c.SemanticInfo);
            Col(t, "accent", c.AccentPrimary);
            Col(t, "accent2", c.AccentSecondary);
            Col(t, "neutral", c.SurfacePrimary);
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
