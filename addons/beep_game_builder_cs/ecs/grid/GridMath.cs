using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The small numeric guards a grid component needs to keep an authored or runtime
    /// value inside a safe range: a NaN/Infinity/negative export must never reach the
    /// simulation. One owner, so the rule cannot drift between a component and its copy.
    /// </summary>
    public static class GridMath
    {
        /// <summary>The value when it is finite and positive, otherwise 0.</summary>
        public static float NonNegativeFinite(float value)
            => float.IsFinite(value) && value > 0f ? value : 0f;

        /// <summary>True when the value is finite and strictly positive.</summary>
        public static bool FinitePositive(float value)
            => float.IsFinite(value) && value > 0f;

        /// <summary>A frame delta clamped to (0, one day] seconds; 0 for a non-finite or non-positive delta.</summary>
        public static float DeltaSeconds(double delta)
            => double.IsFinite(delta) && delta > 0.0 ? (float)Mathf.Min(delta, 86400.0) : 0f;

        /// <summary>True when both components of the vector are finite.</summary>
        public static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        /// <summary>Each non-finite component replaced by the fallback's.</summary>
        public static Vector2 FiniteVector(Vector2 value, Vector2 fallback)
            => new(float.IsFinite(value.X) ? value.X : fallback.X,
                   float.IsFinite(value.Y) ? value.Y : fallback.Y);

        /// <summary>Each component that is not finite-and-positive replaced by the fallback's.</summary>
        public static Vector2 PositiveVector(Vector2 value, Vector2 fallback)
            => new(FinitePositive(value.X) ? value.X : fallback.X,
                   FinitePositive(value.Y) ? value.Y : fallback.Y);
    }
}
