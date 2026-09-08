from __future__ import annotations

import json
import hashlib
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


PROJECT_ROOT = Path(__file__).resolve().parents[1]
OUT_ROOT = PROJECT_ROOT / "addons/beep_game_builder_cs/generated/mountain_access/ramps"
BACKGROUND = (19, 32, 39, 255)
PANEL = (23, 39, 47, 255)

HEIGHTS = [
    ("quarter", 0.25, 152, 88),
    ("half", 0.50, 160, 108),
    ("full", 1.00, 168, 130),
]

DIRECTIONS = ["front", "left", "right"]


PREFAB_STYLES = [
    {
        "id": "mountain_prefab_1",
        "label": "Mountain Prefab 1",
        "root": PROJECT_ROOT
        / "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs",
        "themes": [
            ("sandstone", "Low Poly Sandstone", "modular_front_2_5d/modular_mountain_pack_manifest.json"),
            ("grass_granite", "Grass + Granite", "modular_themes/grass_granite/modular_mountain_pack_manifest.json"),
            ("grey_rock", "Grey Rock", "modular_themes/grey_rock/modular_mountain_pack_manifest.json"),
            ("volcanic_basalt", "Volcanic Basalt", "modular_themes/volcanic_basalt/modular_mountain_pack_manifest.json"),
            ("meadow_hill", "Gentle Meadow Hill", "modular_themes/meadow_hill/modular_mountain_pack_manifest.json"),
            ("red_rock_mesa", "Red Rock Mesa", "modular_themes/red_rock_mesa/modular_mountain_pack_manifest.json"),
            ("alpine_snow", "Alpine Snow", "modular_themes/alpine_snow/modular_mountain_pack_manifest.json"),
        ],
    },
    {
        "id": "mountain_prefab_2",
        "label": "Mountain Prefab 2",
        "root": PROJECT_ROOT
        / "addons/beep_game_builder_cs/generated/mountains/natural_plateau/authored_prefabs/mountain_prefab_2/themes",
        "themes": [
            ("sandstone", "Low Poly Sandstone", "sandstone/mountain_prefab_2_manifest.json"),
            ("grass_granite", "Grass + Granite", "grass_granite/mountain_prefab_2_manifest.json"),
            ("grey_rock", "Grey Rock", "grey_rock/mountain_prefab_2_manifest.json"),
            ("volcanic_basalt", "Volcanic Basalt", "volcanic_basalt/mountain_prefab_2_manifest.json"),
            ("meadow_hill", "Gentle Meadow Hill", "meadow_hill/mountain_prefab_2_manifest.json"),
            ("red_rock_mesa", "Red Rock Mesa", "red_rock_mesa/mountain_prefab_2_manifest.json"),
            ("alpine_snow", "Alpine Snow", "alpine_snow/mountain_prefab_2_manifest.json"),
        ],
    },
]


def rel(path: Path) -> str:
    return str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")


def stable_seed(*parts: object) -> int:
    digest = hashlib.sha256("|".join(str(part) for part in parts).encode("utf-8")).digest()
    return int.from_bytes(digest[:4], "big")


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    path = Path("C:/Windows/Fonts/arial.ttf")
    return ImageFont.truetype(path, size) if path.exists() else ImageFont.load_default()


def clamp_color(value: float) -> int:
    return max(0, min(255, round(value)))


def adjust(color: tuple[int, int, int], factor: float) -> tuple[int, int, int]:
    return tuple(clamp_color(channel * factor) for channel in color)


