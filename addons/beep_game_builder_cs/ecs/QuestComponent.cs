using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Quest tracking system. Define objectives as QuestObjective resources.
    /// Progress objectives by calling ProgressObjective(targetId, amount).
    /// Emits signals when objectives complete or the whole quest is done.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class QuestComponent : GameplayComponent
    {
        [Export] public QuestObjective[] Objectives { get; set; } = System.Array.Empty<QuestObjective>();
        [Export] public string QuestName { get; set; } = "New Quest";

        [Signal] public delegate void ObjectiveCompletedEventHandler(int index);
        [Signal] public delegate void QuestCompletedEventHandler();
        [Signal] public delegate void QuestFailedEventHandler();
        [Signal] public delegate void ObjectiveProgressEventHandler(int index, int current, int required);

        public bool IsComplete { get; private set; }

        // Per-instance progress, indexed to Objectives. Lives HERE, not on the QuestObjective
        // resources — those are shared, authored definitions; writing progress onto them would make
        // two quests sharing an objective share a count, and bake run state into the .tres.
        private int[] _counts = System.Array.Empty<int>();

        public override void _Ready()
        {
            base._Ready();
            EnsureCounts();
        }

        private void EnsureCounts()
        {
            if (_counts.Length != Objectives.Length) _counts = new int[Objectives.Length];
        }

        /// <summary>Current progress toward objective <paramref name="index"/>.</summary>
        public int GetProgress(int index) => (index >= 0 && index < _counts.Length) ? _counts[index] : 0;

        /// <summary>Whether objective <paramref name="index"/> has met its RequiredCount.</summary>
        public bool IsObjectiveComplete(int index)
            => index >= 0 && index < Objectives.Length && GetProgress(index) >= Objectives[index].RequiredCount;

        /// <summary>Progress an objective by its target ID. Auto-completes when count met.</summary>
        public void ProgressObjective(string targetId, int amount = 1)
        {
            if (!IsActive || IsComplete) return;
            EnsureCounts();
            for (int i = 0; i < Objectives.Length; i++)
            {
                if (Objectives[i].TargetId == targetId && !IsObjectiveComplete(i))
                {
                    _counts[i] += amount;
                    EmitSignal(SignalName.ObjectiveProgress, i, _counts[i], Objectives[i].RequiredCount);
                    if (IsObjectiveComplete(i))
                    {
                        EmitSignal(SignalName.ObjectiveCompleted, i);
                        CheckAllComplete();
                    }
                    return;
                }
            }
        }

        /// <summary>Force-complete a specific objective by index.</summary>
        public void CompleteObjective(int index)
        {
            if (index < 0 || index >= Objectives.Length) return;
            EnsureCounts();
            if (!IsObjectiveComplete(index))
            {
                _counts[index] = Objectives[index].RequiredCount;
                EmitSignal(SignalName.ObjectiveCompleted, index);
                CheckAllComplete();
            }
        }

        private void CheckAllComplete()
        {
            for (int i = 0; i < Objectives.Length; i++)
                if (!IsObjectiveComplete(i)) return;
            IsComplete = true;
            EmitSignal(SignalName.QuestCompleted);
        }
    }

    /// <summary>
    /// A single quest objective. Drag-and-drop on QuestComponent.Objectives.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class QuestObjective : Resource
    {
        public enum ObjectiveType { Kill, Collect, Reach, Talk, Escort, Survive }

        [Export] public string Description { get; set; } = "New Objective";
        [Export] public ObjectiveType Type { get; set; } = ObjectiveType.Kill;
        [Export] public string TargetId { get; set; } = "";
        [Export] public int RequiredCount { get; set; } = 1;

        // No CurrentCount / IsComplete / Progress here: this is the shared DEFINITION. Per-instance
        // progress lives on QuestComponent (GetProgress / IsObjectiveComplete), because a `.tres`
        // shared across quests must not carry run state.
    }
}
