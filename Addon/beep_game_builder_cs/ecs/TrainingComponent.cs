using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Training participation component. Blind — works for any entity that trains.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TrainingComponent : EntityComponent
    {
        [Export] public float Participation { get; set; } = 0.5f; // 0.0 to 1.0
        [Export] public string FocusArea { get; set; } = "Balanced";
        [Export] public int DaysTrainedThisWeek { get; set; } = 0;
        [Export] public bool IsImproving { get; set; } = false;
        [Export] public int WeeklyImprovementPoints { get; set; } = 0;

        public enum TrainingFocus { Attacking, Defensive, Balanced, Intensive }

        [Signal] public delegate void ImprovedEventHandler(string stat, int amount);
        [Signal] public delegate void FatiguedEventHandler();
        [Signal] public delegate void TrainingCompletedEventHandler(int daysThisWeek);

        public void Train(TrainingFocus focus, double intensity = 1.0)
        {
            if (!IsActive || IsInjured) return;
            Participation += (float)(0.03 * intensity);
            Participation = Mathf.Clamp(Participation, 0f, 1f);
            DaysTrainedThisWeek++;

            if (Participation > 0.7f) IsImproving = true;
            if (Participation > 0.95f) EmitSignal(SignalName.Fatigued);
        }

        public void EndWeek()
        {
            EmitSignal(SignalName.TrainingCompleted, DaysTrainedThisWeek);
            DaysTrainedThisWeek = 0;
            IsImproving = false;
        }

        // Quick check — parent entity should have an InjuryComponent sibling
        private bool IsInjured
        {
            get
            {
                var injury = GetSiblingComponent<InjuryComponent>();
                return injury != null && injury.IsInjured;
            }
        }
    }
}
