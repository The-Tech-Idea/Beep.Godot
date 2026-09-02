from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageOps

from prepare_low_poly_modular_mountain_prefabs import RAW_DIR, create_one_level_sheet
from prepare_low_poly_modular_mountain_themes import CATALOG_PATH, OUTPUT_ROOT, extract_asset, relative
from prepare_low_poly_transition_prefab import create_preview


FAMILIES = [
    {
        "id": "meadow_hill",
        "label": "Gentle Meadow Hill",
        "background": "gradient",
    },
    {
        "id": "red_rock_mesa",
        "label": "Red Rock Mesa",
        "background": "gradient",
    },
    {
        "id": "alpine_snow",
        "label": "Alpine Snow",
        "background": "gradient",
    },
]

PLATE_BOXES = {
    "plate_base": (0, 20, 1024, 625),
    "plate_middle": (40, 590, 984, 1080),
    "plate_top": (130, 1030, 900, 1500),
}

RAMP_BOXES = {
    "entry_left": (0, 0, 625, 470),
    "entry_front": (575, 0, 965, 485),
    "entry_right": (895, 0, 1536, 470),
    "middle_left": (115, 350, 785, 815),
    "middle_right": (745, 350, 1425, 815),
    "upper_left": (245, 675, 825, 1024),
    "upper_right": (720, 675, 1300, 1024),
}

SMALL_RAMP_WIDTH_RATIO = 0.31


def fit_width(image: Image.Image, width: int) -> Image.Image:
    height = round(image.height * width / image.width)
    return image.resize((width, height), Image.Resampling.LANCZOS)


def fit_height(image: Image.Image, height: int) -> Image.Image:
    width = round(image.width * height / image.height)
    return image.resize((width, height), Image.Resampling.LANCZOS)


def pack_sheet(assets: list[tuple[str, Image.Image]], path: Path, columns: int) -> dict[str, dict[str, int]]:
    padding = 24
    row_count = (len(assets) + columns - 1) // columns
    column_widths = [0] * columns
    row_heights = [0] * row_count
    for index, (_, image) in enumerate(assets):
        column = index % columns
        row = index // columns
        column_widths[column] = max(column_widths[column], image.width)
        row_heights[row] = max(row_heights[row], image.height)

    width = sum(column_widths) + padding * (columns + 1)
    height = sum(row_heights) + padding * (row_count + 1)
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    regions: dict[str, dict[str, int]] = {}
    y = padding
    for row in range(row_count):
        x = padding
        for column in range(columns):
            index = row * columns + column
            if index >= len(assets):
                break
            asset_id, image = assets[index]
            sheet.alpha_composite(image, (x, y))
            regions[asset_id] = {
                "x": x,
                "y": y,
                "width": image.width,
                "height": image.height,
            }
            x += column_widths[column] + padding
        y += row_heights[row] + padding
    sheet.save(path, optimize=True)
    return regions


