using Godot;

namespace Beep.ECS.Scenes
{
    [Tool]
    [GlobalClass]
    public partial class SettingsMenu : CanvasLayer
    {
        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            this.ConnectButton("CloseButton", OnClosePressed);
            this.ConnectButton("ResetButton", OnResetPressed);
            WireSettingsWidgets();
            PopulateControls();
            NormalizeLayout();

            // Focus something, or a keyboard/gamepad player opens this screen and every
            // press goes nowhere — there is no mouse to fall back on. Deferred so the tab
            // pages are laid out and their controls are focusable.
            Callable.From(GrabInitialFocus).CallDeferred();
        }

        private void GrabInitialFocus()
        {
            if (this.Find<Godot.Range>("MasterSlider") is { } first) first.GrabFocus();
            else this.Find<Button>("CloseButton")?.GrabFocus();
        }

        /// <summary>Put every settings row on the same three-column grid, and share the
        /// dialog shell numbers with the save/load menus via <see cref="UI.BeepDialogLayout"/>.
        ///
        /// The label column MUST be fixed-width and non-expanding, or the control beside it
        /// lands at a different x on every tab. The scene had it both ways — Fullscreen,
        /// Subtitles, ScreenShake and DamageNumbers expanded their label; Master, SFX, Music,
        /// Resolution and Language did not — so the controls visibly jumped sideways when you
        /// switched tabs. Rows also had no minimum height, so a slider row, a checkbox row and
        /// an option-button row were three different heights down the same list.
        ///
        /// Runs on every open, so a project still carrying an older settings_menu.tscn is
        /// corrected without regenerating the scene.</summary>
        private void NormalizeLayout()
        {
            UI.BeepDialogLayout.ApplyShell(this);

            // ApplyShell's panel sizing looks for a node named "PanelContainer" (the save/load
            // spine) and this screen's is named "Panel", so it never reached settings — and it
            // must not: those two want a 620px floor for their slot list, this one wants its
            // height to come from the form. Width only, height explicitly zero.
            if (this.Find<PanelContainer>("Panel") is { } panel)
                panel.CustomMinimumSize = new Vector2(UI.BeepDialogLayout.SettingsPanelWidth, 0);

            if (this.Find<VBoxContainer>("ContentVBox") is { } content)
                content.AddThemeConstantOverride("separation", UI.BeepDialogLayout.SectionGap);
            if (this.Find<TabContainer>("Tabs") is { } tabs)
                tabs.CustomMinimumSize = new Vector2(0, UI.BeepDialogLayout.SettingsTabHeight);
            if (this.Find<HBoxContainer>("Footer") is { } footer)
                footer.AddThemeConstantOverride("separation", UI.BeepDialogLayout.ButtonGap);
            foreach (string b in new[] { "ResetButton", "CloseButton" })
                if (this.Find<Button>(b) is { } btn)
                    btn.CustomMinimumSize = new Vector2(0, UI.BeepDialogLayout.ActionButtonHeight);
            foreach (string list in new[] { "AudioList", "DisplayList", "GameList", "ControlsList" })
                if (this.Find<VBoxContainer>(list) is { } l)
                    l.AddThemeConstantOverride("separation", UI.BeepDialogLayout.RowGap);

            NormalizeRows(this);
        }

