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
uniform sampler2D lake_map : filter_linear, repeat_disable;
uniform sampler2D lake_width_map : filter_nearest, repeat_disable;
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
	// The single square-tile batch is centered on its first cell, not its
	// top-left corner. Restore map-local coordinates for ID and coast sampling.
	surface_local = VERTEX + cell_size * 0.5;
}

// Width of the blend between two terrains, as a fraction of a tile.
uniform float blend_width : hint_range(0.0, 0.9) = 0.42;
uniform float blend_sharpness : hint_range(1.0, 8.0) = 4.0;
uniform float material_edge_detail : hint_range(0.0, 1.0) = 0.75;

// How much the blend edge is broken up, so borders are not straight lines.
uniform float edge_noise : hint_range(0.0, 1.0) = 0.55;
uniform float noise_scale = 5.0;

uniform float shade_strength : hint_range(0.0, 2.0) = 0.35;

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
uniform bool contour_debug = false;

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
uniform sampler2D tex_lava : repeat_enable, filter_linear_mipmap;
// Optional per-texture repeat sizes; terrain aliases share their source texture's scale.
uniform float grass_texture_tiles = 0.0;
uniform float dry_grass_texture_tiles = 0.0;
uniform float dirt_texture_tiles = 0.0;
uniform float snow_texture_tiles = 0.0;
uniform float mud_texture_tiles = 0.0;
uniform float gravel_texture_tiles = 0.0;
uniform float rock_texture_tiles = 0.0;
uniform float lava_texture_tiles = 0.0;
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
uniform vec3 tint_lava = vec3(1.0);

// Optional colour grading is neutral by default: preserve authored material
// colours instead of desaturating every biome and the shared sea.
uniform float saturation : hint_range(0.0, 2.0) = 1.0;
uniform float contrast : hint_range(0.5, 2.0) = 1.0;

// Zero preserves the original painted presentation, including its materials.
uniform int art_style = 0;
uniform int art_pixels_per_cell = 64;
uniform float art_ground_detail = 0.12;
uniform float art_repeat_cells = 6.0;
uniform vec3 art_grass = vec3(0.36, 0.64, 0.22);
uniform vec3 art_dry_grass = vec3(0.55, 0.67, 0.27);
uniform vec3 art_beach = vec3(0.92, 0.77, 0.46);
uniform vec3 art_shallow = vec3(0.18, 0.72, 0.68);
uniform vec3 art_deep = vec3(0.06, 0.43, 0.51);

float material_repeat_tiles(int id) {
	if (art_style > 0) { return art_repeat_cells; }
	if (id == 2 || id == 3) { return sand_repeat_tiles(); }
	float repeat_tiles = 0.0;
	if (id == 0 || id == 7) { repeat_tiles = grass_texture_tiles; }
	else if (id == 1) { repeat_tiles = dry_grass_texture_tiles; }
	else if (id == 4) { repeat_tiles = dirt_texture_tiles; }
	else if (id == 5 || id == 6) { repeat_tiles = snow_texture_tiles; }
	else if (id == 8) { repeat_tiles = mud_texture_tiles; }
	else if (id == 9) { repeat_tiles = gravel_texture_tiles; }
	else if (id == 10) { repeat_tiles = rock_texture_tiles; }
	else if (id == 13) { repeat_tiles = lava_texture_tiles; }
	return repeat_tiles > 0.0 ? max(0.25, repeat_tiles) : max(1.0, ground_texture_tiles);
}

