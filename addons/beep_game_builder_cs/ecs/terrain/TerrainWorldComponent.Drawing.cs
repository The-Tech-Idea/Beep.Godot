using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Drawing the built world: which renderers a projection uses, and how big
    /// the result is on screen.
    /// </summary>
    public partial class TerrainWorldComponent
    {
        /// <summary>
        /// Shows exactly the renderers this projection is drawn from, rebuilds
        /// them, and hides the rest.
        ///
        /// EVERY renderer is named here, including the ones that stay on. That is
        /// the point: the top-down feature renderer was the one of six whose
        /// visibility nobody set, and it stayed hidden by accident because at
        /// Node2D's default z it fell behind the isometric sea. When the props
        /// moved to their proper place above the stack the accident ran out and
        /// flat trees appeared standing on the open ocean. A renderer left out of
        /// this method is not "left alone" - it is left to whatever the last
        /// projection happened to do to it.
        ///
        /// The flat projections share the top-down feature renderer and the map
        /// overlay, because both stamp on the square grid; the isometric view has
        /// its own feature renderer drawing the same vegetation in its own
        /// projection.
        /// </summary>
        private void Draw(Vector2I size)
        {
            bool flat = Projection is not (TerrainProjection.Isometric or TerrainProjection.IsometricAutotile);

            // The tile size of whichever FLAT renderer is actually visible right
            // now, not always the painted one. Gating this on _painted alone left
            // a Tiles- or Isometric-only scene - one that never wires a painted
            // renderer - with its data-layer and overlay TileSize stuck on
            // whatever default they started at, unchecked against the renderer
            // actually on screen.
            int? flatTileSize = Projection switch
            {
                TerrainProjection.Painted => _painted?.TileSize,
                TerrainProjection.Tiles => _tiles is not null ? Mathf.Max(1, _tiles.AtlasTileSize.X) : null,
                _ => null,
            };

            // Cell data first, and for EVERY projection. It is what a game reads,
            // so it must not depend on which view happens to be drawn - that is
            // the whole reason it is not taken from the drawing layers.
            if (_dataLayers is not null)
            {
                _dataLayers.BoundsSize = size;
                if (flatTileSize is { } dataTileSize)
                    _dataLayers.TileSize = dataTileSize;
                _dataLayers.Rebuild();
            }

            // The painted renderer's C# type derives from Node while the scene
            // node it is attached to is a Node2D, so visibility is toggled
            // through the node rather than the component type.
            if (_paintedNode is not null)
                _paintedNode.Visible = Projection == TerrainProjection.Painted;

            if (_painted is not null && Projection == TerrainProjection.Painted)
            {
                _painted.BoundsSize = size;
                _painted.Rebuild();
            }

            // Vegetation is whatever the GENERATOR decided, drawn - not a second
            // scatter inventing its own placement from terrain kind. One owner.
            if (_features is not null)
            {
                _features.Visible = flat;
                if (flat)
                {
                    _features.BoundsSize = size;
                    _features.Seed = Mathf.Max(0, Seed);
                    _features.Rebuild();
                }
            }

            if (_tiles is not null)
            {
                _tiles.Visible = Projection == TerrainProjection.Tiles;
                if (Projection == TerrainProjection.Tiles)
                {
                    _tiles.BoundsSize = size;
                    _tiles.Rebuild();
                }
            }

            if (_iso is not null)
            {
                _iso.Visible = Projection == TerrainProjection.Isometric;
                if (Projection == TerrainProjection.Isometric)
                {
                    _iso.BoundsSize = size;
                    _iso.Rebuild();
                }
            }

            if (_isometricAutotile is not null)
            {
                _isometricAutotile.Visible = Projection == TerrainProjection.IsometricAutotile;
                if (Projection == TerrainProjection.IsometricAutotile)
                {
                    _isometricAutotile.BoundsSize = size;
                    _isometricAutotile.Rebuild();
                }
            }

            // The isometric feature renderer is placed against the BLOCK
            // renderer's geometry - its cell size and lifts - so it stays with
            // that projection rather than being reused here.
            if (_isometricFeatures is not null)
            {
                _isometricFeatures.Visible = Projection == TerrainProjection.Isometric;
                if (Projection == TerrainProjection.Isometric)
                {
                    _isometricFeatures.BoundsSize = size;
                    _isometricFeatures.Rebuild();
                }
            }

            // Relief objects and resource icons are stamped on the square grid,
            // so they belong to the flat projections for the same reason the
            // top-down feature renderer does.
            if (_relief is not null)
            {
                _relief.Visible = flat;
                if (flat)
                {
                    _relief.BoundsSize = size;
                    _relief.Seed = Mathf.Max(0, Seed);
                    _relief.Rebuild();
                }
            }

            if (_resources is not null)
            {
                _resources.Visible = flat;
                if (flat)
                {
                    _resources.BoundsSize = size;
                    _resources.Rebuild();
                }
            }

            // The flat overlay is drawn on the square tile grid, so it lines up
            // with the flat projections only. Left on for the isometric view it
            // would sit over the map in the wrong projection.
            if (_overlayNode is not null)
                _overlayNode.Visible = flat;

            if (_overlay is not null && flat)
            {
                _overlay.BoundsSize = size;
                if (flatTileSize is { } overlayTileSize)
                    _overlay.TileSize = overlayTileSize;
                _overlay.Rebuild();
            }
        }

        /// <summary>
        /// Where a player would begin, in the coordinates the renderers draw in.
        ///
        /// The isometric renderer owns the projection and the elevation rule, so
        /// it is ASKED where a cell's surface is rather than having that
        /// arithmetic copied here - a tree standing beside its own hill instead
        /// of on it is the usual symptom of the second copy.
        ///
        /// Falls back to the middle of the map only when the generator produced
        /// no start position at all.
        /// </summary>
        public Vector2 StartPositionView()
        {
            Resolve();
            if (_generator is null)
                return Vector2.Zero;

            Godot.Collections.Array<Vector2I> starts = _generator.GetStartPositions();
            Vector2I cell = starts.Count > 0 ? starts[0] : BuiltSize / 2;

            if (Projection == TerrainProjection.Isometric && _iso is not null)
                return _iso.SurfacePosition(cell);

            int tile = _painted?.TileSize ?? 64;
            return new Vector2((cell.X + 0.5f) * tile, (cell.Y + 0.5f) * tile);
        }

        /// <summary>
        /// The rectangle the built world occupies, in the coordinates of the node
        /// the renderers live under: Position is the top-left corner, Size the
        /// extent.
        ///
        /// A caller frames the map with this - by scaling a preview node, or by
        /// zooming a Camera2D - without knowing anything about projections. That
        /// arithmetic was written twice, once in the lab and once in the tile
        /// demo, and the two disagreed: the demo framed a flat rectangle for
        /// every view, so the isometric map, whose diamond extends to the LEFT of
        /// its own origin, fell half off the edge of the screen.
        /// </summary>
        public Rect2 PreviewExtent()
        {
            Resolve();
            Vector2I size = BuiltSize.X > 0 ? BuiltSize : TerrainMapSetup.BoundsFor(MapSize);

            if (Projection == TerrainProjection.Isometric && _iso is not null)
            {
                float halfWide = Mathf.Max(1, _iso.CellSize.X) * 0.5f;
                float halfHigh = Mathf.Max(1, _iso.CellSize.Y) * 0.5f;
                return new Rect2(
                    new Vector2(-size.Y * halfWide, -_iso.LevelHeight * 2),
                    new Vector2(
                        Mathf.Max(1.0f, (size.X + size.Y) * halfWide),
                        Mathf.Max(1.0f, ((size.X + size.Y) * halfHigh) + (_iso.LevelHeight * 2))));
            }

            int tile = _painted?.TileSize ?? 64;
            return new Rect2(
                Vector2.Zero,
                new Vector2(Mathf.Max(1, size.X * tile), Mathf.Max(1, size.Y * tile)));
        }
    }
}
