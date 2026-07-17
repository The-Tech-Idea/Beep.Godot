using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class SettingsMenu : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Center/Panel/Margin/ContentVBox/CloseButton").Pressed += OnClosePressed;
            WireSettingsWidgets();
        }

        /// <summary>Bind the six controls to the Settings autoload. The scene has always
        /// shown them, but nothing read or wrote them — SettingsComponent is a ConfigFile
        /// store with no UI code, and this script only wired Close. Each control is seeded
        /// from the stored value, then writes back on change; SettingsComponent's own
        /// setters persist and apply.</summary>
        private void WireSettingsWidgets()
        {
            var settings = UI.SettingsComponent.Instance;
            if (settings == null)
            {
                GD.PushWarning($"[{Name}] Settings autoload not found — controls left inert.");
                return;
            }

            const string video = "Center/Panel/Margin/ContentVBox/Tabs/Video/";
            const string language = "Center/Panel/Margin/ContentVBox/Tabs/Language/";
            const string gameplay = "Center/Panel/Margin/ContentVBox/Tabs/Gameplay/";

            Bind(video + "FullscreenCheck", settings.Fullscreen, v => settings.Fullscreen = v);
            Bind(gameplay + "SubtitlesCheck", settings.SubtitlesEnabled, v => settings.SubtitlesEnabled = v);
            Bind(gameplay + "ScreenShakeCheck", settings.ScreenShakeEnabled, v => settings.ScreenShakeEnabled = v);
            Bind(gameplay + "DamageNumbersCheck", settings.DamageNumbersEnabled, v => settings.DamageNumbersEnabled = v);

            if (GetNodeOrNull<OptionButton>(video + "ResolutionOption") is { } resolution)
            {
                if (settings.ResolutionIndex >= 0 && settings.ResolutionIndex < resolution.ItemCount)
                    resolution.Selected = settings.ResolutionIndex;
                resolution.ItemSelected += index =>
                {
                    settings.ResolutionIndex = (int)index;
                    settings.ApplyDisplaySettings();
                    settings.SaveSettings();
                };
            }

            if (GetNodeOrNull<OptionButton>(language + "LanguageOption") is { } locale)
            {
                int i = LocaleCodes.IndexOf(settings.Language);
                if (i >= 0 && i < locale.ItemCount) locale.Selected = i;
                locale.ItemSelected += index =>
                {
                    if (index < 0 || index >= LocaleCodes.Count) return;
                    settings.Language = LocaleCodes[(int)index];
                    settings.ApplyLocaleSettings();
                    settings.SaveSettings();
                };
            }
        }

        /// <summary>Locale code per LanguageOption item, in the order the scene declares
        /// them (English / Español / 日本語) — matches templates/i18n/translations.csv.</summary>
        private static readonly System.Collections.Generic.List<string> LocaleCodes = new() { "en", "es", "ja" };

        private void Bind(string path, bool current, System.Action<bool> apply)
        {
            if (GetNodeOrNull<CheckButton>(path) is not { } check) return;
            check.ButtonPressed = current;
            check.Toggled += value =>
            {
                apply(value);
                UI.SettingsComponent.Instance?.SaveSettings();
            };
        }

        /// <summary>Close correctly whether we are the current scene or an overlay.
        ///
        /// Opened from the main menu we ARE the scene, so we navigate back to it. Opened
        /// from the pause menu or the level map we are an overlay instanced over a live
        /// scene — navigating would destroy whatever is underneath (that was the bug:
        /// pause → settings → close threw away the running game). Freeing ourselves simply
        /// reveals it again, still paused.</summary>
        private void OnClosePressed()
        {
            if (GetTree()?.CurrentScene != this)
            {
                QueueFree();
                return;
            }
            ChangeScene(GameApp.Instance?.MainMenuPath);
        }

        /// <summary>Navigate to a scene. Reports why it failed instead of doing nothing —
        /// a missing/unset target used to make the button appear dead.</summary>
        private void ChangeScene(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                GD.PushError($"[{Name}] Navigation target is not set (check GameInfo scene paths).");
                return;
            }
            if (!ResourceLoader.Exists(path))
            {
                GD.PushError($"[{Name}] Navigation target does not exist: {path}");
                return;
            }
            Error err = GetTree().ChangeSceneToFile(path);
            if (err != Error.Ok)
                GD.PushError($"[{Name}] Failed to change scene to {path}: {err}");
        }
    }
}
