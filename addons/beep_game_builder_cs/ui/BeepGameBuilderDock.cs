using Godot;
using System;
using System.Collections.Generic;

namespace Beep.GameBuilder;

/// <summary>
/// Beep Game Builder editor dock. A SINGLE scrollable form — no tabs.
/// Top to bottom: Genre → Theme → Palette → Geometry → Game Identity →
/// Display → Audio → Language → Generate button.
///
/// Everything else (components) is added via Godot's native Add Node dialog —
/// all [GlobalClass] components appear categorized under EntityComponent →
/// UIComponent / GameplayComponent / ControllerComponent / WorldComponent.
/// </summary>
[Tool]
public partial class BeepGameBuilderDock : VBoxContainer
{
    public EditorPlugin EditorPlugin { get; set; }

    private TextEdit _output;

    // Genre + skin cascading pickers.
    private OptionButton _genrePicker, _themePicker, _palettePicker;
    private Label _genreDescription;
    private readonly List<string> _genreIds = new();
    private readonly List<string> _themeIds = new();
    private readonly List<string> _paletteIds = new();

    // Game identity.
    private LineEdit _gameName, _version, _developer, _description;

    // Display.
    private SpinBox _resW, _resH, _targetFps;
    private CheckBox _pixelArt, _fullscreen;

    // Audio.
    private HSlider _masterVol, _sfxVol, _musicVol;

    // Language.
    private OptionButton _language;

    // Regen mode.
    private OptionButton _regenMode;

    public override void _Ready()
    {
        Name = "Beep Game Builder";
        BeepFileUtils.LogCallback = msg => Log(msg);
        BeepFileUtils.ErrorCallback = msg => Log("[ERROR] " + msg);
        BuildUI();
    }

    private void BuildUI()
    {
        // Title.
        var title = new Label { Text = "Beep Game Builder", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 16);
        AddChild(title);

        var subtitle = new Label
        {
            Text = "Configure your game, then Generate.\n"
                 + "Add components via Add Node (Ctrl+A).",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 10);
        AddChild(subtitle);

        // Single scrollable form — no TabContainer.
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(scroll);
        var b = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(b);

        BuildGenreSection(b);
        BuildIdentitySection(b);
        BuildDisplaySection(b);
        BuildAudioSection(b);
        BuildLanguageSection(b);
        BuildActionsSection(b);

        // Output log.
        _output = new TextEdit { CustomMinimumSize = new Vector2(0, 100), Editable = false, PlaceholderText = "Output..." };
        AddChild(_output);
    }

    // ════════════════════════════════════════════════════════════════
    // Genre + Skin section (cascading dropdowns)
    // ════════════════════════════════════════════════════════════════

    private void BuildGenreSection(VBoxContainer b)
    {
        AddSectionHeader(b, "Genre & Skin");

        _genrePicker = AddDropdown(b, "Genre");
        _genreDescription = new Label { Text = "", CustomMinimumSize = new Vector2(0, 30) };
        _genreDescription.AddThemeFontSizeOverride("font_size", 10);
        _genreDescription.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        b.AddChild(_genreDescription);

        _themePicker = AddDropdown(b, "Theme");
        _palettePicker = AddDropdown(b, "Palette");
        // Geometry is per-genre (geometry.json) — applied automatically, no picker needed.

        // Wire cascading. Use named method references (not lambdas) so Godot
        // can cleanly disconnect on cleanup. Select() doesn't emit item_selected
        // in Godot 4, so we call the cascade methods manually after each Select().
        _genrePicker.ItemSelected += OnGenreItemSelected;
        _themePicker.ItemSelected += OnThemeItemSelected;

        PopulateGenres();
    }

