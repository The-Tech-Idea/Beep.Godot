shader_type canvas_item;
// No `render_mode unshaded`: Godot skips the scene's CanvasModulate for an
// unshaded item, so this plateau art ignored day/night, weather and seasons.
varying vec4 tint;

void vertex() {
    tint = COLOR;
}

void fragment() {
    vec4 sample_color = texture(TEXTURE, UV);
    float green_excess = sample_color.g - max(sample_color.r, sample_color.b);
    float coverage = 1.0 - smoothstep(0.12, 0.25, green_excess);
    COLOR = tint * vec4(sample_color.rgb, sample_color.a * coverage);
}
