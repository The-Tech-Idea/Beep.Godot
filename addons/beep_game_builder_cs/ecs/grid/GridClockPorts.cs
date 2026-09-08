using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Duck-typed lookup for the game clock, so the grid toolkit can be driven by
    /// it without depending on it.
    ///
    /// The grid references no global anywhere - no GameApp, no autoload path, no
    /// singleton - and that is worth keeping: it is why a grid scene can be
    /// opened on its own, and why every headless probe in tests/ runs without
    /// standing up a game. So the clock is found and read BY NAME, exactly the
    /// way GridPorts reads a load/unload port and GridConstructionVisualPorts
    /// reads a construction visual. A GDScript clock of the same shape works
    /// just as well as the shipped C# one.
    ///
    /// The shape: an "Advanced(beats)" signal, a "DayAdvanced(day)" signal, and
    /// a "BeatsPerDay" property.
    /// </summary>
    internal static class GridClockPorts
    {
        public const string AdvancedSignal = "Advanced";
        public const string DayAdvancedSignal = "DayAdvanced";

        private static readonly StringName BeatsPerDayProperty = new("BeatsPerDay");

        /// <summary>The clock's own day fraction, read by name for a progress bar.</summary>
        public static readonly StringName DayProgressProperty = new("DayProgress01");

        /// <summary>Whether a node answers the clock shape this addon drives work from.</summary>
        public static bool AnswersClockShape(Node? node)
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
                return false;

            return node.HasSignal(AdvancedSignal)
                && node.HasSignal(DayAdvancedSignal)
                && node.Get(BeatsPerDayProperty).VariantType != Variant.Type.Nil;
        }

        /// <summary>
        /// Beats in one day, as the clock reports it - the cascade divisor that
        /// turns beats into the day the grid measures work in. A clock that
        /// answers a nonsense value is treated as 1, never as 0.
        /// </summary>
        public static double BeatsPerDay(Node clock)
        {
            double beats = clock.Get(BeatsPerDayProperty).AsDouble();
            return double.IsFinite(beats) && beats > 0.0 ? beats : 1.0;
        }

        /// <summary>
        /// The game clock, or null when there is none. Autoloads are direct
        /// children of the root and own their subsystems, so the search is the
        /// root's children and their children - deliberately shallow, and it
        /// never walks the game's own scene.
        /// </summary>
        public static Node? FindClock(SceneTree? tree)
        {
            Node? root = tree?.Root;
            if (root == null)
                return null;

            foreach (Node child in root.GetChildren())
            {
                if (AnswersClockShape(child))
                    return child;

                foreach (Node grandChild in child.GetChildren())
                {
                    if (AnswersClockShape(grandChild))
                        return grandChild;
                }
            }

            return null;
        }
    }
}
