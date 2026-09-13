uniform bool depth_field_enabled = false;
uniform sampler2D depth_field_texture : filter_nearest, repeat_disable;
uniform sampler2D depth_mask_texture : filter_nearest, repeat_disable;
uniform vec2 depth_field_origin = vec2(0.0);
uniform vec2 depth_cell_bias = vec2(0.0);
uniform vec2 depth_field_size = vec2(1.0);
uniform vec2 depth_mask_cell_size = vec2(64.0);
uniform vec2 depth_mask_texture_size = vec2(512.0, 6144.0);
uniform sampler2D shore_field_texture : filter_nearest, repeat_disable;
uniform vec2 shore_field_origin = vec2(0.0);
uniform vec2 shore_field_size = vec2(1.0);

vec2 depth_cell_position(vec2 logical) {
    vec2 position = logical + depth_cell_bias;
    if (depth_mask_cell_size.y < depth_mask_cell_size.x) {
        // Match native diamond rasterization before choosing a logical cell.
        vec2 projected = vec2((position.x-position.y)*32.0, (position.x+position.y)*16.0);
        projected = floor(projected + vec2(0.001)) + vec2(0.5);
        position = vec2(projected.x/64.0+projected.y/32.0, projected.y/32.0-projected.x/64.0);
    }
    return position;
}

float mapped_water_mask(vec2 logical, bool shore) {
    vec2 position = depth_cell_position(logical);
    vec2 origin = shore ? shore_field_origin : depth_field_origin;
    vec2 size = shore ? shore_field_size : depth_field_size;
    vec2 cell = floor(position) - origin;
    if (!depth_field_enabled || any(lessThan(cell, vec2(0.0))) || any(greaterThanEqual(cell, size))) {
        return 0.0;
    }
    vec2 uv = (cell + vec2(0.5)) / size;
    vec3 encoded = shore ? texture(shore_field_texture, uv).rgb : texture(depth_field_texture, uv).rgb;
    vec3 entry = floor(encoded * 255.0 + vec3(0.5));
    if (entry.b < 254.0) { return 0.0; }
    vec2 local = fract(position) * 64.0;
    if (depth_mask_cell_size.y < depth_mask_cell_size.x) {
        local = vec2(32.0 + (local.x - local.y) * 0.5, (local.x + local.y) * 0.25);
    }
    vec2 local_texel = clamp(floor(local + vec2(0.001)), vec2(0.0), depth_mask_cell_size - vec2(1.0));
    vec2 texel = entry.rg * depth_mask_cell_size + local_texel + vec2(0.5);
    return texture(depth_mask_texture, texel / depth_mask_texture_size).r;
}

float mapped_depth(vec2 logical) { return mapped_water_mask(logical, false); }
float mapped_shore(vec2 logical) { return mapped_water_mask(logical, true); }

float mapped_shore_role(vec2 logical) {
    vec2 cell = floor(depth_cell_position(logical)) - shore_field_origin;
    if (any(lessThan(cell, vec2(0.0))) || any(greaterThanEqual(cell, shore_field_size))) { return 255.0; }
    return floor(texture(shore_field_texture, (cell+vec2(0.5))/shore_field_size).a*255.0+0.5);
}