def mix(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    return tuple(clamp_color(a[index] * (1.0 - t) + b[index] * t) for index in range(3))


def trim_alpha(image: Image.Image, pad: int = 2) -> Image.Image:
    alpha = np.asarray(image.getchannel("A"))
    ys, xs = np.where(alpha > 0)
    if len(xs) == 0:
        return image
    x0 = max(0, int(xs.min()) - pad)
    y0 = max(0, int(ys.min()) - pad)
    x1 = min(image.width, int(xs.max()) + pad + 1)
    y1 = min(image.height, int(ys.max()) + pad + 1)
    return image.crop((x0, y0, x1, y1))


def read_json(path: Path) -> dict[str, object]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def resolve_asset_file(manifest_path: Path, manifest: dict[str, object], names: tuple[str, ...]) -> Path:
    theme_dir = manifest_path.parent
    for name in names:
        candidate = theme_dir / name
        if candidate.exists():
            return candidate
    for module_group in ("plate_modules", "base_prefabs"):
        modules = manifest.get(module_group, [])
        if not isinstance(modules, list):
            continue
        for module in modules:
            if not isinstance(module, dict):
                continue
            file_value = module.get("file")
            if not isinstance(file_value, str) or not file_value:
                continue
            candidate = PROJECT_ROOT / file_value
            if candidate.exists():
                return candidate
    raise FileNotFoundError(f"No image source found for {manifest_path}")


def solid_texture_from_crop(crop: Image.Image) -> Image.Image:
    rgba = np.array(crop.convert("RGBA"), dtype=np.uint8)
    visible = rgba[:, :, 3] > 30
    if visible.any():
        fill = np.median(rgba[:, :, :3][visible], axis=0)
    else:
        fill = np.array([128, 128, 128], dtype=np.float32)
        visible = np.ones(rgba.shape[:2], dtype=bool)
    rgb = rgba[:, :, :3]
    rgb[~visible] = fill.astype(np.uint8)
    rgba[:, :, 3] = 255
    return Image.fromarray(rgba, "RGBA").resize((96, 96), Image.Resampling.LANCZOS)


def crop_visible_band(image: Image.Image, y0_frac: float, y1_frac: float, x0_frac: float = 0.18, x1_frac: float = 0.82) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8)
    alpha = rgba[:, :, 3]
    ys, xs = np.where(alpha > 30)
    if len(xs) == 0:
        return image.resize((96, 96), Image.Resampling.LANCZOS)
    x0 = int(np.percentile(xs, x0_frac * 100))
    x1 = int(np.percentile(xs, x1_frac * 100))
    y0 = int(np.percentile(ys, y0_frac * 100))
    y1 = int(np.percentile(ys, y1_frac * 100))
    if x1 <= x0 or y1 <= y0:
        return image.resize((96, 96), Image.Resampling.LANCZOS)
    return image.crop((x0, y0, x1, y1))


def sample_material(top_source_path: Path, side_source_path: Path) -> dict[str, object]:
    top_image = Image.open(top_source_path).convert("RGBA")
    side_image = Image.open(side_source_path).convert("RGBA")

    image = top_image
    rgba = np.asarray(image, dtype=np.uint8)
    visible = rgba[:, :, 3] > 40
    if not visible.any():
        raise ValueError(f"Image has no visible pixels: {top_source_path}")

    rgb = rgba[:, :, :3][visible]
    top_color_np = np.percentile(rgb, 64, axis=0)
    shadow_color_np = np.percentile(rgb, 28, axis=0)
    top_color = tuple(int(channel) for channel in top_color_np)
    shadow_color = tuple(int(channel) for channel in shadow_color_np)
    edge_color = mix(top_color, (255, 255, 255), 0.35)

    top_texture = solid_texture_from_crop(crop_visible_band(top_image, 0.10, 0.45, 0.18, 0.82))
    side_texture = solid_texture_from_crop(crop_visible_band(side_image, 0.58, 0.92, 0.12, 0.88))

    return {
        "top_texture": top_texture,
        "side_texture": side_texture,
        "top": top_color,
        "shadow": shadow_color,
        "side": adjust(mix(top_color, shadow_color, 0.55), 0.82),
        "side_dark": adjust(mix(top_color, shadow_color, 0.72), 0.64),
        "edge": edge_color,
    }


