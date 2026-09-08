using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The duck-typing fallback for IWorker, mirroring GridPorts exactly:
    /// GDScript cannot implement a C# interface, so a system that must also
    /// recognize a duck-typed GDScript worker reads IsWorking/CurrentJobId
    /// by NAME instead. A GDScript participant answers the shape by
    /// exposing members with these exact PascalCase names - the same
    /// convention this addon's own GDScript test doubles already use for
    /// the port contracts.
    /// </summary>
    internal static class GridWorkerPorts
    {
        /// <summary>Whether a node answers the IWorker shape - implements the
        /// interface, or exposes IsWorking/CurrentJobId by name.</summary>
        public static bool AnswersWorkerShape(Node? node)
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
                return false;
            if (node is IWorker)
                return true;

            return node.Get("IsWorking").VariantType != Variant.Type.Nil
                && node.Get("CurrentJobId").VariantType != Variant.Type.Nil;
        }

        public static bool IsWorking(Node node)
            => node is IWorker typed ? typed.IsWorking : node.Get("IsWorking").AsBool();

        public static string CurrentJobId(Node node)
            => node is IWorker typed ? typed.CurrentJobId : node.Get("CurrentJobId").AsString();
    }
}
