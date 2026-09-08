using Godot;

namespace Beep.ECS;

public enum ActorAction { Move, Attack, Stop, Work, Interact, Follow }

/// <summary>A command is copied on submission so editing a Resource cannot change queued orders.</summary>
[GlobalClass]
public partial class ActorCommand : Resource
{
    [Export] public string IssuerId { get; set; } = "";
    [Export] public Godot.Collections.Array<string> Recipients { get; set; } = new();
    [Export] public ActorAction Action { get; set; }
    [Export] public Vector2I TargetCell { get; set; }
    [Export] public string TargetActorId { get; set; } = "";
    [Export] public string JobId { get; set; } = "";
    [Export] public bool Append { get; set; }
    [Export(PropertyHint.Range, "1,65536,1")] public float FollowDistance { get; set; } = 64;

    internal Godot.Collections.Dictionary Snapshot() => new()
    {
        ["issuer"] = IssuerId, ["action"] = (int)Action,
        ["x"] = TargetCell.X, ["y"] = TargetCell.Y,
        ["target"] = TargetActorId, ["job"] = JobId, ["follow_distance"] = FollowDistance
    };
}
