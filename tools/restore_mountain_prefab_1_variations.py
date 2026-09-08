from __future__ import annotations

import json
import hashlib
import math
import random
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


PROJECT_ROOT = Path(__file__).resolve().parents[1]
OUT_ROOT = (
    PROJECT_ROOT
    / "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs"
)
BASE_ROOT = OUT_ROOT / "modular_front_2_5d"
THEME_ROOT = OUT_ROOT / "modular_themes"
CATALOG_PATH = THEME_ROOT / "modular_mountain_theme_catalog.json"
PREVIEW_PATH = THEME_ROOT / "mountain_prefab_1_all_variations_preview.png"
STEP_PREVIEW_PATH = THEME_ROOT / "mountain_prefab_1_all_step_plates_preview.png"

STYLE_ID = "mountain_prefab_1"
STYLE_LABEL = "Mountain Prefab 1"
BACKGROUND = (19, 32, 39, 255)
PANEL = (23, 39, 47, 255)

THEMES = [
    {
        "id": "sandstone",
        "label": "Low Poly Sandstone",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_YyWYNw4rygOUhjHgftSVkONo.png",
        "top": (242, 181, 44),
        "top_light": (255, 205, 70),
        "side": (191, 132, 29),
        "side_dark": (128, 85, 21),
        "edge": (255, 230, 125),
        "accent": (230, 158, 26),
        "surface": "sand",
        "cliff": "folded",
    },
    {
        "id": "grass_granite",
        "label": "Grass + Granite",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_TQKRB1BWJnqzF5PrmxzIt3H0.png",
        "top": (86, 142, 43),
        "top_light": (125, 178, 57),
        "side": (122, 122, 107),
        "side_dark": (61, 67, 65),
        "edge": (154, 188, 74),
        "accent": (190, 169, 92),
        "surface": "grass",
        "cliff": "granite",
    },
    {
        "id": "grey_rock",
        "label": "Grey Rock",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_tpytikX56yfeWTRJBJOKfyEg.png",
        "top": (151, 153, 150),
        "top_light": (190, 194, 191),
        "side": (104, 107, 107),
        "side_dark": (55, 58, 60),
        "edge": (219, 223, 219),
        "accent": (83, 86, 84),
        "surface": "stone",
        "cliff": "granite",
    },
    {
        "id": "volcanic_basalt",
        "label": "Volcanic Basalt",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_KeUDwxHFAcJpYec26mDGuevo.png",
        "top": (47, 44, 43),
        "top_light": (78, 72, 68),
        "side": (35, 35, 36),
        "side_dark": (18, 18, 19),
        "edge": (101, 92, 83),
        "accent": (239, 78, 27),
        "surface": "volcano",
        "cliff": "basalt",
    },
    {
        "id": "meadow_hill",
        "label": "Gentle Meadow Hill",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_UDLiYpSU2oSiW0tAoOkmPVvl.png",
        "top": (88, 178, 37),
        "top_light": (160, 218, 40),
        "side": (211, 154, 83),
        "side_dark": (121, 84, 47),
        "edge": (137, 216, 56),
        "accent": (245, 228, 87),
        "surface": "meadow",
        "cliff": "soft",
    },
    {
        "id": "red_rock_mesa",
        "label": "Red Rock Mesa",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_jQtKqXog9cmfQ9mVimRs4bhc.png",
        "top": (219, 95, 26),
        "top_light": (249, 132, 37),
        "side": (178, 67, 28),
        "side_dark": (105, 44, 28),
        "edge": (255, 162, 60),
        "accent": (109, 55, 31),
        "surface": "mesa",
        "cliff": "column",
    },
    {
        "id": "alpine_snow",
        "label": "Alpine Snow",
        "source_sheet": r"C:\Users\f_ald\.codex\generated_images\01a04987-d744-7b40-a3c8-866f00508feb\call_WOWfLfvGPvh6LJhUIitf1apD.png",
        "top": (218, 238, 253),
        "top_light": (244, 251, 255),
        "side": (104, 139, 171),
        "side_dark": (54, 82, 114),
        "edge": (252, 255, 255),
        "accent": (94, 103, 111),
        "surface": "snow",
        "cliff": "ice",
    },
]

PLATES = {
    "plate_base": {"size": (940, 515), "center": (560, 560), "level": 0, "z": 0, "seed": 18},
    "plate_middle": {"size": (700, 360), "center": (560, 350), "level": 1, "z": 10, "seed": 29},
    "plate_top": {"size": (460, 245), "center": (560, 190), "level": 2, "z": 20, "seed": 43},
}

