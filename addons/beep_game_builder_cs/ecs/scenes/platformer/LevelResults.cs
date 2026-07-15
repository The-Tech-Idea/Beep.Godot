using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelResults : CanvasLayer
    {
        public override void _Ready()
        {
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/NextButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/RetryButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/MapButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelSelectPath ?? "res://scenes/ui/platformer/level_select.tscn");
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
