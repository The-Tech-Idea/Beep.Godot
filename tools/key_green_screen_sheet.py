"""Turn a sprite sheet drawn on a pure green screen into a transparent PNG.

Usage:
    python tools/key_green_screen_sheet.py SOURCE.png OUTPUT.png

The source is an RGB sheet whose background is the key colour (0, 255, 0), with the anti-aliased
edge of every sprite blended into it. A hard key - "this exact green is transparent" - leaves that
edge behind as a green halo, and dropping every greenish pixel eats teal foliage. So this keys the
way a compositor does against a known backing colour:

- ALPHA from green dominance, d = g - max(r, b). The backing is d >= BACKING_DOMINANCE - the pure
  key and the compression noise around it. Art is d <= ART_DOMINANCE: a sprite's own colours - teal
  leaves, grey rock, yellow birch - never have green that far above both other channels. Between
  the two, alpha falls linearly, which is the blend the anti-aliasing made. Keying the noise as
  faint alpha instead would make every frame's visible bounds the whole cell.
- COLOUR un-mixed from the backing: an edge pixel p = a*C + (1 - a)*key, so the sprite's own colour
  is C = (p - (1 - a)*key) / a. That removes the green spill rather than tinting it away.
- BLEED: fully transparent pixels take the colour of the nearest sprite pixel. Filtering and mipmaps
  average transparent texels into the edge, so a black or green backing there would draw a dark or
  green fringe at map zoom.

The tool refuses a source that has an alpha channel already, or one whose most common colour is not
the key: it is for green-screen sheets only, and saying so beats keying the wrong art.
"""
import sys
from collections import Counter

from PIL import Image

KEY = (0, 255, 0)
# Green dominance at or below which a pixel is fully the sprite's own colour, and at or above which
# it is the backing. Measured on the cartoon prop sheets: every sprite colour sits at or below 16,
# the key and its noise at 240 and above, and only anti-aliased edges fall between.
ART_DOMINANCE = 16
BACKING_DOMINANCE = 240
# Transparent pixels further than this from any sprite pixel keep black; nothing samples them.
BLEED_PASSES = 4


def key_pixel(r: int, g: int, b: int) -> tuple[int, int, int, int]:
    dominance = g - max(r, b)
    if dominance <= ART_DOMINANCE:
        return r, g, b, 255
    if dominance >= BACKING_DOMINANCE:
        return 0, 0, 0, 0
    alpha = (BACKING_DOMINANCE - dominance) / (BACKING_DOMINANCE - ART_DOMINANCE)
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
    background = Counter(rgb.get_flattened_data()).most_common(1)[0][0]
    if background != KEY:
        raise SystemExit(f"{source_path}: the most common colour is {background}, not the green key {KEY}")

    pixels = [[key_pixel(*rgb.getpixel((x, y))) for x in range(width)] for y in range(height)]
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
