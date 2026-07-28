#!/usr/bin/env python
"""Import real Kenney art into the 14 per-component HUD slots.

Companion to import_kenney.py (which does the menu slots) — same source pack, same opt-in
model: nothing ships in the addon, the developer runs this against their own CC0 copy.

HUD art is per GENRE, not per theme: shape/border/shadow belong to the genre, and the five
themes inside it recolour via StyleBoxTexture.modulate declared in theme.json.
See docs/HUD_TEXTURE_SYSTEM.md.

Destination is textures/hud/<genre>/<slot>.png — exactly where theme.json already points, so
this REPLACES the generated fallback masters in place. Any slot with no matching source art is
left alone, so it keeps its generated master rather than going blank.

    python import_kenney_hud.py
"""
import hashlib, os, shutil

SRC = r"H:\GameDev\GFX\GameAssets\Kenney Game Assets All-in-1 3.6.0\UI assets"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Destination defaults to the ADDON only if you ask for it. The addon is meant to ship the
# feature, not the art (Phase 1), so importing third-party art into it by default is exactly
# the mistake this variable exists to prevent — pass --dest to target YOUR game project:
#
#     python import_kenney_hud.py --dest "C:/.../GodotGames/new-game-project/ui_textures"
#
# then point the dock at it: Game tab §5 -> "My own textures" -> that folder.
# Anything not supplied there falls back per slot to the addon's generated masters.
DEFAULT_DEST = os.path.join(REPO, "addons", "beep_game_builder_cs", "textures")
DEST = DEFAULT_DEST


def _parse_dest():
    global DEST
    import argparse
    ap = argparse.ArgumentParser(description="Import Kenney art into the 14 HUD slots.")
    ap.add_argument("--dest", default=None,
                    help="Target textures folder. Default: the addon (not recommended — "
                         "prefer a folder inside your own game project).")
    a = ap.parse_args()
    if a.dest:
        DEST = a.dest
    return DEST


def first_existing(*cands):
    for c in cands:
        if c and os.path.isfile(c):
            return c
    return None


# genre -> source family. Chosen for HUD suitability, not menu suitability: the sci-fi pack's
# glass panels are translucent, which is what a HUD plate needs and what a menu plate does not.
HUD_MAP = {
    # The four sci-fi genres take DIFFERENT colours and DIFFERENT header cuts, or they end up
    # visually identical — the whole point of per-genre art.
    "citybuilder": "scifi:Grey:large",     # civic, blunt header
    "strategy":    "scifi:Blue:notch",     # command, notched
    "shooter":     "scifi:Grey:blade",     # tactical, angular blade
    "racing":      "scifi:Red:small",      # compact, low-profile
    "topdown":     "uipack:Grey:flat",
    "puzzle":      "uipack:Blue:gloss",
    "platformer":  "uipack:Green:gloss",
    "rpg":         "fantasy:015:brown",
    "cardgame":    "fantasy:008:brown",
    "survival":    "adventure:grey",
}

HUD_SLOTS = ["panel", "button_normal", "button_hover", "button_pressed", "button_disabled",
             "button_focus", "tab_normal", "tab_selected", "slot_empty", "slot_filled",
             "bar_bg", "bar_fill", "frame", "tooltip"]


def A(root, rel):
    return os.path.join(SRC, root, "PNG", rel)


def adv(rel):
    return os.path.join(SRC, "UI Adventure Pack", "PNG", rel)


def square(color, style):
    """UI Pack squares serve as inventory slots for every family — no other pack ships a
    square cell, and a slot must be square or the item icon inside it distorts."""
    return first_existing(A("UI Pack", f"{color}\\Default\\button_square_depth_{style}.png"),
                          A("UI Pack", "Grey\\Default\\button_square_depth_flat.png"))


def project_root_for(dest):
    """Directory holding project.godot — the anchor every res:// path is relative to."""
    d = os.path.abspath(dest)
    while True:
        if os.path.isfile(os.path.join(d, "project.godot")):
            return d
        parent = os.path.dirname(d)
        if parent == d:
            return None
        d = parent


