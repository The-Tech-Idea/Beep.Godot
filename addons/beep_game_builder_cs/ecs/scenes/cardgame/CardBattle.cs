using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class CardBattle : CanvasLayer
    {
        public override void _Ready()
        {
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/EndTurnButton").Pressed += () => GD.Print("TODO: End turn not yet implemented");
            GetNode<Button>("Center/Panel/Margin/VBox/ButtonRow/ForfeitButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
