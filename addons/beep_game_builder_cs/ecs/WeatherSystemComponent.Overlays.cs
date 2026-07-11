using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Partial: cloud + cloud-shadow overlays and the smooth weather cross-fade.
    ///
    /// Kept separate from the main file because these are the heaviest pieces
    /// (two procedural canvas_item shaders + a transition tween) and they read
    /// cleaner on their own. They share the same node cache the main file builds.
    /// </summary>
    public partial class WeatherSystemComponent
    {
        // ── Cloud + shadow shader source ──
        // Both are faked-volumetric: drifting FBM noise tiles across the screen.
        // The cloud shader renders soft white blobs lit by a sun direction; the
        // shadow shader renders the same blob pattern darkened and offset, so the
        // land/water below appears dappled as clouds pass.
        private const string CloudShader = @"
shader_type canvas_item;
uniform float coverage   = 0.55;   // 0 clear sky .. 1 overcast
uniform float scroll     = 0.0;    // accumulated time
uniform float wind_dir   = 0.0;    // radians
uniform float speed      = 0.04;   // drift speed
uniform vec4  cloud_col  : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4  shadow_col : source_color = vec4(0.0, 0.0, 0.0, 0.35);

float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p){
    vec2 i = floor(p), f = fract(p);
    float a=hash(i), b=hash(i+vec2(1,0)), c=hash(i+vec2(0,1)), d=hash(i+vec2(1,1));
    vec2 u = f*f*(3.0-2.0*f);
    return mix(mix(a,b,u.x), mix(c,d,u.x), u.y);
}
float fbm(vec2 p){
    float v=0.0, a=0.5;
    for(int i=0;i<5;i++){ v += a*noise(p); p*=2.0; a*=0.5; }
    return v;
}

void fragment(){
    vec2 dir = vec2(cos(wind_dir), sin(wind_dir));
    vec2 p = UV * 4.0 + dir * scroll * speed;

    // Two layers at different scales/offsets for parallax depth.
    float n1 = fbm(p);
    float n2 = fbm(p * 1.8 + vec2(5.2, 1.3) + dir * scroll * speed * 1.4);
    float clouds = smoothstep(1.0 - coverage, 1.0 - coverage * 0.4, n1 * 0.6 + n2 * 0.4);

    COLOR = vec4(cloud_col.rgb, clouds * cloud_col.a);
}
";

        private const string CloudShadowShader = @"
shader_type canvas_item;
// Same FBM pattern as the cloud shader, but rendered as a dark multiplier so
// it reads as a shadow cast on whatever is drawn below. Offset slightly so the
// shadow trails the visible cloud (sun is never straight up).
uniform float coverage  = 0.55;
uniform float scroll    = 0.0;
uniform float wind_dir  = 0.0;
uniform float speed     = 0.04;
uniform vec4  shadow_col : source_color = vec4(0.0, 0.0, 0.0, 0.35);

float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p){
    vec2 i = floor(p), f = fract(p);
    float a=hash(i), b=hash(i+vec2(1,0)), c=hash(i+vec2(0,1)), d=hash(i+vec2(1,1));
    vec2 u = f*f*(3.0-2.0*f);
    return mix(mix(a,b,u.x), mix(c,d,u.x), u.y);
}
float fbm(vec2 p){
    float v=0.0, a=0.5;
    for(int i=0;i<5;i++){ v += a*noise(p); p*=2.0; a*=0.5; }
    return v;
}

