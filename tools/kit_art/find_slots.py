"""
Propose widget rectangles in a reference sheet, and emit them as ready-to-paste
`slice_sheets.py --slot` arguments.

WHY THIS EXISTS
---------------
`slice_sheets.py` needs `x,y,w,h` plus 9-patch margins per widget, and working those out by hand
across 43 sheets is what left phase E with a tool nobody could use. This does the mechanical part:
finds the discrete objects on a sheet and measures each one's corner inset, which is the margin a
9-patch needs in order not to slice its own corner artwork.

IT EMITS COORDINATES, NOT PIXELS -- which matters, because the sheets have mixed provenance
(gameui2/3/7 are watermarked comps). A rect is a measurement, exactly like the proportions already
recorded in plans/game-ui-kit/art/*.md; it can be committed and shared freely. Only
`slice_sheets.py`, which actually copies pixels, is gated on licensing.

KNOWN LIMITATION -- MEASURED, NOT ASSUMED
-----------------------------------------
This works only on sheets whose widgets sit on a FLAT field. Run against `rpgui.png` it finds
5 fragments and misses the PLAY button, the title bar, every bar and every banner: that sheet's
widgets sit on a dark TEXTURED backdrop, so the background flood cannot reach between them and
the whole sheet is one connected mass.

The five rects it emitted looked entirely plausible -- sensible sizes, sensible margins -- which
is the point of the montage. Check it every time; a sheet this fails on fails silently in the
numbers.

For textured sheets the fallback is the method the art documents already used: scanlines through
the real pixels, by hand, per widget. That is slower and it is what produced every reliable
measurement in plans/game-ui-kit/art/.

METHOD
    1. background = the sheet's most common edge colour;
    2. flood the background inward, so enclosed detail is not mistaken for background;
    3. label the remaining connected regions and keep ones of plausible widget size;
    4. per region, walk in from each edge along the centre lines until the colour stops changing,
       which approximates the frame thickness -- the 9-patch margin.

Every number is a PROPOSAL. Eyeball the montage it writes before slicing anything: a sheet with
touching widgets will merge them into one region, and that is obvious in the montage and invisible
in the numbers.

USAGE
    python find_slots.py --sheet rpgui
    python find_slots.py --sheet rpgui --min 48 --max 520 --out slots_rpgui.txt
"""
import argparse
import os
import sys
from collections import Counter, deque

try:
    from PIL import Image, ImageDraw
except ImportError:
    sys.exit("PIL is required: pip install pillow")

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "Example_Art"))


