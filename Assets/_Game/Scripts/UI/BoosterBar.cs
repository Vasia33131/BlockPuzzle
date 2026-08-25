using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Managers;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Three rewarded boosters between the board and the figure tray. Each caption
    /// says that watching an ad is required. Platform code shows the video; this bar
    /// only raises <see cref="UndoRequested"/>, <see cref="ExtraRequested"/> and
    /// <see cref="ClearRequested"/>. Hidden while paused or on Game Over.
    /// </summary>
    public class BoosterBar : MonoBehaviour
    {
        public const float BarHeight = 108f;
        public const float TrayGap = 10f;

        private const float ButtonFontSize = 22f;
        private const float ButtonSpacing = 12f;
        private const string UndoCaption = "Отменить ход — реклама";
        private const string ExtraCaption = "Ещё фигура — реклама";
        private const string ClearCaption = "Убрать линию — реклама";

        private static readonly Color DisabledBackground = new Color(0.32f, 0.32f, 0.38f, 0.95f);
        private static readonly Color DisabledLabel = new Color(0.62f, 0.62f, 0.7f, 0.9f);

        [SerializeField] private GameManager gameManager;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button extraButton;
        [SerializeField] private Button clearButton;

        private CanvasGroup canvasGroup;
        private Image undoImage;
        private Image extraImage;
        private Image clearImage;
        private TMP_Text undoLabel;
        private TMP_Text extraLabel;
        private TMP_Text clearLabel;
        private GridManager grid;
        private ShapeSpawner spawner;

        /// <summary>Raised when the player taps undo. Platform code shows the rewarded ad.</summary>
        public event Action UndoRequested;

        /// <summary>Raised when the player taps extra figure. Platform code shows the rewarded ad.</summary>
        public event Action ExtraRequested;

        /// <summary>Raised when the player taps clear line. Platform code shows the rewarded ad.</summary>
        public event Action ClearRequested;

        private void Awake()
        {
            EnsureCanvasGroup();
            if (gameManager != null)
            {
                Bind(gameManager);
            }
        }

        public void Bind(GameManager manager, Button undo, Button extra, Button clear)
        {
            undoButton = undo;
            extraButton = extra;
            clearButton = clear;
            Bind(manager);
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            EnsureCanvasGroup();
            CacheButtonParts();

            if (gameManager == null)
            {
                ApplyVisible(false);
                return;
            }

            grid = gameManager.Grid;
            spawner = gameManager.Spawner;

            gameManager.StateChanged += HandleStateChanged;
            if (grid != null)
            {
                grid.ShapePlaced += HandleBoardChanged;
            }

            if (spawner != null)
            {
                spawner.ShapesChanged += RefreshAvailability;
                spawner.BatchSpawned += RefreshAvailability;
            }

            Listen(undoButton, HandleUndoClicked);
            Listen(extraButton, HandleExtraClicked);
            Listen(clearButton, HandleClearClicked);

            HandleStateChanged(gameManager.State);
        }

        private void Update()
        {
            if (IsPlaying())
            {
                RefreshAvailability();
            }
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
            }

            if (grid != null)
            {
                grid.ShapePlaced -= HandleBoardChanged;
            }

            if (spawner != null)
            {
                spawner.ShapesChanged -= RefreshAvailability;
                spawner.BatchSpawned -= RefreshAvailability;
            }

            if (undoButton != null)
            {
                undoButton.onClick.RemoveListener(HandleUndoClicked);
            }

            if (extraButton != null)
            {
                extraButton.onClick.RemoveListener(HandleExtraClicked);
            }

            if (clearButton != null)
            {
                clearButton.onClick.RemoveListener(HandleClearClicked);
            }

            gameManager = null;
            grid = null;
            spawner = null;
        }

        private void HandleStateChanged(GameState state)
        {
            ApplyVisible(state == GameState.Playing);
            RefreshAvailability();
        }

        private void HandleBoardChanged(PlacementResult result) => RefreshAvailability();

        private void HandleUndoClicked()
        {
            if (!IsPlaying())
            {
                return;
            }

            UndoRequested?.Invoke();
        }

        private void HandleExtraClicked()
        {
            if (!IsPlaying())
            {
                return;
            }

            ExtraRequested?.Invoke();
        }

        private void HandleClearClicked()
        {
            if (!IsPlaying())
            {
                return;
            }

            ClearRequested?.Invoke();
        }

        private void RefreshAvailability()
        {
            BoosterController boosters = gameManager != null ? gameManager.Boosters : null;
            bool playing = IsPlaying();
            ApplyButton(undoButton, undoImage, undoLabel, playing && boosters != null && boosters.CanUndo);
            ApplyButton(extraButton, extraImage, extraLabel, playing && boosters != null && boosters.CanExtraPiece);
            ApplyButton(clearButton, clearImage, clearLabel, playing && boosters != null && boosters.CanClearLine);
        }

        private bool IsPlaying() => gameManager != null && gameManager.State == GameState.Playing;

        private void ApplyVisible(bool visible)
        {
            EnsureCanvasGroup();
            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        private void ApplyButton(Button button, Image image, TMP_Text label, bool available)
        {
            if (button != null)
            {
                button.interactable = available;
            }

            if (image != null)
            {
                image.color = available ? GameTheme.ButtonSecondary : DisabledBackground;
            }

            if (label != null)
            {
                label.color = available ? GameTheme.TextPrimary : DisabledLabel;
            }
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void CacheButtonParts()
        {
            CacheButton(undoButton, out undoImage, out undoLabel);
            CacheButton(extraButton, out extraImage, out extraLabel);
            CacheButton(clearButton, out clearImage, out clearLabel);
        }

        private static void CacheButton(Button button, out Image image, out TMP_Text label)
        {
            image = button != null ? button.targetGraphic as Image : null;
            if (image == null && button != null)
            {
                image = button.GetComponent<Image>();
            }

            label = null;
            if (button == null)
            {
                return;
            }

            Transform labelTransform = button.transform.Find("Label");
            label = labelTransform != null
                ? labelTransform.GetComponent<TMP_Text>()
                : button.GetComponentInChildren<TMP_Text>(true);
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

        /// <summary>Builds the three-button row between the board and the figure tray.</summary>
        public static BoosterBar Build(RectTransform parent, GameManager manager)
        {
            RectTransform root = UIFactory.CreateRect("BoosterBar", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;

            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = ButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(0, 0, 0, 0);

            Button undo = CreateBoosterButton(root, "UndoButton", UndoCaption);
            Button extra = CreateBoosterButton(root, "ExtraButton", ExtraCaption);
            Button clear = CreateBoosterButton(root, "ClearButton", ClearCaption);

            var bar = root.gameObject.AddComponent<BoosterBar>();
            bar.Bind(manager, undo, extra, clear);
            return bar;
        }

        private static Button CreateBoosterButton(Transform parent, string name, string caption)
        {
            Button button = UIFactory.CreateButton(
                name, parent, caption, GameTheme.ButtonSecondary, GameTheme.TextPrimary, ButtonFontSize);

            var layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = 72f;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableWordWrapping = true;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.fontSize = ButtonFontSize;
                UIFactory.Stretch(label.rectTransform, 8f);
            }

            return button;
        }
    }
}
