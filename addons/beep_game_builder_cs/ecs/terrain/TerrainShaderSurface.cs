using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// A TileMapLayer that exists to be painted by a shader.
    ///
    /// The sea and the painted ground are continuous fields - depth, foam, the
    /// blend between two materials - computed per pixel from a coast distance
    /// map. They used to be drawn on a Sprite2D stretched over the map, which
    /// worked and left them outside the tile system: not saved with the scene,
    /// carrying no tile data, and invisible to collision and navigation.
    ///
    /// They are tiles now. The shader does not care what it draws on as long as
    /// it can find out where a fragment is, and both shaders take that from
    /// VERTEX (documented as local space in PIXELS) via a varying, which a tile
    /// layer supplies exactly as a quad did.
    ///
    /// The tile itself is blank on purpose. It contributes no colour; it is
    /// there so the layer has something to rasterise, and every pixel of it is
    /// replaced by the shader.
    /// </summary>
    public static class TerrainShaderSurface
    {
        /// <summary>
        /// Builds the one-tile TileSet a shader surface draws on.
        ///
        /// The blank tile is a DIAMOND for isometric layers and a rectangle for
        /// square ones, and that distinction is not cosmetic. Isometric cells
        /// overlap: a full rectangle per cell would cover its neighbours' halves,
        /// and since the sea is transparent the overlaps would blend twice and
        /// draw a lattice of brighter diamonds across the whole ocean. A shape
        /// that tessellates exactly is what makes one continuous surface.
        /// </summary>
        public static TileSet BuildTileSet(Vector2I cellSize, bool isometric)
        {
            Vector2I size = new(Mathf.Max(2, cellSize.X), Mathf.Max(2, cellSize.Y));
            var image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);

            if (isometric)
            {
                image.Fill(Colors.Transparent);
                var half = new Vector2(size.X * 0.5f, size.Y * 0.5f);
                for (int y = 0; y < size.Y; y++)
                {
                    for (int x = 0; x < size.X; x++)
                    {
                        // Inside the diamond |dx|/w + |dy|/h <= 1/2, measured
                        // from the centre. Pixel centres, so opposite edges meet
                        // rather than overlapping by one row.
                        float dx = Mathf.Abs((x + 0.5f) - half.X) / half.X;
                        float dy = Mathf.Abs((y + 0.5f) - half.Y) / half.Y;
                        if (dx + dy <= 1.0f)
                            image.SetPixel(x, y, Colors.White);
                    }
                }
            }
            else
            {
                image.Fill(Colors.White);
            }

            var source = new TileSetAtlasSource
            {
                Texture = ImageTexture.CreateFromImage(image),
                TextureRegionSize = size,
            };
            source.CreateTile(Vector2I.Zero);

            // Through the one builder, so a shader surface is shaped the same way
            // every other terrain TileSet is.
            TileSet tileSet = TerrainTileSets.Create(size, isometric);
            tileSet.AddSource(source, 0);
            return tileSet;
        }

        /// <summary>
        /// Fills a size x size block of cells from the ORIGIN, so the surface the
        /// shader paints is continuous. A gap would not read as a missing tile -
        /// it would read as a hole in the sea.
        ///
        /// ONE RENDERING QUADRANT, and that is the whole reason this method sets
        /// it. Godot batches tiles into quadrants of rendering_quadrant_size
        /// (16 by default) and draws each as its own canvas item with its own
        /// origin, so VERTEX - which the shaders read to find out where they are
        /// - restarts at every quadrant boundary. The painted map came back as
        /// the same 16-tile patch repeated in a 4x4 grid across a 64-tile map,
        /// which is exactly 64/16: perfectly regular, and nothing reported an
        /// error because every quadrant drew precisely what it was asked to.
        ///
        /// Filling from the origin rather than an arbitrary rectangle is part of
        /// the same requirement: a cell at -1 falls in quadrant -1 however large
        /// the quadrants are, and that is a second quadrant. A caller wanting
        /// margin around the map moves the LAYER instead and tells the shader
        /// how many tiles it shifted.
        /// </summary>
        public static void Fill(TileMapLayer layer, Vector2I size)
        {
            Vector2I extent = new(Mathf.Max(1, size.X), Mathf.Max(1, size.Y));

            layer.Clear();
            layer.RenderingQuadrantSize = Mathf.Max(extent.X, extent.Y) + 1;

            for (int y = 0; y < extent.Y; y++)
            {
                for (int x = 0; x < extent.X; x++)
                    layer.SetCell(new Vector2I(x, y), 0, Vector2I.Zero);
            }
        }
    }
}
