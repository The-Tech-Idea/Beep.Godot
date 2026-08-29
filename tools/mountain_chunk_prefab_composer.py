#!/usr/bin/env python3
"""Compose smoother levelled mountains from large hand-painted chunks."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


TERRACE_ROLES = ((3, "terrace_w3"), (5, "terrace_w5"), (7, "terrace_w7"), (9, "terrace_w9"))
CASTLE_ROLES = ((4, "castle_plateau_w4"), (6, "castle_plateau_w6"))
ROUTE_ROLES = ("bottom_entry_path", "long_zigzag_ramp", "left_switchback_ramp", "right_switchback_ramp", "top_landing_path")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a smoother levelled mountain from large chunks.")
    parser.add_argument("--chunk-dir", required=True, type=Path, help="Directory with chunk_atlas_manifest.json.")
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_chunk_levelled")
    parser.add_argument("--level-widths", default="9,7,5,4", help="Comma-separated level widths, bottom to top.")
    parser.add_argument("--level-gap", type=int, default=96)
    parser.add_argument("--scale", type=float, default=1.0)
    parser.add_argument("--castle-width", type=int, default=190)
    parser.add_argument("--castle-height", type=int, default=120)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    widths = [max(3, int(part.strip())) for part in args.level_widths.split(",") if part.strip()]
    image, manifest = compose(args.chunk_dir, args.name, widths, args.level_gap, args.scale, args.castle_width, args.castle_height)
    image.save(args.output_dir / "prefab.png")
    (args.output_dir / "prefab_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_preview(args.output_dir / "prefab_preview.png", image)
    write_debug(args.output_dir / "prefab_debug_preview.png", image, manifest)
    print(f"Wrote prefab: {args.output_dir / 'prefab.png'}")
    print(f"Wrote manifest: {args.output_dir / 'prefab_manifest.json'}")
    print(f"Wrote levels: {len(widths)}")


def compose(
    chunk_dir: Path,
    name: str,
    widths: list[int],
    level_gap: int,
    scale: float,
    castle_width: int,
    castle_height: int,
) -> tuple[Image.Image, dict]:
    data = json.loads((chunk_dir / "chunk_atlas_manifest.json").read_text(encoding="utf-8"))
    assets = {asset["role"]: asset for asset in data.get("assets", [])}

    sprites = {role: load_sprite(chunk_dir / asset["file"]) for role, asset in assets.items()}
    level_roles = [nearest_role(width, CASTLE_ROLES if index == len(widths) - 1 else TERRACE_ROLES) for index, width in enumerate(widths)]
    scaled_sizes = [scaled_size(sprites[role], scale) for role in level_roles]
    canvas_width = max(width for width, _ in scaled_sizes) + 420
    canvas_height = sum(max(98, height // 2) for _, height in scaled_sizes) + (len(widths) - 1) * level_gap + 210
    canvas = Image.new("RGBA", (canvas_width, canvas_height), (0, 0, 0, 0))

    center_x = canvas_width // 2
    base_y = canvas_height - 210
    placements: list[dict] = []
    levels: list[dict] = []
    walkable_regions: list[dict] = []
    route_edges: list[dict] = []
    level_points = []

    for index, (requested_width, role) in enumerate(zip(widths, level_roles)):
        sprite_size = scaled_size(sprites[role], scale)
        offset = (-95 if index % 2 == 0 else 85) + (len(widths) - index - 1) * 22
        x = round(center_x - sprite_size[0] * 0.5 + offset)
        y = round(base_y - index * level_gap - sprite_size[1] * 0.52)
        z = index * 10
        paste(canvas, chunk_dir, assets, placements, role, x, y, scale, z)
        if index == 0:
            paste(canvas, chunk_dir, assets, placements, "front_bulge", x - 58, y + 42, scale * 0.72, z + 1)
        elif index == 1:
            paste(canvas, chunk_dir, assets, placements, "left_extension", x - 74, y + 14, scale * 0.58, z + 1)
        elif index == len(widths) - 2:
            paste(canvas, chunk_dir, assets, placements, "right_extension", x + sprite_size[0] - 78, y + 12, scale * 0.54, z + 1)

        region_id = f"level_{index}_walk"
        walkable = {
            "id": region_id,
            "level": index,
            "x": x + round(sprite_size[0] * 0.17),
            "y": y + round(sprite_size[1] * 0.16),
            "width": round(sprite_size[0] * 0.66),
            "height": round(sprite_size[1] * 0.34),
            "kind": "castle_plateau" if index == len(widths) - 1 else "walkable_level",
        }
        walkable_regions.append(walkable)
        levels.append(
            {
                "id": f"level_{index}",
                "index": index,
                "height": index,
                "requested_width_units": requested_width,
                "selected_chunk": role,
                "walkable_region": region_id,
            }
        )
        level_points.append({"x": x, "y": y, "width": sprite_size[0], "height": sprite_size[1]})

    for index in range(len(level_points) - 1):
        lower = level_points[index]
        upper = level_points[index + 1]
        role = ROUTE_ROLES[min(index + 1, len(ROUTE_ROLES) - 1)]
        if index == 0:
            role = "bottom_entry_path"
        elif index % 2:
            role = "left_switchback_ramp"
        else:
            role = "right_switchback_ramp"
        route_scale = scale * 0.72
        route_size = scaled_size(sprites[role], route_scale)
        x = round((lower["x"] + lower["width"] * 0.40 + upper["x"] + upper["width"] * 0.48) * 0.5 - route_size[0] * 0.5)
        y = round((lower["y"] + upper["y"]) * 0.5 + 32)
        paste(canvas, chunk_dir, assets, placements, role, x, y, route_scale, 60 + index)
        route_region_id = f"route_{index}_to_{index + 1}"
        walkable_regions.append(
            {
                "id": route_region_id,
                "level": index,
                "from_level": index,
                "to_level": index + 1,
                "x": x + round(route_size[0] * 0.18),
                "y": y + round(route_size[1] * 0.18),
                "width": round(route_size[0] * 0.64),
                "height": round(route_size[1] * 0.58),
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

    top = level_points[-1]
    castle_anchor = {
        "x": round(top["x"] + top["width"] * 0.5 - castle_width * 0.5),
        "y": round(top["y"] + top["height"] * 0.18),
        "width": castle_width,
        "height": castle_height,
        "level": len(widths) - 1,
        "pivot": "footprint_center",
        "z_index": 100,
    }

    manifest = {
        "name": name,
        "kind": "mountain_chunk_levelled_prefab",
        "source_pack": str(chunk_dir).replace("\\", "/"),
        "prefab_image": "prefab.png",
        "size": {"width": canvas.width, "height": canvas.height},
        "parameters": {"level_widths": widths, "level_gap": level_gap, "scale": scale},
        "levels": levels,
        "walkable_regions": walkable_regions,
        "route_edges": route_edges,
        "route_up": [edge["role"] for edge in route_edges],
        "anchors": {
            "castle_anchor": castle_anchor,
            "player_spawn": {"x": level_points[0]["x"] + 48, "y": level_points[0]["y"] + level_points[0]["height"] * 0.42, "level": 0},
            "plateau_exit": {"x": castle_anchor["x"] + castle_width * 0.5, "y": castle_anchor["y"] + castle_height * 0.72, "level": len(widths) - 1},
        },
        "placements": sorted(placements, key=lambda item: item["z_index"]),
        "notes": [
            "Uses whole hand-painted chunks instead of repeated square modules.",
            "Developers control level count and requested widths; generator selects nearest available width chunk.",
            "walkable_regions and route_edges provide gameplay metadata.",
        ],
    }
    return canvas, manifest


def nearest_role(width: int, choices: tuple[tuple[int, str], ...]) -> str:
    return min(choices, key=lambda item: abs(item[0] - width))[1]


def load_sprite(path: Path) -> Image.Image:
    sprite = Image.open(path).convert("RGBA")
    bbox = sprite.getchannel("A").getbbox()
    return sprite.crop(bbox) if bbox else sprite


def scaled_size(sprite: Image.Image, scale: float) -> tuple[int, int]:
    return max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale))


def paste(canvas: Image.Image, chunk_dir: Path, assets: dict, placements: list[dict], role: str, x: int, y: int, scale: float, z_index: int) -> None:
    asset = assets.get(role)
    if not asset:
        return
    sprite = load_sprite(chunk_dir / asset["file"])
    sprite = sprite.resize(scaled_size(sprite, scale), Image.Resampling.LANCZOS)
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
        color = (92, 230, 132, 255) if region["kind"] != "climb_route" else (86, 180, 255, 255)
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
