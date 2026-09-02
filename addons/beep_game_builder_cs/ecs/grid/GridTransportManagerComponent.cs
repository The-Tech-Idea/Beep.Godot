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
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridTransportManagerComponent : Node
    {
        [Signal] public delegate void TransporterRegisteredEventHandler(Node transporter);
        [Signal] public delegate void TransporterUnregisteredEventHandler(Node transporter);
        [Signal] public delegate void HaulAssignedEventHandler(Node transporter, int x, int y, string resourceId, int amount);
        [Signal] public delegate void HaulUnassignedEventHandler(int x, int y, string resourceId, int amount);

        private readonly List<Node> _transporters = new();

        /// <summary>Adds a transporter; duplicates are ignored.</summary>
        public void Register(Node transporter)
        {
            if (transporter == null || !GodotObject.IsInstanceValid(transporter) || _transporters.Contains(transporter))
                return;

            if (!transporter.HasMethod("RequestHaul") || !transporter.HasMethod("CanAccept")
                || !transporter.HasMethod("Load") || !transporter.HasMethod("Unload"))
            {
                GD.PushWarning($"[{Name}] {transporter.Name} does not answer the transporter contract (CanAccept, Load, Unload, RequestHaul) and was not registered.");
                return;
            }

            _transporters.Add(transporter);
            EmitSignal(SignalName.TransporterRegistered, transporter);
        }

        public void Unregister(Node transporter)
        {
            if (transporter == null || !_transporters.Remove(transporter))
                return;

            if (GodotObject.IsInstanceValid(transporter))
                EmitSignal(SignalName.TransporterUnregistered, transporter);
        }

        public int TransporterCount
        {
            get
            {
                Prune();
                return _transporters.Count;
            }
        }

        /// <summary>
        /// Offers a haul to the registered transporters - FASTEST first, by
        /// their TransportRate (a transporter that does not expose one counts
        /// as 1) - and reports whether one took it. A false return means the
        /// load is still the caller's problem - the shipped extractor falls
        /// back to the wallet so yield is never lost.
        /// </summary>
        public bool RequestHaul(Vector2I fromCell, string resourceId, int amount)
        {
            Prune();

            var candidates = new List<(Node Transporter, float Rate)>();
            foreach (Node transporter in _transporters)
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
            Variant rate = transporter.Get("TransportRate");
            return rate.VariantType == Variant.Type.Nil ? 1f : Mathf.Max(0f, rate.AsSingle());
        }

        /// <summary>
        /// HOOK: the dispatch policy. The default offers hauls fastest-first
        /// by TransportRate; override for nearest-first, round-robin, cost
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

        private void Prune()
        {
            for (int i = _transporters.Count - 1; i >= 0; i--)
            {
                if (!GodotObject.IsInstanceValid(_transporters[i]))
                    _transporters.RemoveAt(i);
            }
        }
    }
}
