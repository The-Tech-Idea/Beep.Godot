using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Injury component. Blind — tracks injury state for any entity (player, staff, even a machine).
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class InjuryComponent : GameplayComponent
    {
        [Export] public bool IsInjured { get; set; } = false;
        [Export] public string InjuryType { get; set; } = "";
        [Export] public int DaysRemaining { get; set; } = 0;
        [Export] public int TotalDays { get; set; } = 0;
        [Export] public float InjuryRisk { get; set; } = 0.05f; // 5% base risk per match/training

        [Signal] public delegate void InjuredEventHandler(string type, int days);
        [Signal] public delegate void RecoveredEventHandler();
        [Signal] public delegate void InjuryProgressEventHandler(int daysRemaining, int totalDays);

        /// <summary>
        /// Apply a new injury. Scale with injury risk factor.
        /// </summary>
        public void ApplyInjury(string type, int baseDays, float riskMultiplier = 1f)
        {
            if (!IsActive || IsInjured) return;
            if (GD.Randf() > InjuryRisk * riskMultiplier) return; // Dodged it

            IsInjured = true;
            InjuryType = type;
            TotalDays = baseDays + (int)GD.RandRange(-3, 5);
            DaysRemaining = TotalDays;
            EmitSignal(SignalName.Injured, type, TotalDays);
        }

        /// <summary>
        /// Advance recovery by one day. Call from a day-advance system.
        /// </summary>
        public void AdvanceDay()
        {
            if (!IsInjured || !IsActive) return;
            DaysRemaining--;
            EmitSignal(SignalName.InjuryProgress, DaysRemaining, TotalDays);
            if (DaysRemaining <= 0)
            {
                IsInjured = false;
                InjuryType = "";
                EmitSignal(SignalName.Recovered);
            }
        }
    }
}
