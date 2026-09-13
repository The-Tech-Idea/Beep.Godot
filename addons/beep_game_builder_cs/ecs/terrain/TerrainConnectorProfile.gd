@tool
extends Resource

@export var stable_id: String = ""
@export_enum("Square", "Isometric") var projection: int = 0
@export_enum("North", "East", "South", "West") var direction: int = 0
@export var material_role: String = "shallow_water"
@export var opening_width_pixels: float = 0.0
@export var elevation_pixels: int = 0
@export_enum("Static", "Inlet", "Outlet") var flow_role: int = 0
@export_enum("Static", "Lake", "River", "Waterfall", "Sea") var motion_role: int = 0
@export var bank_profile: String = ""
@export var frame_count: int = 1
@export var period_seconds: float = 0.0
@export var phase_group: String = ""
@export var tiles: TileSet
@export var source_id: int = 0
@export var atlas_coords: Vector2i = Vector2i.ZERO

func validate() -> PackedStringArray:
	var issues = PackedStringArray()
	if stable_id.is_empty(): issues.append("Connector has no stable ID.")
	if projection < 0 or projection > 1: issues.append("Unsupported projection.")
	if direction < 0 or direction > 3: issues.append("Invalid logical direction.")
	if material_role.is_empty() or bank_profile.is_empty(): issues.append("Material/bank profile is missing.")
	if not is_finite(opening_width_pixels) or opening_width_pixels <= 0: issues.append("Opening width must be positive and finite.")
	if flow_role < 0 or flow_role > 2: issues.append("Invalid flow role.")
	if motion_role < 0 or motion_role > 4: issues.append("Invalid motion role.")
	if motion_role in [2,3] and flow_role == 0: issues.append("River/waterfall ports require an explicit inlet or outlet.")
	if motion_role != 0 and (frame_count != 16 or not is_equal_approx(period_seconds,1.2) or phase_group.is_empty()):
		issues.append("Animated connectors require a named phase group and 16-frame, 1.2-second loop.")
	if tiles == null or not tiles.has_source(source_id):
		issues.append("Connector artwork source is missing.")
		return issues
	var expected = TileSet.TILE_SHAPE_ISOMETRIC if projection == 1 else TileSet.TILE_SHAPE_SQUARE
	if tiles.tile_shape != expected: issues.append("Artwork projection is incompatible.")
	var atlas = tiles.get_source(source_id) as TileSetAtlasSource
	if atlas == null or not atlas.has_tile(atlas_coords):
		issues.append("Connector atlas region is missing.")
	elif motion_role != 0:
		if atlas.get_tile_animation_frames_count(atlas_coords) != frame_count:
			issues.append("Artwork frame count does not match the connector.")
		if period_seconds > 0 and not is_equal_approx(atlas.get_tile_animation_speed(atlas_coords),frame_count/period_seconds):
			issues.append("Artwork animation speed does not match the connector.")
	return issues

func connection_issues(other: Resource) -> PackedStringArray:
	var issues = validate()
	if other == null or other.get_script() != get_script():
		issues.append("Other connector profile is missing or unsupported.")
		return issues
	issues.append_array(other.validate())
	if not issues.is_empty(): return issues
	if projection != other.projection: issues.append("Projection mismatch.")
	if (direction+2)%4 != other.direction: issues.append("Ports do not face opposite logical directions.")
	if material_role != other.material_role: issues.append("Material transition artwork is required.")
	if bank_profile != other.bank_profile: issues.append("Bank transition artwork is required.")
	if not is_equal_approx(opening_width_pixels,other.opening_width_pixels): issues.append("Width transition artwork is required.")
	if elevation_pixels != other.elevation_pixels: issues.append("Elevation transition artwork is required.")
	if motion_role != other.motion_role: issues.append("Motion-role transition artwork is required.")
	if (flow_role == 0) != (other.flow_role == 0) or (flow_role != 0 and flow_role == other.flow_role):
		issues.append("Explicit inlet-to-outlet flow is required.")
	if frame_count != other.frame_count or not is_equal_approx(period_seconds,other.period_seconds) or phase_group != other.phase_group:
		issues.append("Animation phase contracts do not match.")
	return issues
