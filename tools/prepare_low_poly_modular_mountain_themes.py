from __future__ import annotations

import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageOps

from prepare_low_poly_modular_mountain_prefabs import RAW_DIR, create_one_level_sheet
from prepare_low_poly_transition_prefab import PROJECT_ROOT, create_preview, largest_component


OUTPUT_ROOT = (
    PROJECT_ROOT
    / "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/modular_themes"
)
CATALOG_PATH = OUTPUT_ROOT / "modular_mountain_theme_catalog.json"
STYLE_ID = "mountain_prefab_1"
STYLE_LABEL = "Mountain Prefab 1"
SMALL_RAMP_WIDTH_RATIO = 0.19
RAMP_RUN_SCALE = 0.72
SOCKET_PROFILES = {
    "grass_granite": {
        "entry_left": (0.230, 0.750), "entry_front": (0.500, 0.840), "entry_right": (0.770, 0.750),
        "level_0_to_1_left": (0.350, 0.440), "level_0_to_1_right": (0.650, 0.440),
        "level_1_to_2_left": (0.380, 0.200), "level_1_to_2_right": (0.620, 0.200),
    },
    "stone": {
        "entry_left": (0.240, 0.750), "entry_front": (0.500, 0.840), "entry_right": (0.760, 0.750),
        "level_0_to_1_left": (0.340, 0.420), "level_0_to_1_right": (0.660, 0.420),
        "level_1_to_2_left": (0.380, 0.180), "level_1_to_2_right": (0.620, 0.180),
    },
}

THEMES = [
    {
        "id": "grass_granite",
        "label": "Grass + Granite",
        "background": "gradient",
        "socket_profile": "grass_granite",
        "three": RAW_DIR / "grass_granite_three_level_pack_raw.png",
        "one": RAW_DIR / "grass_granite_one_level_pack_raw.png",
        "three_boxes": {
            "three_level_wide_no_ramps": (0, 0, 1055, 1024),
            "ramp_left": (1050, 0, 1536, 365),
            "ramp_front": (1110, 310, 1460, 710),
            "ramp_right": (1040, 610, 1536, 1024),
        },
    },
    {
        "id": "grey_rock",
        "label": "Grey Rock",
        "background": "checker",
        "socket_profile": "stone",
        "three": RAW_DIR / "grey_rock_three_level_pack_raw.png",
        "one": RAW_DIR / "grey_rock_one_level_pack_raw.png",
        "three_boxes": {
            "three_level_wide_no_ramps": (0, 0, 910, 971),
            "ramp_left": (900, 0, 1260, 340),
            "ramp_front": (1000, 315, 1170, 640),
            "ramp_right": (895, 620, 1265, 971),
        },
    },
    {
        "id": "volcanic_basalt",
        "label": "Volcanic Basalt",
        "background": "checker",
        "socket_profile": "stone",
        "three": RAW_DIR / "volcanic_basalt_three_level_pack_raw.png",
        "one": RAW_DIR / "volcanic_basalt_one_level_pack_raw.png",
        "three_boxes": {
            "three_level_wide_no_ramps": (0, 0, 910, 971),
            "ramp_left": (900, 0, 1260, 340),
            "ramp_front": (1000, 315, 1170, 640),
            "ramp_right": (895, 620, 1265, 971),
        },
    },
]
ONE_LEVEL_BOX = (0, 0, 1180, 1024)


def fit_width(image: Image.Image, width: int) -> Image.Image:
    height = round(image.height * width / image.width)
    return image.resize((width, height), Image.Resampling.LANCZOS)


def fit_height(image: Image.Image, height: int) -> Image.Image:
    width = round(image.width * height / image.height)
    return image.resize((width, height), Image.Resampling.LANCZOS)


def shorten_ramp_run(image: Image.Image, direction: str) -> Image.Image:
    if direction == "front":
        size = (image.width, max(1, round(image.height * RAMP_RUN_SCALE)))
    else:
        size = (max(1, round(image.width * RAMP_RUN_SCALE)), image.height)
    return image.resize(size, Image.Resampling.LANCZOS)


def relative(path: Path) -> str:
    return str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")


