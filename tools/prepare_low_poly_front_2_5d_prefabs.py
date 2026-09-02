from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image

from prepare_low_poly_transition_prefab import PROJECT_ROOT, create_preview, remove_checkerboard, shift_points


PREFAB_ROOT = (
    PROJECT_ROOT
    / "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs"
)
DEFAULT_RAW_DIR = PREFAB_ROOT / "raw"
DEFAULT_OUTPUT = PREFAB_ROOT / "front_2_5d"


def clean_source(source: Image.Image) -> tuple[Image.Image, tuple[int, int]]:
    rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")
    alpha_range = alpha.getextrema()
    if alpha_range[0] < 16:
        bbox = alpha.getbbox()
        if bbox is None:
            raise ValueError("Generated prefab contains no visible pixels")
        padding = 12
        left = max(0, bbox[0] - padding)
        top = max(0, bbox[1] - padding)
        right = min(rgba.width, bbox[2] + padding)
        bottom = min(rgba.height, bbox[3] + padding)
        return rgba.crop((left, top, right, bottom)), (left, top)
    return remove_checkerboard(rgba)


def route_region(
    region_id: str,
    from_id: str,
    to_id: str,
    from_level: int,
    to_level: int,
    points: list[dict[str, int]],
    *,
    is_entry: bool = False,
) -> dict[str, object]:
    region: dict[str, object] = {
        "id": region_id,
        "role": "integrated_entry_ramp" if is_entry else "integrated_ramp",
        "from": from_id,
        "to": to_id,
        "from_level": from_level,
        "to_level": to_level,
        "from_elevation_px": from_level * 240,
        "to_elevation_px": to_level * 240,
        "walkable": True,
        "climbable": True,
        "visual_includes_wall": True,
        "points": points,
    }
    if is_entry:
        region["is_entry"] = True
    return region


def build_nested_support(level_points: list[list[tuple[int, int]]], minimum_margin_px: int) -> dict[str, object]:
    footprints = []
    for level, points in enumerate(level_points):
        xs = [point[0] for point in points]
        footprints.append(
            {
                "height_level": level,
                "x_min": min(xs),
                "x_max": max(xs),
            }
        )

    transitions = []
    for lower, upper in zip(footprints, footprints[1:]):
        left_margin = int(upper["x_min"]) - int(lower["x_min"])
        right_margin = int(lower["x_max"]) - int(upper["x_max"])
        if left_margin < minimum_margin_px or right_margin < minimum_margin_px:
            raise ValueError(
                f"Level {upper['height_level']} hangs outside level {lower['height_level']}: "
                f"left margin {left_margin}px, right margin {right_margin}px"
            )
        transitions.append(
            {
                "lower_level": lower["height_level"],
                "upper_level": upper["height_level"],
                "left_margin_px": left_margin,
                "right_margin_px": right_margin,
            }
        )

    return {
        "strict_nested_footprints": True,
        "minimum_side_margin_px": minimum_margin_px,
        "footprints": footprints,
        "transitions": transitions,
    }


def write_prefab(
    *,
    variant_id: str,
    source_path: Path,
    output_dir: Path,
    level_points: list[list[tuple[int, int]]],
    route_points: list[list[tuple[int, int]]],
    anchors: dict[str, tuple[int, int, int]],
    route_pattern: str,
) -> dict[str, object]:
    source = Image.open(source_path)
    sprite, origin = clean_source(source)
    sprite_name = f"{variant_id}.png"
    preview_name = f"{variant_id}_preview.png"
    chunk_name = f"{variant_id}_chunk_manifest.json"
    manifest_name = f"{variant_id}_prefab_manifest.json"
    sprite_path = output_dir / sprite_name
    sprite.save(sprite_path, optimize=True)
    create_preview(sprite, output_dir / preview_name)

    walkable_regions = []
    levels = []
    for index, points in enumerate(level_points):
        region_id = f"walk_level_{index}"
        levels.append({"id": f"level_{index}", "height_level": index, "walkable_region": region_id})
        walkable_regions.append(
            {
                "id": region_id,
                "kind": "walkable_floor",
                "height_level": index,
                "points": shift_points(points, origin),
            }
        )

    routes = [
        route_region(
            "entrance_to_level_0",
            "outside",
            "level_0",
            -1,
            0,
            shift_points(route_points[0], origin),
            is_entry=True,
        )
    ]
    route_edges = []
    for level in range(len(level_points) - 1):
        route_id = f"route_{level}_to_{level + 1}"
        route_edges.append(
            {
                "id": route_id,
                "role": "integrated_ramp",
                "from": f"level_{level}",
                "to": f"level_{level + 1}",
            }
        )
        routes.append(
            route_region(
                route_id,
                f"level_{level}",
                f"level_{level + 1}",
                level,
                level + 1,
                shift_points(route_points[level + 1], origin),
            )
        )

    relative_sprite = str(sprite_path.relative_to(PROJECT_ROOT)).replace("\\", "/")
    chunk_manifest = {
        "schema_version": 1,
        "pack_id": f"low_poly_sandstone_{variant_id}_chunk",
        "assets": [
            {
                "id": f"LP-FRONT-{len(level_points)}L-01",
                "role": "front_facing_height_aware_mountain",
                "category": "height_aware_prefab",
                "file": relative_sprite,
                "default_position": {"x": 0, "y": 0},
                "scale": 1.0,
                "height_level": len(level_points) - 1,
                "z_index": 0,
                "walkable": True,
                "climbable": True,
                "visual_includes_wall": True,
            }
        ],
    }
    (output_dir / chunk_name).write_text(json.dumps(chunk_manifest, indent=2) + "\n", encoding="utf-8")

    shifted_anchors = {
        name: {
            "x": point[0] - origin[0],
            "y": point[1] - origin[1],
            "height_level": point[2],
        }
        for name, point in anchors.items()
    }
    nested_support = build_nested_support(level_points, minimum_margin_px=32)
    manifest = {
        "schema_version": 1,
        "pack_id": f"low_poly_sandstone_{variant_id}",
        "variant_id": variant_id,
        "projection": "front_2_5d",
        "camera_direction": "front",
        "entrance_direction": "front_center",
        "route_pattern": route_pattern,
        "level_count": len(level_points),
        "supports_castle": len(level_points) >= 3,
        "nested_support": nested_support,
        "prefab_chunk_manifest": chunk_name,
        "levels": levels,
        "walkable_regions": walkable_regions,
        "route_edges": route_edges,
        "route_regions": routes,
        "anchors": shifted_anchors,
        "artifacts": {"sprite": sprite_name, "preview": preview_name},
        "image_size": [sprite.width, sprite.height],
        "source_generation_prompt": (
            "One unified front-facing 2.5D low-poly sandstone mountain with explicit height bands, "
            "a bottom-center entrance, and ramps that terminate on their correct floors."
        ),
    }
    (output_dir / manifest_name).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return {"id": variant_id, "manifest": manifest_name, "preview": preview_name, "levels": len(level_points)}


