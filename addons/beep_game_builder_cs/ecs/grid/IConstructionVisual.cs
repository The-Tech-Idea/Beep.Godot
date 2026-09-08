using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// A scene that PRESENTS a structure while it is under construction -
    /// a construction-stage scene listed in GridBuildDefinition.ConstructionStages,
    /// or the build's own Scene root. The grid layer never draws: it tells
    /// the scene the facts of the site through this contract, and the scene
    /// renders them however it likes - tile a material swatch to the
    /// footprint, scale pre-rendered art, drive a reveal shader, play an
    /// animation. The shipped construction_stage.gd is one implementation;
    /// a project's own scene is another.
    ///
    /// The facts are the ones real construction sites are read by (see
    /// docs/grid-system/CONSTRUCTION_VISUALS_RESEARCH.md): how big the site
    /// is, what STATE it is in (a staked plot waiting for materials looks
    /// different from a site nobody is working, which looks different from
    /// one being built), what materials are on it, how far along it is, and
    /// when a hit of work actually lands.
    ///
    /// A GDScript scene cannot implement a C# interface (the same limitation
    /// the port contracts document). GridBuildStageVisualComponent reads
    /// this shape by NAME as well - see GridConstructionVisualPorts - so a
    /// GDScript scene participates by exposing properties with these exact
    /// PascalCase names. Every member is optional to a scene: what it does
    /// not expose it is simply not told.
    /// </summary>
    public interface IConstructionVisual
    {
        /// <summary>How many grid cells the site occupies. Set before the
        /// scene enters the tree, so _ready can lay itself out.</summary>
        Vector2I SiteFootprint { get; set; }

        /// <summary>The world size of one grid cell, in pixels. Set before
        /// the scene enters the tree.</summary>
        Vector2 CellSize { get; set; }

        /// <summary>
        /// Which entry of the build's ConstructionStages this instance is,
        /// and how many there are. A scene listed once (StageCount 1) draws
        /// the whole build from BuildProgress; a scene listed among several
        /// is shown for BuildProgress in [StageIndex/StageCount, (StageIndex+1)/StageCount).
        /// </summary>
        int StageIndex { get; set; }
        int StageCount { get; set; }

        /// <summary>
        /// "pending" (placed, waiting for RequiredMaterials to arrive),
        /// "queued" (job exists, no worker has claimed it), "working"
        /// (a worker holds the job), "complete" (finished; the scene lives a
        /// moment longer to tear its site down).
        /// </summary>
        string SiteState { get; set; }

        /// <summary>
        /// The physical stock on site: one Dictionary per RequiredMaterials
        /// entry with "id" (string), "required" (int) and "delivered" (int).
        /// While pending, delivered is what the site's storage holds; once
        /// the job starts the stock is built into the structure, so a scene
        /// shows it shrinking with BuildProgress.
        /// </summary>
        Godot.Collections.Array SiteMaterials { get; set; }

        /// <summary>Build progress, 0 at the start of the job and 1 at
        /// completion, pushed by the grid layer as work advances.</summary>
        float BuildProgress { get; set; }

        /// <summary>
        /// A hit of work landed on the site - progress advanced while a
        /// worker held the job. The scene plays whatever a hit looks and
        /// sounds like for its material at the point being worked; nothing
        /// about the worker is assumed.
        /// </summary>
        void WorkPulse();
    }
}
