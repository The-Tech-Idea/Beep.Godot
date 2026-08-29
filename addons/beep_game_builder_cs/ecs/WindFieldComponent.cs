using System.Collections.Generic;
using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Physical wind field driven by a <see cref="WeatherSystemComponent"/>.
    /// Converts the weather's data-only <c>WindForce</c> into actual physics
    /// forces on bodies inside an <see cref="Area2D"/>.
    ///
    /// Two pathways (Area2D gravity only auto-affects RigidBodies):
    ///   • <b>RigidBody2D</b> (crates, debris, dropped items): pushed automatically
    ///     via the Area2D's gravity space override — zero gameplay code needed.
    ///   • <b>CharacterBody2D</b> (player, enemies): <c>move_and_slide</c> ignores
    ///     area gravity, so this component tracks bodies that enter the field and
    ///     applies a velocity push to them in <c>_PhysicsProcess</c>.
    ///
    /// Attach to an <see cref="Area2D"/> node with a large <see cref="CollisionShape2D"/>
    /// spanning the level. Then add a <see cref="WeatherSystemComponent"/> somewhere
    /// in the scene and assign it (or leave blank to auto-find via group).
    ///
    /// Pattern from the weathersystem.txt production reference: Area2D as a global
    /// wind tunnel whose gravity direction/magnitude mirrors the weather wind.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WindFieldComponent : WorldComponent
    {
        /// <summary>Auto-find the WeatherSystemComponent in the scene tree if not assigned.</summary>
        [Export] public NodePath? WeatherSystemPath { get; set; }
        /// <summary>
        /// Multiplier on the weather's WindForce when applied to physics bodies.
        /// WeatherSystemComponent.WindForce is normalized-ish (±1 range from its
        /// random walk); scale up here for a visible physical push.
        /// </summary>
        [Export] public float PhysicsWindScale { get; set; } = 400f;
        /// <summary>Velocity added per second to CharacterBody2Ds in the field.</summary>
        [Export] public float CharacterPushAccel { get; set; } = 250f;
        /// <summary>
        /// When true, CharacterBody2Ds are only pushed while airborne (so wind
        /// doesn't drag a grounded player around). Off = always push.
        /// </summary>
        [Export] public bool OnlyPushAirborne { get; set; } = true;
        /// <summary>
        /// Maximum horizontal wind speed for CharacterBody2Ds (prevents infinite acceleration).
        /// </summary>
        [Export] public float MaxCharacterWindSpeed { get; set; } = 300f;

        private Area2D? _area;
        private WeatherSystemComponent? _weather;
        private readonly List<CharacterBody2D> _characters = new();

        public float EffectivePhysicsWindScale => NonNegative(PhysicsWindScale);
        public float EffectiveCharacterPushAccel => NonNegative(CharacterPushAccel);
        public float EffectiveMaxCharacterWindSpeed => NonNegative(MaxCharacterWindSpeed);

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            _area = GetParent() as Area2D;
            if (_area == null)
            {
                GD.PushWarning($"[WindField] Must be a child of an Area2D. Parent was {GetParent()?.GetType().Name ?? "null"}.");
                return;
            }

            // Auto-discover the weather system if not wired.
            if (WeatherSystemPath != null && !WeatherSystemPath.IsEmpty)
                _weather = GetNodeOrNull<WeatherSystemComponent>(WeatherSystemPath);
            _weather ??= FindWeatherSystem();
            if (_weather == null)
                GD.PushWarning($"[{Name}] No WeatherSystemComponent found (no 'weather_system' group member, no WeatherSystemPath) — this wind field mirrors the weather's WindForce, so it will push nothing. Add a WeatherSystemComponent to the scene.");

            // Combine so wind ADDS to normal downward gravity rather than replacing it.
            if (_area.GravitySpaceOverride == Area2D.SpaceOverride.Disabled)
                _area.GravitySpaceOverride = Area2D.SpaceOverride.Combine;

            // Track CharacterBody2D entries/exits so we can push them manually.
            _area.BodyEntered += OnBodyEntered;
            _area.BodyExited += OnBodyExited;
        }

        public override void _ExitTree()
        {
            if (_area != null && GodotObject.IsInstanceValid(_area))
            {
                _area.BodyEntered -= OnBodyEntered;
                _area.BodyExited -= OnBodyExited;
            }
            base._ExitTree();
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive || _area == null || !GodotObject.IsInstanceValid(_area)) return;
            PruneCharacters();
            if (_weather == null || !GodotObject.IsInstanceValid(_weather))
            {
                _weather = FindWeatherSystem();
                if (_weather == null)
                {
                    _area.Gravity = 0f;
                    _area.GravityDirection = Vector2.Zero;
                    return;
                }
            }

            // Mirror the weather wind into the Area2D gravity (drives RigidBodies).
            Vector2 sourceWind = IsFinite(_weather.WindForce) ? _weather.WindForce : Vector2.Zero;
            Vector2 wind = sourceWind * EffectivePhysicsWindScale;
            _area.Gravity = wind.Length();
            _area.GravityDirection = wind.Length() > 0.001f
                ? wind.Normalized()
                : Vector2.Zero;

            // Manually push CharacterBody2Ds (they ignore area gravity).
            float dt = DeltaSeconds(delta);
            // Iterate backwards so we can prune bodies freed inside the field. BodyExited
            // does not always fire before a body is QueueFree'd (e.g. an enemy dies in the
            // wind), which would leave a disposed reference here — touching it throws.
            for (int i = _characters.Count - 1; i >= 0; i--)
            {
                var body = _characters[i];
                if (!GodotObject.IsInstanceValid(body))
                {
                    _characters.RemoveAt(i);
                    continue;
                }
                if (OnlyPushAirborne && body.IsOnFloor()) continue;
                // Apply horizontal wind push to the character's velocity with clamping.
                var v = IsFinite(body.Velocity) ? body.Velocity : Vector2.Zero;
                v.X += wind.X * 0.01f * EffectiveCharacterPushAccel * dt;
                v.X = Mathf.Clamp(v.X, -EffectiveMaxCharacterWindSpeed, EffectiveMaxCharacterWindSpeed);
                body.Velocity = v;
            }
        }

        // ── Discovery + body tracking ──

        private void PruneCharacters()
        {
            for (int i = _characters.Count - 1; i >= 0; i--)
            {
                var body = _characters[i];
                if (!GodotObject.IsInstanceValid(body))
                    _characters.RemoveAt(i);
            }
        }

        private WeatherSystemComponent? FindWeatherSystem()
        {
            var tree = GetTree();
            if (tree == null) return null;
            // WeatherSystemComponent unconditionally self-registers into the "weather_system"
            // group in its _Ready() regardless of ComponentGroup; fall back to a tree scan.
            foreach (var n in tree.GetNodesInGroup("weather_system"))
                if (n is WeatherSystemComponent w) return w;
            return tree.Root.FindChild("WeatherSystemComponent", true, false) as WeatherSystemComponent;
        }

        private void OnBodyEntered(Node body)
        {
            if (body is CharacterBody2D cb && !_characters.Contains(cb))
                _characters.Add(cb);
        }

        private void OnBodyExited(Node body)
        {
            if (body is CharacterBody2D cb)
                _characters.Remove(cb);
        }

        private static float DeltaSeconds(double delta) =>
            double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

        private static float NonNegative(float value) =>
            float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

        private static bool IsFinite(Vector2 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
