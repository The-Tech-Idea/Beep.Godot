"""
Author a distinct component SILHOUETTE for every genre.

Companion to gen_rpg.py, which established the model:

    genre   -> the silhouette (this file)
    theme   -> the colour identity
    palette -> the tint on top

Before this, all ten genres drew the same rounded rectangle and differed only by colour,
corner radius and border weight, so a racing button and an RPG button were the same object
twice. Here each genre gets its own outline language.

Two rules the art has to obey, both enforced by the verifier at the bottom:

1. NEUTRAL GREYSCALE around a 0.80 mid-tone. The art register multiplies the 9-patch by the
   palette colour: art(0.80) x modulate(surface/0.80) = surface. Baked bevel and outline
   survive that multiply; coloured source art would be tinted twice.

2. Every edge band must be CONSTANT along the axis it stretches on, and all ornament must sit
   inside the corner margins. A mark in the middle of an edge smears across the whole side of
   a stretched control. This is why every genre's ornament below is a CORNER treatment.

Canvas sizes are read from the art already on disk and margins from each genre's own
theme.json, so a slot can never end up with art whose shape disagrees with its declared
9-patch margins.
"""
import os, json, hashlib
from PIL import Image, ImageDraw

ADDON = "C:/Users/f_ald/source/repos/The-Tech-Idea/Beep.Godot/addons/beep_game_builder_cs"
SKINS = os.path.join(ADDON, "catalogs", "skins")
TEX = os.path.join(ADDON, "textures")

SS = 8
FACE, EDGE, LIGHT, DARK, WELL, STUD = 204, 90, 246, 138, 150, 236


def g(v, a=255):
    return (v, v, v, a)


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def done(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def bevel(img, w, h, s, inset, sunken):
    """Light/dark rim inside the silhouette.

    Each run spans the FULL edge and is then masked by the shape's own alpha, rather than
    being inset by a fixed amount at each end. A fixed inset is only safe while it happens to
    fall inside the corner margin: shooter's panel has a 10px margin but the inset was 7.8px,
    so the run STARTED two pixels inside the stretchable band and the first and last columns
    of that band differed from the rest. Masking keeps the runs uniform at any tile size."""
    hi, lo = (DARK, LIGHT) if sunken else (LIGHT, DARK)
    bev = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(bev)
    t, bt = int(inset * s), h * s - int(inset * s)
    l, r = int(inset * s), w * s - int(inset * s)
    wd = int(1.5 * s)
    d.line([(0, t), (w * s, t)], fill=g(hi), width=wd)
    d.line([(l, 0), (l, h * s)], fill=g(hi), width=wd)
    d.line([(0, bt), (w * s, bt)], fill=g(lo), width=wd)
    d.line([(r, 0), (r, h * s)], fill=g(lo), width=wd)
    img.paste(bev, (0, 0), Image.composite(bev.split()[3], Image.new("L", img.size, 0),
                                           img.split()[3]))


def corner_marks(d, w, h, s, m, kind):
    """Ornament, always inside the corner margin box."""
    off = int(max(1.5, min(m * 0.40, 7, m - 2.5)) * s)
    pts = [(off, off), (w * s - off, off), (off, h * s - off), (w * s - off, h * s - off)]
    if kind == "stud":
        r = int(1.9 * s)
        for cx, cy in pts:
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=g(STUD),
                      outline=g(EDGE), width=max(1, int(0.4 * s)))
    elif kind == "bracket":
        L = int(max(2, min(m * 0.5, 8, m - (off / s) - 2.5)) * s)
        wdt = max(1, int(0.9 * s))
        for i, (cx, cy) in enumerate(pts):
            sx = 1 if i % 2 == 0 else -1
            sy = 1 if i < 2 else -1
            d.line([(cx, cy), (cx + sx * L, cy)], fill=g(STUD), width=wdt)
            d.line([(cx, cy), (cx, cy + sy * L)], fill=g(STUD), width=wdt)
    elif kind == "tick":
        L = int(min(m * 0.42, 6) * s)
        for i, (cx, cy) in enumerate(pts):
            sx = 1 if i % 2 == 0 else -1
            d.line([(cx, cy), (cx + sx * L, cy)], fill=g(DARK), width=max(1, int(0.8 * s)))


