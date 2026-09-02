using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Tracks objective activation, progress, completion, and save/load state
    /// for top-down and isometric builder games.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridObjectiveTrackerComponent : Node, ISaveable
    {
        [Signal] public delegate void ObjectiveActivatedEventHandler(string objectiveId, bool active);
        [Signal] public delegate void ObjectiveProgressChangedEventHandler(string objectiveId, int progress, int target);
        [Signal] public delegate void ObjectiveCompletedEventHandler(string objectiveId);

        [Export] public Godot.Collections.Array Objectives { get; set; } = new();
        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_world.objectives";
        [Export] public bool AutoActivateAll { get; set; } = false;

        private readonly Dictionary<string, ObjectiveState> _states = new();

        public override void _Ready()
        {
            EnsureStates();
            if (!Engine.IsEditorHint() && ParticipatesInSave)
                AddToGroup(SaveableHelper.Group);
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return new[] { "SaveKey must not be empty." };
            return Array.Empty<string>();
        }

        public bool SetObjectiveActive(string objectiveId, bool active)
        {
            ObjectiveState? state = StateFor(objectiveId);
            if (state == null)
                return false;

            if (state.Active == active)
                return true;

            state.Active = active;
            EmitSignal(SignalName.ObjectiveActivated, state.ObjectiveId, active);
            return true;
        }

        public bool AddProgress(string objectiveId, int amount = 1)
        {
            ObjectiveState? state = StateFor(objectiveId);
            if (state == null || !state.Active || state.Completed)
                return false;

            return SetProgress(state.ObjectiveId, state.Progress + amount);
        }

        public bool SetProgress(string objectiveId, int progress)
        {
            ObjectiveState? state = StateFor(objectiveId);
            GridObjectiveDefinition? definition = DefinitionFor(objectiveId);
            if (state == null || definition == null)
                return false;

            int target = definition.EffectiveTargetCount;
            int next = Mathf.Clamp(progress, 0, target);
            if (state.Progress == next && state.Completed == (next >= target))
                return true;

            state.Progress = next;
            EmitSignal(SignalName.ObjectiveProgressChanged, state.ObjectiveId, state.Progress, target);

            if (definition.AutoComplete && state.Progress >= target)
                CompleteObjective(state.ObjectiveId);

            return true;
        }

        public bool CompleteObjective(string objectiveId)
        {
            ObjectiveState? state = StateFor(objectiveId);
            GridObjectiveDefinition? definition = DefinitionFor(objectiveId);
            if (state == null || definition == null)
                return false;

            state.Progress = Mathf.Max(state.Progress, definition.EffectiveTargetCount);
            if (state.Completed)
                return true;

            state.Completed = true;
            EmitSignal(SignalName.ObjectiveProgressChanged, state.ObjectiveId, state.Progress, definition.EffectiveTargetCount);
            EmitSignal(SignalName.ObjectiveCompleted, state.ObjectiveId);
            return true;
        }

        public bool ResetObjective(string objectiveId)
        {
            ObjectiveState? state = StateFor(objectiveId);
            GridObjectiveDefinition? definition = DefinitionFor(objectiveId);
            if (state == null || definition == null)
                return false;

            state.Progress = 0;
            state.Completed = false;
            state.Active = AutoActivateAll || definition.ActiveOnStart;
            EmitSignal(SignalName.ObjectiveProgressChanged, state.ObjectiveId, state.Progress, definition.EffectiveTargetCount);
            EmitSignal(SignalName.ObjectiveActivated, state.ObjectiveId, state.Active);
            return true;
        }

        public bool IsComplete(string objectiveId)
            => StateFor(objectiveId)?.Completed ?? false;

        public bool IsActive(string objectiveId)
            => StateFor(objectiveId)?.Active ?? false;

        public int GetProgress(string objectiveId)
            => StateFor(objectiveId)?.Progress ?? 0;

        public int GetTarget(string objectiveId)
            => DefinitionFor(objectiveId)?.EffectiveTargetCount ?? 1;

        public Godot.Collections.Array<string> GetActiveObjectives()
        {
            EnsureStates();
            var active = new Godot.Collections.Array<string>();
            foreach (GridObjectiveDefinition definition in GridObjectiveDefinition.Enumerate(Objectives))
            {
                string id = DefinitionId(definition);
                if (!string.IsNullOrEmpty(id) && IsActive(id))
                    active.Add(id);
            }
            return active;
        }

        public Godot.Collections.Dictionary CaptureState()
        {
            EnsureStates();
            var state = new Godot.Collections.Dictionary();
            var objectiveStates = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (KeyValuePair<string, ObjectiveState> pair in _states)
            {
                objectiveStates.Add(new Godot.Collections.Dictionary
                {
                    ["objective_id"] = pair.Value.ObjectiveId,
                    ["progress"] = pair.Value.Progress,
                    ["active"] = pair.Value.Active,
                    ["completed"] = pair.Value.Completed
                });
            }

            state["objectives"] = objectiveStates;
            return state;
        }

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            EnsureStates();
            if (!state.ContainsKey("objectives") || state["objectives"].VariantType != Variant.Type.Array)
                return;

            foreach (Variant value in GridVariantReader.Array(state, "objectives"))
            {
                if (!GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary data))
                    continue;

                string id = ReadString(data, "objective_id");
                ObjectiveState? objective = StateFor(id);
                if (objective == null)
                    continue;

                objective.Progress = ReadInt(data, "progress");
                objective.Active = ReadBool(data, "active", objective.Active);
                objective.Completed = ReadBool(data, "completed", objective.Completed);
            }
        }

        public void Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey))
                state.GameData[SaveKey] = CaptureState();
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return;

            if (state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
                RestoreState(saved);
        }

        public GridObjectiveDefinition? DefinitionFor(string objectiveId)
        {
            string normalized = GridObjectiveDefinition.Normalize(objectiveId);
            foreach (GridObjectiveDefinition definition in GridObjectiveDefinition.Enumerate(Objectives))
            {
                if (definition == null)
                    continue;
                if (DefinitionId(definition) == normalized)
                    return definition;
            }
            return null;
        }

        private ObjectiveState? StateFor(string objectiveId)
        {
            EnsureStates();
            string normalized = GridObjectiveDefinition.Normalize(objectiveId);
            return _states.TryGetValue(normalized, out ObjectiveState? state) ? state : null;
        }

        private void EnsureStates()
        {
            foreach (GridObjectiveDefinition definition in GridObjectiveDefinition.Enumerate(Objectives))
            {
                string id = DefinitionId(definition);
                if (string.IsNullOrEmpty(id) || _states.ContainsKey(id))
                    continue;

                _states[id] = new ObjectiveState
                {
                    ObjectiveId = id,
                    Active = AutoActivateAll || definition.ActiveOnStart
                };
            }
        }

        private static string DefinitionId(GridObjectiveDefinition? definition)
            => definition == null ? "" : definition.NormalizedId();

        private static string ReadString(Godot.Collections.Dictionary data, string key)
            => data.ContainsKey(key) ? data[key].AsString() : "";

        private static int ReadInt(Godot.Collections.Dictionary data, string key)
            => GridVariantReader.Int(data, key, 0);

        private static bool ReadBool(Godot.Collections.Dictionary data, string key, bool fallback)
            => GridVariantReader.Bool(data, key, fallback);

        private sealed class ObjectiveState
        {
            public string ObjectiveId { get; init; } = "";
            public int Progress { get; set; }
            public bool Active { get; set; }
            public bool Completed { get; set; }
        }
    }
}
