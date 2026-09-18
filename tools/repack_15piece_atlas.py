"""Put a 15-piece dual-grid atlas into the order this engine's tile view expects.

Usage:
    python tools/repack_15piece_atlas.py SOURCE.png OUTPUT.png

A dual-grid display cell straddles four terrain cells, and TerrainTransitionLayerComponent selects a
tile by a four-bit mask - top-left 1, top-right 2, bottom-left 4, bottom-right 8, set where that
corner is the terrain the layer paints. Which atlas cell each mask uses is fixed by
`CanonicalMaskToAtlasIndex` in that component, and a sheet drawn in any other order draws a correct
map out of the wrong pieces: every coast comes out inside out, and nothing reports it, because every
cell is a legitimate tile.

So this does not trust a naming convention or a README. It READS each cell - sampling the four
quadrant centres and asking which are water - and rebuilds the sheet with each mask's art in the cell
the engine will look for it in. Verified against the engine's own water_15piece.png, whose sixteen
cells read back as exactly the sixteen masks its table claims.

THE TWO DIAGONALS. A 16-cell sheet has room for all sixteen cases, but a set drawn by hand often
spends two of them on water variants instead, leaving the diagonals - water at top-right AND
bottom-left, or top-left AND bottom-right - undrawn. They are rare on a map and never absent from
one. Where they are missing, this composites each from the two single-corner tiles that make it up,
taking the wetter of the two pixels, so the join is the artist's own shoreline rather than invented.
"""
import sys

from PIL import Image

# TerrainTransitionLayerComponent.CanonicalMaskToAtlasIndex, mask -> cell.
CANONICAL = [12, 15, 8, 9, 0, 11, 14, 7, 13, 4, 1, 10, 3, 2, 5, 6]
CORNERS = [(1, 0.28, 0.28), (2, 0.72, 0.28), (4, 0.28, 0.72), (8, 0.72, 0.72)]
COLUMNS = ROWS = 4


def wetness(pixel) -> float:
    """
    How much this pixel reads as water rather than land. Transparent is not water.

    Blue against GREEN, not against the strongest other channel. Measured against max(r, g), the
    shallow-water cyan in these sheets - (43, 203, 226) - scores 23, a hair over any sane threshold,
    so two of its corners read as land and the whole tile was mistaken for a diagonal case. Land here
    is green and sand is warm; both sit far below their own blue, so one comparison separates them.
    """
    if len(pixel) > 3 and pixel[3] < 128:
        return -1000.0
    return float(pixel[2]) - float(pixel[1])


def cell_box(index: int, cell_w: int, cell_h: int) -> tuple[int, int, int, int]:
    x, y = (index % COLUMNS) * cell_w, (index // COLUMNS) * cell_h
    return x, y, x + cell_w, y + cell_h


def read_masks(image: Image.Image, cell_w: int, cell_h: int) -> dict[int, int]:
    pixels = image.load()
    masks = {}
    for index in range(COLUMNS * ROWS):
        x, y, _, _ = cell_box(index, cell_w, cell_h)
        mask = 0
        for bit, fx, fy in CORNERS:
            if wetness(pixels[x + int(cell_w * fx), y + int(cell_h * fy)]) > 10:
                mask |= bit
        masks[index] = mask
    return masks


def wetter_of(a: Image.Image, b: Image.Image) -> Image.Image:
    """Per pixel, whichever of the two tiles is more water. Used to build a missing diagonal."""
    out = Image.new(a.mode, a.size)
    a_pixels, b_pixels, out_pixels = a.load(), b.load(), out.load()
    for y in range(a.size[1]):
        for x in range(a.size[0]):
            first, second = a_pixels[x, y], b_pixels[x, y]
            out_pixels[x, y] = first if wetness(first) >= wetness(second) else second
    return out


def main(source_path: str, output_path: str) -> None:
    source = Image.open(source_path)
    image = source.convert("RGBA") if source.mode in ("RGBA", "LA", "P") else source.convert("RGB")
    width, height = image.size
    cell_w, cell_h = width // COLUMNS, height // ROWS
    masks = read_masks(image, cell_w, cell_h)
    by_mask: dict[int, int] = {}
    for index, mask in masks.items():
        by_mask.setdefault(mask, index)
    print(f"{source_path}: {width}x{height}, cells {cell_w}x{cell_h}")

    result = Image.new(image.mode, (width, height))
    for mask in range(16):
        target = CANONICAL[mask]
        source_index = by_mask.get(mask)
        if source_index is not None:
            tile = image.crop(cell_box(source_index, cell_w, cell_h))
            print(f"  cell {target:2d} (mask {mask:2d}) <- source cell {source_index}")
        else:
            first, second = None, None
            for bit in (1, 2, 4, 8):
                if mask & bit and by_mask.get(bit) is not None:
                    if first is None:
                        first = by_mask[bit]
                    elif second is None:
                        second = by_mask[bit]
            if first is None or second is None:
                raise SystemExit(f"mask {mask} is missing and cannot be built from single corners")
            tile = wetter_of(image.crop(cell_box(first, cell_w, cell_h)),
                             image.crop(cell_box(second, cell_w, cell_h)))
            print(f"  cell {target:2d} (mask {mask:2d}) <- built from source cells {first} and {second}")
        result.paste(tile, cell_box(target, cell_w, cell_h))
    result.save(output_path)
    print(f"{output_path}: written in the engine's canonical order")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
