using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Plays the game's sound effects. Clips, volumes and pitch spread come from the
    /// <see cref="SoundBank"/> asset; an event without a clip falls back to the synthesised
    /// <see cref="ProceduralSfx"/>. Events arrive through <see cref="SfxHub"/> (UI, pieces) or from
    /// <see cref="GameManager"/>, which forwards the placement results, so neither the board nor
    /// the figures have to know that sound exists.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        /// <summary>Overall level of the effects, on top of each event's own volume.</summary>
        public const float SfxVolume = 0.8f;

        private const int PoolSize = 6;

        /// <summary>The same sound is not started again within this time, so a burst cannot smear into noise.</summary>
        private const float SpamGuardSeconds = 0.05f;

        private const float ComboPitchStep = 0.07f;
        private const float MaxComboPitch = 1.5f;
        private const float BoardClearDelay = 0.14f;

        private static AudioManager instance;
        private static bool suspended;

        /// <summary>
        /// False until the first tap or key press: a browser keeps its audio context locked until then, and
        /// a sound started earlier would only pile up and burst out when the context wakes.
        /// </summary>
        private static bool unlocked;

        private readonly List<AudioSource> pool = new List<AudioSource>(PoolSize);
        private readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
        private readonly Dictionary<SfxId, AudioClip> fallbacks = new Dictionary<SfxId, AudioClip>();
        private int nextSource;

        /// <summary>The sound effects switch; the music has its own, see <see cref="SoundSettings"/>.</summary>
        private static bool IsMuted => SoundSettings.SfxMuted;

        /// <summary>
        /// Silences the effects at once: an ad is open, the SDK holds the game paused or the tab is hidden
        /// (Yandex 1.3 / 4.7). Driven by the platform layer, like <see cref="MusicManager.SetSuspended"/>.
        /// </summary>
        public static void SetSuspended(bool value)
        {
            if (suspended == value)
            {
                return;
            }

            suspended = value;
            if (value && instance != null)
            {
                instance.StopAll();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            suspended = false;
            unlocked = false;
        }

        private void Awake()
        {
            instance = this;
            EnsureSources();
        }

        private void Update()
        {
            if (!unlocked)
            {
                unlocked = Input.GetMouseButtonDown(0) || Input.touchCount > 0 || Input.anyKeyDown;
            }
        }

        private void OnEnable()
        {
            SoundSettings.Changed += HandleSettingsChanged;
            SfxHub.Requested += HandleRequested;
        }

        private void OnDisable()
        {
            SoundSettings.Changed -= HandleSettingsChanged;
            SfxHub.Requested -= HandleRequested;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>Click confirming that a figure has landed on the board.</summary>
        public void PlayPlacement() => Play(SfxId.PiecePlace);

        /// <summary>
        /// One line plays the single-line sound, two or more the multi-line one. Every step of a
        /// running <paramref name="combo"/> lifts the pitch (+7% per step, up to 1.5), and a combo of two
        /// or more adds the combo sound on top.
        /// </summary>
        public void PlayLineClear(int lines, int combo = 1)
        {
            if (lines <= 0)
            {
                return;
            }

            float pitch = Mathf.Min(1f + ComboPitchStep * Mathf.Max(0, combo - 1), MaxComboPitch);
            Play(lines >= 2 ? SfxId.LineClearMulti : SfxId.LineClear, pitch);

            if (combo >= 2)
            {
                Play(SfxId.Combo, pitch);
            }
        }

        /// <summary>Follows the line sound a moment later so the two do not smear.</summary>
        public void PlayBoardClear() => Play(SfxId.BoardClear, 1f, BoardClearDelay);

        /// <summary>Plays one event with a pitch multiplier and an optional start delay.</summary>
        public void Play(SfxId id, float pitch = 1f, float delay = 0f)
        {
            if (IsMuted || suspended || !unlocked || !EnsureSources())
            {
                return;
            }

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < SpamGuardSeconds)
            {
                return;
            }

            SoundEntry entry = SoundBank.Load().Get(id);
            AudioClip clip = entry != null && entry.Clip != null ? entry.Clip : GetFallback(id);
            if (clip == null)
            {
                return;
            }

            lastPlayed[id] = now;

            float spread = entry != null ? entry.PitchSpread : 0f;
            float volume = (entry != null ? entry.Volume : 1f) * SfxVolume;
            float randomPitch = spread > 0f ? Random.Range(1f - spread, 1f + spread) : 1f;

            AudioSource source = NextSource();
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch * randomPitch;
            if (delay > 0f)
            {
                source.PlayDelayed(delay);
            }
            else
            {
                source.Play();
            }

            if (IsJingle(id))
            {
                MusicManager.Duck(clip.length / Mathf.Max(0.1f, source.pitch));
            }
        }

        private void HandleRequested(SfxId id, float pitch, float delay) => Play(id, pitch, delay);

        private void HandleSettingsChanged()
        {
            if (IsMuted)
            {
                StopAll();
            }
        }

        private void StopAll()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    pool[i].Stop();
                }
            }
        }

        private static bool IsJingle(SfxId id)
        {
            return id == SfxId.JingleNewRecord || id == SfxId.JingleGameOver || id == SfxId.JingleWin;
        }

        /// <summary>Round-robin over the pool, preferring an idle source so nothing is cut off needlessly.</summary>
        private AudioSource NextSource()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                int index = (nextSource + i) % pool.Count;
                if (!pool[index].isPlaying)
                {
                    nextSource = (index + 1) % pool.Count;
                    return pool[index];
                }
            }

            AudioSource oldest = pool[nextSource];
            nextSource = (nextSource + 1) % pool.Count;
            return oldest;
        }

        /// <summary>Synthesised stand-in used while an event has no clip assigned in the bank.</summary>
        private AudioClip GetFallback(SfxId id)
        {
            if (fallbacks.TryGetValue(id, out AudioClip clip))
            {
                return clip;
            }

            switch (id)
            {
                case SfxId.LineClear:
                case SfxId.LineClearMulti:
                case SfxId.Combo:
                case SfxId.BoosterUse:
                case SfxId.Coin:
                    clip = ProceduralSfx.CreateSparkle();
                    break;
                case SfxId.BoardClear:
                case SfxId.JingleNewRecord:
                case SfxId.JingleGameOver:
                case SfxId.JingleWin:
                    clip = ProceduralSfx.CreateSparkle(6);
                    break;
                default:
                    clip = ProceduralSfx.CreateClick();
                    break;
            }

            fallbacks[id] = clip;
            return clip;
        }

        /// <summary>Creates the pooled sources on first use.</summary>
        private bool EnsureSources()
        {
            if (pool.Count >= PoolSize)
            {
                return true;
            }

            if (!Application.isPlaying)
            {
                return false;
            }

            while (pool.Count < PoolSize)
            {
                pool.Add(CreateSource($"Sfx_{pool.Count}"));
            }

            return true;
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject($"Audio_{name}");
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            // ignoreListenerPause stays off: an ad or an SDK pause sets AudioListener.pause,
            // and Yandex 1.3 / 4.7 require the game to fall silent behind it.
            return source;
        }
    }
}
