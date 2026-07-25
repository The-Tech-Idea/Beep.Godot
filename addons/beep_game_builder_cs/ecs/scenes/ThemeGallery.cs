using Godot;
using System.Collections.Generic;
using System.Linq;
using Beep.ECS.UI;

namespace Beep.ECS.Scenes
{
    /// <summary>
    /// Every themed widget on one screen, with live genre / theme / palette pickers and a
    /// Textures on-off toggle.
    ///
    /// Why it exists: judging a skin previously meant opening several of 35 screens and
    /// remembering what the last one looked like, and there was no way at all to compare a
    /// baked 9-patch against the procedural StyleBoxFlat it replaces. Flip the toggle here and
    /// the difference is side by side — including the one regression that matters, a widget
    /// changing size because a baked texture's margins disagree with its theme.json.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class ThemeGallery : Control
    {
        private ThemePresetComponent? _theme;
        private OptionButton? _genre, _themePick, _palette;
        private CheckBox? _textures;
        private bool _loading;   // guards the programmatic Select() calls from re-entering handlers

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;

            _theme = this.Find<ThemePresetComponent>("Theme");
            _genre = this.Find<OptionButton>("GenreOption");
            _themePick = this.Find<OptionButton>("ThemeOption");
            _palette = this.Find<OptionButton>("PaletteOption");
            _textures = this.Find<CheckBox>("TexturesCheck");

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
            if (_textures != null)
            {
                _textures.ButtonPressed = _theme.UseTextures;
                _textures.Toggled += on => { _theme.UseTextures = on; };
            }

            // One control starts disabled and one starts focused, so the disabled and focus
            // StyleBoxes are actually visible — they are the two states nobody ever checks.
            this.Find<Button>("DisabledButton")?.SetDisabled(true);
            Callable.From(() => this.Find<Button>("NormalButton")?.GrabFocus()).CallDeferred();
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
                _themePick.Select(Mathf.Max(0, IndexOf(_themePick, genre?.DefaultTheme ?? "")));
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

        /// <summary>Push the three pickers onto the themer. Order matters only in that each
        /// setter re-applies, and ThemePresetComponent bails when a value is unchanged.</summary>
        private void Apply()
        {
            if (_loading || _theme == null || _genre == null || _themePick == null) return;
            if (_themePick.ItemCount == 0) return;

            _theme.GenreName = _genre.GetItemText(_genre.Selected);
            _theme.PresetName = _themePick.GetItemText(_themePick.Selected);
            _theme.PaletteName = _palette is { ItemCount: > 0 } ? _palette.GetItemText(_palette.Selected) : "Default";
        }

        private static int IndexOf(OptionButton o, string text)
        {
            for (int i = 0; i < o.ItemCount; i++)
                if (o.GetItemText(i) == text) return i;
            return -1;
        }
    }
}