    private void PopulateGenres()
    {
        _genrePicker.Clear();
        _genreIds.Clear();
        foreach (var kvp in Beep.ECS.UI.SkinCatalog.AllGenres)
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

    /// <summary>Signal handler for _genrePicker.ItemSelected (named method, not lambda).</summary>
    private void OnGenreItemSelected(long index) => OnGenreChanged();

    /// <summary>Signal handler for _themePicker.ItemSelected (named method, not lambda).</summary>
    private void OnThemeItemSelected(long index) => OnThemeChanged();

    private void OnGenreChanged()
    {
        string genreId = GetSelectedGenreId();
        if (genreId == null) return;
        var genre = Beep.ECS.UI.SkinCatalog.GetGenre(genreId);
        if (genre == null) return;

        _genreDescription.Text = genre.Description;

        // Themes.
        _themePicker.Clear();
        _themeIds.Clear();
        int defaultIdx = 0;
        int i = 0;
        foreach (var tkvp in genre.Themes)
        {
            _themePicker.AddItem(tkvp.Value.DisplayName, i);
            if (tkvp.Key == genre.DefaultTheme) defaultIdx = i;
            _themeIds.Add(tkvp.Key);
            i++;
        }
        if (_themeIds.Count > 0)
            _themePicker.Select(defaultIdx);
        OnThemeChanged();
    }

    private void OnThemeChanged()
    {
        string genreId = GetSelectedGenreId();
        string themeId = GetSelectedThemeId();
        if (genreId == null || themeId == null) return;
        var theme = Beep.ECS.UI.SkinCatalog.GetTheme(genreId, themeId);
        if (theme == null) return;

        _palettePicker.Clear();
        _paletteIds.Clear();
        int defaultIdx = 0;
        int i = 0;
        foreach (var pkvp in theme.Palettes)
        {
            _palettePicker.AddItem(pkvp.Value.DisplayName, i);
            if (pkvp.Key == "default") defaultIdx = i;
            _paletteIds.Add(pkvp.Key);
            i++;
        }
        if (_paletteIds.Count > 0)
            _palettePicker.Select(defaultIdx);
    }

    // ════════════════════════════════════════════════════════════════
    // Game Identity section
    // ════════════════════════════════════════════════════════════════

    private void BuildIdentitySection(VBoxContainer b)
    {
        AddSectionHeader(b, "Game Identity");
        _gameName = AddField(b, "Game Name", "My Game");
        _version = AddField(b, "Version", "0.1.0");
        _developer = AddField(b, "Developer", "");
        _description = AddField(b, "Description", "");
    }

    // ════════════════════════════════════════════════════════════════
    // Display section
    // ════════════════════════════════════════════════════════════════

    private void BuildDisplaySection(VBoxContainer b)
    {
        AddSectionHeader(b, "Display");

        var resRow = new HBoxContainer();
        resRow.AddChild(new Label { Text = "Resolution: " });
        _resW = new SpinBox { MinValue = 320, MaxValue = 3840, Value = 1280, CustomMinimumSize = new Vector2(80, 0) };
        _resH = new SpinBox { MinValue = 240, MaxValue = 2160, Value = 720, CustomMinimumSize = new Vector2(80, 0) };
        resRow.AddChild(_resW);
        resRow.AddChild(new Label { Text = " × " });
        resRow.AddChild(_resH);
        b.AddChild(resRow);

        _targetFps = AddSpinBox(b, "Target FPS", 30, 240, 60);
        _pixelArt = new CheckBox { Text = "Pixel Art", ButtonPressed = true };
        b.AddChild(_pixelArt);
        _fullscreen = new CheckBox { Text = "Fullscreen" };
        b.AddChild(_fullscreen);
    }

    // ════════════════════════════════════════════════════════════════
    // Audio section
    // ════════════════════════════════════════════════════════════════

    private void BuildAudioSection(VBoxContainer b)
    {
        AddSectionHeader(b, "Audio");
        _masterVol = AddSlider(b, "Master", 80);
        _sfxVol = AddSlider(b, "SFX", 90);
        _musicVol = AddSlider(b, "Music", 70);
    }

    // ════════════════════════════════════════════════════════════════
    // Language section
    // ════════════════════════════════════════════════════════════════

    private void BuildLanguageSection(VBoxContainer b)
    {
        AddSectionHeader(b, "Language");
        _language = new OptionButton();
        _language.AddItem("English (en)", 0);
        _language.AddItem("Español (es)", 1);
        _language.AddItem("日本語 (ja)", 2);
        _language.Select(0);
        b.AddChild(_language);
    }

    // ════════════════════════════════════════════════════════════════
    // Actions section — Generate + Save + Reload
    // ════════════════════════════════════════════════════════════════

    private void BuildActionsSection(VBoxContainer b)
    {
        AddSectionHeader(b, "Actions");

        // Regen mode selector — controls what happens to existing scenes.
        _regenMode = AddDropdown(b, "Regen Mode");
        _regenMode.AddItem("Skip existing (safe)", 0);
        _regenMode.AddItem("Update unmodified only", 1);
        _regenMode.AddItem("Overwrite all", 2);
        _regenMode.Selected = 0;

        var hint = new Label { Text = "• Skip existing: never touch files that exist\n"
                                   + "• Update unmodified: refresh scenes you haven't edited\n"
                                   + "• Overwrite all: replace everything (destroys edits)" };
        hint.AddThemeFontSizeOverride("font_size", 9);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        b.AddChild(hint);

        AddButton(b, "▶ Generate Project", GenerateProject);
        AddButton(b, "🔄 Reload from game_info.tres", LoadFromGameInfo);
    }

    /// <summary>Generate a full starter project from the current selections.</summary>
    private void GenerateProject()
    {
        string genreId = GetSelectedGenreId();
        if (genreId == null) { Log("No genre selected."); return; }

        var genre = Beep.ECS.UI.SkinCatalog.GetGenre(genreId);
        string themeId = GetSelectedThemeId() ?? genre?.DefaultTheme ?? "modern";
        string paletteDisplayName = GetSelectedPaletteDisplayName();
        // Geometry comes from the genre's geometry.json — applied automatically.
        string geometryName = genre?.Geometry?.DisplayName ?? "As-Authored";

        var info = LoadGameInfoFromFile() ?? new GameInfo();
        info.GameName = string.IsNullOrWhiteSpace(_gameName.Text) ? "My Game" : _gameName.Text;
        info.Version = _version.Text;
        info.Developer = _developer.Text;
        info.Description = _description.Text;
        info.Genre = GameInfo.GenreFromId(genreId);
        info.DefaultThemePreset = themeId;
        info.PaletteName = paletteDisplayName;
        info.GeometryProfileName = geometryName;
        info.TargetResolutionWidth = (int)_resW.Value;
        info.TargetResolutionHeight = (int)_resH.Value;
        info.TargetFps = (int)_targetFps.Value;
        info.PixelArt = _pixelArt.ButtonPressed;

        Log($"Generating {genre.DisplayName} project: {info.GameName}...");
        var mode = _regenMode.Selected switch
        {
            1 => BeepGenreGenerator.RegenMode.UpdateUnmodified,
            2 => BeepGenreGenerator.RegenMode.OverwriteAll,
            _ => BeepGenreGenerator.RegenMode.SkipExisting
        };
        var log = BeepGenreGenerator.CreateProject(genreId, info, mode);
        foreach (var line in log) Log(line);
    }

    private void LoadFromGameInfo()
    {
        var info = LoadGameInfoFromFile();
        if (info == null) { Log("No game_info.tres found. Generate a project first."); return; }

        _gameName.Text = info.GameName;
        _version.Text = info.Version;
        _developer.Text = info.Developer;
        _description.Text = info.Description;
        _resW.Value = info.TargetResolutionWidth;
        _resH.Value = info.TargetResolutionHeight;
        _targetFps.Value = info.TargetFps;
        _pixelArt.ButtonPressed = info.PixelArt;

        // Select genre → cascades themes → cascades palettes.
        string genreId = info.Genre.ToString().ToLowerInvariant();
        int genreIdx = _genreIds.IndexOf(genreId);
        if (genreIdx >= 0)
        {
            _genrePicker.Selected = genreIdx;
            OnGenreChanged();

            int themeIdx = _themeIds.IndexOf(info.DefaultThemePreset.ToLowerInvariant());
            if (themeIdx >= 0)
            {
                _themePicker.Selected = themeIdx;
                OnThemeChanged();
            }
        }
        Log("Loaded from game_info.tres.");
    }

    private static GameInfo? LoadGameInfoFromFile()
    {
        if (!FileAccess.FileExists(GameInfo.TresPath)) return null;
        return ResourceLoader.Load<GameInfo>(GameInfo.TresPath);
    }

    // ════════════════════════════════════════════════════════════════
    // Picker helpers
    // ════════════════════════════════════════════════════════════════

    private string GetSelectedGenreId()
        => _genrePicker.Selected >= 0 && _genrePicker.Selected < _genreIds.Count
            ? _genreIds[_genrePicker.Selected] : null;

    private string GetSelectedThemeId()
        => _themePicker.Selected >= 0 && _themePicker.Selected < _themeIds.Count
            ? _themeIds[_themePicker.Selected] : null;

    private string GetSelectedPaletteDisplayName()
    {
        string genreId = GetSelectedGenreId();
        string themeId = GetSelectedThemeId();
        if (genreId == null || themeId == null) return "Default";
        var theme = Beep.ECS.UI.SkinCatalog.GetTheme(genreId, themeId);
        if (theme == null || _palettePicker.Selected < 0 || _palettePicker.Selected >= _paletteIds.Count)
            return "Default";
        string palId = _paletteIds[_palettePicker.Selected];
        return theme.Palettes.TryGetValue(palId, out var pal) ? pal.DisplayName : "Default";
    }

    // ════════════════════════════════════════════════════════════════
    // UI helpers
    // ════════════════════════════════════════════════════════════════

    private void AddSectionHeader(VBoxContainer parent, string text)
    {
        parent.AddChild(new Label()); // spacer
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 14);
        parent.AddChild(lbl);
    }

