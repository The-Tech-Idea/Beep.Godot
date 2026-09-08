using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The duck-typing fallback for IConstructionVisual, mirroring
    /// GridWorkerPorts exactly: GDScript cannot implement a C# interface, so
    /// the grid layer also recognizes a scene that exposes the contract's
    /// members by NAME. Every member is optional: a scene is told only what
    /// it exposes, and a scene that exposes none of it is still a valid
    /// stage - it is simply not told anything.
    /// </summary>
    internal static class GridConstructionVisualPorts
    {
        /// <summary>Whether a node answers the IConstructionVisual shape -
        /// implements the interface, or exposes BuildProgress by name.</summary>
        public static bool AnswersConstructionVisualShape(Node? node)
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
                return false;
            if (node is IConstructionVisual)
                return true;

            return node.Get("BuildProgress").VariantType != Variant.Type.Nil;
        }

        public static void ConfigureSite(Node node, Vector2I footprint, Vector2 cellSize, int stageIndex, int stageCount)
        {
            if (node is IConstructionVisual typed)
            {
                typed.SiteFootprint = footprint;
                typed.CellSize = cellSize;
                typed.StageIndex = stageIndex;
                typed.StageCount = stageCount;
                return;
            }

            SetIfExposed(node, "SiteFootprint", footprint);
            SetIfExposed(node, "CellSize", cellSize);
            SetIfExposed(node, "StageIndex", stageIndex);
            SetIfExposed(node, "StageCount", stageCount);
        }

        public static void SetSiteState(Node node, string state)
        {
            if (node is IConstructionVisual typed)
                typed.SiteState = state;
            else
                SetIfExposed(node, "SiteState", state);
        }

        public static void SetSiteMaterials(Node node, Godot.Collections.Array materials)
        {
            if (node is IConstructionVisual typed)
                typed.SiteMaterials = materials;
            else
                SetIfExposed(node, "SiteMaterials", materials);
        }

        public static void SetBuildProgress(Node node, float progress)
        {
            if (node is IConstructionVisual typed)
                typed.BuildProgress = progress;
            else
                SetIfExposed(node, "BuildProgress", progress);
        }

        public static void WorkPulse(Node node)
        {
            if (node is IConstructionVisual typed)
                typed.WorkPulse();
            else if (node.HasMethod("WorkPulse"))
                node.Call("WorkPulse");
        }

        private static void SetIfExposed(Node node, string property, Variant value)
        {
            if (node.Get(property).VariantType != Variant.Type.Nil)
                node.Set(property, value);
        }
    }
}
