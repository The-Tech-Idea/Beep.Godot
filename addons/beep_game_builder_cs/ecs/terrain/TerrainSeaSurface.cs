using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// The sea a view draws, as one object: the coast field it reads, the material it paints
    /// with, and the geometry it covers.
    ///
    /// Two views had written this twice and a half. Both resolved the same coast field through
    /// their own caches, both adopted-or-created the same material, both set the same block of
    /// surface uniforms, and both then differed only in the SHAPE they put it on - a batched tile
    /// layer top-down, an overscanned polygon in the block view. The painted view has no surface
    /// at all; it mixes the sea into its own opaque ground pass, so it keeps only the shared
    /// dials and is not a caller here.
    ///
    /// What each view still owns is what is genuinely its own: which coast window it reads
    /// (range and detail), the transparent-sheet uniforms, and its geometry's parameters.
    /// <see cref="TerrainWaterLook"/> owns how the sea looks, for every view at once.
    /// </summary>
    internal sealed class TerrainSeaSurface
    {
        /// <summary>
        /// The uniforms <c>iso_water.gdshader</c> declares for a transparent sheet floating over
        /// seabed geometry. They describe a SURFACE, not a look, so they stay per view: the
        /// painted composite has no equivalent, and the block view's water is read through more
        /// of it than the flat view's.
        /// </summary>
        internal readonly record struct Sheet(
            float MaxOpacity, float ClarityTiles, float LakeOpacity, float ShoreOpacity);

        private readonly TerrainCoastField.RenderCache _renderCoast = new();
        private readonly TerrainCoastField.LiveCache _liveCoast = new();
        private ImageTexture? _coastMap;
        private TerrainCoastField.Pixels? _coastPixels;
        private ShaderMaterial? _material;

        /// <summary>The material the sea is painted with, or null before one is built.</summary>
        internal ShaderMaterial? Material => _material;

        /// <summary>Whether a coast field has been resolved; without one the sea has no shallows.</summary>
        internal bool HasCoast => _coastMap is not null;

        /// <summary>
        /// Reads the coastline this sea is measured from: from the live cells when a map has
        /// them, else from the generator's field. The live cache keeps its own revision, which is
        /// what lets the render cache skip re-uploading an unchanged field.
        /// </summary>
        internal void ResolveCoast(
            GridCellDataComponent? cells, TerrainGeneratorComponent? generator,
            Vector2I origin, Vector2I size, int detail, float rangeTiles)
        {
            if (cells is not null)
            {
                _coastMap = _liveCoast.Resolve(cells, origin, size, detail, rangeTiles);
                _coastPixels = _liveCoast.CoastField;
            }
            else if (generator is not null)
            {
                // Built as pixels and uploaded here, so a view that also reads depth on the CPU
                // reads the SAME field it draws rather than measuring a second coastline.
                TerrainCoastField.Pixels pixels = TerrainCoastField.BuildPixels(generator, size, detail, rangeTiles);
                _coastPixels = pixels;
                _coastMap = pixels.Upload();
            }
            else
            {
                _coastMap = null;
                _coastPixels = null;
            }
        }

        /// <summary>
        /// How far each cell is from the waterline, in tiles, positive out to sea - the field this
        /// sea is drawn from, decoded per cell. Empty when no coast has been resolved.
        /// </summary>
        internal float[] CellDistances(Vector2I size, float rangeTiles)
            => _coastPixels is null
                ? System.Array.Empty<float>()
                : TerrainCoastField.CellDistances(_coastPixels, size, rangeTiles);

        /// <summary>Drops the coast field, so the next build resolves it again.</summary>
        internal void ForgetCoast()
        {
            _coastMap = null;
            _coastPixels = null;
        }

        /// <summary>
        /// Builds or refreshes the material: the shader, the coast field, the sheet's own
        /// uniforms and the shared look, in that order.
        ///
        /// The material saved with the scene is ADOPTED rather than replaced, because replacing
        /// it wiped every uniform hand-tuned in the Inspector on each reload. Everything written
        /// here still wins; only uniforms nothing writes survive by it.
        /// </summary>
        internal ShaderMaterial? BuildMaterial(
            Node owner, string shaderPath, TerrainWaterLook look, in Sheet sheet,
            Vector2I origin, Vector2I size, Vector2I cellSize, float coastRange,
            bool flatProjection, bool tileBatch, ShaderMaterial? authored = null)
        {
            ShaderMaterial material = _material
                ?? authored?.Duplicate() as ShaderMaterial
                ?? new ShaderMaterial();
            if (material.Shader is null)
            {
                var shader = GD.Load<Shader>(shaderPath);
                if (shader is null)
                {
                    GD.PushWarning($"[{owner.Name}] could not load water shader '{shaderPath}'; there will be no sea.");
                    return null;
                }
                material.Shader = shader;
            }

            // The coast field is what tells the shader where the shore is, and how far from it
            // each pixel lies. Handing it a null texture loses the shallows SILENTLY, so say so.
            if (_coastMap is null)
                GD.PushWarning($"[{owner.Name}] the coast field is missing; the sea will draw without shallows.");
            else
                material.SetShaderParameter("coast_map", _renderCoast.Resolve(_coastMap, size, _liveCoast.CoastRevision));

            material.SetShaderParameter("cell_size", new Vector2(cellSize.X, cellSize.Y));
            material.SetShaderParameter("tile_offset", Vector2.Zero);
            material.SetShaderParameter("tile_batch", tileBatch);
            material.SetShaderParameter("flat_projection", flatProjection ? 1.0f : 0.0f);
            material.SetShaderParameter("max_opacity", sheet.MaxOpacity);
            material.SetShaderParameter("clarity_tiles", sheet.ClarityTiles);
            material.SetShaderParameter("lake_opacity", sheet.LakeOpacity);
            material.SetShaderParameter("shore_opacity", sheet.ShoreOpacity);

            // Everything water_common.gdshaderinc declares, through its one writer, from the one
            // look this world draws its sea with.
            TerrainWaterMaterial.Apply(material, look.Settings(size, origin, coastRange));
            TerrainWaterMaterial.ApplyTextures(
                material, owner.Name, look.ShallowTexturePath, look.DeepTexturePath,
                look.SeabedSandTexturePath, look.FoamSheetPath);

            _material = material;
            return material;
        }

        /// <summary>
        /// The flat and isometric-tile geometry: one blank tile per cell, batched into a single
        /// rendering quadrant, with the shader deciding what is water.
        ///
        /// EVERY cell is filled, not just the wet ones: the shader draws the shore fade and the
        /// foam slightly INLAND of the waterline, and filling only water cells would clip both at
        /// a cell boundary and put a straight edge along every beach.
        /// </summary>
        internal TileMapLayer TileBatched(
            Node2D owner, string layerName, Vector2I cellSize, Vector2I size, Vector2 position, bool isometric)
        {
            TileMapLayer layer = TerrainAuthoring.EnsureLayer(owner, layerName);
            Vector2I cell = new(Mathf.Max(2, cellSize.X), Mathf.Max(2, cellSize.Y));
            if (layer.TileSet is null || layer.TileSet.TileSize != cell
                || (layer.TileSet.TileShape == TileSet.TileShapeEnum.Isometric) != isometric)
                layer.TileSet = TerrainShaderSurface.BuildTileSet(cell, isometric);

            TerrainShaderSurface.Fill(layer, size);
            layer.Position = position;
            // The shared sea level: over the water tiles that are its bed, and under the land.
            layer.ZIndex = TerrainLayers.ZFor(TerrainLayers.Sea);
            layer.ZAsRelative = false;
            return layer;
        }

        /// <summary>
        /// The block view's geometry: one overscanned quad in the renderer's own isometric
        /// projection, so the surface's own edge is never in frame - beyond the map the shader
        /// draws plain open sea, and there is nothing to give the boundary away.
        /// </summary>
        internal static Vector2[] Polygon(Vector2I cellSize, Vector2I size, int margin)
        {
            Vector2 Project(float x, float y) => new(
                cellSize.X * 0.5f * (1 + x - y), cellSize.Y * 0.5f * (x + y));
            return new[]
            {
                Project(-margin, -margin), Project(size.X + margin, -margin),
                Project(size.X + margin, size.Y + margin), Project(-margin, size.Y + margin)
            };
        }
    }
}
