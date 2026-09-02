using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Builds the coast map: for every point on the map, how far it is from the
    /// waterline and whether the water there is open sea.
    ///
    /// This is what a water surface is actually drawn from. A shader given only
    /// "is this tile wet" has to invent its own coastline and gets a staircase;
    /// given a real distance it can put a beach two tiles wide and have it BE
    /// two tiles wide on screen, shelve the depth away from the shore, and run
    /// surf out from the waterline rather than around a tile.
    ///
    /// It lives here rather than inside one renderer because both the flat and
    /// the isometric views draw the same sea. Two copies of this would be two
    /// coastlines that disagree, and the disagreement shows up as the water in
    /// one view breaking somewhere the other says is dry land.
    ///
    /// Encoding, per texel:
    ///   R, B  signed distance to the waterline, in tiles, mapped to 0..1 with
    ///         0.5 at the waterline; positive out to sea.
    ///   G     1 where the water is open sea.
    /// </summary>
    public static class TerrainCoastField
    {
        /// <summary>
        /// One orthogonal step, in the units <see cref="Distance"/> returns.
        /// Distances come back scaled by this so the chamfer weights can be
        /// whole numbers.
        /// </summary>
        private const int ChamferStep = 3;

        /// <summary>
        /// Renders the field for a generated map.
        ///
        /// <paramref name="detail"/> is how many samples per tile edge: the
        /// generator knows where water is below tile resolution, and using that
        /// is what keeps the contours curved instead of following tile patches.
        /// <paramref name="rangeTiles"/> is the distance that saturates the
        /// encoding, so bands wider than it cannot be expressed.
        /// </summary>
        public static ImageTexture Build(
            TerrainGeneratorComponent generator, Vector2I size, int detail, float rangeTiles)
        {
            ArgumentNullException.ThrowIfNull(generator);

            detail = Mathf.Clamp(detail, 1, 8);
            var fine = new Vector2I(Mathf.Max(1, size.X) * detail, Mathf.Max(1, size.Y) * detail);
            int count = fine.X * fine.Y;

            var water = new bool[count];
            for (int y = 0; y < fine.Y; y++)
            {
                for (int x = 0; x < fine.X; x++)
                {
                    var at = new Vector2((x + 0.5f) / detail, (y + 0.5f) / detail);
                    water[(y * fine.X) + x] = generator.IsWaterAtPosition(at);
                }
            }

            int[] toWater = Distance(water, fine, seedOnWater: true);
            int[] toLand = Distance(water, fine, seedOnWater: false);

            // Which water is OPEN SEA. Surf belongs to a coast with a fetch
            // behind it; a lake or a river has no swell running onto it, and
            // drawing breakers around a pond reads as wrong immediately.
            bool[] ocean = OceanCells(generator, size);

            float range = Mathf.Max(1.0f, rangeTiles);
            var image = Image.CreateEmpty(fine.X, fine.Y, false, Image.Format.Rgba8);
            for (int y = 0; y < fine.Y; y++)
            {
                for (int x = 0; x < fine.X; x++)
                {
                    int index = (y * fine.X) + x;
                    // Distances come back in chamfer units of a sub-cell;
                    // convert to tiles.
                    float signed = (water[index] ? toLand[index] : -toWater[index])
                        / (float)(detail * ChamferStep);
                    float encoded = Mathf.Clamp((signed / range * 0.5f) + 0.5f, 0.0f, 1.0f);
                    float open = ocean[((y / detail) * Mathf.Max(1, size.X)) + (x / detail)] ? 1.0f : 0.0f;
                    image.SetPixel(x, y, new Color(encoded, open, encoded, 1.0f));
                }
            }
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>
        /// Cells that are open sea, grown by one cell.
        ///
        /// The growth matters: the coast map is sampled below the tile grid and
        /// filtered linearly, so without it the land-side fringe of a coastal
        /// tile reads as "not sea" and the surf is cut off exactly where it
        /// should be strongest.
        /// </summary>
        private static bool[] OceanCells(TerrainGeneratorComponent generator, Vector2I size)
        {
            GeneratedTerrainField field = generator.ResolveField();
            int width = Mathf.Max(1, size.X);
            int height = Mathf.Max(1, size.Y);
            int count = width * height;

            var seed = new bool[count];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    seed[(y * width) + x] = field.WaterSourceAtCell(new Vector2I(x, y)) == "ocean";
            }

            var grown = new bool[count];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !near; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                                continue;
                            near = seed[(ny * width) + nx];
                        }
                    }
                    grown[(y * width) + x] = near;
                }
            }
            return grown;
        }

        /// <summary>
        /// Chamfer distance to the nearest seed, scaled by <see cref="ChamferStep"/>.
        ///
        /// A four-neighbour flood measures MANHATTAN distance, whose contours are
        /// diamonds. That is invisible in a band a fraction of a tile wide, but
        /// the shallow-water shelf is a couple of tiles deep and drew those
        /// diamonds as angular polygons out in the sea. Weighting diagonal steps
        /// at 4 against 3 puts the contours within a few percent of circular,
        /// for two sweeps and no queue.
        /// </summary>
        private static int[] Distance(bool[] water, Vector2I size, bool seedOnWater)
        {
            const int diagonal = 4;
            // Headroom so a saturated cell plus one step cannot overflow.
            int far = int.MaxValue - (diagonal * 2);

            var distance = new int[water.Length];
            for (int index = 0; index < water.Length; index++)
                distance[index] = water[index] == seedOnWater ? 0 : far;

            // Forward sweep reads the half of the neighbourhood already settled.
            for (int y = 0; y < size.Y; y++)
            {
                for (int x = 0; x < size.X; x++)
                {
                    int index = (y * size.X) + x;
                    int best = distance[index];
                    if (x > 0)
                        best = Math.Min(best, distance[index - 1] + ChamferStep);
                    if (y > 0)
                    {
                        best = Math.Min(best, distance[index - size.X] + ChamferStep);
                        if (x > 0)
                            best = Math.Min(best, distance[index - size.X - 1] + diagonal);
                        if (x < size.X - 1)
                            best = Math.Min(best, distance[index - size.X + 1] + diagonal);
                    }
                    distance[index] = best;
                }
            }

            // Backward sweep covers the other half.
            for (int y = size.Y - 1; y >= 0; y--)
            {
                for (int x = size.X - 1; x >= 0; x--)
                {
                    int index = (y * size.X) + x;
                    int best = distance[index];
                    if (x < size.X - 1)
                        best = Math.Min(best, distance[index + 1] + ChamferStep);
                    if (y < size.Y - 1)
                    {
                        best = Math.Min(best, distance[index + size.X] + ChamferStep);
                        if (x > 0)
                            best = Math.Min(best, distance[index + size.X - 1] + diagonal);
                        if (x < size.X - 1)
                            best = Math.Min(best, distance[index + size.X + 1] + diagonal);
                    }
                    distance[index] = best;
                }
            }
            return distance;
        }
    }
}
