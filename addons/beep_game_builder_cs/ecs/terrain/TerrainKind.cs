using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One terrain kind's meaning - what "grass" or "rock" IS, independent of the string id that
    /// names it. The generator and the views read a kind's properties from here instead of from a
    /// dozen private tables, so a kind (or a game's new kind) is defined in one place. Properties are
    /// folded in one at a time as each table that used to own them is retired (DUP-13); the string
    /// ids themselves never change - cells, saves and shaders still speak "grass".
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TerrainKind : Resource
    {
        /// <summary>The kind's string id, e.g. "grass". This is what cells and saves store.</summary>
        [Export] public string Id { get; set; } = "";

        /// <summary>Whether a start position may be placed on this kind. Snow, ice, rock and lava are
        /// not startable; every other land kind is (water and mountain-relief cells are excluded by
        /// separate checks, not by kind).</summary>
        [Export] public bool Startable { get; set; } = true;
    }
}
