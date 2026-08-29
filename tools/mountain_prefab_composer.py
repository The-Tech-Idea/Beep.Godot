#!/usr/bin/env python3
"""Compose complete mountain prefabs from a generated mountain asset pack.

This is separate from TileMap slicing. It treats the atlas as a kit of parts and
builds a single layered mountain/island object: base cliffs, top plateau, road
or ramp cuts, rocks, vegetation, and optional special features.
"""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw


@dataclass(frozen=True)
class Placement:
    role: str
    asset_id: str
    file: str
    x: int
    y: int
    scale: float
    z_index: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Compose full mountain prefabs from a generated asset pack.")
    parser.add_argument("--pack-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", required=True)
    parser.add_argument("--width", type=int, default=320)
    parser.add_argument("--height", type=int, default=260)
    parser.add_argument("--seed", type=int, default=43117)
    parser.add_argument("--showcase", type=Path, default=None)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    image, placements = compose_prefab(args.pack_dir, args.name, args.width, args.height, args.seed)
    image.save(args.output_dir / "prefab.png")
    write_manifest(args.output_dir / "prefab_manifest.json", args.name, args.pack_dir, image.size, placements)
    print(f"Wrote prefab: {args.output_dir / 'prefab.png'}")
    print(f"Wrote manifest: {args.output_dir / 'prefab_manifest.json'}")


