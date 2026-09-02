shader_type canvas_item;

// Continuous terrain rendering, after Factorio's approach (FFF-214): instead of
// one sprite per tile, a large seamless material texture is sampled in WORLD
// space and the terrain shapes are cut out of it. That is what stops every
// grass tile being the same pixels.
//
// A fragment shader cannot read a TileMap, so the terrain grid is fed in as a
// texture: one texel per tile, terrain id in red, hillshade in green, sampled
// with filter_nearest so ids never interpolate. Neighbour lookups are then just
// samples at +/- one texel, which is what makes edge blending possible at all.
//
// The WATER - depth, swell, surf, foam - lives in water_common.gdshaderinc,
// shared with the isometric/top-down water shader so both views draw one sea.
// This file owns everything on the LAND side of the waterline and the opaque
// composite of the two.

#include "res://addons/beep_game_builder_cs/shaders/water_common.gdshaderinc"

uniform sampler2D id_map : filter_nearest, repeat_disable;
// Separate, linearly filtered, because ids must never interpolate but shade
// must - sharing one map makes relief a grid of hard brightness squares.
uniform sampler2D shade_map : filter_linear, repeat_disable;

// Pixels per tile. Needed because this shader now draws on a TileMapLayer
// rather than on one quad stretched over the map: a quad's UV meant "how far
// across the map am I" for free, and a tile layer's UV means "how far across
// this one tile's atlas region", which is a different question entirely.
uniform vec2 cell_size = vec2(64.0, 64.0);

// Where this fragment sits in the SURFACE's own coordinates, carried from the
// vertex stage.
//
// VERTEX in vertex() is documented as local space in PIXELS relative to the
// node's origin - exactly the quantity wanted, and unaffected by any parent
// transform, which the world position is not. In fragment() the same name means
// screen space, so it has to travel as a varying.
varying vec2 surface_local;

void vertex() {
	surface_local = VERTEX;
}

// Width of the blend between two terrains, as a fraction of a tile.
uniform float blend_width : hint_range(0.0, 0.9) = 0.42;

// How much the blend edge is broken up, so borders are not straight lines.
uniform float edge_noise : hint_range(0.0, 1.0) = 0.55;
uniform float noise_scale = 5.0;

uniform float shade_strength : hint_range(0.0, 2.0) = 1.0;

// The shoreline is deliberately NOT blended like a land border. Land meeting
// land is a gradual change of ground; land meeting water is an edge, and
// smearing it looks like fog rather than a coast.
//
// How far the land/water mix is spread either side of the waterline, in tiles.
// Small: this is a shore, not a fade. The BAND that makes a coast read as a
// coast is the sand and surf sequence below, not a wide blur here.
uniform float shore_blend_tiles : hint_range(0.05, 1.5) = 0.28;

// Swash: the sheet of water that runs UP the sand and drains back between
// crests. Standard shoreline practice (Cyanilux) is to drive it from a slower
// cosine synced to the arriving crests, and it does more to sell a heavy sea
// than the crests themselves. Measured in tiles at full intensity.
uniform float swash_tiles : hint_range(0.0, 3.0) = 0.26;
uniform float swash_speed : hint_range(0.0, 4.0) = 1.1;
uniform float wet_tiles : hint_range(0.0, 2.0) = 0.45;
uniform float shore_wet : hint_range(0.0, 1.0) = 0.30;

// A beach. Water does not meet grass directly on a real coast, and drawing it
// that way is most of why the edge read as two colours abutting.
uniform float beach_tiles : hint_range(0.0, 4.0) = 1.15;
uniform float beach_strength : hint_range(0.0, 1.0) = 0.92;

// The coastline is a tile-resolution shape, so a crisp edge traces the tile
// grid and reads as a staircase. This is a LOW frequency, LARGE amplitude warp
// - it moves the boundary by most of a tile, which is what turns straight
// tile-length runs into a meandering coast. The fine edge_noise above only
// jitters within a fraction of a tile and cannot do this on its own.
uniform float coast_wander : hint_range(0.0, 2.0) = 0.9;
uniform float coast_wander_scale : hint_range(0.1, 4.0) = 0.85;

uniform sampler2D tex_grass : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_dry_grass : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_dirt : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_snow : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_mud : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_gravel : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_rock : repeat_enable, filter_linear_mipmap;
// tex_sand, tex_shallow and tex_deep live in the water include: the beach and
// seabed sand is the same material the shallows show through.

