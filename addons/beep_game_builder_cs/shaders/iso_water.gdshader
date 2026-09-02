shader_type canvas_item;
render_mode blend_mix;

// The SAME sea as the flat renderer, drawn in the isometric (or top-down)
// projection.
//
// The water itself - depth, swell, surf, foam - lives in
// water_common.gdshaderinc, included by this shader and by
// terrain_splat.gdshader alike. It used to be a copy kept identical by hand;
// the include is the same contract enforced by the compiler, so the two views
// can never again drift into two different oceans.
//
// EXACTLY THREE things are added here, and none touches the water maths:
//
//   1. The projection, chosen by flat_projection. The flat shader's surface
//      covers the map one-to-one, so its local position IS the map. Drawn
//      isometrically the surface is a diamond, so the fragment's local
//      position is run back through the inverse isometric transform to
//      recover which tile it is over; drawn top-down it simply divides by
//      the cell size. Everything downstream then works in tiles, unchanged,
//      whichever way it got there.
//
//   2. Alpha from depth. The flat shader is opaque - it composites the sand
//      itself, in the same pass, and never needs to be seen through. Here the
//      seabed is real geometry on its own layers underneath, so the surface
//      has to actually be transparent for any of it to show. Shallow water is
//      clear and deep water is opaque, which is the standard model (Age of
//      Empires IV calls the distance at which water goes opaque "Deep Water
//      Depth") and is also simply what water does.
//
//   3. The sea running on under coastal land, for the isometric projection
//      only - an isometric block overhangs the cell below it, and a sea
//      masked off at the waterline leaves that strip undrawn.

#include "res://addons/beep_game_builder_cs/shaders/water_common.gdshaderinc"

// ADDITION 1 - the projection. Half the diamond a tile occupies, which is what
// turns a local position back into a tile coordinate.
uniform vec2 cell_size = vec2(111.0, 64.0);

// Which projection the surface is drawn in: 0 isometric, 1 top-down. The water
// is identical either way - only the step that recovers which tile a fragment
// is over differs - so both views share this file rather than each keeping a
// sea of its own to tune.
uniform float flat_projection = 0.0;

// How far the surface was shifted, in TILES, when it was filled.
//
// A tile layer cannot be filled from a negative cell without landing in a
// second rendering quadrant, and a second quadrant restarts VERTEX. So a sea
// that overscans the map is filled from the origin and the LAYER is moved
// instead; this is what converts the result back into map tiles.
uniform vec2 tile_offset = vec2(0.0);

// Where this fragment sits in the SURFACE's own coordinates, carried over from
// the vertex stage.
//
// It must not be the world position: that is wrong the moment anything between
// the renderer and the viewport scales, because the transform is folded into it
// and the shader cannot see the factor. The lab draws its preview inside a
// container that scales to fit the panel, and the coast field was sampled at
// the wrong tiles - the isometric view lost its beaches and shallows entirely
// and left a patch of surf stranded in open water. The standalone demos have no
// such parent, which is exactly why they looked right and hid it.
//
// VERTEX in vertex() is documented as local space in PIXELS relative to the
// node's origin, which is precisely the quantity wanted, and it is unaffected
// by any parent transform. In fragment() the same name means something else
// entirely - screen space - so it has to travel as a varying.
varying vec2 surface_local;

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

void vertex() {
	// The surface's own pixel position, for the fragment stage. There is exactly
	// one vertex() in a shader: adding a second one beside this compiled to
	// "Redefinition of 'vertex'", and a shader that fails to compile is not
	// skipped - the item draws UNSHADED, so the sea came out as a flat white
	// field of blank tiles rather than as nothing at all.
	surface_local = VERTEX;
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
	// Where this fragment is in the surface's own space, free of any parent
	// transform. See surface_local.
	vec2 local_pos = surface_local;

	vec2 tile;
	if (flat_projection > 0.5) {
		// Top-down: local pixels ARE the map, so dividing by the cell size gives
		// the tile directly. A square TileMapLayer puts cell (0,0) across
		// 0..cell_size, so no half-cell correction belongs here - map_to_local
		// reports a cell's CENTRE, which is a different question.
		tile = local_pos / max(cell_size, vec2(1.0));
	} else {
		float ex = (local_pos.x - half_cell.x) / half_cell.x;
		float ey = local_pos.y / half_cell.y;
		tile = vec2((ex + ey) * 0.5, (ey - ex) * 0.5);
	}
	tile -= tile_offset;
	vec2 UVm = tile / map_size;

	float t = TIME * wave_speed;

	// Past the edge of the map there is no coastline, only open sea. This has
	// to be said explicitly: the coast map is repeat_disable, so sampling
	// outside 0..1 CLAMPS to its border texels and smears them outward - which
	// drew four pale streaks running off the map's four edges in a great X
	// across the sea. sea_state() takes it from here.
	vec2 inside = step(vec2(0.0), tile) * step(tile, map_size);
	float in_map = inside.x * inside.y;

	// The whole sea - depth, swell, surf, foam - in one shared pass.
	SeaState sea = sea_state(tile, UVm, in_map, t);
	vec3 water_col = mix(sea.colour, vec3(0.96, 0.99, 1.0), sea.foam);

	// ADDITION 2. Clear in the shallows so the bed shows, closing to opaque as
	// it deepens. Foam is surface, so it stays visible whatever is beneath.
	float body_opacity = mix(lake_opacity, max_opacity, sea.open_sea);
	float depth_alpha = mix(
		shore_opacity, body_opacity, smoothstep(0.0, max(0.001, clarity_tiles), sea.sd));

	// ADDITION 3. Drawn TOP-DOWN the sea shares one plane with the land, so it
	// has to stop at the shore. Isometrically it deliberately does not: there
	// the land is opaque geometry drawn above the water, and letting the sea run
	// on underneath is what fills the strip a coastal block would otherwise
	// leave bare. Same field, same shoreline, different compositing.
	float on_water = mix(1.0, smoothstep(-0.35, 0.15, sea.sd), flat_projection);

	COLOR = vec4(water_col, max(depth_alpha, sea.foam) * on_water);
}
