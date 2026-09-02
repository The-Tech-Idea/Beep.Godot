from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image

from prepare_low_poly_transition_prefab import (
    PROJECT_ROOT,
    create_preview,
    remove_checkerboard,
    shift_points,
)


DEFAULT_INPUT = (
    PROJECT_ROOT
    / "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/raw/three_level_mountain_with_entry_raw.png"
)
DEFAULT_OUTPUT = (
    PROJECT_ROOT
    / "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs"
)


def build(input_path: Path, output_dir: Path) -> dict[str, object]:
    output_dir.mkdir(parents=True, exist_ok=True)
    source = Image.open(input_path)
    sprite, origin = remove_checkerboard(source)
    sprite_path = output_dir / "three_level_mountain.png"
    sprite.save(sprite_path, optimize=True)
    create_preview(sprite, output_dir / "three_level_mountain_preview.png")

    level_0 = shift_points(
        [(135, 585), (550, 405), (735, 475), (820, 570), (1100, 620), (950, 740), (550, 825), (205, 705)],
        origin,
    )
    level_1 = shift_points(
        [(720, 300), (865, 247), (1030, 285), (1110, 375), (1385, 410), (1420, 470), (1200, 575), (950, 510), (790, 435)],
        origin,
    )
    level_2 = shift_points(
        [(1000, 120), (1190, 38), (1515, 75), (1595, 185), (1430, 275), (1230, 275)],
        origin,
    )
    ramp_0_to_1 = shift_points(
        [(680, 510), (790, 580), (930, 390), (825, 338)],
        origin,
    )
    ramp_1_to_2 = shift_points(
        [(1025, 325), (1135, 385), (1260, 235), (1160, 180)],
        origin,
    )
    entrance_ramp = shift_points(
        [(175, 840), (325, 910), (525, 715), (360, 650)],
        origin,
    )

    relative_sprite = str(sprite_path.relative_to(PROJECT_ROOT)).replace("\\", "/")
    chunk_manifest = {
        "schema_version": 1,
        "pack_id": "low_poly_sandstone_three_level_mountain_chunk",
        "assets": [
            {
                "id": "LP-AUTH-MOUNTAIN-03",
                "role": "three_level_integrated_ramp_mountain",
                "category": "height_aware_prefab",
                "file": relative_sprite,
                "default_position": {"x": 0, "y": 0},
                "scale": 1.0,
                "height_level": 2,
                "z_index": 0,
                "walkable": True,
                "climbable": True,
                "visual_includes_wall": True,
            }
        ],
    }
    (output_dir / "three_level_mountain_chunk_manifest.json").write_text(
        json.dumps(chunk_manifest, indent=2) + "\n", encoding="utf-8"
    )

    manifest = {
        "schema_version": 1,
        "pack_id": "low_poly_sandstone_three_level_mountain",
        "projection": "2_to_1_isometric_presentation",
        "prefab_chunk_manifest": "three_level_mountain_chunk_manifest.json",
        "levels": [
            {"id": "level_0", "height_level": 0, "walkable_region": "walk_level_0"},
            {"id": "level_1", "height_level": 1, "walkable_region": "walk_level_1"},
            {"id": "level_2", "height_level": 2, "walkable_region": "walk_level_2"},
        ],
        "walkable_regions": [
            {"id": "walk_level_0", "kind": "walkable_floor", "height_level": 0, "points": level_0},
            {"id": "walk_level_1", "kind": "walkable_floor", "height_level": 1, "points": level_1},
            {"id": "walk_level_2", "kind": "walkable_floor", "height_level": 2, "points": level_2},
        ],
        "route_edges": [
            {"id": "route_0_to_1", "role": "integrated_ramp", "from": "level_0", "to": "level_1"},
            {"id": "route_1_to_2", "role": "integrated_ramp", "from": "level_1", "to": "level_2"},
        ],
        "route_regions": [
            {
                "id": "entrance_to_level_0",
                "role": "integrated_entry_ramp",
                "from": "outside",
                "to": "level_0",
                "from_level": -1,
                "to_level": 0,
                "from_elevation_px": -160,
                "to_elevation_px": 0,
                "walkable": True,
                "climbable": True,
                "visual_includes_wall": True,
                "is_entry": True,
                "points": entrance_ramp,
            },
            {
                "id": "route_0_to_1",
                "role": "integrated_ramp",
                "from": "level_0",
                "to": "level_1",
                "from_level": 0,
                "to_level": 1,
                "from_elevation_px": 0,
                "to_elevation_px": 210,
                "walkable": True,
                "climbable": True,
                "visual_includes_wall": True,
                "points": ramp_0_to_1,
            },
            {
                "id": "route_1_to_2",
                "role": "integrated_ramp",
                "from": "level_1",
                "to": "level_2",
                "from_level": 1,
                "to_level": 2,
                "from_elevation_px": 210,
                "to_elevation_px": 420,
                "walkable": True,
                "climbable": True,
                "visual_includes_wall": True,
                "points": ramp_1_to_2,
            },
        ],
        "anchors": {
            "entrance": {"x": entrance_ramp[0]["x"] + 75, "y": entrance_ramp[0]["y"] + 35, "height_level": -1},
            "player_spawn": {"x": level_0[1]["x"], "y": level_0[1]["y"] + 185, "height_level": 0},
            "middle_landing": {"x": level_1[4]["x"] - 210, "y": level_1[4]["y"] + 20, "height_level": 1},
            "summit": {"x": level_2[2]["x"] - 175, "y": level_2[2]["y"] + 75, "height_level": 2},
            "castle_anchor": {"x": level_2[2]["x"] - 175, "y": level_2[2]["y"] + 75, "height_level": 2},
        },
        "artifacts": {
            "sprite": "three_level_mountain.png",
            "preview": "three_level_mountain_preview.png",
        },
        "image_size": [sprite.width, sprite.height],
        "source_generation_prompt": "One unified three-floor sandstone mountain with a ground entrance ramp, two sequential internal ramps, and a broad castle-ready summit.",
    }
    (output_dir / "three_level_mountain_prefab_manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    return manifest


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Prepare the approved three-level sandstone mountain prefab.")
    parser.add_argument("--input", type=Path, default=DEFAULT_INPUT)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest = build(args.input.resolve(), args.output.resolve())
    print(f"Prepared approved three-level mountain: {manifest['image_size']}")
    print(f"Output: {args.output.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
