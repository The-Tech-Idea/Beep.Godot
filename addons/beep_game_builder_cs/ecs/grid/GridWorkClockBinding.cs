using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Binds a component to the grid's work clock, and reports whether it found
    /// one. Held by composition rather than inherited, because the components
    /// that need it do not share a base: GridProductionComponent is a plain
    /// Node, GridWorkerComponent and GridExtractorComponent are not.
    ///
    /// Every timed grid subsystem needs the same two lines - subscribe to
    /// WorkTick, and self-tick when there is nothing to subscribe to - and five
    /// copies of that is exactly the duplication this codebase treats as a
    /// defect. The standalone fallback is what keeps template scenes and every
    /// headless probe running with no clock in the tree at all.
    /// </summary>
    internal sealed class GridWorkClockBinding
    {
        private GridWorkClockComponent? _clock;
        private GridWorkClockComponent.WorkTickEventHandler? _handler;

        /// <summary>
        /// True when a work clock is driving the owner. False means the owner
        /// must advance itself from its own frame delta.
        /// </summary>
        public bool FollowsClock => _clock != null && GodotObject.IsInstanceValid(_clock);

        /// <summary>
        /// Finds the work clock - the exported path first, then scene-wide - and
        /// routes its WorkTick to <paramref name="onTick"/>, which receives
        /// TURNS. Returns whether a clock was found, so the caller can decide to
        /// self-tick instead; nothing is inferred from silence.
        /// </summary>
        public bool Bind(Node owner, NodePath path, GridWorkClockComponent.WorkTickEventHandler onTick)
        {
            Unbind();

            _clock = GridWorkClockComponent.FindFor(owner, path);

            if (_clock == null)
                return false;

            _handler = onTick;
            _clock.WorkTick += _handler;
            return true;
        }

        public void Unbind()
        {
            if (_clock != null && GodotObject.IsInstanceValid(_clock) && _handler != null)
                _clock.WorkTick -= _handler;

            _clock = null;
            _handler = null;
        }

        /// <summary>
        /// Turns elapsed for a frame's delta, for the self-ticking fallback. One
        /// turn per second, and a single huge frame is clamped the way every
        /// other delta consumer in this addon clamps one.
        /// </summary>
        public static float TurnsForDelta(double delta)
            => double.IsFinite(delta) && delta > 0.0 ? (float)Math.Min(delta, 86400.0) : 0f;
    }
}
