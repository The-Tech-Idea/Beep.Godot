#!/usr/bin/env python3
"""Extract clean separated sprite atlases from white-background JFIF tileset sheets."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


DEFAULT_SOURCES = [
    Path(r"C:\Users\f_ald\source\repos\The-Tech-Idea\Art\TileSets\isometric Cliff and Mountain Tileset Atlas.jfif"),
    Path(r"C:\Users\f_ald\source\repos\The-Tech-Idea\Art\TileSets\Mountain Cliff Terrain Tile Atlas.jfif"),
    Path(r"C:\Users\f_ald\source\repos\The-Tech-Idea\Art\TileSets\isometric Cliff and Mountain  Paths 1 Tileset Atlas.jfif"),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Extract clean separated atlases from JFIF tileset sheets.")
    parser.add_argument("--source", action="append", type=Path, help="Source JFIF atlas. Can be passed more than once.")
    parser.add_argument(
        "--output-root",
        type=Path,
        default=Path("addons/beep_game_builder_cs/generated/mountains/clean_source_atlases"),
    )
    parser.add_argument("--threshold", type=int, default=245)
    parser.add_argument("--min-area", type=int, default=700)
    parser.add_argument("--min-size", type=int, default=20)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    sources = args.source or DEFAULT_SOURCES
    args.output_root.mkdir(parents=True, exist_ok=True)
    for source in sources:
        output_dir = args.output_root / slugify(source.stem)
        extract_source(source, output_dir, args)


def extract_source(source_path: Path, output_dir: Path, args: argparse.Namespace) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    sprites_dir = output_dir / "sprites"
    sprites_dir.mkdir(exist_ok=True)
    for old in sprites_dir.glob("*.png"):
        old.unlink()

    source = Image.open(source_path).convert("RGBA")
    boxes = detect_sprite_boxes(source, args.threshold, args.min_area, args.min_size)
    sprites = []
    for index, box in enumerate(boxes, start=1):
        sprite = crop_clean_sprite(source, box)
        sprite_id = f"{slugify(source_path.stem)}_{index:03d}"
        file_name = f"{sprite_id}.png"
        sprite.save(sprites_dir / file_name)
        sprites.append(
            {
                "id": sprite_id,
                "file": f"sprites/{file_name}",
                "source_rect": rect_from_box(box),
                "size": {"width": sprite.width, "height": sprite.height},
                "role": infer_role(source_path.stem, box, sprite.size),
            }
        )

    write_atlas(output_dir / "clean_extracted_atlas.png", output_dir, sprites, labels=False)
    write_atlas(output_dir / "clean_extracted_atlas_preview.png", output_dir, sprites, labels=True)
    write_manifest(output_dir / "clean_extracted_manifest.json", source_path, sprites, args)
    print(f"Wrote {output_dir / 'clean_extracted_atlas_preview.png'} ({len(sprites)} sprites)")


def detect_sprite_boxes(source: Image.Image, threshold: int, min_area: int, min_size: int) -> list[tuple[int, int, int, int]]:
    rgb = np.array(source.convert("RGB"))
    mask = np.any(rgb < threshold, axis=2).astype("uint8") * 255
    kernel = np.ones((3, 3), np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel, iterations=1)
    num_labels, _labels, stats, _centroids = cv2.connectedComponentsWithStats(mask, 8)
    boxes: list[tuple[int, int, int, int]] = []
    for label in range(1, num_labels):
        x, y, width, height, area = stats[label]
        if area < min_area or width < min_size or height < min_size:
            continue
        boxes.append(expand_box((int(x), int(y), int(x + width), int(y + height)), source.size, 4))
    return sorted(boxes, key=lambda box: (box[1] // 24, box[0]))


def crop_clean_sprite(source: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    crop = source.crop(box).convert("RGBA")
    pixels = crop.load()
    for y in range(crop.height):
        for x in range(crop.width):
            r, g, b, a = pixels[x, y]
            if r > 246 and g > 246 and b > 246:
                pixels[x, y] = (r, g, b, 0)
            elif r > 232 and g > 232 and b > 232:
                pixels[x, y] = (r, g, b, min(a, 72))
    bbox = crop.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    return crop.crop(expand_box(bbox, crop.size, 2))


def expand_box(box: tuple[int, int, int, int], image_size: tuple[int, int], pad: int) -> tuple[int, int, int, int]:
    width, height = image_size
    return (max(0, box[0] - pad), max(0, box[1] - pad), min(width, box[2] + pad), min(height, box[3] + pad))


def rect_from_box(box: tuple[int, int, int, int]) -> dict:
    return {"x": box[0], "y": box[1], "width": box[2] - box[0], "height": box[3] - box[1]}


def infer_role(source_name: str, box: tuple[int, int, int, int], size: tuple[int, int]) -> str:
    name = source_name.lower()
    width, height = size
    if "paths" in name:
        if height > 170 or width > 220:
            return "path_or_ramp_large"
        if height > 90:
            return "path_or_stairs"
        return "path_prop_or_small_tile"
    if "terrain" in name:
        if width > 230 and height > 180:
            return "terrain_platform_large"
        if height > 120:
            return "terrain_platform_or_ramp"
        return "terrain_prop_or_small_tile"
    if height > 250:
        return "cliff_platform_large"
    if width > 210:
        return "cliff_slope_or_platform"
    return "cliff_column_or_prop"


def write_atlas(path: Path, root: Path, sprites: list[dict], labels: bool) -> None:
    cell_w = 300
    cell_h = 250
    cols = 5
    rows = max(1, (len(sprites) + cols - 1) // cols)
    bg = (0, 0, 0, 0) if not labels else (28, 33, 34, 255)
    atlas = Image.new("RGBA", (cols * cell_w, rows * cell_h), bg)
    draw = ImageDraw.Draw(atlas)
    for index, item in enumerate(sprites):
        sprite = Image.open(root / item["file"]).convert("RGBA")
        thumb = sprite.copy()
        thumb.thumbnail((cell_w - 18, cell_h - (54 if labels else 18)), Image.Resampling.LANCZOS)
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        atlas.alpha_composite(thumb, (x + (cell_w - thumb.width) // 2, y + 8))
        if labels:
            draw.rectangle((x + 4, y + 4, x + cell_w - 4, y + cell_h - 4), outline=(70, 76, 74, 255))
            draw.text((x + 8, y + cell_h - 36), item["id"][-18:], fill=(238, 241, 237, 255))
            draw.text((x + 8, y + cell_h - 18), item["role"][:36], fill=(180, 190, 184, 255))
    atlas.convert("RGBA" if not labels else "RGB").save(path)


def write_manifest(path: Path, source_path: Path, sprites: list[dict], args: argparse.Namespace) -> None:
    manifest = {
        "name": slugify(source_path.stem),
        "kind": "clean_extracted_jfif_tileset_atlas",
        "source": str(source_path),
        "atlas": "clean_extracted_atlas.png",
        "preview": "clean_extracted_atlas_preview.png",
        "extraction": {
            "method": "same_white_background_connected_component_extraction",
            "threshold": args.threshold,
            "min_area": args.min_area,
            "min_size": args.min_size,
            "note": "Large connected artwork remains one chunk when the source sheet has no white gap separating it.",
        },
        "sprites": sprites,
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def slugify(value: str) -> str:
    value = value.strip().lower()
    value = re.sub(r"[^a-z0-9]+", "_", value)
    return value.strip("_")


if __name__ == "__main__":
    main()
