#!/usr/bin/env python3
"""Build a shape-grid mountain prefab from reusable middle/edge/corner tiles."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFilter


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = PROJECT_ROOT / "addons/beep_game_builder_cs/generated/mountains/clean_source_atlases"
OUTPUT_DIR = PROJECT_ROOT / "addons/beep_game_builder_cs/generated/mountains/shape_based_mountain_prefab"

TILE_W = 96
TILE_H = 48
CLIFF_H = 62
CELL_W = 128
CELL_H = 128
LEVEL_STEP_Y = 78
CANVAS_SIZE = (900, 660)

TOP_POINTS = [(64, 8), (112, 32), (64, 56), (16, 32)]
SIDE_POINTS = {
    "n": [(16, 32), (64, 8), (64, 8 + CLIFF_H), (16, 32 + CLIFF_H)],
    "e": [(64, 8), (112, 32), (112, 32 + CLIFF_H), (64, 8 + CLIFF_H)],
    "s": [(112, 32), (64, 56), (64, 56 + CLIFF_H), (112, 32 + CLIFF_H)],
    "w": [(64, 56), (16, 32), (16, 32 + CLIFF_H), (64, 56 + CLIFF_H)],
}
OPPOSITE = {"n": "s", "s": "n", "e": "w", "w": "e"}
NEIGHBORS = {"n": (0, -1), "e": (1, 0), "s": (0, 1), "w": (-1, 0)}


LEVELS = [
    {
        "id": "level_0_base",
        "height_level": 0,
        "origin": (330, 360),
        "cells": [(-2, 0), (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 0), (0, 1), (1, -1), (1, 0), (2, 0)],
        "path": [(-1, 1), (0, 0), (1, 0)],
    },
    {
        "id": "level_1_right",
        "height_level": 1,
        "origin": (450, 338),
        "cells": [(-1, 0), (0, 0), (1, 0), (-1, 1), (0, 1)],
        "path": [(-1, 1), (0, 0)],
    },
    {
        "id": "level_2_left",
        "height_level": 2,
        "origin": (380, 346),
        "cells": [(0, 0), (1, 0), (0, 1), (1, 1)],
        "path": [(1, 1), (0, 0)],
    },
    {
        "id": "level_3_castle",
        "height_level": 3,
        "origin": (460, 360),
        "cells": [(0, 0), (1, 0), (0, 1), (1, 1)],
        "path": [(0, 1), (0, 0)],
    },
]

ROUTES = [
    {
        "id": "route_0_to_1",
        "from": "level_0_base",
        "to": "level_1_right",
        "from_level": 0,
        "to_level": 1,
        "direction": "ascend_north_east",
        "position": (382, 308),
    },
    {
        "id": "route_1_to_2",
        "from": "level_1_right",
        "to": "level_2_left",
        "from_level": 1,
        "to_level": 2,
        "direction": "ascend_north_west",
        "position": (422, 238),
    },
    {
        "id": "route_2_to_3",
        "from": "level_2_left",
        "to": "level_3_castle",
        "from_level": 2,
        "to_level": 3,
        "direction": "ascend_north_east",
        "position": (462, 182),
    },
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a tile-shape based mountain prefab.")
    parser.add_argument("--source-root", type=Path, default=SOURCE_ROOT)
    parser.add_argument("--output-dir", type=Path, default=OUTPUT_DIR)
    parser.add_argument("--name", default="shape_based_mountain")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    chunks_dir = args.output_dir / "prefab_chunks"
    chunks_dir.mkdir(parents=True, exist_ok=True)
    for old in chunks_dir.glob("*.png"):
        old.unlink()

    textures = load_textures(args.source_root)
    tile_defs = build_tile_library(textures)
    save_tile_library(chunks_dir, tile_defs)

    placements = build_placements(args.name, tile_defs)
    prefab = compose(chunks_dir, placements)
    prefab.save(args.output_dir / "prefab.png")
    write_preview(args.output_dir / "prefab_preview.png", prefab)
    write_shape_debug(args.output_dir / "prefab_shape_debug.png", prefab, placements)
    write_tile_atlas(args.output_dir / "tile_atlas.png", tile_defs, labels=False)
    write_tile_atlas(args.output_dir / "tile_atlas_preview.png", tile_defs, labels=True)
    write_chunk_manifest(args.output_dir / "prefab_chunk_manifest.json", args.name, placements)
    write_prefab_manifest(args.output_dir / "prefab_manifest.json", args.name, placements)

    print(f"Wrote {args.output_dir / 'prefab_manifest.json'}")
    print(f"Wrote {args.output_dir / 'prefab_preview.png'}")
    print(f"Wrote {args.output_dir / 'tile_atlas_preview.png'}")


def load_textures(source_root: Path) -> dict[str, Image.Image]:
    terrain = source_root / "mountain_cliff_terrain_tile_atlas" / "sprites" / "mountain_cliff_terrain_tile_atlas_007.png"
    route = source_root / "isometric_cliff_and_mountain_paths_1_tileset_atlas" / "sprites" / "isometric_cliff_and_mountain_paths_1_tileset_atlas_155.png"
    terrain_img = Image.open(terrain).convert("RGBA")
    route_img = Image.open(route).convert("RGBA")
    return {
        "top": terrain_img.crop((36, 28, min(250, terrain_img.width), min(155, terrain_img.height))),
        "wall": terrain_img.crop((20, 120, min(275, terrain_img.width), min(280, terrain_img.height))),
        "path": route_img,
    }


def build_tile_library(textures: dict[str, Image.Image]) -> dict[str, Image.Image]:
    tiles: dict[str, Image.Image] = {"middle": make_cell_tile(textures, ())}
    for side in ("n", "e", "s", "w"):
        tiles[f"edge_{side}"] = make_cell_tile(textures, (side,))
    for first, second in (("n", "e"), ("e", "s"), ("s", "w"), ("w", "n")):
        tiles[f"corner_{first}{second}"] = make_cell_tile(textures, (first, second))
    for mask in ("nes", "new", "nsw", "esw"):
        tiles[f"cap_{mask}"] = make_cell_tile(textures, tuple(mask))
    tiles["path_middle"] = make_path_tile(textures)
    tiles["ramp_ne"] = make_ramp_tile(textures, "ne")
    tiles["ramp_nw"] = make_ramp_tile(textures, "nw")
    return tiles


def save_tile_library(chunks_dir: Path, tile_defs: dict[str, Image.Image]) -> None:
    for name, image in tile_defs.items():
        image.save(chunks_dir / f"{name}.png")


def make_cell_tile(textures: dict[str, Image.Image], missing_sides: tuple[str, ...]) -> Image.Image:
    image = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")

    for side in ("n", "e", "w", "s"):
        if side in missing_sides:
            wall = masked_texture(textures["wall"], (CELL_W, CELL_H), SIDE_POINTS[side])
            shade = {"n": (20, 25, 32, 90), "e": (10, 12, 18, 54), "s": (0, 0, 0, 84), "w": (12, 14, 20, 68)}[side]
            wall.alpha_composite(flat_polygon(SIDE_POINTS[side], shade))
            image.alpha_composite(wall)
            draw.line([SIDE_POINTS[side][0], SIDE_POINTS[side][1]], fill=(214, 218, 190, 130), width=1)

    top = masked_texture(textures["top"], (CELL_W, CELL_H), TOP_POINTS)
    top.alpha_composite(edge_darkening(missing_sides))
    image.alpha_composite(top)
    draw.line(TOP_POINTS + [TOP_POINTS[0]], fill=(236, 244, 218, 70), width=1)
    return image


def make_path_tile(textures: dict[str, Image.Image]) -> Image.Image:
    image = make_cell_tile(textures, ())
    overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
    path = [(31, 28), (62, 16), (99, 32), (65, 48)]
    texture = masked_texture(textures["path"], image.size, path, alpha=178)
    overlay.alpha_composite(texture)
    draw = ImageDraw.Draw(overlay, "RGBA")
    draw.line(path + [path[0]], fill=(238, 224, 178, 110), width=1)
    image.alpha_composite(overlay)
    return image


def make_ramp_tile(textures: dict[str, Image.Image], direction: str) -> Image.Image:
    image = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    if direction == "ne":
        base = [(30, 88), (76, 58), (108, 70), (60, 104)]
        wall = [(30, 88), (60, 104), (60, 124), (30, 108)]
    else:
        base = [(98, 88), (52, 58), (20, 70), (68, 104)]
        wall = [(98, 88), (68, 104), (68, 124), (98, 108)]
    image.alpha_composite(masked_texture(textures["wall"], image.size, wall))
    image.alpha_composite(masked_texture(textures["top"], image.size, base))
    path = masked_texture(textures["path"], image.size, base, alpha=160)
    image.alpha_composite(path)
    draw.line(base + [base[0]], fill=(238, 242, 214, 130), width=1)
    draw.line(wall + [wall[0]], fill=(18, 22, 24, 120), width=1)
    return image


def masked_texture(source: Image.Image, size: tuple[int, int], polygon: list[tuple[int, int]], alpha: int = 255) -> Image.Image:
    texture = tile_texture(source, size)
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon(polygon, fill=alpha)
    texture.putalpha(mask)
    return texture


def flat_polygon(polygon: list[tuple[int, int]], color: tuple[int, int, int, int]) -> Image.Image:
    image = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
    ImageDraw.Draw(image, "RGBA").polygon(polygon, fill=color)
    return image


def tile_texture(source: Image.Image, size: tuple[int, int]) -> Image.Image:
    output = Image.new("RGBA", size, (0, 0, 0, 0))
    sample = flatten_opaque_texture(source).resize((max(32, source.width // 2), max(32, source.height // 2)))
    for y in range(0, size[1], sample.height):
        for x in range(0, size[0], sample.width):
            output.alpha_composite(sample, (x, y))
    return output


def flatten_opaque_texture(source: Image.Image) -> Image.Image:
    image = source.convert("RGBA")
    pixels = list(image.getdata())
    opaque = [pixel for pixel in pixels if pixel[3] > 24]
    if not opaque:
        return Image.new("RGBA", image.size, (90, 96, 82, 255))

    avg = tuple(sum(pixel[channel] for pixel in opaque) // len(opaque) for channel in range(3))
    background = Image.new("RGBA", image.size, (*avg, 255))
    background.alpha_composite(image)
    return background


def edge_darkening(missing_sides: tuple[str, ...]) -> Image.Image:
    image = Image.new("RGBA", (CELL_W, CELL_H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image, "RGBA")
    for side in missing_sides:
        draw.line(SIDE_POINTS[side][:2], fill=(20, 24, 20, 85), width=4)
    return image.filter(ImageFilter.GaussianBlur(0.45))


def trim(image: Image.Image) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    return image.crop(bbox) if bbox else image


def build_placements(name: str, tile_defs: dict[str, Image.Image]) -> list[dict[str, Any]]:
    placements: list[dict[str, Any]] = []
    for level in LEVELS:
        cells = set(tuple(cell) for cell in level["cells"])
        path_cells = set(tuple(cell) for cell in level["path"])
        for cell in sorted(cells, key=lambda c: (c[0] + c[1], c[1])):
            missing = tuple(side for side, delta in NEIGHBORS.items() if (cell[0] + delta[0], cell[1] + delta[1]) not in cells)
            tile_name = tile_for_missing(missing)
            x, y = project_cell(level, cell)
            placements.append(asset(name, level, cell, tile_name, "level_tile", x, y, missing))
            if cell in path_cells:
                placements.append(asset(name, level, cell, "path_middle", "path_overlay", x, y - 1, ()))

    for route in ROUTES:
        tile_name = "ramp_ne" if route["direction"] == "ascend_north_east" else "ramp_nw"
        x, y = route["position"]
        placements.append(
            {
                "id": f"{name}_{route['id']}",
                "role": route["id"],
                "category": "route_chunk",
                "file": f"prefab_chunks/{tile_name}.png",
                "tile_role": tile_name,
                "default_position": {"x": x, "y": y},
                "height_level": route["to_level"],
                "from_level": route["from_level"],
                "to_level": route["to_level"],
                "direction": route["direction"],
                "entry_side": "south_west" if tile_name == "ramp_ne" else "south_east",
                "exit_side": "north_east" if tile_name == "ramp_ne" else "north_west",
                "walkable": True,
                "climbable": True,
                "visual_includes_wall": True,
            }
        )
    return placements


def asset(
    name: str,
    level: dict[str, Any],
    cell: tuple[int, int],
    tile_name: str,
    category: str,
    x: int,
    y: int,
    missing: tuple[str, ...],
) -> dict[str, Any]:
    return {
        "id": f"{name}_{level['id']}_{cell[0]}_{cell[1]}_{category}",
        "role": f"{level['id']}_{category}_{cell[0]}_{cell[1]}",
        "category": category,
        "file": f"prefab_chunks/{tile_name}.png",
        "tile_role": tile_name,
        "cell": {"x": cell[0], "y": cell[1]},
        "missing_neighbor_sides": list(missing),
        "default_position": {"x": x, "y": y},
        "height_level": level["height_level"],
        "from_level": None,
        "to_level": None,
        "walkable": category in ("level_tile", "path_overlay"),
        "climbable": False,
        "visual_includes_wall": bool(missing),
    }


def tile_for_missing(missing: tuple[str, ...]) -> str:
    if not missing:
        return "middle"
    missing_set = set(missing)
    if len(missing_set) == 1:
        return f"edge_{next(iter(missing_set))}"
    if len(missing_set) == 2:
        for name in ("ne", "es", "sw", "wn"):
            if set(name) == missing_set:
                return f"corner_{name}"
    if len(missing_set) == 3:
        return f"cap_{''.join(side for side in 'nesw' if side in missing_set)}"
    return "middle"


def project_cell(level: dict[str, Any], cell: tuple[int, int]) -> tuple[int, int]:
    origin_x, origin_y = level["origin"]
    x, y = cell
    return int(origin_x + (x - y) * (TILE_W / 2)), int(origin_y + (x + y) * (TILE_H / 2) - level["height_level"] * LEVEL_STEP_Y)


def compose(chunks_dir: Path, placements: list[dict[str, Any]]) -> Image.Image:
    image = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    for placement in sorted(placements, key=placement_sort_key):
        sprite = Image.open(chunks_dir / Path(placement["file"]).name).convert("RGBA")
        pos = placement["default_position"]
        image.alpha_composite(sprite, (int(pos["x"]), int(pos["y"])))
    return trim(image)


def placement_sort_key(placement: dict[str, Any]) -> tuple[int, int, int]:
    category = {"level_tile": 0, "path_overlay": 2, "route_chunk": 4}.get(placement["category"], 1)
    pos = placement["default_position"]
    return int(placement["height_level"]) * 1000 + int(pos["y"]) + category * 10, int(pos["x"]), category


def write_preview(path: Path, image: Image.Image) -> None:
    preview = Image.new("RGBA", (image.width + 40, image.height + 40), (20, 34, 43, 255))
    preview.alpha_composite(image, (20, 20))
    ImageDraw.Draw(preview).text((14, 10), "SHAPE-BASED MOUNTAIN PREFAB", fill=(230, 236, 236, 255))
    preview.convert("RGB").save(path)


def write_shape_debug(path: Path, image: Image.Image, placements: list[dict[str, Any]]) -> None:
    debug = Image.new("RGBA", image.size, (0, 0, 0, 0))
    debug.alpha_composite(image)
    draw = ImageDraw.Draw(debug, "RGBA")
    for placement in placements:
        if placement["category"] != "level_tile":
            continue
        pos = placement["default_position"]
        polygon = [(pos["x"] + x, pos["y"] + y) for x, y in TOP_POINTS]
        draw.line(polygon + [polygon[0]], fill=(255, 250, 90, 160), width=1)
        draw.text((pos["x"] + 46, pos["y"] + 28), f"L{placement['height_level']}", fill=(255, 255, 255, 220))
    debug.save(path)


def write_tile_atlas(path: Path, tile_defs: dict[str, Image.Image], labels: bool) -> None:
    names = list(tile_defs)
    cols = 5
    cell_w = 172
    cell_h = 150
    rows = (len(names) + cols - 1) // cols
    atlas = Image.new("RGBA", (cols * cell_w, rows * cell_h), (20, 34, 43, 255) if labels else (0, 0, 0, 0))
    draw = ImageDraw.Draw(atlas)
    for index, name in enumerate(names):
        image = tile_defs[name]
        thumb = image.copy()
        thumb.thumbnail((cell_w - 18, cell_h - 34), Image.Resampling.LANCZOS)
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        atlas.alpha_composite(thumb, (x + (cell_w - thumb.width) // 2, y + 8))
        if labels:
            draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(80, 96, 100, 255))
            draw.text((x + 8, y + cell_h - 22), name, fill=(235, 240, 240, 255))
    atlas.save(path)


def write_chunk_manifest(path: Path, name: str, placements: list[dict[str, Any]]) -> None:
    path.write_text(
        json.dumps(
            {
                "name": f"{name}_shape_chunks",
                "kind": "shape_based_mountain_prefab_chunk_manifest",
                "atlas": "tile_atlas.png",
                "preview": "tile_atlas_preview.png",
                "contract": {
                    "shape_rule": "Each level is generated from cells. Middle cells have no missing neighbors; edge/corner/cap cells are selected from the neighbor mask.",
                    "route_rule": "Ramps are separate climb chunks between adjacent height levels.",
                },
                "assets": placements,
            },
            indent=2,
        ),
        encoding="utf-8",
    )


def write_prefab_manifest(path: Path, name: str, placements: list[dict[str, Any]]) -> None:
    walkable_regions = []
    for level in LEVELS:
        points = [project_cell(level, cell) for cell in level["cells"]]
        min_x = min(x for x, _ in points) + 16
        max_x = max(x for x, _ in points) + 112
        min_y = min(y for _, y in points) + 8
        max_y = max(y for _, y in points) + 56
        walkable_regions.append(
            {
                "id": level["id"],
                "level": level["height_level"],
                "height_level": level["height_level"],
                "elevation_px": level["height_level"] * LEVEL_STEP_Y,
                "kind": "shape_grid_terrace",
                "points": [{"x": min_x, "y": min_y}, {"x": max_x, "y": min_y}, {"x": max_x, "y": max_y}, {"x": min_x, "y": max_y}],
                "cells": [{"x": x, "y": y} for x, y in level["cells"]],
            }
        )

    route_edges = []
    route_regions = []
    for route in ROUTES:
        x, y = route["position"]
        points = [{"x": x + 24, "y": y + 18}, {"x": x + 104, "y": y + 18}, {"x": x + 104, "y": y + 72}, {"x": x + 24, "y": y + 72}]
        route_edges.append(
            {
                "from": route["from"],
                "to": route["to"],
                "route_region": route["id"],
                "role": route["id"],
                "direction": route["direction"],
                "from_level": route["from_level"],
                "to_level": route["to_level"],
                "climbable": True,
                "points": points,
            }
        )
        route_regions.append(
            {
                "id": route["id"],
                "from": route["from"],
                "to": route["to"],
                "from_level": route["from_level"],
                "to_level": route["to_level"],
                "from_elevation_px": route["from_level"] * LEVEL_STEP_Y,
                "to_elevation_px": route["to_level"] * LEVEL_STEP_Y,
                "direction": route["direction"],
                "role": route["id"],
                "kind": "shape_grid_ramp",
                "climbable": True,
                "walkable": True,
                "visual_includes_wall": True,
                "points": points,
            }
        )

    path.write_text(
        json.dumps(
            {
                "name": name,
                "kind": "shape_based_mountain_prefab",
                "variant": "green_grey_shape_grid",
                "source_pack": ".",
                "prefab_image": "prefab.png",
                "prefab_chunk_atlas": "tile_atlas.png",
                "prefab_chunk_manifest": "prefab_chunk_manifest.json",
                "tile_atlas_preview": "tile_atlas_preview.png",
                "shape_model": {
                    "tile_width": TILE_W,
                    "tile_height": TILE_H,
                    "cliff_height": CLIFF_H,
                    "level_step_y": LEVEL_STEP_Y,
                    "rule": "Build each level from middle cells and surrounding edge/corner/cap cells selected from the cell neighbor mask.",
                    "levels": LEVELS,
                },
                "height_model": {
                    "height_step_px": LEVEL_STEP_Y,
                    "z_index_step": 10,
                    "rule": "Higher levels are drawn with smaller shape grids and adjacent climb ramps.",
                },
                "levels": [
                    {
                        "id": level["id"],
                        "index": level["height_level"],
                        "height": level["height_level"],
                        "height_level": level["height_level"],
                        "elevation_px": level["height_level"] * LEVEL_STEP_Y,
                        "walkable_region": level["id"],
                    }
                    for level in LEVELS
                ],
                "walkable_regions": walkable_regions,
                "route_edges": route_edges,
                "route_regions": route_regions,
                "anchors": {
                    "player_spawn": {"x": 310, "y": 464, "level": 0, "height_level": 0, "elevation_px": 0, "kind": "route_start"},
                    "castle_anchor": {"x": 560, "y": 160, "width": 140, "height": 90, "level": 3, "height_level": 3, "elevation_px": 3 * LEVEL_STEP_Y, "pivot": "bottom_center", "z_index": 30},
                },
                "placements": placements,
                "notes": [
                    "This replaces the stacked-island prefab with a shape-grid mountain.",
                    "Middle, edge, corner, and cap tiles are chosen from each level shape's neighbor mask.",
                ],
            },
            indent=2,
        ),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
