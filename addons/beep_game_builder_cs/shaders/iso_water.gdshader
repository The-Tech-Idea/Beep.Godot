shader_type canvas_item;
render_mode blend_mix;

// The SAME sea as the flat renderer, drawn in the isometric projection.
//
// The water body of this file is copied from terrain_splat.gdshader, expression
// for expression and uniform for uniform, on purpose. That sea was tuned until
// it was right; a second implementation of it would be a second thing to tune,
// and the two would drift until the same map had two different oceans.
//
// EXACTLY TWO things are added, and neither touches the water maths:
//
//   1. The projection. The flat shader is a full-map quad, so its UV already IS
//      the map. Here the surface is a diamond, so the fragment's world position
//      is run back through the inverse isometric transform to recover which tile
//      it is over. Everything downstream then works in tiles, unchanged.
//
//   2. Alpha from depth. The flat shader is opaque - it composites the sand
//      itself, in the same pass, and never needs to be seen through. Here the
//      seabed is real geometry on its own layers underneath, so the surface has
//      to actually be transparent for any of it to show. Shallow water is clear
//      and deep water is opaque, which is the standard model (Age of Empires IV
//      calls the distance at which water goes opaque "Deep Water Depth") and is
//      also simply what water does: the same depth value already driving the
//      colour ramp and the surf drives clarity too.
//
// Everything else - the shore frame, the depth ramp, the swell, the surf train,
// the foam arrival - is the flat renderer's, untouched.

uniform sampler2D coast_map : filter_linear, repeat_disable;
uniform float coast_range = 5.0;
uniform vec2 map_size = vec2(96.0, 60.0);
uniform float texture_tiles = 6.0;

uniform sampler2D tex_sand : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_shallow : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_deep : repeat_enable, filter_linear_mipmap;
uniform vec3 tint_sand = vec3(0.98, 0.94, 0.82);
uniform vec3 tint_shallow = vec3(0.62, 0.80, 0.88);
uniform vec3 tint_deep = vec3(0.44, 0.58, 0.78);

uniform float shore_blend_tiles : hint_range(0.05, 1.5) = 0.28;
uniform float foam_strength : hint_range(0.0, 1.0) = 0.40;
uniform float wave_intensity : hint_range(0.0, 2.0) = 1.0;
uniform float foam_tiles : hint_range(0.1, 6.0) = 0.95;
uniform float wave_speed : hint_range(0.0, 4.0) = 1.0;
uniform float wave_amount : hint_range(0.0, 0.06) = 0.022;
uniform float shallow_tiles : hint_range(0.0, 8.0) = 1.8;
uniform float shallow_sand : hint_range(0.0, 1.0) = 0.34;
uniform float deep_tiles : hint_range(0.5, 12.0) = 4.5;

uniform sampler2D foam_sheet : repeat_enable, filter_linear_mipmap;
uniform bool use_foam_sheet = false;
uniform float foam_tiles_along : hint_range(1.0, 48.0) = 11.0;
uniform float foam_tiles_across : hint_range(0.3, 8.0) = 7.0;
uniform float foam_scroll : hint_range(0.0, 4.0) = 0.055;
uniform float foam_pulse : hint_range(0.0, 1.0) = 0.34;
uniform float foam_arrival_rate : hint_range(0.0, 4.0) = 0.9;
uniform float foam_lines : hint_range(1.0, 8.0) = 2.3;

// ADDITION 1 - the projection. Half the diamond a tile occupies, which is what
// turns a world position back into a tile coordinate.
uniform vec2 cell_size = vec2(111.0, 64.0);

// ADDITION 2 - clarity. How opaque the water gets once it is deep, and how far
// out that takes. Below one, the seabed under it stays visible even offshore.
uniform float max_opacity : hint_range(0.0, 1.0) = 0.92;
uniform float clarity_tiles : hint_range(0.1, 12.0) = 3.0;

// Lakes separately. A lake three tiles from its shore is not open ocean - it is
// shallow, still and usually clear enough to see the bottom - but the distance
// field alone cannot tell them apart, so without this a pond reads as deep sea.
// The coast field already marks open water in its green channel, which is the
// same flag that keeps surf off a lake.
uniform float lake_opacity : hint_range(0.0, 1.0) = 0.42;