def make_noise(size: tuple[int, int], seed: int, strength: int = 18) -> Image.Image:
    rng = np.random.default_rng(seed)
    noise = rng.normal(0, strength, (size[1], size[0], 1))
    noise = np.clip(noise + 128, 0, 255).astype(np.uint8)
    return Image.fromarray(np.repeat(noise, 4, axis=2), "RGBA").filter(ImageFilter.GaussianBlur(1.2))


def tile_texture(texture: Image.Image, size: tuple[int, int], brightness: float, seed: int) -> Image.Image:
    texture = texture.convert("RGBA").resize((96, 96), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    for y in range(0, size[1], texture.height):
        for x in range(0, size[0], texture.width):
            canvas.alpha_composite(texture, (x, y))

    arr = np.asarray(canvas, dtype=np.float32)
    noise = np.asarray(make_noise(size, seed), dtype=np.float32)
    arr[:, :, :3] = np.clip(arr[:, :, :3] * brightness + (noise[:, :, :3] - 128.0) * 0.10, 0, 255)
    arr[:, :, 3] = 255
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def polygon_mask(size: tuple[int, int], points: list[tuple[int, int]]) -> Image.Image:
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(0.25))


def paste_polygon(
    canvas: Image.Image,
    texture: Image.Image,
    points: list[tuple[int, int]],
    outline: tuple[int, int, int] | None = None,
) -> None:
    mask = polygon_mask(canvas.size, points)
    canvas.paste(texture, (0, 0), mask)
    if outline is not None:
        draw = ImageDraw.Draw(canvas)
        draw.line(points + [points[0]], fill=outline + (150,), width=1, joint="curve")


def ramp_points(width: int, height: int, direction: str, relative_height: float) -> dict[str, list[tuple[int, int]]]:
    wall = round(16 + 38 * relative_height)
    front_drop = max(4, round(wall * 0.12))
    if direction == "front":
        surface = [
            (round(width * 0.14), height - 16),
            (round(width * 0.86), height - 16),
            (round(width * 0.64), 18),
            (round(width * 0.36), 18),
        ]
    elif direction == "left":
        surface = [
            (round(width * 0.30), height - 15),
            (round(width * 0.80), height - 27),
            (round(width * 0.51), 17),
            (round(width * 0.16), 29),
        ]
    elif direction == "right":
        surface = [
            (round(width * 0.20), height - 27),
            (round(width * 0.70), height - 15),
            (round(width * 0.84), 29),
            (round(width * 0.49), 17),
        ]
    else:
        raise ValueError(f"Unsupported ramp direction: {direction}")

    front_lip = [
        surface[0],
        surface[1],
        (surface[1][0] + 5, surface[1][1] + front_drop),
        (surface[0][0] - 5, surface[0][1] + front_drop),
    ]
    back_face = [
        surface[3],
        surface[2],
        (surface[2][0] + 7, surface[2][1] + wall),
        (surface[3][0] - 7, surface[3][1] + wall),
    ]
    left_face = [
        surface[0],
        surface[3],
        (surface[3][0] - 7, surface[3][1] + wall),
        (surface[0][0] - 5, surface[0][1] + front_drop),
    ]
    right_face = [
        surface[1],
        surface[2],
        (surface[2][0] + 7, surface[2][1] + wall),
        (surface[1][0] + 5, surface[1][1] + front_drop),
    ]
    return {"surface": surface, "front_lip": front_lip, "back_face": back_face, "left_face": left_face, "right_face": right_face}