vec3 source_material(int id, vec2 world_uv, vec2 uv_dx, vec2 uv_dy) {
	float scale = ground_texture_tiles / material_repeat_tiles(id);
	world_uv *= scale;
	uv_dx *= scale;
	uv_dy *= scale;
	if (art_style > 0) {
		vec2 direction = mix(vec2(1.0), vec2(-1.0), step(vec2(1.0), mod(floor(world_uv), vec2(2.0))));
		world_uv = vec2(1.0) - abs(mod(world_uv, vec2(2.0)) - vec2(1.0));
		uv_dx *= direction;
		uv_dy *= direction;
	}
	// IDs vary per fragment. Explicit gradients keep mip selection defined
	// inside these branches, particularly at material borders when zoomed out.
	if (id == 0)  { return sample_ground(tex_grass, world_uv, uv_dx, uv_dy) * tint_grass; }
	if (id == 1)  { return sample_ground(tex_dry_grass, world_uv, uv_dx, uv_dy) * tint_dry_grass; }
	if (id == 2)  { return sample_ground(tex_sand, world_uv, uv_dx, uv_dy) * tint_desert; }
	if (id == 3)  { return sample_ground(tex_sand, world_uv, uv_dx, uv_dy) * tint_sand; }
	if (id == 4)  { return sample_ground(tex_dirt, world_uv, uv_dx, uv_dy) * tint_tundra; }
	if (id == 5)  { return sample_ground(tex_snow, world_uv, uv_dx, uv_dy) * tint_snow; }
	if (id == 6)  { return sample_ground(tex_snow, world_uv, uv_dx, uv_dy) * tint_ice; }
	if (id == 7)  { return sample_ground(tex_grass, world_uv, uv_dx, uv_dy) * tint_jungle; }
	if (id == 8)  { return sample_ground(tex_mud, world_uv, uv_dx, uv_dy) * tint_swamp; }
	if (id == 9)  { return sample_ground(tex_gravel, world_uv, uv_dx, uv_dy) * tint_gravel; }
	if (id == 10) { return sample_ground(tex_rock, world_uv, uv_dx, uv_dy) * tint_rock; }
	if (id == 13) { return sample_ground(tex_lava, world_uv, uv_dx, uv_dy) * tint_lava; }
	if (id == 11) { return textureGrad(tex_shallow, world_uv, uv_dx, uv_dy).rgb * tint_shallow; }
	return textureGrad(tex_deep, world_uv, uv_dx, uv_dy).rgb * tint_deep;
}

vec3 material(int id, vec2 world_uv, vec2 uv_dx, vec2 uv_dy) {
	vec3 sampled = source_material(id, world_uv, uv_dx, uv_dy);
	if (art_style == 0) { return sampled; }
	vec3 base = art_grass;
	if (id == 1) { base = art_dry_grass; }
	else if (id == 2 || id == 3) { base = art_beach; }
	else if (id == 4) { base = vec3(0.50, 0.40, 0.29); }
	else if (id == 5) { base = vec3(0.89, 0.95, 0.97); }
	else if (id == 6) { base = vec3(0.58, 0.85, 0.92); }
	else if (id == 7) { base = vec3(0.20, 0.47, 0.20); }
	else if (id == 8) { base = vec3(0.40, 0.34, 0.22); }
	else if (id == 9 || id == 10) { base = vec3(0.48, 0.48, 0.43); }
	else if (id == 13) { return sampled; }
	vec3 result = mix(base, sampled, art_ground_detail);
	return art_style == 2 ? floor(result * 24.0 + 0.5) / 24.0 : result;
}

bool is_water_id(int id) {
	return id == 11 || id == 12;
}

int id_at(vec2 cell) {
	vec2 clamped = clamp(cell, vec2(0.0), map_size - vec2(1.0));
	vec2 uv = (clamped + vec2(0.5)) / map_size;
    vec4 data = texture(id_map, uv);
    bool bank = data.a > 0.0 || texture(lake_width_map, uv).r > 0.0;
    return int((bank ? data.b : data.r) * 255.0 + 0.5);
}

