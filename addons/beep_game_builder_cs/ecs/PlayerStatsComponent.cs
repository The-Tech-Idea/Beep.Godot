using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Player statistics component. Blind — attach to any player entity regardless of club/league.
    /// The owning entity configures and reads values. Systems use groups to process squads.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class PlayerStatsComponent : EntityComponent
    {
        [ExportGroup("Technical")]
        [Export] public int Shooting { get; set; } = 50;
        [Export] public int Passing { get; set; } = 50;
        [Export] public int Dribbling { get; set; } = 50;
        [Export] public int Tackling { get; set; } = 50;
        [Export] public int Keeping { get; set; } = 50;

        [ExportGroup("Physical")]
        [Export] public int Speed { get; set; } = 50;
        [Export] public int Stamina { get; set; } = 50;
        [Export] public int Strength { get; set; } = 50;

        [ExportGroup("Mental")]
        [Export] public int Vision { get; set; } = 50;
        [Export] public int Positioning { get; set; } = 50;
        [Export] public int Composure { get; set; } = 50;

        [ExportGroup("Identity")]
        [Export] public string PlayerName { get; set; } = "Player";
        [Export] public int Age { get; set; } = 23;
        [Export] public int ShirtNumber { get; set; } = 1;
        [Export] public string Position { get; set; } = "CM";

        [ExportGroup("Potential")]
        [Export] public int PotentialRating { get; set; } = 3; // 1-5 stars
        [Export] public bool PotentialRevealed { get; set; } = false;

        [Signal] public delegate void StatsChangedEventHandler(string statName, int newValue);
        [Signal] public delegate void PotentialDiscoveredEventHandler(int stars);

        /// <summary>
        /// Calculate overall rating from all stats (0-100).
        /// </summary>
        public int OverallRating => (Shooting + Passing + Dribbling + Tackling + Keeping +
            Speed + Stamina + Strength + Vision + Positioning + Composure) / 11;

        public void SetStat(string name, int value)
        {
            int clamped = Mathf.Clamp(value, 1, 99);
            switch (name)
            {
                case "Shooting": Shooting = clamped; break;
                case "Passing": Passing = clamped; break;
                case "Dribbling": Dribbling = clamped; break;
                case "Tackling": Tackling = clamped; break;
                case "Keeping": Keeping = clamped; break;
                case "Speed": Speed = clamped; break;
                case "Stamina": Stamina = clamped; break;
                case "Strength": Strength = clamped; break;
                case "Vision": Vision = clamped; break;
                case "Positioning": Positioning = clamped; break;
                case "Composure": Composure = clamped; break;
                default: return;
            }
            EmitSignal(SignalName.StatsChanged, name, clamped);
        }
    }
}
