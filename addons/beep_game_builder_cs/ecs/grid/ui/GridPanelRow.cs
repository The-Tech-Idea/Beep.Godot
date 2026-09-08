using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// One row a <see cref="GridListPanelComponent"/> renders: the key it is
    /// reused and sorted by, the text to show, and the colour for its state.
    ///
    /// The key is what makes the panel's row diff possible - a row Label is
    /// created once per id and reused on every later refresh, instead of the
    /// whole list being freed and rebuilt four times a second.
    /// </summary>
    public readonly struct GridPanelRow
    {
        public GridPanelRow(string id, string text, Color color, string tooltip = "")
        {
            Id = id;
            Text = text;
            Color = color;
            Tooltip = string.IsNullOrWhiteSpace(tooltip) ? id : tooltip;
        }

        public string Id { get; }
        public string Text { get; }
        public Color Color { get; }

        /// <summary>Hover text; the id itself when the panel has nothing better to say.</summary>
        public string Tooltip { get; }
    }
}
