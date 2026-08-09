# Component Reference

This reference summarizes the public C# addon classes found in the exhaustive 2026-08-09 scan. It is intentionally grouped by functional area rather than listing every export property, because there are hundreds of exported fields and the inspector remains the source of truth after the build gate fix.

## Counting Rule

C# editor-addable classes are counted by `[GlobalClass]` declarations under `addons/beep_game_builder_cs/**/*.cs`. Current count: 307.

## Base Categories

| Category | Use |
| --- | --- |
| `EntityComponent` | Shared active/state/group base behavior for component nodes |
| `GameplayComponent` | Rule/state components attached to entities or managers |
| `ControllerComponent` | Movement, input, camera, navigation, and control behavior |
| `WorldComponent` | World-space, environment, area, particle, and atmosphere behavior |
| `UIComponent` | UI behavior attached under controls |
| `UIScreenComponent` | Screen-level UI controllers |
| `AreaTriggerComponent` | Components that depend on `Area2D` signals |

## Gameplay

`AggroComponent`, `AnimalBehaviorComponent`, `AttackComponent`, `AudioComponent`, `AutoHealComponent`, `CardDeckComponent`, `CityEconomyComponent`, `ConsumableUseComponent`, `CooldownComponent`, `CraftingComponent`, `DropTableComponent`, `EquipmentComponent`, `GameFlowComponent`, `GameOverOnDeathComponent`, `GameStateManagerComponent`, `HealthComponent`, `HealthBarComponent`, `HungerStaminaComponent`, `InventoryComponent`, `KnockbackComponent`, `LevelingComponent`, `MovementComponent`, `ObjectPoolComponent`, `PickupComponent`, `ProjectileComponent`, `ProjectileModifierComponent`, `PuzzleLevelComponent`, `QuestComponent`, `RaceStateComponent`, `ResistanceComponent`, `RespawnComponent`, `RpgPartyComponent`, `ShooterCombatComponent`, `SpawnerComponent`, `StateMachineComponent`, `StatsComponent`, `StatusEffectComponent`, `StrategyEmpireComponent`, `SurvivalVitalsComponent`, `TurretComponent`, `TweenComponent`, `WorkComponent`.

Typical use: attach to the entity, manager, or UI node indicated by warnings and exported `NodePath` fields. Most components emit Godot signals for UI binding or gameplay orchestration.

## Controllers

`AIController`, `BootComponent`, `CameraZoomComponent`, `DashComponent`, `FlyComponent`, `FollowTargetComponent`, `GlideComponent`, `HoverComponent`, `JumpComponent`, `NavigationComponent`, `PlatformerController`, `ScreenShakeComponent`, `ShooterController`, `SlideComponent`, `SquashAndStretchComponent`, `TemperatureComponent`, `TopDownController`, `WallJumpComponent`.

Typical use: attach as child of a `CharacterBody2D`, `Camera2D`, or relevant `Node2D` parent. Input-driven controllers check `InputMap` action existence before use.

## World And Atmosphere

`AmbientAudioComponent`, `AmbientController`, `CheckpointComponent`, `CloudSpriteLayer`, `DayNightCycleComponent`, `DestructibleComponent`, `DoorSwitchComponent`, `DynamicFogLayer`, `FootstepComponent`, `HazardComponent`, `HitSparkComponent`, `HitStopComponent`, `InteractableComponent`, `LevelLoaderComponent`, `LightningBoltComponent`, `MovingPlatformComponent`, `ParticleComponent`, `SeasonalComponent`, `SpriteEffectComponent`, `WeatherAudioController`, `WeatherSpriteLayer`, `WeatherSystemComponent`, `WindFieldComponent`.

Typical use: attach world behavior under `Node2D`, `Area2D`, or scene-level managers. Weather/season/time components can be combined through signals and group lookup.

## UI Components

