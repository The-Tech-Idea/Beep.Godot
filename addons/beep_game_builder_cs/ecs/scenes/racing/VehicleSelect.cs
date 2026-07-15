using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class VehicleSelect : Control
    {
        public override void _Ready()
        {
            GetNode<Button>("Margin/VBox/Header/BackButton").Pressed += () => ChangeScene("res://scenes/ui/racing/garage.tscn");
            GetNode<Button>("Margin/VBox/VehicleGrid/Car1/Car1VBox/Car1Button").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Margin/VBox/VehicleGrid/Car2/Car2VBox/Car2Button").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Margin/VBox/VehicleGrid/Car3/Car3VBox/Car3Button").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
