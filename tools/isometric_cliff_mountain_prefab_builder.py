#!/usr/bin/env python3
"""Build a reference-style prefab pack from the isometric cliff/mountain atlas."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


CHUNKS = [
    {
        "role": "level_0_wide_base_cliff",
        "category": "level_chunk",
        "box": (7, 569, 270, 756),
        "default_position": (88, 315),
        "height_level": 0,
        "walkable": True,
        "notes": "Wide green grey-rock base cliff from the isometric atlas.",
    },
    {
        "role": "level_1_right_plateau_cliff",
        "category": "level_chunk",
        "box": (276, 571, 450, 753),
        "default_position": (292, 238),
        "height_level": 1,
        "walkable": True,
        "notes": "Mid-height right plateau with matching mossy cliff support.",
    },
    {
        "role": "level_2_left_plateau_cliff",
        "category": "level_chunk",
        "box": (562, 571, 666, 756),
        "default_position": (226, 164),
        "height_level": 2,
        "walkable": True,
        "notes": "Narrow upper cliff pillar used as a supported level-2 landing.",
    },
    {
        "role": "level_3_castle_ready_plateau",
        "category": "castle_chunk",
        "box": (7, 0, 267, 196),
        "default_position": (310, 52),
        "height_level": 3,
        "walkable": True,
        "notes": "Highest wide castle-ready plateau; its cliff body is kept under the floor.",
    },
    {
        "role": "route_0_to_1_ramp_with_wall",
        "category": "route_chunk",
        "box": (951, 620, 1128, 756),
        "default_position": (178, 278),
        "height_level": 1,
        "from_level": 0,
        "to_level": 1,
        "walkable": True,
        "climbable": True,
        "notes": "Lower diagonal ramp chunk; the sprite includes wall height.",
    },
    {
        "role": "route_1_to_2_switchback_with_wall",
        "category": "route_chunk",
        "box": (1219, 147, 1379, 229),
        "default_position": (205, 208),
        "height_level": 2,
        "from_level": 1,
        "to_level": 2,
        "walkable": True,
        "climbable": True,
        "notes": "Switchback ramp chunk from the isometric atlas.",
    },
    {
        "role": "route_2_to_3_high_path_with_wall",
        "category": "route_chunk",
        "box": (1221, 32, 1340, 129),
        "default_position": (284, 124),
        "height_level": 3,
        "from_level": 2,
        "to_level": 3,
        "walkable": True,
        "climbable": True,
        "notes": "Upper grey cliff connector into the castle-height platform.",
    },
    {
        "role": "small_side_plateau",
        "category": "variation_chunk",
        "box": (1405, 896, 1524, 976),
        "default_position": (22, 393),
        "height_level": 0,
        "walkable": True,
        "notes": "Small side plateau for widening or alternate layouts.",
    },
    {
        "role": "small_round_column",
        "category": "variation_chunk",
        "box": (1306, 897, 1390, 976),
        "default_position": (475, 300),
        "height_level": 1,
        "walkable": True,
        "notes": "Small round cliff column for optional side levels.",
    },
]


PRESET_OFFSETS = {
    "reference": {},
    "wide": {
        "level_0_wide_base_cliff": (-50, 20),
        "small_side_plateau": (-35, 10),
        "level_1_right_plateau_cliff": (95, 18),
        "small_round_column": (80, 15),
        "level_2_left_plateau_cliff": (-35, -10),
        "route_0_to_1_ramp_with_wall": (20, 12),
        "route_1_to_2_switchback_with_wall": (-18, -4),
        "level_3_castle_ready_plateau": (24, -5),
    },
    "high_castle": {
        "level_2_left_plateau_cliff": (0, -30),
        "route_1_to_2_switchback_with_wall": (0, -18),
        "route_2_to_3_high_path_with_wall": (0, -54),
        "level_3_castle_ready_plateau": (0, -78),
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build an isometric cliff mountain prefab pack.")
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="isometric_cliff_green")
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
        sprite = crop_alpha(source, spec["box"])
        file_name = f"{args.name}_{spec['role']}.png"
        sprite.save(chunks_dir / file_name)
        x, y = spec["default_position"]
        asset = {
            "id": f"{args.name}_{spec['role']}",
            "role": spec["role"],
            "category": spec["category"],
            "file": f"prefab_chunks/{file_name}",
            "source_rect": {
                "x": spec["box"][0],
                "y": spec["box"][1],
                "width": spec["box"][2] - spec["box"][0],
                "height": spec["box"][3] - spec["box"][1],
            },
            "default_position": {"x": x, "y": y},
            "sprite_size": {"width": sprite.width, "height": sprite.height},
            "height_level": spec["height_level"],
            "from_level": spec.get("from_level"),
            "to_level": spec.get("to_level"),
            "walkable": spec.get("walkable", False),
            "climbable": spec.get("climbable", False),
            "visual_includes_wall": True,
            "notes": spec["notes"],
        }
        assets.append(asset)

    write_chunk_manifest(args.output_dir / "prefab_chunk_manifest.json", args.name, assets)
    write_chunk_atlas(args.output_dir / "prefab_chunk_atlas.png", assets, args.output_dir)
    write_chunk_atlas_preview(args.output_dir / "prefab_chunk_atlas_preview.png", assets, args.output_dir)

    prefab = compose_prefab(args.output_dir, assets, "reference")
    prefab.save(args.output_dir / "prefab.png")
    write_preview(args.output_dir / "prefab_preview.png", prefab, "REFERENCE")
    manifest = build_prefab_manifest(args.name, prefab.size, assets)
    (args.output_dir / "prefab_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_path_debug(args.output_dir / "prefab_path_debug.png", prefab, manifest)

    previews_dir = args.output_dir / "layout_previews"
    previews_dir.mkdir(exist_ok=True)
    for preset in PRESET_OFFSETS:
        image = compose_prefab(args.output_dir, assets, preset)
        image.save(previews_dir / f"{preset}.png")
        write_preview(previews_dir / f"{preset}_preview.png", image, preset.upper())

    print(f"Wrote {args.output_dir / 'prefab_chunk_manifest.json'}")
    print(f"Wrote {args.output_dir / 'prefab_preview.png'}")


def crop_alpha(source: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    crop = source.crop(box).convert("RGBA")
    bbox = crop.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    x1 = max(0, bbox[0] - 3)
    y1 = max(0, bbox[1] - 3)
    x2 = min(crop.width, bbox[2] + 3)
    y2 = min(crop.height, bbox[3] + 3)
    return crop.crop((x1, y1, x2, y2))


def compose_prefab(root: Path, assets: list[dict], preset: str) -> Image.Image:
    offsets = PRESET_OFFSETS[preset]
    placements = []
    for asset in assets:
        role = asset["role"]
        if preset == "reference" and asset["category"] == "variation_chunk":
            continue
        pos = asset["default_position"]
        dx, dy = offsets.get(role, (0, 0))
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        placements.append((asset, sprite, int(pos["x"] + dx), int(pos["y"] + dy)))

    min_x = min(x for _, _, x, _ in placements)
    min_y = min(y for _, _, _, y in placements)
    max_x = max(x + sprite.width for _, sprite, x, _ in placements)
    max_y = max(y + sprite.height for _, sprite, _, y in placements)
    pad = 28
    image = Image.new("RGBA", (max_x - min_x + pad * 2, max_y - min_y + pad * 2), (0, 0, 0, 0))
    for asset, sprite, x, y in sorted(placements, key=lambda item: sort_key(item[0])):
        image.alpha_composite(sprite, (x - min_x + pad, y - min_y + pad))
    return image


def sort_key(asset: dict) -> tuple[int, int]:
    category = asset["category"]
    cat = 8 if category == "castle_chunk" else 5 if category == "route_chunk" else 1 if category == "variation_chunk" else 0
    return int(asset["height_level"]) * 10 + cat, int(asset.get("to_level") or -1)


def write_chunk_manifest(path: Path, name: str, assets: list[dict]) -> None:
    manifest = {
        "name": f"{name}_prefab_chunks",
        "kind": "reference_style_prefab_chunk_atlas",
        "source_style": "isometric_cliff_and_mountain_tileset_atlas",
        "atlas": "prefab_chunk_atlas.png",
        "preview": "prefab_chunk_atlas_preview.png",
        "contract": {
            "prefab_way": "Use independent isometric cliff/mountain chunks, not 17-piece autotile roles.",
            "level_order": "level_0_base -> level_1_right_plateau -> level_2_left_plateau -> level_3_castle_with_support.",
            "height_rule": "Each route chunk has from_level/to_level and includes visible cliff/wall height.",
            "composition": "Layouts can create different mountains because chunks come from separate source sprites.",
        },
        "assets": assets,
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def build_prefab_manifest(name: str, size: tuple[int, int], assets: list[dict]) -> dict:
    return {
        "name": name,
        "kind": "mountain_semantic_composite_prefab",
        "variant": "isometric_cliff_green",
        "source_pack": ".",
        "prefab_image": "prefab.png",
        "prefab_chunk_atlas": "prefab_chunk_atlas.png",
        "prefab_chunk_manifest": "prefab_chunk_manifest.json",
        "size": {"width": size[0], "height": size[1]},
        "height_model": {
            "height_step_px": 48,
            "z_index_step": 10,
            "rule": "Flat plateau chunks keep one height_level. Route chunks climb from_level to to_level and include cliff height.",
        },
        "levels": [
            {"id": "base", "index": 0, "height": 0, "height_level": 0, "elevation_px": 0, "walkable_region": "level_0_entry"},
            {"id": "right_plateau", "index": 1, "height": 1, "height_level": 1, "elevation_px": 48, "walkable_region": "level_1_right_plateau"},
            {"id": "left_plateau", "index": 2, "height": 2, "height_level": 2, "elevation_px": 96, "walkable_region": "level_2_left_plateau"},
            {"id": "castle_plateau", "index": 3, "height": 3, "height_level": 3, "elevation_px": 144, "walkable_region": "level_3_castle_plateau"},
        ],
        "walkable_regions": [
            {"id": "level_0_entry", "level": 0, "height_level": 0, "elevation_px": 0, "kind": "terrace", "points": points([(70, 378), (190, 330), (310, 378), (188, 430)])},
            {"id": "level_1_right_plateau", "level": 1, "height_level": 1, "elevation_px": 48, "kind": "terrace", "points": points([(348, 292), (458, 254), (548, 300), (438, 344)])},
            {"id": "level_2_left_plateau", "level": 2, "height_level": 2, "elevation_px": 96, "kind": "terrace", "points": points([(216, 204), (304, 172), (376, 213), (291, 248)])},
            {"id": "level_3_castle_plateau", "level": 3, "height_level": 3, "elevation_px": 144, "kind": "castle_plateau", "points": points([(356, 96), (465, 58), (564, 104), (456, 148)])},
        ],
        "route_edges": [
            {"from": "base", "to": "right_plateau", "route_region": "ramp_base_to_right", "role": "route_0_to_1_ramp_with_wall", "climbable": True, "from_level": 0, "to_level": 1, "from_elevation_px": 0, "to_elevation_px": 48, "points": points([(248, 360), (332, 332), (420, 298)])},
            {"from": "right_plateau", "to": "left_plateau", "route_region": "ramp_right_to_left", "role": "route_1_to_2_switchback_with_wall", "climbable": True, "from_level": 1, "to_level": 2, "from_elevation_px": 48, "to_elevation_px": 96, "points": points([(420, 298), (356, 248), (292, 212)])},
            {"from": "left_plateau", "to": "castle_plateau", "route_region": "ramp_left_to_castle", "role": "route_2_to_3_high_path_with_wall", "climbable": True, "from_level": 2, "to_level": 3, "from_elevation_px": 96, "to_elevation_px": 144, "points": points([(292, 212), (352, 160), (456, 106)])},
        ],
        "route_regions": [
            {"id": "ramp_base_to_right", "from": "base", "to": "right_plateau", "from_level": 0, "to_level": 1, "from_elevation_px": 0, "to_elevation_px": 48, "role": "route_0_to_1_ramp_with_wall", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": points([(234, 342), (322, 300), (430, 282), (450, 315), (336, 360), (246, 386)])},
            {"id": "ramp_right_to_left", "from": "right_plateau", "to": "left_plateau", "from_level": 1, "to_level": 2, "from_elevation_px": 48, "to_elevation_px": 96, "role": "route_1_to_2_switchback_with_wall", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": points([(294, 218), (365, 220), (432, 270), (414, 310), (330, 264), (282, 238)])},
            {"id": "ramp_left_to_castle", "from": "left_plateau", "to": "castle_plateau", "from_level": 2, "to_level": 3, "from_elevation_px": 96, "to_elevation_px": 144, "role": "route_2_to_3_high_path_with_wall", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": points([(320, 150), (396, 108), (480, 104), (498, 132), (420, 174), (340, 188)])},
        ],
        "anchors": {
            "player_spawn": {"x": 185, "y": 382, "level": 0, "height_level": 0, "elevation_px": 0, "kind": "route_start"},
            "castle_anchor": {"x": 456, "y": 96, "width": 128, "height": 80, "level": 3, "height_level": 3, "elevation_px": 144, "pivot": "bottom_center", "z_index": 30},
            "plateau_exit": {"x": 456, "y": 106, "level": 3, "height_level": 3, "elevation_px": 144, "kind": "route_end"},
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
        ],
        "notes": [
            "Generated from Art/TileSets/isometric Cliff and Mountain Tileset Atlas.png.",
            "The atlas contains independent source sprites, so prefab variations are not recolored copies of one complete mountain.",
        ],
    }


def points(raw: list[tuple[int, int]]) -> list[dict]:
    return [{"x": x, "y": y} for x, y in raw]


def write_chunk_atlas(path: Path, assets: list[dict], root: Path) -> None:
    write_tiled_image(path, assets, root, transparent=True, labels=False)


def write_chunk_atlas_preview(path: Path, assets: list[dict], root: Path) -> None:
    write_tiled_image(path, assets, root, transparent=False, labels=True)


def write_tiled_image(path: Path, assets: list[dict], root: Path, transparent: bool, labels: bool) -> None:
    cell_w = 310
    cell_h = 240
    cols = 3
    rows = (len(assets) + cols - 1) // cols
    bg = (0, 0, 0, 0) if transparent else (29, 32, 31, 255)
    atlas = Image.new("RGBA", (cols * cell_w, rows * cell_h), bg)
    draw = ImageDraw.Draw(atlas)
    for index, asset in enumerate(assets):
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        thumb = sprite.copy()
        thumb.thumbnail((cell_w - 18, cell_h - (54 if labels else 18)), Image.Resampling.LANCZOS)
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        atlas.alpha_composite(thumb, (x + (cell_w - thumb.width) // 2, y + 8))
        if labels:
            draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(73, 78, 75, 255))
            draw.text((x + 8, y + cell_h - 32), asset["role"][:40], fill=(238, 241, 237, 255))
            draw.text((x + 8, y + cell_h - 16), f"H{asset['height_level']} {asset['category']}", fill=(177, 187, 179, 255))
    atlas.convert("RGBA" if transparent else "RGB").save(path)


def write_preview(path: Path, image: Image.Image, title: str) -> None:
    margin = 28
    preview = Image.new("RGBA", (image.width + margin * 2, image.height + margin * 2), (20, 35, 45, 255))
    preview.alpha_composite(image, (margin, margin))
    ImageDraw.Draw(preview).text((14, 10), f"ISOMETRIC CLIFF MOUNTAIN PREFAB - {title}", fill=(230, 236, 230, 255))
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
