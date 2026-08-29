#!/usr/bin/env python3
"""Compose configurable multi-level mountain prefabs from a modular atlas."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a levelled mountain from a modular mountain atlas.")
    parser.add_argument("--atlas-dir", required=True, type=Path, help="Directory with level_atlas_manifest.json.")
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_level_generated")
    parser.add_argument("--level-widths", default="8,6,5,4", help="Comma-separated level widths, bottom to top.")
    parser.add_argument("--level-height", type=int, default=118, help="Vertical distance between level tops.")
    parser.add_argument("--unit-width", type=int, default=92, help="Horizontal distance per repeated platform unit.")
    parser.add_argument("--scale", type=float, default=0.62)
    parser.add_argument("--castle-width", type=int, default=190)
    parser.add_argument("--castle-height", type=int, default=130)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    widths = parse_widths(args.level_widths)
    image, manifest = compose(args.atlas_dir, args.name, widths, args.level_height, args.unit_width, args.scale, args.castle_width, args.castle_height)
    image.save(args.output_dir / "prefab.png")
    (args.output_dir / "prefab_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_debug_preview(args.output_dir / "prefab_debug_preview.png", image, manifest)
    write_flat_preview(args.output_dir / "prefab_preview.png", image)
    print(f"Wrote prefab: {args.output_dir / 'prefab.png'}")
    print(f"Wrote manifest: {args.output_dir / 'prefab_manifest.json'}")
    print(f"Wrote levels: {len(widths)}")


def parse_widths(value: str) -> list[int]:
    widths = [max(2, int(part.strip())) for part in value.split(",") if part.strip()]
    if not widths:
        raise ValueError("--level-widths must contain at least one number")
    return widths


def compose(
    atlas_dir: Path,
    name: str,
    widths: list[int],
    level_height: int,
    unit_width: int,
    scale: float,
    castle_width: int,
    castle_height: int,
) -> tuple[Image.Image, dict]:
    atlas_manifest = json.loads((atlas_dir / "level_atlas_manifest.json").read_text(encoding="utf-8"))
    assets = {asset["role"]: asset for asset in atlas_manifest.get("assets", [])}

    margin_x = 120
    top_margin = 72
    bottom_margin = 160
    max_width = max(widths)
    canvas_width = margin_x * 2 + max_width * unit_width + 220
    canvas_height = top_margin + bottom_margin + (len(widths) - 1) * level_height + 180
    center_x = canvas_width // 2
    canvas = Image.new("RGBA", (canvas_width, canvas_height), (0, 0, 0, 0))
    placements: list[dict] = []
    levels: list[dict] = []
    walkable_regions: list[dict] = []
    route_edges: list[dict] = []

    level_layout = []
    for index, width in enumerate(widths):
        y = top_margin + (len(widths) - 1 - index) * level_height
        width_px = width * unit_width
        x = round(center_x - width_px * 0.5 + ((index % 2) - 0.5) * 74)
        level_layout.append({"index": index, "width": width, "x": x, "y": y, "width_px": width_px})

    for level in level_layout:
        draw_level(canvas, atlas_dir, assets, placements, level, unit_width, scale)
        region_id = f"level_{level['index']}_walk"
        walkable_regions.append(
            {
                "id": region_id,
                "level": level["index"],
                "x": level["x"] + 22,
                "y": level["y"] + 8,
                "width": max(64, level["width_px"] - 44),
                "height": 56,
                "kind": "castle_plateau" if level["index"] == len(widths) - 1 else "walkable_level",
            }
        )
        levels.append(
            {
                "id": f"level_{level['index']}",
                "index": level["index"],
                "height": level["index"],
                "width_units": level["width"],
                "walkable_region": region_id,
            }
        )

    for index in range(len(level_layout) - 1):
        lower = level_layout[index]
        upper = level_layout[index + 1]
        route_role = "ramp_up_left_to_right" if index % 2 == 0 else "ramp_up_right_to_left"
        route_x = round((lower["x"] + upper["x"]) * 0.5 + unit_width * 0.65)
        route_y = round((lower["y"] + upper["y"]) * 0.5 + 20)
        paste_role(canvas, atlas_dir, assets, placements, route_role, route_x, route_y, scale * 0.78, 40 + index)
        route_region_id = f"route_{index}_to_{index + 1}"
        walkable_regions.append(
            {
                "id": route_region_id,
                "level": index,
                "from_level": index,
                "to_level": index + 1,
                "x": route_x + 26,
                "y": route_y + 24,
                "width": 178,
                "height": 86,
                "kind": "climb_route",
            }
        )
        route_edges.append(
            {
                "from": f"level_{index}",
                "to": f"level_{index + 1}",
                "role": route_role,
                "climbable": True,
                "walkable_region": route_region_id,
                "entry": {"x": lower["x"] + lower["width_px"] - 96, "y": lower["y"] + 44},
                "exit": {"x": upper["x"] + 72, "y": upper["y"] + 48},
            }
        )

    top = level_layout[-1]
    castle_anchor = {
        "x": round(top["x"] + top["width_px"] * 0.5 - castle_width * 0.5),
        "y": top["y"] + 8,
        "width": castle_width,
        "height": min(castle_height, 92),
        "level": top["index"],
        "pivot": "footprint_center",
        "z_index": 80,
    }
    player_spawn = {"x": level_layout[0]["x"] + 48, "y": level_layout[0]["y"] + 44, "level": 0, "kind": "route_start"}
    plateau_exit = {"x": top["x"] + top["width_px"] - 90, "y": top["y"] + 42, "level": top["index"], "kind": "route_end"}

    manifest = {
        "name": name,
        "kind": "mountain_configurable_level_prefab",
        "source_pack": str(atlas_dir).replace("\\", "/"),
        "prefab_image": "prefab.png",
        "size": {"width": canvas_width, "height": canvas_height},
        "parameters": {
            "level_widths": widths,
            "level_height": level_height,
            "unit_width": unit_width,
            "scale": scale,
        },
        "levels": levels,
        "walkable_regions": walkable_regions,
        "route_edges": route_edges,
        "route_up": [edge["role"] for edge in route_edges],
        "anchors": {"castle_anchor": castle_anchor, "player_spawn": player_spawn, "plateau_exit": plateau_exit},
        "placements": sorted(placements, key=lambda item: item["z_index"]),
        "notes": [
            "This prefab is generated from developer parameters, not a fixed hand-placed mountain.",
            "level_widths controls how wide each walkable level is, bottom to top.",
            "route_edges connect levels and should be used for climb/movement rules.",
        ],
    }
    return canvas, manifest


def draw_level(
    canvas: Image.Image,
    atlas_dir: Path,
    assets: dict,
    placements: list[dict],
    level: dict,
    unit_width: int,
    scale: float,
) -> None:
    x = level["x"]
    y = level["y"]
    width = level["width"]
    cliff_y = y + 52
    paste_role(canvas, atlas_dir, assets, placements, "cliff_left_side", x, cliff_y, scale * 0.72, 0 + level["index"])
    for unit in range(max(1, width - 2)):
        paste_role(canvas, atlas_dir, assets, placements, "cliff_middle_repeat", x + unit_width * (unit + 1), cliff_y, scale * 0.72, 1 + level["index"])
    paste_role(canvas, atlas_dir, assets, placements, "cliff_right_side", x + unit_width * (width - 1), cliff_y, scale * 0.72, 2 + level["index"])

    paste_role(canvas, atlas_dir, assets, placements, "top_left_end", x, y, scale * 0.72, 20 + level["index"])
    for unit in range(max(1, width - 2)):
        paste_role(canvas, atlas_dir, assets, placements, "top_middle_repeat", x + unit_width * (unit + 1), y, scale * 0.72, 21 + level["index"])
    paste_role(canvas, atlas_dir, assets, placements, "top_right_end", x + unit_width * (width - 1), y, scale * 0.72, 22 + level["index"])
    if width >= 5:
        paste_role(canvas, atlas_dir, assets, placements, "dirt_path_overlay", x + unit_width * 2, y + 12, scale * 0.34, 32 + level["index"])


def paste_role(
    canvas: Image.Image,
    atlas_dir: Path,
    assets: dict,
    placements: list[dict],
    role: str,
    x: int,
    y: int,
    scale: float,
    z_index: int,
) -> None:
    asset = assets.get(role)
    if not asset:
        return
    sprite = load_trimmed_sprite(atlas_dir / asset["file"])
    sprite = sprite.resize(
        (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale))),
        Image.Resampling.LANCZOS,
    )
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


def load_trimmed_sprite(path: Path) -> Image.Image:
    sprite = Image.open(path).convert("RGBA")
    bbox = sprite.getchannel("A").getbbox()
    return sprite.crop(bbox) if bbox else sprite


def write_flat_preview(path: Path, image: Image.Image) -> None:
    bg = Image.new("RGBA", image.size, (24, 34, 43, 255))
    bg.alpha_composite(image)
    bg.convert("RGB").save(path)


def write_debug_preview(path: Path, image: Image.Image, manifest: dict) -> None:
    bg = Image.new("RGBA", image.size, (24, 34, 43, 255))
    bg.alpha_composite(image)
    draw = ImageDraw.Draw(bg)
    for region in manifest["walkable_regions"]:
        draw.rectangle(
            (region["x"], region["y"], region["x"] + region["width"], region["y"] + region["height"]),
            outline=(80, 220, 120, 255),
            width=3,
        )
    anchor = manifest["anchors"]["castle_anchor"]
    draw.rectangle(
        (anchor["x"], anchor["y"], anchor["x"] + anchor["width"], anchor["y"] + anchor["height"]),
        outline=(255, 220, 80, 255),
        width=4,
    )
    spawn = manifest["anchors"]["player_spawn"]
    draw.ellipse((spawn["x"] - 9, spawn["y"] - 9, spawn["x"] + 9, spawn["y"] + 9), fill=(80, 180, 255, 255))
    exit_point = manifest["anchors"]["plateau_exit"]
    draw.ellipse(
        (exit_point["x"] - 9, exit_point["y"] - 9, exit_point["x"] + 9, exit_point["y"] + 9),
        fill=(255, 120, 80, 255),
    )
    bg.convert("RGB").save(path)


if __name__ == "__main__":
    main()
