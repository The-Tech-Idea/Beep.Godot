using Godot;
using System;

namespace Beep.ECS;

/// <summary>Bounded, cell-stable decorative anchors shared by terrain projections.</summary>
internal static class TerrainFeatureScatter
{
    public const int MaximumCount = 16;

    public static int Fill(Span<Vector2> offsets, Vector2I identity, int seed,
        float spread, Vector2 sampleCentre, Func<Vector2, bool> dry)
    {
        spread = Mathf.Clamp(spread, 0f, 1f);
        int written = 0;
        for (int slot = 0; slot < offsets.Length; slot++)
        {
            Vector2 best = Vector2.Zero;
            float bestDistance = -1f;
            // Choose the farthest of a small fixed candidate set, avoiding
            // coincident clumps without a map-wide spatial index or retry loop.
            for (int candidate = 0; candidate < 12; candidate++)
            {
                int salt = unchecked(seed + 1013 + slot * 1543 + candidate * 7919);
                var offset = new Vector2(
                    TerrainGeometry.Hash01(identity.X, identity.Y, salt) - 0.5f,
                    TerrainGeometry.Hash01(identity.X, identity.Y, unchecked(salt + 104729)) - 0.5f) * spread;
                if (!dry(sampleCentre + offset)) continue;
                float distance = float.MaxValue;
                for (int previous = 0; previous < written; previous++)
                    distance = Mathf.Min(distance, offset.DistanceSquaredTo(offsets[previous]));
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                best = offset;
            }
            if (bestDistance < 0f)
            {
                // Preserve a lone valid centre, but do not stack every rejected
                // shoreline candidate there. Zero spread is an explicit opt-out.
                if ((written > 0 && spread > 0f) || !dry(sampleCentre)) continue;
            }
            if (written > 0 && spread > 0f && bestDistance == 0f) continue;
            offsets[written++] = best;
        }
        return written;
    }
}
