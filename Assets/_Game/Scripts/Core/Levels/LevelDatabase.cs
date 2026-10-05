using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Levels
{
    /// <summary>Ordered list of every campaign level. Lives in Resources so the game can load it by path.</summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Block Puzzle/Level Database")]
    public sealed class LevelDatabase : ScriptableObject
    {
        public const string ResourcePath = "Levels/LevelDatabase";
        public const int LevelCount = 100;

        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public int Count => levels.Count;

        /// <summary>Level by its 1-based number, or null when there is none.</summary>
        public LevelDefinition GetByNumber(int number)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] != null && levels[i].Number == number)
                {
                    return levels[i];
                }
            }

            return null;
        }

        /// <summary>Loads the shipped database from Resources. Null when it has not been generated yet.</summary>
        public static LevelDatabase Load() => Resources.Load<LevelDatabase>(ResourcePath);

        /// <summary>Authoring entry point: replaces the list, sorted by level number.</summary>
        public void SetLevels(IEnumerable<LevelDefinition> source)
        {
            levels = new List<LevelDefinition>(source);
            levels.RemoveAll(level => level == null);
            levels.Sort((a, b) => a.Number.CompareTo(b.Number));
        }
    }
}
