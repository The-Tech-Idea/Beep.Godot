using Godot;
using System;

namespace Beep.ECS;

public partial class SlideComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.slide", IsActive, new()
    {
        ["remaining"] = Mathf.Max(0, _slideTimer), ["direction"] = _slideDirection,
        ["speed"] = _slideSpeed, ["reduced"] = IsCollisionReduced,
        ["ratio"] = _slideShape is not null && _standingShape is not null ? _slideShape.Size.Y / _standingShape.Size.Y : 1f,
        ["input_latched"] = _inputHeld
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.slide");
        bool active = MovementAbilityState.Flag(data, "active");
        bool reduced = MovementAbilityState.Flag(data, "reduced");
        bool latched = MovementAbilityState.Flag(data, "input_latched");
        float remaining = MovementAbilityState.Time(data, "remaining");
        float speed = MovementAbilityState.Time(data, "speed");
        float direction = MovementAbilityState.Direction(data, "direction");
        float ratio = MovementAbilityState.Number(data, "ratio");
        if (ratio < 0.1f || ratio > 1f || (remaining > 0 && (direction == 0 || speed == 0)))
            throw new FormatException("Invalid saved slide momentum or collision ratio.");
        if (reduced && (!GodotObject.IsInstanceValid(_collision)
            || (_collision!.Shape == _slideShape ? _standingShape : _collision.Shape) is not RectangleShape2D))
            throw new FormatException("Saved slide requires an authored rectangle collider.");

        RestoreCollision();
        if (reduced) ReduceCollision(ratio);
        IsActive = active;
        _slideTimer = Mathf.Min(remaining, EffectiveSlideDuration);
        _slideSpeed = Mathf.Min(speed, EffectiveSlideSpeed);
        _slideDirection = direction;
        _sliding = _slideTimer > 0 && _slideSpeed > 0;
        _inputHeld = latched;
    }
}
