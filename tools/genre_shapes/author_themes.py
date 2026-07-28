"""
Author the theme layer for the six genres where it was dead.

cardgame, citybuilder, racing, rpg, strategy and survival each shipped FIVE themes whose
colours and geometry were byte-identical, so selecting a theme inside those genres changed
nothing at all — the genre->theme->palette cascade was genre->(nothing)->palette.

Each theme below is anchored on six deliberate colours plus its own geometry; the remaining
sixteen colour keys are derived with one shared set of rules so a theme stays internally
consistent (hover lifts, pressed sinks, disabled desaturates toward the panel). The existing
id / display_name / category / description / textures blocks are preserved untouched.
"""
import json, os, colorsys

ROOT = ("C:/Users/f_ald/source/repos/The-Tech-Idea/Beep.Godot/addons/"
        "beep_game_builder_cs/catalogs/skins")


# ── colour helpers ────────────────────────────────────────────────────────────────────
def hx(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) / 255 for i in (0, 2, 4))


def sx(c):
    return '#' + ''.join(f'{max(0, min(255, round(v * 255))):02X}' for v in c) + 'FF'


def lift(c, k):
    """Lighten toward white by k."""
    return tuple(v + (1 - v) * k for v in c)


def sink(c, k):
    """Darken toward black by k."""
    return tuple(v * (1 - k) for v in c)


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def desat(c, k):
    h, l, s = colorsys.rgb_to_hls(*c)
    return colorsys.hls_to_rgb(h, l, s * (1 - k))


