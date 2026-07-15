using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelUpChoice : CanvasLayer
    {
        public override void _Ready()
        {
            GetNode<Button>("Center/VBox/CardRow/Card1/Card1VBox/Pick1").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/VBox/CardRow/Card2/Card2VBox/Pick2").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/VBox/CardRow/Card3/Card3VBox/Pick3").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