`AccordionComponent`, `AchievementToastComponent`, `AnimatedMenuComponent`, `BadgeComponent`, `BossHealthBarComponent`, `BuffBarComponent`, `BuildToolbarComponent`, `CarouselComponent`, `ChipComponent`, `ChromaticAberrationComponent`, `CollapsiblePanelComponent`, `ComboCounterComponent`, `ContextMenuComponent`, `CoroutineHostComponent`, `CounterComponent`, `CrosshairComponent`, `CursorComponent`, `DataBinderHostComponent`, `DemandMeterComponent`, `DialogUIComponent`, `DragComponent`, `EffectComponent`, `FlipCardComponent`, `GameInfoBinder`, `GameSpeedComponent`, `GenreScreenComponent`, `HudCollapseComponent`, `HudComponent`, `InteractionPromptComponent`, `KeybindManagerComponent`, `LoadGameMenuComponent`, `LoadingScreenComponent`, `LocalizationComponent`, `MarqueeComponent`, `Match3BoardComponent`, `MatchTimerComponent`, `MenuComponent`, `MeterBarComponent`, `MinimapComponent`, `ModalComponent`, `NinePatchFrameComponent`, `PanelFrameComponent`, `PauseComponent`, `ProgressRingComponent`, `PulseComponent`, `RatingComponent`, `ResourceBadgeComponent`, `RippleComponent`, `SafeAreaComponent`, `SaveGameMenuComponent`, `SaveLoadManagerComponent`, `SceneTransitionComponent`, `ScreenFlashComponent`, `SearchBarComponent`, `SettingsComponent`, `ShakeComponent`, `SkeletonLoaderComponent`, `SlideInOutComponent`, `StepperComponent`, `TabGroupComponent`, `TableComponent`, `ThemePresetComponent`, `ToastNotificationComponent`, `ToggleSwitchComponent`, `TooltipComponent`, `UIEffectComponent`, `VignetteComponent`, `WeatherForecastUI`, `WeatherHUDComponent`.

Typical use: attach under the `Control` subtree they affect. Several components create child controls and disconnect their own signals in `_ExitTree`.

## HUD Components

`GenreHudComponent` is the base for genre HUD scripts:

- `CardGameHudComponent`
- `CityBuilderHudComponent`
- `PlatformerHudComponent`
- `PuzzleHudComponent`
- `RacingHudComponent`
- `RpgHudComponent`
- `ShooterHudComponent`
- `StrategyHudComponent`
- `SurvivalHudComponent`
- `TopDownHudComponent`

These are used in generated/template HUD scenes and bind genre-specific game data into common HUD surfaces.

## Game UI Kit Controls

Drop-in or near drop-in controls:

`KitPushButton`, `KitButton`, `KitBuildTile`, `KitCheckBox`, `KitCheckButton`, `KitColorRect`, `KitGodotTree`, `KitIconButton`, `KitItemList`, `KitKnob`, `KitLabel`, `KitMeter`, `KitOptionButton`, `KitPanel`, `KitPanelContainer`, `KitRemovableChip`, `KitSlider`, `KitSliderBar`, `KitStarRating`, `KitSwitchVisual`, `KitTabPanel`, `KitTabStrip`, `KitToggle`.

Custom kit controls:

`KitArrowSelector`, `KitAvatarFrame`, `KitBookSpread`, `KitChip`, `KitCollapsiblePanel`, `KitColorOverlay`, `KitContextMenu`, `KitCurrencyBar`, `KitDialogBox`, `KitGemSlot`, `KitHeartRow`, `KitHudText`, `KitInputHint`, `KitInventorySlot`, `KitItemCard`, `KitLabelValue`, `KitLevelButton`, `KitLevelPath`, `KitModalShade`, `KitNodeCard`, `KitOrbMeter`, `KitOrnament`, `KitPager`, `KitPanelHanger`, `KitRadarChart`, `KitRadialMeter`, `KitRow`, `KitSegmentedIconGroup`, `KitSlotGrid`, `KitSpeechBubble`, `KitSpinner`, `KitSpinWheel`, `KitTableCell`, `KitToast`, `KitTooltip`, `KitTree`, `KitWeatherForecastCard`.

## Resources And Data Models

Core resources and data models include:

- `GameInfo`
- `GameStateData` and nested save-state types
- `WeatherForecast`, `WeatherData`
- `GameItem`, `GameEquipment`, `GameWeapon`, `GameArmor`, `GameShield`, `GameConsumable`, `GameLiquid`
- `DropTableEntry`
- `Stat`, `StatModifier`
- `ColorPalette`, `GeometryProfile`, `UISkin`

## Fixed Component Issues From Review

- `TweenComponent` now implements every exported preset and the headless runtime smoke validates finite endpoint behavior on both `Control` and `Node2D` targets.
- `DataBinderHostComponent` and `BeepDataBinder` now normalize common Godot target property names and convert boxed reflection values into typed Variants before calling `Set()`.
- `DataBinderHostComponent` now treats `OneWayToSource` as target-to-source during initial bind and refresh.
- Build verification now excludes generated directories from the root project and passes with 0 warnings and 0 errors.
- Automated addon checks now cover source contracts, clean build, Godot headless runtime smoke, and Godot headless editor startup.
