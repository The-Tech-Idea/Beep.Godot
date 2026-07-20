#!/usr/bin/env python
"""Write the `textures{}` block into every genre theme.json, per the import mapping.

Reuses MAP from import_kenney.py. Margins/axis/modulate come from the source pack family
(see FAM). Re-runnable: overwrites only the "textures" key, preserving the rest of each file.
Values are STARTING points — calibrate margins in the editor (Phase 2).
"""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from import_kenney import MAP, REPO  # noqa

SKINS = os.path.join(REPO, "addons", "beep_game_builder_cs", "catalogs", "skins")

# Per source-family nine-patch margins (L,T,R,B). Buttons stretch (ax 0) except pixel; panels
# tile (ax 1) for fantasy borders + pixel so ornate/pixel detail isn't smeared.
FAM = {
 "uipack":    {"bn": (20,20,20,28), "bp": (20,20,20,20), "pn": (16,16,16,16)},
 "adventure": {"bn": (18,18,18,18), "bp": (18,18,18,18), "pn": (24,24,24,24)},
 "scifi":     {"bn": (14,14,14,14), "bp": (14,14,14,14), "pn": (16,16,16,16)},
 "fantasy":   {"bn": (18,18,18,18), "bp": (18,18,18,18), "pn": (28,28,28,28)},
 "pixel":     {"bn": (6,6,6,6),     "bp": (6,6,6,6),     "pn": (6,6,6,6)},
}

def block(genre, theme, fam):
    base = f"res://addons/beep_game_builder_cs/textures/{genre}/{theme}"
    f = FAM[fam]
    btn_ax = 1 if fam == "pixel" else 0
    panel_ax = 1 if fam in ("fantasy", "pixel") else 0
    # Kenney ships one button per color for adventure/scifi/fantasy/pixel, so "pressed" darkens
    # via modulate. UI Pack has a real flat (pressed) texture, so no darken needed there.
    press_mod = "#FFFFFFFF" if fam == "uipack" else "#CFCFCFFF"
    def slot(fname, m, ax, mod="#FFFFFFFF"):
        return {
            "texture_path": f"{base}/{fname}",
            "margin_left": m[0], "margin_top": m[1], "margin_right": m[2], "margin_bottom": m[3],
            "axis_stretch_horizontal": ax, "axis_stretch_vertical": ax,
            "draw_center": True, "modulate": mod,
        }
    return {
        "button_normal":   slot("button_normal.png",  f["bn"], btn_ax),
        "button_hover":    slot("button_hover.png",    f["bn"], btn_ax),
        "button_pressed":  slot("button_pressed.png",  f["bp"], btn_ax, press_mod),
        "button_disabled": slot("button_normal.png",   f["bn"], btn_ax, "#FFFFFF80"),
        "panel":           slot("panel.png",           f["pn"], panel_ax),
    }

def main():
    done = missing = 0
    for (genre, theme), desc in MAP.items():
        fam = desc.split(":")[0]
        path = os.path.join(SKINS, genre, "themes", theme, "theme.json")
        if not os.path.isfile(path):
            print("  NO theme.json:", genre, theme); missing += 1; continue
        with open(path, encoding="utf-8") as fh:
            data = json.load(fh)
        data["textures"] = block(genre, theme, fam)
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(data, fh, indent=2, ensure_ascii=False)
            fh.write("\n")
        done += 1
    print(f"stamped={done} missing={missing}")

if __name__ == "__main__":
    main()