def extract_asset(source: Image.Image, box: tuple[int, int, int, int], background: str) -> Image.Image:
    box = (
        max(0, box[0]),
        max(0, box[1]),
        min(source.width, box[2]),
        min(source.height, box[3]),
    )
    rgba_source = source.convert("RGBA")
    crop_rgba = rgba_source.crop(box)
    source_alpha = np.asarray(crop_rgba.getchannel("A"), dtype=np.uint8)
    if int(source_alpha.min()) < 250:
        foreground = largest_component((source_alpha > 16).astype(np.uint8))
        ys, xs = np.where(foreground)
        if len(xs) == 0:
            raise ValueError(f"No alpha foreground found in crop {box}")
        rgba = np.asarray(crop_rgba, dtype=np.uint8).copy()
        rgba[:, :, 3] = np.where(foreground, source_alpha, 0).astype(np.uint8)
        rgba[rgba[:, :, 3] == 0, :3] = 0
        padding = 8
        left = max(0, int(xs.min()) - padding)
        top = max(0, int(ys.min()) - padding)
        right = min(crop_rgba.width, int(xs.max()) + padding + 1)
        bottom = min(crop_rgba.height, int(ys.max()) + padding + 1)
        return Image.fromarray(rgba, mode="RGBA").crop((left, top, right, bottom))

    crop = crop_rgba.convert("RGB")
    rgb = np.asarray(crop, dtype=np.uint8)

    if background == "checker":
        maximum = rgb.max(axis=2)
        minimum = rgb.min(axis=2)
        neutral_light = (minimum > 214) & ((maximum - minimum) < 24)
        mask = (~neutral_light).astype(np.uint8)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8), iterations=2)
    else:
        bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
        grab_mask = np.zeros(rgb.shape[:2], dtype=np.uint8)
        rect = (6, 6, max(1, crop.width - 12), max(1, crop.height - 12))
        background_model = np.zeros((1, 65), np.float64)
        foreground_model = np.zeros((1, 65), np.float64)
        cv2.grabCut(bgr, grab_mask, rect, background_model, foreground_model, 6, cv2.GC_INIT_WITH_RECT)
        mask = np.where(
            (grab_mask == cv2.GC_FGD) | (grab_mask == cv2.GC_PR_FGD),
            1,
            0,
        ).astype(np.uint8)

    foreground = largest_component(mask)
    ys, xs = np.where(foreground)
    if len(xs) == 0:
        raise ValueError(f"No foreground found in crop {box}")

    alpha = np.where(foreground, 255, 0).astype(np.uint8)
    rgba = np.dstack((rgb, alpha))
    rgba[alpha == 0, :3] = 0
    padding = 8
    left = max(0, int(xs.min()) - padding)
    top = max(0, int(ys.min()) - padding)
    right = min(crop.width, int(xs.max()) + padding + 1)
    bottom = min(crop.height, int(ys.max()) + padding + 1)
    return Image.fromarray(rgba, mode="RGBA").crop((left, top, right, bottom))


def point(image: Image.Image, x: float, y: float) -> dict[str, int]:
    return {"x": round(image.width * x), "y": round(image.height * y)}


def socket(socket_id: str, from_level: int, to_level: int, direction: str, image: Image.Image, x: float, y: float) -> dict[str, object]:
    return {
        "id": socket_id,
        "from_level": from_level,
        "to_level": to_level,
        "direction": direction,
        "upper_landing": point(image, x, y),
        "compatible_ramp": f"ramp_{direction}",
    }


def sockets_for_three_level(image: Image.Image, profile: dict[str, tuple[float, float]]) -> list[dict[str, object]]:
    return [
        socket("entry_left", -1, 0, "left", image, *profile["entry_left"]),
        socket("entry_front", -1, 0, "front", image, *profile["entry_front"]),
        socket("entry_right", -1, 0, "right", image, *profile["entry_right"]),
        socket("level_0_to_1_left", 0, 1, "left", image, *profile["level_0_to_1_left"]),
        socket("level_0_to_1_right", 0, 1, "right", image, *profile["level_0_to_1_right"]),
        socket("level_1_to_2_left", 1, 2, "left", image, *profile["level_1_to_2_left"]),
        socket("level_1_to_2_right", 1, 2, "right", image, *profile["level_1_to_2_right"]),
    ]


