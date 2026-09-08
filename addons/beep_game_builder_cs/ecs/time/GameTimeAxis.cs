namespace Beep.ECS
{
    /// <summary>
    /// How a game measures time. Declared once, in GameInfo, and read once, by
    /// GameApp when it configures the clock.
    ///
    /// This is a DECLARATION, never an inference. The previous design inferred
    /// the axis from whether a TurnManager autoload happened to be in the tree,
    /// so every durational component had to ask the question for itself and
    /// branch on the answer - and a genre that declared turns but shipped no
    /// driver (strategy did exactly that) froze every duration in the game with
    /// nothing to report the mistake.
    /// </summary>
    public enum GameTimeAxis
    {
        /// <summary>
        /// The clock advances itself, once per frame, scaled by GameClock.Scale.
        /// One beat is one second of game time. RTS, action, farming.
        /// </summary>
        Realtime,

        /// <summary>
        /// The clock advances only when something calls GameClock.EndTurn().
        /// One beat is one turn. Strategy, card games.
        /// </summary>
        Turns
    }
}
