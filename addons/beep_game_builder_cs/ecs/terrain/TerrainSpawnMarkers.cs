using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Player starts as ORDINARY SCENE NODES: a <c>Spawns</c> node of <c>Start_&lt;k&gt;</c>
    /// Marker2Ds, one per start, standing on that start's headquarters anchor.
    ///
    /// This is what lets a generated map be saved as a .tscn and opened as a plain Godot scene
    /// without losing its starts. The per-cell reservation (terrain_start_area) and the start
    /// order published by the data layers both need the generator, or the Beep gameplay
    /// baseline, to exist; a marker needs neither, which is exactly the native-map profile
    /// docs/game-builder/TILEMAP_OUTPUT.md requires. A designer can also drag one of these in
    /// an authored map, and GridStartAreaComponent reads it the same way.
    ///
    /// The names and metadata keys here are the contract, so they live in one place rather than
    /// being spelled out again at each end. Positions are written in the MAP ROOT's space: the
    /// root is what gets packed and instanced, and a marker measured in some intermediate
    /// node's space moves when that node does.
    /// </summary>
    internal static class TerrainSpawnMarkers
    {
        /// <summary>Conventional name of the node holding the markers, under the map root.</summary>
        public const string RootName = "Spawns";

        /// <summary>Marker name prefix; the suffix is the start index, so Start_0 is start 0.</summary>
        public const string NamePrefix = "Start_";

        /// <summary>Which start a marker is - its index in the generator's start order.</summary>
        public const string IndexMeta = "start_index";

        /// <summary>The headquarters footprint the start was validated for, as a Vector2I.</summary>
        public const string FootprintMeta = "hq_footprint";

        /// <summary>Set on a start whose area the generator reported as unplayable.</summary>
        public const string UnusableMeta = "unusable";

        /// <summary>
        /// Writes one marker per start under <paramref name="spawnsRoot"/>, replacing the markers
        /// of a previous emission (a rebuild has fewer or more starts than the last one), and
        /// returns how many were written.
        ///
        /// Cells are generator-local, as the reports carry them; <paramref name="boundsOrigin"/>
        /// is the generator's bounds origin, which makes them the absolute cells the grid draws.
        /// A grid that cannot place a cell (no geometry bound) writes no marker for it rather
        /// than a marker at a NaN position.
        /// </summary>
        public static int Emit(
            Node2D spawnsRoot,
            IReadOnlyList<TerrainStartAreaReport> starts,
            GridProjectionComponent grid,
            Vector2I boundsOrigin)
        {
            Clear(spawnsRoot);

            int written = 0;
            foreach (TerrainStartAreaReport start in starts)
            {
                Vector2 world = grid.CellToWorld(boundsOrigin + start.Origin);
                if (!world.IsFinite())
                {
                    GD.PushWarning($"[{spawnsRoot.Name}] start {start.Index} has no position on the bound grid; no marker was written for it.");
                    continue;
                }

                var marker = new Marker2D
                {
                    Name = NamePrefix + start.Index,
                    Position = spawnsRoot.ToLocal(world),
                };
                marker.SetMeta(IndexMeta, start.Index);
                marker.SetMeta(FootprintMeta, start.Footprint);
                // Recorded rather than dropped: an unplayable start is a fact about the map the
                // workflow review requires reported, and a native map has no report to read.
                if (!start.Usable) marker.SetMeta(UnusableMeta, true);
                spawnsRoot.AddChild(marker);
                written++;
            }
            return written;
        }

        /// <summary>Removes every emitted marker, leaving anything else under the node alone.</summary>
        public static void Clear(Node spawnsRoot)
        {
            foreach (Node child in spawnsRoot.GetChildren())
                if (child is Marker2D && child.Name.ToString().StartsWith(NamePrefix, System.StringComparison.Ordinal))
                {
                    spawnsRoot.RemoveChild(child);
                    child.QueueFree();
                }
        }

        /// <summary>
        /// The marker for start <paramref name="index"/>, or null. Read by its metadata, not its
        /// name: the name is the convention a human reads, the metadata is what survives a
        /// rename in an authored scene.
        /// </summary>
        public static Marker2D? Find(Node spawnsRoot, int index)
        {
            foreach (Node child in spawnsRoot.GetChildren())
                if (child is Marker2D marker && marker.HasMeta(IndexMeta)
                    && marker.GetMeta(IndexMeta).AsInt32() == index)
                    return marker;
            return null;
        }
    }
}
