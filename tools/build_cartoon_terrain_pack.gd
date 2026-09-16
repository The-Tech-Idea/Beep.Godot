extends SceneTree

# Mechanical atlas extraction and Godot packaging; the generated source art is retained.
const ROOT := "res://addons/beep_game_builder_cs/generated/cartoon_terrain/grass_rock/"
const OUT := ROOT + "v3_connected/"
const TILE := 64
const GREEN := Color(0, 1, 0, 1)
const OFFSETS := [Vector2i(1,0), Vector2i(0,1), Vector2i(-1,0), Vector2i(0,-1), Vector2i(1,-1), Vector2i(1,1), Vector2i(-1,1), Vector2i(-1,-1)]
const BITS := [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER]
var masks: Array[int] = []
var parents: Dictionary = {}

func _initialize() -> void:
    call_deferred("run")

func check(condition: bool, message: String) -> void:
    if not condition:
        push_error(message)
        quit(1)
        assert(condition, message)

func canonical(mask: int) -> int:
    for rule in [[4,0,3], [5,0,1], [6,1,2], [7,2,3]]:
        if not (mask & (1 << rule[1]) and mask & (1 << rule[2])):
            mask &= ~(1 << rule[0])
    return mask

func run() -> void:
    for value in range(256):
        var mask := canonical(value)
        if mask not in masks:
            masks.append(mask)
    masks.sort()
    check(masks.size() == 47, "Expected 47 canonical terrain masks")
    if "--resources" in OS.get_cmdline_user_args():
        build_resources()
    elif "--capture" in OS.get_cmdline_user_args():
        await capture()
    else:
        pack_images()
    quit()

func extract(source: Image, rect: Rect2i, size: Vector2i) -> Image:
    check(Rect2i(Vector2i.ZERO, source.get_size()).encloses(rect), "Crop outside source")
    var image := source.get_region(rect)
    image.convert(Image.FORMAT_RGBA8)
    # Remove only chroma background for resampling; source PNGs are never rewritten.
    for y in range(image.get_height()):
        for x in range(image.get_width()):
            var c := image.get_pixel(x,y)
            var excess := c.g - maxf(c.r,c.b)
            c.a *= 1.0 - smoothstep(0.30,0.65,excess)
            if c.a < 0.999:
                c.g = minf(c.g, maxf(c.r,c.b))
            image.set_pixel(x,y,c)
    image.resize(size.x,size.y,Image.INTERPOLATE_LANCZOS)
    return image

func find_root(value: int) -> int:
    if not parents.has(value):
        parents[value] = value
    var root: int = value
    while parents[root] != root:
        root = parents[root]
    while parents[value] != value:
        var next: int = parents[value]
        parents[value] = root
        value = next
    return root

func seam_groups() -> Dictionary:
    var groups := {}
    for i in range(masks.size()):
        var mask: int = masks[i]
        var bits: Array[int] = []
        for b in range(8):
            bits.append(1 if mask & (1 << b) else 0)
        var sides := [
            [0,"v",[bits[3],bits[4],bits[1],bits[5]]],
            [2,"v",[bits[7],bits[3],bits[6],bits[1]]],
            [3,"h",[bits[7],bits[2],bits[4],bits[0]]],
            [1,"h",[bits[2],bits[6],bits[0],bits[5]]]]
        for side in sides:
            if not bits[side[0]]:
                continue
            for t in range(TILE):
                var point: Vector2i = {0:Vector2i(63,t),2:Vector2i(0,t),3:Vector2i(t,0),1:Vector2i(t,63)}[side[0]]
                var key := str(side[1],side[2],":",t)
                if not groups.has(key):
                    groups[key] = []
                groups[key].append(i * 4096 + point.y * 64 + point.x)
    return groups

func pixel_at(images: Array[Image], id: int) -> Color:
    return images[id / 4096].get_pixel(id % 64, (id % 4096) / 64)

