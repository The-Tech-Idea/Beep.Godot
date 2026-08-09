using Godot;
using System.Collections.Generic;
using Beep.ECS.UI;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.Scenes
{
    /// <summary>
    /// Every Game UI Kit widget on one scrolling page, with a genre switcher.
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

        /// <summary>Genre to open on. Empty = rpg. Lets a scene or a probe open the board on a
        /// chosen register, which is also how the layout gets checked against a genre whose face
        /// is not RPG's wide display one — the sizes here were all judged against that.</summary>
        [Export] public string StartGenre { get; set; } = "";

        private OptionButton? _genrePicker;
        private VBoxContainer? _content;
        private Label? _summary;
        private ThemePresetComponent? _theme;
        private readonly List<string> _genreIds = new();

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;
            BuildChrome();
            PopulateGenres();
            Rebuild();
        }

        // ── chrome ──────────────────────────────────────────────────────────────────────

        private void BuildChrome()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);

            var bg = new ColorRect { Color = new Color(0.09f, 0.09f, 0.12f) };
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(bg);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            foreach (string s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                margin.AddThemeConstantOverride(s, 18);
            AddChild(margin);

            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 10);
            margin.AddChild(root);

            // ── header ──
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 12);
            root.AddChild(header);

            var title = new Label { Text = "Beep Game UI Kit" };
            title.AddThemeFontSizeOverride("font_size", TitleFont);
            header.AddChild(title);

            header.AddChild(new Godot.Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

            header.AddChild(new Label { Text = "Genre:" });
            _genrePicker = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
            _genrePicker.ItemSelected += _ => Rebuild();
            header.AddChild(_genrePicker);

            // Build stamp. Not decoration: a stale Godot editor keeps its own loaded assembly, and
            // a window left open from an earlier run keeps the code it started with — both look
            // identical to "the fix did not work". This says which build you are actually looking
            // at, so that question can be answered by reading the screen.
            var stamp = new Label { Text = "build " + BuildStamp() };
            stamp.AddThemeFontSizeOverride("font_size", CaptionFont);
            stamp.AddThemeColorOverride("font_color", new Color(0.45f, 0.85f, 0.60f));
            header.AddChild(stamp);

            _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _summary.AddThemeFontSizeOverride("font_size", SummaryFont);
            _summary.AddThemeColorOverride("font_color", new Color(0.62f, 0.64f, 0.72f));
            root.AddChild(_summary);

            root.AddChild(new HSeparator());

            // ── scrolling body ──
            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            root.AddChild(scroll);

            _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _content.AddThemeConstantOverride("separation", 14);
            scroll.AddChild(_content);

            // Parented to the CONTENT control, not the root — ThemePresetComponent themes
            // GetParent()'s subtree. Without it UiSurface.Semantic finds no 'BeepSemantic' type
            // and every accent/success/danger resolves to a surface-derived grey, so the whole
            // board renders monochrome and a chip's colour tells you nothing.
            _theme = new ThemePresetComponent { Name = "Theme" };
            _content.AddChild(_theme);
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
            string want = string.IsNullOrWhiteSpace(StartGenre) ? "rpg" : StartGenre.ToLowerInvariant();
            int idx = _genreIds.IndexOf(want);
            if (idx < 0 && !string.IsNullOrWhiteSpace(StartGenre))
                GD.PushWarning($"[KitBrowser] StartGenre '{StartGenre}' is not in the catalog — "
                             + $"opening on '{_genreIds[0]}' instead.");
            _genrePicker.Select(idx >= 0 ? idx : 0);
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
            string theme = SkinCatalog.GetGenre(genre)?.DefaultTheme ?? "";
            SkinCatalog.SetActiveSkin(genre, theme, "default", "");

            // Everything except the themer — it lives here so it can theme this subtree, and
            // freeing it would take the palette with it.
            foreach (var child in _content.GetChildren())
                if (child != _theme) child.QueueFree();

            if (_summary != null)
                _summary.Text = $"Genre '{genre}' → silhouette {KitMaterial.ShapeForGenre(genre)}, "
                              + $"theme '{theme}'. Same widgets, same data — only the genre changed.";

            Buttons();
            Meters();
            Panels();
            Inventory();
            Navigation();

            // AFTER the widgets exist: ThemePresetComponent walks its parent's subtree, so
            // applying before the rebuild would theme an empty container and leave every new
            // widget unthemed — the half-applied state this class rebuilds to avoid.
            if (_theme != null)
            {
                _theme.GenreName = genre;
                _theme.PresetName = theme;
                _theme.PaletteName = "default";
                _theme.ApplyTheme();
            }
        }

        private HFlowContainer Section(string title)
        {
            var head = new Label { Text = title };
            head.AddThemeFontSizeOverride("font_size", SectionFont);
            head.AddThemeColorOverride("font_color", new Color(0.85f, 0.88f, 1f));
            _content!.AddChild(head);

            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            flow.AddThemeConstantOverride("h_separation", 18);
            flow.AddThemeConstantOverride("v_separation", 16);
            _content.AddChild(flow);
            return flow;
        }

        /// <summary>One labelled cell. <paramref name="size"/> is a floor, not a cap: widgets that
        /// size themselves in _Ready keep their own minimum when it is larger.</summary>
        private static void Card(HFlowContainer row, string name, Godot.Control w, Vector2 size)
        {
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 4);

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

            var cap = new Label
            {
                Text = name,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                // A floor, so a long widget name wraps instead of stretching its cell and
                // ragging the flow; the widget's own width still wins when it is wider.
                CustomMinimumSize = new Vector2(Mathf.Max(size.X, MinCellWidth), 0),
            };
            cap.AddThemeFontSizeOverride("font_size", CaptionFont);
            cap.AddThemeColorOverride("font_color", new Color(0.72f, 0.75f, 0.84f));
            box.AddChild(cap);

            row.AddChild(box);
        }

        // ── sections ────────────────────────────────────────────────────────────────────

        private void Buttons()
        {
            var r = Section("Buttons & input");

            Card(r, "KitPushButton", new KitPushButton { Text = "PLAY" }, new Vector2(130, 44));
            Card(r, "KitButton", new KitButton { Text = "BUY" }, new Vector2(130, 44));
            Card(r, "KitIconButton", new KitIconButton { Glyph = "+" }, new Vector2(52, 52));
            Card(r, "KitIconButton (locked)",
                 new KitIconButton { Glyph = "?", Locked = true, Requirement = "Lv 5" }, new Vector2(52, 52));
            Card(r, "KitBuildTile", new KitBuildTile(), new Vector2(84, 84));

            Card(r, "KitToggle (switch)", new KitToggle { ButtonPressed = true }, new Vector2(74, 40));
            Card(r, "KitToggle (box)", new KitToggle { Style = KitToggle.ToggleStyle.Box }, new Vector2(52, 46));
            Card(r, "KitCheckBox", new KitCheckBox { Text = "Enabled" }, new Vector2(150, 40));
            Card(r, "KitCheckButton", new KitCheckButton { Text = "Music" }, new Vector2(160, 40));

            Card(r, "KitSlider", Ranged(new KitSlider(), 0.62), new Vector2(150, 40));
            Card(r, "KitKnob", Ranged(new KitKnob(), 0.65), new Vector2(74, 74));

            var sel = new KitArrowSelector();
            sel.Options.AddRange(new[] { "EASY", "NORMAL", "HARD" });
            sel.Current = 1;
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
            Card(r, "KitRadialMeter", new KitRadialMeter { CentreText = "62" }, new Vector2(90, 90));
            Card(r, "KitOrbMeter", new KitOrbMeter(), new Vector2(84, 84));
            Card(r, "KitHeartRow", new KitHeartRow(), new Vector2(180, 38));
            Card(r, "KitSpinner", new KitSpinner { Kind = KitSpinner.SpinnerKind.Ring }, new Vector2(64, 64));

            Card(r, "KitLabelValue", new KitLabelValue { Label = "ATTACK", Value = "7" }, new Vector2(150, 34));

            var cur = new KitCurrencyBar();
            cur.Entries.Clear();
            cur.Entries.AddRange(new[]
            {
                new KitCurrencyBar.Entry { Value = "12,480", Glyph = "$", Accent = UiSurface.Role.Warning },
                new KitCurrencyBar.Entry { Value = "340", Glyph = "*", Accent = UiSurface.Role.Info },
            });
            Card(r, "KitCurrencyBar", cur, new Vector2(300, 46));

            Card(r, "KitChip (rarity)", new KitChip { Kind = KitChip.ChipKind.Rarity, Text = "EPIC" }, new Vector2(86, 32));
            Card(r, "KitChip (count)", new KitChip { Kind = KitChip.ChipKind.Count, Text = "12" }, new Vector2(52, 32));
            Card(r, "KitChip (delta)", new KitChip { Kind = KitChip.ChipKind.Delta, Delta = 3f, Positive = true }, new Vector2(70, 32));
            Card(r, "KitChip (lock)", new KitChip { Kind = KitChip.ChipKind.Lock, Text = "Lv 8" }, new Vector2(86, 32));
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
            Card(r, "KitInputHint", new KitInputHint { Action = "Interact" }, new Vector2(180, 46));
        }

        private void Inventory()
        {
            var r = Section("Inventory & progression");

            var grid = new KitSlotGrid { Columns = 4, Rows = 3, Selected = 1 };
            grid.Slots.AddRange(new[]
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
            tree.Nodes.AddRange(new[]
            {
                new KitTree.Node { Column = 1, Tier = 0, Branch = 0, State = KitTree.NodeState.Owned, Cost = 1 },
                new KitTree.Node { Column = 0, Tier = 1, Branch = 0, State = KitTree.NodeState.Available, Cost = 2 },
                new KitTree.Node { Column = 2, Tier = 1, Branch = 1, State = KitTree.NodeState.Available, Cost = 2 },
                new KitTree.Node { Column = 1, Tier = 2, Branch = 2, State = KitTree.NodeState.Locked },
            });
            tree.Nodes[1].Parents.Add(0);
            tree.Nodes[2].Parents.Add(0);
            tree.Nodes[3].Parents.Add(1);
            Card(r, "KitTree", tree, new Vector2(240, 190));

            var path = new KitLevelPath { PerRow = 4, Current = 2 };
            path.Levels.AddRange(new[]
            {
                new KitLevelPath.Level { Label = "1", State = KitLevelPath.LevelState.Complete, Stars = 3 },
                new KitLevelPath.Level { Label = "2", State = KitLevelPath.LevelState.Complete, Stars = 2 },
                new KitLevelPath.Level { Label = "3", State = KitLevelPath.LevelState.Available },
                new KitLevelPath.Level { Label = "4", State = KitLevelPath.LevelState.Locked },
            });
            Card(r, "KitLevelPath", path, new Vector2(260, 130));

            Card(r, "KitLevelButton", new KitLevelButton(), new Vector2(74, 74));
        }

        private void Navigation()
        {
            var r = Section("Navigation & ornament");

            var tabs = new KitTabStrip();
            tabs.Tabs.Clear();
            tabs.Tabs.AddRange(new[]
            {
                new KitTabStrip.Tab { Text = "GEAR" },
                new KitTabStrip.Tab { Text = "MAP", Badge = 3 },
                new KitTabStrip.Tab { Text = "QUESTS" },
            });
            Card(r, "KitTabStrip", tabs, new Vector2(280, 42));

            Card(r, "KitPager", new KitPager { Page = 1, MaxDots = 5 }, new Vector2(180, 40));

            var seg = new KitSegmentedIconGroup { Current = 1 };
            seg.Segments.AddRange(new[]
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
            radar.Axes.AddRange(new[] { "ATK", "DEF", "SPD", "MAG", "LUK" });
            radar.Values.AddRange(new[] { 0.8f, 0.5f, 0.65f, 0.9f, 0.35f });
            Card(r, "KitRadarChart", radar, new Vector2(150, 150));

            Card(r, "KitOrnament", new KitOrnament { Kind = KitOrnament.OrnamentKind.Crown }, new Vector2(90, 60));
            Card(r, "KitPanelHanger", new KitPanelHanger { Kind = KitPanelHanger.HangerKind.Chain }, Vector2.Zero);
            Card(r, "KitSpinWheel", new KitSpinWheel(), new Vector2(140, 140));
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
