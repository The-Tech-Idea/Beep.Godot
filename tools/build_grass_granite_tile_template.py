"""Pack the approved green-background artwork into a connected 64px tile set.

The generated sheet is a source template, not an evenly spaced atlas. Region
measurements below are explicit; no generated object is treated as a whole map.
"""

from __future__ import annotations

import json
import argparse
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "addons/beep_game_builder_cs/generated/terrain_templates/grass_granite_tile_template_v1"
TILE = 64
# E, S, W, N, NE, SE, SW, NW. Diagonals exist only between occupied sides.
OFFSETS = [(1, 0), (0, 1), (-1, 0), (0, -1), (1, -1), (1, 1), (-1, 1), (-1, -1)]


def canonical(mask: int) -> int:
    for diagonal, a, b in [(4, 0, 3), (5, 0, 1), (6, 1, 2), (7, 2, 3)]:
        if not (mask & (1 << a) and mask & (1 << b)):
            mask &= ~(1 << diagonal)
    return mask


MASKS = sorted({canonical(mask) for mask in range(256)})


def key_background(image: Image.Image) -> Image.Image:
    a = np.array(image.convert("RGBA"), dtype=np.float32)
    excess = a[:, :, 1] - np.maximum(a[:, :, 0], a[:, :, 2])
    # The generated green is near (4,245,5), not exactly #00ff00. Olive grass
    # has low green excess, so chroma distance preserves its opaque pixels.
    alpha = np.clip((205 - excess) / 140, 0, 1)
    a[:, :, 3] = np.minimum(a[:, :, 3], alpha * 255)
    edge = alpha < 1
    a[:, :, 1][edge] = np.minimum(a[:, :, 1][edge], np.maximum(a[:, :, 0][edge], a[:, :, 2][edge]) * 1.1)
    a[a[:, :, 3] < 2] = 0
    return Image.fromarray(a.astype(np.uint8))


def region(image: Image.Image, box: tuple, size: tuple) -> Image.Image:
    return image.crop(box).resize(size, Image.Resampling.LANCZOS)


def build_tops(master: Image.Image) -> list[np.ndarray]:
    tiles = []
    for mask in MASKS:
        tile = Image.new("RGBA", (64, 64))
        for qx, qy, sx, sy, diagonal in [(0, 0, 2, 3, 7), (1, 0, 0, 3, 4), (0, 1, 2, 1, 6), (1, 1, 0, 1, 5)]:
            x_inside, y_inside = bool(mask & (1 << sx)), bool(mask & (1 << sy))
            col = 1 if x_inside else qx * 2
            row = 1 if y_inside else qy * 2
            x, y = col * 64 + qx * 32, row * 64 + qy * 32
            quarter = master.crop((x, y, x + 32, y + 32))
            if x_inside and y_inside and not mask & (1 << diagonal):
                # Concave micro-rim uses the same corner stones, not a new
                # material or a full outer-corner hole in a filled tile.
                rim = master.crop((qx * 128 + qx * 32, qy * 128 + qy * 32,
                                   qx * 128 + qx * 32 + 32, qy * 128 + qy * 32 + 32))
                rim = rim.transpose(Image.Transpose.ROTATE_180).resize((10, 10), Image.Resampling.LANCZOS)
                quarter.alpha_composite(rim, (22 if qx else 0, 22 if qy else 0))
            tile.paste(quarter, (qx * 32, qy * 32))
        tiles.append(np.array(tile))
    return tiles


def seam_groups() -> list[list[tuple[int, int, int]]]:
    groups = {}
    for i, mask in enumerate(MASKS):
        bit = lambda b: (mask >> b) & 1
        for side, inside, key in [
            ("e", bit(0), (bit(3), bit(4), bit(1), bit(5))),
            ("w", bit(2), (bit(7), bit(3), bit(6), bit(1))),
            ("n", bit(3), (bit(7), bit(2), bit(4), bit(0))),
            ("s", bit(1), (bit(2), bit(6), bit(0), bit(5))),
        ]:
            if not inside:
                continue
            axis = "v" if side in ("e", "w") else "h"
            for t in range(64):
                x, y = {"e": (63, t), "w": (0, t), "n": (t, 0), "s": (t, 63)}[side]
                groups.setdefault((axis, key, t), []).append((i, y, x))
    return list(groups.values())


def match_seams(tiles: list[np.ndarray]) -> None:
    # Union boundary constraints, including shared tile vertices, so fixing
    # horizontal joins cannot subsequently break vertical joins.
    parent = {}

    def find(p):
        parent.setdefault(p, p)
        if parent[p] != p:
            parent[p] = find(parent[p])
        return parent[p]

    groups = seam_groups()
    for members in groups:
        first = find(members[0])
        for p in members[1:]:
            parent[find(p)] = first
    merged = {}
    for p in parent:
        merged.setdefault(find(p), []).append(p)
    for members in merged.values():
        colors = np.array([tiles[i][y, x] for i, y, x in members], dtype=float)
        alpha = colors[:, 3].mean()
        rgb = (colors[:, :3] * colors[:, 3:4]).sum(axis=0) / max(1, colors[:, 3].sum())
        color = np.rint([*rgb, alpha]).astype(np.uint8)
        for i, y, x in members:
            tiles[i][y, x] = color