func match_boundaries(images: Array[Image]) -> void:
    var groups := seam_groups()
    for members in groups.values():
        var first := find_root(members[0])
        for member in members:
            parents[find_root(member)] = first
    var merged := {}
    for member in parents.keys():
        var key := find_root(member)
        if not merged.has(key):
            merged[key] = []
        merged[key].append(member)
    for members in merged.values():
        var color := Color(0,0,0,0)
        var alpha := 0.0
        for member in members:
            var c := pixel_at(images,member)
            color += Color(c.r*c.a,c.g*c.a,c.b*c.a,0)
            alpha += c.a
        color /= maxf(alpha,0.0001)
        color.a = alpha / members.size()
        for member in members:
            images[member / 4096].set_pixel(member % 64,(member % 4096) / 64,color)
    for members in groups.values():
        var expected := pixel_at(images,members[0])
        for member in members:
            check(pixel_at(images,member) == expected,"Unmatched terrain boundary")

func save_green_atlas(images: Array[Image], columns: int, rows: int, name: String) -> void:
    var atlas := Image.create(columns*64,rows*64,false,Image.FORMAT_RGBA8)
    atlas.fill(GREEN)
    for i in range(images.size()):
        var tile := images[i].duplicate() as Image
        for y in range(tile.get_height()):
            for x in range(tile.get_width()):
                var c := tile.get_pixel(x,y)
                # Binary chroma keeps the runtime key clean and avoids green edge halos.
                if c.a < 0.5:
                    c = GREEN
                else:
                    c.a = 1.0
                tile.set_pixel(x,y,c)
        atlas.blit_rect(tile,Rect2i(Vector2i.ZERO,tile.get_size()),Vector2i(i%columns*64,i/columns*64))
    check(atlas.save_png(OUT+name) == OK,"Could not save atlas")

func pack_images() -> void:
    DirAccess.make_dir_recursive_absolute(OUT)
    var source := Image.load_from_file(ROOT+"v2/grass_ground_sheet_green.png")
    check(source.get_size() == Vector2i(1254,1254),"Ground source changed")
    var starts := [38,342,647,951]
    var tops: Array[Image] = []
    for row in range(4):
        for col in range(4):
            tops.append(extract(source,Rect2i(starts[col],starts[row]+(1 if row == 0 else 0),264,264),Vector2i(64,64)))
    var images: Array[Image] = []
    for mask in masks:
        var image := Image.create(64,64,false,Image.FORMAT_RGBA8)
        for q in [[0,0,2,3,7,8,12],[1,0,0,3,4,9,13],[1,1,0,1,5,10,14],[0,1,2,1,6,11,15]]:
            var x_inside: bool = bool(mask & (1 << q[2]))
            var y_inside: bool = bool(mask & (1 << q[3]))
            var index := 0
            if not x_inside and not y_inside:
                index = q[5]
            elif not x_inside:
                index = 7 if q[0] == 0 else 5
            elif not y_inside:
                index = 4 if q[1] == 0 else 6
            elif not mask & (1 << q[4]):
                index = q[6]
            var origin := Vector2i(q[0]*32,q[1]*32)
            image.blit_rect(tops[index],Rect2i(origin,Vector2i(32,32)),origin)
        images.append(image)
    match_boundaries(images)
    save_green_atlas(images,8,6,"ground_atlas_green.png")
    var cliffs := Image.load_from_file(ROOT+"v2/cliff_elevation_sheet_green.png")
    check(cliffs.get_size() == Vector2i(1536,1024),"Cliff source changed")
    var walls: Array[Image] = []
    # Crop measured rock faces; turf caps come from the ground layer above them.
    var wall_regions := [Rect2i(80,126,266,232),Rect2i(443,126,266,232),Rect2i(62,840,288,94),Rect2i(62,840,288,94),Rect2i(443,906,266,30),Rect2i(443,906,266,30)]
    for i in range(6):
        var height: int = [64,32,16][i/2]
        var wall := extract(cliffs,wall_regions[i],Vector2i(64,height))
        # Remove crop-edge background specks from solid rock faces only.
        for y in range(height):
            var seam := wall.get_pixel(2,y).lerp(wall.get_pixel(61,y),0.5)
            seam.a = 1
            wall.set_pixel(0,y,seam)
            wall.set_pixel(63,y,seam)
        walls.append(wall)
    # Both full-height variants share the same two boundary columns.
    for y in range(64):
        var edge := walls[0].get_pixel(0,y).lerp(walls[1].get_pixel(0,y),0.5)
        for wall in [walls[0],walls[1]]:
            wall.set_pixel(0,y,edge)
            wall.set_pixel(63,y,edge)
    save_green_atlas(walls,2,3,"walls_atlas_green.png")
    var entries: Array = []
    for i in range(masks.size()):
        entries.append({"id":"ground_mask_%03d" % masks[i],"source":0,"atlas":[i%8,i/8],"neighbor_mask":masks[i]})
    for i in range(6):
        entries.append({"id":"wall_%s_%s" % [["full","half","quarter"][i/2],["a","b"][i%2]],"source":1,"atlas":[i%2,i/2],"wall_height_px":[64,32,16][i/2]})
    var manifest := {"tile_size":64,"mask_order":["E","S","W","N","NE","SE","SW","NW"],"ground_tiles":47,"wall_tiles":6,"heights_px":[64,32,16],"entries":entries,"sources_preserved":true,"side_walls":"Example uses outward-extruded Polygon2D faces; ambiguous generated corner drawings are excluded.","collision":"Not configured; artwork and terrain connectivity only."}
    var file := FileAccess.open(OUT+"atlas_manifest.json",FileAccess.WRITE)
    file.store_string(JSON.stringify(manifest,"  "))
    print("PACK PASS: 47 ground masks, 6 wall regions, exact compatible ground boundaries")

