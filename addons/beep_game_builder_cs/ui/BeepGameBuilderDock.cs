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

    // ── Skin & screen tools ──
    private LineEdit _newScreenName;
    private CheckBox _overwriteScenes;
    private OptionButton _textureSource;
    private LineEdit _textureRoot;

    /// <summary>
    /// The editor's own UI scale. Every metric in this dock is multiplied by it.
    ///
    /// Godot scales the whole editor on a hiDPI display (1.5x, 2x, and the user can set it
    /// manually in Editor Settings). A dock that hardcodes pixels does not scale with it, so this
    /// one's 10pt captions and 8px spacers were unreadable slivers at 2x while every native panel
    /// beside them looked right. Same defect class as the kit's flat font size, one layer up.
    /// </summary>
    private static float EditorScale =>
        Engine.IsEditorHint() ? EditorInterface.Singleton?.GetEditorScale() ?? 1f : 1f;

    private static int S(float px) => Mathf.Max(1, Mathf.RoundToInt(px * EditorScale));

    /// <summary>A font size relative to the EDITOR's own, not a literal. Roles rather than
    /// numbers, mirroring UiSurface.TextRole on the runtime side.</summary>
    private int DockFont(float scale)
    {
        int b = GetThemeFontSize("font_size", "Label");
        if (b <= 0) b = Mathf.RoundToInt(14 * EditorScale);
        return Mathf.Max(8, Mathf.RoundToInt(b * scale));
    }

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
        title.AddThemeFontSizeOverride("font_size", DockFont(1.30f));   // dock title
        AddChild(title);

        var subtitle = new Label
        {
            Text = "Pick a genre, set your options, click Create Game.\nComponents are added via Add Node (Ctrl+A).",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.AddThemeFontSizeOverride("font_size", DockFont(0.85f)); // caption
        AddChild(subtitle);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(scroll);
        var b = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(b);

        // ── 1. Genre + skin pickers ──
        AddSectionHeader(b, "1. Pick your genre");
        _genrePicker = AddDropdown(b, "Genre");
        _genreDescription = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _genreDescription.AddThemeFontSizeOverride("font_size", DockFont(0.85f));
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
        // Regenerating a project SKIPS scenes that already exist, so template fixes never reach an
        // existing project (a stale main_menu.tscn kept the Load button dead, for one). Tick this to
        // overwrite existing scenes with the current templates. Off by default — it also overwrites
        // player_template/enemy_template/levels, the scenes you're meant to edit in place.
        _overwriteScenes = new CheckBox { Text = "Overwrite existing scenes (refresh from templates)", ButtonPressed = false };
        b.AddChild(_overwriteScenes);

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

        // ── 5. Skin + screen tools ──
        AddSectionHeader(b, "5. Skin & screens");

        // Where the art behind every theme slot comes from. The theme/skin system itself is
        // unchanged — this only chooses the SOURCE, so a project can ship the built-in art,
        // point at its own folder, or run textureless on the procedural look.
        _textureSource = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _textureSource.AddItem("Built-in textures (shipped with the addon)", (int)TextureSlotDef.SourceMode.BuiltIn);
        _textureSource.AddItem("My own textures (folder below)",            (int)TextureSlotDef.SourceMode.Custom);
        _textureSource.AddItem("No textures — procedural skin only",        (int)TextureSlotDef.SourceMode.None);
        _textureSource.Selected = _textureSource.GetItemIndex((int)TextureSlotDef.Source);
        b.AddChild(Row("Texture source", _textureSource));

        _textureRoot = new LineEdit
        {
            PlaceholderText = "res://ui_textures   (…/<genre>/<theme>/<slot>.png, or flat <slot>.png)",
            Text = TextureSlotDef.CustomRoot,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        b.AddChild(Row("My textures folder", _textureRoot));

        var applySrcBtn = new Button { Text = "✔ Apply texture source" };
        applySrcBtn.Pressed += ApplyTextureSource;
        b.AddChild(applySrcBtn);
        b.AddChild(new Label
        {
            Text = "Own textures fall back per slot: supply only the files you want to replace and "
                 + "the rest keep the built-in art.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        b.AddChild(new HSeparator());
        b.AddChild(new Label { Text = "Every theme.json declares 9-patch textures. Bake writes them from that theme's own colors." });

        var bakeThemeBtn = new Button { Text = "🎨 Bake textures — selected genre" };
        bakeThemeBtn.Pressed += () => RunBake(allGenres: false);
        b.AddChild(bakeThemeBtn);

        var bakeAllBtn = new Button { Text = "🎨 Bake textures — ALL genres + page backgrounds" };
        bakeAllBtn.Pressed += () => RunBake(allGenres: true);
        b.AddChild(bakeAllBtn);

        b.AddChild(new HSeparator());
        var driftBtn = new Button { Text = "🔎 Check my scenes against the templates" };
        driftBtn.Pressed += CheckSceneDrift;
        b.AddChild(driftBtn);
        b.AddChild(new Label
        {
            Text = "Generated scenes are COPIES, not references — an addon update does not reach "
                 + "them. This reports which of yours have fallen behind, and changes nothing. "
                 + "Tick 'Overwrite existing scenes' above and re-Create to pull them forward "
                 + "(that discards your edits to those files).",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var galleryBtn = new Button { Text = "🔍 Open Theme Gallery (compare textures vs procedural)" };
        galleryBtn.Pressed += OpenThemeGallery;
        b.AddChild(galleryBtn);

        b.AddChild(new HSeparator());
        _newScreenName = new LineEdit { PlaceholderText = "New screen name (e.g. Shop, RaceResults)" };
        b.AddChild(_newScreenName);
        var newScreenBtn = new Button { Text = "✚ New screen for the selected genre" };
        newScreenBtn.Pressed += CreateNewScreen;
        b.AddChild(newScreenBtn);

        // ── Output log ──
        _output = new TextEdit { CustomMinimumSize = new Vector2(0, S(100)), Editable = false, PlaceholderText = "Output..." };
        AddChild(_output);
    }

    /// <summary>Persist the texture source into ProjectSettings and re-skin what is open.
    /// Written to ProjectSettings rather than held in the dock so the running GAME honours it
    /// too — the dock is an editor tool, and a setting only the editor knew would make the
    /// export behave differently from the editor preview.</summary>
    private void ApplyTextureSource()
    {
        var mode = (TextureSlotDef.SourceMode)_textureSource.GetItemId(_textureSource.Selected);
        string root = _textureRoot.Text.Trim();

        if (mode == TextureSlotDef.SourceMode.Custom)
        {
            if (string.IsNullOrEmpty(root))
            {
                Log("✖ Pick a folder first — 'My own textures' with an empty path would silently "
                  + "fall back to the built-in art for every slot.");
                return;
            }
            if (!DirAccess.DirExistsAbsolute(root))
            {
                Log($"✖ '{root}' does not exist. Create it, or point at the folder holding your PNGs.");
                return;
            }
        }

        ProjectSettings.SetSetting(TextureSlotDef.SettingSource, mode.ToString());
        ProjectSettings.SetSetting(TextureSlotDef.SettingRoot, root);
        // Keep them in the saved project so an exported build resolves the same art.
        ProjectSettings.SetInitialValue(TextureSlotDef.SettingSource,
                                        TextureSlotDef.SourceMode.BuiltIn.ToString());
        ProjectSettings.SetInitialValue(TextureSlotDef.SettingRoot, "");
        ProjectSettings.Save();
        TextureSlotDef.RefreshSourceSettings();

        int reskinned = ReapplyOpenThemes();
        Log(mode switch
        {
            TextureSlotDef.SourceMode.None =>
                $"✔ Texture source: none — every theme now renders its procedural box. Re-skinned {reskinned} component(s).",
            TextureSlotDef.SourceMode.Custom =>
                $"✔ Texture source: '{root}'. Slots you have not supplied keep the built-in art. Re-skinned {reskinned} component(s).",
            _ => $"✔ Texture source: built-in addon art. Re-skinned {reskinned} component(s).",
        });
    }

    /// <summary>Re-run ApplyTheme on every ThemePresetComponent in the edited scene so the
    /// change is visible immediately instead of on the next scene load.</summary>
    private int ReapplyOpenThemes()
    {
        var root = EditorInterface.Singleton?.GetEditedSceneRoot();
        if (root == null) return 0;
        int n = 0;
        void Walk(Node node)
        {
            if (node is Beep.ECS.UI.ThemePresetComponent tp) { tp.ApplyTheme(); n++; }
            foreach (var child in node.GetChildren()) Walk(child);
        }
        Walk(root);
        return n;
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
        if (gid == null) return;                 // nothing picked yet: not an error
        var genre = SkinCatalog.GetGenre(gid);
        if (genre == null)
        {
            // SILENT before. The theme and palette dropdowns simply stayed empty and the user had
            // no way to tell a catalog-loading failure from a genre that legitimately has no
            // themes. Both look like a dead dropdown.
            Log($"[ERROR] Genre '{gid}' is in the picker but not in the skin catalog — "
              + "catalogs/skins/ may have failed to load. Theme and palette lists stay empty.");
            return;
        }
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
        else Log($"[WARN] Genre '{gid}' declares no themes, so there is nothing to skin it with. "
               + $"Add catalogs/skins/{gid}/themes/<name>/theme.json.");
    }

    private void OnThemeItemSelected(long index) => OnThemeChanged();

    private void OnThemeChanged()
    {
        string gid = GetSelectedGenreId(), tid = GetSelectedThemeId();
        if (gid == null || tid == null) return;   // nothing picked yet: not an error
        var theme = SkinCatalog.GetTheme(gid, tid);
        if (theme == null)
        {
            Log($"[ERROR] Theme '{tid}' is listed for genre '{gid}' but has no theme.json the "
              + "catalog could read. The palette list stays empty.");
            return;
        }

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
        var log = BeepGenreGenerator.CreateProject(gid, info, overwrite: _overwriteScenes.ButtonPressed);
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

    /// <summary>Report which generated scenes are behind their templates. READ-ONLY.
    ///
    /// The comparison lives in <see cref="BeepSceneDrift"/>, not here: this dock creates an
    /// EditorResourcePicker in _Ready, so instantiating it outside the editor segfaults and
    /// nothing embedded in it can be tested headlessly. The dock formats; the helper decides.</summary>
    private void CheckSceneDrift()
    {
        foreach (string line in BeepSceneTemplateAudit.Describe(BeepSceneTemplateAudit.AuditAll()))
            Log(line);
        foreach (string line in BeepSceneDrift.Describe(BeepSceneDrift.Compare()))
            Log(line);
    }

    // ════════════════════════════════════════════════════════════════
    // UI helpers
    // ════════════════════════════════════════════════════════════════

    private void AddSectionHeader(VBoxContainer parent, string text)
    {
        parent.AddChild(new Godot.Control { CustomMinimumSize = new Vector2(0, S(8)) });
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", DockFont(1.05f));      // section header
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
        sb = new SpinBox { MinValue = min, MaxValue = max, Value = val, Rounded = true, CustomMinimumSize = new Vector2(S(minWidth), 0) };
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

    // ════════════════════════════════════════════════════════════════
    // Skin & screen tools
    // ════════════════════════════════════════════════════════════════

    /// <summary>Write the 9-patch PNGs every theme.json already points at. Until this runs the
    /// whole texture pipeline is inert: SkinCatalog cannot load a file that isn't there, so each
    /// slot falls back to the procedural box.</summary>
    private void RunBake(bool allGenres)
    {
        string genreId = GetSelectedGenreId();
        if (!allGenres && string.IsNullOrEmpty(genreId)) { Log("✗ Pick a genre first."); return; }

        Log(allGenres ? "Baking every genre…" : $"Baking '{genreId}'…");
        var log = allGenres ? BeepTextureBaker.BakeAll() : BeepTextureBaker.BakeGenre(genreId);
        foreach (var line in log) Log("  " + line);
        Log("Done. Godot re-imports the new files automatically; toggle Textures in the Theme Gallery to compare.");
    }

    private void OpenThemeGallery()
    {
        const string src = "res://addons/beep_game_builder_cs/templates/scenes/theme_gallery.tscn";
        if (!ResourceLoader.Exists(src)) { Log($"✗ {src} is missing."); return; }
        EditorInterface.Singleton.OpenSceneFromPath(src);
        Log("Opened the Theme Gallery — press F5-equivalent (Play Scene) to interact with it.");
    }

    /// <summary>Stamp a new screen that already follows the repo's conventions (themed opaque
    /// Background, BeepTitle header + rule, sized back button, ThemePresetComponent on the
    /// content Control, and a script wiring by NAME).</summary>
    private void CreateNewScreen()
    {
        string genreId = GetSelectedGenreId();
        if (string.IsNullOrEmpty(genreId)) { Log("✗ Pick a genre first."); return; }
        string name = _newScreenName?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) { Log("✗ Type a screen name first."); return; }

        foreach (var line in BeepScreenGenerator.CreateScreen(genreId, name, "", overwrite: false)) Log("  " + line);
        EditorInterface.Singleton.GetResourceFilesystem()?.Scan();
    }
}
