using Beep.ECS;
using Godot;
using System;
using System.Diagnostics;
using System.Threading;

public partial class TerrainSampleValuesSmoke : Node
{
    public bool Run()
    {
        foreach (int width in new[] { 1, 31, 32, 33, 65 })
        foreach (int height in new[] { 1, 32, 67 })
        {
            var shade = new float[width * height];
            var water = new WaterBody[shade.Length];
            for (int i = 0; i < shade.Length; i++)
            {
                shade[i] = BitConverter.Int32BitsToSingle((i % 7) switch
                { 0 => int.MinValue, 1 => 0x7fc00001, 2 => 0x7fc00002, _ => i * 31 });
                water[i] = (WaterBody)(i % 4);
            }
            var shades = new TerrainSampleValues<float>(shade, width, height);
            var waters = new TerrainSampleValues<WaterBody>(water, width, height);
            for (int i = 0; i < shade.Length; i++)
                if (BitConverter.SingleToInt32Bits(shades[i]) != BitConverter.SingleToInt32Bits(shade[i])
                    || waters[i] != water[i]) return Fail("Sample or partial-chunk indexing changed");
            float original = shades[0];
            shade[0] = 123;
            if (BitConverter.SingleToInt32Bits(shades[0]) != BitConverter.SingleToInt32Bits(original))
                return Fail("Published chunks retained mutable source arrays");
            try { _ = shades[-1]; return Fail("Negative index accepted"); } catch (IndexOutOfRangeException) { }
            try { _ = shades[shade.Length]; return Fail("Past-end index accepted"); } catch (IndexOutOfRangeException) { }
        }
        var watch = Stopwatch.StartNew();
        var ocean = new WaterBody[1024 * 1024];
        Array.Fill(ocean, WaterBody.Ocean);
        var compact = new TerrainSampleValues<WaterBody>(ocean, 1024, 1024);
        if (compact.UniformChunkCount != 1024 || compact.PayloadBytes != 1024)
            return Fail("Uniform ocean did not collapse to one value per chunk");
        var flat = new float[ocean.Length];
        Array.Fill(flat, 1f);
        var shadesFlat = new TerrainSampleValues<float>(flat, 1024, 1024);
        if (shadesFlat.PayloadBytes != 4096 || shadesFlat.UniformChunkCount != 1024)
            return Fail("Flat shading did not collapse");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { _ = new TerrainSampleValues<float>(flat, 1024, 1024, cancelled.Token); return Fail("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        try { _ = new TerrainSampleValues<float>(flat, 1023, 1024); return Fail("Invalid dimensions accepted"); }
        catch (ArgumentException) { }
        var buffer = new TerrainGenerationBuffer(33, 35, 1);
        Array.Fill(buffer.Terrain, "grass");
        Array.Fill(buffer.Water, WaterBody.Lake);
        Array.Fill(buffer.Shade, .75f);
        try { buffer.PackTerrain(cancelled.Token); return Fail("Cancelled terrain packing executed"); }
        catch (OperationCanceledException) { }
        try { buffer.PackWater(cancelled.Token); return Fail("Cancelled water packing executed"); }
        catch (OperationCanceledException) { }
        try { buffer.PackShade(cancelled.Token); return Fail("Cancelled shade packing executed"); }
        catch (OperationCanceledException) { }
        if (buffer.Terrain[0] != "grass" || buffer.Water[0] != WaterBody.Lake || buffer.Shade[0] != .75f)
            return Fail("Cancelled packing discarded its source");
        var kinds = buffer.PackTerrain(default);
        var packedWater = buffer.PackWater(default);
        var packedShade = buffer.PackShade(default);
        buffer.ReleaseGenerationScratch();
        for (int i = 0; i < buffer.Count; i++)
            if (kinds[i] != "grass" || packedWater[i] != WaterBody.Lake || packedShade[i] != .75f)
                return Fail("Retiring dense source invalidated published samples");
        foreach (Action read in new Action[] { () => { _ = buffer.Terrain; }, () => { _ = buffer.Water; },
            () => { _ = buffer.Shade; }, () => { _ = buffer.Land; }, () => { _ = buffer.Footprint; } })
        {
            try { read(); return Fail("Retired generation array remains available or was reallocated"); }
            catch (InvalidOperationException) { }
        }
        GD.Print($"[terrain-sample-values] bit-exact partial chunks, source isolation, cancellation OK; uniform 1024x1024 water/shade payload=5120 bytes (excluding chunk/object overhead), {watch.ElapsedMilliseconds} ms");
        return true;
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
