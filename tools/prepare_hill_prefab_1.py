from __future__ import annotations

import json
import math
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


PROJECT_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = PROJECT_ROOT / "addons/beep_game_builder_cs/generated/hills/soft_grass/authored_prefabs/hill_prefab_1"
MANIFEST_PATH = OUTPUT_DIR / "hill_prefab_1_manifest.json"
PREVIEW_PATH = OUTPUT_DIR / "hill_prefab_1_preview.png"
SOURCE_SHEET = Path(
    r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_PgHJTBi7ZJJvFHZIf52Slx9N.png"
)

BACKGROUND = (19, 32, 39, 255)
PANEL = (23, 39, 47, 255)

SPRITES = {
    "hill_large": {"label": "Large Rounded Hill", "box": (0.00, 0.10, 0.43, 0.57), "relative_level_height": 1.0},
    "hill_medium": {"label": "Medium Rounded Hill", "box": (0.38, 0.14, 0.75, 0.55), "relative_level_height": 0.75},
    "hill_small": {"label": "Small Rounded Hill", "box": (0.70, 0.15, 1.00, 0.53), "relative_level_height": 0.55},
    "plate_quarter_height": {"label": "Quarter Height Hill Plate", "box": (0.00, 0.56, 0.36, 0.90), "relative_level_height": 0.25},
    "plate_half_height": {"label": "Half Height Hill Plate", "box": (0.34, 0.52, 0.71, 0.92), "relative_level_height": 0.5},
    "square_declined_edge_plate": {"label": "Square Declined Edge Hill Plate", "box": (0.68, 0.52, 1.00, 0.94), "relative_level_height": 0.42},
}


def rel(path: Path) -> str:
    return str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    path = Path("C:/Windows/Fonts/arial.ttf")
    return ImageFont.truetype(path, size) if path.exists() else ImageFont.load_default()


def extract_sprite(crop: Image.Image) -> Image.Image:
    rgb = np.asarray(crop.convert("RGB"), dtype=np.uint8)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    hue = hsv[:, :, 0]
    saturation = hsv[:, :, 1]
    value = hsv[:, :, 2]
    greenish = (hue >= 20) & (hue <= 95) & (saturation > 34) & (value > 48)
    foreground = greenish.astype(np.uint8)
    foreground = cv2.morphologyEx(
        foreground,
        cv2.MORPH_CLOSE,
        np.ones((9, 9), dtype=np.uint8),
        iterations=2,
    )
    count, labels, stats, _ = cv2.connectedComponentsWithStats(foreground, connectivity=8)
    if count <= 1:
        raise ValueError("No hill foreground found in source cell.")
    largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    foreground = (labels == largest).astype(np.uint8)
    foreground = cv2.dilate(foreground, np.ones((3, 3), dtype=np.uint8), iterations=1)

    distance = cv2.distanceTransform(foreground, cv2.DIST_L2, 3)
    alpha = np.clip(distance * 220.0, 0, 255).astype(np.uint8)
    ys, xs = np.where(alpha > 0)
    if len(xs) == 0:
        raise ValueError("Extracted hill sprite is empty.")
    pad = 10
    x0 = max(0, int(xs.min()) - pad)
    y0 = max(0, int(ys.min()) - pad)
    x1 = min(crop.width, int(xs.max()) + pad + 1)
    y1 = min(crop.height, int(ys.max()) + pad + 1)

    rgba = np.dstack((rgb, alpha))
    rgba[alpha == 0, :3] = 0
    return Image.fromarray(rgba, "RGBA").crop((x0, y0, x1, y1))


def extract_sheet() -> dict[str, Image.Image]:
    source = Image.open(SOURCE_SHEET).convert("RGB")
    assets: dict[str, Image.Image] = {}
    for sprite_id, spec in SPRITES.items():
        bx0, by0, bx1, by1 = spec["box"]
        x0 = round(source.width * bx0)
        y0 = round(source.height * by0)
        x1 = round(source.width * bx1)
        y1 = round(source.height * by1)
        assets[sprite_id] = extract_sprite(source.crop((x0, y0, x1, y1)))
    return assets


def pack_sheet(items: list[tuple[str, Image.Image]], path: Path, columns: int) -> dict[str, dict[str, int]]:
    padding = 24
    rows = math.ceil(len(items) / columns)
    column_widths = [0] * columns
    row_heights = [0] * rows
    for index, (_, image) in enumerate(items):
        column = index % columns
        row = index // columns
        column_widths[column] = max(column_widths[column], image.width)
        row_heights[row] = max(row_heights[row], image.height)

    sheet = Image.new(
        "RGBA",
        (
            sum(column_widths) + padding * (columns + 1),
            sum(row_heights) + padding * (rows + 1),
        ),
        (0, 0, 0, 0),
    )
    regions: dict[str, dict[str, int]] = {}
    y = padding
    for row in range(rows):
        x = padding
        for column in range(columns):
            index = row * columns + column
            if index >= len(items):
                break
            item_id, image = items[index]
            sheet.alpha_composite(image, (x, y))
            regions[item_id] = {"x": x, "y": y, "width": image.width, "height": image.height}
            x += column_widths[column] + padding
        y += row_heights[row] + padding
    sheet.save(path, optimize=True)
    return regions


def create_surface_fill_tile(square: Image.Image) -> Image.Image:
    rgb = square.convert("RGB")
    crop_size = min(256, rgb.width, rgb.height)
    left = (rgb.width - crop_size) // 2
    top = max(0, min(round(rgb.height * 0.12), rgb.height - crop_size))
    patch = rgb.crop((left, top, left + crop_size, top + crop_size))
    tile = Image.new("RGB", (512, 512))
    tile.paste(patch.resize((256, 256), Image.Resampling.LANCZOS), (0, 0))
    tile.paste(patch.transpose(Image.Transpose.FLIP_LEFT_RIGHT).resize((256, 256)), (256, 0))
    tile.paste(patch.transpose(Image.Transpose.FLIP_TOP_BOTTOM).resize((256, 256)), (0, 256))
    tile.paste(
        patch.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        .transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        .resize((256, 256)),
        (256, 256),
    )
    return tile


