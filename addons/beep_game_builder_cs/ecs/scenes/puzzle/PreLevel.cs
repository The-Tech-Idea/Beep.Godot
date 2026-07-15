using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class PreLevel : CanvasLayer
    {
        public override void _Ready()
        {
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/BackButton").Pressed += () => ChangeScene(GameInfo.Instance?.LevelMapPath ?? "res://scenes/ui/puzzle/level_map.tscn");
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/PlayButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/Panel/Margin/VBox/BoosterRow/Booster1").Pressed += () => GD.Print("TODO: Booster1 not yet implemented");
            GetNode<Button>("Center/Panel/Margin/VBox/BoosterRow/Booster2").Pressed += () => GD.Print("TODO: Booster2 not yet implemented");
            GetNode<Button>("Center/Panel/Margin/VBox/BoosterRow/Booster3").Pressed += () => GD.Print("TODO: Booster3 not yet implemented");
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
