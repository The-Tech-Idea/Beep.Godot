shader_type canvas_item;

#include "res://addons/beep_game_builder_cs/shaders/terrain_water_depth_field.gdshaderinc"

uniform bool surface_enabled = true;
uniform bool depth_permitted = false;
uniform bool depth_authoring_layer = false;
// Opt-in connected routes use the sprite shader clock, including loop wrap.
uniform bool connected_water_clock = false;
uniform sampler2D secondary_texture : source_color, repeat_enable, filter_linear;
uniform vec2 surface_origin = vec2(0.0);
uniform vec2 surface_u = vec2(0.00390625, 0.0);
uniform vec2 surface_v = vec2(0.0, 0.00390625);
uniform float surface_repeat_cells = 4.0;
uniform vec3 deep_tint = vec3(0.65, 0.8, 0.9);
varying vec2 surface_uv;
varying vec4 item_tint;

void vertex() {
    vec2 position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy - surface_origin;
    surface_uv = vec2(dot(position, surface_u), dot(position, surface_v));
    item_tint = COLOR;
}

void fragment() {
    vec2 texel = floor(UV / TEXTURE_PIXEL_SIZE + vec2(0.001)) + vec2(0.5);
    if (connected_water_clock && !depth_authoring_layer) {
        float band_height = round(1.0 / TEXTURE_PIXEL_SIZE.y / 16.0);
        float frame = floor(mod(TIME, 1.2) * (16.0 / 1.2));
        texel.y = mod(texel.y, band_height) + frame * band_height;
    }
    vec4 base = texture(TEXTURE, texel * TEXTURE_PIXEL_SIZE);
    vec3 bytes = floor(base.rgb * 255.0 + vec3(0.5));
    vec3 rgb = base.rgb;
    if (base.a > 0.0 && surface_enabled) {
        if (bytes == vec3(0.0, 0.0, 255.0)) {
            rgb = texture(secondary_texture, surface_uv).rgb;
        } else if (depth_authoring_layer) {
            // Native depth tiles are authoring data, never painted over the lake.
            if (!depth_permitted) { base.a = 0.0; }
        } else if (bytes != vec3(189.0, 167.0, 121.0) && depth_field_enabled) {
            // Preserve the authored local-ripple colors; depth is only a tint.
            vec2 logical = surface_uv * surface_repeat_cells;
            float shoreline = 1.0 - mapped_shore(logical);
            float shallow = max(mapped_depth(logical), shoreline);
            float role = mapped_shore_role(logical);
            float influence = role == 0.0 ? 1.0 : 0.0;
            if (role >= 2.0 && role <= 5.0) {
                vec2 local = fract(depth_cell_position(logical)) * 64.0;
                float port = role - 2.0;
                float along = port == 0.0 ? local.y : (port == 1.0 ? 64.0-local.x : (port == 2.0 ? 64.0-local.y : local.x));
                influence = smoothstep(0.5, 8.0, along);
            }
            rgb = base.rgb * mix(vec3(1.0), deep_tint, influence * (1.0-shallow));
        }
    }
    COLOR = vec4(rgb * item_tint.rgb, base.a * item_tint.a);
}
