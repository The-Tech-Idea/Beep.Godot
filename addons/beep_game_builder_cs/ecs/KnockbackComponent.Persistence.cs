using Godot;
using System;

namespace Beep.ECS;

public partial class KnockbackComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.knockback", IsActive, new()
    {
        ["remaining"] = Mathf.Max(0, _remaining), ["x"] = _knockbackVelocity.X, ["y"] = _knockbackVelocity.Y
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.knockback");
        bool active = MovementAbilityState.Flag(data, "active");
        float remaining = MovementAbilityState.Time(data, "remaining");
        Vector2 impulse = MovementAbilityState.Vector(data);
        if (!float.IsFinite(impulse.LengthSquared())) throw new FormatException("Saved knockback impulse is too large.");
        IsActive = active;
        _remaining = Mathf.Min(remaining, EffectiveDuration);
        _knockbackVelocity = impulse.LimitLength(EffectiveMaxKnockbackMagnitude);
    }
}
