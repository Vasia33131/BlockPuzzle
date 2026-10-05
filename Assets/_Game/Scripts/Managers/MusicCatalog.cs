using UnityEngine;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// The background tracks, kept in Resources so <see cref="MusicManager"/> can find them
    /// without a scene reference. A track that does not loop cleanly is faded out near its
    /// end and started again; tick "seamless" only for a track that loops without a gap.
    /// </summary>
    [CreateAssetMenu(fileName = ResourcePath, menuName = "Block Puzzle/Music Catalog")]
    public sealed class MusicCatalog : ScriptableObject
    {
        public const string ResourcePath = "MusicCatalog";

        [Tooltip("Main menu, level map and shop.")]
        public AudioClip menu;
        public bool menuSeamless;

        [Tooltip("Endless runs and levels.")]
        public AudioClip game;
        public bool gameSeamless;
    }
}
