@tool
extends Node2D

const DIRS := [Vector2i.RIGHT, Vector2i.DOWN, Vector2i.LEFT, Vector2i.UP,
    Vector2i(1, -1), Vector2i(1, 1), Vector2i(-1, 1), Vector2i(-1, -1)]
const SHAPES := [
    ["########", "########", "########", "########", "########"],
    ["###.....", "###.....", "###.....", "########", "########"],
    ["########", "########", "##....##", "##....##", "##....##", "########", "########"],
]

@export_range(1, 4, 1) var cliff_rows: int = 1:
    set(value):
        cliff_rows = clampi(value, 1, 4)
        if is_inside_tree():
            schedule_wall_update()

var wall_update_pending := false
var occupancy_hashes := {}
var poll_elapsed := 0.0

func _process(delta: float) -> void:
    poll_elapsed += delta
    if poll_elapsed < 0.15:
        return
    poll_elapsed = 0.0
    # TileMapLayer.changed does not report every painted cell edit. Poll
    # occupancy, not terrain artwork, to also handle editor brush and erase.
    for group in get_children():
        var tops := group.get_node_or_null("PaintableTops") as TileMapLayer
        if tops == null:
            continue
        var current: int = tops.get_used_cells().hash()
        if occupancy_hashes.get(group.name, -1) != current:
            occupancy_hashes[group.name] = current
            schedule_wall_update()

func _ready() -> void:
    if get_child_count() == 0:
        build_examples()
    for group in get_children():
        var tops := group.get_node_or_null("PaintableTops") as TileMapLayer
        if tops != null and not tops.changed.is_connected(schedule_wall_update):
            tops.changed.connect(schedule_wall_update)

func schedule_wall_update() -> void:
    if wall_update_pending:
        return
    wall_update_pending = true
    call_deferred("update_all_walls")

func update_all_walls() -> void:
    wall_update_pending = false
    for group in get_children():
        var tops := group.get_node_or_null("PaintableTops") as TileMapLayer
        var walls := group.get_node_or_null("CliffWalls") as TileMapLayer
        if tops != null and walls != null:
            rebuild_walls(tops, walls, cliff_rows)

func build_examples() -> void:
    var tiles: TileSet = load(get_script().resource_path.get_base_dir().path_join("grass_granite_tileset.tres"))
    for index in range(SHAPES.size()):
        var group := Node2D.new()
        group.name = ["Rectangle", "LShape", "Courtyard"][index]
        group.position = Vector2(50 + index * 580, 90)
        add_child(group)
        group.owner = self
        var wall := TileMapLayer.new()
        wall.name = "CliffWalls"
        wall.tile_set = tiles
        wall.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
        wall.position.y = -8
        group.add_child(wall)
        wall.owner = self
        var floor_layer := TileMapLayer.new()
        floor_layer.name = "PaintableTops"
        floor_layer.tile_set = tiles
        floor_layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
        group.add_child(floor_layer)
        floor_layer.owner = self
        var cells: Array[Vector2i] = []
        for y in range(SHAPES[index].size()):
            for x in range(SHAPES[index][y].length()):
                if SHAPES[index][y][x] == "#":
                    cells.append(Vector2i(x, y))
        floor_layer.set_cells_terrain_connect(cells, 0, 0, false)
        rebuild_walls(floor_layer, wall, cliff_rows)

static func rebuild_walls(tops: TileMapLayer, walls: TileMapLayer, height: int) -> void:
    walls.clear()
    var occupied := {}
    for cell in tops.get_used_cells():
        occupied[cell] = true
    for cell in occupied:
        if occupied.has(cell + Vector2i.DOWN):
            continue
        var left := occupied.has(cell + Vector2i.LEFT) and not occupied.has(cell + Vector2i(-1, 1))
        var right := occupied.has(cell + Vector2i.RIGHT) and not occupied.has(cell + Vector2i(1, 1))
        var cap := 0
        if not left and not right:
            cap = 3
        elif not left:
            cap = 1
        elif not right:
            cap = 2
        var column := posmod(int(cell.x), 6) * 4 + cap
        for depth in range(1, height + 1):
            var target: Vector2i = cell + Vector2i(0, depth)
            if occupied.has(target):
                break
            walls.set_cell(target, 1, Vector2i(column, 1 if depth == height else 0))

static func expected_mask(cell: Vector2i, occupied: Dictionary) -> int:
    var mask := 0
    for bit in range(8):
        if occupied.has(cell + DIRS[bit]):
            mask |= 1 << bit
    for triple in [[4,0,3], [5,0,1], [6,1,2], [7,2,3]]:
        if not (mask & (1 << triple[1]) and mask & (1 << triple[2])):
            mask &= ~(1 << triple[0])
    return mask
