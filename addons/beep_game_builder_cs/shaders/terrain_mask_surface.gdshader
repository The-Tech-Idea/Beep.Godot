shader_type canvas_item;

uniform bool surface_enabled = false;
uniform sampler2D surface_texture : source_color, repeat_enable, filter_nearest;
uniform vec3 mask_color : source_color = vec3(1.0, 0.0, 0.0);
uniform float mask_tolerance : hint_range(0.0, 0.1) = 0.001;
uniform vec2 surface_origin = vec2(0.0);
// UV cycles per world pixel. Set these to the inverse material-plane basis.
uniform vec2 surface_u = vec2(0.00390625, 0.0);
uniform vec2 surface_v = vec2(0.0, 0.00390625);
varying vec2 surface_uv;
varying vec4 item_tint;

void vertex() {
    vec2 position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy - surface_origin;
    surface_uv = vec2(dot(position, surface_u), dot(position, surface_v));
    item_tint = COLOR;
}

void fragment() {
    vec4 base = texture(TEXTURE, UV);
    vec3 difference = abs(base.rgb - mask_color);
    bool masked = max(max(difference.r, difference.g), difference.b) <= mask_tolerance;
    vec3 rgb = base.rgb;
    if (surface_enabled && masked && base.a > 0.0) {
        // Surface art changes RGB only: atlas coverage remains authoritative.
        rgb = texture(surface_texture, surface_uv).rgb;
    }
    COLOR = vec4(rgb * item_tint.rgb, base.a * item_tint.a);
}
