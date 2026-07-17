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
            const string audio = "Center/Panel/Margin/ContentVBox/Tabs/Audio/";

            // The three audio sliders shipped bound to nothing: the scene drew them,
            // SettingsComponent stored and applied the volumes, and no code joined the two.
            // Dragging them did nothing, and their positions were scene literals that never
            // reflected the stored value.
            Bind(audio + "MasterSlider", settings.MasterVolume, v => settings.MasterVolume = v);
            Bind(audio + "SfxSlider", settings.SfxVolume, v => settings.SfxVolume = v);
            Bind(audio + "MusicSlider", settings.MusicVolume, v => settings.MusicVolume = v);

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

        /// <summary>Bind a slider to a stored value. Applies on every change, so the volume
        /// moves under the player's finger.
        ///
        /// No explicit save here, deliberately: every SettingsComponent setter persists via
        /// Set(), and that write is debounced at the source. An earlier version of this saved
        /// on DragEnded to avoid per-frame writes — which achieved nothing, since the apply
        /// callback was already writing to disk on every change, and DragEnded only fires for
        /// mouse drags (keyboard and wheel adjustments would have skipped it entirely).</summary>
        private void Bind(string path, float current, System.Action<float> apply)
        {
            if (GetNodeOrNull<Godot.Range>(path) is not { } slider) return;

            // Seed before subscribing: assigning Value emits ValueChanged synchronously,
            // which would otherwise write the scene's literal back over the stored setting.
            slider.Value = current;
            slider.ValueChanged += value => apply((float)value);
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

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
