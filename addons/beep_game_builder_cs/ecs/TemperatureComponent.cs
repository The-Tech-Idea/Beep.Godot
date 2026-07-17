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

        [ExportGroup("Vignette")]
        /// <summary>CanvasLayer index for the full-screen warning vignette. Exported (was a
        /// hardcoded 200) so it can be set relative to other overlays.</summary>
        [Export] public int VignetteLayerIndex { get; set; } = 200;

        [Signal] public delegate void TemperatureChangedEventHandler(float temp, int state);
        [Signal] public delegate void TemperatureDamageEventHandler(float damage);

        private TemperatureState _currentState = TemperatureState.Normal;
        private double _damageAccumulator = 0;
        private HealthComponent? _health;
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
            CallDeferred(nameof(SetupVignette));
        }

        private void SetupVignette()
        {
            // One shared vignette layer, adopted if the scene already has it — several
            // temperature entities (player + companions) would otherwise each stack their
            // own full-screen ColorRect at the same layer index and fight. Named so the
            // adopt-or-create works; index is exported.
            var root = GetTree().Root;
            _vignetteLayer = root.GetNodeOrNull<CanvasLayer>("TemperatureVignette");
            if (_vignetteLayer != null)
            {
                _vignetteRect = _vignetteLayer.GetNodeOrNull<ColorRect>("VignetteRect");
                if (_vignetteRect != null) return;   // already built by another instance
            }
            else
            {
                _vignetteLayer = new CanvasLayer { Name = "TemperatureVignette", Layer = VignetteLayerIndex };
                root.AddChild(_vignetteLayer);
            }

            _vignetteRect = new ColorRect
            {
                Name = "VignetteRect",
                Color = new Color(1, 1, 1, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _vignetteRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _vignetteLayer.AddChild(_vignetteRect);
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint() || !IsActive) return;

            // Update temperature toward ambient
            if (EnableAmbientTempInfluence)
            {
                float delta_temp = (AmbientTemp - CurrentTemp) * (float)delta * TemperatureRecoveryRate;
                CurrentTemp = Mathf.Clamp(CurrentTemp + delta_temp, MinTemp, MaxTemp);
            }

            // Check state and apply debuffs
            TemperatureState newState = GetTemperatureState();
            if (newState != _currentState)
            {
                _currentState = newState;
                EmitSignal(SignalName.TemperatureChanged, CurrentTemp, (int)_currentState);
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
                _health.TakeDamage(damage);
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
    }
}
