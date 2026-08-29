#!/usr/bin/env python3
"""Normalize a generated mountain atlas into a floor-first 17 piece atlas."""

from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont


FLOOR17_ROLES = (
    "floor_center",
    "floor_edge_n",
    "floor_edge_s",
    "floor_edge_w",
    "floor_edge_e",
    "floor_corner_nw",
    "floor_corner_ne",
    "floor_corner_sw",
    "floor_corner_se",
    "floor_edge_n_alt",
    "floor_edge_s_alt",
    "floor_edge_w_alt",
    "floor_edge_e_alt",
    "floor_corner_nw_alt",
    "floor_corner_ne_alt",
    "floor_corner_sw_alt",
    "floor_corner_se_alt",
)

CLIFF_ROLES = (
    "cliff_left",
    "cliff_middle_a",
    "cliff_middle_b",
    "cliff_right",
    "cliff_corner_left",
    "cliff_corner_right",
    "vertical_column",
)

ROUTE_ROLES = (
    "bottom_entry_ramp",
    "ramp_left_to_right",
    "ramp_right_to_left",
    "switchback_landing_left",
    "switchback_landing_right",
    "stairs_up",
    "top_landing",
)

CASTLE_ROLES = (
    "castle_floor_left",
    "castle_floor_middle",
    "castle_floor_right",
    "castle_foundation",
    "front_lip",
    "side_lip",
    "bridge_edge",
)

DETAIL_ROLES = (
    "dirt_path_overlay",
    "grass_patch",
    "cracks",
    "rocks",
    "boulder",
    "conifer_a",
    "conifer_b",
    "shadow",
)

EXTRA_ROLE_GROUPS = (
    ("route", ROUTE_ROLES, 0.55, 0.70),
    ("castle", CASTLE_ROLES, 0.70, 0.82),
    ("detail", DETAIL_ROLES, 0.82, 1.01),
)

