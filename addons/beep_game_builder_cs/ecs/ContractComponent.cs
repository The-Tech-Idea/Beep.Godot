using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Contract component for players/staff. Blind — works for any entity with a contract.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ContractComponent : GameplayComponent
    {
        [Export] public int WeeklyWage { get; set; } = 5000;
        [Export] public int ContractYears { get; set; } = 3;
        [Export] public int ReleaseClause { get; set; } = 0; // 0 = none
        [Export] public bool IsLoanContract { get; set; } = false;
        [Export] public string ContractExpiry { get; set; } = "2029-06-30";

        [Signal] public delegate void ContractExpiringEventHandler(int monthsRemaining);
        [Signal] public delegate void WageChangedEventHandler(int newWage);
        [Signal] public delegate void ReleaseClauseMetEventHandler(int amount);

        public int MarketValue => WeeklyWage * 52 * ContractYears; // Simplified

        public void ExtendContract(int additionalYears, int newWage = 0)
        {
            ContractYears += additionalYears;
            if (newWage > 0)
            {
                WeeklyWage = newWage;
                EmitSignal(SignalName.WageChanged, newWage);
            }
        }

        public bool CheckReleaseClause(int bidAmount)
        {
            if (ReleaseClause > 0 && bidAmount >= ReleaseClause)
            {
                EmitSignal(SignalName.ReleaseClauseMet, bidAmount);
                return true;
            }
            return false;
        }
    }
}
