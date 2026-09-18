using Godot;
using System;
using System.Runtime.InteropServices;
using System.Threading;

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
    ///   R     signed distance to the waterline, in tiles, mapped to 0..1 with
    ///         0.5 at the waterline; positive out to sea.
    ///   G     1 where the water is open sea.
    ///   B     signed ocean distance with the same encoding (lake shores excluded).
    /// </summary>
    public static class TerrainCoastField
    {
        public const int DefaultDetail = 12;
        public const float DefaultRangeTiles = 5f;

        internal sealed record Pixels(Vector2I Size, byte[] Data, Image.Format Format)
        {
            // Only publication calls this; CPU workers return managed pixel data.
            internal ImageTexture Upload()
            {
                using var image = Image.CreateFromData(Size.X, Size.Y, false, Format, Data);
                return ImageTexture.CreateFromImage(image);
            }
        }

        /// <summary>
        /// Per-view reconstruction of a coast field for linear GPU sampling: contour smoothing
        /// and a 2x cubic upscale. The smoothed field, its padded copy and the reconstruction are
        /// kept between updates, so a windowed field change re-smooths and re-upscales only the
        /// blocks that can see it. The whole-field pass they replace was 447 ms of an 82k-cell
        /// shoreline edit once the distance field itself had become a window.
        /// </summary>
        internal sealed class RenderCache
        {
            private const int Block = 64;
            private ImageTexture? _source, _texture;
            private Vector2I _cells, _fine;
            private ulong _revision;
            private (ImageTexture Source, Vector2I Cells, Pixels Pixels)? _prepared;
            private byte[]? _smoothed;
            private Image? _padded, _reconstructed;

            internal void Prepare(ImageTexture source, Vector2I cells, Pixels pixels)
                => _prepared = (source, cells, pixels);

            /// <summary>
            /// <paramref name="revision"/> is the field's content revision, since an updated field
            /// arrives as a new texture with mostly the same content. <paramref name="field"/> is the
            /// raw field the texture was made from, which saves a GPU readback; <paramref name="dirty"/>
            /// is the fine-sample window the last update touched, or null when the whole field changed.
            /// </summary>
            public ImageTexture Resolve(ImageTexture source, Vector2I cells, ulong revision, Pixels? field = null, Rect2I? dirty = null)
            {
                var prepared = _prepared;
                _prepared = null;
                if (_source == source && _cells == cells && _revision == revision && GodotObject.IsInstanceValid(_texture))
                    return _texture!;

                int width = source.GetWidth(), height = source.GetHeight();
                var fine = new Vector2I(width, height);
                bool windowable = dirty is not null && field is { Format: Image.Format.Rgbaf } raw && raw.Size == fine
                    && _smoothed is not null && _smoothed.Length == raw.Data.Length && _padded is not null
                    && _reconstructed is not null && _fine == fine && _cells == cells && GodotObject.IsInstanceValid(_texture);
                _source = source;
                _cells = cells;
                _revision = revision;
                if (windowable)
                {
                    UpdateWindow(field!, dirty!.Value);
                    return _texture!;
                }

                _texture = source;
                _fine = fine;
                _smoothed = null;
                _padded = null;
                _reconstructed = null;
                // Coarse masks preserve cell-centre geometry. High-detail masks
                // still need contour smoothing when a 2x texture exceeds budget.
                if (width < Math.Max(1, cells.X) * 2 || height < Math.Max(1, cells.Y) * 2)
                    return source;

                bool hasPrepared = prepared is { } value && value.Source == source && value.Cells == cells
                    && value.Pixels.Size == fine;
                // Work on pixels of our own. A headless texture hands back its own stored image
                // from GetImage, so smoothing in place there would rewrite the source.
                Pixels working;
                if (hasPrepared) working = prepared!.Value.Pixels;
                else if (field is { } given && given.Size == fine) working = given;
                else
                {
                    using var downloaded = source.GetImage();
                    working = new Pixels(fine, downloaded.GetData(), downloaded.GetFormat());
                }
                bool canUpscale = width <= 2048 && height <= 2048
                    && (long)width * height * 4 <= 4_194_304;
                if (!canUpscale && (working.Format != Image.Format.Rgbaf
                    || (long)width * height > 1_250_000))
                    return source;
                if (working.Format == Image.Format.Rgbaf)
                {
                    if (!hasPrepared) working = PrepareContours(working, cells);
                    _smoothed = working.Data;
                }
                using var image = Image.CreateFromData(width, height, false, working.Format, working.Data);
                if (!canUpscale)
                {
                    _texture = ImageTexture.CreateFromImage(image);
                    return _texture;
                }
                var padded = Image.CreateEmpty(width + 4, height + 4, false, image.GetFormat());
                padded.BlitRect(image, new Rect2I(0, 0, width, height), new Vector2I(2, 2));
                ReplicateEdges(padded, width, height);
                if (image.GetFormat() == Image.Format.Rgba8)
                {
                    using (padded)
                    {
                        byte[] pixels = working.Data;
                        using var reconstructed = Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels);
                        reconstructed.Resize(width * 2, height * 2, Image.Interpolation.Nearest);
                        // Saturated inland/offshore blocks are constant, so nearest is
                        // already exact. Reconstruct varying blocks with a shared halo.
                        for (int y = 0; y < height; y += Block)
                        for (int x = 0; x < width; x += Block)
                        {
                            int w = Math.Min(Block, width - x), h = Math.Min(Block, height - y);
                            if (IsConstant(pixels, width, HaloOf(x, y, w, h, width, height), 4)) continue;
                            using var region = padded.GetRegion(new Rect2I(x, y, w + 4, h + 4));
                            region.Resize((w + 4) * 2, (h + 4) * 2, Image.Interpolation.Cubic);
                            reconstructed.BlitRect(region, new Rect2I(4, 4, w * 2, h * 2), new Vector2I(x * 2, y * 2));
                        }
                        _texture = ImageTexture.CreateFromImage(reconstructed);
                    }
                    return _texture;
                }
                if (working.Format != Image.Format.Rgbaf || _smoothed is null)
                {
                    // Other float layouts (a single-channel field, for one): the whole-image cubic
                    // resize, not retained; windows apply to the four-channel coast field only.
                    using (padded)
                    {
                        padded.Resize((width + 4) * 2, (height + 4) * 2, Image.Interpolation.Cubic);
                        using var reconstructed = padded.GetRegion(new Rect2I(4, 4, width * 2, height * 2));
                        _texture = ImageTexture.CreateFromImage(reconstructed);
                    }
                    return _texture;
                }
                // The coast field: block by block, and retained, so a later window rebuilds only its blocks.
                var full = Image.CreateEmpty(width * 2, height * 2, false, Image.Format.Rgbaf);
                for (int y = 0; y < height; y += Block)
                for (int x = 0; x < width; x += Block)
                    ReconstructBlock(padded, full, _smoothed, width, height, x, y);
                _padded = padded;
                _reconstructed = full;
                _texture = ImageTexture.CreateFromImage(full);
                return _texture;
            }

            private void UpdateWindow(Pixels field, Rect2I dirty)
            {
                int width = _fine.X, height = _fine.Y;
                var bounds = new Rect2I(Vector2I.Zero, _fine);
                (int stepX, int stepY) = Steps(_fine, _cells);
                // Smoothing reads two taps each side, so the window that must be re-smoothed is the
                // dirty window grown by that reach.
                Rect2I smooth = dirty.Grow(2 * Math.Max(stepX, stepY)).Intersection(bounds);
                if (smooth.Size.X <= 0 || smooth.Size.Y <= 0) return;
                SmoothWindow(field.Data, _smoothed!, _fine, stepX, stepY, smooth);
                using (var window = Image.CreateFromData(smooth.Size.X, smooth.Size.Y, false, Image.Format.Rgbaf, Region(_smoothed!, width, smooth)))
                    _padded!.BlitRect(window, new Rect2I(Vector2I.Zero, smooth.Size), smooth.Position + new Vector2I(2, 2));
                ReplicateEdges(_padded!, width, height);
                // Every block whose halo overlaps the re-smoothed window.
                Rect2I affected = smooth.Grow(2).Intersection(bounds);
                for (int y = affected.Position.Y / Block * Block; y < affected.End.Y; y += Block)
                for (int x = affected.Position.X / Block * Block; x < affected.End.X; x += Block)
                    ReconstructBlock(_padded!, _reconstructed!, _smoothed!, width, height, x, y);
                _texture = ImageTexture.CreateFromImage(_reconstructed!);
            }

            private static void ReconstructBlock(Image padded, Image reconstructed, byte[] smoothed, int width, int height, int x, int y)
            {
                int w = Math.Min(Block, width - x), h = Math.Min(Block, height - y);
                if (IsConstant(smoothed, width, HaloOf(x, y, w, h, width, height), 16))
                {
                    // Constant through the halo: the cubic kernel would reproduce the constant.
                    reconstructed.FillRect(new Rect2I(x * 2, y * 2, w * 2, h * 2), padded.GetPixel(x + 2, y + 2));
                    return;
                }
                using var region = padded.GetRegion(new Rect2I(x, y, w + 4, h + 4));
                region.Resize((w + 4) * 2, (h + 4) * 2, Image.Interpolation.Cubic);
                reconstructed.BlitRect(region, new Rect2I(4, 4, w * 2, h * 2), new Vector2I(x * 2, y * 2));
            }

            private static Rect2I HaloOf(int x, int y, int w, int h, int width, int height)
            {
                var halo = new Rect2I(Math.Max(0, x - 2), Math.Max(0, y - 2), 0, 0);
                halo.End = new Vector2I(Math.Min(width, x + w + 2), Math.Min(height, y + h + 2));
                return halo;
            }

            // Padding keeps the cubic kernel away from negative source indices and gives all four
            // edges the same clamp semantics as the sampler. Reads the padded image's own first and
            // last field columns/rows, so it can be redone after a window blit.
            private static void ReplicateEdges(Image padded, int width, int height)
            {
                for (int edge = 0; edge < 2; edge++)
                {
                    padded.BlitRect(padded, new Rect2I(2, 2, 1, height), new Vector2I(edge, 2));
                    padded.BlitRect(padded, new Rect2I(width + 1, 2, 1, height), new Vector2I(width + 2 + edge, 2));
                }
                for (int edge = 0; edge < 2; edge++)
                {
                    padded.BlitRect(padded, new Rect2I(0, 2, width + 4, 1), new Vector2I(0, edge));
                    padded.BlitRect(padded, new Rect2I(0, height + 1, width + 4, 1), new Vector2I(0, height + 2 + edge));
                }
            }

            private static bool IsConstant(byte[] pixels, int width, Rect2I region, int bytesPerPixel)
            {
                ReadOnlySpan<byte> expected = pixels.AsSpan((region.Position.Y * width + region.Position.X) * bytesPerPixel, bytesPerPixel);
                for (int y = region.Position.Y; y < region.End.Y; y++)
                for (int x = region.Position.X; x < region.End.X; x++)
                    if (!pixels.AsSpan((y * width + x) * bytesPerPixel, bytesPerPixel).SequenceEqual(expected)) return false;
                return true;
            }

            private static byte[] Region(byte[] field, int width, Rect2I region)
            {
                const int bytesPerPixel = 16;
                var result = new byte[region.Size.X * region.Size.Y * bytesPerPixel];
                for (int y = 0; y < region.Size.Y; y++)
                    Buffer.BlockCopy(field, ((region.Position.Y + y) * width + region.Position.X) * bytesPerPixel,
                        result, y * region.Size.X * bytesPerPixel, region.Size.X * bytesPerPixel);
                return result;
            }

            // Sigma is one third of a cell, independent of source resolution.
            private static (int X, int Y) Steps(Vector2I fine, Vector2I cells)
                => (Math.Max(1, Mathf.RoundToInt(fine.X / (float)Math.Max(1, cells.X) / 3f)),
                    Math.Max(1, Mathf.RoundToInt(fine.Y / (float)Math.Max(1, cells.Y) / 3f)));

            internal static Pixels PrepareContours(Pixels pixels, Vector2I cells, CancellationToken token = default)
            {
                token.ThrowIfCancellationRequested();
                int width = pixels.Size.X, height = pixels.Size.Y;
                bool canUpscale = width <= 2048 && height <= 2048 && (long)width * height * 4 <= 4_194_304;
                if (pixels.Format != Image.Format.Rgbaf
                    || width < Math.Max(1, cells.X) * 2 || height < Math.Max(1, cells.Y) * 2
                    || (!canUpscale && (long)width * height > 1_250_000)) return pixels;
                var result = new byte[pixels.Data.Length];
                (int stepX, int stepY) = Steps(pixels.Size, cells);
                SmoothWindow(pixels.Data, result, pixels.Size, stepX, stepY, new Rect2I(Vector2I.Zero, pixels.Size), token);
                return new Pixels(pixels.Size, result, pixels.Format);
            }

            /// <summary>
            /// Smooth the distance contours, not the terrain textures: a separable five-tap kernel on
            /// the two distance channels, over <paramref name="window"/> of the field, reading the raw
            /// field and writing the smoothed one. Explicit whole-cell painting (alpha below 0.5) is
            /// geometry, not generated sub-cell coastline, and is preserved, including isolated cells.
            /// </summary>
            private static void SmoothWindow(byte[] rawField, byte[] smoothedField, Vector2I fine, int stepX, int stepY,
                Rect2I window, CancellationToken token = default)
            {
                int width = fine.X, height = fine.Y;
                ReadOnlySpan<float> source = MemoryMarshal.Cast<byte, float>(rawField);
                Span<float> result = MemoryMarshal.Cast<byte, float>(smoothedField.AsSpan());
                ReadOnlySpan<float> weights = [1f / 16, 4f / 16, 6f / 16, 4f / 16, 1f / 16];
                // The vertical pass reads two taps above and below the window, so the horizontal
                // pass covers those rows as well.
                int rowStart = Math.Max(0, window.Position.Y - 2 * stepY), rowEnd = Math.Min(height, window.End.Y + 2 * stepY);
                int rows = rowEnd - rowStart, columns = window.Size.X, left = window.Position.X;
                var horizontal = new float[rows * columns * 4];
                for (int y = rowStart; y < rowEnd; y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int x = left; x < window.End.X; x++)
                    for (int channel = 0; channel < 4; channel++)
                    {
                        int target = ((y - rowStart) * columns + (x - left)) * 4 + channel;
                        if (channel is 1 or 3) { horizontal[target] = source[(y * width + x) * 4 + channel]; continue; }
                        for (int tap = -2; tap <= 2; tap++)
                            horizontal[target] += source[(y * width + Math.Clamp(x + tap * stepX, 0, width - 1)) * 4 + channel] * weights[tap + 2];
                    }
                }
                for (int y = window.Position.Y; y < window.End.Y; y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int x = left; x < window.End.X; x++)
                    for (int channel = 0; channel < 4; channel++)
                    {
                        int target = (y * width + x) * 4 + channel;
                        int column = x - left;
                        if (channel is 1 or 3) { result[target] = horizontal[((y - rowStart) * columns + column) * 4 + channel]; continue; }
                        if (source[(y * width + x) * 4 + 3] < 0.5f)
                        {
                            result[target] = source[target];
                            continue;
                        }
                        float sum = 0f;
                        for (int tap = -2; tap <= 2; tap++)
                            sum += horizontal[((Math.Clamp(y + tap * stepY, 0, height - 1) - rowStart) * columns + column) * 4 + channel] * weights[tap + 2];
                        result[target] = sum;
                    }
                }
                token.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// One bounded cache per view; only the actual coast inputs determine reuse.
        ///
        /// Both fields - the coast and the lake banks - are kept as their input arrays and
        /// their encoded samples, so a change is answered by diffing the inputs and recomputing
        /// only the window that can see it. Distances saturate at the coast range, so a sample
        /// farther than the range from every changed cell cannot change; ocean connectivity is
        /// the one global dependency, recomputed in full, and a change to it outside the window
        /// falls back to a whole rebuild. Whole rebuilds cost 742 ms for an inland edit and
        /// 820 ms for a shoreline edit at 320x256, most of it distance transforms over samples
        /// nothing had moved.
        /// </summary>
        public sealed class LiveCache
        {
            private readonly FieldCache _coast = new(lake: false);
            private readonly FieldCache _lake = new(lake: true);

            /// <summary>Changes whenever the coast texture's content changes, including in place.</summary>
            public ulong CoastRevision => _coast.Revision;
            /// <summary>Changes whenever the lake texture's content changes, including in place.</summary>
            public ulong LakeRevision => _lake.Revision;
            /// <summary>The raw coast field behind the current texture, for a render cache that would otherwise read it back from the GPU.</summary>
            internal Pixels? CoastField => _coast.FieldPixels;
            /// <summary>Fine-sample window the last coast update rewrote; null after a whole rebuild.</summary>
            internal Rect2I? CoastDirty => _coast.LastDirty;
            internal Pixels? LakeField => _lake.FieldPixels;
            internal Rect2I? LakeDirty => _lake.LastDirty;

            public ImageTexture Resolve(GridCellDataComponent cells, Vector2I origin, Vector2I size, int detail, float rangeTiles)
            {
                size = new Vector2I(Mathf.Max(1, size.X), Mathf.Max(1, size.Y));
                bool[] water = ReadWater(cells, origin, size);
                GridTerrainWaterPatch?[] patches = ReadPatches(cells, origin, size);
                return ResolveSamples(water, patches, size, detail, rangeTiles);
            }

            internal ImageTexture ResolveSamples(bool[] water, GridTerrainWaterPatch?[] patches, Vector2I size, int detail, float rangeTiles)
                => _coast.Resolve(water, patches, size, Mathf.Clamp(detail, 1, 16), Mathf.Max(1f, rangeTiles));

            /// <summary>Lake banks, cached on the lake patches alone; a land edit reuses the field.</summary>
            internal ImageTexture ResolveLakeSamples(GridTerrainWaterPatch?[] lakes, Vector2I size, int detail, float rangeTiles)
                => _lake.Resolve(null, lakes, size, Mathf.Clamp(detail, 1, 16), Mathf.Max(1f, rangeTiles));

            /// <summary>Adopts a coast field another stage computed for exactly these inputs, so the first edit does not rebuild it.</summary>
            internal ImageTexture AdoptCoast(bool[] water, GridTerrainWaterPatch?[] patches, Pixels pixels, ImageTexture texture,
                Vector2I size, int detail, float rangeTiles)
                => _coast.Adopt(water, patches, pixels, texture, size, Mathf.Clamp(detail, 1, 16), Mathf.Max(1f, rangeTiles));

            internal ImageTexture AdoptLake(GridTerrainWaterPatch?[] lakes, Pixels pixels, ImageTexture texture,
                Vector2I size, int detail, float rangeTiles)
                => _lake.Adopt(null, lakes, pixels, texture, size, Mathf.Clamp(detail, 1, 16), Mathf.Max(1f, rangeTiles));
        }

        /// <summary>One distance field kept with its inputs, updated by window where a change allows it.</summary>
        private sealed class FieldCache
        {
            private readonly bool _lake;
            private bool[]? _water;
            private GridTerrainWaterPatch?[]? _patches;
            private bool[]? _ocean;
            private byte[]? _field;
            private Vector2I _size, _fine;
            private int _detail, _samples;
            private float _range;
            private Image? _image;
            private ImageTexture? _texture;
            internal ulong Revision { get; private set; }
            /// <summary>Fine-sample window the last Resolve rewrote; null after a whole rebuild or adoption.</summary>
            internal Rect2I? LastDirty { get; private set; }
            internal Pixels? FieldPixels => _field is null ? null : new Pixels(_fine, _field, Image.Format.Rgbaf);

            internal FieldCache(bool lake) => _lake = lake;

            private bool Matches(Vector2I size, int detail, float range)
                => _texture is not null && GodotObject.IsInstanceValid(_texture) && _patches is not null && _field is not null
                    && _size == size && _detail == detail && _range == range;

            internal ImageTexture Resolve(bool[]? water, GridTerrainWaterPatch?[] patches, Vector2I size, int detail, float range)
            {
                if (!Matches(size, detail, range) || patches.Length != _patches!.Length || (water is not null) != (_water is not null))
                    return Rebuild(water, patches, size, detail, range);

                // Diff against the owned copies; the changed cells' bounding box is the window seed.
                int width = size.X;
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                for (int i = 0; i < patches.Length; i++)
                {
                    if (Equals(patches[i], _patches[i]) && (water is null || water[i] == _water![i])) continue;
                    int x = i % width, y = i / width;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
                if (minX == int.MaxValue) return _texture!;

                // Every sample within range of a changed cell is recomputed (update); the transform
                // runs over a window one more range wider (compute) so those samples see every
                // boundary that can reach them. +2: the half-cell interpolation and safety.
                int halo = Mathf.CeilToInt(range) + 2;
                Rect2I update = Clamp(new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1).Grow(halo), size);
                Rect2I compute = Clamp(update.Grow(halo), size);
                if ((long)compute.Size.X * compute.Size.Y * 2 > (long)size.X * size.Y)
                    return Rebuild(water, patches, size, detail, range);

                bool[]? ocean = null;
                if (!_lake)
                {
                    ocean = OceanMask(water!, size);
                    for (int i = 0; i < ocean.Length; i++)
                        if (ocean[i] != _ocean![i] && !update.HasPoint(new Vector2I(i % width, i / width)))
                            return Rebuild(water, patches, size, detail, range);
                }

                Array.Copy(patches, _patches, patches.Length);
                if (water is not null) Array.Copy(water, _water!, water.Length);
                if (ocean is not null) _ocean = ocean;
                WriteWindow(compute, update);
                LastDirty = new Rect2I(update.Position * _samples, update.Size * _samples);
                _image!.SetData(_fine.X, _fine.Y, false, Image.Format.Rgbaf, _field!);
                // A new texture rather than ImageTexture.Update: the headless renderer never applies
                // an Update, and the render cache and the probes read texture data back.
                _texture = ImageTexture.CreateFromImage(_image);
                Revision++;
                return _texture;
            }

            internal ImageTexture Adopt(bool[]? water, GridTerrainWaterPatch?[] patches, Pixels pixels, ImageTexture texture,
                Vector2I size, int detail, float range)
            {
                int samples = EffectiveDetail(size, detail);
                if (pixels.Format != Image.Format.Rgbaf || pixels.Size != size * samples || !GodotObject.IsInstanceValid(texture))
                    return Rebuild(water, patches, size, detail, range);
                Store(water, patches, pixels, size, detail, range);
                LastDirty = null;
                _image = Image.CreateFromData(_fine.X, _fine.Y, false, Image.Format.Rgbaf, _field!);
                _texture = texture;
                Revision++;
                return texture;
            }

            private ImageTexture Rebuild(bool[]? water, GridTerrainWaterPatch?[] patches, Vector2I size, int detail, float range)
            {
                bool[]? ocean = _lake ? null : OceanMask(water!, size);
                Pixels pixels = _lake
                    ? BuildLakePixels(patches, size, detail, range)
                    : BuildLivePixels(water!, patches, ocean!, size, detail, range);
                Store(water, patches, pixels, size, detail, range, ocean);
                LastDirty = null;
                _image = Image.CreateFromData(_fine.X, _fine.Y, false, Image.Format.Rgbaf, _field!);
                _texture = ImageTexture.CreateFromImage(_image);
                Revision++;
                return _texture;
            }

            private void Store(bool[]? water, GridTerrainWaterPatch?[] patches, Pixels pixels, Vector2I size, int detail, float range,
                bool[]? ocean = null)
            {
                _water = water is null ? null : (bool[])water.Clone();
                _patches = (GridTerrainWaterPatch?[])patches.Clone();
                _ocean = _lake ? null : ocean ?? OceanMask(water!, size);
                _field = pixels.Data;
                _fine = pixels.Size;
                _size = size;
                _detail = detail;
                _samples = EffectiveDetail(size, detail);
                _range = range;
            }

            private bool IsWater(Vector2 at)
            {
                if (!_lake) return SampleLiveWater(_water!, _patches!, _size.X, _size.Y, at);
                var cell = new Vector2I(Mathf.Clamp((int)at.X, 0, _size.X - 1), Mathf.Clamp((int)at.Y, 0, _size.Y - 1));
                return _patches![cell.Y * _size.X + cell.X]?.Contains(at - (Vector2)cell) ?? false;
            }

            // The same encoding BuildPixels writes, over a window. Samples are fine-grid; cells are
            // map cells; both windows are in cells.
            private void WriteWindow(Rect2I compute, Rect2I update)
            {
                int d = _samples;
                Vector2I fineOrigin = compute.Position * d, fineSize = compute.Size * d;
                int count = fineSize.X * fineSize.Y;
                var waterSamples = new bool[count];
                for (int fy = 0; fy < fineSize.Y; fy++)
                for (int fx = 0; fx < fineSize.X; fx++)
                    waterSamples[fy * fineSize.X + fx] = IsWater(new Vector2((fineOrigin.X + fx + 0.5f) / d, (fineOrigin.Y + fy + 0.5f) / d));
                float[] distances = TerrainEuclideanDistance.Signed(waterSamples, fineSize, d);
                bool[]? openWater = null;
                double[]? toOcean = null;
                if (!_lake)
                {
                    openWater = new bool[count];
                    for (int fy = 0; fy < fineSize.Y; fy++)
                    for (int fx = 0; fx < fineSize.X; fx++)
                    {
                        int cell = ((fineOrigin.Y + fy) / d) * _size.X + (fineOrigin.X + fx) / d;
                        openWater[fy * fineSize.X + fx] = waterSamples[fy * fineSize.X + fx] && _ocean![cell];
                    }
                    toOcean = TerrainEuclideanDistance.Squared(openWater, fineSize, true);
                }
                Span<float> field = MemoryMarshal.Cast<byte, float>(_field!.AsSpan());
                Vector2I innerOrigin = update.Position * d, innerEnd = update.End * d;
                for (int fy = innerOrigin.Y; fy < innerEnd.Y; fy++)
                for (int fx = innerOrigin.X; fx < innerEnd.X; fx++)
                {
                    int local = (fy - fineOrigin.Y) * fineSize.X + (fx - fineOrigin.X);
                    int cell = (fy / d) * _size.X + fx / d;
                    int pixel = (fy * _fine.X + fx) * 4;
                    float signed = distances[local];
                    field[pixel] = Mathf.Clamp(signed / _range * 0.5f + 0.5f, 0f, 1f);
                    field[pixel + 1] = !_lake && _ocean![cell] ? 1f : 0f;
                    double away = toOcean is null ? double.PositiveInfinity : toOcean[local];
                    float oceanDistance = openWater is not null && openWater[local] ? signed
                        : -TerrainEuclideanDistance.ToTiles(away, d);
                    field[pixel + 2] = Mathf.Clamp(oceanDistance / _range * 0.5f + 0.5f, 0f, 1f);
                    field[pixel + 3] = _patches![cell] is not null ? 1f : 0f;
                }
            }

            private static Rect2I Clamp(Rect2I rect, Vector2I size)
                => rect.Intersection(new Rect2I(Vector2I.Zero, size));
        }

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
            => BuildPixels(generator, size, detail, rangeTiles).Upload();

        /// <summary>The generated field before upload, for a caller that also reads it on the CPU.</summary>
        internal static Pixels BuildPixels(
            TerrainGeneratorComponent generator, Vector2I size, int detail, float rangeTiles)
        {
            ArgumentNullException.ThrowIfNull(generator);

            // All samples in this build share one recipe snapshot. Calling the
            // component per sample repeatedly constructs and compares settings.
            GeneratedTerrainField field = generator.ResolveField();
            return BuildPixels(size, detail, rangeTiles, field.IsWaterAtPosition, OceanCells(field, size));
        }

        /// <summary>
        /// How far each CELL is from the waterline, in tiles, positive out to sea: the R channel
        /// decoded at the cell's centre sample.
        ///
        /// Depth has one owner, and this is it. The block view used to measure its own with a
        /// four-neighbour sweep out from land - Manhattan steps - while the sea drawn over that
        /// bed shades its depth from this field, which is Euclidean. Along a diagonal coast the
        /// two disagreed by up to a step, so the seabed band changed where the water tint did
        /// not (VIEW-05).
        ///
        /// Saturates at the range the field was built with, as the encoding does: a caller
        /// wanting bands wider than that must build the field with a wider range.
        /// </summary>
        internal static float[] CellDistances(Pixels field, Vector2I size, float rangeTiles)
        {
            int width = Mathf.Max(1, size.X), height = Mathf.Max(1, size.Y);
            var distances = new float[checked(width * height)];
            int detailX = Mathf.Max(1, field.Size.X / width), detailY = Mathf.Max(1, field.Size.Y / height);
            float range = Mathf.Max(1.0f, rangeTiles);
            ReadOnlySpan<float> texels = MemoryMarshal.Cast<byte, float>(field.Data.AsSpan());

            for (int y = 0; y < height; y++)
            {
                // The sample nearest the cell's centre: samples sit at (x + 0.5) / detail tiles,
                // so the centre of cell y falls in sample y * detail + detail / 2.
                int fy = Math.Min(field.Size.Y - 1, (y * detailY) + (detailY / 2));
                for (int x = 0; x < width; x++)
                {
                    int fx = Math.Min(field.Size.X - 1, (x * detailX) + (detailX / 2));
                    float encoded = texels[((fy * field.Size.X) + fx) * 4];
                    distances[(y * width) + x] = (encoded - 0.5f) * 2f * range;
                }
            }
            return distances;
        }

        /// <summary>Live grid coastline. Boundary-connected water is ocean; enclosed water is inland.</summary>
        public static ImageTexture Build(GridCellDataComponent cells, Vector2I origin, Vector2I size, int detail, float rangeTiles)
            => BuildLive(ReadWater(cells, origin, size), ReadPatches(cells, origin, size), size, detail, rangeTiles);

        // Lake membership is persisted at sub-cell resolution. Never infer it
        // from enclosed water: that would also put lake banks along rivers.
        //
        // Adding rivers here was tried on 2026-09-18 and reverted the same hour: a river is a dense
        // network of sub-cell threads, so a band grown from every one of them at a lake's width
        // merged into a sand desert holding a couple of ponds. A riverbank needs the river as it is
        // DRAWN and a width of its own - see GeneratedTerrainField.IsLakeAtPosition.
        internal static ImageTexture BuildLake(GridCellDataComponent? cells, GeneratedTerrainField? field,
            Vector2I origin, Vector2I size, int detail, float rangeTiles)
        {
            Func<Vector2, bool> isLake;
            bool[]? smoothCells = null;
            if (cells is null)
                isLake = field!.IsLakeAtPosition;
            else
            {
                var patches = new GridTerrainWaterPatch?[size.X * size.Y];
                smoothCells = new bool[patches.Length];
                for (int y = 0; y < size.Y; y++)
                for (int x = 0; x < size.X; x++)
                {
                    int index = y * size.X + x;
                    patches[index] = cells.LakePatchAtCell(origin + new Vector2I(x, y));
                    smoothCells[index] = patches[index] is not null;
                }
                isLake = at =>
                {
                    var cell = new Vector2I(Mathf.Clamp((int)at.X, 0, size.X - 1),
                        Mathf.Clamp((int)at.Y, 0, size.Y - 1));
                    return patches[cell.Y * size.X + cell.X]?.Contains(at - (Vector2)cell) ?? false;
                };
            }
            return Build(size, detail, rangeTiles, isLake, new bool[size.X * size.Y], smoothCells);
        }

        internal static ImageTexture BuildLake(GridTerrainWaterPatch?[] patches, Vector2I size, int detail, float rangeTiles)
            => BuildLakePixels(patches, size, detail, rangeTiles).Upload();

        internal static Pixels BuildLakePixels(GridTerrainWaterPatch?[] patches, Vector2I size, int detail,
            float rangeTiles, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            var smooth = new bool[patches.Length];
            for (int i = 0; i < patches.Length; i++) smooth[i] = patches[i] is not null;
            bool IsLake(Vector2 at)
            {
                var cell = new Vector2I(Mathf.Clamp((int)at.X, 0, size.X - 1), Mathf.Clamp((int)at.Y, 0, size.Y - 1));
                return patches[cell.Y * size.X + cell.X]?.Contains(at - (Vector2)cell) ?? false;
            }
            return BuildPixels(size, detail, rangeTiles, IsLake, new bool[patches.Length], smooth, token);
        }

        /// <summary>One live surface snapshot for prop placement, in renderer-local tile coordinates.</summary>
        internal static Func<Vector2, bool> CreateLiveWaterSampler(GridCellDataComponent cells, Vector2I origin, Vector2I size)
        {
            bool[] wet = ReadWater(cells, origin, size);
            GridTerrainWaterPatch?[] patches = ReadPatches(cells, origin, size);
            return at => SampleLiveWater(wet, patches, Mathf.Max(1, size.X), Mathf.Max(1, size.Y), at);
        }

        /// <summary>Local main-thread queries for streamed props, without a whole-map snapshot.</summary>
        internal static Func<Vector2, bool> CreateLiveWaterQuery(GridCellDataComponent cells, Vector2I origin, Vector2I size)
        {
            int width = Mathf.Max(1, size.X), height = Mathf.Max(1, size.Y);
            bool Wet(int x, int y) => TerrainTileSets.IsWaterKind(GridCellRules.TerrainKindAt(cells, origin + new Vector2I(x, y)));
            return at => ReconstructWater(width, height, at, Wet,
                (x, y) => cells.WaterPatchAtCell(origin + new Vector2I(x, y)));
        }

        private static GridTerrainWaterPatch?[] ReadPatches(GridCellDataComponent cells, Vector2I origin, Vector2I size)
        {
            int width = Mathf.Max(1, size.X), height = Mathf.Max(1, size.Y);
            var patches = new GridTerrainWaterPatch?[checked(width * height)];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    patches[y * width + x] = cells.WaterPatchAtCell(origin + new Vector2I(x, y));
            return patches;
        }

        private static bool[] ReadWater(GridCellDataComponent cells, Vector2I origin, Vector2I size)
        {
            ArgumentNullException.ThrowIfNull(cells);
            int width = Mathf.Max(1, size.X), height = Mathf.Max(1, size.Y);
            var wet = new bool[checked(width * height)];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    wet[y * width + x] = TerrainTileSets.IsWaterKind(GridCellRules.TerrainKindAt(cells, origin + new Vector2I(x, y)));
            return wet;
        }

        private static ImageTexture BuildLive(bool[] wet, GridTerrainWaterPatch?[] patches, Vector2I size, int detail, float rangeTiles)
            => BuildLivePixels(wet, patches, size, detail, rangeTiles).Upload();

        internal static Pixels BuildLivePixels(bool[] wet, GridTerrainWaterPatch?[] patches, Vector2I size,
            int detail, float rangeTiles, CancellationToken token = default)
            => BuildLivePixels(wet, patches, OceanMask(wet, size, token), size, detail, rangeTiles, token);

        private static Pixels BuildLivePixels(bool[] wet, GridTerrainWaterPatch?[] patches, bool[] ocean, Vector2I size,
            int detail, float rangeTiles, CancellationToken token = default)
        {
            int width = Mathf.Max(1, size.X), height = Mathf.Max(1, size.Y);
            return BuildPixels(size, detail, rangeTiles,
                at => SampleLiveWater(wet, patches, width, height, at),
                ocean, Array.ConvertAll(patches, patch => patch is not null), token);
        }

        /// <summary>Boundary-connected water, grown by one cell: the open-sea mask the field's G channel and surf read.</summary>
        internal static bool[] OceanMask(bool[] wet, Vector2I size, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            int width = Mathf.Max(1, size.X), height = Mathf.Max(1, size.Y);
            var ocean = new bool[wet.Length];
            var queue = new System.Collections.Generic.Queue<int>();
            void Add(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                int index = y * width + x;
                if (!wet[index] || ocean[index]) return;
                ocean[index] = true;
                queue.Enqueue(index);
            }
            for (int x = 0; x < width; x++) { Add(x, 0); Add(x, height - 1); }
            for (int y = 0; y < height; y++) { Add(0, y); Add(width - 1, y); }
            int visited = 0;
            while (queue.TryDequeue(out int index))
            {
                if ((visited++ & 1023) == 0) token.ThrowIfCancellationRequested();
                int x = index % width, y = index / width;
                Add(x - 1, y); Add(x + 1, y); Add(x, y - 1); Add(x, y + 1);
            }
            return GrowOcean(ocean, width, height, token);
        }

        /// <summary>Samples per cell edge after the field's sample budget; the cache and BuildPixels must agree on it.</summary>
        internal static int EffectiveDetail(Vector2I size, int detail)
        {
            detail = Mathf.Clamp(detail, 1, 16);
            while (detail > 1 && (long)Mathf.Max(1, size.X) * Mathf.Max(1, size.Y) * detail * detail > 1_250_000) detail--;
            return detail;
        }

        /// <summary>
        /// The sub-cell water reconstruction, in ONE place: a patch cell answers for its own
        /// interior, and everywhere else the four nearest cell centres are bilinearly interpolated
        /// and thresholded at a half. The two live entry points differ only in where they read
        /// wet-ness and patches from - a captured whole-map snapshot or a per-query live read - so
        /// they supply those as delegates and share this geometry. If the two copies diverged, the
        /// same coastline would place props differently by map size.
        /// </summary>
        private static bool ReconstructWater(int width, int height, Vector2 at,
            Func<int, int, bool> wet, Func<int, int, GridTerrainWaterPatch?> patchAt)
        {
            int cellX = Mathf.Clamp(Mathf.FloorToInt(at.X), 0, width - 1);
            int cellY = Mathf.Clamp(Mathf.FloorToInt(at.Y), 0, height - 1);
            if (patchAt(cellX, cellY) is { } patch)
                return patch.IsWater(at - new Vector2(cellX, cellY), wet(cellX, cellY));

            // Interpolate centre samples, not cell rectangles. The half-cell
            // contour rounds corners while preserving every gameplay cell centre
            // and straight, one-cell-wide channels. Clamp at the map boundary so
            // reconstruction never invents an ocean outside a land-edged map.
            float x = Mathf.Clamp(at.X - 0.5f, 0f, width - 1f);
            float y = Mathf.Clamp(at.Y - 0.5f, 0f, height - 1f);
            int x0 = (int)x, y0 = (int)y;
            int x1 = Math.Min(x0 + 1, width - 1), y1 = Math.Min(y0 + 1, height - 1);
            float upper = Mathf.Lerp(wet(x0, y0) ? 1f : 0f, wet(x1, y0) ? 1f : 0f, x - x0);
            float lower = Mathf.Lerp(wet(x0, y1) ? 1f : 0f, wet(x1, y1) ? 1f : 0f, x - x0);
            return Mathf.Lerp(upper, lower, y - y0) >= 0.5f;
        }

        private static bool SampleLiveWater(bool[] wet, GridTerrainWaterPatch?[] patches, int width, int height, Vector2 at)
            => ReconstructWater(width, height, at,
                (x, y) => wet[y * width + x],
                (x, y) => patches[y * width + x]);

        private static ImageTexture Build(Vector2I size, int detail, float rangeTiles, Func<Vector2, bool> isWater, bool[] ocean, bool[]? smoothCells = null)
            => BuildPixels(size, detail, rangeTiles, isWater, ocean, smoothCells).Upload();

        private static Pixels BuildPixels(Vector2I size, int detail, float rangeTiles, Func<Vector2, bool> isWater,
            bool[] ocean, bool[]? smoothCells = null, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            detail = EffectiveDetail(size, detail);
            var fine = new Vector2I(Mathf.Max(1, size.X) * detail, Mathf.Max(1, size.Y) * detail);
            int count = fine.X * fine.Y;

            var water = new bool[count];
            for (int y = 0; y < fine.Y; y++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < fine.X; x++)
                {
                    var at = new Vector2((x + 0.5f) / detail, (y + 0.5f) / detail);
                    water[(y * fine.X) + x] = isWater(at);
                }
            }

            float[] distances = TerrainEuclideanDistance.Signed(water, fine, detail, token);
            var openWater = new bool[count];
            for (int y = 0; y < fine.Y; y++)
            for (int x = 0; x < fine.X; x++)
                openWater[y * fine.X + x] = water[y * fine.X + x]
                    && ocean[(y / detail) * Mathf.Max(1, size.X) + x / detail];
            double[] toOcean = TerrainEuclideanDistance.Squared(openWater, fine, true, token);

            // Which water is OPEN SEA. Surf belongs to a coast with a fetch
            // behind it; a lake or a river has no swell running onto it, and
            // drawing breakers around a pond reads as wrong immediately.

            float range = Mathf.Max(1.0f, rangeTiles);
            // Build pixels in managed memory instead of crossing the native
            // boundary for every sub-cell in a potentially million-pixel field.
            var pixels = new float[checked(count * 4)];
            for (int y = 0; y < fine.Y; y++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < fine.X; x++)
                {
                    int index = (y * fine.X) + x;
                    float signed = distances[index];
                    float encoded = Mathf.Clamp((signed / range * 0.5f) + 0.5f, 0.0f, 1.0f);
                    int pixel = index * 4;
                    pixels[pixel] = encoded;
                    pixels[pixel + 1] = ocean[((y / detail) * Mathf.Max(1, size.X)) + (x / detail)] ? 1f : 0f;
                    float oceanDistance = openWater[index] ? signed
                        : -TerrainEuclideanDistance.ToTiles(toOcean[index], detail);
                    pixels[pixel + 2] = Mathf.Clamp(oceanDistance / range * 0.5f + 0.5f, 0f, 1f);
                    pixels[pixel + 3] = smoothCells is null || smoothCells[(y / detail) * size.X + x / detail] ? 1f : 0f;
                }
            }
            byte[] bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
            token.ThrowIfCancellationRequested();
            return new Pixels(fine, bytes, Image.Format.Rgbaf);
        }

        /// <summary>
        /// Cells that are open sea, grown by one cell.
        ///
        /// The growth matters: the coast map is sampled below the tile grid and
        /// filtered linearly, so without it the land-side fringe of a coastal
        /// tile reads as "not sea" and the surf is cut off exactly where it
        /// should be strongest.
        /// </summary>
        private static bool[] OceanCells(GeneratedTerrainField field, Vector2I size)
        {
            int width = Mathf.Max(1, size.X);
            int height = Mathf.Max(1, size.Y);
            int count = width * height;

            var seed = new bool[count];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    seed[(y * width) + x] = field.WaterSourceAtCell(new Vector2I(x, y)) == "ocean";
            }

            return GrowOcean(seed, width, height);
        }

        private static bool[] GrowOcean(bool[] seed, int width, int height, CancellationToken token = default)
        {
            var grown = new bool[seed.Length];
            for (int y = 0; y < height; y++)
            {
                token.ThrowIfCancellationRequested();
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

    }
}
