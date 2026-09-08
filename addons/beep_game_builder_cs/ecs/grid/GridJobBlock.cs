namespace Beep.ECS
{
    /// <summary>
    /// Why a cell cannot take a job, as decided by <see cref="GridCellRules"/>.
    ///
    /// A reason, not a message: the two components that ask - the click/toolbar
    /// path and the settler-style selection path - report the same condition in
    /// their own signal vocabulary ("unworkable_terrain" vs
    /// "unqueueable_terrain"), and those words are each component's public API.
    /// One rule, two vocabularies.
    /// </summary>
    internal enum GridJobBlock
    {
        /// <summary>Outside the navigation grid's bounds.</summary>
        OutOfBounds,

        /// <summary>Blocked, by navigation or by the cell's own Blocked flag.</summary>
        Blocked,

        /// <summary>The terrain kind here is not one that can be worked.</summary>
        UnworkableTerrain
    }
}
