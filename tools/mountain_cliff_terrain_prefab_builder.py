#!/usr/bin/env python3
"""Build prefab-style mountain chunks from Mountain Cliff Terrain Tile Atlas."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


CANVAS_SIZE = (690, 590)


CHUNKS = [
    {
        "role": "level_0_main_grass_cliff",
        "category": "level_chunk",
        "box": (1887, 307, 2171, 568),
        "position": (76, 302),
        "height_level": 0,
        "walkable": True,
    },
    {
        "role": "level_1_right_grass_cliff",
        "category": "level_chunk",
        "box": (1888, 578, 2146, 778),
        "position": (342, 246),
        "height_level": 1,
        "walkable": True,
    },
    {
        "role": "level_2_left_grass_cliff",
        "category": "level_chunk",
        "box": (1903, 967, 2162, 1164),
        "position": (176, 178),
        "height_level": 2,
        "walkable": True,
    },
    {
        "role": "level_3_castle_stone_cliff",
        "category": "castle_chunk",
        "box": (1886, 11, 2172, 292),
        "position": (352, 42),
        "height_level": 3,
        "walkable": True,
    },
    {
        "role": "route_0_to_1_mossy_ramp",
        "category": "route_asset",
        "box": (1886, 785, 2062, 950),
        "position": (222, 306),
        "height_level": 1,
        "from_level": 0,
        "to_level": 1,
        "walkable": True,
        "climbable": True,
    },
    {
        "role": "route_1_to_2_mossy_ramp",
        "category": "route_asset",
        "box": (1898, 1185, 2075, 1340),
        "position": (240, 222),
        "height_level": 2,
        "from_level": 1,
        "to_level": 2,
        "walkable": True,
        "climbable": True,
    },
    {
        "role": "route_2_to_3_short_ramp",
        "category": "route_asset",
        "box": (1897, 1356, 2018, 1463),
        "position": (326, 140),
        "height_level": 3,
        "from_level": 2,
        "to_level": 3,
        "walkable": True,
        "climbable": True,
    },
    {
        "role": "level_0_side_grass_cliff",
        "category": "variation_chunk",
        "box": (172, 764, 315, 928),
        "position": (18, 365),
        "height_level": 0,
        "walkable": True,
    },
    {
        "role": "small_mossy_rock_prop",
        "category": "prop_chunk",
        "box": (2038, 1476, 2172, 1563),
        "position": (510, 370),
        "height_level": 1,
        "walkable": False,
    },
]


PRESETS = {
    "reference": {},
    "wide": {
        "level_0_main_grass_cliff": (-42, 18),
        "level_0_side_grass_cliff": (-40, 26),
        "level_1_right_grass_cliff": (58, 12),
        "route_0_to_1_mossy_ramp": (22, 10),
        "level_2_left_grass_cliff": (-22, -8),
        "level_3_castle_stone_cliff": (30, -10),
        "small_mossy_rock_prop": (68, 10),
    },
    "high_castle": {
        "level_2_left_grass_cliff": (-8, -24),
        "route_1_to_2_mossy_ramp": (-8, -18),
        "route_2_to_3_short_ramp": (4, -40),
        "level_3_castle_stone_cliff": (8, -70),
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a Mountain Cliff Terrain prefab pack.")
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_cliff_terrain")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    chunks_dir = args.output_dir / "prefab_chunks"
    chunks_dir.mkdir(exist_ok=True)
    for old in chunks_dir.glob("*.png"):
        old.unlink()

    source = Image.open(args.source).convert("RGBA")
    assets = []
    for spec in CHUNKS:
        sprite = extract_sprite(source, spec)
        file_name = f"{args.name}_{spec['role']}.png"
        sprite.save(chunks_dir / file_name)
        assets.append(
            {
                "id": f"{args.name}_{spec['role']}",
                "role": spec["role"],
                "category": spec["category"],
                "file": f"prefab_chunks/{file_name}",
                "source_rect": rect_from_box(spec["box"]),
                "default_position": {"x": spec["position"][0], "y": spec["position"][1]},
                "sprite_size": {"width": sprite.width, "height": sprite.height},
                "height_level": spec["height_level"],
                "from_level": spec.get("from_level"),
                "to_level": spec.get("to_level"),
                "walkable": spec.get("walkable", False),
                "climbable": spec.get("climbable", False),
                "visual_includes_wall": spec["category"] != "prop_chunk",
            }
        )

    write_chunk_manifest(args.output_dir / "prefab_chunk_manifest.json", args.name, assets)
    write_chunk_atlas(args.output_dir / "prefab_chunk_atlas.png", assets, args.output_dir, False)
    write_chunk_atlas(args.output_dir / "prefab_chunk_atlas_preview.png", assets, args.output_dir, True)

    prefab = compose(args.output_dir, assets, "reference")
    prefab.save(args.output_dir / "prefab.png")
    write_preview(args.output_dir / "prefab_preview.png", prefab, "REFERENCE")
    manifest = build_manifest(args.name, prefab.size, assets)
    (args.output_dir / "prefab_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_path_debug(args.output_dir / "prefab_path_debug.png", prefab, manifest)

    previews_dir = args.output_dir / "layout_previews"
    previews_dir.mkdir(exist_ok=True)
    for preset in PRESETS:
        image = compose(args.output_dir, assets, preset)
        image.save(previews_dir / f"{preset}.png")
        write_preview(previews_dir / f"{preset}_preview.png", image, preset.upper())

    print(f"Wrote {args.output_dir / 'prefab_manifest.json'}")
    print(f"Wrote {args.output_dir / 'prefab_preview.png'}")


def rect_from_box(box: tuple[int, int, int, int]) -> dict:
    return {"x": box[0], "y": box[1], "width": box[2] - box[0], "height": box[3] - box[1]}


def extract_sprite(source: Image.Image, spec: dict) -> Image.Image:
    crop = source.crop(spec["box"]).convert("RGBA")
    crop = remove_white_background(crop)
    bbox = crop.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    x1 = max(0, bbox[0] - 2)
    y1 = max(0, bbox[1] - 2)
    x2 = min(crop.width, bbox[2] + 2)
    y2 = min(crop.height, bbox[3] + 2)
    return crop.crop((x1, y1, x2, y2))


def remove_white_background(image: Image.Image) -> Image.Image:
    pixels = image.load()
    width, height = image.size
    result = image.copy()
    out = result.load()
    for y in range(height):
        for x in range(width):
            r, g, b, a = out[x, y]
            if r > 244 and g > 244 and b > 244:
                out[x, y] = (r, g, b, 0)
            elif r > 230 and g > 230 and b > 230:
                out[x, y] = (r, g, b, min(a, 70))
    return result


def remove_atlas_background(image: Image.Image) -> Image.Image:
    pixels = image.load()
    width, height = image.size
    seeds = [
        pixels[0, 0][:3],
        pixels[width - 1, 0][:3],
        pixels[0, height - 1][:3],
        pixels[width - 1, height - 1][:3],
    ]

    def distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
        return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2) ** 0.5

    def is_bg(x: int, y: int) -> bool:
        color = pixels[x, y][:3]
        return min(distance(color, seed) for seed in seeds) < 34

    stack = [(x, 0) for x in range(width)] + [(x, height - 1) for x in range(width)]
    stack += [(0, y) for y in range(height)] + [(width - 1, y) for y in range(height)]
    seen: set[tuple[int, int]] = set()
    transparent: set[tuple[int, int]] = set()
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or x < 0 or y < 0 or x >= width or y >= height:
            continue
        seen.add((x, y))
        if not is_bg(x, y):
            continue
        transparent.add((x, y))
        stack.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))

    result = image.copy()
    result_pixels = result.load()
    for x, y in transparent:
        r, g, b, _ = result_pixels[x, y]
        result_pixels[x, y] = (r, g, b, 0)
    return result


def compose(root: Path, assets: list[dict], preset: str) -> Image.Image:
    offsets = PRESETS[preset]
    placements = []
    for asset in assets:
        if preset == "reference" and asset["category"] == "variation_chunk":
            continue
        if asset["category"] == "route_asset":
            continue
        pos = asset["default_position"]
        dx, dy = offsets.get(asset["role"], (0, 0))
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        sprite = add_local_path_marks(sprite, asset["role"])
        placements.append((asset, sprite, int(pos["x"] + dx), int(pos["y"] + dy)))

    image = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    for asset, sprite, x, y in sorted(placements, key=lambda item: sort_key(item[0])):
        image.alpha_composite(sprite, (x, y))
    return image


def add_local_path_marks(sprite: Image.Image, role: str) -> Image.Image:
    paths = {
        "level_0_main_grass_cliff": [[(55, 98), (105, 76), (160, 58)]],
        "level_1_right_grass_cliff": [[(46, 72), (108, 54), (170, 38)]],
        "level_2_left_grass_cliff": [[(64, 68), (124, 50), (184, 36)]],
        "level_3_castle_stone_cliff": [[(70, 70), (142, 52), (214, 42)]],
    }
    if role not in paths:
        return sprite

    result = sprite.copy()
    overlay = Image.new("RGBA", sprite.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay, "RGBA")
    for path in paths[role]:
        draw.line(path, fill=(95, 78, 50, 75), width=13, joint="curve")
        draw.line(path, fill=(188, 164, 112, 105), width=8, joint="curve")
        draw.line(path, fill=(224, 204, 150, 70), width=3, joint="curve")
        for x, y in path:
            draw.ellipse((x - 2, y - 1, x + 3, y + 2), fill=(62, 54, 42, 80))
    result.alpha_composite(overlay)
    return result


def sort_key(asset: dict) -> tuple[int, int]:
    cat = {"level_chunk": 0, "variation_chunk": 1, "route_asset": 5, "route_chunk": 5, "castle_chunk": 8, "prop_chunk": 9}.get(asset["category"], 4)
    return int(asset["height_level"]) * 10 + cat, int(asset.get("to_level") or -1)


def write_chunk_manifest(path: Path, name: str, assets: list[dict]) -> None:
    manifest = {
        "name": f"{name}_prefab_chunks",
        "kind": "mountain_cliff_terrain_prefab_chunk_atlas",
            "source_style": "Mountain Cliff Terrain Tile Atlas JFIF",
        "atlas": "prefab_chunk_atlas.png",
        "preview": "prefab_chunk_atlas_preview.png",
        "contract": {
            "prefab_way": "Use complete isometric cliff terrain chunks from the source atlas.",
            "height_rule": "Every higher platform is a whole cliff sprite and every route chunk has from_level/to_level.",
            "castle_rule": "The level_3 castle chunk is the highest supported floor.",
        },
        "assets": assets,
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def build_manifest(name: str, size: tuple[int, int], assets: list[dict]) -> dict:
    return {
        "name": name,
        "kind": "mountain_semantic_composite_prefab",
        "variant": "mountain_cliff_terrain_green",
        "source_pack": ".",
        "prefab_image": "prefab.png",
        "prefab_chunk_atlas": "prefab_chunk_atlas.png",
        "prefab_chunk_manifest": "prefab_chunk_manifest.json",
        "size": {"width": size[0], "height": size[1]},
        "height_model": {
            "height_step_px": 54,
            "z_index_step": 10,
            "rule": "Platforms are discrete supported cliff chunks. Route chunks climb between adjacent height levels.",
        },
        "levels": [
            {"id": "base", "index": 0, "height": 0, "height_level": 0, "elevation_px": 0, "walkable_region": "level_0_base"},
            {"id": "right_plateau", "index": 1, "height": 1, "height_level": 1, "elevation_px": 54, "walkable_region": "level_1_right"},
            {"id": "left_plateau", "index": 2, "height": 2, "height_level": 2, "elevation_px": 108, "walkable_region": "level_2_left"},
            {"id": "castle_plateau", "index": 3, "height": 3, "height_level": 3, "elevation_px": 162, "walkable_region": "level_3_castle"},
        ],
        "walkable_regions": [
            {"id": "level_0_base", "level": 0, "height_level": 0, "elevation_px": 0, "kind": "terrace", "points": points([(112, 350), (206, 316), (300, 350), (206, 392)])},
            {"id": "level_1_right", "level": 1, "height_level": 1, "elevation_px": 54, "kind": "terrace", "points": points([(306, 276), (402, 242), (494, 276), (402, 318)])},
            {"id": "level_2_left", "level": 2, "height_level": 2, "elevation_px": 108, "kind": "terrace", "points": points([(218, 202), (304, 168), (392, 202), (304, 242)])},
            {"id": "level_3_castle", "level": 3, "height_level": 3, "elevation_px": 162, "kind": "castle_plateau", "points": points([(340, 100), (430, 70), (522, 100), (430, 136)])},
        ],
        "route_edges": [
            {"from": "base", "to": "right_plateau", "route_region": "route_base_to_right", "role": "route_0_to_1_mossy_ramp", "climbable": True, "from_level": 0, "to_level": 1, "points": points([(220, 344), (302, 310), (392, 280)])},
            {"from": "right_plateau", "to": "left_plateau", "route_region": "route_right_to_left", "role": "route_1_to_2_mossy_ramp", "climbable": True, "from_level": 1, "to_level": 2, "points": points([(382, 276), (318, 238), (292, 202)])},
            {"from": "left_plateau", "to": "castle_plateau", "route_region": "route_left_to_castle", "role": "route_2_to_3_short_ramp", "climbable": True, "from_level": 2, "to_level": 3, "points": points([(314, 196), (360, 152), (426, 104)])},
        ],
        "route_regions": [
            {"id": "route_base_to_right", "from": "base", "to": "right_plateau", "from_level": 0, "to_level": 1, "from_elevation_px": 0, "to_elevation_px": 54, "role": "route_0_to_1_mossy_ramp", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": points([(202, 324), (292, 286), (398, 264), (426, 292), (318, 334), (216, 366)])},
            {"id": "route_right_to_left", "from": "right_plateau", "to": "left_plateau", "from_level": 1, "to_level": 2, "from_elevation_px": 54, "to_elevation_px": 108, "role": "route_1_to_2_mossy_ramp", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": points([(264, 218), (350, 220), (418, 260), (398, 296), (308, 258), (252, 236)])},
            {"id": "route_left_to_castle", "from": "left_plateau", "to": "castle_plateau", "from_level": 2, "to_level": 3, "from_elevation_px": 108, "to_elevation_px": 162, "role": "route_2_to_3_short_ramp", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": points([(306, 158), (368, 116), (444, 92), (470, 116), (392, 158), (324, 188)])},
        ],
        "anchors": {
            "player_spawn": {"x": 204, "y": 360, "level": 0, "height_level": 0, "elevation_px": 0, "kind": "route_start"},
            "castle_anchor": {"x": 430, "y": 94, "width": 112, "height": 72, "level": 3, "height_level": 3, "elevation_px": 162, "pivot": "bottom_center", "z_index": 30},
            "plateau_exit": {"x": 426, "y": 104, "level": 3, "height_level": 3, "elevation_px": 162, "kind": "route_end"},
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
                "walkable": asset["walkable"],
                "climbable": asset["climbable"],
                "visual_includes_wall": asset["visual_includes_wall"],
            }
            for asset in assets
            if asset["category"] != "variation_chunk"
            and asset["category"] != "route_asset"
        ],
        "notes": [
            "Generated from Art/TileSets/Mountain Cliff Terrain Tile Atlas.jfif.",
            "This pack uses complete isometric terrain chunks with their own cliff faces and tops.",
        ],
    }


def points(raw: list[tuple[int, int]]) -> list[dict]:
    return [{"x": x, "y": y} for x, y in raw]


def write_chunk_atlas(path: Path, assets: list[dict], root: Path, labels: bool) -> None:
    cell_w = 285
    cell_h = 210
    cols = 3
    rows = (len(assets) + cols - 1) // cols
    bg = (30, 34, 32, 255) if labels else (0, 0, 0, 0)
    atlas = Image.new("RGBA", (cols * cell_w, rows * cell_h), bg)
    draw = ImageDraw.Draw(atlas)
    for index, asset in enumerate(assets):
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        thumb = sprite.copy()
        thumb.thumbnail((cell_w - 18, cell_h - (48 if labels else 18)), Image.Resampling.LANCZOS)
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        atlas.alpha_composite(thumb, (x + (cell_w - thumb.width) // 2, y + 8))
        if labels:
            draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(72, 78, 74, 255))
            draw.text((x + 8, y + cell_h - 32), asset["role"][:34], fill=(238, 241, 237, 255))
            draw.text((x + 8, y + cell_h - 16), f"H{asset['height_level']} {asset['category']}", fill=(177, 187, 179, 255))
    atlas.convert("RGB" if labels else "RGBA").save(path)


def write_preview(path: Path, image: Image.Image, title: str) -> None:
    margin = 26
    preview = Image.new("RGBA", (image.width + margin * 2, image.height + margin * 2), (20, 35, 45, 255))
    preview.alpha_composite(image, (margin, margin))
    ImageDraw.Draw(preview).text((14, 10), f"MOUNTAIN CLIFF TERRAIN PREFAB - {title}", fill=(230, 236, 230, 255))
    preview.convert("RGB").save(path)


def write_path_debug(path: Path, image: Image.Image, manifest: dict) -> None:
    debug = image.copy()
    draw = ImageDraw.Draw(debug, "RGBA")
    for region in manifest["walkable_regions"]:
        poly = [(p["x"], p["y"]) for p in region["points"]]
        draw.polygon(poly, fill=(80, 220, 120, 48), outline=(230, 255, 170, 220))
        cx = sum(x for x, _ in poly) / len(poly)
        cy = sum(y for _, y in poly) / len(poly)
        draw.text((cx - 10, cy - 8), f"L{region['level']}", fill=(255, 255, 255, 255))
    for region in manifest["route_regions"]:
        poly = [(p["x"], p["y"]) for p in region["points"]]
        draw.polygon(poly, fill=(255, 215, 40, 65), outline=(60, 40, 10, 255))
        draw.text((poly[0][0], poly[0][1]), f"H{region['from_level']}->{region['to_level']}", fill=(255, 255, 255, 255))
    for edge in manifest["route_edges"]:
        pts = [(p["x"], p["y"]) for p in edge["points"]]
        draw.line(pts, fill=(255, 245, 95, 255), width=5)
        draw.line(pts, fill=(72, 48, 18, 255), width=2)
    debug.save(path)


if __name__ == "__main__":
    main()