func build_resources() -> void:
    var ground := load(OUT+"ground_atlas_green.png") as Texture2D
    var walls := load(OUT+"walls_atlas_green.png") as Texture2D
    check(ground != null and walls != null,"Run import between packing and resources")
    var tiles := TileSet.new()
    tiles.tile_size = Vector2i(64,64)
    tiles.add_terrain_set()
    tiles.set_terrain_set_mode(0,TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
    tiles.add_terrain(0)
    tiles.set_terrain_name(0,0,"Cartoon Grass")
    tiles.set_terrain_color(0,0,Color("829b61"))
    for name in ["piece_name","neighbor_mask","wall_height_px"]:
        var index := tiles.get_custom_data_layers_count()
        tiles.add_custom_data_layer()
        tiles.set_custom_data_layer_name(index,name)
        tiles.set_custom_data_layer_type(index,TYPE_STRING if index == 0 else TYPE_INT)
    var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(OUT+"atlas_manifest.json"))
    for source_id in range(2):
        var atlas := TileSetAtlasSource.new()
        atlas.texture = ground if source_id == 0 else walls
        atlas.texture_region_size = Vector2i(64,64)
        tiles.add_source(atlas,source_id)
        for entry in manifest.entries:
            if entry.source != source_id:
                continue
            var coords := Vector2i(entry.atlas[0],entry.atlas[1])
            atlas.create_tile(coords)
            var data := atlas.get_tile_data(coords,0)
            data.set_custom_data("piece_name",entry.id)
            data.set_custom_data("neighbor_mask",entry.get("neighbor_mask",-1))
            data.set_custom_data("wall_height_px",entry.get("wall_height_px",0))
            if source_id == 0:
                data.terrain_set = 0
                data.terrain = 0
                for b in range(8):
                    data.set_terrain_peering_bit(BITS[b],0 if int(entry.neighbor_mask) & (1 << b) else -1)
            var texture := AtlasTexture.new()
            texture.atlas = atlas.texture
            texture.region = Rect2(coords*64,Vector2i(64,int(entry.get("wall_height_px",64))))
            texture.filter_clip = true
            DirAccess.make_dir_recursive_absolute(OUT+"regions")
            check(ResourceSaver.save(texture,OUT+"regions/"+entry.id+".tres") == OK,"Region save failed")
    check(ResourceSaver.save(tiles,OUT+"cartoon_grass_rock.tres") == OK,"TileSet save failed")
    var scene := Node2D.new()
    scene.name = "CartoonTerrainExample"
    scene.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
    var shader := Shader.new()
    # Shaded: an unshaded terrain item skips the scene's CanvasModulate (day/night, weather).
    shader.code = "shader_type canvas_item; varying vec4 tint; void vertex(){tint=COLOR;} void fragment(){vec4 c=texture(TEXTURE,UV); float k=c.g-max(c.r,c.b); COLOR=vec4(c.rgb,c.a*(1.0-step(0.6,k)))*tint;}"
    check(ResourceSaver.save(shader,OUT+"green_key.gdshader") == OK,"Shader save failed")
    var material := ShaderMaterial.new()
    material.shader = shader
    check(ResourceSaver.save(material,OUT+"green_key.tres") == OK,"Material save failed")
    add_plateau(scene,tiles,material,Vector2(112,120),Vector2i(8,5),64,"FullHeight")
    add_plateau(scene,tiles,material,Vector2(800,152),Vector2i(4,3),32,"HalfHeight")
    add_plateau(scene,tiles,material,Vector2(800,456),Vector2i(4,3),16,"QuarterHeight")
    var packed := PackedScene.new()
    check(packed.pack(scene) == OK,"Scene packing failed")
    check(ResourceSaver.save(packed,OUT+"connected_plateau_example.tscn") == OK,"Scene save failed")
    scene.free()
    print("RESOURCES PASS: 53 named regions, TileSet terrain bits, example with 64/32/16px walls")

