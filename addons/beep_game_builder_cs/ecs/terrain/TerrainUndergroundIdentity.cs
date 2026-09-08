using Godot;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Beep.ECS;

internal static class TerrainUndergroundIdentity
{
    internal static int RichnessBand(float richness)
        => Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp(richness, 0f, 1f) * 4f), 0, 3);

    internal static byte[] Content(GeneratedTerrainField field, Vector2I size,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        using var hash = SHA256.Create();
        using var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
        // Batch the small per-cell writes without retaining a full-map byte copy.
        using var buffered = new BufferedStream(stream, 16384);
        using var writer = new BinaryWriter(buffered);
        for (int y = 0; y < size.Y; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < size.X; x++)
            {
                var cell = new Vector2I(x, y);
                writer.Write(field.UndergroundResourceAtCell(cell));
                writer.Write(RichnessBand(field.UndergroundRichnessAtCell(cell)));
                writer.Write(field.UndergroundDepthAtCell(cell));
            }
        }
        writer.Flush();
        stream.FlushFinalBlock();
        cancellation.ThrowIfCancellationRequested();
        return hash.Hash!;
    }

    internal static string Compose(Vector2I origin, Vector2I size, ReadOnlySpan<byte> content)
    {
        Span<byte> identity = stackalloc byte[52];
        BinaryPrimitives.WriteInt32LittleEndian(identity, 2);
        BinaryPrimitives.WriteInt32LittleEndian(identity[4..], origin.X);
        BinaryPrimitives.WriteInt32LittleEndian(identity[8..], origin.Y);
        BinaryPrimitives.WriteInt32LittleEndian(identity[12..], size.X);
        BinaryPrimitives.WriteInt32LittleEndian(identity[16..], size.Y);
        content.CopyTo(identity[20..]);
        return Convert.ToHexString(SHA256.HashData(identity));
    }
}
