using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Boot/initialization component. Place in the project's main (boot) scene.
    /// On _Ready it:
    /// 1. Ensures GameApp autoload is initialized and has loaded game_info.tres.
    /// 2. Applies saved audio/display settings from GameApp.
    /// 3. Waits <see cref="MinBootTime"/> seconds (so a splash/logo is visible).
    /// 4. Transitions to the main menu via NavigationComponent (or directly to game).
    ///
    /// This is the single entry point for app lifecycle — the boot scene owns it,
    /// and it hands off to the menu/game scenes. No GDScript boot scripts needed.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BootComponent : GameplayComponent
    {
        /// <summary>Minimum time (seconds) to show the boot/splash screen before transitioning.</summary>
        [Export] public double MinBootTime { get; set; } = 1.5;

        /// <summary>If true, transition to the main menu after boot. If false, go straight to the game scene.</summary>
        [Export] public bool GoToMenuAfterBoot { get; set; } = true;

        /// <summary>Emitted when boot initialization is complete (before the transition).</summary>
        [Signal] public delegate void BootCompletedEventHandler();

        private double _elapsed;

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            CallDeferred(nameof(InitializeApp));
        }

        private void InitializeApp()
        {
            // Load and apply user settings (audio/display/locale) from SettingsComponent.
            var settings = UI.SettingsComponent.Instance;
            if (settings != null)
            {
                settings.LoadSettings();  // reads user://settings.cfg
                settings.ApplyAudioSettings();
                settings.ApplyDisplaySettings();
                settings.ApplyLocaleSettings();
            }
        }

        public override void _Process(double delta)
        {
            if (!IsActive || Engine.IsEditorHint()) return;
            _elapsed += delta;
            if (_elapsed >= MinBootTime)
            {
                SetProcess(false);
                EmitSignal(SignalName.BootCompleted);
                TransitionOut();
            }
        }

        private void TransitionOut()
        {
            var nav = GetSiblingComponent<NavigationComponent>();
            if (nav == null)
            {
                // No NavigationComponent sibling — change scene directly.
                var info = GameBuilder.GameInfo.Instance;
                string target = GoToMenuAfterBoot
                    ? (info?.MainMenuPath ?? "res://scenes/ui/main_menu.tscn")
                    : (info?.GameScenePath ?? "res://scenes/main/main.tscn");
                GetTree().ChangeSceneToFile(target);
                return;
            }
            if (GoToMenuAfterBoot) nav.GoToMainMenu();
            else nav.GoToGame();
        }
    }
}