// Per-terrain tint. Two terrains can share one material texture and still read
// differently - jungle is the grass material darkened, plains is the pale dry
// grassland pushed toward olive so it does not read as more sand.
// tint_sand/tint_shallow/tint_deep live in the water include.
uniform vec3 tint_grass = vec3(0.78, 0.94, 0.66);
uniform vec3 tint_dry_grass = vec3(0.80, 0.84, 0.56);
uniform vec3 tint_desert = vec3(0.94, 0.86, 0.68);
uniform vec3 tint_tundra = vec3(0.86, 0.84, 0.76);
uniform vec3 tint_snow = vec3(1.0, 1.0, 1.0);
uniform vec3 tint_ice = vec3(0.80, 0.93, 1.02);
uniform vec3 tint_jungle = vec3(0.46, 0.68, 0.40);
uniform vec3 tint_swamp = vec3(0.74, 0.82, 0.58);
uniform vec3 tint_gravel = vec3(0.94, 0.93, 0.90);
uniform vec3 tint_rock = vec3(0.78, 0.78, 0.78);

// Terrain textures are photographic and read flat when laid side by side, so a
// little saturation and contrast is applied at the end rather than baked into
// every texture.
uniform float saturation : hint_range(0.0, 2.0) = 0.86;
uniform float contrast : hint_range(0.5, 2.0) = 0.98;

vec3 material(int id, vec2 world_uv) {
	if (id == 0)  { return texture(tex_grass, world_uv).rgb * tint_grass; }
	if (id == 1)  { return texture(tex_dry_grass, world_uv).rgb * tint_dry_grass; }
	if (id == 2)  { return texture(tex_sand, world_uv).rgb * tint_desert; }
	if (id == 3)  { return texture(tex_sand, world_uv).rgb * tint_sand; }
	if (id == 4)  { return texture(tex_dirt, world_uv).rgb * tint_tundra; }
	if (id == 5)  { return texture(tex_snow, world_uv).rgb * tint_snow; }
	if (id == 6)  { return texture(tex_snow, world_uv).rgb * tint_ice; }
	if (id == 7)  { return texture(tex_grass, world_uv).rgb * tint_jungle; }
	if (id == 8)  { return texture(tex_mud, world_uv).rgb * tint_swamp; }
	if (id == 9)  { return texture(tex_gravel, world_uv).rgb * tint_gravel; }
	if (id == 10) { return texture(tex_rock, world_uv).rgb * tint_rock; }
	if (id == 11) { return texture(tex_shallow, world_uv).rgb * tint_shallow; }
	return texture(tex_deep, world_uv).rgb * tint_deep;
}

bool is_water_id(int id) {
	return id >= 11;
}

int id_at(vec2 cell) {
	vec2 clamped = clamp(cell, vec2(0.0), map_size - vec2(1.0));
	vec2 uv = (clamped + vec2(0.5)) / map_size;
	return int(texture(id_map, uv).r * 255.0 + 0.5);
}

