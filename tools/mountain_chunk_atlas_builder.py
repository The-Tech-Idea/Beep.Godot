#!/usr/bin/env python3
"""Package a hand-painted mountain chunk atlas.

Chunk atlases trade perfect tile repetition for better-looking mountains. Each
role is a larger connected terrace, route, or castle platform that the generator
can combine by requested level widths.
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


ROWS = [
    RowSpec("terraces", 0.00, 0.27, ("terrace_w3", "terrace_w5", "terrace_w7", "terrace_w9"), "terrace", True, False),
    RowSpec(
        "extensions",
        0.25,
        0.45,
        ("left_extension", "right_extension", "front_bulge", "back_cliff", "side_ledge_narrow", "side_ledge_wide"),
        "extension",
        True,
        False,
    ),
    RowSpec(
        "routes",
        0.43,
        0.66,
        ("bottom_entry_path", "long_zigzag_ramp", "left_switchback_ramp", "right_switchback_ramp", "stair_climb", "top_landing_path"),
        "route",
        True,
        True,
    ),
    RowSpec(
        "castle",
        0.64,
        0.80,
        ("castle_plateau_w4", "castle_plateau_w6", "flat_foundation", "cliff_under_castle", "front_castle_lip", "side_castle_lip"),
        "castle",
        True,
        False,
    ),
    RowSpec(
        "details",
        0.78,
        1.01,
        ("dirt_path_overlay", "grass_patch_overlay", "grass_patch_small", "cracks_a", "cracks_b", "boulder_large", "boulder_tall", "small_rocks", "conifer_a", "conifer_b", "conifer_c", "conifer_d", "bush_a", "bush_b", "shadow_a", "shadow_b", "shadow_c", "shadow_d"),
        "detail",
        False,
        False,
    ),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a chunk atlas manifest from a generated image.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_chunk_green")
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
    grouped: dict[str, list[tuple[int, int, int, int, int]]] = {row.name: [] for row in ROWS}
    for component in components(image, args.alpha_threshold, args.min_area):
        row = row_for(component, image.height)
        if row:
            grouped[row.name].append(component)

    assets: list[dict] = []
    for row in ROWS:
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
                    "file": f"sprites/{file_name}",
                    "source_rect": {"x": x1, "y": y1, "width": x2 - x1, "height": y2 - y1},
                    "sprite_size": {"width": sprite.width, "height": sprite.height},
                    "alpha_area": area,
                    "walkable": row.walkable,
                    "climbable": row.climbable,
                }
            )

    manifest = {
        "name": args.name,
        "kind": "mountain_level_chunk_atlas",
        "source_atlas": "atlas.png",
        "source_size": {"width": image.width, "height": image.height},
        "roles": [asset["role"] for asset in assets],
        "assets": assets,
        "generator_contract": {
            "width_control": "Choose the nearest terrace_w3, terrace_w5, terrace_w7, terrace_w9, or castle_plateau_w4/w6 for top levels.",
            "level_control": "Stack one large terrace chunk per level, with route chunks connecting adjacent levels.",
            "visual_priority": "Prefer whole chunks over repeated tiles to avoid a blocky look.",
        },
    }
    (args.output_dir / "chunk_atlas_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_preview(args.output_dir / "chunk_atlas_preview.png", args.output_dir, assets)
    print(f"Wrote atlas: {args.output_dir / 'atlas.png'}")
    print(f"Wrote manifest: {args.output_dir / 'chunk_atlas_manifest.json'}")
    print(f"Wrote preview: {args.output_dir / 'chunk_atlas_preview.png'}")
    print(f"Wrote sprites: {len(assets)}")


def clean_background(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    width, height = image.size
    pixels = image.load()
    queue: deque[tuple[int, int]] = deque()
    seen: set[tuple[int, int]] = set()
    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))
    while queue:
        x, y = queue.popleft()
        if x < 0 or y < 0 or x >= width or y >= height or (x, y) in seen:
            continue
        r, g, b, a = pixels[x, y]
        if a == 0 or (max(r, g, b) >= 226 and max(r, g, b) - min(r, g, b) <= 12):
            pixels[x, y] = (r, g, b, 0)
            seen.add((x, y))
            queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return image


def components(image: Image.Image, alpha_threshold: int, min_area: int) -> list[tuple[int, int, int, int, int]]:
    alpha = image.getchannel("A")
    pixels = alpha.load()
    width, height = image.size
    seen: set[tuple[int, int]] = set()
    found: list[tuple[int, int, int, int, int]] = []
    for y in range(height):
        for x in range(width):
            if (x, y) in seen or pixels[x, y] <= alpha_threshold:
                continue
            queue = deque([(x, y)])
            seen.add((x, y))
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
                    if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in seen:
                        continue
                    if pixels[nx, ny] <= alpha_threshold:
                        continue
                    seen.add((nx, ny))
                    queue.append((nx, ny))
            if area >= min_area:
                found.append((min_x, min_y, max_x + 1, max_y + 1, area))
    return sorted(found, key=lambda item: (item[1], item[0]))


def row_for(rect: tuple[int, int, int, int, int], height: int) -> RowSpec | None:
    _, y1, _, y2, _ = rect
    center = ((y1 + y2) * 0.5) / height
    for row in ROWS:
        if row.top <= center < row.bottom:
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
    cell_w = 238
    cell_h = 196
    rows = (len(assets) + columns - 1) // columns
    preview = Image.new("RGBA", (columns * cell_w, max(1, rows) * cell_h), (29, 32, 31, 255))
    draw = ImageDraw.Draw(preview)
    for index, asset in enumerate(assets):
        col = index % columns
        row = index // columns
        x = col * cell_w
        y = row * cell_h
        sprite = Image.open(output_dir / asset["file"]).convert("RGBA")
        sprite.thumbnail((206, 126), Image.Resampling.LANCZOS)
        preview.alpha_composite(sprite, (x + (cell_w - sprite.width) // 2, y + 10))
        draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(70, 76, 72, 255))
        draw.text((x + 8, y + 142), asset["role"], fill=(237, 240, 235, 255), font=font)
        draw.text((x + 8, y + 160), asset["category"], fill=(170, 178, 171, 255), font=font)
    preview.convert("RGB").save(path)


if __name__ == "__main__":
    main()