// Opacity at and INLAND of the waterline.
//
// The flat shader masks its water off at the shore because it draws the land
// itself, in the same pass. Here the land is separate geometry, and an
// isometric block hangs a whole cell BELOW the tile it stands on - so the strip
// of screen under a coastal block maps back to tiles that are still land. Mask
// the water off there and nothing draws that strip at all: it comes out as a
// hard grey line running along every beach.
//
// The sea simply runs on under the land instead. The land is opaque and drawn
// above it, so nothing inland changes - it only fills the strip that the land's
// own overhang leaves behind.
uniform float shore_opacity : hint_range(0.0, 1.0) = 0.55;

varying vec2 world_pos;

float hash(vec2 p) {
	// NOT fract(sin(dot(p, ...)) * 43758.0). That loses precision once the
	// coordinate is large - and the coordinate here is tile position times a
	// frequency, so it reaches the hundreds - which makes neighbouring lattice
	// cells hash to near-identical values and shows up as flat rectangular
	// patches in the open water, exactly the tile-grid look this shader exists
	// to avoid.
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
			   mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	// ADDITION 1. Diamond-down isometric places a tile at
	//   world = ((x - y) * w/2, (x + y) * h/2)
	// so inverting it gives the tile back. Sampling by screen UV instead would
	// stretch the whole sea across the diamond's bounding box and put the surf
	// on a coastline that is not there.
	// Godot places a tile's CENTRE, not its corner, so cell (0,0) sits at
	// (w/2, h/2) and not at the origin. Inverting the textbook transform without
	// that half-tile puts the whole coastline one tile out - measured against
	// map_to_local, every cell came back exactly (+1, 0) wrong, which drew the
	// surf on a shore that is not where the land is.
	vec2 half_cell = max(cell_size * 0.5, vec2(1.0));
	float ex = (world_pos.x - half_cell.x) / half_cell.x;
	float ey = world_pos.y / half_cell.y;
	vec2 tile = vec2((ex + ey) * 0.5, (ey - ex) * 0.5);
	vec2 UVm = tile / map_size;

	vec2 world_uv = tile / texture_tiles;
	float t = TIME * wave_speed;

	// The shore frame, from the gradient of the distance field: which way is out
	// to sea, and which way the coast runs. Everything that MOVES on the water
	// is expressed in this frame, because a drift written in world axes runs
	// out to sea on whichever coasts happen to face that way.
	vec2 shore_texel = 1.0 / map_size;
	vec2 shore_grad = vec2(
		texture(coast_map, UVm + vec2(shore_texel.x, 0.0)).r - texture(coast_map, UVm - vec2(shore_texel.x, 0.0)).r,
		texture(coast_map, UVm + vec2(0.0, shore_texel.y)).r - texture(coast_map, UVm - vec2(0.0, shore_texel.y)).r);
	float shore_glen = length(shore_grad);
	vec2 seaward = shore_glen > 1e-6 ? shore_grad / shore_glen : vec2(0.0, 1.0);
	vec2 along_shore = vec2(-seaward.y, seaward.x);

	// Sampled in WORLD space, on world axes. Translating these coordinates by a
	// shore-relative vector was a mistake: that direction points at the nearest
	// coast, so inside a lake it converges from every side, and translating UVs
	// by a field that varies per pixel SHEARS the texture.
	vec2 water_uv = world_uv + vec2(
		sin((world_uv.y * 7.0) + (t * 0.7)),
		cos((world_uv.x * 6.0) + (t * 0.5))) * wave_amount;

	// Past the edge of the map there is no coastline, only open sea. This has to
	// be said explicitly: the coast map is repeat_disable, so sampling outside
	// 0..1 CLAMPS to its border texels and smears them outward - which drew four
	// pale streaks running off the map's four edges in a great X across the sea.
	vec2 inside = step(vec2(0.0), tile) * step(tile, map_size);
	float in_map = inside.x * inside.y;

	// Signed distance to the waterline, in tiles: positive out to sea.
	vec4 coast = texture(coast_map, UVm);
	float sd = mix(coast_range, (coast.r - 0.5) * 2.0 * coast_range, in_map);
	// Green marks open sea. Surf needs a fetch behind it, so a lake or a river
	// stays flat however close to the shore it is. Outside the map it is all
	// open sea - and safely so, because sd is saturated there, which puts the
	// surf band's falloff at zero.
	float open_sea = mix(1.0, smoothstep(0.35, 0.75, coast.g), in_map);
	// The field is one value per sub-tile, so its contours are still faceted
	// and surf drawn on them looks like survey lines. Two octaves of wobble
	// break that up.
	sd += (value_noise(tile * 1.1) - 0.5) * 0.30;
	sd += (value_noise((tile * 3.4) + vec2(5.3, 9.1)) - 0.5) * 0.18;

	// Depth from the same distance field: shallow against the shore, shelving to
	// deep offshore.
	vec3 water_col = mix(
		texture(tex_shallow, water_uv).rgb * tint_shallow,
		texture(tex_deep, water_uv).rgb * tint_deep,
		smoothstep(0.0, max(0.001, deep_tiles), sd));

	// Shallow water is water OVER sand, so the bottom shows through near the
	// shore. Sampled on world_uv, not the drifting water_uv: a seabed that swims
	// with the surface reads as fog, not as depth.
	float shallow = 1.0 - smoothstep(0.0, max(0.001, shallow_tiles), sd);
	water_col = mix(water_col, texture(tex_sand, world_uv).rgb * tint_sand, shallow * shallow_sand);

	// Moving swell on open water, so the sea is never a flat sheet.
	float swell = value_noise((tile * 1.4) + vec2(t * 0.20, t * -0.13));
	water_col *= 0.94 + (0.12 * swell);

	// Surf, as a band measured out from the waterline rather than around a tile.
	float reach = foam_tiles * (0.45 + (0.55 * wave_intensity));
	float from_shore = clamp(sd / max(0.001, reach), 0.0, 1.0);
	float patch = 0.72 + (0.28 * smoothstep(0.25, 0.80,
		value_noise((tile * 1.5) + vec2(t * 0.10, t * 0.06))));

	float lines;
	if (use_foam_sheet) {
		float along_coord = dot(tile, along_shore);
		lines = texture(foam_sheet, vec2(
			along_coord / max(0.001, foam_tiles_along),
			(sd / max(0.001, foam_tiles_across)) - (t * foam_scroll))).a;

		float arrival = sin((from_shore * foam_lines * 6.28318)
			+ (t * foam_arrival_rate)
			+ (value_noise(vec2(along_coord * 0.09, 0.0)) * 6.28318));
		lines *= mix(1.0, smoothstep(-0.35, 0.85, arrival), foam_pulse);
	} else {
		float surf = value_noise((tile * 2.2) + vec2(t * 0.18, t * 0.11));
		// PLUS t, not minus. Swell runs onto a beach, not off it.
		float crest = sin((from_shore * foam_lines * 6.28318) + (t * 2.4) + (surf * 3.0));
		float heavy = clamp(wave_intensity * 0.5, 0.0, 1.0);
		lines = smoothstep(mix(0.55, -0.05, heavy), mix(0.99, 0.70, heavy), crest);
	}

	float falloff = 1.0 - smoothstep(0.0, 1.0, from_shore);
	float foam = clamp(lines * falloff * patch * foam_strength * open_sea
		* clamp(wave_intensity, 0.0, 1.4) * smoothstep(-0.12, 0.06, sd), 0.0, 1.0);
	water_col = mix(water_col, vec3(0.96, 0.99, 1.0), foam);

	// ADDITION 2. Clear in the shallows so the bed shows, closing to opaque as
	// it deepens. Foam is surface, so it stays visible whatever is beneath.
	float body_opacity = mix(lake_opacity, max_opacity, open_sea);
	float depth_alpha = mix(
		shore_opacity, body_opacity, smoothstep(0.0, max(0.001, clarity_tiles), sd));

	COLOR = vec4(water_col, max(depth_alpha, foam));
}
