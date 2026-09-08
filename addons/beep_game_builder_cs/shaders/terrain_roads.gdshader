shader_type canvas_item;
render_mode unshaded;

uniform sampler2D brick_texture : repeat_enable, filter_linear_mipmap;
uniform sampler2D terrain_texture : repeat_disable, filter_linear_mipmap;
uniform bool pixel_art = false;
varying vec2 local_position;

void vertex() { local_position = VERTEX; }

float segment_distance(vec2 p, vec2 end) {
	return length(p - end * clamp(dot(p, end) / dot(end, end), 0.0, 1.0));
}

void fragment() {
	// Native atlas coordinates encode N/E/S/W connections, then material row.
	vec2 atlas = UV * vec2(16.0, 3.0);
	int mask = int(floor(atlas.x));
	int kind = int(floor(atlas.y));
	vec2 p = fract(atlas) - 0.5;
	if (pixel_art) { p = (floor(p * 64.0) + 0.5) / 64.0; }
	float distance = length(p);
	if ((mask & 1) != 0) { distance = min(distance, segment_distance(p, vec2(0.0, -0.6))); }
	if ((mask & 2) != 0) { distance = min(distance, segment_distance(p, vec2(0.6, 0.0))); }
	if ((mask & 4) != 0) { distance = min(distance, segment_distance(p, vec2(0.0, 0.6))); }
	if ((mask & 8) != 0) { distance = min(distance, segment_distance(p, vec2(-0.6, 0.0))); }
	vec2 repeat_uv = local_position / (kind == 0 ? 32.0 : 64.0);
	if (pixel_art) { repeat_uv = floor(repeat_uv * 64.0) / 64.0; }
	vec3 material_color;
	if (kind == 0) {
		material_color = texture(brick_texture, repeat_uv).rgb;
	} else {
		vec2 origin = kind == 1 ? vec2(627.0, 0.0) : vec2(0.0, 314.0);
		vec2 mirrored = vec2(1.0) - abs(mod(repeat_uv, vec2(2.0)) - vec2(1.0));
		vec2 coord = (origin + vec2(2.0) + mirrored * vec2(309.0, 310.0)) / 1254.0;
		material_color = texture(terrain_texture, coord).rgb;
		if (!pixel_art) {
			material_color = mix(kind == 1 ? vec3(0.85, 0.69, 0.39) : vec3(0.40, 0.29, 0.18), material_color, 0.23);
		}
	}
	float edge = pixel_art ? 0.005 : max(fwidth(distance), 0.008);
	float alpha = 1.0 - smoothstep(0.31 - edge, 0.31 + edge, distance);
	material_color *= mix(1.0, 0.77, smoothstep(0.26, 0.31, distance));
	COLOR = vec4(material_color, alpha);
}
