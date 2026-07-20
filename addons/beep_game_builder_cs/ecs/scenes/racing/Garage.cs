using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class Garage : Control
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectPressed("Margin/VBox/Header/BackButton", () => ChangeScene(GameApp.Instance?.MainMenuPath));

            // The garage is the only route to vehicle select. Resolve it through the nav
            // registry ("vehicle_select" key) instead of a hardcoded literal, so relocating the
            // file no longer breaks it silently.
            if (GetNodeOrNull<Button>("Margin/VBox/VehicleSelectButton") is { } vehicleSelect)
                vehicleSelect.Pressed += () => ChangeScene(VehicleSelectPath());

            this.ConnectPressed("Margin/VBox/RaceButton", () => ChangeScene(GameApp.Instance?.GameScenePath));
        }

        private static string VehicleSelectPath()
        {
            string p = Beep.GameBuilder.GameInfo.Instance?.GetGenreScenePath("vehicle_select") ?? "";
            return string.IsNullOrEmpty(p) ? "res://scenes/ui/racing/vehicle_select.tscn" : p;
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
