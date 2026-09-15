extends SceneTree

const CONTACTS = "res://addons/beep_game_builder_cs/generated/dev/cartoon/elevation/waterfall_contacts_v1/"
const ROUTES = "res://addons/beep_game_builder_cs/generated/dev/cartoon/water/depth_waterfall_route_v1/"
const OUTPUT = "res://addons/beep_game_builder_cs/generated/test/library/output/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	DirAccess.make_dir_recursive_absolute(CONTACTS + "staging")
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	var records: Array = []
	for width in [1, 2, 3, 5]:
		var source_path = ROUTES + "square/width_%d.tscn" % width
		var source_hash = FileAccess.get_sha256(source_path)
		var scene = load(source_path).instantiate()
		var first = scene.get_node("Fall0") as Sprite2D
		var last = scene.get_node("Fall%d" % (width - 1)) as Sprite2D
		# The channel's land/bank split is x=10 and x=54; the curtain drops y=64..128.
		var left = first.position + Vector2(10, 64)
		var right = last.position + Vector2(54, 64)
		var contacts = Node2D.new()
		contacts.name = "CandidateGraniteContacts"
		contacts.z_index = 4
		scene.add_child(contacts)
		contacts.owner = scene
		for side in ["left", "right"]:
			for role in ["wall", "lip", "bottom"]:
				var id = side + "_" + role + "_contact"
				var contact = load(CONTACTS + "godot/" + id + ".tscn").instantiate()
				contact.position = left if side == "left" else right
				if role == "bottom": contact.position.y += 64
				contacts.add_child(contact)
				contact.owner = scene
		scene.set_meta("contact_review_status", "unapproved_candidate")
		var packed = PackedScene.new()
		var destination = CONTACTS + "staging/route_width_%d.tscn" % width
		if packed.pack(scene) != OK or ResourceSaver.save(packed, destination) != OK:
			push_error("Contact review save failed")
			quit(1)
			return
		scene.free()
		var reopened = (ResourceLoader.load(destination, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE) as PackedScene).instantiate()
		if reopened.get_node("CandidateGraniteContacts").get_child_count() != 6 or FileAccess.get_sha256(source_path) != source_hash:
			push_error("Contact review changed source or lost contacts")
			quit(1)
			return
		var viewport = SubViewport.new()
		viewport.size = Vector2i(1100, 800)
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.add_child(reopened)
		reopened.position = Vector2(160, 128)
		for frame in range(4): await process_frame
		await RenderingServer.frame_post_draw
		var capture = OUTPUT + "contact_route_width_%d.png" % width
		if viewport.get_texture().get_image().save_png(capture) != OK:
			quit(1)
			return
		records.append({"width":width, "scene":destination, "source":source_path, "sourceSha256":source_hash, "saveReopen":true, "capture":capture, "approvalEvidence":null})
		viewport.queue_free()
		await process_frame
	var file = FileAccess.open(CONTACTS + "staging/routes_manifest.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"status":"visual_review_candidate", "cases":records, "pending":["visual_joins", "animated_contact_review", "isometric_contacts", "production_approval"]}, "  "))
	print("CONTACT ROUTE REVIEW PASSED: four saved candidates; existing routes unchanged")
	quit(0)
