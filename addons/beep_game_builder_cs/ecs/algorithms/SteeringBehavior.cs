using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Craig Reynolds steering behaviors — the building blocks of game AI movement. Pure math,
    /// no node state, allocation-free: each takes a current velocity and returns a desired velocity
    /// (or a steering force), so a CharacterBody2D controller can compose them per-frame.
    ///
    /// Compose by adding the returned forces and clamping to a max speed — a weighted sum of
    /// Seek + Flee(avoid) + Wander is a complete wandering-pursuer. These are helpers, not a
    /// component, because they carry no per-node state; the caller owns velocity.
    /// </summary>
    public static class SteeringBehavior
    {
        /// <summary>Move directly toward a target at max speed. No slowing on approach.</summary>
        public static Vector2 Seek(Vector2 pos, Vector2 target, float maxSpeed) =>
            !IsFinite(pos) || !IsFinite(target) || NonNegative(maxSpeed) <= 0f || (target - pos).LengthSquared() < 0.0001f
                ? Vector2.Zero
                : (target - pos).Normalized() * NonNegative(maxSpeed);

        /// <summary>Move directly away from a target at max speed.</summary>
        public static Vector2 Flee(Vector2 pos, Vector2 threat, float maxSpeed) =>
            !IsFinite(pos) || !IsFinite(threat) || NonNegative(maxSpeed) <= 0f || (pos - threat).LengthSquared() < 0.0001f
                ? Vector2.Zero
                : (pos - threat).Normalized() * NonNegative(maxSpeed);

        /// <summary>Seek that eases to a stop within <paramref name="slowRadius"/> of the target —
        /// the difference between "chase" and "walk up and stand next to".</summary>
        public static Vector2 Arrive(Vector2 pos, Vector2 target, float maxSpeed, float slowRadius)
        {
            if (!IsFinite(pos) || !IsFinite(target)) return Vector2.Zero;
            float speedLimit = NonNegative(maxSpeed);
            if (speedLimit <= 0f) return Vector2.Zero;
            Vector2 toTarget = target - pos;
            float dist = toTarget.Length();
            if (dist < 0.0001f) return Vector2.Zero;
            // Ramp speed down to 0 as dist → 0; full speed beyond slowRadius.
            float speed = speedLimit * Mathf.Clamp(dist / Mathf.Max(NonNegative(slowRadius), 0.0001f), 0f, 1f);
            return toTarget / dist * speed;
        }

        /// <summary>Flee that falls off with distance — full force inside <paramref name="panicRadius"/>,
        /// none outside it. Returns Zero when the threat is out of range so callers can sum forces
        /// without a spurious constant push.</summary>
        public static Vector2 Avoid(Vector2 pos, Vector2 threat, float maxSpeed, float panicRadius)
        {
            if (!IsFinite(pos) || !IsFinite(threat)) return Vector2.Zero;
            float speedLimit = NonNegative(maxSpeed);
            float radius = NonNegative(panicRadius);
            if (speedLimit <= 0f || radius <= 0f) return Vector2.Zero;
            Vector2 away = pos - threat;
            float dist = away.Length();
            if (!float.IsFinite(dist) || dist >= radius || dist < 0.0001f) return Vector2.Zero;
            float strength = 1f - dist / radius;   // 1 at the threat, 0 at the edge
            return away / dist * speedLimit * strength;
        }

        /// <summary>Smooth random wander via a wander ring: project a circle ahead of the agent,
        /// nudge a point on it by a random angle each call, steer toward that point. Returns the
        /// new heading; the caller keeps <paramref name="wanderAngle"/> between calls.</summary>
        public static Vector2 Wander(Vector2 velocity, ref float wanderAngle, float maxSpeed,
            float ringDistance = 40f, float ringRadius = 25f, float jitter = 0.4f)
        {
            float speedLimit = NonNegative(maxSpeed);
            if (speedLimit <= 0f) return Vector2.Zero;
            if (!float.IsFinite(wanderAngle)) wanderAngle = 0f;
            wanderAngle += (GD.Randf() * 2f - 1f) * FiniteOr(jitter, 0f);
            Vector2 forward = IsFinite(velocity) && velocity.LengthSquared() > 0.0001f ? velocity.Normalized() : Vector2.Right;
            Vector2 ringCenter = forward * NonNegative(ringDistance);
            Vector2 displacement = Vector2.FromAngle(wanderAngle) * NonNegative(ringRadius);
            Vector2 desired = (ringCenter + displacement).LengthSquared() > 0.0001f
                ? (ringCenter + displacement).Normalized() * speedLimit
                : forward * speedLimit;
            return desired;
        }

        /// <summary>Cap a velocity to max speed, preserving direction. Compose-forces helper.</summary>
        public static Vector2 Limit(Vector2 velocity, float maxSpeed)
        {
            if (!IsFinite(velocity)) return Vector2.Zero;
            maxSpeed = NonNegative(maxSpeed);
            if (maxSpeed <= 0f) return Vector2.Zero;
            float sq = velocity.LengthSquared();
            if (sq <= maxSpeed * maxSpeed) return velocity;
            return velocity / Mathf.Sqrt(sq) * maxSpeed;
        }

        private static float NonNegative(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static float FiniteOr(float value, float fallback) =>
            float.IsFinite(value) ? value : fallback;

        private static bool IsFinite(Vector2 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
