using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The pause screen and the HUD button that opens it. Both live here because they are
    /// two halves of the same interaction, and keeping them together means the whole
    /// feature subscribes and unsubscribes in one place.
    ///
    /// Pausing freezes the time scale, so every animation on this panel runs on unscaled
    /// time — otherwise the screen would appear without ever fading in.
    /// </summary>
    public class PausePanel : MonoBehaviour
    {
        private const float ShowDuration = 0.24f;
        private const float HideDuration = 0.16f;
        private const float PauseCardHeight = 700f;
        private const string SoundOnLabel = "ЗВУК: ВКЛ";
        private const string SoundOffLabel = "ЗВУК: ВЫКЛ";

        [SerializeField] private GameManager gameManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button soundButton;

        private TMP_Text soundLabel;
        private AudioManager audioManager;

        private void Awake()
        {
            ResolveCard();
            ResolveSoundButton();
            HideLegacyShopButton();

            if (gameManager != null)
            {
                Bind(gameManager);
            }
        }

        public void Bind(
            GameManager manager,
            CanvasGroup group,
            RectTransform cardRect,
            Button pause,
            Button resume,
            Button restart,
            Button sound = null)
        {
            canvasGroup = group;
            card = cardRect;
            pauseButton = pause;
            resumeButton = resume;
            restartButton = restart;
            soundButton = sound != null ? sound : soundButton;
            Bind(manager);
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            audioManager = gameManager != null ? gameManager.Audio : null;
            if (audioManager == null)
            {
                audioManager = FindObjectOfType<AudioManager>(true);
            }

            if (gameManager == null)
            {
                return;
            }

            ResolveSoundButton();
            HideLegacyShopButton();
            gameManager.StateChanged += HandleStateChanged;

            Listen(pauseButton, HandlePauseClicked);
            Listen(resumeButton, HandleResumeClicked);
            Listen(restartButton, HandleRestartClicked);
            Listen(soundButton, HandleSoundClicked);

            RefreshSoundLabel();
            SetVisible(false);
            HandleStateChanged(gameManager.State);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            pauseButton?.onClick.RemoveListener(HandlePauseClicked);
            resumeButton?.onClick.RemoveListener(HandleResumeClicked);
            restartButton?.onClick.RemoveListener(HandleRestartClicked);
            soundButton?.onClick.RemoveListener(HandleSoundClicked);
            audioManager = null;
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void HandlePauseClicked() => gameManager?.SetPaused(true);

        private void HandleResumeClicked() => gameManager?.SetPaused(false);

        private void HandleRestartClicked() => gameManager?.RestartGame();

        private void HandleSoundClicked()
        {
            if (audioManager == null)
            {
                audioManager = FindObjectOfType<AudioManager>(true);
            }

            if (audioManager == null)
            {
                return;
            }

            audioManager.SetMuted(!audioManager.IsMuted);
            RefreshSoundLabel();
        }

        private void RefreshSoundLabel()
        {
            if (soundLabel == null && soundButton != null)
            {
                soundLabel = soundButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (soundLabel == null)
            {
                return;
            }

            bool muted = audioManager != null && audioManager.IsMuted;
            soundLabel.text = muted ? SoundOffLabel : SoundOnLabel;
        }

        private void HandleStateChanged(GameState state)
        {
            if (pauseButton != null)
            {
                pauseButton.interactable = state == GameState.Playing;
            }

            if (state == GameState.Paused)
            {
                RefreshSoundLabel();
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void Show()
        {
            ResolveCard();
            EnsureCanvasGroup();

            if (canvasGroup == null)
            {
                gameObject.SetActive(true);
                return;
            }

            GameTween.Kill(canvasGroup);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            GameTween.Fade(canvasGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            if (card != null)
            {
                GameTween.Kill(card);
                card.localScale = Vector3.one * 0.85f;
                GameTween.Scale(card, Vector3.one, ShowDuration, TweenEase.OutBack, unscaled: true);
            }
        }

        private void Hide()
        {
            ResolveCard();
            EnsureCanvasGroup();

            if (canvasGroup == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // Input is blocked immediately so a click cannot slip through the fade-out.
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (canvasGroup.alpha <= 0f)
            {
                return;
            }

            GameTween.Kill(canvasGroup);
            GameTween.Fade(canvasGroup, 0f, HideDuration, TweenEase.InQuad, unscaled: true);

            if (card != null)
            {
                GameTween.Kill(card);
                GameTween.Scale(card, Vector3.one * 0.85f, HideDuration, TweenEase.InQuad, unscaled: true);
            }
        }

        private void SetVisible(bool visible)
        {
            EnsureCanvasGroup();
            ResolveCard();

            if (card != null)
            {
                card.localScale = visible ? Vector3.one : Vector3.one * 0.85f;
            }

            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void ResolveCard()
        {
            if (card == null)
            {
                card = transform.Find("Card") as RectTransform;
            }
        }

        private void ResolveSoundButton()
        {
            if (soundButton == null)
            {
                soundButton = transform.Find("Card/SoundButton")?.GetComponent<Button>();
            }

            if (soundButton != null)
            {
                soundLabel = soundButton.GetComponentInChildren<TMP_Text>(true);
                return;
            }

            if (card == null)
            {
                return;
            }

            // Older baked prefabs lack the control; build it so mute still works.
            soundButton = UIFactory.CreateButton(
                "SoundButton",
                card,
                SoundOnLabel,
                GameTheme.ButtonSecondary,
                GameTheme.TextPrimary,
                34f);
            soundLabel = soundButton.GetComponentInChildren<TMP_Text>(true);
            LayoutPauseButtons();
        }

        private void HideLegacyShopButton()
        {
            Transform leftover = transform.Find("Card/ShopButton");
            if (leftover != null)
            {
                leftover.gameObject.SetActive(false);
            }

            LayoutPauseButtons();
        }

        private void LayoutPauseButtons()
        {
            if (card != null && card.sizeDelta.y > PauseCardHeight)
            {
                card.sizeDelta = new Vector2(card.sizeDelta.x, PauseCardHeight);
            }

            if (soundButton != null)
            {
                UIFactory.Anchor(
                    (RectTransform)soundButton.transform,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 360f),
                    new Vector2(620f, 100f));
            }
        }
    }
}