        /// <summary>Walk every "*Row" HBox and stamp the three-column grid onto it.</summary>
        private static void NormalizeRows(Node node)
        {
            if (node is HBoxContainer row && row.Name.ToString().EndsWith("Row"))
            {
                row.AddThemeConstantOverride("separation", UI.BeepDialogLayout.RowInnerGap);
                row.CustomMinimumSize = new Vector2(0, UI.BeepDialogLayout.SettingsRowHeight);
                bool first = true;
                foreach (var child in row.GetChildren())
                {
                    if (child is not Godot.Control c) continue;
                    string n = c.Name.ToString();
                    // Our own right-hand gutter: skip it, or the "everything else" branch below
                    // would hand it ExpandFill on the next pass and it would eat the row.
                    if (n == "ScrollGutter") continue;
                    if (first && c is Label lbl)
                    {
                        // Column 1: the label. Fixed width, never expands, vertically centred.
                        lbl.CustomMinimumSize = new Vector2(UI.BeepDialogLayout.SettingsLabelColumn, 0);
                        lbl.SizeFlagsHorizontal = Godot.Control.SizeFlags.Fill;
                        lbl.SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter;
                        lbl.VerticalAlignment = VerticalAlignment.Center;
                        first = false;
                        continue;
                    }
                    if (c is Label value && n.EndsWith("Value"))
                    {
                        // Column 3: the read-out. Fixed width, right-aligned, so the digits
                        // form a straight edge instead of drifting with the slider.
                        value.CustomMinimumSize = new Vector2(UI.BeepDialogLayout.SettingsValueColumn, 0);
                        value.SizeFlagsHorizontal = Godot.Control.SizeFlags.Fill;
                        value.HorizontalAlignment = HorizontalAlignment.Right;
                        value.VerticalAlignment = VerticalAlignment.Center;
                        continue;
                    }
                    // Column 2: the control. A CheckButton keeps its natural width and sits
                    // right; everything else fills the gap between label and read-out.
                    c.SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter;
                    c.SizeFlagsHorizontal = c is CheckButton or CheckBox
                        ? Godot.Control.SizeFlags.ShrinkEnd
                        : Godot.Control.SizeFlags.ExpandFill;
                }
                AddScrollGutter(row);
            }
            foreach (var child in node.GetChildren()) NormalizeRows(child);
        }

        /// <summary>Reserve a strip at the right of every settings row so the tab's vertical
        /// scrollbar cannot sit on top of the read-out column.
        ///
        /// A ScrollContainer's scrollbar OVERLAYS its content rather than displacing it, so as
        /// soon as the Controls tab held more bindings than fit, the bar came down through the
        /// right-hand column and clipped the values — "W, Up" and "Pad 0" were sliced in half.
        /// (BeepDialogLayout.ScrollGutter exists because the same thing happened to the load
        /// menu's Delete buttons.)
        ///
        /// Applied to EVERY row, not only the tabs that currently overflow: if just the
        /// scrolling tab were inset, its value column would sit 14px left of the others and the
        /// numbers would visibly jump when you changed tabs. Name-guarded so the repeated
        /// NormalizeLayout calls cannot stack spacers.</summary>
        private static void AddScrollGutter(HBoxContainer row)
        {
            const string spacerName = "ScrollGutter";
            if (row.FindChild(spacerName, recursive: false, owned: false) != null) return;

            var spacer = new Godot.Control
            {
                Name = spacerName,
                CustomMinimumSize = new Vector2(UI.BeepDialogLayout.ScrollGutter, 0),
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
            };
            spacer.SizeFlagsHorizontal = Godot.Control.SizeFlags.Fill;
            row.AddChild(spacer);
        }

        // As a modal overlay (which it always is now — opened via SettingsOverlay.Open), close on the
        // Cancel/Escape key and CONSUME it, so the same press doesn't also reach GameFlow's pause toggle
        // and close the pause menu out from under this dialog. _Input runs before _UnhandledInput.
        public override void _Input(InputEvent @event)
        {
            if (Engine.IsEditorHint()) return;
            if (GetTree()?.CurrentScene == this) return;   // not an overlay — leave input alone
            if (@event.IsActionPressed("ui_cancel"))
            {
                OnClosePressed();
                GetViewport()?.SetInputAsHandled();
            }
        }

