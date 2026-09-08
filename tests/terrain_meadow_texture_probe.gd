extends SceneTree

const ART := "res://addons/beep_game_builder_cs/textures/terrain/"

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	for pair in [["grass.png", "meadow_ground.png"], ["dry_grass.png", "dry_meadow_ground.png"]]:
		var old := Image.load_from_file(ProjectSettings.globalize_path(ART + pair[0]))
		var meadow := Image.load_from_file(ProjectSettings.globalize_path(ART + pair[1]))
		assert(not old.is_empty() and not meadow.is_empty())
		assert(meadow.get_width() >= 1024 and meadow.get_width() == meadow.get_height())
		var imported: Texture2D = load(ART + pair[1])
		assert(imported != null and imported.get_image().has_mipmaps(), "Meadow import has no mip chain")
		assert(meadow.detect_alpha() == Image.ALPHA_NONE, "Meadow contains transparency")
		var old_stats := metrics(old)
		var new_stats := metrics(meadow)
		print("[terrain-meadow-texture] ", pair[1], " old=", old_stats, " new=", new_stats)
		assert(new_stats.macro_deviation < old_stats.macro_deviation * 0.65,
			"Replacement did not reduce broad brightness mottling")
		assert(new_stats.seam_x < 0.04 and new_stats.seam_y < 0.04,
			"Opposite edges have a large average colour mismatch")
	print("[terrain-meadow-texture] OK")
	quit()

func luminance(c: Color) -> float:
	return c.r * 0.299 + c.g * 0.587 + c.b * 0.114

func difference(a: Color, b: Color) -> float:
	return (absf(a.r - b.r) + absf(a.g - b.g) + absf(a.b - b.b)) / 3.0

func metrics(image: Image) -> Dictionary:
	var means: Array[float] = []
	var mean := 0.0
	var size := image.get_width()
	for by in 8:
		for bx in 8:
			var sum := 0.0
			var count := 0
			for y in range(by * size / 8, (by + 1) * size / 8, 3):
				for x in range(bx * size / 8, (bx + 1) * size / 8, 3):
					var pixel := image.get_pixel(x, y)
					assert(pixel.a == 1.0, "Ground albedo contains transparency")
					sum += luminance(pixel)
					count += 1
			means.append(sum / count)
			mean += sum / count / 64.0
	var variance := 0.0
	for block_mean in means:
		variance += pow(block_mean - mean, 2.0) / 64.0
	var seam_x := 0.0
	var seam_y := 0.0
	for i in size:
		seam_x += difference(image.get_pixel(0, i), image.get_pixel(size - 1, i)) / size
		seam_y += difference(image.get_pixel(i, 0), image.get_pixel(i, size - 1)) / size
	return {"mean": mean, "macro_deviation": sqrt(variance), "seam_x": seam_x, "seam_y": seam_y}
