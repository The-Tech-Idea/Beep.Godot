// The ONE sea, shared by every water surface in the addon.
//
// Two shaders draw water and they composite it differently: the painted view
// (terrain_splat.gdshader) is the whole ground in one opaque pass and mixes
// the sea against its own land, while the isometric/top-down water
// (iso_water.gdshader) is a transparent surface floating over real seabed
// geometry. The WATER ITSELF must be identical in both, or the same map grows
// two different oceans. That identity used to be kept by copying the body
// between the files expression for expression; this include is the same
// contract enforced by the compiler instead of by discipline.
//
// Everything here works in TILES against the coast distance field
// (TerrainCoastField): a caller recovers "which tile is this fragment over"
// in whatever projection it draws, and from there the sea is the same sea.

// Signed distance to the coastline in tiles, encoded 0..1 around 0.5.
// Beach and surf are specified in TILES against this, because a land/water
// fraction ramps across barely a pixel and any band expressed in it vanishes.
uniform sampler2D coast_map : filter_linear, repeat_disable;
uniform float coast_range = 5.0;
uniform vec2 map_size = vec2(96.0, 60.0);

// How many tiles one repeat of a material texture covers. Larger means the
// texture detail is bigger relative to a tile.
uniform float texture_tiles = 6.0;

uniform sampler2D tex_sand : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_shallow : repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_deep : repeat_enable, filter_linear_mipmap;
uniform vec3 tint_sand = vec3(0.98, 0.94, 0.82);
uniform vec3 tint_shallow = vec3(0.62, 0.80, 0.88);
uniform vec3 tint_deep = vec3(0.44, 0.58, 0.78);

uniform float foam_strength : hint_range(0.0, 1.0) = 0.50;

// How heavy the sea is. ONE dial, because big waves are not simply brighter
// foam: they reach further up the beach, their crests are broader, and the
// swash surges further inland. Scaling those together is what reads as a heavy
// sea rather than as the same small waves turned up. 0 is a millpond, 1 an
// ordinary day, 2 a storm.
uniform float wave_intensity : hint_range(0.0, 2.0) = 1.0;

uniform float foam_tiles : hint_range(0.1, 6.0) = 0.95;
uniform float wave_speed : hint_range(0.0, 4.0) = 1.0;
uniform float wave_amount : hint_range(0.0, 0.06) = 0.022;

// The shelf of shallow water lying over the beach sand, in tiles, and how much
// of that seabed shows through it.
uniform float shallow_tiles : hint_range(0.0, 8.0) = 1.8;
uniform float shallow_sand : hint_range(0.0, 1.0) = 0.34;

// How far out the sea reaches full depth, in tiles.
uniform float deep_tiles : hint_range(0.5, 12.0) = 4.5;

// Authored surf. A seamless foam texture, sampled in SHORE SPACE: across it
// runs distance from the waterline, along it runs the coast itself. The shore
// tangent is taken from the gradient of the distance field, which is what lets
// authored crests follow a curving coast instead of lying along the world axes.
//
// The texture needs a SOFT fringe. A flat cutout silhouette carries no falloff
// and collapses to one solid band however it is sampled.
uniform sampler2D foam_sheet : repeat_enable, filter_linear_mipmap;
uniform bool use_foam_sheet = false;
// One repeat of the foam texture covers this many tiles ALONG the shore. Long,
// so a coastline does not visibly repeat.
uniform float foam_tiles_along : hint_range(1.0, 48.0) = 11.0;
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

// The direction the swell TRAVELS, in degrees, y-down screen space. Waves
// come from somewhere: surf belongs to the shores facing them, and a lee
// shore keeps only what refraction wraps around. Without this every island
// wore its surf as a closed ring, which no real sea draws.
uniform float swell_direction_degrees : hint_range(0.0, 360.0) = 210.0;
// 0 restores surf on every shore alike; 1 leaves lee shores nearly flat.
uniform float swell_directionality : hint_range(0.0, 1.0) = 0.65;