def sockets_for_one_level(image: Image.Image, profile: dict[str, tuple[float, float]]) -> list[dict[str, object]]:
    return [
        socket("entry_left", -1, 0, "left", image, *profile["entry_left"]),
        socket("entry_front", -1, 0, "front", image, *profile["entry_front"]),
        socket("entry_right", -1, 0, "right", image, *profile["entry_right"]),
    ]


def compose_assembled_preview(
    base: Image.Image,
    ramps: dict[str, Image.Image],
    sockets: list[dict[str, object]],
    output: Path,
) -> None:
    socket_index = {item["id"]: item for item in sockets}
    selected = [
        ("entry_front", "ramp_front"),
        ("level_0_to_1_left", "ramp_left"),
        ("level_1_to_2_right", "ramp_right"),
    ]
    layers: list[tuple[Image.Image, tuple[int, int]]] = [(base, (0, 0))]
    for socket_id, ramp_id in selected:
        ramp = ramps[ramp_id]
        landing = socket_index[socket_id]["upper_landing"]
        direction = ramp_id.rsplit("_", 1)[1]
        anchor_x = 0.82 if direction == "left" else 0.18 if direction == "right" else 0.5
        anchor_y = 0.13 if direction != "front" else 0.08
        layers.append(
            (
                ramp,
                (
                    round(landing["x"] - ramp.width * anchor_x),
                    round(landing["y"] - ramp.height * anchor_y),
                ),
            )
        )

    padding = 24
    min_x = min(position[0] for _, position in layers)
    min_y = min(position[1] for _, position in layers)
    max_x = max(position[0] + image.width for image, position in layers)
    max_y = max(position[1] + image.height for image, position in layers)
    canvas = Image.new(
        "RGBA",
        (max_x - min_x + padding * 2, max_y - min_y + padding * 2),
        (0, 0, 0, 0),
    )
    for image, position in layers:
        canvas.alpha_composite(image, (position[0] - min_x + padding, position[1] - min_y + padding))
    create_preview(canvas, output)


