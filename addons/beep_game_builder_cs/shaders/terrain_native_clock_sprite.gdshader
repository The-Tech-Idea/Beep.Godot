shader_type canvas_item;
#include "res://addons/beep_game_builder_cs/shaders/terrain_shared_waterfall.gdshaderinc"

uniform sampler2D animation_atlas : filter_nearest, repeat_disable;
uniform vec2 animation_origin = vec2(0.0);
uniform vec2 animation_stride = vec2(0.0, 64.0);
uniform vec2 animation_size = vec2(64.0);
uniform vec2 animation_atlas_size = vec2(64.0, 1024.0);
uniform float frame_override = -1.0;
uniform bool surface_enabled = true;
uniform bool secondary_enabled = false;
uniform sampler2D secondary_texture : source_color, repeat_enable, filter_linear;
uniform vec2 surface_origin = vec2(0.0);
uniform vec2 surface_u = vec2(0.00390625, 0.0);
uniform vec2 surface_v = vec2(0.0, 0.00390625);
varying vec2 local_pixel;
varying vec2 surface_uv;
varying vec4 item_tint;

void vertex() {
    // Route sprites use their original top-left pivot and fixed frame bounds.
    local_pixel = VERTEX;
    vec2 world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy - surface_origin;
    surface_uv = vec2(dot(world, surface_u), dot(world, surface_v));
    item_tint = COLOR;
}

void fragment() {
    // Godot's native animation slices and TIME share the renderer clock.
    float frame = frame_override >= 0.0 ? frame_override : floor(mod(TIME, 1.2) * (16.0 / 1.2));
    vec2 pixel = clamp(floor(local_pixel + vec2(0.001)), vec2(0.0), animation_size - vec2(1.0));
    vec2 uv = (animation_origin + frame * animation_stride + pixel + vec2(0.5)) / animation_atlas_size;
    vec4 base = texture(animation_atlas, uv);
    if (shared_waterfall_enabled && base.a > 0.0) {
        base.rgb = shared_fall_color(pixel + vec2(0.5), frame, base.rgb);
    }
    vec3 bytes = floor(base.rgb * 255.0 + vec3(0.5));
    if (surface_enabled && secondary_enabled && bytes == vec3(0.0, 0.0, 255.0)) {
        base.rgb = texture(secondary_texture, surface_uv).rgb;
    }
    COLOR = base * item_tint;
}
