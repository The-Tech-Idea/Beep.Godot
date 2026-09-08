from __future__ import annotations

import json
import math
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


PROJECT_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = PROJECT_ROOT / (
    "addons/beep_game_builder_cs/generated/mountains/natural_plateau/"
    "authored_prefabs/mountain_prefab_2"
)
THEME_ROOT = OUTPUT_DIR / "themes"
CATALOG_PATH = THEME_ROOT / "mountain_prefab_2_theme_catalog.json"
CATALOG_PREVIEW_PATH = THEME_ROOT / "mountain_prefab_2_all_variations_preview.png"
STEP_PREVIEW_PATH = THEME_ROOT / "mountain_prefab_2_all_step_plates_preview.png"
MANIFEST_PATH = OUTPUT_DIR / "mountain_prefab_2_manifest.json"

BACKGROUND = (19, 32, 39, 255)
PANEL = (23, 39, 47, 255)

THEMES = [
    {
        "id": "sandstone",
        "label": "Low Poly Sandstone",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_DMtNA444bBGD3jPOBsQ2oy0D.png",
        "background": "checker",
    },
    {
        "id": "grass_granite",
        "label": "Grass + Granite",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_OHKDUGKTr3xNPKRWTAjjtLL5.png",
        "background": "checker",
    },
    {
        "id": "grey_rock",
        "label": "Grey Rock",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_CUhEQddABtvQSKQBYlkfL9rJ.png",
        "background": "checker",
    },
    {
        "id": "volcanic_basalt",
        "label": "Volcanic Basalt",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_MfpId37uXFMvlgLZGaVbYdVM.png",
        "background": "checker",
    },
    {
        "id": "meadow_hill",
        "label": "Gentle Meadow Hill",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_rIChOuZkasz6Ci3gPWbqh2GI.png",
        "background": "checker",
    },
    {
        "id": "red_rock_mesa",
        "label": "Red Rock Mesa",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_FeaC6P2h54uq9SAdXtF07U55.png",
        "background": "checker",
    },
    {
        "id": "alpine_snow",
        "label": "Alpine Snow",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_ZwvNSOR2C0xj6J98gvX47kH6.png",
        "background": "dark",
    },
]

SHEET_BOXES = {
    "assembled": (0.0, 0.0, 0.5, 0.5),
    "plate_base": (0.5, 0.0, 1.0, 0.5),
    "plate_middle": (0.0, 0.5, 0.5, 1.0),
    "plate_top": (0.5, 0.5, 1.0, 1.0),
}


def rel(path: Path) -> str:
    return str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    path = Path("C:/Windows/Fonts/arial.ttf")
    return ImageFont.truetype(path, size) if path.exists() else ImageFont.load_default()


def largest_component(mask: np.ndarray) -> np.ndarray:
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, connectivity=8)
    if count <= 1:
        raise ValueError("No foreground object was found.")
    largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    return (labels == largest).astype(np.uint8)


def fill_holes(mask: np.ndarray) -> np.ndarray:
    padded = np.pad(mask, 1, mode="constant")
    flood = padded.copy()
    cv2.floodFill(flood, None, (0, 0), 2)
    holes = flood == 0
    return np.where((padded > 0) | holes, 1, 0).astype(np.uint8)[1:-1, 1:-1]


