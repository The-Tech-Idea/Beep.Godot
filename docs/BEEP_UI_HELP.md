# beep_ui Help

`beep_ui` is a GDScript UI addon that can be used without the C# game-builder UI kit.

## Public Nodes And Resources

| Name | Kind | File | Purpose |
| --- | --- | --- | --- |
| `BeepPreset` | `RefCounted` | `theme/beep_theme.gd` | Base theme data and static preset registry |
| `BeepThemeApplier` | `Node` | `theme/theme_applier.gd` | Applies a preset to a `Control` subtree |
| `BeepUIEffect` | `Node` | `effects/ui_effect.gd` | Adds tweened UI effects |
| `BeepWidgetFactory` | `RefCounted` | `widgets/widget_factory.gd` | Creates styled controls |
| `BeepToastHost` | `Control` | `widgets/toast_host.gd` | Displays transient toast messages |

## Presets

`BeepPreset.preset_names()` returns:

`Modern`, `SciFi`, `Cartoon`, `Classic`, `Desert`, `OilGas`, `Sea`, `Sports`, `Soccer`, `Fantasy`, `Horror`, `Nature`, `Space`, `Military`, `Steampunk`, `Retro80s`, `Pixel8Bit`, `Winter`, `Cyberpunk`, `Japan`, `Toxic`, `Candy`.

Each `preset_*.gd` script extends `BeepPreset` and sets:

- primary/accent/background/text colors
- button, panel, and input colors
- geometry values
- animation values
- ripple/effect styling

## BeepThemeApplier

Exports:

- `preset`: preset name string.
- `enable_animations`: whether button hover/press animations are injected.
- `enable_ripple`: whether ripple behavior is injected.
- `active`: whether theming runs.

Signals:

- `theme_applied`

Placement:

- Child of a `Control`: themes the parent subtree.
- Parent of controls: themes child `Control` subtrees.

Known issue:

- The inspector preset enum is duplicated manually in `theme_applier.gd`; the source of truth should be `BeepPreset._PRESET_SCRIPTS`.

## BeepUIEffect

Effect types:

- `SLIDE`
- `SHAKE`
- `PULSE`
- `BOB`
- `FLASH`
- `GLITCH`
- `ROTATE`
- `FADE`
- `TYPEWRITER`
- `BOUNCE`
- `OFFSET`

Scope types:

- `SELF`
- `CHILDREN`
- `SCENE`
- `GLOBAL`

Signals:

- `effect_started`
- `effect_completed`
- `effect_looped(loop_count)`

Common exports include duration, delay, easing, transition, looping, slide direction/distance, shake intensity, pulse scale, bob height/speed, flash color/count, glitch settings, rotation axis/angle, fade direction/alpha, typewriter speed/cursor, bounce height/count, and offset target.

## BeepToastHost

Toast types:

- `INFO`
- `SUCCESS`
- `WARNING`
- `ERROR`

Exports:

- `duration`
- `toast_size`
- `max_visible`

Use this as a persistent UI node where transient status messages should appear.

## Theme Studio

`editor/theme_studio.gd` provides an editor dock that:

- Lists all registered presets.
- Shows swatches.
- Applies a selected preset to the edited scene.
- Creates common widgets through `BeepWidgetFactory`.

## Recommended Fixes

- Derive inspector choices dynamically from `BeepPreset.preset_names()`.
- Add a GDScript test that instantiates `BeepThemeApplier`, loops every preset name, and confirms a theme is applied or an actionable warning is produced.
