using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>How a water sample connects to the rest of the world.</summary>
    internal enum WaterBody : byte
    {
        None = 0,
        Ocean = 1,
        Lake = 2,
        River = 3,
    }

    /// <summary>
    /// The single mutable working set every generation stage reads and writes.
    /// One array per fact, all sharing one index space, so gameplay cells,
    /// continuous rendering, lakes and prop placement can never disagree about
    /// what is at a position.
    /// </summary>
    internal sealed class TerrainWorld
    {
        public TerrainWorld(int width, int height, int samplesPerCell)
        {
            Width = width;
            Height = height;
            SamplesPerCell = samplesPerCell;
            int count = width * height;

            Land = new bool[count];
            Footprint = new bool[count];
            Water = new WaterBody[count];
            Elevation = new float[count];
            Temperature = new float[count];
            Moisture = new float[count];
            Relief = new TerrainRelief[count];
            CoastDistance = new int[count];
            Terrain = new string[count];
            Shade = new float[count];
            Array.Fill(Shade, 1.0f);
            Continent = new int[count];

            int cells = CellsWide * CellsHigh;
            Resource = new string[cells];
            Array.Fill(Resource, string.Empty);
            StartPositions = new List<Vector2I>();

            CellTerrain = new string[cells];
            Array.Fill(CellTerrain, "grass");
            CellWater = new WaterBody[cells];
            CellRelief = new TerrainRelief[cells];
            CellElevation = new float[cells];
            CellShade = new float[cells];
            Array.Fill(CellShade, 1.0f);
            CellContinent = new int[cells];
            Feature = new string[cells];
            Array.Fill(Feature, string.Empty);
        }

        public int Width { get; }
        public int Height { get; }
        public int SamplesPerCell { get; }
        public int Count => Width * Height;

        /// <summary>True where the sample is dry land.</summary>
        public bool[] Land { get; }

        /// <summary>
        /// The landmass outline as the landmass stage chose it, before lakes
        /// were carved out of it. This is what "Land Coverage" means and what
        /// counts as one landmass: a lake inside an island does not make it two
        /// islands, and an emergent inland sea was never part of a footprint.
        /// </summary>
        public bool[] Footprint { get; }

        /// <summary>Ocean reaches the map border; a lake never does.</summary>
        public WaterBody[] Water { get; }

        /// <summary>Normalized 0..1 height above sea level on land.</summary>
        public float[] Elevation { get; }

        /// <summary>Normalized 0..1, 1 being hottest.</summary>
        public float[] Temperature { get; }

        /// <summary>Normalized 0..1, 1 being wettest.</summary>
        public float[] Moisture { get; }

        /// <summary>Flat, hills or mountains, assigned by elevation percentile.</summary>
        public TerrainRelief[] Relief { get; }

        /// <summary>Samples to the nearest water, 0 in water itself.</summary>
        public int[] CoastDistance { get; }

        /// <summary>Final terrain kind consumed by gameplay and rendering.</summary>
        public string[] Terrain { get; }

        /// <summary>
        /// Multiplier on the painted base colour, 1 being unlit. Relief is
        /// carried here rather than baked into the terrain kind, so a hill can
        /// stay grassland and still read as a hill.
        /// </summary>
        public float[] Shade { get; }

        /// <summary>
        /// Which landmass a sample belongs to; 0 is water. Gameplay asks this to
        /// tell "the same continent" from "across the sea".
        /// </summary>
        public int[] Continent { get; }

        /// <summary>
        /// Resource per GAMEPLAY CELL, not per sample: a resource is something a
        /// tile has, so storing it per sample would let one tile hold several.
        /// Empty means none.
        /// </summary>
        public string[] Resource { get; }

        /// <summary>Fair player start tiles, in gameplay cell coordinates.</summary>
        public List<Vector2I> StartPositions { get; }

        // The gameplay-resolution view of the world. These are the authoritative
        // outputs: one value per tile, which is what a game actually moves,
        // paths and builds on. The sample arrays above exist to decide these
        // well, not to be consumed directly.

        /// <summary>Terrain kind per gameplay tile.</summary>
        public string[] CellTerrain { get; }

        /// <summary>Water body per gameplay tile; None means dry land.</summary>
        public WaterBody[] CellWater { get; }

        /// <summary>Relief per gameplay tile.</summary>
        public TerrainRelief[] CellRelief { get; }

        /// <summary>
        /// Land height per tile, 0 to 1, reduced from the sample grid.
        ///
        /// Relief only says flat, hills or mountains, which is enough to decide
        /// what a tile IS and not enough to decide how it looks against its
        /// neighbours: every tile of a range shares one band, so anything drawn
        /// from relief alone is flat-topped. Height is the field that says which
        /// part of a range is its crest.
        /// </summary>
        public float[] CellElevation { get; }

        /// <summary>Averaged hillshade per gameplay tile.</summary>
        public float[] CellShade { get; }

        /// <summary>Landmass id per gameplay tile; 0 is water.</summary>
        public int[] CellContinent { get; }

        /// <summary>
        /// Terrain feature per tile - woods, jungle, marsh, oasis - or empty.
        /// A feature sits ON the terrain rather than replacing it, so a wooded
        /// grassland tile is still grassland underneath.
        /// </summary>
        public string[] Feature { get; }

        public int CellIndex(int cellX, int cellY) => (cellY * CellsWide) + cellX;

        public bool CellInBounds(int cellX, int cellY)
            => cellX >= 0 && cellY >= 0 && cellX < CellsWide && cellY < CellsHigh;

        public int CellsWide => Mathf.Max(1, Width / Mathf.Max(1, SamplesPerCell));
        public int CellsHigh => Mathf.Max(1, Height / Mathf.Max(1, SamplesPerCell));

        /// <summary>The sample at the centre of a gameplay cell.</summary>
        public int CellCentreIndex(int cellX, int cellY)
        {
            int x = Mathf.Clamp((cellX * SamplesPerCell) + (SamplesPerCell / 2), 0, Width - 1);
            int y = Mathf.Clamp((cellY * SamplesPerCell) + (SamplesPerCell / 2), 0, Height - 1);
            return Index(x, y);
        }

        public int Index(int x, int y) => (y * Width) + x;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Tile-space position of a sample's centre.</summary>
        public Vector2 TileCentre(int x, int y)
            => new((x + 0.5f) / SamplesPerCell, (y + 0.5f) / SamplesPerCell);

        /// <summary>
        /// 0 at the equator, 1 at either pole. Drives the climate bands that give
        /// a Civilization-style map its recognizable polar/temperate/tropical
        /// structure.
        /// </summary>
        /// <param name="offsetSamples">
        /// Noise displacement applied to the row before the latitude is taken,
        /// so the climate bands meander instead of drawing as perfectly
        /// horizontal stripes across the map.
        /// </param>
        /// <summary>
        /// Latitude at a row: 0 at the equator, 1 at a pole.
        ///
        /// A WHOLE-WORLD map - span 1 - runs pole to equator to pole down its
        /// height, which is what gives a Civilization map its structure. A small
        /// map is not a whole world, and treating it as one is what puts an ice
        /// cap, a desert and a jungle on the same island: the map is only fifty
        /// tiles tall, so those fifty tiles get handed the entire climate range.
        ///
        /// Below span 1 the map becomes a WINDOW on one band instead - a gentle
        /// gradient across it, centred where centre says. One hemisphere, one
        /// climate, which is what a regional map actually is.
        /// </summary>
        public float Latitude(int y, float offsetSamples, float span, float centre)
        {
            float down = (y + 0.5f + offsetSamples) / Height;
            if (span >= 1.0f)
                return Mathf.Abs((down * 2.0f) - 1.0f);

            return Mathf.Clamp(centre + ((down - 0.5f) * span), 0.0f, 1.0f);
        }
    }

    internal enum TerrainRelief : byte
    {
        Flat = 0,
        Hills = 1,
        Mountains = 2,
    }
}
