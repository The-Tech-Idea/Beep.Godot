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

            var tabs = GetNode<TabContainer>("Margin/MainHBox/ContentArea/Tabs");

            GetNode<Button>("Margin/MainHBox/TabRail/InventoryButton").Pressed += () => { tabs.CurrentTab = 0; };
            GetNode<Button>("Margin/MainHBox/TabRail/MapButton").Pressed += () => { tabs.CurrentTab = 1; };
            GetNode<Button>("Margin/MainHBox/TabRail/QuestButton").Pressed += () => { tabs.CurrentTab = 2; };
            GetNode<Button>("Margin/MainHBox/TabRail/StatusButton").Pressed += () => { tabs.CurrentTab = 3; };
            GetNode<Button>("Margin/MainHBox/TabRail/SaveButton").Pressed += OnSavePressed;
            GetNode<Button>("Margin/MainHBox/TabRail/ResumeButton").Pressed += () => { GetTree().Paused = false; QueueFree(); };
            GetNode<Button>("Margin/MainHBox/TabRail/QuitButton").Pressed += () => { GetTree().Paused = false; ChangeScene(GameApp.Instance?.MainMenuPath); };
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
