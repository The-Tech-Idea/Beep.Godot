using Godot;

namespace Beep.ECS;

public partial class FlyComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.flight", IsActive, new()
    {
        ["boost"] = Mathf.Max(0, _boostTimer), ["rotation"] = _targetRotation,
        ["bank"] = GodotObject.IsInstanceValid(_bankSprite) ? _bankSprite!.Skew : 0
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.flight");
        bool active = MovementAbilityState.Flag(data, "active");
        float boost = MovementAbilityState.Time(data, "boost");
        float rotation = MovementAbilityState.Number(data, "rotation");
        float bank = MovementAbilityState.Number(data, "bank");
        IsActive = active;
        _boostTimer = Mathf.Min(boost, EffectiveBoostDuration);
        _targetRotation = rotation;
        if (GodotObject.IsInstanceValid(_bankSprite)) _bankSprite!.Skew = bank;
    }
}
