# Natural Landforms 1

Three new grass/granite terrain designs, generated with the built-in image generation tool. Existing mountain, hill, and headland packs are unchanged.

## Assets
- canyon_pair.tscn: two independently movable banks. Each bank also has its own canyon_left.tscn or canyon_right.tscn, using half of canyon_banks.png through AtlasTexture.
- cave_entrance.tscn: a front-facing natural rock opening with an opaque dark interior baked into the terrain sprite.
- elongated_hill.tscn: a low elongated grassy mound with gently sloping edges.

PNG exports have real alpha transparency. Scenes use native image scale and approximate bottom-center ground origins. Scale a whole prefab uniformly as needed; the images do not have calibrated common world-unit dimensions.

The canyon gap is adjustable by moving its two child scene instances. Banks are independent landforms, not seamless tiles. No automatic snapping, measured elevation sockets, collision, navigation, or gameplay triggers are supplied. The cave is one artwork layer, not a separate foreground arch/interior pack.

No ramps, roads, buildings, trees, water, or surrounding ground are baked in. Grass and the natural rocky transition are part of the terrain material.

## Validation
Image alpha and scene resource paths were checked. Godot runtime import and gameplay placement have not been tested. Camera and material consistency are visual art targets, not a calibrated 3D projection.

See prompts.json for generation prompts and source provenance.
