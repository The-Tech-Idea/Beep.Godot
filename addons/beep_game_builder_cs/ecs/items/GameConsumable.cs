using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// A one-shot consumable — food, scroll, bandage, elixir. Earns its class with a heal and a
    /// timed status effect on use. `bread.tres`, `scroll_haste.tres` are `.tres` of this class.
    /// (Survival's GameFood earns a further subclass by adding HungerRestore — see items/survival.md.)
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GameConsumable : GameItem
    {
        /// <summary>Health restored on use. 0 = none.</summary>
        [Export] public float HealAmount { get; set; } = 0f;

        /// <summary>Status effect applied on use, by id, or empty for none.</summary>
        [Export] public string StatusEffectId { get; set; } = "";

        /// <summary>How long the status effect lasts, in CLOCK UNITS — not seconds. 3 = 3 seconds
        /// in a real-time genre, 3 turns in a turn-based one, the same `.tres`, because the genre
        /// owns the clock (Phase 7).</summary>
        [Export] public float Duration { get; set; } = 0f;
    }
}
