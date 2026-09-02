using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// A standing TRANSPORT CHAIN between ports - the general thing a
    /// pipeline is one dress for. Author the Chain as an ordered list of
    /// nodes answering the port contracts - first the source (an extractor's
    /// buffer, a tank), anything in between (tanks, buffers, even parked
    /// transporters: every ITransporter is both ports), last the sink - and
    /// every tick material moves one link along at FlowRatePerSecond, each
    /// hop the same safe hand-off the whole logistics layer uses
    /// (GridPorts.Transfer). A crude pipeline, a conveyor line, a train of
    /// cars, a bucket brigade of boats: same component, different links and
    /// rate.
    ///
    /// The chain carries whatever its links HOLD - it asks each port
    /// (StoredIds) instead of being told. Restricting it to one resource is
    /// the developer's authoring choice (the ResourceIds filter), never the
    /// component's rule.
    ///
    /// BACKPRESSURE is the design, not an accident: material moves from the
    /// SINK end backward, so each link only advances into the space the link
    /// ahead just freed. When the LAST port is full the whole chain stops -
    /// buffers hold what they hold, the source stops being drained (a
    /// buffered extractor stalls with its deposit intact), and ChainBlocked
    /// says so; ChainUnblocked fires when the sink drains and flow resumes.
    /// Nothing is ever pushed into a full port and nothing is lost.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridTransportChainComponent : Node
    {
        [Signal] public delegate void FlowedEventHandler(string resourceId, int amountIntoSink);
        [Signal] public delegate void ChainBlockedEventHandler(string resourceId);
        [Signal] public delegate void ChainUnblockedEventHandler(string resourceId);

        /// <summary>
        /// OPTIONAL filter: the resources this chain carries. EMPTY carries
        /// anything its links hold - the chain asks each port what is inside
        /// (StoredIds) rather than being told. A single-resource crude
        /// pipeline is authored by putting one id here; the restriction is
        /// the developer's, never the component's.
        /// </summary>
        [Export] public Godot.Collections.Array<string> ResourceIds { get; set; } = new();

        /// <summary>
        /// Ordered ports, source first, sink last. Two entries is a direct
        /// coupling; more puts tanks or buffers along the run.
        /// </summary>
        [Export] public Godot.Collections.Array<NodePath> Chain { get; set; } = new();

        /// <summary>
        /// Units per second the chain moves - the transport-speed dial, and
        /// why a pipeline is not a truck: a pipe flows continuously and fast,
        /// a conveyor slower, a mule train slower still.
        /// </summary>
        [Export(PropertyHint.Range, "0.1,9999,0.1")] public float FlowRatePerSecond { get; set; } = 6f;

        public bool IsBlocked { get; private set; }

        private readonly List<Node> _links = new();
        private double _flowBudget;

        public override void _Ready()
        {
            SetProcess(!Engine.IsEditorHint());
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (Chain.Count < 2)
                return new[] { "Chain needs at least a source and a sink NodePath." };
            return System.Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint())
                return;

            Tick(delta);
        }

        public void Tick(double delta)
        {
            if (!ResolveLinks())
                return;

            double step = double.IsFinite(delta) && delta > 0.0 ? System.Math.Min(delta, 86400.0) : 0.0;
            _flowBudget += step * Mathf.Max(0.1f, FlowRatePerSecond);
            int budget = (int)_flowBudget;
            if (budget <= 0)
                return;
            // Spent whether it moves or not: a stalled chain must not bank an
            // unbounded burst for the moment the sink drains.
            _flowBudget -= budget;

            // Sink end first, so each link advances into space the link ahead
            // has just freed - which is exactly what makes a full sink stall
            // the whole run instead of cramming the middle. Each link moves
            // whatever its giver actually HOLDS (asked, not authored),
            // filtered by ResourceIds when the developer set one.
            string blockedId = "";
            int intoSink = 0;
            for (int i = _links.Count - 2; i >= 0; i--)
            {
                int linkBudget = budget;
                foreach (string id in CarriableIds(_links[i]))
                {
                    if (linkBudget <= 0)
                        break;
                    int moved = MoveLink(_links[i], _links[i + 1], id, linkBudget);
                    linkBudget -= moved;
                    if (i == _links.Count - 2 && moved > 0)
                    {
                        intoSink += moved;
                        EmitSignal(SignalName.Flowed, id, moved);
                    }
                }
            }

            if (intoSink > 0)
            {
                if (IsBlocked)
                {
                    IsBlocked = false;
                    EmitSignal(SignalName.ChainUnblocked, "");
                }
                return;
            }

            // Nothing reached the sink. That is a BLOCKAGE only when the sink
            // genuinely has no space and there is material waiting behind it;
            // an empty chain is merely idle.
            Node sink = _links[^1];
            bool sinkFull = GridPorts.FreeSpace(sink) <= 0;
            for (int i = 0; i < _links.Count - 1 && blockedId.Length == 0; i++)
            {
                foreach (string id in CarriableIds(_links[i]))
                {
                    blockedId = id;
                    break;
                }
            }

            if (sinkFull && blockedId.Length > 0 && !IsBlocked)
            {
                IsBlocked = true;
                EmitSignal(SignalName.ChainBlocked, blockedId);
            }
        }

        /// <summary>
        /// HOOK: one hand-off along the chain. The default is the shared safe
        /// transfer; override for custom link behaviour - leaks, filters per
        /// link, processing while in transit.
        /// </summary>
        protected virtual int MoveLink(Node from, Node to, string resourceId, int amount)
            => GridPorts.Transfer(from, to, resourceId, amount);

        /// <summary>What a giver holds that this chain is allowed to carry.</summary>
        private IEnumerable<string> CarriableIds(Node giver)
        {
            bool canAskAmount = giver.HasMethod("Stored");
            foreach (string id in GridPorts.StoredIdsOf(giver))
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                if (ResourceIds.Count > 0 && !ResourceIds.Contains(id))
                    continue;
                if (canAskAmount && giver.Call("Stored", id).AsInt32() <= 0)
                    continue;
                yield return id;
            }
        }

        private bool ResolveLinks()
        {
            _links.Clear();
            foreach (NodePath path in Chain)
            {
                if (path == null || path.IsEmpty)
                    continue;
                Node? node = GetNodeOrNull<Node>(path);
                if (node != null && GodotObject.IsInstanceValid(node))
                    _links.Add(node);
            }
            return _links.Count >= 2;
        }
    }
}