DEFAULT_TEXTURE_DIR = Path(__file__).resolve().parents[2] / "Art" / "textures"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create a normalized first-row floor17 atlas.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--name", default="mountain_floor17_green")
    parser.add_argument("--slot-width", type=int, default=128)
    parser.add_argument("--slot-height", type=int, default=128)
    parser.add_argument("--extra-columns", type=int, default=8)
    parser.add_argument("--min-area", type=int, default=800)
    parser.add_argument("--padding", type=int, default=3)
    parser.add_argument("--texture", type=Path, default=None, help="Single material texture used for both floor17 tiles and wall extras.")
    parser.add_argument("--texture-cell", default=None, help="Optional atlas cell as COL,ROW using 1-based indexes, for example 3,2.")
    parser.add_argument("--surface-texture", type=Path, default=DEFAULT_TEXTURE_DIR / "Natural Grassy Meadow Texture.png")
    parser.add_argument("--cliff-texture", type=Path, default=DEFAULT_TEXTURE_DIR / "Cliff Texture Atlas 2.png")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    sprites_dir = args.output_dir / "sprites"
    sprites_dir.mkdir(exist_ok=True)

    source = clean_background(Image.open(args.input))
    source.save(args.output_dir / "source_clean.png")
    rects = components(source, args.min_area)
    floor_sprites = build_floor17_sprites(args, source, rects)
    cliff_sprites = build_cliff_sprites(args, source, rects)

    extra_specs: list[tuple[str, str, tuple[int, int, int, int, int]]] = []
    for category, roles, top, bottom in EXTRA_ROLE_GROUPS:
        band_rects = [rect for rect in rects if top <= center_y(rect, source.height) < bottom]
        for role, rect in zip(roles, sorted(band_rects, key=lambda item: item[0])):
            extra_specs.append((category, role, rect))

    floor_atlas = Image.new("RGBA", (len(FLOOR17_ROLES) * args.slot_width, args.slot_height), (0, 0, 0, 0))
    floor_assets: list[dict] = []

    for index, role in enumerate(FLOOR17_ROLES):
        add_sprite_slot(floor_atlas, sprites_dir, floor_assets, args.name, role, "floor17", floor_sprites[role], index, 0, args.slot_width, args.slot_height, args.padding, True, False, 20, "synthesized_floor17")

    extras_items: list[tuple[str, str, Image.Image | tuple[int, int, int, int, int]]] = [("cliff", role, cliff_sprites[role]) for role in CLIFF_ROLES]
    extras_items.extend((category, role, rect) for category, role, rect in extra_specs)
    extras_rows = max(1, (len(extras_items) + args.extra_columns - 1) // args.extra_columns)
    extras_atlas = Image.new("RGBA", (args.extra_columns * args.slot_width, extras_rows * args.slot_height), (0, 0, 0, 0))
    extras_assets: list[dict] = []

    for index, (category, role, sprite_or_rect) in enumerate(extras_items):
        col = index % args.extra_columns
        row = index // args.extra_columns
        walkable, climbable, z_index = classify_extra(role)
        if isinstance(sprite_or_rect, Image.Image):
            add_sprite_slot(extras_atlas, sprites_dir, extras_assets, args.name, role, category, sprite_or_rect, col, row, args.slot_width, args.slot_height, args.padding, walkable, climbable, z_index, "synthesized_cliff")
        else:
            add_slot(source, extras_atlas, sprites_dir, extras_assets, args.name, role, category, sprite_or_rect, col, row, args.slot_width, args.slot_height, args.padding, walkable, climbable, z_index)

    floor_atlas.save(args.output_dir / "floor17_atlas.png")
    extras_atlas.save(args.output_dir / "extras_atlas.png")
    floor_manifest = {
        "name": args.name,
        "kind": "mountain_floor17_level_atlas",
        "source_atlas": "floor17_atlas.png",
        "tile_width": args.slot_width,
        "tile_height": args.slot_height,
        "material_texture": str((args.texture or args.surface_texture)).replace("\\", "/"),
        "floor17_roles": list(FLOOR17_ROLES),
        "assets": floor_assets,
        "generator_contract": {
            "first_row": "Exactly 17 walkable floor pieces, in floor17_roles order: one plain center, four edges, four corners, and eight alternates.",
            "draw_level": "Draw each level as a width x depth rectangle.",
            "center_fill": "Repeat floor_center inside the rectangle.",
            "borders": "Use north/south/east/west edge tiles around the center fill.",
            "corners": "Use corner tiles at the four rectangle corners.",
            "alternates": "Use *_alt roles only for variation; the primary center/edge/corner roles are sufficient.",
            "single_material": "When --texture is supplied, the 17 base tiles and wall extras are sampled from that same texture.",
            "extras": "Paths, cliffs, props, and castle support sprites live in extras_atlas.png and extras_manifest.json.",
        },
    }
    extras_manifest = {
        "name": f"{args.name}_extras",
        "kind": "mountain_floor17_extras_atlas",
        "source_atlas": "extras_atlas.png",
        "tile_width": args.slot_width,
        "tile_height": args.slot_height,
        "material_texture": str((args.texture or args.cliff_texture)).replace("\\", "/"),
        "assets": extras_assets,
        "categories": {
            "cliff": list(CLIFF_ROLES),
            "route": list(ROUTE_ROLES),
            "castle": list(CASTLE_ROLES),
            "detail": list(DETAIL_ROLES),
        },
    }
    pack_manifest = {
        "name": f"{args.name}_pack",
        "kind": "mountain_floor17_split_pack",
        "floor17_manifest": "floor17_manifest.json",
        "extras_manifest": "extras_manifest.json",
        "contract": floor_manifest["generator_contract"],
    }
    (args.output_dir / "floor17_manifest.json").write_text(json.dumps(floor_manifest, indent=2), encoding="utf-8")
    (args.output_dir / "extras_manifest.json").write_text(json.dumps(extras_manifest, indent=2), encoding="utf-8")
    (args.output_dir / "pack_manifest.json").write_text(json.dumps(pack_manifest, indent=2), encoding="utf-8")
    write_preview(args.output_dir / "floor17_preview.png", floor_atlas, floor_assets, args.slot_width, args.slot_height, "floor17_atlas.png: only the 17 base floor tiles")
    write_preview(args.output_dir / "extras_preview.png", extras_atlas, extras_assets, args.slot_width, args.slot_height, "extras_atlas.png: cliffs, paths, castle, detail sprites")
    print(f"Wrote atlas: {args.output_dir / 'floor17_atlas.png'}")
    print(f"Wrote extras: {args.output_dir / 'extras_atlas.png'}")
    print(f"Wrote manifest: {args.output_dir / 'floor17_manifest.json'}")
    print(f"Wrote extras manifest: {args.output_dir / 'extras_manifest.json'}")
    print(f"Wrote preview: {args.output_dir / 'floor17_preview.png'}")
    print(f"Wrote extras preview: {args.output_dir / 'extras_preview.png'}")
    print(f"Wrote floor assets: {len(floor_assets)}")
    print(f"Wrote extra assets: {len(extras_assets)}")


def build_floor17_sprites(args: argparse.Namespace, source: Image.Image, rects: list[tuple[int, int, int, int, int]]) -> dict[str, Image.Image]:
    if args.texture:
        material = load_texture(args.texture)
        surface = pick_material_patch(material) if material else fallback_floor_texture(source, rects)
    else:
        surface = load_texture(args.surface_texture) or fallback_floor_texture(source, rects)
    roles: dict[str, Image.Image] = {}
    for index, role in enumerate(FLOOR17_ROLES):
        roles[role] = make_floor_tile(role, surface, args.slot_width, args.slot_height, index)
    return roles


def build_cliff_sprites(args: argparse.Namespace, source: Image.Image, rects: list[tuple[int, int, int, int, int]]) -> dict[str, Image.Image]:
    cliff = load_texture(args.texture or args.cliff_texture)
    rock = pick_material_patch(cliff) if cliff else fallback_cliff_texture(source, rects)
    roles: dict[str, Image.Image] = {}
    for index, role in enumerate(CLIFF_ROLES):
        roles[role] = make_cliff_tile(role, rock, args.slot_width, args.slot_height, index)
    return roles


def load_texture(path: Path | None) -> Image.Image | None:
    if not path or not path.exists():
        return None
    return Image.open(path).convert("RGBA")


def fallback_floor_texture(source: Image.Image, rects: list[tuple[int, int, int, int, int]]) -> Image.Image:
    floor_rects = [rect for rect in rects if center_y(rect, source.height) < 0.36]
    if floor_rects:
        x1, y1, x2, y2, _ = floor_rects[0]
        return source.crop((x1, y1, x2, y2)).convert("RGBA")
    return source.convert("RGBA")


def fallback_cliff_texture(source: Image.Image, rects: list[tuple[int, int, int, int, int]]) -> Image.Image:
    cliff_rects = [rect for rect in rects if 0.36 <= center_y(rect, source.height) < 0.55]
    if cliff_rects:
        x1, y1, x2, y2, _ = cliff_rects[0]
        return source.crop((x1, y1, x2, y2)).convert("RGBA")
    return source.convert("RGBA")


def pick_cliff_patch(atlas: Image.Image) -> Image.Image:
    grid_patches = atlas_grid_patches(atlas)
    if grid_patches:
        return max(grid_patches, key=score_cliff_patch)

    rects = opaque_components(atlas, 10_000)
    if not rects:
        return atlas

    best_rect = rects[0]
    best_score = -1_000_000.0
    for rect in rects:
        x1, y1, x2, y2, _ = rect
        patch = atlas.crop((x1, y1, x2, y2)).convert("RGBA")
        score = score_cliff_patch(patch) + rect[4] / 6000.0
        if score > best_score:
            best_score = score
            best_rect = rect

    x1, y1, x2, y2, _ = best_rect
    return atlas.crop((x1, y1, x2, y2)).convert("RGBA")


def pick_material_patch(texture: Image.Image) -> Image.Image:
    # Kept for callers that do not have argparse available.
    grid_patches = atlas_grid_patches(texture)
    if grid_patches:
        return max(grid_patches, key=score_cliff_patch)
    return pick_cliff_patch(texture)


def pick_material_patch_for_args(texture: Image.Image, args: argparse.Namespace) -> Image.Image:
    grid_patches = atlas_grid_patches(texture)
    if args.texture_cell and grid_patches:
        col_text, row_text = args.texture_cell.split(",", 1)
        col = max(1, int(col_text.strip()))
        row = max(1, int(row_text.strip()))
        index = (row - 1) * 4 + (col - 1)
        if 0 <= index < len(grid_patches):
            return grid_patches[index]
    if grid_patches:
        return max(grid_patches, key=score_cliff_patch)
    return pick_cliff_patch(texture)


def atlas_grid_patches(atlas: Image.Image) -> list[Image.Image]:
    width, height = atlas.size
    candidates: list[tuple[int, int]] = []
    if width % 4 == 0 and height % 4 == 0:
        candidates.append((4, 4))
    if width % 3 == 0 and height % 4 == 0:
        candidates.append((3, 4))
    if width % 4 == 0 and height % 3 == 0:
        candidates.append((4, 3))

    patches: list[Image.Image] = []
    for columns, rows in candidates:
        cell_w = width // columns
        cell_h = height // rows
        if cell_w < 96 or cell_h < 96:
            continue
        for row in range(rows):
            for col in range(columns):
                patch = atlas.crop((col * cell_w, row * cell_h, (col + 1) * cell_w, (row + 1) * cell_h)).convert("RGBA")
                patches.append(crop_non_background(patch))
        if patches:
            return patches
    return patches


def crop_non_background(image: Image.Image) -> Image.Image:
    small = image.resize((32, 32), Image.Resampling.BILINEAR)
    samples = list(small.getdata())
    corners = [samples[0], samples[31], samples[-32], samples[-1]]
    bg = tuple(round(sum(pixel[index] for pixel in corners) / len(corners)) for index in range(3))
    pixels = image.load()
    width, height = image.size
    min_x, min_y = width, height
    max_x, max_y = -1, -1
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            color_distance = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
            if a > 8 and color_distance > 42:
                min_x = min(min_x, x)
                min_y = min(min_y, y)
                max_x = max(max_x, x)
                max_y = max(max_y, y)
    if max_x < min_x or max_y < min_y:
        return image
    pad = 4
    return image.crop((max(0, min_x - pad), max(0, min_y - pad), min(width, max_x + pad + 1), min(height, max_y + pad + 1)))


def score_cliff_patch(patch: Image.Image) -> float:
    pixels = [pixel for pixel in patch.resize((24, 24), Image.Resampling.BILINEAR).getdata() if pixel[3] > 20 and max(pixel[:3]) > 25]
    if not pixels:
        return -1_000_000.0
    avg_r = sum(pixel[0] for pixel in pixels) / len(pixels)
    avg_g = sum(pixel[1] for pixel in pixels) / len(pixels)
    avg_b = sum(pixel[2] for pixel in pixels) / len(pixels)
    green_moss = max(0.0, avg_g - avg_r * 0.65 - avg_b * 0.35)
    neutral_rock = 120.0 - abs(avg_r - avg_g) - abs(avg_g - avg_b)
    too_bright = max(0.0, (avg_r + avg_g + avg_b) / 3.0 - 185.0)
    too_warm = max(0.0, avg_r - avg_b - 42.0)
    return green_moss * 2.4 + neutral_rock - too_bright * 1.6 - too_warm * 2.0


def opaque_components(image: Image.Image, min_area: int) -> list[tuple[int, int, int, int, int]]:
    image = image.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    seen: set[tuple[int, int]] = set()
    found: list[tuple[int, int, int, int, int]] = []
    for y in range(height):
        for x in range(width):
            if (x, y) in seen:
                continue
            r, g, b, a = pixels[x, y]
            if a <= 8 or max(r, g, b) <= 24:
                continue
            queue = deque([(x, y)])
            seen.add((x, y))
            min_x = max_x = x
            min_y = max_y = y
            area = 0
            while queue:
                cx, cy = queue.popleft()
                area += 1
                min_x = min(min_x, cx)
                max_x = max(max_x, cx)
                min_y = min(min_y, cy)
                max_y = max(max_y, cy)
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in seen:
                        continue
                    nr, ng, nb, na = pixels[nx, ny]
                    if na <= 8 or max(nr, ng, nb) <= 24:
                        continue
                    seen.add((nx, ny))
                    queue.append((nx, ny))
            if area >= min_area:
                found.append((min_x, min_y, max_x + 1, max_y + 1, area))
    return sorted(found, key=lambda item: (item[1], item[0]))


def make_floor_tile(role: str, surface: Image.Image, width: int, height: int, variant: int) -> Image.Image:
    base = sample_texture(surface, width, height, variant * 37, variant * 53)
    base = ImageEnhance.Contrast(base).enhance(1.08)
    base = ImageEnhance.Color(base).enhance(1.04)
    tile = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    tile.alpha_composite(base)

    edges = role_edges(role)
    draw = ImageDraw.Draw(tile, "RGBA")
    if edges:
        if "n" in edges:
            draw.line((0, 1, width, 1), fill=(18, 22, 18, 55), width=2)
        if "s" in edges:
            draw.line((0, height - 2, width, height - 2), fill=(18, 22, 18, 70), width=3)
        if "w" in edges:
            draw.line((1, 0, 1, height), fill=(18, 22, 18, 55), width=2)
        if "e" in edges:
            draw.line((width - 2, 0, width - 2, height), fill=(18, 22, 18, 55), width=2)
    return tile


def role_edges(role: str) -> set[str]:
    clean = role.removesuffix("_alt")
    if clean == "floor_edge_n":
        return {"n"}
    if clean == "floor_edge_s":
        return {"s"}
    if clean == "floor_edge_w":
        return {"w"}
    if clean == "floor_edge_e":
        return {"e"}
    if clean == "floor_corner_nw":
        return {"n", "w"}
    if clean == "floor_corner_ne":
        return {"n", "e"}
    if clean == "floor_corner_sw":
        return {"s", "w"}
    if clean == "floor_corner_se":
        return {"s", "e"}
    return set()


def overlay_edge(tile: Image.Image, rock: Image.Image, edge: str, variant: int) -> None:
    width, height = tile.size
    horizontal = edge in {"n", "s"}
    thickness = 24 if edge != "s" else 34
    length = width if horizontal else height
    strip = sample_texture(rock, length, thickness, variant * 29 + thickness, variant * 17 + length)
    strip = ImageEnhance.Contrast(strip).enhance(1.16)
    strip = ImageEnhance.Brightness(strip).enhance(0.92 if edge == "s" else 0.98)

    mask = Image.new("L", strip.size, 0)
    draw = ImageDraw.Draw(mask)
    if horizontal:
        for y in range(thickness):
            edge_alpha = 205 if edge == "s" else 150
            fade = int(edge_alpha * (1.0 - y / max(1, thickness - 1)))
            if edge == "n":
                draw.line((0, y, length, y), fill=fade)
            else:
                draw.line((0, thickness - 1 - y, length, thickness - 1 - y), fill=fade)
        if edge == "n":
            blend_with_mask(tile, strip, mask, 0, 0)
        else:
            blend_with_mask(tile, strip, mask, 0, height - thickness)
    else:
        strip = strip.rotate(90, expand=True).resize((thickness, height), Image.Resampling.BILINEAR)
        mask = Image.new("L", (thickness, height), 0)
        draw = ImageDraw.Draw(mask)
        for x in range(thickness):
            fade = int(150 * (1.0 - x / max(1, thickness - 1)))
            if edge == "w":
                draw.line((x, 0, x, height), fill=fade)
            else:
                draw.line((thickness - 1 - x, 0, thickness - 1 - x, height), fill=fade)
        blend_with_mask(tile, strip, mask, 0 if edge == "w" else width - thickness, 0)


def blend_with_mask(tile: Image.Image, overlay: Image.Image, mask: Image.Image, x: int, y: int) -> None:
    region = tile.crop((x, y, x + overlay.width, y + overlay.height))
    region.alpha_composite(Image.composite(overlay, Image.new("RGBA", overlay.size, (0, 0, 0, 0)), mask))
    tile.paste(region, (x, y))


def make_cliff_tile(role: str, rock: Image.Image, width: int, height: int, variant: int) -> Image.Image:
    tile = sample_texture(rock, width, height, variant * 41, variant * 23)
    tile = ImageEnhance.Contrast(tile).enhance(1.14)
    draw = ImageDraw.Draw(tile, "RGBA")
    draw.rectangle((0, 0, width - 1, height - 1), outline=(26, 30, 28, 130), width=2)
    if role in {"cliff_left", "cliff_corner_left"}:
        draw.rectangle((0, 0, 15, height), fill=(0, 0, 0, 55))
    if role in {"cliff_right", "cliff_corner_right"}:
        draw.rectangle((width - 16, 0, width, height), fill=(0, 0, 0, 55))
    if role == "vertical_column":
        mask = Image.new("L", (width, height), 0)
        ImageDraw.Draw(mask).rounded_rectangle((7, 2, width - 8, height - 3), radius=18, fill=255)
        tile.putalpha(mask)
    return tile


def sample_texture(texture: Image.Image, width: int, height: int, offset_x: int = 0, offset_y: int = 0) -> Image.Image:
    texture = texture.convert("RGBA")
    if texture.width == 0 or texture.height == 0:
        return Image.new("RGBA", (width, height), (120, 130, 85, 255))
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for y in range(-offset_y % texture.height - texture.height, height, texture.height):
        for x in range(-offset_x % texture.width - texture.width, width, texture.width):
            canvas.alpha_composite(texture, (x, y))
    return canvas.crop((0, 0, width, height)).filter(ImageFilter.GaussianBlur(0.15))


def add_sprite_slot(
    atlas: Image.Image,
    sprites_dir: Path,
    assets: list[dict],
    name: str,
    role: str,
    category: str,
    sprite: Image.Image,
    col: int,
    row: int,
    slot_w: int,
    slot_h: int,
    padding: int,
    walkable: bool,
    climbable: bool,
    z_index: int,
    source: str,
) -> None:
    sprite = sprite.convert("RGBA")
    if sprite.width > slot_w or sprite.height > slot_h:
        sprite.thumbnail((slot_w - padding * 2, slot_h - padding * 2), Image.Resampling.LANCZOS)
    slot_x = col * slot_w
    slot_y = row * slot_h
    dx = slot_x + (slot_w - sprite.width) // 2
    dy = slot_y + (slot_h - sprite.height) // 2
    atlas.alpha_composite(sprite, (dx, dy))

    file_name = f"{name}_{role}.png"
    sprite.save(sprites_dir / file_name)
    assets.append(
        {
            "id": f"{name}_{role}",
            "role": role,
            "category": category,
            "file": f"sprites/{file_name}",
            "atlas": {"x": col, "y": row},
            "tile_width": slot_w,
            "tile_height": slot_h,
            "source_rect": source,
            "sprite_size": {"width": sprite.width, "height": sprite.height},
            "alpha_area": count_alpha(sprite),
            "walkable": walkable,
            "climbable": climbable,
            "suggested_z_index": z_index,
        }
    )


def count_alpha(image: Image.Image) -> int:
    return sum(1 for alpha in image.getchannel("A").getdata() if alpha > 8)


def add_slot(
    source: Image.Image,
    atlas: Image.Image,
    sprites_dir: Path,
    assets: list[dict],
    name: str,
    role: str,
    category: str,
    rect: tuple[int, int, int, int, int],
    col: int,
    row: int,
    slot_w: int,
    slot_h: int,
    padding: int,
    walkable: bool,
    climbable: bool,
    z_index: int,
) -> None:
    x1, y1, x2, y2, area = rect
    sprite = source.crop((x1, y1, x2, y2))
    bbox = sprite.getchannel("A").getbbox()
    if bbox:
        sprite = sprite.crop(bbox)
    sprite.thumbnail((slot_w - padding * 2, slot_h - padding * 2), Image.Resampling.LANCZOS)
    slot_x = col * slot_w
    slot_y = row * slot_h
    dx = slot_x + (slot_w - sprite.width) // 2
    dy = slot_y + max(padding, slot_h - sprite.height - padding)
    atlas.alpha_composite(sprite, (dx, dy))

    file_name = f"{name}_{role}.png"
    sprite.save(sprites_dir / file_name)
    assets.append(
        {
            "id": f"{name}_{role}",
            "role": role,
            "category": category,
            "file": f"sprites/{file_name}",
            "atlas": {"x": col, "y": row},
            "tile_width": slot_w,
            "tile_height": slot_h,
            "source_rect": {"x": x1, "y": y1, "width": x2 - x1, "height": y2 - y1},
            "sprite_size": {"width": sprite.width, "height": sprite.height},
            "alpha_area": area,
            "walkable": walkable,
            "climbable": climbable,
            "suggested_z_index": z_index,
        }
    )


def classify_extra(role: str) -> tuple[bool, bool, int]:
    if role.startswith("cliff") or role == "vertical_column":
        return False, False, 0
    if "ramp" in role or "landing" in role or role == "stairs_up" or role == "top_landing":
        return True, True, 30
    if role.startswith("castle") or role in {"front_lip", "side_lip", "bridge_edge"}:
        return True, False, 24
    return False, False, 40


def center_y(rect: tuple[int, int, int, int, int], image_height: int) -> float:
    _, y1, _, y2, _ = rect
    return (y1 + y2) * 0.5 / image_height


def clean_background(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    width, height = image.size
    pixels = image.load()
    queue: deque[tuple[int, int]] = deque()
    seen: set[tuple[int, int]] = set()
    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))
    while queue:
        x, y = queue.popleft()
        if x < 0 or y < 0 or x >= width or y >= height or (x, y) in seen:
            continue
        r, g, b, a = pixels[x, y]
        if a == 0 or (max(r, g, b) >= 226 and max(r, g, b) - min(r, g, b) <= 12):
            pixels[x, y] = (r, g, b, 0)
            seen.add((x, y))
            queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return image


