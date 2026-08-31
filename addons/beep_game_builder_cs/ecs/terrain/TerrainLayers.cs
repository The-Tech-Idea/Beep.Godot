namespace Beep.ECS
{
    /// <summary>
    /// The layer stack every terrain view draws into, defined ONCE.
    ///
    /// A map is the same world however it is drawn, so the order things stack in
    /// - seabed, sea, ground, hills, mountains, summits, and the props standing
    /// on each - belongs to the terrain, not to a renderer. It used to live
    /// inside the isometric renderer: the feature renderer reached into that
    /// component for it, and the tile renderer could not reach it at all and
    /// invented its own scheme from a base z index. Three views, three answers
    /// to one question, and only one of them could be corrected at a time.
    ///
    /// THE ORDER, bottom to top:
    ///
    ///     floor             a filled base, or a whole map composited at once
    ///     seabed steps      the bed under see-through water, deepest first
    ///     sea               the water surface
    ///     ground            flat land
    ///     hills             one step up
    ///     mountains         the flanks of a range
    ///     summits           its crest
    ///     props             by the level they stand on
    ///     markers           icons that are not part of the world
    ///
    /// EVERY TERRAIN LEVEL OWNS AN EVEN Z, leaving the odd one below it for the
    /// layer that draws that level and the even slot itself for anything drawn
    /// over it at the same level - a detail pass, a decal.
    ///
    /// Props do NOT interleave with the terrain levels. All terrain draws first
    /// and props follow in level order, above every level; see ZForProps for
    /// why. An earlier design interleaved them and this comment described it
    /// long after ZForProps stopped doing it, which is worse than no comment:
    /// the next reader implements the scheme the documentation states.
    ///
    /// A renderer decides how to REALISE a level - the isometric view stacks
    /// blocks, the tile view draws a plane, the painted view composites in one
    /// pass - but none of them decides what the levels are or which order they
    /// come in.
    /// </summary>
    public static class TerrainLayers
    {
        /// <summary>The water surface.</summary>
        public const int Sea = 0;

        /// <summary>Flat land.</summary>
        public const int Ground = 1;

        /// <summary>Hills: one step above the ground.</summary>
        public const int Hills = 2;

        /// <summary>The flanks of a mountain range.</summary>
        public const int Mountains = 3;

        /// <summary>The crest of a range, deep inside a massif.</summary>
        public const int Summits = 4;

        /// <summary>How many levels the stack has, sea included.</summary>
        public const int Count = 5;

        /// <summary>The lowest land level. Everything below it is water.</summary>
        public const int FirstLand = Ground;

        /// <summary>Z index of a terrain level.</summary>
        public static int ZFor(int level) => level * 2;

        /// <summary>
        /// Z index of a seabed step, counted downward from the sea: step 0 sits
        /// just under the surface and each one further out draws behind the last.
        /// </summary>
        public static int ZForSeabed(int step) => ZFor(Sea) - 2 - step;

        /// <summary>
        /// Z index for the props standing on a level. ALL terrain draws first,
        /// then props in level order.
        ///
        /// Interleaving them - a level's props immediately above that level's
        /// terrain - is wrong in a way that is easy to miss. Higher terrain is
        /// not always FURTHER from the camera: a hill behind a tree still draws
        /// after it, so the hill was cutting the tree off at the trunk.
        /// </summary>
        public static int ZForProps(int level) => (Count * 2) + level;

        /// <summary>
        /// The bottom of the world: a filled base beneath the tile layers, or
        /// the single blended surface the painted view draws in one pass.
        ///
        /// Below the seabed, because a view that composites its whole map into
        /// one quad is drawing the bed and the sea and the land together, and
        /// anything the stack puts under the water still has to come out over
        /// it.
        /// </summary>
        public static int ZForFloor() => ZForSeabed(Count) - 1;

        /// <summary>
        /// Icons and markers that are not part of the world: resource symbols,
        /// start positions. Above the props, because a tree must never hide the
        /// thing the player is meant to click.
        /// </summary>
        public static int ZForMarkers() => ZForProps(Count);

        /// <summary>
        /// Which level a tile belongs to.
        ///
        /// Water sits below the shore so a coast steps DOWN into the sea instead
        /// of meeting it flat, and relief raises the ground the generator marked
        /// hill or mountain. Hills and mountains are separate bands the
        /// generator works to place, so they get separate steps: giving them the
        /// same one made a peak the same two-block stack as a hillside, and the
        /// classification made no visible difference at all.
        /// </summary>
        public static int LevelFor(string terrain, int relief) => terrain switch
        {
            "deep_water" or "shallow_water" or "water" => Sea,
            _ => relief >= 2 ? Mountains : relief > 0 ? Hills : Ground,
        };

        /// <summary>
        /// Which level a terrain kind belongs to when there is no relief field
        /// to consult - the flat views, where a layer draws ONE kind and the
        /// kind is all there is to go on.
        ///
        /// Some kinds are their own relief: gravel is a hillside and rock is a
        /// mountain, whatever the relief map says. That knowledge lived in a
        /// private table inside the tile renderer, so a layer built anywhere
        /// else - by a scene, by the transition component - had no way to reach
        /// it and fell back to a hand-written z. This is the single answer both
        /// now ask.
        /// </summary>
        public static int LevelForKind(string terrain) => terrain switch
        {
            "deep_water" or "shallow_water" or "water" => Sea,
            "gravel" => Hills,
            "rock" => Mountains,
            _ => Ground,
        };

        /// <summary>
        /// The z a flat view's layer for this terrain draws at: just under the
        /// level's own even slot, leaving that slot for anything drawn at the
        /// same level but over it.
        /// </summary>
        public static int ZForKind(string terrain) => ZFor(LevelForKind(terrain)) - 1;

        /// <summary>The name of a level, for diagnostics and guards.</summary>
        public static string NameFor(int level) => level switch
        {
            Sea => "sea",
            Ground => "ground",
            Hills => "hills",
            Mountains => "mountains",
            Summits => "summits",
            _ => "unknown",
        };
    }
}
