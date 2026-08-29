#!/usr/bin/env python3
"""Build a fixed-role mountain TileMap atlas from a generated asset pack.

The image generator produces source art. The pack slicer extracts sprites.
This tool chooses a small, named subset for TileMap use so every atlas slot has
an explicit gameplay/rendering purpose.
"""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


@dataclass(frozen=True)
class RoleSpec:
    role: str
    category: str
    selector: str
    walkable: bool
    climbable: bool
    tags: tuple[str, ...]


ROLE_SPECS = [
    RoleSpec("top_center", "top_surface", "largest", True, False, ("top", "walkable")),
    RoleSpec("top_north_edge", "hill_edge", "wide", True, False, ("top", "edge", "north")),
    RoleSpec("top_south_edge", "hill_edge", "wide_alt", True, False, ("top", "edge", "south")),
    RoleSpec("top_west_edge", "top_surface", "tall", True, False, ("top", "edge", "west")),
    RoleSpec("top_east_edge", "top_surface", "tall_alt", True, False, ("top", "edge", "east")),
    RoleSpec("top_corner_nw", "cliff_edge_corner", "small", True, False, ("top", "corner", "north_west")),
    RoleSpec("top_corner_ne", "cliff_edge_corner", "small_alt", True, False, ("top", "corner", "north_east")),
    RoleSpec("top_corner_sw", "cliff_edge_corner", "square", True, False, ("top", "corner", "south_west")),
    RoleSpec("top_corner_se", "cliff_edge_corner", "square_alt", True, False, ("top", "corner", "south_east")),
    RoleSpec("cliff_front", "cliff_face", "largest", False, False, ("cliff", "south")),
    RoleSpec("cliff_front_left", "cliff_face", "left", False, False, ("cliff", "south_west")),
    RoleSpec("cliff_front_right", "cliff_face", "right", False, False, ("cliff", "south_east")),
    RoleSpec("cliff_column", "cliff_face", "tall", False, False, ("cliff", "column")),
    RoleSpec("cliff_side_left", "cliff_edge_corner", "left", False, False, ("cliff", "west")),
    RoleSpec("cliff_side_right", "cliff_edge_corner", "right", False, False, ("cliff", "east")),
    RoleSpec("cliff_base", "base_footer", "largest", False, False, ("cliff", "base")),
    RoleSpec("ramp_north", "slope_ramp", "upper_left", True, True, ("ramp", "north", "climbable")),
    RoleSpec("ramp_south", "slope_ramp", "lower_left", True, True, ("ramp", "south", "climbable")),
    RoleSpec("ramp_west", "slope_ramp", "left", True, True, ("ramp", "west", "climbable")),
    RoleSpec("ramp_east", "slope_ramp", "right", True, True, ("ramp", "east", "climbable")),
    RoleSpec("road_vertical", "path_cut", "largest", True, True, ("road", "vertical", "climbable")),
    RoleSpec("road_horizontal", "path_cut", "wide", True, True, ("road", "horizontal", "climbable")),
    RoleSpec("road_turn_left", "path_cut", "left", True, True, ("road", "turn", "left", "climbable")),
    RoleSpec("road_turn_right", "path_cut", "right", True, True, ("road", "turn", "right", "climbable")),
    RoleSpec("transition_rock_ground", "rock_ground_transition", "largest", True, False, ("transition", "ground")),
    RoleSpec("overlay_crack", "strata_overlay", "wide", False, False, ("overlay", "crack")),
    RoleSpec("overlay_strata", "strata_overlay", "largest", False, False, ("overlay", "strata")),
    RoleSpec("prop_boulder", "debris", "largest", False, False, ("prop", "rock")),
    RoleSpec("prop_small_rocks", "debris", "small", False, False, ("prop", "rock", "small")),
    RoleSpec("prop_vegetation", "vegetation", "largest", False, False, ("prop", "vegetation")),
    RoleSpec("special_cave", "special_feature", "wide", False, False, ("feature", "cave")),
    RoleSpec("special_peak", "special_feature", "tall", False, False, ("feature", "peak")),
    RoleSpec("shadow_small", "shadow", "small", False, False, ("shadow",)),
    RoleSpec("shadow_large", "shadow", "largest", False, False, ("shadow",)),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create a semantic mountain role atlas from a generated pack.")
    parser.add_argument("--pack-dir", required=True, type=Path, help="Directory containing atlas.png, manifest.json and sprites/.")
    parser.add_argument("--output-dir", required=True, type=Path, help="Directory for role_atlas.png and role_manifest.json.")
    parser.add_argument("--name", required=True, help="Role atlas name.")
    parser.add_argument("--slot-width", type=int, default=192)
    parser.add_argument("--slot-height", type=int, default=192)
    parser.add_argument("--columns", type=int, default=8)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((args.pack_dir / "manifest.json").read_text(encoding="utf-8"))
    assets = manifest.get("assets", [])
    groups: dict[str, list[dict]] = {}
    for asset in assets:
        groups.setdefault(asset.get("category", "misc"), []).append(asset)

    selected = []
    used_by_category: dict[str, set[str]] = {}
    for spec in ROLE_SPECS:
        asset = select_asset(groups, spec, used_by_category)
        if asset is None:
            continue
        selected.append((spec, asset))
        used_by_category.setdefault(spec.category, set()).add(asset["id"])

    slot = (max(1, args.slot_width), max(1, args.slot_height))
    columns = max(1, args.columns)
    rows = (len(selected) + columns - 1) // columns
    atlas = Image.new("RGBA", (columns * slot[0], max(1, rows) * slot[1]), (0, 0, 0, 0))

    role_assets = []
    for index, (spec, asset) in enumerate(selected):
        sprite = Image.open(args.pack_dir / asset["file"]).convert("RGBA")
        if sprite.width > slot[0] or sprite.height > slot[1]:
            sprite.thumbnail(slot, Image.Resampling.LANCZOS)

        col = index % columns
        row = index // columns
        dx = (col * slot[0]) + ((slot[0] - sprite.width) // 2)
        dy = (row * slot[1]) + max(0, slot[1] - sprite.height)
        atlas.alpha_composite(sprite, (dx, dy))
        role_assets.append(
            {
                "id": f"{args.name}_{spec.role}",
                "role": spec.role,
                "category": spec.category,
                "file": "role_atlas.png",
                "source_asset_id": asset["id"],
                "source_file": asset["file"],
                "atlas": {"x": col, "y": row},
                "tile_width": slot[0],
                "tile_height": slot[1],
                "source_rect": {"x": col, "y": row, "width": 1, "height": 1},
                "sprite_size": {"width": slot[0], "height": slot[1]},
                "walkable": spec.walkable,
                "climbable": spec.climbable,
                "suggested_z_index": z_index_for(spec.role),
                "tags": list(spec.tags),
            }
        )

    atlas.save(args.output_dir / "role_atlas.png")
    write_manifest(args.output_dir / "role_manifest.json", args.name, slot, columns, role_assets)
    write_preview(args.output_dir / "role_preview.png", atlas, role_assets, slot, columns)
    print(f"Wrote role atlas: {args.output_dir / 'role_atlas.png'}")
    print(f"Wrote role manifest: {args.output_dir / 'role_manifest.json'}")
    print(f"Wrote roles: {len(role_assets)}")


def select_asset(groups: dict[str, list[dict]], spec: RoleSpec, used_by_category: dict[str, set[str]]) -> dict | None:
    candidates = list(groups.get(spec.category, []))
    if not candidates and spec.category == "path_cut":
        candidates = list(groups.get("slope_ramp", []))
    if not candidates:
        return None

    used = used_by_category.get(spec.category, set())
    fresh = [asset for asset in candidates if asset["id"] not in used]
    if fresh:
        candidates = fresh

    key = selector_key(spec.selector)
    return sorted(candidates, key=key)[0]


def selector_key(selector: str):
    def values(asset: dict) -> tuple:
        rect = asset.get("source_rect", {})
        x = rect.get("x", 0)
        y = rect.get("y", 0)
        w = rect.get("width", 1)
        h = rect.get("height", 1)
        area = asset.get("alpha_area", w * h)
        aspect = w / max(1, h)
        square = abs(aspect - 1.0)
        if selector == "largest":
            return (-area, y, x)
        if selector == "small":
            return (area, y, x)
        if selector == "small_alt":
            return (area, x, y)
        if selector == "wide":
            return (-aspect, -area, y, x)
        if selector == "wide_alt":
            return (-aspect, y, -x)
        if selector == "tall":
            return (aspect, -area, y, x)
        if selector == "tall_alt":
            return (aspect, y, -x)
        if selector == "square":
            return (square, -area, y, x)
        if selector == "square_alt":
            return (square, -area, y, -x)
        if selector == "left":
            return (x, y, -area)
        if selector == "right":
            return (-x, y, -area)
        if selector == "upper_left":
            return (y, x, -area)
        if selector == "lower_left":
            return (-y, x, -area)
        return (-area, y, x)

    return values


def write_manifest(path: Path, name: str, slot: tuple[int, int], columns: int, role_assets: list[dict]) -> None:
    manifest = {
        "name": name,
        "kind": "mountain_role_tilemap_atlas",
        "source_atlas": "role_atlas.png",
        "tile_width": slot[0],
        "tile_height": slot[1],
        "atlas_columns": columns,
        "categories": sorted({asset["category"] for asset in role_assets}),
        "roles": [asset["role"] for asset in role_assets],
        "assets": role_assets,
        "notes": [
            "This is the TileMap-ready semantic atlas.",
            "Each atlas coordinate has a fixed role; do not infer role from image position.",
            "Use the raw generated pack only as source art.",
        ],
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def write_preview(path: Path, atlas: Image.Image, role_assets: list[dict], slot: tuple[int, int], columns: int) -> None:
    font = ImageFont.load_default()
    cell_w = 220
    cell_h = 232
    rows = (len(role_assets) + columns - 1) // columns
    preview = Image.new("RGBA", (columns * cell_w, max(1, rows) * cell_h), (30, 32, 31, 255))
    draw = ImageDraw.Draw(preview)
    for index, asset in enumerate(role_assets):
        col = index % columns
        row = index // columns
        x = col * cell_w
        y = row * cell_h
        src = atlas.crop((asset["atlas"]["x"] * slot[0], asset["atlas"]["y"] * slot[1], (asset["atlas"]["x"] + 1) * slot[0], (asset["atlas"]["y"] + 1) * slot[1]))
        draw.rectangle((x + 2, y + 2, x + cell_w - 2, y + cell_h - 2), outline=(70, 76, 72, 255))
        preview.alpha_composite(src, (x + (cell_w - slot[0]) // 2, y + 8))
        draw.text((x + 8, y + slot[1] + 18), asset["role"], fill=(236, 238, 234, 255), font=font)
        draw.text((x + 8, y + slot[1] + 34), asset["category"], fill=(166, 174, 166, 255), font=font)
    preview.convert("RGB").save(path)


def z_index_for(role: str) -> int:
    if role.startswith("shadow"):
        return -10
    if role.startswith("prop") or role.startswith("special"):
        return 5
    if role.startswith("overlay"):
        return 4
    if role.startswith("cliff"):
        return -1
    return 0


if __name__ == "__main__":
    main()
