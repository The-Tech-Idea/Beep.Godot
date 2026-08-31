using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Maintains a display TileMapLayer for one logical terrain kind. By
    /// default it delegates connection selection to Godot TileSet terrains;
    /// the legacy manual dual-grid mapping is available only for authored
    /// atlases with a verified mask-to-tile layout.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridTerrainTransitionLayerComponent : Node
    {
        [Export] public NodePath CellDataPath { get; set; } = new("");
        [Export] public NodePath DisplayLayerPath { get; set; } = new("");
        [Export] public NodePath DetailDisplayLayerPath { get; set; } = new("");

        [ExportGroup("Map")]
        [Export] public Vector2I BoundsOrigin { get; set; } = Vector2I.Zero;
        [Export] public Vector2I BoundsSize { get; set; } = new(64, 64);
        [Export] public string TransitionTerrainKind { get; set; } = "water";
		[Export] public bool RenderFilledBase { get; set; } = false;

        [ExportGroup("Godot Terrain Set")]
        [Export] public bool UseTileSetTerrains { get; set; } = true;
        [Export] public int TerrainSet { get; set; } = 0;
        [Export] public int Terrain { get; set; } = 0;
        [Export] public bool IgnoreEmptyTerrains { get; set; } = true;

        [ExportGroup("Atlas")]
        [Export] public int SourceId { get; set; } = 0;
        [Export] public int DetailSourceId { get; set; } = 1;
        [Export] public Vector2I AtlasOrigin { get; set; } = Vector2I.Zero;
        [Export(PropertyHint.Range, "1,16,1")] public int AtlasColumns { get; set; } = 4;
		[Export] public bool UseCanonical15PieceLayout { get; set; } = true;
        [Export] public int AlternativeTile { get; set; } = 0;
        [Export(PropertyHint.File, "*.png,*.webp")] public string AtlasTexturePath { get; set; } = "";
        [Export] public bool BuildTileSetFromAtlasPath { get; set; } = false;
        [Export] public Vector2I AtlasTileSize { get; set; } = new(64, 64);
        [Export(PropertyHint.Range, "1,16,1")] public int AtlasTileRows { get; set; } = 4;
        [Export(PropertyHint.File, "*.png,*.webp")] public string DetailAtlasTexturePath { get; set; } = "";
        [Export] public bool BuildDetailTileSetFromAtlasPath { get; set; } = false;

        [ExportGroup("Refresh")]
        [Export] public bool RefreshOnReady { get; set; } = true;
        [Export] public bool RefreshInEditor { get; set; } = false;

        private GridCellDataComponent? _cells;
        private TileMapLayer? _displayLayer;
        private TileMapLayer? _detailDisplayLayer;
        private GridCellDataComponent? _connectedCells;
        private bool _refreshQueued;

        public Vector2I EffectiveBoundsSize => new(Mathf.Max(1, BoundsSize.X), Mathf.Max(1, BoundsSize.Y));

        public override void _Ready()
        {
            ResolveReferences();
            ConnectSignals();
            UpdateConfigurationWarnings();

            if (RefreshOnReady && (!Engine.IsEditorHint() || RefreshInEditor))
                RequestRefresh();
        }

        public override void _ExitTree()
        {
            if (_connectedCells != null && GodotObject.IsInstanceValid(_connectedCells))
            {
                _connectedCells.CellChanged -= OnCellChanged;
                _connectedCells.CellsChanged -= OnCellsChanged;
            }
            _connectedCells = null;
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (CellDataPath.IsEmpty)
                return new[] { "CellDataPath should point to GridCellDataComponent." };
            if (DisplayLayerPath.IsEmpty)
                return new[] { "DisplayLayerPath should point to a TileMapLayer with a configured TileSet." };
            if (string.IsNullOrWhiteSpace(TransitionTerrainKind))
                return new[] { "TransitionTerrainKind must name the terrain represented by this layer." };
            if (UseTileSetTerrains && (TerrainSet < 0 || Terrain < 0))
                return new[] { "TerrainSet and Terrain must identify an authored Godot TileSet terrain." };
            return Array.Empty<string>();
        }

        public void RequestRefresh()
        {
            if (_refreshQueued)
                return;

            _refreshQueued = true;
            CallDeferred(nameof(RefreshTransitions));
        }

        /// <summary>
        /// Rewrites only the bounded display cells. When the TileSet has
        /// authored terrain peering bits, Godot selects the correct edge and
        /// corner variants in one batched call. This is the supported Godot 4
        /// terrain API and avoids guessing an atlas's numeric tile layout.
        /// </summary>
        public void RefreshTransitions()
        {
            _refreshQueued = false;
            ResolveReferences();
            if (_cells == null || _displayLayer == null)
                return;

            PlaceDisplayLayer();
            if (!UseTileSetTerrains)
            {
                EnsureDisplayTileSet(_displayLayer, AtlasTexturePath, BuildTileSetFromAtlasPath, SourceId);
                EnsureDisplayTileSet(_detailDisplayLayer, DetailAtlasTexturePath, BuildDetailTileSetFromAtlasPath, DetailSourceId);
            }
            if (_displayLayer.TileSet is null)
            {
                GD.PushWarning($"[{Name}] GridTerrainTransitionLayerComponent requires an authored TileSet terrain.");
                return;
            }

            if (UseTileSetTerrains)
            {
                RefreshUsingTileSetTerrains();
                return;
            }

            Vector2I size = EffectiveBoundsSize;
            for (int y = 0; y <= size.Y; y++)
            {
                for (int x = 0; x <= size.X; x++)
                {
                    Vector2I displayCell = BoundsOrigin + new Vector2I(x, y);
                    int mask = RenderFilledBase ? 15 : DualGridMaskAt(displayCell);
                    _displayLayer.EraseCell(displayCell);
                    _detailDisplayLayer?.EraseCell(displayCell);
                    if (mask == 0)
                    {
                        continue;
                    }

                    Vector2I atlas = AtlasOrigin + AtlasCoordinatesForMask(mask);
                    _displayLayer.SetCell(displayCell, SourceId, atlas, AlternativeTile);
                    if (_detailDisplayLayer?.TileSet is not null)
                        _detailDisplayLayer.SetCell(displayCell, DetailSourceId, atlas, AlternativeTile);
                }
            }
        }

        private void RefreshUsingTileSetTerrains()
        {
            if (_displayLayer is null)
                return;

            Vector2I size = EffectiveBoundsSize;
            var cells = new Godot.Collections.Array<Vector2I>();
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    Vector2I cell = BoundsOrigin + new Vector2I(x, y);
                    _displayLayer.EraseCell(cell);
                    if (IsTransitionTerrain(cell))
                        cells.Add(cell);
                }
            }

            if (cells.Count == 0)
                return;

            _displayLayer.SetCellsTerrainConnect(cells, TerrainSet, Terrain, IgnoreEmptyTerrains);
        }

        /// <summary>
        /// Four terrain cells meet at one display cell. The bit order is
        /// top-left, top-right, bottom-left, bottom-right.
        /// </summary>
        public int DualGridMaskAt(Vector2I displayCell)
        {
            int mask = 0;
            if (IsTransitionTerrain(displayCell + new Vector2I(-1, -1))) mask |= 1;
            if (IsTransitionTerrain(displayCell + new Vector2I(0, -1))) mask |= 2;
            if (IsTransitionTerrain(displayCell + new Vector2I(-1, 0))) mask |= 4;
            if (IsTransitionTerrain(displayCell)) mask |= 8;
            return mask;
        }

		/// <summary>
		/// The supplied 4x4 terrain sheets share this verified layout. Cell 12
		/// is empty and cell 6 is solid; the rest encode the four corner bits.
		/// Keep this separate from an arbitrary row-major atlas order.
		/// </summary>
		public Vector2I AtlasCoordinatesForMask(int mask)
		{
			int clamped = Mathf.Clamp(mask, 0, 15);
			int index = UseCanonical15PieceLayout ? CanonicalMaskToAtlasIndex[clamped] : clamped;
			int columns = Mathf.Max(1, AtlasColumns);
			return new Vector2I(index % columns, index / columns);
		}

		private static readonly int[] CanonicalMaskToAtlasIndex =
		{
			12, 15, 8, 9,
			0, 11, 14, 7,
			13, 4, 1, 10,
			3, 2, 5, 6,
		};

        private bool IsTransitionTerrain(Vector2I cell)
        {
            if (_cells == null)
                return false;

            string terrain = Normalize(_cells.GetTerrainKind(cell));
            string transition = Normalize(TransitionTerrainKind);
            return terrain == transition
                || (transition == "water" && terrain is "deep_water" or "shallow_water")
                || (transition == "grass" && terrain is "grassland" or "dry_grass")
                || (transition == "desert" && terrain == "sand")
                || (transition == "mud" && terrain is "swamp" or "earth" or "dirt" or "soil");
        }

        private void ResolveReferences()
        {
            if (_cells == null || !GodotObject.IsInstanceValid(_cells))
                _cells = !CellDataPath.IsEmpty
                    ? GetNodeOrNull<GridCellDataComponent>(CellDataPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridCellDataComponent>(GetTree()?.CurrentScene) : null;

            if (_displayLayer == null || !GodotObject.IsInstanceValid(_displayLayer))
                _displayLayer = !DisplayLayerPath.IsEmpty ? GetNodeOrNull<TileMapLayer>(DisplayLayerPath) : null;

            if (_detailDisplayLayer == null || !GodotObject.IsInstanceValid(_detailDisplayLayer))
                _detailDisplayLayer = !DetailDisplayLayerPath.IsEmpty ? GetNodeOrNull<TileMapLayer>(DetailDisplayLayerPath) : null;
        }

        /// <summary>
        /// Puts this layer where the shared stack says its terrain belongs.
        ///
        /// A layer knows which terrain kind it paints, and TerrainLayers knows
        /// which level that kind is, so there is nothing left for a scene to
        /// decide. The 15-piece demo scene carried six hand-written z values and
        /// a comment calling that "visual ownership in design-time nodes" - which
        /// is exactly how it kept water stacked ABOVE grass and desert long after
        /// the same fault was found and fixed in the tile renderer. A number
        /// typed into scene data cannot be corrected by fixing the code, and
        /// nothing reports it, because scene data is not wrong on its own.
        ///
        /// This runs on every refresh rather than only when the atlas is built,
        /// so a layer handed an authored TileSet is placed too.
        ///
        /// The filled base is the floor of the world rather than a terrain of
        /// its own: it exists so a gap between layers shows water, not a hole.
        /// </summary>
        private void PlaceDisplayLayer()
        {
            if (_displayLayer is null)
                return;

            int z = RenderFilledBase
                ? TerrainLayers.ZForFloor()
                : TerrainLayers.ZForKind(Normalize(TransitionTerrainKind));

            _displayLayer.ZIndex = z;
            _displayLayer.ZAsRelative = false;

            // The detail pass belongs to the same level, drawn over it - which
            // is what the level's own even slot is kept free for.
            if (_detailDisplayLayer is not null && _detailDisplayLayer != _displayLayer)
            {
                _detailDisplayLayer.ZIndex = z + 1;
                _detailDisplayLayer.ZAsRelative = false;
            }
        }

        private void EnsureDisplayTileSet(TileMapLayer? layer, string atlasPath, bool buildFromPath, int sourceId)
        {
            if (!buildFromPath || layer is null || string.IsNullOrWhiteSpace(atlasPath))
                return;

			if (layer.TileSet?.HasSource(sourceId) == true)
				return;

            // Through the shared loader, which imports where it can and builds
            // a mip chain where it cannot. Reading the PNG straight off disk
            // here bypassed the import pipeline entirely, so the mipmap setting
            // beside every atlas never applied and this view alone drew its
            // tiles unfiltered.
            Texture2D? texture = TerrainTextures.Load(
                atlasPath, Name, "15-piece terrain atlas");
            if (texture is null)
                return;

            Vector2I tileSize = new(Mathf.Max(1, AtlasTileSize.X), Mathf.Max(1, AtlasTileSize.Y));
            var source = new TileSetAtlasSource
            {
                Texture = texture,
                TextureRegionSize = tileSize,
            };

            int columns = Mathf.Max(1, AtlasColumns);
            int rows = Mathf.Max(1, AtlasTileRows);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                    source.CreateTile(AtlasOrigin + new Vector2I(x, y));
            }

            var tileSet = layer.TileSet ?? new TileSet { TileSize = tileSize };
            tileSet.AddSource(source, sourceId);
            layer.TileSet = tileSet;

            // Having built the atlas WITH a mip chain, say how it is sampled.
            //
            // Otherwise the two halves of one decision sit in different places:
            // this builds mipmaps, and a scene that authored the TileMapLayer
            // itself decides whether anything ever uses them. The 15-piece demo
            // scene had every layer on Nearest, so the chain built here was
            // discarded at draw time and the tiles aliased exactly as they did
            // before any of this was fixed - with nothing anywhere reporting a
            // problem, because neither half is wrong on its own.
            //
            // Nearest is not an option worth preserving here: it cannot sample a
            // mip level at all, so it is the one setting guaranteed to waste
            // what this method just produced.
            layer.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
        }

        private void ConnectSignals()
        {
            if (_connectedCells == _cells)
                return;

            if (_connectedCells != null && GodotObject.IsInstanceValid(_connectedCells))
            {
                _connectedCells.CellChanged -= OnCellChanged;
                _connectedCells.CellsChanged -= OnCellsChanged;
            }

            if (_cells != null)
            {
                _cells.CellChanged += OnCellChanged;
                _cells.CellsChanged += OnCellsChanged;
            }
            _connectedCells = _cells;
        }

        private void OnCellChanged(int x, int y) => RequestRefresh();
        private void OnCellsChanged() => RequestRefresh();

        private static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "grass" : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }
}
