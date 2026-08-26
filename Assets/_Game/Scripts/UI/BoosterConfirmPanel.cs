using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Overlay shown before a rewarded booster. Explains the bonus and that it
    /// costs a video; the ad is requested only after the player taps Watch.
    /// Dims the board like pause, but does not freeze the time scale.
    /// </summary>
    public class BoosterConfirmPanel : MonoBehaviour
    {
        public const string ObjectName = "BoosterConfirmPanel";

        private const float ShowDuration = 0.24f;
        private const float HideDuration = 0.16f;
        private const string WatchLabel = "Смотреть";
        private const string CancelLabel = "Отмена";
        private const string WarningText = "Бонус за просмотр рекламы";
        private const string UndoTitle = "Отмена хода";
        private const string UndoBody = "Вернёт последнюю поставленную фигуру на панель.";
        private const string ExtraTitle = "Лишняя фигура";
        private const string ExtraBody = "Добавит ещё одну фигуру на панель, если есть свободный слот.";
        private const string ClearTitle = "Очистка линии";
        private const string ClearBody = "Уберёт самую заполненную строку или столбец.";

        [SerializeField] private GameManager gameManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text warningLabel;
        [SerializeField] private Button watchButton;
        [SerializeField] private Button cancelButton;

        private bool visible;
        private bool figuresBlocked;
        private Action pendingWatch;

        /// <summary>True while the overlay covers the board. Platform code stops GameplayAPI on it.</summary>
        public bool IsOpen => visible;

        private void Awake()
        {
            ResolveRefs();
            if (gameManager != null)
            {
                Bind(gameManager);
            }
            else
            {
                SetVisible(false);
            }
        }

        public void Bind(
            GameManager manager,
            CanvasGroup group,
            RectTransform cardRect,
            Image iconImage,
            TMP_Text title,
            TMP_Text body,
            TMP_Text warning,
            Button watch,
            Button cancel)
        {
            canvasGroup = group;
            card = cardRect;
            icon = iconImage;
            titleLabel = title;
            bodyLabel = body;
            warningLabel = warning;
            watchButton = watch;
            cancelButton = cancel;
            Bind(manager);
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            ResolveRefs();

            Listen(watchButton, HandleWatchClicked);
            Listen(cancelButton, HandleCancelClicked);
            ApplyButtonCaption(watchButton, WatchLabel);
            ApplyButtonCaption(cancelButton, CancelLabel);

            if (gameManager != null)
            {
                gameManager.StateChanged += HandleStateChanged;
            }

            SetVisible(false);
            if (gameManager != null)
            {
                HandleStateChanged(gameManager.State);
            }
        }

        private void OnDestroy() => Unbind();

        /// <summary>
        /// Fills the card for the tapped booster. <paramref name="onWatch"/> runs
        /// only after Watch, once this overlay has already closed.
        /// </summary>
        public void Show(FreeBoosterType type, Action onWatch)
        {
            if (gameManager != null && gameManager.State != GameState.Playing)
            {
                return;
            }

            ResolveRefs();
            ApplyCopy(type);
            pendingWatch = onWatch;
            Show();
        }

        public void Hide()
        {
            pendingWatch = null;
            HideVisual();
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
            }

            watchButton?.onClick.RemoveListener(HandleWatchClicked);
            cancelButton?.onClick.RemoveListener(HandleCancelClicked);
            pendingWatch = null;
            UnblockFigures();
            gameManager = null;
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

        private static void ApplyButtonCaption(Button button, string caption)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = caption;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.Playing)
            {
                Hide();
            }
        }

        private void HandleWatchClicked()
        {
            Action watch = pendingWatch;
            pendingWatch = null;
            HideVisual();
            watch?.Invoke();
        }

        private void HandleCancelClicked() => Hide();

        private void ApplyCopy(FreeBoosterType type)
        {
            string title;
            string body;
            switch (type)
            {
                case FreeBoosterType.Extra:
                    title = ExtraTitle;
                    body = ExtraBody;
                    break;
                case FreeBoosterType.Clear:
                    title = ClearTitle;
                    body = ClearBody;
                    break;
                default:
                    title = UndoTitle;
                    body = UndoBody;
                    break;
            }

            if (titleLabel != null)
            {
                titleLabel.text = title;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = body;
            }

            if (warningLabel != null)
            {
                warningLabel.text = WarningText;
            }

            if (icon != null)
            {
                icon.sprite = BoosterBar.IconFor(type);
                icon.preserveAspect = true;
                icon.color = Color.white;
            }
        }

        private void Show()
        {
            ResolveRefs();
            visible = true;
            BlockFigures();

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

        private void HideVisual()
        {
            ResolveRefs();
            UnblockFigures();

            if (!visible && (canvasGroup == null || canvasGroup.alpha <= 0f))
            {
                return;
            }

            visible = false;

            if (canvasGroup == null)
            {
                gameObject.SetActive(false);
                return;
            }

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

        private void SetVisible(bool isVisible)
        {
            visible = isVisible;
            ResolveRefs();

            if (!isVisible)
            {
                UnblockFigures();
            }

            if (card != null)
            {
                card.localScale = isVisible ? Vector3.one : Vector3.one * 0.85f;
            }

            if (canvasGroup == null)
            {
                gameObject.SetActive(isVisible);
                return;
            }

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.blocksRaycasts = isVisible;
            canvasGroup.interactable = isVisible;
        }

        private void BlockFigures()
        {
            if (figuresBlocked)
            {
                return;
            }

            figuresBlocked = true;
            gameManager?.Spawner?.SetInteractable(false);
        }

        private void UnblockFigures()
        {
            if (!figuresBlocked)
            {
                return;
            }

            figuresBlocked = false;
            if (gameManager != null && gameManager.State == GameState.Playing)
            {
                gameManager.Spawner?.SetInteractable(true);
            }
        }

        private void ResolveRefs()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (card == null)
            {
                card = transform.Find("Card") as RectTransform;
            }

            if (card == null)
            {
                return;
            }

            if (icon == null)
            {
                icon = card.Find("Icon")?.GetComponent<Image>();
            }

            if (titleLabel == null)
            {
                titleLabel = card.Find("Title")?.GetComponent<TMP_Text>();
            }

            if (bodyLabel == null)
            {
                bodyLabel = card.Find("Body")?.GetComponent<TMP_Text>();
            }

            if (warningLabel == null)
            {
                warningLabel = card.Find("Warning")?.GetComponent<TMP_Text>();
            }

            if (watchButton == null)
            {
                watchButton = card.Find("WatchButton")?.GetComponent<Button>();
            }

            if (cancelButton == null)
            {
                cancelButton = card.Find("CancelButton")?.GetComponent<Button>();
            }
        }
    }
}