void fragment() {
	// What the item was drawn with: the blank surface tile times the node's
	// Modulate, SelfModulate and every modulate inherited from its parents. The
	// ground replaces the tile's colour, never the modulate, so the result is
	// multiplied by this at the end - written without it, a renderer tinted or
	// faded through its node kept drawing at full colour and full opacity.
	// (The scene's CanvasModulate is applied by Godot after this function and
	// never passes through COLOR.)
	vec4 modulate = COLOR;
	// The map-wide UV the old full-map quad used to hand over as UV.
	vec2 map_uv = surface_local / max(map_size * cell_size, vec2(1.0));
	vec2 tile = surface_local / max(cell_size, vec2(1.0));
	vec2 grid_tile = tile + map_origin;

	// Material is sampled from world position, not tile-local UV, so it runs
	// continuously across tiles with no visible repeat seam.
	vec2 world_uv = grid_tile / ground_texture_tiles;
	vec2 uv_dx = dFdx(world_uv);
	vec2 uv_dy = dFdy(world_uv);
	// Snap only sampling positions. Derivatives retain the continuous footprint
	// so minification still chooses mipmaps when the camera zooms out.
	if (art_style == 2) {
		float pixels = float(max(32, art_pixels_per_cell));
		tile = (floor(tile * pixels) + 0.5) / pixels;
		grid_tile = tile + map_origin;
		map_uv = tile / map_size;
		world_uv = grid_tile / ground_texture_tiles;
	}

	float t = TIME * wave_speed;

	// The whole sea - depth, swell, surf, foam - in one shared pass. This
	// surface never overscans the map, so in_map is a constant 1.
	SeaState sea = sea_state(tile, map_uv, 1.0, t);
	float sd = sea.sd;
	float open_sea = sea.open_sea;

	// Low-frequency contour variation only. High-frequency displacement made
	// otherwise rounded material boundaries look like chipped cell edges.
	vec2 wander = vec2(
		value_noise(grid_tile * coast_wander_scale),
		value_noise((grid_tile * coast_wander_scale) + vec2(19.3, 7.1))) - vec2(0.5);
	vec2 fine = vec2(
		value_noise(grid_tile * noise_scale * 0.1),
		value_noise((grid_tile * noise_scale * 0.1) + vec2(37.2, 11.7))) - vec2(0.5);
	// Preserve the shared shoreline in every style. Inland material roughness
	// must not displace grass across the beach independently of the waterline.
	float art_displacement = art_style > 0 ? 0.42 : 1.0;
	float coast_displacement = smoothstep(0.5, 2.0, max(0.0, -sd));
	vec2 material_tile = tile + (wander * coast_wander + fine * edge_noise * 0.15) * art_displacement * coast_displacement;
	vec2 cell = floor(material_tile);
	vec2 p = fract(material_tile);

	// Land and water are accumulated SEPARATELY. Blending them together in one
	// pass is what made a coast look like a soft smear of every neighbour.
	// Only LAND is accumulated from neighbouring ids. Water colour comes from
	// the shared sea instead, because choosing it per tile made the sea a
	// patchwork of tile-square shallow and deep blocks - the id grid showing
	// through in the open water, which is precisely where nothing should betray
	// that the map is made of tiles.
	vec3 land_acc = vec3(0.0);
	float land_w = 0.0;
	float width = max(0.001, blend_width);
	float nearest_distance = 100.0;
	int nearest_land = 3;
	float terrain_weights[14];
	for (int id = 0; id < 14; id++) { terrain_weights[id] = 0.0; }

	for (int dy = -2; dy <= 2; dy++) {
		for (int dx = -2; dx <= 2; dx++) {
			vec2 offset = vec2(float(dx), float(dy));
			int nid = id_at(cell + offset);
			if (is_water_id(nid)) { continue; }

			vec2 delta = abs(p - offset - vec2(0.5));
			float d = dot(delta, delta);
			if (d < nearest_distance) {
				nearest_distance = d;
				nearest_land = nid;
			}
			// Gather coverage by MATERIAL before sharpening. Sharpening each
			// cell independently creates square Voronoi-like patches even when
			// several neighbours belong to the same continuous terrain.
			vec2 coverage = vec2(1.0) - smoothstep(vec2(0.5 - width), vec2(0.5 + width), delta);
			float radius = mix(0.708, 2.5, clamp(blend_width / 0.42, 0.0, 1.0));
			float w = blend_width <= 0.0 ? coverage.x * coverage.y
				: 1.0 - smoothstep(0.0, radius, length(delta));
			terrain_weights[nid] += w;
		}
	}
	for (int id = 0; id < 14; id++) {
		if (terrain_weights[id] <= 0.0) { continue; }
		vec3 sampled = material(id, world_uv, uv_dx, uv_dy);
		float detail = dot(sampled, vec3(0.299, 0.587, 0.114));
		// The broad contour kernel rounds shapes; this narrower transition
		// keeps it from turning into a broad, blurry colour blend.
		float w = pow(terrain_weights[id], blend_sharpness * 4.0)
			* exp2((detail - 0.5) * 8.0 * material_edge_detail);
		land_acc += sampled * w;
		land_w += w;
	}

	vec3 land_col = land_w > 0.0 ? land_acc / land_w : material(nearest_land, world_uv, uv_dx, uv_dy);

	// ONE waterline, and it is the shared sea's wobbled distance field.
	// Mixing land to water on the tile-resolution neighbour fraction instead
	// put a second, coarser coastline on the map: it disagreed with the field
	// that beach, wet sand and surf are measured against, so it drew the tile
	// staircase AND covered the beach before that band had any width on
	// screen.
	// A LAKE ends in a line. The sea's edge is soft on purpose - the sand, the wash and the
	// surf carry that transition - but a lake has none of them, so the same blend reads as its
	// water smeared into the ground. open_sea is the flag that already separates the two.
	float shore_softness = mix(shore_blend_tiles * 0.2, shore_blend_tiles, open_sea);
	float shore = smoothstep(-shore_softness, shore_softness, sd);
	if (art_style == 1) { shore = smoothstep(-0.06, 0.06, sd); }
	else if (art_style == 2) { shore = step(0.0, sd); }

	// Sand on the land side, widest at the waterline and fading inland. It stays
	// at full strength out past sd = 0 on purpose: that is the sand the shallow
	// water is drawn over, and without it the beach would stop dead at the
	// waterline instead of running on under the surf.
	float inland = max(0.0, -sd);
    float beach_width = texture(id_map, map_uv).a * 4.0;
    float ocean_inland = max(0.0, (0.5 - texture(coast_map, map_uv).b) * 2.0 * coast_range);
    float edge_aa = max(0.005, fwidth(ocean_inland) * 0.5);
    float beach = beach_width > 0.0 ? 1.0 - smoothstep(beach_width - edge_aa, beach_width + edge_aa, ocean_inland) : 0.0;
    float lake_width = texture(lake_width_map, map_uv).r * 3.0;
    float lake_inland = max(0.0, (0.5 - texture(lake_map, map_uv).r) * 2.0 * coast_range);
    float lake_aa = max(0.005, fwidth(lake_inland) * 0.5);
    float lake_bank = lake_width > 0.0 ? 1.0 - smoothstep(lake_width - lake_aa, lake_width + lake_aa, lake_inland) : 0.0;
    beach = max(beach, lake_bank);
    land_col = mix(land_col, material(3, world_uv, uv_dx, uv_dy), beach);

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
	if (art_style == 1) {
		water_col = mix(art_shallow, art_deep, smoothstep(0.0, max(1.0, deep_tiles), sd));
		water_col = mix(water_col, vec3(0.86, 0.98, 0.94), sea.foam * 0.5);
	} else if (art_style == 2) {
		float depth_band = floor(clamp(sd / max(1.0, deep_tiles), 0.0, 1.0) * 3.0) / 3.0;
		water_col = mix(art_shallow, art_deep, depth_band);
		float rim = step(0.0, sd) * (1.0 - step(0.10, sd));
		float ripples = step(0.92, value_noise(grid_tile * 2.0 + vec2(t * 0.15, 0.0)));
		water_col = mix(water_col, vec3(0.70, 0.86, 0.88), max(rim, ripples * 0.18));
	}

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

    COLOR = vec4(col, 1.0) * modulate;
    if (contour_debug) {
        vec3 ground = beach > 0.5 ? vec3(0.91, 0.73, 0.40) : vec3(0.27, 0.57, 0.20);
        COLOR = vec4(sd >= 0.0 ? vec3(0.10, 0.48, 0.64) : ground, 1.0) * modulate;
    }
}
