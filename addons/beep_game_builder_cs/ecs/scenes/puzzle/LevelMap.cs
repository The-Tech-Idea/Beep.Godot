using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class LevelMap : Control
    {
        public override void _Ready()
        {
            GetNode<Button>("TopBar/SettingsButton").Pressed += () => ChangeScene(GameApp.Instance?.SettingsScenePath);
            GetNode<Button>("Scroll/Path/Level1Row/Level1Button").Pressed += () => ChangeScene(GameInfo.Instance?.PreLevelPath ?? "res://scenes/ui/puzzle/pre_level.tscn");
            GetNode<Button>("Scroll/Path/Level2Row/Level2Button").Pressed += () => ChangeScene(GameInfo.Instance?.PreLevelPath ?? "res://scenes/ui/puzzle/pre_level.tscn");
            GetNode<Button>("Scroll/Path/Level3Row/Level3Button").Pressed += () => ChangeScene(GameInfo.Instance?.PreLevelPath ?? "res://scenes/ui/puzzle/pre_level.tscn");
            GetNode<Button>("Scroll/Path/Level4Row/Level4Button").Pressed += () => ChangeScene(GameInfo.Instance?.PreLevelPath ?? "res://scenes/ui/puzzle/pre_level.tscn");
            GetNode<Button>("Scroll/Path/Level5Row/Level5Button").Pressed += () => ChangeScene(GameInfo.Instance?.PreLevelPath ?? "res://scenes/ui/puzzle/pre_level.tscn");
            GetNode<Button>("Scroll/Path/Level6Row/Level6Button").Pressed += () => ChangeScene(GameInfo.Instance?.PreLevelPath ?? "res://scenes/ui/puzzle/pre_level.tscn");
        }

        private void ChangeScene(string? path) { if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path)) GetTree().ChangeSceneToFile(path); }
    }
}
