using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// The machinery behind a registry of duck-typed nodes: one node announces
    /// itself to ONE manager instead of every consumer crawling the tree, and
    /// the manager never decides what the node's job MEANS - it only asks
    /// whether the node answers the shape by name.
    ///
    /// This base owns the four mechanics that are identical whichever contract
    /// is being asked - the backing list, the duplicate guard, the count that
    /// prunes freed nodes first, and the reverse-loop prune itself - because
    /// they are ONE capability. Two managers hand-rolling them drift the moment
    /// a change lands in only one: a signal-timing fix, a valid-node check, a
    /// remainder rule would each have to be written twice.
    ///
    /// A subclass supplies only what genuinely differs:
    /// AnswersContract (the by-name shape test), ContractSummary (the words in
    /// the refusal warning), and OnRegistered/OnUnregistered (its own typed
    /// signals). GridTransportManagerComponent asks by METHOD, because a
    /// transporter answers with RequestHaul/CanAccept/Load/Unload;
    /// GridExtractionManagerComponent asks by PROPERTY, because an extractor
    /// answers with IsExtracting/ActiveResourceId and Get returns a Nil Variant
    /// for a property that does not exist - the same "does it answer the shape"
    /// question asked one level down.
    ///
    /// Pruning is LAZY, deliberately: a freed node is dropped on the next read
    /// (Count, Registered) rather than on a timer or from the tracked node's own
    /// _ExitTree, so a manager nobody queries carries inert stale entries
    /// instead of paying for bookkeeping nobody asked for.
    ///
    /// Never instantiated on its own - it carries no [GlobalClass], so the
    /// editor's create-node dialog still offers only the concrete managers.
    /// </summary>
    public abstract partial class DuckTypedNodeRegistry : Node
    {
        private readonly List<Node> _nodes = new();

        /// <summary>
        /// Adds a node to the registry. Registering the same live node twice is
        /// a no-op that still reports success - the caller asked for the node to
        /// be in the registry and it is. Returns false, with a named warning, for
        /// a node that does not answer the subclass's contract: without the check
        /// a malformed registrant joined silently and only failed later, at read
        /// time, somewhere else entirely.
        /// </summary>
        public bool Register(Node node)
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
                return false;

            if (_nodes.Contains(node))
                return true;

            if (!AnswersContract(node))
            {
                GD.PushWarning($"[{Name}] {node.Name} does not answer the {ContractSummary} and was not registered.");
                return false;
            }

            _nodes.Add(node);
            OnRegistered(node);
            return true;
        }

        /// <summary>
        /// Drops a node from the registry and announces it. A node that was
        /// already freed is pruned silently by the next read instead - that is
        /// not an unregistration the manager owes anybody a signal for.
        /// </summary>
        public void Unregister(Node node)
        {
            if (node == null || !_nodes.Remove(node))
                return;

            if (GodotObject.IsInstanceValid(node))
                OnUnregistered(node);
        }

        /// <summary>How many nodes are registered, freed ones pruned first.</summary>
        public int Count
        {
            get
            {
                Prune();
                return _nodes.Count;
            }
        }

        /// <summary>
        /// The registered nodes, pruned of freed ones - the view every
        /// subclass's domain methods iterate.
        /// </summary>
        protected IReadOnlyList<Node> Registered
        {
            get
            {
                Prune();
                return _nodes;
            }
        }

        /// <summary>
        /// Drops every freed node. The reverse loop is what makes removal safe
        /// in place, and it runs on every read rather than on a timer so a node
        /// freed a frame ago is never handed to a caller.
        /// </summary>
        protected void Prune()
        {
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (!GodotObject.IsInstanceValid(_nodes[i]))
                    _nodes.RemoveAt(i);
            }
        }

        /// <summary>
        /// Whether a node answers this manager's shape, asked by name so a
        /// GDScript registrant participates exactly like a shipped C# one.
        /// </summary>
        protected abstract bool AnswersContract(Node node);

        /// <summary>
        /// The contract's own words - "transporter contract (CanAccept, Load,
        /// Unload, RequestHaul)" - so each manager's refusal warning still names
        /// what it actually asked for.
        /// </summary>
        protected abstract string ContractSummary { get; }

        /// <summary>HOOK: announce the registration on the subclass's own typed signal.</summary>
        protected virtual void OnRegistered(Node node) { }

        /// <summary>HOOK: announce the unregistration on the subclass's own typed signal.</summary>
        protected virtual void OnUnregistered(Node node) { }
    }
}
