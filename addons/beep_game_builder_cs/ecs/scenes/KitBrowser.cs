using Godot;
using System.Collections.Generic;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.Scenes
{
    /// <summary>
    /// Every Game UI Kit widget on one scrolling page, with a genre switcher and a material
    /// switcher.
    ///
    /// The material picker is the second comparison this board exists to make: a plate is either
    /// the register's band stack computed over the palette colour, or a neutral nine-slice sprite
    /// re-tinted to it. Which one applies is a property of the genre (overridable by its theme), so
    /// only one can be live at a time and the two cannot be shown side by side — flipping the
    /// picker changes every widget's material with nothing else moving, which is the comparison.
    /// See <see cref="PopulateMaterials"/> for why it drives the theme's own `kit.material` key
    /// rather than a switch of its own.
    ///
    /// Why it exists: the kit is GENRE-AWARE — <see cref="KitChrome.GenreOf"/> reads the global
    /// <see cref="SkinCatalog.ActiveGenre"/>, and shape/material/geometry/font/text-treatment all
    /// resolve from it (48 ForGenre call sites). So "what does the kit look like" has ten answers,
    /// and the only way to compare them was to render ten PNGs from a probe and flip between
    /// files. kit_gallery shows one register and has no picker; theme_gallery has pickers but
    /// covers a handful of Godot-derived controls, not the kit.
    ///
    /// Switching genre REBUILDS rather than redraws. Several widgets cache their genre in _Ready
    /// (`_genre = KitChrome.GenreOf(this)`) and only refresh on NotificationThemeChanged, so a
    /// bare QueueRedraw would leave those drawing the previous genre's silhouette — the exact
    /// class of half-applied state this repo keeps getting bitten by. Rebuilding is cheap here
    /// and cannot be half-right.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class KitBrowser : Godot.Control
    {
        // One place for the browser's own chrome type scale. These are the TOOL's sizes, not the
        // kit's — widget text sizes itself from UiSurface/the active theme and must not be
        // overridden here, or the board would stop showing what a genre actually does to type.
        private const int TitleFont = 26;
        private const int SectionFont = 17;
        private const int CaptionFont = 12;
        private const int SummaryFont = 13;
        private const float MinCellWidth = 96f;

        /// <summary>Genre to open on. Empty = citybuilder. Lets a scene or a probe open the board on a
        /// chosen register, which is also how the layout gets checked against a genre whose face
        /// is not RPG's wide display one — the sizes here were all judged against that.</summary>
        [Export] public string StartGenre { get; set; } = "citybuilder";

        /// <summary>Preferred theme for the starting genre. Empty = the genre default.</summary>
        [Export] public string StartTheme { get; set; } = "oilfield_days";

        /// <summary>
        /// Material to open on, in `kit.material` terms: `"ui_pack"` for the nine-slice artwork,
        /// `"none"` for the procedural stack. Empty opens on whatever the starting genre declares.
        ///
        /// Exists for the same reason <see cref="StartGenre"/> does: a render of this board is only
        /// evidence if the thing being compared can be chosen from outside, and both materials need
        /// capturing to compare them.
        /// </summary>
        [Export] public string StartMaterial { get; set; } = "";

        /// <summary>The material name the picker's "artwork" entry asks for. One set ships; when
        /// there are more this becomes a per-set entry rather than a boolean.</summary>
        private const string ArtworkMaterial = "ui_pack";

        private OptionButton? _genrePicker;
        private OptionButton? _materialPicker;
        private VBoxContainer? _content;
        private Label? _summary;
        private ThemePresetComponent? _theme;
        private readonly List<string> _genreIds = new();

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;
            BindChrome();
            PopulateGenres();
            PopulateMaterials();
            CallDeferred(nameof(DeferredInitialRebuild));
        }

        private void DeferredInitialRebuild()
            => Rebuild();

        // ── chrome ──────────────────────────────────────────────────────────────────────

        private void BindChrome()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);

            var title = this.Find<Label>("TitleLabel");
            if (title != null)
            {
                title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                KitChrome.SetFontSizeOverrideIfChanged(title, "font_size", TitleFont);
            }

            _genrePicker = RequireNode<OptionButton>("GenrePicker");
            _genrePicker.ItemSelected += _ => Rebuild();

            _materialPicker = RequireNode<OptionButton>("MaterialPicker");
            _materialPicker.ItemSelected += _ => Rebuild();

            // Build stamp. Not decoration: a stale Godot editor keeps its own loaded assembly, and
            // a window left open from an earlier run keeps the code it started with — both look
            // identical to "the fix did not work". This says which build you are actually looking
            // at, so that question can be answered by reading the screen.
            var stamp = this.Find<Label>("BuildStampLabel");
            if (stamp != null)
            {
                stamp.Text = "build " + BuildStamp();
                KitChrome.SetFontSizeOverrideIfChanged(stamp, "font_size", CaptionFont);
                KitChrome.SetColorOverrideIfChanged(stamp, "font_color", new Color(0.45f, 0.85f, 0.60f));
            }

            _summary = this.Find<Label>("SummaryLabel");
            if (_summary != null)
            {
                _summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                KitChrome.SetFontSizeOverrideIfChanged(_summary, "font_size", SummaryFont);
                KitChrome.SetColorOverrideIfChanged(_summary, "font_color", new Color(0.62f, 0.64f, 0.72f));
            }

            _content = RequireNode<VBoxContainer>("Content");
            _theme = RequireNode<ThemePresetComponent>("Theme");
        }

        private T RequireNode<T>(string name) where T : Node
        {
            if (this.Find<T>(name) is { } node)
                return node;
            throw new System.InvalidOperationException(
                $"{nameof(KitBrowser)} requires a design-time {typeof(T).Name} named '{name}'.");
        }

        private void PopulateGenres()
        {
            if (_genrePicker == null) return;
            _genrePicker.Clear();
            _genreIds.Clear();
            foreach (var kvp in SkinCatalog.AllGenres)
            {
                _genrePicker.AddItem(kvp.Value.DisplayName, _genreIds.Count);
                _genreIds.Add(kvp.Key);
            }
            if (_genreIds.Count == 0)
            {
                GD.PushWarning("[KitBrowser] SkinCatalog loaded no genres — the picker is empty and "
                             + "every widget will draw its fallback register. Check catalogs/skins/.");
                return;
            }
            string want = string.IsNullOrWhiteSpace(StartGenre) ? "citybuilder" : StartGenre.ToLowerInvariant();
            int idx = _genreIds.IndexOf(want);
            if (idx < 0 && !string.IsNullOrWhiteSpace(StartGenre))
                GD.PushWarning($"[KitBrowser] StartGenre '{StartGenre}' is not in the catalog — "
                             + $"opening on '{_genreIds[0]}' instead.");
            _genrePicker.Select(idx >= 0 ? idx : 0);
        }

        /// <summary>
        /// The two ways a kit plate can be built, so both can be seen on the same widgets.
        ///
        /// A plate is either the register's band stack — flat fill, rim, keyline, a seven-band
        /// vertical shade, bevel and gloss, all arithmetic over the palette colour — or a neutral
        /// nine-slice sprite re-tinted to that same colour. Which one a genre uses is a property of
        /// the genre, overridable by its theme, so there is exactly ONE answer live at a time and
        /// the two cannot be rendered side by side. This picker is how you see both: flip it and
        /// every widget on the page changes material with nothing else moving.
        ///
        /// It goes through the theme's own `kit.material` key rather than a separate runtime
        /// switch — the browser plays the part of a theme author, so the mechanism being
        /// demonstrated is the one a real theme would use, and the material keeps one owner.
        /// </summary>
        private void PopulateMaterials()
        {
            if (_materialPicker == null) return;
            _materialPicker.Clear();
            _materialPicker.AddItem("Artwork — nine-slice", 0);
            _materialPicker.AddItem("Procedural — drawn", 1);

            string wanted = StartMaterial?.Trim().ToLowerInvariant() ?? "";
            if (wanted.Length > 0)
            {
                _materialPicker.Select(wanted is "none" or "procedural" ? 1 : 0);
                return;
            }

            // Nothing asked for: open on whatever the starting genre actually declares, so the
            // board first shows the kit as it ships rather than as this tool prefers it. No theme
            // has been applied yet, so this reads the genre's built-in table entry, which is the
            // right basis.
            string genre = string.IsNullOrWhiteSpace(StartGenre) ? "citybuilder" : StartGenre.ToLowerInvariant();
            _materialPicker.Select(
                string.IsNullOrEmpty(KitGeometry.ForGenre(genre).Material) ? 1 : 0);
        }

        /// <summary>The material the picker is asking for, in `kit.material` terms.</summary>
        private string ChosenMaterial()
            => _materialPicker != null && _materialPicker.Selected == 1 ? "none" : ArtworkMaterial;

        /// <summary>
        /// Publish the active theme's `kit` block with the picker's material substituted in.
        ///
        /// A copy, never the catalog's own dictionary: that instance is the parsed theme and is
        /// handed out again on every skin change, so mutating it would make the tool's choice
        /// permanent for the session and follow the theme into any other scene that loads it.
        /// </summary>
        private void ApplyMaterialChoice(string genre, string theme)
        {
            var def = string.IsNullOrEmpty(genre) || string.IsNullOrEmpty(theme)
                ? null : SkinCatalog.GetTheme(genre, theme);
            var kit = def?.Kit != null
                ? (Godot.Collections.Dictionary)def.Kit.Duplicate()
                : new Godot.Collections.Dictionary();
            kit["material"] = ChosenMaterial();
            KitStyleJson.Set(genre, kit);
        }

        /// <summary>When the loaded assembly was written, HH:mm:ss. Read off the DLL rather than
        /// baked in, because the thing worth knowing is what the running process loaded.</summary>
        private static string BuildStamp()
        {
            try
            {
                string path = typeof(KitBrowser).Assembly.Location;
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                    return System.IO.File.GetLastWriteTime(path).ToString("HH:mm:ss");

                string assemblyName = typeof(KitBrowser).Assembly.GetName().Name ?? "";
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    string candidate = ProjectSettings.GlobalizePath(
                        $"res://.godot/mono/temp/bin/Debug/{assemblyName}.dll");
                    if (System.IO.File.Exists(candidate))
                        return System.IO.File.GetLastWriteTime(candidate).ToString("HH:mm:ss");
                }
            }
            catch (System.Exception e) { GD.PushWarning($"[KitBrowser] build stamp unavailable: {e.Message}"); }
            return "unknown";
        }

        private string CurrentGenre()
            => _genrePicker != null && _genrePicker.Selected >= 0 && _genrePicker.Selected < _genreIds.Count
                ? _genreIds[_genrePicker.Selected] : "";

        // ── build ───────────────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_content == null) return;

            string genre = CurrentGenre();
            // Genre is GLOBAL, not per-node: KitChrome.GenreOf ignores its argument and reads
            // SkinCatalog.ActiveGenre. Theme/palette/geometry are left empty so the comparison
            // varies exactly ONE axis — the same reason KitProofProbe holds colour constant.
            string theme = PreferredThemeFor(genre);
            _theme?.SetThemeSelection(genre, theme, "default", "");

            // AFTER the skin is published and BEFORE the widgets are built. SetThemeSelection puts
            // the theme's own `kit` block into KitStyleJson, so overriding earlier would be
            // overwritten; and several widgets measure themselves from the geometry in _Ready, so
            // overriding later would leave those sized for the other material. ApplyTheme below
            // does not republish the skin, so this stays the last word.
            ApplyMaterialChoice(genre, theme);

            // Everything except the themer — it lives here so it can theme this subtree, and
            // freeing it would take the palette with it.
            foreach (var child in _content.GetChildren())
                if (child != _theme) child.QueueFree();

            if (_summary != null)
                _summary.Text = $"Genre '{genre}' → silhouette {KitMaterial.ShapeForGenre(genre)}, "
                              + $"theme '{theme}'. {MaterialSummary(genre)}";

            Buttons();
            Meters();
            Panels();
            Inventory();
            Navigation();

            // AFTER the widgets exist: ThemePresetComponent walks its parent's subtree, so
            // applying before the rebuild would theme an empty container and leave every new
            // widget unthemed — the half-applied state this class rebuilds to avoid.
            if (_theme != null)
                _theme.ApplyTheme();
        }

        /// <summary>
        /// What the material picker is actually doing to this genre, in words.
        ///
        /// The interesting case is the one that looks broken: asking for artwork on a genre whose
        /// silhouette a nine-slice cannot serve changes nothing at all, because
        /// <see cref="KitSprite.FitsSilhouette"/> refuses anything but a rounded rectangle. Without
        /// this line that reads as a dead picker rather than as the rule working, which is the
        /// whole reason the shape axis survived the material being added.
        /// </summary>
        private string MaterialSummary(string genre)
        {
            if (ChosenMaterial() == "none")
                return "Plates are drawn PROCEDURALLY — the register's band stack over the palette "
                     + "colour. Switch Material to compare.";

            KitShape shape = KitMaterial.ShapeForGenre(genre);
            if (!KitSprite.FitsSilhouette(shape))
                return $"Artwork is requested, but this genre's {shape} silhouette is not a rounded "
                     + "rectangle, so its buttons and panels keep drawing procedurally — a "
                     + "nine-slice has one outline and the genre's own is worth more than the depth.";

            return "Plates are cut from ARTWORK — one neutral nine-slice set, re-tinted to this "
                 + "theme's palette. Switch Material to compare.";
        }

        private string PreferredThemeFor(string genre)
        {
            var genreDef = SkinCatalog.GetGenre(genre);
            if (genreDef == null) return "";

            if (genre.Equals((string.IsNullOrWhiteSpace(StartGenre) ? "citybuilder" : StartGenre).ToLowerInvariant(),
                    System.StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(StartTheme)
                && genreDef.Themes.ContainsKey(StartTheme.ToLowerInvariant()))
            {
                return StartTheme.ToLowerInvariant();
            }

            return genreDef.DefaultTheme ?? "";
        }

        private bool CompactLayout()
        {
            float width = Size.X > 0f ? Size.X : GetViewportRect().Size.X;
            return width > 0f && width < 560f;
        }

        private Container Section(string title)
        {
            var head = new Label { Text = title };
            KitChrome.SetFontSizeOverrideIfChanged(head, "font_size", SectionFont);
            KitChrome.SetColorOverrideIfChanged(head, "font_color", new Color(0.85f, 0.88f, 1f));
            _content!.AddChild(head);

            if (CompactLayout())
            {
                var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                KitChrome.SetConstantOverrideIfChanged(list, "separation", 14);
                _content.AddChild(list);
                return list;
            }

            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            KitChrome.SetConstantOverrideIfChanged(flow, "h_separation", 18);
            KitChrome.SetConstantOverrideIfChanged(flow, "v_separation", 16);
            _content.AddChild(flow);
            return flow;
        }

        /// <summary>One labelled cell. <paramref name="size"/> is a floor, not a cap: widgets that
        /// size themselves in _Ready keep their own minimum when it is larger.</summary>
        private static void Card(Container row, string name, Godot.Control w, Vector2 size)
        {
            var box = new VBoxContainer();
            if (row is VBoxContainer)
                box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            KitChrome.SetConstantOverrideIfChanged(box, "separation", 4);

            w.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            w.SizeFlagsVertical = SizeFlags.ShrinkCenter;

            // ADD FIRST, then raise the floor. Several widgets compute their own minimum in
            // _Ready behind `if (CustomMinimumSize != Vector2.Zero) return;` — setting a size
            // beforehand suppresses that calculation entirely and IMPOSES an aspect. That is what
            // squashed KitItemCard: its default Row layout wants 220-360 x 66-94, a pre-set
            // 215x215 square silenced ApplyMinimumSize, and the row's text drew off the card and
            // across two neighbours. AddChild runs _Ready, so by here the widget has had its say.
            box.AddChild(w);

            // Vector2.Zero means "you know best" — leave the widget's own minimum untouched.
            if (size != Vector2.Zero)
                w.CustomMinimumSize = new Vector2(Mathf.Max(w.CustomMinimumSize.X, size.X),
                                                  Mathf.Max(w.CustomMinimumSize.Y, size.Y));

            string caption = HumanizeCaption(name);
            var cap = new Label
            {
                Text = caption,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                // A floor on BOTH axes. Width so a long name wraps instead of stretching its cell
                // and ragging the flow. Height for two lines, because a genre with a wide display
                // face — cardgame's blocky caps are the worst — wraps captions the narrow faces
                // fit on one line, and the flow row had already been sized for one: "METER
                // (SEGMENTED)" and "RADIAL METER" ended up drawn across the widgets around them.
                CustomMinimumSize = new Vector2(Mathf.Max(size.X, MinCellWidth), CaptionFont * 2.5f),
            };
            KitChrome.SetFontSizeOverrideIfChanged(cap, "font_size", CaptionFont);
            KitChrome.SetColorOverrideIfChanged(cap, "font_color", new Color(0.72f, 0.75f, 0.84f));
            box.AddChild(cap);

            row.AddChild(box);
        }

        /// <summary>
        /// Pin a chip to a host widget's top-right corner, straddling it.
        ///
        /// A chip is not a small button, and shown free-floating in a row at button size that is
        /// exactly what it looks like. The kit's own art notes call the top-right corner straddle
        /// "the attention anchor" and <see cref="BadgeComponent"/> uses chips this way for real:
        /// as a CHILD of the control being marked, offset so it hangs over the corner. The board
        /// now shows them the way they are used instead of laid out like controls of their own.
        ///
        /// Anchored rather than positioned, so the chip stays on the corner when the host is
        /// resized by the cell's minimum-size floor.
        /// </summary>
        private static Godot.Control Pinned(Godot.Control host, KitChip chip)
        {
            host.AddChild(chip);

            Vector2 s = chip.GetCombinedMinimumSize();
            chip.SetAnchorsPreset(LayoutPreset.TopRight, keepOffsets: false);

            // INSIDE the host, flush into its top-right corner. An earlier version hung the chip
            // over the host's outer edge, which put it above the control entirely -- outside the
            // one rect a container has already allocated, so it would land on whatever sat above.
            // A badge overhangs the PLATE it marks, never the control's own bounds; that is the
            // rule KitChrome.DrawCornerBadge follows for the drawn badges and it holds here too.
            chip.OffsetLeft = -s.X;
            chip.OffsetRight = 0f;
            chip.OffsetTop = 0f;
            chip.OffsetBottom = s.Y;
            return host;
        }

        private static string HumanizeCaption(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            int variantStart = name.IndexOf(" (", System.StringComparison.Ordinal);
            string main = variantStart >= 0 ? name[..variantStart] : name;
            string variant = variantStart >= 0 ? name[variantStart..] : "";
            if (main.StartsWith("Kit", System.StringComparison.Ordinal))
                main = main[3..];

            var words = new System.Text.StringBuilder(main.Length + variant.Length + 6);
            for (int i = 0; i < main.Length; i++)
            {
                char c = main[i];
                bool startsWord = i > 0
                    && char.IsUpper(c)
                    && (char.IsLower(main[i - 1])
                     || (i + 1 < main.Length && char.IsLower(main[i + 1])));
                if (startsWord)
                    words.Append(' ');
                words.Append(c);
            }

            words.Append(variant);
            return words.ToString().Trim();
        }

        // ── sections ────────────────────────────────────────────────────────────────────

        private void Buttons()
        {
            var r = Section("Buttons & input");

            // Each of these has to show the thing that makes it NOT its neighbour. Shown bare,
            // KitPushButton and KitPushButton are the same yellow rectangle with different text, and
            // KitBuildTile is an empty square indistinguishable from an icon button — which is
            // exactly how the browser presented them, and it made three distinct widgets look like
            // one widget listed three times.
            Card(r, "KitPushButton", new KitPushButton { Text = "PLAY" }, new Vector2(130, 44));
            // The badge IS the reason this class exists over KitPushButton.
            Card(r, "KitPushButton (badge)", new KitPushButton { Text = "BUY", BadgeText = "250" }, new Vector2(130, 44));
            Card(r, "KitIconButton", new KitIconButton { Glyph = "+" }, new Vector2(52, 52));
            Card(r, "KitIconButton (locked)",
                 new KitIconButton { Glyph = "?", Locked = true, Requirement = "Lv 5" }, new Vector2(52, 52));
            // A build tile is icon + name + cost + how many you own. Empty, it is just a square.
            Card(r, "KitBuildTile",
                 new KitBuildTile { Caption = "Refinery", CostText = "120", OwnedText = "x3" },
                 new Vector2(84, 84));

            Card(r, "KitToggle (switch)", new KitToggle { ButtonPressed = true }, new Vector2(74, 40));
            Card(r, "KitToggle (box)", new KitToggle { Style = KitToggle.ToggleStyle.Box }, new Vector2(52, 46));
            Card(r, "KitCheckBox", new KitCheckBox { Text = "Enabled" }, new Vector2(150, 40));
            Card(r, "KitCheckButton", new KitCheckButton { Text = "Music" }, new Vector2(160, 40));

            Card(r, "KitSlider", Ranged(new KitSlider(), 0.62), new Vector2(150, 40));
            Card(r, "KitKnob", Ranged(new KitKnob(), 0.65), new Vector2(74, 74));

            var sel = new KitArrowSelector();
            sel.SetOptions(new[] { "EASY", "NORMAL", "HARD" }, 1);
            Card(r, "KitArrowSelector", sel, new Vector2(170, 42));

            var stars = new KitStarRating();
            stars.MaxValue = 5; stars.Step = 1; stars.Value = 3;
            Card(r, "KitStarRating", stars, new Vector2(140, 34));
        }

        private void Meters()
        {
            var r = Section("Meters & status");

            Card(r, "KitMeter (segmented)", Meter(0.62, 10, UiSurface.Role.Success), new Vector2(180, 26));
            Card(r, "KitMeter (continuous)", Meter(0.4, 0, UiSurface.Role.Danger), new Vector2(180, 26));
            // Every meter states its value EXPLICITLY, and the radial's ring agrees with the
            // number printed in it. The board showed `CentreText = "62"` over a ring left at its
            // 0.68 default, so the widget contradicted itself on screen and read as broken; the
            // orb and the heart row were built with no value at all and inherited a full one,
            // which renders as a plain green disc and five identical hearts.
            Card(r, "KitRadialMeter", new KitRadialMeter { Value = 0.62f, CentreText = "62" }, new Vector2(90, 90));
            Card(r, "KitOrbMeter", new KitOrbMeter { Value = 0.62f }, new Vector2(84, 84));
            // 3.5 of 5 shows all three states the row can draw at once: full, partial, empty.
            Card(r, "KitHeartRow", new KitHeartRow { MaxHearts = 5, Value = 3.5f }, new Vector2(180, 38));
            Card(r, "KitSpinner", new KitSpinner { Kind = KitSpinner.SpinnerKind.Ring }, new Vector2(64, 64));

            Card(r, "KitLabelValue", new KitLabelValue { Label = "ATTACK", Value = "7" }, new Vector2(150, 34));

            var cur = new KitCurrencyBar();
            cur.SetEntries(new[]
            {
                new KitCurrencyBar.Entry { Value = "12,480", Glyph = "$", Accent = UiSurface.Role.Warning },
                new KitCurrencyBar.Entry { Value = "340", Glyph = "*", Accent = UiSurface.Role.Info },
            });
            Card(r, "KitCurrencyBar", cur, new Vector2(300, 46));

            // Chips, shown ATTACHED. Every one of these is a marker pinned to something else --
            // an unread count on an icon, a rarity on an item, a requirement on a locked slot --
            // and a chip standing alone in a row is just a small button with a word in it.
            Card(r, "Chip: count on an icon",
                 Pinned(new KitIconButton { Glyph = "\u2709" },
                        new KitChip { Kind = KitChip.ChipKind.Count, Text = "12", Role = UiSurface.Role.Danger }),
                 new Vector2(52, 52));

            Card(r, "Chip: dot on an icon",
                 Pinned(new KitIconButton { Glyph = "\u2630" },
                        new KitChip { Kind = KitChip.ChipKind.Dot, Role = UiSurface.Role.Danger }),
                 new Vector2(52, 52));

            Card(r, "Chip: rarity on an item",
                 Pinned(new KitInventorySlot(),
                        new KitChip { Kind = KitChip.ChipKind.Rarity, Text = "EPIC", Role = UiSurface.Role.Accent2 }),
                 new Vector2(66, 66));

            Card(r, "Chip: lock on a slot",
                 Pinned(new KitInventorySlot(),
                        new KitChip { Kind = KitChip.ChipKind.Lock, Text = "Lv 8", Role = UiSurface.Role.Danger }),
                 new Vector2(66, 66));

            Card(r, "Chip: status on a slot",
                 Pinned(new KitInventorySlot(),
                        new KitChip { Kind = KitChip.ChipKind.Status, Positive = true, Role = UiSurface.Role.Success }),
                 new Vector2(66, 66));

            // Not a KitLabelValue host: that widget right-aligns its value, so a chip pinned to the
            // top-right corner lands straight on top of the number it is commenting on.
            Card(r, "Chip: delta on an item",
                 Pinned(new KitInventorySlot(),
                        new KitChip { Kind = KitChip.ChipKind.Delta, Delta = 3f, Positive = true }),
                 new Vector2(66, 66));
        }

        private void Panels()
        {
            var r = Section("Panels & messaging");

            Card(r, "KitPanel", new KitPanel { Title = "EQUIPMENT" }, new Vector2(220, 130));
            Card(r, "KitPanelContainer", new KitPanelContainer(), new Vector2(180, 110));
            Card(r, "KitCollapsiblePanel", new KitCollapsiblePanel { Title = "INVENTORY" }, new Vector2(220, 130));
            Card(r, "KitCollapsiblePanel (shut)",
                 new KitCollapsiblePanel { Title = "SHUT", Collapsed = true }, new Vector2(200, 46));
            Card(r, "KitBookSpread",
                 new KitBookSpread
                 {
                     Tabs = new[] { "Bag", "Quest", "Map", "Lore" },
                     LeftPageTitles = new[] { "Bag", "Quest", "Map", "Lore", "Crafting", "Reputation" },
                     RightPageTitles = new[] { "Equipment", "Rewards", "Markers", "Creatures", "Recipes", "Factions" },
                 }, new Vector2(330, 170));
            Card(r, "KitDialogBox", new KitDialogBox(), new Vector2(300, 110));
            Card(r, "KitSpeechBubble", new KitSpeechBubble(), new Vector2(220, 100));
            Card(r, "KitToast", new KitToast(), new Vector2(230, 60));
            Card(r, "KitTooltip", new KitTooltip { Text = "Restores 25 HP" }, new Vector2(200, 72));
            // Two cards, because one key and a word does not say what this widget is for. It is
            // the on-screen prompt -- "[E] Gather Wood" over a resource, "L2 + X" on a controller
            // -- and CHORD support is the stated reason it exists. Showing only a lone "E" left
            // the board with an unexplained keycap on it.
            Card(r, "KitInputHint", new KitInputHint { Keys = new[] { "E" }, Action = "Gather Wood" }, new Vector2(200, 46));
            Card(r, "KitInputHint (chord)", new KitInputHint { Keys = new[] { "L2", "X" }, Action = "Special" }, new Vector2(200, 46));
        }

        private void Inventory()
        {
            var r = Section("Inventory & progression");

            var grid = new KitSlotGrid { Columns = 4, Rows = 3, Selected = 1 };
            grid.SetSlots(new[]
            {
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Filled, Count = 12, Tint = UiSurface.Role.Info },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Filled, Count = 3 },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Invite },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Blank },
                new KitSlotGrid.Slot { Kind = KitSlotGrid.SlotKind.Locked, Requirement = "Lv 12" },
            });
            Card(r, "KitSlotGrid", grid, new Vector2(210, 160));

            Card(r, "KitInventorySlot", new KitInventorySlot(), new Vector2(66, 66));
            Card(r, "KitGemSlot", new KitGemSlot { State_ = KitGemSlot.SocketState.Invite }, new Vector2(66, 66));
            // Zero: KitItemCard sizes itself per Layout (Row is wide and short, Tile is narrow and
            // tall) and any floor we impose here silences that. Both layouts shown, because the
            // shape difference IS the widget.
            Card(r, "KitItemCard (row)", new KitItemCard { Layout = KitItemCardLayout.Row }, Vector2.Zero);
            Card(r, "KitItemCard (tile)", new KitItemCard { Layout = KitItemCardLayout.Tile }, Vector2.Zero);
            Card(r, "KitNodeCard",
                 new KitNodeCard { Title = "Iron Axe", Footer = KitNodeCard.FooterKind.Status, FooterText = "OWNED" },
                 new Vector2(130, 190));

            var tree = new KitTree { Columns = 4, Tiers = 3, Selected = 1 };
            tree.SetNodes(new[]
            {
                new KitTree.Node { Column = 1, Tier = 0, Branch = 0, State = KitTree.NodeState.Owned, Cost = 1 },
                new KitTree.Node { Column = 0, Tier = 1, Branch = 0, State = KitTree.NodeState.Available, Cost = 2, Parents = { 0 } },
                new KitTree.Node { Column = 2, Tier = 1, Branch = 1, State = KitTree.NodeState.Available, Cost = 2, Parents = { 0 } },
                new KitTree.Node { Column = 1, Tier = 2, Branch = 2, State = KitTree.NodeState.Locked, Parents = { 1 } },
            });
            Card(r, "KitTree", tree, new Vector2(240, 190));

            var path = new KitLevelPath { PerRow = 4 };
            path.SetLevels(new[]
            {
                new KitLevelPath.Level { Label = "1", State = KitLevelPath.LevelState.Complete, Stars = 3 },
                new KitLevelPath.Level { Label = "2", State = KitLevelPath.LevelState.Complete, Stars = 2 },
                new KitLevelPath.Level { Label = "3", State = KitLevelPath.LevelState.Available },
                new KitLevelPath.Level { Label = "4", State = KitLevelPath.LevelState.Locked },
            }, current: 2);
            Card(r, "KitLevelPath", path, new Vector2(260, 130));

            Card(r, "KitLevelButton", new KitLevelButton(), new Vector2(74, 74));
        }

        private void Navigation()
        {
            var r = Section("Navigation & ornament");

            var tabs = new KitTabStrip();
            tabs.SetTabs(new[]
            {
                new KitTabStrip.Tab { Text = "GEAR" },
                new KitTabStrip.Tab { Text = "MAP", Badge = 3 },
                new KitTabStrip.Tab { Text = "QUESTS" },
            });
            Card(r, "KitTabStrip", tabs, new Vector2(280, 42));

            Card(r, "KitPager", new KitPager { Page = 1, MaxDots = 5 }, new Vector2(180, 40));

            var seg = new KitSegmentedIconGroup { Current = 1 };
            seg.SetSegments(new[]
            {
                new KitSegmentedIconGroup.Segment { Glyph = "A" },
                new KitSegmentedIconGroup.Segment { Glyph = "B" },
                new KitSegmentedIconGroup.Segment { Glyph = "C" },
            });
            Card(r, "KitSegmentedIconGroup", seg, new Vector2(150, 42));

            Card(r, "KitRow",
                 new KitRow { Rank = "1", Title = "Ashvale", Subtitle = "Region", Value = "12,480" },
                 new Vector2(300, 52));

            Card(r, "KitAvatarFrame", new KitAvatarFrame { BadgeText = "24" }, new Vector2(84, 84));

            var radar = new KitRadarChart();
            radar.SetData(new[] { "ATK", "DEF", "SPD", "MAG", "LUK" },
                          new[] { 0.8f, 0.5f, 0.65f, 0.9f, 0.35f });
            Card(r, "KitRadarChart", radar, new Vector2(150, 150));

            Card(r, "KitOrnament", new KitOrnament { Kind = KitOrnament.OrnamentKind.Crown }, new Vector2(90, 60));
            Card(r, "KitPanelHanger", new KitPanelHanger { Kind = KitPanelHanger.HangerKind.Chain }, Vector2.Zero);
            var wheel = new KitSpinWheel();
            wheel.SetWedges(new[] { "50", "10", "x2", "5", "100", "1", "x3", "25" });
            Card(r, "KitSpinWheel", wheel, new Vector2(140, 140));
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────

        /// <summary>Set the domain BEFORE the value. Range.Step defaults to 1, so assigning 0.62
        /// first snaps it to 1.0 — the widgets restate the range in _Ready, which is too late for
        /// an assignment made here. This is what made a KitKnob read 100 instead of 65.</summary>
        private static T Ranged<T>(T range, double value) where T : Godot.Range
        {
            range.MinValue = 0.0;
            range.MaxValue = 1.0;
            range.Step = 0.001;
            range.Value = value;
            return range;
        }

        private static KitMeter Meter(double value, int segments, UiSurface.Role fill)
        {
            var m = Ranged(new KitMeter(), value);
            m.Segments = segments;
            m.Fill = fill;
            return m;
        }
    }
}
