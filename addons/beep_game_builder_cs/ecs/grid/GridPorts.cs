using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The one implementation of the safe port hand-off, shared by the
    /// transport manager, the pipeline and the hauler's depot delivery.
    ///
    /// The guarantee: the giver is never drawn beyond the room the receiver
    /// advertises; material the receiver declines is returned to a giver that
    /// can re-accept it; and a shortfall that cannot be returned - an
    /// unload-only giver handing to a receiver that under-takes its own
    /// reported free space - is REPORTED rather than dropped. Cargo is never
    /// duplicated, and never silently lost. Ports are read by NAME so GDScript
    /// nodes participate; see ILoadPort / IUnloadPort.
    /// </summary>
    internal static class GridPorts
    {
        /// <summary>Whether the node answers the receiving-port shape.</summary>
        public static bool AnswersLoadPort(Node? node)
            => node != null && GodotObject.IsInstanceValid(node)
                && node.HasMethod("Load") && node.HasMethod("CanAccept");

        /// <summary>
        /// Whether the node can give material. Only Unload is demanded -
        /// a mechanism asks for exactly what it uses, and readers of Stored
        /// or StoredIds guard for themselves.
        /// </summary>
        public static bool AnswersUnloadPort(Node? node)
            => node != null && GodotObject.IsInstanceValid(node)
                && node.HasMethod("Unload");

        /// <summary>
        /// Free space in a load port, read as Capacity - CurrentLoad. A node
        /// that does not expose the two properties is treated as open - the
        /// contract asks for them "at least", but a duck-typed stand-in that
        /// omits them should still receive.
        /// </summary>
        public static int FreeSpace(Node node)
        {
            Variant capacity = node.Get("Capacity");
            Variant load = node.Get("CurrentLoad");
            if (capacity.VariantType == Variant.Type.Nil || load.VariantType == Variant.Type.Nil)
                return int.MaxValue;
            return Mathf.Max(0, capacity.AsInt32() - load.AsInt32());
        }

        /// <summary>
        /// What a port currently holds, asked rather than authored. A node
        /// without StoredIds answers empty - it can still be worked by id.
        /// </summary>
        public static Godot.Collections.Array<string> StoredIdsOf(Node node)
        {
            if (node == null || !GodotObject.IsInstanceValid(node) || !node.HasMethod("StoredIds"))
                return new Godot.Collections.Array<string>();
            return node.Call("StoredIds").AsGodotArray<string>();
        }

        /// <summary>
        /// Hands material from one port to the next and returns how much
        /// moved. See the class summary for the safety contract.
        /// </summary>
        public static int Transfer(Node? from, Node? to, string resourceId, int amount)
        {
            if (from == null || to == null || from == to || amount <= 0)
                return 0;
            if (!AnswersUnloadPort(from) || !AnswersLoadPort(to))
                return 0;
            if (!to.Call("CanAccept", resourceId).AsBool())
                return 0;

            // Never draw more than the receiver advertises room for. An unload-only giver has no
            // way to take material back, so over-drawing it destroys cargo that then cannot be
            // returned - the whole point of this being the one safe hand-off.
            int room = FreeSpace(to);
            if (room <= 0)
                return 0;
            int want = Mathf.Min(amount, room);

            int given = from.Call("Unload", resourceId, want).AsInt32();
            if (given <= 0)
                return 0;

            int taken = to.Call("Load", resourceId, given).AsInt32();
            if (taken < given)
            {
                // The receiver took less than it reported room for (a type filter, or a per-id cap
                // inside Load). Return the remainder to the giver if it can re-accept it; a giver
                // that cannot - an unload-only source - means this hand-off could not honour the
                // "never lost" contract, so report the shortfall instead of swallowing it.
                int returned = from.HasMethod("Load")
                    ? from.Call("Load", resourceId, given - taken).AsInt32()
                    : 0;
                int lost = (given - taken) - returned;
                if (lost > 0)
                    GD.PushWarning($"GridPorts.Transfer: {lost} '{resourceId}' could not be delivered or returned to {from.Name}; the giver is unload-only or full.");
            }
            return taken;
        }
    }
}