SHEET_BOXES = {
    "three_level_wide_no_ramps": (0.00, 0.00, 0.50, 0.50),
    "plate_base": (0.50, 0.00, 1.00, 0.50),
    "plate_middle": (0.00, 0.50, 0.50, 1.00),
    "plate_top": (0.50, 0.50, 1.00, 1.00),
}


def rel(path: Path) -> str:
    return str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")


def stable_seed(*parts: object) -> int:
    digest = hashlib.sha256("|".join(str(part) for part in parts).encode("utf-8")).digest()
    return int.from_bytes(digest[:4], "big")


def rgba(color: tuple[int, int, int], alpha: int = 255) -> tuple[int, int, int, int]:
    return color[0], color[1], color[2], alpha


def mix(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    return tuple(max(0, min(255, round(a[i] * (1.0 - t) + b[i] * t))) for i in range(3))


def blob_points(width: int, height: int, seed: int, count: int = 42) -> list[tuple[int, int]]:
    rng = random.Random(seed)
    cx = width / 2
    cy = height * 0.32
    rx = width * 0.45
    ry = height * 0.255
    points: list[tuple[int, int]] = []
    for index in range(count):
        angle = 2.0 * math.pi * index / count
        r = 1.0 + rng.uniform(-0.07, 0.065)
        if math.sin(angle) > 0.58:
            r += rng.uniform(-0.04, 0.035)
        x = round(cx + math.cos(angle) * rx * r)
        y = round(cy + math.sin(angle) * ry * r)
        points.append((x, y))
    return points


def lower_edge(points: list[tuple[int, int]], drop: int, spread: float = 0.11) -> list[tuple[int, int]]:
    result: list[tuple[int, int]] = []
    for x, y in points:
        local = math.sin((x + y) * 0.038) * drop * spread
        result.append((x, round(y + drop + local)))
    return result


def mask_from_polygon(size: tuple[int, int], points: list[tuple[int, int]]) -> Image.Image:
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def extract_sheet_asset(sheet: Image.Image, box: tuple[float, float, float, float]) -> Image.Image:
    sx0, sy0, sx1, sy1 = box
    crop = sheet.crop(
        (
            round(sheet.width * sx0),
            round(sheet.height * sy0),
            round(sheet.width * sx1),
            round(sheet.height * sy1),
        )
    ).convert("RGB")
    rgb = np.asarray(crop, dtype=np.uint8)
    samples = np.concatenate(
        [
            rgb[:8, :, :].reshape(-1, 3),
            rgb[-8:, :, :].reshape(-1, 3),
            rgb[:, :8, :].reshape(-1, 3),
            rgb[:, -8:, :].reshape(-1, 3),
        ],
        axis=0,
    )
    light = samples[np.mean(samples, axis=1) > 218]
    if len(light) == 0:
        light = samples
    quantized = (light // 8) * 8
    colors, counts = np.unique(quantized, axis=0, return_counts=True)
    background_colors = colors[np.argsort(counts)[-4:]]
    diff = np.stack(
        [np.linalg.norm(rgb.astype(np.int16) - color.astype(np.int16), axis=2) for color in background_colors],
        axis=0,
    )
    min_diff = diff.min(axis=0)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    saturation = hsv[:, :, 1]
    value = hsv[:, :, 2]
    mask = ((min_diff > 16) | (saturation > 18) | (value < 220)).astype(np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8), iterations=1)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8), iterations=2)
    component_count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    if component_count <= 1:
        raise ValueError("No foreground object found in generated source sheet quadrant.")
    largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    foreground = (labels == largest).astype(np.uint8)
    foreground = cv2.dilate(foreground, np.ones((3, 3), np.uint8), iterations=1)
    ys, xs = np.where(foreground > 0)
    pad = 6
    x0 = max(0, int(xs.min()) - pad)
    y0 = max(0, int(ys.min()) - pad)
    x1 = min(crop.width, int(xs.max()) + pad + 1)
    y1 = min(crop.height, int(ys.max()) + pad + 1)
    alpha = (foreground * 255).astype(np.uint8)
    rgba_data = np.dstack((rgb, alpha))
    rgba_data[alpha == 0, :3] = 0
    return Image.fromarray(rgba_data, "RGBA").crop((x0, y0, x1, y1))


