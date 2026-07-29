namespace BlockPuzzle.Core
{
    /// <summary>
    /// Supplies the next figure to be offered to the player. Abstracting the source
    /// lets the spawner stay unaware of how figures are picked (random, weighted,
    /// scripted for a tutorial, replayed from a seed, ...).
    /// </summary>
    public interface IShapeProvider
    {
        BlockShape Next();
    }
}
