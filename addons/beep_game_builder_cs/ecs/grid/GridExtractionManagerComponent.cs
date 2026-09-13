using Godot;

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
    ///
    /// The registry itself - the list, the duplicate guard, the count-with-prune
    /// and the prune - is DuckTypedNodeRegistry's; what stays here is the shape
    /// question and the fleet queries.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridExtractionManagerComponent : DuckTypedNodeRegistry
    {
        [Signal] public delegate void ExtractorRegisteredEventHandler(Node extractor);
        [Signal] public delegate void ExtractorUnregisteredEventHandler(Node extractor);

        // The extractor contract is answered by PROPERTY, not by method, so
        // this cannot be the HasMethod check GridTransportManagerComponent
        // uses for its transporters - Get returns a Nil Variant for a property
        // the registrant does not have, which is the same "does it answer the
        // shape" question one level down. That difference is the whole of what
        // the two managers do not share, so it is the override, not a copy.
        private static readonly StringName IsExtractingProperty = new("IsExtracting");
        private static readonly StringName ActiveResourceIdProperty = new("ActiveResourceId");

        /// <summary>The registered extractors, freed ones pruned first.</summary>
        public int ExtractorCount => Count;

        protected override string ContractSummary => "extractor contract (IsExtracting, ActiveResourceId)";

        /// <summary>
        /// An extractor answers by PROPERTY. A node without them used to join the
        /// registry silently and only fail later, at read time, in
        /// IsActivelyExtracting.
        /// </summary>
        protected override bool AnswersContract(Node extractor)
            => extractor.Get(IsExtractingProperty).VariantType != Variant.Type.Nil
                && extractor.Get(ActiveResourceIdProperty).VariantType != Variant.Type.Nil;

        protected override void OnRegistered(Node extractor)
            => EmitSignal(SignalName.ExtractorRegistered, extractor);

        protected override void OnUnregistered(Node extractor)
            => EmitSignal(SignalName.ExtractorUnregistered, extractor);

        /// <summary>The registered extractors, pruned of freed nodes.</summary>
        public Godot.Collections.Array<Node> Extractors()
        {
            var result = new Godot.Collections.Array<Node>();
            foreach (Node extractor in Registered)
                result.Add(extractor);
            return result;
        }

        /// <summary>How many registered extractors are actively working the resource.</summary>
        public int ActiveCountFor(string resourceId)
        {
            int count = 0;
            foreach (Node extractor in Registered)
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
            float total = 0f;
            foreach (Node extractor in Registered)
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
    }
}
