using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Pure-math easing curves for game feel — the classic Robert Penner set, normalized to
    /// t in [0,1] → value in [0,1]. Static and allocation-free, so gameplay code can sample them
    /// per-frame without a Tween. Use for hit-flash falloff, UI pops, knockback decay, squash-and-
    /// stretch, and any lerp that should feel snappy rather than linear.
    ///
    /// These match Godot's Tween.TransitionType names where they overlap, so a hand-rolled lerp
    /// can be swapped for a Tween without changing the feel. Not a component — no node state.
    /// </summary>
    public static class Easings
    {
        public enum Kind
        {
            Linear,
            QuadIn, QuadOut, QuadInOut,
            CubicIn, CubicOut, CubicInOut,
            QuartIn, QuartOut,
            ExpoIn, ExpoOut,
            BackIn, BackOut,
            ElasticOut, BounceOut,
        }

        /// <summary>Sample a curve by kind. t is clamped to [0,1].</summary>
        public static float Eval(Kind kind, float t)
        {
            t = Mathf.Clamp(t, 0f, 1f);
            return kind switch
            {
                Kind.Linear => t,
                Kind.QuadIn => QuadIn(t),
                Kind.QuadOut => QuadOut(t),
                Kind.QuadInOut => QuadInOut(t),
                Kind.CubicIn => CubicIn(t),
                Kind.CubicOut => CubicOut(t),
                Kind.CubicInOut => CubicInOut(t),
                Kind.QuartIn => QuartIn(t),
                Kind.QuartOut => QuartOut(t),
                Kind.ExpoIn => ExpoIn(t),
                Kind.ExpoOut => ExpoOut(t),
                Kind.BackIn => BackIn(t),
                Kind.BackOut => BackOut(t),
                Kind.ElasticOut => ElasticOut(t),
                Kind.BounceOut => BounceOut(t),
                _ => t,
            };
        }

        /// <summary>Lerp a→b through an easing curve — the common case.</summary>
        public static float Lerp(Kind kind, float a, float b, float t) =>
            a + (b - a) * Eval(kind, t);

        public static float QuadIn(float t) => t * t;
        public static float QuadOut(float t) => 1f - (1f - t) * (1f - t);
        public static float QuadInOut(float t) =>
            t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

        public static float CubicIn(float t) => t * t * t;
        public static float CubicOut(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float CubicInOut(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        public static float QuartIn(float t) => t * t * t * t;
        public static float QuartOut(float t) => 1f - Mathf.Pow(1f - t, 4f);

        public static float ExpoIn(float t) => t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
        public static float ExpoOut(float t) => t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

        // Back: overshoots the target slightly then settles — the "pop" curve.
        public static float BackIn(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }
        public static float BackOut(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float ElasticOut(float t)
        {
            const float c4 = (2f * Mathf.Pi) / 3f;
            return t == 0f ? 0f : t == 1f ? 1f
                : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static float BounceOut(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1; return n1 * t * t + 0.984375f;
        }
    }
}
