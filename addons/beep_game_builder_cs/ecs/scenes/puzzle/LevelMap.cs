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
            GetNode<Button>("TopBar/SettingsButton").Pressed += () => UI.SettingsOverlay.Open(this);
            GetNode<Button>("TopBar/BackButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
            GetNode<Button>("Scroll/Path/Level1Row/Level1Button").Pressed += () => GoToLevel(1);
            GetNode<Button>("Scroll/Path/Level2Row/Level2Button").Pressed += () => GoToLevel(2);
            GetNode<Button>("Scroll/Path/Level3Row/Level3Button").Pressed += () => GoToLevel(3);
            GetNode<Button>("Scroll/Path/Level4Row/Level4Button").Pressed += () => GoToLevel(4);
            GetNode<Button>("Scroll/Path/Level5Row/Level5Button").Pressed += () => GoToLevel(5);
            GetNode<Button>("Scroll/Path/Level6Row/Level6Button").Pressed += () => GoToLevel(6);
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
