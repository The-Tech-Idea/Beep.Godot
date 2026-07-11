using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Save/load manager. Uses Godot's ConfigFile to persist key-value data to user://.
    /// Attach as an autoload or in the boot scene. Save() writes to disk; Load() reads.
    /// SaveSceneState/LoadSceneState capture per-scene data (e.g. collected pickups)
    /// under a section named after the scene file.
    /// Replaces the old GDScript save_manager.gd.template.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class SaveManagerComponent : GameplayComponent
    {
        [Export] public string SavePath { get; set; } = "user://savegame.cfg";

        private ConfigFile _config = new();
        private bool _loaded;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            Load();
        }

        /// <summary>Load the save file. Returns true if it existed.</summary>
        public bool Load()
        {
            Error err = _config.Load(SavePath);
            _loaded = err == Error.Ok;
            return _loaded;
        }

        /// <summary>Write all data to disk.</summary>
        public void Save()
        {
            _config.Save(SavePath);
        }

        // ── Global key-value ──
        public void SetValue(string key, Variant value) => _config.SetValue("global", key, value);
        public Variant GetValue(string key, Variant fallback = default) => _config.GetValue("global", key, fallback);
        public bool HasKey(string key) => _config.HasSectionKey("global", key);

        // ── Per-scene state (snapshot/restore) ──
        /// <summary>Save a key-value pair under the current scene's section.</summary>
        public void SaveSceneState(string key, Variant value)
        {
            string section = GetSceneSection();
            _config.SetValue(section, key, value);
        }

        /// <summary>Read a key from the current scene's section.</summary>
        public Variant LoadSceneState(string key, Variant fallback = default)
        {
            string section = GetSceneSection();
            return _config.GetValue(section, key, fallback);
        }

        /// <summary>Clear all saved data for the current scene.</summary>
        public void ClearSceneState()
        {
            string section = GetSceneSection();
            if (_config.HasSection(section))
                _config.EraseSection(section);
        }

        /// <summary>Clear all saved data (full reset).</summary>
        public void ClearAll()
        {
            _config.Clear();
            _config.Save(SavePath);
        }

        private string GetSceneSection()
        {
            var scene = GetTree()?.CurrentScene;
            if (scene == null) return "unknown_scene";
            return scene.SceneFilePath.GetFile().GetBaseName();
        }
    }
}
