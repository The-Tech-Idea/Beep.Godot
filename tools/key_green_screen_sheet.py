"""Turn a sprite sheet drawn on a pure green screen into a transparent PNG.

Usage:
    python tools/key_green_screen_sheet.py SOURCE.png OUTPUT.png

The source is an RGB sheet whose background is the key colour (0, 255, 0), with the anti-aliased
edge of every sprite blended into it. A hard key - "this exact green is transparent" - leaves that
edge behind as a green halo, so this keys the way a compositor does against a known backing colour.

WHAT SEPARATES ART FROM BACKING. Not green dominance. This tool used to call a pixel backing when
g - max(r, b) rose above a threshold, which worked only while the foliage on these sheets was teal.
Measured on the sheets drawn 2026-09-18, whose trees and bushes are properly GREEN, that rule read
almost every leaf as half-transparent backing and un-mixed its colour away: of the 3380 pixels of
art in bushes_cartoon_32x32_v2.png it left 127 opaque and dissolved 3154 into edge. The art's own
greens reach (0, 187, 4) - saturated, but nowhere near the key - while the backing and its
compression noise sit at g >= 251. That gap is what the key is built on now:

- BACKING is near the key itself: r and b at or below CHANNEL_LIMIT with g at or above KEY_FLOOR.
  Those pixels go fully transparent wherever they are, including the holes enclosed by a canopy,
  which a flood fill from the border cannot reach (102 to 267 such pixels per tree sheet).
- EDGE is the one ring of pixels touching the backing, where the anti-aliasing mixed sprite and key.
  Alpha comes from how far that pixel's green dominance carries toward the key, and the colour is
  un-mixed from the backing: p = a*C + (1 - a)*key, so C = (p - (1 - a)*key) / a. That removes the
  green spill rather than tinting it away. Art further in is never touched, however green it is.
- BLEED: fully transparent pixels take the colour of the nearest sprite pixel. Filtering and mipmaps
  average transparent texels into the edge, so a black or green backing there would draw a dark or
  green fringe at map zoom.

The tool refuses a source that has an alpha channel already, or one whose most common colour is not
near the key: it is for green-screen sheets only, and saying so beats keying the wrong art.
"""
import sys
from collections import Counter

from PIL import Image

KEY = (0, 255, 0)
# A pixel is the backing when it is this close to the key. The window sits in the measured gap
# between the art's greenest colour (0, 187, 4) and the backing's own noise, which never falls below
# (3, 251, 3) on any shipped sheet - so it takes the whole backing and none of the art.
KEY_FLOOR = 220
CHANNEL_LIMIT = 60
# Below this green dominance an edge pixel carries no key at all and keeps full alpha; at the
# backing's own dominance it is gone. Between them alpha falls linearly, which is the blend the
# anti-aliasing made.
EDGE_FLOOR = 24
# Transparent pixels further than this from any sprite pixel keep black; nothing samples them.
BLEED_PASSES = 4


def is_backing(pixel: tuple[int, int, int]) -> bool:
    red, green, blue = pixel
    return green >= KEY_FLOOR and red <= CHANNEL_LIMIT and blue <= CHANNEL_LIMIT


def key_edge(r: int, g: int, b: int) -> tuple[int, int, int, int]:
    """One pixel of the ring that touches the backing, un-mixed from the key."""
    dominance = g - max(r, b)
    if dominance <= EDGE_FLOOR:
        return r, g, b, 255
    ceiling = KEY[1] - 0
    alpha = max(0.0, min(1.0, (ceiling - dominance) / (ceiling - EDGE_FLOOR)))
    if alpha <= 0.0:
        return 0, 0, 0, 0
    unmixed = (
        (r - (1.0 - alpha) * KEY[0]) / alpha,
        (g - (1.0 - alpha) * KEY[1]) / alpha,
        (b - (1.0 - alpha) * KEY[2]) / alpha,
    )
    red, green, blue = (int(round(max(0.0, min(255.0, channel)))) for channel in unmixed)
    return red, green, blue, int(round(alpha * 255))


def bleed(pixels: list[list[tuple[int, int, int, int]]], width: int, height: int) -> None:
    """Give transparent pixels the average colour of their sprite neighbours, a few rings out."""
    for _ in range(BLEED_PASSES):
        updates = []
        for y in range(height):
            for x in range(width):
                if pixels[y][x][3] != 0 or pixels[y][x][:3] != (0, 0, 0):
                    continue
                total, count = [0, 0, 0], 0
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if (dx or dy) and 0 <= nx < width and 0 <= ny < height:
                            neighbour = pixels[ny][nx]
                            if neighbour[3] != 0 or neighbour[:3] != (0, 0, 0):
                                total = [t + c for t, c in zip(total, neighbour[:3])]
                                count += 1
                if count:
                    updates.append((x, y, tuple(t // count for t in total)))
        if not updates:
            return
        for x, y, colour in updates:
            pixels[y][x] = (*colour, 0)


def main(source_path: str, output_path: str) -> None:
    source = Image.open(source_path)
    if source.mode not in ("RGB", "P", "L"):
        raise SystemExit(f"{source_path} is {source.mode}; a sheet that already has alpha needs no key")
    rgb = source.convert("RGB")
    width, height = rgb.size
    background = Counter(rgb.getdata()).most_common(1)[0][0]
    if not is_backing(background):
        raise SystemExit(f"{source_path}: the most common colour is {background}, not the green key {KEY}")

    source_pixels = rgb.load()
    backing = [[is_backing(source_pixels[x, y]) for x in range(width)] for y in range(height)]

    def touches_backing(x: int, y: int) -> bool:
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if (dx or dy) and 0 <= nx < width and 0 <= ny < height and backing[ny][nx]:
                    return True
        return False

    pixels: list[list[tuple[int, int, int, int]]] = []
    for y in range(height):
        row = []
        for x in range(width):
            if backing[y][x]:
                row.append((0, 0, 0, 0))
                continue
            r, g, b = source_pixels[x, y]
            row.append(key_edge(r, g, b) if touches_backing(x, y) else (r, g, b, 255))
        pixels.append(row)

    bleed(pixels, width, height)
    keyed = Image.new("RGBA", (width, height))
    keyed.putdata([pixel for row in pixels for pixel in row])
    keyed.save(output_path)
    transparent = sum(1 for row in pixels for pixel in row if pixel[3] == 0)
    partial = sum(1 for row in pixels for pixel in row if 0 < pixel[3] < 255)
    print(f"{output_path}: {width}x{height}, {transparent} transparent, {partial} edge, "
          f"{width * height - transparent - partial} opaque")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
