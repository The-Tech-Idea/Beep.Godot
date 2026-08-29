#!/usr/bin/env python3
"""Compose a mountain prefab from a semantic mountain atlas manifest."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


@dataclass(frozen=True)
class PlacementSpec:
    role: str
    x: int
    y: int
    scale: float
    z_index: int
    required: bool = False


@dataclass(frozen=True)
class LayoutSpec:
    width: int
    height: int
    placements: tuple[PlacementSpec, ...]
    walkable_regions: tuple[dict, ...] = ()
    levels: tuple[dict, ...] = ()
    route_edges: tuple[dict, ...] = ()
    anchors: dict[str, dict] | None = None


LAYOUTS: dict[str, LayoutSpec] = {
    "small_hill": LayoutSpec(
        330,
        250,
        (
            PlacementSpec("body_mid", 38, 18, 0.58, 0, True),
            PlacementSpec("ramp_straight_up", 38, 128, 0.28, 6, True),
            PlacementSpec("path_overlay", 122, 76, 0.22, 10),
            PlacementSpec("overlay_ground_patch", 96, 58, 0.22, 12),
        ),
    ),
    "medium_switchback": LayoutSpec(
        460,
        350,
        (
            PlacementSpec("body_back", 32, 18, 0.78, 0, True),
            PlacementSpec("ramp_straight_up", 54, 160, 0.36, 6, True),
            PlacementSpec("ramp_switchback_left", 112, 132, 0.47, 8, True),
            PlacementSpec("path_overlay", 176, 92, 0.31, 10),
            PlacementSpec("overlay_ground_patch", 126, 72, 0.30, 12),
        ),
    ),
    "wide_plateau": LayoutSpec(
        560,
        330,
        (
            PlacementSpec("body_wide_mesa", 68, 22, 0.82, 0, True),
            PlacementSpec("top_plateau_large", 90, 46, 0.34, 4),
            PlacementSpec("ramp_switchback_right", 214, 126, 0.44, 8, True),
            PlacementSpec("path_overlay", 182, 86, 0.34, 10),
            PlacementSpec("overlay_grass_patch", 190, 58, 0.26, 12),
        ),
    ),
    "tall_peak": LayoutSpec(
        430,
        420,
        (
            PlacementSpec("body_tall_peak", 112, 8, 0.86, 0, True),
            PlacementSpec("ramp_straight_up", 86, 250, 0.34, 7, True),
            PlacementSpec("path_overlay", 146, 188, 0.24, 10),
        ),
    ),
    "two_level_terrace": LayoutSpec(
        500,
        360,
        (
            PlacementSpec("body_front", 42, 44, 0.70, 0, True),
            PlacementSpec("top_terrace", 150, 70, 0.38, 4),
            PlacementSpec("ramp_switchback_left", 84, 152, 0.40, 8, True),
            PlacementSpec("path_overlay", 180, 106, 0.28, 10),
            PlacementSpec("overlay_ground_patch", 160, 86, 0.24, 12),
        ),
    ),
    "large_castle_plateau": LayoutSpec(
        820,
        560,
        (
            PlacementSpec("body_wide_mesa", 144, 54, 1.08, 0, True),
            PlacementSpec("body_back", 34, 12, 0.92, 1),
            PlacementSpec("top_plateau_large", 210, 68, 0.62, 4, True),
            PlacementSpec("top_plateau_medium", 350, 104, 0.50, 5),
            PlacementSpec("ramp_straight_up", 80, 330, 0.58, 7, True),
            PlacementSpec("ramp_switchback_left", 178, 248, 0.64, 8, True),
            PlacementSpec("ramp_switchback_right", 332, 172, 0.56, 9, True),
            PlacementSpec("path_overlay", 382, 108, 0.42, 10),
            PlacementSpec("overlay_ground_patch", 310, 92, 0.40, 12),
        ),
        walkable_regions=(
            {"id": "castle_plateau", "x": 292, "y": 86, "width": 238, "height": 122, "kind": "plateau"},
            {"id": "upper_walk_ring", "x": 206, "y": 122, "width": 398, "height": 154, "kind": "walkable_top"},
            {"id": "lower_entry", "x": 88, "y": 380, "width": 148, "height": 72, "kind": "route_entry"},
        ),
        anchors={
            "castle_anchor": {
                "x": 330,
                "y": 78,
                "width": 170,
                "height": 120,
                "pivot": "bottom_center",
                "z_index": 30,
                "notes": "Place the castle sprite here; bottom-center should sit on the upper plateau.",
            },
            "player_spawn": {"x": 126, "y": 424, "kind": "route_start"},
            "plateau_exit": {"x": 430, "y": 154, "kind": "route_end"},
        },
    ),
    "large_levelled_castle": LayoutSpec(
        920,
        660,
        (
            PlacementSpec("body_back", 34, 20, 0.94, 0, True),
            PlacementSpec("body_wide_mesa", 318, 58, 0.86, 1, True),
            PlacementSpec("body_front", 250, 228, 0.96, 2),
            PlacementSpec("top_plateau_large", 344, 82, 0.58, 4, True),
            PlacementSpec("top_plateau_medium", 244, 206, 0.50, 5),
            PlacementSpec("top_terrace", 150, 318, 0.50, 6),
            PlacementSpec("ramp_straight_up", 92, 430, 0.62, 8, True),
            PlacementSpec("ramp_switchback_left", 210, 346, 0.62, 9, True),
            PlacementSpec("ramp_switchback_right", 314, 264, 0.58, 10, True),
            PlacementSpec("path_overlay", 436, 162, 0.42, 11),
            PlacementSpec("ledge_connector", 350, 236, 0.38, 12),
            PlacementSpec("overlay_ground_patch", 408, 112, 0.36, 13),
        ),
        walkable_regions=(
            {"id": "level_0_entry", "level": 0, "x": 90, "y": 474, "width": 178, "height": 76, "kind": "route_entry"},
            {"id": "level_1_lower_terrace", "level": 1, "x": 178, "y": 350, "width": 226, "height": 94, "kind": "terrace"},
            {"id": "level_2_middle_terrace", "level": 2, "x": 278, "y": 260, "width": 238, "height": 98, "kind": "terrace"},
            {"id": "level_3_upper_walk", "level": 3, "x": 392, "y": 154, "width": 286, "height": 110, "kind": "walkable_top"},
            {"id": "level_4_castle_plateau", "level": 4, "x": 430, "y": 82, "width": 250, "height": 130, "kind": "castle_plateau"},
        ),
        levels=(
            {"id": "base", "index": 0, "height": 0, "walkable_region": "level_0_entry"},
            {"id": "lower_terrace", "index": 1, "height": 1, "walkable_region": "level_1_lower_terrace"},
            {"id": "middle_terrace", "index": 2, "height": 2, "walkable_region": "level_2_middle_terrace"},
            {"id": "upper_walk", "index": 3, "height": 3, "walkable_region": "level_3_upper_walk"},
            {"id": "castle_plateau", "index": 4, "height": 4, "walkable_region": "level_4_castle_plateau"},
        ),
        route_edges=(
            {"from": "base", "to": "lower_terrace", "role": "ramp_straight_up", "climbable": True},
            {"from": "lower_terrace", "to": "middle_terrace", "role": "ramp_switchback_left", "climbable": True},
            {"from": "middle_terrace", "to": "upper_walk", "role": "ramp_switchback_right", "climbable": True},
            {"from": "upper_walk", "to": "castle_plateau", "role": "path_overlay", "climbable": True},
        ),
        anchors={
            "castle_anchor": {
                "x": 470,
                "y": 74,
                "width": 176,
                "height": 126,
                "level": 4,
                "pivot": "bottom_center",
                "z_index": 30,
                "notes": "Place the castle sprite here; bottom-center should sit on level_4_castle_plateau.",
            },
            "player_spawn": {"x": 136, "y": 510, "level": 0, "kind": "route_start"},
            "plateau_exit": {"x": 510, "y": 170, "level": 4, "kind": "route_end"},
        },
    ),
}


DEFAULT_VARIANT = "large_levelled_castle"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Compose a demo mountain prefab from semantic atlas roles.")
    parser.add_argument("--semantic-dir", required=True, type=Path, help="Directory containing semantic_manifest.json.")
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_semantic_green_prefab")
    parser.add_argument("--variant", choices=sorted(LAYOUTS), default=DEFAULT_VARIANT)
    parser.add_argument("--all-variants", action="store_true", help="Write one subdirectory per built-in variant.")
    parser.add_argument("--width", type=int, default=None, help="Optional canvas width override for single-variant mode.")
    parser.add_argument("--height", type=int, default=None, help="Optional canvas height override for single-variant mode.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)

    if args.all_variants:
        written = []
        for variant in sorted(LAYOUTS):
            variant_dir = args.output_dir / variant
            variant_dir.mkdir(parents=True, exist_ok=True)
            image, placements = compose(args.semantic_dir, variant)
            image.save(variant_dir / "prefab.png")
            write_manifest(
                variant_dir / "prefab_manifest.json",
                f"{args.name}_{variant}",
                args.semantic_dir,
                image.size,
                placements,
                variant,
            )
            written.append((variant, variant_dir / "prefab.png"))
        write_showcase(args.output_dir / "showcase.png", written)
        print(f"Wrote variants: {len(written)}")
        print(f"Wrote showcase: {args.output_dir / 'showcase.png'}")
        return

    image, placements = compose(args.semantic_dir, args.variant, args.width, args.height)
    image.save(args.output_dir / "prefab.png")
    write_manifest(args.output_dir / "prefab_manifest.json", args.name, args.semantic_dir, image.size, placements, args.variant)
    print(f"Wrote prefab: {args.output_dir / 'prefab.png'}")
    print(f"Wrote manifest: {args.output_dir / 'prefab_manifest.json'}")
    print(f"Wrote placements: {len(placements)}")


def compose(
    semantic_dir: Path,
    variant: str = DEFAULT_VARIANT,
    width: int | None = None,
    height: int | None = None,
) -> tuple[Image.Image, list[dict]]:
    manifest = json.loads((semantic_dir / "semantic_manifest.json").read_text(encoding="utf-8"))
    assets_by_role = {asset["role"]: asset for asset in manifest.get("assets", [])}
    layout = LAYOUTS[variant]
    width = width if width is not None else layout.width
    height = height if height is not None else layout.height
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    placements: list[dict] = []

    for spec in sorted(layout.placements, key=lambda item: item.z_index):
        asset = assets_by_role.get(spec.role)
        if asset is None:
            if spec.required:
                raise RuntimeError(f"Required semantic role is missing: {spec.role}")
            continue
        sprite = load_trimmed_sprite(semantic_dir / asset["file"])
        sprite = sprite.resize(
            (max(1, round(sprite.width * spec.scale)), max(1, round(sprite.height * spec.scale))),
            Image.Resampling.LANCZOS,
        )
        canvas.alpha_composite(sprite, (spec.x, spec.y))
        placements.append(
            {
                "role": spec.role,
                "asset_id": asset["id"],
                "file": asset["file"],
                "position": {"x": spec.x, "y": spec.y},
                "scale": spec.scale,
                "z_index": spec.z_index,
                "walkable": asset.get("walkable", False),
                "climbable": asset.get("climbable", False),
            }
        )

    return canvas, placements


def load_trimmed_sprite(path: Path) -> Image.Image:
    sprite = Image.open(path).convert("RGBA")
    bbox = sprite.getchannel("A").getbbox()
    if bbox is None:
        return sprite
    return sprite.crop(bbox)


def write_manifest(
    path: Path,
    name: str,
    semantic_dir: Path,
    size: tuple[int, int],
    placements: list[dict],
    variant: str,
) -> None:
    layout = LAYOUTS[variant]
    manifest = {
        "name": name,
        "kind": "mountain_semantic_composite_prefab",
        "variant": variant,
        "source_pack": str(semantic_dir).replace("\\", "/"),
        "prefab_image": "prefab.png",
        "size": {"width": size[0], "height": size[1]},
        "levels": list(layout.levels),
        "walkable_regions": list(layout.walkable_regions),
        "route_edges": list(layout.route_edges),
        "anchors": layout.anchors or {},
        "route_up": [
            placement["role"]
            for placement in placements
            if placement.get("climbable") or placement["role"] in {"path_overlay", "ledge_connector"}
        ],
        "placements": placements,
        "notes": [
            "Composed from a semantic atlas with fixed role names.",
            "Default large variants are level based: walkable_regions map to elevation levels.",
            "route_up lists the visual climb path from lower mountain to upper plateau.",
            "Godot can instantiate placements as Sprite2D nodes or use prefab.png as a baked sprite.",
        ],
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def write_showcase(path: Path, prefabs: list[tuple[str, Path]]) -> None:
    font = ImageFont.load_default()
    thumbs = []
    for name, prefab_path in prefabs:
        image = Image.open(prefab_path).convert("RGBA")
        image.thumbnail((260, 220), Image.Resampling.LANCZOS)
        thumbs.append((name, image))

    cell_w = 300
    cell_h = 270
    columns = 3
    rows = (len(thumbs) + columns - 1) // columns
    showcase = Image.new("RGBA", (columns * cell_w, max(1, rows) * cell_h), (24, 34, 43, 255))
    draw = ImageDraw.Draw(showcase)

    for index, (name, image) in enumerate(thumbs):
        col = index % columns
        row = index // columns
        x = col * cell_w + (cell_w - image.width) // 2
        y = row * cell_h + 18 + (220 - image.height) // 2
        showcase.alpha_composite(image, (x, y))
        draw.text((col * cell_w + 14, row * cell_h + cell_h - 28), name, fill=(226, 231, 226, 255), font=font)

    showcase.convert("RGB").save(path)


if __name__ == "__main__":
    main()
