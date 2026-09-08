using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The registry of everything currently extracting: every derrick, mine
    /// and custom rig announces itself here, so game logic asks ONE node
    /// instead of crawling the tree. GridObjectiveEventBinderComponent wires
    /// each registered GridExtractorComponent's ExtractionCycle to objective
    /// progress under the same resource id GridResourceNodeComponent.Gathered
    /// already uses, so a "collect N wood" objective advances the same way
    /// whichever path acquired it. No dedicated HUD panel ships for this
    /// registry, though - GridWorkerStatusPanelComponent,
    /// GridProductionPanelComponent, and GridJobBoardComponent cover workers,
    /// production, and jobs, but extraction and transport
    /// (GridTransportManagerComponent) have none.
    ///
    /// EXPANDABLE BY REGISTRATION, not by type: Register accepts any Node
    /// that answers the IGridExtractor shape by name - Get("IsExtracting"),
    /// Get("ActiveResourceId"), optionally the rate methods
    /// CurrentAmountPerCycle/CurrentCycleTurns - so a GDScript extractor
    /// participates exactly like the shipped C# one. The shipped
    /// GridExtractorComponent registers itself automatically when a manager
    /// exists in the scene.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridExtractionManagerComponent : Node
    {
        [Signal] public delegate void ExtractorRegisteredEventHandler(Node extractor);
        [Signal] public delegate void ExtractorUnregisteredEventHandler(Node extractor);

        private readonly List<Node> _extractors = new();

        // The extractor contract is answered by PROPERTY, not by method, so
        // this cannot be the HasMethod check GridTransportManagerComponent
        // uses for its transporters - Get returns a Nil Variant for a property
        // the registrant does not have, which is the same "does it answer the
        // shape" question one level down.
        private static readonly StringName IsExtractingProperty = new("IsExtracting");
        private static readonly StringName ActiveResourceIdProperty = new("ActiveResourceId");

        /// <summary>
        /// Adds an extractor to the registry. Registering the same node twice
        /// is a no-op that still reports success. Returns false, with a named
        /// warning, for a node that does not answer the extractor contract:
        /// without this check a malformed registrant joined silently and only
        /// failed later, at read time, in IsActivelyExtracting.
        /// </summary>
        public bool Register(Node extractor)
        {
            if (extractor == null || !GodotObject.IsInstanceValid(extractor))
                return false;

            if (_extractors.Contains(extractor))
                return true;

            if (extractor.Get(IsExtractingProperty).VariantType == Variant.Type.Nil
                || extractor.Get(ActiveResourceIdProperty).VariantType == Variant.Type.Nil)
            {
                GD.PushWarning($"[{Name}] {extractor.Name} does not answer the extractor contract "
                    + "(IsExtracting, ActiveResourceId) and was not registered.");
                return false;
            }

            _extractors.Add(extractor);
            EmitSignal(SignalName.ExtractorRegistered, extractor);
            return true;
        }

        public void Unregister(Node extractor)
        {
            if (extractor == null || !_extractors.Remove(extractor))
                return;

            if (GodotObject.IsInstanceValid(extractor))
                EmitSignal(SignalName.ExtractorUnregistered, extractor);
        }

        public int ExtractorCount
        {
            get
            {
                Prune();
                return _extractors.Count;
            }
        }

        /// <summary>The registered extractors, pruned of freed nodes.</summary>
        public Godot.Collections.Array<Node> Extractors()
        {
            Prune();
            var result = new Godot.Collections.Array<Node>();
            foreach (Node extractor in _extractors)
                result.Add(extractor);
            return result;
        }

        /// <summary>How many registered extractors are actively working the resource.</summary>
        public int ActiveCountFor(string resourceId)
        {
            Prune();
            int count = 0;
            foreach (Node extractor in _extractors)
            {
                if (IsActivelyExtracting(extractor, resourceId))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Units per TURN currently flowing for a resource, summed over the
        /// active extractors that expose their rate (the shipped one does; a
        /// custom extractor without the rate methods counts as unknown, 0).
        /// The unit is the turn because a cycle is: the same number reads as
        /// units per in-game day on either time axis.
        /// </summary>
        public float EstimatedRatePerTurn(string resourceId)
        {
            Prune();
            float total = 0f;
            foreach (Node extractor in _extractors)
            {
                if (!IsActivelyExtracting(extractor, resourceId))
                    continue;
                if (!extractor.HasMethod("CurrentAmountPerCycle") || !extractor.HasMethod("CurrentCycleTurns"))
                    continue;

                float turns = extractor.Call("CurrentCycleTurns").AsSingle();
                int amount = extractor.Call("CurrentAmountPerCycle").AsInt32();
                if (turns > 0f && amount > 0)
                    total += amount / turns;
            }
            return total;
        }

        private static bool IsActivelyExtracting(Node extractor, string resourceId)
        {
            if (!extractor.Get("IsExtracting").AsBool())
                return false;
            return string.IsNullOrEmpty(resourceId)
                || extractor.Get("ActiveResourceId").AsString() == resourceId;
        }

        private void Prune()
        {
            for (int i = _extractors.Count - 1; i >= 0; i--)
            {
                if (!GodotObject.IsInstanceValid(_extractors[i]))
                    _extractors.RemoveAt(i);
            }
        }
    }
}