def lum(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def build_colors(canvas, panel, surface, accent, accent2, text,
                 success, danger, warning, info):
    """Expand six anchors into the full 22-key schema."""
    cv, pn, sf = hx(canvas), hx(panel), hx(surface)
    ac, a2, tx = hx(accent), hx(accent2), hx(text)
    dark = lum(sf) < 0.5

    # A dark skin lifts on hover and sinks on press; a light skin does the reverse, or
    # hover on a near-white face is invisible.
    hover = lift(sf, 0.14) if dark else sink(sf, 0.06)
    press = sink(sf, 0.22) if dark else sink(sf, 0.14)
    disabled = mix(desat(sf, 0.65), pn, 0.45)

    return {
        "surface_primary":    sx(sf),
        "surface_hover":      sx(hover),
        "surface_pressed":    sx(press),
        "surface_disabled":   sx(disabled),
        "text_primary":       sx(tx),
        "text_hover":         sx(lift(tx, 0.25) if dark else sink(tx, 0.25)),
        "text_disabled":      sx(mix(tx, sf, 0.58)),
        "text_on_dark":       sx(lift(tx, 0.35)),
        "accent_primary":     sx(ac),
        "accent_secondary":   sx(a2),
        # The border is the accent pulled toward the surface, so it frames the control
        # instead of competing with the accent used for state.
        "border_normal":      sx(mix(ac, sf, 0.45)),
        "border_hover":       sx(ac),
        "border_focus":       sx(a2),
        "border_bevel_light": sx(lift(sf, 0.34)),
        "border_bevel_dark":  sx(sink(sf, 0.42)),
        "shadow_color":       sx(sink(cv, 0.55)),
        "bg_panel":           sx(pn),
        "bg_canvas":          sx(cv),
        "semantic_success":   success,
        "semantic_danger":    danger,
        "semantic_warning":   warning,
        "semantic_info":      info,
    }


def geom(radius, border, shadow, padx, pady, font, offy=None):
    return {
        "corner_radius": radius,
        "border_left": border, "border_top": border,
        "border_right": border, "border_bottom": border,
        "shadow_size": shadow,
        "shadow_offset_x": 0,
        "shadow_offset_y": shadow // 3 if offy is None else offy,
        "pad_left": padx, "pad_right": padx,
        "pad_top": pady, "pad_bottom": pady,
        "font_size": font,
    }


def anim(hs, hd, ps, pd, lift_shadow, glow):
    return {"hover_scale": hs, "hover_duration": hd, "press_scale": ps,
            "press_duration": pd, "shadow_lift": lift_shadow, "focus_glow": glow}


# semantic sets, chosen to sit inside each theme's own world rather than being one fixed
# traffic-light quartet pasted into all thirty
WARM = ("#4E9A4EFF", "#C0392BFF", "#E0A020FF", "#4A82C0FF")
COOL = ("#2ECC71FF", "#E74C3CFF", "#F1C40F FF".replace(' ', ''), "#3498DBFF")
NEON = ("#00FF9CFF", "#FF2D55FF", "#FFD500FF", "#00D4FFFF")
MUTED = ("#6B9E5FFF", "#A85248FF", "#C9A227FF", "#5B8AA6FF")
ICE = ("#5FD9A8FF", "#E0605FFF", "#EBC15AFF", "#6FC3E8FF")

# ── the thirty themes ─────────────────────────────────────────────────────────────────
# genre -> theme -> (canvas, panel, surface, accent, accent2, text, semantics, geometry, animation)
THEMES = {
    "cardgame": {
        "arcane":  ("#1A1030", "#241645", "#2E1C57", "#9B6BE8", "#4FD8E8", "#E8DCFF", NEON,
                    geom(10, 2, 10, 16, 9, 15), anim(1.05, 0.12, 0.96, 0.06, True, True)),
        "casino":  ("#0B3A22", "#0F4A2C", "#146138", "#E8C34A", "#D3453A", "#FFF4D6", WARM,
                    geom(8, 3, 8, 18, 10, 15), anim(1.04, 0.10, 0.97, 0.05, True, False)),
        "paper":   ("#EDE7DA", "#F6F1E7", "#FFFDF7", "#7A6A52", "#B08A46", "#2E2822", MUTED,
                    geom(3, 1, 3, 14, 8, 14), anim(1.02, 0.14, 0.98, 0.07, False, False)),
        "royal":   ("#101A3A", "#16244E", "#1D2E63", "#D8B24C", "#7A93D8", "#F2ECD8", WARM,
                    geom(6, 3, 10, 18, 10, 15), anim(1.04, 0.12, 0.97, 0.06, True, True)),
        "velvet":  ("#330E1A", "#4A1526", "#5E1B30", "#E0B550", "#D8607A", "#FBE9EC", WARM,
                    geom(12, 2, 12, 17, 10, 15), anim(1.05, 0.13, 0.96, 0.06, True, True)),
    },
    "citybuilder": {
        "blueprint": ("#0A2036", "#0E2C48", "#12385C", "#4FC3F7", "#B0E4FF", "#DCEFFF", COOL,
                      geom(2, 2, 4, 15, 8, 14), anim(1.02, 0.10, 0.98, 0.05, False, True)),
        "eco":       ("#E4EFE0", "#EFF6EC", "#F8FCF6", "#4A9E52", "#8CC63F", "#22331F", MUTED,
                      geom(12, 2, 6, 16, 9, 14), anim(1.03, 0.12, 0.97, 0.06, True, False)),
        "future":    ("#F2F6F8", "#FAFCFD", "#FFFFFF", "#00A6A6", "#5AD2D2", "#12232B", COOL,
                      geom(14, 1, 8, 18, 10, 15), anim(1.03, 0.09, 0.98, 0.05, True, True)),
        "industrial": ("#2A2A28", "#383834", "#454540", "#E09A2B", "#8C8C82", "#EDEBE4", WARM,
                       geom(2, 3, 5, 16, 9, 14), anim(1.02, 0.10, 0.97, 0.05, False, False)),
        "urban":     ("#DCE6EC", "#D6E0E6", "#E8EEF2", "#2E86AB", "#4FA66A", "#1B2B33", COOL,
                      geom(6, 2, 6, 16, 9, 14), anim(1.03, 0.11, 0.97, 0.06, True, False)),
    },
    "racing": {
        "arcade":     ("#1A0E2E", "#25143F", "#331A55", "#FFD400", "#FF3D7F", "#FFF6E0", NEON,
                       geom(10, 3, 10, 18, 10, 16), anim(1.06, 0.09, 0.95, 0.04, True, True)),
        "carbon":     ("#0C0C0E", "#141417", "#1C1C20", "#E02020", "#9AA0A6", "#F0F0F2", NEON,
                       geom(3, 2, 8, 16, 9, 14), anim(1.03, 0.08, 0.97, 0.04, True, False)),
        "motorsport": ("#F0F0F2", "#FFFFFF", "#E8E8EA", "#D31027", "#1A1A1E", "#141418", COOL,
                       geom(2, 3, 5, 17, 9, 15), anim(1.03, 0.08, 0.97, 0.04, False, False)),
        "neon":       ("#05050D", "#0A0A18", "#101024", "#FF00A8", "#00E5FF", "#EAF6FF", NEON,
                       geom(4, 2, 14, 16, 9, 15), anim(1.05, 0.09, 0.96, 0.04, True, True)),
        "street":     ("#1E1F22", "#2A2C30", "#36393E", "#FF7A18", "#4FA3D1", "#EDEFF2", WARM,
                       geom(6, 2, 7, 16, 9, 14), anim(1.04, 0.10, 0.96, 0.05, True, False)),
    },
    "rpg": {
        "arcane":      ("#150E2E", "#1E1440", "#291B55", "#A97BFF", "#4FE0D0", "#EDE4FF", NEON,
                        geom(8, 2, 11, 17, 10, 15), anim(1.04, 0.13, 0.97, 0.06, True, True)),
        "darkfantasy": ("#0E0B0C", "#171213", "#221A1C", "#B03030", "#7A6250", "#E6DAD2", WARM,
                        geom(4, 3, 12, 17, 10, 15), anim(1.03, 0.14, 0.97, 0.07, True, False)),
        "fantasy":     ("#4A321D", "#5C3E24", "#6E4A2B", "#D3A83D", "#7FA86B", "#F7E7C4", WARM,
                        geom(7, 3, 9, 17, 10, 15), anim(1.04, 0.12, 0.97, 0.06, True, False)),
        "parchment":   ("#E8DCC0", "#F2E9D2", "#FAF3E2", "#8A6A32", "#A8804A", "#3A2E1C", MUTED,
                        geom(5, 2, 4, 16, 9, 15), anim(1.02, 0.15, 0.98, 0.07, False, False)),
        "royal":       ("#141B3E", "#1C2652", "#243268", "#E0C05A", "#6E8AD8", "#F4EEDC", WARM,
                        geom(9, 3, 11, 18, 10, 16), anim(1.04, 0.12, 0.97, 0.06, True, True)),
    },
    "strategy": {
        "blueprint": ("#08243A", "#0C304C", "#103E62", "#40C4E8", "#9BD9EE", "#DCF0FA", COOL,
                      geom(2, 2, 4, 15, 8, 14), anim(1.02, 0.10, 0.98, 0.05, False, True)),
        "command":   ("#0A1410", "#101F18", "#162B21", "#3BE07A", "#8AD8A8", "#DFF7E8", NEON,
                      geom(2, 2, 6, 15, 8, 14), anim(1.02, 0.09, 0.98, 0.04, False, True)),
        "military":  ("#2A2E20", "#353A28", "#434935", "#C8A54A", "#7E8A5E", "#E9EBDC", MUTED,
                      geom(3, 3, 5, 16, 9, 14), anim(1.02, 0.11, 0.97, 0.05, False, False)),
        "royal":     ("#241540", "#2F1D54", "#3B2569", "#E2BE55", "#9B7ED8", "#F3ECDC", WARM,
                      geom(8, 3, 10, 18, 10, 15), anim(1.04, 0.12, 0.97, 0.06, True, True)),
        "scifi":     ("#050B14", "#09131F", "#0E1B2C", "#00D2FF", "#7A5CFF", "#E2F2FF", NEON,
                      geom(4, 2, 12, 16, 9, 15), anim(1.04, 0.09, 0.96, 0.04, True, True)),
    },
    "survival": {
        "apocalypse": ("#221A14", "#2E241B", "#3B2E22", "#D2691E", "#8A7A5C", "#EDE2D2", WARM,
                       geom(3, 3, 8, 16, 9, 14), anim(1.03, 0.12, 0.97, 0.06, True, False)),
        "desert":     ("#D9C49A", "#E6D5AE", "#F0E3C4", "#B5651D", "#7A9E8E", "#3A2E1E", MUTED,
                       geom(6, 2, 5, 16, 9, 15), anim(1.03, 0.13, 0.97, 0.06, False, False)),
        "frozen":     ("#D6E6F0", "#E4F0F8", "#F2F9FD", "#3E7FA8", "#8CC5E0", "#17303E", ICE,
                       geom(10, 2, 7, 17, 9, 15), anim(1.03, 0.12, 0.97, 0.06, True, True)),
        "industrial": ("#26262A", "#313136", "#3E3E44", "#E0B020", "#7A8A96", "#E8EAEE", WARM,
                       geom(2, 3, 6, 16, 9, 14), anim(1.02, 0.10, 0.97, 0.05, False, False)),
        "wilderness": ("#1E2A1A", "#283823", "#34472C", "#A8C24E", "#C9A227", "#E8F0DC", MUTED,
                       geom(7, 2, 8, 16, 9, 14), anim(1.03, 0.12, 0.97, 0.06, True, False)),
    },
}


def main():
    written = 0
    for genre, themes in THEMES.items():
        for name, (cv, pn, sf, ac, a2, tx, sem, g, a) in themes.items():
            path = os.path.join(ROOT, genre, "themes", name, "theme.json")
            if not os.path.exists(path):
                print(f"  MISSING, skipped: {genre}/{name}")
                continue
            d = json.load(open(path, encoding="utf-8"))
            d["colors"] = build_colors(cv, pn, sf, ac, a2, tx, *sem)
            d["geometry"] = g
            d["animation"] = a
            with open(path, "w", encoding="utf-8", newline="\n") as fh:
                json.dump(d, fh, indent=2, ensure_ascii=False)
                fh.write("\n")
            written += 1
    print(f"  authored {written} themes")


main()
