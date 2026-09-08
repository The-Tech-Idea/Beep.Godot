using Godot;

namespace Beep.ECS;

public partial class JumpComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.jump", IsActive, new()
    {
        ["remaining"] = _jumpsRemaining, ["coyote"] = Mathf.Max(0, _coyoteTimer),
        ["buffer"] = Mathf.Max(0, _bufferTimer), ["cut"] = Mathf.Max(0, _jumpCutTimer)
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.jump");
        bool active = MovementAbilityState.Flag(data, "active");
        int remaining = MovementAbilityState.Count(data, "remaining");
        float coyote = MovementAbilityState.Time(data, "coyote");
        float buffer = MovementAbilityState.Time(data, "buffer");
        float cut = MovementAbilityState.Time(data, "cut");
        IsActive = active;
        _jumpsRemaining = Mathf.Min(remaining, EffectiveMaxJumps);
        _coyoteTimer = Mathf.Min(coyote, EffectiveCoyoteTime);
        _bufferTimer = Mathf.Min(buffer, EffectiveJumpBufferTime);
        _jumpCutTimer = Mathf.Min(cut, EffectiveVariableJumpCutDuration);
        _jumpHeld = false;
    }
}