def build_natural_walls() -> tuple[Image.Image, list[np.ndarray]]:
    source = key_background(Image.open(OUT / "source_cliff_natural_green.png"))
    # Preserve the approved rock proportions: six 64x80 pieces, rather than
    # squeezing the broad strip into a single square and repeating it.
    strip = np.array(region(source, (85, 308, 1695, 666), (384, 80)))
    for x in range(384):
        visible = np.flatnonzero(strip[:, x, 3] > 128)
        if visible.size:
            strip[:visible[0], x] = strip[visible[0], x]
    # Preserve contiguous source joins. Only reconcile the strip wrap seam.
    edge = ((strip[:,0].astype(float) + strip[:,-1]) / 2).astype(np.uint8)
    for d in range(4):
        weight = (4-d)/4
        strip[:,d] = (strip[:,d]*(1-weight) + edge*weight).astype(np.uint8)
        strip[:,-1-d] = (strip[:,-1-d]*(1-weight) + edge*weight).astype(np.uint8)
    # Exact adjoining pixels remove single-pixel filtering cracks; the
    # interiors retain their distinct arrangements, in source order.
    for x in range(64,384,64):
        seam = ((strip[:,x-1].astype(float)+strip[:,x])/2).astype(np.uint8)
        strip[:,x-1] = seam
        strip[:,x] = seam
    atlas = Image.new("RGBA", (1536,160))
    samples = []
    for row in range(2):
        for phase in range(6):
            base = strip[:,phase*64:(phase+1)*64].copy()
            if row == 0:
                # Middle rows overlap by 16px on the 64px map grid. Their
                # lower margin is buried behind the following wall row.
                for x in range(64):
                    visible = np.flatnonzero(base[:,x,3] > 128)
                    if visible.size:
                        base[visible[-1]:,x] = base[visible[-1],x]
            for cap in range(4):
                tile = base.copy()
                if cap in (1,3):
                    tile[:,0,3] = 0
                    tile[:,1,3] //= 2
                if cap in (2,3):
                    tile[:,-1,3] = 0
                    tile[:,-2,3] //= 2
                atlas.paste(Image.fromarray(tile), ((phase*4+cap)*64,row*80))
            if row == 1:
                samples.append(base)
    return atlas, samples


def run() -> None:
    original = Image.open(OUT / "source_sheet_green.png")
    if original.size != (1536, 1024):
        raise ValueError("Source measurements require the approved 1536x1024 sheet")
    keyed = key_background(original)
    master = region(keyed, (4, 3, 490, 468), (192, 192))
    tops = build_tops(master)
    match_seams(tops)
    atlas = Image.new("RGBA", (512, 384))
    for i, tile in enumerate(tops):
        atlas.paste(Image.fromarray(tile), (i % 8 * 64, i // 8 * 64))
    atlas.save(OUT / "terrain_tiles.png")
    walls_atlas, wall_samples = build_natural_walls()
    walls_atlas.save(OUT / "cliff_tiles.png")
    # Keep the artist-facing reference arrangement as well as Godot's
    # expanded terrain palette. All regions below lie on the same 64px grid.
    compact = Image.new("RGBA", (576, 448))
    cardinal_coords = [(3,3),(0,3),(3,0),(0,0),(2,3),(1,3),(2,0),(1,0),
                       (3,2),(0,2),(3,1),(0,1),(2,2),(1,2),(2,1),(1,1)]
    for mask, coord in enumerate(cardinal_coords):
        full = canonical(mask | 240)
        tile = Image.fromarray(tops[MASKS.index(full)])
        for offset in (0, 320):
            compact.paste(tile, (coord[0] * 64 + offset, coord[1] * 64))
    for i, wall in enumerate(wall_samples):
        compact.paste(Image.fromarray(wall), (320 + i % 3 * 64, 256 + i // 3 * 96))
    compact.paste(Image.fromarray(wall_samples[0]), (512, 256))
    compact.paste(Image.fromarray(wall_samples[3]), (512, 352))
    compact.paste(region(keyed, (4, 624, 173, 960), (64,128)), (0,256))
    compact.paste(region(keyed, (492,624,657,960), (64,128)), (192,256))
    compact.save(OUT / "compact_elevation_template.png")
    green = Image.new("RGBA", compact.size, (0,255,0,255))
    green.alpha_composite(compact)
    green.convert("RGB").save(OUT / "compact_elevation_template_green.png")
    manifest = {
        "tile_size": 64, "terrain_mode": "corners_and_sides", "masks": MASKS,
        "mask_bit_offsets": OFFSETS,
        "terrain_tiles": [{"mask":m,"atlas":[i%8,i//8]} for i,m in enumerate(MASKS)],
        "walls": {"phases":6,"region_size":[64,80],"caps":["none","left","right","isolated"],
                  "rows":["middle","bottom"],"ordering":"atlas_x = phase * 4 + cap; phase = map_x modulo 6"},
        "source_region_top": [4,3,490,468],
        "notes": "47 connectivity combinations assembled from the compact template. Not 47 separate material variants."
    }
    (OUT / "tile_manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    validate(tops)
    print(f"Packed {len(tops)} terrain configurations and 48 natural cliff tiles: {OUT}")


def validate(tops: list[np.ndarray]) -> None:
    assert len(MASKS) == 47
    for members in seam_groups():
        pixels = [tops[i][y,x] for i,y,x in members]
        assert all(np.array_equal(pixels[0], p) for p in pixels[1:]), "Connected tile seam differs"
    assert np.min(tops[MASKS.index(255)][:,:,3]) == 255, "Plain center has a hole"
    for tile in tops:
        visible = tile[:,:,3] > 128
        rgb = tile[:,:,:3].astype(int)
        assert not np.any(visible & (rgb[:,:,1] - np.maximum(rgb[:,:,0],rgb[:,:,2]) > 150)), "Chroma spill"


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, default=OUT)
    args = parser.parse_args()
    OUT = args.output_dir.resolve()
    run()
