# Example Art Audit

Purpose: component rules observed from every raster reference image in `Example_Art/`.
This is an implementation map, not a mood board. Each repeated visual pattern should resolve
to a reusable kit component under `addons/beep_game_builder_cs/ecs/ui/kit/`.

## Coverage Added

- `KitHeartRow`: RPG/platform life rows. Replaces text-only HP for heart-based HUDs.
- `KitSpeechBubble`: dialogue, tutorial, quest, and city/world callout bubbles with tails.
- `KitItemCard`: shop rows, quest rows, item/equipment cards, and compact item tiles.
- `KitLevelButton`: fixed-square level/world-node buttons with lock/star states.
- Existing coverage confirmed: `KitCurrencyBar`, `KitNodeCard`, `KitOrbMeter`, `KitMeter`,
  `KitInventorySlot`, `KitSlotGrid`, `KitTabStrip`, `KitBuildTile`, `KitIconButton`,
  `KitAvatarFrame`, `KitBookSpread`, `KitTree`, `KitDialogBox`, `KitContextMenu`,
  `KitCollapsiblePanel`, and `KitPanelContainer`.

## Component Matrix

| Reference | Components visible | Kit mapping |
| --- | --- | --- |
| `citybuilder1.png` | Top resource strips, left circular action buttons, badges, compact edge HUD | `KitCurrencyBar`, `KitIconButton`, `KitChip`, `KitPanelIntent.Hud` |
| `citybuilder2.png` | Resource bars, fixed square side actions, bottom circular tools | `KitCurrencyBar`, `KitIconButton`, `KitSegmentedIconGroup` |
| `citybuilder3.png` | Flat resource pills, bottom build dock, side info sheet, small title strip | `KitCurrencyBar`, `KitBuildTile`, `KitPanelContainer` with `Hud`/`Sheet` intent |
| `citybuilder4.png` | Vertical icon rail, build item rows, muted sheet header | `KitIconButton`, `KitItemCard`, `KitPanelContainer` |
| `citybuilder5.png` | Compact resource badges, build category buttons, fixed build tiles | `KitCurrencyBar`, `KitBuildTile`, `KitIconButton` |
| `gameui1.png` | Parchment panels, tabs, inventory grids, toggles, bars | `KitPanelContainer`, `KitTabStrip`, `KitSlotGrid`, `KitToggle`, `KitMeter` |
| `gameui2.png` | Rounded mobile panels, icon grids, popup buttons, map widgets | `KitPanelContainer`, `KitIconButton`, `KitDialogBox`, `KitLevelButton` |
| `gameui3.png` | Wood/paper panels, tab caps, fixed icon buttons | `KitPanelContainer`, `KitTabStrip`, `KitIconButton` |
| `gameui4.png` | Heart HP, minimap frame, inventory/shop panels, fixed level buttons | `KitHeartRow`, `KitPanelContainer`, `KitItemCard`, `KitLevelButton` |
| `gameui5.png` | Casual button states, icon buttons, badges | `KitButton`, `KitIconButton`, `KitChip` |
| `gameui6.png` | Pause/settings popup, sliders, toggles, rounded buttons | `KitDialogBox`, `KitSlider`, `KitToggle`, `KitButton` |
| `gameui7.png` | App-like blue panels, compact cards, fixed button sizes | `KitPanelContainer`, `KitItemCard`, `KitButton`, `KitIconButton` |
| `gameui8.png` | RPG HUD, hearts, circular abilities, shop cards, minimap | `KitHeartRow`, `KitOrbMeter`, `KitIconButton`, `KitItemCard`, `KitAvatarFrame` |
| `gameui9.png` | Crafting table UI, top tabs, map, bottom hotbar slots, wood plates | `KitTabStrip`, `KitSlotGrid`, `KitInventorySlot`, `KitPanelContainer` |
| `racing1.png` | Transparent telemetry, thin gauges, minimal framed HUD | `KitHudText`, `KitRadialMeter`, `KitMeter`, `KitPanelIntent.Hud` |
| `racing2.png` | Angled finish banner, minimap, speed gauge | `KitPanel`/`KitPanelContainer` with racing shape, `KitRadialMeter` |
| `racing3.png` | Garage cards, angular panels, compact icon grid | `KitItemCard`, `KitIconButton`, `KitPanelContainer` |
| `racing4.png` | Sharp garage panels, white hairlines, vehicle card rows | `KitPanelContainer`, `KitItemCard`, `KitSegmentedIconGroup` |
| `rpg1.png` | Parchment shop, item row cards, top tabs, buy buttons | `KitPanelContainer`, `KitItemCard`, `KitTabStrip`, `KitButton` |
| `rpg2.png` | Parchment columns, wood item rows, tab title strip | `KitPanelContainer`, `KitItemCard`, `KitTabStrip` |
| `rpg3.png` | Inventory slot grid, compact stats, equipment cells | `KitSlotGrid`, `KitInventorySlot`, `KitItemCard`, `KitLabelValue` |
| `rpgui.png` | RPG HUD bars, orbs, portraits, icon slots | `KitOrbMeter`, `KitMeter`, `KitAvatarFrame`, `KitInventorySlot` |
| `rpgui1.png` | Dark fantasy HUD, slim bars, circular icons, rectangular menus | `KitMeter`, `KitIconButton`, `KitPanelContainer` |
| `rpgui2.png` | Dense RPG inventory, stat cards, tabs, item details | `KitSlotGrid`, `KitItemCard`, `KitTabStrip`, `KitPanelContainer` |
| `rpgui3.png` | Classic RPG info rows, equipment slots, compact cells | `KitRow`, `KitInventorySlot`, `KitItemCard` |
| `settings1.png` | Settings rows, selectors, sliders, parchment modal | `KitDialogBox`, `KitRow`, `KitArrowSelector`, `KitSlider` |
| `skilltree.png` | Dark skill panel, square nodes, orthogonal connectors | `KitTree`, `KitIconButton`, `KitTooltip` |
| `skilltree1.png` | Vertical skill tree, square nodes, bottom CTA | `KitTree`, `KitLevelButton`, `KitButton` |
| `skilltree3.png` | Skill nodes, compact cards, upgrade CTA | `KitTree`, `KitNodeCard`, `KitButton` |
| `skilltree4.png` | Skill grid, colored nodes, simple tabs | `KitTree`, `KitIconButton`, `KitTabStrip` |
| `store.png` | Store sheet, item grid cards, price badges | `KitPanelContainer`, `KitItemCard`, `KitChip` |
| `store1.png` | Mobile store cards, fixed item cells, purchase badges | `KitItemCard`, `KitNodeCard`, `KitButton` |
| `survaivleandrpg.png` | Journal tabs, parchment pages, item/category rows | `KitBookSpread`, `KitTabStrip`, `KitItemCard`, `KitRow` |
| `survaivleandrpg1.png` | Book equipment page, slots, thin dividers | `KitBookSpread`, `KitInventorySlot`, `KitSlotGrid` |
| `survaivleandrpg2.png` | Survival inventory, compact toolbar, square grid | `KitSlotGrid`, `KitInventorySlot`, `KitIconButton` |
| `ui1.png` | Mission/list rows, toggles, sliders, badges, CTA buttons | `KitItemCard`, `KitToggle`, `KitSlider`, `KitChip`, `KitButton` |
| `ui2.png` | Hero stat rows, rune slots, large CTAs | `KitRow`, `KitInventorySlot`, `KitButton`, `KitAvatarFrame` |
| `ui3.png` | Item grid, action buttons, compact shop/inventory cards | `KitSlotGrid`, `KitItemCard`, `KitButton` |
| `ui5.png` | Button/icon sprite sheet, fixed pressed/disabled states | `KitButton`, `KitIconButton`, `KitLevelButton` |
| `ui6.png` | Notebook spread inventory, tabbed pages, thin grid | `KitBookSpread`, `KitTabStrip`, `KitSlotGrid` |
| `ui7.png` | Crafting surface, category tabs, recipe list, hotbar, map | `KitTabStrip`, `KitItemCard`, `KitSlotGrid`, `KitInventorySlot` |
| `ui8.png` | City-builder HUD with edge rails, resources, friend/shop cards | `KitCurrencyBar`, `KitIconButton`, `KitItemCard`, `KitPanelIntent.Hud` |
| `ui9.png` | RPG kit sheet: hearts, minimap, quests, dialogue, abilities, banners | `KitHeartRow`, `KitSpeechBubble`, `KitItemCard`, `KitIconButton`, `KitAvatarFrame` |
| `uitexturs.png` | Leather, rubber, leaf, metal, parchment, wood, fabric, paper surfaces | `KitGrain`/`KitStyleJson` material inputs |
| `uiwood.png` | Wood panels, rope bars, circular and square wood icon buttons | `KitPanelContainer`, `KitButton`, `KitIconButton` |
| `Upgrades.png` | Upgrade columns, square nodes, tooltip, reset/done buttons | `KitTree`, `KitLevelButton`, `KitTooltip`, `KitButton` |

