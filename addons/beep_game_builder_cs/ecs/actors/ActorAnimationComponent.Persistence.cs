using Godot;
using System;

namespace Beep.ECS;

public partial class ActorAnimationComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state)
    {
        var data = new Godot.Collections.Dictionary
        {
            ["x"] = Facing.X, ["y"] = Facing.Y,
            ["attack"] = _attackTime, ["hurt"] = _hurtTime, ["interact"] = _interactTime,
            ["state"] = (int)CurrentState, ["restart"] = _restartAction.HasValue ? (int)_restartAction.Value + 1 : 0,
            ["clip"] = CurrentClip, ["frame"] = 0, ["progress"] = 0.0,
            ["position"] = 0.0, ["playing"] = false
        };
        if (GodotObject.IsInstanceValid(_target))
        {
            if (_target is AnimatedSprite2D sprite)
            {
                data["frame"] = sprite.Frame; data["progress"] = sprite.FrameProgress;
                data["playing"] = sprite.IsPlaying();
            }
            else if (_target is AnimationPlayer player && !string.IsNullOrEmpty(player.AssignedAnimation))
            {
                data["position"] = player.CurrentAnimationPosition;
                data["playing"] = player.IsPlaying();
            }
        }
        MovementAbilityState.Write(state, "actor.animation", IsActive, data);
    }

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "actor.animation");
        bool active = MovementAbilityState.Flag(data, "active");
        bool playing = MovementAbilityState.Flag(data, "playing");
        Vector2 facing = MovementAbilityState.Vector(data);
        float attack = MovementAbilityState.Time(data, "attack");
        float hurt = MovementAbilityState.Time(data, "hurt");
        float interact = MovementAbilityState.Time(data, "interact");
        int selected = MovementAbilityState.Count(data, "state");
        int restart = MovementAbilityState.Count(data, "restart");
        int frame = MovementAbilityState.Count(data, "frame");
        float progress = MovementAbilityState.Time(data, "progress");
        float position = MovementAbilityState.Time(data, "position");
        if (!Enum.IsDefined(typeof(ActorAnimationState), selected)
            || (restart != 0 && restart != (int)ActorAnimationState.Attack + 1
                && restart != (int)ActorAnimationState.Hurt + 1 && restart != (int)ActorAnimationState.Interact + 1)
            || progress > 1 || !float.IsFinite(facing.LengthSquared()) || facing.LengthSquared() < 0.0001f
            || !data.TryGetValue("clip", out var clipValue) || clipValue.VariantType != Variant.Type.String)
            throw new FormatException("Invalid saved actor animation state.");
        string clip = clipValue.AsString();

        IsActive = active; Facing = facing.Normalized();
        _attackTime = attack; _hurtTime = hurt; _interactTime = interact;
        _restartAction = restart == 0 ? null : (ActorAnimationState)(restart - 1);
        CurrentState = (ActorAnimationState)selected;
        _lastPosition = _body?.GlobalPosition ?? Vector2.Zero;
        _wasActive = active;
        CurrentClip = GodotObject.IsInstanceValid(_target) && HasClip(clip) ? clip : "";
        if (CurrentClip.Length == 0) return;
        // Seek without evaluating animation tracks: loading must not replay gameplay method keys.
        switch (_target)
        {
            case AnimatedSprite2D sprite:
                sprite.Play(clip);
                sprite.SetFrameAndProgress(Math.Min(frame, sprite.SpriteFrames.GetFrameCount(clip) - 1), progress);
                if (!playing) sprite.Pause();
                break;
            case AnimationPlayer player:
                player.Play(clip);
                player.Seek(Math.Min(position, player.GetAnimation(clip).Length), false);
                if (!playing) player.Pause();
                break;
            case AnimationTree when _playback is not null:
                _playback.Start(clip, true);
                break;
        }
        if (Profile?.Directions == ActorAnimationDirections.FlipHorizontal && Facing.X != 0
            && GodotObject.IsInstanceValid(_flipSprite))
        {
            if (_flipSprite is AnimatedSprite2D animated) animated.FlipH = Facing.X < 0;
            else if (_flipSprite is Sprite2D sprite) sprite.FlipH = Facing.X < 0;
        }
    }
}
