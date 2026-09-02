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

uniform sampler2D id_map : filter_nearest, repeat_disable;
// Separate, linearly filtered, because ids must never interpolate but shade
// must - sharing one map makes relief a grid of hard brightness squares.
uniform sampler2D shade_map : filter_linear, repeat_disable;
// Signed distance to the coastline in tiles, encoded 0..1 around 0.5.
// Beach and surf are specified in TILES against this, because a land/water
// fraction ramps across barely a pixel and any band expressed in it vanishes.
uniform sampler2D coast_map : filter_linear, repeat_disable;
uniform float coast_range = 5.0;
uniform vec2 map_size = vec2(96.0, 60.0);

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

// How many tiles one repeat of a material texture covers. Larger means the
// texture detail is bigger relative to a tile.
uniform float texture_tiles = 6.0;

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
uniform float foam_strength : hint_range(0.0, 1.0) = 0.50;

// How heavy the sea is. ONE dial, because big waves are not simply brighter
// foam: they reach further up the beach, their crests are broader, and the
// swash surges further inland. Scaling those together is what reads as a heavy
// sea rather than as the same small waves turned up. 0 is a millpond, 1 an
// ordinary day, 2 a storm.
uniform float wave_intensity : hint_range(0.0, 2.0) = 1.0;

// Swash: the sheet of water that runs UP the sand and drains back between
// crests. Standard shoreline practice (Cyanilux) is to drive it from a slower
// cosine synced to the arriving crests, and it does more to sell a heavy sea
// than the crests themselves. Measured in tiles at full intensity.
uniform float swash_tiles : hint_range(0.0, 3.0) = 0.26;
uniform float swash_speed : hint_range(0.0, 4.0) = 1.1;
uniform float foam_tiles : hint_range(0.1, 6.0) = 0.95;
uniform float wave_speed : hint_range(0.0, 4.0) = 1.0;
uniform float wave_amount : hint_range(0.0, 0.06) = 0.022;
uniform float wet_tiles : hint_range(0.0, 2.0) = 0.45;
uniform float shore_wet : hint_range(0.0, 1.0) = 0.30;

// A beach. Water does not meet grass directly on a real coast, and drawing it
// that way is most of why the edge read as two colours abutting.
uniform float beach_tiles : hint_range(0.0, 4.0) = 1.15;
uniform float beach_strength : hint_range(0.0, 1.0) = 0.92;

// The shelf of shallow water lying over the beach sand, in tiles, and how much
// of that seabed shows through it.
uniform float shallow_tiles : hint_range(0.0, 8.0) = 1.8;
uniform float shallow_sand : hint_range(0.0, 1.0) = 0.34;

// How far out the sea reaches full depth, in tiles.
uniform float deep_tiles : hint_range(0.5, 12.0) = 4.5;

// Authored surf. A seamless foam texture, sampled in SHORE SPACE: across it
// runs distance from the waterline, along it runs the coast itself. The shore
// tangent is taken from the gradient of the distance field, which is what lets
// authored crests follow a curving coast instead of lying along the world axes
// - the reason an earlier attempt to slide along a frame strip drew diagonal
// stripes out to sea.
//
// The texture needs a SOFT fringe. A flat cutout silhouette carries no falloff
// and collapses to one solid band however it is sampled.
uniform sampler2D foam_sheet : repeat_enable, filter_linear_mipmap;
uniform bool use_foam_sheet = false;
// One repeat of the foam texture covers this many tiles ALONG the shore. Long,
// so a coastline does not visibly repeat.
uniform float foam_tiles_along : hint_range(1.0, 48.0) = 11.0;
// ...and this many ACROSS it. Short, because the surf band is about a tile
// deep: scale both axes together and only a fraction of the texture's height
// lands inside the band, so a single ribbon is stretched along the whole coast
// instead of several crests sitting in it.
// SHORT on purpose: the surf band is under a tile deep, and a repeat spread
// over many tiles parks the sheet's crest bands outside it - the visible
// strip then shows the empty texture between bands, and the beach has no
// foam at all for most of each scroll cycle.
uniform float foam_tiles_across : hint_range(0.3, 8.0) = 1.6;
// How fast the authored crests advance onto the beach.
uniform float foam_scroll : hint_range(0.0, 4.0) = 0.055;

