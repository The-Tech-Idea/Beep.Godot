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
    /// every tick material moves one link along at FlowRatePerTurn, each
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
        /// Units per TURN the chain moves - the transport-speed dial, and why
        /// a pipeline is not a truck: a pipe flows continuously and fast, a
        /// conveyor slower, a mule train slower still. A turn is a day, so the
        /// authored throughput means the same amount of world material on the
        /// turn axis and the real-time one.
        /// </summary>
        [Export(PropertyHint.Range, "0.1,9999,0.1")] public float FlowRatePerTurn { get; set; } = 6f;

        /// <summary>
        /// The clock that decides what a turn of flow is. Empty finds one
        /// scene-wide; with none anywhere the chain runs off its own frame
        /// delta at one turn per second.
        /// </summary>
        [Export] public NodePath WorkClockPath { get; set; } = new("");

        public bool IsBlocked { get; private set; }

        private readonly List<Node> _links = new();
        private readonly GridWorkClockBinding _workClock = new();
        private double _flowBudget;

        public override void _Ready()
        {
            if (!Engine.IsEditorHint())
            {
                // Throughput is measured in TURNS, so the chain advances on the
                // work clock - otherwise a turn-based game would have pipelines
                // flowing in real time while everything else waited for a turn.
                bool bound = _workClock.Bind(this, WorkClockPath, AdvanceWork);
                SetProcess(!bound);
            }
            else
            {
                SetProcess(false);
            }
            UpdateConfigurationWarnings();
        }

        public override void _ExitTree()
        {
            _workClock.Unbind();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (Chain.Count < 2)
                return new[] { "Chain needs at least a source and a sink NodePath." };
            return System.Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            // Reached only when no work clock was found - a template scene
            // opened on its own, or a headless probe. SetProcess is off otherwise.
            if (Engine.IsEditorHint())
                return;

            AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta));
        }

        /// <summary>
        /// Moves material by turns of flow. Kept public under its old name so a
        /// caller that steps the chain deliberately still can; one turn is one
        /// second on the real-time axis, so the meaning is unchanged there.
        /// </summary>
        public void Tick(double delta) => AdvanceWork(GridWorkClockBinding.TurnsForDelta(delta));

        /// <summary>Moves one hop's worth of material per turn. Bound to the work clock.</summary>
        public void AdvanceWork(float turns)
        {
            if (!ResolveLinks())
                return;

            if (!float.IsFinite(turns) || turns <= 0f)
                return;

            _flowBudget += turns * Mathf.Max(0.1f, FlowRatePerTurn);
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
