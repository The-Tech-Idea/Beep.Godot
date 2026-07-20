using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class PauseSubscreen : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            // GetNodeOrNull (not throwing) so a missing Tabs node warns and disables the tab buttons
            // rather than aborting _Ready and killing Save/Resume/Quit too. The tab lambdas null-guard it.
            var tabs = GetNodeOrNull<TabContainer>("Margin/MainHBox/ContentArea/Tabs");
            if (tabs == null)
                GD.PushWarning($"[{Name}] Tabs TabContainer not found — tab buttons will do nothing.");

            this.ConnectPressed("Margin/MainHBox/TabRail/InventoryButton", () => { if (tabs != null) tabs.CurrentTab = 0; });
            this.ConnectPressed("Margin/MainHBox/TabRail/MapButton", () => { if (tabs != null) tabs.CurrentTab = 1; });
            this.ConnectPressed("Margin/MainHBox/TabRail/QuestButton", () => { if (tabs != null) tabs.CurrentTab = 2; });
            this.ConnectPressed("Margin/MainHBox/TabRail/StatusButton", () => { if (tabs != null) tabs.CurrentTab = 3; });
            this.ConnectPressed("Margin/MainHBox/TabRail/SaveButton", OnSavePressed);
            // The Save tab's own button was never wired (only the TabRail SaveButton was), so it
            // was a dead button. Wire it to the same save action.
            if (GetNodeOrNull<Button>("Margin/MainHBox/ContentArea/Tabs/Save/SaveButton") is { } saveTabButton)
                saveTabButton.Pressed += OnSavePressed;
            this.ConnectPressed("Margin/MainHBox/TabRail/ResumeButton", () => { GetTree().Paused = false; QueueFree(); });
            this.ConnectPressed("Margin/MainHBox/TabRail/QuitButton", () => { GetTree().Paused = false; ChangeScene(GameApp.Instance?.MainMenuPath); });
        }

        /// <summary>Write the autosave slot through the GameStateManager autoload. Was a
        /// GD.Print TODO; a real save system exists, so this now actually saves.</summary>
        private void OnSavePressed()
        {
            var manager = GameStateManagerComponent.Instance;
            if (manager == null)
            {
                GD.PushError($"[{Name}] Save pressed but no GameStateManager autoload is registered.");
                return;
            }
            manager.SyncAllSaveables();
            if (manager.SaveAutosave())
                GD.Print("[PauseSubscreen] Game saved (autosave slot).");
            else
                GD.PushError("[PauseSubscreen] Autosave failed.");
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
