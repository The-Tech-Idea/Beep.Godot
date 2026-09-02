using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A stationary cargo hold: a tank, a silo, a warehouse, a pipeline
    /// buffer. It holds material, answers the IGridCargoHold contract, and
    /// does nothing else - which is the point: an extractor fills it, a
    /// hauler draws from it, a pipeline segment hands through it, all via
    /// Load/Unload and GridTransportManagerComponent.Transfer, and none of
    /// them need to know it is a tank.
    ///
    /// Attach under a placed building beside its GridObjectComponent. Give
    /// each storage its own SaveKey; the contents are world state and
    /// round-trip through saves.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridStorageComponent : Node, IStorage, ISaveable
    {
        [Signal] public delegate void StorageChangedEventHandler(string resourceId, int stored, int currentLoad);

        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_storage.state";

        /// <summary>Total units the storage takes, across every resource in it.</summary>
        [Export(PropertyHint.Range, "1,999999,1")] public int Capacity { get; set; } = 200;

        /// <summary>Resource ids this storage accepts; empty accepts anything.</summary>
        [Export] public Godot.Collections.Array<string> AllowedResourceIds { get; set; } = new();

        private readonly Dictionary<string, int> _stored = new(StringComparer.OrdinalIgnoreCase);

        public override void _Ready()
        {
            if (!Engine.IsEditorHint() && ParticipatesInSave)
                AddToGroup(SaveableHelper.Group);
        }

        public override void _ExitTree()
        {
            if (ParticipatesInSave)
                RemoveFromGroup(SaveableHelper.Group);
        }

        public int CurrentLoad
        {
            get
            {
                int total = 0;
                foreach (int amount in _stored.Values)
                    total += amount;
                return total;
            }
        }

        int ILoadPort.Capacity => Mathf.Max(1, Capacity);

        public bool CanAccept(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return false;
            if (AllowedResourceIds.Count == 0)
                return true;

            foreach (string allowed in AllowedResourceIds)
            {
                if (string.Equals(allowed?.Trim(), resourceId.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public int Load(string resourceId, int amount)
        {
            if (amount <= 0 || !CanAccept(resourceId))
                return 0;

            string id = resourceId.Trim();
            int space = Mathf.Max(0, Mathf.Max(1, Capacity) - CurrentLoad);
            int taken = Mathf.Min(space, amount);
            if (taken <= 0)
                return 0;

            _stored[id] = Stored(id) + taken;
            EmitSignal(SignalName.StorageChanged, id, _stored[id], CurrentLoad);
            return taken;
        }

        public int Unload(string resourceId, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(resourceId))
                return 0;

            string id = resourceId.Trim();
            int held = Stored(id);
            int released = Mathf.Min(amount, held);
            if (released <= 0)
                return 0;

            int remaining = held - released;
            if (remaining <= 0)
                _stored.Remove(id);
            else
                _stored[id] = remaining;
            EmitSignal(SignalName.StorageChanged, id, remaining, CurrentLoad);
            return released;
        }

        public int Stored(string resourceId)
            => !string.IsNullOrWhiteSpace(resourceId) && _stored.TryGetValue(resourceId.Trim(), out int amount)
                ? amount
                : 0;

        public Godot.Collections.Array<string> StoredIds()
        {
            var ids = new Godot.Collections.Array<string>();
            foreach (string id in _stored.Keys)
                ids.Add(id);
            return ids;
        }

        public Godot.Collections.Dictionary CaptureState()
        {
            var contents = new Godot.Collections.Dictionary();
            foreach ((string id, int amount) in _stored)
                contents[id] = amount;
            return new Godot.Collections.Dictionary { ["contents"] = contents };
        }

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            _stored.Clear();
            if (!state.ContainsKey("contents")
                || !GridVariantReader.TryDictionary(state["contents"], out Godot.Collections.Dictionary contents))
                return;

            foreach (Variant key in contents.Keys)
            {
                string id = key.AsString();
                int amount = GridVariantReader.Int(contents[key], 0);
                if (!string.IsNullOrWhiteSpace(id) && amount > 0)
                    _stored[id.Trim()] = amount;
            }
        }

        // Explicit interface implementations, so the cargo Load(resourceId,
        // amount) is the only "Load" Godot's name-based Call dispatch can see
        // - an overload against ISaveable.Load(state) would make every duck
        // hand-off ambiguous.
        void ISaveable.Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey))
                state.GameData[SaveKey] = CaptureState();
        }

        void ISaveable.Load(GameBuilder.GameStateData state)
        {
            if (string.IsNullOrWhiteSpace(SaveKey))
                return;

            if (state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
                RestoreState(saved);
        }
    }
}
