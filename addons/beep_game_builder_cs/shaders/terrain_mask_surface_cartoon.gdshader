shader_type canvas_item;

uniform bool surface_enabled = false;
uniform sampler2D surface_texture : source_color, repeat_enable, filter_linear;
uniform vec3 mask_color : source_color = vec3(1.0, 0.0, 0.0);
uniform bool secondary_enabled = false;
uniform sampler2D secondary_texture : source_color, repeat_enable, filter_linear;
uniform vec3 secondary_mask_color : source_color = vec3(0.0, 0.0, 1.0);
uniform float mask_tolerance : hint_range(0.0, 0.1) = 0.001;
uniform vec2 surface_origin = vec2(0.0);
uniform vec2 surface_u = vec2(0.00390625, 0.0);
uniform vec2 surface_v = vec2(0.0, 0.00390625);
varying vec2 surface_uv;
varying vec4 item_tint;

void vertex() {
    vec2 position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy - surface_origin;
    surface_uv = vec2(dot(position, surface_u), dot(position, surface_v));
    item_tint = COLOR;
}

bool matches_mask(vec3 base, vec3 marker) {
    vec3 difference = abs(base - marker);
    return max(max(difference.r, difference.g), difference.b) <= mask_tolerance;
}

void fragment() {
    // Keep atlas sampling nearest on the layer; only the surface samplers are linear.
    vec4 base = texture(TEXTURE, UV);
    vec3 rgb = base.rgb;
    if (base.a > 0.0) {
        if (surface_enabled && matches_mask(base.rgb, mask_color)) {
            rgb = texture(surface_texture, surface_uv).rgb;
        } else if (secondary_enabled && matches_mask(base.rgb, secondary_mask_color)) {
            rgb = texture(secondary_texture, surface_uv).rgb;
        }
    }
    COLOR = vec4(rgb * item_tint.rgb, base.a * item_tint.a);
}
