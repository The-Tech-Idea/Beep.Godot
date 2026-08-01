"""A genre main instances the atmosphere IF AND ONLY IF its genre declares weather.

`genre.json` carries `tuning.enable_weather`, and `BeepGenreGenerator` reads it into
`GameInfo.EnableWeather` for a generated project. The genre MAIN SCENES have to agree: a scene
that instances `atmosphere.tscn` ships a weather system, whatever the genre says.

Today they agree exactly — the six genres declaring `true` are the six whose mains instance it.
Nothing enforced that, so flipping a flag or pasting the atmosphere into one more scene would
diverge silently: a puzzle game would grow rain, or a survival game would lose it, and the only
symptom is a scene that looks subtly wrong. Neither the compiler nor the scene validator can see
it, because both halves are individually valid.

Run:  python tools/check_genre_weather.py
"""
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SKINS = ROOT / "addons/beep_game_builder_cs/catalogs/skins"
SCENES = ROOT / "addons/beep_game_builder_cs/templates/scenes"

bad = checked = 0
for genre_file in sorted(SKINS.glob("*/genre.json")):
    data = json.loads(genre_file.read_text(encoding="utf-8"))
    gid = data.get("id", genre_file.parent.name)
    declared = bool(data.get("tuning", {}).get("enable_weather", False))

    main = SCENES / f"{gid}_main.tscn"
    if not main.exists():
        continue                       # not every genre ships a main scene
    checked += 1
    instanced = "atmosphere.tscn" in main.read_text(encoding="utf-8", errors="ignore")

    if declared == instanced:
        continue
    bad += 1
    if declared:
        print(f"FAIL {gid}: genre.json says enable_weather=true but {main.name} does NOT "
              f"instance atmosphere.tscn — the genre promises weather and the scene has none")
    else:
        print(f"FAIL {gid}: genre.json says enable_weather=false but {main.name} DOES "
              f"instance atmosphere.tscn — the scene ships weather the genre disclaims")

print(f"\ngenre weather: {checked} genre mains checked, "
      + ("all agree with their genre.json" if bad == 0 else f"{bad} MISMATCH(ES)"))
print("PASS" if bad == 0 else "FAIL")
sys.exit(0 if bad == 0 else 1)
