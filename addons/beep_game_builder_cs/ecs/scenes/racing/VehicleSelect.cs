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

            // The one navigation in ecs/scenes/ that doesn't route through GameApp/GameInfo:
            // there is no GameInfo property for the garage (the Genre Scenes group covers only
            // platformer/shooter/puzzle). This literal matches the generator's
            // res://scenes/ui/{genre.Id}/{scene} convention, so it is correct but invisible to
            // nav_wiring — relocating garage.tscn breaks it silently.
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

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
