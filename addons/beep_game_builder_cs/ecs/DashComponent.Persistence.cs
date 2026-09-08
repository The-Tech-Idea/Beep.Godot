using Godot;
using System;

namespace Beep.ECS;

public partial class DashComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.dash", IsActive, new()
    {
        ["remaining"] = Mathf.Max(0, _dashTimer), ["cooldown"] = Mathf.Max(0, _cooldownTimer),
        ["x"] = _dashDirection.X, ["y"] = _dashDirection.Y
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.dash");
        bool active = MovementAbilityState.Flag(data, "active");
        float remaining = MovementAbilityState.Time(data, "remaining");
        float cooldown = MovementAbilityState.Time(data, "cooldown");
        Vector2 direction = MovementAbilityState.Vector(data);
        if (remaining > 0 && (direction.LengthSquared() < 0.0001f || !float.IsFinite(direction.LengthSquared())))
            throw new FormatException("Saved dash requires a finite direction.");
        IsActive = active;
        _dashDirection = remaining > 0 ? direction.Normalized() : Vector2.Zero;
        _dashTimer = Mathf.Min(remaining, EffectiveDashDuration);
        _cooldownTimer = Mathf.Min(cooldown, EffectiveDashCooldown);
    }
}
