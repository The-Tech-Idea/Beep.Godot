using Godot;
using System;
using System.Collections.Generic;
using Beep.ECS;               // GameApp
using Beep.ECS.UI;             // SkinCatalog, ThemePresetComponent
using Beep.GameBuilder;         // GameInfo, BeepGenreGenerator

namespace Beep.GameBuilder;

/// <summary>
/// Beep Game Builder editor dock. A SINGLE scrollable form — one page,
/// one button. The user picks a genre (dynamically loaded from the
/// file-based skin catalog), sets game options, and clicks "Create Game".
/// All scenes, navigation, theming, and autoloads are stamped in one pass.
///
/// Adding components (Health, Attack, TopDownController, etc.) is done via
/// Godot's native Add Node dialog (Ctrl+A) — every [GlobalClass] component
/// appears there automatically.
/// </summary>
[Tool]
public partial class BeepGameBuilderDock : VBoxContainer
{
    public EditorPlugin EditorPlugin { get; set; }

    private TextEdit _output;

    // ── Picker state ──
    private OptionButton _genrePicker, _themePicker, _palettePicker;
    private EditorResourcePicker _skinPicker;
    private readonly List<string> _genreIds = new();
    private readonly List<string> _themeIds = new();
    private readonly List<string> _paletteIds = new();
    private Label _genreDescription;

    // ── Game identity ──
    private LineEdit _gameName, _version;

    // ── Display ──
    private SpinBox _resW, _resH, _targetFps;
    private CheckBox _pixelArt;

    public override void _Ready()
    {
        Name = "Beep Game Builder";
        BeepFileUtils.LogCallback = msg => Log(msg);
        BeepFileUtils.ErrorCallback = msg => Log("[ERROR] " + msg);
        BuildUI();
    }

    /// <summary>Drop the static callbacks. They capture `this`, and the plugin QueueFrees the
    /// dock on _ExitTree — leaving BeepFileUtils holding lambdas that write to a freed node.</summary>
    public override void _ExitTree()
    {
        BeepFileUtils.LogCallback = _ => { };
        BeepFileUtils.ErrorCallback = _ => { };
        base._ExitTree();
    }

