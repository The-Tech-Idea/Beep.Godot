using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// What the sea LOOKS like, for every view of one world.
    ///
    /// One map has one ocean. Three renderers used to export the same thirteen dials and the same
    /// four texture paths, each with its own defaults, and the tile view's differed from the other
    /// two - foam at 1.0 against 0.50, deep at 6.0 against 4.5, shallows at 6.0 against 1.8 - so
    /// one map drawn twice grew two seas. Each view's comment said the other exposed "the same
    /// dials"; nothing made them the same numbers.
    ///
    /// The dials live here, once, and a scene assigns this resource to its world the way it
    /// assigns <see cref="TerrainPropSizing"/> or <see cref="TerrainMapArt"/>. A scene that wants
    /// its own sea authors its own .tres and assigns it - a scene preference, rather than a
    /// renderer default that quietly disagrees with the renderer next to it.
    ///
    /// WHAT IS NOT HERE, because it is genuinely per surface rather than per look: the coast
    /// window each view resolves for itself (CoastRangeTiles/CoastDetail), the transparent-surface
    /// uniforms only a floating water sheet has (MaxOpacity, ClarityTiles, LakeOpacity,
    /// ShoreOpacity), and the block view's seabed and overscan.
    ///
    /// The defaults are the shader author's own values, which are what the painted and block views
    /// already drew.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainWaterLook : Resource
    {
        /// <summary>
        /// How heavy the sea is: 0 a millpond, 1 an ordinary day, 2 a storm. One dial rather than
        /// several, because big waves are not just brighter foam - the surf reaches further out,
        /// the crests broaden, and the wash runs further up the sand.
        /// </summary>
        [Export(PropertyHint.Range, "0,2,0.05")] public float WaveIntensity { get; set; } = 1.0f;

        /// <summary>How bright the surf paints, 0 for none.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float FoamStrength { get; set; } = 0.50f;

        /// <summary>Tiles from the shore over which the sandy bottom shows through.</summary>
        [Export(PropertyHint.Range, "0,8,0.1")] public float ShallowTiles { get; set; } = 1.8f;

        /// <summary>Tiles from the shore at which the water reaches full depth colour.</summary>
        [Export(PropertyHint.Range, "0.5,12,0.1")] public float DeepTiles { get; set; } = 4.5f;

        /// <summary>Tiles per sandy-seabed texture repeat, under the shallows.</summary>
        [Export(PropertyHint.Range, "1,32,0.5")] public float GroundTextureTiles { get; set; } = 12.0f;

        /// <summary>Tiles per animated water-texture repeat.</summary>
        [Export(PropertyHint.Range, "1,32,0.5")] public float WaterTextureTiles { get; set; } = 6.0f;

        /// <summary>Tiles covered by one repeat of the foam texture ALONG the shore.</summary>
        [Export(PropertyHint.Range, "1,48,0.5")] public float FoamTilesAlong { get; set; } = 11.0f;

        /// <summary>
        /// Tiles covered by one repeat ACROSS the shore. Short: the surf band is under a tile
        /// deep, and a repeat spread over many tiles parks the sheet's crest bands outside it.
        /// </summary>
        [Export(PropertyHint.Range, "0.3,8,0.1")] public float FoamTilesAcross { get; set; } = 1.6f;

        /// <summary>How fast the authored crests advance onto the beach.</summary>
        [Export(PropertyHint.Range, "0,4,0.01")] public float FoamScroll { get; set; } = 0.055f;

        /// <summary>How strongly the surf pulses as crests arrive, 0 for a steady band.</summary>
        [Export(PropertyHint.Range, "0,1,0.05")] public float FoamPulse { get; set; } = 0.34f;

        /// <summary>How fast arriving crests follow one another.</summary>
        [Export(PropertyHint.Range, "0,4,0.05")] public float FoamArrivalRate { get; set; } = 0.9f;

        /// <summary>Direction the swell travels, in degrees, y-down screen space.</summary>
        [Export(PropertyHint.Range, "0,360,1")] public float SwellDirectionDegrees { get; set; } = 210.0f;

        /// <summary>How strongly surf favours coasts facing the swell. 0 puts surf on every shore alike.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float SwellDirectionality { get; set; } = 0.65f;

        [ExportGroup("Textures")]
        /// <summary>Seabed under the shallows. Read by the views that draw a water surface.</summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string ShallowTexturePath { get; set; } = "";

        /// <summary>Open-water bed, past the shallows.</summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string DeepTexturePath { get; set; } = "";

        /// <summary>
        /// Sand where the SEABED meets the beach, under the water surface. Named for the seabed
        /// because the painted renderer has its own <c>SandTexturePath</c> for the LAND material
        /// of the same name, pointing at different art: one is the beach a unit walks on, the
        /// other is the bottom seen through the shallows. They bind the same shader uniform from
        /// different views, so one name for both would have quietly swapped a demo's beach.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string SeabedSandTexturePath { get; set; } = "";

        /// <summary>
        /// A strip of equal frames of authored foam, sampled by distance from the waterline rather
        /// than stamped per tile, so the coastline stays the smooth one the distance field
        /// describes. It needs a SOFT fringe - a flat cutout silhouette collapses to one solid
        /// band. Empty for generated crests.
        /// </summary>
        [Export(PropertyHint.File, "*.png,*.webp")] public string FoamSheetPath { get; set; } = "";

        /// <summary>
        /// The shared block as <see cref="TerrainWaterMaterial"/> takes it. The per-surface facts -
        /// the map rectangle and the coast window the caller built its field with - stay the
        /// caller's, because they are not part of how the sea looks.
        /// </summary>
        internal TerrainWaterMaterial.Settings Settings(Vector2I size, Vector2I origin, float coastRange)
            => new(
                Size: size,
                Origin: origin,
                CoastRange: coastRange,
                GroundTextureTiles: GroundTextureTiles,
                WaterTextureTiles: WaterTextureTiles,
                WaveIntensity: WaveIntensity,
                FoamStrength: FoamStrength,
                ShallowTiles: ShallowTiles,
                DeepTiles: DeepTiles,
                FoamTilesAlong: FoamTilesAlong,
                FoamTilesAcross: FoamTilesAcross,
                FoamScroll: FoamScroll,
                FoamPulse: FoamPulse,
                FoamArrivalRate: FoamArrivalRate,
                SwellDirectionDegrees: SwellDirectionDegrees,
                SwellDirectionality: SwellDirectionality);

        /// <summary>
        /// The look a view draws with when its world assigns none: the shipped defaults, shared by
        /// every view so an unassigned scene still draws ONE sea rather than three.
        /// </summary>
        internal static TerrainWaterLook Shared { get; } = new();
    }
}
