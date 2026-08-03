"""The lightning flash must have a RETURN STROKE, not just a decay.

A real strike is a primary bolt followed by a second peak down the same ionised channel a few
hundredths of a second later. That second peak is what the eye reads as lightning; without it the
overlay is a lamp being switched off.

The shipped envelope was `1.0 -> 0.6 -> 0.3 -> 0` — monotonically decreasing, with its third step
commented "secondary". A step DIMMER than the one before it is not a secondary strike, and nothing
caught that for as long as the comment claimed otherwise. Prose in a comment is not a check.

So this asserts the shape rather than the wording:

  * times strictly increase          -- FlashEnvelope divides by (t1 - t0)
  * it starts and ends dark          -- an envelope that ends lit never releases the screen
  * SOME key rises after a fall      -- the return stroke, i.e. NOT monotonic
  * the tail is longer than the rise -- a storm re-darkens slowly; a symmetric blip reads as a glitch

Run:  python tools/check_lightning_envelope.py
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "addons/beep_game_builder_cs/ecs/atmosphere/WeatherSystemComponent.cs"

text = SRC.read_text(encoding="utf-8")
block = re.search(r"FlashKeys\s*=\s*\{(.*?)\};", text, re.S)
if not block:
    print("FAIL could not find the FlashKeys table in WeatherSystemComponent.cs")
    sys.exit(1)

keys = [(float(t), float(v)) for t, v in
        re.findall(r"\(\s*([0-9.]+)f\s*,\s*([0-9.]+)f\s*\)", block.group(1))]

fails = []
if len(keys) < 4:
    fails.append(f"only {len(keys)} keys — a double flash needs at least rise/dip/peak/fall")

times = [t for t, _ in keys]
vals = [v for _, v in keys]

if any(b <= a for a, b in zip(times, times[1:])):
    fails.append(f"timestamps are not strictly increasing: {times} — FlashEnvelope divides by the gap")
if vals and vals[0] != 0.0:
    fails.append(f"starts at {vals[0]}, not dark — the screen is lit before the bolt")
if vals and vals[-1] != 0.0:
    fails.append(f"ends at {vals[-1]}, not dark — the flash never releases the screen")

# The return stroke: a rise that happens AFTER a fall.
fell = False
return_stroke = False
for a, b in zip(vals, vals[1:]):
    if b < a:
        fell = True
    elif b > a and fell:
        return_stroke = True
if not return_stroke:
    fails.append(f"envelope {vals} never rises after falling — that is a DECAY, not a double flash. "
                 "The return stroke is the whole signature.")

if len(keys) >= 3:
    rise = times[1] - times[0]
    tail = times[-1] - times[-2]
    if tail <= rise:
        fails.append(f"tail {tail:.3f}s is not longer than the rise {rise:.3f}s — "
                     "a symmetric flash reads as a rendering glitch, not weather")

for f in fails:
    print(f"FAIL {f}")
print(f"\nlightning envelope: {len(keys)} keys, "
      + ("primary + return stroke, dark at both ends" if not fails else f"{len(fails)} PROBLEM(S)"))
print("PASS" if not fails else "FAIL")
sys.exit(0 if not fails else 1)
