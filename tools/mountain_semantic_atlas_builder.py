#!/usr/bin/env python3
"""Package a semantic mountain atlas for prefab/object generation.

This differs from the earlier broad slicer: the source atlas is expected to be
laid out in semantic rows, so the output manifest names each extracted sprite by
its generator role instead of guessing from texture appearance.
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
        "mountain_bodies",
        0.00,
        0.32,
        ("body_back", "body_mid", "body_front", "body_tall_peak", "body_wide_mesa"),
        "body",
        False,
        False,
        0,
        ("mountain", "mass"),
    ),
    RowSpec(
        "walkable_tops",
        0.30,
        0.51,
        (
            "top_plateau_large",
            "top_plateau_small",
            "top_terrace",
            "top_plateau_medium",
            "top_lip_low",
            "top_lip_round",
        ),
        "top",
        True,
        False,
        3,
        ("top", "walkable"),
    ),
    RowSpec(
        "cliff_walls",
        0.49,
        0.66,
        (
            "cliff_front",
            "cliff_side_left",
            "cliff_side_right",
            "cliff_corner_left",
            "cliff_corner_right",
            "cliff_column",
        ),
        "cliff",
        False,
        False,
        2,
        ("cliff", "wall"),
    ),
    RowSpec(
        "route_up",
        0.64,
        0.86,
        (
            "ramp_straight_up",
            "ramp_switchback_left",
            "ramp_switchback_right",
            "path_overlay",
            "stairs_up",
            "ledge_connector",
        ),
        "route",
        True,
        True,
        5,
        ("route", "climbable"),
    ),
    RowSpec(
        "details",
        0.84,
        1.01,
        (
            "overlay_cracks",
            "overlay_ground_patch",
            "overlay_grass_patch",
            "prop_small_rocks",
            "prop_boulder_low",
            "prop_boulder_tall",
            "prop_tree_large",
            "prop_tree_mid",
            "prop_tree_small",
            "shadow_large",
            "shadow_mid",
            "shadow_small",
        ),
        "detail",
        False,
        False,
        8,
        ("detail", "prop"),
    ),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a generator-ready semantic mountain atlas pack.")
    parser.add_argument("--input", required=True, type=Path, help="Source atlas PNG.")
    parser.add_argument("--output-dir", required=True, type=Path, help="Output directory.")
    parser.add_argument("--name", default="mountain_semantic_green", help="Manifest/asset name prefix.")
    parser.add_argument("--min-area", type=int, default=700, help="Ignore components smaller than this.")
    parser.add_argument("--alpha-threshold", type=int, default=8, help="Solid alpha threshold for slicing.")
    parser.add_argument("--padding", type=int, default=3, help="Transparent sprite padding.")
    return parser.parse_args()


def is_checker_background(r: int, g: int, b: int) -> bool:
    return max(r, g, b) >= 226 and max(r, g, b) - min(r, g, b) <= 10


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
    width, height = image.size
    pixels = alpha.load()
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

    return sorted(components, key=lambda rect: (rect[1], rect[0]))


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


def write_preview(path: Path, assets: list[dict], output_dir: Path) -> None:
    font = ImageFont.load_default()
    columns = 4
    cell_w = 260
    cell_h = 220
    rows = (len(assets) + columns - 1) // columns
    preview = Image.new("RGBA", (columns * cell_w, max(1, rows) * cell_h), (29, 32, 31, 255))
    draw = ImageDraw.Draw(preview)

    for index, asset in enumerate(assets):
        col = index % columns
        row = index // columns
        x = col * cell_w
        y = row * cell_h
        sprite = Image.open(output_dir / asset["file"]).convert("RGBA")
        sprite.thumbnail((220, 150), Image.Resampling.LANCZOS)
        draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(70, 76, 72, 255))
        preview.alpha_composite(sprite, (x + (cell_w - sprite.width) // 2, y + 12))
        draw.text((x + 10, y + 166), asset["role"], fill=(239, 241, 236, 255), font=font)
        draw.text((x + 10, y + 184), asset["category"], fill=(169, 177, 170, 255), font=font)
        if asset["climbable"]:
            draw.text((x + 10, y + 202), "climbable route", fill=(189, 221, 157, 255), font=font)

    preview.convert("RGB").save(path)


def write_manifest(path: Path, name: str, source_size: tuple[int, int], assets: list[dict]) -> None:
    roles = {asset["role"]: asset for asset in assets}
    missing_route_roles = [
        role for role in ("ramp_straight_up", "ramp_switchback_left", "ramp_switchback_right", "stairs_up")
        if role not in roles
    ]
    manifest = {
        "name": name,
        "kind": "mountain_semantic_prefab_atlas",
        "source_atlas": "atlas.png",
        "source_size": {"width": source_size[0], "height": source_size[1]},
        "required_generator_roles": {
            "body": ["body_back", "body_mid", "body_front"],
            "walkable_top": ["top_plateau_large", "top_plateau_medium"],
            "cliff": ["cliff_front", "cliff_side_left", "cliff_side_right"],
            "route_up": ["ramp_straight_up", "ramp_switchback_left", "ramp_switchback_right", "stairs_up"],
        },
        "roles": sorted(roles),
        "assets": assets,
        "validation": {
            "has_way_up": not missing_route_roles,
            "missing_route_roles": missing_route_roles,
        },
        "notes": [
            "This atlas is for assembling complete mountain prefabs, not square TileMap terrain.",
            "Route-up assets are marked climbable and should be connected from base to top plateau.",
            "The source image was generated as semantic rows; role assignment follows row order.",
        ],
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    sprites_dir = args.output_dir / "sprites"
    sprites_dir.mkdir(exist_ok=True)

    image = clean_background(Image.open(args.input))
    image.save(args.output_dir / "atlas.png")

    grouped: dict[str, list[tuple[int, int, int, int, int]]] = {row.name: [] for row in ROW_SPECS}
    for component in alpha_components(image, args.alpha_threshold, args.min_area):
        row = row_for(component, image.height)
        if row is not None:
            grouped[row.name].append(component)

    assets: list[dict] = []
    for row in ROW_SPECS:
        components = sorted(grouped[row.name], key=lambda rect: rect[0])
        for index, component in enumerate(components):
            role = row.roles[index] if index < len(row.roles) else f"{row.name}_{index + 1:02d}"
            x1, y1, x2, y2, area = component
            sprite = padded_crop(image, (x1, y1, x2, y2), args.padding)
            file_name = f"{args.name}_{role}.png"
            sprite.save(sprites_dir / file_name)
            tags = set(row.tags)
            if row.climbable:
                tags.add("route_up")
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
                    "tags": sorted(tags),
                }
            )

    write_manifest(args.output_dir / "semantic_manifest.json", args.name, image.size, assets)
    write_preview(args.output_dir / "semantic_preview.png", assets, args.output_dir)
    print(f"Wrote semantic atlas: {args.output_dir / 'atlas.png'}")
    print(f"Wrote semantic manifest: {args.output_dir / 'semantic_manifest.json'}")
    print(f"Wrote preview: {args.output_dir / 'semantic_preview.png'}")
    print(f"Wrote sprites: {len(assets)}")


if __name__ == "__main__":
    main()
