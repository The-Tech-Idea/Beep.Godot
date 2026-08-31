#!/usr/bin/env python3
"""Build independent reference-style mountain prefab chunks from the source atlas."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

from mountain_reference_prefab_from_sheet import clean_sprite_crop, semantic_sprite_specs


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build independent prefab chunks from the mountain source atlas.")
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="reference_green")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    chunks_dir = args.output_dir / "prefab_chunks"
    chunks_dir.mkdir(exist_ok=True)
    for old in chunks_dir.glob("*.png"):
        old.unlink()

    source = Image.open(args.source).convert("RGBA")
    source_assets = extract_source_assets(source)
    chunks = build_chunks(source_assets)

    manifest_assets: list[dict] = []
    for chunk in chunks:
        file_name = f"{args.name}_{chunk['role']}.png"
        chunk["image"].save(chunks_dir / file_name)
        manifest_assets.append(
            {
                "id": f"{args.name}_{chunk['role']}",
                "role": chunk["role"],
                "category": chunk["category"],
                "file": f"prefab_chunks/{file_name}",
                "source_rect": {"x": 0, "y": 0, "width": chunk["image"].width, "height": chunk["image"].height},
                "mask_points": [{"x": x, "y": y} for x, y in chunk.get("mask_points", [])],
                "default_position": {"x": chunk["x"], "y": chunk["y"]},
                "sprite_size": {"width": chunk["image"].width, "height": chunk["image"].height},
                "height_level": chunk["height_level"],
                "from_level": chunk.get("from_level"),
                "to_level": chunk.get("to_level"),
                "walkable": chunk.get("walkable", False),
                "climbable": chunk.get("climbable", False),
                "visual_includes_wall": chunk.get("visual_includes_wall", True),
                "notes": chunk["notes"],
            }
        )

    write_chunk_atlas(args.output_dir / "prefab_chunk_atlas.png", manifest_assets, args.output_dir)
    write_chunk_atlas_preview(args.output_dir / "prefab_chunk_atlas_preview.png", manifest_assets, args.output_dir)
    write_chunk_manifest(args.output_dir / "prefab_chunk_manifest.json", args.name, manifest_assets)
    print(f"Wrote {args.output_dir / 'prefab_chunk_manifest.json'}")


def extract_source_assets(source: Image.Image) -> dict[str, Image.Image]:
    assets: dict[str, Image.Image] = {}
    for spec in semantic_sprite_specs():
        assets[spec.role] = clean_sprite_crop(source.crop(spec.box))
    return assets


def build_chunks(assets: dict[str, Image.Image]) -> list[dict]:
    cliff = assets["cliff_grey_wide"]
    grass = assets["top_grass_alt"]
    rocky_grass = assets["top_rock_grass"]
    stone = assets["castle_plateau_stone"]

    chunks = [
        {
            "role": "level_0_base_with_front_cliff",
            "category": "level_chunk",
            "image": make_plateau_chunk(
                (390, 245),
                grass,
                cliff,
                [(24, 72), (112, 26), (286, 40), (367, 98), (342, 176), (207, 227), (66, 196)],
                [(42, 142), (205, 170), (351, 139), (367, 199), (219, 244), (48, 217)],
            ),
            "x": 0,
            "y": 280,
            "height_level": 0,
            "walkable": True,
            "notes": "Independent broad entry plateau with its own front cliff.",
        },
        {
            "role": "level_1_right_plateau_with_cliff",
            "category": "level_chunk",
            "image": make_plateau_chunk(
                (310, 214),
                grass,
                cliff,
                [(26, 62), (108, 20), (242, 38), (299, 88), (278, 151), (164, 201), (38, 170)],
                [(36, 118), (163, 143), (286, 112), (300, 173), (170, 213), (32, 186)],
            ),
            "x": 286,
            "y": 178,
            "height_level": 1,
            "walkable": True,
            "notes": "Independent right terrace chunk with cliff support below the floor.",
        },
        {
            "role": "level_2_left_plateau_with_cliff",
            "category": "level_chunk",
            "image": make_plateau_chunk(
                (285, 198),
                rocky_grass,
                cliff,
                [(22, 72), (100, 24), (217, 20), (277, 66), (260, 128), (160, 188), (38, 160)],
                [(34, 118), (158, 139), (266, 106), (277, 158), (166, 197), (30, 178)],
            ),
            "x": 28,
            "y": 108,
            "height_level": 2,
            "walkable": True,
            "notes": "Independent upper-left terrace chunk; no full-prefab rectangle included.",
        },
        {
            "role": "level_3_castle_floor_with_support",
            "category": "castle_chunk",
            "image": make_castle_chunk(stone, cliff),
            "x": 334,
            "y": 36,
            "height_level": 3,
            "walkable": True,
            "notes": "Highest stone castle floor with visible cliff support underneath.",
        },
        {
            "role": "route_0_to_1_ramp_with_wall",
            "category": "route_chunk",
            "image": fit_sprite(assets["path_ramp_diagonal"], (190, 165)),
            "x": 178,
            "y": 256,
            "height_level": 1,
            "from_level": 0,
            "to_level": 1,
            "walkable": True,
            "climbable": True,
            "notes": "Independent lower ramp path; carries cliff height visually.",
        },
        {
            "role": "route_1_to_2_switchback_with_wall",
            "category": "route_chunk",
            "image": fit_sprite(assets["path_switchback_large"], (238, 180)),
            "x": 150,
            "y": 138,
            "height_level": 2,
            "from_level": 1,
            "to_level": 2,
            "walkable": True,
            "climbable": True,
            "notes": "Independent middle switchback path with cliff support.",
        },
        {
            "role": "route_2_to_3_high_path_with_wall",
            "category": "route_chunk",
            "image": fit_sprite(assets["path_cliff_column_right"], (180, 170)),
            "x": 286,
            "y": 68,
            "height_level": 3,
            "from_level": 2,
            "to_level": 3,
            "walkable": True,
            "climbable": True,
            "notes": "Independent high connector into the castle floor.",
        },
    ]

    return chunks


def make_plateau_chunk(
    size: tuple[int, int],
    top_texture: Image.Image,
    cliff_texture: Image.Image,
    top_points: list[tuple[int, int]],
    cliff_points: list[tuple[int, int]],
) -> Image.Image:
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    shadow = polygon_mask(size, [(x + 8, y + 12) for x, y in cliff_points], blur=5)
    image.alpha_composite(tint_mask(shadow, (0, 0, 0, 86)))

    cliff_mask = polygon_mask(size, cliff_points)
    paste_pattern(image, cliff_texture, cliff_mask, scale=1.65)
    draw = ImageDraw.Draw(image, "RGBA")
    draw.line(cliff_points + [cliff_points[0]], fill=(28, 31, 29, 190), width=3)

    top_mask = polygon_mask(size, top_points)
    paste_pattern(image, top_texture, top_mask, scale=1.35)
    draw.line(top_points + [top_points[0]], fill=(40, 52, 38, 210), width=3)
    draw.line(top_points[2:6], fill=(216, 224, 145, 120), width=2)
    draw.line(top_points[-3:] + top_points[:2], fill=(18, 28, 22, 130), width=2)
    return trim_alpha(image, 4)


def make_castle_chunk(stone_texture: Image.Image, cliff_texture: Image.Image) -> Image.Image:
    size = (260, 178)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    support = [(42, 82), (114, 70), (211, 78), (245, 118), (204, 174), (86, 166), (28, 125)]
    floor = [(34, 52), (100, 14), (199, 18), (248, 56), (224, 103), (89, 105)]
    paste_pattern(image, cliff_texture, polygon_mask(size, support), scale=1.5)
    paste_pattern(image, stone_texture, polygon_mask(size, floor), scale=1.4)
    draw = ImageDraw.Draw(image, "RGBA")
    draw.line(support + [support[0]], fill=(24, 25, 24, 210), width=3)
    draw.line(floor + [floor[0]], fill=(50, 49, 43, 230), width=3)
    for x, y in [(72, 22), (191, 25), (50, 68), (218, 68)]:
        draw.rectangle((x - 9, y - 22, x + 12, y + 6), fill=(82, 82, 73, 235), outline=(35, 35, 31, 240), width=2)
        draw.rectangle((x - 4, y - 30, x + 7, y - 20), fill=(105, 105, 94, 235), outline=(35, 35, 31, 240), width=1)
    draw.line([(70, 49), (207, 53)], fill=(70, 70, 62, 160), width=2)
    draw.line([(88, 72), (220, 76)], fill=(220, 215, 176, 90), width=2)
    return trim_alpha(image, 4)


def fit_sprite(sprite: Image.Image, target: tuple[int, int]) -> Image.Image:
    fitted = sprite.copy().convert("RGBA")
    fitted.thumbnail(target, Image.Resampling.LANCZOS)
    return trim_alpha(fitted, 3)


def polygon_mask(size: tuple[int, int], points: list[tuple[int, int]], blur: int = 0) -> Image.Image:
    scale = 3
    mask = Image.new("L", (size[0] * scale, size[1] * scale), 0)
    scaled = [(x * scale, y * scale) for x, y in points]
    ImageDraw.Draw(mask).polygon(scaled, fill=255)
    if blur > 0:
        mask = mask.filter(ImageFilter.GaussianBlur(blur * scale))
    return mask.resize(size, Image.Resampling.LANCZOS)


def paste_pattern(target: Image.Image, texture: Image.Image, mask: Image.Image, scale: float = 1.0) -> None:
    tex = texture.convert("RGBA")
    if scale != 1.0:
        tex = tex.resize((max(1, round(tex.width * scale)), max(1, round(tex.height * scale))), Image.Resampling.LANCZOS)
    pattern = Image.new("RGBA", target.size, (0, 0, 0, 0))
    for y in range(0, pattern.height, tex.height):
        for x in range(0, pattern.width, tex.width):
            pattern.alpha_composite(tex, (x, y))
    alpha = Image.composite(pattern.getchannel("A"), Image.new("L", target.size, 0), mask)
    pattern.putalpha(alpha)
    target.alpha_composite(pattern)


def tint_mask(mask: Image.Image, color: tuple[int, int, int, int]) -> Image.Image:
    image = Image.new("RGBA", mask.size, color)
    image.putalpha(mask.point(lambda value: int(value * color[3] / 255)))
    return image


def trim_alpha(image: Image.Image, pad: int) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    x1 = max(0, bbox[0] - pad)
    y1 = max(0, bbox[1] - pad)
    x2 = min(image.width, bbox[2] + pad)
    y2 = min(image.height, bbox[3] + pad)
    return image.crop((x1, y1, x2, y2))


def write_chunk_atlas(path: Path, assets: list[dict], root: Path) -> None:
    cell_w = 320
    cell_h = 250
    cols = 4
    rows = (len(assets) + cols - 1) // cols
    atlas = Image.new("RGBA", (cell_w * cols, cell_h * rows), (0, 0, 0, 0))
    for index, asset in enumerate(assets):
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        col = index % cols
        row = index // cols
        x = col * cell_w + (cell_w - sprite.width) // 2
        y = row * cell_h + (cell_h - sprite.height) // 2
        atlas.alpha_composite(sprite, (x, y))
    atlas.save(path)


def write_chunk_atlas_preview(path: Path, assets: list[dict], root: Path) -> None:
    cell_w = 320
    cell_h = 270
    cols = 4
    rows = (len(assets) + cols - 1) // cols
    preview = Image.new("RGBA", (cell_w * cols, cell_h * rows), (29, 32, 31, 255))
    draw = ImageDraw.Draw(preview)
    for index, asset in enumerate(assets):
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        sprite.thumbnail((cell_w - 18, cell_h - 54), Image.Resampling.LANCZOS)
        col = index % cols
        row = index // cols
        x = col * cell_w
        y = row * cell_h
        preview.alpha_composite(sprite, (x + (cell_w - sprite.width) // 2, y + 8))
        draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(73, 78, 75, 255))
        draw.text((x + 8, y + cell_h - 32), asset["role"][:38], fill=(238, 241, 237, 255))
        draw.text((x + 8, y + cell_h - 16), f"H{asset['height_level']} {asset['category']}", fill=(177, 187, 179, 255))
    preview.convert("RGB").save(path)


def write_chunk_manifest(path: Path, name: str, assets: list[dict]) -> None:
    manifest = {
        "name": f"{name}_prefab_chunks",
        "kind": "reference_style_prefab_chunk_atlas",
        "source_style": "source_atlas_independent_green_mountain",
        "atlas": "prefab_chunk_atlas.png",
        "preview": "prefab_chunk_atlas_preview.png",
        "contract": {
            "prefab_way": "Use independent level, route, and castle chunks; these are not 17-piece autotiles.",
            "level_order": "level_0_base -> level_1_right_plateau -> level_2_left_plateau -> level_3_castle_with_support.",
            "height_rule": "Route chunks carry from_level/to_level and visually include the cliff wall they climb.",
            "composition": "Layouts are composed from separate chunks, so presets can produce visibly different mountains.",
        },
        "assets": assets,
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
