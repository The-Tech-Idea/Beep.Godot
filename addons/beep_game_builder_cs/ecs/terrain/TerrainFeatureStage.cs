using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Assigns terrain FEATURES - woods, jungle, marsh, oasis - as a layer over
    /// the base terrain, the way Civilization models them: a tile is grassland
    /// *with* woods on it, not a separate "forest terrain".
    ///
    /// Keeping them separate is what lets the renderer draw a canopy as an
    /// object standing on the ground, instead of recolouring the ground darker
    /// and hoping it reads as forest.
    ///
    /// Features are decided per gameplay tile, after the tile reduction, so a
    /// feature always covers a whole tile and can never half-cover one.
    /// </summary>
    internal static class TerrainFeatureStage
    {
        public const string None = "";
        public const string Woods = "woods";
        public const string Jungle = "jungle";
        public const string Marsh = "marsh";
        public const string Oasis = "oasis";

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            if (settings.FeatureDensity <= 0.0f)
                return;

            int wide = world.CellsWide;
            int high = world.CellsHigh;

            for (int cellY = 0; cellY < high; cellY++)
            {
                for (int cellX = 0; cellX < wide; cellX++)
                {
                    int cell = world.CellIndex(cellX, cellY);
                    if (world.CellWater[cell] != WaterBody.None)
                        continue;

                    // Mountains carry no vegetation: a canopy drawn over a peak
                    // reads as a mistake.
                    if (world.CellRelief[cell] == TerrainRelief.Mountains)
                        continue;

                    world.Feature[cell] = Choose(world, settings, cell, cellX, cellY);
                }
            }
        }

        private static string Choose(
            TerrainWorld world,
            TerrainGenerationSettings settings,
            int cell,
            int cellX,
            int cellY)
        {
            string terrain = world.CellTerrain[cell];
            int sample = world.CellCentreIndex(cellX, cellY);
            float moisture = world.Moisture[sample];
            float temperature = world.Temperature[sample];
            float roll = Hash01(settings.Seed + 55001, cellX, cellY);
            float density = Mathf.Clamp(settings.FeatureDensity, 0.0f, 4.0f);

            // Terrain that already means dense vegetation always carries the
            // matching feature, so the ground under it can be ordinary soil.
            if (terrain == "jungle")
                return Jungle;
            if (terrain == "swamp")
                return Marsh;

            // An oasis is the rare exception that makes a desert readable.
            if (terrain == "desert")
                return roll < 0.012f * density ? Oasis : None;

            if (terrain is not ("grass" or "dry_grass" or "tundra"))
                return None;

            // Woods want rain and not too much heat; the chance is scaled by
            // moisture so forest thickens toward wet ground rather than
            // appearing at a flat rate everywhere.
            if (moisture < 0.26f || temperature < 0.15f)
                return None;

            float chance = Mathf.Clamp((moisture - 0.26f) * 2.6f, 0.0f, 0.85f) * density;
            if (terrain == "tundra")
                chance *= 0.45f;

            return roll < chance ? Woods : None;
        }

        private static float Hash01(int seed, int x, int y)
        {
            uint value = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215.0f;
        }
    }
}
