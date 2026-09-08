namespace Beep.ECS;

public partial class GlideComponent : ISaveable
{
    public void Save(GameBuilder.GameStateData state) => MovementAbilityState.Write(state, "ability.glide", IsActive, new()
    {
        ["gliding"] = _isGliding, ["speed_x"] = _glideX
    });

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, "ability.glide");
        bool active = MovementAbilityState.Flag(data, "active");
        bool gliding = MovementAbilityState.Flag(data, "gliding");
        float speed = MovementAbilityState.Number(data, "speed_x");
        IsActive = active;
        _isGliding = gliding;
        _glideX = speed;
    }
}
