using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
	[Tool]
	[GlobalClass]
	public partial class ThemePresetComponent : UIComponent
	{
		/// <summary>Theme preset name (e.g. "cartoon", "modern"). Resolved from the
		/// file-based skin catalog at runtime. Free-form string — any theme.json in
		/// the skins/ tree works. Set alongside GenreName so the catalog knows where
		/// to look. Falls back to the genre's default_theme if not found.</summary>
		[Export]
		public string PresetName
		{
			get => _presetName;
			// Palette options depend on the selected theme — refresh the list so the
			// PaletteName dropdown re-cascades.
			set { _presetName = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); if (IsInsideTree()) ApplyTheme(); }
		}
		private string _presetName = "modern";

		/// <summary>Genre this component belongs to (e.g. "platformer"). Determines
		/// which genre's theme tree to load from. Falls back to "platformer".</summary>
		[Export]
		public string GenreName
		{
			get => _genreName;
			// Theme/palette/geometry options all hang off the genre — refresh the list
			// so those dropdowns re-cascade.
			set { _genreName = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); if (IsInsideTree()) ApplyTheme(); }
		}
		private string _genreName = "platformer";

		[Export] public bool EnableAnimations { get; set; } = true;
		[Export] public bool EnableRippleOnClick { get; set; } = true;

		/// <summary>OPTIONAL color-palette variant. Resolved from the file-based skin
		/// catalog (palettes live in skins/&lt;genre&gt;/themes/&lt;theme&gt;/&lt;palette&gt;.json).
		/// Retints the whole theme in HSV space. "Default" = no tint.</summary>
		[Export]
		public string PaletteName
		{
			get => _paletteName;
			set { _paletteName = value; if (IsInsideTree()) ApplyTheme(); }
		}
		private string _paletteName = "Default";

		/// <summary>OPTIONAL geometry/shape override. Resolved from the file-based skin
		/// catalog's per-genre geometry.json. Overrides corner radius/border/shadow/
		/// padding/font — independent of theme color. "As-Authored" = use the theme's
		/// own geometry.</summary>
		[Export]
		public string GeometryProfileName
		{
			get => _geometryProfileName;
			set { _geometryProfileName = value; if (IsInsideTree()) ApplyTheme(); }
		}
		private string _geometryProfileName = "As-Authored";

		private GeometryProfile? _geometry;

		// ── Per-genre shape overrides (from geometry.json's "shapes" block).
		// Defaults match the legacy hardcoded literals so a genre that omits the
		// "shapes" block remains a visual no-op. See ShapeOverrides.cs. ──
		private static readonly ShapeOverrides _emptyShapes = new();
		/// <summary>Active per-genre shape overrides. Never null — falls back to
		/// <see cref="_emptyShapes"/> (legacy defaults) when no geometry loaded.</summary>
		private ShapeOverrides ActiveShapes => _geometry?.Shapes ?? _emptyShapes;

		// ── Background image (from geometry.json's "background_image" + "background_mode"). ──
		// Spawned as the first child of the themed subtree root, behind everything
		// else, full-rect anchored. Reused across re-themes so we don't leak nodes.
		private TextureRect? _backgroundRect;

		/// <summary>OPTIONAL texture skin. When set (via GameApp or directly), the theme
		/// engine builds StyleBoxTexture (9-patch) for nodes with a matching texture
		/// slot, instead of procedural StyleBoxFlat. Pushed by GameInfoBinder from GameApp.Skin.</summary>
		[Export]
		public UISkin? Skin
		{
			get => _skin;
			set { _skin = value; if (IsInsideTree()) ApplyTheme(); }
		}
		private UISkin? _skin;

		/// <summary>MASTER on/off for texture skinning on THIS component. Set false to
		/// force all nodes in this subtree to use the procedural theme (colors + geometry),
		/// ignoring the UISkin entirely. Per-scene kill switch — e.g. turn off textures
		/// in the pause menu but keep them in the main menu.</summary>
		[Export]
		public bool UseTextures
		{
			get => _useTextures;
			set { _useTextures = value; if (IsInsideTree()) ApplyTheme(); }
		}
		private bool _useTextures = true;

		/// <summary>Per-node-type texture toggles — these are PER-SCENE (on this component),
		/// independent of the global UISkin. Turn off e.g. UsePanelTextures here to make
		/// THIS scene's panels use procedural boxes while buttons stay textured.
		/// Only effective when UseTextures = true AND a Skin is set.</summary>
		[ExportGroup("Per-Node Texture Toggles")]
		[Export] public bool UseButtonTextures { get; set; } = true;
		[Export] public bool UsePanelTextures { get; set; } = true;
		[Export] public bool UseInputTextures { get; set; } = true;
		[Export] public bool UseProgressBarTextures { get; set; } = true;
		[Export] public bool UseDialogTextures { get; set; } = true;
		[Export] public bool UseSliderTextures { get; set; } = true;
		[Export] public bool UseScrollBarTextures { get; set; } = true;
		[Export] public bool UseSeparatorTextures { get; set; } = true;

		[Signal] public delegate void ThemeAppliedEventHandler();

		private IThemePreset? _presetInstance;
		/// <summary>Geometry template from the loaded theme.json (used for font size fallback).</summary>
		private ThemeGeometry _loadedThemeGeometry;
		private Godot.Control? _targetControl;
		private bool _isSingleButton;
		private Theme? _generatedTheme;
		private readonly Dictionary<Button, Tween?> _activeTweens = new();

		public override void _Ready()
		{
			base._Ready();
			_targetControl = GetParent() as Godot.Control;
			if (_targetControl != null) ApplyTheme();
		}

		public override void _ExitTree()
		{
			foreach (var kvp in _activeTweens) kvp.Value?.Kill();
			_activeTweens.Clear();
			if (_backgroundRect != null && GodotObject.IsInstanceValid(_backgroundRect))
				_backgroundRect.QueueFree();
			_backgroundRect = null;
			base._ExitTree();
		}

		public void ApplyTheme()
		{
			if (_targetControl == null || !IsActive) return;

			// Load the theme from the file-based skin catalog. This replaces the old
			// enum → CreatePresetInstance switch. Falls back to the genre's default
			// theme if PresetName isn't found.
			var themeDef = SkinCatalog.GetTheme(_genreName, _presetName);
			if (themeDef == null)
			{
				var genre = SkinCatalog.GetGenre(_genreName);
				themeDef = genre != null && genre.Themes.TryGetValue(genre.DefaultTheme, out var dt) ? dt : null;
			}
			if (themeDef == null) return;
			_presetInstance = new FileThemePreset(themeDef);
			_loadedThemeGeometry = themeDef.Geometry;

			// Apply the optional color palette. Looked up from this theme's palette
			// files in the skins tree (loaded by SkinCatalog). If not found there,
			// ColorPalette.ByName searches all genres/themes as a cross-fallback.
			if (!string.IsNullOrEmpty(_paletteName)
				&& !_paletteName.Equals("Default", StringComparison.OrdinalIgnoreCase))
			{
				ColorPalette? palette = null;
				if (themeDef.Palettes.TryGetValue(_paletteName.ToLowerInvariant(), out var filePal))
					palette = filePal;
				else if (ColorPalette.ByName(_paletteName) is { } catalogPal)
					palette = catalogPal;
				if (palette != null)
					_presetInstance = new PaletteTintedPreset(_presetInstance, palette);
			}

			// Resolve the optional geometry profile from this genre's geometry.json
			// (loaded by SkinCatalog). GeometryProfile.ByName searches all genres.
			_geometry = null;
			if (!string.IsNullOrEmpty(_geometryProfileName)
				&& !_geometryProfileName.Equals("As-Authored", StringComparison.OrdinalIgnoreCase))
			{
				GeometryProfile? geo = null;
				var genre = SkinCatalog.GetGenre(_genreName);
				if (genre?.Geometry != null && genre.Geometry.DisplayName.Equals(_geometryProfileName, StringComparison.OrdinalIgnoreCase))
					geo = genre.Geometry.ToProfile();
				else if (GeometryProfile.ByName(_geometryProfileName) is { } catalogGeo)
					geo = catalogGeo;
				if (geo != null && geo.HasOverrides)
					_geometry = geo;
			}
			_isSingleButton = _targetControl is Button;
			if (_isSingleButton) ApplyToSingleButton((Button)_targetControl);
			else ApplyToSubtree(_targetControl);
			EmitSignal(SignalName.ThemeApplied);
		}

		// ═══════════════════════════════════════════════
		// Subtree Mode
		// ═══════════════════════════════════════════════

		private void ApplyToSubtree(Godot.Control root)
		{
			var preset = _presetInstance!;
			_generatedTheme = new Theme();
			ExtractGeometry(preset.GetButtonNormal());
			ApplyBackground();

			// Each UI node type themed by its OWN dedicated method — all colors,
			// all StyleBox backgrounds, and geometry for that type in one place.
			ThemeButton();
			ThemeCheckButton();
			ThemeCheckBox();
			ThemeOptionButton();
			ThemeMenuButton();
			ThemeColorPickerButton();
			ThemeLabel();
			ThemeRichTextLabel();
			ThemeLineEdit();
			ThemeTextEdit();
			ThemeSpinBox();
			ThemeProgressBar();
			ThemeSlider("HSlider");
			ThemeSlider("VSlider");
			ThemeScrollBar("HScrollBar");
			ThemeScrollBar("VScrollBar");
			ThemeTree();
			ThemeItemList();
			ThemePopupMenu();
			ThemeTabBar();
			ThemeTabContainer();
			ThemePanel();
			ThemePanelContainer();
			ThemeSeparator();
			ThemeWindow();

			root.Theme = _generatedTheme;

			if (EnableAnimations || EnableRippleOnClick)
				InjectIntoButtons(root);
      		// Per-node overrides for immediate editor visibility
			ApplyButtonOverrides(root, preset);
				ApplyButtonOverrides(this, preset);
		}

      	private void ApplyButtonOverrides(Node node, IThemePreset preset)
		{
			if (node is Button btn)
			{
				btn.AddThemeStyleboxOverride("normal", Duplicate(preset.GetButtonNormal()));
				btn.AddThemeStyleboxOverride("hover", Duplicate(preset.GetButtonHover()));
				btn.AddThemeStyleboxOverride("pressed", Duplicate(preset.GetButtonPressed()));
				btn.AddThemeStyleboxOverride("disabled", Duplicate(preset.GetButtonDisabled()));
				btn.AddThemeStyleboxOverride("focus", Duplicate(preset.GetButtonFocus()));
				btn.AddThemeColorOverride("font_color", preset.Colors.TextPrimary);
			}
			foreach (var child in node.GetChildren())
				ApplyButtonOverrides(child, preset);
		}

		/// <summary>Build the 5 button-state StyleBoxes for a button-like type FROM THE
		/// THEME SCHEMA (not the preset's own boxes), so geometry+color+background are
		/// composed consistently for every button type. The preset contributes only its
		/// ColorSchema + AnimationConfig.</summary>
		private void RegisterButtonType(string typeName, IThemePreset preset)
		{
			var c = preset.Colors;
			Sb("normal", typeName, BuildButtonBox(c.SurfacePrimary, c.BorderNormal, c.ShadowColor, _shadowSize));
			Sb("hover", typeName, BuildButtonBox(c.SurfaceHover, c.BorderHover, c.ShadowColor, _shadowSize + 4));
			Sb("pressed", typeName, BuildButtonBox(c.SurfacePressed, c.BorderNormal, c.ShadowColor, Math.Max(0, _shadowSize - 6)));
			Sb("disabled", typeName, BuildButtonBox(c.SurfaceDisabled, new Color(c.BorderNormal.R, c.BorderNormal.G, c.BorderNormal.B, 0.4f), new Color(0,0,0,0), 0));
			Sb("focus", typeName, BuildButtonBox(c.SurfacePrimary, c.BorderFocus, c.ShadowColor, _shadowSize));
		}

		/// <summary>Dedicated button StyleBox builder — full theme schema + extracted geometry.</summary>
		private StyleBoxFlat BuildButtonBox(Color bg, Color border, Color shadow, int shadowSize)
		{
			var sb = NewBox();
			sb.BgColor = bg;
			sb.BorderColor = border;
			sb.ShadowColor = shadow;
			sb.ShadowSize = shadowSize;
			return sb;
		}

		/// <summary>Chokepoint for every StyleBox assignment: restamps the geometry
		/// profile onto the box (ALL ui nodes — panels, inputs, sliders, scrollbars,
		/// selected states, separators — not just buttons) then sets it on the theme.</summary>
		private void Sb(string name, string type, StyleBox box)
			=> _generatedTheme!.SetStylebox(name, type, StampGeometry(box));

		// ═══════════════════════════════════════════════
		// Geometry extracted once from preset's normal button
		// ═══════════════════════════════════════════════

		private int _gTL, _gTR, _gBR, _gBL;
		private int _bL, _bR, _bT, _bB;
		private Color _bColor;
		private int _shadowSize;
		private Vector2 _shadowOff;
		private Color _shadowColor;
		private float _padL, _padR, _padT, _padB;

		private void ExtractGeometry(StyleBox sb)
		{
			if (sb is StyleBoxFlat flat)
			{
				_gTL = (int)flat.CornerRadiusTopLeft;
				_gTR = (int)flat.CornerRadiusTopRight;
				_gBR = (int)flat.CornerRadiusBottomRight;
				_gBL = (int)flat.CornerRadiusBottomLeft;
				_bL = (int)flat.BorderWidthLeft;
				_bR = (int)flat.BorderWidthRight;
				_bT = (int)flat.BorderWidthTop;
				_bB = (int)flat.BorderWidthBottom;
				_bColor = flat.BorderColor;
				_shadowSize = flat.ShadowSize;
				_shadowOff = flat.ShadowOffset;
				_shadowColor = flat.ShadowColor;
				_padL = flat.ContentMarginLeft;
				_padR = flat.ContentMarginRight;
				_padT = flat.ContentMarginTop;
				_padB = flat.ContentMarginBottom;
			}

			// Geometry profile override: replace the extracted fields so every
			// NewBox()-derived StyleBox inherits the profile's shape. Preset's own
			// button boxes are restamped separately in RegisterButtonType.
			if (_geometry != null)
			{
				if (_geometry.CornerRadius >= 0)
					_gTL = _gTR = _gBR = _gBL = _geometry.CornerRadius;
				if (_geometry.BorderWidth >= 0)
					_bL = _bR = _bT = _bB = _geometry.BorderWidth;
				if (_geometry.ShadowSize >= 0) _shadowSize = _geometry.ShadowSize;
				if (_geometry.ShadowOffsetY >= 0) _shadowOff = new Vector2(_shadowOff.X, _geometry.ShadowOffsetY);
				if (_geometry.ContentPadding >= 0)
					_padL = _padR = _padT = _padB = _geometry.ContentPadding;
			}
		}

		private StyleBoxFlat NewBox()
		{
			var sb = new StyleBoxFlat();
			sb.CornerRadiusTopLeft = _gTL;
			sb.CornerRadiusTopRight = _gTR;
			sb.CornerRadiusBottomRight = _gBR;
			sb.CornerRadiusBottomLeft = _gBL;
			sb.BorderWidthLeft = _bL;
            sb.BorderWidthRight = _bR;
            sb.BorderWidthTop = _bT;
            sb.BorderWidthBottom = _bB;
			sb.BorderColor = _bColor;
			sb.ShadowSize = _shadowSize;
			sb.ShadowOffset = _shadowOff;
			sb.ShadowColor = _shadowColor;
			sb.ContentMarginLeft = _padL;
			sb.ContentMarginRight = _padR;
			sb.ContentMarginTop = _padT;
			sb.ContentMarginBottom = _padB;
			return sb;
		}

		// ═══════════════════════════════════════════════
		// Building-block factories
		// ═══════════════════════════════════════════════

		private StyleBox BuildSurface(ColorSchema c, Color surface)
		{
			var sb = NewBox();
			sb.BgColor = surface;
			sb.BorderColor = c.BorderNormal;
			return sb;
		}

		private StyleBox BuildPanel(ColorSchema c)
		{
			var sb = NewBox();
			sb.BgColor = c.BgPanel;
			sb.BorderColor = c.BorderNormal;
			sb.ShadowColor = c.ShadowColor;
			sb.ShadowSize = Math.Max(0, _shadowSize - 2);
			return sb;
		}

		private StyleBox BuildInput(ColorSchema c)
		{
			var sb = NewBox();
			sb.BgColor = c.SurfacePressed;
			sb.BorderColor = c.BorderNormal;
			sb.ShadowSize = 0;
			sb.ContentMarginLeft = Math.Max(4, _padL - 4);
			sb.ContentMarginRight = Math.Max(4, _padR - 4);
			sb.ContentMarginTop = Math.Max(2, _padT - 3);
			sb.ContentMarginBottom = Math.Max(2, _padB - 3);
			return sb;
		}

		private StyleBox BuildInputFocus(ColorSchema c)
		{
			var sb = (StyleBoxFlat)BuildInput(c);
			sb.BorderWidthLeft = Math.Max(2, _bL);
            sb.BorderWidthRight = Math.Max(2, _bR);
            sb.BorderWidthTop = Math.Max(2, _bT);
            sb.BorderWidthBottom = Math.Max(2, _bB);
			sb.BorderColor = c.BorderFocus;
			return sb;
		}

		private StyleBox BuildInputReadOnly(ColorSchema c)
		{
			var sb = (StyleBoxFlat)BuildInput(c);
			sb.BgColor = new Color(c.SurfaceDisabled, 0.6f);
			sb.BorderColor = new Color(c.BorderNormal, 0.4f);
			return sb;
		}

		private StyleBox BuildProgressBg(ColorSchema c)
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = c.SurfaceDisabled;
			sb.CornerRadiusTopLeft = Math.Max(2, _gTL - 4);
			sb.CornerRadiusTopRight = Math.Max(2, _gTR - 4);
			sb.CornerRadiusBottomRight = Math.Max(2, _gBR - 4);
			sb.CornerRadiusBottomLeft = Math.Max(2, _gBL - 4);
			sb.ContentMarginLeft = 2; sb.ContentMarginRight = 2;
			sb.ContentMarginTop = 2; sb.ContentMarginBottom = 2;
			return sb;
		}

		private StyleBox BuildProgressFill(ColorSchema c)
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = c.AccentPrimary;
			sb.CornerRadiusTopLeft = Math.Max(2, _gTL - 4);
			sb.CornerRadiusTopRight = Math.Max(2, _gTR - 4);
			sb.CornerRadiusBottomRight = Math.Max(2, _gBR - 4);
			sb.CornerRadiusBottomLeft = Math.Max(2, _gBL - 4);
			sb.ContentMarginLeft = 2; sb.ContentMarginRight = 2;
			sb.ContentMarginTop = 2; sb.ContentMarginBottom = 2;
			return sb;
		}

		private StyleBox BuildSliderGrabber(ColorSchema c)
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = c.AccentPrimary;
			int r = (_gTL + _gTR) / 2;
			sb.CornerRadiusTopLeft = r; sb.CornerRadiusTopRight = r;
			sb.CornerRadiusBottomRight = r; sb.CornerRadiusBottomLeft = r;
			sb.ShadowSize = 3;
			sb.ShadowOffset = _shadowOff * 0.5f;
			sb.ShadowColor = c.ShadowColor;
			return sb;
		}

		private StyleBox BuildSliderGrabberHover(ColorSchema c)
		{
			var sb = (StyleBoxFlat)BuildSliderGrabber(c);
			sb.BgColor = c.AccentSecondary;
			sb.ShadowSize = 5;
			return sb;
		}

		private StyleBox BuildSliderTrack(ColorSchema c)
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = c.SurfaceDisabled;
			sb.CornerRadiusTopLeft = Math.Max(2, _gTL / 2);
			sb.CornerRadiusTopRight = Math.Max(2, _gTR / 2);
			sb.CornerRadiusBottomRight = Math.Max(2, _gBR / 2);
			sb.CornerRadiusBottomLeft = Math.Max(2, _gBL / 2);
			return sb;
		}

		private StyleBox BuildScrollGrabber(ColorSchema c)
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = new Color(c.TextDisabled, 0.5f);
			int r = Math.Max(3, (_gTL + _gTR) / 3);
			sb.CornerRadiusTopLeft = r; sb.CornerRadiusTopRight = r;
			sb.CornerRadiusBottomRight = r; sb.CornerRadiusBottomLeft = r;
			return sb;
		}

		private StyleBox BuildScrollGrabberHover(ColorSchema c)
		{
			var sb = (StyleBoxFlat)BuildScrollGrabber(c);
			sb.BgColor = new Color(c.TextDisabled, 0.8f);
			return sb;
		}

		private StyleBox BuildScrollTrack(ColorSchema c)
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = new Color(c.BgCanvas, 0.7f);
			return sb;
		}

		private StyleBox BuildSelected(ColorSchema c)
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = new Color(c.AccentPrimary.R, c.AccentPrimary.G, c.AccentPrimary.B, 0.25f);
			int r = Math.Max(2, _gTL / 2);
			sb.CornerRadiusTopLeft = r; sb.CornerRadiusTopRight = r;
			sb.CornerRadiusBottomRight = r; sb.CornerRadiusBottomLeft = r;
			sb.ContentMarginLeft = 4; sb.ContentMarginRight = 4;
			return sb;
		}

		private StyleBox BuildSelectedFocus(ColorSchema c)
		{
			var sb = (StyleBoxFlat)BuildSelected(c);
			sb.BgColor = new Color(c.AccentPrimary.R, c.AccentPrimary.G, c.AccentPrimary.B, 0.40f);
			sb.BorderWidthLeft = 1; sb.BorderWidthRight = 1;
            sb.BorderWidthTop = 1; sb.BorderWidthBottom = 1;
			sb.BorderColor = c.BorderFocus;
			return sb;
		}

		private StyleBox BuildSeparator(ColorSchema c)
		{
			var sb = new StyleBoxFlat();
			sb.BgColor = c.BorderNormal;
			return sb;
		}

		// ═══════════════════════════════════════════════
		// Single Button Mode
		// ═══════════════════════════════════════════════

		private void ApplyToSingleButton(Button btn)
		{
			var preset = _presetInstance!;
			btn.AddThemeStyleboxOverride("normal", StampGeometry(Duplicate(preset.GetButtonNormal())));
			btn.AddThemeStyleboxOverride("hover", StampGeometry(Duplicate(preset.GetButtonHover())));
			btn.AddThemeStyleboxOverride("pressed", StampGeometry(Duplicate(preset.GetButtonPressed())));
			btn.AddThemeStyleboxOverride("disabled", StampGeometry(Duplicate(preset.GetButtonDisabled())));
			btn.AddThemeStyleboxOverride("focus", StampGeometry(Duplicate(preset.GetButtonFocus())));
			btn.AddThemeColorOverride("font_color", preset.Colors.TextPrimary);
			int fontSize = _geometry != null && _geometry.FontSize >= 0
				? _geometry.FontSize
				: (_loadedThemeGeometry.FontSize > 0 ? _loadedThemeGeometry.FontSize : 14);
			btn.AddThemeFontSizeOverride("font_size", fontSize);
			if (EnableAnimations) SetupButtonAnimations(btn);
			if (EnableRippleOnClick) SetupRipple(btn);
		}

		// ═══════════════════════════════════════════════
		// Background image
		// ═══════════════════════════════════════════════

		/// <summary>Spawn (or refresh) a full-rect TextureRect behind the themed
		/// subtree root, when the active genre's geometry.json sets
		/// <c>background_image</c>. Honors <c>background_mode</c>:
		///   <c>stretch</c> (default) — scale to fill the canvas,
		///   <c>tile</c> — repeat at native size,
		///   <c>center</c> — keep native size, centered.
		/// No-op when the geometry has no background image or the resource is missing.</summary>
		private void ApplyBackground()
		{
			if (_targetControl == null) return;
			var geo = _geometry;
			if (geo == null) return;
			string? img = geo.BackgroundImage;
			if (string.IsNullOrEmpty(img)) return;
			if (!ResourceLoader.Exists(img)) return;

			if (_backgroundRect == null || !GodotObject.IsInstanceValid(_backgroundRect))
			{
				_backgroundRect = new TextureRect
				{
					Name = "ThemeBackground",
					MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
				};
				// Insert at index 0 so it draws under everything else.
				_targetControl.AddChild(_backgroundRect);
				_targetControl.MoveChild(_backgroundRect, 0);
				_backgroundRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			}
			_backgroundRect.Texture = ResourceLoader.Load<Texture2D>(img);

			switch ((geo.BackgroundMode ?? "stretch").ToLowerInvariant())
			{
				case "tile":
					_backgroundRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
					_backgroundRect.StretchMode = TextureRect.StretchModeEnum.Tile;
					break;
				case "center":
					_backgroundRect.ExpandMode = TextureRect.ExpandModeEnum.KeepSize;
					_backgroundRect.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
					break;
				case "stretch":
				default:
					_backgroundRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
					_backgroundRect.StretchMode = TextureRect.StretchModeEnum.Scale;
					break;
			}
		}

		// ═══════════════════════════════════════════════
		// Animations
		// ═══════════════════════════════════════════════

		private void InjectIntoButtons(Godot.Control root)
		{
			foreach (var btn in FindAllButtons(root))
			{
				if (EnableAnimations) SetupButtonAnimations(btn);
				if (EnableRippleOnClick) SetupRipple(btn);
			}
		}

		private void SetupButtonAnimations(Button btn)
		{
			var anim = _presetInstance!.Animation;
			btn.MouseEntered += () =>
			{
				if (!IsActive || !EnableAnimations || !btn.IsVisibleInTree()) return;
				if (_activeTweens.TryGetValue(btn, out var e)) e?.Kill();
				var t = btn.CreateTween().SetParallel(true);
				t.TweenProperty(btn, "scale", new Vector2(anim.HoverScaleAmount, anim.HoverScaleAmount), anim.HoverScaleDuration).SetEase(Tween.EaseType.Out);
				if (anim.EnableShadowLift)
					t.TweenProperty(btn, "position:y", btn.Position.Y - 2f, anim.HoverScaleDuration).SetEase(Tween.EaseType.Out);
				_activeTweens[btn] = t;
			};
			btn.MouseExited += () =>
			{
				if (!IsActive || !EnableAnimations) return;
				if (_activeTweens.TryGetValue(btn, out var e)) e?.Kill();
				var t = btn.CreateTween().SetParallel(true);
				t.TweenProperty(btn, "scale", Vector2.One, anim.HoverScaleDuration).SetEase(Tween.EaseType.Out);
				if (anim.EnableShadowLift)
					t.TweenProperty(btn, "position:y", btn.Position.Y + 2f, anim.HoverScaleDuration).SetEase(Tween.EaseType.Out);
				_activeTweens[btn] = t;
			};
			btn.ButtonDown += () =>
			{
				if (!IsActive || !EnableAnimations || !btn.IsVisibleInTree()) return;
				if (_activeTweens.TryGetValue(btn, out var e)) e?.Kill();
				var t = btn.CreateTween();
				t.TweenProperty(btn, "scale", new Vector2(anim.PressScaleAmount, anim.PressScaleAmount), anim.PressScaleDuration).SetEase(Tween.EaseType.In);
				_activeTweens[btn] = t;
			};
			btn.ButtonUp += () =>
			{
				if (!IsActive || !EnableAnimations) return;
				if (_activeTweens.TryGetValue(btn, out var e)) e?.Kill();
				var t = btn.CreateTween();
				t.TweenProperty(btn, "scale", Vector2.One, anim.PressScaleDuration * 1.5f).SetEase(Tween.EaseType.Out);
				_activeTweens[btn] = t;
			};
			if (anim.EnableFocusGlow)
			{
				// Glow toward the theme's secondary accent so the focus state matches the theme.
				var c = _presetInstance!.Colors;
				Color glowTarget = c.AccentSecondary.Blend(c.TextOnDark);
				btn.FocusEntered += () =>
				{
					if (!IsActive || !EnableAnimations) return;
					btn.CreateTween().TweenProperty(btn, "modulate", glowTarget, 0.2f).SetEase(Tween.EaseType.Out);
				};
				btn.FocusExited += () =>
				{
					if (!IsActive || !EnableAnimations) return;
					btn.CreateTween().TweenProperty(btn, "modulate", new Color(1f, 1f, 1f, 1f), 0.2f).SetEase(Tween.EaseType.Out);
				};
			}
		}

		private void SetupRipple(Button btn)
		{
			// Ripple uses the theme's primary accent so it matches the chosen theme/palette.
			var c = _presetInstance!.Colors;
			btn.AddChild(new RippleComponent
			{
				RippleColor = new Color(c.AccentPrimary.R, c.AccentPrimary.G, c.AccentPrimary.B, 0.35f),
				Duration = 0.5f, MaxRadius = 120f, IsActive = true
			});
		}

		// ═══════════════════════════════════════════════
		// Helpers
		// ═══════════════════════════════════════════════

		private static List<Button> FindAllButtons(Godot.Control root)
		{
			var list = new List<Button>();
			CollectButtons(root, list);
			return list;
		}

		private static void CollectButtons(Node node, List<Button> list)
		{
			if (node is Button btn) list.Add(btn);
			foreach (var child in node.GetChildren())
				if (child is Node n) CollectButtons(n, list);
		}

		private static StyleBox Duplicate(StyleBox original)
		{
			if (original is StyleBoxFlat flat)
			{
				var dup = new StyleBoxFlat();
				dup.BgColor = flat.BgColor;
				dup.BorderWidthLeft = flat.BorderWidthLeft;
				dup.BorderWidthRight = flat.BorderWidthRight;
				dup.BorderWidthTop = flat.BorderWidthTop;
				dup.BorderWidthBottom = flat.BorderWidthBottom;
				dup.BorderColor = flat.BorderColor;
				dup.CornerRadiusTopLeft = (int)flat.CornerRadiusTopLeft;
				dup.CornerRadiusTopRight = (int)flat.CornerRadiusTopRight;
				dup.CornerRadiusBottomRight = (int)flat.CornerRadiusBottomRight;
				dup.CornerRadiusBottomLeft = (int)flat.CornerRadiusBottomLeft;
				dup.ShadowSize = flat.ShadowSize;
				dup.ShadowOffset = flat.ShadowOffset;
				dup.ShadowColor = flat.ShadowColor;
				dup.ContentMarginLeft = flat.ContentMarginLeft;
				dup.ContentMarginRight = flat.ContentMarginRight;
				dup.ContentMarginTop = flat.ContentMarginTop;
				dup.ContentMarginBottom = flat.ContentMarginBottom;
				dup.ExpandMarginLeft = flat.ExpandMarginLeft;
				dup.ExpandMarginRight = flat.ExpandMarginRight;
				dup.ExpandMarginTop = flat.ExpandMarginTop;
				dup.ExpandMarginBottom = flat.ExpandMarginBottom;
				return dup;
			}
			if (original is StyleBoxTexture tex)
			{
				var dup = new StyleBoxTexture();
				dup.Texture = tex.Texture;
				dup.TextureMarginLeft = tex.TextureMarginLeft;
				dup.TextureMarginRight = tex.TextureMarginRight;
				dup.TextureMarginTop = tex.TextureMarginTop;
				dup.TextureMarginBottom = tex.TextureMarginBottom;
				dup.ModulateColor = tex.ModulateColor;
				dup.ContentMarginLeft = tex.ContentMarginLeft;
				dup.ContentMarginRight = tex.ContentMarginRight;
				dup.ContentMarginTop = tex.ContentMarginTop;
				dup.ContentMarginBottom = tex.ContentMarginBottom;
				return dup;
			}
			return original;
		}

		// ═══════════════════════════════════════════════
		// Factory — deleted. Themes are now loaded from the file-based
		// skin catalog (skins/<genre>/themes/<theme>/theme.json) via
		// SkinCatalog.GetTheme() in ApplyTheme() above. See FileThemePreset.
		// ═══════════════════════════════════════════════

		// ═══════════════════════════════════════════════
		// Inspector dropdowns — values come from the skin catalog at edit time.
		// ═══════════════════════════════════════════════

		public override void _ValidateProperty(Godot.Collections.Dictionary property)
		{
			base._ValidateProperty(property);

			switch ((string)property["name"])
			{
				case nameof(GenreName):
					SkinPropertyHints.ApplyEnum(property, SkinPropertyHints.GenreHint(_genreName));
					break;
				case nameof(PresetName):
					SkinPropertyHints.ApplyEnum(property, SkinPropertyHints.ThemeHint(_genreName, _presetName));
					break;
				case nameof(PaletteName):
					SkinPropertyHints.ApplyEnum(property, SkinPropertyHints.PaletteHint(_genreName, _presetName, _paletteName));
					break;
				case nameof(GeometryProfileName):
					SkinPropertyHints.ApplyEnum(property, SkinPropertyHints.GeometryHint(_genreName, _geometryProfileName));
					break;
			}
		}
	}
}
