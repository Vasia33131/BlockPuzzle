namespace BlockPuzzle.Levels
{
    /// <summary>What the player has to achieve to finish a level.</summary>
    public enum LevelGoalType
    {
        /// <summary>Reach N points, within the move limit when there is one.</summary>
        Score = 0,

        /// <summary>Collect N crystals. A crystal is collected when its row or column is cleared.</summary>
        Gems = 1,

        /// <summary>Clear every marked cell. The target is the number of marked cells on the board.</summary>
        ClearMarked = 2,

        /// <summary>Clear N lines (rows and columns count alike), within the move limit when there is one.</summary>
        Lines = 3
    }
}
