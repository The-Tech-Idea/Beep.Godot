using Godot;

namespace Beep.ECS;

public partial class PlatformerController : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.platformer", IsActive, new()
    {
        ["coyote"] = Mathf.Max(0, _coyoteTimer), ["buffer"] = Mathf.Max(0, _jumpBufferTimer),
        ["was_in_air"] = _wasInAir
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.platformer");
        bool active = MovementAbilityState.Flag(data, "active");
        bool airborne = MovementAbilityState.Flag(data, "was_in_air");
        float coyote = MovementAbilityState.Time(data, "coyote");
        float buffer = MovementAbilityState.Time(data, "buffer");
        IsActive = active;
        _coyoteTimer = Mathf.Min(coyote, EffectiveCoyoteTime);
        _jumpBufferTimer = Mathf.Min(buffer, EffectiveJumpBufferTime);
        _wasInAir = airborne;
    }
}
