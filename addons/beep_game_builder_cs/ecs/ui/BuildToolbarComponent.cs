using Godot;
using System.Collections.Generic;
using Beep.ECS.UI.Kit;

namespace Beep.ECS.UI
{
    /// <summary>
    /// The docked build toolbar: category tabs along the bottom, each expanding into a palette
    /// of buildings with cost and affordability.
    ///
    /// This is the piece the City Builder HUD was missing. The genre's whole session is spent
    /// in this control — Cities: Skylines, SimCity and Anno all dock it permanently along the
    /// bottom. Ours existed only as `build_menu.tscn`, a FULL-SCREEN MODAL opened by a hotkey,
    /// which is the wrong shape: it covers the city you are building into and costs a keypress
    /// per placement.
    ///
    /// It is wired to <see cref="CityEconomyComponent"/>, so pressing an item actually spends
    /// treasury and changes the simulation. Unaffordable items are greyed rather than hidden,
    /// which keeps the player's sense of progression — hiding them is the common mistake.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class BuildToolbarComponent : UIComponent
    {
        /// <summary>Economy to buy from. Empty = search the scene for the first one.</summary>
        [Export] public NodePath EconomyPath { get; set; } = new("");
        [Export] public int ItemMinWidth { get; set; } = 96;

        /// <summary>Folder holding `icon_&lt;id&gt;.png` for each catalogue entry. A palette of
        /// icon tiles is what every city-builder reference uses; a column of
        /// "House x3 / 1,200" text rows is the thing that makes a build bar read as a list box.
        /// Missing icons fall back to the text-only tile rather than an empty square.</summary>
        [Export] public string IconFolder { get; set; } =
            "res://addons/beep_game_builder_cs/textures/citybuilder/icons";

        /// <summary>Emitted after a successful purchase, so a world layer (when a project has
        /// one) can place the building. The economy has already been debited.</summary>
        [Signal] public delegate void BuildingPurchasedEventHandler(string id);
        [Signal] public delegate void CategorySelectedEventHandler(string category);

        private CityEconomyComponent? _economy;
        private HBoxContainer? _tabs;
        private HBoxContainer? _palette;
        private readonly Dictionary<string, Button> _items = new();
        private readonly List<Button> _tabButtons = new();
        private string _category = "";

        public override void _Ready()
        {
            base._Ready();
            if (Engine.IsEditorHint()) return;
            // Deferred: a node cannot AddChild to a parent that is still inside its own
            // _Ready ("Parent node is busy setting up children"), which silently produced an
            // EMPTY widget — the code ran, the error went to the log, and the UI was blank.
            // GenreHudComponent already defers its Setup for the same reason.
            CallDeferred(nameof(Setup));
        }

        private void Setup()
        {
            _economy = ResolveEconomy();
            if (_economy == null)
            {
                GD.PushWarning($"[{Name}] BuildToolbarComponent found no CityEconomyComponent — a toolbar that cannot spend or check affordability would be decoration, so it is not built.");
                return;
            }

            Build();
            _economy.StatsChanged += RefreshAffordability;
            _economy.BuildingsChanged += RefreshAffordability;
            RefreshAffordability();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (_economy != null && GodotObject.IsInstanceValid(_economy))
            {
                _economy.StatsChanged -= RefreshAffordability;
                _economy.BuildingsChanged -= RefreshAffordability;
            }
            _economy = null;
        }

        private CityEconomyComponent? ResolveEconomy()
        {
            if (!EconomyPath.IsEmpty && GetNodeOrNull<CityEconomyComponent>(EconomyPath) is { } direct) return direct;
            var scene = GetTree()?.CurrentScene;
            return scene == null ? null : FindIn(scene);

            static CityEconomyComponent? FindIn(Node n)
            {
                if (n is CityEconomyComponent c) return c;
                foreach (var child in n.GetChildren())
                    if (FindIn(child) is { } found) return found;
                return null;
            }
        }

        private void Build()
        {
            if (GetParent() is not Godot.Control parent) return;
            int fs = UiSurface.FontSize(this);
            int captionFs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
            int smallFs = UiSurface.FontSize(this, UiSurface.TextRole.Small);

            var root = new VBoxContainer { Name = "Toolbar" };
            root.AddThemeConstantOverride("separation", 6);
            parent.AddChild(root);

            _tabs = new HBoxContainer { Name = "Categories" };
            _tabs.AddThemeConstantOverride("separation", 6);
            root.AddChild(_tabs);

            var scroll = new ScrollContainer
            {
                Name = "PaletteScroll",
                CustomMinimumSize = new Vector2(0, fs * 5.8f),
                VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            root.AddChild(scroll);

            _palette = new HBoxContainer { Name = "Palette", SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill };
            _palette.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_palette);

            // Categories in catalogue order, de-duplicated — adding a building type to the
            // catalogue adds it to the toolbar with no scene edit.
            var seen = new List<string>();
            foreach (var b in CityEconomyComponent.Catalogue)
                if (!seen.Contains(b.Category)) seen.Add(b.Category);

            foreach (string cat in seen)
            {
                string c = cat;
                var tab = new KitPushButton
                {
                    Name = $"Tab{cat}", Text = cat, ToggleMode = true,
                    CustomMinimumSize = new Vector2(0, fs * 2.4f),
                    FocusMode = Godot.Control.FocusModeEnum.None,
                };
                tab.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                tab.AddThemeFontSizeOverride("font_size", captionFs);
                tab.Pressed += () => SelectCategory(c);
                _tabs.AddChild(tab);
                _tabButtons.Add(tab);
            }

            if (seen.Count > 0) SelectCategory(seen[0]);
        }

        /// <summary>Show one category's palette. Categories stay visible while a palette is
        /// open — the player must never lose the way back.</summary>
        public void SelectCategory(string category)
        {
            if (_palette == null) return;
            int fs = UiSurface.FontSize(this);
            int captionFs = UiSurface.FontSize(this, UiSurface.TextRole.Caption);
            int smallFs = UiSurface.FontSize(this, UiSurface.TextRole.Small);
            _category = category;

            foreach (var t in _tabButtons)
                if (GodotObject.IsInstanceValid(t)) t.SetPressedNoSignal(t.Text == category);

            foreach (var child in _palette.GetChildren()) child.QueueFree();
            _items.Clear();

            foreach (var b in CityEconomyComponent.Catalogue)
            {
                if (b.Category != category) continue;
                string id = b.Id;
                var item = new KitBuildTile
                {
                    Name = $"Build_{b.Id}",
                    Caption = b.Display,
                    CostText = b.Cost.ToString("N0"),
                    TileIcon = LoadIcon(b.Id),
                    CustomMinimumSize = new Vector2(Mathf.Max(ItemMinWidth, Mathf.RoundToInt(fs * 5.8f)), fs * 6.0f),
                    TooltipText = Describe(b),
                };
                item.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                item.AddThemeFontSizeOverride("font_size", smallFs);
                item.Pressed += () => Purchase(id);
                _palette.AddChild(item);
                _items[b.Id] = item;
            }

            RefreshAffordability();
            EmitSignal(SignalName.CategorySelected, category);
        }

        /// <summary>Icon for a catalogue id, or null when the set does not ship one.</summary>
        private Texture2D? LoadIcon(string id)
        {
            if (string.IsNullOrEmpty(IconFolder)) return null;
            string path = $"{IconFolder.TrimEnd('/')}/icon_{id}.png";
            return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        }

        private static string Describe(CityEconomyComponent.BuildingDef b)
        {
            var parts = new List<string> { $"Cost {b.Cost:N0}", $"Upkeep {b.Upkeep:N0}/mo" };
            if (b.Residents > 0) parts.Add($"Houses {b.Residents}");
            if (b.Jobs > 0) parts.Add($"Jobs {b.Jobs}");
            if (b.Power != 0) parts.Add(b.Power > 0 ? $"Uses {b.Power} power" : $"Supplies {-b.Power} power");
            if (b.Water != 0) parts.Add(b.Water > 0 ? $"Uses {b.Water} water" : $"Supplies {-b.Water} water");
            if (b.Happiness != 0) parts.Add($"Happiness {b.Happiness:+0;-0}");
            return string.Join("\n", parts);
        }

        private void Purchase(string id)
        {
            if (_economy == null) return;
            if (_economy.TryPurchase(id)) EmitSignal(SignalName.BuildingPurchased, id);
            // The failure path is the economy's: it raises the alert. Nothing to do here.
        }

        /// <summary>Grey what the player cannot afford, and show how many they own. Runs on
        /// every economy change, so the toolbar can never claim an item is affordable when the
        /// treasury has moved underneath it.</summary>
        private void RefreshAffordability()
        {
            if (_economy == null) return;
            foreach (var (id, button) in _items)
            {
                if (!GodotObject.IsInstanceValid(button)) continue;
                var def = CityEconomyComponent.Find(id);
                if (def == null) continue;
                button.Disabled = !_economy.CanAfford(id);
                int owned = _economy.CountOf(id);
                string caption = owned > 0 ? $"{def.Display} ×{owned}" : def.Display;

                if (button is KitBuildTile tile)
                {
                    tile.Caption = caption;
                    tile.CostText = def.Cost.ToString("N0");
                    tile.OwnedText = owned > 0 ? owned.ToString() : "";
                }
                else
                    button.Text = $"{caption}\n{def.Cost:N0}";
            }
        }
    }
}
