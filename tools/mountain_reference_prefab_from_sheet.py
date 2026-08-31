#!/usr/bin/env python3
"""Build a reference-style mountain prefab from the surviving mountain atlas sheet."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw


@dataclass(frozen=True)
class SpriteSpec:
    role: str
    category: str
    box: tuple[int, int, int, int]
    walkable: bool = False
    climbable: bool = False
    visual_includes_wall: bool = False


@dataclass(frozen=True)
class PlacementSpec:
    role: str
    x: int
    y: int
    scale: float
    z_index: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Extract a reference-style mountain prefab from an atlas sheet.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_semantic_green_large_levelled_castle")
    parser.add_argument("--extract-mode", choices=("sample-row", "full-reference", "semantic-sheet"), default="sample-row")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    sprites_dir = args.output_dir / "sprites"
    sprites_dir.mkdir(exist_ok=True)
    for stale_sprite in sprites_dir.glob(f"{args.name}_*.png"):
        stale_sprite.unlink()

    source = Image.open(args.input).convert("RGBA")
    if args.extract_mode == "semantic-sheet":
        build_from_semantic_sheet(source, args.output_dir, args.name)
        return

    mountain = (
        extract_full_reference_mountain(source)
        if args.extract_mode == "full-reference"
        else extract_green_sample_mountain(source)
    )
    sprite_path = sprites_dir / f"{args.name}_reference_mountain.png"
    mountain.save(sprite_path)
    prefab = Image.new("RGBA", mountain.size, (0, 0, 0, 0))
    prefab.alpha_composite(mountain)
    placements = [
        {
            "role": "reference_mountain_prefab",
            "asset_id": f"{args.name}_reference_mountain",
            "file": f"sprites/{sprite_path.name}",
            "position": {"x": 0, "y": 0},
            "scale": 1.0,
            "z_index": 0,
            "height_level": 0,
            "covers_height_levels": [0, 1, 2, 3],
            "walkable": False,
            "climbable": False,
        }
    ]
    castle_raise_px = 0
    prefab.save(args.output_dir / "prefab.png")

    manifest = build_manifest(args.output_dir / "prefab_manifest.json", args.name, prefab.size, placements, castle_raise_px)
    write_preview(args.output_dir / "prefab_preview.png", prefab)
    write_debug_overlay(args.output_dir / "prefab_path_debug.png", prefab, manifest)
    write_prefab_chunk_atlas(args.output_dir, args.name, prefab)
    write_manifest(args.output_dir / "prefab_manifest.json", manifest)
    print(f"Wrote prefab: {args.output_dir / 'prefab.png'}")
    print(f"Wrote preview: {args.output_dir / 'prefab_preview.png'}")
    print(f"Wrote path debug: {args.output_dir / 'prefab_path_debug.png'}")
    print(f"Wrote prefab chunk atlas: {args.output_dir / 'prefab_chunk_atlas.png'}")
    print(f"Wrote manifest: {args.output_dir / 'prefab_manifest.json'}")


def build_from_semantic_sheet(source: Image.Image, output_dir: Path, name: str) -> None:
    sprites_dir = output_dir / "sprites"
    sprite_specs = semantic_sprite_specs()
    sprite_assets: dict[str, dict] = {}
    for spec in sprite_specs:
        sprite = clean_sprite_crop(source.crop(spec.box))
        file_name = f"{name}_{spec.role}.png"
        sprite.save(sprites_dir / file_name)
        sprite_assets[spec.role] = {
            "id": f"{name}_{spec.role}",
            "role": spec.role,
            "category": spec.category,
            "file": f"sprites/{file_name}",
            "source_rect": {
                "x": spec.box[0],
                "y": spec.box[1],
                "width": spec.box[2] - spec.box[0],
                "height": spec.box[3] - spec.box[1],
            },
            "sprite_size": {"width": sprite.width, "height": sprite.height},
            "walkable": spec.walkable,
            "climbable": spec.climbable,
            "visual_includes_wall": spec.visual_includes_wall,
        }

    write_sprite_atlas(output_dir / "semantic_sprite_atlas.png", sprite_assets, output_dir)
    prefab, placements = compose_semantic_sprite_prefab(sprite_assets, output_dir)
    prefab.save(output_dir / "prefab.png")
    manifest = build_semantic_sprite_manifest(output_dir / "prefab_manifest.json", name, prefab.size, placements, sprite_assets)
    write_preview(output_dir / "prefab_preview.png", prefab)
    write_debug_overlay(output_dir / "prefab_path_debug.png", prefab, manifest)
    write_manifest(output_dir / "prefab_manifest.json", manifest)
    print(f"Wrote semantic sprite atlas: {output_dir / 'semantic_sprite_atlas.png'}")
    print(f"Wrote prefab: {output_dir / 'prefab.png'}")
    print(f"Wrote preview: {output_dir / 'prefab_preview.png'}")
    print(f"Wrote path debug: {output_dir / 'prefab_path_debug.png'}")
    print(f"Wrote manifest: {output_dir / 'prefab_manifest.json'}")


def semantic_sprite_specs() -> list[SpriteSpec]:
    return [
        SpriteSpec("top_grass_main", "top_surface", (1230, 292, 1299, 361), True),
        SpriteSpec("top_grass_alt", "top_surface", (1230, 383, 1299, 452), True),
        SpriteSpec("top_rock_grass", "top_surface", (1305, 292, 1374, 361), True),
        SpriteSpec("castle_plateau_stone", "top_surface", (1306, 383, 1375, 452), True),
        SpriteSpec("cliff_grey_wide", "cliff_wall", (307, 32, 383, 88), False, False, True),
        SpriteSpec("cliff_grey_column", "cliff_wall", (459, 33, 535, 89), False, False, True),
        SpriteSpec("hill_edge_long", "embankment", (13, 571, 142, 612), False, False, True),
        SpriteSpec("hill_edge_front", "embankment", (14, 685, 137, 732), False, False, True),
        SpriteSpec("hill_edge_round", "embankment", (151, 628, 205, 681), False, False, True),
        SpriteSpec("path_cliff_shelf_upper", "route_tile", (608, 564, 722, 621), True, True, True),
        SpriteSpec("path_switchback_large", "route_tile", (721, 561, 842, 664), True, True, True),
        SpriteSpec("path_cliff_column_right", "route_tile", (823, 563, 901, 665), True, True, True),
        SpriteSpec("path_cliff_shelf_mid", "route_tile", (608, 684, 733, 735), True, True, True),
        SpriteSpec("path_ramp_diagonal", "route_tile", (737, 665, 833, 749), True, True, True),
        SpriteSpec("path_shelf_lower_long", "route_tile", (608, 750, 737, 835), True, True, True),
        SpriteSpec("path_curve_lower", "route_tile", (732, 750, 837, 836), True, True, True),
    ]


def clean_sprite_crop(crop: Image.Image) -> Image.Image:
    crop = crop.convert("RGBA")
    pixels = crop.load()
    mask = Image.new("L", crop.size, 255)
    mask_pixels = mask.load()
    for y in range(crop.height):
        for x in range(crop.width):
            r, g, b, a = pixels[x, y]
            if a == 0 or is_label_text(r, g, b) or is_panel_frame(r, g, b):
                mask_pixels[x, y] = 0

    remove_connected_crop_background(crop, mask)
    keep_largest_foreground_component(mask)

    bbox = mask.getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))

    pad = 3
    x1 = max(0, bbox[0] - pad)
    y1 = max(0, bbox[1] - pad)
    x2 = min(crop.width, bbox[2] + pad)
    y2 = min(crop.height, bbox[3] + pad)
    sprite = crop.crop((x1, y1, x2, y2))
    sprite_mask = mask.crop((x1, y1, x2, y2))
    sprite.putalpha(sprite_mask)
    return sprite


def keep_largest_foreground_component(mask: Image.Image) -> None:
    pixels = mask.load()
    width, height = mask.size
    visited: set[tuple[int, int]] = set()
    components: list[list[tuple[int, int]]] = []

    for y in range(height):
        for x in range(width):
            if (x, y) in visited or pixels[x, y] == 0:
                continue
            stack = [(x, y)]
            visited.add((x, y))
            component: list[tuple[int, int]] = []
            while stack:
                cx, cy = stack.pop()
                component.append((cx, cy))
                for nx in (cx - 1, cx, cx + 1):
                    for ny in (cy - 1, cy, cy + 1):
                        if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in visited:
                            continue
                        if pixels[nx, ny] == 0:
                            continue
                        visited.add((nx, ny))
                        stack.append((nx, ny))
            components.append(component)

    if not components:
        return

    largest = max(components, key=len)
    keep = set(largest)
    for y in range(height):
        for x in range(width):
            if pixels[x, y] != 0 and (x, y) not in keep:
                pixels[x, y] = 0


def remove_connected_crop_background(crop: Image.Image, mask: Image.Image) -> None:
    pixels = crop.load()
    mask_pixels = mask.load()
    width, height = crop.size
    stack: list[tuple[int, int]] = []
    seen: set[tuple[int, int]] = set()

    for x in range(width):
        stack.append((x, 0))
        stack.append((x, height - 1))
    for y in range(height):
        stack.append((0, y))
        stack.append((width - 1, y))

    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= width or y >= height or (x, y) in seen:
            continue
        seen.add((x, y))
        r, g, b, a = pixels[x, y]
        if a == 0 or mask_pixels[x, y] == 0 or is_crop_background_candidate(r, g, b):
            mask_pixels[x, y] = 0
            stack.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))


def is_crop_background_candidate(r: int, g: int, b: int) -> bool:
    return max(r, g, b) < 86 or is_dark_sheet_background(r, g, b) or is_panel_frame(r, g, b)


def is_panel_frame(r: int, g: int, b: int) -> bool:
    return b > 55 and g > 30 and r < 35 and b - r > 28


def write_sprite_atlas(path: Path, assets: dict[str, dict], output_dir: Path) -> None:
    cell_w = 180
    cell_h = 150
    columns = 4
    rows = (len(assets) + columns - 1) // columns
    atlas = Image.new("RGBA", (columns * cell_w, rows * cell_h), (20, 35, 45, 255))
    draw = ImageDraw.Draw(atlas)
    for index, asset in enumerate(assets.values()):
        col = index % columns
        row = index // columns
        x = col * cell_w
        y = row * cell_h
        sprite = Image.open(output_dir / asset["file"]).convert("RGBA")
        thumb = sprite.copy()
        thumb.thumbnail((cell_w - 20, cell_h - 42), Image.Resampling.LANCZOS)
        atlas.alpha_composite(thumb, (x + (cell_w - thumb.width) // 2, y + 8))
        draw.rectangle((x + 3, y + 3, x + cell_w - 4, y + cell_h - 4), outline=(76, 91, 101, 255))
        draw.text((x + 8, y + cell_h - 30), asset["role"][:24], fill=(235, 239, 232, 255))
        draw.text((x + 8, y + cell_h - 16), asset["category"][:24], fill=(172, 186, 174, 255))
    atlas.convert("RGB").save(path)


def compose_semantic_sprite_prefab(assets: dict[str, dict], output_dir: Path) -> tuple[Image.Image, list[dict]]:
    placements = [
        PlacementSpec("cliff_grey_wide", 96, 371, 1.25, 0),
        PlacementSpec("cliff_grey_wide", 196, 374, 1.20, 0),
        PlacementSpec("path_shelf_lower_long", 74, 350, 1.45, 2),
        PlacementSpec("path_curve_lower", 198, 308, 1.16, 3),
        PlacementSpec("path_cliff_shelf_mid", 238, 282, 1.18, 4),
        PlacementSpec("path_ramp_diagonal", 292, 245, 1.00, 5),
        PlacementSpec("path_switchback_large", 312, 152, 1.10, 6),
        PlacementSpec("path_cliff_column_right", 436, 96, 1.00, 7),
        PlacementSpec("path_cliff_shelf_upper", 430, 66, 1.22, 8),
    ]
    canvas = Image.new("RGBA", (720, 560), (0, 0, 0, 0))
    manifest_placements: list[dict] = []
    for spec in placements:
        asset = assets[spec.role]
        sprite = Image.open(output_dir / asset["file"]).convert("RGBA")
        scaled = sprite.resize(
            (max(1, round(sprite.width * spec.scale)), max(1, round(sprite.height * spec.scale))),
            Image.Resampling.LANCZOS,
        )
        canvas.alpha_composite(scaled, (spec.x, spec.y))
        manifest_placements.append(
            {
                "role": spec.role,
                "asset_id": asset["id"],
                "file": asset["file"],
                "position": {"x": spec.x, "y": spec.y},
                "scale": spec.scale,
                "z_index": spec.z_index,
                "walkable": asset["walkable"],
                "climbable": asset["climbable"],
                "visual_includes_wall": asset["visual_includes_wall"],
            }
        )
    return canvas, manifest_placements


def build_semantic_sprite_manifest(
    path: Path,
    name: str,
    size: tuple[int, int],
    placements: list[dict],
    assets: dict[str, dict],
) -> dict:
    def p(points: list[tuple[float, float]]) -> list[dict]:
        return [{"x": round(x, 2), "y": round(y, 2)} for x, y in points]

    width, height = size
    return {
        "name": name,
        "kind": "mountain_semantic_composite_prefab",
        "variant": "semantic_sprite_sheet_mountain",
        "source_pack": str(path.parent).replace("\\", "/"),
        "prefab_image": "prefab.png",
        "semantic_sprite_atlas": "semantic_sprite_atlas.png",
        "size": {"width": width, "height": height},
        "source_assets": list(assets.values()),
        "levels": [
            {"id": "base", "index": 0, "height": 0, "walkable_region": "level_0_entry"},
            {"id": "lower_terrace", "index": 1, "height": 1, "walkable_region": "level_1_lower_terrace"},
            {"id": "middle_terrace", "index": 2, "height": 2, "walkable_region": "level_2_middle_terrace"},
            {"id": "upper_walk", "index": 3, "height": 3, "walkable_region": "level_3_upper_walk"},
            {"id": "castle_plateau", "index": 4, "height": 4, "walkable_region": "level_4_castle_plateau"},
        ],
        "walkable_regions": [
            {"id": "level_0_entry", "level": 0, "kind": "terrace", "points": p([(80, 362), (225, 352), (285, 385), (263, 431), (112, 441), (58, 408)])},
            {"id": "level_1_lower_terrace", "level": 1, "kind": "terrace", "points": p([(194, 316), (285, 296), (350, 322), (327, 364), (236, 384), (188, 356)])},
            {"id": "level_2_middle_terrace", "level": 2, "kind": "terrace", "points": p([(242, 287), (349, 278), (405, 306), (383, 342), (275, 348), (229, 319)])},
            {"id": "level_3_upper_walk", "level": 3, "kind": "terrace", "points": p([(314, 163), (416, 148), (491, 184), (462, 240), (352, 241), (300, 198)])},
            {"id": "level_4_castle_plateau", "level": 4, "kind": "castle_plateau", "points": p([(433, 73), (555, 67), (585, 104), (550, 143), (451, 135), (403, 101)])},
        ],
        "route_edges": [
            {"from": "base", "to": "lower_terrace", "route_region": "ramp_base_to_lower", "role": "path_shelf_lower_long", "climbable": True, "points": p([(169, 393), (207, 355), (248, 332)])},
            {"from": "lower_terrace", "to": "middle_terrace", "route_region": "ramp_lower_to_middle", "role": "path_curve_lower", "climbable": True, "points": p([(248, 332), (292, 315), (336, 294)])},
            {"from": "middle_terrace", "to": "upper_walk", "route_region": "ramp_middle_to_upper", "role": "path_switchback_large", "climbable": True, "points": p([(336, 294), (365, 242), (358, 194), (410, 176)])},
            {"from": "upper_walk", "to": "castle_plateau", "route_region": "ramp_upper_to_castle", "role": "path_cliff_column_right", "climbable": True, "points": p([(410, 176), (453, 150), (471, 112), (468, 80)])},
        ],
        "route_regions": [
            {"id": "ramp_base_to_lower", "from": "base", "to": "lower_terrace", "from_level": 0, "to_level": 1, "role": "path_shelf_lower_long", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": p([(77, 354), (235, 348), (284, 377), (264, 412), (152, 418), (80, 391)])},
            {"id": "ramp_lower_to_middle", "from": "lower_terrace", "to": "middle_terrace", "from_level": 1, "to_level": 2, "role": "path_curve_lower", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": p([(199, 317), (292, 300), (351, 322), (328, 363), (238, 379), (191, 354)])},
            {"id": "ramp_middle_to_upper", "from": "middle_terrace", "to": "upper_walk", "from_level": 2, "to_level": 3, "role": "path_switchback_large", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": p([(312, 157), (412, 151), (464, 187), (434, 237), (354, 238), (311, 207)])},
            {"id": "ramp_upper_to_castle", "from": "upper_walk", "to": "castle_plateau", "from_level": 3, "to_level": 4, "role": "path_cliff_column_right", "kind": "height_ramp_tile", "climbable": True, "walkable": True, "visual_includes_wall": True, "points": p([(438, 98), (496, 107), (499, 164), (461, 192), (433, 165), (434, 115)])},
        ],
        "anchors": {
            "player_spawn": {"x": 169, "y": 393, "level": 0, "kind": "route_start"},
            "castle_anchor": {"x": 468, "y": 80, "width": 92, "height": 76, "level": 4, "pivot": "bottom_center", "z_index": 30},
            "plateau_exit": {"x": 468, "y": 80, "level": 4, "kind": "route_end"},
        },
        "route_up": ["path_shelf_lower_long", "path_cliff_shelf_mid", "path_switchback_large", "path_cliff_column_right"],
        "placements": placements,
        "notes": [
            "Regenerated from actual atlas sprites, not from a baked reference preview.",
            "route_regions are ramp/path tiles whose visuals include cliff wall height.",
            "walkable_regions are flat terrace tops; route_regions connect their height levels.",
        ],
    }


def extract_green_sample_mountain(source: Image.Image) -> Image.Image:
    width, height = source.size
    # The surviving atlas sheet has a row of finished sample mountains at the
    # bottom. The green multi-level mountain is the left sample in that row.
    sample = source.crop((0, round(height * 0.81), round(width * 0.36), height))
    pixels = sample.load()
    mask = Image.new("L", sample.size, 0)
    mask_pixels = mask.load()

    for y in range(sample.height):
        for x in range(sample.width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            if is_dark_sheet_background(r, g, b) or is_label_text(r, g, b):
                continue
            mask_pixels[x, y] = 255

    bbox = mask.getbbox()
    if bbox is None:
        raise RuntimeError("Could not locate the green sample mountain in the source sheet.")

    pad = 6
    x1 = max(0, bbox[0] - pad)
    y1 = max(0, bbox[1] - pad)
    x2 = min(sample.width, bbox[2] + pad)
    y2 = min(sample.height, bbox[3] + pad)
    mountain = sample.crop((x1, y1, x2, y2))
    mountain_mask = mask.crop((x1, y1, x2, y2))
    mountain.putalpha(mountain_mask)
    return mountain


def extract_full_reference_mountain(source: Image.Image) -> Image.Image:
    pixels = source.load()
    mask = Image.new("L", source.size, 0)
    mask_pixels = mask.load()

    for y in range(source.height):
        for x in range(source.width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            if is_dark_sheet_background(r, g, b) or is_label_text(r, g, b):
                continue
            mask_pixels[x, y] = 255

    bbox = mask.getbbox()
    if bbox is None:
        raise RuntimeError("Could not locate mountain foreground in the reference image.")

    pad = 8
    x1 = max(0, bbox[0] - pad)
    y1 = max(0, bbox[1] - pad)
    x2 = min(source.width, bbox[2] + pad)
    y2 = min(source.height, bbox[3] + pad)
    mountain = source.crop((x1, y1, x2, y2))
    mountain_mask = mask.crop((x1, y1, x2, y2))
    mountain.putalpha(mountain_mask)
    return mountain


def compose_raised_castle_prefab(mountain: Image.Image, sprites_dir: Path, name: str) -> tuple[Image.Image, list[dict], int]:
    raise_px = 54
    width, height = mountain.size
    canvas_size = (width, height + raise_px)
    castle_polygon = [(347, 31), (478, 8), (538, 58), (501, 122), (360, 119), (305, 68)]
    castle_mask = Image.new("L", mountain.size, 0)
    ImageDraw.Draw(castle_mask).polygon(castle_polygon, fill=255)

    castle_layer = Image.new("RGBA", mountain.size, (0, 0, 0, 0))
    castle_layer.alpha_composite(mountain)
    castle_layer.putalpha(Image.composite(mountain.getchannel("A"), Image.new("L", mountain.size, 0), castle_mask))
    castle_bbox = castle_layer.getbbox() or (300, 0, 540, 130)
    castle_sprite = castle_layer.crop(castle_bbox)
    castle_file = sprites_dir / f"{name}_level_3_castle_top.png"
    castle_sprite.save(castle_file)

    base_layer = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    lowered = mountain.copy()
    lowered_alpha = lowered.getchannel("A")
    shifted_castle_mask = Image.new("L", mountain.size, 0)
    ImageDraw.Draw(shifted_castle_mask).polygon(castle_polygon, fill=255)
    lowered.putalpha(Image.composite(Image.new("L", mountain.size, 0), lowered_alpha, shifted_castle_mask))
    base_layer.alpha_composite(lowered, (0, raise_px))
    base_bbox = base_layer.getbbox() or (0, 0, width, height + raise_px)
    base_sprite = base_layer.crop(base_bbox)
    base_file = sprites_dir / f"{name}_levels_0_2_body.png"
    base_sprite.save(base_file)

    support_x, support_y = 316, 98
    support_w, support_h = 210, raise_px + 92
    rock_fill = Image.new("RGBA", (support_w, support_h), (89, 91, 82, 255))
    texture_crop = mountain.crop((360, 246, 522, 430)).resize((support_w, support_h), Image.Resampling.LANCZOS)
    rock_fill.alpha_composite(texture_crop)
    texture_crop = rock_fill
    support_mask = Image.new("L", (support_w, support_h), 0)
    ImageDraw.Draw(support_mask).polygon([(28, 0), (190, 0), (210, support_h), (0, support_h)], fill=255)
    support_alpha = Image.composite(Image.new("L", (support_w, support_h), 245), Image.new("L", (support_w, support_h), 0), support_mask)
    texture_crop.putalpha(support_alpha)
    support_draw = ImageDraw.Draw(texture_crop)
    for x in (46, 88, 131, 171):
        support_draw.line((x, 8, x - 9, support_h - 10), fill=(43, 45, 42, 150), width=3)
        support_draw.line((x + 5, 12, x - 3, support_h - 22), fill=(156, 158, 143, 90), width=1)
    support_draw.line((26, 2, 188, 2), fill=(37, 39, 36, 185), width=3)
    support_file = sprites_dir / f"{name}_level_3_castle_cliff_support.png"
    texture_crop.save(support_file)

    prefab = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    prefab.alpha_composite(base_sprite, base_bbox[:2])
    prefab.alpha_composite(texture_crop, (support_x, support_y))
    prefab.alpha_composite(castle_sprite, castle_bbox[:2])

    placements = [
        {
            "role": "mountain_body_levels_0_2",
            "asset_id": f"{name}_levels_0_2_body",
            "file": f"sprites/{base_file.name}",
            "position": {"x": base_bbox[0], "y": base_bbox[1]},
            "scale": 1.0,
            "z_index": 0,
            "height_level": 0,
            "covers_height_levels": [0, 1, 2],
            "walkable": False,
            "climbable": False,
            "visual_includes_wall": True,
        },
        {
            "role": "castle_cliff_support_level_3",
            "asset_id": f"{name}_level_3_castle_cliff_support",
            "file": f"sprites/{support_file.name}",
            "position": {"x": support_x, "y": support_y},
            "scale": 1.0,
            "z_index": 25,
            "height_level": 3,
            "walkable": False,
            "climbable": False,
            "visual_includes_wall": True,
        },
        {
            "role": "castle_top_floor_level_3",
            "asset_id": f"{name}_level_3_castle_top",
            "file": f"sprites/{castle_file.name}",
            "position": {"x": castle_bbox[0], "y": castle_bbox[1]},
            "scale": 1.0,
            "z_index": 30,
            "height_level": 3,
            "walkable": True,
            "climbable": False,
            "visual_includes_wall": False,
        },
    ]
    return prefab, placements, raise_px


def is_dark_sheet_background(r: int, g: int, b: int) -> bool:
    return b >= g >= r and b < 132 and g < 114 and r < 96 and b - r > 8


def is_label_text(r: int, g: int, b: int) -> bool:
    return r > 185 and g > 185 and b > 185


def write_preview(path: Path, prefab: Image.Image) -> None:
    margin = 28
    preview = Image.new("RGBA", (prefab.width + margin * 2, prefab.height + margin * 2), (20, 35, 45, 255))
    draw = ImageDraw.Draw(preview)
    preview.alpha_composite(prefab, (margin, margin))
    draw.text((16, 10), "REFERENCE-STYLE GREEN MOUNTAIN PREFAB", fill=(225, 232, 226, 255))
    preview.convert("RGB").save(path)


def build_manifest(path: Path, name: str, size: tuple[int, int], placements: list[dict], castle_raise_px: int) -> dict:
    width, height = size
    height_step_px = 36
    level_heights = {
        "base": 0,
        "right_plateau": 1,
        "left_plateau": 2,
        "castle_plateau": 3,
    }

    def level_entry(level_id: str, index: int, region_id: str) -> dict:
        height_level = level_heights[level_id]
        return {
            "id": level_id,
            "index": index,
            "height": height_level,
            "height_level": height_level,
            "elevation_px": height_level * height_step_px,
            "z_index": height_level * 10,
            "walkable_region": region_id,
        }

    def walkable_region(region_id: str, level_id: str, index: int, kind: str, points: list[tuple[float, float]]) -> dict:
        height_level = level_heights[level_id]
        return {
            "id": region_id,
            "level": index,
            "height_level": height_level,
            "elevation_px": height_level * height_step_px,
            "z_index": height_level * 10,
            "kind": kind,
            "points": p(points),
        }

    def route_region(
        region_id: str,
        from_id: str,
        to_id: str,
        role: str,
        points: list[tuple[float, float]],
        centerline: list[tuple[float, float]],
    ) -> dict:
        from_height = level_heights[from_id]
        to_height = level_heights[to_id]
        return {
            "id": region_id,
            "from": from_id,
            "to": to_id,
            "from_level": from_height,
            "to_level": to_height,
            "from_elevation_px": from_height * height_step_px,
            "to_elevation_px": to_height * height_step_px,
            "z_index": to_height * 10,
            "role": role,
            "kind": "height_ramp_tile",
            "climbable": True,
            "walkable": True,
            "visual_includes_wall": True,
            "points": p(points),
            "centerline": p(centerline),
        }

    def p(points: list[tuple[float, float]]) -> list[dict]:
        return [{"x": round(x, 2), "y": round(y, 2)} for x, y in points]

    def lower(points: list[tuple[float, float]]) -> list[tuple[float, float]]:
        return [(x, y + castle_raise_px) for x, y in points]

    def climb_to_castle(points: list[tuple[float, float]]) -> list[tuple[float, float]]:
        if castle_raise_px <= 0:
            return points
        count = max(1, len(points) - 1)
        return [(x, y + round(castle_raise_px * (1.0 - index / count), 2)) for index, (x, y) in enumerate(points)]

    return {
        "name": name,
        "kind": "mountain_semantic_composite_prefab",
        "variant": "reference_style_green_mountain",
        "source_pack": str(path.parent).replace("\\", "/"),
        "prefab_image": "prefab.png",
        "prefab_chunk_atlas": "prefab_chunk_atlas.png",
        "prefab_chunk_manifest": "prefab_chunk_manifest.json",
        "size": {"width": width, "height": height},
        "height_model": {
            "height_step_px": height_step_px,
            "z_index_step": 10,
            "rule": "Flat terraces keep one height_level. Ramp/path regions carry from/to elevation because their visuals include cliff height.",
        },
        "levels": [
            level_entry("base", 0, "level_0_entry"),
            level_entry("right_plateau", 1, "level_1_right_plateau"),
            level_entry("left_plateau", 2, "level_2_left_plateau"),
            level_entry("castle_plateau", 3, "level_3_castle_plateau"),
        ],
        "walkable_regions": [
            walkable_region("level_0_entry", "base", 0, "route_entry", lower([(53, 346), (138, 303), (255, 306), (352, 347), (301, 429), (105, 429)])),
            walkable_region("level_1_right_plateau", "right_plateau", 1, "terrace", lower([(334, 225), (414, 188), (531, 212), (523, 292), (408, 333), (323, 289)])),
            walkable_region("level_2_left_plateau", "left_plateau", 2, "terrace", lower([(58, 176), (145, 127), (253, 144), (273, 205), (189, 257), (62, 229)])),
            walkable_region("level_3_castle_plateau", "castle_plateau", 3, "castle_plateau", [(347, 39), (476, 17), (531, 59), (492, 104), (365, 100), (315, 66)]),
        ],
        "route_edges": [
            {
                "from": "base",
                "to": "right_plateau",
                "route_region": "ramp_base_to_right",
                "role": "ramp_straight_up",
                "climbable": True,
                "from_level": level_heights["base"],
                "to_level": level_heights["right_plateau"],
                "from_elevation_px": level_heights["base"] * height_step_px,
                "to_elevation_px": level_heights["right_plateau"] * height_step_px,
                "points": p(lower([(215, 348), (262, 319), (312, 282), (371, 239)])),
            },
            {
                "from": "right_plateau",
                "to": "left_plateau",
                "route_region": "ramp_right_to_left",
                "role": "ramp_switchback_left",
                "climbable": True,
                "from_level": level_heights["right_plateau"],
                "to_level": level_heights["left_plateau"],
                "from_elevation_px": level_heights["right_plateau"] * height_step_px,
                "to_elevation_px": level_heights["left_plateau"] * height_step_px,
                "points": p(lower([(371, 239), (323, 217), (266, 202), (192, 190)])),
            },
            {
                "from": "left_plateau",
                "to": "castle_plateau",
                "route_region": "ramp_left_to_castle",
                "role": "path_overlay",
                "climbable": True,
                "from_level": level_heights["left_plateau"],
                "to_level": level_heights["castle_plateau"],
                "from_elevation_px": level_heights["left_plateau"] * height_step_px,
                "to_elevation_px": level_heights["castle_plateau"] * height_step_px,
                "points": p(climb_to_castle([(192, 190), (251, 161), (313, 124), (379, 91), (426, 83)])),
            },
        ],
        "route_regions": [
            route_region("ramp_base_to_right", "base", "right_plateau", "ramp_straight_up", lower([(196, 352), (238, 354), (279, 319), (326, 282), (386, 248), (371, 225), (306, 253), (252, 294), (205, 326)]), lower([(215, 348), (286, 299), (371, 239)])),
            route_region("ramp_right_to_left", "right_plateau", "left_plateau", "ramp_switchback_left", lower([(362, 223), (374, 248), (318, 232), (261, 213), (190, 203), (177, 180), (249, 183), (317, 199)]), lower([(371, 239), (282, 209), (192, 190)])),
            route_region("ramp_left_to_castle", "left_plateau", "castle_plateau", "path_overlay", climb_to_castle([(177, 183), (203, 205), (264, 167), (323, 129), (381, 101), (434, 88), (418, 69), (365, 84), (302, 113), (238, 151)]), climb_to_castle([(192, 190), (313, 124), (426, 83)])),
        ],
        "anchors": {
            "player_spawn": {"x": 178, "y": 356 + castle_raise_px, "level": 0, "height_level": 0, "elevation_px": 0, "kind": "route_start"},
            "castle_anchor": {"x": 426, "y": 61, "width": 128, "height": 82, "level": 3, "height_level": level_heights["castle_plateau"], "elevation_px": level_heights["castle_plateau"] * height_step_px, "pivot": "bottom_center", "z_index": 30},
            "plateau_exit": {"x": 426, "y": 83, "level": 3, "height_level": level_heights["castle_plateau"], "elevation_px": level_heights["castle_plateau"] * height_step_px, "kind": "route_end"},
        },
        "route_up": ["ramp_straight_up", "ramp_switchback_left", "path_overlay"],
        "placements": placements,
        "notes": [
            "Regenerated from the surviving Mountain & Hill Terrain Asset Pack sheet.",
            "Visuals use the reference-style composed mountain sample split into body, castle support, and castle top layers.",
            "The castle floor is a separate highest visual layer with cliff support underneath it.",
            "The semantic levels match the marked reference: bottom floor 0, right plateau 1, left plateau 2, castle floor 3.",
            "walkable_regions describe flat level surfaces; route_regions describe climbable ramp tiles with their own height transitions.",
            "Walkable regions are polygons fitted to visible terraces, not rough rectangles.",
            "Ramp tiles are not treated as plain path lines because their visuals include cliff/wall height.",
        ],
    }


def write_prefab_chunk_atlas(output_dir: Path, name: str, prefab: Image.Image) -> None:
    sprites_dir = output_dir / "prefab_chunks"
    sprites_dir.mkdir(exist_ok=True)
    for stale in sprites_dir.glob(f"{name}_*.png"):
        stale.unlink()

    chunks = reference_prefab_chunk_specs()
    assets: list[dict] = []
    cell_w = 260
    cell_h = 210
    columns = 4
    rows = (len(chunks) + columns - 1) // columns
    atlas = Image.new("RGBA", (columns * cell_w, rows * cell_h), (0, 0, 0, 0))
    preview = Image.new("RGBA", (columns * cell_w, rows * (cell_h + 34)), (29, 32, 31, 255))
    draw = ImageDraw.Draw(preview)

    for index, spec in enumerate(chunks):
        role = spec["role"]
        sprite, source_offset = crop_prefab_chunk(prefab, spec["box"], spec.get("mask_points"))
        file_name = f"{name}_{role}.png"
        sprite.save(sprites_dir / file_name)

        col = index % columns
        row = index // columns
        slot_x = col * cell_w
        slot_y = row * cell_h
        atlas.alpha_composite(centered_thumbnail(sprite, cell_w - 18, cell_h - 18), (slot_x + 9, slot_y + 9))

        preview_y = row * (cell_h + 34)
        thumb = centered_thumbnail(sprite, cell_w - 18, cell_h - 46)
        preview.alpha_composite(thumb, (slot_x + 9, preview_y + 9))
        draw.rectangle((slot_x + 4, preview_y + 4, slot_x + cell_w - 4, preview_y + cell_h + 29), outline=(73, 78, 75, 255))
        draw.text((slot_x + 8, preview_y + cell_h - 28), role[:34], fill=(238, 241, 237, 255))
        draw.text((slot_x + 8, preview_y + cell_h - 13), f"H{spec['height_level']} {spec['category']}", fill=(177, 187, 179, 255))

        assets.append(
            {
                "id": f"{name}_{role}",
                "role": role,
                "category": spec["category"],
                "file": f"prefab_chunks/{file_name}",
            "source_rect": {"x": spec["box"][0], "y": spec["box"][1], "width": spec["box"][2] - spec["box"][0], "height": spec["box"][3] - spec["box"][1]},
                "mask_points": [{"x": x, "y": y} for x, y in spec.get("mask_points", [])],
                "default_position": {"x": source_offset[0], "y": source_offset[1]},
                "sprite_size": {"width": sprite.width, "height": sprite.height},
                "height_level": spec["height_level"],
                "from_level": spec.get("from_level"),
                "to_level": spec.get("to_level"),
                "walkable": spec.get("walkable", False),
                "climbable": spec.get("climbable", False),
                "visual_includes_wall": spec.get("visual_includes_wall", True),
                "notes": spec["notes"],
            }
        )

    atlas.save(output_dir / "prefab_chunk_atlas.png")
    preview.convert("RGB").save(output_dir / "prefab_chunk_atlas_preview.png")
    chunk_manifest = {
        "name": f"{name}_prefab_chunks",
        "kind": "reference_style_prefab_chunk_atlas",
        "source_prefab": "prefab.png",
        "source_style": "reference_style_green_mountain",
        "atlas": "prefab_chunk_atlas.png",
        "preview": "prefab_chunk_atlas_preview.png",
        "contract": {
            "prefab_way": "Use these large role chunks, not 17-piece autotile roles.",
            "level_order": "level_0_base -> level_1_right_plateau -> level_2_left_plateau -> level_3_castle_with_support.",
            "height_rule": "The castle chunk includes its visual support and is the highest chunk.",
            "composition": "The complete prefab remains prefab.png; chunks are for future generator variation and developer placement.",
        },
        "assets": assets,
    }
    (output_dir / "prefab_chunk_manifest.json").write_text(json.dumps(chunk_manifest, indent=2), encoding="utf-8")


def reference_prefab_chunk_specs() -> list[dict]:
    return [
        {
            "role": "level_0_base_with_front_cliff",
            "category": "level_chunk",
            "box": (0, 280, 360, 504),
            "mask_points": [(10, 335), (75, 310), (172, 286), (287, 300), (355, 356), (344, 468), (248, 504), (58, 475), (6, 411)],
            "height_level": 0,
            "walkable": True,
            "notes": "Bottom player entry floor with its front cliff support.",
        },
        {
            "role": "level_1_right_plateau_with_cliff",
            "category": "level_chunk",
            "box": (290, 170, 547, 380),
            "mask_points": [(292, 207), (395, 172), (530, 194), (547, 259), (520, 340), (412, 378), (314, 325), (290, 258)],
            "height_level": 1,
            "walkable": True,
            "notes": "Right plateau floor with visible cliff below it.",
        },
        {
            "role": "level_2_left_plateau_with_cliff",
            "category": "level_chunk",
            "box": (20, 96, 286, 292),
            "mask_points": [(24, 166), (124, 106), (238, 105), (286, 153), (272, 222), (190, 290), (54, 262), (20, 214)],
            "height_level": 2,
            "walkable": True,
            "notes": "Left higher plateau with cliff support.",
        },
        {
            "role": "level_3_castle_floor_with_support",
            "category": "castle_chunk",
            "box": (292, 0, 547, 160),
            "mask_points": [(306, 45), (380, 10), (497, 12), (542, 50), (531, 115), (464, 158), (352, 145), (292, 92)],
            "height_level": 3,
            "walkable": True,
            "notes": "Highest castle floor and the mountain support below it.",
        },
        {
            "role": "route_0_to_1_ramp_with_wall",
            "category": "route_chunk",
            "box": (170, 245, 390, 430),
            "mask_points": [(180, 332), (226, 292), (292, 258), (376, 245), (390, 322), (350, 398), (260, 430), (178, 407)],
            "height_level": 1,
            "from_level": 0,
            "to_level": 1,
            "walkable": True,
            "climbable": True,
            "notes": "Lower ramp chunk; includes height and wall context.",
        },
        {
            "role": "route_1_to_2_switchback_with_wall",
            "category": "route_chunk",
            "box": (150, 135, 390, 295),
            "mask_points": [(155, 178), (232, 144), (322, 135), (390, 188), (360, 252), (276, 295), (178, 255)],
            "height_level": 2,
            "from_level": 1,
            "to_level": 2,
            "walkable": True,
            "climbable": True,
            "notes": "Middle switchback ramp chunk.",
        },
        {
            "role": "route_2_to_3_high_path_with_wall",
            "category": "route_chunk",
            "box": (230, 52, 470, 215),
            "mask_points": [(238, 148), (304, 112), (380, 76), (462, 52), (470, 124), (425, 192), (318, 215), (230, 188)],
            "height_level": 3,
            "from_level": 2,
            "to_level": 3,
            "walkable": True,
            "climbable": True,
            "notes": "Final high path into the castle floor.",
        },
        {
            "role": "full_reference_mountain_prefab",
            "category": "complete_prefab",
            "box": (0, 0, 547, 504),
            "height_level": 0,
            "walkable": False,
            "notes": "Complete accepted reference-style prefab image.",
        },
    ]


def crop_prefab_chunk(
    prefab: Image.Image,
    box: tuple[int, int, int, int],
    mask_points: list[tuple[int, int]] | None = None,
) -> tuple[Image.Image, tuple[int, int]]:
    crop = prefab.crop(box).convert("RGBA")
    if mask_points:
        mask = Image.new("L", crop.size, 0)
        local_points = [(x - box[0], y - box[1]) for x, y in mask_points]
        ImageDraw.Draw(mask).polygon(local_points, fill=255)
        alpha = crop.getchannel("A")
        alpha = Image.composite(alpha, Image.new("L", crop.size, 0), mask)
        crop.putalpha(alpha)
    bbox = crop.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0)), (box[0], box[1])
    return crop.crop(bbox), (box[0] + bbox[0], box[1] + bbox[1])


def centered_thumbnail(sprite: Image.Image, width: int, height: int) -> Image.Image:
    thumb = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    copy = sprite.copy()
    copy.thumbnail((width, height), Image.Resampling.LANCZOS)
    thumb.alpha_composite(copy, ((width - copy.width) // 2, (height - copy.height) // 2))
    return thumb


def write_manifest(path: Path, manifest: dict) -> None:
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def write_debug_overlay(path: Path, prefab: Image.Image, manifest: dict) -> None:
    out = prefab.copy()
    draw = ImageDraw.Draw(out, "RGBA")
    colors = [
        (70, 185, 255, 34),
        (90, 220, 120, 34),
        (255, 210, 75, 34),
        (255, 150, 70, 34),
        (210, 100, 255, 34),
    ]
    for region in manifest["walkable_regions"]:
        points = [(point["x"], point["y"]) for point in region.get("points", [])]
        if len(points) >= 3:
            color = colors[region.get("level", 0) % len(colors)]
            outline = (color[0], color[1], color[2], 240)
            draw.polygon(points, fill=color, outline=outline)
            draw.line(points + [points[0]], fill=outline, width=3)
            cx = sum(point[0] for point in points) / len(points)
            cy = sum(point[1] for point in points) / len(points)
            draw.text((cx - 8, cy - 7), f"L{region.get('level', 0)}", fill=(255, 255, 255, 255))

    for region in manifest.get("route_regions", []):
        points = [(point["x"], point["y"]) for point in region.get("points", [])]
        if len(points) >= 3:
            draw.polygon(points, fill=(255, 215, 55, 82), outline=(80, 46, 10, 255))
            draw.line(points + [points[0]], fill=(255, 248, 156, 255), width=3)
            cx = sum(point[0] for point in points) / len(points)
            cy = sum(point[1] for point in points) / len(points)
            draw.text((cx - 17, cy - 7), f"H{region.get('from_level')}->{region.get('to_level')}", fill=(30, 22, 12, 255))

    for edge in manifest["route_edges"]:
        points = [(point["x"], point["y"]) for point in edge.get("points", [])]
        if len(points) >= 2:
            draw.line(points, fill=(255, 252, 120, 245), width=5, joint="curve")
            draw.line(points, fill=(88, 56, 20, 245), width=2, joint="curve")

    for anchor_id, anchor in manifest["anchors"].items():
        x = anchor["x"]
        y = anchor["y"]
        draw.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(255, 70, 70, 255), outline=(255, 255, 255, 255))
        draw.text((x + 7, y - 7), anchor_id, fill=(255, 255, 255, 255))

    out.save(path)


if __name__ == "__main__":
    main()
