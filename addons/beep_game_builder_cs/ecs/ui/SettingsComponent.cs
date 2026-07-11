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
            set { _resIndex = value; Set(DisplaySection, "resolution_index", value); EmitChanged(); }
        }
        private int _resIndex = 0;

        // ── Locale ──
        [ExportGroup("Locale")]
        [Export] public string Language
        {
            get => _language;
            set { _language = value; Set(LocaleSection, "language", value); EmitChanged(); }
        }
        private string _language = "en";

        [Export] public bool SubtitlesEnabled
        {
            get => _subtitles;
            set { _subtitles = value; Set(LocaleSection, "subtitles", value); EmitChanged(); }
        }
        private bool _subtitles = true;

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
            if (err != Error.Ok) return; // no file yet = use defaults

            _master = (float)_config.GetValue(AudioSection, "master", 80f);
            _sfx = (float)_config.GetValue(AudioSection, "sfx", 90f);
            _music = (float)_config.GetValue(AudioSection, "music", 70f);
            _fullscreen = (bool)_config.GetValue(DisplaySection, "fullscreen", false);
            _resIndex = (int)_config.GetValue(DisplaySection, "resolution_index", 0);
            _language = (string)_config.GetValue(LocaleSection, "language", "en");
            _subtitles = (bool)_config.GetValue(LocaleSection, "subtitles", true);

            ApplyAudioSettings();
            ApplyDisplaySettings();
        }

        /// <summary>Write all settings to disk.</summary>
        public void SaveSettings() => _config.Save(SettingsPath);

        // ════════════════════════════════════════════════════════════════
        // Apply to engine
        // ════════════════════════════════════════════════════════════════

        /// <summary>Apply audio volumes to AudioServer buses.</summary>
        public void ApplyAudioSettings()
        {
            SetBusVolume("Master", _master);
            SetBusVolume("SFX", _sfx);
            SetBusVolume("Music", _music);
        }

        /// <summary>Apply fullscreen/windowed mode.</summary>
        public void ApplyDisplaySettings()
        {
            DisplayServer.WindowSetMode(_fullscreen
                ? DisplayServer.WindowMode.ExclusiveFullscreen
                : DisplayServer.WindowMode.Windowed);
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
