from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageOps

from prepare_low_poly_front_2_5d_prefabs import PREFAB_ROOT, clean_source
from prepare_low_poly_transition_prefab import PROJECT_ROOT, create_preview, shift_points


RAW_DIR = PREFAB_ROOT / "raw"
OUTPUT_DIR = PREFAB_ROOT / "modular_front_2_5d"


def relative(path: Path) -> str:
    return str(path.relative_to(PROJECT_ROOT)).replace("\\", "/")


def save_clean(source_path: Path, output_path: Path) -> tuple[Image.Image, tuple[int, int]]:
    sprite, origin = clean_source(Image.open(source_path))
    sprite.save(output_path, optimize=True)
    return sprite, origin


def socket(
    socket_id: str,
    from_level: int,
    to_level: int,
    direction: str,
    point: tuple[int, int],
    origin: tuple[int, int],
) -> dict[str, object]:
    shifted = shift_points([point], origin)[0]
    return {
        "id": socket_id,
        "from_level": from_level,
        "to_level": to_level,
        "direction": direction,
        "upper_landing": shifted,
        "compatible_ramp": f"ramp_{direction}",
    }


def create_module_preview(base: Image.Image, ramps: list[tuple[str, Image.Image]], output: Path) -> None:
    width = 1500
    height = 900
    canvas = Image.new("RGBA", (width, height), (19, 32, 39, 255))

    base_preview = base.copy()
    base_preview.thumbnail((760, 760), Image.Resampling.LANCZOS)
    canvas.alpha_composite(base_preview, (40, (height - base_preview.height) // 2))

    y = 65
    for _, ramp in ramps:
        module = ramp.copy()
        module.thumbnail((420, 235), Image.Resampling.LANCZOS)
        canvas.alpha_composite(module, (1000 - module.width // 2, y))
        y += 270

    canvas.convert("RGB").save(output, quality=95)


def create_one_level_sheet(
    base: Image.Image,
    ramps: list[tuple[str, Image.Image, float]],
    output: Path,
) -> dict[str, dict[str, int]]:
    padding = 32
    module_gap = 24
    scaled_ramps = []
    for ramp_id, ramp, scale in ramps:
        size = (max(1, round(ramp.width * scale)), max(1, round(ramp.height * scale)))
        scaled_ramps.append((ramp_id, ramp.resize(size, Image.Resampling.LANCZOS)))

    module_width = max(module.width for _, module in scaled_ramps)
    module_height = sum(module.height for _, module in scaled_ramps) + module_gap * (len(scaled_ramps) - 1)
    sheet_width = padding * 3 + base.width + module_width
    sheet_height = padding * 2 + max(base.height, module_height)
    sheet = Image.new("RGBA", (sheet_width, sheet_height), (0, 0, 0, 0))
    sheet.alpha_composite(base, (padding, padding))

    regions = {
        "one_level_wide_no_ramps": {
            "x": padding,
            "y": padding,
            "width": base.width,
            "height": base.height,
        }
    }
    x = padding * 2 + base.width
    y = padding
    for ramp_id, module in scaled_ramps:
        sheet.alpha_composite(module, (x, y))
        regions[ramp_id] = {"x": x, "y": y, "width": module.width, "height": module.height}
        y += module.height + module_gap

    sheet.save(output, optimize=True)
    return regions


def build() -> dict[str, object]:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    base_path = OUTPUT_DIR / "three_level_wide_no_ramps.png"
    base, base_origin = save_clean(
        RAW_DIR / "front_2_5d_three_level_no_ramps_raw.png",
        base_path,
    )
    create_preview(base, OUTPUT_DIR / "three_level_wide_no_ramps_preview.png")

    left_path = OUTPUT_DIR / "ramp_left.png"
    front_path = OUTPUT_DIR / "ramp_front.png"
    right_path = OUTPUT_DIR / "ramp_right.png"
    left, _ = save_clean(RAW_DIR / "front_2_5d_ramp_left_raw.png", left_path)
    front, _ = save_clean(RAW_DIR / "front_2_5d_ramp_front_raw.png", front_path)
    right = ImageOps.mirror(left)
    right.save(right_path, optimize=True)

    one_level_path = OUTPUT_DIR / "one_level_wide_no_ramps.png"
    one_level, one_level_origin = save_clean(
        RAW_DIR / "front_2_5d_one_level_no_ramps_raw.png",
        one_level_path,
    )
    create_preview(one_level, OUTPUT_DIR / "one_level_wide_no_ramps_preview.png")

    ramps = [
        {
            "id": "ramp_left",
            "direction": "left",
            "file": relative(left_path),
            "upper_anchor_normalized": {"x": 0.82, "y": 0.13},
            "display_scale": 0.22,
            "from_level_delta": -1,
            "to_level_delta": 0,
            "walkable": True,
            "climbable": True,
        },
        {
            "id": "ramp_front",
            "direction": "front",
            "file": relative(front_path),
            "upper_anchor_normalized": {"x": 0.5, "y": 0.08},
            "display_scale": 0.24,
            "from_level_delta": -1,
            "to_level_delta": 0,
            "walkable": True,
            "climbable": True,
        },
        {
            "id": "ramp_right",
            "direction": "right",
            "file": relative(right_path),
            "upper_anchor_normalized": {"x": 0.18, "y": 0.13},
            "display_scale": 0.22,
            "from_level_delta": -1,
            "to_level_delta": 0,
            "walkable": True,
            "climbable": True,
        },
    ]

    sockets = [
        socket("entry_left", -1, 0, "left", (245, 940), base_origin),
        socket("entry_front", -1, 0, "front", (575, 1055), base_origin),
        socket("entry_right", -1, 0, "right", (905, 940), base_origin),
        socket("level_0_to_1_left", 0, 1, "left", (390, 545), base_origin),
        socket("level_0_to_1_right", 0, 1, "right", (760, 545), base_origin),
        socket("level_1_to_2_left", 1, 2, "left", (465, 275), base_origin),
        socket("level_1_to_2_right", 1, 2, "right", (690, 275), base_origin),
    ]

    walkable_source = [
        [(45, 655), (180, 420), (340, 370), (810, 370), (970, 425), (1105, 655), (1085, 950), (900, 1055), (245, 1055), (65, 950)],
        [(245, 315), (335, 280), (805, 280), (895, 330), (905, 455), (790, 545), (355, 545), (240, 465)],
        [(390, 145), (500, 125), (650, 125), (770, 150), (785, 225), (700, 275), (450, 275), (385, 225)],
    ]
    walkable_regions = [
        {
            "id": f"walk_level_{level}",
            "height_level": level,
            "points": shift_points(points, base_origin),
        }
        for level, points in enumerate(walkable_source)
    ]

    one_level_sockets = [
        socket("one_level_entry_left", -1, 0, "left", (245, 940), one_level_origin),
        socket("one_level_entry_front", -1, 0, "front", (575, 1025), one_level_origin),
        socket("one_level_entry_right", -1, 0, "right", (905, 940), one_level_origin),
    ]
    sheet_path = OUTPUT_DIR / "one_level_mountain_sheet.png"
    sheet_regions = create_one_level_sheet(
        one_level,
        [("ramp_left", left, 0.22), ("ramp_front", front, 0.24), ("ramp_right", right, 0.22)],
        sheet_path,
    )

    manifest = {
        "schema_version": 1,
        "pack_id": "low_poly_sandstone_modular_front_2_5d",
        "projection": "front_2_5d",
        "base_prefabs": [
            {
                "id": "three_level_wide_no_ramps",
                "file": relative(base_path),
                "preview": relative(OUTPUT_DIR / "three_level_wide_no_ramps_preview.png"),
                "level_count": 3,
                "image_size": [base.width, base.height],
                "strict_nested_footprints": True,
                "walkable_regions": walkable_regions,
                "sockets": sockets,
            },
            {
                "id": "one_level_wide_no_ramps",
                "file": relative(one_level_path),
                "preview": relative(OUTPUT_DIR / "one_level_wide_no_ramps_preview.png"),
                "level_count": 1,
                "image_size": [one_level.width, one_level.height],
                "strict_nested_footprints": True,
                "walkable_regions": [
                    {
                        "id": "walk_level_0",
                        "height_level": 0,
                        "points": shift_points(
                            [(45, 655), (180, 420), (340, 370), (810, 370), (970, 425), (1105, 655), (1085, 950), (900, 1055), (245, 1055), (65, 950)],
                            one_level_origin,
                        ),
                    }
                ],
                "sockets": one_level_sockets,
            }
        ],
        "ramp_modules": ramps,
        "rules": {
            "one_ramp_per_transition": True,
            "adjacent_levels_only": True,
            "ramp_anchor": "upper_landing",
            "default_entry_socket": "entry_front",
            "default_transition_sockets": ["level_0_to_1_left", "level_1_to_2_right"],
        },
        "atlas_sheets": [
            {
                "id": "one_level_mountain_sheet",
                "file": relative(sheet_path),
                "transparent": True,
                "regions": sheet_regions,
            }
        ],
    }
    manifest_path = OUTPUT_DIR / "modular_mountain_pack_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    create_module_preview(
        base,
        [("left", left), ("front", front), ("right", right)],
        OUTPUT_DIR / "modular_mountain_pack_preview.png",
    )
    return manifest


def main() -> int:
    manifest = build()
    print(
        f"Prepared {len(manifest['base_prefabs'])} ramp-free mountains and "
        f"{len(manifest['ramp_modules'])} ramp modules"
    )
    print(f"Output: {OUTPUT_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
