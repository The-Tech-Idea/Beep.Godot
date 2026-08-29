#!/usr/bin/env python3
"""Build a compact elevated terrain sheet using the ForestTileSet reference layout."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from collections import deque

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_TEMPLATE = ROOT / "Art" / "TileSets" / "ForestTileSet" / "Tilemap_color5.png"
DEFAULT_SURFACE = ROOT / "Art" / "textures" / "Natural Grassy Meadow Texture.png"
DEFAULT_CLIFF = ROOT / "Art" / "textures" / "Cliff Texture Atlas 4.png"
DEFAULT_PATH = ROOT / "Art" / "textures" / "Seamless Warm Brown Dirt Texture.png"

REGIONS = (
    ("top_large", "floor", 0, 0, 188, 188, True, False),
    ("top_vertical", "floor", 192, 0, 64, 188, True, False),
    ("top_horizontal", "floor", 0, 192, 188, 64, True, False),
    ("top_small", "floor", 192, 192, 64, 64, True, False),
    ("side_cliff_left", "cliff_side", 0, 256, 128, 128, False, False),
    ("side_cliff_right", "cliff_side", 128, 256, 128, 128, False, False),
    ("elevated_large", "floor_with_cliff", 320, 0, 188, 384, True, False),
    ("elevated_vertical", "floor_with_cliff", 512, 0, 64, 381, True, False),
    ("elevated_horizontal", "floor_with_cliff", 320, 192, 188, 192, True, False),
    ("elevated_small", "floor_with_cliff", 512, 192, 64, 189, True, False),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create the compact Tilemap_color5-style elevated terrain atlas.")
    parser.add_argument("--template", type=Path, default=DEFAULT_TEMPLATE)
    parser.add_argument("--surface-texture", type=Path, default=DEFAULT_SURFACE)
    parser.add_argument("--cliff-texture", type=Path, default=DEFAULT_CLIFF)
    parser.add_argument("--path-texture", type=Path, default=DEFAULT_PATH)
    parser.add_argument("--cliff-cell", default="3,2", help="1-based atlas cell for wall rock, for example 3,2.")
    parser.add_argument("--single-source", action="store_true", help="Derive floor, wall, and support material from the same cliff atlas cell.")
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_compact_green")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    template = Image.open(args.template).convert("RGBA")
    cliff = Image.open(args.cliff_texture).convert("RGBA")
    cliff_source = best_cliff_cell(cliff, args.cliff_cell)
    if args.single_source:
        surface = make_floor_material(cliff_source)
        path = make_route_material(cliff_source)
    else:
        surface = Image.open(args.surface_texture).convert("RGBA")
        path = Image.open(args.path_texture).convert("RGBA")

    atlas = render_atlas(template, surface, cliff_source)
    support_atlas, support_manifest = render_support_atlas(args, surface, cliff_source, path)
    atlas_path = args.output_dir / "compact_elevation_atlas.png"
    support_path = args.output_dir / "support_atlas.png"
    manifest_path = args.output_dir / "compact_elevation_manifest.json"
    support_manifest_path = args.output_dir / "support_manifest.json"
    preview_path = args.output_dir / "compact_elevation_preview.png"
    support_preview_path = args.output_dir / "support_preview.png"
    atlas.save(atlas_path)
    support_atlas.save(support_path)
    write_manifest(args, template.size, manifest_path)
    support_manifest_path.write_text(json.dumps(support_manifest, indent=2), encoding="utf-8")
    write_preview(preview_path, atlas)
    write_preview(support_preview_path, support_atlas)

    print(f"Wrote atlas: {atlas_path}")
    print(f"Wrote support atlas: {support_path}")
    print(f"Wrote manifest: {manifest_path}")
    print(f"Wrote support manifest: {support_manifest_path}")
    print(f"Wrote preview: {preview_path}")
    print(f"Wrote support preview: {support_preview_path}")


def render_atlas(template: Image.Image, surface: Image.Image, cliff_source: Image.Image) -> Image.Image:
    width, height = template.size
    out = Image.new("RGBA", template.size, (0, 0, 0, 0))
    surface_tile = tile_texture(surface, width, height, 0, 0)
    cliff_tile = tile_texture(cliff_source, width, height, 17, 31)
    pixels_out = out.load()
    surf = surface_tile.load()
    wall = ImageEnhance.Contrast(cliff_tile).enhance(1.05).load()

    for role, category, rx, ry, rw, rh, _, _ in REGIONS:
        mask = cleaned_region_mask(template.crop((rx, ry, rx + rw, ry + rh)))
        mask_pixels = mask.load()
        for local_y in range(rh):
            for local_x in range(rw):
                alpha = mask_pixels[local_x, local_y]
                if alpha <= 8:
                    continue

                x = rx + local_x
                y = ry + local_y
                is_cliff = region_pixel_is_cliff(role, category, local_y)
                sr, sg, sb, _ = wall[x, y] if is_cliff else surf[x, y]
                shade = 0.98 if not is_cliff else 0.82 + min(0.18, local_y / max(1, rh) * 0.18)
                if mask_boundary(mask, local_x, local_y):
                    shade *= 0.58
                pixels_out[x, y] = (clamp(sr * shade), clamp(sg * shade), clamp(sb * shade), alpha)

    return out.filter(ImageFilter.UnsharpMask(radius=1.2, percent=80, threshold=4))


def region_pixel_is_cliff(role: str, category: str, local_y: int) -> bool:
    if category in {"cliff_side"}:
        return True
    if category == "floor_with_cliff":
        if role in {"elevated_large", "elevated_vertical"}:
            return local_y >= 190
        return local_y >= 65
    return False


def cleaned_region_mask(region: Image.Image) -> Image.Image:
    raw = region.getchannel("A")
    width, height = raw.size
    pixels = raw.load()
    exterior: set[tuple[int, int]] = set()
    queue: deque[tuple[int, int]] = deque()
    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))

    while queue:
        x, y = queue.popleft()
        if x < 0 or y < 0 or x >= width or y >= height or (x, y) in exterior:
            continue
        if pixels[x, y] > 8:
            continue
        exterior.add((x, y))
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    clean = Image.new("L", (width, height), 0)
    clean_pixels = clean.load()
    for y in range(height):
        for x in range(width):
            if (x, y) not in exterior:
                clean_pixels[x, y] = 255
    return clean.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))


def mask_boundary(mask: Image.Image, x: int, y: int) -> bool:
    width, height = mask.size
    if x <= 1 or y <= 1 or x >= width - 2 or y >= height - 2:
        return True
    for ny in range(y - 2, y + 3):
        for nx in range(x - 2, x + 3):
            if mask.getpixel((nx, ny)) <= 8:
                return True
    return False


def best_cliff_cell(atlas: Image.Image, cell: str | None = None) -> Image.Image:
    width, height = atlas.size
    if width == 1024 and height == 1024:
        cells = []
        for row in range(4):
            for col in range(4):
                cells.append(atlas.crop((col * 256, row * 256, (col + 1) * 256, (row + 1) * 256)))
        return pick_cell(cells, cell, 6)
    if 1200 <= width <= 1300 and 1200 <= height <= 1300:
        starts_x = scaled_positions(width, [22, 330, 639, 948], 1254)
        starts_y = scaled_positions(height, [22, 330, 639, 948], 1254)
        cell_w = round(width * 286 / 1254)
        cell_h = round(height * 286 / 1254)
        cells = []
        for y in starts_y:
            for x in starts_x:
                cells.append(atlas.crop((x, y, min(width, x + cell_w), min(height, y + cell_h))))
        return pick_cell(cells, cell, 6)
    return atlas


def scaled_positions(size: int, positions: list[int], source_size: int) -> list[int]:
    return [round(size * position / source_size) for position in positions]


def pick_cell(cells: list[Image.Image], cell: str | None, fallback: int) -> Image.Image:
    if cell:
        col_text, row_text = cell.split(",", 1)
        col = max(1, int(col_text.strip()))
        row = max(1, int(row_text.strip()))
        index = (row - 1) * 4 + (col - 1)
        if 0 <= index < len(cells):
            return cells[index]
    return cells[min(fallback, len(cells) - 1)]


def make_floor_material(cliff_source: Image.Image) -> Image.Image:
    source = cliff_source.convert("RGBA")
    width, height = source.size
    pixels = source.load()
    total_r = total_g = total_b = count = 0
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a > 8:
                total_r += r
                total_g += g
                total_b += b
                count += 1

    if count == 0:
        return source

    base = (total_r / count, total_g / count, total_b / count)
    grain = ImageEnhance.Contrast(source.convert("L")).enhance(0.35)
    grain = grain.filter(ImageFilter.GaussianBlur(1.2))
    grain_pixels = grain.load()
    floor = Image.new("RGBA", source.size, (0, 0, 0, 255))
    out = floor.load()
    for y in range(height):
        for x in range(width):
            noise = ((x * 37 + y * 17 + (x * y) * 3) % 19) - 9
            grain_delta = (grain_pixels[x, y] - 128) * 0.16 + noise
            out[x, y] = (
                clamp(base[0] * 1.08 + grain_delta),
                clamp(base[1] * 1.08 + grain_delta),
                clamp(base[2] * 1.08 + grain_delta),
                255,
            )
    return floor.filter(ImageFilter.GaussianBlur(0.35))


def make_route_material(cliff_source: Image.Image) -> Image.Image:
    route = cliff_source.convert("RGBA")
    route = ImageEnhance.Contrast(route).enhance(0.28)
    route = ImageEnhance.Brightness(route).enhance(0.88)
    route = route.filter(ImageFilter.GaussianBlur(2.2))
    return route


def render_support_atlas(args: argparse.Namespace, surface: Image.Image, cliff_source: Image.Image, path: Image.Image) -> tuple[Image.Image, dict]:
    cell = 128
    roles = (
        ("path_horizontal", "path", True, False),
        ("path_vertical", "path", True, False),
        ("path_corner_ne", "path", True, False),
        ("path_corner_nw", "path", True, False),
        ("ramp_up", "route", True, True),
        ("stairs_up", "route", True, True),
        ("cliff_column", "cliff", False, False),
        ("rock_boulder", "prop", False, False),
        ("grass_patch", "prop", False, False),
        ("castle_foundation", "foundation", True, False),
    )
    columns = 5
    rows = 2
    atlas = Image.new("RGBA", (columns * cell, rows * cell), (0, 0, 0, 0))
    assets = []
    for index, (role, category, walkable, climbable) in enumerate(roles):
        sprite = make_support_sprite(role, surface, cliff_source, path, cell)
        x = (index % columns) * cell
        y = (index // columns) * cell
        atlas.alpha_composite(sprite, (x, y))
        assets.append(
            {
                "id": role,
                "category": category,
                "rect": {"x": x, "y": y, "width": cell, "height": cell},
                "walkable": walkable,
                "climbable": climbable,
            }
        )
    manifest = {
        "name": f"{args.name}_support",
        "kind": "compact_reference_support_atlas",
        "source_atlas": "support_atlas.png",
        "tile_width": cell,
        "tile_height": cell,
        "cliff_texture": str(args.cliff_texture).replace("\\", "/"),
        "single_source": args.single_source,
        "cliff_cell": args.cliff_cell,
        "surface_texture": "derived_from_cliff_cell" if args.single_source else str(args.surface_texture).replace("\\", "/"),
        "path_texture": "derived_from_cliff_cell" if args.single_source else str(args.path_texture).replace("\\", "/"),
        "assets": assets,
    }
    return atlas, manifest


def make_support_sprite(role: str, surface: Image.Image, cliff: Image.Image, path: Image.Image, size: int) -> Image.Image:
    sprite = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    base = tile_texture(surface, size, size, 11, 23)
    dirt = ImageEnhance.Contrast(tile_texture(path, size, size, 29, 7)).enhance(1.08)
    rock = ImageEnhance.Contrast(tile_texture(cliff, size, size, 5, 17)).enhance(1.08)
    draw_mask = Image.new("L", (size, size), 0)
    mask_draw = ImageDraw.Draw(draw_mask)
    if role == "path_horizontal":
        sprite.alpha_composite(base)
        mask_draw.rounded_rectangle((0, 42, size, 86), radius=18, fill=235)
        sprite.alpha_composite(Image.composite(dirt, Image.new("RGBA", (size, size), (0, 0, 0, 0)), draw_mask))
    elif role == "path_vertical":
        sprite.alpha_composite(base)
        mask_draw.rounded_rectangle((42, 0, 86, size), radius=18, fill=235)
        sprite.alpha_composite(Image.composite(dirt, Image.new("RGBA", (size, size), (0, 0, 0, 0)), draw_mask))
    elif role == "path_corner_ne":
        sprite.alpha_composite(base)
        mask_draw.pieslice((30, 30, 138, 138), 180, 270, fill=235)
        mask_draw.rectangle((64, 42, size, 86), fill=235)
        mask_draw.rectangle((42, 0, 86, 64), fill=235)
        sprite.alpha_composite(Image.composite(dirt, Image.new("RGBA", (size, size), (0, 0, 0, 0)), draw_mask))
    elif role == "path_corner_nw":
        sprite.alpha_composite(base)
        mask_draw.pieslice((-10, 30, 98, 138), 270, 360, fill=235)
        mask_draw.rectangle((0, 42, 64, 86), fill=235)
        mask_draw.rectangle((42, 0, 86, 64), fill=235)
        sprite.alpha_composite(Image.composite(dirt, Image.new("RGBA", (size, size), (0, 0, 0, 0)), draw_mask))
    elif role == "ramp_up":
        sprite.alpha_composite(rock)
        mask_draw.polygon(((18, 104), (110, 104), (82, 26), (46, 26)), fill=245)
        sprite.alpha_composite(Image.composite(dirt, Image.new("RGBA", (size, size), (0, 0, 0, 0)), draw_mask))
    elif role == "stairs_up":
        sprite.alpha_composite(rock)
        for y in range(30, 102, 14):
            ImageDraw.Draw(sprite, "RGBA").rectangle((28, y, 100, y + 5), fill=(210, 195, 160, 190))
    elif role == "cliff_column":
        sprite.alpha_composite(rock)
    elif role == "rock_boulder":
        mask_draw.ellipse((22, 34, 106, 104), fill=255)
        sprite.alpha_composite(Image.composite(rock, Image.new("RGBA", (size, size), (0, 0, 0, 0)), draw_mask))
    elif role == "grass_patch":
        mask_draw.ellipse((18, 34, 110, 96), fill=220)
        sprite.alpha_composite(Image.composite(base, Image.new("RGBA", (size, size), (0, 0, 0, 0)), draw_mask))
    elif role == "castle_foundation":
        sprite.alpha_composite(rock)
        ImageDraw.Draw(sprite, "RGBA").rectangle((0, 0, size - 1, size - 1), outline=(32, 34, 32, 180), width=3)
    return sprite


def tile_texture(texture: Image.Image, width: int, height: int, offset_x: int, offset_y: int) -> Image.Image:
    texture = texture.convert("RGBA")
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for y in range(-offset_y % texture.height - texture.height, height, texture.height):
        for x in range(-offset_x % texture.width - texture.width, width, texture.width):
            canvas.alpha_composite(texture, (x, y))
    return canvas.crop((0, 0, width, height)).filter(ImageFilter.GaussianBlur(0.1))


def write_manifest(args: argparse.Namespace, size: tuple[int, int], path: Path) -> None:
    manifest = {
        "name": args.name,
        "kind": "compact_reference_elevation_atlas",
        "source_atlas": "compact_elevation_atlas.png",
        "template": str(args.template).replace("\\", "/"),
        "cliff_texture": str(args.cliff_texture).replace("\\", "/"),
        "single_source": args.single_source,
        "cliff_cell": args.cliff_cell,
        "surface_texture": "derived_from_cliff_cell" if args.single_source else str(args.surface_texture).replace("\\", "/"),
        "size": {"width": size[0], "height": size[1]},
        "contract": {
            "layout": "Matches ForestTileSet/Tilemap_color5.png: compact elevation sheet, not an equal-cell atlas.",
            "base_tiles": "Large top, vertical strip, horizontal strip, small corner/cap, two side cliff pieces.",
            "elevated_tiles": "Same top pieces with cliff columns attached below.",
            "usage": "Developer expands floors by repeating the large/strip/corner regions and uses elevated regions when a visible cliff wall is needed.",
        },
        "regions": [
            {
                "id": role,
                "category": category,
                "rect": {"x": x, "y": y, "width": w, "height": h},
                "walkable": walkable,
                "climbable": climbable,
            }
            for role, category, x, y, w, h, walkable, climbable in REGIONS
        ],
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def write_preview(path: Path, atlas: Image.Image) -> None:
    bg = Image.new("RGBA", atlas.size, (0, 0, 0, 255))
    bg.alpha_composite(atlas)
    bg.convert("RGB").save(path)


def clamp(value: float) -> int:
    return max(0, min(255, round(value)))


if __name__ == "__main__":
    main()
