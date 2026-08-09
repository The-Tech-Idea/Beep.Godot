using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Seeded 2D level generator — produces a tile grid any genre can consume. Static and
    /// deterministic: the same seed always builds the same level, which is what makes a seeded
    /// roguelike run reproducible and a generated level saveable (store the seed, not the tiles).
    ///
    /// Two algorithms, both genre-standard:
    ///   • Rooms — non-overlapping rectangles joined by L-corridors (top-down dungeon, RPG cave).
    ///   • DrunkardWalk — a random-walking carver (organic cave for a platformer or digging game).
    ///
    /// The result is a plain <see cref="Tile"/> grid; rendering (TileMap, sprites, mesh) is the
    /// caller's job, so the generator stays engine-agnostic and unit-testable.
    /// </summary>
    public static class ProceduralLevelGenerator
    {
        public enum Tile : byte { Wall = 0, Floor = 1 }

        /// <summary>A rectangular room in grid cells.</summary>
        public readonly record struct Room(Rect2I Rect)
        {
            public Vector2I Center => Rect.Position + Rect.Size / 2;
        }

        /// <summary>The generated level: the tile grid plus the rooms that were placed (room-gen
        /// only — walk-gen has no discrete rooms). Rooms are handed back so the caller can spawn
        /// entities/props in them without re-deriving where the floor is.</summary>
        public sealed class Result
        {
            public int Width { get; }
            public int Height { get; }
            public Tile[,] Tiles { get; }
            public IReadOnlyList<Room> Rooms { get; }

            public Result(int width, int height, Tile[,] tiles, IReadOnlyList<Room> rooms)
            {
                Width = width; Height = height; Tiles = tiles; Rooms = rooms;
            }

            public Tile At(int x, int y) =>
                x < 0 || y < 0 || x >= Width || y >= Height ? Tile.Wall : Tiles[x, y];
        }

        /// <summary>
        /// Rooms-and-corridors dungeon. Tries to place <paramref name="maxRooms"/> non-overlapping
        /// rooms of random size, then connects each new room's center to the previous one's with an
        /// L-shaped corridor. Deterministic under <paramref name="seed"/>.
        /// </summary>
        public static Result Rooms(int width, int height, ulong seed,
            int maxRooms = 12, int minRoomSize = 4, int maxRoomSize = 9)
        {
            var tiles = new Tile[width, height];   // defaults to Wall (0)
            var rng = new RandomNumberGenerator { Seed = seed };
            var rooms = new List<Room>();

            for (int attempt = 0; attempt < maxRooms * 4 && rooms.Count < maxRooms; attempt++)
            {
                int w = rng.RandiRange(minRoomSize, maxRoomSize);
                int h = rng.RandiRange(minRoomSize, maxRoomSize);
                // Inset by 1 so a room never touches the grid edge (keeps a wall border).
                int x = rng.RandiRange(1, Mathf.Max(1, width - w - 1));
                int y = rng.RandiRange(1, Mathf.Max(1, height - h - 1));
                var rect = new Rect2I(x, y, w, h);

                // Reject overlaps (1-cell gap keeps rooms from merging into an unreadable blob).
                bool overlaps = false;
                foreach (var existing in rooms)
                    if (existing.Rect.Grow(1).Intersects(rect)) { overlaps = true; break; }
                if (overlaps) continue;

                CarveRect(tiles, rect);
                // Connect to the previous room so the dungeon is one connected space.
                if (rooms.Count > 0)
                    CarveCorridor(tiles, rooms[^1].Center, rect.Position + rect.Size / 2, rng);
                rooms.Add(new Room(rect));
            }

            return new Result(width, height, tiles, rooms);
        }

        /// <summary>
        /// Drunkard's-walk cave. A carver starts at the grid center and random-walks, turning each
        /// visited cell to floor, until <paramref name="floorFraction"/> of the grid is carved.
        /// Organic, blobby caves. Deterministic under <paramref name="seed"/>.
        /// </summary>
        public static Result DrunkardWalk(int width, int height, ulong seed, float floorFraction = 0.4f)
        {
            var tiles = new Tile[width, height];
            var rng = new RandomNumberGenerator { Seed = seed };

            int targetFloor = (int)(width * height * Mathf.Clamp(floorFraction, 0.05f, 0.9f));
            int carved = 0;
            var pos = new Vector2I(width / 2, height / 2);

            // Bounded steps so a pathological walk can't spin forever without reaching the target.
            int maxSteps = width * height * 8;
            for (int step = 0; step < maxSteps && carved < targetFloor; step++)
            {
                if (tiles[pos.X, pos.Y] == Tile.Wall) { tiles[pos.X, pos.Y] = Tile.Floor; carved++; }

                // Cardinal step, clamped to a 1-cell wall border.
                pos += rng.RandiRange(0, 3) switch
                {
                    0 => Vector2I.Right,
                    1 => Vector2I.Left,
                    2 => Vector2I.Down,
                    _ => Vector2I.Up,
                };
                pos.X = Mathf.Clamp(pos.X, 1, width - 2);
                pos.Y = Mathf.Clamp(pos.Y, 1, height - 2);
            }

            return new Result(width, height, tiles, System.Array.Empty<Room>());
        }

        private static void CarveRect(Tile[,] tiles, Rect2I r)
        {
            for (int x = r.Position.X; x < r.Position.X + r.Size.X; x++)
                for (int y = r.Position.Y; y < r.Position.Y + r.Size.Y; y++)
                    tiles[x, y] = Tile.Floor;
        }

        /// <summary>L-shaped corridor between two centers — horizontal-then-vertical or the reverse,
        /// chosen at random so corridors don't all share one bias.</summary>
        private static void CarveCorridor(Tile[,] tiles, Vector2I a, Vector2I b, RandomNumberGenerator rng)
        {
            bool horizontalFirst = rng.RandiRange(0, 1) == 0;
            if (horizontalFirst) { CarveH(tiles, a.X, b.X, a.Y); CarveV(tiles, a.Y, b.Y, b.X); }
            else { CarveV(tiles, a.Y, b.Y, a.X); CarveH(tiles, a.X, b.X, b.Y); }
        }

        private static void CarveH(Tile[,] tiles, int x1, int x2, int y)
        {
            for (int x = Mathf.Min(x1, x2); x <= Mathf.Max(x1, x2); x++)
                if (x >= 0 && y >= 0 && x < tiles.GetLength(0) && y < tiles.GetLength(1))
                    tiles[x, y] = Tile.Floor;
        }

        private static void CarveV(Tile[,] tiles, int y1, int y2, int x)
        {
            for (int y = Mathf.Min(y1, y2); y <= Mathf.Max(y1, y2); y++)
                if (x >= 0 && y >= 0 && x < tiles.GetLength(0) && y < tiles.GetLength(1))
                    tiles[x, y] = Tile.Floor;
        }
    }
}
