#!/usr/bin/env python3
"""Compose a mountain using an elevation17 atlas as real tiles."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a tile-stamped mountain from an elevation17 atlas.")
    parser.add_argument("--elevation17-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="tile_mountain")
    parser.add_argument("--levels", default="9x4,7x3,5x3,3x2", help="Bottom-to-top tile sizes.")
    parser.add_argument("--tile-step", type=int, default=64)
    parser.add_argument("--level-rise", type=int, default=128)
    parser.add_argument("--wall-height", type=int, default=96)
    parser.add_argument("--draw-castle", action="store_true")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    levels = parse_levels(args.levels)
    image, manifest = compose(args, levels)
    image.save(args.output_dir / "tile_mountain.png")
    write_preview(args.output_dir / "tile_mountain_preview.png", image)
    write_debug(args.output_dir / "tile_mountain_debug.png", image, manifest)
    (args.output_dir / "tile_mountain_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Wrote mountain: {args.output_dir / 'tile_mountain.png'}")
    print(f"Wrote preview: {args.output_dir / 'tile_mountain_preview.png'}")
    print(f"Wrote debug: {args.output_dir / 'tile_mountain_debug.png'}")
    print(f"Wrote manifest: {args.output_dir / 'tile_mountain_manifest.json'}")


def parse_levels(text: str) -> list[tuple[int, int]]:
    levels: list[tuple[int, int]] = []
    for item in text.split(","):
        if not item.strip():
            continue
        width_text, height_text = item.lower().split("x", 1)
        levels.append((max(2, int(width_text)), max(2, int(height_text))))
    return levels


def compose(args: argparse.Namespace, levels: list[tuple[int, int]]) -> tuple[Image.Image, dict]:
    data = json.loads((args.elevation17_dir / "elevation17_manifest.json").read_text(encoding="utf-8"))
    atlas = Image.open(args.elevation17_dir / data["source_atlas"]).convert("RGBA")
    tile_size = data["tile_width"]
    tiles = source_tiles(atlas, tile_size)
    cliff_left = crop_named(atlas, data, "side_cliff_left")
    cliff_right = crop_named(atlas, data, "side_cliff_right")
    wall_source = load_wall_source(data) or make_wall_source(cliff_left, cliff_right)

    max_w = max(width for width, _ in levels)
    max_h = max(height for _, height in levels)
    canvas_w = max_w * args.tile_step + 260
    canvas_h = (len(levels) - 1) * args.level_rise + max_h * args.tile_step + args.wall_height + 180
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
    center_x = canvas_w // 2
    base_y = canvas_h - max_h * args.tile_step - args.wall_height - 52

    level_rects: list[dict] = []
    placements: list[dict] = []
    walkable: list[dict] = []

    for level_index, (width, height) in enumerate(levels):
        px_w = width * args.tile_step
        px_h = height * args.tile_step
        x = center_x - px_w // 2 + (-32 if level_index % 2 == 0 else 28)
        y = base_y - level_index * args.level_rise
        draw_tile_level(canvas, tiles, wall_source, x, y, width, height, args.tile_step, args.wall_height, level_index, placements)
        rect = {"id": f"level_{level_index}", "index": level_index, "x": x, "y": y, "width": px_w, "height": px_h}
        level_rects.append(rect)
        walkable.append(
            {
                "id": f"level_{level_index}_walkable",
                "kind": "walkable_top",
                "level": level_index,
                "x": x + args.tile_step,
                "y": y + args.tile_step // 2,
                "width": max(args.tile_step, px_w - args.tile_step * 2),
                "height": max(args.tile_step, px_h - args.tile_step),
            }
        )

    routes = draw_tile_route(canvas, tiles, level_rects, args.tile_step, walkable)
    castle = draw_castle(canvas, level_rects[-1]) if args.draw_castle else None
    manifest = {
        "name": args.name,
        "kind": "elevation17_tile_mountain",
        "source_elevation17": str(args.elevation17_dir).replace("\\", "/"),
        "image": "tile_mountain.png",
        "levels": level_rects,
        "walkable_regions": walkable,
        "routes": routes,
        "anchors": {
            "player_spawn": {"x": level_rects[0]["x"] + args.tile_step, "y": level_rects[0]["y"] + level_rects[0]["height"] - args.tile_step, "level": 0},
            "castle_anchor": castle,
        },
        "placements": placements,
    }
    return canvas, manifest


def source_tiles(atlas: Image.Image, tile_size: int) -> dict[str, Image.Image]:
    grid = {(col, row): atlas.crop((col * tile_size, row * tile_size, (col + 1) * tile_size, (row + 1) * tile_size)).convert("RGBA") for row in range(4) for col in range(4)}
    return {
        "center": grid[(2, 1)],
        "n": grid[(3, 0)],
        "s": grid[(1, 2)],
        "w": grid[(1, 0)],
        "e": grid[(3, 2)],
        "nw": grid[(1, 1)],
        "ne": grid[(2, 0)],
        "sw": grid[(0, 2)],
        "se": grid[(2, 2)],
    }


def draw_tile_level(
    canvas: Image.Image,
    tiles: dict[str, Image.Image],
    wall_source: Image.Image,
    x: int,
    y: int,
    width: int,
    height: int,
    step: int,
    wall_height: int,
    level_index: int,
    placements: list[dict],
) -> None:
    for col in range(width):
        wx = x + col * step
        wy = y + height * step - 18
        wall = wall_column(wall_source, step, wall_height, level_index + col)
        canvas.alpha_composite(wall, (wx, wy))
        placements.append({"role": "wall_column", "x": wx, "y": wy, "level": level_index})

    for row in range(height):
        for col in range(width):
            role = tile_role(col, row, width, height)
            tx = x + col * step
            ty = y + row * step
            canvas.alpha_composite(tiles[role], (tx, ty))
            placements.append({"role": role, "x": tx, "y": ty, "level": level_index})


def tile_role(col: int, row: int, width: int, height: int) -> str:
    if col == 0 and row == 0:
        return "nw"
    if col == width - 1 and row == 0:
        return "ne"
    if col == 0 and row == height - 1:
        return "sw"
    if col == width - 1 and row == height - 1:
        return "se"
    if row == 0:
        return "n"
    if row == height - 1:
        return "s"
    if col == 0:
        return "w"
    if col == width - 1:
        return "e"
    return "center"


def wall_column(source: Image.Image, width: int, height: int, seed: int) -> Image.Image:
    texture = tile_texture(source, width, height, seed * 19, seed * 31)
    texture = vertical_shade(texture)
    mask = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(mask)
    wobble = seed % 7
    draw.polygon(((4 + wobble, 0), (width - 4, 0), (width - 10, height - 5), (10, height - 1)), fill=238)
    return Image.composite(texture, Image.new("RGBA", (width, height), (0, 0, 0, 0)), mask)


def draw_tile_route(canvas: Image.Image, tiles: dict[str, Image.Image], levels: list[dict], step: int, walkable: list[dict]) -> list[dict]:
    routes: list[dict] = []
    overlay = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay, "RGBA")
    points: list[tuple[int, int]] = []
    for index, level in enumerate(levels):
        col = 1 if index % 2 == 0 else max(1, level["width"] // step - 2)
        row = max(1, level["height"] // step - 2)
        points.append((level["x"] + col * step + step // 2, level["y"] + row * step + step // 2))
    draw.line(points, fill=(54, 50, 43, 120), width=24, joint="curve")
    for px, py in points:
        draw.ellipse((px - 17, py - 17, px + 17, py + 17), fill=(54, 50, 43, 130))
    canvas.alpha_composite(overlay)
    for index in range(len(points) - 1):
        x1, y1 = points[index]
        x2, y2 = points[index + 1]
        route = {
            "id": f"route_{index}_{index + 1}",
            "kind": "climb_route",
            "from_level": index,
            "to_level": index + 1,
            "x": min(x1, x2) - 24,
            "y": min(y1, y2) - 24,
            "width": abs(x2 - x1) + 48,
            "height": abs(y2 - y1) + 48,
        }
        routes.append(route)
        walkable.append(route)
    return routes


def draw_castle(canvas: Image.Image, top: dict) -> dict:
    width = 150
    height = 88
    x = top["x"] + top["width"] // 2 - width // 2
    y = top["y"] + 18
    draw = ImageDraw.Draw(canvas, "RGBA")
    draw.rectangle((x, y + 38, x + width, y + height), fill=(76, 72, 65, 230), outline=(26, 24, 22, 230), width=3)
    draw.rectangle((x + 18, y + 8, x + 48, y + 42), fill=(88, 84, 76, 235), outline=(26, 24, 22, 230), width=3)
    draw.rectangle((x + width - 48, y + 8, x + width - 18, y + 42), fill=(88, 84, 76, 235), outline=(26, 24, 22, 230), width=3)
    return {"x": x, "y": y, "width": width, "height": height, "level": top["index"]}


def load_wall_source(data: dict) -> Image.Image | None:
    wall_texture = data.get("wall_texture")
    wall_cell = data.get("wall_cell")
    if not wall_texture or wall_texture == "derived_from_input_15_piece":
        return None
    path = Path(wall_texture)
    if not path.exists():
        return None
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
        starts_x = [round(width * value / 1254) for value in [22, 330, 639, 948]]
        starts_y = [round(height * value / 1254) for value in [22, 330, 639, 948]]
        cell_w = round(width * 286 / 1254)
        cell_h = round(height * 286 / 1254)
        return [texture.crop((x, y, min(width, x + cell_w), min(height, y + cell_h))) for y in starts_y for x in starts_x]
    return []


def crop_named(atlas: Image.Image, data: dict, role: str) -> Image.Image:
    asset = next(asset for asset in data["assets"] if asset["role"] == role)
    rect = asset["rect"]
    return atlas.crop((rect["x"], rect["y"], rect["x"] + rect["width"], rect["y"] + rect["height"])).convert("RGBA")


def make_wall_source(left: Image.Image, right: Image.Image) -> Image.Image:
    width = left.width + right.width
    height = max(left.height, right.height)
    out = Image.new("RGBA", (width, height), dominant_color(left))
    out.alpha_composite(left, (0, height - left.height))
    out.alpha_composite(right, (left.width, height - right.height))
    return out


def tile_texture(texture: Image.Image, width: int, height: int, offset_x: int, offset_y: int) -> Image.Image:
    bg = Image.new("RGBA", texture.size, dominant_color(texture))
    bg.alpha_composite(texture.convert("RGBA"))
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for y in range(-offset_y % bg.height - bg.height, height, bg.height):
        for x in range(-offset_x % bg.width - bg.width, width, bg.width):
            canvas.alpha_composite(bg, (x, y))
    return canvas.crop((0, 0, width, height))


def vertical_shade(image: Image.Image) -> Image.Image:
    image = ImageEnhance.Contrast(image).enhance(1.12)
    pixels = image.load()
    for y in range(image.height):
        shade = 0.96 - y / max(1, image.height - 1) * 0.25
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (clamp(r * shade), clamp(g * shade), clamp(b * shade), a)
    return image


def dominant_color(image: Image.Image) -> tuple[int, int, int, int]:
    pixels = [pixel for pixel in image.resize((32, 32), Image.Resampling.BILINEAR).getdata() if pixel[3] > 8]
    if not pixels:
        return (96, 96, 90, 255)
    return (
        round(sum(pixel[0] for pixel in pixels) / len(pixels)),
        round(sum(pixel[1] for pixel in pixels) / len(pixels)),
        round(sum(pixel[2] for pixel in pixels) / len(pixels)),
        255,
    )


def write_preview(path: Path, image: Image.Image) -> None:
    bg = Image.new("RGBA", image.size, (20, 31, 40, 255))
    shadow = image.getchannel("A").filter(ImageFilter.GaussianBlur(10))
    bg.alpha_composite(Image.composite(Image.new("RGBA", image.size, (0, 0, 0, 110)), Image.new("RGBA", image.size, (0, 0, 0, 0)), shadow), (14, 18))
    bg.alpha_composite(image)
    bg.convert("RGB").save(path)


def write_debug(path: Path, image: Image.Image, manifest: dict) -> None:
    bg = Image.new("RGBA", image.size, (20, 31, 40, 255))
    bg.alpha_composite(image)
    draw = ImageDraw.Draw(bg, "RGBA")
    for region in manifest["walkable_regions"]:
        color = (80, 225, 125, 255) if region["kind"] == "walkable_top" else (80, 170, 255, 255)
        draw.rectangle((region["x"], region["y"], region["x"] + region["width"], region["y"] + region["height"]), outline=color, width=3)
    castle = manifest["anchors"]["castle_anchor"]
    if castle:
        draw.rectangle((castle["x"], castle["y"], castle["x"] + castle["width"], castle["y"] + castle["height"]), outline=(255, 220, 80, 255), width=3)
    bg.convert("RGB").save(path)


def clamp(value: float) -> int:
    return max(0, min(255, round(value)))


if __name__ == "__main__":
    main()
