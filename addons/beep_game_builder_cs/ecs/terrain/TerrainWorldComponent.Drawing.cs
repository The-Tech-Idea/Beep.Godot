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
        /// Every view the gameplay grid is bound to - Painted, Tiles and
        /// IsometricAutotile - shares the companions: vegetation, relief props,
        /// resource icons and the map overlay. They place and size every stamp
        /// through the grid's CellToWorld/CellCorners, so they stand on a square
        /// cell or a diamond alike. Only the block view has its own feature
        /// renderer, because its props stand on a stacked surface rather than on
        /// the grid. Gating the companions on "flat" left IsometricAutotile a
        /// bare map with no trees, rocks, resources or start markers.
        /// </summary>
        private TerrainDataLayersComponent? _drawnDataLayers;
        private TerrainGeneratorComponent? _drawnDataGenerator;

        private void Draw(Vector2I size, bool rebuildRecipeData = true, bool queueCollision = false, bool preparedAutotile = false)
        {
            RefreshStructureVisibility();
            bool propsFollowSurface = _iso is not null && _isometricFeatures?.FollowsSurface(_iso) == true;
            // The companions are drawn wherever the grid binds a native layer - see
            // BindGameplayGrid. The block view binds its own elevated surface instead.
            bool gridBound = Projection is not TerrainProjection.Isometric;
            if (_features is not null) _features.PropSizing = PropSizing;
            if (_relief is not null) _relief.PropSizing = PropSizing;
            if (_isometricFeatures is not null) _isometricFeatures.PropSizing = PropSizing;
            // One sea for the world, pushed exactly as PropSizing is: the views that draw water
            // read the same dials, so switching view cannot change the ocean (VIEW-04).
            if (_painted is not null) _painted.WaterLook = WaterLook;
            if (_tiles is not null) _tiles.WaterLook = WaterLook;
            if (_iso is not null) _iso.WaterLook = WaterLook;
            if (_isometricAutotile is not null) _isometricAutotile.WaterLook = WaterLook;
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

            // No tile size is owned here. Each surface owns its cell geometry, the
            // gameplay grid reports it (GridProjectionComponent.EffectiveTileSize,
            // CellToWorld, CellCorners), and the companions place and size through
            // the grid. This method used to derive a flat size from whichever
            // renderer was active and copy it into the data layers - whose TileSize
            // is their own metadata atlas size, not a view fact - and into four
            // companion exports, keeping five copies in step with the surface.

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
                    || _dataLayers.BoundsSize != size;
                _dataLayers.BoundsSize = size;
                _dataLayers.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _dataLayers.TerrainGeneratorPath = dataGeneratorPath;
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
                _features.Visible = gridBound;
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

            // Relief objects, resource icons and the overlay are stamped through
            // the grid binding, so they follow the same rule as the feature
            // renderer above. The block view is the exception: its relief is the
            // blocks themselves, and an overlay there would sit on the flat grid
            // beneath a raised surface.
            if (_relief is not null)
            {
                _relief.BoundsSize = size;
                _relief.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _relief.Visible = gridBound;
            }

            if (_resources is not null)
            {
                _resources.BoundsSize = size;
                _resources.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _resources.Visible = gridBound;
            }

            if (_overlay is not null)
            {
                _overlay.BoundsSize = size;
                _overlay.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _overlay.Visible = gridBound;
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
            if (_features is not null && gridBound)
            {
                _features.BoundsSize = size;
                _features.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _features.GridPath = GridPathFrom(_features);
                _features.Rebuild();
            }
            if (_resources is not null && gridBound)
            {
                _resources.BoundsSize = size;
                _resources.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _resources.GridPath = GridPathFrom(_resources);
                _resources.Rebuild();
            }
            if (_overlay is not null && gridBound)
            {
                _overlay.BoundsSize = size;
                _overlay.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _overlay.GridPath = GridPathFrom(_overlay);
                _overlay.Rebuild();
            }
            // After the grid is bound: a marker's position comes from the grid's own geometry.
            if (!SpawnsPath.IsEmpty) EmitSpawnMarkers(grid);

            // Bind the new projection before relief queries gameplay cell positions.
            if (_relief is not null && gridBound)
            {
                _relief.BoundsSize = size;
                _relief.BoundsOrigin = _generator?.BoundsOrigin ?? Vector2I.Zero;
                _relief.Seed = Mathf.Max(0, Seed);
                _relief.GridPath = GridPathFrom(_relief);
                _relief.Rebuild();
            }
        }

        /// <summary>
        /// Publishes this world's starts as scene nodes under SpawnsPath. Rewritten on every
        /// draw, because the starts belong to the world that was last built: a map redrawn after
        /// a new world must not keep the previous one's markers.
        /// </summary>
        private void EmitSpawnMarkers(GridProjectionComponent? grid)
        {
            if (GetNodeOrNull<Node2D>(SpawnsPath) is not { } spawns)
            {
                GD.PushWarning($"[{Name}] SpawnsPath '{SpawnsPath}' is not a Node2D; no start markers were written.");
                return;
            }
            if (_generator is null || grid is null)
            {
                // Nothing to publish, and nothing to publish it against: leave whatever is there
                // rather than clearing an authored map's own markers.
                GD.PushWarning($"[{Name}] start markers need a generator and a gameplay grid; none were written.");
                return;
            }

            TerrainSpawnMarkers.Emit(spawns, _generator.ResolveField().StartAreas, grid, _generator.BoundsOrigin);
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
                TileMapLayer? layer = Projection == TerrainProjection.IsometricAutotile
                    ? _isometricAutotile?.GetTerrainLayer()
                    : FlatTerrainLayer();
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
        public Vector2 StartPositionView() => StartPositionViewAt(0);

        /// <summary>
        /// Where start <paramref name="index"/> begins, in the coordinates the renderers draw
        /// in - start k of the generator's order, the same k GridStartAreaComponent and the
        /// cells' terrain_start_area name. A map with no starts answers the middle of the map,
        /// as above; an index past the last start warns and answers the same, because a camera
        /// sent to a start that does not exist has nowhere truer to look.
        /// </summary>
        public Vector2 StartPositionViewAt(int index)
        {
            Resolve();
            if (_generator is null)
                return Vector2.Zero;

            Godot.Collections.Array<Vector2I> starts = _generator.GetStartPositions();
            if (starts.Count > 0 && (index < 0 || index >= starts.Count))
                GD.PushWarning($"[{Name}] start {index} does not exist - the map has {starts.Count}; showing the middle of the map.");
            Vector2I cell = index >= 0 && index < starts.Count ? starts[index] : BuiltSize / 2;

            if (Projection == TerrainProjection.Isometric && _iso is not null)
                return _iso.Transform * _iso.SurfacePosition(_iso.BoundsOrigin + cell);

            if (Projection == TerrainProjection.IsometricAutotile && _isometricAutotile is not null)
                return _isometricAutotile.Transform * _isometricAutotile.CellPosition(_isometricAutotile.BoundsOrigin + cell);

            // A flat view's cell centre is its logical layer's, the geometry the grid binds.
            // A projection whose renderer is not wired has no position to give.
            if (FlatTerrainLayer() is not { } layer)
                return Vector2.Zero;
            return RendererTransform() * (layer.Transform * layer.MapToLocal(_generator.BoundsOrigin + cell));
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

            // A projection whose renderer is not wired draws nothing, so it has no extent.
            if (FlatTerrainLayer() is not { TileSet: { } tiles } layer)
                return new Rect2();
            Vector2 tile = tiles.TileSize;
            Vector2I origin = _generator?.BoundsOrigin ?? Vector2I.Zero;
            return RendererTransform() * (layer.Transform * new Rect2(
                new Vector2(origin.X * tile.X, origin.Y * tile.Y),
                new Vector2(Mathf.Max(1, size.X * tile.X), Mathf.Max(1, size.Y * tile.Y))));
        }

        /// <summary>
        /// The square logical layer of the active flat view - the surface's own statement of its
        /// cell size, which the gameplay grid binds. Null when the projection is not flat or its
        /// renderer is not wired. This replaced a private re-derivation that fell back to the
        /// painted renderer's size, or 64, for a view that was not the one on screen.
        /// </summary>
        private TileMapLayer? FlatTerrainLayer() => Projection switch
        {
            TerrainProjection.Painted => _painted?.GetTerrainLayer(),
            TerrainProjection.Tiles => _tiles?.GetTerrainLayer(),
            _ => null
        };

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

        public Vector2 StartPositionGlobal() => StartPositionGlobalAt(0);

        /// <summary>Start <paramref name="index"/> in global coordinates, for a camera. See StartPositionViewAt.</summary>
        public Vector2 StartPositionGlobalAt(int index)
        {
            Vector2 position = StartPositionViewAt(index);
            return ViewToGlobalTransform() * position;
        }

        public Rect2 WorldExtent()
        {
            Rect2 extent = PreviewExtent();
            return ViewToGlobalTransform() * extent;
        }
    }
}
