"""Author a `kit` block into every shipped theme.

Phase G of plans/game-ui-kit/PLAN_STYLE_SYSTEM.md. Each entry is a REGISTER read off the art
pass, not a taste call: the note names the reference family it comes from. The point is that the
five themes of one genre must differ MATERIALLY, not by hue -- which is the complaint the whole
style system exists to answer.

The UI kit is procedural-only: theme packs may change shadow, outline, corners, font,
tracking, shear, wobble, and selection cues, but they must not author texture or grain keys.
"""
import json, pathlib, sys

SKINS = pathlib.Path("addons/beep_game_builder_cs/catalogs/skins")

# genre -> theme -> (note, kit)
PACKS = {
"cardgame": {
  "arcane":   ("mystic glow used as the depth mechanism", dict(shadow="glow", outline_shade=1.72, font="heavy", upper_case=True, select_chip="glow|border")),
  "casino":   ("felt table: heavy weave, gold rim", dict(shadow="soft", outline_shade=2.00, select_slot="border|lift")),
  "paper":    ("papery family (art 04): fine sheet, typewriter", dict(shadow="soft", outline_shade=1.02, font="mono", tracking=0.04, corner_bar=0.50)),
  "royal":    ("carved/ornate: bright rim on stone", dict(shadow="hard", outline_shade=2.05, font="heavy", upper_case=True, select_slot="border|glow")),
  "velvet":   ("deep nap, thick dark outline", dict(shadow="soft", outline_shade=0.14, corner_panel=0.30)),
},
"citybuilder": {
  "eco":      ("living surface (art 22/28 foliage)", dict(shadow="soft", outline_shade=1.60, font="rounded", upper_case=False, corner_panel=0.22)),
  "future":   ("sci-fi sheets (14/43): sheared, glowing", dict(shadow="glow", outline_shade=1.90, font="condensed", tracking=0.10, shear=0.06, corner_bar=0.02, select_slot="glow|border")),
  "industrial": ("slab seen from above (art 35) on plate", dict(shadow="extrude", outline_shade=1.30, font="condensed", upper_case=True, corner=0.0, corner_panel=0.0)),
},
"platformer": {
  "cartoon":  ("casual arcade (art 12): wobble on rubber", dict(shadow="hard", wobble=0.020, corner=0.50, corner_panel=0.34)),
  "modern":   ("flat-translucent (art 03): no depth at all", dict(shadow="none", outline_shade=1.85, font="sans", upper_case=False, wobble=0.0, corner_panel=0.12)),
  "nature":   ("glossy leaf", dict(shadow="soft", wobble=0.014, font="rounded")),
  "pixel8bit": ("PIXEL register (art 40/42): 1px, stepped, bitmap", dict(shadow="none", outline_shade=0.10, font="pixel", tracking=0.0, wobble=0.0, corner=0.0, corner_panel=0.0, corner_slot=0.0, corner_bar=0.0, corner_chip=0.0)),
  "retro80s": ("neon: glow as the depth mechanism", dict(shadow="glow", outline_shade=1.95, font="condensed", upper_case=True, tracking=0.12, corner_bar=0.50, wobble=0.0, select_button="glow|underline")),
},
"puzzle": {
  "candy":    ("glossy arcade: soft, round, springy", dict(shadow="soft", wobble=0.020, corner=0.50, corner_panel=0.34, corner_slot=0.40, select_slot="fill|lift")),
  "cartoon":  ("thick DARK outline (the casual band inverted)", dict(shadow="hard", outline_shade=0.16, wobble=0.016, corner_panel=0.30)),
  "japan":    ("papery: fine sheet, no shadow, no caps", dict(shadow="none", outline_shade=1.02, font="mono", upper_case=False, tracking=0.06, corner_panel=0.06, wobble=0.0)),
  "modern":   ("flat neutral", dict(shadow="none", outline_shade=1.40, font="sans", upper_case=False, wobble=0.0, corner_panel=0.10)),
  "sea":      ("glossy leaf, glow select", dict(shadow="soft", corner=0.40, font="rounded", select_slot="glow|border")),
},
"racing": {
  "arcade":   ("hairline plus glow, heavy tracking", dict(shadow="glow", outline_shade=2.00, tracking=0.14, wobble=0.0, select_button="fill|glow")),
  "carbon":   ("diamond plate, slab depth", dict(shadow="extrude", outline_shade=1.20, shear=0.10)),
  "motorsport": ("brushed metal, hard offset", dict(shadow="hard", outline_shade=1.90, font="heavy", tracking=0.06)),
  "neon":     ("art 07: thin letter-spaced caps, max shear", dict(shadow="glow", outline_shade=2.05, tracking=0.16, shear=0.20, select_panel="glow|border")),
  "street":   ("dark asphalt, low shear, lower case", dict(shadow="hard", outline_shade=0.90, shear=0.06, font="sans", upper_case=False)),
},
"rpg": {
  "arcane":   ("gothic: blackletter is a KNOWN CC0 gap and warns", dict(shadow="glow", outline_shade=2.00, font="blackletter", select_slot="glow|border")),
  "darkfantasy": ("heavy plank, tight corners, hard shadow", dict(shadow="hard", outline_shade=1.40, corner=0.08, corner_panel=0.05)),
  "fantasy":  ("art 11 ornate: the register the genre is built on", dict(shadow="soft", outline_shade=1.90, select_slot="border|lift")),
  "parchment": ("papery/typewriter (art 45)", dict(shadow="soft", outline_shade=1.10, font="mono", tracking=0.03, corner_panel=0.06)),
  "royal":    ("ornate plus caps plus every selection cue", dict(shadow="soft", outline_shade=2.05, font="serif", upper_case=True, select_slot="glow|border|lift")),
},
"shooter": {
  "cyberpunk": ("art 43: sheared, glowing, wide tracking", dict(shadow="glow", outline_shade=2.05, tracking=0.18, shear=0.14, select_panel="glow|border")),
  "military": ("no shear, square, plate", dict(shadow="hard", outline_shade=1.20, shear=0.0, corner=0.0, corner_panel=0.0, corner_slot=0.0)),
  "scifi":    ("the sci-fi sheet at full strength", dict(shadow="glow", outline_shade=1.95, tracking=0.14)),
  "space":    ("art 09: hairline dark, mono, pill bars", dict(shadow="none", outline_shade=1.95, font="mono", tracking=0.10, corner_bar=0.50, shear=0.04)),
  "toxic":    ("glow as hazard, on plate", dict(shadow="glow", outline_shade=1.70, shear=0.12, select_button="glow|fill")),
},
"strategy": {
  "blueprint": ("drafting sheet (art 03/04): flat and papery", dict(shadow="none", outline_shade=1.02, font="mono", upper_case=False, tracking=0.05, corner_bar=0.50)),
  "command":  ("slab depth on plate", dict(shadow="extrude", outline_shade=1.50)),
  "military": ("leather map case, square", dict(shadow="hard", outline_shade=1.30, corner=0.0, corner_panel=0.0)),
  "royal":    ("ornate stone plus serif caps", dict(shadow="soft", outline_shade=2.05, font="serif", upper_case=True, corner_panel=0.10, select_slot="glow|border")),
  "scifi":    ("a sheared technical theme inside a carved genre", dict(shadow="glow", outline_shade=1.90, shear=0.08, tracking=0.12, corner_bar=0.02)),
},
"survival": {
  "apocalypse": ("scavenged plate, tight corners", dict(shadow="hard", outline_shade=1.10, font="condensed", upper_case=True, corner=0.04, corner_panel=0.03)),
  "desert":   ("sun-bleached stone", dict(shadow="soft", outline_shade=1.70, corner=0.10)),
  "frozen":   ("glow, fine frost, sans", dict(shadow="glow", outline_shade=2.00, font="sans", corner=0.12, select_slot="glow|border")),
  "industrial": ("slab plus diamond plate, square", dict(shadow="extrude", outline_shade=1.20, font="condensed", upper_case=True, corner=0.0, corner_panel=0.0)),
  "wilderness": ("art 13: wood-parchment, torn log frame", dict(shadow="soft", outline_shade=1.80, corner=0.14, select_slot="border|lift")),
},
"topdown": {
  "classic":  ("PIXEL register at its purest", dict(shadow="none", outline_shade=0.10, font="pixel", tracking=0.0, corner=0.0, corner_panel=0.0, corner_slot=0.0, corner_bar=0.0, corner_chip=0.0)),
  "fantasy":  ("leaves the pixel register entirely: serif on wood", dict(shadow="soft", outline_shade=1.60, font="serif", corner=0.16, corner_panel=0.12)),
  "japan":    ("papery, fine sheet", dict(shadow="none", outline_shade=1.02, font="mono", tracking=0.05, corner_panel=0.06)),
  "military": ("plate, square, condensed caps", dict(shadow="hard", outline_shade=1.20, font="condensed", upper_case=True, corner=0.0, corner_panel=0.0)),
  "nature":   ("glossy leaf, round", dict(shadow="soft", outline_shade=1.50, font="rounded", corner=0.22, corner_panel=0.20)),
},
}


def main():
    written = skipped = 0
    for genre, themes in PACKS.items():
        for theme, (note, kit) in themes.items():
            p = SKINS / genre / "themes" / theme / "theme.json"
            if not p.exists():
                print(f"MISSING {p}")
                sys.exit(2)
            raw = p.read_text(encoding="utf-8")
            if '"kit"' in raw:
                print(f"skip   {genre}/{theme} (already has a kit block)")
                skipped += 1
                continue
            body = json.dumps(kit, indent=4)[1:-1].rstrip()
            body = "\n".join("  " + ln for ln in body.splitlines())
            end = raw.rstrip()
            assert end.endswith("}"), p
            new = (end[:-1].rstrip().rstrip(",") + ",\n"
                   + f'  "_kit_note": "{note}",\n'
                   + '  "kit": {\n' + body + "\n  }\n}\n")
            json.loads(new)              # never write a file that will not parse
            p.write_text(new, encoding="utf-8", newline="\n")
            written += 1
    print(f"\nwrote {written} kit blocks, skipped {skipped}")


main()