def build_theme(theme: dict[str, object]) -> dict[str, object]:
    theme_id = str(theme["id"])
    output = OUTPUT_ROOT / theme_id
    output.mkdir(parents=True, exist_ok=True)
    background = str(theme["background"])
    socket_profile = SOCKET_PROFILES[str(theme["socket_profile"])]
    three_source = Image.open(Path(theme["three"]))
    one_source = Image.open(Path(theme["one"]))

    three_boxes = theme["three_boxes"]
    assets = {
        asset_id: extract_asset(three_source, box, background)
        for asset_id, box in three_boxes.items()
    }
    assets["one_level_wide_no_ramps"] = extract_asset(one_source, ONE_LEVEL_BOX, background)
    three = assets["three_level_wide_no_ramps"]
    one = assets["one_level_wide_no_ramps"]
    small_ramp_length = round(three.width * SMALL_RAMP_WIDTH_RATIO)
    left = shorten_ramp_run(fit_width(assets["ramp_left"], small_ramp_length), "left")
    front = shorten_ramp_run(fit_height(assets["ramp_front"], small_ramp_length), "front")
    right = ImageOps.mirror(left)
    assets.update({"ramp_left": left, "ramp_front": front, "ramp_right": right})
    for asset_id, image in assets.items():
        image.save(output / f"{asset_id}.png", optimize=True)

    create_preview(three, output / "three_level_wide_no_ramps_preview.png")
    create_preview(one, output / "one_level_wide_no_ramps_preview.png")
    three_sockets = sockets_for_three_level(three, socket_profile)
    assembled_preview_path = output / "assembled_with_level_ramps_preview.png"
    compose_assembled_preview(
        three,
        {"ramp_left": left, "ramp_front": front, "ramp_right": right},
        three_sockets,
        assembled_preview_path,
    )
    sheet_path = output / "one_level_mountain_sheet.png"
    sheet_regions = create_one_level_sheet(
        one,
        [("ramp_left", left, 1.0), ("ramp_front", front, 1.0), ("ramp_right", right, 1.0)],
        sheet_path,
    )

    ramp_modules = []
    for ramp_id, direction, anchor_x in [
        ("ramp_left", "left", 0.82),
        ("ramp_front", "front", 0.50),
        ("ramp_right", "right", 0.18),
    ]:
        ramp_modules.append(
            {
                "id": ramp_id,
                "direction": direction,
                "size_class": "small",
                "file": relative(output / f"{ramp_id}.png"),
                "image_size": [assets[ramp_id].width, assets[ramp_id].height],
                "upper_anchor_normalized": {"x": anchor_x, "y": 0.13 if direction != "front" else 0.08},
                "display_scale": 1.0,
                "run_scale": RAMP_RUN_SCALE,
                "level_rise": 1.0,
                "walkable": True,
                "climbable": True,
            }
        )

    manifest = {
        "schema_version": 1,
        "pack_id": f"modular_front_2_5d_{theme_id}",
        "style_id": STYLE_ID,
        "style_label": STYLE_LABEL,
        "theme_id": theme_id,
        "theme_label": theme["label"],
        "projection": "front_2_5d",
        "base_prefabs": [
            {
                "id": "three_level_wide_no_ramps",
                "display_name": f"{STYLE_LABEL} - {theme['label']}",
                "style_id": STYLE_ID,
                "file": relative(output / "three_level_wide_no_ramps.png"),
                "preview": relative(output / "three_level_wide_no_ramps_preview.png"),
                "level_count": 3,
                "image_size": [three.width, three.height],
                "strict_nested_footprints": True,
                "sockets": three_sockets,
            },
            {
                "id": "one_level_wide_no_ramps",
                "display_name": f"{STYLE_LABEL} - {theme['label']} (One Level)",
                "style_id": STYLE_ID,
                "file": relative(output / "one_level_wide_no_ramps.png"),
                "preview": relative(output / "one_level_wide_no_ramps_preview.png"),
                "level_count": 1,
                "image_size": [one.width, one.height],
                "strict_nested_footprints": True,
                "sockets": sockets_for_one_level(one, socket_profile),
            },
        ],
        "ramp_modules": ramp_modules,
        "atlas_sheets": [
            {
                "id": f"one_level_mountain_sheet_{theme_id}",
                "file": relative(sheet_path),
                "transparent": True,
                "regions": sheet_regions,
            }
        ],
    }
    manifest_path = output / "modular_mountain_pack_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return {
        "id": theme_id,
        "label": theme["label"],
        "display_name": f"{STYLE_LABEL} - {theme['label']}",
        "style_id": STYLE_ID,
        "style_label": STYLE_LABEL,
        "manifest": relative(manifest_path),
        "three_level_preview": relative(output / "three_level_wide_no_ramps_preview.png"),
        "assembled_ramp_preview": relative(assembled_preview_path),
        "one_level_sheet": relative(sheet_path),
    }


def main() -> int:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    entries = [build_theme(theme) for theme in THEMES]
    generated_ids = {entry["id"] for entry in entries} | {"sandstone"}
    preserved = []
    if CATALOG_PATH.exists():
        current = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
        preserved = [
            entry
            for entry in current.get("themes", [])
            if entry.get("id") not in generated_ids
        ]
    sandstone_root = PROJECT_ROOT / (
        "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/"
        "authored_prefabs/modular_front_2_5d"
    )
    sandstone = {
        "id": "sandstone",
        "label": "Low Poly Sandstone",
        "display_name": f"{STYLE_LABEL} - Low Poly Sandstone",
        "style_id": STYLE_ID,
        "style_label": STYLE_LABEL,
        "manifest": relative(sandstone_root / "modular_mountain_pack_manifest.json"),
        "three_level_preview": relative(sandstone_root / "three_level_wide_no_ramps_preview.png"),
        "assembled_ramp_preview": relative(sandstone_root / "assembled_with_level_ramps_preview.png"),
        "one_level_sheet": relative(sandstone_root / "one_level_mountain_sheet.png"),
    }
    catalog = {
        "schema_version": 1,
        "catalog_id": "modular_front_2_5d_material_themes",
        "style_id": STYLE_ID,
        "style_label": STYLE_LABEL,
        "themes": [sandstone] + entries + preserved,
    }
    CATALOG_PATH.write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(entries)} modular mountain material themes")
    print(f"Catalog: {CATALOG_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
