# Phase 2 — i18n pipeline made live

## The defect (MEDIUM)

The framework ships a **complete, wired** localization pipeline:
- `templates/i18n/translations.csv` — en/es/ja for a menu-chrome key set.
- `BeepGenreGenerator.cs:273` registers the `Locale` autoload (`LocalizationComponent`), which
  `LoadCsv`s the file and `TranslationServer.AddTranslation`s each column.
- `SettingsMenu.cs` ships a working English/Español/日本語 selector that calls `SetLanguage`.

But it does nothing visible. `LocalizationComponent.LoadCsv` maps `msgid = keys-column` → locale value,
and Godot's auto-translation looks up **a Control's `text` as the msgid**. The CSV keys are *symbolic*
(`MENU_PLAY`, `PAUSE_TITLE`), while the scenes render literal English (`"New Game"`, `"Paused"`). No
Control text ever equals a key, so `TranslationServer.Translate("Paused")` finds nothing and returns
`"Paused"` unchanged. Selecting Español/日本語 changes **nothing** on any shipped screen — a shipped,
wired feature that silently no-ops (the repo's dominant defect class). The symbolic keys don't even
correspond to the literals (`MENU_PLAY`'s English is `"Play"`, but the menu says `"New Game"`), so a
developer can't fix it by translating alone.

## The fix (Decision A: source-string rekey)

Godot 4 auto-translation is **source-string based** — the source English string *is* the msgid. So the
correct, idiomatic fix is to rekey the CSV's `keys` column on the exact English strings the shipped
scenes render, with the `en` column echoing the source and `es`/`ja` translating it. Then:
- `LoadCsv` registers `Translate("New Game", "es") → "Nueva partida"`.
- A Label with `text = "New Game"` auto-translates on locale change — **no scene edits, no code edits.**

Verified safe: nothing calls `Tr("MENU_PLAY")` (grep) — the symbolic keys live only in the CSV and doc
comments, so no code path depends on them.

**Scope — chrome only.** The rekeyed CSV covers the framework-shipped, static menu chrome (the strings
enumerated from `main_menu`/`pause_menu`/`settings_menu`/`game_over`/`level_summary`/`save`/`load`
scenes). It deliberately omits:
- Dynamic/runtime strings (`"Score: 0"`, `"100 / 100"`, `"Slot 1 - Empty"`, resolution numbers) — set
  by code from live data; the developer localizes those via `TrF` if wanted.
- Game identity (`"Game Title"`, `"v0.1.0"`) — developer content, overwritten by `GameInfoBinder`.
- Language endonyms (`English`/`Español`/`日本語`) — shown as-is in every locale, by convention.

## Doc correction

`LocalizationComponent.cs` — update the class doc + CSV example so they teach the **source-string**
convention (`keys = the on-screen English`), not the symbolic-key example that never matched, and note
that the shipped CSV covers chrome only; the developer extends it with their own source strings.

## Verify
- `dotnet build` → 0 errors (no C# behavior change; doc-comment only); `validate_scenes.sh` → PASS.
- Editor: generate a project → run → open Settings → switch to Español → the menu chrome
  (Paused/Settings/Quit/Resume/Game Over/…) changes language. English round-trips unchanged.
