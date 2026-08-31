#!/usr/bin/env python3
"""Compose a Godot-ready mountain prefab from the clean extracted atlas sprites."""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = PROJECT_ROOT / "addons/beep_game_builder_cs/generated/mountains/clean_source_atlases"
OUTPUT_DIR = PROJECT_ROOT / "addons/beep_game_builder_cs/generated/mountains/clean_atlas_mountain_prefab"
CANVAS_SIZE = (820, 640)


ASSETS: list[dict[str, Any]] = [
    {
        "role": "level_0_left_approach_cliff",
        "category": "level_chunk",
        "source_pack": "mountain_cliff_terrain_tile_atlas",
        "sprite_no": 21,
        "default_position": {"x": 70, "y": 395},
        "height_level": 0,
        "walkable": True,
        "visual_includes_wall": True,
    },
    {
        "role": "level_0_main_cliff",
        "category": "level_chunk",
        "source_pack": "mountain_cliff_terrain_tile_atlas",
        "sprite_no": 7,
        "default_position": {"x": 230, "y": 335},
        "height_level": 0,
        "connectors": {
            "out_north_east": {"x": 162, "y": 93, "side": "north_east"},
        },
        "walkable": True,
        "visual_includes_wall": True,
    },
    {
        "role": "level_0_path_surface",
        "category": "path_overlay",
        "source_pack": "isometric_cliff_and_mountain_paths_1_tileset_atlas",
        "sprite_no": 155,
        "default_position": {"x": 290, "y": 390},
        "height_level": 0,
        "walkable": True,
        "visual_includes_wall": False,
    },
    {
        "role": "level_1_right_plateau_cliff",
        "category": "level_chunk",
        "source_pack": "mountain_cliff_terrain_tile_atlas",
        "sprite_no": 15,
        "default_position": {"x": 420, "y": 286},
        "height_level": 1,
        "connectors": {
            "in_south_west": {"x": 38, "y": 63, "side": "south_west"},
            "out_north_west": {"x": 80, "y": 60, "side": "north_west"},
        },
        "walkable": True,
        "visual_includes_wall": True,
    },
    {
        "role": "level_1_path_surface",
        "category": "path_overlay",
        "source_pack": "isometric_cliff_and_mountain_paths_1_tileset_atlas",
        "sprite_no": 155,
        "default_position": {"x": 500, "y": 310},
        "height_level": 1,
        "walkable": True,
        "visual_includes_wall": False,
    },
    {
        "role": "level_2_left_plateau_cliff",
        "category": "level_chunk",
        "source_pack": "mountain_cliff_terrain_tile_atlas",
        "sprite_no": 48,
        "default_position": {"x": 260, "y": 204},
        "height_level": 2,
        "connectors": {
            "in_south_east": {"x": 142, "y": 63, "side": "south_east"},
            "out_north_east": {"x": 206, "y": 68, "side": "north_east"},
        },
        "walkable": True,
        "visual_includes_wall": True,
    },
    {
        "role": "level_2_path_surface",
        "category": "path_overlay",
        "source_pack": "isometric_cliff_and_mountain_paths_1_tileset_atlas",
        "sprite_no": 155,
        "default_position": {"x": 310, "y": 226},
        "height_level": 2,
        "walkable": True,
        "visual_includes_wall": False,
    },
    {
        "role": "level_3_castle_support_cliff",
        "category": "castle_chunk",
        "source_pack": "mountain_cliff_terrain_tile_atlas",
        "sprite_no": 21,
        "default_position": {"x": 440, "y": 96},
        "height_level": 3,
        "connectors": {
            "in_south_west": {"x": 80, "y": 75, "side": "south_west"},
        },
        "walkable": True,
        "visual_includes_wall": True,
    },
    {
        "role": "level_3_castle_path_surface",
        "category": "path_overlay",
        "source_pack": "isometric_cliff_and_mountain_paths_1_tileset_atlas",
        "sprite_no": 155,
        "default_position": {"x": 500, "y": 145},
        "height_level": 3,
        "walkable": True,
        "visual_includes_wall": False,
    },
    {
        "role": "route_0_to_1_mossy_cliff_ramp",
        "category": "route_chunk",
        "source_pack": "mountain_cliff_terrain_tile_atlas",
        "sprite_no": 64,
        "default_position": {"x": 292, "y": 310},
        "height_level": 1,
        "from_level": 0,
        "to_level": 1,
        "direction": "ascend_north_east",
        "entry_side": "south_west",
        "exit_side": "north_east",
        "local_enter": {"x": 60, "y": 120},
        "local_exit": {"x": 150, "y": 45},
        "walkable": True,
        "climbable": True,
        "visual_includes_wall": True,
    },
    {
        "role": "route_1_to_2_stone_stairs",
        "category": "route_chunk",
        "source_pack": "isometric_cliff_and_mountain_paths_1_tileset_atlas",
        "sprite_no": 136,
        "default_position": {"x": 338, "y": 224},
        "trim_box": (0, 0, 168, 166),
        "height_level": 2,
        "from_level": 1,
        "to_level": 2,
        "direction": "ascend_north_west",
        "entry_side": "south_east",
        "exit_side": "north_west",
        "local_enter": {"x": 150, "y": 120},
        "local_exit": {"x": 42, "y": 42},
        "walkable": True,
        "climbable": True,
        "visual_includes_wall": True,
    },
    {
        "role": "route_2_to_3_castle_stairs",
        "category": "route_chunk",
        "source_pack": "isometric_cliff_and_mountain_paths_1_tileset_atlas",
        "sprite_no": 132,
        "default_position": {"x": 418, "y": 146},
        "trim_box": (0, 0, 162, 166),
        "height_level": 3,
        "from_level": 2,
        "to_level": 3,
        "direction": "ascend_north_east",
        "entry_side": "south_west",
        "exit_side": "north_east",
        "local_enter": {"x": 48, "y": 126},
        "local_exit": {"x": 130, "y": 42},
        "walkable": True,
        "climbable": True,
        "visual_includes_wall": True,
    },
]


