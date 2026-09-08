using Beep.ECS;
using Godot;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

public partial class TerrainIdentitySmoke : Node
{
    public bool Run()
    {
        var size = new Vector2I(1024, 1024);
        var field = Make(size);
        var origin = new Vector2I(-50, 70);
        string identity = field.UndergroundIdentity(origin, size);
        if (identity != Oracle(field, origin, size)) return Fail("Full identity differs from independent stream");
        var crop = new Vector2I(17, 13);
        if (field.UndergroundIdentity(origin, crop) != Oracle(field, origin, crop)) return Fail("Crop identity differs");
        if (field.UndergroundIdentity(Vector2I.Zero, size) == identity) return Fail("Origin is missing from identity");
        if (field.UndergroundIdentity(origin, crop) == identity) return Fail("Size is missing from identity");
        var small = new Vector2I(8, 8);
        string baseline = Make(small).UndergroundIdentity(origin, small);
        foreach (int change in new[] { 1, 2, 3 })
            if (Make(small, change).UndergroundIdentity(origin, small) == baseline)
                return Fail("Changed underground content retained identity");
        if (Make(small, 4).UndergroundIdentity(origin, small) != baseline)
            return Fail("Same richness band changed metadata identity");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { TerrainUndergroundIdentity.Content(field, size, cancelled.Token); return Fail("Cancelled hash executed"); }
        catch (OperationCanceledException) { }

        // Warm cryptography/JIT before measuring repeated publication lookups.
        _ = field.UndergroundIdentity(origin, size);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
            if (field.UndergroundIdentity(origin, size) != identity) return Fail("Identity is unstable");
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (allocated > 1_000_000) return Fail("Repeated publication rebuilt content buffers");
        GD.Print($"[terrain-identity] content/bounds/crop/cancel OK; 1000 lookups={watch.ElapsedMilliseconds} ms, {allocated} bytes");
        return true;
    }

    private static GeneratedTerrainField Make(Vector2I size, int change = 0)
    {
        var world = new TerrainGenerationBuffer(size.X, size.Y, 1);
        for (int i = 0; i < size.X * size.Y; i++)
        {
            world.CellUndergroundResource[i] = i % 3 == 0 ? "iron" : "";
            world.CellUndergroundRichness[i] = .3f;
            world.CellUndergroundDepth[i] = 1;
        }
        if (change == 1) world.CellUndergroundResource[0] = "gold";
        if (change == 2) world.CellUndergroundRichness[0] = .8f;
        if (change == 3) world.CellUndergroundDepth[0] = 2;
        if (change == 4) world.CellUndergroundRichness[0] = .4f;
        return new GeneratedTerrainField(world, default);
    }

    private static string Oracle(GeneratedTerrainField field, Vector2I origin, Vector2I size)
    {
        using var content = new MemoryStream();
        using var writer = new BinaryWriter(content);
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
        {
            var cell = new Vector2I(x, y);
            writer.Write(field.UndergroundResourceAtCell(cell));
            writer.Write(Math.Clamp((int)Math.Floor(Math.Clamp(field.UndergroundRichnessAtCell(cell), 0, 1) * 4), 0, 3));
            writer.Write(field.UndergroundDepthAtCell(cell));
        }
        writer.Flush();
        using var identity = new MemoryStream();
        using var header = new BinaryWriter(identity);
        header.Write(2);
        header.Write(origin.X);
        header.Write(origin.Y);
        header.Write(size.X);
        header.Write(size.Y);
        header.Write(SHA256.HashData(content.ToArray()));
        header.Flush();
        return Convert.ToHexString(SHA256.HashData(identity.ToArray()));
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