def draw_face_facets(
    canvas: Image.Image,
    face: list[tuple[int, int]],
    color: tuple[int, int, int],
    seed: int,
    count: int,
) -> None:
    rng = np.random.default_rng(seed)
    mask = polygon_mask(canvas.size, face)
    facet = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(facet)
    xs = [point[0] for point in face]
    ys = [point[1] for point in face]
    x0, x1 = min(xs), max(xs)
    y0, y1 = min(ys), max(ys)
    for index in range(count):
        x = round(x0 + (x1 - x0) * (index + 0.25 + float(rng.random()) * 0.35) / count)
        tone = 0.76 + float(rng.random()) * 0.44
        draw.polygon(
            [
                (x, y0 + round(float(rng.random()) * 9)),
                (x + round((x1 - x0) / max(3, count)) + 6, y0 + round(float(rng.random()) * 14)),
                (x + round((x1 - x0) / max(4, count)) - 2, y1 - round(float(rng.random()) * 10)),
                (x - 6, y1 - round(float(rng.random()) * 7)),
            ],
            fill=adjust(color, tone) + (64,),
        )
    canvas.alpha_composite(Image.composite(facet, Image.new("RGBA", canvas.size, (0, 0, 0, 0)), mask))


def draw_ramp(
    material: dict[str, object],
    height_id: str,
    relative_height: float,
    width: int,
    height: int,
    direction: str,
    seed: int,
) -> Image.Image:
    top_texture_source = material["top_texture"]
    side_texture_source = material["side_texture"]
    if not isinstance(top_texture_source, Image.Image) or not isinstance(side_texture_source, Image.Image):
        raise TypeError("material textures must be PIL images")
    top_color = material["top"]
    side_color = material["side"]
    side_dark = material["side_dark"]
    edge_color = material["edge"]
    if not isinstance(top_color, tuple) or not isinstance(side_color, tuple):
        raise TypeError("material colors must be tuples")

    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    points = ramp_points(width, height, direction, relative_height)
    top_texture = tile_texture(top_texture_source, (width, height), 1.03, seed)
    side_texture = tile_texture(side_texture_source, (width, height), 0.88, seed + 17)
    dark_texture = tile_texture(side_texture_source, (width, height), 0.72, seed + 31)

    paste_polygon(canvas, dark_texture, points["back_face"], adjust(side_dark, 0.90))
    paste_polygon(canvas, dark_texture, points["left_face"], adjust(side_dark, 0.84))
    paste_polygon(canvas, side_texture, points["right_face"], adjust(side_dark, 0.94))
    paste_polygon(canvas, side_texture, points["front_lip"], adjust(side_color, 0.78))
    paste_polygon(canvas, top_texture, points["surface"], edge_color)

    surface_light = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    light_arr = np.zeros((height, width, 4), dtype=np.uint8)
    for y in range(height):
        t = y / max(1, height - 1)
        light_arr[y, :, :3] = 255
        light_arr[y, :, 3] = round(18 + t * 36)
    surface_light = Image.fromarray(light_arr, "RGBA")
    surface_mask = polygon_mask(canvas.size, points["surface"])
    canvas.alpha_composite(Image.composite(surface_light, Image.new("RGBA", canvas.size, (0, 0, 0, 0)), surface_mask))

    draw = ImageDraw.Draw(canvas)
    rng = np.random.default_rng(seed + 101)
    details = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    detail_draw = ImageDraw.Draw(details)
    xs = [point[0] for point in points["surface"]]
    ys = [point[1] for point in points["surface"]]
    for _ in range(5):
        x = round(float(rng.uniform(min(xs) + 12, max(xs) - 12)))
        y = round(float(rng.uniform(min(ys) + 10, max(ys) - 8)))
        r = round(float(rng.uniform(1.3, 2.8)))
        detail_draw.ellipse((x - r, y - r, x + r, y + r), fill=adjust(top_color, 1.03) + (24,))
    canvas.alpha_composite(Image.composite(details, Image.new("RGBA", canvas.size, (0, 0, 0, 0)), surface_mask))
    draw.line(points["surface"] + [points["surface"][0]], fill=edge_color + (155,), width=1, joint="curve")

    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    shadow_mask = canvas.getchannel("A").filter(ImageFilter.GaussianBlur(5))
    ImageDraw.Draw(shadow).bitmap((0, 0), shadow_mask, fill=(0, 0, 0, 42))
    merged = Image.alpha_composite(shadow, canvas)

    metadata = {
        "height_id": height_id,
        "relative_height": relative_height,
        "direction": direction,
    }
    merged.info.update({key: str(value) for key, value in metadata.items()})
    return trim_alpha(merged, 4)


