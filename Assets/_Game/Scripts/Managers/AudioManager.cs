using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Plays the game's sound effects. It is driven entirely by <see cref="GameManager"/>,
    /// which forwards the placement events to it, so neither the board nor the figures
    /// have to know that sound exists.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public const string MutedKey = "BlockPuzzle.Muted";

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float placeVolume = 0.5f;

        [Header("Line clear")]
        [Tooltip("Volume of a single cleared line.")]
        [SerializeField, Range(0f, 1f)] private float clearVolume = 0.55f;

        [Tooltip("Added to the volume for every line cleared beyond the first.")]
        [SerializeField, Range(0f, 0.5f)] private float clearVolumePerExtraLine = 0.15f;

        [Tooltip("Pitch added for every line cleared beyond the first.")]
        [SerializeField, Range(0f, 0.3f)] private float clearPitchPerExtraLine = 0.06f;

        private AudioSource placeSource;
        private AudioSource clearSource;
        private AudioClip placeClip;
        private AudioClip clearClip;

        public bool IsMuted { get; private set; }

        private void Awake()
        {
            IsMuted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            EnsureSources();
        }

        /// <summary>Short click confirming that a figure has landed on the board.</summary>
        public void PlayPlacement()
        {
            if (IsMuted || !EnsureSources())
            {
                return;
            }

            placeSource.pitch = Random.Range(0.96f, 1.05f);
            placeSource.PlayOneShot(placeClip, placeVolume * masterVolume);
        }

        /// <summary>
        /// Sparkle for a cleared line, louder and higher the more lines went at once, so a
        /// double or a triple is audible as an achievement rather than as a repeat.
        /// </summary>
        public void PlayLineClear(int lines)
        {
            if (lines <= 0 || IsMuted || !EnsureSources())
            {
                return;
            }

            int extra = lines - 1;
            float volume = Mathf.Clamp01(clearVolume + extra * clearVolumePerExtraLine) * masterVolume;

            clearSource.pitch = 1f + Mathf.Min(extra, 3) * clearPitchPerExtraLine;
            clearSource.PlayOneShot(clearClip, volume);
        }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();

            if (muted)
            {
                placeSource?.Stop();
                clearSource?.Stop();
            }
        }

        /// <summary>
        /// Creates the two clips and the two sources on first use. The sources are kept apart
        /// so that re-pitching a line clear never bends a click that is still ringing.
        /// </summary>
        private bool EnsureSources()
        {
            if (placeSource != null && clearSource != null)
            {
                return true;
            }

            if (!Application.isPlaying)
            {
                return false;
            }

            placeClip = placeClip != null ? placeClip : ProceduralSfx.CreateClick();
            clearClip = clearClip != null ? clearClip : ProceduralSfx.CreateSparkle();

            placeSource = placeSource != null ? placeSource : CreateSource("Place");
            clearSource = clearSource != null ? clearSource : CreateSource("LineClear");
            return true;
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject($"Audio_{name}");
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }
    }
}
