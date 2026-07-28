"""
The greyscale gate for the Game UI Kit (plans/game-ui-kit/PLAN.md 4.1).

Render the same widget under every genre, strip the colour, and check the genres are still
tellable apart. If they are not, the kit is skinning by palette and the per-genre geometry and
material are doing no work.

WHY THIS WAS REWRITTEN
----------------------
The first version scored 45/45 PASS while the silhouettes were near-identical rectangles. Two
defects, both of which let colour alone carry a pass:

  1. It resized every silhouette to a fixed 128x64 before comparing, which erased proportion —
     and proportion (HeightRatio, PadRatio) is one of the genre tells the kit is built on.
  2. Its second axis was a raw greyscale HISTOGRAM, which moves when nothing but the fill
     colour changes. Combined with `shape OR texture`, any pair could pass on colour alone.

Both axes here are colour-invariant by construction:

  OUTLINE   : aspect ratio at NATURAL size, per-corner occupancy, and edge rake. Pure
              silhouette geometry — a fill change cannot move it at all.
  STRUCTURE : the normalised lightness PROFILE across and down the widget (frame thickness,
              bevel, plate inset, studs). Each profile is standardised to zero mean and unit
              range BEFORE comparison, so a uniform lighten/darken/recolour scores zero and
              only the arrangement of light and dark counts.

A pair passes on either axis, which is legitimate here precisely because neither axis responds
to colour — `--selftest` proves that rather than asserting it.

MATERIAL CHECK (reported, not gated)
------------------------------------
plans/game-ui-kit/art/INDEX.md records two measured ratios that separate the PAINTED reference
family from the FLAT one. Feeding painted proportions to a flat renderer was the root error of
the earlier sessions, so the gate reports where each genre lands:

    bottom : peak lightness within a plate   painted 0.18-0.27   flat 0.76-0.84
    rim : body lightness                     painted 1.78-2.05x  flat 1.3-1.5x

This is reported rather than enforced because the target family is a per-genre design decision
(casual/mobile genres SHOULD read flat), and because gameui1.md records a deliberate exemption:
progress can read as saturation rather than brightness.

USAGE
    python verify_greyscale.py <folder-with-gs_*.png>
    python verify_greyscale.py --selftest
"""
import sys, os, glob

from PIL import Image, ImageDraw

# Minimum separation for a pair to count as distinguishable, on either colour-invariant axis.
# Calibrated by --selftest: a pure colour swap must score ~0 and a real shape change must clear
# these comfortably. Do not raise these to make a run pass.
OUTLINE_MIN = 0.040
STRUCTURE_MIN = 0.070

BG_TOLERANCE = 6        # greyscale distance from the corner pixel that counts as "widget"
PROFILE_BINS = 24


# ── extraction ────────────────────────────────────────────────────────────────────────────

def _mask(gr, bg):
    """Boolean widget mask as a list of rows."""
    w, h = gr.size
    px = gr.load()
    return [[abs(px[x, y] - bg) > BG_TOLERANCE for x in range(w)] for y in range(h)]


def _bbox_of(mask):
    ys = [y for y, row in enumerate(mask) if any(row)]
    if not ys:
        return None
    xs0 = min(row.index(True) for row in mask if any(row))
    xs1 = max(len(row) - 1 - row[::-1].index(True) for row in mask if any(row))
    return xs0, ys[0], xs1, ys[-1]


def _standardise(v):
    """Zero mean, unit range. This is what makes the structure axis colour-invariant: a uniform
    lighten, darken or recolour maps to the same standardised profile."""
    if not v:
        return v
    lo, hi = min(v), max(v)
    if hi - lo < 1e-6:
        return [0.0] * len(v)
    m = sum(v) / len(v)
    return [(x - m) / (hi - lo) for x in v]


def _resample(v, n):
    if not v:
        return [0.0] * n
    return [v[min(len(v) - 1, int(i * len(v) / n))] for i in range(n)]