// How strongly the surf pulses as crests arrive, and how fast they come. Kept
// slow and few: crowding crests into a band a tile deep and running them
// quickly aliases, and the train then reads as moving backwards.
uniform float foam_pulse : hint_range(0.0, 1.0) = 0.34;
uniform float foam_arrival_rate : hint_range(0.0, 4.0) = 0.9;



// Surf arrives as a train of crests running parallel to the shore, not as one
// band. The count is what makes it read as waves rather than an outline.
uniform float foam_lines : hint_range(1.0, 8.0) = 2.3;

// The coastline is a tile-resolution shape, so a crisp edge traces the tile
// grid and reads as a staircase. This is a LOW frequency, LARGE amplitude warp
// - it moves the boundary by most of a tile, which is what turns straight
// tile-length runs into a meandering coast. The fine edge_noise above only
// jitters within a fraction of a tile and cannot do this on its own.
uniform float coast_wander : hint_range(0.0, 2.0) = 0.9;
uniform float coast_wander_scale : hint_range(0.1, 4.0) = 0.85;

// The direction the swell TRAVELS, in degrees, y-down screen space. Waves
// come from somewhere: surf belongs to the shores facing them, and a lee
// shore keeps only what refraction wraps around. Without this every island
// wore its surf as a closed ring, which no real sea draws.
uniform float swell_direction_degrees : hint_range(0.0, 360.0) = 210.0;
// 0 restores surf on every shore alike; 1 leaves lee shores nearly flat.
uniform float swell_directionality : hint_range(0.0, 1.0) = 0.65;

uniform sampler2D tex_grass : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_dry_grass : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_sand : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_dirt : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_snow : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_mud : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_gravel : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_rock : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_shallow : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_deep : repeat_enable, filter_linear_mipmap;

// Per-terrain tint, so two terrains can share one material texture and still
// read differently - jungle is the grass material darkened, for instance.
// Per-terrain tint. Two terrains can share one material texture and still read
// differently - jungle is the grass material darkened, plains is the pale dry
// grassland pushed toward olive so it does not read as more sand.
uniform vec3 tint_grass = vec3(0.78, 0.94, 0.66);
uniform vec3 tint_dry_grass = vec3(0.80, 0.84, 0.56);
uniform vec3 tint_desert = vec3(0.94, 0.86, 0.68);
uniform vec3 tint_sand = vec3(0.98, 0.94, 0.82);
uniform vec3 tint_tundra = vec3(0.86, 0.84, 0.76);
uniform vec3 tint_snow = vec3(1.0, 1.0, 1.0);
uniform vec3 tint_ice = vec3(0.80, 0.93, 1.02);
uniform vec3 tint_jungle = vec3(0.46, 0.68, 0.40);
uniform vec3 tint_swamp = vec3(0.74, 0.82, 0.58);
uniform vec3 tint_gravel = vec3(0.94, 0.93, 0.90);
uniform vec3 tint_rock = vec3(0.78, 0.78, 0.78);
uniform vec3 tint_shallow = vec3(0.62, 0.80, 0.88);
uniform vec3 tint_deep = vec3(0.44, 0.58, 0.78);

// Terrain textures are photographic and read flat when laid side by side, so a
// little saturation and contrast is applied at the end rather than baked into
// every texture.
uniform float saturation : hint_range(0.0, 2.0) = 0.86;
uniform float contrast : hint_range(0.5, 2.0) = 0.98;

