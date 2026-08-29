#!/usr/bin/env python3
"""Compose configurable mountain levels from a 17-piece floor atlas."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


PRIMARY_FLOOR_ROLES = {
    "center": "floor_center",
    "n": "floor_edge_n",
    "s": "floor_edge_s",
    "w": "floor_edge_w",
    "e": "floor_edge_e",
    "nw": "floor_corner_nw",
    "ne": "floor_corner_ne",
    "sw": "floor_corner_sw",
    "se": "floor_corner_se",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a levelled mountain from the first-row floor17 atlas contract.")
    parser.add_argument("--floor17-dir", required=True, type=Path, help="Directory with floor17_manifest.json.")
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_floor17_generated")
    parser.add_argument("--level-widths", default="10,8,6,5", help="Comma-separated floor tile counts, bottom to top.")
    parser.add_argument("--level-sizes", default=None, help="Comma-separated WIDTHxDEPTH levels, bottom to top. Example: 10x4,8x3,6x3,5x3.")
    parser.add_argument("--tile-step", type=int, default=128, help="Horizontal spacing between connected floor pieces.")
    parser.add_argument("--depth-step", type=int, default=128, help="Vertical spacing between floor rows inside one level.")
    parser.add_argument("--level-gap", type=int, default=224, help="Vertical spacing between mountain levels.")
    parser.add_argument("--scale", type=float, default=1.0)
    parser.add_argument("--castle-width", type=int, default=220)
    parser.add_argument("--castle-height", type=int, default=120)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    level_sizes = parse_level_sizes(args.level_sizes, args.level_widths)
    image, manifest = compose(args.floor17_dir, args.name, level_sizes, args.tile_step, args.depth_step, args.level_gap, args.scale, args.castle_width, args.castle_height)
    image.save(args.output_dir / "prefab.png")
    (args.output_dir / "prefab_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_preview(args.output_dir / "prefab_preview.png", image)
    write_debug(args.output_dir / "prefab_debug_preview.png", image, manifest)
    print(f"Wrote prefab: {args.output_dir / 'prefab.png'}")
    print(f"Wrote manifest: {args.output_dir / 'prefab_manifest.json'}")
    print(f"Wrote levels: {len(level_sizes)}")


def parse_level_sizes(level_sizes: str | None, level_widths: str) -> list[tuple[int, int]]:
    if level_sizes:
        parsed = []
        for part in level_sizes.split(","):
            if not part.strip():
                continue
            width_text, depth_text = part.lower().split("x", 1)
            parsed.append((max(2, int(width_text.strip())), max(2, int(depth_text.strip()))))
        if parsed:
            return parsed
    return [(max(2, int(part.strip())), 3) for part in level_widths.split(",") if part.strip()]


def compose(
    floor17_dir: Path,
    name: str,
    level_sizes: list[tuple[int, int]],
    tile_step: int,
    depth_step: int,
    level_gap: int,
    scale: float,
    castle_width: int,
    castle_height: int,
) -> tuple[Image.Image, dict]:
    data = json.loads((floor17_dir / "floor17_manifest.json").read_text(encoding="utf-8"))
    asset_list = list(data.get("assets", []))
    extras_manifest = floor17_dir / "extras_manifest.json"
    if extras_manifest.exists():
        extra_data = json.loads(extras_manifest.read_text(encoding="utf-8"))
        asset_list.extend(extra_data.get("assets", []))
    assets = {asset["role"]: asset for asset in asset_list}
    sprites = {role: load_sprite(floor17_dir / asset["file"]) for role, asset in assets.items()}

    floor_width = round(avg_floor_width(sprites) * scale)
    floor_height = round(avg_floor_height(sprites) * scale)
    cliff_height = round(avg_cliff_height(sprites) * scale * 0.86)
    max_width_px = max(width for width, _ in level_sizes) * tile_step + 260
    canvas_width = max_width_px + 260
    max_depth_px = (max(depth for _, depth in level_sizes) - 1) * depth_step + floor_height
    canvas_height = 160 + (len(level_sizes) - 1) * level_gap + max_depth_px + cliff_height + 90
    canvas = Image.new("RGBA", (canvas_width, canvas_height), (0, 0, 0, 0))
    center_x = canvas_width // 2
    base_y = canvas_height - max_depth_px - cliff_height - 50

    placements: list[dict] = []
    levels: list[dict] = []
    walkable_regions: list[dict] = []
    route_edges: list[dict] = []
    level_rects = []

    for index, (width, depth) in enumerate(level_sizes):
        level_width_px = (width - 1) * tile_step + floor_width
        level_depth_px = (depth - 1) * depth_step + floor_height
        x = round(center_x - level_width_px * 0.5 + (-50 if index % 2 == 0 else 54))
        y = round(base_y - index * level_gap)
        draw_floor_level(canvas, floor17_dir, assets, sprites, placements, x, y, width, depth, tile_step, depth_step, scale, index)

        region_id = f"level_{index}_floor"
        walkable_regions.append(
            {
                "id": region_id,
                "level": index,
                "x": x + 22,
                "y": y + 20,
                "width": max(32, level_width_px - 44),
                "height": max(42, level_depth_px - 28),
                "kind": "castle_plateau" if index == len(level_sizes) - 1 else "walkable_floor",
            }
        )
        levels.append(
            {
                "id": f"level_{index}",
                "index": index,
                "height": index,
                "width_tiles": width,
                "depth_tiles": depth,
                "walkable_region": region_id,
            }
        )
        level_rects.append({"x": x, "y": y, "width": level_width_px, "height": level_depth_px})

    for index in range(len(level_rects) - 1):
        lower = level_rects[index]
        upper = level_rects[index + 1]
        role = "ramp_left_to_right" if index % 2 == 0 else "ramp_right_to_left"
        if index == 0 and "bottom_entry_ramp" in assets:
            role = "bottom_entry_ramp"
        route_sprite = sprites.get(role)
        if not route_sprite:
            continue
        route_scale = scale * 0.82
        route_width = round(route_sprite.width * route_scale)
        route_height = round(route_sprite.height * route_scale)
        x = round((lower["x"] + lower["width"] * 0.45 + upper["x"] + upper["width"] * 0.55) * 0.5 - route_width * 0.5)
        y = round((lower["y"] + lower["height"] + upper["y"]) * 0.5 - route_height * 0.35)
        paste(canvas, floor17_dir, assets, placements, role, x, y, route_scale, 50 + index)
        route_region_id = f"route_{index}_to_{index + 1}"
        walkable_regions.append(
            {
                "id": route_region_id,
                "level": index,
                "from_level": index,
                "to_level": index + 1,
                "x": x + round(route_width * 0.18),
                "y": y + round(route_height * 0.22),
                "width": round(route_width * 0.64),
                "height": round(route_height * 0.50),
                "kind": "climb_route",
            }
        )
        route_edges.append(
            {
                "from": f"level_{index}",
                "to": f"level_{index + 1}",
                "role": role,
                "walkable_region": route_region_id,
                "climbable": True,
            }
        )

    top = level_rects[-1]
    castle_anchor = {
        "x": round(top["x"] + top["width"] * 0.5 - castle_width * 0.5),
        "y": top["y"] + 12,
        "width": castle_width,
        "height": castle_height,
        "level": len(level_sizes) - 1,
        "pivot": "footprint_center",
        "z_index": 100,
    }
    manifest = {
        "name": name,
        "kind": "mountain_floor17_level_prefab",
        "source_pack": str(floor17_dir).replace("\\", "/"),
        "prefab_image": "prefab.png",
        "size": {"width": canvas.width, "height": canvas.height},
        "parameters": {
            "level_sizes": [{"width": width, "depth": depth} for width, depth in level_sizes],
            "tile_step": tile_step,
            "depth_step": depth_step,
            "level_gap": level_gap,
            "scale": scale,
        },
        "floor17_contract": data.get("generator_contract", {}),
        "levels": levels,
        "walkable_regions": walkable_regions,
        "route_edges": route_edges,
        "route_up": [edge["role"] for edge in route_edges],
        "anchors": {
            "castle_anchor": castle_anchor,
            "player_spawn": {"x": level_rects[0]["x"] + 52, "y": level_rects[0]["y"] + 46, "level": 0, "kind": "route_start"},
            "plateau_exit": {"x": castle_anchor["x"] + castle_width * 0.5, "y": castle_anchor["y"] + castle_height * 0.70, "level": len(level_sizes) - 1, "kind": "route_end"},
        },
        "placements": sorted(placements, key=lambda item: item["z_index"]),
        "notes": [
            "Uses a strict first-row 17-piece floor atlas.",
            "Developer controls each level with --level-sizes WIDTHxDEPTH.",
            "Floor surfaces are built only from floor17 roles; cliffs/routes/castle pieces are separate rows.",
        ],
    }
    return canvas, manifest


def draw_floor_level(
    canvas: Image.Image,
    atlas_dir: Path,
    assets: dict,
    sprites: dict,
    placements: list[dict],
    x: int,
    y: int,
    width: int,
    depth: int,
    tile_step: int,
    depth_step: int,
    scale: float,
    level_index: int,
) -> None:
    for row in range(depth):
        for col in range(width):
            role = floor_role_for(col, row, width, depth, assets)
            paste(
                canvas,
                atlas_dir,
                assets,
                placements,
                role,
                x + tile_step * col,
                y + depth_step * row,
                scale,
                10 + level_index + row,
            )
    if "cliff_middle_a" in assets:
        cliff_y = y + (depth - 1) * depth_step + round(avg_floor_height(sprites) * scale) - 2
        paste(canvas, atlas_dir, assets, placements, "cliff_left", x, cliff_y, scale * 0.86, level_index)
        for tile in range(max(0, width - 2)):
            role = "cliff_middle_a" if tile % 2 == 0 else "cliff_middle_b"
            paste(canvas, atlas_dir, assets, placements, role, x + tile_step * (tile + 1), cliff_y, scale * 0.86, level_index)
        paste(canvas, atlas_dir, assets, placements, "cliff_right", x + tile_step * (width - 1), cliff_y, scale * 0.86, level_index)


def floor_role_for(col: int, row: int, width: int, depth: int, assets: dict) -> str:
    last_col = width - 1
    last_row = depth - 1
    if width == 1 and depth == 1:
        return role_or("floor_center", assets)
    if col == 0 and row == 0:
        return role_or("floor_corner_nw", assets)
    if col == last_col and row == 0:
        return role_or("floor_corner_ne", assets)
    if col == 0 and row == last_row:
        return role_or("floor_corner_sw", assets)
    if col == last_col and row == last_row:
        return role_or("floor_corner_se", assets)
    if row == 0:
        return role_or("floor_edge_n", assets)
    if row == last_row:
        return role_or("floor_edge_s", assets)
    if col == 0:
        return role_or("floor_edge_w", assets)
    if col == last_col:
        return role_or("floor_edge_e", assets)
    return role_or("floor_center", assets)


def role_or(role: str, assets: dict) -> str:
    if role in assets:
        return role
    return next((fallback for fallback in PRIMARY_FLOOR_ROLES.values() if fallback in assets), role)


def avg_floor_width(sprites: dict) -> int:
    widths = [sprites[role].width for role in ("floor_center", "floor_edge_w", "floor_edge_e") if role in sprites]
    return round(sum(widths) / len(widths)) if widths else 96


def avg_floor_height(sprites: dict) -> int:
    heights = [sprites[role].height for role in ("floor_center", "floor_edge_n", "floor_edge_s") if role in sprites]
    return round(sum(heights) / len(heights)) if heights else 96


def avg_cliff_height(sprites: dict) -> int:
    heights = [sprites[role].height for role in ("cliff_left", "cliff_middle_a", "cliff_right") if role in sprites]
    return round(sum(heights) / len(heights)) if heights else 96


def load_sprite(path: Path) -> Image.Image:
    sprite = Image.open(path).convert("RGBA")
    bbox = sprite.getchannel("A").getbbox()
    return sprite.crop(bbox) if bbox else sprite


def paste(canvas: Image.Image, atlas_dir: Path, assets: dict, placements: list[dict], role: str, x: int, y: int, scale: float, z_index: int) -> None:
    asset = assets.get(role)
    if not asset:
        return
    sprite = load_sprite(atlas_dir / asset["file"])
    sprite = sprite.resize((max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale))), Image.Resampling.LANCZOS)
    canvas.alpha_composite(sprite, (x, y))
    placements.append(
        {
            "role": role,
            "asset_id": asset["id"],
            "file": asset["file"],
            "position": {"x": x, "y": y},
            "scale": scale,
            "z_index": z_index,
            "walkable": asset.get("walkable", False),
            "climbable": asset.get("climbable", False),
        }
    )


def write_preview(path: Path, image: Image.Image) -> None:
    bg = Image.new("RGBA", image.size, (24, 34, 43, 255))
    bg.alpha_composite(image)
    bg.convert("RGB").save(path)


def write_debug(path: Path, image: Image.Image, manifest: dict) -> None:
    bg = Image.new("RGBA", image.size, (24, 34, 43, 255))
    bg.alpha_composite(image)
    draw = ImageDraw.Draw(bg)
    for region in manifest["walkable_regions"]:
        color = (88, 230, 132, 255) if region["kind"] != "climb_route" else (80, 178, 255, 255)
        draw.rectangle((region["x"], region["y"], region["x"] + region["width"], region["y"] + region["height"]), outline=color, width=3)
    anchor = manifest["anchors"]["castle_anchor"]
    draw.rectangle((anchor["x"], anchor["y"], anchor["x"] + anchor["width"], anchor["y"] + anchor["height"]), outline=(255, 220, 80, 255), width=4)
    spawn = manifest["anchors"]["player_spawn"]
    draw.ellipse((spawn["x"] - 9, spawn["y"] - 9, spawn["x"] + 9, spawn["y"] + 9), fill=(80, 180, 255, 255))
    exit_point = manifest["anchors"]["plateau_exit"]
    draw.ellipse((exit_point["x"] - 9, exit_point["y"] - 9, exit_point["x"] + 9, exit_point["y"] + 9), fill=(255, 120, 80, 255))
    bg.convert("RGB").save(path)


if __name__ == "__main__":
    main()
