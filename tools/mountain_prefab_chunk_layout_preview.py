#!/usr/bin/env python3
"""Compose visible layout previews from a reference-style prefab chunk manifest."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


PRESET_OFFSETS: dict[str, dict[str, tuple[int, int]]] = {
    "reference": {},
    "compact": {
        "level_1_right_plateau_with_cliff": (-28, 4),
        "level_2_left_plateau_with_cliff": (22, 4),
        "route_0_to_1_ramp_with_wall": (-12, 2),
        "route_1_to_2_switchback_with_wall": (8, 0),
        "route_2_to_3_high_path_with_wall": (10, -4),
        "level_3_castle_floor_with_support": (12, -4),
    },
    "wide": {
        "level_0_base_with_front_cliff": (-18, 10),
        "level_1_right_plateau_with_cliff": (74, 10),
        "level_2_left_plateau_with_cliff": (-58, 0),
        "route_0_to_1_ramp_with_wall": (28, 8),
        "route_1_to_2_switchback_with_wall": (-20, 2),
        "route_2_to_3_high_path_with_wall": (-6, -4),
        "level_3_castle_floor_with_support": (12, -4),
    },
    "high_castle": {
        "level_2_left_plateau_with_cliff": (0, -10),
        "route_1_to_2_switchback_with_wall": (0, -8),
        "route_2_to_3_high_path_with_wall": (0, -42),
        "level_3_castle_floor_with_support": (0, -72),
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build visible previews from prefab chunks.")
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--preset", choices=sorted(PRESET_OFFSETS), default=None)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    manifest_path = args.manifest
    output_dir = args.output_dir or manifest_path.parent / "layout_previews"
    output_dir.mkdir(parents=True, exist_ok=True)

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    presets = [args.preset] if args.preset else list(PRESET_OFFSETS)
    for preset in presets:
        image = compose_preset(manifest_path.parent, manifest, preset)
        image.save(output_dir / f"{preset}.png")
        write_preview(output_dir / f"{preset}_preview.png", image, preset)
        print(f"Wrote {output_dir / f'{preset}.png'}")


def compose_preset(root: Path, manifest: dict, preset: str) -> Image.Image:
    assets = [
        asset
        for asset in manifest["assets"]
        if asset.get("category") != "complete_prefab"
    ]
    offsets = PRESET_OFFSETS[preset]
    bounds = []
    loaded: list[tuple[dict, Image.Image, tuple[int, int]]] = []
    for asset in assets:
        role = asset["role"]
        sprite = Image.open(root / asset["file"]).convert("RGBA")
        position = asset["default_position"]
        dx, dy = offsets.get(role, (0, 0))
        x = int(position["x"] + dx)
        y = int(position["y"] + dy)
        loaded.append((asset, sprite, (x, y)))
        bounds.append((x, y, x + sprite.width, y + sprite.height))

    min_x = min(x1 for x1, _, _, _ in bounds)
    min_y = min(y1 for _, y1, _, _ in bounds)
    max_x = max(x2 for _, _, x2, _ in bounds)
    max_y = max(y2 for _, _, _, y2 in bounds)
    pad = 20
    canvas = Image.new("RGBA", (max_x - min_x + pad * 2, max_y - min_y + pad * 2), (0, 0, 0, 0))

    def sort_key(item: tuple[dict, Image.Image, tuple[int, int]]) -> tuple[int, int]:
        asset, _, _ = item
        category = asset.get("category", "")
        z_category = 5 if category == "route_chunk" else 8 if category == "castle_chunk" else 0
        return int(asset.get("height_level", 0)) * 10 + z_category, int(asset.get("to_level") or -1)

    for asset, sprite, (x, y) in sorted(loaded, key=sort_key):
        canvas.alpha_composite(sprite, (x - min_x + pad, y - min_y + pad))

    return canvas


def write_preview(path: Path, image: Image.Image, preset: str) -> None:
    margin = 26
    preview = Image.new("RGBA", (image.width + margin * 2, image.height + margin * 2), (20, 35, 45, 255))
    preview.alpha_composite(image, (margin, margin))
    draw = ImageDraw.Draw(preview)
    draw.text((14, 10), f"REFERENCE-STYLE PREFAB CHUNKS - {preset.upper()}", fill=(230, 236, 230, 255))
    preview.convert("RGB").save(path)


if __name__ == "__main__":
    main()
