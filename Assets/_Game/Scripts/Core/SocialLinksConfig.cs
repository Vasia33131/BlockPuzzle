using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// The "Our games" links shown in the settings panel. One asset in
    /// <c>Resources/SocialLinksConfig</c>; an empty URL hides its button, and
    /// <see cref="enabled"/> off hides the whole block.
    /// </summary>
    [CreateAssetMenu(fileName = "SocialLinksConfig", menuName = "BlockPuzzle/Social Links Config")]
    public sealed class SocialLinksConfig : ScriptableObject
    {
        public const string ResourcePath = "SocialLinksConfig";

        [Tooltip("Master switch for the whole \"Our games\" block.")]
        public bool enabled = true;

        [Tooltip("Full https:// address of the Telegram channel. Empty hides the button.")]
        public string telegramUrl = string.Empty;

        [Tooltip("Full https:// address of the YouTube channel. Empty hides the button.")]
        public string youtubeUrl = string.Empty;
    }
}
