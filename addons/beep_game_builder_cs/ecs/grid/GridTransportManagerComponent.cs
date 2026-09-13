using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The dispatcher between "something needs moving" and "something that
    /// moves things". A producer - the extractor delivering via transport, or
    /// any game script - asks RequestHaul; the manager offers the job to its
    /// registered transporters and the first one that accepts takes it.
    ///
    /// EXPANDABLE BY REGISTRATION, not by type: Register accepts any Node
    /// that answers the IGridTransporter shape by name - the IsBusy property,
    /// CanCarry and RequestHaul methods - so a GDScript truck, a train, a
    /// drone or a conveyor head participates exactly like the shipped
    /// GridHaulerComponent. The manager never decides what hauling MEANS;
    /// each transporter does.
    ///
    /// The registry itself - the list, the duplicate guard, the count-with-prune
    /// and the prune - is DuckTypedNodeRegistry's; what stays here is the shape
    /// question (by METHOD, unlike the extractor's by-property one) and the
    /// dispatch policy.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridTransportManagerComponent : DuckTypedNodeRegistry
    {
        [Signal] public delegate void TransporterRegisteredEventHandler(Node transporter);
        [Signal] public delegate void TransporterUnregisteredEventHandler(Node transporter);
        [Signal] public delegate void HaulAssignedEventHandler(Node transporter, int x, int y, string resourceId, int amount);
        [Signal] public delegate void HaulUnassignedEventHandler(int x, int y, string resourceId, int amount);

        /// <summary>The registered transporters, freed ones pruned first.</summary>
        public int TransporterCount => Count;

        protected override string ContractSummary => "transporter contract (CanAccept, Load, Unload, RequestHaul)";

        /// <summary>
        /// A transporter answers by METHOD: the four calls dispatch makes. A node
        /// missing one of them would otherwise fail from inside RequestHaul, one
        /// haul late and with no named warning.
        /// </summary>
        protected override bool AnswersContract(Node transporter)
            => transporter.HasMethod("RequestHaul") && transporter.HasMethod("CanAccept")
                && transporter.HasMethod("Load") && transporter.HasMethod("Unload");

        protected override void OnRegistered(Node transporter)
            => EmitSignal(SignalName.TransporterRegistered, transporter);

        protected override void OnUnregistered(Node transporter)
            => EmitSignal(SignalName.TransporterUnregistered, transporter);

        /// <summary>
        /// Offers a haul to the registered transporters - FASTEST first, by
        /// their TransportRatePerTurn (a transporter that does not expose one counts
        /// as 1) - and reports whether one took it. A false return means the
        /// load is still the caller's problem - the shipped extractor falls
        /// back to the wallet so yield is never lost.
        /// </summary>
        public bool RequestHaul(Vector2I fromCell, string resourceId, int amount)
        {
            var candidates = new List<(Node Transporter, float Rate)>();
            foreach (Node transporter in Registered)
            {
                if (transporter.Get("IsBusy").AsBool())
                    continue;
                if (!transporter.Call("CanAccept", resourceId).AsBool())
                    continue;
                candidates.Add((transporter, RateOf(transporter)));
            }
            OrderCandidates(candidates);

            foreach ((Node transporter, float _) in candidates)
            {
                if (!transporter.Call("RequestHaul", fromCell, resourceId, amount).AsBool())
                    continue;

                EmitSignal(SignalName.HaulAssigned, transporter, fromCell.X, fromCell.Y, resourceId, amount);
                return true;
            }

            EmitSignal(SignalName.HaulUnassigned, fromCell.X, fromCell.Y, resourceId, amount);
            return false;
        }

        private static float RateOf(Node transporter)
        {
            Variant rate = transporter.Get("TransportRatePerTurn");
            return rate.VariantType == Variant.Type.Nil ? 1f : Mathf.Max(0f, rate.AsSingle());
        }

        /// <summary>
        /// HOOK: the dispatch policy. The default offers hauls fastest-first
        /// by TransportRatePerTurn; override for nearest-first, round-robin, cost
        /// models, or whatever the game means by "the right vehicle".
        /// </summary>
        protected virtual void OrderCandidates(List<(Node Transporter, float Rate)> candidates)
            => candidates.Sort((a, b) => b.Rate.CompareTo(a.Rate));

        /// <summary>
        /// Hands cargo from one transporter to the next and returns how much
        /// moved. Unload from the giver, load into the receiver, and give any
        /// remainder BACK to the giver - cargo is never duplicated and never
        /// lost mid-hand-off. This is the primitive a pipeline is built from:
        /// a chain of stationary transporters transferring a load along.
        /// </summary>
        public int Transfer(Node from, Node to, string resourceId, int amount)
            => GridPorts.Transfer(from, to, resourceId, amount);
    }
}
