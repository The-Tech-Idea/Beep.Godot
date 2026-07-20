# Phase 1 — Import pipeline & folder layout

## Why

The theme.json files already point at `res://addons/beep_game_builder_cs/textures/<genre>/<theme>/<slot>.png`.
Phase 1 puts real files at those paths. Getting the import settings right **once, per pack type**
(smooth vs pixel) is what makes the difference between crisp UI and a blurry or aliased mess.

## The target layout

Mirror the paths the theme.json already declares. For each themed genre:

```
addons/beep_game_builder_cs/textures/
├── <genre>/
│   └── <theme>/
│       ├── button_normal.png     ← Kenney file, renamed to the slot
│       ├── button_hover.png
│       ├── button_pressed.png
│       ├── button_disabled.png   (optional slot)
│       ├── panel.png
│       ├── input_normal.png      (optional)
│       ├── progress_bg.png       (optional)
│       └── progress_fill.png     (optional)
└── _kenney/
    └── LICENSE_Kenney_CC0.txt    ← copy of the pack License.txt + source URL
```

**Rename to the slot name** (`button_normal.png`, not `button_rectangle_depth_flat.png`). That keeps
every theme.json path stable and identical in shape across genres — the theme just points at its
own folder. The Kenney→slot mapping is recorded in Phase 3.

## The work

1. **Pick the source files** per theme from the Phase 3 mapping table.
2. **Copy + rename** into `textures/<genre>/<theme>/`. A copy step (not a reference to `H:\`) — the
   addon must be self-contained; the `res://` paths resolve inside the addon at runtime.
   - Do this with a small script (bash/pwsh) driven by the Phase 3 table so it's reproducible, not
     hand-copied. Keep the script in `plans/ui-asset-integration/` for re-runs.
3. **Set import settings** (this is the load-bearing step — Godot writes a `.import` per PNG):
   - **Smooth packs** (UI Pack, Sci-fi, Adventure, Fantasy Borders): default 2D import is fine —
     `Filter = Linear`, mipmaps off. These scale up cleanly.
   - **Pixel packs** (UI Pixel Pack, UI Pack - Pixel Adventure): **`Filter = Nearest`**, mipmaps
     off, `Fix Alpha Border` on. Without nearest, pixel UI blurs into mush. Set this via the
     editor's Import dock (select all pixel PNGs → Preset "2D Pixel" → Reimport), or pre-write the
     `.import` files with `filter=false`.
4. **Commit the generated sidecars.** Godot creates a `<file>.png.import` and (4.4+) a `.png` may
   get a `uid://` recorded in the `.import`. Per the repo rule, commit every `.import` alongside its
   PNG — they are resource identity; untracked `.import` files break other machines' load.
5. **Ship the license.** Copy the pack's `License.txt` to `textures/_kenney/LICENSE_Kenney_CC0.txt`
   and note the source pack + version. CC0 needs no attribution, but shipping the license is good
   hygiene and documents provenance.

## Gotchas

- **`res://` paths are case-sensitive on export** even though Windows dev is not — keep the renamed
  files exactly `button_normal.png` (lowercase) to match the theme.json.
- **The generator doesn't need to copy these.** theme.json uses absolute `res://addons/...` paths, so
  textures load straight from the addon in any project that has the addon enabled — no per-project
  copy step (unlike scene templates). Verify no generator code tries to duplicate them.
- **Don't import the whole pack** — 875+822+… files is thousands of unused imports bloating the
  project. Import only the selected slot files (Phase 3). Everything else stays on `H:\`.
- **`.godot/imported/` is build cache**, not committed — only the `.png` + `.png.import` are.

## Verify

1. Open the project in Godot → the Import happens automatically → no import errors in Output.
2. In the FileSystem dock, a renamed PNG previews correctly; a pixel PNG shows crisp pixels at 2×.
3. `git status` shows each new `.png` paired with its `.png.import` (none untracked-and-alone).
