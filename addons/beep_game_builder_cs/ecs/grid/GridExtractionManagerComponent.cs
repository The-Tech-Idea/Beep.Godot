using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The registry of everything currently extracting: every derrick, mine
    /// and custom rig announces itself here, and HUDs, objectives and game
    /// logic ask ONE node instead of crawling the tree.
    ///
    /// EXPANDABLE BY REGISTRATION, not by type: Register accepts any Node
    /// that answers the IGridExtractor shape by name - Get("IsExtracting"),
    /// Get("ActiveResourceId"), optionally the rate methods
    /// CurrentAmountPerCycle/CurrentCycleSeconds - so a GDScript extractor
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

        /// <summary>Adds an extractor to the registry; duplicates are ignored.</summary>
        public void Register(Node extractor)
        {
            if (extractor == null || !GodotObject.IsInstanceValid(extractor) || _extractors.Contains(extractor))
                return;

            _extractors.Add(extractor);
            EmitSignal(SignalName.ExtractorRegistered, extractor);
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
        /// Units per second currently flowing for a resource, summed over the
        /// active extractors that expose their rate (the shipped one does; a
        /// custom extractor without the rate methods counts as unknown, 0).
        /// </summary>
        public float EstimatedRatePerSecond(string resourceId)
        {
            Prune();
            float total = 0f;
            foreach (Node extractor in _extractors)
            {
                if (!IsActivelyExtracting(extractor, resourceId))
                    continue;
                if (!extractor.HasMethod("CurrentAmountPerCycle") || !extractor.HasMethod("CurrentCycleSeconds"))
                    continue;

                float seconds = extractor.Call("CurrentCycleSeconds").AsSingle();
                int amount = extractor.Call("CurrentAmountPerCycle").AsInt32();
                if (seconds > 0f && amount > 0)
                    total += amount / seconds;
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
