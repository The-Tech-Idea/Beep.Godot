using Godot;
using System.Collections.Generic;
using System.Linq;
using Beep.ECS.UI;

namespace Beep.ECS.Scenes
{
    /// <summary>
    /// Every themed widget on one screen, with live genre / theme / palette pickers.
    ///
    /// Why it exists: judging a skin previously meant opening several screens and remembering
    /// what the last one looked like. This scene keeps every control type together so geometry,
    /// palette, focus states, and compact HUD treatment can be reviewed in one place.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ThemeGallery : Control
    {
        private ThemePresetComponent? _theme;
        private OptionButton? _genre, _themePick, _palette;
        private ItemList? _sampleItems;
        private Tree? _sampleTree;
        private OptionButton? _sampleOption;
        private bool _loading;   // guards the programmatic Select() calls from re-entering handlers

        public override void _Ready()
        {
            PopulateControlSamples();
            if (Engine.IsEditorHint()) return;

            _theme = this.Find<ThemePresetComponent>("Theme");
            _genre = this.Find<OptionButton>("GenreOption");
            _themePick = this.Find<OptionButton>("ThemeOption");
            _palette = this.Find<OptionButton>("PaletteOption");

            if (_theme == null)
            {
                // Without the themer this screen is a plain grey form and nothing it shows means
                // anything — say so rather than let it look like the theme is broken.
                GD.PushWarning($"[{Name}] no ThemePresetComponent named 'Theme' — the gallery cannot preview anything.");
                return;
            }

            FillGenres();
            if (_genre != null) _genre.ItemSelected += _ => { OnGenreChanged(); };
            if (_themePick != null) _themePick.ItemSelected += _ => { OnThemeChanged(); };
            if (_palette != null) _palette.ItemSelected += _ => { Apply(); };
            // One control starts disabled and one starts focused, so the disabled and focus
            // StyleBoxes are actually visible — they are the two states nobody ever checks.
            this.Find<Button>("DisabledButton")?.SetDisabled(true);
            Callable.From(() => this.Find<Button>("NormalButton")?.GrabFocus()).CallDeferred();
        }

        private void PopulateControlSamples()
        {
            _sampleOption = this.Find<OptionButton>("SampleOption");
            if (_sampleOption != null && _sampleOption.ItemCount == 0)
            {
                _sampleOption.AddItem("Windowed");
                _sampleOption.AddItem("Borderless");
                _sampleOption.AddItem("Fullscreen");
                _sampleOption.Select(1);
            }

            _sampleItems = this.Find<ItemList>("SampleItemList");
            if (_sampleItems != null && _sampleItems.ItemCount == 0)
            {
                _sampleItems.AddItem("Gather wood");
                _sampleItems.AddItem("Build workshop");
                _sampleItems.AddItem("Assign workers");
                _sampleItems.Select(1);
            }

            _sampleTree = this.Find<Tree>("SampleTree");
            if (_sampleTree != null && TreeIsEmpty(_sampleTree))
            {
                _sampleTree.HideRoot = true;
                _sampleTree.Columns = 1;
                _sampleTree.Clear();

                TreeItem root = _sampleTree.CreateItem();
                TreeItem economy = _sampleTree.CreateItem(root);
                economy.SetText(0, "Camp");
                TreeItem storage = _sampleTree.CreateItem(economy);
                storage.SetText(0, "Storage");
                TreeItem workshop = _sampleTree.CreateItem(economy);
                workshop.SetText(0, "Workshop");

                TreeItem routes = _sampleTree.CreateItem(root);
                routes.SetText(0, "Routes");
                TreeItem road = _sampleTree.CreateItem(routes);
                road.SetText(0, "Road crew");
                TreeItem convoy = _sampleTree.CreateItem(routes);
                convoy.SetText(0, "Supply run");

                economy.Collapsed = false;
                routes.Collapsed = false;
                _sampleTree.SetSelected(workshop, 0);
            }
        }

        private static bool TreeIsEmpty(Tree tree)
        {
            TreeItem? root = tree.GetRoot();
            return root == null || root.GetFirstChild() == null;
        }

        private void FillGenres()
        {
            if (_genre == null) return;
            _loading = true;
            _genre.Clear();
            foreach (var id in SkinCatalog.AllGenres.Keys.OrderBy(k => k)) _genre.AddItem(id);
            int start = Mathf.Max(0, IndexOf(_genre, _theme!.GenreName));
            _genre.Select(start);
            _loading = false;
            OnGenreChanged();
        }

        private void OnGenreChanged()
        {
            if (_loading || _genre == null || _themePick == null) return;
            string genreId = _genre.GetItemText(_genre.Selected);
            var genre = SkinCatalog.GetGenre(genreId);

            _loading = true;
            _themePick.Clear();
            foreach (var id in (genre?.Themes.Keys ?? Enumerable.Empty<string>()).OrderBy(k => k))
                _themePick.AddItem(id);
            if (_themePick.ItemCount > 0)
            {
                int authored = IndexOf(_themePick, _theme?.PresetName ?? "");
                int fallback = IndexOf(_themePick, genre?.DefaultTheme ?? "");
                _themePick.Select(authored >= 0 ? authored : Mathf.Max(0, fallback));
            }
            _loading = false;

            OnThemeChanged();
        }

        private void OnThemeChanged()
        {
            if (_loading || _genre == null || _themePick == null) return;
            if (_palette != null)
            {
                string genreId = _genre.GetItemText(_genre.Selected);
                string themeId = _themePick.ItemCount > 0 ? _themePick.GetItemText(_themePick.Selected) : "";
                var def = SkinCatalog.GetTheme(genreId, themeId);

                _loading = true;
                _palette.Clear();
                _palette.AddItem("Default");
                foreach (var p in (def?.Palettes.Keys ?? Enumerable.Empty<string>()).OrderBy(k => k))
                    _palette.AddItem(p);
                _palette.Select(0);
                _loading = false;
            }
            Apply();
        }

        /// <summary>Publish the selected skin once, then apply the preview theme.</summary>
        private void Apply()
        {
            if (_loading || _theme == null || _genre == null || _themePick == null) return;
            if (_themePick.ItemCount == 0) return;

            string genre = _genre.GetItemText(_genre.Selected);
            string theme = _themePick.GetItemText(_themePick.Selected);
            string palette = _palette is { ItemCount: > 0 } ? _palette.GetItemText(_palette.Selected) : "Default";

            _theme.SetThemeSelection(
                genre,
                theme,
                palette);
        }

        private static int IndexOf(OptionButton o, string text)
        {
            for (int i = 0; i < o.ItemCount; i++)
                if (o.GetItemText(i) == text) return i;
            return -1;
        }
    }
}
