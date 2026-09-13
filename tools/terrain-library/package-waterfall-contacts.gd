extends SceneTree

const BASE = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/waterfall_contacts_v1/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(BASE + "manifest.json"))
	if not manifest is Dictionary or manifest.get("productionReady", true):
		push_error("Expected development contact manifest")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(BASE + "godot")
	var resources: Array = []
	for piece in manifest.pieces:
		var image = Image.load_from_file(BASE + piece.runtime)
		var size = Vector2i(int(piece.runtimeBounds[0]), int(piece.runtimeBounds[1]))
		if image == null or image.get_size() != size:
			push_error("Contact image dimensions differ: " + piece.id)
			quit(1)
			return
		var texture = ImageTexture.create_from_image(image)
		var texture_path = BASE + "godot/" + piece.id + ".res"
		if ResourceSaver.save(texture, texture_path) != OK:
			quit(1)
			return
		var scene = Node2D.new()
		scene.name = piece.id.to_pascal_case()
		scene.set_meta("status", "calibration_candidate")
		scene.set_meta("visual_rise_pixels", piece.risePixels)
		scene.set_meta("water_side", piece.waterSide)
		scene.set_meta("source_sha256", manifest.sourceSha256)
		var sprite = Sprite2D.new()
		sprite.name = "Artwork"
		sprite.centered = false
		sprite.position = -Vector2(piece.pivot[0], piece.pivot[1])
		sprite.texture = load(texture_path)
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		scene.add_child(sprite)
		sprite.owner = scene
		var packed = PackedScene.new()
		var scene_path = BASE + "godot/" + piece.id + ".tscn"
		if packed.pack(scene) != OK or ResourceSaver.save(packed, scene_path) != OK:
			quit(1)
			return
		var reopened = (ResourceLoader.load(scene_path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE) as PackedScene).instantiate()
		var artwork = reopened.get_node("Artwork") as Sprite2D
		if artwork.position != sprite.position or artwork.texture.get_size() != Vector2(size) or reopened.get_child_count() != 1:
			push_error("Contact save/reopen failed: " + piece.id)
			quit(1)
			return
		resources.append({"id": piece.id, "scene": "godot/" + piece.id + ".tscn", "texture": "godot/" + piece.id + ".res", "saveReopen": true})
		reopened.free()
		scene.free()
	var file = FileAccess.open(BASE + "godot/manifest.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"calibration_candidate", "resources":resources, "approvalEvidence":null, "pending":["visual_contact_review", "route_integration", "isometric_artwork"]}, "  "))
	print("WATERFALL CONTACT PACKAGING PASSED: ", resources.size(), " anchored scenes; no collision or route changes")
	quit(0)
