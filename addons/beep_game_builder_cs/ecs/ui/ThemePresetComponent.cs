using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS.UI
{
	[Tool]
	[GlobalClass]
	public partial class ThemePresetComponent : UIComponent
	{
		/// <summary>Theme preset name (e.g. "oilfield_days", "modern"). Resolved from the
		/// file-based skin catalog at runtime. Free-form string — any theme.json in
		/// the skins/ tree works. Set alongside GenreName so the catalog knows where
		/// to look. Falls back to the genre's default_theme if not found.</summary>
		[Export]
		public string PresetName
		{
			get => _presetName;
			// Palette options depend on the selected theme — refresh the list so the
			// PaletteName dropdown re-cascades.
			// Bail when unchanged: GameInfoBinder pushes four of these in a row, and each
			// setter rebuilds the entire theme.
			set { if (_presetName == value) return; _presetName = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); if (!_suspendAutoApply && IsInsideTree()) ApplyTheme(); }
		}
		private string _presetName = "oilfield_days";

		/// <summary>Genre this component belongs to (e.g. "citybuilder"). Determines
		/// which genre's theme tree to load from. Falls back to "citybuilder".</summary>
		[Export]
		public string GenreName
		{
			get => _genreName;
			// Theme/palette/geometry options all hang off the genre — refresh the list
			// so those dropdowns re-cascade.
			set { if (_genreName == value) return; _genreName = value; if (Engine.IsEditorHint()) NotifyPropertyListChanged(); if (!_suspendAutoApply && IsInsideTree()) ApplyTheme(); }
		}
		/// <summary>Genre a component starts on. A scene still holding this is treated as
		/// "not chosen", so GameInfoBinder may replace it with the project's genre; anything
		/// else is the scene's deliberate choice and is left alone.</summary>
		public const string DefaultGenre = "citybuilder";
		private string _genreName = DefaultGenre;

		[Export]
		public bool EnableAnimations
		{
			get => _enableAnimations;
			set { if (_enableAnimations == value) return; _enableAnimations = value; RequestThemeReapply(); }
		}
		private bool _enableAnimations = true;

		[Export]
		public bool EnableRippleOnClick
		{
			get => _enableRippleOnClick;
			set { if (_enableRippleOnClick == value) return; _enableRippleOnClick = value; RequestThemeReapply(); }
		}
		private bool _enableRippleOnClick = true;

		/// <summary>Paint the screen's page canvas from the theme's <c>bg_canvas</c>.
		///
		/// Every genre screen opens with a hardcoded <c>ColorRect</c> named "Background"
		/// (0.1,0.1,0.1 in racing/garage, 0.08,0.08,0.12 in rpg/inventory, …). A plain
		/// ColorRect has no theme entry, so <c>bg_canvas</c> — which every theme.json
		/// defines — reached nothing but a scrollbar track: switching genre, theme or
		/// palette left all 35 screens on the same flat near-black.
		///
		/// Only OPAQUE backgrounds are repainted (alpha >= 0.99). A translucent
		/// ColorRect is a dim over live gameplay — game_over/level_summary name theirs
		/// "Dim", topdown/pause_subscreen names its 0.92-alpha dim "Background" — and
		/// repainting those would turn an overlay into a wall.</summary>
		[Export]
		public bool ThemePageBackground
		{
			get => _themePageBackground;
			set { if (_themePageBackground == value) return; _themePageBackground = value; RequestThemeReapply(); }
		}
		private bool _themePageBackground = true;

		/// <summary>Give Labels a type hierarchy (title / subtitle / value / caption)
		/// derived from the theme's own font size. Without it every Label, Button and
		/// input renders at the single <c>Fs</c>, so a screen title is exactly as big as
		/// a body label — the reason every screen read as one flat wall of text.
		/// See <see cref="ApplyTypography"/> for how a Label's role is decided.</summary>
		[Export]
		public bool AutoTypography
		{
			get => _autoTypography;
			set { if (_autoTypography == value) return; _autoTypography = value; RequestThemeReapply(); }
		}
		private bool _autoTypography = true;

		[ExportGroup("Button Sounds")]
		/// <summary>Play a hover/press sound on every themed button. Falls back to the addon's
		/// shipped UI clicks if the streams below are left unset (so a menu gets sound with no
		/// per-scene wiring); assign HoverSound/PressSound to override, or turn this off.</summary>
		[Export]
		public bool EnableButtonSounds
		{
			get => _enableButtonSounds;
			set { if (_enableButtonSounds == value) return; _enableButtonSounds = value; RequestThemeReapply(); }
		}
		private bool _enableButtonSounds = true;

		[Export]
		public AudioStream? HoverSound
		{
			get => _hoverSound;
			set { if (_hoverSound == value) return; _hoverSound = value; RequestThemeReapply(); }
		}
		private AudioStream? _hoverSound;

		[Export]
		public AudioStream? PressSound
		{
			get => _pressSound;
			set { if (_pressSound == value) return; _pressSound = value; RequestThemeReapply(); }
		}
		private AudioStream? _pressSound;

		[Export(PropertyHint.Range, "-40,6,0.5")]
		public float ButtonSoundVolumeDb
		{
			get => _buttonSoundVolumeDb;
			set
			{
				if (Mathf.Abs(_buttonSoundVolumeDb - value) < 0.001f) return;
				_buttonSoundVolumeDb = value;
				if (_uiAudio != null && GodotObject.IsInstanceValid(_uiAudio))
					_uiAudio.VolumeDb = value;
				RequestThemeReapply();
			}
		}
		private float _buttonSoundVolumeDb = -6f;

		/// <summary>OPTIONAL color-palette variant. Resolved from the file-based skin
		/// catalog (palettes live in skins/&lt;genre&gt;/themes/&lt;theme&gt;/&lt;palette&gt;.json).
		/// Retints the whole theme in HSV space. "Default" = no tint.</summary>
		[Export]
		public string PaletteName
		{
			get => _paletteName;
			set { if (_paletteName == value) return; _paletteName = value; if (!_suspendAutoApply && IsInsideTree()) ApplyTheme(); }
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
			set { if (_geometryProfileName == value) return; _geometryProfileName = value; if (!_suspendAutoApply && IsInsideTree()) ApplyTheme(); }
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

		private const string RetiredBackgroundNodeName = "ThemeBackground";

		/// <summary>Generate HUD chrome instead of menu chrome for this subtree.
		///
		/// A menu skin is opaque, bevelled and raised because it owns the whole screen. A HUD
		/// sits ON the gameplay: it has to let the world through, stay flat so it never competes
		/// with the game, and signal state with accent colour rather than a pressed 3D plate.
		/// Without this the in-game HUD is generated in the settings-dialog skin — opaque,
		/// bevelled menu buttons and the same solid page background — which is exactly why it
		/// reads as an application toolbar rather than a game interface.
		///
		/// Same palette, same typography, different furniture. Set this on the
		/// ThemePresetComponent that lives under a HUD CanvasLayer.</summary>
		// The game-art register is PROJECT-WIDE and lives in ProjectSettings — see
		// SkinCatalog.SettingChrome/Outline/Shadow/HudOpacity. It used to be four
		// [Export]s here, which made one art direction a per-scene decision: every scene
		// carried its own copy, they drifted, and "turn the chrome off" meant editing 40 files.
		// Read-only mirrors so the rest of this class reads the same names as before.
		private static bool GameArtChrome => SkinCatalog.GameArtChrome;
		private static int GameArtOutline => SkinCatalog.GameArtOutline;
		private static int GameArtShadow => SkinCatalog.GameArtShadow;
		private static float HudPlateOpacity => SkinCatalog.HudPlateOpacity;

		/// <summary>Whether THIS subtree is a HUD. Stays per-scene on purpose: it is not an art
		/// preference but a structural fact about the scene — a HUD CanvasLayer over the world
		/// versus a menu — and it differs between scenes of the same game by definition.</summary>
		[ExportGroup("HUD")]
		[Export]
		public bool HudMode
		{
			get => _hudMode;
			set { if (_hudMode == value) return; _hudMode = value; RequestThemeReapply(); }
		}
		private bool _hudMode;

		[Signal] public delegate void ThemeAppliedEventHandler();

		private IThemePreset? _presetInstance;
		/// <summary>Geometry template from the loaded theme.json (used for font size fallback).</summary>
		private ThemeGeometry _loadedThemeGeometry;
		private Godot.Control? _targetControl;
		private bool _isSingleButton;
		private Theme? _generatedTheme;
		private readonly Dictionary<Button, Tween?> _activeTweens = new();
		// Undo actions for the per-button signal handlers SetupButtonAnimations attaches. The
		// handlers capture this (IsActive, _presetInstance, _activeTweens); if the themer is
		// removed while the themed buttons persist, a later hover/press would fire them on a
		// freed component. Run on _ExitTree to disconnect.
		private readonly System.Collections.Generic.List<System.Action> _buttonDisconnectors = new();
		private bool _suspendAutoApply;
		private bool _applyingTheme;
		private string _lastThemeBuildKey = "";
		private Font? _themeFont;

		private void RequestThemeReapply()
		{
			if (!_suspendAutoApply && IsInsideTree())
				ApplyTheme();
		}

		public void SetThemeSelection(string genreName, string presetName, string paletteName = "Default",
			string geometryProfileName = "As-Authored")
		{
			_suspendAutoApply = true;
			try
			{
				_genreName = genreName ?? "";
				_presetName = presetName ?? "";
				_paletteName = paletteName ?? "Default";
				_geometryProfileName = geometryProfileName ?? "As-Authored";
				if (Engine.IsEditorHint()) NotifyPropertyListChanged();
			}
			finally
			{
				_suspendAutoApply = false;
			}

			SkinCatalog.SetActiveSkin(_genreName, _presetName, _paletteName, _geometryProfileName);
			if (IsInsideTree()) ApplyTheme();
		}

		public override void _Ready()
		{
			base._Ready();
			PublishOwnSkinWhenStandalone();
			// Fall back to the addon's shipped UI clicks when no sounds are assigned, so themed
			// buttons make sound with zero per-scene wiring. Missing files just leave these null.
			if (EnableButtonSounds && !Engine.IsEditorHint())
			{
				HoverSound ??= LoadIfExists("res://addons/beep_game_builder_cs/audio/ui/ui_hover.ogg");
				PressSound ??= LoadIfExists("res://addons/beep_game_builder_cs/audio/ui/ui_click.ogg");
			}
			_targetControl = GetParent() as Godot.Control;
			if (_targetControl != null) { ApplyTheme(); return; }

			// Say so. This component themes GetParent()'s subtree, so a non-Control parent
			// means it does nothing at all — and it used to do that in silence. Every scene
			// whose root is a CanvasLayer (pause, settings, game over, and most genre
			// screens) parents this at the root and has therefore never been themed. Move
			// this node under the Control you want themed (the panel, not the dim).
			if (!Engine.IsEditorHint())
				GD.PushWarning($"[{Name}] ThemePresetComponent's parent is {GetParent()?.GetType().Name ?? "null"}, not a Control — nothing will be themed. Reparent it under the Control whose subtree should be themed.");
		}

		private void PublishOwnSkinWhenStandalone()
		{
			if (SkinCatalog.HasActiveSkin)
				return;

			string theme = string.IsNullOrWhiteSpace(_presetName)
				? SkinCatalog.GetGenre(_genreName)?.DefaultTheme ?? ""
				: _presetName;
			SkinCatalog.SetActiveSkin(_genreName, theme, _paletteName, _geometryProfileName);
		}

		public override void _ExitTree()
		{
			foreach (var kvp in _activeTweens) kvp.Value?.Kill();
			_activeTweens.Clear();
			foreach (var disconnect in _buttonDisconnectors) disconnect();
			_buttonDisconnectors.Clear();
			base._ExitTree();
		}

		public void ApplyTheme()
		{
			if (_targetControl == null || !IsActive) return;
			if (_applyingTheme) return;
			_applyingTheme = true;
			try
			{
			// Load the theme from the file-based skin catalog. This replaces the old
			// enum → CreatePresetInstance switch. Falls back to the genre's default
			// theme if PresetName isn't found.
			// The GAME's skin wins, always. Falls back to this component's exported values
			// only when no game has published one — an isolated scene in the editor.
			string gGenre = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveGenre : _genreName;
			string gTheme = SkinCatalog.HasActiveSkin ? SkinCatalog.ActiveTheme : _presetName;
			string gPalette = SkinCatalog.HasActiveSkin && !string.IsNullOrEmpty(SkinCatalog.ActivePalette)
				? SkinCatalog.ActivePalette : _paletteName;
			string gGeometry = SkinCatalog.HasActiveSkin && !string.IsNullOrEmpty(SkinCatalog.ActiveGeometry)
				? SkinCatalog.ActiveGeometry : _geometryProfileName;
			_targetControl.SetMeta(Kit.KitChrome.GenreMeta, gGenre);

			var themeDef = SkinCatalog.GetTheme(gGenre, gTheme);
			if (themeDef == null)
			{
				var genre = SkinCatalog.GetGenre(gGenre);
				themeDef = genre != null && genre.Themes.TryGetValue(genre.DefaultTheme, out var dt) ? dt : null;
			}
			if (themeDef == null) return;
			string buildKey = BuildThemeKey(gGenre, themeDef.Id, gPalette, gGeometry);
			if (buildKey == _lastThemeBuildKey
				&& _generatedTheme != null
				&& _targetControl.Theme == _generatedTheme)
			{
				return;
			}
			_presetInstance = new FileThemePreset(themeDef);
			_loadedThemeGeometry = themeDef.Geometry;
			_themeFont = Kit.KitFonts.Fallback(_targetControl, Kit.KitGeometry.ForGenre(gGenre).Font);

			// Apply the optional color palette. Looked up from this theme's palette
			// files in the skins tree (loaded by SkinCatalog). If not found there,
			// ColorPalette.ByName searches all genres/themes as a cross-fallback.
			if (!string.IsNullOrEmpty(gPalette)
				&& !gPalette.Equals("Default", StringComparison.OrdinalIgnoreCase))
			{
				ColorPalette? palette = null;
				if (themeDef.Palettes.TryGetValue(gPalette.ToLowerInvariant(), out var filePal))
					palette = filePal;
				else if (ColorPalette.ByName(gPalette) is { } catalogPal)
					palette = catalogPal;
				if (palette != null)
					_presetInstance = new PaletteTintedPreset(_presetInstance, palette);
			}

			// Resolve the optional geometry profile from this genre's geometry.json
			// (loaded by SkinCatalog). GeometryProfile.ByName searches all genres.
			_geometry = null;
			if (!string.IsNullOrEmpty(gGeometry)
				&& !gGeometry.Equals("As-Authored", StringComparison.OrdinalIgnoreCase))
			{
				GeometryProfile? geo = null;
				var genre = SkinCatalog.GetGenre(gGenre);
				if (genre?.Geometry != null && genre.Geometry.DisplayName.Equals(gGeometry, StringComparison.OrdinalIgnoreCase))
					geo = genre.Geometry.ToProfile();
				else if (GeometryProfile.ByName(gGeometry) is { } catalogGeo)
					geo = catalogGeo;
				if (geo != null && geo.HasOverrides)
					_geometry = geo;
			}
			_isSingleButton = _targetControl is Button;
			if (_isSingleButton) ApplyToSingleButton((Button)_targetControl);
			else ApplyToSubtree(_targetControl);
			_lastThemeBuildKey = buildKey;
			EmitSignal(SignalName.ThemeApplied);
			}
			finally
			{
				_applyingTheme = false;
			}
		}

		private string BuildThemeKey(string genre, string theme, string palette, string geometry)
		{
			return string.Join("|", new[]
			{
				genre ?? "",
				theme ?? "",
				palette ?? "",
				geometry ?? "",
				HudMode.ToString(),
				ThemePageBackground.ToString(),
				AutoTypography.ToString(),
				EnableAnimations.ToString(),
				EnableRippleOnClick.ToString(),
				EnableButtonSounds.ToString(),
				ButtonSoundVolumeDb.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
				HoverSound?.GetInstanceId().ToString() ?? "",
				PressSound?.GetInstanceId().ToString() ?? "",
				SkinCatalog.GameArtChrome.ToString(),
				SkinCatalog.GameArtOutline.ToString(),
				SkinCatalog.GameArtShadow.ToString(),
				SkinCatalog.HudPlateOpacity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
			});
		}

		// ═══════════════════════════════════════════════
		// Subtree Mode
		// ═══════════════════════════════════════════════

		private void ApplyToSubtree(Godot.Control root)
		{
			var preset = _presetInstance!;
			ResetInjectedButtons(root);
			_generatedTheme = new Theme();
			ExtractGeometry(preset.GetButtonNormal());
			RemoveRetiredBackgroundNode(root);

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
			ThemeSemantics();
			RegisterFonts();
			RegisterTypography();

			// A HUD is not a menu. Replace the generated menu chrome with HUD chrome before it
			// ever reaches the tree — same palette, different furniture. See ApplyHudChrome.
			var typography = preset.Colors;
			if (HudMode) typography = ApplyHudChrome(preset.Colors);

			root.Theme = _generatedTheme;

			if (EnableAnimations || EnableRippleOnClick)
				InjectIntoButtons(root);
      		// Per-node overrides for immediate editor visibility.
			// Both are skipped in HUD mode: ApplyButtonOverrides stamps menu boxes onto every
			// button, and ApplyPageBackground paints an opaque rect over the whole Control —
			// together they would repaint the dialog skin straight back over the HUD chrome and
			// hide the game behind it.
			if (!HudMode) ApplyButtonOverrides(root, preset);
			if (ThemePageBackground && !HudMode) ApplyPageBackground(root, preset.Colors);
			if (AutoTypography) ApplyTypography(root, typography);
		}

		/// <summary>Rewrite the generated theme's chrome for on-world HUD use, and return the
		/// colour schema the typography pass must use so text stays legible on the new plates.
		///
		/// Every colour is derived from the ACTIVE skin's palette, so all 50 themes still tell
		/// themselves apart — a HUD is not "one dark grey for everybody". What changes is the
		/// furniture: translucent plates instead of opaque ones, a hairline instead of a bevel,
		/// and an accent fill for the selected state instead of a pressed 3D face.
		///
		/// Written straight into the theme rather than through <c>Sb()</c> on purpose: Sb runs
		/// StampGeometry, which would re-apply the menu geometry's padding and shadow and undo
		/// the flattening this method exists to do.</summary>
		private ColorSchema ApplyHudChrome(ColorSchema c)
		{
			static float Lum(Color x) => 0.2126f * x.R + 0.7152f * x.G + 0.0722f * x.B;
			static Color Mul(Color x, float k) => new(x.R * k, x.G * k, x.B * k, x.A);
			static Color Mix(Color a, Color b, float t) =>
				new(Mathf.Lerp(a.R, b.R, t), Mathf.Lerp(a.G, b.G, t), Mathf.Lerp(a.B, b.B, t), a.A);

			// Drive the skin's own surface dark and translucent. A HUD plate has to sit UNDER
			// the reading of the world, so it is dark regardless of whether the menu skin is
			// light — but it keeps a trace of the skin's accent so the genre still reads.
			Color baseTint = Mix(Mul(c.SurfacePrimary, 0.26f), c.AccentPrimary, 0.12f);
			Color plate  = baseTint with { A = HudPlateOpacity };
			Color raised = Mix(baseTint, c.AccentPrimary, 0.10f) with { A = Mathf.Min(1f, HudPlateOpacity + 0.12f) };
			Color accent = c.AccentPrimary;
			Color edge   = accent with { A = 0.32f };

			// Text must contrast with the PLATE, not with the menu surface it was authored
			// against — this is the light-skin failure that made hover text unreadable before.
			Color text     = Lum(plate) < 0.45f ? new Color(0.94f, 0.96f, 0.98f) : new Color(0.07f, 0.08f, 0.10f);
			Color textDim  = text with { A = 0.62f };
			Color onAccent = Lum(accent) < 0.5f ? new Color(1, 1, 1) : new Color(0.06f, 0.07f, 0.09f);

			StyleBoxFlat Box(Color bg, Color border, int width, int radius, int padX, int padY)
			{
				var b = new StyleBoxFlat { BgColor = bg, DrawCenter = true, BorderColor = border };
				b.SetBorderWidthAll(width);
				b.SetCornerRadiusAll(radius);
				// Explicit so theme controls do not inherit arbitrary fallback margins.
				b.ContentMarginLeft = padX; b.ContentMarginRight = padX;
				b.ContentMarginTop = padY; b.ContentMarginBottom = padY;
				return b;
			}
			void Set(string name, string type, StyleBox box) => _generatedTheme!.SetStylebox(name, type, box);
			void Col(string name, string type, Color v) => _generatedTheme!.SetColor(name, type, v);

			// HUD chrome stays procedural for the same reason menu chrome does: the skin's
			// colours and geometry should define the kit, not mismatched baked image plates.
			StyleBox Art(string slot, StyleBoxFlat fallback)
			{
				_ = slot;
				return fallback;
			}

			// ── Plates ────────────────────────────────────────────────────────────────────
			foreach (string t in new[] { "Panel", "PanelContainer" })
				Set("panel", t, Art("panel", Box(plate, edge, 1, 6, 10, 8)));

			// ── Tiles (build palette, speed control, every HUD button) ────────────────────
			foreach (string t in new[] { "Button", "OptionButton", "MenuButton", "CheckBox", "CheckButton" })
			{
				Set("normal",   t, Art("button_normal",  Box(plate,  edge,                     1, 5, 14, 9)));
				Set("hover",    t, Art("button_hover",   Box(raised, accent with { A = 0.75f },1, 5, 14, 9)));
				Set("pressed",  t, Art("button_pressed", Box(accent with { A = 0.92f }, accent, 1, 5, 14, 9)));
				Set("focus",    t, Art("button_focus",   Box(new Color(0, 0, 0, 0), accent,     2, 5, 14, 9)));
				Set("disabled", t, Art("button_disabled", Box(Mul(plate, 0.7f) with { A = HudPlateOpacity * 0.55f },
				                       edge with { A = 0.16f },           1, 5, 14, 9)));
				Col("font_color", t, text);
				Col("font_hover_color", t, text);
				Col("font_pressed_color", t, onAccent);
				Col("font_hover_pressed_color", t, onAccent);
				Col("font_focus_color", t, text);
				Col("font_disabled_color", t, textDim);
			}

			// ── Readouts ─────────────────────────────────────────────────────────────────
			Col("font_color", "Label", text);
			Col("font_shadow_color", "Label", new Color(0, 0, 0, 0.55f));   // legible over any world
			Col("font_outline_color", "Label", new Color(0, 0, 0, 0.75f));
			Set("panel", "ScrollContainer", Box(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 0, 0, 0, 0));

			Set("background", "ProgressBar", Art("bar_bg",   Box(Mul(baseTint, 0.7f) with { A = 0.75f }, edge, 1, 3, 0, 0)));
			Set("fill",       "ProgressBar", Art("bar_fill", Box(accent, accent, 0, 3, 0, 0)));

			// Toolbar category tabs. TabBar/TabContainer name their states differently from
			// Button, which is why these are set explicitly rather than folded into the loop.
			foreach (string t in new[] { "TabBar", "TabContainer" })
			{
				Set("tab_selected", t, Art("tab_selected", Box(raised, accent with { A = 0.8f }, 1, 5, 16, 7)));
				Set("tab_unselected", t, Art("tab_normal", Box(plate, edge, 1, 5, 16, 7)));
				Set("tab_hovered", t, Art("tab_selected", Box(raised, accent with { A = 0.6f }, 1, 5, 16, 7)));
				Col("font_selected_color", t, text);
				Col("font_unselected_color", t, textDim);   // NOT "font_color" — that key does not exist on TabBar
				Col("font_hovered_color", t, text);
			}

			// Tooltips ride over gameplay too, so they take the HUD plate rather than the menu one.
			Set("panel", "TooltipPanel", Art("tooltip", Box(raised, edge, 1, 5, 10, 7)));
			Col("font_color", "TooltipLabel", text);

			// Typography runs after this and would otherwise re-apply the MENU text colours as
			// per-node overrides, winning over everything set above.
			c.TextPrimary = text;
			c.TextOnDark = text;
			c.TextHover = text;
			c.TextDisabled = textDim;
			return c;
		}

		// ═══════════════════════════════════════════════
		// Page canvas + typography
		// ═══════════════════════════════════════════════

		/// <summary>Repaint the screen's page canvas with the theme's bg_canvas. Walks the
		/// themed subtree for a ColorRect named "Background"; skips translucent ones, which
		/// are dims over live gameplay rather than page canvases (see
		/// <see cref="ThemePageBackground"/>). Needs no scene change, so it also reaches
		/// projects generated before this existed.</summary>
		private static void ApplyPageBackground(Node node, ColorSchema c)
		{
			if (node is ColorRect rect && node.Name == "Background" && rect.Color.A >= 0.99f)
				rect.Color = c.BgCanvas;
			foreach (var child in node.GetChildren())
				ApplyPageBackground(child, c);
		}

		/// <summary>Marks a Label whose font size / color override THIS component owns, so a
		/// re-theme updates it while a size the scene author set by hand is left alone.
		/// Lives on the Label, so it is freed with it and needs no bookkeeping here.</summary>
		private const string TypographyMeta = "_beep_typography";

		/// <summary>Give each Label in the subtree its role's size and color.
		///
		/// A role is decided in this order:
		///   1. An explicit <c>theme_type_variation</c> of BeepTitle / BeepSubtitle /
		///      BeepValue / BeepCaption — this is the intended way, and always wins.
		///   2. COMPATIBILITY FALLBACK: the naming convention the shipped templates already
		///      follow (TitleLabel, *Title, *Caption, *Value, VersionLabel, HintLabel). This
		///      exists so a project generated before the variations existed still gets a
		///      hierarchy from a plain rebuild — regenerating scenes is easy to forget, and a
		///      template-only fix would never reach it. New scenes should set the variation.
		///
		/// A Label with no role is left entirely alone.</summary>
		private void ApplyTypography(Node node, ColorSchema c)
		{
			if (node is Kit.KitLabel)
				return;

			if (node is Label label && RoleFor(label) is { } role)
			{
				// An override we did not put there is the scene author's — don't stomp it.
				bool ours = label.HasMeta(TypographyMeta);
				bool authored = !ours && (label.HasThemeFontSizeOverride("font_size")
					|| label.HasThemeColorOverride("font_color"));
				if (!authored)
				{
					label.SetMeta(TypographyMeta, true);
					Kit.KitChrome.SetFontSizeOverrideIfChanged(label, "font_size", SizeFor(role));
					Kit.KitChrome.SetColorOverrideIfChanged(label, "font_color", ColorFor(role, c));
				}
			}
			foreach (var child in node.GetChildren())
				ApplyTypography(child, c);
		}

		/// <summary>Oversized HUD readout — a score, a speedometer, an energy counter. Exists
		/// because four main scenes hardcoded 32/40/56px font overrides to get a big readout,
		/// which bypasses the theme entirely: switching theme or geometry left them at a fixed
		/// size while every other label rescaled. A role keeps them big AND theme-driven.</summary>
		private const string DisplayVariation = "BeepDisplay";
		private const string TitleVariation = "BeepTitle";
		private const string SubtitleVariation = "BeepSubtitle";
		private const string ValueVariation = "BeepValue";
		private const string CaptionVariation = "BeepCaption";

		/// <summary>Which type step a Label belongs to, or null to leave it untouched.</summary>
		private static string? RoleFor(Label label)
		{
			string variation = label.ThemeTypeVariation.ToString();
			if (variation == DisplayVariation || variation == TitleVariation || variation == SubtitleVariation
				|| variation == ValueVariation || variation == CaptionVariation)
				return variation;

			string n = label.Name.ToString();
			if (n == "TitleLabel" || n == "BannerLabel" || n.EndsWith("Title")) return TitleVariation;
			if (n.EndsWith("Heading") || n == "WorldLabel") return SubtitleVariation;
			if (n.EndsWith("Value")) return ValueVariation;
			if (n.EndsWith("Caption") || n == "VersionLabel" || n == "HintLabel") return CaptionVariation;
			return null;
		}

		/// <summary>Type scale, as multiples of the theme's base size.
		///
		/// Was 1.9 / 1.35 / 1.25 / 0.85, which off a 17px base gave 32 / 23 / 21 / 14. Two
		/// problems: a 1.9x title is poster-sized for a dialog heading, and subtitle and value
		/// landed 2px apart (23 vs 21) — close enough to read as an inconsistency rather than a
		/// distinction, while the drop to the 14px caption was a cliff. These are a conventional
		/// ~1.25 ratio scale: each step is clearly separated from its neighbours and the title
		/// still dominates without shouting.
///
/// The caption step is for SECONDARY METADATA only (a save's Level/Time, a version
/// string, a hint). It is not a label style: a form field's label and a table row's
/// label are primary text and belong at the base size. Using caption for those is
/// what made "Save Name:" render at 13px disabled-grey beside a 16px input.</summary>
		private int SizeFor(string role) => role switch
		{
			DisplayVariation => Mathf.RoundToInt(Fs * 1.72f),
			TitleVariation => Mathf.RoundToInt(Fs * UiSurface.Multiplier(UiSurface.TextRole.Title)),
			SubtitleVariation => Mathf.RoundToInt(Fs * UiSurface.Multiplier(UiSurface.TextRole.Subtitle)),
			ValueVariation => Mathf.RoundToInt(Fs * UiSurface.Multiplier(UiSurface.TextRole.Value)),
			_ => Mathf.Max(12, Mathf.RoundToInt(Fs * UiSurface.Multiplier(UiSurface.TextRole.Caption))),
		};

		private static Color ColorFor(string role, ColorSchema c) => role switch
		{
			DisplayVariation => c.AccentPrimary,
			ValueVariation => c.AccentPrimary,
			CaptionVariation => c.TextDisabled,
			_ => c.TextPrimary,
		};

		private void RegisterFonts()
		{
			if (_themeFont == null) return;

			foreach (string type in new[]
			{
				"Label",
				"Button",
				"OptionButton",
				"MenuButton",
				"CheckButton",
				"CheckBox",
				"LineEdit",
				"TextEdit",
				"SpinBox",
				"Tree",
				"ItemList",
				"PopupMenu",
				"TabBar",
				"TooltipLabel",
			})
			{
				_generatedTheme!.SetFont("font", type, _themeFont);
			}

			_generatedTheme!.SetFont("normal_font", "RichTextLabel", _themeFont);
		}

		/// <summary>Register the four steps as Theme type variations of Label, so a scene can
		/// opt in explicitly with <c>theme_type_variation = &amp;"BeepTitle"</c> and see the
		/// result in the editor's inspector dropdown.</summary>
		private void RegisterTypography()
		{
			var c = _presetInstance!.Colors;
			foreach (var role in new[] { DisplayVariation, TitleVariation, SubtitleVariation, ValueVariation, CaptionVariation })
			{
				_generatedTheme!.AddType(role);
				_generatedTheme.SetTypeVariation(role, "Label");
				if (_themeFont != null)
					_generatedTheme.SetFont("font", role, _themeFont);
				_generatedTheme.SetFontSize("font_size", role, SizeFor(role));
				_generatedTheme.SetColor("font_color", role, ColorFor(role, c));
				_generatedTheme.SetColor("font_outline_color", role, c.ShadowColor);
			}
		}

		private static readonly string[] ButtonStates = { "normal", "hover", "pressed", "disabled", "focus" };

		private void ApplyButtonOverrides(Node node, IThemePreset preset)
		{
			if (node is Button btn)
			{
				// Take the generated procedural boxes for the concrete button type, then copy
				// them as per-node overrides so custom nodes and normal Godot buttons match.
				string typeName = node is OptionButton ? "OptionButton" : "Button";
				foreach (string state in ButtonStates)
					if (_generatedTheme != null && _generatedTheme.HasStylebox(state, typeName))
						Kit.KitChrome.SetStyleboxOverrideIfChanged(btn, state, Duplicate(_generatedTheme.GetStylebox(state, typeName)));
				Kit.KitChrome.SetColorOverrideIfChanged(btn, "font_color", preset.Colors.TextPrimary);
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
		/// selected states, separators — not just buttons) then sets it on the theme.
		///
		/// The slot name and control type are passed down so the register can tune specific
		/// procedural roles without scattering node-type checks through every builder.</summary>
		private void Sb(string name, string type, StyleBox box)
			=> _generatedTheme!.SetStylebox(name, type, StampGeometry(box, name, type));

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
			ResetInjectedButtons(btn);
			Kit.KitChrome.SetStyleboxOverrideIfChanged(btn, "normal", StampGeometry(Duplicate(preset.GetButtonNormal())));
			Kit.KitChrome.SetStyleboxOverrideIfChanged(btn, "hover", StampGeometry(Duplicate(preset.GetButtonHover())));
			Kit.KitChrome.SetStyleboxOverrideIfChanged(btn, "pressed", StampGeometry(Duplicate(preset.GetButtonPressed())));
			Kit.KitChrome.SetStyleboxOverrideIfChanged(btn, "disabled", StampGeometry(Duplicate(preset.GetButtonDisabled())));
			Kit.KitChrome.SetStyleboxOverrideIfChanged(btn, "focus", StampGeometry(Duplicate(preset.GetButtonFocus())));
			Kit.KitChrome.SetColorOverrideIfChanged(btn, "font_color", preset.Colors.TextPrimary);
			// > 0, not >= 0 — see the note on Fs in ThemePresetComponent.NodeTheming.cs. A 0 here
			// is not a size, and it reaches the text server as one.
			int fontSize = _geometry != null && _geometry.FontSize > 0
				? _geometry.FontSize
				: (_loadedThemeGeometry.FontSize > 0 ? _loadedThemeGeometry.FontSize : 14);
			Kit.KitChrome.SetFontSizeOverrideIfChanged(btn, "font_size", fontSize);
			if (EnableAnimations) SetupButtonAnimations(btn);
			if (EnableRippleOnClick) SetupRipple(btn);
		}

		// ═══════════════════════════════════════════════
		// Retired generated background
		// ═══════════════════════════════════════════════

		/// <summary>Remove generated background nodes left by older skin passes.</summary>
		private static void RemoveRetiredBackgroundNode(Godot.Control root)
		{
			Node? background = root.GetNodeOrNull<Node>(RetiredBackgroundNodeName);
			if (background != null && GodotObject.IsInstanceValid(background))
				background.QueueFree();
		}

		// ═══════════════════════════════════════════════
		// Animations
		// ═══════════════════════════════════════════════

		/// <summary>Marks a button whose hover/press handlers this component has already
		/// attached, so a re-theme doesn't attach a second set.</summary>
		private const string AnimatedMeta = "_beep_theme_animated";

		/// <summary>Marks a button that already owns a RippleComponent child.</summary>
		private const string RippledMeta = "_beep_theme_rippled";
		private const string RippleOwnedMeta = "_beep_theme_owned_ripple";
		private const string RippleNodeName = "BeepThemeRipple";

		private void ResetInjectedButtons(Godot.Control root)
		{
			foreach (var kvp in _activeTweens) kvp.Value?.Kill();
			_activeTweens.Clear();
			foreach (var disconnect in _buttonDisconnectors) disconnect();
			_buttonDisconnectors.Clear();

			foreach (var btn in FindAllButtons(root))
			{
				if (btn.HasMeta(AnimatedMeta)) btn.RemoveMeta(AnimatedMeta);
				if (!btn.HasMeta(RippledMeta)) continue;
				btn.RemoveMeta(RippledMeta);
				foreach (var child in btn.GetChildren())
				{
					if (child is RippleComponent ripple
						&& (ripple.Name == RippleNodeName || ripple.HasMeta(RippleOwnedMeta)))
					{
						ripple.QueueFree();
					}
				}
			}
		}

		/// <summary>Attach per-button chrome. MUST be idempotent: ApplyTheme is public and
		/// every one of this component's setters calls it, so a single scene load runs it
		/// several times (GameInfoBinder alone pushes genre, preset, palette and geometry in
		/// sequence). It previously wasn't — each pass added another RippleComponent child
		/// and another full set of hover/press handlers, so buttons ended up with a stack of
		/// ripples and several tweens racing on every hover. The meta flags live on the
		/// button, so they're freed with it and need no bookkeeping here.</summary>
		private void InjectIntoButtons(Godot.Control root)
		{
			foreach (var btn in FindAllButtons(root))
			{
				if (EnableAnimations) SetupButtonAnimations(btn);
				if (EnableRippleOnClick) SetupRipple(btn);
			}
		}

		private AudioStreamPlayer? _uiAudio;

		/// <summary>Play a short UI sound (hover/press). One reused player — fine for clicks.</summary>
		private void PlayUiSound(AudioStream? sound)
		{
			if (!EnableButtonSounds || sound == null || !IsInsideTree()) return;
			if (_uiAudio == null || !GodotObject.IsInstanceValid(_uiAudio))
			{
				_uiAudio = new AudioStreamPlayer { Name = "UiAudio", VolumeDb = ButtonSoundVolumeDb };
				AddChild(_uiAudio);
			}
			_uiAudio.Stream = sound;
			_uiAudio.Play();
		}

		private static AudioStream? LoadIfExists(string path)
			=> ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;

		private void SetupButtonAnimations(Button btn)
		{
			if (btn.HasMeta(AnimatedMeta)) return;
			btn.SetMeta(AnimatedMeta, true);

			// Animate the offset_transform layer, not scale/position — these buttons sit in menu
			// VBox/HBox/GridContainers that re-sort every layout pass and overwrote the raw
			// scale/position tweens (the exact fix theme_applier.gd uses). Offsets are relative
			// to the laid-out position, so neutral is Vector2.One / 0.
			btn.OffsetTransformEnabled = true;

			var anim = _presetInstance!.Animation;
			System.Action onMouseEntered = () =>
			{
				if (!IsActive || !EnableAnimations || !btn.IsVisibleInTree()) return;
				if (_activeTweens.TryGetValue(btn, out var e)) e?.Kill();
				var t = btn.CreateTween().SetParallel(true);
				PlayUiSound(HoverSound);
				t.TweenProperty(btn, "offset_transform_scale", new Vector2(anim.HoverScaleAmount, anim.HoverScaleAmount), anim.HoverScaleDuration).SetEase(Tween.EaseType.Out);
				if (anim.EnableShadowLift)
					t.TweenProperty(btn, "offset_transform_position:y", -2f, anim.HoverScaleDuration).SetEase(Tween.EaseType.Out);
				_activeTweens[btn] = t;
			};
			System.Action onMouseExited = () =>
			{
				if (!IsActive || !EnableAnimations) return;
				if (_activeTweens.TryGetValue(btn, out var e)) e?.Kill();
				var t = btn.CreateTween().SetParallel(true);
				t.TweenProperty(btn, "offset_transform_scale", Vector2.One, anim.HoverScaleDuration).SetEase(Tween.EaseType.Out);
				if (anim.EnableShadowLift)
					t.TweenProperty(btn, "offset_transform_position:y", 0f, anim.HoverScaleDuration).SetEase(Tween.EaseType.Out);
				_activeTweens[btn] = t;
			};
			System.Action onButtonDown = () =>
			{
				if (!IsActive || !EnableAnimations || !btn.IsVisibleInTree()) return;
				if (_activeTweens.TryGetValue(btn, out var e)) e?.Kill();
				var t = btn.CreateTween();
				PlayUiSound(PressSound);
				t.TweenProperty(btn, "offset_transform_scale", new Vector2(anim.PressScaleAmount, anim.PressScaleAmount), anim.PressScaleDuration).SetEase(Tween.EaseType.In);
				_activeTweens[btn] = t;
			};
			System.Action onButtonUp = () =>
			{
				if (!IsActive || !EnableAnimations) return;
				if (_activeTweens.TryGetValue(btn, out var e)) e?.Kill();
				var t = btn.CreateTween();
				t.TweenProperty(btn, "offset_transform_scale", Vector2.One, anim.PressScaleDuration * 1.5f).SetEase(Tween.EaseType.Out);
				_activeTweens[btn] = t;
			};
			btn.MouseEntered += onMouseEntered;
			btn.MouseExited += onMouseExited;
			btn.ButtonDown += onButtonDown;
			btn.ButtonUp += onButtonUp;
			_buttonDisconnectors.Add(() =>
			{
				if (!GodotObject.IsInstanceValid(btn)) return;
				btn.MouseEntered -= onMouseEntered;
				btn.MouseExited -= onMouseExited;
				btn.ButtonDown -= onButtonDown;
				btn.ButtonUp -= onButtonUp;
			});
			if (anim.EnableFocusGlow)
			{
				// Glow toward the theme's secondary accent so the focus state matches the theme.
				var c = _presetInstance!.Colors;
				Color glowTarget = c.AccentSecondary.Blend(c.TextOnDark);
				System.Action onFocusEntered = () =>
				{
					if (!IsActive || !EnableAnimations) return;
					btn.CreateTween().TweenProperty(btn, "modulate", glowTarget, 0.2f).SetEase(Tween.EaseType.Out);
				};
				System.Action onFocusExited = () =>
				{
					if (!IsActive || !EnableAnimations) return;
					btn.CreateTween().TweenProperty(btn, "modulate", new Color(1f, 1f, 1f, 1f), 0.2f).SetEase(Tween.EaseType.Out);
				};
				btn.FocusEntered += onFocusEntered;
				btn.FocusExited += onFocusExited;
				_buttonDisconnectors.Add(() =>
				{
					if (!GodotObject.IsInstanceValid(btn)) return;
					btn.FocusEntered -= onFocusEntered;
					btn.FocusExited -= onFocusExited;
				});
			}
		}

		private void SetupRipple(Button btn)
		{
			if (btn.HasMeta(RippledMeta)) return;
			btn.SetMeta(RippledMeta, true);

			// Ripple uses the theme's primary accent so it matches the chosen theme/palette.
			var c = _presetInstance!.Colors;
			var ripple = new RippleComponent
			{
				Name = RippleNodeName,
				RippleColor = new Color(c.AccentPrimary.R, c.AccentPrimary.G, c.AccentPrimary.B, 0.35f),
				Duration = 0.5f, MaxRadius = 120f, IsActive = true
			};
			ripple.SetMeta(RippleOwnedMeta, true);
			btn.AddChild(ripple);
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
