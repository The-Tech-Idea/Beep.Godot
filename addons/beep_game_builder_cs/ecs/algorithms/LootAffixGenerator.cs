using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Procedural loot affixes — the Diablo/PoE pattern where a base item rolls random bonus
    /// modifiers ("of the Bear" = +armor, "of Speed" = ×move_speed). Seeded, so the same drop is
    /// reproducible (deterministic loot for a seeded run, or a saved drop re-rolled identically).
    ///
    /// CRITICAL shape constraint: <see cref="GameItem"/> is a SHARED .tres definition — affixes
    /// must NEVER be written onto it, or every iron sword in the world would share one roll. An
    /// affix roll is a per-instance <see cref="StatModifier"/>[] produced at spawn time; the
    /// spawner attaches them to the dropped node's StatsComponent (or stores them on the slot).
    /// Static and allocation-light, no node state.
    /// </summary>
    public static class LootAffixGenerator
    {
        /// <summary>One rollable affix template: which stat it buffs and the value range per tier.</summary>
        public readonly record struct AffixTemplate(
            StringName Stat,
            StatOp Op,
            float MinAmount,
            float MaxAmount,
            string DisplaySuffix);

        /// <summary>Rarity tier gates how many affixes roll and how wide the value band is —
        /// a Legendary rolls more, stronger affixes than a Common.</summary>
        public static int AffixCountFor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 0,
            ItemRarity.Uncommon => 1,
            ItemRarity.Rare => 2,
            ItemRarity.Epic => 3,
            ItemRarity.Legendary => 4,
            _ => 0,
        };

        /// <summary>Rarity scales the rolled amount within the template's band — Legendary samples
        /// the top of the range, Common the bottom.</summary>
        private static float AmountBiasFor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => 0.4f,
            ItemRarity.Rare => 0.6f,
            ItemRarity.Epic => 0.8f,
            ItemRarity.Legendary => 1.0f,
            _ => 0.25f,
        };

        /// <summary>
        /// Roll a set of affix modifiers for an item of <paramref name="rarity"/>, deterministic
        /// under <paramref name="seed"/>. Picks distinct affixes (no double "of the Bear") and
        /// returns <paramref name="count"/> = AffixCountFor(rarity) modifiers, or fewer when the
        /// template pool is smaller. Returns an empty array for Common or an empty pool.
        /// </summary>
        public static StatModifier[] Roll(IReadOnlyList<AffixTemplate> pool, ItemRarity rarity, ulong seed)
        {
            int count = AffixCountFor(rarity);
            if (count <= 0 || pool == null || pool.Count == 0) return System.Array.Empty<StatModifier>();
            count = Mathf.Min(count, pool.Count);

            var rng = new RandomNumberGenerator { Seed = seed };
            float bias = AmountBiasFor(rarity);

            // Partial Fisher–Yates on an index copy so we never mutate the caller's pool and never
            // pick the same affix twice.
            var indices = new int[pool.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            var result = new List<StatModifier>(count);
            for (int pick = 0; pick < count; pick++)
            {
                int swap = rng.RandiRange(pick, indices.Length - 1);
                (indices[pick], indices[swap]) = (indices[swap], indices[pick]);

                var t = pool[indices[pick]];
                // Bias the sample toward the top of the band for high rarity, but keep a little
                // variance so two Legendaries don't roll identically.
                float roll = Mathf.Lerp(bias, 1f, rng.Randf() * 0.3f);
                float amount = Mathf.Lerp(t.MinAmount, t.MaxAmount, Mathf.Clamp(roll, 0f, 1f));
                // Permanent (Duration < 0): affixes live as long as the item instance does.
                result.Add(new StatModifier { Stat = t.Stat, Op = t.Op, Amount = amount, Duration = -1f });
            }
            return result.ToArray();
        }
    }
}
