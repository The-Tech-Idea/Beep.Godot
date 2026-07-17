using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelUpChoice : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Center/VBox/CardRow/Card1/Card1VBox/Pick1").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/VBox/CardRow/Card2/Card2VBox/Pick2").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
            GetNode<Button>("Center/VBox/CardRow/Card3/Card3VBox/Pick3").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
        }

        /// <summary>Navigate to a scene. Reports why it failed instead of doing nothing —
        /// a missing/unset target used to make the button appear dead.</summary>
        private void ChangeScene(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                GD.PushError($"[{Name}] Navigation target is not set (check GameInfo scene paths).");
                return;
            }
            if (!ResourceLoader.Exists(path))
            {
                GD.PushError($"[{Name}] Navigation target does not exist: {path}");
                return;
            }
            Error err = GetTree().ChangeSceneToFile(path);
            if (err != Error.Ok)
                GD.PushError($"[{Name}] Failed to change scene to {path}: {err}");
        }
    }
}