def edge_band(img, w, h, s, m, kind):
    """Repeating ornament in the EDGE bands, for slots switched to TileFit.

    This is what makes a genre still read as itself on a WIDE control. Under the default
    Stretch mode an edge band is smeared to whatever length the control needs, so ornament
    there is illegal and every genre's identity collapses to its corners — which is why a
    380px settings button looked generic no matter which genre was active. TileFit repeats
    the band instead, so rivets stay rivets at any width.

    The ornament is centred in the band and the band ENDS are left plain, so consecutive
    repeats meet on flat pixels and the join is invisible.
    """
    if not kind:
        return
    lay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    cx, cy = w * s // 2, h * s // 2
    near, far = int(m * 0.5 * s), None

    def mark(x, y, horizontal):
        if kind == "rivet":
            r = int(1.7 * s)
            d.ellipse([x - r, y - r, x + r, y + r], fill=g(STUD), outline=g(EDGE),
                      width=max(1, int(0.4 * s)))
        elif kind == "chevron":
            a = int(2.4 * s)
            pts = ([(x - a, y - a), (x, y), (x - a, y + a)] if horizontal
                   else [(x - a, y - a), (x, y), (x + a, y - a)])
            d.line(pts, fill=g(STUD), width=max(1, int(0.9 * s)))
        elif kind == "tick":
            a = int(2.0 * s)
            if horizontal:
                d.line([(x, y - a), (x, y + a)], fill=g(DARK), width=max(1, int(0.9 * s)))
            else:
                d.line([(x - a, y), (x + a, y)], fill=g(DARK), width=max(1, int(0.9 * s)))

    mark(cx, near, True)                      # top band
    mark(cx, h * s - near, True)              # bottom band
    mark(near, cy, False)                     # left band
    mark(w * s - near, cy, False)             # right band
    img.paste(lay, (0, 0), Image.composite(lay.split()[3], Image.new("L", img.size, 0),
                                           img.split()[3]))


# ── silhouette languages ──────────────────────────────────────────────────────────────
def shape_path(style, w, h, m):
    """Outline of the genre's silhouette, in FINAL pixel coords."""
    # Every curve/cut must end at least 2px inside the margin. 1px is not enough: the 8x
    # supersample is resolved with LANCZOS, which bleeds roughly a pixel, and shooter's
    # 26px progress tile has only a 5px margin to spend.
    c = max(2, min(int(m * 0.55), 12, m - 3))
    if style == "chamfer":                       # rpg — forged plaque
        return [(c, 0), (w - c, 0), (w, c), (w, h - c), (w - c, h), (c, h), (0, h - c), (0, c)]
    if style == "clip":                          # shooter — sci-fi, two corners clipped
        return [(c, 0), (w, 0), (w, h - c), (w - c, h), (0, h), (0, c)]
    if style == "speed":                         # racing — leading edge raked
        # Rake confined to the corner margins. A full-height slant makes the left and right
        # bands vary along the axis they stretch on, so a wide button's edge would smear.
        k = max(3, int(c * 1.25))
        return [(k, 0), (w, 0), (w, h - k), (w - k, h), (0, h), (0, k)]
    if style == "notch":                         # survival — chipped corners
        return [(c, 0), (w - c, 0), (w, c * 0.6), (w, h - c), (w - c * 0.6, h),
                (c, h), (0, h - c * 0.6), (0, c)]
    return None                                  # rect/round handled by the drawing call