LEVELS = [
    {"id": "base_approach", "index": 0, "height": 0, "height_level": 0, "elevation_px": 0, "walkable_region": "level_0_base"},
    {"id": "right_plateau", "index": 1, "height": 1, "height_level": 1, "elevation_px": 62, "walkable_region": "level_1_right"},
    {"id": "left_plateau", "index": 2, "height": 2, "height_level": 2, "elevation_px": 124, "walkable_region": "level_2_left"},
    {"id": "castle_plateau", "index": 3, "height": 3, "height_level": 3, "elevation_px": 186, "walkable_region": "level_3_castle"},
]


WALKABLE_REGIONS = [
    {"id": "level_0_base", "level": 0, "height_level": 0, "elevation_px": 0, "kind": "terrace", "points": [(168, 430), (344, 366), (502, 430), (338, 504)]},
    {"id": "level_1_right", "level": 1, "height_level": 1, "elevation_px": 62, "kind": "terrace", "points": [(476, 318), (572, 286), (688, 322), (574, 376)]},
    {"id": "level_2_left", "level": 2, "height_level": 2, "elevation_px": 124, "kind": "terrace", "points": [(308, 236), (406, 202), (514, 238), (406, 292)]},
    {"id": "level_3_castle", "level": 3, "height_level": 3, "elevation_px": 186, "kind": "castle_floor", "points": [(476, 140), (548, 116), (626, 142), (552, 176)]},
]