def load_generated_theme_assets(theme: dict[str, object]) -> tuple[dict[str, Image.Image], Image.Image | None]:
    source = Path(str(theme.get("source_sheet", "")))
    if not source.exists():
        return {}, None
    sheet = Image.open(source).convert("RGB")
    assets = {asset_id: extract_sheet_asset(sheet, box) for asset_id, box in SHEET_BOXES.items()}
    return {
        "plate_base": assets["plate_base"],
        "plate_middle": assets["plate_middle"],
        "plate_top": assets["plate_top"],
    }, assets["three_level_wide_no_ramps"]


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


def textured_fill(mask: Image.Image, base: tuple[int, int, int], light: tuple[int, int, int], seed: int) -> Image.Image:
    rng = random.Random(seed)
    width, height = mask.size
    img = Image.new("RGBA", mask.size, rgba(base, 0))
    px = img.load()
    mask_px = mask.load()
    for y in range(height):
        shade_y = 0.11 * (1.0 - y / max(1, height))
        for x in range(width):
            if mask_px[x, y] == 0:
                continue
            noise = rng.randint(-10, 10)
            wave = math.sin((x * 0.045) + (y * 0.026)) * 8
            c = mix(base, light, 0.17 + shade_y)
            px[x, y] = (
                max(0, min(255, c[0] + round(noise + wave))),
                max(0, min(255, c[1] + round(noise + wave))),
                max(0, min(255, c[2] + round(noise + wave))),
                255,
            )
    return img.filter(ImageFilter.GaussianBlur(0.45))


