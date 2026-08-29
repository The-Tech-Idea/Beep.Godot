#!/usr/bin/env python3
"""Package a generated mountain/hill atlas into developer-ready assets.

The image-generation step creates the art. This tool makes the result usable:
it cleans the background, slices separated alpha components, assigns each
sprite to a terrain category, and writes a JSON manifest for Godot/tooling.
"""

from __future__ import annotations

import argparse
import json
from collections import deque
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw, ImageFilter, ImageFont


@dataclass(frozen=True)
class RegionRule:
    category: str
    # Normalized atlas bounds: left, top, right, bottom.
    rect: tuple[float, float, float, float]
    role: str
    walkable: bool
    climbable: bool
    z_index: int
    tags: tuple[str, ...]


DEFAULT_RULES = [
    RegionRule("top_surface", (0.00, 0.00, 0.23, 0.26), "terrain_top", True, False, 0, ("ground", "top")),
    RegionRule("cliff_face", (0.22, 0.00, 0.50, 0.23), "cliff", False, False, -1, ("wall", "height")),
    RegionRule("cliff_edge_corner", (0.49, 0.00, 0.78, 0.34), "edge_corner", True, False, 1, ("edge", "corner")),
    RegionRule("rock_ground_transition", (0.78, 0.00, 1.00, 0.43), "transition", True, False, 2, ("blend", "ground")),
    RegionRule("slope_ramp", (0.00, 0.20, 0.49, 0.34), "ramp", True, True, 1, ("slope", "climbable")),
    RegionRule("base_footer", (0.00, 0.34, 0.27, 0.60), "base", False, False, -1, ("rock", "footer")),
    RegionRule("strata_overlay", (0.26, 0.33, 0.49, 0.58), "overlay", False, False, 3, ("crack", "strata")),
    RegionRule("slope_ramp", (0.49, 0.30, 0.75, 0.54), "ramp", True, True, 1, ("path", "climbable")),
    RegionRule("vegetation", (0.49, 0.43, 1.00, 0.64), "prop", False, False, 5, ("plant", "vegetation")),
    RegionRule("hill_edge", (0.00, 0.60, 0.49, 0.75), "hill_edge", True, False, 1, ("edge", "embankment")),
    RegionRule("path_cut", (0.00, 0.72, 0.50, 0.86), "path", True, True, 2, ("road", "cut", "climbable")),
    RegionRule("special_feature", (0.45, 0.64, 0.79, 1.00), "feature", False, False, 3, ("mountain", "cave", "arch")),
    RegionRule("shadow", (0.78, 0.64, 1.00, 1.00), "shadow", False, False, -10, ("shadow",)),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Slice a mountain/hill terrain atlas into a developer pack.")
    parser.add_argument("--input", required=True, type=Path, help="Source atlas PNG.")
    parser.add_argument("--output-dir", required=True, type=Path, help="Directory for the packaged output.")
    parser.add_argument("--name", default="mountain_hill", help="Pack name used in manifest IDs.")
    parser.add_argument("--alpha-threshold", type=int, default=24, help="Minimum alpha considered solid for slicing.")
    parser.add_argument("--min-area", type=int, default=600, help="Ignore tiny alpha components smaller than this pixel area.")
    parser.add_argument(
        "--merge-radius",
        type=int,
        default=0,
        help="Grow the alpha mask by this many pixels before slicing, so broken highlights become one asset.",
    )
    parser.add_argument("--padding", type=int, default=2, help="Transparent padding around sliced sprites.")
    parser.add_argument("--clean-background", action="store_true", help="Flood-fill likely checker/solid background to alpha first.")
    parser.add_argument("--no-preview", action="store_true", help="Skip writing preview.png.")
    return parser.parse_args()


def is_likely_background(r: int, g: int, b: int) -> bool:
    if max(r, g, b) > 200 and max(r, g, b) - min(r, g, b) < 28:
        return True
    if 120 <= r <= 190 and 120 <= g <= 190 and 120 <= b <= 190 and max(r, g, b) - min(r, g, b) < 20:
        return True
    if max(r, g, b) < 16:
        return True
    return False


def clean_background(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    width, height = image.size
    px = image.load()
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
        if (x, y) in seen or x < 0 or y < 0 or x >= width or y >= height:
            continue

        r, g, b, a = px[x, y]
        if a == 0:
            seen.add((x, y))
        elif is_likely_background(r, g, b):
            px[x, y] = (r, g, b, 0)
            seen.add((x, y))
        else:
            continue

        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    return image


def component_mask(image: Image.Image, alpha_threshold: int, merge_radius: int) -> Image.Image:
    mask = image.getchannel("A").point(lambda value: 255 if value > alpha_threshold else 0)
    if merge_radius <= 0:
        return mask

    filter_size = merge_radius * 2 + 1
    return mask.filter(ImageFilter.MaxFilter(filter_size))


def find_components(
    image: Image.Image,
    alpha_threshold: int,
    min_area: int,
    merge_radius: int,
) -> list[tuple[int, int, int, int, int]]:
    alpha = component_mask(image, alpha_threshold, merge_radius)
    width, height = image.size
    px = alpha.load()
    seen: set[tuple[int, int]] = set()
    components: list[tuple[int, int, int, int, int]] = []

    for y in range(height):
        for x in range(width):
            if (x, y) in seen or px[x, y] <= alpha_threshold:
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
                    if px[nx, ny] <= alpha_threshold:
                        continue
                    seen.add((nx, ny))
                    queue.append((nx, ny))

            if area >= min_area:
                components.append((min_x, min_y, max_x + 1, max_y + 1, area))

    components.sort(key=lambda item: (item[1], item[0]))
    return components


def rule_for(rect: tuple[int, int, int, int], size: tuple[int, int]) -> RegionRule:
    x1, y1, x2, y2 = rect
    width, height = size
    cx = ((x1 + x2) * 0.5) / width
    cy = ((y1 + y2) * 0.5) / height
    rect_width = x2 - x1
    rect_height = y2 - y1

    if cy < 0.16 and cx < 0.23:
        return DEFAULT_RULES[0]
    if cy < 0.18 and 0.22 <= cx < 0.49 and rect_height > rect_width * 0.65:
        return DEFAULT_RULES[1]
    if cy < 0.27 and rect_width > rect_height * 1.25 and cx < 0.48:
        return RegionRule("top_surface", (0, 0, 1, 1), "terrain_top", True, False, 0, ("ground", "top"))
    if 0.18 <= cy <= 0.42 and rect_height > rect_width * 0.75 and cx < 0.49:
        return RegionRule("slope_ramp", (0, 0, 1, 1), "ramp", True, True, 1, ("slope", "climbable"))

    for rule in DEFAULT_RULES:
        left, top, right, bottom = rule.rect
        if left <= cx <= right and top <= cy <= bottom:
            return rule

    return RegionRule("misc", (0, 0, 1, 1), "prop", False, False, 0, ("misc",))


def safe_crop(image: Image.Image, rect: tuple[int, int, int, int], padding: int) -> Image.Image:
    x1, y1, x2, y2 = rect
    crop = image.crop((x1, y1, x2, y2))
    if padding <= 0:
        return crop

    out = Image.new("RGBA", (crop.width + padding * 2, crop.height + padding * 2), (0, 0, 0, 0))
    out.alpha_composite(crop, (padding, padding))
    return out


def write_manifest(
    path: Path,
    name: str,
    atlas_path: Path,
    source_size: tuple[int, int],
    assets: list[dict],
) -> None:
    manifest = {
        "name": name,
        "kind": "mountain_hill_asset_pack",
        "source_atlas": atlas_path.name,
        "source_size": {"width": source_size[0], "height": source_size[1]},
        "categories": sorted({asset["category"] for asset in assets}),
        "assets": assets,
        "notes": [
            "Coordinates are in source atlas pixels.",
            "Sliced PNGs include transparent padding.",
            "walkable/climbable are starter gameplay hints for developers, not collision geometry.",
        ],
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def write_preview(path: Path, image: Image.Image, assets: list[dict], max_thumb_size: int = 112) -> None:
    font = ImageFont.load_default()
    grouped: dict[str, list[dict]] = {}
    for asset in assets:
        grouped.setdefault(asset["category"], []).append(asset)

    cell_width = 152
    cell_height = 152
    header_height = 24
    columns = 6
    rows = sum(((len(items) + columns - 1) // columns) + 1 for items in grouped.values())
    preview = Image.new("RGBA", (columns * cell_width, max(1, rows) * cell_height), (31, 34, 32, 255))
    draw = ImageDraw.Draw(preview)
    y = 0

    for category in sorted(grouped):
        draw.rectangle((0, y, preview.width, y + header_height), fill=(46, 51, 48, 255))
        draw.text((8, y + 6), f"{category} ({len(grouped[category])})", fill=(235, 238, 232, 255), font=font)
        y += header_height

        for index, asset in enumerate(grouped[category]):
            col = index % columns
            if col == 0 and index > 0:
                y += cell_height

            x = col * cell_width
            rect = asset["source_rect"]
            sprite = image.crop((rect["x"], rect["y"], rect["x"] + rect["width"], rect["y"] + rect["height"]))
            sprite.thumbnail((max_thumb_size, max_thumb_size), Image.Resampling.LANCZOS)
            px = x + (cell_width - sprite.width) // 2
            py = y + 8 + (max_thumb_size - sprite.height) // 2
            draw.rectangle((x + 2, y + 2, x + cell_width - 2, y + cell_height - 2), outline=(69, 76, 72, 255))
            preview.alpha_composite(sprite, (px, py))
            draw.text((x + 6, y + 128), asset["id"].rsplit("_", 2)[-2] + "_" + asset["id"].rsplit("_", 1)[-1], fill=(214, 218, 211, 255), font=font)

        y += cell_height

    preview.crop((0, 0, preview.width, y)).convert("RGB").save(path)


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    sprites_dir = args.output_dir / "sprites"
    sprites_dir.mkdir(exist_ok=True)

    image = Image.open(args.input).convert("RGBA")
    if args.clean_background:
        image = clean_background(image)

    atlas_path = args.output_dir / "atlas.png"
    image.save(atlas_path)

    components = find_components(image, args.alpha_threshold, args.min_area, args.merge_radius)
    counters: dict[str, int] = {}
    assets: list[dict] = []

    for x1, y1, x2, y2, area in components:
        rule = rule_for((x1, y1, x2, y2), image.size)
        index = counters.get(rule.category, 0) + 1
        counters[rule.category] = index
        asset_id = f"{args.name}_{rule.category}_{index:03d}"
        file_name = f"{asset_id}.png"
        sprite = safe_crop(image, (x1, y1, x2, y2), args.padding)
        sprite.save(sprites_dir / file_name)

        assets.append(
            {
                "id": asset_id,
                "category": rule.category,
                "role": rule.role,
                "file": f"sprites/{file_name}",
                "source_rect": {"x": x1, "y": y1, "width": x2 - x1, "height": y2 - y1},
                "sprite_size": {"width": sprite.width, "height": sprite.height},
                "alpha_area": area,
                "walkable": rule.walkable,
                "climbable": rule.climbable,
                "suggested_z_index": rule.z_index,
                "tags": list(rule.tags),
            }
        )

    write_manifest(args.output_dir / "manifest.json", args.name, atlas_path, image.size, assets)
    if not args.no_preview:
        write_preview(args.output_dir / "preview.png", image, assets)

    categories = {category: counters[category] for category in sorted(counters)}
    print(f"Wrote atlas: {atlas_path}")
    print(f"Wrote manifest: {args.output_dir / 'manifest.json'}")
    if not args.no_preview:
        print(f"Wrote preview: {args.output_dir / 'preview.png'}")
    print(f"Wrote sprites: {len(assets)}")
    print(f"Categories: {json.dumps(categories, sort_keys=True)}")


if __name__ == "__main__":
    main()