// Smooth value noise. Straight smoothstep blends read as machine-made arcs, so
// the edge position is perturbed before the blend is evaluated.
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

	// Water gets a slow rolling warp so the surface is alive rather than a flat
	// painted sheet.
	float t = TIME * wave_speed;
	// The shore frame, from the gradient of the distance field: which way is out
	// to sea, and which way the coast runs. Everything that MOVES on the water
	// is expressed in this frame, because a drift written in world axes runs
	// out to sea on whichever coasts happen to face that way.
	vec2 shore_texel = 1.0 / map_size;
	vec2 shore_grad = vec2(
		texture(coast_map, map_uv + vec2(shore_texel.x, 0.0)).r - texture(coast_map, map_uv - vec2(shore_texel.x, 0.0)).r,
		texture(coast_map, map_uv + vec2(0.0, shore_texel.y)).r - texture(coast_map, map_uv - vec2(0.0, shore_texel.y)).r);
	float shore_glen = length(shore_grad);
	// A flat gradient is open water far from any coast, where none of this is
	// visible; any direction will do rather than normalising zero.
	vec2 seaward = shore_glen > 1e-6 ? shore_grad / shore_glen : vec2(0.0, 1.0);
	vec2 along_shore = vec2(-seaward.y, seaward.x);

	// How exposed this stretch of coast is to the incoming swell: 1 facing
	// it head on, falling toward the lee. Everything the sea throws at the
	// shore - surf, swash, the arriving roll - scales by this.
	float swell_rad = radians(swell_direction_degrees);
	vec2 swell_travel = vec2(cos(swell_rad), sin(swell_rad));
	float exposure = clamp(-dot(seaward, swell_travel), 0.0, 1.0);
	float facing = mix(1.0, 0.20 + (0.80 * exposure), swell_directionality);

	// Sampled in WORLD space, on world axes. Translating these coordinates by a
	// shore-relative vector was a mistake: that direction points at the nearest
	// coast, so inside a lake it converges from every side, and translating UVs
	// by a field that varies per pixel SHEARS the texture - it came out as
	// swirling marbled smears in the water. Only the surf band, thin enough that
	// the frame barely turns across it, is drawn shore-relative.
	vec2 water_uv = world_uv + vec2(
		sin((world_uv.y * 7.0) + (t * 0.7)),
		cos((world_uv.x * 6.0) + (t * 0.5))) * wave_amount;

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
	// Only LAND is accumulated from neighbouring ids. Water colour is taken from
	// depth further down instead, because choosing it per tile made the sea a
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

	// Signed distance to the waterline, in tiles: positive out to sea.
	vec4 coast = texture(coast_map, map_uv);
	float sd = (coast.r - 0.5) * 2.0 * coast_range;
	// Green marks open sea. Surf needs a fetch behind it, so a lake or a river
	// stays flat however close to the shore it is.
	float open_sea = smoothstep(0.35, 0.75, coast.g);
	// The field is one value per sub-tile, so its contours are still faceted
	// and surf drawn on them looks like survey lines. Two octaves of wobble
	// break that up: a broad one to meander the whole band, a finer one to
	// roughen each crest.
	sd += (value_noise(tile * 1.1) - 0.5) * 0.30;
	sd += (value_noise((tile * 3.4) + vec2(5.3, 9.1)) - 0.5) * 0.18;

	// Depth from the same distance field: shallow against the shore, shelving to
	// deep offshore. Taking it from tile ids instead is what tiled the sea.
	vec3 water_col = mix(
		texture(tex_shallow, water_uv).rgb * tint_shallow,
		texture(tex_deep, water_uv).rgb * tint_deep,
		smoothstep(0.0, max(0.001, deep_tiles), sd));

	// ONE waterline, and it is this distance field. Mixing land to water on the
	// tile-resolution neighbour fraction instead put a second, coarser coastline
	// on the map: it disagreed with the field that beach, wet sand and surf are
	// measured against, so it drew the tile staircase AND covered the beach
	// before that band had any width on screen.
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

	// Shallow water is water OVER that same sand, so the bottom shows through
	// near the shore. Sampled on world_uv, not the drifting water_uv: a seabed
	// that swims with the surface reads as fog, not as depth. Without this the
	// sea is at full depth the instant it starts, which is what makes a coast
	// look cut rather than shelving.
	float shallow = 1.0 - smoothstep(0.0, max(0.001, shallow_tiles), sd);
	water_col = mix(water_col, material(3, world_uv), shallow * shallow_sand);

	// Hillshade applies to ground only; lighting the sea by land relief looks
	// like a stain on the water.
	float shade = mix(1.0, texture(shade_map, map_uv).r * 2.0, shade_strength);
	land_col *= shade;

	// Swell, in two parts, because "which way is the sea moving" has two
	// honest answers. Near the coast it moves TOWARD the beach: a luminance
	// wave travelling down the distance field, so its fronts lie along the
	// shore and always arrive, never depart. The previous version scrolled
	// one noise along a fixed world vector while its comment claimed it
	// rolled shoreward - on every coast facing against that vector the sea
	// visibly ran out to sea. Where the field saturates there is no "toward"
	// left to express, so the roll hands over to a driftless shimmer: two
	// static patterns crossfaded, movement with no net direction.
	float shimmer = mix(
		value_noise((tile * 1.4) + vec2(3.1, 7.7)),
		value_noise((tile * 1.4) + vec2(9.2, 1.3)),
		0.5 + (0.5 * sin(t * 0.5)));
	float roll = sin((sd * 2.6) + (t * 1.1) + (value_noise(tile * 0.9) * 2.2));
	// Confined to the surf zone, and only on water with a fetch. Let out to
	// several tiles it drew faint concentric rings around every island -
	// and rings pulsing around a shore read as radiating OUT of it, the
	// stone-in-a-pond look, whichever way their phase actually runs.
	float nearing = (1.0 - smoothstep(0.3, 1.6, sd)) * open_sea * facing;
	water_col *= (0.95 + (0.10 * shimmer)) + (0.035 * roll * nearing);

	// Surf, as a band measured out from the waterline rather than around a tile.
	// A heavier sea throws its surf further out.
	float reach = foam_tiles * (0.45 + (0.55 * wave_intensity));
	float from_shore = clamp(sd / max(0.001, reach), 0.0, 1.0);
	// Broken up along the shore so the band is never an even ribbon. The
	// pattern shifts without travelling - two static noises crossfaded -
	// because scrolling a noise here was one more thing quietly sliding
	// beach-to-sea on whichever coasts face against the scroll vector.
	float patch = 0.72 + (0.28 * smoothstep(0.25, 0.80, mix(
		value_noise((tile * 1.5) + vec2(3.7, 8.2)),
		value_noise((tile * 1.5) + vec2(11.4, 2.9)),
		0.5 + (0.5 * sin(t * 0.35)))));

	float lines;
	if (use_foam_sheet) {
		float along_coord = dot(tile, along_shore);
		// PLUS t on the across coordinate. V grows OUT to sea, so adding
		// time moves a fixed texture feature to smaller sd - onto the beach.
		// The original minus scrolled every authored crest out to sea, and
		// was invisible only because no shipped scene assigned a sheet.
		lines = texture(foam_sheet, vec2(
			along_coord / max(0.001, foam_tiles_along),
			(sd / max(0.001, foam_tiles_across)) + (t * foam_scroll))).a;

		// The texture supplies foam SHAPE; this supplies its ARRIVAL. Real shore
		// break pulses - a crest sweeps in, breaks and drains - and a band of
		// even brightness sitting on the coast is the single thing that most
		// makes a shoreline read as a decal stuck to the map rather than water.
		//
		// The phase drifts ALONG the shore as well, because sets do not arrive
		// down a whole coastline at once; without it the entire island pulses
		// white in unison, which is its own kind of wrong.
		float arrival = sin((from_shore * foam_lines * 6.28318)
			+ (t * foam_arrival_rate)
			+ (value_noise(vec2(along_coord * 0.09, 0.0)) * 6.28318));
		lines *= mix(1.0, smoothstep(-0.35, 0.85, arrival), foam_pulse);
	} else {
		// No sheet assigned: a generated breaker train. Deliberate, not a
		// stand-in for missing configuration - a scene may legitimately want
		// no art here.
		//
		// A real wave is not a band of constant white sliding in: it BUILDS
		// as the bottom shoals, breaks brightest at one depth, and dissolves
		// into lacy wash. Phase counts crests out from the shore and time
		// marches them in - PLUS t, not minus: a crest sits where the phase
		// is constant, so with a minus the whole train runs out to sea. The
		// integer part of the phase is WHICH wave, and the shaping below keys
		// off it so each wave arrives in its own segments at its own moment
		// instead of the coast pulsing as concentric rings.
		float along_coord = dot(tile, along_shore);
		// Bent per shore position, so no two crests stay parallel.
		float bend = (value_noise(vec2(along_coord * 0.13, 11.7)) - 0.5) * 0.9;
		float phase = (from_shore * foam_lines) + (t * 0.42) + bend;
		float wave_id = floor(phase);
		// 0 on a crest's centre line, 1 midway to the next crest.
		float off_crest = abs(fract(phase) - 0.5) * 2.0;

		// Heavier seas throw broader crests.
		float heavy = clamp(wave_intensity * 0.5, 0.0, 1.0);
		float crest_line = 1.0 - smoothstep(0.0, mix(0.16, 0.42, heavy), off_crest);

		// Each wave whitens in SEGMENTS of its own, gated by noise keyed on
		// the shore position AND the wave's index - the next wave breaks
		// somewhere else along the coast, which is most of what stops the
		// train reading as drawn rings. The seam where the key changes sits
		// exactly between crests, where crest_line is zero and hides it.
		float seg = smoothstep(0.30, 0.62,
			value_noise(vec2(along_coord * 0.45, wave_id * 7.31)));

		// The break itself: brightest where the water has just shoaled, dim
		// seaward of it (unbroken swell) and spent shoreward. The band's
		// overall fade stays with the falloff below; this only moves the PEAK
		// off the waterline and out to the break depth.
		float shoal = from_shore - 0.22;
		float breaker = exp(-(shoal * shoal) / 0.045);

		// Lace, so the white is broken foam and never a solid stripe.
		float lace = 0.55 + (0.45 * value_noise((tile * 7.0) + vec2(t * 0.3, wave_id * 3.7)));

		// Spent water running on to the sand after the break. Its texture
		// stands still and its BRIGHTNESS surges in step with the crests
		// arriving - scrolling this noise along a world vector was one more
		// thing quietly sliding beach-to-sea on half the coasts.
		float wash_pulse = 0.55 + (0.45 * sin(((t * 0.42) + bend) * 6.28318));
		float wash = (1.0 - smoothstep(0.0, 0.30, from_shore))
			* (0.30 + (0.70 * value_noise(tile * 4.5)))
			* wash_pulse;

		lines = clamp((crest_line * seg * breaker * lace * 1.7) + (wash * 0.45), 0.0, 1.0);
	}

	// The shorebreak: a thin lacy fringe pinned to the waterline itself, so
	// the beach is never bare while the crests are still out in the band.
	// Its texture stands still; only its brightness breathes, out of step
	// along the shore.
	float fringe = (1.0 - smoothstep(0.02, 0.30, abs(sd)))
		* (0.40 + (0.60 * value_noise(tile * 6.3)))
		* (0.70 + (0.30 * sin((t * 0.9) + (value_noise(vec2(dot(tile, along_shore) * 0.2, 4.7)) * 6.28318))));
	lines = max(lines, fringe);

	float falloff = 1.0 - smoothstep(0.0, 1.0, from_shore);
	// Surf goes into the WATER, not over the finished pixel, so the shore is
	// composited on top of it. Painted afterwards it washed across the beach,
	// putting foam on dry sand.
	water_col = mix(water_col, vec3(0.96, 0.99, 1.0),
		clamp(lines * falloff * patch * foam_strength * open_sea * facing
			* clamp(wave_intensity, 0.0, 1.4) * smoothstep(-0.12, 0.06, sd), 0.0, 1.0));

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
		clamp(front * run_up * patch * foam_strength * open_sea * facing
			* step(0.02, swash_reach), 0.0, 1.0));

	// Land over water, along the one waterline.
	vec3 col = mix(land_col, water_col, shore);

	float grey = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(vec3(grey), col, saturation);
	col = clamp((col - vec3(0.5)) * contrast + vec3(0.5), vec3(0.0), vec3(1.0));

	COLOR = vec4(col, 1.0);
}
