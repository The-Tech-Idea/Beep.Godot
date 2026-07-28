"""
Author the RPG genre's own component SILHOUETTES.

Until now every genre drew the same rounded rectangle: only colour, corner radius, border
weight and shadow changed, so an RPG button and a racing button were the same object in two
colours. The reference kits do the opposite — the SHAPE is what identifies the genre.

Model this establishes:
    genre  -> the silhouette (this file)
    theme  -> the colour identity
    palette-> the tint applied on top

so ONE silhouette per genre is written into all five of its theme folders, and the five look
different because the themes now carry different colours (they did not before).

Everything is drawn NEUTRAL GREYSCALE around a 0.80 mid-tone, because the art register
multiplies the 9-patch by the palette colour: art(0.80) x modulate(surface/0.80) = surface.
Baked bevel, outline and ornament survive that multiply; a coloured source would not.

9-patch discipline: all ornament lives strictly inside the corner margins, and every edge
band is CONSTANT along the axis it stretches on. A stud in the middle of an edge would smear
across the whole side of a stretched control.
"""
import os
from PIL import Image, ImageDraw

ADDON = ("C:/Users/f_ald/source/repos/The-Tech-Idea/Beep.Godot/addons/beep_game_builder_cs")
THEMES = ["arcane", "darkfantasy", "fantasy", "parchment", "royal"]

SS = 8                    # supersample

# Neutral greyscale register. FACE is the 0.80 mid-tone the modulate maths assumes.
FACE = 204
EDGE = 90                 # outer outline
LIGHT = 246               # top-left bevel
DARK = 138                # bottom-right bevel
WELL = 150                # recessed interiors
STUD = 236


def g(v, a=255):
    return (v, v, v, a)


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def done(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def chamfer(w, h, cut):
    """Octagon path — the cut-corner plaque that reads as forged metal rather than a CSS box."""
    return [(cut, 0), (w - cut, 0), (w, cut), (w, h - cut),
            (w - cut, h), (cut, h), (0, h - cut), (0, cut)]


def plaque(w, h, cut, m, face=FACE, sunken=False, studs=True, ring=0):
    """Chamfered plaque with a bevelled rim. `m` is the 9-patch margin: nothing decorative
    may cross it, or it smears when the control stretches."""
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    pts = [(x * s, y * s) for x, y in chamfer(w, h, cut)]
    d.polygon(pts, fill=g(face), outline=g(EDGE))

    # Rim: 2px band inset from the silhouette. Drawn as an outline pass so the corners
    # follow the chamfer instead of a rectangle poking through it.
    inner = [(x * s, y * s) for x, y in chamfer(w, h, cut)]
    for i in range(int(2.2 * s)):
        k = i / (2.2 * s)
        d.line(inner + [inner[0]], fill=g(EDGE), width=max(1, int(0.5 * s)))
        inner = [(x + (w * s / 2 - x) * 0.012, y + (h * s / 2 - y) * 0.012) for x, y in inner]

    # Bevel: light along the top/left, dark along the bottom/right, inside the rim.
    b = int(3.2 * s)
    top_y, bot_y = int(3.0 * s), h * s - int(3.0 * s)
    lx, rx = int(3.0 * s), w * s - int(3.0 * s)
    hi, lo = (DARK, LIGHT) if sunken else (LIGHT, DARK)
    d.line([(lx + b, top_y), (rx - b, top_y)], fill=g(hi), width=int(1.6 * s))
    d.line([(lx, top_y + b), (lx, bot_y - b)], fill=g(hi), width=int(1.6 * s))
    d.line([(lx + b, bot_y), (rx - b, bot_y)], fill=g(lo), width=int(1.6 * s))
    d.line([(rx, top_y + b), (rx, bot_y - b)], fill=g(lo), width=int(1.6 * s))

    # Corner studs — inside the margin box, so they survive 9-patch stretching.
    if studs:
        r = int(1.9 * s)
        off = int(min(m * 0.42, cut * 0.9) * s)
        for cx, cy in ((off, off), (w * s - off, off), (off, h * s - off), (w * s - off, h * s - off)):
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=g(STUD), outline=g(EDGE),
                      width=max(1, int(0.4 * s)))

    if ring:
        d.polygon(pts, outline=g(ring), width=int(1.4 * s))
    return done(img, w, h)


def well(w, h, cut, m, face=WELL):
    """Recessed input: sunken bevel plus an inner shadow under the top edge."""
    img = plaque(w, h, cut, m, face=face, sunken=True, studs=False)
    big = img.resize((w * SS, h * SS), Image.NEAREST)
    d = ImageDraw.Draw(big)
    s = SS
    for i in range(int(2.5 * s)):
        a = int(70 * (1 - i / (2.5 * s)))
        d.line([(int(4.5 * s), int(4.5 * s) + i), (w * s - int(4.5 * s), int(4.5 * s) + i)],
               fill=(0, 0, 0, a), width=1)
    return done(big, w, h)


