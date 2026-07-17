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
        // A drifting density field tiled across the screen. The cloud shader renders it as
        // soft white blobs; the shadow shader renders the SAME field darkened and offset,
        // so the ground below appears dappled as clouds pass. Their density() functions
        // must stay identical or the shadows stop matching the clouds.
        //
        // Two ways to get that field:
        //   • procedural (default) — 5-octave FBM, two layers. No art needed, but it is the
        //     most expensive thing the weather system does and it cannot look like pixel art.
        //   • CloudTexture — a tiling image of real cloud shapes, sampled once. Cheaper and
        //     art-directable. This is art, NOT a noise source: it is deliberately not
        //     layered across scales the way the procedural path is, which would smear it.
        private const string CloudShader = @"
shader_type canvas_item;
uniform float coverage   = 0.55;   // 0 clear sky .. 1 overcast
uniform float scroll     = 0.0;    // accumulated time
uniform float wind_dir   = 0.0;    // radians
uniform float speed      = 0.04;   // drift speed
uniform vec4  cloud_col  : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4  shadow_col : source_color = vec4(0.0, 0.0, 0.0, 0.35);
// Camera position in screen-widths, pre-scaled by the caller's parallax factor. Distant
// sky barely shifts as the camera pans, so this is fed a much smaller factor than the
// ground shadows get — the classic 2D parallax cue.
uniform vec2  world_offset = vec2(0.0);
// Optional authored cloud texture. Must tile seamlessly — it is scrolled, so a
// non-tiling image shows a visible seam. Use the red channel as the density mask.
// Flagged explicitly rather than sniffing for a default-white sampler (the trick the
// fog shader uses) because clouds ARE white: white pixels in a real cloud texture
// would be mistaken for 'no texture bound' and fall back to procedural.
uniform sampler2D cloud_tex : hint_default_white, repeat_enable;
uniform bool  use_cloud_tex = false;
// How many times the cloud texture tiles across the pattern space. Lower = bigger,
// fewer clouds; higher = smaller, denser. Only used when use_cloud_tex is on.
uniform float tex_scale = 1.0;

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

// Density at p: one texture fetch when authored, otherwise ~10 noise() calls.
float density(vec2 p, vec2 dir){
    if (use_cloud_tex) {
        // ONE sample, preserving the artwork's shapes.
        //
        // The procedural path below blends two copies at different scales because that is
        // how FBM builds detail out of noise. Doing that to an authored image instead
        // smears two mismatched copies of your clouds over each other — you drew cloud
        // shapes, so the shader's job is to show them, not to treat them as a noise field.
        // (This is where fog and clouds part ways: fog's NoiseTexture really is a noise
        // source and wants the layered treatment; a cloud texture is art.)
        return texture(cloud_tex, p * tex_scale).r;
    }
    float n1 = fbm(p);
    float n2 = fbm(p * 1.8 + vec2(5.2, 1.3) + dir * scroll * speed * 1.4);
    return n1 * 0.6 + n2 * 0.4;
}

