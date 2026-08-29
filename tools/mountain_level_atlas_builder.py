#!/usr/bin/env python3
"""Package a modular mountain level atlas.

This atlas contract is for procedural 2D level construction. It names repeatable
platform, cliff, route, transition, and detail modules so a developer can choose
how many elevation levels to build and how wide each level should be.
"""

from __future__ import annotations

import argparse
import json
from collections import deque
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


@dataclass(frozen=True)
class RowSpec:
    name: str
    top: float
    bottom: float
    roles: tuple[str, ...]
    category: str
    walkable: bool
    climbable: bool
    z_index: int
    tags: tuple[str, ...]


ROW_SPECS = [
    RowSpec(
        "platform_modules",
        0.00,
        0.22,
        ("top_left_end", "top_middle_repeat", "top_right_end", "top_inner_fill", "front_lip_repeat", "back_lip_repeat"),
        "platform",
        True,
        False,
        10,
        ("level", "platform", "walkable"),
    ),
    RowSpec(
        "cliff_modules",
        0.20,
        0.43,
        ("cliff_left_side", "cliff_middle_repeat", "cliff_right_side", "cliff_front_corner_left", "cliff_front_corner_right", "vertical_column_repeat"),
        "cliff",
        False,
        False,
        0,
        ("cliff", "support"),
    ),
    RowSpec(
        "route_modules",
        0.42,
        0.65,
        ("bottom_entry_ramp", "ramp_up_left_to_right", "ramp_up_right_to_left", "switchback_landing_left", "switchback_landing_right", "stair_ramp_up"),
        "route",
        True,
        True,
        20,
        ("route", "climbable"),
    ),
    RowSpec(
        "transition_modules",
        0.64,
        0.82,
        ("small_ledge_left", "small_ledge_middle", "small_ledge_right", "plateau_connector", "bridgeable_gap_edge_left", "bridgeable_gap_edge_right"),
        "transition",
        True,
        True,
        18,
        ("ledge", "connector"),
    ),
    RowSpec(
        "detail_modules",
        0.80,
        1.01,
        ("dirt_path_overlay", "grass_patch_overlay", "rock_cracks_a", "rock_cracks_b", "boulder_large", "boulder_small", "small_rocks", "conifer_small", "conifer_large", "shadow_soft"),
        "detail",
        False,
        False,
        30,
        ("detail",),
    ),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a modular level atlas manifest from a generated image.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_level_green")
    parser.add_argument("--min-area", type=int, default=900)
    parser.add_argument("--alpha-threshold", type=int, default=8)
    parser.add_argument("--padding", type=int, default=3)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    sprites_dir = args.output_dir / "sprites"
    sprites_dir.mkdir(exist_ok=True)

    image = clean_background(Image.open(args.input))
    image.save(args.output_dir / "atlas.png")
    components = alpha_components(image, args.alpha_threshold, args.min_area)
    grouped: dict[str, list[tuple[int, int, int, int, int]]] = {row.name: [] for row in ROW_SPECS}
    for component in components:
        row = row_for(component, image.height)
        if row:
            grouped[row.name].append(component)

    assets: list[dict] = []
    for row in ROW_SPECS:
        for index, rect in enumerate(sorted(grouped[row.name], key=lambda item: item[0])):
            role = row.roles[index] if index < len(row.roles) else f"{row.name}_{index + 1:02d}"
            x1, y1, x2, y2, area = rect
            sprite = padded_crop(image, (x1, y1, x2, y2), args.padding)
            file_name = f"{args.name}_{role}.png"
            sprite.save(sprites_dir / file_name)
            assets.append(
                {
                    "id": f"{args.name}_{role}",
                    "role": role,
                    "category": row.category,
                    "row": row.name,
                    "file": f"sprites/{file_name}",
                    "source_rect": {"x": x1, "y": y1, "width": x2 - x1, "height": y2 - y1},
                    "sprite_size": {"width": sprite.width, "height": sprite.height},
                    "alpha_area": area,
                    "walkable": row.walkable,
                    "climbable": row.climbable,
                    "suggested_z_index": row.z_index,
                    "tags": list(row.tags),
                }
            )

    manifest = {
        "name": args.name,
        "kind": "mountain_modular_level_atlas",
        "source_atlas": "atlas.png",
        "source_size": {"width": image.width, "height": image.height},
        "roles": [asset["role"] for asset in assets],
        "assets": assets,
        "generator_contract": {
            "level_width_control": "Repeat top_middle_repeat and cliff_middle_repeat between left/right caps.",
            "level_count_control": "Add one platform row per requested level.",
            "route_control": "Connect adjacent levels with ramp_up_left_to_right or ramp_up_right_to_left and switchback landings.",
            "castle_control": "Place castle on the highest level's castle_anchor.",
        },
        "required_roles": [
            "top_left_end",
            "top_middle_repeat",
            "top_right_end",
            "cliff_left_side",
            "cliff_middle_repeat",
            "cliff_right_side",
            "bottom_entry_ramp",
            "ramp_up_left_to_right",
            "ramp_up_right_to_left",
        ],
    }
    (args.output_dir / "level_atlas_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_preview(args.output_dir / "level_atlas_preview.png", args.output_dir, assets)
    print(f"Wrote atlas: {args.output_dir / 'atlas.png'}")
    print(f"Wrote manifest: {args.output_dir / 'level_atlas_manifest.json'}")
    print(f"Wrote preview: {args.output_dir / 'level_atlas_preview.png'}")
    print(f"Wrote sprites: {len(assets)}")


def is_checker_background(r: int, g: int, b: int) -> bool:
    return max(r, g, b) >= 226 and max(r, g, b) - min(r, g, b) <= 12


def clean_background(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    width, height = image.size
    pixels = image.load()
    queue: deque[tuple[int, int]] = deque()
    visited: set[tuple[int, int]] = set()
    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))
    while queue:
        x, y = queue.popleft()
        if x < 0 or y < 0 or x >= width or y >= height or (x, y) in visited:
            continue
        r, g, b, a = pixels[x, y]
        if a == 0 or is_checker_background(r, g, b):
            pixels[x, y] = (r, g, b, 0)
            visited.add((x, y))
            queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return image


def alpha_components(image: Image.Image, alpha_threshold: int, min_area: int) -> list[tuple[int, int, int, int, int]]:
    alpha = image.getchannel("A")
    pixels = alpha.load()
    width, height = image.size
    visited: set[tuple[int, int]] = set()
    components: list[tuple[int, int, int, int, int]] = []
    for y in range(height):
        for x in range(width):
            if (x, y) in visited or pixels[x, y] <= alpha_threshold:
                continue
            queue = deque([(x, y)])
            visited.add((x, y))
            min_x = max_x = x
            min_y = max_y = y
            area = 0
            while queue:
                cx, cy = queue.popleft()
                area += 1
                min_x = min(min_x, cx)
                max_x = max(max_x, cx)
                min_y = min(min_y, cy)
                max_y = max(max_y, cy)
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in visited:
                        continue
                    if pixels[nx, ny] <= alpha_threshold:
                        continue
                    visited.add((nx, ny))
                    queue.append((nx, ny))
            if area >= min_area:
                components.append((min_x, min_y, max_x + 1, max_y + 1, area))
    return sorted(components, key=lambda item: (item[1], item[0]))


def row_for(rect: tuple[int, int, int, int, int], image_height: int) -> RowSpec | None:
    _, y1, _, y2, _ = rect
    center_y = ((y1 + y2) * 0.5) / image_height
    for row in ROW_SPECS:
        if row.top <= center_y < row.bottom:
            return row
    return None


def padded_crop(image: Image.Image, rect: tuple[int, int, int, int], padding: int) -> Image.Image:
    x1, y1, x2, y2 = rect
    crop = image.crop((x1, y1, x2, y2))
    out = Image.new("RGBA", (crop.width + padding * 2, crop.height + padding * 2), (0, 0, 0, 0))
    out.alpha_composite(crop, (padding, padding))
    return out


def write_preview(path: Path, output_dir: Path, assets: list[dict]) -> None:
    font = ImageFont.load_default()
    columns = 5
    cell_w = 230
    cell_h = 192
    rows = (len(assets) + columns - 1) // columns
    preview = Image.new("RGBA", (columns * cell_w, max(1, rows) * cell_h), (29, 32, 31, 255))
    draw = ImageDraw.Draw(preview)
    for index, asset in enumerate(assets):
        col = index % columns
        row = index // columns
        x = col * cell_w
        y = row * cell_h
        sprite = Image.open(output_dir / asset["file"]).convert("RGBA")
        sprite.thumbnail((196, 122), Image.Resampling.LANCZOS)
        preview.alpha_composite(sprite, (x + (cell_w - sprite.width) // 2, y + 10))
        draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(70, 76, 72, 255))
        draw.text((x + 8, y + 138), asset["role"], fill=(237, 240, 235, 255), font=font)
        draw.text((x + 8, y + 156), asset["category"], fill=(170, 178, 171, 255), font=font)
    preview.convert("RGB").save(path)


if __name__ == "__main__":
    main()
