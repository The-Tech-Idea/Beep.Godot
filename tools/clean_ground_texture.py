"""Take the objects out of a ground texture, keep its surface.

Usage:
    python tools/clean_ground_texture.py SOURCE.png OUTPUT.png [x y w h ...]

A ground texture is sampled at a few texels a screen pixel and repeated across a whole biome, so
anything drawn in it larger than a few texels lands on screen at about the size of a prop, in every
tile. The shipped cartoon atlas's sand holds a 22-texel starfish and 12-to-18-texel shells, and they
read as objects lying on the beach in every view that lays a ground texture over its terrain. No
sampling change fixes that - a tighter repeat shrinks them and makes them tile, and a high-pass
grain removes their fill and leaves their outlines. The art has to stop containing them.

WHAT SEPARATES A SURFACE FROM A THING. Not size alone - sand speckle and a shell's edge are both
small. It is AMPLITUDE at scale: grain is many small departures from the local tone, while a drawn
object is a large, coherent departure that survives a wide median. So:

- BASE is a median over MEDIAN_DIAMETER texels. A median keeps edges of things larger than its window
  and erases everything smaller, so at 13 texels it holds the dunes and tone and drops the speckle.
- DETAIL is the source minus that base: speckle, grain, and the strong edges of anything drawn.
- The result is base plus detail CLAMPED to +/- DETAIL_LIMIT. Grain lives well inside that limit and
  passes through untouched; a shell's outline is far outside it and is cut back to the surrounding
  tone. Nothing is blurred, so the texture keeps its own resolution.

Regions are optional: given none, the whole image is cleaned; given some, only those rectangles are,
which is what an atlas needs when its other rows hold props that must not be touched.
"""
import sys

from PIL import Image, ImageFilter

# Wider than the objects, not merely as wide: a median keeps whatever fills more than about half its
# window, so at 13 texels the 12-to-18-texel shells survived into the base and came straight back
# out. Thirty-one swallows them and the 22-texel starfish whole, while the dunes it must keep run at
# about a hundred.
MEDIAN_DIAMETER = 61
# How far a texel may depart from its local tone and still count as surface, per channel, 0-255.
# Measured on the cartoon atlas's sand: speckle sits within about 8, the shells and starfish reach
# 40 and beyond.
DETAIL_LIMIT = 10


def clean(image: Image.Image) -> Image.Image:
    base = image.filter(ImageFilter.MedianFilter(MEDIAN_DIAMETER))
    source_pixels = image.load()
    base_pixels = base.load()
    width, height = image.size
    result = Image.new("RGB", (width, height))
    out = result.load()
    kept = cut = 0
    for y in range(height):
        for x in range(width):
            source = source_pixels[x, y]
            local = base_pixels[x, y]
            # Split the departure into tone and colour. Surface grain is almost entirely tone - sand
            # is sand, lighter here and darker there - while a drawn object carries hue of its own,
            # which is why the atlas's starfish survived a per-channel clamp as an orange trace on
            # yellow sand. Colour is therefore held to a third of what tone may do.
            deltas = [source[channel] - local[channel] for channel in range(3)]
            tone = sum(deltas) / 3.0
            clipped = False
            if tone > DETAIL_LIMIT:
                tone, clipped = DETAIL_LIMIT, True
            elif tone < -DETAIL_LIMIT:
                tone, clipped = -DETAIL_LIMIT, True
            colour_limit = DETAIL_LIMIT / 3.0
            channels = []
            for channel in range(3):
                chroma = deltas[channel] - sum(deltas) / 3.0
                if chroma > colour_limit:
                    chroma, clipped = colour_limit, True
                elif chroma < -colour_limit:
                    chroma, clipped = -colour_limit, True
                channels.append(max(0, min(255, int(round(local[channel] + tone + chroma)))))
            out[x, y] = tuple(channels)
            if clipped:
                cut += 1
            else:
                kept += 1
    print(f"    {kept} texels kept as surface, {cut} pulled back to their local tone")
    return result


def main(source_path: str, output_path: str, regions: list[tuple[int, int, int, int]]) -> None:
    image = Image.open(source_path)
    mode = image.mode
    working = image.convert("RGB")
    if not regions:
        regions = [(0, 0, working.size[0], working.size[1])]
    for x, y, w, h in regions:
        print(f"  region {x},{y} {w}x{h}")
        box = (x, y, x + w, y + h)
        working.paste(clean(working.crop(box)), box)
    if mode == "RGBA":
        working.putalpha(image.getchannel("A"))
    working.save(output_path)
    print(f"{output_path}: {len(regions)} region(s) cleaned")


if __name__ == "__main__":
    if len(sys.argv) < 3 or (len(sys.argv) - 3) % 4 != 0:
        raise SystemExit(__doc__)
    numbers = [int(value) for value in sys.argv[3:]]
    main(sys.argv[1], sys.argv[2],
         [tuple(numbers[i:i + 4]) for i in range(0, len(numbers), 4)])
