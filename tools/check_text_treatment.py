"""No kit widget may draw a string directly.

Every label must go through KitControl.DrawText or KitChrome.DrawText so a theme's
`text_treatment` reaches all of them. This check exists because the FONT ROLE already failed
exactly this way: it was wired into the derive-from-Godot drop-ins, every KitControl widget kept
calling GetThemeDefaultFont() directly, and four genres rendered identical type while the feature
looked finished. The proof it was broken was a warning count of zero, not a render.

A raw DrawString is invisible in review and silent at runtime, so it gets counted.
"""
import pathlib
import re
import sys

KIT = pathlib.Path(__file__).resolve().parent.parent / "addons/beep_game_builder_cs/ecs/ui/kit"
# The two files that IMPLEMENT the helper, and so must call DrawString.
ALLOWED = {"KitChrome.cs", "KitControl.cs"}

bad = 0
for f in sorted(KIT.glob("Kit*.cs")):
    if f.name in ALLOWED:
        continue
    text = f.read_text(encoding="utf-8", errors="ignore")
    for i, line in enumerate(text.splitlines(), 1):
        if re.search(r"(?<!Kit)(?<!\.)\bDrawString\(", line):
            print(f"FAIL {f.name}:{i} draws a string directly — use DrawText so the theme's "
                  f"text_treatment applies")
            bad += 1

print(f"\ntext treatment: {'every kit label routed' if bad == 0 else f'{bad} RAW DrawString call(s)'}")
print("PASS" if bad == 0 else "FAIL")
sys.exit(0 if bad == 0 else 1)