// Smooth value noise, shared by the sea and by the callers' own edges.
float hash(vec2 p) {
	// NOT fract(sin(dot(p, ...)) * 43758.0). That loses precision once the
	// coordinate is large - and the coordinate here is tile position times a
	// frequency, so it reaches the hundreds - which makes neighbouring lattice
	// cells hash to near-identical values and shows up as flat rectangular
	// patches in the open water, exactly the tile-grid look this sea exists
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

// Everything a caller needs back from the sea, in one pass. `colour` is the
// water body BEFORE foam; `foam` is the white mix factor, kept separate
// because the isometric surface also feeds it into its alpha. `sd`,
// `open_sea`, `facing` and `patch` come back because the painted view's LAND
// effects - shore composite, beach, wet sand, swash - measure against the
// same wobbled waterline and sea state, and recomputing them would be the
// two-coastlines defect all over again.
struct SeaState {
	vec3 colour;
	float foam;
	float sd;
	float open_sea;
	float facing;
	float patch;
};

// The water, in tiles. `uv` is the map-wide UV (tile / map_size); `in_map` is
// 1 inside the map and 0 beyond it - a surface that never overscans passes a
// constant 1.0 and the mixes below collapse to the plain field reads. Beyond
// the map there is no coastline, only open sea, and it has to be said
// explicitly: the coast map clamps at its border texels and smears them
// outward otherwise.
SeaState sea_state(vec2 tile, vec2 uv, float in_map, float t) {
	SeaState sea;

	// The shore frame, from the gradient of the distance field: which way is
	// out to sea, and which way the coast runs. Everything that MOVES on the
	// water is expressed in this frame, because a drift written in world axes
	// runs out to sea on whichever coasts happen to face that way.
	vec2 shore_texel = 1.0 / map_size;
	vec2 shore_grad = vec2(
		texture(coast_map, uv + vec2(shore_texel.x, 0.0)).r - texture(coast_map, uv - vec2(shore_texel.x, 0.0)).r,
		texture(coast_map, uv + vec2(0.0, shore_texel.y)).r - texture(coast_map, uv - vec2(0.0, shore_texel.y)).r);
	float shore_glen = length(shore_grad);
	// A flat gradient is open water far from any coast, where none of this is
	// visible; any direction will do rather than normalising zero.
	vec2 seaward = shore_glen > 1e-6 ? shore_grad / shore_glen : vec2(0.0, 1.0);
	vec2 along_shore = vec2(-seaward.y, seaward.x);

	// How exposed this stretch of coast is to the incoming swell: 1 facing it
	// head on, falling toward the lee. Everything the sea throws at the shore
	// - surf, swash, the arriving roll - scales by this.
	float swell_rad = radians(swell_direction_degrees);
	vec2 swell_travel = vec2(cos(swell_rad), sin(swell_rad));
	float exposure = clamp(-dot(seaward, swell_travel), 0.0, 1.0);
	sea.facing = mix(1.0, 0.20 + (0.80 * exposure), swell_directionality);

	// Material is sampled from world position, so it runs continuously across
	// tiles with no visible repeat seam. The water textures get a slow
	// oscillating warp on world axes so the surface is alive; translating
	// these coordinates by a shore-relative vector was a mistake - that
	// direction converges from every side inside a lake, and a per-pixel
	// varying translation SHEARS the texture into marbled smears.
	vec2 world_uv = tile / texture_tiles;
	vec2 water_uv = world_uv + vec2(
		sin((world_uv.y * 7.0) + (t * 0.7)),
		cos((world_uv.x * 6.0) + (t * 0.5))) * wave_amount;

	// Signed distance to the waterline, in tiles: positive out to sea. Green
	// marks open sea - surf needs a fetch behind it, so a lake or a river
	// stays flat however close to the shore it is. Outside the map it is all
	// open sea, and safely so: sd saturates there, which puts the surf band's
	// falloff at zero.
	vec4 coast = texture(coast_map, uv);
	float sd = mix(coast_range, (coast.r - 0.5) * 2.0 * coast_range, in_map);
	float open_sea = mix(1.0, smoothstep(0.35, 0.75, coast.g), in_map);
	// The field is one value per sub-tile, so its contours are still faceted
	// and surf drawn on them looks like survey lines. Two octaves of wobble
	// break that up: a broad one to meander the whole band, a finer one to
	// roughen each crest.
	sd += (value_noise(tile * 1.1) - 0.5) * 0.30;
	sd += (value_noise((tile * 3.4) + vec2(5.3, 9.1)) - 0.5) * 0.18;
	sea.sd = sd;
	sea.open_sea = open_sea;

	// Depth from the same distance field: shallow against the shore, shelving
	// to deep offshore. Taking it from tile ids instead is what tiled the sea.
	vec3 water_col = mix(
		texture(tex_shallow, water_uv).rgb * tint_shallow,
		texture(tex_deep, water_uv).rgb * tint_deep,
		smoothstep(0.0, max(0.001, deep_tiles), sd));

	// Shallow water is water OVER sand, so the bottom shows through near the
	// shore. Sampled on world_uv, not the drifting water_uv: a seabed that
	// swims with the surface reads as fog, not as depth.
	float shallow = 1.0 - smoothstep(0.0, max(0.001, shallow_tiles), sd);
	water_col = mix(water_col, texture(tex_sand, world_uv).rgb * tint_sand, shallow * shallow_sand);

	// Swell, in two parts, because "which way is the sea moving" has two
	// honest answers. Near the coast it moves TOWARD the beach: a luminance
	// wave travelling down the distance field, so its fronts lie along the
	// shore and always arrive, never depart. A version that scrolled one
	// noise along a fixed world vector made every coast facing against it
	// watch the sea run backwards. Where the field saturates there is no
	// "toward" left to express, so the roll hands over to a driftless
	// shimmer: two static patterns crossfaded, movement with no net
	// direction.
	float shimmer = mix(
		value_noise((tile * 1.4) + vec2(3.1, 7.7)),
		value_noise((tile * 1.4) + vec2(9.2, 1.3)),
		0.5 + (0.5 * sin(t * 0.5)));
	float roll = sin((sd * 2.6) + (t * 1.1) + (value_noise(tile * 0.9) * 2.2));
	// Confined to the surf zone, and only on water with a fetch. Let out to
	// several tiles it drew faint concentric rings around every island - and
	// rings pulsing around a shore read as radiating OUT of it, the
	// stone-in-a-pond look, whichever way their phase actually runs.
	float nearing = (1.0 - smoothstep(0.3, 1.6, sd)) * open_sea * sea.facing;
	water_col *= (0.95 + (0.10 * shimmer)) + (0.035 * roll * nearing);
	sea.colour = water_col;

	// Surf, as a band measured out from the waterline rather than around a
	// tile. A heavier sea throws its surf further out.
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
	sea.patch = patch;

	float lines;
	if (use_foam_sheet) {
		float along_coord = dot(tile, along_shore);
		// PLUS t on the across coordinate. V grows OUT to sea, so adding time
		// moves a fixed texture feature to smaller sd - onto the beach. The
		// original minus scrolled every authored crest out to sea, and was
		// invisible only because no shipped scene assigned a sheet.
		lines = texture(foam_sheet, vec2(
			along_coord / max(0.001, foam_tiles_along),
			(sd / max(0.001, foam_tiles_across)) + (t * foam_scroll))).a;

		// The texture supplies foam SHAPE; this supplies its ARRIVAL. Real
		// shore break pulses - a crest sweeps in, breaks and drains - and a
		// band of even brightness sitting on the coast is the single thing
		// that most makes a shoreline read as a decal stuck to the map.
		//
		// The phase drifts ALONG the shore as well, because sets do not
		// arrive down a whole coastline at once; without it the entire island
		// pulses white in unison, which is its own kind of wrong.
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
		// integer part of the phase is WHICH wave, and the shaping below
		// keys off it so each wave arrives in its own segments at its own
		// moment instead of the coast pulsing as concentric rings.
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
		// overall fade stays with the falloff below; this only moves the
		// PEAK off the waterline and out to the break depth.
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
	sea.foam = clamp(lines * falloff * patch * foam_strength * open_sea * sea.facing
		* clamp(wave_intensity, 0.0, 1.4) * smoothstep(-0.12, 0.06, sd), 0.0, 1.0);

	return sea;
}