def load(path):
    """Return the colour-invariant feature set for one render."""
    im = Image.open(path).convert("L")
    bg = im.getpixel((2, 2))
    mask = _mask(im, bg)
    box = _bbox_of(mask)
    if box is None:
        raise ValueError(f"{path}: no widget found against the background")
    x0, y0, x1, y1 = box
    w, h = x1 - x0 + 1, y1 - y0 + 1
    px = im.load()

    # ── OUTLINE ──
    aspect = w / float(h)

    # Per-corner occupancy: the fraction of a corner box that is widget. Square corners give
    # ~1.0, a pill ~0.46, a chamfer ~0.54 — and it is unaffected by fill colour.
    #
    # Sampled at THREE radii because a single radius cannot tell a straight diagonal from a
    # curve: a chamfer and a pill can coincide at one k and separate sharply at another. The
    # three together also register a difference of corner RADIUS alone, which is what separates
    # the genres that share the Round silhouette.
    corners = []
    for frac in (0.20, 0.32, 0.45):
        k = max(3, int(min(w, h) * frac))
        for cy, cx in ((0, 0), (0, 1), (1, 0), (1, 1)):
            tot = hit = 0
            for dy in range(k):
                for dx in range(k):
                    y = y0 + dy if cy == 0 else y1 - dy
                    x = x0 + dx if cx == 0 else x1 - dx
                    tot += 1
                    if mask[y][x]:
                        hit += 1
            corners.append(hit / float(tot))

    # Edge rake: how far the top and bottom rows are inset relative to the middle. Separates a
    # symmetric chamfer from a one-sided clip or a raked speed shape.
    def row_span(y):
        row = mask[y]
        idx = [x for x in range(x0, x1 + 1) if row[x]]
        return (idx[0] - x0, x1 - idx[-1]) if idx else (w / 2.0, w / 2.0)

    tl, tr = row_span(y0 + max(1, h // 20))
    bl, br = row_span(y1 - max(1, h // 20))
    rake = [tl / w, tr / w, bl / w, br / w]

    # WEIGHTED, because a flat mean lets one feature be diluted by the rest: a genre differing
    # only in PROPORTION (racing 124x35 vs platformer 142x50 — a real tell, set by HeightRatio
    # and PadRatio) moved the old flat mean by 0.03 and read as identical. Aspect therefore
    # carries a third of the weight on its own, and the 12 corner samples share a third.
    outline = ([(aspect / 2.0, 3.0)]
               + [(c, 4.0 / len(corners)) for c in corners]
               + [(r, 2.0 / len(rake)) for r in rake])

    # ── STRUCTURE ── (frame, bevel, plate inset, studs) — standardised, so colour drops out
    midy, midx = (y0 + y1) // 2, (x0 + x1) // 2
    hprof = _standardise(_resample([px[x, midy] for x in range(x0, x1 + 1)], PROFILE_BINS))
    vprof = _standardise(_resample([px[midx, y] for y in range(y0, y1 + 1)], PROFILE_BINS))

    # Both diagonals, because the centre cross NEVER CROSSES A CORNER. Corner ornament — studs,
    # rivets, brackets — is a documented genre tell (strategy carries studs, citybuilder does
    # not), and with centre scanlines alone the gate was blind to it: the pair scored 0.069
    # against a 0.070 bar while differing by four visible studs.
    n = max(abs(x1 - x0), abs(y1 - y0)) + 1
    d1 = [px[x0 + (x1 - x0) * i // n, y0 + (y1 - y0) * i // n] for i in range(n)]
    d2 = [px[x1 - (x1 - x0) * i // n, y0 + (y1 - y0) * i // n] for i in range(n)]
    dprof1 = _standardise(_resample(d1, PROFILE_BINS))
    dprof2 = _standardise(_resample(d2, PROFILE_BINS))

    structure = hprof + vprof + dprof1 + dprof2

    # ── MATERIAL ── (reported, not gated)
    #
    # Both ratios are measured WITHIN THE PLATE, i.e. inside the frame. Measuring down the whole
    # widget instead counts the dark ink rim as "the bottom of the plate" and drives bottom:peak
    # to 0.02-0.14 — below even the painted range — for every genre, which is a measurement
    # artefact and not a material reading. The inset clears any plausible frame: the measured
    # frame is 3.5px + 0.07 x height (citybuilder5.md), so 0.18 x height is comfortably past it.
    inset = max(3, int(h * 0.18))
    py0, py1 = y0 + inset, y1 - inset
    pxx0, pxx1 = x0 + inset, x1 - inset
    if py1 <= py0 or pxx1 <= pxx0:
        py0, py1, pxx0, pxx1 = y0, y1, x0, x1

    # Each row is represented by its DOMINANT tone, not by the centre pixel. A single centre
    # column runs straight through the label: on platformer it read peak=221 off the "PLAY"
    # glyph against a 58 plate, so bottom:peak was measuring text contrast (0.26) and no change
    # to the material moved it at all. The glyph is a minority of any row, so the mode ignores it.
    def row_tone(y):
        h2 = {}
        for x in range(pxx0, pxx1 + 1):
            if mask[y][x]:
                v = px[x, y] // 4
                h2[v] = h2.get(v, 0) + 1
        return (max(h2, key=h2.get) * 4 + 2) if h2 else 0

    plate_col = [row_tone(y) for y in range(py0, py1 + 1)]
    peak = max(plate_col) or 1
    nb = max(1, len(plate_col) // 6)
    bottom_peak = (sum(plate_col[-nb:]) / nb) / peak

    # Body is the plate's OWN colour, taken as the modal value rather than the median.
    # citybuilder5.md lists the gloss band as a layer separate from the plate ("plate #75864F
    # L=0.42" vs "gloss band ~8px of teal L=0.67"), so a body that includes the gloss is not the
    # quantity the reference ratios are stated against. The gloss covers ~34% of the plate here,
    # enough to drag a median upward and depress every rim:body reading to ~0.6x of target.
    # The mode is robust to it, because gloss and bevel are both minorities by area.
    hist = {}
    for y in range(py0, py1 + 1):
        for x in range(pxx0, pxx1 + 1):
            v = px[x, y] // 4          # 4-level buckets, so antialiasing does not split the peak
            hist[v] = hist.get(v, 0) + 1
    body = (max(hist, key=hist.get) * 4 + 2) if hist else 1

    # The rim is the outermost ring of the widget. Its polarity matters: the carved-stone
    # reference draws a BRIGHT outer rim at 2.05x the plate, while this kit currently draws a
    # DARK ink rim, so the ratio can legitimately land either side of 1.0.
    ring = []
    for t in range(2):
        for x in range(x0, x1 + 1):
            if mask[y0 + t][x]: ring.append(px[x, y0 + t])
            if mask[y1 - t][x]: ring.append(px[x, y1 - t])
    ring.sort()
    rim = ring[len(ring) // 2] if ring else body
    rim_body = rim / float(body or 1)

    return {
        "size": (w, h), "outline": outline, "structure": structure,
        "bottom_peak": bottom_peak, "rim_body": rim_body,
    }


def _dist(a, b):
    """Mean absolute difference. Accepts either a plain list (structure) or a list of
    (value, weight) pairs (outline)."""
    if a and isinstance(a[0], tuple):
        num = sum(w * abs(x - y) for (x, w), (y, _) in zip(a, b))
        return num / sum(w for _, w in a)
    return sum(abs(x - y) for x, y in zip(a, b)) / len(a)


# ── reporting ─────────────────────────────────────────────────────────────────────────────

def montage(folder, names, data):
    """Emit the renders side by side in greyscale. A gate whose output is only numbers is how
    the last one survived being wrong — the montage is there to be looked at."""
    crops = []
    for n in names:
        im = Image.open(os.path.join(folder, f"gs_{n}.png")).convert("L")
        bg = im.getpixel((2, 2))
        box = _bbox_of(_mask(im, bg))
        if box:
            crops.append((n, im.crop((box[0] - 6, box[1] - 6, box[2] + 7, box[3] + 7))))
    if not crops:
        return None
    cw = max(c.size[0] for _, c in crops)
    ch = max(c.size[1] for _, c in crops)
    cols = 5
    rows = (len(crops) + cols - 1) // cols
    M = Image.new("L", (cols * (cw + 16), rows * (ch + 30)), 30)
    d = ImageDraw.Draw(M)
    for i, (n, c) in enumerate(crops):
        x = (i % cols) * (cw + 16) + 8
        y = (i // cols) * (ch + 30) + 22
        M.paste(c, (x, y))
        d.text((x, y - 14), n, fill=255)
    out = os.path.join(folder, "montage_grey.png")
    M.save(out)
    return out


def classify(bp, rb):
    if bp <= 0.30 and rb >= 1.7:
        return "painted"
    if bp >= 0.70 and rb <= 1.55:
        return "flat"
    return "mixed"


def run(folder, quiet=False):
    files = sorted(glob.glob(os.path.join(folder, "gs_*.png")))
    if len(files) < 2:
        print(f"  no gs_*.png renders found in {folder}")
        return 1

    data = {os.path.basename(f)[3:-4]: load(f) for f in files}
    names = sorted(data)

    # A pair that clears its threshold by a hair is not a result to build on, so it is reported
    # separately rather than being folded into the pass count.
    MARGINAL = 1.15

    pairs, fails, marginal = [], [], []
    for i in range(len(names)):
        for j in range(i + 1, len(names)):
            a, b = names[i], names[j]
            od = _dist(data[a]["outline"], data[b]["outline"])
            sd = _dist(data[a]["structure"], data[b]["structure"])
            score = max(od / OUTLINE_MIN, sd / STRUCTURE_MIN)
            ok = score >= 1.0
            pairs.append((score, od, sd, a, b, ok))
            if not ok:
                fails.append((od, sd, a, b))
            elif score < MARGINAL:
                marginal.append((score, od, sd, a, b))
    pairs.sort()
    marginal.sort()

    if not quiet:
        print(f"  {len(names)} genres, {len(pairs)} pairs compared in GREYSCALE")
        print(f"  thresholds: outline >= {OUTLINE_MIN}  structure >= {STRUCTURE_MIN}\n")
        print("  genre         size      outline-vs-nearest   material (bottom:peak / rim:body)")
        for n in names:
            near = min((p for p in pairs if n in (p[3], p[4])), key=lambda p: p[1])
            other = near[4] if near[3] == n else near[3]
            d = data[n]
            print(f"    {n:<12s} {d['size'][0]:>3}x{d['size'][1]:<3}  "
                  f"{near[1]:.3f} vs {other:<12s} "
                  f"{d['bottom_peak']:.2f} / {d['rim_body']:.2f}  "
                  f"{classify(d['bottom_peak'], d['rim_body'])}")
        print(f"\n  indistinguishable pairs: {len(fails)}")
        for od, sd, a, b in fails:
            print(f"    {a:12s} vs {b:12s}  outline={od:.3f} structure={sd:.3f}")
        if marginal:
            print(f"\n  MARGINAL passes (< {int((MARGINAL - 1) * 100)}% over threshold — "
                  f"treat as unresolved):")
            for score, od, sd, a, b in marginal:
                print(f"    {a:12s} vs {b:12s}  outline={od:.3f} structure={sd:.3f} "
                      f"(x{score:.2f})")
        print("\n  closest 5 pairs that DO pass:")
        for score, od, sd, a, b, ok in [p for p in pairs if p[5]][:5]:
            print(f"    {a:12s} vs {b:12s}  outline={od:.3f} structure={sd:.3f}")
        m = montage(folder, names, data)
        if m:
            print(f"\n  montage: {m}  <- look at this, do not trust the numbers alone")
        print("\n  PASS" if not fails else "\n  FAIL")
    return 0 if not fails else 1


# ── self-test ─────────────────────────────────────────────────────────────────────────────

def _synth(path, shape, fill, rim, size=(130, 42)):
    """One synthetic widget on the same flat field the probe uses."""
    W, H = 300, 160
    im = Image.new("L", (W, H), 107)
    d = ImageDraw.Draw(im)
    w, h = size
    x0, y0 = (W - w) // 2, (H - h) // 2
    x1, y1 = x0 + w, y0 + h
    if shape == "rect":
        d.rectangle([x0, y0, x1, y1], fill=fill, outline=rim, width=3)
    elif shape == "pill":
        d.rounded_rectangle([x0, y0, x1, y1], radius=h // 2, fill=fill, outline=rim, width=3)
    elif shape == "chamfer":
        c = 12
        d.polygon([(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c),
                   (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)],
                  fill=fill, outline=rim)
    elif shape == "tall":
        y0, y1 = (H - 70) // 2, (H - 70) // 2 + 70
        d.rectangle([x0, y0, x1, y1], fill=fill, outline=rim, width=3)
    im.save(path)


def selftest():
    import tempfile, shutil
    ok = True
    tmp = tempfile.mkdtemp(prefix="gsgate_")
    try:
        # CASE 1 — ten plates identical in every way but FILL COLOUR. A valid gate must call
        # these indistinguishable. The previous gate scored this 45/45 PASS.
        d1 = os.path.join(tmp, "colour_only")
        os.makedirs(d1)
        for i, n in enumerate(["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"]):
            _synth(os.path.join(d1, f"gs_{n}.png"), "rect", fill=40 + i * 20, rim=15 + i * 20)
        r1 = run(d1, quiet=True)
        print(f"  [selftest] colour-only differences -> {'FAIL (correct)' if r1 else 'PASS (WRONG)'}")
        ok &= (r1 == 1)

        # CASE 2 — genuinely different silhouettes must pass.
        d2 = os.path.join(tmp, "real_shapes")
        os.makedirs(d2)
        for n, s in (("rect", "rect"), ("pill", "pill"), ("chamfer", "chamfer"), ("tall", "tall")):
            _synth(os.path.join(d2, f"gs_{n}.png"), s, fill=150, rim=40)
        r2 = run(d2, quiet=True)
        print(f"  [selftest] real shape differences  -> {'PASS (correct)' if not r2 else 'FAIL (WRONG)'}")
        ok &= (r2 == 0)

        # CASE 3 — the exact failure mode the rewrite exists to catch: same silhouette, same
        # structure, only the fill differs. Must score ~0 on BOTH axes, not merely fail the gate.
        _synth(os.path.join(tmp, "x1.png"), "rect", fill=60, rim=20)
        _synth(os.path.join(tmp, "x2.png"), "rect", fill=200, rim=90)
        a, b = load(os.path.join(tmp, "x1.png")), load(os.path.join(tmp, "x2.png"))
        od, sd = _dist(a["outline"], b["outline"]), _dist(a["structure"], b["structure"])
        colour_invariant = od < 0.005 and sd < 0.005
        print(f"  [selftest] colour-invariance       -> outline={od:.4f} structure={sd:.4f} "
              f"{'OK' if colour_invariant else 'NOT INVARIANT'}")
        ok &= colour_invariant
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    print("\n  SELFTEST PASS" if ok else "\n  SELFTEST FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    if "--selftest" in sys.argv:
        sys.exit(selftest())
    sys.exit(run(sys.argv[1] if len(sys.argv) > 1 else "."))
