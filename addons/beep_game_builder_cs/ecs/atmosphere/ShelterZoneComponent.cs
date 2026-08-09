using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Drives <see cref="WeatherSystemComponent.InsideShelter"/> from an Area2D over a roof /
    /// building interior. Attach as a CHILD of the Area2D that marks the sheltered region:
    ///
    ///     House  (Area2D, with a CollisionShape2D covering the interior)
    ///     └─ ShelterZone  (Node, this component)
    ///
    /// While any watched body is inside, the weather's <c>InsideShelter</c> goes true, which eases
    /// <see cref="WeatherSystemComponent.ShelterFactor"/> up — precipitation particles stop and
    /// weather audio muffles over ~0.3s rather than snapping.
    ///
    /// This is the producer the weather system ships without: <c>InsideShelter</c> is documented
    /// as "yours to drive," and this is the stock driver for the common 2D case (a rectangular or
    /// polygonal roof footprint). Bodies are tracked by group (default the player); overlapping
    /// shelter zones refcounts occupants so two adjacent buildings don't fight over the bool.
    ///
    /// In the Add Node tree this appears as:
    ///   EntityComponent → AreaTriggerComponent → ShelterZoneComponent
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ShelterZoneComponent : AreaTriggerComponent
    {
        /// <summary>Only bodies in this group count as sheltered. Defaults to the player — an enemy
        /// walking under a roof should not stop the rain for the player. Empty watches every body.</summary>
        [Export] public string WatchGroup { get; set; } = "players";

        /// <summary>Optional explicit weather system. Null = auto-find via the "weather_system"
        /// group (the same discovery WindFieldComponent and WeatherHUDComponent use).</summary>
        [Export] public NodePath? WeatherSystemPath { get; set; }

        private WeatherSystemComponent? _weather;

        // Refcount of sheltered bodies, not a bool — two bodies inside one roof must keep the
        // weather sheltered until the LAST leaves, and OnBodyExited does not always fire before a
        // body is QueueFree'd, so entries are pruned for validity before counting.
        private readonly List<Node2D> _inside = new();

        public override void _Ready()
        {
            base._Ready();   // wires TriggerArea + body signals; warns if parent is not an Area2D
            _weather = ResolveWeatherSystem();
            if (_weather == null && !Engine.IsEditorHint())
                GD.PushWarning(
                    $"[{Name}] No WeatherSystemComponent found (no 'weather_system' group member, no " +
                    "WeatherSystemPath) — this zone tracks shelter but has nothing to drive. Add a " +
                    "WeatherSystemComponent to the scene.");
        }

        protected override void OnBodyEntered(Node2D body)
        {
            if (!IsActive || !Watches(body) || _inside.Contains(body)) return;
            _inside.Add(body);
            ApplyShelter();
        }

        protected override void OnBodyExited(Node2D body)
        {
            if (!_inside.Remove(body)) return;
            ApplyShelter();
        }

        /// <summary>Push the current occupancy into the weather system. Pruning freed bodies first
        /// keeps a QueueFree'd occupant (an enemy that died under the roof) from wedging the
        /// weather sheltered forever — BodyExited is not guaranteed to run before free.</summary>
        private void ApplyShelter()
        {
            if (_weather == null || !GodotObject.IsInstanceValid(_weather)) return;
            for (int i = _inside.Count - 1; i >= 0; i--)
                if (!GodotObject.IsInstanceValid(_inside[i])) _inside.RemoveAt(i);
            _weather.InsideShelter = _inside.Count > 0;
        }

        private bool Watches(Node2D body) =>
            string.IsNullOrEmpty(WatchGroup) || body.IsInGroup(WatchGroup);

        private WeatherSystemComponent? ResolveWeatherSystem()
        {
            if (WeatherSystemPath != null && GetNodeOrNull<WeatherSystemComponent>(WeatherSystemPath) is { } explicit_)
                return explicit_;
            var tree = GetTree();
            if (tree == null) return null;
            foreach (var n in tree.GetNodesInGroup("weather_system"))
                if (n is WeatherSystemComponent w) return w;
            return null;
        }

        public override void _ExitTree()
        {
            // Leaving the tree (scene teardown, level unload) must release the shelter, or the
            // weather system is left reporting InsideShelter=true for a roof that no longer exists.
            if (_inside.Count > 0)
            {
                _inside.Clear();
                if (_weather != null && GodotObject.IsInstanceValid(_weather))
                    _weather.InsideShelter = false;
            }
            base._ExitTree();
        }
    }
}