def make_sheet(items: list[tuple[str, Image.Image]], path: Path) -> dict[str, dict[str, int]]:
    cell_width = 190
    cell_height = 170
    sheet = Image.new("RGBA", (cell_width * 3, cell_height * 3), (0, 0, 0, 0))
    regions: dict[str, dict[str, int]] = {}
    for index, (item_id, image) in enumerate(items):
        row = index // 3
        col = index % 3
        x = col * cell_width + (cell_width - image.width) // 2
        y = row * cell_height + (cell_height - image.height) // 2
        sheet.alpha_composite(image, (x, y))
        regions[item_id] = {"x": x, "y": y, "w": image.width, "h": image.height}
    sheet.save(path)
    return regions


def create_theme_pack(style: dict[str, object], theme: tuple[str, str, str]) -> dict[str, object]:
    theme_id, theme_label, manifest_rel = theme
    style_id = str(style["id"])
    style_label = str(style["label"])
    manifest_path = Path(style["root"]) / manifest_rel
    manifest = read_json(manifest_path)
    top_source_path = resolve_asset_file(manifest_path, manifest, ("surface_fill_tile.png", "plate_top.png", "plate_middle.png"))
    side_source_path = resolve_asset_file(manifest_path, manifest, ("plate_base.png", "plate_middle.png", "plate_top.png"))
    material = sample_material(top_source_path, side_source_path)
    theme_out = OUT_ROOT / style_id / theme_id
    theme_out.mkdir(parents=True, exist_ok=True)

    ramp_modules: list[dict[str, object]] = []
    sheet_items: list[tuple[str, Image.Image]] = []
    for height_index, (height_id, ratio, width, height) in enumerate(HEIGHTS):
        for direction_index, direction in enumerate(DIRECTIONS):
            ramp_id = f"ramp_{height_id}_height_{direction}"
            image = draw_ramp(
                material,
                height_id,
                ratio,
                width,
                height,
                direction,
                seed=stable_seed(style_id, theme_id, height_id, direction),
            )
            file_path = theme_out / f"{ramp_id}.png"
            image.save(file_path)
            sheet_items.append((ramp_id, image))
            ramp_modules.append(
                {
                    "id": ramp_id,
                    "display_name": f"{theme_label} {height_id.title()} Height {direction.title()} Ramp",
                    "file": rel(file_path),
                    "image_size": [image.width, image.height],
                    "direction": direction,
                    "height_class": height_id,
                    "relative_level_height": ratio,
                    "placement": "manual",
                    "narrow_profile": True,
                    "max_game_width_px": image.width,
                }
            )

    sheet_path = theme_out / "mountain_access_ramp_sheet.png"
    regions = make_sheet(sheet_items, sheet_path)
    manifest_out = theme_out / "mountain_access_ramps_manifest.json"
    out_manifest = {
        "schema_version": 1,
        "pack_id": f"{style_id}_{theme_id}_mountain_access_ramps",
        "style_id": style_id,
        "style_label": style_label,
        "theme_id": theme_id,
        "theme_label": theme_label,
        "source_prefab_manifest": rel(manifest_path),
        "source_top_material_image": rel(top_source_path),
        "source_side_material_image": rel(side_source_path),
        "projection": "front_2_5d",
        "ramp_modules": ramp_modules,
        "atlas_sheets": [
            {
                "id": "mountain_access_ramp_sheet",
                "file": rel(sheet_path),
                "cell_size": [190, 170],
                "regions": regions,
            }
        ],
        "usage": "Manual ramp placement. These ramps are intentionally narrow and are not embedded in the mountain prefab bases.",
    }
    manifest_out.write_text(json.dumps(out_manifest, indent=2), encoding="utf-8")
    return {
        "theme_id": theme_id,
        "theme_label": theme_label,
        "manifest": rel(manifest_out),
        "sheet": rel(sheet_path),
        "ramp_count": len(ramp_modules),
        "source_prefab_manifest": rel(manifest_path),
    }


