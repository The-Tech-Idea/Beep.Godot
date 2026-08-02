using System;
using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Partial: weather intensity engine + global shader parameters.
    ///
    /// This is the single most important architectural piece the system was
    /// missing. Instead of a binary "rain on / rain off", weather now has a
    /// smooth 0..1 intensity that cross-fades EVERYTHING driven by the weather:
    ///   • particle emission count (amount_ratio equivalent)
    ///   • fog overlay density
    ///   • ambient tint (lerped clear→weather tint by intensity)
    ///   • wind strength
    ///   • global shader uniforms so ground/foliage shaders can react
    ///     (puddle_depth grows in rain, snow_accumulation grows in snow, etc.)
    ///
    /// `TransitionTo(weather, duration)` coordinates a fade-out → switch →
    /// fade-in so switching weather is a deliberate cinematic cross-fade, not
    /// an instant pop. Direct `SetWeather()` remains available for snaps.
    ///
    /// Global shader parameters are registered on first use and updated every
    /// frame. ANY canvas_item shader in the project can read them by declaring
    /// `global uniform float puddle_depth;` etc. — no per-node wiring needed.
    /// Pattern taken from the weathersystem.txt production reference.
    /// </summary>
    public partial class WeatherSystemComponent
    {
        // Last CpuParticles2D.Amount actually applied — so ProcessIntensity only re-sets it on a
        // change (each set reallocates the pool). Reset to -1 on emitter rebuild to force re-apply.
        private int _lastParticleAmount = -1;

        // ── Intensity exports ──
        [ExportGroup("Intensity")]
        /// <summary>Current intensity 0..1. Read-only at runtime (driven by transitions).</summary>
        [Export] public float WeatherIntensity { get; set; } = 0f;
        /// <summary>How hard the current weather pushes when fully intense.</summary>
        [Export] public float TargetIntensity { get; set; } = 1f;
        /// <summary>Lerp speed for the intensity value toward its target (per second).</summary>
        [Export] public float IntensityLerpSpeed { get; set; } = 1.5f;

        [ExportGroup("Global Shader Params")]
        /// <summary>
        /// When true, the system publishes wind/puddle/snow/intensity as global
        /// shader uniforms every frame so any canvas_item shader in the project
        /// can react without per-node wiring.
        /// </summary>
        [Export] public bool PublishGlobalShaderParams { get; set; } = true;

        [Signal] public delegate void IntensityChangedEventHandler(float value);

        // Global shader parameter names — kept as constants so the spelling is
        // guaranteed to match what a consuming shader declares.
        public const string ParamWindStrength = "beep_wind_strength";
        public const string ParamWindX = "beep_wind_x";
        public const string ParamPuddleDepth = "beep_puddle_depth";
        public const string ParamSnowAccumulation = "beep_snow_accumulation";
        public const string ParamWeatherIntensity = "beep_weather_intensity";

        // ── Transition state ──
        private bool _transitioning;
        private float _transitionTargetIntensity;
        private WeatherType _pendingWeather;
        private bool _globalsRegistered;

        /// <summary>Smooth intensity value currently applied (eased toward TargetIntensity).</summary>
        private float _intensityCurrent;

        /// <summary>
        /// Cross-fade to a new weather over `duration` seconds: fade the current
        /// weather's intensity to 0, switch, then ramp to full. Audio/particles/
        /// fog/ambient all follow the intensity so the whole scene cross-fades.
        /// </summary>
        public async void TransitionTo(WeatherType newWeather, float duration = 3f, float targetIntensity = 1f)
        {
            if (CurrentWeather == newWeather && Math.Abs(WeatherIntensity - targetIntensity) < 0.01f) return;

            _transitioning = true;
            _pendingWeather = newWeather;
            _transitionTargetIntensity = targetIntensity;

            // Phase 1: fade out current weather.
            TargetIntensity = 0f;
            float half = Math.Max(0.05f, duration * 0.5f);
            await ToSignal(CreateTween().TweenInterval(half), "finished");
            if (!GodotObject.IsInstanceValid(this)) return;   // scene freed mid-transition (async void)

            // Phase 2: switch at zero intensity (no visible pop).
            SetWeather(newWeather);
            WeatherIntensity = 0f;
            _intensityCurrent = 0f;

            // Phase 3: fade in the new weather.
            TargetIntensity = targetIntensity;
            await ToSignal(CreateTween().TweenInterval(half), "finished");
            if (!GodotObject.IsInstanceValid(this)) return;

            _transitioning = false;
            EmitSignal(SignalName.IntensityChanged, _intensityCurrent);
        }

        // ════════════════════════════════════════════════════════════════
        //  Per-frame intensity update — called from the main _Process
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Ease the intensity toward its target, then derive everything that
        /// should scale with it: particle count, fog density, wind strength,
        /// ambient tint, and the global shader uniforms.
        /// </summary>
        private void ProcessIntensity(double delta)
        {
            // Ease the canonical intensity value.
            float before = _intensityCurrent;
            _intensityCurrent = Mathf.MoveToward(
                _intensityCurrent, TargetIntensity, IntensityLerpSpeed * (float)delta);
            WeatherIntensity = _intensityCurrent;

            if (!Mathf.IsEqualApprox(before, _intensityCurrent))
                EmitSignal(SignalName.IntensityChanged, _intensityCurrent);

            // ── Particle count scales with intensity ──
            // A rain at 30% intensity emits ~30% of the configured ParticleCount. Assign ONLY on a
            // change: CpuParticles2D.Amount reallocates + resets the whole pool on every set (no
            // same-value early-out in Godot 4), so writing it each frame reset the pool every frame —
            // precipitation flickered at the emitter and never fell (which also masked the per-type
            // Lifetime tuning). Cache the last applied amount.
            // SHELTER, eased. Cutting precipitation off on the frame the player crosses a
            // doorway reads as a glitch, so the bool slides into a 0-1 factor over ~0.3s and
            // everything downstream scales by it. It multiplies intensity rather than replacing
            // it: standing indoors during a light shower must not look like standing indoors
            // during a storm.
            ShelterFactor = Mathf.MoveToward(ShelterFactor, InsideShelter ? 1f : 0f, 3.5f * (float)delta);
            float exposure = _intensityCurrent * (1f - ShelterFactor);

            if (_particles != null && _particles.Emitting)
            {
                int amount = Mathf.Max(1, (int)(ParticleCount * exposure));
                if (amount != _lastParticleAmount)
                {
                    _particles.Amount = amount;
                    _lastParticleAmount = amount;
                }
            }

            // Splashes are impacts on ground you can see — under a roof there are none. Emitting
            // is a cheap toggle (no pool realloc), but guard it anyway so it only writes on a
            // change, matching the Amount discipline above.
            if (_splashes != null)
            {
                bool wet = ShelterFactor < 0.5f && _splashWanted;
                if (wet != _splashes.Emitting) _splashes.Emitting = wet;
            }

            // (Fog density now lives in the standalone DynamicFogLayer, which reads
            // WeatherIntensity directly — see DynamicFogLayer.FogWeightFor.)

            // ── Publish global shader uniforms so any shader can react ──
            if (PublishGlobalShaderParams) PublishGlobals(delta);
        }

        /// <summary>
        /// Push the current wind/intensity/puddle/snow values to global shader
        /// uniforms. Registers the parameter names on first call (idempotent).
        /// Consuming shaders declare e.g. `global uniform float beep_puddle_depth;`.
        /// </summary>
        private void PublishGlobals(double delta)
        {
            if (!_globalsRegistered)
            {
                // Register each as an actual RENDERING global. Setting a plain ProjectSettings key
                // does NOT create one, so GlobalShaderParameterSet below was a silent no-op and no
                // shader ever received these values. GlobalShaderParameterAdd is what a shader's
                // `global uniform float beep_puddle_depth;` resolves against.
                // GlobalShaderParameterGet is EDITOR-ONLY. Godot answers a runtime call with
                // "This function should never be used outside the editor, it can severely damage
                // performance." -- once per name, so every genre main logged five engine errors
                // at startup. The existence CHECK is what was editor-only; Add itself is not.
                //
                // GlobalShaderParameterAdd is already idempotent (re-adding a name replaces its
                // declaration), and `_globalsRegistered` guards the block anyway, so the check
                // was buying nothing even in the editor.
                //
                // Found by bisection, not by reading: removing the Weather node silenced the
                // errors, then stubbing _Process silenced them, then this loop. Two earlier
                // guesses -- five particle textures, five exported Texture2D properties -- each
                // matched the count of five exactly and were each disproved by a direct test.
                foreach (var name in new[]{
                    ParamWindStrength, ParamWindX, ParamPuddleDepth,
                    ParamSnowAccumulation, ParamWeatherIntensity })
                {
                    RenderingServer.GlobalShaderParameterAdd(
                        name, RenderingServer.GlobalShaderParameterType.Float, 0f);
                }
                _globalsRegistered = true;
            }

            RenderingServer.GlobalShaderParameterSet(ParamWeatherIntensity, _intensityCurrent);
            RenderingServer.GlobalShaderParameterSet(ParamWindStrength, WindForce.Length());
            RenderingServer.GlobalShaderParameterSet(ParamWindX, WindForce.X);

            // Puddle depth grows during rain/storm and slowly evaporates otherwise.
            // Snow accumulation grows during snow and melts otherwise. Both are
            // eased per-frame so they feel like a slow environmental response.
            // Ease at a per-SECOND rate (× delta), not a baked-in per-frame step — the old
            // 0.05f * 0.016f assumed 60 fps, so accumulation drifted with framerate. (0.016×60 ≈ 1,
            // so the per-second coefficients below match the old feel.)
            float puddleTarget = CurrentWeather is WeatherType.Rain or WeatherType.Storm
                ? _intensityCurrent : 0f;
            _puddleDepth = Mathf.MoveToward(_puddleDepth, puddleTarget, 0.05f * (float)delta);
            RenderingServer.GlobalShaderParameterSet(ParamPuddleDepth, _puddleDepth);

            float snowTarget = CurrentWeather == WeatherType.Snow ? _intensityCurrent : 0f;
            _snowAccumulation = Mathf.MoveToward(_snowAccumulation, snowTarget, 0.02f * (float)delta);
            RenderingServer.GlobalShaderParameterSet(ParamSnowAccumulation, _snowAccumulation);
        }

        // Slow environmental accumulators — eased in PublishGlobals.
        private float _puddleDepth;
        private float _snowAccumulation;
    }
}