def compose_prefab(pack_dir: Path, name: str, width: int, height: int, seed: int) -> tuple[Image.Image, list[Placement]]:
    data = json.loads((pack_dir / "manifest.json").read_text(encoding="utf-8"))
    groups: dict[str, list[dict]] = {}
    for asset in data.get("assets", []):
        groups.setdefault(asset.get("category", "misc"), []).append(asset)

    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    placements: list[Placement] = []

    # Broad soft shadow first.
    for role, x, y, scale in [
        ("shadow_large", width // 2 - 78, height - 52, 1.12),
        ("shadow_small", width // 2 + 42, height - 82, 0.58),
    ]:
        asset = pick(groups, "shadow", role, seed)
        if asset:
            paste_asset(canvas, pack_dir, asset, role, x, y, scale, -10, placements)

    # Main mountain body. Prefer large special pieces because they already
    # encode coherent cliff contours better than a repeated tile grid.
    for role, category, x, y, scale, z in [
        ("main_body", "special_feature", width // 2 - 118, 72, 1.18, 2),
        ("rear_cliff_left", "cliff_face", width // 2 - 122, 104, 0.66, 3),
        ("rear_cliff_right", "cliff_face", width // 2 + 34, 108, 0.60, 3),
        ("front_cliff_left", "cliff_face", width // 2 - 112, 150, 0.62, 5),
        ("front_cliff_right", "cliff_face", width // 2 + 44, 150, 0.58, 5),
    ]:
        asset = pick(groups, category, role, seed)
        if asset:
            paste_asset(canvas, pack_dir, asset, role, x, y, scale, z, placements)

    # Top surface should read as one plateau, not a grid.
    for role, x, y, scale in [
        ("top_plateau_main", width // 2 - 76, 56, 0.90),
        ("top_plateau_front", width // 2 - 48, 116, 0.62),
    ]:
        asset = pick(groups, "top_surface", role, seed)
        if asset:
            paste_asset(canvas, pack_dir, asset, role, x, y, scale, 6, placements)

    # Side rims and walk-up path/ramp.
    for role, category, x, y, scale, z in [
        ("left_rim", "cliff_edge_corner", width // 2 - 126, 112, 0.58, 7),
        ("right_rim", "cliff_edge_corner", width // 2 + 84, 120, 0.54, 7),
        ("front_rim", "hill_edge", width // 2 - 88, 166, 0.72, 8),
        ("road_cut", "path_cut", width // 2 - 34, 88, 0.74, 9),
        ("climb_ramp", "slope_ramp", width // 2 - 18, 124, 0.66, 10),
    ]:
        asset = pick(groups, category, role, seed)
        if asset:
            paste_asset(canvas, pack_dir, asset, role, x, y, scale, z, placements)

    # Props are sparse and placed after structural pieces.
    for i, (role, category, x, y, scale) in enumerate([
        ("prop_rocks_left", "debris", width // 2 - 112, height - 66, 0.46),
        ("prop_rocks_front", "debris", width // 2 - 12, height - 42, 0.38),
        ("prop_vegetation_top", "vegetation", width // 2 + 44, 58, 0.44),
        ("prop_vegetation_front", "vegetation", width // 2 - 82, height - 94, 0.34),
    ]):
        asset = pick(groups, category, f"{role}_{i}", seed)
        if asset:
            paste_asset(canvas, pack_dir, asset, role, x, y, scale, 12 + i, placements)

    return canvas, sorted(placements, key=lambda item: item.z_index)


def pick(groups: dict[str, list[dict]], category: str, salt: str, seed: int) -> dict | None:
    assets = [asset for asset in groups.get(category, []) if asset_quality(asset) >= minimum_quality(category, salt)]
    if not assets:
        return None

    def score(asset: dict) -> tuple[float, int]:
        rect = asset.get("source_rect", {})
        width = rect.get("width", 1)
        height = rect.get("height", 1)
        area = asset.get("alpha_area", width * height)
        aspect = width / max(1, height)
        salt_hash = stable_hash(salt, seed)
        quality = asset_quality(asset)
        if "shadow" in salt:
            return (-area, salt_hash)
        if "plateau" in salt:
            return (-quality, -area * (1.0 + min(aspect, 2.0) * 0.1), salt_hash)
        if "peak" in salt:
            return (aspect, -height, salt_hash)
        if "main_body" in salt:
            return (-area, -quality, salt_hash)
        if "rim" in salt or "road" in salt:
            return (-aspect, -area, salt_hash)
        return (-area, -quality, salt_hash)

    ordered = sorted(assets, key=score)
    return ordered[stable_hash(salt, seed) % min(len(ordered), 5)]


def asset_quality(asset: dict) -> float:
    rect = asset.get("source_rect", {})
    width = max(1, rect.get("width", 1))
    height = max(1, rect.get("height", 1))
    area = max(1, width * height)
    alpha_area = max(0, asset.get("alpha_area", area))
    coverage = alpha_area / area
    aspect = width / max(1, height)
    aspect_penalty = 0.0 if 0.28 <= aspect <= 3.8 else 0.4
    return coverage - aspect_penalty


def minimum_quality(category: str, salt: str) -> float:
    if category == "top_surface":
        return 0.50
    if category == "shadow":
        return 0.05
    if "main_body" in salt:
        return 0.35
    return 0.25


def paste_asset(
    canvas: Image.Image,
    pack_dir: Path,
    asset: dict,
    role: str,
    x: int,
    y: int,
    scale: float,
    z_index: int,
    placements: list[Placement],
) -> None:
    sprite = Image.open(pack_dir / asset["file"]).convert("RGBA")
    bbox = sprite.getchannel("A").getbbox()
    if bbox is None:
        return
    sprite = sprite.crop(bbox)
    if sprite.width > canvas.width * 0.72 or sprite.height > canvas.height * 0.72:
        return

    scale = max(0.05, scale)
    size = (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale)))
    sprite = sprite.resize(size, Image.Resampling.LANCZOS)
    canvas.alpha_composite(sprite, (x, y))
    placements.append(Placement(role, asset["id"], asset["file"], x, y, scale, z_index))


def write_manifest(path: Path, name: str, pack_dir: Path, size: tuple[int, int], placements: list[Placement]) -> None:
    manifest = {
        "name": name,
        "kind": "mountain_composite_prefab",
        "source_pack": str(pack_dir).replace("\\", "/"),
        "prefab_image": "prefab.png",
        "size": {"width": size[0], "height": size[1]},
        "placements": [
            {
                "role": item.role,
                "asset_id": item.asset_id,
                "file": item.file,
                "position": {"x": item.x, "y": item.y},
                "scale": item.scale,
                "z_index": item.z_index,
            }
            for item in placements
        ],
        "notes": [
            "Use this as a full mountain/island object.",
            "The generator composes atlas parts into layers; it is not a grid TileMap fill.",
        ],
    }
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def stable_hash(value: str, seed: int) -> int:
    h = seed & 0xFFFFFFFF
    for char in value:
        h = ((h * 16777619) ^ ord(char)) & 0xFFFFFFFF
    return h


if __name__ == "__main__":
    main()
