#!/usr/bin/env python3
"""Extract a clean separated isometric mountain chunk atlas from overlapping source sheets."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


SPRITES = [
    {
        "id": "top_wide_green_cliff",
        "role": "level_top",
        "source": "cliffs",
        "box": (14, 5, 438, 322),
        "height_role": "wide supported floor",
    },
    {
        "id": "top_medium_green_cliff",
        "role": "level_top",
        "source": "cliffs",
        "box": (455, 5, 741, 322),
        "height_role": "medium supported floor",
    },
    {
        "id": "top_narrow_green_column",
        "role": "level_top",
        "source": "cliffs",
        "box": (755, 6, 899, 318),
        "height_role": "narrow high floor",
    },
    {
        "id": "top_round_green_column",
        "role": "level_top",
        "source": "cliffs",
        "box": (926, 6, 1096, 322),
        "height_role": "round high floor",
    },
    {
        "id": "top_tall_green_column",
        "role": "level_top",
        "source": "cliffs",
        "box": (1134, 5, 1307, 319),
        "height_role": "tall high floor",
    },
    {
        "id": "top_long_diagonal_green_cliff",
        "role": "level_top",
        "source": "cliffs",
        "box": (1567, 80, 1856, 323),
        "height_role": "diagonal terrace",
    },
    {
        "id": "top_small_green_column",
        "role": "level_top",
        "source": "cliffs",
        "box": (1874, 5, 1979, 145),
        "height_role": "small high floor",
    },
    {
        "id": "top_small_green_slope",
        "role": "level_top",
        "source": "cliffs",
        "box": (2009, 52, 2205, 213),
        "height_role": "small diagonal terrace",
    },
    {
        "id": "support_wide_front_left_green",
        "role": "cliff_support",
        "source": "bottoms",
        "box": (12, 262, 140, 348),
        "height_role": "wide front-left support",
    },
    {
        "id": "support_wide_front_right_green",
        "role": "cliff_support",
        "source": "bottoms",
        "box": (142, 262, 262, 348),
        "height_role": "wide front-right support",
    },
    {
        "id": "support_medium_front_green",
        "role": "cliff_support",
        "source": "bottoms",
        "box": (13, 371, 138, 450),
        "height_role": "medium front support",
    },
    {
        "id": "support_left_corner_green",
        "role": "cliff_support",
        "source": "bottoms",
        "box": (394, 253, 487, 353),
        "height_role": "left corner support",
    },
    {
        "id": "support_right_corner_green",
        "role": "cliff_support",
        "source": "bottoms",
        "box": (510, 258, 595, 353),
        "height_role": "right corner support",
    },
    {
        "id": "support_round_column_green",
        "role": "cliff_support",
        "source": "bottoms",
        "box": (612, 256, 700, 348),
        "height_role": "round column support",
    },
    {
        "id": "support_mound_green",
        "role": "cliff_support",
        "source": "bottoms",
        "box": (828, 252, 939, 354),
        "height_role": "rock mound support",
    },
    {
        "id": "support_cave_green",
        "role": "cliff_support",
        "source": "bottoms",
        "box": (1291, 250, 1425, 358),
        "height_role": "cave support",
    },
    {
        "id": "path_cobble_ramp_large",
        "role": "path",
        "source": "paths",
        "box": (29, 962, 313, 1163),
        "height_role": "large climb route",
    },
    {
        "id": "path_grass_ramp_left",
        "role": "path",
        "source": "paths",
        "box": (27, 1180, 190, 1342),
        "height_role": "grass climb route",
    },
    {
        "id": "path_stone_steps_green",
        "role": "path",
        "source": "paths",
        "box": (338, 1180, 486, 1341),
        "height_role": "stone stair route",
    },
    {
        "id": "path_rock_ramp_green",
        "role": "path",
        "source": "paths",
        "box": (1891, 1180, 2082, 1341),
        "height_role": "rock ramp route",
    },
    {
        "id": "path_flat_stone_green",
        "role": "path",
        "source": "paths",
        "box": (1897, 313, 2038, 442),
        "height_role": "flat floor path tile",
    },
    {
        "id": "path_flat_stone_green_alt",
        "role": "path",
        "source": "paths",
        "box": (2039, 313, 2180, 442),
        "height_role": "flat floor path tile alternate",
    },
    {
        "id": "prop_mossy_rocks",
        "role": "prop",
        "source": "cliffs",
        "box": (1259, 1598, 1349, 1661),
        "height_role": "decoration",
    },
    {
        "id": "prop_grass_bush",
        "role": "prop",
        "source": "paths",
        "box": (33, 1591, 167, 1669),
        "height_role": "decoration",
    },
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a clean separated isometric mountain chunk atlas.")
    parser.add_argument("--cliffs-source", required=True, type=Path)
    parser.add_argument("--bottoms-source", required=True, type=Path)
    parser.add_argument("--paths-source", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    sprites_dir = args.output_dir / "sprites"
    sprites_dir.mkdir(exist_ok=True)
    for old in sprites_dir.glob("*.png"):
        old.unlink()

    sources = {
        "cliffs": Image.open(args.cliffs_source).convert("RGBA"),
        "bottoms": Image.open(args.bottoms_source).convert("RGBA"),
        "paths": Image.open(args.paths_source).convert("RGBA"),
    }

    manifest_sprites = []
    for spec in SPRITES:
        sprite = extract_sprite(sources[spec["source"]], spec["box"], spec["source"])
        file_name = f"{spec['id']}.png"
        sprite.save(sprites_dir / file_name)
        manifest_sprites.append(
            {
                "id": spec["id"],
                "role": spec["role"],
                "source": spec["source"],
                "file": f"sprites/{file_name}",
                "source_rect": rect_from_box(spec["box"]),
                "size": {"width": sprite.width, "height": sprite.height},
                "height_role": spec["height_role"],
            }
        )

    write_atlas(args.output_dir / "clean_mountain_chunk_atlas.png", args.output_dir, manifest_sprites, labels=False)
    write_atlas(args.output_dir / "clean_mountain_chunk_atlas_preview.png", args.output_dir, manifest_sprites, labels=True)
    write_manifest(args.output_dir / "clean_mountain_chunk_manifest.json", manifest_sprites, args)
    print(f"Wrote {args.output_dir / 'clean_mountain_chunk_atlas_preview.png'}")
    print(f"Wrote {args.output_dir / 'clean_mountain_chunk_manifest.json'}")


def extract_sprite(source: Image.Image, box: tuple[int, int, int, int], source_kind: str) -> Image.Image:
    crop = source.crop(box).convert("RGBA")
    if source_kind == "bottoms":
        crop = remove_dark_background(crop)
    else:
        crop = remove_white_background(crop)
    bbox = crop.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    pad = 3
    x1 = max(0, bbox[0] - pad)
    y1 = max(0, bbox[1] - pad)
    x2 = min(crop.width, bbox[2] + pad)
    y2 = min(crop.height, bbox[3] + pad)
    return crop.crop((x1, y1, x2, y2))


def remove_white_background(image: Image.Image) -> Image.Image:
    result = image.copy()
    pixels = result.load()
    for y in range(result.height):
        for x in range(result.width):
            r, g, b, a = pixels[x, y]
            if r > 246 and g > 246 and b > 246:
                pixels[x, y] = (r, g, b, 0)
            elif r > 232 and g > 232 and b > 232:
                pixels[x, y] = (r, g, b, min(a, 70))
    return result


def remove_dark_background(image: Image.Image) -> Image.Image:
    pixels = image.load()
    width, height = image.size
    seeds = [
        pixels[0, 0][:3],
        pixels[width - 1, 0][:3],
        pixels[0, height - 1][:3],
        pixels[width - 1, height - 1][:3],
    ]

    def distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
        return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2) ** 0.5

    def is_background(x: int, y: int) -> bool:
        r, g, b = pixels[x, y][:3]
        if max(r, g, b) > 95:
            return False
        return min(distance((r, g, b), seed) for seed in seeds) < 20

    stack = [(x, 0) for x in range(width)] + [(x, height - 1) for x in range(width)]
    stack += [(0, y) for y in range(height)] + [(width - 1, y) for y in range(height)]
    seen: set[tuple[int, int]] = set()
    transparent: set[tuple[int, int]] = set()
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or x < 0 or y < 0 or x >= width or y >= height:
            continue
        seen.add((x, y))
        if not is_background(x, y):
            continue
        transparent.add((x, y))
        stack.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))

    result = image.copy()
    out = result.load()
    for x, y in transparent:
        r, g, b, _ = out[x, y]
        out[x, y] = (r, g, b, 0)
    return result


def rect_from_box(box: tuple[int, int, int, int]) -> dict:
    return {"x": box[0], "y": box[1], "width": box[2] - box[0], "height": box[3] - box[1]}


def write_atlas(path: Path, root: Path, sprites: list[dict], labels: bool) -> None:
    cell_w = 330
    cell_h = 270
    cols = 4
    rows = (len(sprites) + cols - 1) // cols
    bg = (0, 0, 0, 0) if not labels else (28, 33, 34, 255)
    atlas = Image.new("RGBA", (cols * cell_w, rows * cell_h), bg)
    draw = ImageDraw.Draw(atlas)
    for index, item in enumerate(sprites):
        sprite = Image.open(root / item["file"]).convert("RGBA")
        thumb = sprite.copy()
        thumb.thumbnail((cell_w - 24, cell_h - (56 if labels else 24)), Image.Resampling.LANCZOS)
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        atlas.alpha_composite(thumb, (x + (cell_w - thumb.width) // 2, y + 10))
        if labels:
            draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(72, 78, 76, 255))
            draw.text((x + 8, y + cell_h - 38), item["id"][:42], fill=(238, 241, 237, 255))
            draw.text((x + 8, y + cell_h - 20), f"{item['role']} | {item['source']}", fill=(180, 190, 184, 255))
    atlas.convert("RGBA" if not labels else "RGB").save(path)


def write_manifest(path: Path, sprites: list[dict], args: argparse.Namespace) -> None:
    manifest = {
        "name": "clean_isometric_mountain_chunk_atlas",
        "kind": "clean_separated_mountain_tileset_atlas",
        "atlas": "clean_mountain_chunk_atlas.png",
        "preview": "clean_mountain_chunk_atlas_preview.png",
        "sources": {
            "cliffs": str(args.cliffs_source),
            "bottoms": str(args.bottoms_source),
            "paths": str(args.paths_source),
        },
        "contract": {
            "purpose": "Clean one-sprite-per-cell source pack for prefab mountain generation.",
            "rule": "Use level_top sprites for floors, cliff_support sprites under floors, and path sprites for walkable climb routes.",
        },
        "sprites": sprites,
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