void fragment() {
	// The map-wide UV the old full-map quad used to hand over as UV.
	vec2 map_uv = surface_local / max(map_size * cell_size, vec2(1.0));
	vec2 tile = surface_local / max(cell_size, vec2(1.0));
	vec2 cell = floor(tile);
	vec2 f = fract(tile);

	// Material is sampled from world position, not tile-local UV, so it runs
	// continuously across tiles with no visible repeat seam.
	vec2 world_uv = tile / texture_tiles;

	float t = TIME * wave_speed;

	// The whole sea - depth, swell, surf, foam - in one shared pass. This
	// surface never overscans the map, so in_map is a constant 1.
	SeaState sea = sea_state(tile, map_uv, 1.0, t);
	float sd = sea.sd;
	float open_sea = sea.open_sea;

	// Two octaves: a broad wander that breaks tile-aligned runs, and fine
	// jitter that roughens the resulting line.
	vec2 wander = vec2(
		value_noise(tile * coast_wander_scale),
		value_noise((tile * coast_wander_scale) + vec2(19.3, 7.1))) - vec2(0.5);
	vec2 fine = vec2(
		value_noise(tile * noise_scale),
		value_noise((tile * noise_scale) + vec2(37.2, 11.7))) - vec2(0.5);
	vec2 p = f + (wander * coast_wander) + (fine * edge_noise);

	// Land and water are accumulated SEPARATELY. Blending them together in one
	// pass is what made a coast look like a soft smear of every neighbour.
	// Only LAND is accumulated from neighbouring ids. Water colour comes from
	// the shared sea instead, because choosing it per tile made the sea a
	// patchwork of tile-square shallow and deep blocks - the id grid showing
	// through in the open water, which is precisely where nothing should betray
	// that the map is made of tiles.
	vec3 land_acc = vec3(0.0);
	float land_w = 0.0;
	float radius = 0.5 + blend_width;

	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			vec2 offset = vec2(float(dx), float(dy));
			int nid = id_at(cell + offset);
			if (is_water_id(nid)) { continue; }

			float d = distance(p, offset + vec2(0.5));
			float w = max(0.0, radius - d);
			w = w * w * w;
			if (w <= 0.0) { continue; }

			land_acc += material(nid, world_uv) * w;
			land_w += w;
		}
	}

	vec3 land_col = land_w > 0.0 ? land_acc / land_w : material(0, world_uv);

	// ONE waterline, and it is the shared sea's wobbled distance field.
	// Mixing land to water on the tile-resolution neighbour fraction instead
	// put a second, coarser coastline on the map: it disagreed with the field
	// that beach, wet sand and surf are measured against, so it drew the tile
	// staircase AND covered the beach before that band had any width on
	// screen.
	float shore = smoothstep(-shore_blend_tiles, shore_blend_tiles, sd);

	// Sand on the land side, widest at the waterline and fading inland. It stays
	// at full strength out past sd = 0 on purpose: that is the sand the shallow
	// water is drawn over, and without it the beach would stop dead at the
	// waterline instead of running on under the surf.
	float inland = max(0.0, -sd);
	float beach = smoothstep(0.0, 1.0, clamp(1.0 - (inland / max(0.001, beach_tiles)), 0.0, 1.0));
	land_col = mix(land_col, material(3, world_uv), clamp(beach * beach_strength, 0.0, 1.0));

	// How far the wash has run up the sand right now. Cosine, so it surges and
	// drains rather than scrolling in one direction.
	float swash_reach = swash_tiles * wave_intensity
		* (0.5 + (0.5 * cos(t * swash_speed)));

	// Damp sand: the strip the last wave actually reached, so the waterline
	// leaves a wet mark behind it instead of a fixed band. The swash term is
	// ocean-only, like the swash itself: without the gate a heavy sea made
	// every LAKE's margin breathe in time with surf the lake does not have.
	float wet = clamp(1.0 - (inland / max(0.001, max(wet_tiles, swash_reach * open_sea))), 0.0, 1.0);
	land_col *= mix(1.0, 1.0 - shore_wet, wet);

	// Hillshade applies to ground only; lighting the sea by land relief looks
	// like a stain on the water.
	float shade = mix(1.0, texture(shade_map, map_uv).r * 2.0, shade_strength);
	land_col *= shade;

	// Surf goes into the WATER, not over the finished pixel, so the shore is
	// composited on top of it. Painted afterwards it washed across the beach,
	// putting foam on dry sand.
	vec3 water_col = mix(sea.colour, vec3(0.96, 0.99, 1.0), sea.foam);

	// The leading edge of the wash, drawn on the sand itself - and bright
	// ONLY while it runs UP the beach. Water does not retreat as a white
	// line: the uprush carries the foam edge, then the sheet drains and the
	// foam dies where it stands. The old symmetric version slid the same
	// bright line back down the sand every cycle - a ripple visibly leaving
	// the beach for the sea, the most artificial motion on the map.
	float run_up = smoothstep(-0.1, 0.6, -sin(t * swash_speed));
	float front_width = 0.06 + (0.10 * wave_intensity);
	float front = 1.0 - smoothstep(0.0, front_width, abs(inland - swash_reach));
	land_col = mix(land_col, vec3(0.96, 0.99, 1.0),
		clamp(front * run_up * sea.patch * foam_strength * open_sea * sea.facing
			* step(0.02, swash_reach), 0.0, 1.0));

	// Land over water, along the one waterline.
	vec3 col = mix(land_col, water_col, shore);

	float grey = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(vec3(grey), col, saturation);
	col = clamp((col - vec3(0.5)) * contrast + vec3(0.5), vec3(0.0), vec3(1.0));

	COLOR = vec4(col, 1.0);
}
