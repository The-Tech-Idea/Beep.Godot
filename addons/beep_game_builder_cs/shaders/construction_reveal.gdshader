shader_type canvas_item;

// The shipped rise for construction-stage parts: everything ABOVE a line
// that climbs from reveal_bottom to reveal_top is hidden, so a part appears
// to be built up from the ground.
//
// The line lives in the part's OWN texture axis, not screen space: v is
// UV.y * v_scale, where a stage scene tiles its swatch so that UV.y is the
// face's down-axis in swatch repeats (v_scale = world px per repeat), or
// sets v_scale = 1 with pixel UVs for a flat, untextured part. That keeps
// the cut parallel to a skewed wall's courses instead of the screen.
//
// row_v > 0 quantises the line to whole material rows (brick courses,
// plank rows, roof tiles, steel panels) measured from the bottom - Settlers
// III's construction mask advances a course at a time; a line that lands
// mid-brick is the tell of "a building appearing". cut_v > 0 tints the band
// just below the line toward cut_color - fresh mortar, wet concrete, raw
// timber - so the newest work looks newer than the rest.
//
// Only ALPHA is touched below the line's hidden side. COLOR arrives as the
// item's own texture sample multiplied by its vertex colour and modulate;
// replacing it with a bare texture() sample dropped both.

uniform float reveal_progress : hint_range(0.0, 1.0) = 1.0;
uniform float reveal_bottom = 1.0;
uniform float reveal_top = 0.0;
// 0: the reveal runs on UV.y; 1: on UV.x - a beam whose swatch length runs
// along the part (a post, a beam laid end to end) rises along that axis.
uniform int v_axis = 0;
uniform float v_scale = 1.0;
uniform float row_v = 0.0;
uniform float cut_v = 0.0;
uniform vec4 cut_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	float v = (v_axis == 1 ? UV.x : UV.y) * v_scale;
	float line = mix(reveal_bottom, reveal_top, reveal_progress);
	if (row_v > 0.0) {
		float built = reveal_bottom - line;
		line = reveal_bottom - floor(built / row_v + 0.0001) * row_v;
	}
	if (reveal_progress >= 1.0) {
		line = reveal_top;
	}
	if (v < line) {
		COLOR.a = 0.0;
	} else if (cut_v > 0.0 && v < line + cut_v) {
		float t = (v - line) / cut_v;
		COLOR.rgb = mix(cut_color.rgb, COLOR.rgb, t);
	}
}
