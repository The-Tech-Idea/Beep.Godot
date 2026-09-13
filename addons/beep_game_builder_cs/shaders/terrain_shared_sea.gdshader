shader_type canvas_item;

uniform bool surface_enabled = true;
uniform bool depth_permitted = true;
#include "res://addons/beep_game_builder_cs/shaders/terrain_water_depth_field.gdshaderinc"
uniform sampler2D secondary_texture : source_color, repeat_enable, filter_linear;
uniform vec2 surface_origin = vec2(0.0);
uniform vec2 surface_u = vec2(0.00390625, 0.0);
uniform vec2 surface_v = vec2(0.0, 0.00390625);
uniform float surface_repeat_cells = 4.0;
uniform vec2 cell_bias = vec2(0.0);
uniform vec3 water_base = vec3(0.08, 0.65, 0.82);
varying vec2 surface_uv;
varying vec4 item_tint;

void vertex() {
    vec2 position = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy - surface_origin;
    surface_uv = vec2(dot(position, surface_u), dot(position, surface_v));
    item_tint = COLOR;
}

float wave_hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float swell(vec2 position, float frame) {
    vec2 grid = floor(position / 128.0);
    float result = 0.0;
    for (int j = -1; j <= 1; j++) {
        for (int i = -1; i <= 1; i++) {
            vec2 cell = grid + vec2(float(i), float(j));
            float seed = wave_hash(cell);
            float other = wave_hash(cell + vec2(19.0, 7.0));
            float age = fract(frame / 16.0 + seed);
            vec2 center = (cell + vec2(0.15 + 0.7 * seed, 0.2 + 0.6 * other)) * 128.0;
            center.y += (age - 0.5) * 24.0;
            float dx = (position.x - center.x) / (16.0 + other * 25.0);
            float dy = position.y - center.y - (2.0 + seed * 4.0) * dx * dx;
            if (abs(dx) < 1.0 && abs(dy) < 3.0) {
                float envelope = pow(1.0 - dx * dx, 2.0) * (1.0 - abs(dy) / 3.0);
                result = max(result, envelope * pow(sin(age * 3.14159265), 2.0) * (0.16 + other * 0.14));
            }
        }
    }
    return result;
}

float river_light(float along, float across, float frame) {
    float result = 0.0;
    for (int i = 0; i < 4; i++) {
        float lane = i == 0 ? -13.0 : (i == 1 ? -4.0 : (i == 2 ? 4.0 : 13.0));
        float ds = mod(along - frame * 2.0 - abs(lane) * 1.7 + 16.0, 32.0) - 16.0;
        float dn = across - lane;
        result += exp(-(pow(ds / 1.25, 2.0) + pow(dn / 3.2, 2.0))) * 0.42;
    }
    return clamp(result, 0.0, 1.0);
}

void fragment() {
    // Large animation atlases can round an exact texel boundary to either side
    // as the frame band changes. Sample texel centers with a subpixel tie bias.
    vec2 atlas_texel = floor(UV / TEXTURE_PIXEL_SIZE + vec2(0.001)) + vec2(0.5);
    vec4 base = texture(TEXTURE, atlas_texel * TEXTURE_PIXEL_SIZE);
    vec3 bytes = floor(base.rgb * 255.0 + vec3(0.5));
    vec3 rgb = base.rgb;
    if (surface_enabled && base.a > 0.0) {
        if (bytes == vec3(0.0, 0.0, 255.0)) {
            rgb = texture(secondary_texture, surface_uv).rgb;
        } else if (bytes.b >= 249.0 && bytes.b <= 254.0) {
            // Every controlled atlas has sixteen equally high frame bands.
            // Reading the native tile frame avoids a second, drifting TIME clock.
            float frame = floor(UV.y * 16.0);
            vec2 logical = surface_uv * surface_repeat_cells;
            vec2 position = logical * 64.0;
            bool depth = bytes.b == 249.0;
            if (depth && !depth_permitted) { base.a = 0.0; }
            float distance = depth ? 64.0 : bytes.g / 255.0 * 64.0;
            float shallow = depth ? bytes.r / 255.0 : clamp(1.0 - distance / 20.0, 0.0, 1.0);
            vec3 deep = water_base * vec3(0.55, 0.74, 0.9);
            float surface_mix = shallow * (depth ? 0.7 : 0.55);
            // Depth affects sea shading, including validated mouths. Their river
            // side is blended back below; grass, banks and authored alpha stay fixed.
            if (bytes.b >= 250.0 && bytes.b <= 254.0) { surface_mix = max(surface_mix, mapped_depth(logical) * 0.7); }
            vec3 water = mix(deep, water_base, surface_mix);
            vec3 lit = mix(water, vec3(112.0, 201.0, 224.0) / 255.0, 0.04 + swell(position, frame));
            float age = frame / 16.0;
            float front = 18.0 * (1.0 - age);
            float breaking = clamp(1.0 - abs(distance - front) / 2.5, 0.0, 1.0) * sin(age * 3.14159265);
            float fragments = 0.65 + 0.35 * pow(sin((position.x + position.y) * 3.14159265 / 16.0 + age * 6.2831853), 2.0);
            rgb = mix(lit, vec3(233.0, 252.0, 249.0) / 255.0, breaking * fragments * 0.85);
            if (bytes.b >= 250.0 && bytes.b < 254.0) {
                vec2 local = fract(logical + cell_bias) * 64.0;
                float tag = bytes.b - 250.0;
                float along = tag == 0.0 ? local.y : (tag == 1.0 ? 64.0 - local.x : (tag == 2.0 ? 64.0 - local.y : local.x));
                float across = (tag == 0.0 || tag == 2.0 ? local.x : local.y) - 32.0;
                float light = river_light(along, across, frame) * clamp(distance / 4.0, 0.0, 1.0);
                vec3 river = floor(mix(water_base, vec3(181.0, 239.0, 247.0) / 255.0, light) * 255.0 + vec3(0.5)) / 255.0;
                rgb = mix(river, rgb, bytes.r / 255.0);
            }
        }
    }
    COLOR = vec4(rgb * item_tint.rgb, base.a * item_tint.a);
}
