shader_type canvas_item;
render_mode unshaded;
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
