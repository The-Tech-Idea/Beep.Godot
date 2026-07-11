using Godot;

namespace Beep.ECS.UI
{
    /// <summary>
    /// Per-node-type shape/sizing overrides pulled from a genre's geometry.json
    /// "shapes" block. Parsed by <see cref="SkinCatalog.ParseShapes"/> and held
    /// on <see cref="GeometryDef.Shapes"/>. Each nested type corresponds to one
    /// Godot UI node family (Panel, Input, Progress, …) and contains the
    /// numeric "knobs" the theming engine uses instead of hardcoded literals.
    ///
    /// Defaults match the legacy hardcoded values so an unmodified block is a
    /// visual no-op.
    /// </summary>
    public class ShapeOverrides
    {
        // Defaults ensure every genre ships without a `shapes` block and still
        // renders identically to the pre-refactor theming code.

        public PanelShape Panel = new();
        public InputShape Input = new();
        public ProgressShape Progress = new();
        public SliderShape Slider = new();
        public ScrollbarShape Scrollbar = new();
        public SelectionShape Selection = new();
        public SeparatorShape Separator = new();

        /// <summary>Panel-type nodes (Panel, PanelContainer, Window borders).</summary>
        public class PanelShape
        {
            /// <summary>Pixels subtracted from the genre-wide shadow size when building panel containers.</summary>
            public int ShadowReduction = 2;
        }

        /// <summary>Input-type nodes (LineEdit, TextEdit, SpinBox).</summary>
        public class InputShape
        {
            /// <summary>Horizontal padding inset subtracted from the genre pad (L/R).</summary>
            public int InsetX = 4;
            /// <summary>Vertical padding inset subtracted from the genre pad (T/B).</summary>
            public int InsetY = 3;
            /// <summary>Floor for the resulting left/right content margin.</summary>
            public int MinX = 4;
            /// <summary>Floor for the resulting top/bottom content margin.</summary>
            public int MinY = 2;
            /// <summary>Floor for the focused-state border width.</summary>
            public int FocusBorderMin = 2;
        }

        /// <summary>ProgressBar — background and fill boxes.</summary>
        public class ProgressShape
        {
            /// <summary>Pixels subtracted from the preset corner radius for both bg and fill.</summary>
            public int CornerInset = 4;
            /// <summary>4-side content margin for progress fill boxes.</summary>
            public int Margin = 2;
        }

        /// <summary>Slider grabber and track.</summary>
        public class SliderShape
        {
            /// <summary>Default drop-shadow size for the slider grabber.</summary>
            public int GrabberShadow = 3;
            /// <summary>Drop-shadow size when the grabber is hovered/highlighted.</summary>
            public int GrabberHoverShadow = 5;
            /// <summary>Multiplier applied to the slider track's corner radius.</summary>
            public float ShadowScale = 0.5f;
            /// <summary>Divisor for computing the slider track corner radius.</summary>
            public int TrackDivisor = 2;
        }

        /// <summary>ScrollBar grabber.</summary>
        public class ScrollbarShape
        {
            /// <summary>Divisor for computing the grabber corner radius from the genre's geometry.</summary>
            public int GrabberDivisor = 3;
            /// <summary>Floor for the computed grabber corner radius.</summary>
            public int GrabberMin = 3;
        }

        /// <summary>Selection highlight (Tree, ItemList, PopupMenu hover).</summary>
        public class SelectionShape
        {
            /// <summary>Divisor for the corner radius (corner_radius / divisor).</summary>
            public int CornerDivisor = 2;
            /// <summary>Floor for the computed selection corner radius.</summary>
            public int CornerMin = 2;
            /// <summary>Horizontal content margin for the selection box.</summary>
            public int MarginX = 4;
            /// <summary>Border width when the selection is the keyboard-focused row.</summary>
            public int FocusBorder = 1;
        }

        /// <summary>HSeparator / VSeparator spacing constant.</summary>
        public class SeparatorShape
        {
            /// <summary>Default separation between two separator-gapped children.</summary>
            public int Separation = 4;
        }
    }
}
