shader_type canvas_item;
render_mode blend_mix;

uniform vec2 atlas_grid = vec2(9.0, 16.0);
uniform vec2 frame_size = vec2(92.0, 107.0);
uniform float footprint_height = 53.0;
uniform float side_height = 28.0;
varying vec4 vertex_tint;

void vertex() {
	vertex_tint = COLOR;
}

void fragment() {
	vec2 frame_uv = UV * atlas_grid;
	vec2 frame = floor(frame_uv);
	vec2 pixel = fract(frame_uv) * frame_size;
	// The front edge of the top diamond varies with x. Preserve everything
	// above it and resample only the vertical cliff faces below it.
	float edge = footprint_height * (1.0 - abs(pixel.x / frame_size.x - 0.5));
	float source_side = max(1.0, frame_size.y - footprint_height);
	float ratio = source_side / max(1.0, min(side_height, source_side));
	float source_y = edge + max(0.0, pixel.y - edge) * ratio;
	source_y = pixel.y <= edge ? pixel.y : source_y;
	vec2 sample_uv = (frame + vec2(pixel.x, source_y) / frame_size) / atlas_grid;
	vec2 dx = dFdx(sample_uv);
	vec2 dy = dFdy(sample_uv);
	vec4 colour = textureGrad(TEXTURE, sample_uv, dx, dy);
	// Sampling beyond this frame must not reveal the next atlas row.
	colour.a *= step(source_y, frame_size.y - 0.5);
	COLOR = colour * vertex_tint;
}
