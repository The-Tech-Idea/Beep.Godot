extends Sprite2D

@export_file("*.png") var source_path := ""

func _ready() -> void:
	if source_path.is_empty():
		return

	var disk_path := ProjectSettings.globalize_path(source_path)
	var image := Image.load_from_file(disk_path)
	if image == null or image.is_empty():
		push_warning("KenneyMapDetailSprite could not load " + source_path)
		return

	texture = ImageTexture.create_from_image(image)