def compose_stack(
    base: Image.Image,
    middle: Image.Image,
    top: Image.Image,
) -> tuple[Image.Image, list[dict[str, object]]]:
    base_center = (max(base.width, middle.width, top.width) // 2 + 24, 0)
    middle_rise = round(base.height * 0.28)
    top_rise = round(middle.height * 0.32)
    centers = [
        (base_center[0], middle_rise + top_rise + base.height // 2 + 24),
        (base_center[0], top_rise + middle.height // 2 + 24),
        (base_center[0], top.height // 2 + 24),
    ]
    width = max(base.width, middle.width, top.width) + 48
    height = max(
        centers[0][1] + base.height // 2,
        centers[1][1] + middle.height // 2,
        centers[2][1] + top.height // 2,
    ) + 24
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    assembly = []
    for level, (asset_id, image, center) in enumerate(
        [
            ("plate_base", base, centers[0]),
            ("plate_middle", middle, centers[1]),
            ("plate_top", top, centers[2]),
        ]
    ):
        x = round(center[0] - image.width / 2)
        y = round(center[1] - image.height / 2)
        canvas.alpha_composite(image, (x, y))
        assembly.append(
            {
                "plate": asset_id,
                "level": level,
                "center": {"x": center[0], "y": center[1]},
                "z_index": level * 10,
            }
        )
    return canvas, assembly


def compose_ramp_preview(
    plates: dict[str, Image.Image],
    ramps: dict[str, Image.Image],
    assembly: list[dict[str, object]],
    sockets: list[dict[str, object]],
    canvas_size: tuple[int, int],
    path: Path,
) -> None:
    socket_index = {item["id"]: item for item in sockets}
    ramp_modules = [
        ("entry_front", "ramp_front", 10),
        ("level_0_to_1_left", "ramp_left", 20),
        ("level_1_to_2_right", "ramp_right", 30),
    ]
    layers: list[tuple[int, int, Image.Image, tuple[int, int]]] = []
    for order, part in enumerate(assembly):
        image = plates[str(part["plate"])]
        center = part["center"]
        position = (
            round(center["x"] - image.width / 2),
            round(center["y"] - image.height / 2),
        )
        layers.append((int(part["z_index"]), order, image, position))

    for order, (socket_id, ramp_id, z_index) in enumerate(ramp_modules, start=len(assembly)):
        image = ramps[ramp_id]
        landing = socket_index[socket_id]["upper_landing"]
        direction = ramp_id.rsplit("_", 1)[1]
        anchor_x = 0.82 if direction == "left" else 0.18 if direction == "right" else 0.5
        anchor_y = 0.12 if direction != "front" else 0.08
        center_x = landing["x"] - (anchor_x - 0.5) * image.width
        center_y = landing["y"] - (anchor_y - 0.5) * image.height
        position = (round(center_x - image.width / 2), round(center_y - image.height / 2))
        layers.append((z_index, order, image, position))

    padding = 24
    min_x = min(position[0] for _, _, _, position in layers)
    min_y = min(position[1] for _, _, _, position in layers)
    max_x = max(position[0] + image.width for _, _, image, position in layers)
    max_y = max(position[1] + image.height for _, _, image, position in layers)
    offset_x = padding - min(0, min_x)
    offset_y = padding - min(0, min_y)
    width = max(canvas_size[0], max_x) - min(0, min_x) + padding * 2
    height = max(canvas_size[1], max_y) - min(0, min_y) + padding * 2
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for _, _, image, position in sorted(layers, key=lambda item: (item[0], item[1])):
        canvas.alpha_composite(image, (position[0] + offset_x, position[1] + offset_y))
    create_preview(canvas, path)


def socket(
    socket_id: str,
    from_level: int,
    to_level: int,
    direction: str,
    center: dict[str, int],
    plate: Image.Image,
    ramp_id: str,
) -> dict[str, object]:
    x_factor = {"left": -0.27, "front": 0.0, "right": 0.27}[direction]
    y_factor = 0.27 if direction == "front" else 0.13
    return {
        "id": socket_id,
        "from_level": from_level,
        "to_level": to_level,
        "direction": direction,
        "upper_landing": {
            "x": round(center["x"] + plate.width * x_factor),
            "y": round(center["y"] + plate.height * y_factor),
        },
        "compatible_ramp": ramp_id,
    }


def ramp_module(ramp_id: str, direction: str, path: Path, image: Image.Image) -> dict[str, object]:
    return {
        "id": ramp_id,
        "direction": direction,
        "size_class": "small",
        "file": relative(path),
        "image_size": [image.width, image.height],
        "upper_anchor_normalized": {
            "x": 0.82 if direction == "left" else 0.18 if direction == "right" else 0.5,
            "y": 0.12 if direction != "front" else 0.08,
        },
        "display_scale": 1.0,
        "walkable": True,
        "climbable": True,
    }


def build_family(family: dict[str, str]) -> dict[str, str]:
    family_id = family["id"]
    output = OUTPUT_ROOT / family_id
    output.mkdir(parents=True, exist_ok=True)
    plate_source = Image.open(RAW_DIR / f"{family_id}_plate_family_raw.png")
    ramp_source = Image.open(RAW_DIR / f"{family_id}_ramp_family_raw.png")
    background = family["background"]

    plates = {
        asset_id: extract_asset(plate_source, box, background)
        for asset_id, box in PLATE_BOXES.items()
    }
    authored_ramps = {
        asset_id: extract_asset(ramp_source, box, background)
        for asset_id, box in RAMP_BOXES.items()
    }
    small_ramp_length = round(plates["plate_top"].width * SMALL_RAMP_WIDTH_RATIO)
    ramp_left = fit_width(authored_ramps["upper_left"], small_ramp_length)
    ramps = {
        "ramp_left": ramp_left,
        "ramp_front": fit_height(authored_ramps["entry_front"], small_ramp_length),
        "ramp_right": ImageOps.mirror(ramp_left),
    }
    for obsolete_id in RAMP_BOXES:
        (output / f"{obsolete_id}.png").unlink(missing_ok=True)
    for asset_id, image in {**plates, **ramps}.items():
        image.save(output / f"{asset_id}.png", optimize=True)

    stack, assembly = compose_stack(
        plates["plate_base"],
        plates["plate_middle"],
        plates["plate_top"],
    )
    stack_path = output / "three_level_wide_no_ramps.png"
    stack.save(stack_path, optimize=True)
    stack_preview_path = output / "three_level_wide_no_ramps_preview.png"
    create_preview(stack, stack_preview_path)

    plate_sheet_path = output / "mountain_plate_sheet.png"
    plate_regions = pack_sheet(list(plates.items()), plate_sheet_path, columns=2)
    ramp_sheet_path = output / "level_ramp_sheet.png"
    ramp_regions = pack_sheet(list(ramps.items()), ramp_sheet_path, columns=3)
    one_level_sheet_path = output / "one_level_mountain_sheet.png"
    one_level_regions = create_one_level_sheet(
        plates["plate_base"],
        [
            ("ramp_left", ramps["ramp_left"], 1.0),
            ("ramp_front", ramps["ramp_front"], 1.0),
            ("ramp_right", ramps["ramp_right"], 1.0),
        ],
        one_level_sheet_path,
    )

    centers = {item["plate"]: item["center"] for item in assembly}
    base_sockets = [
        socket("entry_left", -1, 0, "left", centers["plate_base"], plates["plate_base"], "ramp_left"),
        socket("entry_front", -1, 0, "front", centers["plate_base"], plates["plate_base"], "ramp_front"),
        socket("entry_right", -1, 0, "right", centers["plate_base"], plates["plate_base"], "ramp_right"),
        socket("level_0_to_1_left", 0, 1, "left", centers["plate_middle"], plates["plate_middle"], "ramp_left"),
        socket("level_0_to_1_right", 0, 1, "right", centers["plate_middle"], plates["plate_middle"], "ramp_right"),
        socket("level_1_to_2_left", 1, 2, "left", centers["plate_top"], plates["plate_top"], "ramp_left"),
        socket("level_1_to_2_right", 1, 2, "right", centers["plate_top"], plates["plate_top"], "ramp_right"),
    ]
    assembled_ramp_preview_path = output / "assembled_with_level_ramps_preview.png"
    compose_ramp_preview(
        plates,
        ramps,
        assembly,
        base_sockets,
        stack.size,
        assembled_ramp_preview_path,
    )
    one_center = {"x": plates["plate_base"].width // 2, "y": plates["plate_base"].height // 2}
    one_sockets = [
        socket("entry_left", -1, 0, "left", one_center, plates["plate_base"], "ramp_left"),
        socket("entry_front", -1, 0, "front", one_center, plates["plate_base"], "ramp_front"),
        socket("entry_right", -1, 0, "right", one_center, plates["plate_base"], "ramp_right"),
    ]

    plate_modules = [
        {
            "id": plate_id,
            "file": relative(output / f"{plate_id}.png"),
            "image_size": [image.width, image.height],
            "walkable": True,
        }
        for plate_id, image in plates.items()
    ]
    ramp_modules = [
        ramp_module(
            ramp_id,
            ramp_id.rsplit("_", 1)[1],
            output / f"{ramp_id}.png",
            ramps[ramp_id],
        )
        for ramp_id in ramps
    ]
    manifest = {
        "schema_version": 2,
        "pack_id": f"modular_front_2_5d_{family_id}",
        "theme_id": family_id,
        "theme_label": family["label"],
        "projection": "front_2_5d",
        "plate_modules": plate_modules,
        "base_prefabs": [
            {
                "id": "three_level_wide_no_ramps",
                "file": relative(stack_path),
                "preview": relative(stack_preview_path),
                "level_count": 3,
                "image_size": [stack.width, stack.height],
                "strict_nested_footprints": True,
                "plate_assembly": assembly,
                "sockets": base_sockets,
            },
            {
                "id": "one_level_wide_no_ramps",
                "file": relative(output / "plate_base.png"),
                "preview": relative(output / "plate_base.png"),
                "level_count": 1,
                "image_size": [plates["plate_base"].width, plates["plate_base"].height],
                "strict_nested_footprints": True,
                "plate_assembly": [
                    {
                        "plate": "plate_base",
                        "level": 0,
                        "center": one_center,
                        "z_index": 0,
                    }
                ],
                "sockets": one_sockets,
            },
        ],
        "ramp_modules": ramp_modules,
        "atlas_sheets": [
            {
                "id": f"mountain_plates_{family_id}",
                "file": relative(plate_sheet_path),
                "transparent": True,
                "regions": plate_regions,
            },
            {
                "id": f"level_ramps_{family_id}",
                "file": relative(ramp_sheet_path),
                "transparent": True,
                "regions": ramp_regions,
            },
            {
                "id": f"one_level_mountain_{family_id}",
                "file": relative(one_level_sheet_path),
                "transparent": True,
                "regions": one_level_regions,
            },
        ],
    }
    manifest_path = output / "modular_mountain_pack_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return {
        "id": family_id,
        "label": family["label"],
        "manifest": relative(manifest_path),
        "three_level_preview": relative(stack_preview_path),
        "assembled_ramp_preview": relative(assembled_ramp_preview_path),
        "plate_sheet": relative(plate_sheet_path),
        "ramp_sheet": relative(ramp_sheet_path),
        "one_level_sheet": relative(one_level_sheet_path),
    }


def main() -> int:
    entries = [build_family(family) for family in FAMILIES]
    existing = {"schema_version": 1, "catalog_id": "modular_front_2_5d_material_themes", "themes": []}
    if CATALOG_PATH.exists():
        existing = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    generated_ids = {entry["id"] for entry in entries}
    preserved = [entry for entry in existing.get("themes", []) if entry.get("id") not in generated_ids]
    existing["themes"] = preserved + entries
    CATALOG_PATH.write_text(json.dumps(existing, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(entries)} modular mountain shape families")
    print(f"Catalog: {CATALOG_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
