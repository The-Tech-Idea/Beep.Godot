#!/usr/bin/env python3
"""Compose a reference-style mountain from an elevation17 atlas.

This targets an irregular connected mountain mass: broad lower plateau,
smaller raised areas, perimeter cliff skirt, a path up, and a top castle pad.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a reference-style mountain from an elevation17 atlas folder.")
    parser.add_argument("--elevation17-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="reference_mountain")
    parser.add_argument("--width", type=int, default=980)
    parser.add_argument("--height", type=int, default=760)
    parser.add_argument("--draw-castle", action="store_true")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    image, manifest = compose(args)
    image.save(args.output_dir / "reference_mountain.png")
    write_preview(args.output_dir / "reference_mountain_preview.png", image)
    write_debug(args.output_dir / "reference_mountain_debug.png", image, manifest)
    (args.output_dir / "reference_mountain_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Wrote mountain: {args.output_dir / 'reference_mountain.png'}")
    print(f"Wrote preview: {args.output_dir / 'reference_mountain_preview.png'}")
    print(f"Wrote debug: {args.output_dir / 'reference_mountain_debug.png'}")
    print(f"Wrote manifest: {args.output_dir / 'reference_mountain_manifest.json'}")


def compose(args: argparse.Namespace) -> tuple[Image.Image, dict]:
    data = json.loads((args.elevation17_dir / "elevation17_manifest.json").read_text(encoding="utf-8"))
    atlas = Image.open(args.elevation17_dir / data["source_atlas"]).convert("RGBA")
    tile = data["tile_width"]
    top_material = top_material_from_atlas(atlas, tile)
    wall_material = wall_material_from_manifest(data, atlas)
    canvas = Image.new("RGBA", (args.width, args.height), (0, 0, 0, 0))

    shapes = [
        {
            "id": "base",
            "level": 0,
            "rect": (110, 340, 810, 320),
            "height": 92,
            "points": [(40, 135), (135, 54), (315, 20), (555, 30), (710, 108), (766, 206), (642, 288), (430, 314), (212, 292), (58, 226)],
        },
        {
            "id": "left_rise",
            "level": 1,
            "rect": (185, 245, 330, 230),
            "height": 96,
            "points": [(20, 98), (85, 35), (190, 18), (304, 60), (316, 145), (240, 214), (116, 204), (34, 162)],
        },
        {
            "id": "right_rise",
            "level": 1,
            "rect": (510, 250, 330, 250),
            "height": 92,
            "points": [(38, 108), (128, 34), (260, 28), (316, 96), (300, 196), (214, 238), (90, 218), (20, 164)],
        },
        {
            "id": "top_castle",
            "level": 2,
            "rect": (395, 120, 330, 190),
            "height": 110,
            "points": [(44, 88), (112, 22), (260, 20), (320, 72), (292, 150), (180, 178), (70, 160)],
        },
        {
            "id": "front_knob",
            "level": 0,
            "rect": (70, 475, 220, 150),
            "height": 54,
            "points": [(22, 74), (84, 18), (170, 22), (210, 78), (174, 128), (76, 138), (14, 112)],
        },
    ]

    placements = []
    walkable = []
    for shape in sorted(shapes, key=lambda item: item["level"]):
        draw_shape(canvas, top_material, wall_material, shape)
        x, y, w, h = shape["rect"]
        walkable.append({"id": f"{shape['id']}_walkable", "kind": "walkable_top", "level": shape["level"], "x": x + 36, "y": y + 28, "width": w - 72, "height": h - 58})
        placements.append({"role": "mountain_level", "id": shape["id"], "level": shape["level"], "rect": {"x": x, "y": y, "width": w, "height": h}})

    routes = draw_reference_path(canvas, top_material, walkable)
    castle = draw_castle_pad(canvas, shapes[3]) if args.draw_castle else None
    scatter_rocks(canvas, wall_material)

    manifest = {
        "name": args.name,
        "kind": "elevation17_reference_mountain",
        "source_elevation17": str(args.elevation17_dir).replace("\\", "/"),
        "image": "reference_mountain.png",
        "walkable_regions": walkable,
        "routes": routes,
        "anchors": {
            "player_spawn": {"x": 210, "y": 570, "level": 0},
            "castle_anchor": castle,
        },
        "placements": placements,
    }
    return canvas, manifest


def draw_shape(canvas: Image.Image, top_material: Image.Image, wall_material: Image.Image, shape: dict) -> None:
    x, y, width, depth = shape["rect"]
    mask = Image.new("L", (width, depth), 0)
    ImageDraw.Draw(mask).polygon(shape["points"], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(0.55))
    wall_h = shape["height"]
    wall_mask = make_skirt_mask(mask, wall_h)
    wall = tile_texture(wall_material, width, depth + wall_h, shape["level"] * 31, shape["level"] * 19)
    wall = vertical_shade(wall)
    canvas.alpha_composite(Image.composite(wall, Image.new("RGBA", wall.size, (0, 0, 0, 0)), wall_mask), (x, y + 26))

    top = tile_texture(top_material, width, depth, shape["level"] * 41, shape["level"] * 11)
    top = top_light(top)
    canvas.alpha_composite(Image.composite(top, Image.new("RGBA", top.size, (0, 0, 0, 0)), mask), (x, y))
    draw_edge(canvas, mask, x, y)


def make_skirt_mask(mask: Image.Image, height: int) -> Image.Image:
    width, depth = mask.size
    skirt = Image.new("L", (width, depth + height), 0)
    for offset in range(height):
        shifted = Image.new("L", skirt.size, 0)
        shifted.paste(mask, (0, offset))
        skirt = Image.composite(Image.new("L", skirt.size, 245), skirt, shifted)
    top_clear = Image.new("L", skirt.size, 0)
    top_clear.paste(mask, (0, 0))
    return Image.composite(Image.new("L", skirt.size, 0), skirt, top_clear).filter(ImageFilter.GaussianBlur(0.45))


def draw_reference_path(canvas: Image.Image, material: Image.Image, walkable: list[dict]) -> list[dict]:
    points = [(182, 565), (324, 500), (410, 405), (332, 335), (465, 275), (548, 214)]
    mask = Image.new("L", canvas.size, 0)
    draw = ImageDraw.Draw(mask)
    draw.line(points, fill=160, width=28, joint="curve")
    for px, py in points:
        draw.ellipse((px - 15, py - 13, px + 15, py + 13), fill=165)
    path = Image.new("RGBA", (64, 64), (150, 126, 76, 255))
    grain = ImageEnhance.Contrast(material.convert("L")).enhance(0.35).resize((64, 64), Image.Resampling.BILINEAR)
    path_pixels = path.load()
    grain_pixels = grain.load()
    for y in range(64):
        for x in range(64):
            delta = round((grain_pixels[x, y] - 128) * 0.10)
            path_pixels[x, y] = (clamp(150 + delta), clamp(126 + delta), clamp(76 + delta), 255)
    canvas.alpha_composite(Image.composite(tile_texture(path, canvas.width, canvas.height, 5, 23), Image.new("RGBA", canvas.size, (0, 0, 0, 0)), mask.filter(ImageFilter.GaussianBlur(0.6))))
    step_draw = ImageDraw.Draw(canvas, "RGBA")
    for index in range(1, len(points) - 1):
        px, py = points[index]
        step_draw.line((px - 20, py + 8, px + 18, py - 7), fill=(56, 50, 39, 95), width=3)
    route = {"id": "main_route", "kind": "climb_route", "from_level": 0, "to_level": 2, "x": 160, "y": 190, "width": 420, "height": 400}
    walkable.append(route)
    return [route]


def draw_castle_pad(canvas: Image.Image, shape: dict) -> dict:
    x, y, width, _ = shape["rect"]
    pad_x = x + width // 2 - 96
    pad_y = y + 50
    draw = ImageDraw.Draw(canvas, "RGBA")
    draw.rounded_rectangle((pad_x, pad_y, pad_x + 192, pad_y + 82), radius=6, fill=(142, 137, 120, 230), outline=(50, 48, 42, 230), width=3)
    for gx in range(pad_x + 18, pad_x + 180, 32):
        draw.line((gx, pad_y + 8, gx, pad_y + 74), fill=(72, 70, 64, 115), width=1)
    for gy in range(pad_y + 16, pad_y + 78, 22):
        draw.line((pad_x + 10, gy, pad_x + 182, gy), fill=(72, 70, 64, 115), width=1)
    for tx in (pad_x + 12, pad_x + 154):
        draw.rectangle((tx, pad_y - 26, tx + 28, pad_y + 8), fill=(100, 96, 84, 245), outline=(36, 34, 30, 245), width=3)
    return {"x": pad_x, "y": pad_y, "width": 192, "height": 82, "level": 2}


def scatter_rocks(canvas: Image.Image, material: Image.Image) -> None:
    draw = ImageDraw.Draw(canvas, "RGBA")
    color = dominant_color(material)
    spots = [(260, 560), (390, 515), (595, 455), (690, 385), (270, 330), (455, 305), (610, 250)]
    for index, (x, y) in enumerate(spots):
        r = 7 + index % 5
        draw.ellipse((x - r, y - r // 2, x + r, y + r // 2), fill=(color[0], color[1], color[2], 160), outline=(32, 32, 30, 120))


def top_material_from_atlas(atlas: Image.Image, tile: int) -> Image.Image:
    center = atlas.crop((tile * 2, tile, tile * 3, tile * 2)).convert("RGBA")
    bg = Image.new("RGBA", center.size, dominant_color(center))
    bg.alpha_composite(center)
    return ImageEnhance.Contrast(bg).enhance(1.05)


def wall_material_from_manifest(data: dict, atlas: Image.Image) -> Image.Image:
    wall_texture = data.get("wall_texture")
    wall_cell = data.get("wall_cell")
    if wall_texture and wall_texture != "derived_from_input_15_piece":
        path = Path(wall_texture)
        if path.exists():
            texture = Image.open(path).convert("RGBA")
            cells = wall_cells(texture)
            if cells:
                index = cell_index(wall_cell)
                return fill_alpha(cells[index if 0 <= index < len(cells) else 0])
            return fill_alpha(texture)
    left = crop_asset(atlas, data, "side_cliff_left")
    right = crop_asset(atlas, data, "side_cliff_right")
    merged = Image.new("RGBA", (left.width + right.width, max(left.height, right.height)), dominant_color(left))
    merged.alpha_composite(left, (0, merged.height - left.height))
    merged.alpha_composite(right, (left.width, merged.height - right.height))
    return merged


def wall_cells(texture: Image.Image) -> list[Image.Image]:
    width, height = texture.size
    if width == 1024 and height == 1024:
        return [texture.crop((col * 256, row * 256, (col + 1) * 256, (row + 1) * 256)) for row in range(4) for col in range(4)]
    if 1200 <= width <= 1300 and 1200 <= height <= 1300:
        starts_x = [round(width * value / 1254) for value in (22, 330, 639, 948)]
        starts_y = [round(height * value / 1254) for value in (22, 330, 639, 948)]
        cell_w = round(width * 286 / 1254)
        cell_h = round(height * 286 / 1254)
        return [texture.crop((x, y, min(width, x + cell_w), min(height, y + cell_h))) for y in starts_y for x in starts_x]
    return []


def cell_index(cell: str | None) -> int:
    if not cell:
        return 0
    col_text, row_text = cell.split(",", 1)
    return (max(1, int(row_text.strip())) - 1) * 4 + (max(1, int(col_text.strip())) - 1)


def crop_asset(atlas: Image.Image, data: dict, role: str) -> Image.Image:
    asset = next(item for item in data["assets"] if item["role"] == role)
    rect = asset["rect"]
    return atlas.crop((rect["x"], rect["y"], rect["x"] + rect["width"], rect["y"] + rect["height"])).convert("RGBA")


def fill_alpha(image: Image.Image) -> Image.Image:
    bg = Image.new("RGBA", image.size, dominant_color(image))
    bg.alpha_composite(image.convert("RGBA"))
    return bg


def draw_edge(canvas: Image.Image, mask: Image.Image, x: int, y: int) -> None:
    edge = mask.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(3))
    canvas.alpha_composite(Image.composite(Image.new("RGBA", mask.size, (230, 226, 200, 90)), Image.new("RGBA", mask.size, (0, 0, 0, 0)), edge), (x, y - 1))
    lower = Image.new("L", mask.size, 0)
    lower.paste(edge, (0, 5))
    canvas.alpha_composite(Image.composite(Image.new("RGBA", mask.size, (18, 18, 15, 145)), Image.new("RGBA", mask.size, (0, 0, 0, 0)), lower), (x, y))


def tile_texture(texture: Image.Image, width: int, height: int, offset_x: int, offset_y: int) -> Image.Image:
    texture = texture.convert("RGBA")
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for y in range(-offset_y % texture.height - texture.height, height, texture.height):
        for x in range(-offset_x % texture.width - texture.width, width, texture.width):
            canvas.alpha_composite(texture, (x, y))
    return canvas.crop((0, 0, width, height))


def top_light(image: Image.Image) -> Image.Image:
    pixels = image.load()
    for y in range(image.height):
        shade = 1.07 - y / max(1, image.height - 1) * 0.11
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (clamp(r * shade), clamp(g * shade), clamp(b * shade), a)
    return image


def vertical_shade(image: Image.Image) -> Image.Image:
    image = ImageEnhance.Contrast(image).enhance(1.16)
    pixels = image.load()
    for y in range(image.height):
        shade = 0.98 - y / max(1, image.height - 1) * 0.32
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (clamp(r * shade), clamp(g * shade), clamp(b * shade), a)
    return image


def dominant_color(image: Image.Image) -> tuple[int, int, int, int]:
    pixels = [pixel for pixel in image.resize((32, 32), Image.Resampling.BILINEAR).getdata() if pixel[3] > 8]
    if not pixels:
        return (112, 112, 100, 255)
    return (
        round(sum(pixel[0] for pixel in pixels) / len(pixels)),
        round(sum(pixel[1] for pixel in pixels) / len(pixels)),
        round(sum(pixel[2] for pixel in pixels) / len(pixels)),
        255,
    )


def write_preview(path: Path, image: Image.Image) -> None:
    bg = Image.new("RGBA", image.size, (20, 31, 40, 255))
    shadow = image.getchannel("A").filter(ImageFilter.GaussianBlur(14))
    bg.alpha_composite(Image.composite(Image.new("RGBA", image.size, (0, 0, 0, 100)), Image.new("RGBA", image.size, (0, 0, 0, 0)), shadow), (18, 24))
    bg.alpha_composite(image)
    bg.convert("RGB").save(path)


def write_debug(path: Path, image: Image.Image, manifest: dict) -> None:
    bg = Image.new("RGBA", image.size, (20, 31, 40, 255))
    bg.alpha_composite(image)
    draw = ImageDraw.Draw(bg, "RGBA")
    for region in manifest["walkable_regions"]:
        color = (70, 225, 120, 255) if region["kind"] == "walkable_top" else (70, 170, 255, 255)
        draw.rectangle((region["x"], region["y"], region["x"] + region["width"], region["y"] + region["height"]), outline=color, width=3)
    castle = manifest["anchors"]["castle_anchor"]
    if castle:
        draw.rectangle((castle["x"], castle["y"], castle["x"] + castle["width"], castle["y"] + castle["height"]), outline=(255, 220, 70, 255), width=3)
    bg.convert("RGB").save(path)


def clamp(value: float) -> int:
    return max(0, min(255, round(value)))


if __name__ == "__main__":
    main()