def draw_surface_details(draw: ImageDraw.ImageDraw, mask: Image.Image, theme: dict[str, object], seed: int) -> None:
    rng = random.Random(seed)
    width, height = mask.size
    mask_px = mask.load()
    surface = str(theme["surface"])
    accent = theme["accent"]
    for _ in range(125):
        x = rng.randrange(0, width)
        y = rng.randrange(0, height)
        if mask_px[x, y] == 0:
            continue
        if surface in {"grass", "meadow"}:
            color = mix(theme["top"], theme["top_light"], rng.random() * 0.7)
            length = rng.randint(3, 9)
            draw.line((x, y, x + rng.randint(-5, 5), y - length), fill=rgba(color, 130), width=1)
            if surface == "meadow" and rng.random() < 0.16:
                draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=rgba(accent, 230))
        elif surface == "snow":
            if rng.random() < 0.45:
                color = (176, 214, 246)
                draw.line((x - 8, y, x + 8, y + rng.randint(-2, 3)), fill=rgba(color, 74), width=1)
            else:
                draw.ellipse((x - 9, y - 4, x + 9, y + 4), fill=rgba((99, 116, 128), 135))
        elif surface == "volcano":
            if rng.random() < 0.23:
                draw.line((x - 12, y, x + 12, y + rng.randint(-8, 8)), fill=rgba(accent, 180), width=2)
            else:
                draw.ellipse((x - 2, y - 2, x + 2, y + 2), fill=rgba((18, 17, 17), 160))
        elif surface in {"sand", "mesa"}:
            color = mix(accent, theme["top_light"], 0.35)
            draw.arc((x - 14, y - 7, x + 14, y + 7), 185, 355, fill=rgba(color, 85), width=1)
        else:
            color = mix(theme["top"], theme["side_dark"], rng.random() * 0.6)
            draw.line((x - 9, y, x + 8, y + rng.randint(-4, 4)), fill=rgba(color, 90), width=1)

    if surface in {"grass", "meadow", "sand", "mesa"}:
        for _ in range(9):
            for _attempt in range(60):
                x = rng.randrange(width // 7, width * 6 // 7)
                y = rng.randrange(height // 5, height * 3 // 5)
                if mask_px[x, y] > 0:
                    break
            r = rng.randint(9, 18)
            color = mix(theme["accent"], theme["top_light"], 0.45)
            draw.ellipse((x - r, y - r // 2, x + r, y + r // 2), fill=rgba(color, 80))


def draw_cliff_facets(
    draw: ImageDraw.ImageDraw,
    top_points: list[tuple[int, int]],
    bottom_points: list[tuple[int, int]],
    theme: dict[str, object],
    seed: int,
) -> None:
    rng = random.Random(seed)
    side = theme["side"]
    dark = theme["side_dark"]
    edge = theme["edge"]
    count = len(top_points)
    for i in range(count):
        a = top_points[i]
        b = top_points[(i + 1) % count]
        if a[1] < min(p[1] for p in top_points) + 8 and b[1] < min(p[1] for p in top_points) + 8:
            continue
        c = bottom_points[(i + 1) % count]
        d = bottom_points[i]
        brightness = 0.15 + 0.45 * max(0.0, math.cos((i / count) * math.pi * 2.0 - 2.6))
        base = mix(dark, side, brightness)
        if str(theme["cliff"]) == "basalt" and rng.random() < 0.18:
            base = mix(base, theme["accent"], 0.2)
        draw.polygon([a, b, c, d], fill=rgba(base))
        mid_x = round((a[0] + b[0] + c[0] + d[0]) / 4)
        draw.line((mid_x, a[1] + 3, mid_x + rng.randint(-8, 8), d[1] - 3), fill=rgba(mix(base, dark, 0.55), 100), width=2)
        if str(theme["cliff"]) in {"column", "folded"}:
            draw.line((a[0], a[1], d[0], d[1]), fill=rgba(mix(edge, side, 0.45), 75), width=1)
    draw.line(top_points + [top_points[0]], fill=rgba(edge, 210), width=2)


def draw_plate(theme: dict[str, object], width: int, height: int, level_seed: int) -> Image.Image:
    top_points = blob_points(width, height, level_seed)
    drop = round(height * 0.43)
    bottom_points = lower_edge(top_points, drop)
    side_poly = top_points + list(reversed(bottom_points))
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    shadow_mask = mask_from_polygon((width, height), side_poly)
    shadow.alpha_composite(Image.new("RGBA", (width, height), (0, 0, 0, 48)), (0, 0))
    shadow.putalpha(shadow_mask.filter(ImageFilter.GaussianBlur(5)))
    canvas.alpha_composite(shadow, (0, 5))

    draw = ImageDraw.Draw(canvas)
    draw_cliff_facets(draw, top_points, bottom_points, theme, level_seed + 300)

    top_mask = mask_from_polygon((width, height), top_points)
    top = textured_fill(top_mask, theme["top"], theme["top_light"], level_seed + 600)
    canvas.alpha_composite(top)
    draw = ImageDraw.Draw(canvas)
    draw_surface_details(draw, top_mask, theme, level_seed + 900)
    draw.line(top_points + [top_points[0]], fill=rgba(theme["edge"], 180), width=2)
    return canvas.crop(canvas.getbbox())


def compose_stack(plates: dict[str, Image.Image]) -> tuple[Image.Image, list[dict[str, object]]]:
    width = 1120
    height = 810
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    assembly: list[dict[str, object]] = []
    for plate_id, spec in PLATES.items():
        image = plates[plate_id]
        center = spec["center"]
        x = round(center[0] - image.width / 2)
        y = round(center[1] - image.height / 2)
        canvas.alpha_composite(image, (x, y))
        assembly.append(
            {
                "plate": plate_id,
                "level": spec["level"],
                "center": {"x": center[0], "y": center[1]},
                "z_index": spec["z"],
            }
        )
    return canvas.crop(canvas.getbbox()), assembly


def preview(image: Image.Image, path: Path) -> None:
    pad = 32
    canvas = Image.new("RGBA", (image.width + pad * 2, image.height + pad * 2), BACKGROUND)
    canvas.alpha_composite(image, (pad, pad))
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
        (sum(column_widths) + padding * (columns + 1), sum(row_heights) + padding * (rows + 1)),
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


def build_theme(theme: dict[str, object], output: Path) -> dict[str, object]:
    output.mkdir(parents=True, exist_ok=True)
    for obsolete in [
        "ramp_left.png",
        "ramp_front.png",
        "ramp_right.png",
        "level_ramp_sheet.png",
        "assembled_with_level_ramps_preview.png",
    ]:
        (output / obsolete).unlink(missing_ok=True)
    plates, reference_stack = load_generated_theme_assets(theme)
    if not plates:
        plates = {
            plate_id: draw_plate(theme, *spec["size"], int(spec["seed"]) + stable_seed(theme["id"]) % 1000)
            for plate_id, spec in PLATES.items()
        }
    for item_id, image in plates.items():
        image.save(output / f"{item_id}.png", optimize=True)

    steps = {
        "step_quarter": create_step_from_top(plates["plate_top"], 245, 0.25),
        "step_half": create_step_from_top(plates["plate_top"], 285, 0.5),
    }
    for step_id, image in steps.items():
        image.save(output / f"{step_id}.png", optimize=True)
        image.save(output / f"{step_alias(step_id)}.png", optimize=True)

    stack, assembly = compose_stack(plates)
    stack_path = output / "three_level_wide_no_ramps.png"
    stack.save(stack_path, optimize=True)
    stack_preview_path = output / "three_level_wide_no_ramps_preview.png"
    preview(reference_stack or stack, stack_preview_path)

    one_path = output / "one_level_wide_no_ramps.png"
    plates["plate_base"].save(one_path, optimize=True)
    one_preview_path = output / "one_level_wide_no_ramps_preview.png"
    preview(plates["plate_base"], one_preview_path)

    plate_regions = pack_sheet(list(plates.items()), output / "mountain_plate_sheet.png", 2)
    step_regions = pack_sheet(list(steps.items()), output / "manual_step_plate_sheet.png", 2)
    one_regions = pack_sheet(
        [("one_level_wide_no_ramps", plates["plate_base"])],
        output / "one_level_mountain_sheet.png",
        1,
    )

    one_center = {"x": plates["plate_base"].width // 2, "y": plates["plate_base"].height // 2}
    plate_modules = [
        {
            "id": plate_id,
            "file": rel(output / f"{plate_id}.png"),
            "image_size": [image.width, image.height],
            "walkable": True,
        }
        for plate_id, image in plates.items()
    ]
    step_modules = [
        {
            "id": step_id,
            "display_name": f"{theme['label']} {step_alias(step_id).replace('_', ' ').title()}",
            "file": rel(output / f"{step_id}.png"),
            "alias_id": step_alias(step_id),
            "alias_file": rel(output / f"{step_alias(step_id)}.png"),
            "image_size": [image.width, image.height],
            "height_class": "quarter" if step_id.endswith("quarter") else "half",
            "relative_level_height": 0.25 if step_id.endswith("quarter") else 0.5,
            "placement": "manual",
        }
        for step_id, image in steps.items()
    ]
    manifest = {
        "schema_version": 2,
        "pack_id": f"modular_front_2_5d_{theme['id']}",
        "style_id": STYLE_ID,
        "style_label": STYLE_LABEL,
        "theme_id": theme["id"],
        "theme_label": theme["label"],
        "projection": "front_2_5d",
        "plate_modules": plate_modules,
        "base_prefabs": [
            {
                "id": "three_level_wide_no_ramps",
                "display_name": f"{STYLE_LABEL} - {theme['label']}",
                "style_id": STYLE_ID,
                "file": rel(stack_path),
                "preview": rel(stack_preview_path),
                "level_count": 3,
                "image_size": [stack.width, stack.height],
                "strict_nested_footprints": True,
                "plate_assembly": assembly,
                "sockets": [],
            },
            {
                "id": "one_level_wide_no_ramps",
                "display_name": f"{STYLE_LABEL} - {theme['label']} (One Level)",
                "style_id": STYLE_ID,
                "file": rel(one_path),
                "preview": rel(one_preview_path),
                "level_count": 1,
                "image_size": [plates["plate_base"].width, plates["plate_base"].height],
                "strict_nested_footprints": True,
                "plate_assembly": [
                    {"plate": "plate_base", "level": 0, "center": one_center, "z_index": 0}
                ],
                "sockets": [],
            },
        ],
        "ramp_modules": [],
        "jump_step_modules": step_modules,
        "atlas_sheets": [
            {
                "id": f"mountain_plates_{theme['id']}",
                "file": rel(output / "mountain_plate_sheet.png"),
                "transparent": True,
                "regions": plate_regions,
            },
            {
                "id": f"mountain_prefab_1_manual_steps_{theme['id']}",
                "file": rel(output / "manual_step_plate_sheet.png"),
                "transparent": True,
                "regions": step_regions,
            },
            {
                "id": f"one_level_mountain_{theme['id']}",
                "file": rel(output / "one_level_mountain_sheet.png"),
                "transparent": True,
                "regions": one_regions,
            },
        ],
    }
    manifest_path = output / "modular_mountain_pack_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return {
        "id": theme["id"],
        "label": theme["label"],
        "display_name": f"{STYLE_LABEL} - {theme['label']}",
        "style_id": STYLE_ID,
        "style_label": STYLE_LABEL,
        "manifest": rel(manifest_path),
        "three_level_preview": rel(stack_preview_path),
        "plate_sheet": rel(output / "mountain_plate_sheet.png"),
        "step_sheet": rel(output / "manual_step_plate_sheet.png"),
        "one_level_sheet": rel(output / "one_level_mountain_sheet.png"),
    }


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    path = Path("C:/Windows/Fonts/arial.ttf")
    return ImageFont.truetype(path, size) if path.exists() else ImageFont.load_default()


def fit(image: Image.Image, width: int, height: int) -> Image.Image:
    scale = min(width / image.width, height / image.height)
    return image.resize((round(image.width * scale), round(image.height * scale)), Image.Resampling.LANCZOS)


def create_catalog_preview(entries: list[dict[str, object]]) -> None:
    canvas = Image.new("RGBA", (2000, 1280), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.text((50, 32), "MOUNTAIN PREFAB 1 - ALL MATERIAL VARIATIONS", fill="white", font=font(42))
    cell_width = 480
    cell_height = 560
    for index, theme in enumerate(entries):
        row = index // 4
        column = index % 4
        x = 40 + column * 490
        y = 100 + row * 575
        draw.rectangle((x, y, x + cell_width, y + cell_height), fill=PANEL)
        preview_image = fit(Image.open(PROJECT_ROOT / str(theme["three_level_preview"])).convert("RGBA"), 430, 455)
        canvas.alpha_composite(preview_image, (x + (cell_width - preview_image.width) // 2, y + 75 + (455 - preview_image.height) // 2))
        draw.text((x + 18, y + 12), STYLE_LABEL.upper(), fill=(158, 184, 193, 255), font=font(20))
        draw.text((x + 18, y + 38), str(theme["label"]), fill=(232, 240, 243, 255), font=font(25))
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(PREVIEW_PATH, quality=95)


def create_step_catalog_preview(entries: list[dict[str, object]]) -> None:
    canvas = Image.new("RGBA", (1500, 1320), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.text((42, 28), "MOUNTAIN PREFAB 1 - MANUAL QUARTER/HALF PLATES", fill="white", font=font(34))
    draw.text((42, 76), "left = quarter height, right = half height", fill=(190, 210, 218, 255), font=font(22))
    row_height = 160
    for index, entry in enumerate(entries):
        y = 125 + index * row_height
        x = 42
        manifest_path = PROJECT_ROOT / str(entry["manifest"])
        theme_dir = manifest_path.parent
        draw.rectangle((x, y, 1458, y + row_height - 16), fill=PANEL)
        draw.text((x + 20, y + 18), str(entry["label"]), fill=(232, 240, 243, 255), font=font(24))
        quarter = Image.open(theme_dir / "plate_quarter_height.png").convert("RGBA")
        half = Image.open(theme_dir / "plate_half_height.png").convert("RGBA")
        quarter.thumbnail((300, 110), Image.Resampling.LANCZOS)
        half.thumbnail((340, 135), Image.Resampling.LANCZOS)
        canvas.alpha_composite(quarter, (500, y + 26 + (110 - quarter.height)))
        canvas.alpha_composite(half, (930, y + 8 + (135 - half.height)))
        draw.text((500, y + 122), "plate_quarter_height", fill=(190, 210, 218, 255), font=font(16))
        draw.text((930, y + 122), "plate_half_height", fill=(190, 210, 218, 255), font=font(16))
    canvas.convert("RGB").save(STEP_PREVIEW_PATH, quality=95)


def main() -> int:
    BASE_ROOT.mkdir(parents=True, exist_ok=True)
    THEME_ROOT.mkdir(parents=True, exist_ok=True)
    entries: list[dict[str, object]] = []
    for theme in THEMES:
        output = BASE_ROOT if theme["id"] == "sandstone" else THEME_ROOT / str(theme["id"])
        entries.append(build_theme(theme, output))

    catalog = {
        "schema_version": 1,
        "catalog_id": "modular_front_2_5d_material_themes",
        "style_id": STYLE_ID,
        "style_label": STYLE_LABEL,
        "themes": entries,
        "step_plate_preview": rel(STEP_PREVIEW_PATH),
    }
    CATALOG_PATH.write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")
    create_catalog_preview(entries)
    create_step_catalog_preview(entries)
    print(f"Prepared {len(entries)} Mountain Prefab 1 material variations")
    print(f"Catalog: {CATALOG_PATH}")
    print(f"Preview: {PREVIEW_PATH}")
    print(f"Step plates preview: {STEP_PREVIEW_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
