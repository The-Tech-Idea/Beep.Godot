using Godot;

namespace Beep.ECS;

public partial class LevelingComponent
{
    private const string SaveKey = "progression.leveling";

    public void Save(GameBuilder.GameStateData state)
    {
        NormalizeProgressionState();
        MovementAbilityState.Write(state, SaveKey, IsActive, new()
        {
            ["level"] = Level, ["xp"] = CurrentXp, ["points"] = StatPoints
        });
    }

    public void Load(GameBuilder.GameStateData state)
    {
        var data = MovementAbilityState.Read(state, SaveKey);
        int level = MovementAbilityState.Count(data, "level");
        float xp = MovementAbilityState.Time(data, "xp");
        int points = MovementAbilityState.Count(data, "points");
        bool active = MovementAbilityState.Flag(data, "active");
        if (level < 1) throw new System.FormatException("Progression level must be positive.");
        Level = Mathf.Min(level, EffectiveMaxLevel);
        CurrentXp = xp;
        StatPoints = points;
        IsActive = active;
        EmitSignal(SignalName.XpChanged, CurrentXp, XpNeeded);
    }
}
