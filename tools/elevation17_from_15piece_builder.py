#!/usr/bin/env python3
"""Convert a 15-piece terrain atlas into a 17-piece elevation atlas.

The output keeps the source 15-piece sheet untouched and appends exactly two
large side-cliff pieces. The cliff material can come from the same source or a
separate rock-wall texture.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SIDE_TEMPLATE = ROOT / "Art" / "TileSets" / "ForestTileSet" / "Tilemap_color5.png"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create a 15+2 elevation atlas from an existing 15-piece sheet.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default=None)
    parser.add_argument("--tile-size", type=int, default=64)
    parser.add_argument("--cliff-size", type=int, default=128)
    parser.add_argument("--side-template", type=Path, default=DEFAULT_SIDE_TEMPLATE)
    parser.add_argument("--wall-texture", type=Path, default=None)
    parser.add_argument("--wall-cell", default=None, help="Optional 1-based COL,ROW for a 4x4 wall atlas.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    source = Image.open(args.input).convert("RGBA")
    side_template = Image.open(args.side_template).convert("RGBA")
    wall_source = load_wall_source(args.wall_texture, args.wall_cell) if args.wall_texture else source
    name = args.name or args.input.stem.lower().replace(" ", "_")

    atlas, manifest = build_atlas(
        name,
        source,
        wall_source,
        side_template,
        args.input,
        args.wall_texture,
        args.wall_cell,
        args.side_template,
        args.tile_size,
        args.cliff_size,
    )
    atlas_path = args.output_dir / "elevation17_atlas.png"
    manifest_path = args.output_dir / "elevation17_manifest.json"
    preview_path = args.output_dir / "elevation17_preview.png"
    atlas.save(atlas_path)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_preview(preview_path, atlas, manifest)

    print(f"Wrote atlas: {atlas_path}")
    print(f"Wrote manifest: {manifest_path}")
    print(f"Wrote preview: {preview_path}")


def build_atlas(
    name: str,
    source: Image.Image,
    wall_source: Image.Image,
    side_template: Image.Image,
    source_path: Path,
    wall_texture_path: Path | None,
    wall_cell: str | None,
    side_template_path: Path,
    tile_size: int,
    cliff_size: int,
) -> tuple[Image.Image, dict]:
    if source.size != (tile_size * 4, tile_size * 4):
        raise ValueError(f"Expected a 4x4 {tile_size}px 15-piece atlas, got {source.size}")

    width = source.width + cliff_size * 2
    height = max(source.height, cliff_size)
    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    atlas.alpha_composite(source, (0, 0))

    left_cliff = make_side_cliff(wall_source, side_template, cliff_size, "left")
    right_cliff = make_side_cliff(wall_source, side_template, cliff_size, "right")
    left_x = source.width
    right_x = source.width + cliff_size
    atlas.alpha_composite(left_cliff, (left_x, 0))
    atlas.alpha_composite(right_cliff, (right_x, 0))

    assets = source_tile_assets(source, tile_size)
    assets.extend(
        [
            {
                "id": "side_cliff_left",
                "role": "side_cliff_left",
                "category": "cliff",
                "rect": {"x": left_x, "y": 0, "width": cliff_size, "height": cliff_size},
                "walkable": False,
                "climbable": False,
            },
            {
                "id": "side_cliff_right",
                "role": "side_cliff_right",
                "category": "cliff",
                "rect": {"x": right_x, "y": 0, "width": cliff_size, "height": cliff_size},
                "walkable": False,
                "climbable": False,
            },
        ]
    )
    manifest = {
        "name": name,
        "kind": "elevation_17_from_15_piece",
        "source_atlas": "elevation17_atlas.png",
        "input": str(source_path).replace("\\", "/"),
        "wall_texture": str(wall_texture_path).replace("\\", "/") if wall_texture_path else "derived_from_input_15_piece",
        "wall_cell": wall_cell,
        "side_template": str(side_template_path).replace("\\", "/"),
        "tile_width": tile_size,
        "tile_height": tile_size,
        "layout": {
            "source_15_piece": {"x": 0, "y": 0, "width": source.width, "height": source.height},
            "side_cliffs": [
                {"id": "side_cliff_left", "x": left_x, "y": 0, "width": cliff_size, "height": cliff_size},
                {"id": "side_cliff_right", "x": right_x, "y": 0, "width": cliff_size, "height": cliff_size},
            ],
        },
        "contract": {
            "piece_count": 17,
            "base": "The first 256x256 area is the original 4x4 15-piece atlas, unchanged.",
            "extra": "The two 128x128 pieces on the right are side cliff pieces generated from the selected rock-wall material.",
            "usage": "Use the 15-piece source area for floor/connectivity and the two appended cliff pieces for side elevation transitions.",
        },
        "assets": assets,
    }
    return atlas, manifest


def source_tile_assets(source: Image.Image, tile_size: int) -> list[dict]:
    assets: list[dict] = []
    alpha = source.getchannel("A")
    index = 0
    for row in range(4):
        for col in range(4):
            x = col * tile_size
            y = row * tile_size
            crop = alpha.crop((x, y, x + tile_size, y + tile_size))
            area = sum(1 for value in crop.getdata() if value > 8)
            if area == 0:
                continue
            index += 1
            assets.append(
                {
                    "id": f"source_tile_{index:02d}",
                    "role": f"source_tile_{index:02d}",
                    "category": "floor_15_piece",
                    "rect": {"x": x, "y": y, "width": tile_size, "height": tile_size},
                    "source_grid": {"column": col, "row": row},
                    "alpha_area": area,
                    "walkable": True,
                    "climbable": False,
                }
            )
    return assets


def load_wall_source(path: Path, wall_cell: str | None) -> Image.Image:
    texture = Image.open(path).convert("RGBA")
    cells = wall_cells(texture)
    if not cells:
        return texture
    if wall_cell:
        col_text, row_text = wall_cell.split(",", 1)
        col = max(1, int(col_text.strip()))
        row = max(1, int(row_text.strip()))
        index = (row - 1) * 4 + (col - 1)
        if 0 <= index < len(cells):
            return cells[index]
    return cells[0]


def wall_cells(texture: Image.Image) -> list[Image.Image]:
    width, height = texture.size
    if width == 1024 and height == 1024:
        return [texture.crop((col * 256, row * 256, (col + 1) * 256, (row + 1) * 256)) for row in range(4) for col in range(4)]
    if 1200 <= width <= 1300 and 1200 <= height <= 1300:
        starts_x = scaled_positions(width, [22, 330, 639, 948], 1254)
        starts_y = scaled_positions(height, [22, 330, 639, 948], 1254)
        cell_w = round(width * 286 / 1254)
        cell_h = round(height * 286 / 1254)
        return [texture.crop((x, y, min(width, x + cell_w), min(height, y + cell_h))) for y in starts_y for x in starts_x]
    if width % 4 == 0 and height % 4 == 0 and width >= 512 and height >= 512:
        cell_w = width // 4
        cell_h = height // 4
        return [texture.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h)) for row in range(4) for col in range(4)]
    return []


def scaled_positions(size: int, positions: list[int], source_size: int) -> list[int]:
    return [round(size * position / source_size) for position in positions]


def make_side_cliff(source: Image.Image, side_template: Image.Image, size: int, side: str) -> Image.Image:
    material = make_material_texture(source, size)
    cliff = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    mask = side_cliff_template_mask(side_template, size, side)
    shaded = apply_cliff_shading(material, mask, side)
    cliff.alpha_composite(shaded)
    draw_cliff_edges(cliff, mask, side)
    return cliff.filter(ImageFilter.UnsharpMask(radius=1.1, percent=70, threshold=3))


def make_material_texture(source: Image.Image, size: int) -> Image.Image:
    bbox = source.getchannel("A").getbbox()
    cropped = source.crop(bbox).convert("RGBA") if bbox else source.convert("RGBA")
    bg = Image.new("RGBA", cropped.size, dominant_color(cropped))
    bg.alpha_composite(cropped)
    texture = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for y in range(-17, size, cropped.height):
        for x in range(-23, size, cropped.width):
            texture.alpha_composite(bg, (x, y))
    texture = texture.crop((0, 0, size, size))
    texture = ImageEnhance.Contrast(texture).enhance(1.18)
    texture = ImageEnhance.Brightness(texture).enhance(0.92)
    return texture


def dominant_color(image: Image.Image) -> tuple[int, int, int, int]:
    pixels = [pixel for pixel in image.resize((32, 32), Image.Resampling.BILINEAR).getdata() if pixel[3] > 8]
    if not pixels:
        return (120, 120, 120, 255)
    return (
        round(sum(pixel[0] for pixel in pixels) / len(pixels)),
        round(sum(pixel[1] for pixel in pixels) / len(pixels)),
        round(sum(pixel[2] for pixel in pixels) / len(pixels)),
        255,
    )


def side_cliff_template_mask(template: Image.Image, size: int, side: str) -> Image.Image:
    x = 0 if side == "left" else 128
    mask = template.crop((x, 256, x + 128, 384)).getchannel("A")
    if mask.size != (size, size):
        mask = mask.resize((size, size), Image.Resampling.LANCZOS)
    return mask.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))


def apply_cliff_shading(texture: Image.Image, mask: Image.Image, side: str) -> Image.Image:
    shaded = Image.new("RGBA", texture.size, (0, 0, 0, 0))
    src = texture.load()
    alpha = mask.load()
    out = shaded.load()
    width, height = texture.size
    for y in range(height):
        for x in range(width):
            a = alpha[x, y]
            if a <= 8:
                continue
            r, g, b, _ = src[x, y]
            vertical = 1.02 - (y / max(1, height - 1)) * 0.28
            side_light = 1.08 - (x / max(1, width - 1)) * 0.16 if side == "left" else 0.92 + (x / max(1, width - 1)) * 0.16
            groove = 0.88 if ((x * 5 + y * 2) % 31) < 3 else 1.0
            shade = vertical * side_light * groove
            out[x, y] = (clamp(r * shade), clamp(g * shade), clamp(b * shade), a)
    return shaded


def draw_cliff_edges(cliff: Image.Image, mask: Image.Image, side: str) -> None:
    outline = mask.filter(ImageFilter.FIND_EDGES)
    dark = Image.new("RGBA", cliff.size, (18, 18, 16, 150))
    cliff.alpha_composite(Image.composite(dark, Image.new("RGBA", cliff.size, (0, 0, 0, 0)), outline))


def write_preview(path: Path, atlas: Image.Image, manifest: dict) -> None:
    checker = checkerboard(atlas.size)
    checker.alpha_composite(atlas)
    font = ImageFont.load_default()
    preview = Image.new("RGBA", (atlas.width, atlas.height + 24), (29, 31, 30, 255))
    preview.alpha_composite(checker, (0, 0))
    draw = ImageDraw.Draw(preview)
    draw.rectangle((0, 0, 255, 255), outline=(235, 235, 235, 150), width=1)
    draw.rectangle((256, 0, 383, 127), outline=(235, 235, 235, 150), width=1)
    draw.rectangle((384, 0, 511, 127), outline=(235, 235, 235, 150), width=1)
    draw.text((8, atlas.height + 6), f"{manifest['name']} | 15 source pieces + 2 side cliffs", fill=(236, 238, 234, 255), font=font)
    preview.convert("RGB").save(path)


def checkerboard(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    cell = 16
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            color = (68, 68, 68, 255) if ((x // cell) + (y // cell)) % 2 == 0 else (92, 92, 92, 255)
            draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=color)
    return image


def clamp(value: float) -> int:
    return max(0, min(255, round(value)))


if __name__ == "__main__":
    main()
