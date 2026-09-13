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
        private TerrainDataLayersComponent? _drawnDataLayers;
        private TerrainGeneratorComponent? _drawnDataGenerator;

        private void Draw(Vector2I size, bool rebuildRecipeData = true, bool queueCollision = false, bool preparedAutotile = false)
        {
            RefreshStructureVisibility();
            bool propsFollowSurface = _iso is not null && _isometricFeatures?.FollowsSurface(_iso) == true;
            bool flat = Projection is not (TerrainProjection.Isometric or TerrainProjection.IsometricAutotile);
            if (_features is not null) _features.PropSizing = PropSizing;
            if (_relief is not null) _relief.PropSizing = PropSizing;
            if (_isometricFeatures is not null) _isometricFeatures.PropSizing = PropSizing;
            if (_painted is not null) _painted.MapArt = MapArt;
            if (_features is not null) _features.MapArt = Projection == TerrainProjection.Painted ? MapArt : null;
            if (_relief is not null) _relief.MapArt = Projection == TerrainProjection.Painted ? MapArt : null;

            // Surface events rebuild visible props synchronously; set visibility before emitting them.
            if (_isometricFeatures is not null)
            {
                _isometricFeatures.Seed = Mathf.Max(0, Seed);
                _isometricFeatures.BoundsSize = size;
                _isometricFeatures.Visible = Projection == TerrainProjection.Isometric;
            }

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
                NodePath dataGeneratorPath = _generator is null ? new NodePath("") : _dataLayers.GetPathTo(_generator);
                bool dataChanged = rebuildRecipeData || _drawnDataLayers != _dataLayers
                    || _drawnDataGenerator != _generator
                    || _dataLayers.TerrainGeneratorPath.ToString() != dataGeneratorPath.ToString()
                    || _dataLayers.BoundsOrigin != (_generator?.BoundsOrigin ?? Vector2I.Zero)
                    || _dataLayers.BoundsSize != size
                    || (flatTileSize.HasValue && _dataLayers.TileSize != flatTileSize.Value);
                _dataLayers.BoundsSize = size;
                _dataLayers.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _dataLayers.TerrainGeneratorPath = dataGeneratorPath;
                if (flatTileSize is { } dataTileSize)
                    _dataLayers.TileSize = dataTileSize;
                if (dataChanged)
                {
                    _dataLayers.Rebuild();
                    _drawnDataLayers = _dataLayers;
                    _drawnDataGenerator = _generator;
                }
            }

            if (_painted is not null)
            {
                _painted.BoundsSize = size;
                _painted.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _painted.Visible = Projection == TerrainProjection.Painted;
                if (Projection == TerrainProjection.Painted) _painted.Rebuild();
            }

            // Vegetation is whatever the GENERATOR decided, drawn - not a second
            // scatter inventing its own placement from terrain kind. One owner.
            if (_features is not null)
            {
                _features.Seed = Mathf.Max(0, Seed);
                _features.BoundsSize = size;
                _features.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _features.Visible = flat;
            }

            if (_tiles is not null)
            {
                _tiles.BoundsSize = size;
                _tiles.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _tiles.Visible = Projection == TerrainProjection.Tiles;
                if (Projection == TerrainProjection.Tiles)
                {
                    _tiles.Rebuild();
                }
            }

            if (_iso is not null)
            {
                // SurfaceRebuilt can immediately rebuild its attached props.
                if (_isometricFeatures is not null) _isometricFeatures.BoundsSize = size;
                _iso.BoundsSize = size;
                _iso.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _iso.Visible = Projection == TerrainProjection.Isometric;
                if (Projection == TerrainProjection.Isometric)
                {
                    _iso.Rebuild();
                }
            }

            if (_isometricAutotile is not null)
            {
                _isometricAutotile.BoundsSize = size;
                _isometricAutotile.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                if (preparedAutotile && Projection == TerrainProjection.IsometricAutotile) _isometricAutotile.ShowPrepared();
                else _isometricAutotile.Visible = Projection == TerrainProjection.IsometricAutotile;
                if (Projection == TerrainProjection.IsometricAutotile)
                {
                    if (!preparedAutotile) _isometricAutotile.Rebuild();
                }
            }

            // The isometric feature renderer is placed against the BLOCK
            // renderer's geometry - its cell size and lifts - so it stays with
            // that projection rather than being reused here.
            if (_isometricFeatures is not null)
            {
                if (Projection == TerrainProjection.Isometric)
                {
                    _isometricFeatures.BoundsSize = size;
                    if (!propsFollowSurface) _isometricFeatures.Rebuild();
                }
            }

            // Relief objects and resource icons are stamped on the square grid,
            // so they belong to the flat projections for the same reason the
            // top-down feature renderer does.
            if (_relief is not null)
            {
                _relief.BoundsSize = size;
                _relief.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _relief.Visible = flat;
            }

            if (_resources is not null)
            {
                _resources.BoundsSize = size;
                _resources.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _resources.Visible = flat;
            }

            // The flat overlay is drawn on the square tile grid, so it lines up
            // with the flat projections only. Left on for the isometric view it
            // would sit over the map in the wrong projection.
            if (_overlay is not null)
            {
                _overlay.BoundsSize = size;
                _overlay.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _overlay.Visible = flat;
            }

            var grid = BindGameplayGrid(size);
            // Preserve explicit missing wiring so views cannot fall back to a different grid.
            NodePath GridPathFrom(Node view) => GridPath.IsEmpty ? new NodePath("")
                : grid is null ? GetPath() : view.GetPathTo(grid);
            if (!CollisionPath.IsEmpty && GetNodeOrNull<TerrainCollisionComponent>(CollisionPath) is { } collision)
            {
                collision.GridPath = GridPathFrom(collision);
                collision.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                collision.BoundsSize = size;
                if (queueCollision) collision.RequestRebuild();
                else collision.Rebuild();
            }
            if (_features is not null && flat)
            {
                _features.BoundsSize = size;
                _features.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _features.GridPath = GridPathFrom(_features);
                if (flatTileSize is { } featureTileSize) _features.TileSize = featureTileSize;
                _features.Rebuild();
            }
            if (_resources is not null && flat)
            {
                _resources.BoundsSize = size;
                _resources.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _resources.GridPath = GridPathFrom(_resources);
                if (flatTileSize is { } resourceTileSize) _resources.TileSize = resourceTileSize;
                _resources.Rebuild();
            }
            if (_overlay is not null && flat)
            {
                _overlay.BoundsSize = size;
                _overlay.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _overlay.GridPath = GridPathFrom(_overlay);
                if (flatTileSize is { } overlayTileSize)
                    _overlay.TileSize = overlayTileSize;
                _overlay.Rebuild();
            }
            // Bind the new projection before relief queries gameplay cell positions.
            if (_relief is not null && flat)
            {
                _relief.BoundsSize = size;
                _relief.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _relief.Seed = Mathf.Max(0, Seed);
                _relief.GridPath = GridPathFrom(_relief);
                if (flatTileSize is { } reliefTileSize) _relief.TileSize = reliefTileSize;
                _relief.Rebuild();
            }
        }

        private GridProjectionComponent? BindGameplayGrid(Vector2I size)
        {
            var grid = GridPath.IsEmpty ? null : GetNodeOrNull<GridProjectionComponent>(GridPath);
            var navigation = NavigationPath.IsEmpty ? null : GetNodeOrNull<GridNavigationComponent>(NavigationPath);
            if (navigation is not null)
            {
                navigation.UseBounds = true;
                navigation.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                navigation.BoundsSize = size;
                if (!GridPath.IsEmpty)
                    navigation.GridPath = grid is null ? GetPath() : navigation.GetPathTo(grid);
            }
            if (grid is null) return null;
            grid.ElevatedTerrainPath = new NodePath("");
            if (Projection == TerrainProjection.Isometric && _iso is not null)
            {
                grid.ElevatedTerrainPath = grid.GetPathTo(_iso);
                grid.TileMapLayerPath = new NodePath("");
            }
            else
            {
                TileMapLayer? layer = Projection switch
                {
                    TerrainProjection.Painted => _painted?.GetTerrainLayer(),
                    TerrainProjection.Tiles => _tiles?.GetTerrainLayer(),
                    TerrainProjection.IsometricAutotile => _isometricAutotile?.GetTerrainLayer(),
                    _ => null
                };
                // An explicitly unavailable view must not silently use the manual grid.
                grid.TileMapLayerPath = layer is null ? GetPath() : grid.GetPathTo(layer);
            }
            grid.NotifyGeometryChanged();
            grid.UpdateConfigurationWarnings();
            return grid;
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
                return _iso.Transform * _iso.SurfacePosition(_iso.BoundsOrigin + cell);

            if (Projection == TerrainProjection.IsometricAutotile && _isometricAutotile is not null)
                return _isometricAutotile.Transform * _isometricAutotile.CellPosition(_isometricAutotile.BoundsOrigin + cell);

            Vector2 tile = FlatViewTileSize();
            cell += _generator.BoundsOrigin;
            return RendererTransform() * new Vector2((cell.X + 0.5f) * tile.X, (cell.Y + 0.5f) * tile.Y);
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

            if (Projection == TerrainProjection.IsometricAutotile && _isometricAutotile is not null)
                return _isometricAutotile.Transform * _isometricAutotile.GridExtent(size);

            if (Projection == TerrainProjection.Isometric && _iso is not null)
            {
                return _iso.Transform * _iso.SurfaceExtent;
            }

            Vector2 tile = FlatViewTileSize();
            return RendererTransform() * new Rect2(
                new Vector2((_generator?.BoundsOrigin.X ?? 0) * tile.X, (_generator?.BoundsOrigin.Y ?? 0) * tile.Y),
                new Vector2(Mathf.Max(1, size.X * tile.X), Mathf.Max(1, size.Y * tile.Y)));
        }

        private Vector2 FlatViewTileSize() => Projection == TerrainProjection.Tiles && _tiles is not null
            ? new Vector2(Mathf.Max(1, _tiles.AtlasTileSize.X), Mathf.Max(1, _tiles.AtlasTileSize.Y))
            : Vector2.One * Mathf.Max(1, _painted?.TileSize ?? 64);

        private Node2D? ActiveRenderer() => Projection switch
        {
            TerrainProjection.Painted => _painted,
            TerrainProjection.Tiles => _tiles,
            TerrainProjection.Isometric => _iso,
            TerrainProjection.IsometricAutotile => _isometricAutotile,
            _ => null
        };

        private Transform2D RendererTransform() => ActiveRenderer()?.Transform ?? Transform2D.Identity;

        // Preview queries are in the active renderer's parent coordinates; camera queries
        // include that parent's canvas transform without applying the renderer twice.
        private Transform2D ViewToGlobalTransform()
        {
            var renderer = ActiveRenderer();
            return renderer is null ? Transform2D.Identity
                : renderer.GlobalTransform * renderer.Transform.AffineInverse();
        }

        public Vector2 StartPositionGlobal()
        {
            Vector2 position = StartPositionView();
            return ViewToGlobalTransform() * position;
        }

        public Rect2 WorldExtent()
        {
            Rect2 extent = PreviewExtent();
            return ViewToGlobalTransform() * extent;
        }
    }
}