def capsule(w, h, m, fill=False):
    """Progress groove / fill — a capsule, with a gloss band on the fill so a full bar
    reads as a filled object rather than a coloured rectangle."""
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    # Radius must not exceed the 9-patch margin, or the round end bleeds into the
    # stretchable centre band and the bar's top edge ripples as it grows.
    r = m * s
    d.rounded_rectangle([0, 0, w * s - 1, h * s - 1], radius=r,
                        fill=g(FACE if fill else WELL), outline=g(EDGE), width=int(1.6 * s))
    if fill:
        # Gloss drawn as horizontal scanlines, the same construction as the groove's inner
        # shadow below. A filled rect composited over the capsule's transparent corners left
        # a faint halo whose alpha varied along x, which is a smear once the bar stretches.
        # Confined to the TOP MARGIN band. Below it lies the centre, which stretches
        # vertically — a gloss reaching into it would smear down the whole face of a tall bar.
        for i in range(int(2.5 * s), int((m - 3.5) * s)):
            d.line([(0, i), (w * s, i)], fill=g(238, 130), width=1)
    else:
        for i in range(int(2.2 * s)):
            a = int(80 * (1 - i / (2.2 * s)))
            d.line([(0, int(2 * s) + i), (w * s, int(2 * s) + i)], fill=(0, 0, 0, a), width=1)
    return done(img, w, h)


def stud_round(w, h):
    """Slider grabber: a domed rivet."""
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    d.ellipse([int(0.8 * s), int(0.8 * s), w * s - int(0.8 * s), h * s - int(0.8 * s)],
              fill=g(FACE), outline=g(EDGE), width=int(1.8 * s))
    d.ellipse([int(4 * s), int(3.4 * s), w * s - int(6 * s), h * s - int(8 * s)], fill=g(LIGHT, 165))
    d.ellipse([int(6 * s), int(6 * s), w * s - int(6 * s), h * s - int(6 * s)],
              outline=g(DARK, 120), width=max(1, int(0.7 * s)))
    return done(img, w, h)


def rule(w, h):
    """Separator: a tapered bar with a centre diamond — the divider every fantasy kit uses."""
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    s = SS
    y = h * s // 2
    d.line([(0, y), (w * s, y)], fill=g(EDGE), width=max(1, int(0.9 * s)))
    d.line([(0, y + int(0.9 * s)), (w * s, y + int(0.9 * s))], fill=g(LIGHT, 130),
           width=max(1, int(0.6 * s)))
    cx, r = w * s // 2, int(2.4 * s)
    d.polygon([(cx, y - r), (cx + r, y), (cx, y + r), (cx - r, y)], fill=g(STUD), outline=g(EDGE))
    return done(img, w, h)


# slot -> (size, builder)
SLOTS = {
    "button_normal":   ((60, 60), lambda: plaque(60, 60, 11, 18)),
    "button_hover":    ((60, 60), lambda: plaque(60, 60, 11, 18, face=222, ring=STUD)),
    "button_pressed":  ((60, 60), lambda: plaque(60, 60, 11, 18, face=176, sunken=True)),
    "button_disabled": ((60, 60), lambda: plaque(60, 60, 11, 18, face=196, studs=False)),
    "button_focus":    ((60, 60), lambda: plaque(60, 60, 11, 18, ring=255)),
    "panel":           ((56, 56), lambda: plaque(56, 56, 10, 16, face=196)),
    "dialog":          ((64, 64), lambda: plaque(64, 64, 13, 20, face=190)),
    "input_normal":    ((50, 50), lambda: well(50, 50, 7, 15)),
    "input_focus":     ((50, 50), lambda: well(50, 50, 7, 15, face=166)),
    "progress_bg":     ((38, 38), lambda: capsule(38, 38, 11)),
    "progress_fill":   ((38, 38), lambda: capsule(38, 38, 11, fill=True)),
    "slider_grabber":  ((32, 32), lambda: stud_round(32, 32)),
    "separator":       ((16, 8), lambda: rule(16, 8)),
}

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


def main():
    import hashlib
    made = 0
    art = {slot: fn() for slot, (_, fn) in SLOTS.items()}
    for theme in THEMES:
        out = os.path.join(ADDON, "textures", "rpg", theme)
        os.makedirs(out, exist_ok=True)
        for slot, img in art.items():
            path = os.path.join(out, f"{slot}.png")
            img.save(path)
            rel = f"addons/beep_game_builder_cs/textures/rpg/{theme}/{slot}.png"
            h = hashlib.md5(rel.encode()).hexdigest()
            with open(path + ".import", "w", encoding="utf-8", newline="\n") as fh:
                fh.write(IMPORT.format(name=f"{slot}.png", h=h, rel=rel))
            made += 1
    print(f"  wrote {made} files ({len(art)} slots x {len(THEMES)} themes)")


main()