def extract_sprite(source: Image.Image, background: str) -> Image.Image:
    rgb = np.asarray(source.convert("RGB"), dtype=np.uint8)
    maximum = rgb.max(axis=2)
    minimum = rgb.min(axis=2)
    samples = np.concatenate(
        [
            rgb[:10, :, :].reshape(-1, 3),
            rgb[-10:, :, :].reshape(-1, 3),
            rgb[:, :10, :].reshape(-1, 3),
            rgb[:, -10:, :].reshape(-1, 3),
        ],
        axis=0,
    )
    quantized = (samples // 8) * 8
    colors, counts = np.unique(quantized, axis=0, return_counts=True)
    background_colors = colors[np.argsort(counts)[-8:]]
    distance = np.stack(
        [
            np.linalg.norm(
                rgb.astype(np.int16) - color.astype(np.int16),
                axis=2,
            )
            for color in background_colors
        ],
        axis=0,
    ).min(axis=0)
    if background == "dark":
        background_seed = (distance < 34).astype(np.uint8)
    else:
        checker_background = (distance < 18) & (minimum > 190) & ((maximum - minimum) < 48)
        dark_background = maximum < 28
        background_seed = (checker_background | dark_background).astype(np.uint8)
    count, labels, _, _ = cv2.connectedComponentsWithStats(background_seed, connectivity=8)
    edge_labels = set(np.unique(labels[0, :]).tolist())
    edge_labels.update(np.unique(labels[-1, :]).tolist())
    edge_labels.update(np.unique(labels[:, 0]).tolist())
    edge_labels.update(np.unique(labels[:, -1]).tolist())
    edge_labels.discard(0)
    connected_background = np.zeros(labels.shape, dtype=bool)
    for label in edge_labels:
        if label < count:
            connected_background |= labels == label
    foreground = (~connected_background).astype(np.uint8)
    foreground = cv2.morphologyEx(
        foreground,
        cv2.MORPH_CLOSE,
        np.ones((7, 7), dtype=np.uint8),
        iterations=2,
    )
    foreground = fill_holes(largest_component(foreground))

    distance_to_edge = cv2.distanceTransform(foreground.astype(np.uint8), cv2.DIST_L2, 3)
    alpha = np.clip(distance_to_edge * 210.0, 0, 255).astype(np.uint8)
    ys, xs = np.where(alpha > 0)
    if len(xs) == 0:
        raise ValueError("Extracted sprite is empty.")

    pad = 8
    x0 = max(0, int(xs.min()) - pad)
    y0 = max(0, int(ys.min()) - pad)
    x1 = min(source.width, int(xs.max()) + pad + 1)
    y1 = min(source.height, int(ys.max()) + pad + 1)

    rgba = np.dstack((rgb, alpha))
    rgba[alpha == 0, :3] = 0
    return Image.fromarray(rgba, "RGBA").crop((x0, y0, x1, y1))


def extract_sheet_assets(sheet_path: Path, background: str) -> dict[str, Image.Image]:
    sheet = Image.open(sheet_path).convert("RGB")
    assets: dict[str, Image.Image] = {}
    for asset_id, box in SHEET_BOXES.items():
        x0, y0, x1, y1 = box
        crop = sheet.crop(
            (
                round(sheet.width * x0),
                round(sheet.height * y0),
                round(sheet.width * x1),
                round(sheet.height * y1),
            )
        )
        assets[asset_id] = extract_sprite(crop, background)
    return assets


def resize_width(sprite: Image.Image, width: int) -> Image.Image:
    height = round(sprite.height * width / sprite.width)
    return sprite.resize((width, height), Image.Resampling.LANCZOS)


def create_step_from_top(top: Image.Image, width: int, height_ratio: float) -> Image.Image:
    source = resize_width(top, width)
    target_height = max(42, round(top.height * height_ratio))
    return source.resize((source.width, target_height), Image.Resampling.LANCZOS)


def step_alias(step_id: str) -> str:
    if step_id == "step_quarter":
        return "plate_quarter_height"
    if step_id == "step_half":
        return "plate_half_height"
    raise ValueError(f"Unsupported step id: {step_id}")


def compose_stack(plates: dict[str, Image.Image]) -> tuple[Image.Image, list[dict[str, object]]]:
    base = plates["plate_base"]
    middle = plates["plate_middle"]
    top = plates["plate_top"]
    width = max(base.width, middle.width + 240, top.width + 360) + 90
    height = base.height + round(middle.height * 0.58) + round(top.height * 0.56)
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))

    centers = {
        "plate_base": (width // 2, height - base.height // 2),
        "plate_middle": (width // 2 - 56, height - base.height + round(middle.height * 0.26)),
        "plate_top": (width // 2 - 96, height - base.height - round(middle.height * 0.34)),
    }
    assembly: list[dict[str, object]] = []
    for index, plate_id in enumerate(["plate_base", "plate_middle", "plate_top"]):
        image = plates[plate_id]
        cx, cy = centers[plate_id]
        x = round(cx - image.width / 2)
        y = round(cy - image.height / 2)
        canvas.alpha_composite(image, (x, y))
        assembly.append(
            {
                "plate": plate_id,
                "level": index,
                "center": {"x": cx, "y": cy},
                "z_index": index * 10,
            }
        )

    bbox = canvas.getbbox()
    if bbox is None:
        raise ValueError("Composed mountain is empty.")
    left, top, _, _ = bbox
    for part in assembly:
        center = part["center"]
        center["x"] = int(center["x"]) - left
        center["y"] = int(center["y"]) - top
    return canvas.crop(bbox), assembly


def create_surface_fill_tile(sprite: Image.Image) -> Image.Image:
    rgb = sprite.convert("RGB")
    crop_size = min(256, rgb.width, rgb.height)
    left = max(0, (rgb.width - crop_size) // 2)
    top = min(max(0, round(rgb.height * 0.22)), rgb.height - crop_size)
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


def preview(sprite: Image.Image, path: Path, size: tuple[int, int] = (1400, 1050)) -> None:
    canvas = Image.new("RGBA", size, BACKGROUND)
    display = sprite.copy()
    display.thumbnail((size[0] - 80, size[1] - 80), Image.Resampling.LANCZOS)
    canvas.alpha_composite(display, ((size[0] - display.width) // 2, (size[1] - display.height) // 2))
    canvas.convert("RGB").save(path, quality=95)


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


def create_fit_preview(mountain: Image.Image, steps: dict[str, Image.Image], path: Path) -> None:
    canvas = Image.new("RGBA", (1600, 1050), BACKGROUND)
    mountain_display = mountain.copy()
    mountain_display.thumbnail((1160, 930), Image.Resampling.LANCZOS)
    canvas.alpha_composite(mountain_display, (70, 70))

    quarter = steps["step_quarter"].copy()
    half = steps["step_half"].copy()
    quarter.thumbnail((240, 170), Image.Resampling.LANCZOS)
    half.thumbnail((285, 205), Image.Resampling.LANCZOS)
    canvas.alpha_composite(quarter, (1280, 360))
    canvas.alpha_composite(half, (1260, 570))

    draw = ImageDraw.Draw(canvas)
    draw.text((1240, 305), "manual step plates", fill=(230, 240, 244, 255), font=load_font(24))
    draw.text((1280, 530), "quarter height", fill=(200, 218, 225, 255), font=load_font(18))
    draw.text((1260, 770), "half height", fill=(200, 218, 225, 255), font=load_font(18))
    canvas.convert("RGB").save(path, quality=95)


def build_theme(theme: dict[str, str]) -> dict[str, object]:
    theme_dir = THEME_ROOT / theme["id"]
    theme_dir.mkdir(parents=True, exist_ok=True)

    source_path = Path(theme["source_sheet"])
    if not source_path.exists():
        raise FileNotFoundError(source_path)
    shutil.copy2(source_path, theme_dir / "theme_source_sheet.png")

    assets = extract_sheet_assets(source_path, theme["background"])
    plates = {
        "plate_base": assets["plate_base"],
        "plate_middle": assets["plate_middle"],
        "plate_top": assets["plate_top"],
    }
    stack, assembly = compose_stack(plates)
    one_level = plates["plate_base"]
    steps = {
        "step_quarter": create_step_from_top(plates["plate_top"], 245, 0.25),
        "step_half": create_step_from_top(plates["plate_top"], 285, 0.5),
    }

    for plate_id, image in plates.items():
        image.save(theme_dir / f"{plate_id}.png", optimize=True)
    for step_id, image in steps.items():
        image.save(theme_dir / f"{step_id}.png", optimize=True)
        image.save(theme_dir / f"{step_alias(step_id)}.png", optimize=True)

    stack_path = theme_dir / "natural_plateau_three_level_no_ramps.png"
    one_path = theme_dir / "natural_plateau_one_level.png"
    stack.save(stack_path, optimize=True)
    one_level.save(one_path, optimize=True)
    create_surface_fill_tile(one_level).save(theme_dir / "surface_fill_tile.png", optimize=True)

    preview(stack, theme_dir / "natural_plateau_three_level_no_ramps_preview.png")
    preview(one_level, theme_dir / "natural_plateau_one_level_preview.png")
    create_fit_preview(stack, steps, theme_dir / "fit_preview.png")

    plate_regions = pack_sheet(list(plates.items()), theme_dir / "mountain_plate_sheet.png", 2)
    step_regions = pack_sheet(list(steps.items()), theme_dir / "manual_step_plate_sheet.png", 2)
    one_regions = pack_sheet([("natural_plateau_one_level", one_level)], theme_dir / "one_level_mountain_sheet.png", 1)

    plate_modules = [
        {
            "id": plate_id,
            "file": rel(theme_dir / f"{plate_id}.png"),
            "image_size": [image.width, image.height],
            "walkable": True,
        }
        for plate_id, image in plates.items()
    ]
    step_modules = [
        {
            "id": step_id,
            "display_name": f"{theme['label']} {step_id.replace('_', ' ').title()}",
            "file": rel(theme_dir / f"{step_id}.png"),
            "alias_id": step_alias(step_id),
            "alias_file": rel(theme_dir / f"{step_alias(step_id)}.png"),
            "preview": rel(theme_dir / "fit_preview.png"),
            "image_size": [image.width, image.height],
            "height_class": "quarter" if step_id.endswith("quarter") else "half",
            "relative_level_height": 0.25 if step_id.endswith("quarter") else 0.5,
            "placement": "manual",
        }
        for step_id, image in steps.items()
    ]

    one_center = {"x": one_level.width // 2, "y": one_level.height // 2}
    manifest = {
        "schema_version": 2,
        "pack_id": f"natural_plateau_front_2_5d_{theme['id']}",
        "style_id": "mountain_prefab_2",
        "style_label": "Mountain Prefab 2 - Natural Plateau",
        "theme_id": theme["id"],
        "theme_label": theme["label"],
        "projection": "front_2_5d",
        "visual_only": True,
        "plate_modules": plate_modules,
        "base_prefabs": [
            {
                "id": "natural_plateau_three_level_no_ramps",
                "display_name": f"Mountain Prefab 2 - {theme['label']}",
                "file": rel(stack_path),
                "preview": rel(theme_dir / "natural_plateau_three_level_no_ramps_preview.png"),
                "level_count": 3,
                "image_size": [stack.width, stack.height],
                "strict_nested_footprints": True,
                "decorative_paths": False,
                "gameplay_metadata": False,
                "plate_assembly": assembly,
                "sockets": [],
            },
            {
                "id": "natural_plateau_extra_wide",
                "display_name": f"Mountain Prefab 2 - {theme['label']} Base Plate",
                "file": rel(one_path),
                "preview": rel(theme_dir / "natural_plateau_one_level_preview.png"),
                "level_count": 1,
                "image_size": [one_level.width, one_level.height],
                "strict_nested_footprints": True,
                "decorative_paths": False,
                "gameplay_metadata": False,
                "fill_tile": rel(theme_dir / "surface_fill_tile.png"),
                "fill_tile_size": [512, 512],
                "plate_assembly": [
                    {"plate": "plate_base", "level": 0, "center": one_center, "z_index": 0}
                ],
                "sockets": [],
            },
        ],
        "ramp_modules": [],
        "jump_step_modules": step_modules,
        "sockets": [],
        "atlas_sheets": [
            {
                "id": f"mountain_prefab_2_plates_{theme['id']}",
                "file": rel(theme_dir / "mountain_plate_sheet.png"),
                "transparent": True,
                "regions": plate_regions,
            },
            {
                "id": f"mountain_prefab_2_manual_steps_{theme['id']}",
                "file": rel(theme_dir / "manual_step_plate_sheet.png"),
                "transparent": True,
                "regions": step_regions,
            },
            {
                "id": f"mountain_prefab_2_one_level_{theme['id']}",
                "file": rel(theme_dir / "one_level_mountain_sheet.png"),
                "transparent": True,
                "regions": one_regions,
            },
        ],
    }
    manifest_path = theme_dir / "mountain_prefab_2_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return {
        "id": theme["id"],
        "label": theme["label"],
        "display_name": f"Mountain Prefab 2 - {theme['label']}",
        "style_id": "mountain_prefab_2",
        "style_label": "Mountain Prefab 2 - Natural Plateau",
        "manifest": rel(manifest_path),
        "preview": rel(theme_dir / "fit_preview.png"),
        "plate_sheet": rel(theme_dir / "mountain_plate_sheet.png"),
        "step_sheet": rel(theme_dir / "manual_step_plate_sheet.png"),
    }


def create_catalog_preview(entries: list[dict[str, object]]) -> None:
    canvas = Image.new("RGBA", (2000, 1280), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.text((50, 32), "MOUNTAIN PREFAB 2 - NATURAL PLATEAU VARIATIONS", fill="white", font=load_font(39))

    cell_width = 480
    cell_height = 560
    for index, entry in enumerate(entries):
        row = index // 4
        column = index % 4
        x = 40 + column * 490
        y = 100 + row * 575
        draw.rectangle((x, y, x + cell_width, y + cell_height), fill=PANEL)
        image = Image.open(PROJECT_ROOT / str(entry["preview"])).convert("RGBA")
        image.thumbnail((430, 450), Image.Resampling.LANCZOS)
        canvas.alpha_composite(image, (x + (cell_width - image.width) // 2, y + 78 + (450 - image.height) // 2))
        draw.text((x + 18, y + 12), "MOUNTAIN PREFAB 2", fill=(158, 184, 193, 255), font=load_font(20))
        draw.text((x + 18, y + 39), str(entry["label"]), fill=(232, 240, 243, 255), font=load_font(25))
    canvas.convert("RGB").save(CATALOG_PREVIEW_PATH, quality=95)


def create_step_catalog_preview(entries: list[dict[str, object]]) -> None:
    canvas = Image.new("RGBA", (1500, 1320), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.text((42, 28), "MOUNTAIN PREFAB 2 - MANUAL QUARTER/HALF PLATES", fill="white", font=load_font(34))
    draw.text((42, 76), "left = quarter height, right = half height", fill=(190, 210, 218, 255), font=load_font(22))
    row_height = 160
    for index, entry in enumerate(entries):
        y = 125 + index * row_height
        x = 42
        theme_dir = PROJECT_ROOT / str(entry["manifest"])
        theme_dir = theme_dir.parent
        draw.rectangle((x, y, 1458, y + row_height - 16), fill=PANEL)
        draw.text((x + 20, y + 18), str(entry["label"]), fill=(232, 240, 243, 255), font=load_font(24))
        quarter = Image.open(theme_dir / "plate_quarter_height.png").convert("RGBA")
        half = Image.open(theme_dir / "plate_half_height.png").convert("RGBA")
        quarter.thumbnail((300, 110), Image.Resampling.LANCZOS)
        half.thumbnail((340, 135), Image.Resampling.LANCZOS)
        canvas.alpha_composite(quarter, (500, y + 26 + (110 - quarter.height)))
        canvas.alpha_composite(half, (930, y + 8 + (135 - half.height)))
        draw.text((500, y + 122), "plate_quarter_height", fill=(190, 210, 218, 255), font=load_font(16))
        draw.text((930, y + 122), "plate_half_height", fill=(190, 210, 218, 255), font=load_font(16))
    canvas.convert("RGB").save(STEP_PREVIEW_PATH, quality=95)


def main() -> int:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    THEME_ROOT.mkdir(parents=True, exist_ok=True)
    entries = [build_theme(theme) for theme in THEMES]
    create_catalog_preview(entries)
    create_step_catalog_preview(entries)

    catalog = {
        "schema_version": 1,
        "catalog_id": "mountain_prefab_2_material_themes",
        "style_id": "mountain_prefab_2",
        "style_label": "Mountain Prefab 2 - Natural Plateau",
        "themes": entries,
        "step_plate_preview": rel(STEP_PREVIEW_PATH),
    }
    CATALOG_PATH.write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")

    default_manifest = json.loads((THEME_ROOT / "grass_granite" / "mountain_prefab_2_manifest.json").read_text(encoding="utf-8"))
    default_manifest["pack_id"] = "natural_plateau_front_2_5d"
    default_manifest["theme_catalog"] = rel(CATALOG_PATH)
    MANIFEST_PATH.write_text(json.dumps(default_manifest, indent=2) + "\n", encoding="utf-8")

    print(f"Prepared {len(entries)} Mountain Prefab 2 material variations")
    print(f"Catalog: {CATALOG_PATH}")
    print(f"Preview: {CATALOG_PREVIEW_PATH}")
    print(f"Step plates preview: {STEP_PREVIEW_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
