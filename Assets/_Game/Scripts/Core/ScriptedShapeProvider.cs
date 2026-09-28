using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Hands out a fixed sequence of figures, one per <see cref="Next"/>. A <c>null</c>
    /// entry leaves that tray slot empty. Once the script runs out it keeps returning
    /// <c>null</c>, which tells the spawner to go back to its own dealer.
    /// </summary>
    public sealed class ScriptedShapeProvider : IShapeProvider
    {
        private readonly Queue<BlockShape> script;

        public ScriptedShapeProvider(IEnumerable<BlockShape> shapes)
        {
            script = new Queue<BlockShape>(shapes ?? new BlockShape[0]);
        }

        public bool IsExhausted => script.Count == 0;

        public BlockShape Next() => script.Count > 0 ? script.Dequeue() : null;
    }
}
