using Godot;
using System;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>Grid helpers shared by the generation stages.</summary>
    internal static class TerrainGeometry
    {
        /// <summary>Four-way neighbours, which is also the connectivity that defines a landmass.</summary>
        public static IEnumerable<int> Neighbours(int x, int y, int width, int height)
        {
            if (x > 0) yield return (y * width) + x - 1;
            if (x < width - 1) yield return (y * width) + x + 1;
            if (y > 0) yield return ((y - 1) * width) + x;
            if (y < height - 1) yield return ((y + 1) * width) + x;
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
        /// Number of four-connected components, for callers that want the count
        /// alone and are not on a hot path.
        /// </summary>
        public static int CountComponents(bool[] mask, int width, int height)
            => LabelComponents(
                mask, width, height, new int[mask.Length], new int[mask.Length], new List<int>());

        /// <summary>
        /// Multi-source BFS giving each cell its step distance to the nearest
        /// cell where <paramref name="source"/> is true. Cells that are sources
        /// get 0. Used for coast distance, which drives both elevation and the
        /// beach band.
        /// </summary>
        public static int[] DistanceTo(bool[] source, int width, int height)
        {
            var distance = new int[source.Length];
            var queue = new int[source.Length];
            int head = 0;
            int tail = 0;

            for (int index = 0; index < source.Length; index++)
            {
                if (source[index])
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
            return distance;
        }

        /// <summary>
        /// The value at the given percentile of the samples where
        /// <paramref name="mask"/> is true. Percentile thresholds are how a Civ
        /// style generator turns "20% of land is hills" into a concrete cutoff
        /// regardless of how the underlying noise happens to be distributed.
        /// </summary>
        public static float Percentile(float[] values, bool[] mask, float percentile)
        {
            var selected = new List<float>();
            for (int index = 0; index < values.Length; index++)
            {
                if (mask[index])
                    selected.Add(values[index]);
            }
            if (selected.Count == 0)
                return 0.0f;

            selected.Sort();
            int position = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp(percentile, 0.0f, 1.0f) * (selected.Count - 1)),
                0,
                selected.Count - 1);
            return selected[position];
        }

        public static float Normalized(float signedNoise) => (signedNoise + 1.0f) * 0.5f;

        public static float Smooth(float value) => value * value * (3.0f - (2.0f * value));

        /// <summary>
        /// Ridged transform: turns smooth fbm into sharp crests, which is what
        /// makes mountain ranges read as ranges rather than round blobs.
        /// </summary>
        public static float Ridged(float signedNoise) => 1.0f - Mathf.Abs(signedNoise);
    }
}
