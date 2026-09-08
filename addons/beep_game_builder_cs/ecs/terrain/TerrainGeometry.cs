using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Beep.ECS
{
    /// <summary>Grid helpers shared by the generation stages.</summary>
    internal static class TerrainGeometry
    {
        /// <summary>
        /// Four-way neighbours - the connectivity that defines a landmass - written
        /// into <paramref name="neighbours"/> in the order -x, +x, -y, +y; returns
        /// how many there are. Every breadth-first walk in the stages is
        /// order-sensitive, so this is the one order they all take.
        ///
        /// It replaced a yield iterator. An iterator allocates its state machine
        /// on every call, and the water and biome stages called it once per water
        /// sample: at Huge that was two-thirds of a million objects per stage.
        /// Callers stackalloc a four-int span once, outside their loops.
        /// </summary>
        public static int Neighbours4(int index, int width, int height, Span<int> neighbours)
        {
            int x = index % width;
            int y = index / width;
            int count = 0;
            if (x > 0) neighbours[count++] = index - 1;
            if (x < width - 1) neighbours[count++] = index + 1;
            if (y > 0) neighbours[count++] = index - width;
            if (y < height - 1) neighbours[count++] = index + width;
            return count;
        }

        /// <summary>
        /// Labels four-connected components of <paramref name="mask"/> in place:
        /// <paramref name="labels"/> gets the component index for a true cell and
        /// -1 for a false one, each component's size is appended to
        /// <paramref name="sizes"/>, and the component count is returned.
        ///
        /// The caller supplies the working buffers because the landmass
        /// bisection labels the whole field seventeen times over. Building a
        /// list per component there, and walking neighbours through an iterator,
        /// allocated millions of short-lived objects and was the single largest
        /// cost in generation.
        /// </summary>
        public static int LabelComponents(
            bool[] mask,
            int width,
            int height,
            int[] labels,
            int[] stack,
            List<int> sizes)
        {
            sizes.Clear();
            Array.Fill(labels, -1);

            int count = 0;
            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || labels[start] >= 0)
                    continue;

                // Each cell is labelled as it is pushed, so it enters the stack
                // exactly once and a buffer the size of the field always fits.
                int top = 0;
                stack[top++] = start;
                labels[start] = count;
                int size = 0;

                while (top > 0)
                {
                    int current = stack[--top];
                    size++;

                    int x = current % width;
                    int y = current / width;
                    if (x > 0) Push(current - 1);
                    if (x < width - 1) Push(current + 1);
                    if (y > 0) Push(current - width);
                    if (y < height - 1) Push(current + width);

                    void Push(int neighbour)
                    {
                        if (!mask[neighbour] || labels[neighbour] >= 0)
                            return;
                        labels[neighbour] = count;
                        stack[top++] = neighbour;
                    }
                }

                sizes.Add(size);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Number of four-connected components, over caller-supplied label and
        /// stack buffers of the mask's length.
        /// </summary>
        public static int CountComponents(bool[] mask, int width, int height, int[] labels, int[] stack)
            => LabelComponents(mask, width, height, labels, stack, new List<int>());

        /// <summary>
        /// Multi-source BFS writing into <paramref name="distance"/> each cell's
        /// step distance to the nearest cell where <paramref name="source"/>
        /// equals <paramref name="sourceValue"/>; those cells get 0. Used for
        /// coast distance, which drives both elevation and the beach band, and
        /// asked there as "distance to where land is false" so no negated copy
        /// of the land mask is built. <paramref name="queue"/> must hold the
        /// whole field: each cell is settled once, so it never overflows.
        /// </summary>
        public static void DistanceTo(bool[] source, bool sourceValue, int width, int height, int[] distance, int[] queue)
        {
            int head = 0;
            int tail = 0;

            for (int index = 0; index < source.Length; index++)
            {
                if (source[index] == sourceValue)
                {
                    distance[index] = 0;
                    queue[tail++] = index;
                }
                else
                {
                    distance[index] = int.MaxValue;
                }
            }

            // Breadth-first with unit steps, so the first distance a cell is
            // given is already its shortest. Settling each cell once is what
            // keeps the queue bounded by the field and lets it be a plain array.
            while (head < tail)
            {
                int current = queue[head++];
                int next = distance[current] + 1;

                int x = current % width;
                int y = current / width;
                if (x > 0) Visit(current - 1);
                if (x < width - 1) Visit(current + 1);
                if (y > 0) Visit(current - width);
                if (y < height - 1) Visit(current + width);

                void Visit(int neighbour)
                {
                    if (distance[neighbour] != int.MaxValue)
                        return;
                    distance[neighbour] = next;
                    queue[tail++] = neighbour;
                }
            }
        }

        /// <summary>
        /// Copies the values where <paramref name="mask"/> is true into
        /// <paramref name="destination"/>, sorts that run ascending and returns
        /// its length, ready for <see cref="RankedValue"/>. Percentile thresholds
        /// are how a Civ style generator turns "20% of land is hills" into a
        /// concrete cutoff regardless of how the underlying noise happens to be
        /// distributed. The relief and feature stages each take two cutoffs from
        /// one selection; each cutoff used to build and sort a list of its own.
        /// </summary>
        public static int SortedSelection(float[] values, bool[] mask, float[] destination)
        {
            int count = 0;
            for (int index = 0; index < values.Length; index++)
            {
                if (mask[index])
                    destination[count++] = values[index];
            }
            Array.Sort(destination, 0, count);
            return count;
        }

        /// <summary>
        /// The value at a percentile of an already-sorted run - the front of a
        /// SortedSelection, or a block the feature stage sorted itself. The two
        /// carried the same clamp-and-round in two files, kept equal by a smoke test
        /// rather than by being one function.
        /// </summary>
        public static float RankedValue(IReadOnlyList<float> sorted, int count, float percentile)
        {
            if (count == 0) return 0.0f;
            int position = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp(percentile, 0.0f, 1.0f) * (count - 1)), 0, count - 1);
            return sorted[position];
        }

        /// <summary>How many cells a mask selects.</summary>
        public static int CountTrue(bool[] values)
        {
            int count = 0;
            foreach (bool value in values)
            {
                if (value)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// The most-counted key, or the fallback when nothing was counted.
        ///
        /// Ties go to the first key counted, because every one of the five inline
        /// votes this replaces - tile reduction, the two landmass votes in the scale
        /// constraint, the coherence stage's two, the shoreline's re-vote - already
        /// compared with a strict greater-than over the same insertion-ordered
        /// dictionary. Same input, same winner.
        /// </summary>
        [return: NotNullIfNotNull(nameof(fallback))]
        public static string? MostCommon(Dictionary<string, int> counts, string? fallback)
        {
            string? best = fallback;
            int most = 0;
            foreach ((string kind, int count) in counts)
            {
                if (count > most)
                {
                    best = kind;
                    most = count;
                }
            }
            return best;
        }

        public static float Normalized(float signedNoise) => (signedNoise + 1.0f) * 0.5f;

        public static float Smooth(float value) => value * value * (3.0f - (2.0f * value));

        /// <summary>
        /// Ridged transform: turns smooth fbm into sharp crests, which is what
        /// makes mountain ranges read as ranges rather than round blobs.
        /// </summary>
        public static float Ridged(float signedNoise) => 1.0f - Mathf.Abs(signedNoise);

        /// <summary>
        /// Deterministic per-cell hash in [0, 1). One seed plus a cell decides
        /// the same value every time, which is what lets jitter/selection be
        /// reproduced from a seed instead of stored.
        ///
        /// This was copy-pasted, byte-for-byte, into eight separate files
        /// before being pulled here - the same Wang-style mix each time
        /// (multiply-XOR-shift-multiply-XOR-shift, masked to 24 bits), two of
        /// the eight even with the seed and cell arguments in a different
        /// order, which is itself the evidence they were pasted rather than
        /// shared.
        /// </summary>
        public static float Hash01(int x, int y, int seed)
        {
            uint value = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215.0f;
        }

        /// <summary>
        /// The salt for per-cell VARIANT choice: which of a terrain's interchangeable
        /// frames a cell shows. One value, so the isometric block view and the
        /// autotile view pick the same variant for the same cell of one map; they
        /// used two different hashes before and disagreed.
        ///
        /// The number is the FNV offset basis the block view had hard-coded into a
        /// private copy of this file's mix. Keeping it makes that view's output
        /// bit-identical to before: the consolidation moved where the mix lives,
        /// not what it draws.
        /// </summary>
        public const int VariantSalt = unchecked((int)2166136261u);

        /// <summary>
        /// The same mix as Hash01, kept as a raw int for callers that index by
        /// modulo rather than by a unit float. One private copy of this
        /// survived the Hash01 consolidation, in the mountain tile painter -
        /// and three more after that, in the block variant picker, the autotile
        /// alternative picker and the resource scatter's amount roll.
        /// </summary>
        public static int HashInt(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)seed;
                value = (value ^ (value >> 13)) * 1274126177u;
                value ^= value >> 16;
                return (int)value;
            }
        }
    }
}
