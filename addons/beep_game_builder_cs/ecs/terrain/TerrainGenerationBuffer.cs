using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

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
    /// Temporary mutable arrays shared by the generation stages in one build.
    /// TerrainFieldBuilder transfers the output arrays to GeneratedTerrainField.
    /// This is not a scene controller or the live gameplay cell store.
    /// </summary>
    internal sealed class TerrainGenerationBuffer
    {
        public TerrainGenerationBuffer(int width, int height, int samplesPerCell)
        {
            Width = width;
            Height = height;
            SamplesPerCell = samplesPerCell;
            int count = width * height;

            _land = new bool[count];
            _footprint = new bool[count];
            _water = new WaterBody[count];
            _terrain = new string[count];

            StartPositions = new List<Vector2I>();
        }

        public int Width { get; }
        public int Height { get; }
        public int SamplesPerCell { get; }
        public int Count => Width * Height;

        /// <summary>True where the sample is dry land.</summary>
        public bool[] Land => _land ?? throw new InvalidOperationException("Land mask has been released.");

        /// <summary>
        /// The landmass outline as the landmass stage chose it, before lakes
        /// were carved out of it. This is what "Land Coverage" means and what
        /// counts as one landmass: a lake inside an island does not make it two
        /// islands, and an emergent inland sea was never part of a footprint.
        /// </summary>
        public bool[] Footprint => _footprint ?? throw new InvalidOperationException("Footprint mask has been released.");

        /// <summary>Ocean reaches the map border; a lake never does.</summary>
        public WaterBody[] Water => _water ?? throw new InvalidOperationException("Water samples have been packed and released.");

        /// <summary>
        /// The BANK of a river: the ring the river carve painted just outside its own water.
        ///
        /// Written by <see cref="TerrainRiverStage"/> and read by <see cref="TerrainShorelineStage"/>,
        /// so it is a named array rather than one of the shared scratch buffers below - those
        /// explicitly may not be read across a stage boundary.
        ///
        /// It is the carve's own shape dilated, which is what makes a trunk's bank wider than a
        /// stream's for free: a river is a union of discs of radius r along its path, and dilation
        /// distributes over union, so painting r + bank(r) in the same loop is EXACTLY that river
        /// offset by bank(r). Measuring it afterwards instead would need the width of the nearest
        /// river at every land sample - a feature transform - to reach the same answer.
        ///
        /// Holds the bank's THICKNESS in samples, not a flag: the width has to survive to the
        /// renderer or every river's bank is drawn at one width, which is the whole fault this
        /// exists to fix. Zero is "not a bank".
        ///
        /// Lazy: a map with no rivers, or with river banks dialled to zero, never allocates it.
        /// </summary>
        public byte[] RiverBank
        {
            get
            {
                // Throws rather than re-allocating. A bare `??= new byte[Count]` would hand a reader
                // after release a silently EMPTY mask - every bank forgotten, and nothing to say so.
                if (_riverBankReleased) throw new InvalidOperationException("River bank samples have been released.");
                return _riverBank ??= new byte[Count];
            }
        }

        /// <summary>Whether any river bank was marked, without walking the mask or allocating one.</summary>
        public bool HasRiverBank => !_riverBankReleased && _riverBank is not null;

        /// <summary>Normalized 0..1 height above sea level on land.</summary>
        public float[] Elevation => Scratch(ref _elevation);

        /// <summary>Normalized 0..1, 1 being hottest.</summary>
        public float[] Temperature => _climateCompacted || _climateReleased
            ? throw new InvalidOperationException("Fine climate is no longer available after biome classification.")
            : Scratch(ref _temperature);

        /// <summary>Normalized 0..1, 1 being wettest.</summary>
        public float[] Moisture => _climateCompacted || _climateReleased
            ? throw new InvalidOperationException("Fine climate is no longer available after biome classification.")
            : Scratch(ref _moisture);

        /// <summary>Flat, hills or mountains, assigned by elevation percentile.</summary>
        public TerrainRelief[] Relief => Scratch(ref _relief);

        /// <summary>Samples to the nearest water, 0 in water itself.</summary>
        public int[] CoastDistance => _coastReleased
            ? throw new InvalidOperationException("Coast distances are no longer available after river generation.")
            : Scratch(ref _coastDistance);

        /// <summary>Final terrain kind consumed by gameplay and rendering.</summary>
        public string[] Terrain => _terrain ?? throw new InvalidOperationException("Terrain samples have been packed and released.");

        /// <summary>
        /// Multiplier on the painted base colour, 1 being unlit. Relief is
        /// carried here rather than baked into the terrain kind, so a hill can
        /// stay grassland and still read as a hill.
        /// </summary>
        public float[] Shade
        {
            get
            {
                if (_shadeReleased) throw new InvalidOperationException("Shade samples have been packed and released.");
                if (_shade is null)
                {
                    _shade = new float[Count];
                    Array.Fill(_shade, 1f);
                }
                return _shade;
            }
        }

        private float[]? _elevation, _temperature, _moisture, _shade;
        private bool[]? _land, _footprint;
        private byte[]? _riverBank;
        private bool _riverBankReleased;
        private WaterBody[]? _water;
        private string[]? _terrain;
        private bool _shadeReleased;
        private float[]? _cellTemperature, _cellMoisture;
        private TerrainRelief[]? _relief;
        private int[]? _coastDistance;
        private bool _coastReleased, _scratchReleased;
        private bool _climateCompacted, _climateReleased;
        private string[]? _resource, _cellTerrain, _cellInlandTerrain, _feature, _cellLiquidResource, _cellUndergroundResource;
        private WaterBody[]? _cellWater;
        private TerrainRelief[]? _cellRelief;
        private float[]? _cellElevation, _cellShade, _cellUndergroundRichness, _cellShoreWidth;
        private int[]? _cellContinent;
        private byte[]? _cellUndergroundDepth, _cellStartArea;
        private ushort[]? _cellStartDistance;
        private int[]? _intScratchA, _intScratchB;
        private float[]? _floatScratchA, _floatScratchB;
        private bool[]? _boolScratch;
        private byte[]? _byteScratch;
        internal long CellPayloadBytes { get; private set; }

        // Stages own one buffer on one worker. Output arrays need not overlap early-stage scratch.
        private T[] CellValues<T>(ref T[]? values, T initial = default!)
        {
            if (values is not null) return values;
            values = new T[checked(CellsWide * CellsHigh)];
            if (!EqualityComparer<T>.Default.Equals(initial, default!)) Array.Fill(values, initial);
            CellPayloadBytes += values.LongLength * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            return values;
        }

        internal long ScratchPayloadBytes => ((_elevation?.LongLength ?? 0)
            + (_temperature?.LongLength ?? 0) + (_moisture?.LongLength ?? 0)
            + (_coastDistance?.LongLength ?? 0) + (_cellTemperature?.LongLength ?? 0)
            + (_cellMoisture?.LongLength ?? 0)) * 4 + (_relief?.LongLength ?? 0);

        internal float TemperatureAtCell(int cell)
            => _climateReleased ? throw new InvalidOperationException("Climate scratch has been released.")
                : _climateCompacted ? _cellTemperature![cell]
                : Temperature[CellCentreIndex(cell % CellsWide, cell / CellsWide)];

        internal float MoistureAtCell(int cell)
            => _climateReleased ? throw new InvalidOperationException("Climate scratch has been released.")
                : _climateCompacted ? _cellMoisture![cell]
                : Moisture[CellCentreIndex(cell % CellsWide, cell / CellsWide)];

        internal void CompactClimate(CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_climateReleased || _scratchReleased) throw new InvalidOperationException("Climate scratch has been released.");
            if (_climateCompacted) return;
            float[] temperature = Temperature, moisture = Moisture;
            float[] cellTemperature = temperature, cellMoisture = moisture;
            if (SamplesPerCell != 1)
            {
                cellTemperature = new float[CellsWide * CellsHigh];
                cellMoisture = new float[cellTemperature.Length];
                for (int y = 0; y < CellsHigh; y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (int x = 0; x < CellsWide; x++)
                    {
                        int cell = CellIndex(x, y), sample = CellCentreIndex(x, y);
                        cellTemperature[cell] = temperature[sample];
                        cellMoisture[cell] = moisture[sample];
                    }
                }
            }
            cancellation.ThrowIfCancellationRequested();
            _cellTemperature = cellTemperature;
            _cellMoisture = cellMoisture;
            _temperature = _moisture = null;
            _climateCompacted = true;
        }

        internal void ReleaseClimate()
        {
            _temperature = _moisture = _cellTemperature = _cellMoisture = null;
            _climateReleased = true;
        }

        private T[] Scratch<T>(ref T[]? data)
        {
            if (_scratchReleased) throw new InvalidOperationException("Generation scratch has been released.");
            return data ??= new T[Count];
        }

        // Field-sized working arrays the stages share instead of allocating
        // their own. Two of each numeric kind because the drainage network
        // wants two int fields and two float fields at once; one of each mask
        // kind because nothing needs more. A stage may assume NOTHING about
        // their contents on entry - the previous stage left whatever it left -
        // and must not read one across a stage boundary. Before these existed
        // a Huge build allocated 271 MiB across its stages: each walk to the
        // sea built its own distance field, queue and negated land mask, each
        // coherence pass cloned the whole kind field, and every BFS through an
        // iterator allocated a state machine per visited sample.
        public int[] IntScratchA => Scratch(ref _intScratchA);
        public int[] IntScratchB => Scratch(ref _intScratchB);
        public float[] FloatScratchA => Scratch(ref _floatScratchA);
        public float[] FloatScratchB => Scratch(ref _floatScratchB);
        public bool[] BoolScratch => Scratch(ref _boolScratch);
        public byte[] ByteScratch => Scratch(ref _byteScratch);

        internal void ReleaseCoastDistances()
        {
            _coastDistance = null;
            _coastReleased = true;
        }

        internal void ReleaseGenerationScratch()
        {
            ReleaseCoastDistances();
            ReleaseClimate();
            _elevation = null;
            _relief = null;
            _land = _footprint = null;
            // The shoreline stage has already named the bank by now, and this runs while the output
            // packing allocates - a Huge map's mask is another 1.2 MB rooted for no reader.
            _riverBank = null;
            _riverBankReleased = true;
            _intScratchA = _intScratchB = null;
            _floatScratchA = _floatScratchB = null;
            _boolScratch = null;
            _byteScratch = null;
            _scratchReleased = true;
        }

        internal TerrainSampleKinds PackTerrain(CancellationToken cancellation)
        {
            var packed = new TerrainSampleKinds(Terrain, Width, Height, cancellation);
            _terrain = null;
            return packed;
        }

        internal TerrainSampleValues<WaterBody> PackWater(CancellationToken cancellation)
        {
            var packed = new TerrainSampleValues<WaterBody>(Water, Width, Height, cancellation);
            _water = null;
            return packed;
        }

        internal TerrainSampleValues<float> PackShade(CancellationToken cancellation)
        {
            var packed = new TerrainSampleValues<float>(Shade, Width, Height, cancellation);
            _shade = null;
            _shadeReleased = true;
            return packed;
        }

        /// <summary>
        /// Resource per GAMEPLAY CELL, not per sample: a resource is something a
        /// tile has, so storing it per sample would let one tile hold several.
        /// Empty means none.
        /// </summary>
        public string[] Resource => CellValues(ref _resource, string.Empty);

        /// <summary>Fair player start tiles, in gameplay cell coordinates.</summary>
        public List<Vector2I> StartPositions { get; }

        /// <summary>
        /// Which start's reserved area a tile belongs to: 0 none, k+1 start k. Allocated only
        /// by the start-area stage, so a map without start areas carries no array at all.
        /// </summary>
        public byte[] CellStartArea => CellValues(ref _cellStartArea);

        /// <summary>The start-area array when the stage allocated it, else null.</summary>
        internal byte[]? CellStartAreaIfGenerated => _cellStartArea;

        /// <summary>One report per start position when start areas were generated, in start order.</summary>
        public List<TerrainStartAreaReport> StartAreas { get; } = new();

        /// <summary>
        /// Distance from each tile to the nearest start, in whole cells, water included. Allocated
        /// only when StartDistanceScaling asks for it (FEAT-14): two bytes a cell is a real cost on a
        /// huge world, and a map that scales nothing by distance has no reader for it.
        /// </summary>
        public ushort[] CellStartDistance => CellValues(ref _cellStartDistance);

        /// <summary>The start-distance array when the stage measured it, else null.</summary>
        internal ushort[]? CellStartDistanceIfGenerated => _cellStartDistance;

        /// <summary>What the kit's Neutral entries placed between the starts, or None without any.</summary>
        public TerrainNeutralSitesReport NeutralSites { get; set; } = TerrainNeutralSitesReport.None;

        // The gameplay-resolution view of the world. These are the authoritative
        // outputs: one value per tile, which is what a game actually moves,
        // paths and builds on. The sample arrays above exist to decide these
        // well, not to be consumed directly.

        /// <summary>
        /// How wide the INLAND shore band is at each tile, in tiles - the width the painted view
        /// draws its bank with.
        ///
        /// Per cell because a river has no single width: the carve sizes each one from its flow, so
        /// a trunk's bank is broader than a headwater stream's. This used to be one number for the
        /// whole map (TerrainGeneratorComponent.LakeShoreWidth), and handing a lake's 0.65 tiles to
        /// every river on 2026-09-18 buried the map in sand - five times the width of the water it
        /// was edging. The per-cell channel that carries it to the shader already existed; only the
        /// value was constant.
        ///
        /// Zero where there is no inland shore, which is also what switches the band off.
        /// </summary>
        public float[] CellShoreWidth => CellValues(ref _cellShoreWidth);

        /// <summary>Terrain kind per gameplay tile.</summary>
        public string[] CellTerrain => CellValues(ref _cellTerrain, "grass");

        /// <summary>Ground beneath the generated ocean beach, before the coastal inset.</summary>
        public string[] CellInlandTerrain => CellValues<string>(ref _cellInlandTerrain);
        public float BeachWidth { get; set; }
        public float LakeShoreWidth { get; set; }

        /// <summary>Water body per gameplay tile; None means dry land.</summary>
        public WaterBody[] CellWater => CellValues(ref _cellWater);

        /// <summary>Relief per gameplay tile.</summary>
        public TerrainRelief[] CellRelief => CellValues(ref _cellRelief);

        /// <summary>
        /// Land height per tile, 0 to 1, reduced from the sample grid.
        ///
        /// Relief only says flat, hills or mountains, which is enough to decide
        /// what a tile IS and not enough to decide how it looks against its
        /// neighbours: every tile of a range shares one band, so anything drawn
        /// from relief alone is flat-topped. Height is the field that says which
        /// part of a range is its crest.
        /// </summary>
        public float[] CellElevation => CellValues(ref _cellElevation);

        /// <summary>Averaged hillshade per gameplay tile.</summary>
        public float[] CellShade => CellValues(ref _cellShade, 1f);

        /// <summary>Landmass id per gameplay tile; 0 is water.</summary>
        public int[] CellContinent => CellValues(ref _cellContinent);

        /// <summary>
        /// Terrain feature per tile - woods, jungle, marsh, oasis - or empty.
        /// A feature sits ON the terrain rather than replacing it, so a wooded
        /// grassland tile is still grassland underneath.
        /// </summary>
        public string[] Feature => CellValues(ref _feature, string.Empty);

        /// <summary>
        /// Resource in the LIQUID stratum per tile - fish in the water column,
        /// whatever the water kinds are skinned as - or empty. Separate from
        /// Resource so each array keeps one meaning: Resource is the surface
        /// stratum on land.
        /// </summary>
        public string[] CellLiquidResource => CellValues(ref _cellLiquidResource, string.Empty);

        /// <summary>
        /// Resource in the UNDERGROUND stratum per tile - beneath land or
        /// seabed - or empty. Deposits are multi-cell fields, not markers.
        /// </summary>
        public string[] CellUndergroundResource => CellValues(ref _cellUndergroundResource, string.Empty);

        /// <summary>Underground richness 0..1 where a deposit exists, else 0.</summary>
        public float[] CellUndergroundRichness => CellValues(ref _cellUndergroundRichness);

        /// <summary>Underground depth band per tile, as (byte)ResourceDepth.</summary>
        public byte[] CellUndergroundDepth => CellValues(ref _cellUndergroundDepth);

        public int CellIndex(int cellX, int cellY) => (cellY * CellsWide) + cellX;

        /// <summary>
        /// The gameplay tile a SAMPLE belongs to. The reverse of CellCentreIndex, for a stage that
        /// walks samples and records something about the tile they fall in.
        /// </summary>
        public int CellOf(int sample)
        {
            int samples = Mathf.Max(1, SamplesPerCell);
            return CellIndex(
                Mathf.Min((sample % Width) / samples, CellsWide - 1),
                Mathf.Min((sample / Width) / samples, CellsHigh - 1));
        }

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
        /// <param name="y">Sample row.</param>
        /// <param name="offsetSamples">Noise displacement of the climate band, in samples.</param>
        /// <param name="span">Latitude range; one covers both hemispheres.</param>
        /// <param name="centre">Regional latitude centre when span is below one.</param>
        public float Latitude(int y, float offsetSamples, float span, float centre)
        {
            float down = (y + 0.5f + offsetSamples) / Height;
            if (span >= 1.0f)
                return Mathf.Abs((down * 2.0f) - 1.0f);

            return Mathf.Clamp(centre + ((down - 0.5f) * span), 0.0f, 1.0f);
        }

        /// <summary>Kilometres from the equator to a pole: the distance one unit of latitude covers.</summary>
        internal const float EquatorToPoleKilometres = 10000.0f;

        /// <summary>
        /// How far one sample row reaches on the ground, in kilometres, read from the same span
        /// <see cref="Latitude"/> draws the climate bands from: below one, the height covers that
        /// fraction of the equator-to-pole distance; at one, it runs pole to equator to pole.
        ///
        /// Distances that shape climate are measured with this rather than in cells. A cell has
        /// no size of its own - Oilfield Days draws 24 km across 144 of them, and a scale-rules
        /// lab map about 42 km into each - so a reach stated in cells describes a different
        /// climate on every map.
        /// </summary>
        /// <param name="span">Latitude range; one covers both hemispheres.</param>
        public float KilometresPerSample(float span)
            => (span >= 1.0f ? 2.0f : span) * EquatorToPoleKilometres / Height;
    }

    internal enum TerrainRelief : byte
    {
        Flat = 0,
        Hills = 1,
        Mountains = 2,
    }
}