        /// <summary>Bind the six controls to the Settings autoload. The scene has always
        /// shown them, but nothing read or wrote them — SettingsComponent is a ConfigFile
        /// store with no UI code, and this script only wired Close. Each control is seeded
        /// from the stored value, then writes back on change; SettingsComponent's own
        /// setters persist and apply.</summary>
        private void WireSettingsWidgets()
        {
            var settings = UI.SettingsComponent.Instance;
            if (settings == null)
            {
                GD.PushWarning($"[{Name}] Settings autoload not found — controls left inert.");
                return;
            }

            // The three audio sliders shipped bound to nothing: the scene drew them,
            // SettingsComponent stored and applied the volumes, and no code joined the two.
            // Dragging them did nothing, and their positions were scene literals that never
            // reflected the stored value.
            Bind("MasterSlider", settings.MasterVolume, v => settings.MasterVolume = v, "MasterValue");
            Bind("SfxSlider", settings.SfxVolume, v => settings.SfxVolume = v, "SfxValue");
            Bind("MusicSlider", settings.MusicVolume, v => settings.MusicVolume = v, "MusicValue");

            Bind("FullscreenCheck", settings.Fullscreen, v =>
            {
                settings.Fullscreen = v;
                SyncResolutionEnabled(v);
            });
            SyncResolutionEnabled(settings.Fullscreen);

            Bind("SubtitlesCheck", settings.SubtitlesEnabled, v => settings.SubtitlesEnabled = v);
            Bind("ScreenShakeCheck", settings.ScreenShakeEnabled, v => settings.ScreenShakeEnabled = v);
            Bind("DamageNumbersCheck", settings.DamageNumbersEnabled, v => settings.DamageNumbersEnabled = v);

            if (this.Find<OptionButton>("ResolutionOption") is { } resolution)
            {
                // The dropdown's items and SettingsComponent.Resolutions are two lists that
                // must stay the same length — ResolutionIndex is an index into the array, so an
                // extra scene item points past its end and ApplyDisplaySettings returns without
                // resizing anything, in silence. (That is exactly what happened when a fourth
                // entry, 1600x900, was added to the scene against a three-entry array.)
                if (resolution.ItemCount != UI.SettingsComponent.Resolutions.Length)
                    GD.PushWarning($"[{Name}] ResolutionOption offers {resolution.ItemCount} items but SettingsComponent.Resolutions has {UI.SettingsComponent.Resolutions.Length} — the extra choices will do nothing. Keep the two in step.");

                if (settings.ResolutionIndex >= 0 && settings.ResolutionIndex < resolution.ItemCount)
                    resolution.Selected = settings.ResolutionIndex;
                resolution.ItemSelected += index =>
                {
                    settings.ResolutionIndex = (int)index;
                    settings.ApplyDisplaySettings();
                    settings.SaveSettings();
                };
            }

            if (this.Find<OptionButton>("LanguageOption") is { } locale)
            {
                int i = LocaleCodes.IndexOf(settings.Language);
                if (i >= 0 && i < locale.ItemCount) locale.Selected = i;
                locale.ItemSelected += index =>
                {
                    if (index < 0 || index >= LocaleCodes.Count) return;
                    settings.Language = LocaleCodes[(int)index];
                    settings.ApplyLocaleSettings();
                    settings.SaveSettings();
                };
            }
        }

        /// <summary>Locale code per LanguageOption item, in the order the scene declares
        /// them (English / Español / 日本語) — matches templates/i18n/translations.csv.</summary>
        private static readonly System.Collections.Generic.List<string> LocaleCodes = new() { "en", "es", "ja" };

        /// <summary>Grey out the resolution picker while fullscreen is on.
        ///
        /// ApplyDisplaySettings returns early when fullscreen — the screen dictates the
        /// resolution — so the control was live, clickable, and completely inert: the player
        /// picked 2560x1440, nothing moved, and nothing explained why. Disabling it makes the
        /// rule visible instead of leaving the player to conclude the game is broken.</summary>
        private void SyncResolutionEnabled(bool fullscreen)
        {
            if (this.Find<OptionButton>("ResolutionOption") is { } res) res.Disabled = fullscreen;
            if (this.Find<Label>("ResolutionLabel") is { } label) label.Modulate = new Color(1, 1, 1, fullscreen ? 0.5f : 1f);
        }

