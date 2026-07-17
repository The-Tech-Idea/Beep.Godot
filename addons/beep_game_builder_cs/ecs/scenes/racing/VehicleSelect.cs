using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class VehicleSelect : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            GetNode<Button>("Margin/VBox/Header/BackButton").Pressed += () => ChangeScene("res://scenes/ui/racing/garage.tscn");
            GetNode<Button>("Margin/VBox/VehicleGrid/Car1/Car1VBox/Car1Button").Pressed += () => SelectVehicle("Car1");
            GetNode<Button>("Margin/VBox/VehicleGrid/Car2/Car2VBox/Car2Button").Pressed += () => SelectVehicle("Car2");
            GetNode<Button>("Margin/VBox/VehicleGrid/Car3/Car3VBox/Car3Button").Pressed += () => SelectVehicle("Car3");
        }

        /// <summary>Record the picked vehicle on GameApp, then start the race. Before, all three
        /// cards loaded the same scene and the choice was silently discarded.</summary>
        private void SelectVehicle(string vehicle)
        {
            if (GameApp.Instance is { } app) app.SelectedVehicle = vehicle;
            ChangeScene(GameApp.Instance?.GameScenePath);
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