void fragment(){
    vec2 dir = vec2(cos(wind_dir), sin(wind_dir));
    // Shadow offset so it trails the cloud slightly (sun-from-the-side feel).
    vec2 p = UV * 4.0 + dir * scroll * speed + vec2(0.15, 0.08);
    float n1 = fbm(p);
    float n2 = fbm(p * 1.8 + vec2(5.2, 1.3) + dir * scroll * speed * 1.4);
    float clouds = smoothstep(1.0 - coverage, 1.0 - coverage * 0.4, n1 * 0.6 + n2 * 0.4);

    COLOR = vec4(shadow_col.rgb, clouds * shadow_col.a);
}
";

        // ── Overlay nodes + materials (owned by this partial) ──
        private ColorRect? _cloudOverlay;
        private ColorRect? _cloudShadowOverlay;
        private ShaderMaterial? _cloudMat;
        private ShaderMaterial? _cloudShadowMat;
        private float _cloudScroll;
        private float _cloudAlphaCurrent = 0f;   // what the cloud layer currently shows
        private float _cloudShadowAlphaCurrent = 0f;

        // Wind direction in radians — derived from WindForce in _Process by the main file,
        // but the overlays read it here so they stay in sync with particle drift.
        private float _windDirectionRad = 0f;

        /// <summary>Build (or find) the cloud + cloud-shadow overlays on the parent.</summary>
        private void EnsureCloudOverlays(Node parent)
        {
            // Cloud layer (sits above the world, below the fog/flash overlays).
            _cloudOverlay = parent.GetNodeOrNull<ColorRect>("WeatherClouds");
            if (_cloudOverlay == null)
            {
                _cloudOverlay = new ColorRect
                {
                    Name = "WeatherClouds",
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                    Color = new Color(0, 0, 0, 0)
                };
                _cloudOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                parent.AddChild(_cloudOverlay);
            }
            if (_cloudOverlay.Material is not ShaderMaterial sm || sm.ResourceName != "WeatherCloudMat")
            {
                _cloudMat = new ShaderMaterial { ResourceName = "WeatherCloudMat", Shader = new Shader { Code = CloudShader } };
                _cloudOverlay.Material = _cloudMat;
            }
            else _cloudMat = sm;

            // Cloud-shadow layer (sits below the clouds, above the world — casts dapple).
            _cloudShadowOverlay = parent.GetNodeOrNull<ColorRect>("WeatherCloudShadows");
            if (_cloudShadowOverlay == null)
            {
                _cloudShadowOverlay = new ColorRect
                {
                    Name = "WeatherCloudShadows",
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                    Color = new Color(0, 0, 0, 0)
                };
                _cloudShadowOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                parent.AddChild(_cloudShadowOverlay);
            }
            if (_cloudShadowOverlay.Material is not ShaderMaterial ssm || ssm.ResourceName != "WeatherCloudShadowMat")
            {
                _cloudShadowMat = new ShaderMaterial { ResourceName = "WeatherCloudShadowMat", Shader = new Shader { Code = CloudShadowShader } };
                _cloudShadowOverlay.Material = _cloudShadowMat;
            }
            else _cloudShadowMat = ssm;
        }

        // ────────────────────────────────────────────────────────────────
        //  Per-frame cloud animation + wind-direction sync
        // ────────────────────────────────────────────────────────────────

        /// <summary>Called every frame by the main _Process to animate cloud drift.</summary>
        private void ProcessClouds(double delta)
        {
            if (_cloudMat == null && _cloudShadowMat == null) return;

            // Keep wind direction in sync with the WindForce vector the main file mutates.
            if (WindForce != Vector2.Zero)
                _windDirectionRad = Mathf.Atan2(WindForce.Y, Mathf.Abs(WindForce.X) + 0.0001f);

            _cloudScroll += (float)delta;
            float cover = GetCloudCoverageFor(CurrentWeather);

            // Ease the visible coverage toward the target so clouds form/dissolve
            // smoothly rather than popping in when weather changes.
            _cloudAlphaCurrent = Mathf.Lerp(_cloudAlphaCurrent, cover, (float)delta * 0.6f);
            _cloudShadowAlphaCurrent = Mathf.Lerp(_cloudShadowAlphaCurrent, cover, (float)delta * 0.6f);

            if (_cloudMat != null)
            {
                _cloudMat.SetShaderParameter("scroll", _cloudScroll);
                _cloudMat.SetShaderParameter("wind_dir", _windDirectionRad);
                _cloudMat.SetShaderParameter("coverage", CloudCoverage);
                _cloudMat.SetShaderParameter("speed", CloudDriftSpeed);
                _cloudMat.SetShaderParameter("cloud_col", CloudColor);
            }
            if (_cloudShadowMat != null)
            {
                _cloudShadowMat.SetShaderParameter("scroll", _cloudScroll);
                _cloudShadowMat.SetShaderParameter("wind_dir", _windDirectionRad);
                _cloudShadowMat.SetShaderParameter("coverage", CloudCoverage);
                _cloudShadowMat.SetShaderParameter("speed", CloudDriftSpeed);
                _cloudShadowMat.SetShaderParameter("shadow_col", CloudShadowColor);
            }

            // Modulate alpha via the ColorRect modulate so unused weather hides them.
            if (_cloudOverlay != null)
                _cloudOverlay.Modulate = new Color(1, 1, 1, _cloudAlphaCurrent);
            if (_cloudShadowOverlay != null)
                _cloudShadowOverlay.Modulate = new Color(1, 1, 1, _cloudShadowAlphaCurrent * CloudShadowStrength);
        }

        // 0 = no clouds, 1 = full overcast. Per-weather tuning.
        private static float GetCloudCoverageFor(WeatherType type) => type switch
        {
            WeatherType.Clear => 0.0f,
            WeatherType.Cloudy => 1.0f,
            WeatherType.Rain => 0.7f,
            WeatherType.Snow => 0.6f,
            WeatherType.Storm => 1.0f,
            WeatherType.Fog => 0.3f,
            WeatherType.Sandstorm => 0.2f,
            WeatherType.Hail => 0.8f,
            WeatherType.LeafFall => 0.2f,
            WeatherType.Heatwave => 0.0f,
            _ => 0.0f
        };
    }
}
