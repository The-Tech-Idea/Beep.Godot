using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The ONE owner of "how much is left underground", per cell.
    ///
    /// The map's data layers say what lies beneath each cell and how rich it
    /// is - immutable facts of the generated world. This component owns the
    /// MUTABLE half: the remaining amount, drawn down by extractors, carried
    /// through saves. Splitting it this way keeps the published map pure - a
    /// reload regenerates it from the saved recipe, TerrainWorldComponent's
    /// axes and seed, through RestoreWorld - while the drawdown lives with
    /// the rest of the gameplay state. The two halves meet lazily: a restored
    /// remaining amount is read against layers that RestoreWorld has rebuilt,
    /// never inside Load, so the order the saveables restore in is irrelevant.
    ///
    /// A cell's remaining amount is seeded LAZILY on first touch:
    /// richness x the catalog definition's Amount (its per-cell amount at
    /// full richness). Cells never touched are never stored, so the save
    /// stays the size of what the player actually worked.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridSubsurfaceStoreComponent : Node, ISaveable
    {
        [Signal] public delegate void DepositChangedEventHandler(int x, int y, string resourceId, int remaining);
        [Signal] public delegate void DepositDepletedEventHandler(int x, int y, string resourceId);
        [Signal] public delegate void StateRestoredEventHandler();

        [Export] public bool ParticipatesInSave { get; set; } = true;
        [Export] public string SaveKey { get; set; } = "grid_subsurface.state";
        [Export] public NodePath DataLayersPath { get; set; } = new("");

        /// <summary>
        /// The shared resource catalog, for each deposit's per-cell Amount.
        /// Without it, DefaultCellAmount stands in for every resource.
        /// </summary>
        [Export] public ResourceCatalog? Catalog { get; set; }
        [Export(PropertyHint.Range, "1,9999,1")] public int DefaultCellAmount { get; set; } = 8;

        private readonly Dictionary<Vector2I, int> _remaining = new();
        private TerrainDataLayersComponent? _dataLayers;
        private string _undergroundIdentity = "";

        public override void _Ready()
        {
            ResolveReferences();
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
            => DataLayersPath.IsEmpty
                ? new[] { "DataLayersPath should point to a TerrainDataLayersComponent." }
                : System.Array.Empty<string>();

        /// <summary>The underground resource id beneath a cell, or empty.</summary>
        public string ResourceIdAt(Vector2I cell)
        {
            ResolveReferences();
            return _dataLayers?.UndergroundResourceAt(cell) ?? "";
        }

        /// <summary>
        /// Units left beneath a cell. 0 where there is no deposit, or where
        /// one has been worked out.
        /// </summary>
        public int RemainingAt(Vector2I cell)
        {
            ResolveReferences();
            if (_dataLayers is null || _dataLayers.UndergroundIdentity.Length == 0
                || _dataLayers.UndergroundResourceAt(cell).Length == 0) return 0;
            if (_undergroundIdentity == _dataLayers.UndergroundIdentity
                && _remaining.TryGetValue(cell, out int stored))
                return stored;
            return SeedAmountAt(cell);
        }

        /// <summary>
        /// Draws up to <paramref name="amount"/> from beneath a cell and
        /// returns what actually came up. Emits DepositDepleted when the last
        /// unit leaves.
        /// </summary>
        public int Draw(Vector2I cell, int amount)
        {
            if (amount <= 0)
                return 0;

            string id = ResourceIdAt(cell);
            if (id.Length == 0)
                return 0;

            int remaining = RemainingAt(cell);
            if (remaining <= 0)
                return 0;

            int drawn = Mathf.Min(amount, remaining);
            remaining -= drawn;
            if (_undergroundIdentity != _dataLayers!.UndergroundIdentity)
            {
                _remaining.Clear();
                _undergroundIdentity = _dataLayers.UndergroundIdentity;
            }
            _remaining[cell] = remaining;
            EmitSignal(SignalName.DepositChanged, cell.X, cell.Y, id, remaining);
            if (remaining <= 0)
                EmitSignal(SignalName.DepositDepleted, cell.X, cell.Y, id);
            return drawn;
        }

        private int SeedAmountAt(Vector2I cell)
        {
            ResolveReferences();
            if (_dataLayers is null)
                return 0;

            string id = _dataLayers.UndergroundResourceAt(cell);
            if (id.Length == 0)
                return 0;

            int baseAmount = Catalog?.Find(id)?.Amount ?? DefaultCellAmount;
            float richness = Mathf.Clamp(_dataLayers.UndergroundRichnessAt(cell), 0.0f, 1.0f);
            return Mathf.Max(1, Mathf.CeilToInt(baseAmount * richness));
        }

        public Godot.Collections.Dictionary CaptureState()
        {
            ResolveReferences();
            string identity = _dataLayers?.UndergroundIdentity ?? "";
            if (identity.Length == 0) identity = _undergroundIdentity;
            var cells = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach ((Vector2I cell, int remaining) in _remaining)
            {
                if (identity != _undergroundIdentity) break;
                cells.Add(new Godot.Collections.Dictionary
                {
                    ["cell"] = cell,
                    ["remaining"] = remaining
                });
            }
            return new Godot.Collections.Dictionary { ["underground_identity"] = identity, ["cells"] = cells };
        }

        public void RestoreState(Godot.Collections.Dictionary state)
        {
            _remaining.Clear();
            _undergroundIdentity = GridVariantReader.String(state, "underground_identity", "");
            foreach (Variant value in GridVariantReader.Array(state, "cells"))
            {
                if (_undergroundIdentity.Length == 0) break;
                if (!GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary dict))
                    continue;

                Vector2I cell = GridVariantReader.Vector2I(dict, "cell", new Vector2I(int.MinValue, int.MinValue));
                if (cell.X == int.MinValue || cell.Y == int.MinValue)
                    continue;

                _remaining[cell] = Mathf.Max(0, GridVariantReader.Int(dict, "remaining", 0));
            }
            EmitSignal(SignalName.StateRestored);
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

        private void ResolveReferences()
        {
            // Explicit wire only, like every other DataLayersPath: a scene
            // with two data-layer nodes must not silently pick one.
            _dataLayers = !DataLayersPath.IsEmpty
                ? GetNodeOrNull<TerrainDataLayersComponent>(DataLayersPath) : null;
        }
    }
}
