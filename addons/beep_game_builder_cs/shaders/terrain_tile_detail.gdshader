shader_type canvas_item;
// No `render_mode unshaded`: Godot skips the scene's CanvasModulate for an
// unshaded item, so the tile view's ground stayed noon-bright under day/night,
// weather and seasons while everything standing on it darkened.

uniform sampler2D ground_texture : source_color, repeat_enable, filter_linear_mipmap;
uniform vec2 cell_size = vec2(64.0);
uniform float repeat_cells = 6.0;
uniform float detail_strength : hint_range(0.0, 1.0) = 0.3;
varying vec2 ground_uv;

void vertex() {
    ground_uv = VERTEX / (max(cell_size, vec2(1.0)) * max(repeat_cells, 1.0));
}

void fragment() {
    // Continuous surface detail, with the authored atlas's alpha left intact.
    vec3 detail = texture(ground_texture, ground_uv).rgb;
    float luminance = dot(detail, vec3(0.2126, 0.7152, 0.0722));
    COLOR.rgb *= mix(1.0, clamp(0.75 + luminance * 0.5, 0.75, 1.25), detail_strength);
}