ROUTE_REGIONS = [
    {
        "id": "route_base_to_right",
        "from": "base_approach",
        "to": "right_plateau",
        "from_level": 0,
        "to_level": 1,
        "from_elevation_px": 0,
        "to_elevation_px": 62,
        "role": "route_0_to_1_cobble_ramp",
        "kind": "height_ramp_tile",
        "climbable": True,
        "walkable": True,
        "visual_includes_wall": True,
        "points": [(314, 352), (406, 314), (522, 286), (568, 322), (460, 372), (344, 394)],
    },
    {
        "id": "route_right_to_left",
        "from": "right_plateau",
        "to": "left_plateau",
        "from_level": 1,
        "to_level": 2,
        "from_elevation_px": 62,
        "to_elevation_px": 124,
        "role": "route_1_to_2_stone_stairs",
        "kind": "height_stairs_tile",
        "climbable": True,
        "walkable": True,
        "visual_includes_wall": True,
        "points": [(360, 250), (430, 220), (518, 244), (506, 288), (420, 286), (354, 270)],
    },
    {
        "id": "route_left_to_castle",
        "from": "left_plateau",
        "to": "castle_plateau",
        "from_level": 2,
        "to_level": 3,
        "from_elevation_px": 124,
        "to_elevation_px": 186,
        "role": "route_2_to_3_castle_stairs",
        "kind": "height_stairs_tile",
        "climbable": True,
        "walkable": True,
        "visual_includes_wall": True,
        "points": [(426, 178), (492, 146), (584, 130), (614, 158), (526, 198), (442, 214)],
    },
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a clean atlas mountain prefab pack.")
    parser.add_argument("--source-root", type=Path, default=SOURCE_ROOT)
    parser.add_argument("--output-dir", type=Path, default=OUTPUT_DIR)
    parser.add_argument("--name", default="clean_atlas_mountain")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    output_dir = args.output_dir
    chunks_dir = output_dir / "prefab_chunks"
    output_dir.mkdir(parents=True, exist_ok=True)
    chunks_dir.mkdir(parents=True, exist_ok=True)

    for old in chunks_dir.glob("*.png"):
        old.unlink()

    manifests = load_manifests(args.source_root)
    assets = copy_selected_sprites(args.name, args.source_root, manifests, chunks_dir)
    write_chunk_manifest(output_dir / "prefab_chunk_manifest.json", args.name, assets)
    write_chunk_atlas(output_dir / "prefab_chunk_atlas.png", assets, output_dir, labels=False)
    write_chunk_atlas(output_dir / "prefab_chunk_atlas_preview.png", assets, output_dir, labels=True)

    prefab = compose(output_dir, assets)
    prefab.save(output_dir / "prefab.png")
    write_preview(output_dir / "prefab_preview.png", prefab, "CLEAN ATLAS MOUNTAIN")

    manifest = build_prefab_manifest(args.name, prefab.size, assets)
    (output_dir / "prefab_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_path_debug(output_dir / "prefab_path_debug.png", prefab, manifest)
    write_direction_debug(output_dir / "prefab_direction_debug.png", prefab, assets)

    layouts_dir = output_dir / "layout_previews"
    layouts_dir.mkdir(exist_ok=True)
    for name, offset_map in layout_variants().items():
        variant = compose(output_dir, assets, offset_map)
        variant.save(layouts_dir / f"{name}.png")
        write_preview(layouts_dir / f"{name}_preview.png", variant, name.upper())

    print(f"Wrote {output_dir / 'prefab_manifest.json'}")
    print(f"Wrote {output_dir / 'prefab_preview.png'}")
    print(f"Wrote {output_dir / 'prefab_path_debug.png'}")
    print(f"Wrote {output_dir / 'prefab_direction_debug.png'}")


def load_manifests(source_root: Path) -> dict[str, dict[int, dict[str, Any]]]:
    manifests: dict[str, dict[int, dict[str, Any]]] = {}
    for folder in source_root.iterdir():
        manifest_path = folder / "clean_extracted_manifest.json"
        if not manifest_path.exists():
            continue
        data = json.loads(manifest_path.read_text(encoding="utf-8"))
        by_number: dict[int, dict[str, Any]] = {}
        for sprite in data["sprites"]:
            number = int(sprite["id"].rsplit("_", 1)[1])
            by_number[number] = sprite
        manifests[folder.name] = by_number
    return manifests


def copy_selected_sprites(
    name: str,
    source_root: Path,
    manifests: dict[str, dict[int, dict[str, Any]]],
    chunks_dir: Path,
) -> list[dict[str, Any]]:
    assets: list[dict[str, Any]] = []
    for spec in ASSETS:
        source_pack = spec["source_pack"]
        source_no = int(spec["sprite_no"])
        sprite_manifest = manifests[source_pack][source_no]
        source_file = source_root / source_pack / "sprites" / f"{sprite_manifest['id']}.png"
        file_name = f"{name}_{spec['role']}.png"
        target_file = chunks_dir / file_name
        shutil.copyfile(source_file, target_file)
        with Image.open(target_file) as source_image:
            cleaned = remove_detached_fragments(source_image.convert("RGBA"))
            if "trim_box" in spec:
                cleaned = cleaned.crop(spec["trim_box"])
                bbox = cleaned.getchannel("A").getbbox()
                if bbox is not None:
                    cleaned = cleaned.crop((max(0, bbox[0] - 1), max(0, bbox[1] - 1), min(cleaned.width, bbox[2] + 1), min(cleaned.height, bbox[3] + 1)))
            cleaned.save(target_file)
            width, height = cleaned.size

        asset = {
            "id": f"{name}_{spec['role']}",
            "role": spec["role"],
            "category": spec["category"],
            "file": f"prefab_chunks/{file_name}",
            "source_pack": source_pack,
            "source_sprite_id": sprite_manifest["id"],
            "source_rect": sprite_manifest["source_rect"],
            "default_position": spec["default_position"],
            "sprite_size": {"width": width, "height": height},
            "height_level": spec["height_level"],
            "from_level": spec.get("from_level"),
            "to_level": spec.get("to_level"),
            "direction": spec.get("direction"),
            "entry_side": spec.get("entry_side"),
            "exit_side": spec.get("exit_side"),
            "local_enter": spec.get("local_enter"),
            "local_exit": spec.get("local_exit"),
            "connectors": spec.get("connectors", {}),
            "walkable": spec.get("walkable", False),
            "climbable": spec.get("climbable", False),
            "visual_includes_wall": spec.get("visual_includes_wall", False),
        }
        assets.append(asset)
    apply_directional_connector_layout(assets)
    return assets


def apply_directional_connector_layout(assets: list[dict[str, Any]]) -> None:
    by_role = {asset["role"]: asset for asset in assets}

    set_position(by_role["level_0_left_approach_cliff"], 70, 395)
    set_position(by_role["level_0_main_cliff"], 230, 335)

    connect_route(
        by_role,
        from_role="level_0_main_cliff",
        from_connector="out_north_east",
        route_role="route_0_to_1_mossy_cliff_ramp",
        to_role="level_1_right_plateau_cliff",
        to_connector="in_south_west",
    )
    connect_route(
        by_role,
        from_role="level_1_right_plateau_cliff",
        from_connector="out_north_west",
        route_role="route_1_to_2_stone_stairs",
        to_role="level_2_left_plateau_cliff",
        to_connector="in_south_east",
    )
    connect_route(
        by_role,
        from_role="level_2_left_plateau_cliff",
        from_connector="out_north_east",
        route_role="route_2_to_3_castle_stairs",
        to_role="level_3_castle_support_cliff",
        to_connector="in_south_west",
    )

    base = by_role["level_0_main_cliff"]["default_position"]
    level1 = by_role["level_1_right_plateau_cliff"]["default_position"]
    level2 = by_role["level_2_left_plateau_cliff"]["default_position"]
    level3 = by_role["level_3_castle_support_cliff"]["default_position"]
    set_position(by_role["level_0_path_surface"], base["x"] + 66, base["y"] + 60)
    set_position(by_role["level_1_path_surface"], level1["x"] + 72, level1["y"] + 34)
    set_position(by_role["level_2_path_surface"], level2["x"] + 48, level2["y"] + 28)
    set_position(by_role["level_3_castle_path_surface"], level3["x"] + 55, level3["y"] + 34)


def connect_route(
    by_role: dict[str, dict[str, Any]],
    *,
    from_role: str,
    from_connector: str,
    route_role: str,
    to_role: str,
    to_connector: str,
) -> None:
    source = by_role[from_role]
    route = by_role[route_role]
    target = by_role[to_role]

    route_enter = route["local_enter"]
    route_exit = route["local_exit"]
    start = world_connector(source, from_connector)
    set_position(route, start["x"] - route_enter["x"], start["y"] - route_enter["y"])

    route_position = route["default_position"]
    exit_world = {"x": route_position["x"] + route_exit["x"], "y": route_position["y"] + route_exit["y"]}
    target_connector = target["connectors"][to_connector]
    set_position(target, exit_world["x"] - target_connector["x"], exit_world["y"] - target_connector["y"])


def set_position(asset: dict[str, Any], x: int, y: int) -> None:
    asset["default_position"] = {"x": int(x), "y": int(y)}


def world_connector(asset: dict[str, Any], connector_name: str) -> dict[str, int]:
    position = asset["default_position"]
    connector = asset["connectors"][connector_name]
    return {"x": int(position["x"] + connector["x"]), "y": int(position["y"] + connector["y"])}


def remove_detached_fragments(image: Image.Image) -> Image.Image:
    alpha = image.getchannel("A")
    width, height = image.size
    data = alpha.load()
    seen: set[tuple[int, int]] = set()
    components: list[list[tuple[int, int]]] = []

    for y in range(height):
        for x in range(width):
            if data[x, y] <= 16 or (x, y) in seen:
                continue
            stack = [(x, y)]
            component: list[tuple[int, int]] = []
            while stack:
                px, py = stack.pop()
                if (px, py) in seen or px < 0 or py < 0 or px >= width or py >= height:
                    continue
                seen.add((px, py))
                if data[px, py] <= 16:
                    continue
                component.append((px, py))
                stack.extend(((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)))
            if component:
                components.append(component)

    if not components:
        return image

    largest = max(len(component) for component in components)
    keep: set[tuple[int, int]] = set()
    for component in components:
        if len(component) == largest or len(component) >= max(850, largest * 0.08):
            keep.update(component)

    cleaned = image.copy()
    pixels = cleaned.load()
    for y in range(height):
        for x in range(width):
            if data[x, y] > 16 and (x, y) not in keep:
                r, g, b, _ = pixels[x, y]
                pixels[x, y] = (r, g, b, 0)

    bbox = cleaned.getchannel("A").getbbox()
    if bbox is None:
        return cleaned
    x1 = max(0, bbox[0] - 1)
    y1 = max(0, bbox[1] - 1)
    x2 = min(width, bbox[2] + 1)
    y2 = min(height, bbox[3] + 1)
    return cleaned.crop((x1, y1, x2, y2))


def compose(root: Path, assets: list[dict[str, Any]], offsets: dict[str, tuple[int, int]] | None = None) -> Image.Image:
    offsets = offsets or {}
    image = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    for asset in sorted(assets, key=sort_key):
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        position = asset["default_position"]
        dx, dy = offsets.get(asset["role"], (0, 0))
        image.alpha_composite(sprite, (int(position["x"] + dx), int(position["y"] + dy)))
    return image


def sort_key(asset: dict[str, Any]) -> tuple[int, int]:
    category_order = {
        "level_chunk": 0,
        "path_overlay": 4,
        "route_chunk": 5,
        "castle_chunk": 8,
        "castle_floor_overlay": 9,
        "prop_chunk": 10,
    }
    return int(asset["height_level"]) * 20 + category_order.get(str(asset["category"]), 4), int(asset["default_position"]["y"])


def layout_variants() -> dict[str, dict[str, tuple[int, int]]]:
    return {
        "wider": {
            "level_0_left_approach_cliff": (-44, 8),
            "level_1_right_plateau_cliff": (44, 8),
            "route_0_to_1_mossy_cliff_ramp": (18, 6),
            "level_2_left_plateau_cliff": (-18, -4),
            "route_1_to_2_stone_stairs": (-18, -2),
            "level_3_castle_support_cliff": (28, -8),
            "route_2_to_3_castle_stairs": (20, -8),
            "castle_floor_column_left": (28, -8),
            "castle_floor_column_right": (28, -8),
        },
        "taller_castle": {
            "level_3_castle_support_cliff": (6, -46),
            "route_2_to_3_castle_stairs": (0, -30),
            "castle_floor_column_left": (6, -46),
            "castle_floor_column_right": (6, -46),
            "level_2_left_plateau_cliff": (-10, -16),
            "route_1_to_2_stone_stairs": (-8, -8),
        },
    }


def build_prefab_manifest(name: str, size: tuple[int, int], assets: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "name": name,
        "kind": "clean_atlas_mountain_prefab",
        "variant": "green_mountain_connected_levels",
        "source_pack": ".",
        "prefab_image": "prefab.png",
        "prefab_chunk_atlas": "prefab_chunk_atlas.png",
        "prefab_chunk_manifest": "prefab_chunk_manifest.json",
        "size": {"width": size[0], "height": size[1]},
        "height_model": {
            "height_step_px": 62,
            "z_index_step": 10,
            "rule": "Every visible climb chunk has from_level/to_level metadata, and every higher floor is drawn on top of a supporting cliff chunk.",
        },
        "direction_model": {
            "projection": "2d_isometric",
            "allowed_route_directions": ["ascend_north_east", "ascend_north_west"],
            "connector_rule": "Route local_enter must align to the lower platform connector. Route local_exit must align to the higher platform connector.",
            "connection_chain": [
                {
                    "from_role": "level_0_main_cliff",
                    "from_connector": "out_north_east",
                    "route_role": "route_0_to_1_mossy_cliff_ramp",
                    "route_entry_side": "south_west",
                    "route_exit_side": "north_east",
                    "to_role": "level_1_right_plateau_cliff",
                    "to_connector": "in_south_west",
                },
                {
                    "from_role": "level_1_right_plateau_cliff",
                    "from_connector": "out_north_west",
                    "route_role": "route_1_to_2_stone_stairs",
                    "route_entry_side": "south_east",
                    "route_exit_side": "north_west",
                    "to_role": "level_2_left_plateau_cliff",
                    "to_connector": "in_south_east",
                },
                {
                    "from_role": "level_2_left_plateau_cliff",
                    "from_connector": "out_north_east",
                    "route_role": "route_2_to_3_castle_stairs",
                    "route_entry_side": "south_west",
                    "route_exit_side": "north_east",
                    "to_role": "level_3_castle_support_cliff",
                    "to_connector": "in_south_west",
                },
            ],
        },
        "levels": LEVELS,
        "walkable_regions": encode_regions(WALKABLE_REGIONS),
        "route_edges": [
            {
                "from": "base_approach",
                "to": "right_plateau",
                "route_region": "route_base_to_right",
                "role": "route_0_to_1_mossy_cliff_ramp",
                "direction": "ascend_north_east",
                "entry_side": "south_west",
                "exit_side": "north_east",
                "from_connector": "level_0_main_cliff.out_north_east",
                "to_connector": "level_1_right_plateau_cliff.in_south_west",
                "climbable": True,
                "from_level": 0,
                "to_level": 1,
                "points": points([(326, 378), (430, 332), (548, 320)]),
            },
            {
                "from": "right_plateau",
                "to": "left_plateau",
                "route_region": "route_right_to_left",
                "role": "route_1_to_2_stone_stairs",
                "direction": "ascend_north_west",
                "entry_side": "south_east",
                "exit_side": "north_west",
                "from_connector": "level_1_right_plateau_cliff.out_north_west",
                "to_connector": "level_2_left_plateau_cliff.in_south_east",
                "climbable": True,
                "from_level": 1,
                "to_level": 2,
                "points": points([(520, 332), (444, 284), (382, 236)]),
            },
            {
                "from": "left_plateau",
                "to": "castle_plateau",
                "route_region": "route_left_to_castle",
                "role": "route_2_to_3_castle_stairs",
                "direction": "ascend_north_east",
                "entry_side": "south_west",
                "exit_side": "north_east",
                "from_connector": "level_2_left_plateau_cliff.out_north_east",
                "to_connector": "level_3_castle_support_cliff.in_south_west",
                "climbable": True,
                "from_level": 2,
                "to_level": 3,
                "points": points([(438, 230), (504, 176), (552, 140)]),
            },
        ],
        "route_regions": encode_regions(ROUTE_REGIONS),
        "anchors": {
            "player_spawn": {"x": 244, "y": 438, "level": 0, "height_level": 0, "elevation_px": 0, "kind": "route_start"},
            "castle_anchor": {"x": 552, "y": 142, "width": 112, "height": 76, "level": 3, "height_level": 3, "elevation_px": 186, "pivot": "bottom_center", "z_index": 30},
            "plateau_exit": {"x": 552, "y": 142, "level": 3, "height_level": 3, "elevation_px": 186, "kind": "route_end"},
        },
        "placements": [
            {
                "role": asset["role"],
                "asset_id": asset["id"],
                "file": asset["file"],
                "position": asset["default_position"],
                "scale": 1.0,
                "z_index": sort_key(asset)[0],
                "height_level": asset["height_level"],
                "from_level": asset.get("from_level"),
                "to_level": asset.get("to_level"),
                "direction": asset.get("direction"),
                "entry_side": asset.get("entry_side"),
                "exit_side": asset.get("exit_side"),
                "local_enter": asset.get("local_enter"),
                "local_exit": asset.get("local_exit"),
                "connectors": asset.get("connectors", {}),
                "walkable": asset["walkable"],
                "climbable": asset["climbable"],
                "visual_includes_wall": asset["visual_includes_wall"],
            }
            for asset in assets
        ],
        "notes": [
            "Generated from the clean extracted source atlases, not from the old 17-piece tile generator.",
            "The path up is made from route chunks: L0->L1 mossy cliff ramp, L1->L2 stone stairs, L2->L3 castle stairs.",
            "The castle floor is the highest level and sits on a dedicated cliff support chunk.",
        ],
    }


def encode_regions(regions: list[dict[str, Any]]) -> list[dict[str, Any]]:
    encoded: list[dict[str, Any]] = []
    for region in regions:
        copy = dict(region)
        copy["points"] = points(region["points"])
        encoded.append(copy)
    return encoded


def points(raw: list[tuple[int, int]]) -> list[dict[str, int]]:
    return [{"x": x, "y": y} for x, y in raw]


def write_chunk_manifest(path: Path, name: str, assets: list[dict[str, Any]]) -> None:
    manifest = {
        "name": f"{name}_prefab_chunks",
        "kind": "clean_atlas_mountain_prefab_chunk_manifest",
        "source_style": "clean extracted isometric cliff and mountain atlas sprites",
        "atlas": "prefab_chunk_atlas.png",
        "preview": "prefab_chunk_atlas_preview.png",
        "contract": {
            "composition": "Prefab chunks are complete isometric sprites placed by explicit positions.",
            "height": "Level chunks have height_level. Route chunks include from_level and to_level.",
            "castle": "The castle anchor sits on the highest level-3 cliff support chunk.",
        },
        "assets": assets,
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def write_chunk_atlas(path: Path, assets: list[dict[str, Any]], root: Path, labels: bool) -> None:
    cell_w, cell_h = 250, 205
    cols = 4
    rows = (len(assets) + cols - 1) // cols
    background = (21, 34, 43, 255) if labels else (0, 0, 0, 0)
    atlas = Image.new("RGBA", (cols * cell_w, rows * cell_h), background)
    draw = ImageDraw.Draw(atlas)
    for index, asset in enumerate(assets):
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        thumb = sprite.copy()
        thumb.thumbnail((cell_w - 18, cell_h - (44 if labels else 18)), Image.Resampling.LANCZOS)
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        atlas.alpha_composite(thumb, (x + (cell_w - thumb.width) // 2, y + 8))
        if labels:
            draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(76, 92, 96, 255))
            draw.text((x + 8, y + cell_h - 32), str(asset["role"])[:31], fill=(235, 240, 240, 255))
            draw.text((x + 8, y + cell_h - 16), f"H{asset['height_level']} {asset['category']}", fill=(177, 190, 190, 255))
    atlas.save(path)


def write_preview(path: Path, image: Image.Image, title: str) -> None:
    margin = 20
    preview = Image.new("RGBA", (image.width + margin * 2, image.height + margin * 2), (20, 34, 43, 255))
    preview.alpha_composite(image, (margin, margin))
    ImageDraw.Draw(preview).text((14, 10), title, fill=(230, 236, 236, 255))
    preview.convert("RGB").save(path)


def write_path_debug(path: Path, image: Image.Image, manifest: dict[str, Any]) -> None:
    debug = image.copy()
    draw = ImageDraw.Draw(debug, "RGBA")
    for region in manifest["walkable_regions"]:
        polygon = [(point["x"], point["y"]) for point in region["points"]]
        draw.polygon(polygon, fill=(68, 216, 118, 42), outline=(222, 255, 176, 230))
        centroid_x = sum(x for x, _ in polygon) / len(polygon)
        centroid_y = sum(y for _, y in polygon) / len(polygon)
        draw.text((centroid_x - 8, centroid_y - 7), f"L{region['level']}", fill=(255, 255, 255, 255))
    for region in manifest["route_regions"]:
        polygon = [(point["x"], point["y"]) for point in region["points"]]
        draw.polygon(polygon, fill=(255, 220, 40, 58), outline=(50, 38, 18, 255))
        draw.text((polygon[0][0], polygon[0][1] - 12), f"H{region['from_level']}->{region['to_level']}", fill=(255, 255, 255, 255))
    for edge in manifest["route_edges"]:
        path_points = [(point["x"], point["y"]) for point in edge["points"]]
        draw.line(path_points, fill=(255, 244, 92, 255), width=4)
        draw.line(path_points, fill=(68, 48, 18, 255), width=1)
    debug.save(path)


def write_direction_debug(path: Path, image: Image.Image, assets: list[dict[str, Any]]) -> None:
    debug = Image.new("RGBA", image.size, (0, 0, 0, 0))
    debug.alpha_composite(image)
    draw = ImageDraw.Draw(debug, "RGBA")

    for asset in assets:
        position = asset["default_position"]
        for name, connector in asset.get("connectors", {}).items():
            x = int(position["x"] + connector["x"])
            y = int(position["y"] + connector["y"])
            draw.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(72, 230, 150, 230), outline=(12, 42, 26, 255))
            draw.text((x + 7, y - 7), f"{asset['role']}.{name}", fill=(235, 255, 236, 255))

        if asset.get("category") == "route_chunk":
            enter = asset["local_enter"]
            exit_point = asset["local_exit"]
            x1 = int(position["x"] + enter["x"])
            y1 = int(position["y"] + enter["y"])
            x2 = int(position["x"] + exit_point["x"])
            y2 = int(position["y"] + exit_point["y"])
            draw.line((x1, y1, x2, y2), fill=(255, 236, 66, 255), width=5)
            draw.line((x1, y1, x2, y2), fill=(78, 56, 18, 255), width=2)
            draw.ellipse((x1 - 6, y1 - 6, x1 + 6, y1 + 6), fill=(255, 128, 64, 230), outline=(70, 28, 10, 255))
            draw.ellipse((x2 - 6, y2 - 6, x2 + 6, y2 + 6), fill=(88, 168, 255, 230), outline=(20, 40, 80, 255))
            draw.text((x1 + 8, y1 + 2), f"in {asset['entry_side']}", fill=(255, 230, 210, 255))
            draw.text((x2 + 8, y2 - 14), f"out {asset['exit_side']} {asset['direction']}", fill=(215, 235, 255, 255))

    debug.save(path)


if __name__ == "__main__":
    main()
