using Godot;
using Beep.GameBuilder;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelMap : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // Settings opens as an overlay — navigating to it used to destroy this scene,
            // and Settings' Close always went to the main menu, so there was no way back
            // to the map.
            this.ConnectButton("SettingsButton", () => UI.SettingsOverlay.Open(this));
            this.ConnectButton("BackButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));
            this.ConnectButton("Level1Button", () => GoToLevel(1));
            this.ConnectButton("Level2Button", () => GoToLevel(2));
            this.ConnectButton("Level3Button", () => GoToLevel(3));
            this.ConnectButton("Level4Button", () => GoToLevel(4));
            this.ConnectButton("Level5Button", () => GoToLevel(5));
            this.ConnectButton("Level6Button", () => GoToLevel(6));
        }

        /// <summary>Record the chosen level, then go to the pre-level screen. The puzzle board
        /// reads GameApp.CurrentLevel to scale difficulty (grid size / target score) — before,
        /// every level button opened the same board.</summary>
        private void GoToLevel(int level)
        {
            GameApp.Instance?.SetLevel(level);
            ChangeScene(GameInfo.Instance?.PreLevelPath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
