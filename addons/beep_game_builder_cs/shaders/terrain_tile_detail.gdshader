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

// How wide the reference sample averages, in texels. Detail finer than this survives as surface;
// anything broader is cancelled. Matches GRAIN_REFERENCE_TEXELS in terrain_splat.gdshader, because
// it is the same decision about the same art.
const float REFERENCE_TEXELS = 4.0;

void fragment() {
    // Continuous surface detail, with the authored atlas's alpha left intact.
    //
    // A HIGH PASS, not the texture's own brightness. This multiplied by the sampled luminance
    // directly, so everything painted into the ground texture came through the tiles as a bright or
    // dark shape at its authored size - the shells and starfish in the sand read as objects lying on
    // the beach, in every view that lays a ground texture over its terrain. Only the isometric block
    // view escaped it, because it draws authored block art with no ground overlay at all, which is
    // how the owner placed the fault. Against a blurred sample of the same texture, what is left is
    // the surface and not the picture.
    vec2 dx = dFdx(ground_uv);
    vec2 dy = dFdy(ground_uv);
    vec3 detail = textureGrad(ground_texture, ground_uv, dx, dy).rgb;
    vec3 reference = textureGrad(ground_texture, ground_uv, dx * REFERENCE_TEXELS, dy * REFERENCE_TEXELS).rgb;
    float difference = dot(detail - reference, vec3(0.2126, 0.7152, 0.0722));
    COLOR.rgb *= clamp(1.0 + difference * 2.0 * detail_strength, 0.75, 1.25);
}
