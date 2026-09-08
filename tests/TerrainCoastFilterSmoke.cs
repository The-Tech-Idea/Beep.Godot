using Beep.ECS;
using Godot;
using System;

public partial class TerrainCoastFilterSmoke : Node
{
    private readonly TerrainCoastField.RenderCache _cache = new();

    // A fixed source per bake: the content revision that in-place coast updates would carry is 0 here.
    public ImageTexture Bake(ImageTexture source, Vector2I cells) => _cache.Resolve(source, cells, 0);

    public ImageTexture BuildRaw(TerrainGeneratorComponent generator, Vector2I size, int detail)
        => TerrainCoastField.Build(generator, size, detail, 5f);

    public ImageTexture BuildLive(GridCellDataComponent cells, Vector2I origin, Vector2I size, int detail)
        => TerrainCoastField.Build(cells, origin, size, detail, 5f);

    public bool Run()
    {
        using var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        image.Fill(new Color(0.8f, 1f, 0.8f, 1f));
        using var raw = ImageTexture.CreateFromImage(image);
        byte[] original = image.GetData();
        ImageTexture baked = Bake(raw, new Vector2I(8, 8));
        Check(baked != raw && baked.GetSize() == raw.GetSize() * 2, "Render texture was not reconstructed");
        Check(Bake(raw, new Vector2I(8, 8)) == baked, "Unchanged source allocated another texture");
        using var pixels = baked.GetImage();
        Check(pixels.GetPixel(20, 20) == image.GetPixel(0, 0), "Constant coast changed");
        foreach (int y in new[] { 0, 1, 62, 63 })
        foreach (int x in new[] { 0, 1, 62, 63 })
            Check(pixels.GetPixel(x, y) == image.GetPixel(0, 0), "Cubic resize changed a constant border");
        using var rawAfter = raw.GetImage();
        Check(original.AsSpan().SequenceEqual(rawAfter.GetData()), "Raw distance snapshot was modified");
        Check(Bake(raw, new Vector2I(32, 32)) == raw, "Coarse cell mask was reconstructed");
        using var changed = ImageTexture.CreateFromImage(image);
        Check(Bake(changed, new Vector2I(8, 8)) != baked, "Changed snapshot reused an old render texture");
        using var largeImage = Image.CreateEmpty(1025, 1024, false, Image.Format.Rgba8);
        using var large = ImageTexture.CreateFromImage(largeImage);
        Check(Bake(large, new Vector2I(256, 256)) == large, "Render texture exceeded its allocation budget");
        VerifyBlockBorders();
        VerifyBudgetedFloatSmoothing();
        VerifyPreparedContours();
        GD.Print("[terrain-coast-filter] cache, bounds, source isolation and allocation cap OK");
        return true;
    }

