using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Background music: one track for the menu screens (menu, level map, shop) and one for a
    /// run, crossfaded between two sources as the game state changes.
    ///
    /// Playback waits for the first tap, because a browser keeps the audio context locked until
    /// the player interacts. It falls silent while an ad is open or the tab is hidden
    /// (<see cref="SetSuspended"/>, driven by the platform layer; Yandex 1.3 / 4.7), sits at half
    /// volume on the pause screen, and ducks under the end-of-run jingles.
    /// A track that is not marked seamless fades out shortly before its end and starts over.
    /// </summary>
    public sealed class MusicManager : MonoBehaviour
    {
        public const float MusicVolume = 0.35f;
        public const float CrossfadeSeconds = 1.2f;

        /// <summary>The menu track melts into the run track more slowly, so the start of a run stays calm.</summary>
        public const float MenuToGameFadeSeconds = 2.5f;

        /// <summary>A non-seamless track starts to fade this long before its end.</summary>
        public const float EndFadeSeconds = 1.5f;

        /// <summary>Share of the music volume kept while a jingle plays.</summary>
        public const float DuckLevel = 0.3f;

        /// <summary>Share of the music volume kept on the pause screen.</summary>
        public const float PausedLevel = 0.5f;

        /// <summary>How long the music stays ducked after an end-of-run jingle starts.</summary>
        private const float JingleHoldSeconds = 2.5f;

        private const float DuckDownSpeed = 1f / 0.15f;
        private const float DuckUpSpeed = 1f / 1f;
        private const float LevelSpeed = 1f / 0.3f;

        private enum Track
        {
            None,
            Menu,
            Game
        }

        private sealed class Voice
        {
            public AudioSource Source;
            public Track Track;
            public bool Seamless;
            public float Fade;
            public float FadeTarget;
        }

        private static MusicManager instance;
        private static bool shopOpen;

        private Voice[] voices;
        private MusicCatalog catalog;
        private bool catalogMissing;

        private GameManager bound;
        private ScoreManager boundScore;
        private GameState state = GameState.Boot;

        private Track current;
        private float fadeSeconds = CrossfadeSeconds;
        private bool unlocked;
        private bool suspended;

        private float level = 1f;
        private float duck = 1f;
        private float duckTimer;

        public static MusicManager Instance => instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            shopOpen = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static MusicManager Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var go = new GameObject(nameof(MusicManager));
            DontDestroyOnLoad(go);
            return go.AddComponent<MusicManager>();
        }

        /// <summary>The shop plays the menu track even when it is opened over a run.</summary>
        public static void SetShopOpen(bool open) => shopOpen = open;

        /// <summary>Pauses or resumes the music at once: an ad is open, or the tab lost the focus.</summary>
        public static void SetSuspended(bool value)
        {
            if (instance != null)
            {
                instance.ApplySuspended(value);
            }
        }

        /// <summary>Lowers the music for <paramref name="seconds"/>, then lets it swell back.</summary>
        public static void Duck(float seconds)
        {
            if (instance != null)
            {
                instance.duckTimer = Mathf.Max(instance.duckTimer, seconds);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            voices = new[] { CreateVoice("MusicA"), CreateVoice("MusicB") };
        }

        private void OnDestroy()
        {
            Unbind();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            EnsureBound();

            if (!unlocked)
            {
                // Nothing is started before the first tap: the browser keeps audio locked until then.
                unlocked = Input.GetMouseButtonDown(0) || Input.touchCount > 0 || Input.anyKeyDown;
                if (!unlocked)
                {
                    return;
                }
            }

            float dt = Time.unscaledDeltaTime;
            SetTrack(ResolveTrack());
            UpdateLevels(dt);

            float master = MusicVolume * level * duck;
            foreach (Voice voice in voices)
            {
                UpdateVoice(voice, master, dt);
            }
        }

        // ---------------------------------------------------------------- game state

        /// <summary>One track for the whole game: the menu one. The run track in the catalog is kept but not used.</summary>
        private Track ResolveTrack() => Track.Menu;

        private void EnsureBound()
        {
            GameManager manager = GameManager.Instance;
            if (manager == bound)
            {
                return;
            }

            Unbind();
            bound = manager;
            if (bound == null)
            {
                state = GameState.Boot;
                return;
            }

            state = bound.State;
            bound.StateChanged += HandleStateChanged;
            boundScore = bound.Score;
            if (boundScore != null)
            {
                boundScore.RecordBroken += HandleRecordBroken;
            }
        }

        private void Unbind()
        {
            if (bound != null)
            {
                bound.StateChanged -= HandleStateChanged;
            }

            if (boundScore != null)
            {
                boundScore.RecordBroken -= HandleRecordBroken;
            }

            bound = null;
            boundScore = null;
        }

        private void HandleStateChanged(GameState next)
        {
            state = next;
            if (next == GameState.GameOver || next == GameState.LevelWon || next == GameState.LevelFailed)
            {
                duckTimer = Mathf.Max(duckTimer, JingleHoldSeconds);
            }
        }

        private void HandleRecordBroken(int score) => duckTimer = Mathf.Max(duckTimer, JingleHoldSeconds);

        // ---------------------------------------------------------------- mixing

        private void UpdateLevels(float dt)
        {
            float levelTarget = SoundSettings.MusicMuted ? 0f : (state == GameState.Paused ? PausedLevel : 1f);
            level = Mathf.MoveTowards(level, levelTarget, LevelSpeed * dt);

            float duckTarget = duckTimer > 0f ? DuckLevel : 1f;
            duckTimer = Mathf.Max(0f, duckTimer - dt);
            float speed = duck > duckTarget ? DuckDownSpeed : DuckUpSpeed;
            duck = Mathf.MoveTowards(duck, duckTarget, speed * dt);
        }

        private void UpdateVoice(Voice voice, float master, float dt)
        {
            AudioSource source = voice.Source;
            if (voice.Track == Track.None)
            {
                return;
            }

            voice.Fade = Mathf.MoveTowards(voice.Fade, voice.FadeTarget, dt / fadeSeconds);
            if (voice.FadeTarget <= 0f && voice.Fade <= 0f)
            {
                source.Stop();
                voice.Track = Track.None;
                return;
            }

            float edge = 1f;
            if (!voice.Seamless && source.clip != null)
            {
                float length = source.clip.length;
                if (!suspended && !AudioListener.pause && voice.FadeTarget > 0f
                    && source.clip.loadState == AudioDataLoadState.Loaded
                    && (!source.isPlaying || source.time >= length - 0.05f))
                {
                    // The track ended: begin again, rising from silence.
                    source.Stop();
                    source.time = 0f;
                    source.Play();
                    voice.Fade = 0f;
                }

                edge = Mathf.Clamp01((length - source.time) / EndFadeSeconds);
            }

            // Eased, so both tracks bend smoothly instead of ramping in a straight line.
            source.volume = master * Mathf.SmoothStep(0f, 1f, voice.Fade) * edge;
        }

        // ---------------------------------------------------------------- tracks

        private void SetTrack(Track next)
        {
            if (next == current)
            {
                return;
            }

            AudioClip clip = ClipFor(next, out bool seamless);
            if (next != Track.None && clip == null)
            {
                return;
            }

            fadeSeconds = current == Track.Menu && next == Track.Game ? MenuToGameFadeSeconds : CrossfadeSeconds;
            current = next;
            foreach (Voice voice in voices)
            {
                voice.FadeTarget = 0f;
            }

            if (next == Track.None)
            {
                return;
            }

            Voice free = PickFreeVoice();
            free.Track = next;
            free.Seamless = seamless;
            free.Fade = 0f;
            free.FadeTarget = 1f;

            AudioSource source = free.Source;
            source.Stop();
            source.clip = clip;
            source.loop = seamless;
            source.time = 0f;
            source.volume = 0f;
            source.Play();
            if (suspended)
            {
                source.Pause();
            }
        }

        /// <summary>An idle source, or else the one that is closest to silence.</summary>
        private Voice PickFreeVoice()
        {
            Voice best = voices[0];
            foreach (Voice voice in voices)
            {
                if (voice.Track == Track.None)
                {
                    return voice;
                }

                if (voice.Fade < best.Fade)
                {
                    best = voice;
                }
            }

            return best;
        }

        private AudioClip ClipFor(Track track, out bool seamless)
        {
            seamless = false;
            if (track == Track.None)
            {
                return null;
            }

            if (catalog == null && !catalogMissing)
            {
                catalog = Resources.Load<MusicCatalog>(MusicCatalog.ResourcePath);
                catalogMissing = catalog == null;
                if (catalogMissing)
                {
                    Debug.LogWarning("MusicManager: Resources/MusicCatalog is missing, there will be no music.");
                }
            }

            if (catalog == null)
            {
                return null;
            }

            if (track == Track.Menu)
            {
                seamless = catalog.menuSeamless;
                return catalog.menu;
            }

            seamless = catalog.gameSeamless;
            return catalog.game;
        }

        private void ApplySuspended(bool value)
        {
            if (suspended == value)
            {
                return;
            }

            suspended = value;
            if (voices == null)
            {
                return;
            }

            foreach (Voice voice in voices)
            {
                if (voice.Track == Track.None)
                {
                    continue;
                }

                if (suspended)
                {
                    voice.Source.Pause();
                }
                else
                {
                    voice.Source.UnPause();
                }
            }
        }

        private Voice CreateVoice(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            // ignoreListenerPause stays off: an ad or an SDK pause sets AudioListener.pause,
            // and Yandex 1.3 / 4.7 require the game to fall silent behind it.
            return new Voice { Source = source };
        }
    }
}
