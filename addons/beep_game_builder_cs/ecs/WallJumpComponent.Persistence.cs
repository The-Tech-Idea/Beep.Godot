using Godot;
using System;

namespace Beep.ECS;

public partial class WallJumpComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.wall_jump", IsActive, new()
    {
        ["stick"] = Mathf.Max(0, _stickTimer), ["lock"] = Mathf.Max(0, _lockTimer),
        ["sliding"] = _isWallSliding, ["slide_direction"] = _slideDirection,
        ["kick_direction"] = _kickDirection, ["launch_pending"] = _launchPending
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.wall_jump");
        bool active = MovementAbilityState.Flag(data, "active");
        bool sliding = MovementAbilityState.Flag(data, "sliding");
        bool launch = MovementAbilityState.Flag(data, "launch_pending");
        float stick = MovementAbilityState.Time(data, "stick");
        float locked = MovementAbilityState.Time(data, "lock");
        int slideDirection = MovementAbilityState.Direction(data, "slide_direction");
        int kickDirection = MovementAbilityState.Direction(data, "kick_direction");
        if ((sliding && slideDirection == 0) || ((locked > 0 || launch) && kickDirection == 0))
            throw new FormatException("Saved wall motion requires a contact direction.");
        IsActive = active;
        _isWallSliding = sliding;
        _stickTimer = Mathf.Min(stick, EffectiveWallStickTime);
        _lockTimer = Mathf.Min(locked, EffectiveWallJumpLockTime);
        _slideDirection = slideDirection;
        _kickDirection = kickDirection;
        _wallDirection = 0;
        _launchPending = launch;
        _hasJumpFrame = launch;
        _jumpFrame = Engine.GetPhysicsFrames();
    }
}
