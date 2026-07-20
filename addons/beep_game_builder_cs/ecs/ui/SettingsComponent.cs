using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// SINGLE RESPONSIBILITY: user settings persistence. This is the ONLY class that
    /// owns runtime user preferences (audio volume, display, language). It:
    /// • Loads from user://settings.cfg on _Ready.
    /// • Auto-saves on every Set (with a SettingsChanged signal).
    /// • Applies audio (AudioServer bus volumes) and display (DisplayServer window mode).
    ///
    /// Place as an autoload or in the boot scene. Other components read/write via
    /// SettingsComponent.Instance or GetSiblingComponent&lt;SettingsComponent&gt;().
    ///
    /// Replaces: BeepConfigManager (static, dead), ConfigManagerComponent (dead),
    /// and the settings fields that were marooned on GameApp (in-memory only).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SettingsComponent : UIComponent
    {
        private const string Section = "settings";
        private const string AudioSection = "audio";
        private const string DisplaySection = "display";
        private const string LocaleSection = "locale";
        private const string GameplaySection = "gameplay";

        [Export] public string SettingsPath { get; set; } = "user://settings.cfg";

        // ── Audio ──
        [ExportGroup("Audio")]
        [Export] public float MasterVolume
        {
            get => _master;
            set { _master = value; Set(AudioSection, "master", value); ApplyAudioSettings(); EmitChanged(); }
        }
        private float _master = 80f;

        [Export] public float SfxVolume
        {
            get => _sfx;
            set { _sfx = value; Set(AudioSection, "sfx", value); ApplyAudioSettings(); EmitChanged(); }
        }
        private float _sfx = 90f;

        [Export] public float MusicVolume
        {
            get => _music;
            set { _music = value; Set(AudioSection, "music", value); ApplyAudioSettings(); EmitChanged(); }
        }
        private float _music = 70f;

        // ── Display ──
        [ExportGroup("Display")]
        [Export] public bool Fullscreen
        {
            get => _fullscreen;
            set { _fullscreen = value; Set(DisplaySection, "fullscreen", value); ApplyDisplaySettings(); EmitChanged(); }
        }
        private bool _fullscreen = false;

        [Export] public int ResolutionIndex
        {
            get => _resIndex;
            // Applies immediately, like Fullscreen above — otherwise the value is stored
            // but the window never changes.
            set { _resIndex = value; Set(DisplaySection, "resolution_index", value); ApplyDisplaySettings(); EmitChanged(); }
        }
        private int _resIndex = 0;

        // ── Locale ──
        [ExportGroup("Locale")]
        [Export] public string Language
        {
            get => _language;
            // Applies immediately, like Fullscreen/ResolutionIndex — otherwise the value is stored
            // but the TranslationServer locale never changes unless something else calls
            // ApplyLocaleSettings (BootComponent/SettingsMenu did; a direct set did nothing).
            set { _language = value; Set(LocaleSection, "language", value); ApplyLocaleSettings(); EmitChanged(); }
        }
        private string _language = "en";

        [Export] public bool SubtitlesEnabled
        {
            get => _subtitles;
            set { _subtitles = value; Set(LocaleSection, "subtitles", value); EmitChanged(); }
        }
        private bool _subtitles = true;

        // ── Gameplay ──
        // settings_menu.tscn has always shown these two toggles, but there was nothing
        // behind them to read or write.
        [ExportGroup("Gameplay")]
        /// <summary>Camera shake on hits/explosions. Read by camera-shake components.</summary>
        [Export] public bool ScreenShakeEnabled
        {
            get => _screenShake;
            set { _screenShake = value; Set(GameplaySection, "screen_shake", value); EmitChanged(); }
        }
        private bool _screenShake = true;

        /// <summary>Floating damage numbers on hits.</summary>
        [Export] public bool DamageNumbersEnabled
        {
            get => _damageNumbers;
            set { _damageNumbers = value; Set(GameplaySection, "damage_numbers", value); EmitChanged(); }
        }
        private bool _damageNumbers = true;

        /// <summary>Fires after any setting changes and is persisted. Note the settings already
        /// self-apply in their setters (audio/display/locale) — this is the external notification
        /// hook for game UI that wants to react to a live change (e.g. re-read a value), not a
        /// requirement for the change to take effect.</summary>
        [Signal] public delegate void SettingsChangedEventHandler();

        private static SettingsComponent? _instance;
        private ConfigFile _config = new();

        /// <summary>The autoloaded instance, or null.</summary>
        public static SettingsComponent? Instance
        {
            get
            {
                if (_instance != null && GodotObject.IsInstanceValid(_instance)) return _instance;
                if (Engine.GetMainLoop() is SceneTree tree
                    && tree.Root.GetNodeOrNull<SettingsComponent>("/root/Settings") is { } s)
                {
                    _instance = s;
                    return s;
                }
                return null;
            }
        }

        public override void _EnterTree()
        {
            if (GetParent() == GetTree()?.Root)
                _instance = this;
        }

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            LoadSettings();
        }

        // ════════════════════════════════════════════════════════════════
        // Load / Save
        // ════════════════════════════════════════════════════════════════

        /// <summary>Load all settings from disk into the fields.</summary>
        public void LoadSettings()
        {
            Error err = _config.Load(SettingsPath);
            if (err != Error.Ok)
            {
                // A missing file is normal (fresh install → defaults). A file that EXISTS but fails
                // to parse is corrupt — say so rather than silently discarding the player's settings.
                if (Godot.FileAccess.FileExists(SettingsPath))
                    GD.PushWarning($"[{Name}] settings file '{SettingsPath}' exists but failed to load ({err}) — falling back to defaults. The file may be corrupt; it will be overwritten on the next save.");
                // Push the (default) field values to the engine so displayed defaults and actual
                // audio/window state match on a fresh install / corrupt config.
                ApplyAudioSettings();
                ApplyDisplaySettings();
                return;
            }

            _master = (float)_config.GetValue(AudioSection, "master", 80f);
            _sfx = (float)_config.GetValue(AudioSection, "sfx", 90f);
            _music = (float)_config.GetValue(AudioSection, "music", 70f);
            _fullscreen = (bool)_config.GetValue(DisplaySection, "fullscreen", false);
            _resIndex = (int)_config.GetValue(DisplaySection, "resolution_index", 0);
            _language = (string)_config.GetValue(LocaleSection, "language", "en");
            _subtitles = (bool)_config.GetValue(LocaleSection, "subtitles", true);
            _screenShake = (bool)_config.GetValue(GameplaySection, "screen_shake", true);
            _damageNumbers = (bool)_config.GetValue(GameplaySection, "damage_numbers", true);

            ApplyAudioSettings();
            ApplyDisplaySettings();
        }

        /// <summary>Persist all settings. Coalesced: the write happens shortly after changes
        /// stop, not on every call.
        ///
        /// Every property setter routes through Set(), which calls this — fine for a
        /// checkbox, wrong for a slider. Dragging a volume slider emits value_changed every
        /// frame, so this was writing user://settings.cfg ~60x/sec for the length of the
        /// drag. Debouncing here fixes it for every caller, rather than making each UI site
        /// remember to be careful.
        ///
        /// Call <see cref="FlushSettings"/> for an immediate write.</summary>
        public void SaveSettings()
        {
            _saveDirty = true;
            _saveTimer = SaveDebounceSeconds;
        }

        /// <summary>Write now, skipping the debounce. Used on exit, where there is no later.</summary>
        public void FlushSettings()
        {
            _saveDirty = false;
            _saveTimer = 0f;
            _config.Save(SettingsPath);
        }

        /// <summary>How long changes must be quiet before the write lands.</summary>
        [Export] public float SaveDebounceSeconds { get; set; } = 0.4f;

        private bool _saveDirty;
        private float _saveTimer;

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !_saveDirty) return;
            _saveTimer -= (float)delta;
            if (_saveTimer <= 0f) FlushSettings();
        }

        /// <summary>Land any pending write before we go — a debounced save must never lose a
        /// change to someone quitting within the debounce window.</summary>
        public override void _ExitTree()
        {
            if (_saveDirty && !Engine.IsEditorHint()) FlushSettings();
            base._ExitTree();
        }

        // ════════════════════════════════════════════════════════════════
        // Apply to engine
        // ════════════════════════════════════════════════════════════════

        /// <summary>Apply audio volumes to AudioServer buses. Editor-guarded for the same
        /// reason as ApplyDisplaySettings: this is a [Tool] script, and the AudioServer it
        /// would mutate in the editor is the editor's own.</summary>
        public void ApplyAudioSettings()
        {
            if (Engine.IsEditorHint()) return;

            SetBusVolume("Master", _master);
            SetBusVolume("SFX", _sfx);
            SetBusVolume("Music", _music);
        }

        /// <summary>Resolutions offered by settings_menu.tscn's ResolutionOption, in the
        /// order its items are declared. ResolutionIndex is an index into this.</summary>
        public static readonly Vector2I[] Resolutions =
        {
            new(1280, 720),
            new(1920, 1080),
            new(2560, 1440)
        };

        /// <summary>Apply fullscreen/windowed mode and the chosen resolution.
        ///
        /// Editor-guarded: this class is [Tool], so without the guard, ticking Fullscreen
        /// in the inspector would fullscreen the EDITOR, and changing the resolution would
        /// resize the editor window. These settings only mean anything in the running game.</summary>
        public void ApplyDisplaySettings()
        {
            if (Engine.IsEditorHint()) return;

            DisplayServer.WindowSetMode(_fullscreen
                ? DisplayServer.WindowMode.ExclusiveFullscreen
                : DisplayServer.WindowMode.Windowed);

            // Apply the resolution too — it used to be stored and reloaded but never
            // applied, so picking one in the settings menu did nothing. Only meaningful
            // windowed; fullscreen takes the screen size.
            if (_fullscreen) return;
            if (_resIndex < 0 || _resIndex >= Resolutions.Length) return;
            DisplayServer.WindowSetSize(Resolutions[_resIndex]);
        }

        /// <summary>Apply the saved language to the LocalizationComponent.</summary>
        public void ApplyLocaleSettings()
        {
            var loc = LocalizationComponent.Instance;
            if (loc != null && !string.IsNullOrEmpty(_language))
                loc.SetLanguage(_language);
        }

        // ════════════════════════════════════════════════════════════════
        // Generic section/key API (for custom settings beyond the typed ones)
        // ════════════════════════════════════════════════════════════════

        public Variant Get(string section, string key, Variant fallback = default)
            => _config.GetValue(section, key, fallback);

        public void Set(string section, string key, Variant value)
        {
            _config.SetValue(section, key, value);
            SaveSettings();
        }

        public bool HasKey(string section, string key) => _config.HasSectionKey(section, key);
        public void EraseSection(string section) => _config.EraseSection(section);

        // ════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════

        private void EmitChanged() => EmitSignal(SignalName.SettingsChanged);

        private static void SetBusVolume(string bus, float value01)
        {
            int i = AudioServer.GetBusIndex(bus);
            if (i < 0) return;
            float db = value01 <= 0 ? -80f : Mathf.LinearToDb(value01 / 100f);
            AudioServer.SetBusVolumeDb(i, db);
        }
    }
}
