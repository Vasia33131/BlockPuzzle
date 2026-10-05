namespace BlockPuzzle.Levels
{
    /// <summary>Content of one prefilled cell of a level. Every type except Empty is an ordinary occupied block.</summary>
    public enum LevelCellType
    {
        Empty = 0,

        /// <summary>Plain block.</summary>
        Block = 1,

        /// <summary>Block that hides a crystal, collected when the line through it is cleared.</summary>
        Crystal = 2,

        /// <summary>Block flagged as a goal cell for <see cref="LevelGoalType.ClearMarked"/>.</summary>
        Marked = 3
    }
}