def background(im):
    """Most common colour around the border — sheets are laid out on a flat field."""
    w, h = im.size
    px = im.load()
    c = Counter()
    for x in range(0, w, max(1, w // 200)):
        c[px[x, 0]] += 1
        c[px[x, h - 1]] += 1
    for y in range(0, h, max(1, h // 200)):
        c[px[0, y]] += 1
        c[px[w - 1, y]] += 1
    return c.most_common(1)[0][0]


def near(a, b, tol):
    return all(abs(a[i] - b[i]) <= tol for i in range(3))


def regions(im, bg, tol, min_side, max_side):
    """Connected non-background regions, found by flooding the background in from the border."""
    w, h = im.size
    px = im.load()
    is_bg = bytearray(w * h)

    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if near(px[x, y], bg, tol) and not is_bg[y * w + x]:
                is_bg[y * w + x] = 1
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if near(px[x, y], bg, tol) and not is_bg[y * w + x]:
                is_bg[y * w + x] = 1
                q.append((x, y))
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not is_bg[ny * w + nx] and near(px[nx, ny], bg, tol):
                is_bg[ny * w + nx] = 1
                q.append((nx, ny))

    seen = bytearray(w * h)
    out = []
    for sy in range(h):
        for sx in range(w):
            i = sy * w + sx
            if is_bg[i] or seen[i]:
                continue
            x0 = x1 = sx
            y0 = y1 = sy
            seen[i] = 1
            stack = [(sx, sy)]
            n = 0
            while stack:
                x, y = stack.pop()
                n += 1
                x0, x1 = min(x0, x), max(x1, x)
                y0, y1 = min(y0, y), max(y1, y)
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    j = ny * w + nx
                    if 0 <= nx < w and 0 <= ny < h and not is_bg[j] and not seen[j]:
                        seen[j] = 1
                        stack.append((nx, ny))
            rw, rh = x1 - x0 + 1, y1 - y0 + 1
            # Reject specks, page-sized blobs, and threads: a widget has area and some bulk.
            if not (min_side <= rw <= max_side and min_side <= rh <= max_side):
                continue
            if n < rw * rh * 0.35:
                continue
            out.append((x0, y0, rw, rh))
    return out


def tighten(im, rect, tol):
    """Shrink a rough box onto the widget inside it.

    Works where auto-segmentation does not, because it never has to SEPARATE widgets -- the human
    already did that by drawing the box. It only has to find where the content stops, which it
    does by walking each edge inward while that whole row or column stays close to the box's own
    border colour (i.e. is still backdrop).
    """
    x, y, w, h = rect
    px = im.load()
    W, H = im.size
    x = max(0, min(x, W - 2)); y = max(0, min(y, H - 2))
    w = max(2, min(w, W - x)); h = max(2, min(h, H - y))

    def row_is_edge(yy, ref):
        n = same = 0
        for xx in range(x, x + w, max(1, w // 60)):
            n += 1
            if near(px[xx, yy], ref, tol):
                same += 1
        return same >= n * 0.92

    def col_is_edge(xx, ref):
        n = same = 0
        for yy in range(y, y + h, max(1, h // 60)):
            n += 1
            if near(px[xx, yy], ref, tol):
                same += 1
        return same >= n * 0.92

    top_ref = px[x + w // 2, y]
    bot_ref = px[x + w // 2, y + h - 1]
    left_ref = px[x, y + h // 2]
    right_ref = px[x + w - 1, y + h // 2]

    t = y
    while t < y + h // 2 and row_is_edge(t, top_ref):
        t += 1
    b = y + h - 1
    while b > t + 2 and row_is_edge(b, bot_ref):
        b -= 1
    l = x
    while l < x + w // 2 and col_is_edge(l, left_ref):
        l += 1
    r = x + w - 1
    while r > l + 2 and col_is_edge(r, right_ref):
        r -= 1
    # One pixel back, so the widget's own outline is included rather than shaved off.
    l = max(x, l - 1); t = max(y, t - 1)
    r = min(x + w - 1, r + 1); b = min(y + h - 1, b + 1)
    return l, t, r - l + 1, b - t + 1


def margins(im, rect, tol):
    """Walk in from each edge along the centre lines until the colour settles: the frame."""
    x, y, w, h = rect
    px = im.load()
    cx, cy = x + w // 2, y + h // 2

    def walk(sx, sy, dx, dy, limit):
        start = px[sx, sy]
        for i in range(1, limit):
            p = px[sx + dx * i, sy + dy * i]
            if not near(p, start, tol):
                return max(2, i)
        return max(2, limit // 4)

    l = walk(x, cy, 1, 0, max(3, w // 2))
    r = walk(x + w - 1, cy, -1, 0, max(3, w // 2))
    t = walk(cx, y, 0, 1, max(3, h // 2))
    b = walk(cx, y + h - 1, 0, -1, max(3, h // 2))
    # The walker stops at the OUTER OUTLINE, not at the end of the frame: on rpgui's PLAY button
    # it reports 2px where the art document measured the wood frame at 0.157 x height (~15px).
    # So anything implausibly thin falls back to the measured structural-frame fit from
    # citybuilder5, 3.5px + 0.07 x height, which is the same formula KitGeometry.FramePx uses.
    floor = 3.5 + 0.07 * h
    if l + r < floor:
        l = r = max(l, int(round(floor)))
    if t + b < floor:
        t = b = max(t, int(round(floor)))

    # A 9-patch must keep a stretchable centre.
    l = min(l, max(2, w // 3)); r = min(r, max(2, w // 3))
    t = min(t, max(2, h // 3)); b = min(b, max(2, h // 3))
    return l, t, r, b


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--sheet", required=True, help="sheet name without .png")
    ap.add_argument("--tol", type=int, default=18, help="colour tolerance")
    ap.add_argument("--min", type=int, default=40, help="smallest widget side, px")
    ap.add_argument("--max", type=int, default=600, help="largest widget side, px")
    ap.add_argument("--out", help="write the --slot lines here as well as stdout")
    ap.add_argument("--refine", help="x,y,w,h of a ROUGH box; tighten it and measure margins. "
                                     "The way to work on textured sheets, where auto-segmentation "
                                     "cannot separate widgets.")
    ap.add_argument("--name", default="widget", help="widget name for the emitted --slot")
    ap.add_argument("--slotname", default="base", help="slot name for the emitted --slot")
    args = ap.parse_args()

    src = os.path.join(ART, args.sheet + ".png")
    if not os.path.isfile(src):
        sys.exit(f"no such sheet: {src}")
    im = Image.open(src).convert("RGB")

    if args.refine:
        try:
            rx, ry, rw, rh = (int(v) for v in args.refine.split(","))
        except ValueError:
            sys.exit("--refine wants x,y,w,h")
        rect = tighten(im, (rx, ry, rw, rh), args.tol)
        m = margins(im, rect, args.tol)
        x, y, w, h = rect
        print(f"{args.sheet}.png  rough {rx},{ry},{rw},{rh}  ->  tight {x},{y},{w},{h}")
        print(f"margins {m}")
        print(f"--slot {args.name}:{args.slotname}:{x},{y},{w},{h}:{m[0]},{m[1]},{m[2]},{m[3]}")
        crop = im.crop((x, y, x + w, y + h))
        out = os.path.join(HERE, f"refine_{args.sheet}_{args.name}.png")
        crop.resize((w * 2, h * 2), Image.NEAREST).save(out)
        print(f"preview: {out}  <- confirm this is the whole widget and nothing else")
        return 0

    bg = background(im)
    found = regions(im, bg, args.tol, args.min, args.max)
    found.sort(key=lambda r: (r[1], r[0]))

    print(f"{args.sheet}.png  {im.size[0]}x{im.size[1]}  background={bg}")
    print(f"{len(found)} candidate region(s)\n")

    lines = []
    for i, rect in enumerate(found):
        m = margins(im, rect, args.tol)
        x, y, w, h = rect
        line = (f'--slot widget{i}:base:{x},{y},{w},{h}:{m[0]},{m[1]},{m[2]},{m[3]}')
        lines.append(line)
        print(f"  [{i:>2}] {w:>4}x{h:<4} at {x:>4},{y:<4}  margins {m}")

    # A montage, because a sheet with touching widgets merges them into one region and that is
    # obvious to the eye and invisible in the numbers.
    prev = im.copy()
    d = ImageDraw.Draw(prev)
    for i, (x, y, w, h) in enumerate(found):
        d.rectangle([x, y, x + w - 1, y + h - 1], outline=(255, 0, 0), width=3)
        d.text((x + 4, y + 4), str(i), fill=(255, 255, 0))
    out_png = os.path.join(HERE, f"slots_{args.sheet}.png")
    prev.save(out_png)
    print(f"\nmontage: {out_png}  <- check this before slicing; merged widgets show up here")

    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
        print(f"slot args: {args.out}")
    print("\nRename widget<N> to the real widget/slot before passing these to slice_sheets.py.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
