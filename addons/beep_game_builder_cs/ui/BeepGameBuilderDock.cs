using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Beep.ECS;                  // GameApp, BeepGenreScene
using Beep.ECS.UI;                // SkinCatalog, ThemePresetComponent
using Beep.GameBuilder;            // GameInfo

namespace Beep.GameBuilder;

/// <summary>
/// Beep Game Builder editor dock. Three tabs:
///
/// <list type="bullet">
///   <item>
///     <term>App</term> GameInfo field grid (name/version/dev/genre/theme/
///     palette/geometry/scene paths/tuning). Save to res://game_info.tres
///     + reload from disk. Read-only autoload status (GameApp, GameInfo).
///   </item>
///   <item>
///     <term>Theme</term> Cascading Genre → Theme → Palette → Geometry
///     dropdowns driven from <c>SkinCatalog.AllGenres</c>. Click "Apply to
///     All Components" to re-theme every <see cref="ThemePresetComponent"/> in
///     the open scene.
///   </item>
///   <item>
///     <term>Settings</term> Project-level writes to <c>ProjectSettings</c>
///     (resolution / FPS / pixel art / fullscreen / main scene /
///     internationalization).
///   </item>
/// </list>
///
/// Components are added via Godot's native Add Node dialog (Ctrl+A) — every
/// <c>[GlobalClass] [Tool]</c> component appears categorized under its base
/// (UIComponent / GameplayComponent / ControllerComponent / WorldComponent /
/// EntityComponent). No component browser is provided here; Godot already
/// has one.
/// </summary>
[Tool]
public partial class BeepGameBuilderDock : VBoxContainer
{
    public EditorPlugin EditorPlugin { get; set; }

    private TextEdit _output;

    // GameInfo edit fields.
    private LineEdit _gameName, _version, _developer, _description;
    private LineEdit _themePreset, _paletteName, _geometryProfile;
    private LineEdit _mainMenuPath, _gameScenePath, _settingsScenePath, _gameOverScenePath;
    private SpinBox _resW, _resH, _targetFps;
    private SpinBox _gravity, _jumpVel, _moveSpd, _fireRate;
    private SpinBox _gridW, _gridH, _targetScore;
    private CheckBox _pixelArt;

    // Theme tab.
    private OptionButton _genrePicker, _themePicker, _palettePicker;
    private readonly List<string> _genreIds = new();
    private readonly List<string> _themeIds = new();
    private readonly List<string> _paletteIds = new();
    private Label _genreDescription, _autoloadStatus, _settingsAutoloadStatus;

    public override void _Ready()
    {
        Name = "Beep Game Builder";
        BeepFileUtils.LogCallback = msg => Log(msg);
        BeepFileUtils.ErrorCallback = msg => Log("[ERROR] " + msg);
        BuildUI();
    }

    private void BuildUI()
    {
        var title = new Label { Text = "Beep Game Builder", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 16);
        AddChild(title);

        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(tabs);

        AddAppTab(tabs);
        AddThemeTab(tabs);
        AddSettingsTab(tabs);

        // Output log at the bottom of the dock — shared across all tabs.
        _output = new TextEdit { CustomMinimumSize = new Vector2(0, 100), Editable = false, PlaceholderText = "Output..." };
        AddChild(_output);
    }

    // ════════════════════════════════════════════════════════════════
    // App tab — GameInfo editor + autoload probe
    // ════════════════════════════════════════════════════════════════

