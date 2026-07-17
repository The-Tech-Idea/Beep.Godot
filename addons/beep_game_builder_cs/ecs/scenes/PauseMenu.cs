using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class PauseMenu : CanvasLayer
    {
        private UI.SaveLoadManagerComponent? _saveLoadManager;

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // GetNodeOrNull to match the tolerance already expressed below, where the Save and
            // Load buttons are fetched with GetNodeOrNull: stripping save/load from a pause
            // menu is a natural edit, and throwing here killed Resume/Restart/Quit with it.
            _saveLoadManager = GetNodeOrNull<UI.SaveLoadManagerComponent>("SaveLoadManager");

            GetNode<Button>("Panel/PauseVBox/ResumeButton").Pressed   += () => { GetTree().Paused = false; QueueFree(); };
            // Open settings OVER the paused game. This used to unpause and ChangeScene,
            // which freed the running game — and since Settings' Close goes to the main
            // menu, pause → settings → close silently destroyed the session. Stay paused.
            GetNode<Button>("Panel/PauseVBox/SettingsButton").Pressed += () => UI.SettingsOverlay.Open(this);
            GetNode<Button>("Panel/PauseVBox/RestartButton").Pressed  += () => { GetTree().Paused = false; GetTree().ReloadCurrentScene(); };
            GetNode<Button>("Panel/PauseVBox/MainMenuButton").Pressed += () => { GetTree().Paused = false; ChangeScene(GameApp.Instance?.MainMenuPath); };
            GetNode<Button>("Panel/PauseVBox/QuitButton").Pressed     += () => GetTree().Quit();

            if (GetNodeOrNull<Button>("Panel/PauseVBox/SaveGameButton") is Button saveBtn)
                saveBtn.Pressed += OnSaveGamePressed;
            if (GetNodeOrNull<Button>("Panel/PauseVBox/LoadGameButton") is Button loadBtn)
                loadBtn.Pressed += OnLoadGamePressed;
        }

        private void OnSaveGamePressed()
        {
            _saveLoadManager?.ShowSaveMenu();
        }

        private void OnLoadGamePressed()
        {
            _saveLoadManager?.ShowLoadMenu();
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
