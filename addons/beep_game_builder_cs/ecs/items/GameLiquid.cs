using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// A liquid — potion, fuel, lamp oil, water. Earns its class with <see cref="Volume"/> and
    /// <see cref="IsDrinkable"/>: fuel and lamp oil are liquids you never swallow, which a plain
    /// consumable cannot express. `potion_health.tres`, `lamp_oil.tres` are `.tres` of this class.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameLiquid : GameItem
    {
        /// <summary>Amount of liquid, in the genre's own units (e.g. mL, units of fuel).</summary>
        [Export] public float Volume { get; set; } = 1f;

        /// <summary>Whether it can be drunk. A potion is; fuel and oil are not.</summary>
        [Export] public bool IsDrinkable { get; set; } = true;

        /// <summary>Health restored if drunk. 0 = none.</summary>
        [Export] public float HealAmount { get; set; } = 0f;

        /// <summary>Status effect applied if drunk, by id, or empty for none.</summary>
        [Export] public string StatusEffectId { get; set; } = "";
    }
}