def build(raw_dir: Path, output_dir: Path) -> dict[str, object]:
    output_dir.mkdir(parents=True, exist_ok=True)
    variants = [
        write_prefab(
            variant_id="front_2_5d_two_level_center",
            source_path=raw_dir / "front_2_5d_two_level_raw.png",
            output_dir=output_dir,
            level_points=[
                [(105, 555), (365, 452), (482, 565), (730, 565), (838, 447), (1090, 540), (1080, 720), (895, 865), (300, 865), (105, 720)],
                [(270, 120), (430, 38), (760, 35), (925, 105), (980, 205), (920, 285), (720, 308), (390, 308), (235, 220)],
            ],
            route_points=[
                [(455, 900), (760, 900), (760, 1275), (455, 1275)],
                [(493, 305), (724, 305), (718, 565), (497, 565)],
            ],
            anchors={
                "entrance": (607, 1220, -1),
                "player_spawn": (607, 760, 0),
                "summit": (607, 170, 1),
                "castle_anchor": (607, 170, 1),
            },
            route_pattern="straight_center_ascent",
        ),
        write_prefab(
            variant_id="front_2_5d_three_level_switchback",
            source_path=raw_dir / "front_2_5d_three_level_raw.png",
            output_dir=output_dir,
            level_points=[
                [(45, 655), (180, 420), (340, 370), (810, 370), (970, 425), (1105, 655), (1085, 950), (900, 1055), (245, 1055), (65, 950)],
                [(245, 315), (335, 280), (805, 280), (895, 330), (905, 455), (790, 545), (355, 545), (240, 465)],
                [(390, 145), (500, 125), (650, 125), (770, 150), (785, 225), (700, 275), (450, 275), (385, 225)],
            ],
            route_points=[
                [(495, 1050), (645, 1050), (645, 1355), (495, 1355)],
                [(420, 715), (500, 715), (605, 545), (525, 545)],
                [(590, 275), (665, 275), (745, 425), (675, 425)],
            ],
            anchors={
                "entrance": (570, 1310, -1),
                "player_spawn": (570, 900, 0),
                "middle_landing": (455, 455, 1),
                "summit": (575, 205, 2),
                "castle_anchor": (575, 205, 2),
            },
            route_pattern="front_switchback",
        ),
    ]
    catalog = {
        "schema_version": 1,
        "pack_id": "low_poly_sandstone_front_2_5d_variants",
        "projection": "front_2_5d",
        "variants": variants,
    }
    (output_dir / "front_2_5d_variant_pack_manifest.json").write_text(
        json.dumps(catalog, indent=2) + "\n", encoding="utf-8"
    )
    return catalog


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Prepare front-facing 2.5D sandstone mountain prefabs.")
    parser.add_argument("--raw-dir", type=Path, default=DEFAULT_RAW_DIR)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    catalog = build(args.raw_dir.resolve(), args.output.resolve())
    print(f"Prepared {len(catalog['variants'])} front-facing 2.5D mountain variants")
    print(f"Output: {args.output.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