func face(parent: Node2D, name: String, points: PackedVector2Array, texture: Texture2D, height: int, tint: Color) -> void:
    var node := Polygon2D.new()
    node.name = name
    node.polygon = points
    node.texture = texture
    node.uv = PackedVector2Array([Vector2(0,0),Vector2(64,0),Vector2(64,height),Vector2(0,height)])
    node.color = tint
    parent.add_child(node)
    node.owner = parent.owner

func add_plateau(scene: Node2D, tiles: TileSet, material: ShaderMaterial, origin: Vector2, size: Vector2i, height: int, title: String) -> void:
    var parent := Node2D.new()
    parent.name = title
    parent.position = origin
    parent.material = material
    scene.add_child(parent)
    parent.owner = scene
    var tier: String = {64:"full",32:"half",16:"quarter"}[height]
    var wall := load(OUT+"regions/wall_"+tier+"_a.tres") as Texture2D
    var flare := float(height)*0.375
    var width := float(size.x*64)
    var depth := float(size.y*64)
    for y in range(size.y):
        var top := float(y*64)
        face(parent,"Left_%d"%y,PackedVector2Array([Vector2(0,top),Vector2(0,top+64),Vector2(-flare,top+64+height),Vector2(-flare,top+height)]),wall,height,Color(0.90,0.94,0.89))
        face(parent,"Right_%d"%y,PackedVector2Array([Vector2(width,top+64),Vector2(width,top),Vector2(width+flare,top+height),Vector2(width+flare,top+64+height)]),wall,height,Color(0.74,0.78,0.76))
    for x in range(size.x):
        var left := float(x*64)
        var texture := load(OUT+"regions/wall_"+tier+"_"+("a" if x%2 == 0 else "b")+".tres") as Texture2D
        face(parent,"Front_%d"%x,PackedVector2Array([Vector2(left,depth),Vector2(left+64,depth),Vector2(left+64+(flare if x == size.x-1 else 0.0),depth+height),Vector2(left-(flare if x == 0 else 0.0),depth+height)]),texture,height,Color.WHITE)
    var top := TileMapLayer.new()
    top.name = "Ground"
    top.tile_set = tiles
    top.position = Vector2.ZERO
    top.material = material
    parent.add_child(top)
    top.owner = scene
    for y in range(size.y):
        for x in range(size.x):
            var mask := 0
            for b in range(8):
                if Rect2i(Vector2i.ZERO,size).has_point(Vector2i(x,y)+OFFSETS[b]):
                    mask |= 1 << b
            var index := masks.find(canonical(mask))
            top.set_cell(Vector2i(x,y),0,Vector2i(index%8,index/8))
    top.update_internals()
    check(top.get_used_cells().size() == size.x*size.y,"Missing ground tiles")
    for node in parent.get_children():
        if node is Polygon2D:
            node.material = material

func capture() -> void:
    root.size = Vector2i(1280,800)
    root.content_scale_size = Vector2i(1280,800)
    RenderingServer.set_default_clear_color(GREEN)
    var packed := load(OUT+"connected_plateau_example.tscn") as PackedScene
    check(packed != null,"Example scene did not load")
    var example := packed.instantiate()
    root.add_child(example)
    for frame in range(4):
        await process_frame
    await RenderingServer.frame_post_draw
    var screenshot := root.get_texture().get_image()
    check(not screenshot.is_empty(),"Empty rendered preview")
    check(screenshot.save_png(OUT+"connected_plateau_example_green.png") == OK,"Preview save failed")
    print("CAPTURE PASS: ",screenshot.get_size())