def compose_preview(packs: list[dict[str, object]]) -> Image.Image:
    font_title = load_font(34)
    font_label = load_font(18)
    cell_width = 320
    cell_height = 300
    columns = 4
    rows = math.ceil(len(packs) / columns)
    width = columns * cell_width + 64
    height = rows * cell_height + 108
    canvas = Image.new("RGBA", (width, height), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.text((30, 26), "NARROW MOUNTAIN ACCESS RAMPS - PREFAB 1 + PREFAB 2", fill=(235, 243, 246), font=font_title)

    for index, pack in enumerate(packs):
        row = index // columns
        col = index % columns
        x = 28 + col * cell_width
        y = 86 + row * cell_height
        draw.rectangle((x, y, x + cell_width - 12, y + cell_height - 14), fill=PANEL)
        label = f"{pack['style_label']} - {pack['theme_label']}"
        draw.text((x + 14, y + 12), label[:36], fill=(221, 232, 236), font=font_label)
        manifest_path = PROJECT_ROOT / str(pack["manifest"])
        manifest = read_json(manifest_path)
        modules = manifest["ramp_modules"]
        if not isinstance(modules, list):
            continue
        sample_ids = [
            "ramp_quarter_height_front",
            "ramp_half_height_left",
            "ramp_full_height_right",
        ]
        offset_x = [22, 112, 206]
        offset_y = [85, 72, 50]
        for sample_index, sample_id in enumerate(sample_ids):
            module = next(item for item in modules if isinstance(item, dict) and item["id"] == sample_id)
            ramp = Image.open(PROJECT_ROOT / str(module["file"])).convert("RGBA")
            target_w = 70 + sample_index * 15
            ramp = ramp.resize((target_w, round(ramp.height * target_w / ramp.width)), Image.Resampling.LANCZOS)
            canvas.alpha_composite(ramp, (x + offset_x[sample_index], y + offset_y[sample_index]))
        draw.text((x + 14, y + cell_height - 48), "quarter / half / full height", fill=(175, 190, 196), font=font_label)
        draw.text((x + 14, y + cell_height - 27), "front, left, right directions", fill=(175, 190, 196), font=font_label)

    return canvas


def main() -> None:
    OUT_ROOT.mkdir(parents=True, exist_ok=True)
    catalog_styles: list[dict[str, object]] = []
    preview_packs: list[dict[str, object]] = []

    for style in PREFAB_STYLES:
        style_entries: list[dict[str, object]] = []
        for theme in style["themes"]:
            entry = create_theme_pack(style, theme)
            entry["style_id"] = style["id"]
            entry["style_label"] = style["label"]
            style_entries.append(entry)
            preview_packs.append(entry)
        catalog_styles.append(
            {
                "style_id": style["id"],
                "style_label": style["label"],
                "themes": style_entries,
            }
        )

    catalog = {
        "schema_version": 1,
        "catalog_id": "mountain_access_ramps",
        "projection": "front_2_5d",
        "height_classes": [
            {"id": height_id, "relative_level_height": ratio}
            for height_id, ratio, _, _ in HEIGHTS
        ],
        "directions": DIRECTIONS,
        "styles": catalog_styles,
    }
    catalog_path = OUT_ROOT / "mountain_access_ramps_catalog.json"
    catalog_path.write_text(json.dumps(catalog, indent=2), encoding="utf-8")

    preview_path = OUT_ROOT / "mountain_access_ramps_preview.png"
    compose_preview(preview_packs).save(preview_path)
    print(f"Wrote {catalog_path}")
    print(f"Wrote {preview_path}")


if __name__ == "__main__":
    main()
