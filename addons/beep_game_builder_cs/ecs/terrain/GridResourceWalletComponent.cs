using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Small settlement/resource wallet for top-down builders. Use it for build
    /// costs such as wood, stone, coins, oil, parts, food, or seeds without
    /// requiring a slot inventory UI.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridResourceWalletComponent : Node, ISaveable
    {
        [Signal] public delegate void ResourceChangedEventHandler(string resourceId, int amount);
        [Signal] public delegate void ResourcesChangedEventHandler();
        [Signal] public delegate void ResourceSpendRejectedEventHandler(string resourceId, int required, int available);

        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_resources.state";
        /// <summary>
        /// Design-time starting balance, authored as plain scene data: { "wood": 120, "stone": 35 }.
        /// Do not store this as C# Resource subresources. Godot can deserialize those as plain
        /// Resource before the managed script type is bound, which loses the actual amounts.
        /// </summary>
        [Export] public Godot.Collections.Dictionary StartingResourceAmounts { get; set; } = new();
        [Export] public bool ApplyStartingResourcesOnReady { get; set; } = true;

        private readonly Dictionary<string, int> _amounts = new();

        public override void _Ready()
        {
            if (ApplyStartingResourcesOnReady && _amounts.Count == 0)
                LoadStartingResourceAmounts(StartingResourceAmounts);

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
                return new[] { "SaveKey must not be empty when the wallet participates in saves." };
            return System.Array.Empty<string>();
        }

        public int GetAmount(string resourceId)
            => _amounts.TryGetValue(Normalize(resourceId), out int amount) ? amount : 0;

        public void SetAmount(string resourceId, int amount)
        {
            string id = Normalize(resourceId);
            if (string.IsNullOrEmpty(id))
                return;

            int normalizedAmount = Mathf.Max(0, amount);
            if (normalizedAmount == 0)
                _amounts.Remove(id);
            else
                _amounts[id] = normalizedAmount;

            EmitSignal(SignalName.ResourceChanged, id, normalizedAmount);
            EmitSignal(SignalName.ResourcesChanged);
        }

        public void AddAmount(string resourceId, int amount)
        {
            if (amount == 0)
                return;

            string id = Normalize(resourceId);
            if (string.IsNullOrEmpty(id))
                return;

            SetAmount(id, GetAmount(id) + amount);
        }

        public bool CanAfford(Godot.Collections.Array costs)
        {
            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(costs))
            {
                string id = Normalize(resourceId);
                int required = Mathf.Max(0, amount);
                if (required > 0 && GetAmount(id) < required)
                    return false;
            }

            return true;
        }

        public bool Spend(Godot.Collections.Array costs)
        {
            if (!CanAfford(costs))
            {
                foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(costs))
                {
                    string id = Normalize(resourceId);
                    int required = Mathf.Max(0, amount);
                    int available = GetAmount(id);
                    if (required > 0 && available < required)
                    {
                        EmitSignal(SignalName.ResourceSpendRejected, id, required, available);
                        break;
                    }
                }
                return false;
            }

            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(costs))
            {
                AddAmount(resourceId, -Mathf.Max(0, amount));
            }

            return true;
        }

        public void Refund(Godot.Collections.Array costs)
        {
            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(costs))
            {
                AddAmount(resourceId, Mathf.Max(0, amount));
            }
        }

        public Godot.Collections.Dictionary CaptureState()
        {
            var state = new Godot.Collections.Dictionary();
            foreach (var pair in _amounts)
                state[pair.Key] = pair.Value;
            return state;
        }

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            _amounts.Clear();
            foreach (Variant key in state.Keys)
            {
                string id = Normalize(key.AsString());
                if (string.IsNullOrEmpty(id))
                    continue;

                Variant value = state[key];
                int amount = GridVariantReader.Int(value, 0);
                if (amount > 0)
                    _amounts[id] = amount;
            }

            EmitSignal(SignalName.ResourcesChanged);
        }

        public Godot.Collections.Dictionary GetAmounts() => CaptureState();

        public void LoadStartingResourceAmounts(Godot.Collections.Dictionary amounts)
        {
            _amounts.Clear();
            foreach (Variant key in amounts.Keys)
            {
                string id = Normalize(key.AsString());
                if (string.IsNullOrEmpty(id))
                    continue;

                Variant value = amounts[key];
                int amount = GridVariantReader.Int(value, 0);

                AddAmount(id, Mathf.Max(0, amount));
            }
        }

        public void LoadAmounts(Godot.Collections.Array amounts)
        {
            _amounts.Clear();
            foreach (Variant entry in amounts)
                LoadAmountEntry(entry);
        }

        private void LoadAmountEntry(Variant entry)
        {
            if (GridResourceAmount.TryRead(entry, out string resourceId, out int amount))
                AddAmount(resourceId, Mathf.Max(0, amount));
        }

        public void Save(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey))
                state.GameData[SaveKey] = CaptureState();
        }

        public void Load(GameBuilder.GameStateData state)
        {
            if (!string.IsNullOrWhiteSpace(SaveKey)
                && state.GameData.TryGetValue(SaveKey, out Variant value)
                && GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary saved))
            {
                RestoreState(saved);
            }
        }

        private static string Normalize(string resourceId)
            => resourceId.Trim().ToLowerInvariant();
    }
}
