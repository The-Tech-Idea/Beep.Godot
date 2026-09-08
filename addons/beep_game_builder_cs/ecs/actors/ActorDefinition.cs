using Godot;

namespace Beep.ECS;

public enum ActorSimulationPolicy { Continuous, AmbientDormancy }

[System.Flags]
public enum ActorCapabilities { None = 0, Move = 1, Attack = 2, Interact = 4, Work = 8, Haul = 16 }

/// <summary>Authored actor type. Ownership and runtime progress belong to instances.</summary>
[GlobalClass]
public partial class ActorDefinition : Resource
{
    [Export] public ActorSimulationPolicy SimulationPolicy { get; set; } = ActorSimulationPolicy.Continuous;
    [Export] public string Id { get; set; } = "actor";
    [Export] public PackedScene? Scene { get; set; }
    [Export(PropertyHint.Flags, "Move,Attack,Interact,Work,Haul")]
    public ActorCapabilities Capabilities { get; set; } = ActorCapabilities.Move;
    [Export] public Vector2 Footprint { get; set; } = new(20, 16);
    [Export] public Vector2 VisualSize { get; set; } = new(32, 48);
    [Export] public Vector2 FeetAnchor { get; set; } = new(0.5f, 1f);
}