## Vecteezy Raster Sheets

| Reference | Components visible | Kit mapping |
| --- | --- | --- |
| `vecteezy_action-game-ui-kit-will-menus-pop-up-screens-and-game_.jpg` | Action modal screens, close buttons, sliders, fixed button groups | `KitDialogBox`, `KitButton`, `KitSlider`, `KitIconButton` |
| `vecteezy_futuristic-hud-frames-user-interface-elements-border-aim_22012250.jpg` | Sci-fi hairline HUD frames, target reticles, asymmetric borders | `KitPanelContainer`, `KitRadialMeter`, racing/shooter geometry |
| `vecteezy_galaxy-space-game-interface-ui-game-buttons-set_11674794.jpg` | Bright fixed icon buttons, galaxy panels, counters | `KitIconButton`, `KitPanelContainer`, `KitCurrencyBar` |
| `vecteezy_game-buttons-of-wooden-and-gold-texture-cartoon-menu_16666175.jpg` | Wood/gold capsules, rectangular buttons, arrows | `KitButton`, `KitIconButton`, `KitPanelContainer` |
| `vecteezy_game-interface-jungle-rain-forest-wood-asset_73884014.jpg` | Jungle/leaf frames, wood button grids, map/dialog assets | `KitPanelContainer`, `KitButton`, `KitSpeechBubble` |
| `vecteezy_game-ui-kit-with-menus-pop-up-screens-and-game-elements_8176844.jpg` | Casual cards, panels, sliders, inventory cells | `KitPanelContainer`, `KitItemCard`, `KitSlider`, `KitInventorySlot` |
| `vecteezy_game-ui-menu-interface-scrolls-and-parchments_12996523.jpg` | Scroll/parchment menus, ribbons, title banners | `KitDialogBox`, `KitPanelContainer`, `KitButton` |
| `vecteezy_hud-frames-futuristic-text-box-border-frame-sci-fi_22394377.jpg` | Futuristic text boxes and angled frames | `KitPanelContainer`, `KitSpeechBubble`, shooter/racing geometry |
| `vecteezy_list-of-mobile-games-game-ui-kit-user-interface-ui-ux_20470778.jpg` | Generic mobile kit: panels, list rows, buttons, toggles | `KitPanelContainer`, `KitItemCard`, `KitButton`, `KitToggle` |
| `vecteezy_medieval-royal-knight-game-interface-asset_73884004.jpg` | Medieval menus, avatar/coat-of-arms frames, stone buttons | `KitPanelContainer`, `KitAvatarFrame`, `KitButton`, `KitIconButton` |
| `vecteezy_square-wooden-frames-for-game-user-avatar_15917876.jpg` | Square/rounded avatar frames | `KitAvatarFrame`, `KitPanelContainer` |
| `vecteezy_stone-game-interface-buttons-and-ui-elements_11156720.jpg` | Stone buttons, chamfered panels, fixed icon cells | `KitButton`, `KitIconButton`, `KitPanelContainer` |
| `vecteezy_wooden-buttons-cartoon-interface-game-ui-elements_10876594.jpg` | Wooden buttons, vine accents, square/circular icons | `KitButton`, `KitIconButton`, `KitPanelContainer` |
| `vecteezy_wooden-game-buttons-cartoon-menu-interface-set_15916901.jpg` | Wood menu buttons, arrows, fixed square controls | `KitButton`, `KitIconButton`, `KitLevelButton` |

