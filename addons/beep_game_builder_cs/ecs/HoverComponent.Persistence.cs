using Godot;

namespace Beep.ECS;

public partial class HoverComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.hover", IsActive, new()
    {
        ["elapsed"] = Mathf.Clamp(_hoverTimer, 0, EffectiveMaxHoverTime),
        ["cooldown"] = Mathf.Max(0, _cooldownTimer), ["hovering"] = _isHovering
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.hover");
        bool active = MovementAbilityState.Flag(data, "active");
        bool hovering = MovementAbilityState.Flag(data, "hovering");
        float elapsed = MovementAbilityState.Time(data, "elapsed");
        float cooldown = MovementAbilityState.Time(data, "cooldown");
        IsActive = active;
        _hoverTimer = Mathf.Min(elapsed, EffectiveMaxHoverTime);
        _cooldownTimer = Mathf.Min(cooldown, EffectiveHoverCooldown);
        _isHovering = hovering;
    }
}
