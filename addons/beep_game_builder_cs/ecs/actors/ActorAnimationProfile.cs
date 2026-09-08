using Godot;

namespace Beep.ECS;

public enum ActorAnimationState { Idle, Move, Attack, Hurt, Death, Work, Interact, Jump, Fall, Dash, Slide, Glide, Hover, Fly }
public enum ActorAnimationDirections { None, FlipHorizontal, Four, Eight }

/// <summary>Clip/state names for native Godot animation nodes, shared by an authored actor type.</summary>
[GlobalClass]
public partial class ActorAnimationProfile : Resource
{
    [ExportGroup("Motion")]
    [Export] public ActorAnimationDirections Directions { get; set; } = ActorAnimationDirections.None;
    [Export] public bool UseGroundedStates { get; set; }
    [Export] public bool FaceAim { get; set; }
    [Export] public float MovingSpeedThreshold { get; set; } = 5f;
    [Export] public string[] DirectionSuffixes { get; set; } = { "_right", "_down_right", "_down", "_down_left", "_left", "_up_left", "_up", "_up_right" };

    [ExportGroup("Clips")]
    [Export] public string Idle { get; set; } = "idle";
    [Export] public string Move { get; set; } = "move";
    [Export] public string Attack { get; set; } = "attack";
    [Export] public string Hurt { get; set; } = "hurt";
    [Export] public string Death { get; set; } = "death";
    [Export] public string Work { get; set; } = "work";
    [Export] public string Interact { get; set; } = "interact";
    [Export] public string Jump { get; set; } = "jump";
    [Export] public string Fall { get; set; } = "fall";
    [Export] public string Dash { get; set; } = "dash";
    [Export] public string Slide { get; set; } = "slide";
    [Export] public string Glide { get; set; } = "glide";
    [Export] public string Hover { get; set; } = "hover";
    [Export] public string Fly { get; set; } = "fly";

    [ExportGroup("Action Holds")]
    [Export] public float AttackSeconds { get; set; } = 0.25f;
    [Export] public float HurtSeconds { get; set; } = 0.18f;
    [Export] public float InteractSeconds { get; set; } = 0.25f;

    internal string Clip(ActorAnimationState state) => state switch
    {
        ActorAnimationState.Move => Move, ActorAnimationState.Attack => Attack,
        ActorAnimationState.Hurt => Hurt, ActorAnimationState.Death => Death,
        ActorAnimationState.Work => Work, ActorAnimationState.Interact => Interact,
        ActorAnimationState.Jump => Jump, ActorAnimationState.Fall => Fall,
        ActorAnimationState.Dash => Dash, ActorAnimationState.Slide => Slide,
        ActorAnimationState.Glide => Glide, ActorAnimationState.Hover => Hover,
        ActorAnimationState.Fly => Fly, _ => Idle
    };

    internal string Suffix(Vector2 facing)
    {
        if (Directions is not (ActorAnimationDirections.Four or ActorAnimationDirections.Eight)) return "";
        int index = Directions == ActorAnimationDirections.Four
            ? (Mathf.Abs(facing.X) > Mathf.Abs(facing.Y) ? (facing.X < 0 ? 4 : 0) : (facing.Y < 0 ? 6 : 2))
            : Mathf.PosMod(Mathf.RoundToInt(facing.Angle() / (Mathf.Pi / 4)), 8);
        return DirectionSuffixes is { } suffixes && index < suffixes.Length ? suffixes[index] : "";
    }
}
