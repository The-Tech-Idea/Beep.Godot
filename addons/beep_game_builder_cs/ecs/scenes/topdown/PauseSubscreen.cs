using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class PauseSubscreen : CanvasLayer
    {
        public override void _Ready()
        {
            var tabs = GetNode<TabContainer>("Margin/MainHBox/ContentArea/Tabs");

            GetNode<Button>("Margin/MainHBox/TabRail/InventoryButton").Pressed += () => { tabs.CurrentTab = 0; };
            GetNode<Button>("Margin/MainHBox/TabRail/MapButton").Pressed += () => { tabs.CurrentTab = 1; };
            GetNode<Button>("Margin/MainHBox/TabRail/QuestButton").Pressed += () => { tabs.CurrentTab = 2; };
            GetNode<Button>("Margin/MainHBox/TabRail/StatusButton").Pressed += () => { tabs.CurrentTab = 3; };
            GetNode<Button>("Margin/MainHBox/TabRail/SaveButton").Pressed += () => GD.Print("TODO: Save game not yet implemented");
            GetNode<Button>("Margin/MainHBox/TabRail/ResumeButton").Pressed += () => { GetTree().Paused = false; QueueFree(); };
            GetNode<Button>("Margin/MainHBox/TabRail/QuitButton").Pressed += () => { GetTree().Paused = false; ChangeScene(GameApp.Instance?.MainMenuPath); };
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
