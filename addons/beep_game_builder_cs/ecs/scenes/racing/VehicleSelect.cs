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

            // Back returns to the garage, which racing wires as NewGameScenePath. Resolve it
            // through GameInfo instead of a hardcoded literal (fallback keeps it working
            // pre-generation).
            this.ConnectPressed("Margin/VBox/Header/BackButton", () => ChangeScene(GaragePath()));
            this.ConnectPressed("Margin/VBox/VehicleGrid/Car1/Car1VBox/Car1Button", () => SelectVehicle("Car1"));
            this.ConnectPressed("Margin/VBox/VehicleGrid/Car2/Car2VBox/Car2Button", () => SelectVehicle("Car2"));
            this.ConnectPressed("Margin/VBox/VehicleGrid/Car3/Car3VBox/Car3Button", () => SelectVehicle("Car3"));
        }

        /// <summary>Record the picked vehicle on GameApp, then start the race. Before, all three
        /// cards loaded the same scene and the choice was silently discarded.</summary>
        private void SelectVehicle(string vehicle)
        {
            if (GameApp.Instance is { } app) app.SelectedVehicle = vehicle;
            ChangeScene(GameApp.Instance?.GameScenePath);
        }

        private static string GaragePath()
        {
            string p = Beep.GameBuilder.GameInfo.Instance?.NewGameScenePath ?? "";
            return string.IsNullOrEmpty(p) ? "res://scenes/ui/racing/garage.tscn" : p;
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
