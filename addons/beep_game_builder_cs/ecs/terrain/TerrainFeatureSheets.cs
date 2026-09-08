using Godot;
using System.Collections.Generic;

namespace Beep.ECS;

/// <summary>
/// The prop sheets a feature renderer draws from, and the grid each one is cut on.
///
/// Both feature renderers - the flat view and the isometric one - carried their own
/// copy of this: the same four-sheet load with the same cache-on-paths, the same
/// feature-to-sheet fallbacks (jungle and oasis fall back to woods; marsh never does,
/// because a canopy standing in a bog misdescribes the ground), and the same
/// woods-only frame bindings.
///
/// They were not identical, and the difference was a defect rather than a divergence.
/// The flat view resolved each sheet's own columns and rows; the isometric copy set
/// columns and rows from WoodsColumns/WoodsRows and never varied them, while still
/// exposing three other sheet paths. So a marsh or oasis sheet authored on a different
/// grid was sliced correctly in one view and wrongly in the other, off the same map.
/// The isometric view did not even expose the per-sheet exports to get it right with.
///
/// A layout of zero INHERITS the woods layout, which is what the isometric view did to
/// every sheet before it had the exports. That is what lets it gain them without
/// re-cutting art that is already correct: terrain_iso_demo.tscn cuts an 8x1 marsh
/// sheet on WoodsColumns = 8, and says nothing about marsh columns.
/// </summary>
internal sealed class TerrainFeatureSheets
{
    /// <summary>One sheet's art with the grid it is cut on, resolved and ready to slice.</summary>
    internal readonly record struct Sheet(Texture2D Texture, int Columns, int Rows);

    /// <summary>
    /// A sheet as a view authors it. Columns or rows at or below zero inherit the
    /// woods layout; the woods layout itself falls back to a 4x4 grid.
    /// </summary>
    internal readonly record struct Layout(string Path, int Columns, int Rows);

    private readonly Dictionary<string, Sheet> _sheets = new();
    private readonly TerrainFeatureFrameBindings _woodsFrames = new();
    private (Layout Woods, Layout Jungle, Layout Oasis, Layout Marsh)? _loaded;

    /// <summary>How many sheets actually loaded. Zero means nothing can be drawn.</summary>
    public int Count => _sheets.Count;

    /// <summary>
    /// Loads whatever changed. Keyed on the layouts as well as the paths, so editing
    /// a sheet's columns takes effect without also having to change its path.
    ///
    /// The frame bindings are reparsed every call because their frame COUNT depends on
    /// the woods layout, and because the binding list is itself an export a scene can
    /// change between rebuilds.
    /// </summary>
    public void Load(string owner, Layout woods, Layout jungle, Layout oasis, Layout marsh, string[] woodsFrameBindings)
    {
        var wanted = (woods, jungle, oasis, marsh);
        if (_loaded != wanted)
        {
            _loaded = wanted;
            _sheets.Clear();

            // The woods layout is the fallback for the others, so it resolves first
            // and against a plain 4x4 rather than against itself.
            Layout resolvedWoods = Resolve(woods, new Layout(woods.Path, 4, 4));
            Add(owner, "woods", resolvedWoods);
            Add(owner, "jungle", Resolve(jungle, resolvedWoods));
            Add(owner, "oasis", Resolve(oasis, resolvedWoods));
            Add(owner, "marsh", Resolve(marsh, resolvedWoods));
        }

        Sheet woodsSheet = _sheets.TryGetValue("woods", out Sheet found) ? found : default;
        int frames = Mathf.Max(1, woodsSheet.Columns) * Mathf.Max(1, woodsSheet.Rows);
        _woodsFrames.Load(woodsFrameBindings, frames, owner);
    }

    /// <summary>
    /// The sheet a feature is drawn from, with its own grid.
    ///
    /// Jungle and oasis fall back to the woods sheet, so a map with only trees
    /// authored still shows vegetation rather than bare ground. Marsh deliberately
    /// does not: reeds are not trees, and the feature simply goes undrawn.
    /// </summary>
    public bool TryGet(string feature, out Sheet sheet)
    {
        string key = feature switch
        {
            // Dense forest is the same art, drawn thicker.
            TerrainFeatureStage.Woods or TerrainFeatureStage.Forest => "woods",
            TerrainFeatureStage.Jungle => _sheets.ContainsKey("jungle") ? "jungle" : "woods",
            TerrainFeatureStage.Oasis => _sheets.ContainsKey("oasis") ? "oasis" : "woods",
            TerrainFeatureStage.Marsh => _sheets.ContainsKey("marsh") ? "marsh" : string.Empty,
            _ => string.Empty,
        };

        if (key.Length == 0)
        {
            sheet = default;
            return false;
        }
        return _sheets.TryGetValue(key, out sheet);
    }

    /// <summary>
    /// Which frames of the sheet suit this terrain, or null for the whole sheet.
    ///
    /// Only the woods sheet is a climate MIX - cherry blossom and snow beside the
    /// plain greens - so only it is bound per terrain. The others are one subject
    /// each and use every frame they have.
    /// </summary>
    public int[]? FramesFor(in Sheet sheet, string terrainKind)
        => _sheets.TryGetValue("woods", out Sheet woods) && sheet.Texture == woods.Texture
            ? _woodsFrames.For(terrainKind)
            : null;

    private static Layout Resolve(Layout layout, Layout fallback) => new(
        layout.Path,
        layout.Columns > 0 ? layout.Columns : fallback.Columns,
        layout.Rows > 0 ? layout.Rows : fallback.Rows);

    private void Add(string owner, string key, Layout layout)
    {
        if (string.IsNullOrWhiteSpace(layout.Path))
            return;

        // Mipmaps matter more here than anywhere else a renderer loads art: a tree
        // frame is around 310 pixels and is drawn about ten across with the whole map
        // in view, a minification of thirty to one. The shared loader is what
        // guarantees the chain exists.
        Texture2D? texture = TerrainTextures.Load(layout.Path, owner, $"the {key} feature sheet");
        if (texture is null)
            return;

        _sheets[key] = new Sheet(texture, Mathf.Max(1, layout.Columns), Mathf.Max(1, layout.Rows));
    }
}