## Rules Applied To Kit

- Gameplay HUD panels use `KitPanelIntent.Hud`: compact edge readouts with no decorative title plate by default.
- Menu, dialog, modal, accordion, and context-menu visuals stay inside the kit and must not generate raw Godot controls for game-facing UI.
- Component sizes are fixed or clamped where the art shows fixed HUD/control dimensions. Buttons and build tiles do not grow from title text.
- RPG HUD should prefer hearts/orbs/meters/cards over label-only stat panels.
- City-builder HUD should prefer compact currency/resource strips, edge rails, build tiles, and small info cards.
- Skill trees use square or stepped nodes and clear connectors. They do not inherit heavy panel silhouettes.
- Shop, quest, inventory, equipment, and mission rows use `KitItemCard`, not arbitrary `PanelContainer + Label + Button` compositions.

## Second Pass Decisions

- Font scale is intentionally quieter than the first kit pass. The references use large display
  text for splash/menu moments, but HUD panels, build widgets, store rows, skill nodes, and RPG
  stat panels mostly use compact text inside fixed shapes.
- Silhouette is now widget-class aware. Bars resolve as bars, chips as chips, slots as slots,
  and panels as panels. A genre-specific special shape no longer leaks into every component.
- Platformer and puzzle no longer use capsule/ellipse as the default shape for all controls.
  Those forms remain valid for bars, badges, meters, and explicit controls.
- RPG/city-builder/survival/strategy panels use less frame, less sparkle, and smaller base type
  so generated HUDs read as playable game overlays instead of decorative menu plates.
- Base geometry font sizes are normalized by genre. Large typography must be requested through
  `BeepDisplay`/`BeepTitle`, not baked into every widget from the base font size.
