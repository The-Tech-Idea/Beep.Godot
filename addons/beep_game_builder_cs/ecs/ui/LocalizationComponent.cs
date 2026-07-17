using System.Collections.Generic;
using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Localization manager using Godot's native <see cref="TranslationServer"/>.
    /// Loads Godot-format CSV translation files (keys,en,es,ja,...), builds per-language
    /// <see cref="Translation"/> resources, and registers them with TranslationServer.
    /// When the locale is set, Godot auto-translates Label/Button/RichTextLabel text
    /// that matches a translation key — no manual Tr() calls needed on UI nodes.
    ///
    /// Place as an autoload ("Locale") or in the boot scene. Other components access
    /// it via <see cref="Instance"/> or <see cref="SettingsComponent.ApplyLocaleSettings"/>.
    ///
    /// CSV format (Godot standard):
    ///   keys,en,es,ja
    ///   MENU_PLAY,Play,Jugar,プレイ
    ///   MENU_SETTINGS,Settings,Ajustes,設定
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class LocalizationComponent : UIComponent
    {
        /// <summary>res:// path(s) to CSV translation files. Multiple files are supported.
        /// Shows a file picker in the inspector filtered to .csv files.
        ///
        /// Defaults to the CSV the generator stamps out (res://i18n/translations.csv).
        /// This used to default to an empty array, and nothing ever assigned it — Locale is
        /// registered as a bare autoload script path, so its exports keep their defaults.
        /// LoadAll() therefore iterated an empty list and no translation was ever loaded,
        /// even though the CSV was sitting right there.</summary>
        [Export(PropertyHint.File, "*.csv,")]
        public string[] TranslationPaths { get; set; } = { "res://i18n/translations.csv" };

        /// <summary>The current locale code (e.g. "en", "es", "ja").</summary>
        [Export] public string CurrentLocale { get; set; } = "en";

        [Signal] public delegate void LanguageChangedEventHandler(string locale);

        private readonly HashSet<string> _loadedLocales = new();
        private readonly List<string> _csvPaths = new();

        private static LocalizationComponent? _instance;

        /// <summary>The autoloaded LocalizationComponent, or null.</summary>
        public static LocalizationComponent? Instance
        {
            get
            {
                if (_instance != null && GodotObject.IsInstanceValid(_instance)) return _instance;
                if (Engine.GetMainLoop() is SceneTree tree
                    && tree.Root.GetNodeOrNull<LocalizationComponent>("/root/Locale") is { } lc)
                {
                    _instance = lc;
                    return lc;
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
            LoadAll();
            // Apply saved locale from settings if available.
            var settings = SettingsComponent.Instance;
            if (settings != null && !string.IsNullOrEmpty(settings.Language))
                SetLanguage(settings.Language);
            else
                SetLanguage(CurrentLocale);
        }

        // ════════════════════════════════════════════════════════════════
        // CSV loading — the core fix
        // ════════════════════════════════════════════════════════════════

        /// <summary>Load all CSV files specified in TranslationPaths.</summary>
        public void LoadAll()
        {
            foreach (var path in TranslationPaths)
                LoadCsv(path);
        }

        /// <summary>Load a single Godot-format CSV translation file. Builds a Translation
        /// resource per language column and registers it with TranslationServer.</summary>
        public void LoadCsv(string path)
        {
            if (string.IsNullOrEmpty(path) || !FileAccess.FileExists(path))
            {
                GD.PushWarning($"[Localization] CSV not found: {path}");
                return;
            }

            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (f == null)
            {
                GD.PushWarning($"[Localization] Could not open CSV: {path}");
                return;
            }

            // Header row: keys,en,es,ja,...
            string[] header = f.GetCsvLine();
            if (header.Length < 2)
            {
                GD.PushWarning($"[Localization] CSV header invalid (need at least keys + 1 language): {path}");
                return;
            }

            // Create a Translation per language column (column 1..N).
            int langCount = header.Length - 1;
            var translations = new Translation[langCount];
            for (int i = 0; i < langCount; i++)
            {
                string locale = header[i + 1].Trim();
                translations[i] = new Translation { Locale = locale };
                _loadedLocales.Add(locale);
            }

            // Read each data row.
            while (!f.EofReached())
            {
                string[] row = f.GetCsvLine();
                if (row.Length == 0) continue;
                if (row[0].Length == 0) continue;

                string key = row[0];
                for (int i = 0; i < langCount; i++)
                {
                    if (i + 1 < row.Length && row[i + 1].Length > 0)
                        translations[i].AddMessage(key, row[i + 1]);
                }
            }

            // Register all translations with the server.
            foreach (var t in translations)
                TranslationServer.AddTranslation(t);

            if (!_csvPaths.Contains(path))
                _csvPaths.Add(path);

            GD.Print($"[Localization] Loaded {langCount} language(s) from {path}");
        }

        /// <summary>Clear all loaded translations and reload from scratch.</summary>
        public void ReloadAll()
        {
            var paths = new List<string>(_csvPaths);
            _csvPaths.Clear();
            _loadedLocales.Clear();
            foreach (var p in paths)
                LoadCsv(p);
            SetLanguage(CurrentLocale);
        }

        // ════════════════════════════════════════════════════════════════
        // Language switching
        // ════════════════════════════════════════════════════════════════

        /// <summary>Switch the active locale. Godot auto-translates all UI nodes whose
        /// text matches a translation key.</summary>
        public void SetLanguage(string locale)
        {
            if (string.IsNullOrEmpty(locale)) return;
            CurrentLocale = locale;
            TranslationServer.SetLocale(locale);
            EmitSignal(SignalName.LanguageChanged, locale);
        }

        /// <summary>All locales that have translations loaded.</summary>
        public string[] AvailableLocales()
        {
            var arr = new string[_loadedLocales.Count];
            _loadedLocales.CopyTo(arr);
            System.Array.Sort(arr);
            return arr;
        }

        /// <summary>Translate a key via TranslationServer (the Godot-native path).
        /// Returns the key unchanged if no translation is found.</summary>
        public string Tr(string key) => TranslationServer.Translate(key);

        /// <summary>Translate with format arguments: TrF("SCORE", score) → "Score: 100".</summary>
        public string TrF(string key, params object[] args)
        {
            string raw = TranslationServer.Translate(key);
            try { return string.Format(raw, args); }
            catch { return raw; }
        }
    }
}
