shader_type canvas_item;

uniform bool surface_enabled = false;
uniform sampler2D surface_texture : source_color, repeat_enable, filter_linear;
uniform sampler2D surface_mask : repeat_disable, filter_nearest;
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

void fragment() {
    vec4 base = texture(TEXTURE, UV);
    float weight = surface_enabled ? texture(surface_mask, UV).r : 0.0;
    vec3 rgb = mix(base.rgb, texture(surface_texture, surface_uv).rgb, weight);
    COLOR = vec4(rgb * item_tint.rgb, base.a * item_tint.a);
}
