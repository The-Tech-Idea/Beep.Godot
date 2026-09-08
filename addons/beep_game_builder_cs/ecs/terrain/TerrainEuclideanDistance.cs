using Godot;
using System;
using System.Threading;

namespace Beep.ECS;

/// <summary>Linear-time Euclidean distance on a sampled binary mask, in sample units.</summary>
internal static class TerrainEuclideanDistance
{
    // Separable lower-envelope algorithm: Felzenszwalb and Huttenlocher,
    // Distance Transforms of Sampled Functions, Theory of Computing 8 (2012).
    // https://cs.brown.edu/people/pfelzens/papers/dt-final.pdf
    internal static double[] Squared(bool[] mask, Vector2I size, bool seedValue, CancellationToken token = default)
        => Squared(mask, size, seedValue, new double[mask.Length], token);

    /// <summary>
    /// The transform written into <paramref name="result"/>, which the caller
    /// owns and which must be at least the mask's length.
    /// </summary>
    internal static double[] Squared(bool[] mask, Vector2I size, bool seedValue, double[] result, CancellationToken token = default)
    {
        Validate(mask, size, result.Length);
        token.ThrowIfCancellationRequested();
        for (int i = 0; i < mask.Length; i++)
            result[i] = mask[i] == seedValue ? 0 : double.PositiveInfinity;

        int capacity = Math.Max(size.X, size.Y);
        var input = new double[capacity];
        var output = new double[capacity];
        var sites = new int[capacity];
        var breaks = new double[capacity + 1];
        for (int y = 0; y < size.Y; y++)
        {
            token.ThrowIfCancellationRequested();
            Array.Copy(result, y * size.X, input, 0, size.X);
            Transform(input, output, sites, breaks, size.X);
            Array.Copy(output, 0, result, y * size.X, size.X);
        }
        for (int x = 0; x < size.X; x++)
        {
            token.ThrowIfCancellationRequested();
            for (int y = 0; y < size.Y; y++) input[y] = result[y * size.X + x];
            Transform(input, output, sites, breaks, size.Y);
            for (int y = 0; y < size.Y; y++) result[y * size.X + x] = output[y];
        }
        return result;
    }

    /// <summary>
    /// The same transform, computed in double per row and column but stored in
    /// a float field. Every intermediate and final value is a whole number - a
    /// squared step count - so the stored field is EXACT wherever the squared
    /// distance is below 2^24 (a straight run of 4096 samples) and rounded but
    /// still monotone beyond it. That is enough for a band test against a
    /// threshold far below that bound, which is what the shoreline stage asks,
    /// through a shared float scratch rather than a private double field the
    /// size of the map. The renderer's signed coast field keeps the double form
    /// above because it draws the value itself.
    /// </summary>
    internal static void Squared(bool[] mask, Vector2I size, bool seedValue, float[] result, CancellationToken token = default)
    {
        Validate(mask, size, result.Length);
        token.ThrowIfCancellationRequested();
        for (int i = 0; i < mask.Length; i++)
            result[i] = mask[i] == seedValue ? 0f : float.PositiveInfinity;

        int capacity = Math.Max(size.X, size.Y);
        var input = new double[capacity];
        var output = new double[capacity];
        var sites = new int[capacity];
        var breaks = new double[capacity + 1];
        for (int y = 0; y < size.Y; y++)
        {
            token.ThrowIfCancellationRequested();
            int row = y * size.X;
            for (int x = 0; x < size.X; x++) input[x] = result[row + x];
            Transform(input, output, sites, breaks, size.X);
            for (int x = 0; x < size.X; x++) result[row + x] = (float)output[x];
        }
        for (int x = 0; x < size.X; x++)
        {
            token.ThrowIfCancellationRequested();
            for (int y = 0; y < size.Y; y++) input[y] = result[y * size.X + x];
            Transform(input, output, sites, breaks, size.Y);
            for (int y = 0; y < size.Y; y++) result[y * size.X + x] = (float)output[y];
        }
    }

    private static void Validate(bool[] mask, Vector2I size, int resultLength)
    {
        if (size.X < 1 || size.Y < 1 || (long)size.X * size.Y != mask.Length)
            throw new ArgumentException("Distance mask dimensions do not match its length.");
        if (resultLength < mask.Length)
            throw new ArgumentException("Distance field is shorter than its mask.");
    }

    private static void Transform(double[] input, double[] output, int[] sites, double[] breaks, int length)
    {
        int last = -1;
        for (int q = 0; q < length; q++)
        {
            if (double.IsPositiveInfinity(input[q])) continue;
            double cross = double.NegativeInfinity;
            while (last >= 0)
            {
                int previous = sites[last];
                cross = (input[q] - input[previous]) / (2.0 * (q - previous))
                    + (q + previous) * 0.5;
                if (cross > breaks[last]) break;
                last--;
            }
            sites[++last] = q;
            breaks[last] = last == 0 ? double.NegativeInfinity : cross;
            breaks[last + 1] = double.PositiveInfinity;
        }
        if (last < 0)
        {
            Array.Fill(output, double.PositiveInfinity, 0, length);
            return;
        }
        int segment = 0;
        for (int q = 0; q < length; q++)
        {
            while (breaks[segment + 1] < q) segment++;
            double delta = q - sites[segment];
            output[q] = delta * delta + input[sites[segment]];
        }
    }

    /// <summary>
    /// Positive water, negative land. The half-sample correction locates a
    /// straight boundary between sample centres. Curves retain sampling error.
    /// An entirely uniform mask saturates at the map diagonal, without NaN/Inf.
    /// </summary>
    internal static float[] Signed(bool[] water, Vector2I size, int samplesPerCell, CancellationToken token = default)
    {
        if (samplesPerCell < 1) throw new ArgumentOutOfRangeException(nameof(samplesPerCell));
        double[] toWater = Squared(water, size, true, token);
        double[] toLand = Squared(water, size, false, token);
        var result = new float[water.Length];
        double maximum = Math.Sqrt((double)size.X * size.X + (double)size.Y * size.Y);
        for (int i = 0; i < result.Length; i++)
        {
            if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
            double distance = Math.Sqrt(water[i] ? toLand[i] : toWater[i]);
            distance = double.IsPositiveInfinity(distance) ? maximum : Math.Max(0, distance - 0.5);
            result[i] = (float)(distance / samplesPerCell) * (water[i] ? 1 : -1);
        }
        return result;
    }
}
