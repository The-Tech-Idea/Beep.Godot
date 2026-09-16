extends SceneTree

# The one wait for the terrain lab's world build. Lab probes extend this script; it is not a probe
# and does nothing when run on its own.
#
# The lab builds on a worker. Its _Ready defers Generate(), which starts
# TerrainWorldComponent.BeginNewWorld(), and the build then publishes over several frames - the
# cells, the painted snapshot, native collision. Until it finishes the renderers have not drawn
# (Preview/Splat/SplatSurface and Preview/Iso/IsoSeabed do not exist yet), BuiltSize can still be
# (0, 0), and the lab has disabled its own controls. A probe that reads the lab after a fixed number
# of frames therefore fails on whichever of those it touches first, with a message naming none of
# them: "node not found", "lab controls overwrote configured map size", "styles must be reachable
# from every view".
#
# Why the signal and not the flags: BuiltSize is set partway through publication, before the
# painted snapshot and collision are done, so "not generating, BuiltSize set" is also exactly what a
# build that failed late looks like. Only GenerationFinished says whether the build worked. The
# copies of this wait that polled IsGenerating alone would have passed a failed build.
#
# Tests that exercise TerrainWorldComponent's generation itself - checking invariants on every frame
# of a build, restarting from inside its handlers, freeing the world mid-build - keep their own
# loops: waiting is not what they test.

const LAB_BUILD_TIMEOUT_MSEC := 120000

## A build is started at the end of the frame the lab enters the tree, or synchronously by the lab's
## Generate() and its Generate button. Allow this many frames for it to show before calling it
## never started.
const LAB_BUILD_START_FRAMES := 3

## Waits for the build the lab has just started and says how it ended:
##   finished - GenerationFinished arrived
##   success  - it reported success
##   message  - its own message ("Complete", "Cancelled", the failure), or why it never finished
##   count    - how many GenerationFinished arrived before the wait ended; one build finishes once
##
## Call it straight after adding the lab to the tree, or straight after starting a build - before
## awaiting anything. A build cannot publish within the frame it starts in, so called there the
## wait cannot miss GenerationFinished. It asserts nothing: a probe that expects a build to fail or
## be cancelled reads the result the same way as one that expects success.
func await_lab_build(world: Node, timeout_msec: int = LAB_BUILD_TIMEOUT_MSEC) -> Dictionary:
	var result := {"finished": false, "success": false, "message": "", "count": 0}
	var record := func(success: bool, message: String) -> void:
		result["finished"] = true
		result["success"] = success
		result["message"] = message
		result["count"] += 1
	world.connect("GenerationFinished", record)
	var deadline := Time.get_ticks_msec() + timeout_msec
	var frames := 0
	var started: bool = world.IsGenerating
	while not result["finished"] and Time.get_ticks_msec() < deadline:
		if not started and frames >= LAB_BUILD_START_FRAMES:
			break
		await process_frame
		frames += 1
		started = started or world.IsGenerating
	world.disconnect("GenerationFinished", record)
	if not result["finished"]:
		result["message"] = ("the lab never started a build (%d frames, BuiltSize %s)" % [frames, world.BuiltSize]
			if not started else "the lab's build did not finish within %d ms" % timeout_msec)
	return result
