shader_type canvas_item;
render_mode unshaded;

// A transparent, screen-space rain overlay for Godot 4.x.
// Each procedural lane owns one falling drop and one synchronized impact splash.

uniform vec2 viewport_size = vec2(1280.0, 720.0);
uniform float intensity : hint_range(0.0, 1.0) = 0.85;
uniform float shelter_factor : hint_range(0.0, 1.0) = 0.0;
uniform float lightning_flash : hint_range(0.0, 1.0) = 0.0;

uniform_group("Rain");
uniform vec4 rain_color : source_color = vec4(0.68, 0.82, 1.0, 0.72);
uniform float rain_density : hint_range(0.0, 1.0) = 0.72;
uniform float fall_speed : hint_range(80.0, 1600.0) = 760.0;
uniform float wind_slant : hint_range(-1.25, 1.25) = -0.18;
uniform float streak_length : hint_range(4.0, 120.0) = 34.0;
uniform float streak_width : hint_range(0.35, 6.0) = 1.15;

uniform_group("Splashes");
uniform vec4 splash_color : source_color = vec4(0.78, 0.9, 1.0, 0.8);
uniform float splash_strength : hint_range(0.0, 2.0) = 1.0;
uniform float splash_radius : hint_range(2.0, 48.0) = 13.0;
uniform float splash_height : hint_range(0.0, 32.0) = 8.0;
uniform float splash_flatness : hint_range(1.0, 6.0) = 2.8;
uniform float impact_y_min : hint_range(0.0, 1.0) = 0.12;
uniform float impact_y_max : hint_range(0.0, 1.0) = 0.94;

uniform_group("Splash Mask");
uniform bool use_splash_mask = false;
uniform sampler2D splash_mask : source_color, filter_linear;
uniform float splash_mask_threshold : hint_range(0.0, 1.0) = 0.5;
uniform float splash_mask_softness : hint_range(0.001, 0.5) = 0.08;

uniform_group("Pixel Art");
uniform bool pixel_art_mode = false;
uniform float pixel_size : hint_range(1.0, 8.0) = 1.0;

const float PI = 3.14159265359;

float hash11(float value) {
    return fract(sin(value * 127.1 + 311.7) * 43758.5453123);
}

float soft_band(float value, float minimum, float maximum, float feather) {
    float enter = smoothstep(minimum - feather, minimum, value);
    float leave = 1.0 - smoothstep(maximum, maximum + feather, value);
    return enter * leave;
}

float splash_mask_at(vec2 impact_uv) {
    if (!use_splash_mask) {
        return 1.0;
    }

    float mask_value = texture(splash_mask, clamp(impact_uv, vec2(0.0), vec2(1.0))).r;
    return smoothstep(
        splash_mask_threshold - splash_mask_softness,
        splash_mask_threshold + splash_mask_softness,
        mask_value
    );
}

