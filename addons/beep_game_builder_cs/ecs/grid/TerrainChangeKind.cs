namespace Beep.ECS
{
    /// <summary>
    /// What a <see cref="GridCellDataComponent"/> change actually touched, carried on
    /// the <c>CellsChanged</c> signal so a listener rebuilds only what moved.
    ///
    /// The distinction that matters most is <see cref="Residency"/> versus the content
    /// flags: a chunk evicted to the archive, or reloaded from it unchanged, moves
    /// nothing a renderer draws or a search reads - only where the data lives. Treating
    /// that as "the map changed" is what made an actor walking out of one chunk's pin
    /// radius rebuild every renderer on the map. A listener ignores a Residency-only
    /// change and acts on any of the content flags.
    /// </summary>
    [System.Flags]
    public enum TerrainChangeKind
    {
        None = 0,
        /// <summary>A chunk left or entered the resident set; its cells are unchanged.</summary>
        Residency = 1,
        /// <summary>Terrain kind, elevation or shoreline - what the surface renderers draw.</summary>
        Terrain = 2,
        /// <summary>Blocked, relief or ramp - what a navigation search reads.</summary>
        Navigation = 4,
        /// <summary>Crop, tilled, watered or metadata - gameplay state, not terrain.</summary>
        Gameplay = 8,
        /// <summary>Any real content change, as opposed to a residency move.</summary>
        Content = Terrain | Navigation | Gameplay,
    }
}