def create_preview(assets: dict[str, Image.Image]) -> None:
    canvas = Image.new("RGBA", (1600, 1050), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.text((44, 30), "HILL PREFAB 1 - SOFT GRASS", fill="white", font=font(38))
    slots = [
        ("hill_large", (60, 130, 580, 460)),
        ("hill_medium", (610, 145, 1010, 440)),
        ("hill_small", (1080, 160, 1480, 420)),
        ("plate_quarter_height", (80, 590, 480, 770)),
        ("plate_half_height", (570, 540, 990, 790)),
        ("square_declined_edge_plate", (1080, 520, 1500, 820)),
    ]
    for sprite_id, box in slots:
        x0, y0, x1, y1 = box
        draw.rectangle((x0 - 18, y0 - 18, x1 + 18, y1 + 44), fill=PANEL)
        image = assets[sprite_id].copy()
        image.thumbnail((x1 - x0, y1 - y0), Image.Resampling.LANCZOS)
        canvas.alpha_composite(image, (x0 + (x1 - x0 - image.width) // 2, y0 + (y1 - y0 - image.height) // 2))
        draw.text((x0, y1 + 10), sprite_id, fill=(210, 225, 230, 255), font=font(18))
    canvas.convert("RGB").save(PREVIEW_PATH, quality=95)


def main() -> int:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    if not SOURCE_SHEET.exists():
        raise FileNotFoundError(SOURCE_SHEET)

    shutil.copy2(SOURCE_SHEET, OUTPUT_DIR / "hill_prefab_1_source_sheet.png")
    assets = extract_sheet()
    for sprite_id, image in assets.items():
        image.save(OUTPUT_DIR / f"{sprite_id}.png", optimize=True)
    assets["plate_quarter_height"].save(OUTPUT_DIR / "step_quarter.png", optimize=True)
    assets["plate_half_height"].save(OUTPUT_DIR / "step_half.png", optimize=True)

    fill_tile = create_surface_fill_tile(assets["square_declined_edge_plate"])
    fill_tile.save(OUTPUT_DIR / "surface_fill_tile.png", optimize=True)
    regions = pack_sheet(list(assets.items()), OUTPUT_DIR / "hill_prefab_1_sheet.png", 3)
    create_preview(assets)

    modules = [
        {
            "id": sprite_id,
            "display_name": spec["label"],
            "file": rel(OUTPUT_DIR / f"{sprite_id}.png"),
            "image_size": [assets[sprite_id].width, assets[sprite_id].height],
            "relative_level_height": spec["relative_level_height"],
            "walkable": True,
            "placement": "manual",
        }
        for sprite_id, spec in SPRITES.items()
    ]
    manifest = {
        "schema_version": 1,
        "pack_id": "hill_prefab_1_soft_grass",
        "style_id": "hill_prefab_1",
        "style_label": "Hill Prefab 1 - Soft Grass",
        "projection": "front_2_5d",
        "visual_only": True,
        "base_prefabs": [
            {
                "id": "hill_large",
                "display_name": "Hill Prefab 1 - Large Hill",
                "file": rel(OUTPUT_DIR / "hill_large.png"),
                "preview": rel(PREVIEW_PATH),
                "image_size": [assets["hill_large"].width, assets["hill_large"].height],
                "level_count": 1,
                "decorative_paths": False,
                "gameplay_metadata": False,
                "sockets": [],
            },
            {
                "id": "square_declined_edge_plate",
                "display_name": "Hill Prefab 1 - Square Declined Edge Plate",
                "file": rel(OUTPUT_DIR / "square_declined_edge_plate.png"),
                "preview": rel(PREVIEW_PATH),
                "image_size": [assets["square_declined_edge_plate"].width, assets["square_declined_edge_plate"].height],
                "level_count": 1,
                "fill_tile": rel(OUTPUT_DIR / "surface_fill_tile.png"),
                "fill_tile_size": [512, 512],
                "decorative_paths": False,
                "gameplay_metadata": False,
                "sockets": [],
            },
        ],
        "hill_modules": modules,
        "ramp_modules": [],
        "jump_step_modules": [
            {
                "id": "step_quarter",
                "alias_id": "plate_quarter_height",
                "file": rel(OUTPUT_DIR / "step_quarter.png"),
                "alias_file": rel(OUTPUT_DIR / "plate_quarter_height.png"),
                "image_size": [assets["plate_quarter_height"].width, assets["plate_quarter_height"].height],
                "relative_level_height": 0.25,
                "placement": "manual",
            },
            {
                "id": "step_half",
                "alias_id": "plate_half_height",
                "file": rel(OUTPUT_DIR / "step_half.png"),
                "alias_file": rel(OUTPUT_DIR / "plate_half_height.png"),
                "image_size": [assets["plate_half_height"].width, assets["plate_half_height"].height],
                "relative_level_height": 0.5,
                "placement": "manual",
            },
        ],
        "atlas_sheets": [
            {
                "id": "hill_prefab_1_sheet",
                "file": rel(OUTPUT_DIR / "hill_prefab_1_sheet.png"),
                "transparent": True,
                "regions": regions,
            }
        ],
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared Hill Prefab 1: {OUTPUT_DIR}")
    print(f"Preview: {PREVIEW_PATH}")
    print(f"Manifest: {MANIFEST_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
