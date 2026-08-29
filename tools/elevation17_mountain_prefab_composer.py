#!/usr/bin/env python3
"""Compose a mountain prefab from an elevation17 atlas."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a layered mountain prefab from an elevation17 atlas folder.")
    parser.add_argument("--elevation17-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="generated_mountain")
    parser.add_argument("--level-sizes", default="760x170,600x145,430x120,270x95", help="Bottom-to-top WIDTHxDEPTH list.")
    parser.add_argument("--level-rise", type=int, default=118)
    parser.add_argument("--wall-height", type=int, default=124)
    parser.add_argument("--path-width", type=int, default=56)
    parser.add_argument("--castle-width", type=int, default=150)
    parser.add_argument("--castle-height", type=int, default=82)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    sizes = parse_sizes(args.level_sizes)
    image, manifest = compose(args, sizes)
    image_path = args.output_dir / "mountain_prefab.png"
    preview_path = args.output_dir / "mountain_prefab_preview.png"
    debug_path = args.output_dir / "mountain_prefab_debug.png"
    manifest_path = args.output_dir / "mountain_prefab_manifest.json"
    image.save(image_path)
    write_preview(preview_path, image)
    write_debug(debug_path, image, manifest)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Wrote mountain: {image_path}")
    print(f"Wrote preview: {preview_path}")
    print(f"Wrote debug: {debug_path}")
    print(f"Wrote manifest: {manifest_path}")


def parse_sizes(text: str) -> list[tuple[int, int]]:
    sizes: list[tuple[int, int]] = []
    for part in text.split(","):
        if not part.strip():
            continue
        width_text, depth_text = part.lower().split("x", 1)
        sizes.append((max(96, int(width_text.strip())), max(56, int(depth_text.strip()))))
    return sizes


def compose(args: argparse.Namespace, sizes: list[tuple[int, int]]) -> tuple[Image.Image, dict]:
    manifest_path = args.elevation17_dir / "elevation17_manifest.json"
    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    atlas = Image.open(args.elevation17_dir / data["source_atlas"]).convert("RGBA")
    top_rect = data["layout"]["source_15_piece"]
    left_rect, right_rect = data["layout"]["side_cliffs"]
    top_source = crop_rect(atlas, top_rect)
    left_cliff = crop_rect(atlas, left_rect)
    right_cliff = crop_rect(atlas, right_rect)

    top_material = make_top_material(top_source)
    wall_source = load_wall_source_from_manifest(data, left_cliff, right_cliff)
    wall_material = make_wall_material(wall_source)
    path_material = make_path_material(top_material)

    max_width = max(width for width, _ in sizes)
    total_rise = args.level_rise * (len(sizes) - 1)
    max_depth = max(depth for _, depth in sizes)
    canvas_w = max_width + 260
    canvas_h = total_rise + max_depth + args.wall_height + 180
    center_x = canvas_w // 2
    base_top_y = canvas_h - max_depth - args.wall_height - 46
    image = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))

    placements: list[dict] = []
    walkable_regions: list[dict] = []
    routes: list[dict] = []
    level_rects: list[dict] = []

    for index, (width, depth) in enumerate(sizes):
        x = center_x - width // 2 + round(math.sin(index * 1.7) * 34)
        y = base_top_y - index * args.level_rise
        wall_height = max(58, round(args.wall_height * (1.0 - index * 0.08)))
        draw_level(image, top_material, wall_material, x, y, width, depth, wall_height, index)
        level = {
            "id": f"level_{index}",
            "index": index,
            "x": x,
            "y": y,
            "width": width,
            "depth": depth,
            "wall_height": wall_height,
        }
        level_rects.append(level)
        walkable_regions.append(
            {
                "id": f"level_{index}_walkable",
                "level": index,
                "x": x + 38,
                "y": y + 26,
                "width": max(24, width - 76),
                "height": max(24, depth - 44),
                "kind": "walkable_top",
            }
        )
        placements.append({"role": "level", **level})

    draw_route(image, path_material, level_rects, args.path_width, routes, walkable_regions)
    castle = draw_castle_anchor(image, level_rects[-1], args.castle_width, args.castle_height) if args.castle_width > 0 and args.castle_height > 0 else None

    manifest = {
        "name": args.name,
        "kind": "elevation17_mountain_prefab",
        "source_elevation17": str(args.elevation17_dir).replace("\\", "/"),
        "source_manifest": str(manifest_path).replace("\\", "/"),
        "prefab_image": "mountain_prefab.png",
        "size": {"width": image.width, "height": image.height},
        "levels": level_rects,
        "walkable_regions": walkable_regions,
        "routes": routes,
        "anchors": {
            "player_spawn": {"x": level_rects[0]["x"] + 70, "y": level_rects[0]["y"] + level_rects[0]["depth"] - 42, "level": 0},
            "castle_anchor": castle,
        },
    }
    return image, manifest


def crop_rect(image: Image.Image, rect: dict) -> Image.Image:
    x = rect["x"]
    y = rect["y"]
    return image.crop((x, y, x + rect["width"], y + rect["height"])).convert("RGBA")


def make_top_material(source: Image.Image) -> Image.Image:
    if source.width >= 192 and source.height >= 128:
        crop = source.crop((128, 64, 192, 128)).convert("RGBA")
    else:
        bbox = source.getchannel("A").getbbox()
        crop = source.crop(bbox).convert("RGBA") if bbox else source.convert("RGBA")
    return flatten(crop, dominant_color(crop))


def load_wall_source_from_manifest(data: dict, fallback_left: Image.Image, fallback_right: Image.Image) -> Image.Image:
    wall_texture = data.get("wall_texture")
    wall_cell = data.get("wall_cell")
    if wall_texture and wall_texture != "derived_from_input_15_piece":
        path = Path(wall_texture)
        if path.exists():
            texture = Image.open(path).convert("RGBA")
            cells = wall_cells(texture)
            if cells and wall_cell:
                col_text, row_text = wall_cell.split(",", 1)
                col = max(1, int(col_text.strip()))
                row = max(1, int(row_text.strip()))
                index = (row - 1) * 4 + (col - 1)
                if 0 <= index < len(cells):
                    return cells[index]
            if cells:
                return cells[0]
            return texture
    width = fallback_left.width + fallback_right.width
    height = max(fallback_left.height, fallback_right.height)
    wall = Image.new("RGBA", (width, height), dominant_color(fallback_left))
    wall.alpha_composite(fallback_left, (0, height - fallback_left.height))
    wall.alpha_composite(fallback_right, (fallback_left.width, height - fallback_right.height))
    return wall


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
    return []


def scaled_positions(size: int, positions: list[int], source_size: int) -> list[int]:
    return [round(size * position / source_size) for position in positions]


def make_wall_material(source: Image.Image) -> Image.Image:
    wall = flatten(source.convert("RGBA"), dominant_color(source))
    wall = ImageEnhance.Contrast(wall).enhance(1.16)
    wall = ImageEnhance.Brightness(wall).enhance(0.88)
    return wall


def flatten(image: Image.Image, bg_color: tuple[int, int, int, int]) -> Image.Image:
    bg = Image.new("RGBA", image.size, bg_color)
    bg.alpha_composite(image)
    return bg


def make_path_material(top: Image.Image) -> Image.Image:
    path = ImageEnhance.Color(top).enhance(0.55)
    path = ImageEnhance.Brightness(path).enhance(0.82)
    path = ImageEnhance.Contrast(path).enhance(0.78)
    return path.filter(ImageFilter.GaussianBlur(1.2))


def draw_level(image: Image.Image, top_material: Image.Image, wall_material: Image.Image, x: int, y: int, width: int, depth: int, wall_height: int, index: int) -> None:
    mask = plateau_mask(width, depth, index)
    wall_mask = Image.new("L", (width, wall_height), 0)
    draw = ImageDraw.Draw(wall_mask)
    draw.polygon(((28, 0), (width - 28, 0), (width - 78, wall_height - 8), (78, wall_height - 8)), fill=245)
    for column in range(60, width - 50, 58):
        draw.line((column, 4, column - 18, wall_height - 12), fill=175, width=3)
    wall = tile_texture(wall_material, width, wall_height, index * 31, index * 17)
    wall = vertical_shade(wall)
    image.alpha_composite(Image.composite(wall, Image.new("RGBA", wall.size, (0, 0, 0, 0)), wall_mask), (x, y + depth - 12))

    top = tile_texture(top_material, width, depth, index * 19, index * 29)
    top = add_top_lighting(top, index)
    image.alpha_composite(Image.composite(top, Image.new("RGBA", top.size, (0, 0, 0, 0)), mask), (x, y))
    draw_lip(image, mask, x, y)


def plateau_mask(width: int, height: int, seed: int) -> Image.Image:
    points = [
        (24, 30 + (seed * 11) % 12),
        (width // 4, 12),
        (width // 2, 18 + (seed * 7) % 10),
        (width - 42, 10),
        (width - 18, height // 3),
        (width - 34, height - 22),
        (width // 2, height - 8),
        (52, height - 18),
        (16, height // 2),
    ]
    mask = Image.new("L", (width, height), 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(1.1))


def draw_route(image: Image.Image, material: Image.Image, levels: list[dict], path_width: int, routes: list[dict], walkable: list[dict]) -> None:
    mask = Image.new("L", image.size, 0)
    draw = ImageDraw.Draw(mask)
    points: list[tuple[int, int]] = []
    for index, level in enumerate(levels):
        side = 0.28 if index % 2 == 0 else 0.68
        points.append((round(level["x"] + level["width"] * side), round(level["y"] + level["depth"] * 0.54)))
    draw.line(points, fill=185, width=max(18, path_width // 2), joint="curve")
    for px, py in points:
        r = max(14, path_width // 3)
        draw.ellipse((px - r, py - r, px + r, py + r), fill=205)
    level_mask = Image.new("L", image.size, 0)
    for index, level in enumerate(levels):
        local = plateau_mask(level["width"], level["depth"], index)
        level_mask.paste(local, (level["x"], level["y"]))
    mask = Image.composite(mask, Image.new("L", image.size, 0), level_mask.filter(ImageFilter.MaxFilter(11)))
    path_texture = tile_texture(material, image.width, image.height, 7, 31)
    image.alpha_composite(Image.composite(path_texture, Image.new("RGBA", image.size, (0, 0, 0, 0)), mask))
    for index in range(len(points) - 1):
        x1, y1 = points[index]
        x2, y2 = points[index + 1]
        rect = {
            "id": f"route_{index}_{index + 1}",
            "from_level": index,
            "to_level": index + 1,
            "x": min(x1, x2) - path_width,
            "y": min(y1, y2) - path_width,
            "width": abs(x2 - x1) + path_width * 2,
            "height": abs(y2 - y1) + path_width * 2,
            "kind": "walkable_climb_route",
        }
        routes.append(rect)
        walkable.append(rect)


def draw_castle_anchor(image: Image.Image, top_level: dict, width: int, height: int) -> dict:
    x = round(top_level["x"] + top_level["width"] * 0.5 - width * 0.5)
    y = top_level["y"] + 16
    draw = ImageDraw.Draw(image, "RGBA")
    draw.rectangle((x, y + height - 20, x + width, y + height), fill=(44, 42, 38, 145))
    draw.rectangle((x + 18, y + 28, x + width - 18, y + height - 16), fill=(102, 98, 90, 210), outline=(36, 34, 32, 210), width=3)
    draw.rectangle((x + 30, y + 4, x + 62, y + 42), fill=(112, 108, 100, 220), outline=(36, 34, 32, 210), width=3)
    draw.rectangle((x + width - 62, y + 4, x + width - 30, y + 42), fill=(112, 108, 100, 220), outline=(36, 34, 32, 210), width=3)
    return {"x": x, "y": y, "width": width, "height": height, "level": top_level["index"]}


def add_top_lighting(image: Image.Image, seed: int) -> Image.Image:
    out = image.copy()
    pixels = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = pixels[x, y]
            if a <= 8:
                continue
            shade = 1.08 - (y / max(1, out.height - 1)) * 0.13
            noise = (((x + seed * 19) * 3 + (y + seed * 7) * 5) % 11 - 5) * 0.7
            pixels[x, y] = (clamp(r * shade + noise), clamp(g * shade + noise), clamp(b * shade + noise), a)
    return out


def vertical_shade(image: Image.Image) -> Image.Image:
    out = image.copy()
    pixels = out.load()
    for y in range(out.height):
        shade = 0.96 - (y / max(1, out.height - 1)) * 0.28
        for x in range(out.width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (clamp(r * shade), clamp(g * shade), clamp(b * shade), a)
    return out


def draw_lip(image: Image.Image, mask: Image.Image, x: int, y: int) -> None:
    edge = mask.filter(ImageFilter.FIND_EDGES)
    light = Image.new("RGBA", mask.size, (242, 238, 220, 80))
    dark = Image.new("RGBA", mask.size, (18, 18, 16, 120))
    image.alpha_composite(Image.composite(light, Image.new("RGBA", mask.size, (0, 0, 0, 0)), edge), (x, y))
    shifted = Image.new("L", mask.size, 0)
    shifted.paste(edge, (0, 4))
    image.alpha_composite(Image.composite(dark, Image.new("RGBA", mask.size, (0, 0, 0, 0)), shifted), (x, y))


def tile_texture(texture: Image.Image, width: int, height: int, offset_x: int, offset_y: int) -> Image.Image:
    texture = texture.convert("RGBA")
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for y in range(-offset_y % texture.height - texture.height, height, texture.height):
        for x in range(-offset_x % texture.width - texture.width, width, texture.width):
            canvas.alpha_composite(texture, (x, y))
    return canvas.crop((0, 0, width, height))


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
    shadow = image.getchannel("A").filter(ImageFilter.GaussianBlur(12))
    bg.alpha_composite(Image.composite(Image.new("RGBA", image.size, (0, 0, 0, 105)), Image.new("RGBA", image.size, (0, 0, 0, 0)), shadow), (18, 22))
    bg.alpha_composite(image)
    bg.convert("RGB").save(path)


def write_debug(path: Path, image: Image.Image, manifest: dict) -> None:
    bg = Image.new("RGBA", image.size, (20, 31, 40, 255))
    bg.alpha_composite(image)
    draw = ImageDraw.Draw(bg, "RGBA")
    for region in manifest["walkable_regions"]:
        color = (78, 220, 120, 255) if region["kind"] == "walkable_top" else (80, 175, 255, 255)
        draw.rectangle((region["x"], region["y"], region["x"] + region["width"], region["y"] + region["height"]), outline=color, width=3)
    castle = manifest["anchors"]["castle_anchor"]
    if castle:
        draw.rectangle((castle["x"], castle["y"], castle["x"] + castle["width"], castle["y"] + castle["height"]), outline=(255, 218, 72, 255), width=3)
    bg.convert("RGB").save(path)


def clamp(value: float) -> int:
    return max(0, min(255, round(value)))


if __name__ == "__main__":
    main()
