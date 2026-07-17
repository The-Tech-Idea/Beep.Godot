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

            GetNode<Button>("Margin/VBox/Header/BackButton").Pressed += () => ChangeScene(GameApp.Instance?.MainMenuPath);

            // The garage is the only route to vehicle select — VehicleSelect's Back returns
            // here, and nothing else pointed at it, so it shipped unreachable. Same
            // convention its own Back button uses.
            if (GetNodeOrNull<Button>("Margin/VBox/VehicleSelectButton") is { } vehicleSelect)
                vehicleSelect.Pressed += () => ChangeScene("res://scenes/ui/racing/vehicle_select.tscn");

            GetNode<Button>("Margin/VBox/RaceButton").Pressed += () => ChangeScene(GameApp.Instance?.GameScenePath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