// Returns x = rain opacity, y = splash opacity.
vec2 weather_lane_layer(
    vec2 pixel,
    float seed,
    float spacing,
    float speed_multiplier,
    float width_multiplier,
    float length_multiplier,
    float splash_multiplier,
    float layer_opacity
) {
    float safe_spacing = max(spacing, 4.0);
    float lane_space_x = pixel.x - wind_slant * pixel.y;
    float lane_id = floor(lane_space_x / safe_spacing);

    float random_x = hash11(lane_id * 1.731 + seed);
    float random_phase = hash11(lane_id * 4.173 + seed * 7.91);
    float random_impact = hash11(lane_id * 2.417 + seed * 13.37);
    float random_enable = hash11(lane_id * 8.113 + seed * 3.17);

    float density_probability = mix(0.28, 1.0, rain_density);
    float enabled_lane = step(1.0 - density_probability, random_enable);

    float base_x = (lane_id + 0.16 + random_x * 0.68) * safe_spacing;
    float min_y = min(impact_y_min, impact_y_max);
    float max_y = max(impact_y_min, impact_y_max);
    float impact_y = mix(viewport_size.y * min_y, viewport_size.y * max_y, random_impact);
    float impact_x = base_x + wind_slant * impact_y;

    float layer_length = max(streak_length * length_multiplier, 2.0);
    float travel_distance = max(impact_y + layer_length + 8.0, 64.0);
    float cycle_rate = (fall_speed * speed_multiplier) / travel_distance;
    float phase = fract(TIME * cycle_rate + random_phase);

    // The first part of the cycle is the falling drop. The remainder is its splash.
    float fall_end = 0.82;
    float fall_t = clamp(phase / fall_end, 0.0, 1.0);
    float head_y = mix(-layer_length - 6.0, impact_y, fall_t);

    float line_center_x = base_x + wind_slant * pixel.y;
    float across_distance = abs(pixel.x - line_center_x);
    float along_distance = pixel.y - head_y;
    float antialias_px = max(0.75, pixel_size * 0.65);

    float layer_width = max(streak_width * width_multiplier, 0.25);
    float line_width_mask = 1.0 - smoothstep(
        layer_width,
        layer_width + antialias_px,
        across_distance
    );
    float line_length_mask = soft_band(
        along_distance,
        -layer_length,
        0.0,
        antialias_px
    );
    float drop_alive = 1.0 - smoothstep(fall_end - 0.012, fall_end, phase);

    float head_radius = max(layer_width * 1.25, 0.8);
    vec2 head_delta = pixel - vec2(base_x + wind_slant * head_y, head_y);
    float head_mask = 1.0 - smoothstep(
        head_radius,
        head_radius + antialias_px,
        length(head_delta)
    );

    float rain_mask = max(line_width_mask * line_length_mask, head_mask * 0.72);
    rain_mask *= drop_alive * enabled_lane * layer_opacity;

    float splash_age = clamp((phase - fall_end) / (1.0 - fall_end), 0.0, 1.0);
    float splash_alive = step(fall_end, phase);
    float eased_age = 1.0 - pow(1.0 - splash_age, 2.0);
    vec2 impact_position = vec2(impact_x, impact_y);
    vec2 impact_delta = pixel - impact_position;

    // Elliptical ground ring, suitable for top-down and RPG perspectives.
    vec2 elliptical_delta = vec2(impact_delta.x, impact_delta.y * splash_flatness);
    float expanding_radius = mix(1.5, splash_radius * splash_multiplier, eased_age);
    float ring_width = mix(1.6, 0.65, splash_age);
    float ring_distance = abs(length(elliptical_delta) - expanding_radius);
    float ring_mask = 1.0 - smoothstep(
        ring_width,
        ring_width + antialias_px,
        ring_distance
    );

    // Two small droplets rise from the impact, creating a readable splash crown.
    float jump = sin(splash_age * PI) * splash_height * splash_multiplier;
    float side_distance = mix(1.0, splash_radius * splash_multiplier * 0.58, eased_age);
    float splash_drop_radius = mix(1.65, 0.55, splash_age);
    vec2 left_drop_position = impact_position + vec2(-side_distance, -jump * (0.82 + random_x * 0.25));
    vec2 right_drop_position = impact_position + vec2(side_distance, -jump * (0.92 + random_impact * 0.2));

    float left_drop = 1.0 - smoothstep(
        splash_drop_radius,
        splash_drop_radius + antialias_px,
        length(pixel - left_drop_position)
    );
    float right_drop = 1.0 - smoothstep(
        splash_drop_radius,
        splash_drop_radius + antialias_px,
        length(pixel - right_drop_position)
    );
    float crown_fade = 1.0 - smoothstep(0.62, 0.94, splash_age);

    float impact_mask = splash_mask_at(impact_position / viewport_size);
    float splash_fade = pow(1.0 - splash_age, 1.35);
    float splash_mask_value = max(ring_mask, (left_drop + right_drop) * crown_fade);
    splash_mask_value *= splash_fade * splash_alive * enabled_lane * layer_opacity * impact_mask;

    return vec2(rain_mask, splash_mask_value);
}

void fragment() {
    vec2 safe_viewport = max(viewport_size, vec2(1.0));
    vec2 pixel = UV * safe_viewport;

    if (pixel_art_mode) {
        float snap_size = max(pixel_size, 1.0);
        pixel = floor(pixel / snap_size) * snap_size + vec2(snap_size * 0.5);
    }

    float primary_spacing = mix(62.0, 20.0, rain_density);

    vec2 near_layer = weather_lane_layer(
        pixel,
        19.71,
        primary_spacing,
        1.0,
        1.0,
        1.0,
        1.0,
        1.0
    );
    vec2 middle_layer = weather_lane_layer(
        pixel,
        83.29,
        primary_spacing * 1.42,
        0.76,
        0.72,
        0.68,
        0.78,
        0.62
    );
    vec2 far_layer = weather_lane_layer(
        pixel,
        151.93,
        primary_spacing * 2.05,
        0.54,
        0.52,
        0.46,
        0.58,
        0.38
    );

    float visibility = intensity * (1.0 - shelter_factor);
    float rain_alpha = clamp(near_layer.x + middle_layer.x + far_layer.x, 0.0, 1.0);
    float splash_alpha = clamp(near_layer.y + middle_layer.y + far_layer.y, 0.0, 1.0);

    rain_alpha *= rain_color.a * visibility;
    splash_alpha *= splash_color.a * splash_strength * visibility;

    float combined_alpha = clamp(rain_alpha + splash_alpha, 0.0, 1.0);
    vec3 combined_rgb = (
        rain_color.rgb * rain_alpha + splash_color.rgb * splash_alpha
    ) / max(combined_alpha, 0.0001);
    combined_rgb = mix(combined_rgb, vec3(1.0), lightning_flash * 0.8);

    COLOR = vec4(combined_rgb, combined_alpha);
}
