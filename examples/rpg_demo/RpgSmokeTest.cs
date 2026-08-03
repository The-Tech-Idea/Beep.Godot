using Godot;

namespace Beep.Examples;

public partial class RpgSmokeTest : Node
{
    private int _frames;

    public override void _Ready()
    {
        var scene = GD.Load<PackedScene>("res://examples/rpg_demo/rpg_demo.tscn");
        if (scene == null)
        {
            GD.PushError("[RpgSmokeTest] Failed to load RPG demo scene.");
            GetTree().Quit(1);
            return;
        }

        AddChild(scene.Instantiate());
    }

    public override void _Process(double delta)
    {
        _frames++;
        if (_frames < 90) return;

        var player = GetNodeOrNull<CharacterBody2D>("RpgDemo/RPGMain/Player");
        var controls = GetNodeOrNull<CanvasLayer>("RpgDemo/RPGMain/RpgWeatherControls");
        var village = GetNodeOrNull<Node2D>("RpgDemo/RPGMain/LevelContainer/DemoVillage");
        if (player == null || controls == null || village == null)
        {
            GD.PushError("[RpgSmokeTest] RPG demo did not initialize expected nodes.");
            GetTree().Quit(1);
            return;
        }

        GetTree().Quit(0);
    }
}
