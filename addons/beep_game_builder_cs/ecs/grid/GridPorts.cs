using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The one implementation of the safe port hand-off, shared by the
    /// transport manager, the pipeline and the hauler's depot delivery:
    /// unload from the giver, load into the receiver, remainder BACK to the
    /// giver - cargo is never duplicated and never lost. Ports are read by
    /// NAME so GDScript nodes participate; see ILoadPort / IUnloadPort.
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

            int given = from.Call("Unload", resourceId, amount).AsInt32();
            if (given <= 0)
                return 0;

            int taken = to.Call("Load", resourceId, given).AsInt32();
            if (taken < given && from.HasMethod("Load"))
                from.Call("Load", resourceId, given - taken);
            return taken;
        }
    }
}
