#!/usr/bin/env python3
"""Compose a clean mountain prefab from an elevation17 asset pack."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a clean layered mountain from an elevation17 atlas folder.")
    parser.add_argument("--elevation17-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="clean_mountain")
    parser.add_argument("--levels", default="760x190,600x155,440x125,280x96", help="Bottom-to-top WIDTHxDEPTH list.")
    parser.add_argument("--level-rise", type=int, default=118)
    parser.add_argument("--wall-height", type=int, default=118)
    parser.add_argument("--draw-castle", action="store_true")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    image, manifest = compose(args, parse_levels(args.levels))
    image.save(args.output_dir / "clean_mountain.png")
    write_preview(args.output_dir / "clean_mountain_preview.png", image)
    write_debug(args.output_dir / "clean_mountain_debug.png", image, manifest)
    (args.output_dir / "clean_mountain_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Wrote mountain: {args.output_dir / 'clean_mountain.png'}")
    print(f"Wrote preview: {args.output_dir / 'clean_mountain_preview.png'}")
    print(f"Wrote debug: {args.output_dir / 'clean_mountain_debug.png'}")
    print(f"Wrote manifest: {args.output_dir / 'clean_mountain_manifest.json'}")


def parse_levels(text: str) -> list[tuple[int, int]]:
    levels: list[tuple[int, int]] = []
    for part in text.split(","):
        if not part.strip():
            continue
        width_text, depth_text = part.lower().split("x", 1)
        levels.append((max(120, int(width_text.strip())), max(70, int(depth_text.strip()))))
    return levels


def compose(args: argparse.Namespace, levels: list[tuple[int, int]]) -> tuple[Image.Image, dict]:
    pack_dir = args.elevation17_dir
    data = json.loads((pack_dir / "elevation17_manifest.json").read_text(encoding="utf-8"))
    atlas = Image.open(pack_dir / data["source_atlas"]).convert("RGBA")
    tile = data["tile_width"]
    top_material = make_top_material(atlas.crop((tile * 2, tile, tile * 3, tile * 2)).convert("RGBA"))
    wall_material = make_wall_material(data, atlas)

    max_w = max(width for width, _ in levels)
    max_d = max(depth for _, depth in levels)
    canvas_w = max_w + 300
    canvas_h = (len(levels) - 1) * args.level_rise + max_d + args.wall_height + 180
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
    center_x = canvas_w // 2
    base_y = canvas_h - max_d - args.wall_height - 48

    level_rects: list[dict] = []
    walkable: list[dict] = []

    for index, (width, depth) in enumerate(levels):
        x = center_x - width // 2 + round(math.sin(index * 1.3) * 24)
        y = base_y - index * args.level_rise
        wall_height = max(68, round(args.wall_height * (1.0 - index * 0.07)))
        mask = plateau_mask(width, depth, index)
        draw_wall(canvas, wall_material, mask, x, y + depth - 10, width, wall_height, index)
        draw_plateau(canvas, top_material, mask, x, y, index)
        rect = {"id": f"level_{index}", "index": index, "x": x, "y": y, "width": width, "depth": depth, "wall_height": wall_height}
        level_rects.append(rect)
        walkable.append({"id": f"level_{index}_walkable", "kind": "walkable_top", "level": index, "x": x + 42, "y": y + 28, "width": width - 84, "height": depth - 52})

    routes = draw_path(canvas, top_material, level_rects, walkable)
    castle = draw_castle(canvas, level_rects[-1]) if args.draw_castle else None
    manifest = {
        "name": args.name,
        "kind": "elevation17_clean_mountain",
        "source_elevation17": str(pack_dir).replace("\\", "/"),
        "image": "clean_mountain.png",
        "levels": level_rects,
        "walkable_regions": walkable,
        "routes": routes,
        "anchors": {
            "player_spawn": {"x": level_rects[0]["x"] + 85, "y": level_rects[0]["y"] + level_rects[0]["depth"] - 42, "level": 0},
            "castle_anchor": castle,
        },
    }
    return canvas, manifest


def make_top_material(tile: Image.Image) -> Image.Image:
    return flatten(tile, dominant_color(tile))


def make_wall_material(data: dict, atlas: Image.Image) -> Image.Image:
    wall_texture = data.get("wall_texture")
    wall_cell = data.get("wall_cell")
    if wall_texture and wall_texture != "derived_from_input_15_piece":
        path = Path(wall_texture)
        if path.exists():
            texture = Image.open(path).convert("RGBA")
            cells = wall_cells(texture)
            if cells:
                index = cell_index(wall_cell)
                return flatten(cells[index if index < len(cells) else 0], dominant_color(cells[0]))
            return flatten(texture, dominant_color(texture))
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
    col = max(1, int(col_text.strip()))
    row = max(1, int(row_text.strip()))
    return (row - 1) * 4 + (col - 1)


def crop_asset(atlas: Image.Image, data: dict, role: str) -> Image.Image:
    asset = next(asset for asset in data["assets"] if asset["role"] == role)
    rect = asset["rect"]
    return atlas.crop((rect["x"], rect["y"], rect["x"] + rect["width"], rect["y"] + rect["height"])).convert("RGBA")


def plateau_mask(width: int, depth: int, seed: int) -> Image.Image:
    mask = Image.new("L", (width, depth), 0)
    points = [
        (34, 34),
        (round(width * 0.25), 14 + seed * 3 % 9),
        (round(width * 0.62), 18),
        (width - 44, 20 + seed * 5 % 11),
        (width - 18, round(depth * 0.48)),
        (width - 42, depth - 20),
        (round(width * 0.48), depth - 8),
        (46, depth - 22),
        (16, round(depth * 0.48)),
    ]
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(0.7))


def draw_plateau(canvas: Image.Image, material: Image.Image, mask: Image.Image, x: int, y: int, seed: int) -> None:
    top = tile_texture(material, mask.width, mask.height, seed * 31, seed * 17)
    top = add_soft_light(top)
    canvas.alpha_composite(Image.composite(top, Image.new("RGBA", top.size, (0, 0, 0, 0)), mask), (x, y))
    edge = mask.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(3))
    canvas.alpha_composite(Image.composite(Image.new("RGBA", mask.size, (235, 229, 204, 92)), Image.new("RGBA", mask.size, (0, 0, 0, 0)), edge), (x, y - 1))
    lower_edge = Image.new("L", mask.size, 0)
    lower_edge.paste(edge, (0, 4))
    canvas.alpha_composite(Image.composite(Image.new("RGBA", mask.size, (18, 17, 15, 130)), Image.new("RGBA", mask.size, (0, 0, 0, 0)), lower_edge), (x, y))


def draw_wall(canvas: Image.Image, material: Image.Image, top_mask: Image.Image, x: int, y: int, width: int, height: int, seed: int) -> None:
    wall_mask = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(wall_mask)
    top_edge = top_mask.crop((0, top_mask.height - 18, width, top_mask.height)).resize((width, 18), Image.Resampling.BILINEAR)
    wall_mask.paste(top_edge, (0, 0))
    draw.polygon(((28, 0), (width - 28, 0), (width - 72, height - 6), (72, height - 6)), fill=242)
    for col in range(70, width - 40, 78):
        draw.line((col, 4, col - 18, height - 8), fill=160, width=3)
    wall = tile_texture(material, width, height, seed * 47, seed * 11)
    wall = ImageEnhance.Contrast(wall).enhance(1.14)
    wall = vertical_shade(wall)
    canvas.alpha_composite(Image.composite(wall, Image.new("RGBA", wall.size, (0, 0, 0, 0)), wall_mask.filter(ImageFilter.GaussianBlur(0.4))), (x, y))


def draw_path(canvas: Image.Image, top_material: Image.Image, levels: list[dict], walkable: list[dict]) -> list[dict]:
    points = []
    for index, level in enumerate(levels):
        fraction = 0.24 if index % 2 == 0 else 0.72
        points.append((round(level["x"] + level["width"] * fraction), round(level["y"] + level["depth"] * 0.56)))
    mask = Image.new("L", canvas.size, 0)
    draw = ImageDraw.Draw(mask)
    draw.line(points, fill=145, width=22, joint="curve")
    for x, y in points:
        draw.ellipse((x - 14, y - 14, x + 14, y + 14), fill=155)
    level_mask = Image.new("L", canvas.size, 0)
    for index, level in enumerate(levels):
        level_mask.paste(plateau_mask(level["width"], level["depth"], index), (level["x"], level["y"]))
    clipped = Image.composite(mask, Image.new("L", canvas.size, 0), level_mask)
    path = ImageEnhance.Brightness(ImageEnhance.Color(top_material).enhance(0.42)).enhance(0.78)
    canvas.alpha_composite(Image.composite(tile_texture(path, canvas.width, canvas.height, 3, 29), Image.new("RGBA", canvas.size, (0, 0, 0, 0)), clipped))
    stair_draw = ImageDraw.Draw(canvas, "RGBA")
    routes = []
    for index in range(len(points) - 1):
        x1, y1 = points[index]
        x2, y2 = points[index + 1]
        for step_index in range(5):
            t = (step_index + 1) / 6
            sx = round(x1 + (x2 - x1) * t)
            sy = round(y1 + (y2 - y1) * t)
            stair_draw.line((sx - 18, sy, sx + 18, sy - 6), fill=(48, 45, 39, 120), width=3)
        route = {"id": f"route_{index}_{index + 1}", "kind": "climb_route", "from_level": index, "to_level": index + 1, "x": min(x1, x2) - 28, "y": min(y1, y2) - 28, "width": abs(x2 - x1) + 56, "height": abs(y2 - y1) + 56}
        routes.append(route)
        walkable.append(route)
    return routes


def draw_castle(canvas: Image.Image, level: dict) -> dict:
    width = 146
    height = 86
    x = round(level["x"] + level["width"] * 0.5 - width * 0.5)
    y = level["y"] + 18
    draw = ImageDraw.Draw(canvas, "RGBA")
    draw.rectangle((x + 12, y + 34, x + width - 12, y + height), fill=(82, 78, 69, 238), outline=(28, 26, 23, 238), width=3)
    draw.rectangle((x + 24, y + 6, x + 54, y + 44), fill=(98, 94, 84, 245), outline=(28, 26, 23, 238), width=3)
    draw.rectangle((x + width - 54, y + 6, x + width - 24, y + 44), fill=(98, 94, 84, 245), outline=(28, 26, 23, 238), width=3)
    return {"x": x, "y": y, "width": width, "height": height, "level": level["index"]}


def flatten(image: Image.Image, color: tuple[int, int, int, int]) -> Image.Image:
    bg = Image.new("RGBA", image.size, color)
    bg.alpha_composite(image)
    return bg


def tile_texture(texture: Image.Image, width: int, height: int, offset_x: int, offset_y: int) -> Image.Image:
    texture = texture.convert("RGBA")
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for y in range(-offset_y % texture.height - texture.height, height, texture.height):
        for x in range(-offset_x % texture.width - texture.width, width, texture.width):
            canvas.alpha_composite(texture, (x, y))
    return canvas.crop((0, 0, width, height))


def add_soft_light(image: Image.Image) -> Image.Image:
    pixels = image.load()
    for y in range(image.height):
        shade = 1.08 - y / max(1, image.height - 1) * 0.12
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (clamp(r * shade), clamp(g * shade), clamp(b * shade), a)
    return image


def vertical_shade(image: Image.Image) -> Image.Image:
    pixels = image.load()
    for y in range(image.height):
        shade = 0.98 - y / max(1, image.height - 1) * 0.26
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (clamp(r * shade), clamp(g * shade), clamp(b * shade), a)
    return image


def dominant_color(image: Image.Image) -> tuple[int, int, int, int]:
    pixels = [pixel for pixel in image.resize((32, 32), Image.Resampling.BILINEAR).getdata() if pixel[3] > 8]
    if not pixels:
        return (110, 110, 100, 255)
    return (
        round(sum(pixel[0] for pixel in pixels) / len(pixels)),
        round(sum(pixel[1] for pixel in pixels) / len(pixels)),
        round(sum(pixel[2] for pixel in pixels) / len(pixels)),
        255,
    )


def write_preview(path: Path, image: Image.Image) -> None:
    bg = Image.new("RGBA", image.size, (20, 31, 40, 255))
    shadow = image.getchannel("A").filter(ImageFilter.GaussianBlur(12))
    bg.alpha_composite(Image.composite(Image.new("RGBA", image.size, (0, 0, 0, 90)), Image.new("RGBA", image.size, (0, 0, 0, 0)), shadow), (16, 20))
    bg.alpha_composite(image)
    bg.convert("RGB").save(path)


def write_debug(path: Path, image: Image.Image, manifest: dict) -> None:
    bg = Image.new("RGBA", image.size, (20, 31, 40, 255))
    bg.alpha_composite(image)
    draw = ImageDraw.Draw(bg, "RGBA")
    for region in manifest["walkable_regions"]:
        color = (80, 225, 120, 255) if region["kind"] == "walkable_top" else (80, 170, 255, 255)
        draw.rectangle((region["x"], region["y"], region["x"] + region["width"], region["y"] + region["height"]), outline=color, width=3)
    castle = manifest["anchors"]["castle_anchor"]
    if castle:
        draw.rectangle((castle["x"], castle["y"], castle["x"] + castle["width"], castle["y"] + castle["height"]), outline=(255, 220, 80, 255), width=3)
    bg.convert("RGB").save(path)


def clamp(value: float) -> int:
    return max(0, min(255, round(value)))


if __name__ == "__main__":
    main()
