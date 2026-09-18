extends "res://tests/terrain_lab_build.gd"

# Every art style the lab offers is a style in its own right.
#
# The lab's view menu is four projections and then one entry per TerrainMapArt in
# TerrainLabComponent.StyleProfiles. This checks the whole chain for each of them: the menu names it,
# selecting it puts that exact profile on the world, the profile carries its OWN prop art, and the
# props actually drew. Two styles sharing a sheet fails - that is what falling back to the renderer's
# default sheet looks like, and it is how a style can appear to work while drawing another's art.
#
# It also pins the menu's shape, because both halves used to be owned twice: the six item names were
# typed into terrain_generator_lab.tscn as well as built in code, and `Fill` only added items to an
# EMPTY chooser - so profiles added to StyleProfiles were silently missing from the menu, with the
# scene's authored list on screen instead and nothing reporting it.

const VIEW := "HUD/Settings/Scroll/Controls/ViewRow/View"
const PROJECTIONS := 4

func _initialize() -> void:
	call_deferred("run")

func sheet_of(texture: Texture2D) -> String:
	return str(texture.atlas.resource_path) if texture is AtlasTexture else str(texture.resource_path)

func run() -> void:
	var lab: Node = load("res://addons/beep_game_builder_cs/templates/scenes/terrain/terrain_generator_lab.tscn").instantiate()
	var world: Node = lab.get_node("World")
	world.set("MapSize", 0)
	root.add_child(lab)
	var build := await await_lab_build(world)
	assert(build.success, "the lab's first build did not succeed: %s" % build.message)

	var controller: Node = lab
	var profiles = controller.get("StyleProfiles")
	assert(profiles != null, "the lab controller has no StyleProfiles list")
	var picker: OptionButton = lab.get_node(VIEW)
	var names: Array[String] = []
	for i in picker.item_count:
		names.append(picker.get_item_text(i))
	print("[terrain-style-profiles] menu: %s" % ", ".join(names))
	assert(picker.item_count == PROJECTIONS + profiles.size(),
		"the menu lists %d entries for %d projections and %d styles" % [picker.item_count, PROJECTIONS, profiles.size()])
	assert(profiles.size() > 0, "no art styles are configured")
	# A name used twice is unreadable in the menu - "Isometric" the projection against "Isometric"
	# the art style, which is exactly what the first pass shipped.
	var unique := {}
	for name in names:
		assert(not unique.has(name), "the view menu lists '%s' twice" % name)
		unique[name] = true

	var features: Node = lab.get_node("Preview/Features")
	var rocks: Node = lab.get_node("Preview/RockObjects")
	var sheets := {}
	for index in range(PROJECTIONS, picker.item_count):
		picker.select(index)
		picker.item_selected.emit(index)
		for i in range(3):
			await process_frame
		var style: String = names[index]
		var art = world.get("MapArt")
		assert(art != null, "%s put no art on the world" % style)
		assert(art == profiles[index - PROJECTIONS], "%s selected a different profile" % style)
		assert(str(art.get("DisplayName")) == style, "%s is named %s by its profile" % [style, str(art.get("DisplayName"))])

		var trees = art.get("Trees")
		var bushes = art.get("Bushes")
		var small_rocks = art.get("SmallRocks")
		var large_rocks = art.get("LargeRocks")
		for named in [["trees", trees], ["bushes", bushes], ["small rocks", small_rocks], ["large rocks", large_rocks]]:
			assert(named[1].size() > 0, "%s has no %s of its own" % [style, named[0]])
		var stamps = features.get("StampCount")
		assert(typeof(stamps) == TYPE_INT and stamps > 0, "%s drew no props (%s)" % [style, str(stamps)])
		assert(rocks.call("GetStampBounds").size() > 0, "%s drew no relief stamps" % style)

		# No two styles may draw from one sheet: that is what a silent fallback looks like.
		var key := sheet_of(trees[0])
		assert(not sheets.has(key), "%s draws the same tree sheet as %s" % [style, sheets.get(key, "")])
		sheets[key] = style
		print("[terrain-style-profiles] %s: %d trees, %d bushes, %d+%d rocks, %d stamps, %s"
			% [style, trees.size(), bushes.size(), small_rocks.size(), large_rocks.size(), stamps, key.get_file()])

	lab.free()
	print("[terrain-style-profiles] OK")
	quit(0)
