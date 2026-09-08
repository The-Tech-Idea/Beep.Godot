using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The one implementation of "can this cell be worked, and can it take a
    /// job" - built by a component from its own exports and asked at the point
    /// of decision.
    ///
    /// GridToolActionComponent (the click/toolbar path) and
    /// GridSelectionJobCommandComponent (the settler-style selection path) used
    /// to derive this independently, and the copies had already diverged: only
    /// the selection path consulted GridCellDataComponent's Blocked flag, and
    /// only the tool path consulted the terrain engine's generated map. A
    /// project wiring both a farming toolbar and a selection command got two
    /// different answers for the same cell, with nothing anywhere documenting
    /// the asymmetry as intended. Same consolidation GridTerrainRules got for
    /// terrain-kind meaning, one layer up.
    ///
    /// CELLS ARE THE MAP. The live terrain kind of a cell has one owner,
    /// GridCellDataComponent: it is what the generator fills, what the player
    /// edits, and what the save carries. The terrain engine's data layers are
    /// a projection of the GENERATED world - the recipe's answer, not the live
    /// map - and are never consulted for kind here. They used to be, and won:
    /// a rebuilt map overrode every restored or edited cell wherever it had a
    /// tile, so an edit survived a save only where the layers had nothing to
    /// say. OpenTTD and Widelands each keep ONE tile array that is the saved
    /// map; this is that array.
    /// </summary>
    internal readonly struct GridCellRules
    {
        public GridNavigationComponent? Navigation { get; init; }
        public GridCellDataComponent? Cells { get; init; }

        public bool UseNavigationBounds { get; init; }
        public bool RejectNavigationBlockedCells { get; init; }
        public bool RejectCellDataBlockedCells { get; init; }
        public bool TreatBlockedTerrainKindsAsBlocking { get; init; }
        public Godot.Collections.Array<string> BlockedTerrainKinds { get; init; }
        public Godot.Collections.Array<string> AllowedTerrainKinds { get; init; }

        /// <summary>
        /// The terrain kind in force at a cell, normalized, from the one owner.
        /// Empty with no cell data wired. Static so GridPlacementComponent -
        /// whose own placement rule is genuinely different, and stays its own -
        /// and GridNavigationComponent's search snapshot still read terrain the
        /// one way rather than each carrying a copy of the precedence.
        /// </summary>
        public static string TerrainKindAt(GridCellDataComponent? cells, Vector2I cell)
            => cells is null ? "" : GridTerrainRules.Normalize(cells.GetTerrainKind(cell));

        public string TerrainKindAt(Vector2I cell)
            => TerrainKindAt(Cells, cell);

        /// <summary>
        /// Whether the land itself allows work here - bounds and terrain kind
        /// only. With no cell data wired there is nothing to judge by and the
        /// answer is yes.
        ///
        /// An emptied BlockedTerrainKinds means nothing is blocked, the same as
        /// every other consumer of that export.
        /// </summary>
        public bool CanWorkTerrain(Vector2I cell)
        {
            if (Navigation != null && UseNavigationBounds && !Navigation.IsInBounds(cell))
                return false;

            if (Cells == null)
                return true;

            string terrainKind = TerrainKindAt(cell);
            if (!GridTerrainRules.IsAllowed(terrainKind, AllowedTerrainKinds))
                return false;

            return !TreatBlockedTerrainKindsAsBlocking
                || !GridTerrainRules.MatchesAny(terrainKind, BlockedTerrainKinds);
        }

        /// <summary>Why this cell cannot take a job, or null when it can.</summary>
        public GridJobBlock? QueueBlock(Vector2I cell)
        {
            if (Navigation != null)
            {
                if (UseNavigationBounds && !Navigation.IsInBounds(cell))
                    return GridJobBlock.OutOfBounds;

                if (RejectNavigationBlockedCells && Navigation.IsBlocked(cell))
                    return GridJobBlock.Blocked;
            }

            if (RejectCellDataBlockedCells && Cells != null
                && Cells.HasFlag(cell, GridCellDataComponent.CellFlags.Blocked))
                return GridJobBlock.Blocked;

            return CanWorkTerrain(cell) ? null : GridJobBlock.UnworkableTerrain;
        }
    }
}