def catalog_root_for(dest):
    """Walk up from the destination to find the owning project's skin catalog."""
    d = os.path.abspath(dest)
    while True:
        cand = os.path.join(d, "addons", "beep_game_builder_cs", "catalogs", "skins")
        if os.path.isdir(cand):
            return cand
        parent = os.path.dirname(d)
        if parent == d:
            return os.path.join(REPO, "addons", "beep_game_builder_cs", "catalogs", "skins")
        d = parent


def resolve_hud(desc):
    p = desc.split(":")
    kind = p[0]

    if kind == "scifi":
        C = p[1] if len(p) > 1 else "Grey"
        cut = p[2] if len(p) > 2 else "large"      # blade | large | notch | small
        R = "UI Pack - Sci-fi"
        glass = A(R, "glassPanel.png")
        metal = first_existing(A(R, f"metalPanel_{C.lower()}.png"), A(R, "metalPanel.png"))

        def hdr(shape, screws=False):
            s = "_screws" if screws else ""
            return A(R, f"{C}\\Default\\button_square_header_{cut}_{shape}{s}.png")

        # Bars are 3-slice in this pack; the _mid slice is the stretchable body and is the
        # correct 9-patch source. bar_round_*_square is an END CAP — using it as the track
        # rendered a small circle instead of a bar.
        bar_col = C.lower() if C.lower() in ("blue", "green", "red", "yellow") else "white"
        return {
            "panel":           first_existing(glass if C == "Grey" else metal, glass),
            "button_normal":   first_existing(hdr("rectangle"), metal),
            "button_hover":    first_existing(hdr("rectangle", True), hdr("rectangle"), metal),
            "button_pressed":  first_existing(A(R, "metalPanel_plate.png"), metal),
            "button_disabled": first_existing(A(R, "metalPanel.png"), metal),
            "button_focus":    first_existing(A(R, "glassPanel_corners.png"), glass),
            "tab_normal":      first_existing(A(R, "glassPanel_tab.png"), glass),
            "tab_selected":    first_existing(A(R, "glassPanel_projection.png"), A(R, "glassPanel_tab.png")),
            "slot_empty":      first_existing(hdr("square"), square(C, "flat")),
            "slot_filled":     first_existing(hdr("square", True), square(C, "border")),
            "bar_bg":          first_existing(A(R, "barHorizontal_shadow_mid.png")),
            "bar_fill":        first_existing(A(R, f"barHorizontal_{bar_col}_mid.png"),
                                              A(R, "barHorizontal_blue_mid.png")),
            "frame":           first_existing(A(R, "glassPanel_corners.png"), glass),
            "tooltip":         first_existing(A(R, "metalPanel_plate.png"), glass),
        }

    if kind == "uipack":
        C, style = p[1], p[2]
        R = "UI Pack"
        rect_d = A(R, f"{C}\\Default\\button_rectangle_depth_{style}.png")
        rect = A(R, f"{C}\\Default\\button_rectangle_{style}.png")
        border = A(R, f"{C}\\Default\\button_rectangle_border.png")
        return {
            "panel":           first_existing(A(R, f"{C}\\Default\\button_square_depth_flat.png"),
                                              A(R, "Grey\\Default\\button_square_depth_flat.png")),
            "button_normal":   rect_d,
            "button_hover":    first_existing(A(R, f"{C}\\Default\\button_rectangle_depth_gloss.png"), rect_d),
            "button_pressed":  rect,
            "button_disabled": first_existing(A(R, "Grey\\Default\\button_rectangle_flat.png"), rect),
            "button_focus":    border,
            "tab_normal":      first_existing(A(R, f"{C}\\Default\\button_rectangle_flat.png"), rect),
            "tab_selected":    rect_d,
            "slot_empty":      square(C, "flat"),
            "slot_filled":     square(C, "border"),
            "bar_bg":          first_existing(adv("barBack_horizontalMid.png"),
                                              A("UI Pack - Sci-fi", "Grey\\Default\\bar_round_large_square.png")),
            "bar_fill":        first_existing(adv(f"bar{C}_horizontalMid.png"),
                                              adv("barGreen_horizontalMid.png")),
            "frame":           border,
            "tooltip":         first_existing(A(R, f"{C}\\Default\\button_rectangle_flat.png"), rect),
        }

    if kind in ("adventure", "fantasy"):
        tone = p[-1]
        R = "UI Pack - Adventure"
        btn = A(R, f"Default\\button_{tone}.png")
        panel = first_existing(A(R, f"Default\\panel_{tone}.png"), A(R, "Default\\panel_brown.png"))
        border = first_existing(A(R, f"Default\\panel_border_{tone}_detail.png"),
                                A(R, f"Default\\panel_border_{tone}.png"),
                                A(R, "Default\\panel_border_brown.png"))
        frame = border
        if kind == "fantasy":
            frame = first_existing(A("Fantasy UI Borders", f"Default\\Border\\panel-border-{p[1]}.png"), border)
        return {
            "panel":           panel,
            "button_normal":   btn,
            "button_hover":    first_existing(A(R, "Default\\button_beige.png"), btn),
            "button_pressed":  first_existing(A(R, f"Default\\panel_{tone}_dark.png"), btn),
            "button_disabled": first_existing(A(R, "Default\\button_grey.png"), btn),
            "button_focus":    border,
            "tab_normal":      first_existing(A(R, f"Default\\panel_{tone}_dark.png"), panel),
            "tab_selected":    panel,
            # A checkbox is round and carries a tick — it reads as "on/off", not as a cell an
            # item icon sits in. Inventory slots take the square cell instead.
            "slot_empty":      first_existing(square("Grey", "flat"),
                                              A(R, f"Default\\checkbox_{tone}_empty.png")),
            "slot_filled":     first_existing(square(tone.capitalize(), "border"),
                                              square("Grey", "border")),
            "bar_bg":          adv("barBack_horizontalMid.png"),
            "bar_fill":        first_existing(adv("barRed_horizontalMid.png"),
                                              adv("barGreen_horizontalMid.png")),
            "frame":           frame,
            "tooltip":         first_existing(A(R, f"Default\\panel_{tone}_dark.png"), panel),
        }

    raise ValueError("bad hud desc " + desc)


