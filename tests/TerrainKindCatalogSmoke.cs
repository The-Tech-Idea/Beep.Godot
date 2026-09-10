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

        // TerrainTileSets.GroundOf / IsWaterKind / IsLandKind now read catalog.Class (DUP-13 Class step).
        // Only deep_water/shallow_water are Water and only rock/lava are Steep; gravel and snow are Land,
        // not Steep. The bare "water" alias and the empty string (no terrain) are resolved outside the
        // catalog. GroundOf feeds descriptive tile metadata, so the determinism baseline does not hash
        // it - this is its guard.
        (string Kind, TerrainTileSets.Ground Class)[] classes =
        {
            ("deep_water", TerrainTileSets.Ground.Water),
            ("shallow_water", TerrainTileSets.Ground.Water),
            ("water", TerrainTileSets.Ground.Water),   // legacy alias
            ("rock", TerrainTileSets.Ground.Steep),
            ("lava", TerrainTileSets.Ground.Steep),
            ("grass", TerrainTileSets.Ground.Land),
            ("sand", TerrainTileSets.Ground.Land),
            ("gravel", TerrainTileSets.Ground.Land),   // a peak material, but Land - not Steep
            ("snow", TerrainTileSets.Ground.Land),
            ("nonsense_kind", TerrainTileSets.Ground.Land), // unknown -> Land default
            ("", TerrainTileSets.Ground.Land),          // no terrain -> Land default
        };
        foreach ((string kind, TerrainTileSets.Ground cls) in classes)
        {
            TerrainTileSets.Ground got = TerrainTileSets.GroundOf(kind);
            if (got != cls)
                return Fail($"GroundOf('{kind}') = {got}, expected {cls}");
        }

        // IsWaterKind: canonical water + the "water" alias; nothing else, and the empty string is not
        // water. IsLandKind is its complement over non-empty ids (Steep kinds are still land).
        (string Kind, bool Water, bool Land)[] membership =
        {
            ("deep_water", true, false),
            ("shallow_water", true, false),
            ("water", true, false),                     // alias: water, and not land
            ("grass", false, true),
            ("sand", false, true),
            ("rock", false, true),                      // Steep is land, not water
            ("lava", false, true),
            ("nonsense_kind", false, true),             // unknown, non-empty -> land
            ("", false, false),                         // no terrain -> neither
        };
        foreach ((string kind, bool water, bool land) in membership)
        {
            bool gotWater = TerrainTileSets.IsWaterKind(kind);
            if (gotWater != water)
                return Fail($"IsWaterKind('{kind}') = {gotWater}, expected {water}");
            bool gotLand = TerrainTileSets.IsLandKind(kind);
            if (gotLand != land)
                return Fail($"IsLandKind('{kind}') = {gotLand}, expected {land}");
        }

        // GridTerrainRules.DefaultBlockedTerrainKinds now reads catalog.BlockedByDefault for the
        // canonical kinds (deep_water, shallow_water, lava) and adds the water/sea/ocean aliases the
        // catalog does not name. This is a grid/build-side default, not a generation input, so the
        // determinism baseline does not cover it. Per-kind flag first:
        (string Kind, bool Blocked)[] blocked =
        {
            ("deep_water", true),
            ("shallow_water", true),
            ("lava", true),
            ("grass", false),
            ("sand", false),
            ("rock", false),   // a cliff, but building is blocked by relief, not by kind here
            ("ice", false),
            ("gravel", false),
            ("nonsense_kind", false),
        };
        foreach ((string kind, bool want) in blocked)
        {
            bool got = TerrainKindCatalog.Standard.BlockedByDefault(kind);
            if (got != want)
                return Fail($"catalog.BlockedByDefault('{kind}') = {got}, expected {want}");
        }

        // The assembled default list must be exactly the aliases followed by the catalog's blocked
        // kinds in catalog order - the same six entries the hardcoded list held, unchanged.
        string[] expectedBlocked = { "water", "sea", "ocean", "deep_water", "shallow_water", "lava" };
        Godot.Collections.Array<string> gotBlocked = GridTerrainRules.DefaultBlockedTerrainKinds();
        if (gotBlocked.Count != expectedBlocked.Length)
            return Fail($"DefaultBlockedTerrainKinds count = {gotBlocked.Count}, expected {expectedBlocked.Length} ([{string.Join(",", gotBlocked)}])");
        for (int i = 0; i < expectedBlocked.Length; i++)
            if (gotBlocked[i] != expectedBlocked[i])
                return Fail($"DefaultBlockedTerrainKinds[{i}] = '{gotBlocked[i]}', expected '{expectedBlocked[i]}'");

        GD.Print("[terrain-kind-catalog] OK");
        return true;
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