    private void VerifyBudgetedFloatSmoothing()
    {
        // Huge lab map at its budget-reduced detail: 2x exceeds the display
        // budget, but throwing away smoothing makes generated shores square.
        const int width = 1408, height = 880;
        var values = new float[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int at = (y * width + x) * 4;
            float distance = x < width / 2 && y < height / 2 ? 0.4f : 0.6f;
            values[at] = values[at + 2] = distance;
            values[at + 1] = values[at + 3] = 1f;
        }
        using var image = Image.CreateFromData(width, height, false, Image.Format.Rgbaf,
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan()).ToArray());
        using var raw = ImageTexture.CreateFromImage(image);
        var size = new Vector2I(128, 80);
        ImageTexture result = Bake(raw, size);
        Check(result != raw && result.GetSize() == raw.GetSize(), "Large generated coast skipped bounded smoothing");
        Check(Bake(raw, size) == result, "Bounded smoothing was not cached");
        using var actual = result.GetImage();
        var corner = new Vector2I(width / 2 - 1, height / 2 - 1);
        Color pixel = actual.GetPixelv(corner);
        Check(pixel.R > 0.45f && pixel.R < 0.55f && pixel.B == pixel.R,
            "Large coastline corner retained its square step or beach diverged");
        Check(pixel.G == 1f && pixel.A == 1f, "Smoothing altered ocean or geometry flags");
        using var unchanged = raw.GetImage();
        Check(unchanged.GetData().AsSpan().SequenceEqual(image.GetData()), "Bounded smoothing mutated its source");
    }

    private static void VerifyPreparedContours()
    {
        var size = new Vector2I(32, 24);
        var cells = new Vector2I(8, 6);
        using var image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgbaf);
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
            image.SetPixel(x, y, new Color(x < 16 && y < 12 ? 0.2f : 0.8f, 1f, x / 32f, x == 2 ? 0f : 1f));
        var input = new TerrainCoastField.Pixels(size, image.GetData(), image.GetFormat());
        byte[] original = (byte[])input.Data.Clone();
        var prepared = TerrainCoastField.RenderCache.PrepareContours(input, cells);
        Check(original.AsSpan().SequenceEqual(input.Data), "Worker smoothing mutated raw coast data");
        Check(!original.AsSpan().SequenceEqual(prepared.Data), "Worker skipped float contour smoothing");
        using var raw = ImageTexture.CreateFromImage(image);
        using var other = ImageTexture.CreateFromImage(image);
        using var expected = new TerrainCoastField.RenderCache().Resolve(raw, cells, 0);
        using var expectedImage = expected.GetImage();
        var cache = new TerrainCoastField.RenderCache();
        cache.Prepare(raw, cells, prepared);
        using var actual = cache.Resolve(raw, cells, 0);
        using var actualImage = actual.GetImage();
        Check(expectedImage.GetData().AsSpan().SequenceEqual(actualImage.GetData()), "Prepared contours differ from synchronous rendering");

        // Deliberately invalid prepared data must be ignored for another source or bounds.
        var poison = new TerrainCoastField.Pixels(size, new byte[input.Data.Length], input.Format);
        foreach (bool wrongSource in new[] { true, false })
        {
            var isolated = new TerrainCoastField.RenderCache();
            isolated.Prepare(wrongSource ? other : raw, wrongSource ? cells : cells + Vector2I.One, poison);
            using var result = isolated.Resolve(raw, cells, 0);
            using var resultImage = result.GetImage();
            Check(expectedImage.GetData().AsSpan().SequenceEqual(resultImage.GetData()), "Stale prepared contours were accepted");
        }
        Check(ReferenceEquals(input, TerrainCoastField.RenderCache.PrepareContours(input, size)), "Coarse mask allocated smoothing buffers");
        using var cancellation = new System.Threading.CancellationTokenSource();
        cancellation.Cancel();
        bool cancelled = false;
        try { TerrainCoastField.RenderCache.PrepareContours(input, cells, cancellation.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Contour smoothing ignored cancellation");
        GD.Print("[terrain-coast-filter] prepared contour parity, source/bounds isolation and cancellation OK");
    }

    public void Profile()
    {
        foreach (int cells in new[] { 32, 144, 240 })
        {
            int width = cells * 4;
            var pixels = new byte[width * width * 4];
            for (int y = 0; y < width; y++)
            for (int x = 0; x < width; x++)
            {
                int at = (y * width + x) * 4;
                float shore = width * 0.4f + Mathf.Sin(y * 0.012f) * width * 0.12f;
                byte distance = (byte)(Mathf.Clamp(0.5f + (x - shore) / 40f, 0f, 1f) * 255);
                pixels[at] = pixels[at + 2] = distance;
                pixels[at + 1] = pixels[at + 3] = 255;
            }
            using var image = Image.CreateFromData(width, width, false, Image.Format.Rgba8, pixels);
            using var source = ImageTexture.CreateFromImage(image);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            ImageTexture result = Bake(source, new Vector2I(cells, cells));
            double cold = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            for (int repeat = 0; repeat < 1000; repeat++)
                Check(Bake(source, new Vector2I(cells, cells)) == result, "Profile cache was not reused");
            GD.Print($"[terrain-coast-filter] cells={cells} first reconstruction={cold:F3} ms; cached={timer.Elapsed.TotalMilliseconds / 1000:F5} ms");
        }
    }

    private void VerifyBlockBorders()
    {
        const int width = 130, height = 98;
        using var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float value = x < 62 ? 0.1f : Mathf.Clamp(0.5f + Mathf.Sin(x * 0.09f + y * 0.11f) * 0.45f, 0f, 1f);
            image.SetPixel(x, y, new Color(value, y < 64 ? 1f : 0f, value, 1f));
        }
        using var source = ImageTexture.CreateFromImage(image);
        using var actual = Bake(source, new Vector2I(32, 24)).GetImage();
        // Independent whole-image reconstruction checks every output texel,
        // especially partial blocks, ocean flags and the two-pixel shared halo.
        using var padded = Image.CreateEmpty(width + 4, height + 4, false, Image.Format.Rgba8);
        for (int y = 0; y < height + 4; y++)
        for (int x = 0; x < width + 4; x++)
            padded.SetPixel(x, y, image.GetPixel(Math.Clamp(x - 2, 0, width - 1), Math.Clamp(y - 2, 0, height - 1)));
        padded.Resize((width + 4) * 2, (height + 4) * 2, Image.Interpolation.Cubic);
        using var expected = padded.GetRegion(new Rect2I(4, 4, width * 2, height * 2));
        Check(actual.GetData().AsSpan().SequenceEqual(expected.GetData()), "Blocked reconstruction differs from whole-image cubic");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