SIDECAR = '''[remap]

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
    _parse_dest()
    if not os.path.isdir(SRC):
        print(f"Source pack not found: {SRC}\nEdit SRC at the top of this file.")
        return
    if os.path.abspath(DEST) == os.path.abspath(DEFAULT_DEST):
        print("  NOTE: importing into the ADDON. Phase 1 says the addon ships the feature, not\n"
              "        the art. Pass --dest <your project folder> to keep it out of the addon.")
    copied = missing = 0
    misses = []
    for genre, desc in HUD_MAP.items():
        slots = resolve_hud(desc)
        outdir = os.path.join(DEST, "hud", genre)
        os.makedirs(outdir, exist_ok=True)
        for slot in HUD_SLOTS:
            src = slots.get(slot)
            dst = os.path.join(outdir, slot + ".png")
            if src and os.path.isfile(src):
                shutil.copyfile(src, dst)
                # res:// path of the file we just wrote, relative to ITS OWN project — not a
                # hardcoded addon path. Getting this wrong makes every sidecar name a file it
                # is not, Godot refuses the import, and ResolvePath silently falls back to
                # built-in art with no error anywhere.
                proot = project_root_for(dst)
                rel = (os.path.relpath(dst, proot) if proot
                       else os.path.join("addons", "beep_game_builder_cs", "textures", "hud",
                                         genre, slot + ".png")).replace("\\", "/")
                h = hashlib.md5(rel.encode()).hexdigest()
                with open(dst + ".import", "w", encoding="utf-8", newline="\n") as f:
                    f.write(SIDECAR.format(name=slot + ".png", h=h, rel=rel))
                copied += 1
            else:
                missing += 1
                misses.append(f"hud/{genre}/{slot}  <- {desc}")
    print(f"HUD: copied={copied} missing={missing} "
          f"(missing slots keep their generated master, so nothing goes blank)")
    for m in misses:
        print("  MISS:", m)
    neutralise_modulate({g for g in HUD_MAP})


# Alpha per slot once the art is real. Opaque source art still has to let the world through,
# and that is the only thing modulate should be doing now.
REAL_ALPHA = {
    "panel": 0.92, "tooltip": 0.96, "frame": 1.00,
    "button_normal": 0.94, "button_hover": 1.00, "button_pressed": 1.00,
    "button_disabled": 0.55, "button_focus": 1.00,
    "tab_normal": 0.88, "tab_selected": 1.00,
    "slot_empty": 0.85, "slot_filled": 1.00,
    "bar_bg": 0.85, "bar_fill": 1.00,
}


def neutralise_modulate(genres):
    """Rewrite hud_* modulate to white-with-alpha for genres whose art is now real.

    The baked masters are GREYSCALE, so theme.json carries a per-theme accent in modulate to
    colour them. Kenney art is already coloured — multiplying it by that same accent tints it
    a second time and muddies it. Real art wants white modulate, with alpha left doing the one
    job a HUD still needs: letting the game show through.
    """
    import glob, json
    n = 0
    # Rewrite the catalog belonging to whatever project we imported INTO — not always the
    # addon. The addon keeps accent modulate for its generated greyscale masters; the project
    # that now holds real art gets white. Getting this backwards double-tints one of them.
    root = catalog_root_for(DEST)
    for tj in sorted(glob.glob(os.path.join(root, "*", "themes", "*", "theme.json"))):
        genre = tj.replace("\\", "/").split("/")[-4]
        if genre not in genres:
            continue
        data = json.load(open(tj, encoding="utf-8"))
        tex = data.get("textures", {})
        changed = False
        for slot, a in REAL_ALPHA.items():
            key = f"hud_{slot}"
            if key in tex:
                tex[key]["modulate"] = "#FFFFFF%02X" % int(255 * a)
                changed = True
        # ── calibrate 9-patch margins to the ACTUAL art ──────────────────────────────
        # The bake script writes a flat margin=30 because its masters are all 128x128. Real
        # art is 18x18 to 192x64, and a 30px margin on a 64px source leaves a 4px stretchable
        # centre — the button renders as two stacked blocks instead of one plate.
        genre_dir = os.path.join(DEST, "hud", genre)
        for slot in HUD_SLOTS:
            key = f"hud_{slot}"
            png = os.path.join(genre_dir, slot + ".png")
            if key not in tex or not os.path.isfile(png):
                continue
            w, h = _png_size(png)
            if not w:
                continue
            mx, my = _margin(w), _margin(h)
            tex[key]["margin_left"] = tex[key]["margin_right"] = mx
            tex[key]["margin_top"] = tex[key]["margin_bottom"] = my

            # The sci-fi pack's buttons carry a BAKED HEADER BAND across their top. With a
            # centred label the text straddles that seam ("House x2 / 1,200" split down the
            # middle). The band lives inside the fixed top 9-patch margin, so pushing the
            # content margin past it drops the label into the body where it belongs.
            if HUD_MAP.get(genre, "").startswith("scifi") and (
                    slot.startswith("button_") or slot.startswith("slot_")):
                tex[key]["content_margin_top"] = my + 2

            changed = True

        if changed:
            json.dump(data, open(tj, "w", encoding="utf-8", newline="\n"), indent=2)
            open(tj, "a", encoding="utf-8", newline="\n").write("\n")
            n += 1
    print(f"  modulate + 9-patch margins calibrated in {n} theme.json "
          f"(white modulate; margins derived from each source's real pixel size)")


def _margin(dim):
    """9-patch margin for one axis: enough to hold the art's border, but always leaving a
    stretchable centre. Never more than half the dimension minus a pixel, or the centre
    collapses and the texture smears."""
    return max(1, min(int(dim * 0.28), dim // 2 - 1, 28))


def _png_size(path):
    """Width/height straight from the PNG IHDR — avoids a hard dependency on PIL here."""
    try:
        with open(path, "rb") as f:
            head = f.read(24)
        if head[:8] != b"\x89PNG\r\n\x1a\n":
            return (0, 0)
        return (int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big"))
    except OSError:
        return (0, 0)


if __name__ == "__main__":
    main()
