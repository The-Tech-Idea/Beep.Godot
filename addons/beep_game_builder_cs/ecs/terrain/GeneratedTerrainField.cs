using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Beep.ECS
{
    /// <summary>
    /// The finished world, at two resolutions that serve two different needs.
    ///
    /// - CELL queries return one value per gameplay tile. That is what a game
    ///   moves, paths and builds on, and it is what the tile renderer draws.
    /// - POSITION queries return the fine sub-tile sample. That is what the
    ///   painter draws, and it is why a painted coastline curves instead of
    ///   stepping around tile corners.
    ///
    /// Both come from the same generation run: the tile value is the MAJORITY of
    /// the samples inside it, so the two can differ within a tile but never
    /// disagree about the tile as a whole. Collapsing the painter onto the tile
    /// grid instead throws away every bit of sub-tile detail and makes a painted
    /// map look like blocks - which is exactly what the fine field exists to
    /// avoid.
    /// </summary>
    internal sealed class GeneratedTerrainField : ITerrainSurfaceData
    {
        private readonly int _wide;
        private readonly int _high;
        private readonly int _samplesPerCell;
        private readonly int _fieldWidth;
        private readonly int _fieldHeight;

        // Gameplay-tile resolution.
        private readonly string[] _terrain;
        private readonly string[] _inlandTerrain;
        public float BeachWidth { get; }
        public float LakeShoreWidth { get; }
        private readonly WaterBody[] _water;
        private readonly int[] _continent;
        private readonly string[] _resource;
        private readonly string[] _liquidResource;
        private readonly string[] _undergroundResource;
        private readonly float[] _undergroundRichness;
        private readonly byte[] _undergroundDepth;
        private readonly TerrainRelief[] _relief;
        private readonly float[] _elevation;
        private readonly string[] _feature;
        // Null when no start areas were generated, so the common map carries no array.
        private readonly byte[]? _startArea;
        // Null unless StartDistanceScaling asked for the distance to be measured (FEAT-14).
        private readonly ushort[]? _startDistance;

        // Sub-tile sample resolution, for painting.
        private readonly TerrainSampleKinds _sampleTerrain;
        private readonly TerrainSampleValues<WaterBody> _sampleWater;
        private readonly TerrainSampleValues<float> _sampleShade;
        private readonly byte[] _undergroundDigest;

        public GeneratedTerrainField(TerrainGenerationBuffer world, TerrainGenerationDiagnostics diagnostics,
            CancellationToken cancellation = default)
        {
            _wide = world.CellsWide;
            _high = world.CellsHigh;
            _samplesPerCell = world.SamplesPerCell;
            _fieldWidth = world.Width;
            _fieldHeight = world.Height;

            _terrain = world.CellTerrain;
            _inlandTerrain = world.CellInlandTerrain;
            BeachWidth = world.BeachWidth;
            LakeShoreWidth = world.LakeShoreWidth;
            _water = world.CellWater;
            _continent = world.CellContinent;
            _resource = world.Resource;
            _liquidResource = world.CellLiquidResource;
            _undergroundResource = world.CellUndergroundResource;
            _undergroundRichness = world.CellUndergroundRichness;
            _undergroundDepth = world.CellUndergroundDepth;
            _relief = world.CellRelief;
            _elevation = world.CellElevation;
            _feature = world.Feature;
            _startArea = world.CellStartAreaIfGenerated;
            _startDistance = world.CellStartDistanceIfGenerated;

            _sampleTerrain = world.PackTerrain(cancellation);
            _sampleWater = world.PackWater(cancellation);
            _sampleShade = world.PackShade(cancellation);

            StartPositions = world.StartPositions;
            StartAreas = world.StartAreas;
            NeutralSites = world.NeutralSites;
            Diagnostics = diagnostics;
            _undergroundDigest = TerrainUndergroundIdentity.Content(this, new(_wide, _high), cancellation);
        }

        internal string UndergroundIdentity(Vector2I origin, Vector2I size)
            => TerrainUndergroundIdentity.Compose(origin, size,
                size == new Vector2I(_wide, _high) ? _undergroundDigest
                    : TerrainUndergroundIdentity.Content(this, size));

        public TerrainGenerationDiagnostics Diagnostics { get; }

        /// <summary>Fair player start tiles, in gameplay tile coordinates.</summary>
        public IReadOnlyList<Vector2I> StartPositions { get; }

        /// <summary>One report per start when start areas were generated, in start order; empty otherwise.</summary>
        public IReadOnlyList<TerrainStartAreaReport> StartAreas { get; }

        /// <summary>What the start kit's Neutral entries placed between the starts; None without any.</summary>
        public TerrainNeutralSitesReport NeutralSites { get; }

        // ---- Gameplay tile queries -------------------------------------------------

        /// <summary>Which start's reserved area a tile is in: 0 none, k+1 start k.</summary>
        public int StartAreaAtCell(Vector2I cell) => _startArea is null ? 0 : _startArea[CellIndex(cell.X, cell.Y)];

        /// <summary>
        /// Distance from a tile to the nearest start, in whole cells, or -1 when this map measured
        /// none (StartDistanceScaling zero, or no starts). Never 0 for "unknown": 0 means the tile IS
        /// a start, and a game spawning raids by distance would put them on a player.
        /// </summary>
        public int StartDistanceAtCell(Vector2I cell) => _startDistance is null ? -1 : _startDistance[CellIndex(cell.X, cell.Y)];

        public string TerrainAtCell(Vector2I cell) => _terrain[CellIndex(cell.X, cell.Y)];
        public string InlandTerrainAtCell(Vector2I cell) => _inlandTerrain[CellIndex(cell.X, cell.Y)];

        /// <summary>"ocean", "lake", "river", or empty for dry land.</summary>
        public string WaterSourceAtCell(Vector2I cell) => _water[CellIndex(cell.X, cell.Y)] switch
        {
            WaterBody.Lake => "lake",
            WaterBody.Ocean => "ocean",
            WaterBody.River => "river",
            _ => string.Empty,
        };

        /// <summary>Landmass id per tile; 0 is water.</summary>
        public int ContinentAtCell(Vector2I cell) => _continent[CellIndex(cell.X, cell.Y)];

        /// <summary>The SURFACE resource on a tile, or empty where there is none.</summary>
        public string ResourceAtCell(Vector2I cell) => _resource[CellIndex(cell.X, cell.Y)];

        /// <summary>The LIQUID-stratum resource in a water tile - fish and kin - or empty.</summary>
        public string LiquidResourceAtCell(Vector2I cell) => _liquidResource[CellIndex(cell.X, cell.Y)];

        /// <summary>The UNDERGROUND resource beneath a tile, or empty. Invisible on the map.</summary>
        public string UndergroundResourceAtCell(Vector2I cell) => _undergroundResource[CellIndex(cell.X, cell.Y)];

        /// <summary>Underground richness 0..1 where a deposit exists, else 0.</summary>
        public float UndergroundRichnessAtCell(Vector2I cell) => _undergroundRichness[CellIndex(cell.X, cell.Y)];

        /// <summary>Underground depth band, as (int)ResourceDepth; 0 where empty too - check the id.</summary>
        public int UndergroundDepthAtCell(Vector2I cell) => _undergroundDepth[CellIndex(cell.X, cell.Y)];

        /// <summary>Flat, hills or mountains, per tile.</summary>
        public TerrainRelief ReliefAtCell(Vector2I cell) => _relief[CellIndex(cell.X, cell.Y)];

        /// <summary>Land height at a tile, 0 to 1. Water is 0.</summary>
        public float ElevationAtCell(Vector2I cell) => _elevation[CellIndex(cell.X, cell.Y)];

        /// <summary>The feature on a tile, or empty where there is none.</summary>
        public string FeatureAtCell(Vector2I cell) => _feature[CellIndex(cell.X, cell.Y)];

        // ---- Sub-tile painting queries ---------------------------------------------

        /// <summary>Terrain at a continuous position, at full sub-tile detail.</summary>
        public string TerrainAtPosition(Vector2 position) => _sampleTerrain[SampleIndexAt(position)];

        /// <summary>True where a prop must never be placed.</summary>
        public bool IsWaterAtPosition(Vector2 position)
            => _sampleWater[SampleIndexAt(position)] != WaterBody.None;

        internal GridTerrainWaterPatch WaterPatchAtCell(Vector2I cell)
            => GridTerrainWaterPatch.Create(_samplesPerCell, (x, y) =>
                _sampleWater[(cell.Y * _samplesPerCell + y) * _fieldWidth
                    + cell.X * _samplesPerCell + x] != WaterBody.None);

        internal GridTerrainWaterPatch LakePatchAtCell(Vector2I cell)
            => GridTerrainWaterPatch.Create(_samplesPerCell, (x, y) =>
                _sampleWater[(cell.Y * _samplesPerCell + y) * _fieldWidth
                    + cell.X * _samplesPerCell + x] == WaterBody.Lake);

        internal bool IsLakeAtPosition(Vector2 position)
            => _sampleWater[SampleIndexAt(position)] == WaterBody.Lake;

        /// <summary>
        /// How much of the area around a position is water, from 0 to 1.
        ///
        /// The shoreline is otherwise a hard in-or-out test per sample, so it
        /// draws as a staircase of sample-sized steps as soon as the map is
        /// zoomed in. Returning coverage instead lets the renderer fade the
        /// water edge across the boundary, which anti-aliases the coast at any
        /// zoom without needing a finer field.
        ///
        /// Unlike the colour and shade blends this deliberately DOES cross the
        /// shoreline - crossing it is the whole point.
        /// </summary>
        public float WaterFractionAtPosition(Vector2 position)
        {
            Corners c = CornersAt(position);
            return (IsWater(c.I00) * c.W00)
                + (IsWater(c.I10) * c.W10)
                + (IsWater(c.I01) * c.W01)
                + (IsWater(c.I11) * c.W11);
        }

        private float IsWater(int index) => _sampleWater[index] != WaterBody.None ? 1.0f : 0.0f;

        /// <summary>
        /// Hillshade for the painted base colour, interpolated between samples so
        /// slopes read as gradients rather than steps.
        /// </summary>
        public float ShadeAtPosition(Vector2 position)
        {
            Corners c = CornersAt(position);
            bool centreIsWater = _sampleWater[c.Centre] != WaterBody.None;

            float total = 0.0f;
            float weight = 0.0f;
            Accumulate(c.I00, c.W00, centreIsWater, ref total, ref weight);
            Accumulate(c.I10, c.W10, centreIsWater, ref total, ref weight);
            Accumulate(c.I01, c.W01, centreIsWater, ref total, ref weight);
            Accumulate(c.I11, c.W11, centreIsWater, ref total, ref weight);

            return weight > 0.0f ? total / weight : _sampleShade[c.Centre];
        }

        /// <summary>
        /// Base colour interpolated between the four surrounding samples, which
        /// softens biome boundaries below tile size.
        ///
        /// Samples across the shoreline are excluded, so the coast stays exactly
        /// where the water layer puts it instead of bleeding sea colour inland.
        /// </summary>
        public Color BlendedBaseColour(Vector2 position, Func<string, Color> colourFor)
        {
            ArgumentNullException.ThrowIfNull(colourFor);

            Corners c = CornersAt(position);
            bool centreIsWater = _sampleWater[c.Centre] != WaterBody.None;

            float red = 0.0f;
            float green = 0.0f;
            float blue = 0.0f;
            float weight = 0.0f;
            AccumulateColour(c.I00, c.W00, centreIsWater, colourFor, ref red, ref green, ref blue, ref weight);
            AccumulateColour(c.I10, c.W10, centreIsWater, colourFor, ref red, ref green, ref blue, ref weight);
            AccumulateColour(c.I01, c.W01, centreIsWater, colourFor, ref red, ref green, ref blue, ref weight);
            AccumulateColour(c.I11, c.W11, centreIsWater, colourFor, ref red, ref green, ref blue, ref weight);

            return weight > 0.0f
                ? new Color(red / weight, green / weight, blue / weight)
                : colourFor(_sampleTerrain[c.Centre]);
        }

        private void Accumulate(int index, float weight, bool centreIsWater, ref float total, ref float weightSum)
        {
            if (weight <= 0.0f || (_sampleWater[index] != WaterBody.None) != centreIsWater)
                return;
            total += _sampleShade[index] * weight;
            weightSum += weight;
        }

        private void AccumulateColour(
            int index,
            float weight,
            bool centreIsWater,
            Func<string, Color> colourFor,
            ref float red,
            ref float green,
            ref float blue,
            ref float weightSum)
        {
            if (weight <= 0.0f || (_sampleWater[index] != WaterBody.None) != centreIsWater)
                return;

            Color colour = colourFor(_sampleTerrain[index]);
            red += colour.R * weight;
            green += colour.G * weight;
            blue += colour.B * weight;
            weightSum += weight;
        }

        /// <summary>
        /// The four samples surrounding a position with their bilinear weights. A
        /// struct, because this runs once per painted pixel and must not
        /// allocate.
        /// </summary>
        private readonly record struct Corners(
            int Centre,
            int I00, float W00,
            int I10, float W10,
            int I01, float W01,
            int I11, float W11);

        private Corners CornersAt(Vector2 position)
        {
            float x = Mathf.Clamp(position.X, 0.0f, _wide - 0.0001f) * _samplesPerCell;
            float y = Mathf.Clamp(position.Y, 0.0f, _high - 0.0001f) * _samplesPerCell;

            float sampleX = x - 0.5f;
            float sampleY = y - 0.5f;
            int x0 = Mathf.FloorToInt(sampleX);
            int y0 = Mathf.FloorToInt(sampleY);
            float tx = sampleX - x0;
            float ty = sampleY - y0;

            return new Corners(
                SampleIndex(Mathf.FloorToInt(x), Mathf.FloorToInt(y)),
                SampleIndex(x0, y0), (1.0f - tx) * (1.0f - ty),
                SampleIndex(x0 + 1, y0), tx * (1.0f - ty),
                SampleIndex(x0, y0 + 1), (1.0f - tx) * ty,
                SampleIndex(x0 + 1, y0 + 1), tx * ty);
        }

        private int SampleIndexAt(Vector2 position)
        {
            int x = Mathf.FloorToInt(Mathf.Clamp(position.X, 0.0f, _wide - 0.0001f) * _samplesPerCell);
            int y = Mathf.FloorToInt(Mathf.Clamp(position.Y, 0.0f, _high - 0.0001f) * _samplesPerCell);
            return SampleIndex(x, y);
        }

        private int SampleIndex(int x, int y)
            => (Mathf.Clamp(y, 0, _fieldHeight - 1) * _fieldWidth) + Mathf.Clamp(x, 0, _fieldWidth - 1);

        private int CellIndex(int cellX, int cellY)
            => (Mathf.Clamp(cellY, 0, _high - 1) * _wide) + Mathf.Clamp(cellX, 0, _wide - 1);
    }
}
