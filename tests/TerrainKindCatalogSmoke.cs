using Beep.ECS;
using Godot;

// DUP-13: some catalog migrations are NOT covered by the generation determinism baseline - they feed
// rendering, z-order or grid rules, not the generated field the baseline hashes. This probe asserts
// those migrated public functions produce the exact per-kind answers, reading through
// TerrainKindCatalog.Standard, so a mutation to the catalog data (or a mis-wired consumer) is caught.
// Generation-affecting migrations are guarded by terrain_generation_baseline_probe instead.
[GlobalClass]
public partial class TerrainKindCatalogSmoke : Node
{
    public bool Run()
    {
        // TerrainLayers.LevelForKind now reads catalog.Level (DUP-13 step 4). Verify the whole mapping.
        (string Kind, int Level)[] levels =
        {
            ("deep_water", TerrainLayers.Sea),
            ("shallow_water", TerrainLayers.Sea),
            ("grass", TerrainLayers.Ground),
            ("dry_grass", TerrainLayers.Ground),
            ("desert", TerrainLayers.Ground),
            ("sand", TerrainLayers.Ground),
            ("tundra", TerrainLayers.Ground),
            ("snow", TerrainLayers.Ground),
            ("ice", TerrainLayers.Ground),
            ("jungle", TerrainLayers.Ground),
            ("swamp", TerrainLayers.Ground),
            ("mud", TerrainLayers.Ground),
            ("gravel", TerrainLayers.Hills),
            ("rock", TerrainLayers.Mountains),
            ("lava", TerrainLayers.Ground),
            ("water", TerrainLayers.Sea),          // legacy alias kept by LevelForKind
            ("nonsense_kind", TerrainLayers.Ground), // unknown -> flat Ground default
        };
        foreach ((string kind, int level) in levels)
        {
            int got = TerrainLayers.LevelForKind(kind);
            if (got != level)
                return Fail($"LevelForKind('{kind}') = {got}, expected {level}");
        }

        GD.Print("[terrain-kind-catalog] LevelForKind maps every kind (+ water alias, unknown default) through the catalog");
        return true;
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
