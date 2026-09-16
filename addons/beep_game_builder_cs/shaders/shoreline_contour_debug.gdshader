shader_type canvas_item;
// Shaded, and the result multiplied by the COLOR received, like every terrain
// shader: an unshaded item skips the scene's CanvasModulate, and replacing COLOR
// drops the node's own modulate.

uniform sampler2D distance_map : filter_linear, repeat_disable;
uniform float beach_width = 1.0;
uniform vec4 water_colour : source_color = vec4(0.10, 0.48, 0.64, 1.0);
uniform vec4 beach_colour : source_color = vec4(0.91, 0.73, 0.40, 1.0);
uniform vec4 inland_colour : source_color = vec4(0.27, 0.57, 0.20, 1.0);

void fragment() {
    vec4 modulate = COLOR;
    float distance = texture(distance_map, UV).r;
    // Two thresholds of ONE field; the beach is an inset, not another biome mask.
    vec4 ground = distance < -beach_width ? inland_colour : beach_colour;
    if (beach_width <= 0.0) { ground = inland_colour; }
    COLOR = (distance >= 0.0 ? water_colour : ground) * modulate;
}