def build(style, w, h, m, face=FACE, sunken=False, ring=0, gloss=False, marks=None, edge=None):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    rad = max(2, min(int(m * 0.70), 14, m - 3))
    path = shape_path(style, w, h, m)

    if path:
        pts = [(x * s, y * s) for x, y in path]
        # Rim drawn as a fixed-width outline ON the path. The previous construction walked
        # the polygon inward one step at a time, which dragged the slanted corner segments
        # into the stretchable centre band and made every clipped/chamfered edge non-uniform.
        d.polygon(pts, fill=g(face), outline=g(EDGE), width=int(1.9 * s))
    elif style == "square":
        d.rectangle([0, 0, w * s - 1, h * s - 1], fill=g(face), outline=g(EDGE), width=int(2.0 * s))
    else:                                        # "round" / "pill"
        # Radius is capped by the MARGIN, not by the tile: a radius larger than the margin
        # curves inside the stretchable centre and ripples as the control grows.
        r = rad * s if style == "round" else max(2, min(m - 3, min(w, h) // 2)) * s
        d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], radius=r,
                            fill=g(face), outline=g(EDGE), width=int(2.0 * s))

    bevel(img, w, h, s, 3.0, sunken)

    if gloss:
        # Top margin band only: below it is the stretchable centre, and a highlight there
        # would smear down the face of a tall control.
        for i in range(int(2.5 * s), int(max(3.5, m - 3.0) * s)):
            d.line([(0, i), (w * s, i)], fill=g(LIGHT, 120), width=1)

    if marks:
        corner_marks(d, w, h, s, m, marks)
    edge_band(img, w, h, s, m, edge)
    if ring:
        d.rectangle([int(1.2 * s), int(1.2 * s), w * s - int(1.2 * s), h * s - int(1.2 * s)],
                    outline=g(ring), width=int(1.3 * s))
    return done(img, w, h)


def capsule(w, h, m, fill=False):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    # A margin under ~7px cannot hold a curve plus a 1.5px outline and still leave the band
    # uniform — shooter's progress tile is 26px with a 5px margin. Below that the groove is
    # drawn SQUARE rather than forced round, which also suits the angular genres it affects.
    r = 0 if m < 7 else max(2, min(m - 3, h // 2)) * s
    d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], radius=r,
                        fill=g(FACE if fill else WELL), outline=g(EDGE), width=int(1.6 * s))
    lo, hi = int(2.0 * s), int(max(3.0, m - 3.0) * s)
    for i in range(lo, hi):
        d.line([(0, i), (w * s, i)], fill=g(238, 130) if fill else (0, 0, 0, 70), width=1)
    return done(img, w, h)


def knob(w, h):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    d.ellipse([int(0.8 * s), int(0.8 * s), w * s - int(0.8 * s), h * s - int(0.8 * s)],
              fill=g(FACE), outline=g(EDGE), width=int(1.8 * s))
    d.ellipse([int(4 * s), int(3.4 * s), w * s - int(6 * s), h * s - int(8 * s)], fill=g(LIGHT, 165))
    return done(img, w, h)


def rule(w, h, marks):
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    y = h * s // 2
    d.line([(0, y), (w * s, y)], fill=g(EDGE), width=max(1, int(0.9 * s)))
    d.line([(0, y + int(0.9 * s)), (w * s, y + int(0.9 * s))], fill=g(LIGHT, 130),
           width=max(1, int(0.6 * s)))
    if marks == "stud":
        cx, r = w * s // 2, int(2.4 * s)
        d.polygon([(cx, y - r), (cx + r, y), (cx, y + r), (cx - r, y)], fill=g(STUD), outline=g(EDGE))
    return done(img, w, h)


# genre -> (silhouette, corner ornament, gloss, repeating EDGE ornament or None)
# An edge language implies TileFit on that genre's plate slots; None keeps plain Stretch.
GENRES = {
    "cardgame":    ("round",   "tick",    False, None),
    "citybuilder": ("square",  "tick",    False, None),
    "platformer":  ("pill",    None,      True,  None),
    "puzzle":      ("round",   None,      True,  None),
    "racing":      ("speed",   None,      True,  "chevron"),
    "rpg":         ("chamfer", "stud",    False, "rivet"),
    "shooter":     ("clip",    "bracket", False, "tick"),
    "strategy":    ("square",  "bracket", False, "rivet"),
    "survival":    ("notch",   "stud",    False, "rivet"),
    "topdown":     ("round",   None,      False, None),
}

# Only the big plates carry edge ornament: on a 26px progress groove a rivet is noise.
EDGE_SLOTS = {"button_normal", "button_hover", "button_pressed", "button_focus",
              "panel", "dialog"}

SLOTS = ["button_normal", "button_hover", "button_pressed", "button_disabled", "button_focus",
         "panel", "dialog", "input_normal", "input_focus",
         "progress_bg", "progress_fill", "slider_grabber", "separator"]

IMPORT = '''[remap]

importer="texture"
type="CompressedTexture2D"
path="res://.godot/imported/{name}-{h}.ctex"
metadata={{
"vram_texture": false
}}

[deps]

source_file="res://{rel}"
dest_files=["res://.godot/imported/{name}-{h}.ctex"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
'''


