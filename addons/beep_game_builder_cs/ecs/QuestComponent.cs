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

        /// <summary>Progress an objective by its target ID. Auto-completes when count met.</summary>
        public void ProgressObjective(string targetId, int amount = 1)
        {
            if (!IsActive || IsComplete) return;
            for (int i = 0; i < Objectives.Length; i++)
            {
                if (Objectives[i].TargetId == targetId && !Objectives[i].IsComplete)
                {
                    Objectives[i].CurrentCount += amount;
                    EmitSignal(SignalName.ObjectiveProgress, i, Objectives[i].CurrentCount, Objectives[i].RequiredCount);
                    if (Objectives[i].IsComplete)
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
            if (!Objectives[index].IsComplete)
            {
                Objectives[index].CurrentCount = Objectives[index].RequiredCount;
                EmitSignal(SignalName.ObjectiveCompleted, index);
                CheckAllComplete();
            }
        }

        private void CheckAllComplete()
        {
            foreach (var obj in Objectives)
                if (!obj.IsComplete) return;
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
        [Export] public int CurrentCount { get; set; } = 0;

        public bool IsComplete => CurrentCount >= RequiredCount;
        public float Progress => RequiredCount > 0 ? Mathf.Clamp((float)CurrentCount / RequiredCount, 0f, 1f) : 1f;
    }
}
