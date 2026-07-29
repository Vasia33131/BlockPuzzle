namespace BlockPuzzle.Grid
{
    /// <summary>
    /// Outcome of a single drop attempt, consumed by the score and game-over systems.
    /// </summary>
    public readonly struct PlacementResult
    {
        public static readonly PlacementResult Failed = new PlacementResult(false, 0, 0, 0);

        public PlacementResult(bool success, int blocksPlaced, int linesCleared, int cellsCleared)
        {
            Success = success;
            BlocksPlaced = blocksPlaced;
            LinesCleared = linesCleared;
            CellsCleared = cellsCleared;
        }

        public bool Success { get; }
        public int BlocksPlaced { get; }
        public int LinesCleared { get; }
        public int CellsCleared { get; }
    }
}
