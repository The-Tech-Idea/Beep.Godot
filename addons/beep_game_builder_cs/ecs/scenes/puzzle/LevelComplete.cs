using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelComplete : CanvasLayer
    {
        public override void _Ready()
        {
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/MapButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelMapPath ?? "res://scenes/ui/puzzle/level_map.tscn");
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/RetryButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/NextButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