def components(image: Image.Image, min_area: int) -> list[tuple[int, int, int, int, int]]:
    alpha = image.getchannel("A")
    pixels = alpha.load()
    width, height = image.size
    seen: set[tuple[int, int]] = set()
    found: list[tuple[int, int, int, int, int]] = []
    for y in range(height):
        for x in range(width):
            if (x, y) in seen or pixels[x, y] <= 8:
                continue
            queue = deque([(x, y)])
            seen.add((x, y))
            min_x = max_x = x
            min_y = max_y = y
            area = 0
            while queue:
                cx, cy = queue.popleft()
                area += 1
                min_x = min(min_x, cx)
                max_x = max(max_x, cx)
                min_y = min(min_y, cy)
                max_y = max(max_y, cy)
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in seen:
                        continue
                    if pixels[nx, ny] <= 8:
                        continue
                    seen.add((nx, ny))
                    queue.append((nx, ny))
            if area >= min_area:
                found.append((min_x, min_y, max_x + 1, max_y + 1, area))
    return sorted(found, key=lambda item: (item[1], item[0]))


def write_preview(path: Path, atlas: Image.Image, assets: list[dict], slot_w: int, slot_h: int, footer: str) -> None:
    font = ImageFont.load_default()
    preview = Image.new("RGBA", (atlas.width, atlas.height + 22), (29, 32, 31, 255))
    preview.alpha_composite(atlas, (0, 0))
    draw = ImageDraw.Draw(preview)
    for asset in assets:
        col = asset["atlas"]["x"]
        row = asset["atlas"]["y"]
        x = col * slot_w
        y = row * slot_h
        draw.rectangle((x, y, x + slot_w - 1, y + slot_h - 1), outline=(64, 70, 66, 255))
        if row == 0:
            draw.text((x + 4, y + 4), str(col + 1), fill=(230, 234, 229, 255), font=font)
    draw.text((8, atlas.height + 5), footer, fill=(235, 238, 234, 255), font=font)
    preview.convert("RGB").save(path)


if __name__ == "__main__":
    main()
