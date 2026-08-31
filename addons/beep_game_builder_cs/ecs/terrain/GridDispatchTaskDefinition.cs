using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One dispatchable job: send a vehicle somewhere, and change the world when
    /// it arrives.
    ///
    /// This is the shape every task in a settlers-style loop shares - clear the
    /// brush, hoe the plots, water them, plant, harvest, lay a road. They differ
    /// only in where the vehicle goes, what becomes visible or hidden, and what
    /// the wallet gains. A demo controller had all eight as a switch over button
    /// names with literal screen coordinates in the cases, so adding a ninth
    /// meant editing C# and nobody could see the set at once.
    /// </summary>
    [GlobalClass]
    public partial class GridDispatchTaskDefinition : Resource
    {
        /// <summary>Matches the NAME of the button that requests it.</summary>
        [Export] public string Action { get; set; } = "Clear";

        /// <summary>Shown while the task runs, and again with "complete" after.</summary>
        [Export] public string Label { get; set; } = "Working";

        /// <summary>Which vehicle goes, and where it goes to.</summary>
        [Export] public NodePath VehiclePath { get; set; } = new("");
        [Export] public Vector2 Target { get; set; } = Vector2.Zero;

        [ExportGroup("On Arrival")]
        [Export] public Godot.Collections.Array<NodePath> Show { get; set; } = new();
        [Export] public Godot.Collections.Array<NodePath> Hide { get; set; } = new();

        /// <summary>
        /// Recolours one node - cleared ground turning to bare earth, watered
        /// plots turning blue. Off unless RecolourTarget is set.
        /// </summary>
        [Export] public NodePath RecolourTarget { get; set; } = new("");
        [Export] public Color Recolour { get; set; } = Colors.White;

        /// <summary>
        /// What the wallet gains, or loses when negative - road stone is spent,
        /// timber and coin are earned. Empty id pays nothing.
        /// </summary>
        [Export] public string RewardResourceId { get; set; } = "";
        [Export] public int RewardAmount { get; set; }
    }
}