    private void AddAppTab(TabContainer tabs)
    {
        var scroll = new ScrollContainer
        {
            Name = "App",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var b = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(b);
        tabs.AddChild(scroll);

        // ── Autoload status (read-only) ──
        _autoloadStatus = new Label { Text = "" };
        _autoloadStatus.AddThemeFontSizeOverride("font_size", 11);
        b.AddChild(_autoloadStatus);

        var refreshStatusBtn = new Button { Text = "↻ Refresh status" };
        refreshStatusBtn.Pressed += RefreshAutoloadStatus;
        b.AddChild(refreshStatusBtn);

        b.AddChild(new HSeparator());

        // ── Identity section ──
        AddSectionHeader(b, "Identity");
        _gameName = AddField(b, "Game name", "My Game");
        _version = AddField(b, "Version", "0.1.0");
        _developer = AddField(b, "Developer", "");
        _description = AddField(b, "Description", "");

        // ── Theme + palette + geometry selection (text fields, not dropdowns — dropdowns live in Theme tab) ──
        AddSectionHeader(b, "Skin (drop a BeepGenreScene into your scene to apply)");
        _themePreset = AddField(b, "Default theme preset", "modern");
        _paletteName = AddField(b, "Palette name", "Default");
        _geometryProfile = AddField(b, "Geometry profile", "As-Authored");

        // ── Scene paths section ──
        AddSectionHeader(b, "Scene paths");
        _mainMenuPath = AddField(b, "Main menu", GameInfo.DefaultMainMenuPath);
        _gameScenePath = AddField(b, "Game scene", GameInfo.DefaultGameScenePath);
        _settingsScenePath = AddField(b, "Settings menu", GameInfo.DefaultSettingsScenePath);
        _gameOverScenePath = AddField(b, "Game over", GameInfo.DefaultGameOverScenePath);

        // ── Display section ──
        AddSectionHeader(b, "Display");
        var resRow = new HBoxContainer();
        resRow.AddChild(new Label { Text = "Resolution: " });
        _resW = NewSpinBox(320, 3840, 1280);
        _resH = NewSpinBox(240, 2160, 720);
        resRow.AddChild(_resW);
        resRow.AddChild(new Label { Text = " × " });
        resRow.AddChild(_resH);
        b.AddChild(resRow);
        _targetFps = NewSpinBox(30, 240, 60);
        _targetFps.CustomMinimumSize = new Vector2(80, 0);
        b.AddChild(WithLabel("Target FPS", _targetFps));
        _pixelArt = new CheckBox { Text = "Pixel art (texture filter off)" };
        b.AddChild(_pixelArt);

        // ── Tuning section ──
        AddSectionHeader(b, "Tuning");
        _gravity = NewSpinBoxFloat(-2000, 2000, 980);
        b.AddChild(WithLabel("Gravity", _gravity));
        _jumpVel = NewSpinBoxFloat(-2000, 2000, -400);
        b.AddChild(WithLabel("Jump velocity", _jumpVel));
        _moveSpd = NewSpinBoxFloat(0, 2000, 200);
        b.AddChild(WithLabel("Move speed", _moveSpd));
        _fireRate = NewSpinBoxFloat(0.01, 5, 0.2, 0.01);
        b.AddChild(WithLabel("Fire rate (s)", _fireRate));
        _gridW = NewSpinBox(1, 20, 8);
        _gridH = NewSpinBox(1, 20, 8);
        _targetScore = NewSpinBox(0, 1_000_000, 1000);
        b.AddChild(WithLabel("Puzzle grid width", _gridW));
        b.AddChild(WithLabel("Puzzle grid height", _gridH));
        b.AddChild(WithLabel("Puzzle target score", _targetScore));

        // ── Save / Reload / Apply actions ──
        b.AddChild(new HSeparator());
        var saveBtn = new Button { Text = "💾 Save to game_info.tres" };
        saveBtn.Pressed += SaveGameInfo;
        b.AddChild(saveBtn);

        var reloadBtn = new Button { Text = "🔄 Reload from game_info.tres" };
        reloadBtn.Pressed += LoadGameInfoIntoForm;
        b.AddChild(reloadBtn);

        var applyBtn = new Button { Text = "▶ Apply live to all ThemePresetComponents in open scene" };
        applyBtn.Pressed += ApplyLiveToAllComponents;
        b.AddChild(applyBtn);

        b.AddChild(new HSeparator());
        var genBtn = new Button { Text = "▶ Generate Full Project (one-click starter)" };
        genBtn.Pressed += GenerateFullProject;
        b.AddChild(genBtn);

        RefreshAutoloadStatus();
    }

    private void RefreshAutoloadStatus()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Autoloads (registered in project.godot):");
        var app = GameApp.Instance;
        sb.AppendLine(app != null ? "  ✅ /root/GameApp   found" : "  ⚠ /root/GameApp   NOT registered");
        sb.AppendLine(GameInfo.Instance != null ? "  ✅ /root/GameInfo  found" : "  ⚠ /root/GameInfo  NOT registered");
        sb.AppendLine("Tip: GameApp autoload is registered by saving game_info.tres with GameInfo.TresPath. "
                    + "If missing, add node \"GameApp\" under /root in the SceneTree dock "
                    + "(Project → SceneTree → Children → Add child node → Beep → GameApp).");
        _autoloadStatus.Text = sb.ToString();
        if (_settingsAutoloadStatus != null) _settingsAutoloadStatus.Text = sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════
    // Theme tab — cascading pickers + apply
    // ════════════════════════════════════════════════════════════════

    private void AddThemeTab(TabContainer tabs)
    {
        var scroll = new ScrollContainer
        {
            Name = "Theme",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var b = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(b);
        tabs.AddChild(scroll);

        AddSectionHeader(b, "Pick a skin — applies when you click 'Apply to All Components'");

        _genrePicker = new OptionButton();
        _genreDescription = new Label { CustomMinimumSize = new Vector2(0, 40) };
        _genreDescription.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _themePicker = new OptionButton();
        _palettePicker = new OptionButton();

        b.AddChild(WithLabel("Genre", _genrePicker));
        b.AddChild(_genreDescription);
        b.AddChild(WithLabel("Theme", _themePicker));
        b.AddChild(WithLabel("Palette", _palettePicker));

        _genrePicker.ItemSelected += OnGenreItemSelected;
        _themePicker.ItemSelected += OnThemeItemSelected;

        b.AddChild(new HSeparator());

        var applyBtn = new Button { Text = "▶ Apply to all ThemePresetComponents in open scene" };
        applyBtn.Pressed += () => ApplyThemeToOpenScene(false);
        b.AddChild(applyBtn);

        var applyBtnForce = new Button { Text = "↻ Re-apply (sets every ThemePresetComponent to selected skin)" };
        applyBtnForce.Pressed += () => ApplyThemeToOpenScene(true);
        b.AddChild(applyBtnForce);

        b.AddChild(new HSeparator());

        AddSectionHeader(b, "Other");
        var showCatBtn = new Button { Text = "📂 Show skin catalog in FileSystem dock" };
        showCatBtn.Pressed += () =>
        {
            EditorPlugin?.GetEditorInterface()?.GetFileSystemDock()?.NavigateToPath(
                "res://addons/beep_game_builder_cs/catalogs/skins/");
            Log("Navigated FileSystem dock to catalogs/skins/.");
        };
        b.AddChild(showCatBtn);

        PopulateGenres();
    }

    // ════════════════════════════════════════════════════════════════
    // Settings tab — ProjectSettings writes + autoload probe
    // ════════════════════════════════════════════════════════════════

    private void AddSettingsTab(TabContainer tabs)
    {
        var scroll = new ScrollContainer
        {
            Name = "Settings",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var b = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(b);
        tabs.AddChild(scroll);

        _settingsAutoloadStatus = new Label();
        _settingsAutoloadStatus.AddThemeFontSizeOverride("font_size", 11);
        b.AddChild(_settingsAutoloadStatus);

        var refreshBtn = new Button { Text = "↻ Refresh status" };
        refreshBtn.Pressed += RefreshAutoloadStatus;
        b.AddChild(refreshBtn);

        b.AddChild(new HSeparator());

        AddSectionHeader(b, "Display");
        var resRow = new HBoxContainer();
        resRow.AddChild(new Label { Text = "Default resolution: " });
        var settingsResW = NewSpinBox(320, 3840, 1280);
        var settingsResH = NewSpinBox(240, 2160, 720);
        resRow.AddChild(settingsResW);
        resRow.AddChild(new Label { Text = " × " });
        resRow.AddChild(settingsResH);
        b.AddChild(resRow);
        var fpsRow = new HBoxContainer();
        fpsRow.AddChild(new Label { Text = "Default FPS: " });
        var settingsFps = NewSpinBox(30, 240, 60);
        fpsRow.AddChild(settingsFps);
        b.AddChild(fpsRow);
        var fullscreenCb = new CheckBox { Text = "Window start mode: fullscreen" };
        b.AddChild(fullscreenCb);

        var applyProjBtn = new Button { Text = "💾 Apply Display settings to ProjectSettings" };
        applyProjBtn.Pressed += () =>
        {
            ProjectSettings.SetSetting("display/window/size/viewport_width", (int)settingsResW.Value);
            ProjectSettings.SetSetting("display/window/size/viewport_height", (int)settingsResH.Value);
            ProjectSettings.SetSetting("application/run/max_fps", (int)settingsFps.Value);
            ProjectSettings.SetSetting("display/window/size/mode", fullscreenCb.ButtonPressed ? 3 : 0);
            ProjectSettings.Save();
            Log("ProjectSettings display updated + saved.");
        };
        b.AddChild(applyProjBtn);

        b.AddChild(new HSeparator());

        AddSectionHeader(b, "Localization");
        b.AddChild(new Label { Text = "Translations CSV:" });
        var i18nPath = new Label();
        i18nPath.AddThemeFontSizeOverride("font_size", 10);
        string trPath = "res://i18n/translations.csv";
        i18nPath.Text = FileAccess.FileExists(trPath) ? "  ✅ " + trPath : "  ⚠ " + trPath + " (missing — run localization setup first)";
        b.AddChild(i18nPath);
        var enableI18nBtn = new Button { Text = "💾 Enable translations in ProjectSettings" };
        enableI18nBtn.Pressed += () =>
        {
            ProjectSettings.SetSetting("internationalization/locale/translations", true);
            ProjectSettings.Save();
            Log("internationalization/locale/translations = true (saved).");
        };
        b.AddChild(enableI18nBtn);

        RefreshAutoloadStatus();
    }

    // ════════════════════════════════════════════════════════════════
    // Theme tab — population + cascade
    // ════════════════════════════════════════════════════════════════

    private void PopulateGenres()
    {
        _genrePicker.Clear();
        _genreIds.Clear();
        foreach (var kvp in SkinCatalog.AllGenres.OrderBy(k => k.Key))
        {
            _genrePicker.AddItem($"{kvp.Value.Icon} {kvp.Value.DisplayName}", _genreIds.Count);
            _genreIds.Add(kvp.Key);
        }
        if (_genreIds.Count > 0)
        {
            _genrePicker.Select(0);
            OnGenreChanged();
        }
    }

    /// <summary>Named signal handler (Godot 4.7 — named methods disconnect cleanly).</summary>
    private void OnGenreItemSelected(long index) => OnGenreChanged();

    private void OnGenreChanged()
    {
        string genreId = GetSelectedGenreId();
        if (genreId == null) return;
        var genre = SkinCatalog.GetGenre(genreId);
        if (genre == null) return;
        _genreDescription.Text = $"{(string.IsNullOrEmpty(genre.Icon) ? "" : genre.Icon + "  ")}{genre.Description}";

        _themePicker.Clear();
        _themeIds.Clear();
        int defaultIdx = 0, i = 0;
        foreach (var tkvp in genre.Themes)
        {
            _themePicker.AddItem(tkvp.Value.DisplayName, i);
            if (tkvp.Key == genre.DefaultTheme) defaultIdx = i;
            _themeIds.Add(tkvp.Key);
            i++;
        }
        if (_themeIds.Count > 0)
        {
            _themePicker.Select(defaultIdx);
            OnThemeChanged();
        }
    }

    /// <summary>Named signal handler for theme picker.</summary>
    private void OnThemeItemSelected(long index) => OnThemeChanged();

    private void OnThemeChanged()
    {
        string genreId = GetSelectedGenreId();
        string themeId = GetSelectedThemeId();
        if (genreId == null || themeId == null) return;
        var theme = SkinCatalog.GetTheme(genreId, themeId);
        if (theme == null) return;

        _palettePicker.Clear();
        _paletteIds.Clear();
        int defaultIdx = 0, i = 0;
        foreach (var pkvp in theme.Palettes)
        {
            _palettePicker.AddItem(pkvp.Value.DisplayName, i);
            if (pkvp.Key == "default") defaultIdx = i;
            _paletteIds.Add(pkvp.Key);
            i++;
        }
        if (_paletteIds.Count > 0) _palettePicker.Select(defaultIdx);
    }

    private string GetSelectedGenreId()
        => _genrePicker != null && _genrePicker.Selected >= 0 && _genrePicker.Selected < _genreIds.Count
            ? _genreIds[_genrePicker.Selected] : null;

    private string GetSelectedThemeId()
        => _themePicker != null && _themePicker.Selected >= 0 && _themePicker.Selected < _themeIds.Count
            ? _themeIds[_themePicker.Selected] : null;

    // ════════════════════════════════════════════════════════════════
    // Theme tab — Apply
    // ════════════════════════════════════════════════════════════════

    private void ApplyThemeToOpenScene(bool force)
    {
        string genreId = GetSelectedGenreId();
        string themeId = GetSelectedThemeId();
        if (string.IsNullOrEmpty(genreId) || string.IsNullOrEmpty(themeId))
        { Log("No genre/theme selected."); return; }
        string palId = (_palettePicker.Selected >= 0 && _palettePicker.Selected < _paletteIds.Count)
            ? _paletteIds[_palettePicker.Selected] : "default";

        var root = EditorPlugin?.GetEditorInterface()?.GetEditedSceneRoot();
        if (root == null) { Log("No scene is currently open in the editor."); return; }

        int count = 0;
        Walk(root, node =>
        {
            if (node is ThemePresetComponent tpc)
            {
                tpc.GenreName = genreId;
                tpc.PresetName = themeId;
                tpc.PaletteName = palId;
                // Geometry comes from the genre's geometry.json (per-genre, single profile).
                var genre = SkinCatalog.GetGenre(genreId);
                if (genre?.Geometry != null)
                    tpc.GeometryProfileName = genre.Geometry.DisplayName;
                if (force) tpc.ApplyTheme();
                count++;
            }
        });
        Log($"Applied {genreId}/{themeId}/{palId} to {count} ThemePresetComponent(s) in '{root.Name}'.");
    }

    /// <summary>Depth-first walk over every Node under <paramref name="n"/>.</summary>
    private static void Walk(Node n, Action<Node> visit)
    {
        visit(n);
        foreach (var child in n.GetChildren())
            Walk(child, visit);
    }

    // ════════════════════════════════════════════════════════════════
    // App tab — Save / Load / Apply
    // ════════════════════════════════════════════════════════════════

    private void SaveGameInfo()
    {
        var info = ReadFormIntoGameInfo();
        var err = ResourceSaver.Save(info, GameInfo.TresPath);
        if (err == Error.Ok) Log($"Saved: {GameInfo.TresPath}"); else Log($"[ERROR] Save failed: {err}");
    }

    private void LoadGameInfoIntoForm()
    {
        var info = LoadGameInfoFromDisk();
        if (info == null) { Log($"No {GameInfo.TresPath} found."); return; }
        _gameName.Text = info.GameName;
        _version.Text = info.Version;
        _developer.Text = info.Developer;
        _description.Text = info.Description;
        _themePreset.Text = info.DefaultThemePreset;
        _paletteName.Text = info.PaletteName;
        _geometryProfile.Text = info.GeometryProfileName;
        _mainMenuPath.Text = info.MainMenuPath;
        _gameScenePath.Text = info.GameScenePath;
        _settingsScenePath.Text = info.SettingsScenePath;
        _gameOverScenePath.Text = info.GameOverScenePath;
        _resW.Value = info.TargetResolutionWidth;
        _resH.Value = info.TargetResolutionHeight;
        _targetFps.Value = info.TargetFps;
        _pixelArt.ButtonPressed = info.PixelArt;
        _gravity.Value = info.Gravity;
        _jumpVel.Value = info.JumpVelocity;
        _moveSpd.Value = info.MoveSpeed;
        _fireRate.Value = info.FireRate;
        _gridW.Value = info.GridWidth;
        _gridH.Value = info.GridHeight;
        _targetScore.Value = info.TargetScore;
        Log($"Loaded: {GameInfo.TresPath}");
    }

    private GameInfo ReadFormIntoGameInfo()
    {
        var info = LoadGameInfoFromDisk() ?? new GameInfo();
        info.GameName = _gameName.Text ?? "My Game";
        info.Version = _version.Text ?? "0.1.0";
        info.Developer = _developer.Text ?? "";
        info.Description = _description.Text ?? "";
        info.DefaultThemePreset = string.IsNullOrWhiteSpace(_themePreset.Text) ? "modern" : _themePreset.Text;
        info.PaletteName = string.IsNullOrWhiteSpace(_paletteName.Text) ? "Default" : _paletteName.Text;
        info.GeometryProfileName = string.IsNullOrWhiteSpace(_geometryProfile.Text) ? "As-Authored" : _geometryProfile.Text;
        info.MainMenuPath = string.IsNullOrWhiteSpace(_mainMenuPath.Text) ? GameInfo.DefaultMainMenuPath : _mainMenuPath.Text;
        info.GameScenePath = string.IsNullOrWhiteSpace(_gameScenePath.Text) ? GameInfo.DefaultGameScenePath : _gameScenePath.Text;
        info.SettingsScenePath = string.IsNullOrWhiteSpace(_settingsScenePath.Text) ? GameInfo.DefaultSettingsScenePath : _settingsScenePath.Text;
        info.GameOverScenePath = string.IsNullOrWhiteSpace(_gameOverScenePath.Text) ? GameInfo.DefaultGameOverScenePath : _gameOverScenePath.Text;
        info.TargetResolutionWidth = (int)_resW.Value;
        info.TargetResolutionHeight = (int)_resH.Value;
        info.TargetFps = (int)_targetFps.Value;
        info.PixelArt = _pixelArt.ButtonPressed;
        info.Gravity = (float)_gravity.Value;
        info.JumpVelocity = (float)_jumpVel.Value;
        info.MoveSpeed = (float)_moveSpd.Value;
        info.FireRate = (float)_fireRate.Value;
        info.GridWidth = (int)_gridW.Value;
        info.GridHeight = (int)_gridH.Value;
        info.TargetScore = (int)_targetScore.Value;
        // Genre is data-driven from the skin catalog; users re-pick by replacing
        // DefaultThemePreset + GenreName on a BeepGenreScene. We don't store a
        // fixed enum here so adding a genre doesn't break old projects.
        return info;
    }

    private static GameInfo? LoadGameInfoFromDisk()
        => FileAccess.FileExists(GameInfo.TresPath)
            ? ResourceLoader.Load<GameInfo>(GameInfo.TresPath)
            : null;

    private void ApplyLiveToAllComponents()
    {
        var root = EditorPlugin?.GetEditorInterface()?.GetEditedSceneRoot();
        if (root == null) { Log("No scene open."); return; }

        var info = ReadFormIntoGameInfo();
        var err = ResourceSaver.Save(info, GameInfo.TresPath);
        if (err != Error.Ok) { Log($"[ERROR] Save failed: {err}"); return; }

        int count = 0;
        Walk(root, node =>
        {
            if (node is ThemePresetComponent tpc)
            {
                tpc.ApplyTheme();
                count++;
            }
        });
        Log($"Saved game_info.tres + re-themed {count} ThemePresetComponent(s).");
    }

    private void GenerateFullProject()
    {
        var info = ReadFormIntoGameInfo();
        // Pick the genre from the Theme tab's current selection, or fall back to
        // whatever's in game_info.tres.
        string genreId = GetSelectedGenreId() ?? info.Genre.ToString().ToLowerInvariant();
        if (string.IsNullOrEmpty(genreId)) { Log("[ERROR] No genre selected."); return; }

        // Pick theme from the Theme tab if one is selected.
        string themeId = GetSelectedThemeId();
        if (!string.IsNullOrEmpty(themeId)) info.DefaultThemePreset = themeId;

        // Pick palette from the Theme tab if one is selected.
        if (_palettePicker != null && _palettePicker.Selected >= 0 && _palettePicker.Selected < _paletteIds.Count)
            info.PaletteName = _paletteIds[_palettePicker.Selected];

        Log($"Generating {genreId} project: {info.GameName}...");
        var log = BeepGenreGenerator.CreateProject(genreId, info, overwrite: false);
        foreach (var line in log) Log(line);
    }

    // ════════════════════════════════════════════════════════════════
    // UI helpers
    // ════════════════════════════════════════════════════════════════

    private void AddSectionHeader(VBoxContainer parent, string text)
    {
        parent.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 13);
        parent.AddChild(lbl);
        parent.AddChild(new HSeparator());
    }

    private static HBoxContainer WithLabel(string label, Node child)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = $"{label}: " });
        row.AddChild(child);
        return row;
    }

    private static LineEdit AddField(VBoxContainer parent, string label, string defaultValue)
    {
        var edit = new LineEdit
        {
            Text = defaultValue ?? "",
            PlaceholderText = label,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        parent.AddChild(WithLabel(label, edit));
        return edit;
    }

    private static SpinBox NewSpinBox(int min, int max, int val)
    {
        var sb = new SpinBox { MinValue = min, MaxValue = max, Value = val, Rounded = true };
        sb.CustomMinimumSize = new Vector2(80, 0);
        return sb;
    }

    private static SpinBox NewSpinBoxFloat(double min, double max, double val, double step = 1)
    {
        var sb = new SpinBox { MinValue = min, MaxValue = max, Value = val, Step = step };
        sb.CustomMinimumSize = new Vector2(80, 0);
        return sb;
    }

    private void Log(string msg)
    {
        if (_output != null) _output.Text += msg + "\n";
        GD.Print("[Beep] " + msg);
    }
}
