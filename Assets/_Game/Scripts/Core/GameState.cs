namespace BlockPuzzle.Core
{
    public enum GameState
    {
        Boot,
        Playing,
        Paused,
        GameOver,
        MainMenu,

        /// <summary>A level's goal was reached; the result screen is up. Endless Game Over never uses this.</summary>
        LevelWon,

        /// <summary>A level ended without reaching its goal (out of moves, or no room for the figures).</summary>
        LevelFailed
    }
}