        /// <summary>Fill the Controls tab from the project's actual InputMap.
        ///
        /// It used to be a single label telling the PLAYER to "edit in Project Settings →
        /// Input Map" — a developer-only instruction shown to someone running a built game,
        /// where Project Settings does not exist. Listing the real bindings is at least true.
        ///
        /// Read-only by design: a rebinding UI has to own conflict resolution, per-device
        /// handling and persistence, which is the game's decision, not the framework's.
        /// Everything needed to build one is here — iterate InputMap.GetActions() and call
        /// InputMap.ActionEraseEvents / ActionAddEvent.</summary>
        private void PopulateControls()
        {
            if (this.Find<VBoxContainer>("ControlsList") is not { } list) return;

            foreach (var action in InputMap.GetActions())
            {
                string name = action.ToString();
                if (name.StartsWith("ui_")) continue;   // Godot's built-ins — noise to a player

                var events = InputMap.ActionGetEvents(name);
                string bound = events.Count == 0
                    ? "unbound"
                    : string.Join(", ", System.Linq.Enumerable.Select(events, e => Describe(e)));

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);

                var label = new Label { Text = Humanize(name), CustomMinimumSize = new Vector2(160, 0) };
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(label);

                var value = new Label { Text = bound, HorizontalAlignment = HorizontalAlignment.Right };
                value.ThemeTypeVariation = "BeepValue";
                row.AddChild(value);

                // These rows are built here, not in the scene, so their names are Godot's
                // auto-generated ones and NormalizeRows — which matches on "*Row" — never sees
                // them. The scroll gutter therefore has to be added at the source: this is the
                // one tab long enough to grow a scrollbar, and without the strip the bar came
                // down over the binding column and sliced "W, Up" and "Pad 0" in half.
                row.AddChild(new Godot.Control
                {
                    Name = "ScrollGutter",
                    CustomMinimumSize = new Vector2(UI.BeepDialogLayout.ScrollGutter, 0),
                    MouseFilter = Godot.Control.MouseFilterEnum.Ignore,
                });

                list.AddChild(row);
            }
        }

        /// <summary>"move_left" -> "Move Left".</summary>
        private static string Humanize(string action)
        {
            var parts = action.Split('_', System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        /// <summary>A player-readable name for one bound event. InputEvent.AsText() returns
        /// things like "A (Physical)" and long joypad strings; this keeps it short.</summary>
        private static string Describe(InputEvent e) => e switch
        {
            InputEventKey k => OS.GetKeycodeString(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode),
            InputEventMouseButton m => $"Mouse {(int)m.ButtonIndex}",
            InputEventJoypadButton j => $"Pad {(int)j.ButtonIndex}",
            InputEventJoypadMotion a => $"Axis {(int)a.Axis}",
            _ => e.AsText(),
        };

        /// <summary>Reset every setting, then re-seed the widgets so the screen shows it.
        ///
        /// Re-seeding matters: the controls were seeded once in _Ready, so without this the
        /// values on disk would change while the sliders sat where the player left them —
        /// the screen would be lying about the state of the game.</summary>
        private void OnResetPressed()
        {
            var settings = UI.SettingsComponent.Instance;
            if (settings == null)
            {
                GD.PushWarning($"[{Name}] Reset pressed but the Settings autoload is missing — nothing to reset.");
                return;
            }
            settings.ResetToDefaults();
            ReseedWidgets(settings);
        }

        /// <summary>Push stored values back into the controls WITHOUT firing their handlers.
        /// Godot's Value/ButtonPressed/Selected setters emit their signals synchronously, so a
        /// naive re-seed would write each value straight back through the binding it just came
        /// from. SetValueNoSignal / SetPressedNoSignal exist for exactly this.</summary>
        private void ReseedWidgets(UI.SettingsComponent s)
        {
            SeedSlider("MasterSlider", s.MasterVolume, "MasterValue");
            SeedSlider("SfxSlider", s.SfxVolume, "SfxValue");
            SeedSlider("MusicSlider", s.MusicVolume, "MusicValue");

            SeedCheck("FullscreenCheck", s.Fullscreen);
            SeedCheck("SubtitlesCheck", s.SubtitlesEnabled);
            SeedCheck("ScreenShakeCheck", s.ScreenShakeEnabled);
            SeedCheck("DamageNumbersCheck", s.DamageNumbersEnabled);
            SyncResolutionEnabled(s.Fullscreen);

            if (this.Find<OptionButton>("ResolutionOption") is { } res
                && s.ResolutionIndex >= 0 && s.ResolutionIndex < res.ItemCount)
                res.Selected = s.ResolutionIndex;

            if (this.Find<OptionButton>("LanguageOption") is { } loc)
            {
                int i = LocaleCodes.IndexOf(s.Language);
                if (i >= 0 && i < loc.ItemCount) loc.Selected = i;
            }
        }

        private void SeedSlider(string name, float value, string readout)
        {
            if (this.Find<Godot.Range>(name) is not { } slider) return;
            slider.SetValueNoSignal(value);
            SetReadout(readout, value);
        }

        private void SeedCheck(string name, bool value)
        {
            if (this.Find<CheckButton>(name) is { } check) check.SetPressedNoSignal(value);
        }

        /// <summary>Write the "80%" next to a slider. A volume slider with no number is the
        /// single most common complaint about a settings screen: the handle position is not a
        /// value, and the player cannot tell whether they just moved it by one or by ten.</summary>
        private void SetReadout(string? name, float value)
        {
            if (name == null) return;
            if (this.Find<Label>(name) is { } label) label.Text = $"{Mathf.RoundToInt(value)}%";
        }

        private void Bind(string name, bool current, System.Action<bool> apply)
        {
            if (this.Find<CheckButton>(name) is not { } check) return;
            check.ButtonPressed = current;
            check.Toggled += value =>
            {
                apply(value);
                UI.SettingsComponent.Instance?.SaveSettings();
            };
        }

        /// <summary>Bind a slider to a stored value. Applies on every change, so the volume
        /// moves under the player's finger.
        ///
        /// No explicit save here, deliberately: every SettingsComponent setter persists via
        /// Set(), and that write is debounced at the source. An earlier version of this saved
        /// on DragEnded to avoid per-frame writes — which achieved nothing, since the apply
        /// callback was already writing to disk on every change, and DragEnded only fires for
        /// mouse drags (keyboard and wheel adjustments would have skipped it entirely).</summary>
        private void Bind(string name, float current, System.Action<float> apply, string? readout = null)
        {
            if (this.Find<Godot.Range>(name) is not { } slider) return;

            // Seed before subscribing: assigning Value emits ValueChanged synchronously,
            // which would otherwise write the scene's literal back over the stored setting.
            slider.Value = current;
            SetReadout(readout, current);
            slider.ValueChanged += value =>
            {
                apply((float)value);
                SetReadout(readout, (float)value);
            };
        }

        /// <summary>Close correctly whether we are the current scene or an overlay.
        ///
        /// Opened from the main menu we ARE the scene, so we navigate back to it. Opened
        /// from the pause menu or the level map we are an overlay instanced over a live
        /// scene — navigating would destroy whatever is underneath (that was the bug:
        /// pause → settings → close threw away the running game). Freeing ourselves simply
        /// reveals it again, still paused.</summary>
        private void OnClosePressed()
        {
            if (GetTree()?.CurrentScene != this)
            {
                QueueFree();
                return;
            }
            ChangeScene(GameApp.Instance?.MainMenuPath);
        }

        // Shared helper: this method was byte-identical in all 33 screen scripts.
        private void ChangeScene(string? path) => UI.SceneNav.ChangeScene(this, path);
    }
}
