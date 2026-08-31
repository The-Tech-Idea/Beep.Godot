using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Cuts valleys into the land where water runs, by the amount of water that
    /// runs there.
    ///
    /// WHY THIS EXISTS. Noise alone does not make a landscape, it makes a
    /// texture: fractal height has ridges and hollows, but they do not connect,
    /// and nothing about them says water ever moved. Real relief is carved -
    /// what makes a range read as a range is the VALLEYS between its spurs, and
    /// valleys are cut by drainage. This is the standard hybrid the modern
    /// generators use: raise land with noise, then let water shape it.
    ///
    /// The model is stream-power incision, the usual one in the literature:
    ///
    ///     lowering = strength * drainage^m * slope^n
    ///
    /// with the exponents near m=0.5, n=1. The drainage term is what matters -
    /// erosion at a point depends far more on how much water passes through it
    /// than on anything local - and it is self-reinforcing, because a hollow
    /// that collects a little more water erodes a little faster and so collects
    /// more still. That feedback is what produces branching valley networks
    /// rather than evenly rounded ground.
    ///
    /// The drainage network is the same one the rivers use, from TerrainFlow,
    /// so the valleys and the rivers in them are the same watercourse rather
    /// than two independent guesses that happen to be drawn together.
    ///
    /// IT RUNS BEFORE RELIEF IS CLASSIFIED. Hills and mountains are cut as
    /// percentiles of the height field, so classifying first and eroding after
    /// would label the land from a shape that no longer exists.
    /// </summary>
    internal static class TerrainErosionStage
    {
        /// <summary>
        /// Drainage exponent: how sharply lowering grows with the water passing
        /// through. Around a half is the usual value, and it is well below one
        /// for a reason - a trunk stream carries hundreds of times the flow of
        /// its headwaters, and eroding in proportion would cut a canyon to the
        /// map floor while leaving the slopes untouched.
        /// </summary>
        private const float DrainageExponent = 0.5f;

        /// <summary>
        /// How much of the height range a fully-drained valley may remove. Kept
        /// modest: this is shaping land the noise already placed, not replacing
        /// it, and cutting too deep flattens the map into channels and plateaus.
        /// </summary>
        private const float Strength = 0.12f;

        /// <summary>Ceiling on the drainage factor, so a trunk cannot trench.</summary>
        private const float MaxDrainageFactor = 3.0f;

        /// <summary>
        /// How many times the incision is applied.
        ///
        /// One pass does almost nothing, and measurably so: a cell is lowered by
        /// at most strength times the drop to its outlet, and at sample
        /// resolution that drop is tiny, so a single pass changed 0.03% of the
        /// rendered map. Erosion is a process, not an operation - the literature
        /// integrates it over many steps, and the valleys appear because the
        /// lowering compounds along a drainage path.
        ///
        /// The network is computed once and reused across the passes: drainage
        /// changes far more slowly than height, and recomputing it every pass
        /// would cost several times as much for a result that looks the same.
        /// </summary>
        private const int Passes = 12;

        public static void Apply(TerrainWorld world, TerrainGenerationSettings settings)
        {
            if (settings.ErosionStrength <= 0.0f)
                return;

            int count = world.Count;
            var flowsTo = new int[count];
            var order = new int[count];
            var flow = new float[count];

            int land = TerrainFlow.Accumulate(world, flowsTo, order, flow);
            if (land == 0)
                return;

            // Normalised against a TYPICAL drainage, not the largest.
            //
            // Dividing by the maximum sounds like the safe choice and makes the
            // whole term vanish: a trunk stream carries thousands of times what
            // an ordinary slope does, so every ordinary cell gets a factor near
            // zero and only the channels - which are already rivers - erode at
            // all. Measured that way, twelve passes changed 0.7% of the rendered
            // map. Against the median, a typical cell sits near one and actually
            // gets cut, while the trunks are held by the clamp below rather than
            // running away.
            var sorted = new float[land];
            for (int i = 0; i < land; i++)
                sorted[i] = flow[order[i]];
            System.Array.Sort(sorted);
            float typical = Mathf.Max(1.0f, sorted[land / 2]);

            float strength = Strength * Mathf.Clamp(settings.ErosionStrength, 0.0f, 4.0f);

            for (int pass = 0; pass < Passes; pass++)
            {
            for (int i = 0; i < land; i++)
            {
                int index = order[i];
                int to = flowsTo[index];
                if (to < 0)
                    continue;

                // Slope toward where this cell drains. Flat ground is not cut
                // even when a great deal of water crosses it - that is a
                // floodplain, and a river there spreads rather than incises.
                float slope = Mathf.Max(0.0f, world.Elevation[index] - world.Elevation[to]);
                if (slope <= 0.0f)
                    continue;

                // Clamped so a trunk stream cuts harder than a hillside without
                // cutting a trench to the map floor.
                float drainage = Mathf.Min(
                    MaxDrainageFactor, Mathf.Pow(flow[index] / typical, DrainageExponent));
                float lowering = strength * drainage * slope;

                // Never below the cell it drains into: a cell cut lower than its
                // own outlet is a pit, and pits break the drainage network the
                // rivers are about to be read from.
                world.Elevation[index] = Mathf.Max(
                    world.Elevation[to], world.Elevation[index] - lowering);
            }
            }
        }
    }
}
