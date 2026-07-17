using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One weighted entry in a <see cref="DropTableComponent"/>, authorable in the inspector as a
    /// .tres — the same pattern as <c>CraftingIngredient</c>/<c>CraftingRecipe</c>.
    ///
    /// Replaces the old code-only nested <c>DropEntry</c>, which had no <c>[Export]</c> and could
    /// only be filled by an <c>AddEntry()</c> that nothing ever called — so every <c>Roll()</c>
    /// returned nothing and no loot dropped anywhere in the addon.
    ///
    /// The season/weather gates are a bool + enum rather than a nullable enum, because Godot does
    /// not export <c>Nullable&lt;TEnum&gt;</c>. Leave <see cref="AnySeason"/>/<see cref="AnyWeather"/>
    /// on (the default) for an unrestricted drop.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class DropTableEntry : Resource
    {
        /// <summary>The authored item this entry drops. Its <see cref="GameItem.WorldScene"/> is
        /// what gets spawned into the world, and the spawned pickup is stamped with this item so
        /// collecting it yields the right thing. Replaced a bare PackedScene, which dropped a node
        /// that carried no item identity.</summary>
        [Export] public GameItem? Item { get; set; }

        /// <summary>Relative weight in the weighted pick. Higher = more likely.</summary>
        [Export] public float Weight { get; set; } = 1f;

        /// <summary>When true, this entry can drop in any season and <see cref="Season"/> is ignored.</summary>
        [Export] public bool AnySeason { get; set; } = true;

        /// <summary>Season this entry is restricted to when <see cref="AnySeason"/> is false.</summary>
        [Export] public SeasonalComponent.Season Season { get; set; } = SeasonalComponent.Season.Spring;

        /// <summary>When true, this entry can drop in any weather and <see cref="Weather"/> is ignored.</summary>
        [Export] public bool AnyWeather { get; set; } = true;

        /// <summary>Weather this entry is restricted to when <see cref="AnyWeather"/> is false.</summary>
        [Export] public WeatherSystemComponent.WeatherType Weather { get; set; } = WeatherSystemComponent.WeatherType.Clear;
    }
}
