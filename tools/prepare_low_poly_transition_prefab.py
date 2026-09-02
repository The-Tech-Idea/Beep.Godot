from __future__ import annotations

import argparse
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


PROJECT_ROOT = Path(r"C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.Godot")
DEFAULT_INPUT = (
    PROJECT_ROOT
    / "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs/raw/two_level_transition_with_entry_raw.png"
)
DEFAULT_OUTPUT = (
    PROJECT_ROOT
    / "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/authored_prefabs"
)


def largest_component(mask: np.ndarray) -> np.ndarray:
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), connectivity=8)
    if count <= 1:
        raise ValueError("No sandstone foreground was detected")
    label = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    return labels == label


def remove_checkerboard(source: Image.Image) -> tuple[Image.Image, tuple[int, int]]:
    rgb = np.asarray(source.convert("RGB"), dtype=np.int16)
    red = rgb[:, :, 0]
    green = rgb[:, :, 1]
    blue = rgb[:, :, 2]

    # The generated sandstone is strongly warm while the baked checkerboard is neutral.
    warm = (red - blue > 34) & (green - blue > 18) & (blue < 205)
    kernel = np.ones((3, 3), dtype=np.uint8)
    closed = cv2.morphologyEx(warm.astype(np.uint8), cv2.MORPH_CLOSE, kernel, iterations=2)
    foreground = largest_component(closed)

    ys, xs = np.where(foreground)
    padding = 18
    left = max(0, int(xs.min()) - padding)
    top = max(0, int(ys.min()) - padding)
    right = min(source.width, int(xs.max()) + padding + 1)
    bottom = min(source.height, int(ys.max()) + padding + 1)

    alpha = np.where(foreground, 255, 0).astype(np.uint8)
    rgba = np.dstack((rgb.astype(np.uint8), alpha))
    rgba[alpha == 0, :3] = 0
    cleaned = Image.fromarray(rgba, mode="RGBA").crop((left, top, right, bottom))
    return cleaned, (left, top)


def shift_points(points: list[tuple[int, int]], origin: tuple[int, int]) -> list[dict[str, int]]:
    left, top = origin
    return [{"x": x - left, "y": y - top} for x, y in points]


def create_preview(sprite: Image.Image, output: Path) -> None:
    margin = 52
    canvas = Image.new("RGBA", (sprite.width + margin * 2, sprite.height + margin * 2), (19, 32, 39, 255))
    canvas.alpha_composite(sprite, (margin, margin))
    canvas.convert("RGB").save(output, quality=95)


def build(input_path: Path, output_dir: Path) -> dict[str, object]:
    output_dir.mkdir(parents=True, exist_ok=True)
    source = Image.open(input_path)
    sprite, origin = remove_checkerboard(source)
    sprite_path = output_dir / "two_level_transition.png"
    sprite.save(sprite_path, optimize=True)
    create_preview(sprite, output_dir / "two_level_transition_preview.png")

    lower_floor = shift_points(
        [(120, 525), (550, 285), (935, 455), (1045, 575), (810, 705), (545, 825), (170, 650)],
        origin,
    )
    upper_floor = shift_points(
        [(1005, 135), (1205, 30), (1605, 180), (1500, 320), (1265, 400), (1085, 305)],
        origin,
    )
    ramp = shift_points(
        [(805, 455), (975, 560), (1260, 260), (1095, 175)],
        origin,
    )
    entrance_ramp = shift_points(
        [(215, 835), (410, 915), (635, 700), (420, 630)],
        origin,
    )

    relative_sprite = str(sprite_path.relative_to(PROJECT_ROOT)).replace("\\", "/")
    chunk_manifest = {
        "schema_version": 1,
        "pack_id": "low_poly_sandstone_two_level_transition_chunk",
        "assets": [
            {
                "id": "LP-AUTH-TRANSITION-01",
                "role": "two_level_integrated_ramp_prefab",
                "category": "height_aware_prefab",
                "file": relative_sprite,
                "default_position": {"x": 0, "y": 0},
                "scale": 1.0,
                "height_level": 1,
                "z_index": 0,
                "walkable": True,
                "climbable": True,
                "visual_includes_wall": True,
            }
        ],
    }
    (output_dir / "two_level_transition_chunk_manifest.json").write_text(
        json.dumps(chunk_manifest, indent=2) + "\n", encoding="utf-8"
    )

    manifest = {
        "schema_version": 1,
        "pack_id": "low_poly_sandstone_two_level_transition",
        "projection": "2_to_1_isometric_presentation",
        "prefab_chunk_manifest": "two_level_transition_chunk_manifest.json",
        "levels": [
            {"id": "level_0", "height_level": 0, "walkable_region": "walk_level_0"},
            {"id": "level_1", "height_level": 1, "walkable_region": "walk_level_1"},
        ],
        "walkable_regions": [
            {"id": "walk_level_0", "kind": "walkable_floor", "height_level": 0, "points": lower_floor},
            {"id": "walk_level_1", "kind": "walkable_floor", "height_level": 1, "points": upper_floor},
        ],
        "route_edges": [
            {"id": "route_0_to_1", "role": "integrated_ramp", "from": "level_0", "to": "level_1"}
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
                "to_elevation_px": 240,
                "walkable": True,
                "climbable": True,
                "visual_includes_wall": True,
                "points": ramp,
            }
        ],
        "anchors": {
            "entrance": {"x": entrance_ramp[0]["x"] + 95, "y": entrance_ramp[0]["y"] + 35, "height_level": -1},
            "player_spawn": {"x": lower_floor[1]["x"], "y": lower_floor[1]["y"] + 190, "height_level": 0},
            "summit": {"x": upper_floor[2]["x"] - 180, "y": upper_floor[2]["y"] + 60, "height_level": 1},
            "castle_anchor": {"x": upper_floor[2]["x"] - 180, "y": upper_floor[2]["y"] + 60, "height_level": 1},
        },
        "artifacts": {
            "sprite": "two_level_transition.png",
            "preview": "two_level_transition_preview.png",
        },
        "image_size": [sprite.width, sprite.height],
        "source_generation_prompt": "One unified two-floor low-poly sandstone terrain prefab with a ground entrance ramp and an integrated level ramp.",
    }
    (output_dir / "two_level_transition_prefab_manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    return manifest


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Prepare the coherent two-level sandstone transition prefab.")
    parser.add_argument("--input", type=Path, default=DEFAULT_INPUT)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest = build(args.input.resolve(), args.output.resolve())
    print(f"Prepared coherent transition prefab: {manifest['image_size']}")
    print(f"Output: {args.output.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