    private void BuildUI()
    {
        var title = new Label
        {
            Text = "Beep Game Builder",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        AddChild(title);

        var subtitle = new Label
        {
            Text = "Pick a genre, set your options, click Create Game.\nComponents are added via Add Node (Ctrl+A).",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 10);
        AddChild(subtitle);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(scroll);
        var b = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(b);

        // ── 1. Genre + skin pickers ──
        AddSectionHeader(b, "1. Pick your genre");
        _genrePicker = AddDropdown(b, "Genre");
        _genreDescription = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _genreDescription.AddThemeFontSizeOverride("font_size", 10);
        b.AddChild(_genreDescription);

        _themePicker = AddDropdown(b, "Theme");
        _palettePicker = AddDropdown(b, "Palette");
        // Geometry is per-genre (geometry.json) — applied automatically.

        _skinPicker = new EditorResourcePicker { BaseType = "UISkin", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        b.AddChild(Row("Texture Skin (optional)", _skinPicker));

        _genrePicker.ItemSelected += OnGenreItemSelected;
        _themePicker.ItemSelected += OnThemeItemSelected;
        PopulateGenres();

        // ── 2. Game identity ──
        AddSectionHeader(b, "2. Name your game");
        _gameName = AddField(b, "Game name", "My Game");
        _version = AddField(b, "Version", "0.1.0");

        // ── 3. Display ──
        AddSectionHeader(b, "3. Display");
        b.AddChild(Row("Resolution",
            SpinBox(320, 3840, 1280, 80, out _resW),
            new Label { Text = " × " },
            SpinBox(240, 2160, 720, 80, out _resH)));
        b.AddChild(Row("Target FPS", SpinBox(30, 240, 60, 80, out _targetFps)));
        _pixelArt = new CheckBox { Text = "Pixel art (texture filter off)", ButtonPressed = true };
        b.AddChild(_pixelArt);

        // ── 4. Actions ──
        AddSectionHeader(b, "4. Create");
        var genBtn = new Button { Text = "▶ Create Game (one-click)" };
        genBtn.Pressed += GenerateFullProject;
        b.AddChild(genBtn);

        b.AddChild(new HSeparator());
        b.AddChild(new Label { Text = "After creating, the main scene is set to scenes/ui/main_menu.tscn — just press Play (F5)." });
        var saveBtn = new Button { Text = "💾 Save current settings to game_info.tres" };
        saveBtn.Pressed += SaveGameInfo;
        b.AddChild(saveBtn);
        var reloadBtn = new Button { Text = "🔄 Reload from game_info.tres" };
        reloadBtn.Pressed += LoadGameInfoIntoForm;
        b.AddChild(reloadBtn);

        // ── Output log ──
        _output = new TextEdit { CustomMinimumSize = new Vector2(0, 100), Editable = false, PlaceholderText = "Output..." };
        AddChild(_output);
    }

    // ════════════════════════════════════════════════════════════════
    // Genre → Theme → Palette cascading
    // ════════════════════════════════════════════════════════════════

    private void PopulateGenres()
    {
        _genrePicker.Clear(); _genreIds.Clear();
        foreach (var kvp in SkinCatalog.AllGenres)
        {
            _genrePicker.AddItem($"{kvp.Value.Icon} {kvp.Value.DisplayName}", _genreIds.Count);
            _genreIds.Add(kvp.Key);
        }
        if (_genreIds.Count > 0) { _genrePicker.Select(0); OnGenreChanged(); }
    }

    private void OnGenreItemSelected(long index) => OnGenreChanged();

    private void OnGenreChanged()
    {
        string gid = GetSelectedGenreId();
        if (gid == null) return;
        var genre = SkinCatalog.GetGenre(gid);
        if (genre == null) return;
        _genreDescription.Text = genre.Description;

        _themePicker.Clear(); _themeIds.Clear();
        int defaultIdx = 0, i = 0;
        foreach (var t in genre.Themes)
        {
            _themePicker.AddItem(t.Value.DisplayName, i);
            if (t.Key == genre.DefaultTheme) defaultIdx = i;
            _themeIds.Add(t.Key); i++;
        }
        if (_themeIds.Count > 0) { _themePicker.Select(defaultIdx); OnThemeChanged(); }
    }

    private void OnThemeItemSelected(long index) => OnThemeChanged();

    private void OnThemeChanged()
    {
        string gid = GetSelectedGenreId(), tid = GetSelectedThemeId();
        if (gid == null || tid == null) return;
        var theme = SkinCatalog.GetTheme(gid, tid);
        if (theme == null) return;

        _palettePicker.Clear(); _paletteIds.Clear();
        int defaultIdx = 0, i = 0;
        foreach (var p in theme.Palettes)
        {
            _palettePicker.AddItem(p.Value.DisplayName, i);
            if (p.Key == "default") defaultIdx = i;
            _paletteIds.Add(p.Key); i++;
        }
        if (_paletteIds.Count > 0) _palettePicker.Select(defaultIdx);
    }

    private string GetSelectedGenreId()
        => _genrePicker.Selected >= 0 && _genrePicker.Selected < _genreIds.Count
            ? _genreIds[_genrePicker.Selected] : null;

    private string GetSelectedThemeId()
        => _themePicker.Selected >= 0 && _themePicker.Selected < _themeIds.Count
            ? _themeIds[_themePicker.Selected] : null;

    // ════════════════════════════════════════════════════════════════
    // One-click generate
    // ════════════════════════════════════════════════════════════════

    /// <summary>The genre's declared default theme, or any theme folder it actually has.
    /// Null when the genre ships no themes at all.</summary>
    private static string? FirstAvailableTheme(GenreDef? genre)
    {
        if (genre == null || genre.Themes.Count == 0) return null;
        if (!string.IsNullOrEmpty(genre.DefaultTheme) && genre.Themes.ContainsKey(genre.DefaultTheme))
            return genre.DefaultTheme;
        foreach (var t in genre.Themes.Values) return t.Id;
        return null;
    }

    private void GenerateFullProject()
    {
        string gid = GetSelectedGenreId();
        if (gid == null) { Log("[ERROR] No genre selected."); return; }

        var genre = SkinCatalog.GetGenre(gid);
        // Fall back through the catalog only — the genre's declared default, then whatever
        // theme folder actually exists. Never guess a theme name that may not be there.
        string tid = GetSelectedThemeId() ?? FirstAvailableTheme(genre);
        if (tid == null) { Log($"[ERROR] Genre '{gid}' has no themes in the skin catalog."); return; }
        string pid = (_palettePicker.Selected >= 0 && _palettePicker.Selected < _paletteIds.Count)
            ? _paletteIds[_palettePicker.Selected] : "default";

        var info = LoadGameInfoFromDisk() ?? new GameInfo();
        info.GameName = string.IsNullOrWhiteSpace(_gameName.Text) ? "My Game" : _gameName.Text;
        // Was hardcoded to "0.1.0", which discarded whatever the user typed in the Version
        // box and reset an existing game_info.tres back to 0.1.0 on every generate.
        info.Version = string.IsNullOrWhiteSpace(_version.Text) ? "0.1.0" : _version.Text;
        info.GenreId = gid;
        info.DefaultThemePreset = tid;
        info.PaletteName = pid;
        info.Skin = _skinPicker.EditedResource as Beep.ECS.UI.UISkin;
        info.GeometryProfileName = genre?.Geometry?.DisplayName ?? "As-Authored";
        info.TargetResolutionWidth = (int)_resW.Value;
        info.TargetResolutionHeight = (int)_resH.Value;
        info.TargetFps = (int)_targetFps.Value;
        info.PixelArt = _pixelArt.ButtonPressed;

        Log($"Stamping {genre?.DisplayName ?? gid} project: {info.GameName} ({tid}/{pid}) …");
        var log = BeepGenreGenerator.CreateProject(gid, info, overwrite: false);
        foreach (var line in log) Log(line);

        Log("Done. The run/main scene is set to scenes/ui/main_menu.tscn — press Play (F5).");
    }

    // ════════════════════════════════════════════════════════════════
    // Save / Load from game_info.tres
    // ════════════════════════════════════════════════════════════════

    private void SaveGameInfo()
    {
        var info = ReadFormIntoGameInfo();
        if (ResourceSaver.Save(info, GameInfo.TresPath) == Error.Ok)
            Log($"Saved: {GameInfo.TresPath}");
        else
            Log($"[ERROR] Save failed: {GameInfo.TresPath}");
    }

    private void LoadGameInfoIntoForm()
    {
        var info = LoadGameInfoFromDisk();
        if (info == null) { Log($"No {GameInfo.TresPath} found."); return; }
        _gameName.Text = info.GameName;
        _version.Text = info.Version;
        _resW.Value = info.TargetResolutionWidth;
        _resH.Value = info.TargetResolutionHeight;
        _targetFps.Value = info.TargetFps;
        _pixelArt.ButtonPressed = info.PixelArt;
        _skinPicker.EditedResource = info.Skin;

        // Select genre → cascades
        string gid = info.GenreId;
        int gi = _genreIds.IndexOf(gid);
        if (gi >= 0) { _genrePicker.Select(gi); OnGenreChanged(); }

        int ti = _themeIds.IndexOf(info.DefaultThemePreset.ToLowerInvariant());
        if (ti >= 0) { _themePicker.Select(ti); OnThemeChanged(); }

        // Restore the palette LAST: OnThemeChanged's cascade rebuilds the palette list and resets it to
        // "default", so a saved non-default palette must be re-selected after the theme cascade runs.
        if (!string.IsNullOrEmpty(info.PaletteName))
        {
            int pi = _paletteIds.IndexOf(info.PaletteName.ToLowerInvariant());
            if (pi >= 0) _palettePicker.Select(pi);
        }

        Log($"Reloaded from {GameInfo.TresPath}.");
    }

    private GameInfo ReadFormIntoGameInfo()
    {
        var info = LoadGameInfoFromDisk() ?? new GameInfo();
        info.GameName = string.IsNullOrWhiteSpace(_gameName.Text) ? "My Game" : _gameName.Text;
        if (!string.IsNullOrWhiteSpace(_version.Text)) info.Version = _version.Text;
        info.TargetResolutionWidth = (int)_resW.Value;
        info.TargetResolutionHeight = (int)_resH.Value;
        info.TargetFps = (int)_targetFps.Value;
        info.PixelArt = _pixelArt.ButtonPressed;
        info.Skin = _skinPicker.EditedResource as Beep.ECS.UI.UISkin;
        string gid = GetSelectedGenreId();
        if (gid != null) info.GenreId = gid;
        string tid = GetSelectedThemeId();
        if (tid != null) info.DefaultThemePreset = tid;
        // Save must persist the same fields Generate does — it previously dropped Palette and Geometry,
        // so the two paths disagreed and a saved non-default palette was silently lost.
        if (_palettePicker.Selected >= 0 && _palettePicker.Selected < _paletteIds.Count)
            info.PaletteName = _paletteIds[_palettePicker.Selected];
        var genre = gid != null ? SkinCatalog.GetGenre(gid) : null;
        info.GeometryProfileName = genre?.Geometry?.DisplayName ?? info.GeometryProfileName;
        return info;
    }

    private static GameInfo? LoadGameInfoFromDisk()
        => FileAccess.FileExists(GameInfo.TresPath)
            ? ResourceLoader.Load<GameInfo>(GameInfo.TresPath) : null;

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

    private LineEdit AddField(VBoxContainer parent, string label, string defaultValue)
    {
        var edit = new LineEdit { Text = defaultValue ?? "", PlaceholderText = label, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(Row(label, edit));
        return edit;
    }

    private OptionButton AddDropdown(VBoxContainer parent, string label)
    {
        var btn = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(Row(label, btn));
        return btn;
    }

    private static Node SpinBox(int min, int max, int val, int minWidth, out SpinBox sb)
    {
        sb = new SpinBox { MinValue = min, MaxValue = max, Value = val, Rounded = true, CustomMinimumSize = new Vector2(minWidth, 0) };
        return sb;
    }

    private static HBoxContainer Row(string label, params Node[] children)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = $"{label}: " });
        foreach (var c in children) row.AddChild(c);
        return row;
    }

    private void Log(string msg)
    {
        if (_output != null) _output.Text += msg + "\n";
        GD.Print("[Beep] " + msg);
    }
}