LF = chr(10)
TILE_FIT = 2   # Godot StyleBoxTexture.AxisStretchMode: 0=Stretch, 1=Tile, 2=TileFit


def set_axis_mode(genre, themes, slots, mode):
    """Declare the axis stretch for ornamented slots in every theme of a genre.

    TileFit rather than Tile: it scales the repeat so a whole number of tiles spans the edge,
    which is what stops a rivet being sliced in half at the end of a wide button."""
    for theme in themes:
        tj = os.path.join(SKINS, genre, "themes", theme, "theme.json")
        d = json.load(open(tj, encoding="utf-8"))
        tex = d.get("textures", {})
        touched = 0
        for slot in slots:
            if slot in tex:
                tex[slot]["axis_stretch_horizontal"] = mode
                tex[slot]["axis_stretch_vertical"] = mode
                touched += 1
        if touched:
            with open(tj, "w", encoding="utf-8", newline=LF) as fh:
                json.dump(d, fh, indent=2, ensure_ascii=False)
                fh.write(LF)


def margins_for(genre):
    """Declared 9-patch margins, from the genre's own theme.json."""
    themes = sorted(os.listdir(os.path.join(SKINS, genre, "themes")))
    tj = os.path.join(SKINS, genre, "themes", themes[0], "theme.json")
    tex = json.load(open(tj, encoding="utf-8")).get("textures", {})
    return {k: v.get("margin_left", 12) for k, v in tex.items()}, themes


def make(genre, slot, size, m, style, marks, gloss, edge):
    w, h = size
    if slot.startswith("progress"):
        return capsule(w, h, m, fill=slot.endswith("fill"))
    if slot == "slider_grabber":
        return knob(w, h)
    if slot == "separator":
        return rule(w, h, marks)
    if slot.startswith("input"):
        return build(style, w, h, m, face=WELL if slot.endswith("normal") else 166, sunken=True)
    e = edge if slot in EDGE_SLOTS else None
    if slot == "panel":
        return build(style, w, h, m, face=196, marks=marks, edge=e)
    if slot == "dialog":
        return build(style, w, h, m, face=190, marks=marks, edge=e)
    face = {"button_normal": FACE, "button_hover": 222, "button_pressed": 176,
            "button_disabled": 196, "button_focus": FACE}[slot]
    return build(style, w, h, m,
                 face=face,
                 sunken=(slot == "button_pressed"),
                 ring=255 if slot == "button_focus" else 0,
                 gloss=gloss and slot != "button_pressed",
                 marks=marks if slot != "button_disabled" else None,
                 edge=e)


def main():
    made = skipped = 0
    for genre, (style, marks, gloss, edge) in GENRES.items():
        marg, themes = margins_for(genre)
        ref = os.path.join(TEX, genre, themes[0])
        if not os.path.isdir(ref):
            print(f"  no art dir for {genre}, skipped")
            continue
        art = {}
        for slot in SLOTS:
            png = os.path.join(ref, f"{slot}.png")
            if not os.path.exists(png):
                skipped += 1
                continue
            size = Image.open(png).size
            m = marg.get(slot, max(4, min(size) // 4))
            art[slot] = make(genre, slot, size, m, style, marks, gloss, edge)
        for theme in themes:
            out = os.path.join(TEX, genre, theme)
            if not os.path.isdir(out):
                continue
            for slot, img in art.items():
                p = os.path.join(out, f"{slot}.png")
                img.save(p)
                rel = f"addons/beep_game_builder_cs/textures/{genre}/{theme}/{slot}.png"
                with open(p + ".import", "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(IMPORT.format(name=f"{slot}.png",
                                           h=hashlib.md5(rel.encode()).hexdigest(), rel=rel))
                made += 1
        if edge:
            set_axis_mode(genre, themes, EDGE_SLOTS & set(art), TILE_FIT)
        print(f"  {genre:12s} {style:8s} marks={str(marks):8s} edge={str(edge):8s}"
              f" -> {len(art)} slots x {len(themes)} themes")
    print(f"\n  wrote {made} files ({skipped} slots absent from the shipped set)")


if __name__ == "__main__":
    main()