    private LineEdit AddField(VBoxContainer parent, string label, string defaultValue)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = $"{label}: " });
        var edit = new LineEdit { Text = defaultValue, CustomMinimumSize = new Vector2(200, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(edit);
        parent.AddChild(row);
        return edit;
    }

    private OptionButton AddDropdown(VBoxContainer parent, string label)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = $"{label}: " });
        var btn = new OptionButton { CustomMinimumSize = new Vector2(200, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(btn);
        parent.AddChild(row);
        return btn;
    }

    private SpinBox AddSpinBox(VBoxContainer parent, string label, double min, double max, double val)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = $"{label}: " });
        var sb = new SpinBox { MinValue = min, MaxValue = max, Value = val, CustomMinimumSize = new Vector2(80, 0) };
        row.AddChild(sb);
        parent.AddChild(row);
        return sb;
    }

    private HSlider AddSlider(VBoxContainer parent, string label, float defaultVal)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = $"{label}: " });
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Value = defaultVal, CustomMinimumSize = new Vector2(150, 0) };
        row.AddChild(slider);
        var valLabel = new Label { Text = $"{(int)defaultVal}%", CustomMinimumSize = new Vector2(40, 0) };
        // Named-method connection (not lambda) so Godot disconnects cleanly on cleanup.
        slider.ValueChanged += (double v) => OnSliderValueChanged(v, valLabel);
        row.AddChild(valLabel);
        parent.AddChild(row);
        return slider;
    }

    private static void OnSliderValueChanged(double v, Label valLabel)
        => valLabel.Text = $"{(int)v}%";

    private void AddButton(Godot.Control parent, string text, Action action)
    {
        var btn = new Button { Text = text };
        btn.Pressed += action;
        parent.AddChild(btn);
    }

    private void Log(string msg)
    {
        if (_output != null)
            _output.Text += msg + "\n";
        GD.Print("[Beep] " + msg);
    }
}
