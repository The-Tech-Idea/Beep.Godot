using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Temperature system that tracks character core temperature and applies debuffs.
    /// Attach to a character or player node.
    ///
    /// Temperature states:
    ///   Frozen (temp &lt; 0°C): 50% movement speed penalty, 0.5 hp/sec damage
    ///   Cold (0°C - 10°C): 20% movement speed penalty, 0.2 hp/sec damage
    ///   Normal (10°C - 35°C): No penalties
    ///   Overheating (35°C - 45°C): 10% movement speed penalty, stamina drain
    ///   HeatStroke (temp &gt; 45°C): 50% movement speed penalty, 1 hp/sec damage
    ///
    /// Integrates with HealthComponent for damage application.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class TemperatureComponent : ControllerComponent
    {
        public enum TemperatureState
        {
            Normal,
            Cold,
            Frozen,
            Overheating,
            HeatStroke
        }

        [ExportGroup("Temperature")]
        [Export] public float CurrentTemp { get; set; } = 20f;
        [Export] public float MinTemp { get; set; } = -40f;
        [Export] public float MaxTemp { get; set; } = 55f;
        [Export] public float AmbientTemp { get; set; } = 20f;

        [ExportGroup("Thresholds")]
        [Export] public float FrozenThreshold { get; set; } = 0f;
        [Export] public float ColdThreshold { get; set; } = 10f;
        [Export] public float OverheatThreshold { get; set; } = 35f;
        [Export] public float HeatStrokeThreshold { get; set; } = 45f;

        [ExportGroup("Damage")]
        [Export] public float FrozenDamagePerSec { get; set; } = 0.5f;
        [Export] public float ColdDamagePerSec { get; set; } = 0.2f;
        [Export] public float HeatStrokeDamagePerSec { get; set; } = 1.0f;

        [ExportGroup("Movement Penalties")]
        [Export] public float FrozenSpeedPenalty { get; set; } = 0.5f;  // 50% of normal
        [Export] public float ColdSpeedPenalty { get; set; } = 0.8f;    // 80% of normal
        [Export] public float OverheatSpeedPenalty { get; set; } = 0.9f;
        [Export] public float HeatStrokeSpeedPenalty { get; set; } = 0.5f;

        [ExportGroup("Temperature Recovery")]
        [Export] public float TemperatureRecoveryRate { get; set; } = 0.5f;  // degrees per second
        [Export] public bool EnableAmbientTempInfluence { get; set; } = true;

        [ExportGroup("Atmosphere Influence")]
        /// <summary>When true, the ambient temperature the body drifts toward is modulated by the
        /// scene's weather, season, and day/night — so snow/winter/night actually make it colder.
        /// The atmosphere components are resolved from the current scene; when none are present the
        /// flat <see cref="AmbientTemp"/> is used. Temperature was previously disconnected from the
        /// atmosphere it shares a scene with — AmbientTemp was a static value nothing updated.</summary>
        [Export] public bool EnableAtmosphereInfluence { get; set; } = true;
        [Export] public float WinterTempOffset { get; set; } = -18f;
        [Export] public float SummerTempOffset { get; set; } = 8f;
        [Export] public float SnowTempOffset { get; set; } = -12f;
        [Export] public float StormTempOffset { get; set; } = -6f;
        [Export] public float RainTempOffset { get; set; } = -3f;
        [Export] public float SandstormTempOffset { get; set; } = 12f;
        [Export] public float NightTempOffset { get; set; } = -6f;

        [ExportGroup("Vignette")]
        /// <summary>CanvasLayer index for the full-screen warning vignette. Exported (was a
        /// hardcoded 200) so it can be set relative to other overlays.</summary>
        [Export] public int VignetteLayerIndex { get; set; } = 200;

        [Signal] public delegate void TemperatureChangedEventHandler(float temp, int state);
        [Signal] public delegate void TemperatureDamageEventHandler(float damage);

        private TemperatureState _currentState = TemperatureState.Normal;
        private double _damageAccumulator = 0;
        private HealthComponent? _health;
        private StatsComponent? _stats;
        private WeatherSystemComponent? _weather;
        private SeasonalComponent? _seasonal;
        private DayNightCycleComponent? _dayNight;
        private bool _speedWarned;
        private CanvasLayer? _vignetteLayer;
        private ColorRect? _vignetteRect;

        public override void _Ready()
        {
            base._Ready();
            CurrentTemp = Mathf.Clamp(CurrentTemp, MinTemp, MaxTemp);
            // Runtime only: SetupVignette adds a CanvasLayer to the scene root.
            if (Engine.IsEditorHint()) return;
            // GetNodeOrNull, not GetNode: the throwing variant meant the "../Health" absent
            // case errored before the "?? .." fallback could run.
            _health = GetNodeOrNull<HealthComponent>("../Health") ?? GetSiblingComponent<HealthComponent>();
            if (_health == null)
                GD.PushWarning($"[{Name}] TemperatureComponent found no HealthComponent (at ../Health or as a sibling) — frozen/heatstroke damage will never apply. Attach it to a character that has a HealthComponent.");
            _stats = GetSiblingComponent<StatsComponent>();
            Callable.From(SetupVignette).CallDeferred();
        }

        /// <summary>Resolve the scene's atmosphere so weather/season/night can drive ambient
        /// temperature. Deferred (from SetupVignette) rather than done in _Ready: the atmosphere is
        /// often instanced into the scene AFTER this entity, so a synchronous _Ready lookup returned
        /// null and temperature silently fell back to the flat AmbientTemp.</summary>
        private void ResolveAtmosphere()
        {
            if (GetTree()?.CurrentScene is { } scene)
            {
                _weather = EntityComponent.FindComponent<WeatherSystemComponent>(scene, true);
                _seasonal = EntityComponent.FindComponent<SeasonalComponent>(scene, true);
                _dayNight = EntityComponent.FindComponent<DayNightCycleComponent>(scene, true);
            }
        }

        // Meta key on the shared layer counting how many temperature entities use it.
        private const string VignetteRefMeta = "beep_temp_vignette_refs";

        private void SetupVignette()
        {
            ResolveAtmosphere();   // deferred, so the atmosphere is in the tree by now
            // One shared vignette layer, adopted if the scene already has it — several
            // temperature entities (player + companions) would otherwise each stack their
            // own full-screen ColorRect at the same layer index and fight. Named so the
            // adopt-or-create works; index is exported.
            var root = GetTree().Root;
            _vignetteLayer = root.GetNodeOrNull<CanvasLayer>("TemperatureVignette");
            if (_vignetteLayer != null)
            {
                _vignetteRect = _vignetteLayer.GetNodeOrNull<ColorRect>("VignetteRect");
            }
            else
            {
                _vignetteLayer = new CanvasLayer { Name = "TemperatureVignette", Layer = VignetteLayerIndex };
                root.AddChild(_vignetteLayer);
            }

            if (_vignetteRect == null)
            {
                _vignetteRect = new ColorRect
                {
                    Name = "VignetteRect",
                    Color = new Color(1, 1, 1, 0),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                _vignetteRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                _vignetteLayer.AddChild(_vignetteRect);
            }

            // Reference-count users of the shared layer. It lives on the window root, so
            // without this it leaks across every scene change (and stays tinted in the
            // menus). _ExitTree frees it when the last temperature entity is gone.
            int refs = (int)_vignetteLayer.GetMeta(VignetteRefMeta, 0);
            _vignetteLayer.SetMeta(VignetteRefMeta, refs + 1);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive) return;

            // Update temperature toward the (possibly atmosphere-modulated) ambient
            if (EnableAmbientTempInfluence)
            {
                float ambient = EffectiveAmbient();
                float delta_temp = (ambient - CurrentTemp) * (float)delta * TemperatureRecoveryRate;
                CurrentTemp = Mathf.Clamp(CurrentTemp + delta_temp, MinTemp, MaxTemp);
            }

            // Check state and apply debuffs
            TemperatureState newState = GetTemperatureState();
            if (newState != _currentState)
            {
                _currentState = newState;
                EmitSignal(SignalName.TemperatureChanged, CurrentTemp, (int)_currentState);
                ApplyMovementModifier();
            }

            // Apply damage over time
            _damageAccumulator += delta;
            if (_damageAccumulator >= 1.0)
            {
                ApplyTemperatureDamage();
                _damageAccumulator = 0;
            }

            // Update vignette color
            UpdateTemperatureVignette();
        }

        /// <summary>The ambient temperature the body drifts toward, modulated by the scene's
        /// weather (snow/storm/rain colder, sandstorm hotter), season (winter colder, summer
        /// warmer), and day/night (night colder). Falls back to the flat AmbientTemp when the
        /// atmosphere isn't present or influence is off.</summary>
        private float EffectiveAmbient()
        {
            float ambient = AmbientTemp;
            if (!EnableAtmosphereInfluence) return ambient;

            if (_seasonal != null)
                ambient += _seasonal.CurrentSeason switch
                {
                    SeasonalComponent.Season.Winter => WinterTempOffset,
                    SeasonalComponent.Season.Summer => SummerTempOffset,
                    _ => 0f
                };
            if (_weather != null)
                ambient += _weather.CurrentWeather switch
                {
                    WeatherSystemComponent.WeatherType.Snow or WeatherSystemComponent.WeatherType.Hail => SnowTempOffset,
                    WeatherSystemComponent.WeatherType.Storm => StormTempOffset,
                    WeatherSystemComponent.WeatherType.Rain => RainTempOffset,
                    WeatherSystemComponent.WeatherType.Sandstorm => SandstormTempOffset,
                    _ => 0f
                };
            if (_dayNight != null)
            {
                float t = _dayNight.TimeOfDay;       // 0..24; roughly night before 6am / after 8pm
                if (t < 6f || t >= 20f) ambient += NightTempOffset;
            }
            return ambient;
        }

        public TemperatureState GetTemperatureState()
        {
            if (CurrentTemp < FrozenThreshold) return TemperatureState.Frozen;
            if (CurrentTemp < ColdThreshold) return TemperatureState.Cold;
            if (CurrentTemp > HeatStrokeThreshold) return TemperatureState.HeatStroke;
            if (CurrentTemp > OverheatThreshold) return TemperatureState.Overheating;
            return TemperatureState.Normal;
        }

        /// <summary>
        /// Get movement speed multiplier based on current temperature state.
        /// </summary>
        public float GetMovementSpeedMultiplier()
        {
            return _currentState switch
            {
                TemperatureState.Frozen => FrozenSpeedPenalty,
                TemperatureState.Cold => ColdSpeedPenalty,
                TemperatureState.Overheating => OverheatSpeedPenalty,
                TemperatureState.HeatStroke => HeatStrokeSpeedPenalty,
                _ => 1.0f
            };
        }

        /// <summary>Push the current temperature speed penalty into the sibling StatsComponent as a
        /// "move_speed" multiplier, so the controllers (which read that stat) actually slow down.
        /// Previously GetMovementSpeedMultiplier() had zero callers — the documented cold/heat
        /// slowdown never applied. Refreshed on every state change; removed by Source identity.</summary>
        private void ApplyMovementModifier()
        {
            float mult = GetMovementSpeedMultiplier();
            if (_stats == null)
            {
                if (mult < 1f && !_speedWarned)
                {
                    GD.PushWarning($"[{Name}] Temperature would apply a {mult:0.##}x movement penalty, but there is no sibling StatsComponent for it to affect — the cold/heat slowdown won't apply. Add a StatsComponent (the controllers read its move_speed stat).");
                    _speedWarned = true;
                }
                return;
            }
            _stats.RemoveBySource(this);
            if (mult < 1f)
                _stats.AddModifier(new StatModifier { Stat = "move_speed", Op = StatOp.Multiply, Amount = mult, Duration = -1f, Source = this });
        }

        private void ApplyTemperatureDamage()
        {
            float damage = _currentState switch
            {
                TemperatureState.Frozen => FrozenDamagePerSec,
                TemperatureState.Cold => ColdDamagePerSec,
                TemperatureState.HeatStroke => HeatStrokeDamagePerSec,
                _ => 0f
            };

            if (damage > 0 && _health != null)
            {
                _health.TakeDamage(new GameDamage(damage, DamageType.Physical));
                EmitSignal(SignalName.TemperatureDamage, damage);
            }
        }

        private void UpdateTemperatureVignette()
        {
            if (_vignetteRect == null) return;

            Color vignetteColor = _currentState switch
            {
                TemperatureState.Frozen => new Color(0.2f, 0.5f, 1.0f, 0.3f),  // Blue tint
                TemperatureState.Cold => new Color(0.4f, 0.7f, 1.0f, 0.15f),   // Light blue
                TemperatureState.Overheating => new Color(1.0f, 0.7f, 0.2f, 0.15f),  // Orange tint
                TemperatureState.HeatStroke => new Color(1.0f, 0.3f, 0.0f, 0.4f),    // Red tint
                _ => new Color(1, 1, 1, 0)  // Transparent
            };

            _vignetteRect.Color = vignetteColor;
        }

        /// <summary>
        /// Apply external temperature change (e.g., standing near fire).
        /// </summary>
        public void ApplyTemperatureChange(float delta_temp)
        {
            CurrentTemp = Mathf.Clamp(CurrentTemp + delta_temp, MinTemp, MaxTemp);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            // Withdraw our move_speed penalty so it doesn't linger on a pooled/reused entity.
            if (_stats != null && GodotObject.IsInstanceValid(_stats)) _stats.RemoveBySource(this);
            if (_vignetteLayer == null || !GodotObject.IsInstanceValid(_vignetteLayer)) return;

            int refs = (int)_vignetteLayer.GetMeta(VignetteRefMeta, 1) - 1;
            if (refs <= 0)
            {
                // Last temperature entity left — free the root overlay instead of leaking it.
                _vignetteLayer.QueueFree();
            }
            else
            {
                _vignetteLayer.SetMeta(VignetteRefMeta, refs);
                // Clear the shared tint so this departing entity doesn't leave the screen colored.
                if (_vignetteRect != null && GodotObject.IsInstanceValid(_vignetteRect))
                    _vignetteRect.Color = new Color(1, 1, 1, 0);
            }
            _vignetteLayer = null;
            _vignetteRect = null;
        }
    }
}