void fragment(){
    vec2 dir = vec2(cos(wind_dir), sin(wind_dir));
    vec2 p = (UV + world_offset) * 4.0 + dir * scroll * speed;

    float clouds = smoothstep(1.0 - coverage, 1.0 - coverage * 0.4, density(p, dir));

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
// Camera position in screen-widths. These overlays are screen-space, so without this
// the shadows stay glued to the camera and slide with the player instead of lying on
// the ground. Feeding the camera in anchors the pattern to world coordinates.
uniform vec2  world_offset = vec2(0.0);
// Same texture the cloud layer uses, so the shadows match the cloud shapes. See the
// cloud shader for why this is an explicit flag rather than a default-white check.
uniform sampler2D cloud_tex : hint_default_white, repeat_enable;
uniform bool  use_cloud_tex = false;
// How many times the cloud texture tiles across the pattern space. Lower = bigger,
// fewer clouds; higher = smaller, denser. Only used when use_cloud_tex is on.
uniform float tex_scale = 1.0;

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

// Must mirror the cloud shader's density() exactly, or the shadows stop matching.
float density(vec2 p, vec2 dir){
    if (use_cloud_tex) {
        // One sample — see the cloud shader for why an authored image is not blended
        // across scales the way the procedural noise is.
        return texture(cloud_tex, p * tex_scale).r;
    }
    float n1 = fbm(p);
    float n2 = fbm(p * 1.8 + vec2(5.2, 1.3) + dir * scroll * speed * 1.4);
    return n1 * 0.6 + n2 * 0.4;
}

void fragment(){
    vec2 dir = vec2(cos(wind_dir), sin(wind_dir));
    // Shadow offset so it trails the cloud slightly (sun-from-the-side feel).
    // world_offset anchors the pattern to the world, so the shadows stay put on the
    // ground as the camera moves and only drift with the wind.
    vec2 p = (UV + world_offset) * 4.0 + dir * scroll * speed + vec2(0.15, 0.08);

    float clouds = smoothstep(1.0 - coverage, 1.0 - coverage * 0.4, density(p, dir));

    COLOR = vec4(shadow_col.rgb, clouds * shadow_col.a);
}
";

        /// <summary>
        /// Optional authored cloud texture — a tiling image of actual cloud shapes. When
        /// set, the cloud and shadow layers sample it instead of generating noise in-shader.
        ///
        /// NOT the same thing as the fog's <see cref="NoiseTexture"/>, despite both being a
        /// Texture2D: that one is a *noise source* (a FastNoiseLite NoiseTexture2D) swapped
        /// in for the in-shader hash, and the fog still layers it like noise. This is *art*,
        /// so it is sampled once and its shapes are shown as drawn.
        ///
        /// Worth doing when any of these apply:
        ///  • Performance. The procedural path runs a 5-octave FBM twice per pixel, on two
        ///    fullscreen layers, every frame (~80 sin() per pixel — at 1080p that's ~166M
        ///    per frame). A texture is 2 fetches. This is the single most expensive thing
        ///    the weather system does, and it costs the same whether it's clear or overcast.
        ///  • Pixel art. GameInfo.PixelArt defaults to true, and smooth continuous FBM does
        ///    not read as pixel art. An authored texture does.
        ///  • Art direction. Coverage/wind/colour stay tunable either way, but only a
        ///    texture lets you choose the cloud SHAPE.
        ///
        /// Requirements: must tile seamlessly (it is scrolled — a non-tiling image shows a
        /// seam), and density is read from the RED channel. Leave null to keep the
        /// zero-asset procedural default.
        /// </summary>
        [ExportGroup("Cloud Texture")]
        [Export] public Texture2D? CloudTexture { get; set; }

        /// <summary>How many times <see cref="CloudTexture"/> tiles across the pattern.
        /// Lower = bigger, sparser clouds; higher = smaller, denser. Start at 1 and tune by
        /// eye — the right value depends on your texture's cloud size. Ignored when no
        /// texture is set.</summary>
        [Export(PropertyHint.Range, "0.1,8,0.05")] public float CloudTextureScale { get; set; } = 1.0f;

        // ── Parallax ──
        // The cloud/shadow overlays are screen-space (a CanvasLayer), which is right for
        // drawing over the whole viewport but means they don't move with the world on their
        // own. These factors feed the camera into the shaders so the patterns sit in world
        // space instead of sticking to the screen.
        //   0 = locked to the screen (old behaviour)
        //   1 = fully world-anchored — pans exactly opposite the camera
        [ExportGroup("Cloud Parallax")]
        /// <summary>Sky clouds are far away, so they shift only slightly as the camera pans.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float CloudParallax { get; set; } = 0.15f;

        /// <summary>Cloud shadows land on the ground, so they must track the world 1:1 —
        /// anything less and they visibly slide along with the player.</summary>
        [Export(PropertyHint.Range, "0,1,0.01")] public float CloudShadowParallax { get; set; } = 1.0f;

        /// <summary>Push the cloud texture (and its on/off flag) to a material.
        ///
        /// The dirty check is deliberately NOT done in here: this runs for both the cloud
        /// and shadow materials, so updating the cache mid-way would let the first call
        /// consume the change and leave the second material without the texture. The caller
        /// decides, then clears the flag once both are done.</summary>
        private void ApplyCloudTexture(ShaderMaterial mat, bool use, bool textureChanged)
        {
            mat.SetShaderParameter("use_cloud_tex", use);
            if (!use) return;

            mat.SetShaderParameter("tex_scale", CloudTextureScale);
            if (textureChanged && CloudTexture != null)
                mat.SetShaderParameter("cloud_tex", CloudTexture);
        }
        private Texture2D? _cachedCloudTexture;

        /// <summary>Camera position expressed in screen-widths, which is the unit the
        /// overlay shaders' UV-space offset expects. Zero when there's no active Camera2D
        /// (a fixed-screen game), which reduces to the previous screen-locked behaviour.</summary>
        private Vector2 CameraOffsetInScreens()
        {
            var viewport = GetViewport();
            if (viewport == null) return Vector2.Zero;

            var camera = viewport.GetCamera2D();
            if (camera == null) return Vector2.Zero;

            Vector2 size = viewport.GetVisibleRect().Size;
            if (size.X <= 0 || size.Y <= 0) return Vector2.Zero;

            return camera.GlobalPosition / size;
        }

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
            float targetCoverage = CloudCoverageAutoDriven ? GetCloudCoverageFor(CurrentWeather) : CloudCoverage;

            // Ease the visible coverage toward the target so clouds form/dissolve
            // smoothly rather than popping in when weather changes.
            _cloudAlphaCurrent = Mathf.Lerp(_cloudAlphaCurrent, targetCoverage, (float)delta * 0.6f);
            _cloudShadowAlphaCurrent = Mathf.Lerp(_cloudShadowAlphaCurrent, targetCoverage, (float)delta * 0.6f);

            // Camera position in screen-widths. The overlays are screen-space, so this is
            // what keeps the pattern tied to the world instead of to the viewport.
            Vector2 cameraOffset = CameraOffsetInScreens();
            bool useCloudTex = CloudTexture != null;
            // Evaluated once for both materials; cleared after both have been fed.
            bool cloudTexChanged = CloudTexture != _cachedCloudTexture;

            if (_cloudMat != null)
            {
                _cloudMat.SetShaderParameter("scroll", _cloudScroll);
                _cloudMat.SetShaderParameter("wind_dir", _windDirectionRad);
                _cloudMat.SetShaderParameter("coverage", _cloudAlphaCurrent);
                _cloudMat.SetShaderParameter("speed", CloudDriftSpeed);
                _cloudMat.SetShaderParameter("cloud_col", CloudColor);
                _cloudMat.SetShaderParameter("world_offset", cameraOffset * CloudParallax);
                ApplyCloudTexture(_cloudMat, useCloudTex, cloudTexChanged);
            }
            if (_cloudShadowMat != null)
            {
                _cloudShadowMat.SetShaderParameter("scroll", _cloudScroll);
                _cloudShadowMat.SetShaderParameter("wind_dir", _windDirectionRad);
                _cloudShadowMat.SetShaderParameter("coverage", _cloudShadowAlphaCurrent);
                _cloudShadowMat.SetShaderParameter("speed", CloudDriftSpeed);
                _cloudShadowMat.SetShaderParameter("shadow_col", CloudShadowColor);
                _cloudShadowMat.SetShaderParameter("world_offset", cameraOffset * CloudShadowParallax);
                ApplyCloudTexture(_cloudShadowMat, useCloudTex, cloudTexChanged);
            }

            // Both materials have now seen the change.
            if (cloudTexChanged) _cachedCloudTexture = CloudTexture;

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
