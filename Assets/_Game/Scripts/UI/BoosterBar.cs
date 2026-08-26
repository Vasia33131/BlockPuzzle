using System;
using System.Collections.Generic;
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
    /// Three boosters between the board and the figure tray. Each button is the
    /// booster sprite itself. A free +1 charge is applied immediately; otherwise
    /// a confirm overlay explains the bonus, then this bar raises
    /// <see cref="UndoRequested"/>, <see cref="ExtraRequested"/> and
    /// <see cref="ClearRequested"/> so platform code can show a rewarded ad.
    /// Hidden while paused or on Game Over.
    /// </summary>
    public class BoosterBar : MonoBehaviour
    {
        public const float BarHeight = 160f;
        public const float TrayGap = 36f;
        public const float BoardGap = 10f;

        private const float IconSize = 152f;
        private const float ButtonSpacing = 130f;
        private const float BadgeFontSize = 64f;
        private const string UndoIconPath = "UI/Icons/IconUndo";
        private const string ExtraIconPath = "UI/Icons/IconExtra";
        private const string ClearIconPath = "UI/Icons/IconClear";

        private static readonly Color DisabledIcon = new Color(0.72f, 0.72f, 0.76f, 0.5f);
        private static readonly Color BadgeRed = new Color(1f, 0.08f, 0.12f, 1f);
        private static readonly Vector2 BadgeSize = new Vector2(120f, 96f);
        private static readonly Vector2 BadgeOffset = new Vector2(-8f, 8f);
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoosterConfirmPanel confirmPanel;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button extraButton;
        [SerializeField] private Button clearButton;

        private CanvasGroup canvasGroup;
        private Image undoImage;
        private Image extraImage;
        private Image clearImage;
        private TextMeshProUGUI undoBadge;
        private TextMeshProUGUI extraBadge;
        private TextMeshProUGUI clearBadge;
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
            ApplySpriteButtons();
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

        public void Bind(GameManager manager, BoosterConfirmPanel confirm)
        {
            confirmPanel = confirm;
            Bind(manager);
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            if (confirmPanel == null)
            {
                confirmPanel = FindObjectOfType<BoosterConfirmPanel>(true);
            }
            EnsureCanvasGroup();
            ApplySpriteButtons();
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
            HandleBoosterClicked(FreeBoosterType.Undo, UndoRequested);
        }

        private void HandleExtraClicked()
        {
            HandleBoosterClicked(FreeBoosterType.Extra, ExtraRequested);
        }

        private void HandleClearClicked()
        {
            HandleBoosterClicked(FreeBoosterType.Clear, ClearRequested);
        }

        private void HandleBoosterClicked(FreeBoosterType type, Action rewardedRequest)
        {
            if (!IsPlaying())
            {
                return;
            }

            BoosterController boosters = gameManager != null ? gameManager.Boosters : null;
            if (boosters != null && boosters.HasFree(type))
            {
                boosters.TryConsumeFree(type);
                RefreshAvailability();
                return;
            }

            if (confirmPanel != null)
            {
                confirmPanel.Show(type, rewardedRequest);
                return;
            }

            rewardedRequest?.Invoke();
        }

        /// <summary>Booster sprite used on the bar and on the confirm overlay.</summary>
        public static Sprite IconFor(FreeBoosterType type)
        {
            switch (type)
            {
                case FreeBoosterType.Extra:
                    return LoadBoosterSprite(ExtraIconPath);
                case FreeBoosterType.Clear:
                    return LoadBoosterSprite(ClearIconPath);
                default:
                    return LoadBoosterSprite(UndoIconPath);
            }
        }

        private void RefreshAvailability()
        {
            BoosterController boosters = gameManager != null ? gameManager.Boosters : null;
            bool playing = IsPlaying();
            ApplyButton(undoButton, undoImage, playing && boosters != null && boosters.CanUndo);
            ApplyButton(extraButton, extraImage, playing && boosters != null && boosters.CanExtraPiece);
            ApplyButton(clearButton, clearImage, playing && boosters != null && boosters.CanClearLine);
            ApplyBadge(undoBadge, boosters != null && boosters.HasFree(FreeBoosterType.Undo));
            ApplyBadge(extraBadge, boosters != null && boosters.HasFree(FreeBoosterType.Extra));
            ApplyBadge(clearBadge, boosters != null && boosters.HasFree(FreeBoosterType.Clear));
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

        private void ApplyButton(Button button, Image image, bool available)
        {
            if (button != null)
            {
                button.interactable = available;
            }

            if (image != null)
            {
                image.color = available ? Color.white : DisabledIcon;
            }
        }

        private static void ApplyBadge(TextMeshProUGUI badge, bool visible)
        {
            if (badge != null)
            {
                badge.gameObject.SetActive(visible);
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
            undoImage = ResolveImage(undoButton);
            extraImage = ResolveImage(extraButton);
            clearImage = ResolveImage(clearButton);
            undoBadge = ResolveBadge(undoButton);
            extraBadge = ResolveBadge(extraButton);
            clearBadge = ResolveBadge(clearButton);
        }

        private static Image ResolveImage(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Image image = button.targetGraphic as Image;
            return image != null ? image : button.GetComponent<Image>();
        }

        private static TextMeshProUGUI ResolveBadge(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Transform child = button.transform.Find("Badge");
            if (child == null)
            {
                return null;
            }

            return child.GetComponent<TextMeshProUGUI>()
                ?? child.GetComponentInChildren<TextMeshProUGUI>(true);
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

        /// <summary>
        /// Replaces the baked rounded-rect buttons with the booster sprites. Runs on
        /// Bind so a scene that was generated before the icons still picks them up.
        /// </summary>
        private void ApplySpriteButtons()
        {
            HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = ButtonSpacing;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(0, 0, 0, 0);
            }

            StyleSpriteButton(undoButton, UndoIconPath);
            StyleSpriteButton(extraButton, ExtraIconPath);
            StyleSpriteButton(clearButton, ClearIconPath);
        }

        private static void StyleSpriteButton(Button button, string iconPath)
        {
            if (button == null)
            {
                return;
            }

            button.transition = Selectable.Transition.None;
            ButtonPressAnimator.Attach(button);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Image image = ResolveImage(button);
            if (image != null)
            {
                image.sprite = LoadBoosterSprite(iconPath);
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                image.raycastTarget = true;
                image.useSpriteMesh = false;
            }

            var layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = button.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
            layoutElement.minWidth = IconSize;
            layoutElement.minHeight = IconSize;
            layoutElement.preferredWidth = IconSize;
            layoutElement.preferredHeight = IconSize;

            HideChild(button.transform, "Label");
            HideChild(button.transform, "Icon");
            EnsureBadge(button);
        }

        private static TextMeshProUGUI EnsureBadge(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Transform existing = button.transform.Find("Badge");
            TextMeshProUGUI badge = existing != null
                ? existing.GetComponent<TextMeshProUGUI>() ?? existing.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;

            if (existing != null)
            {
                Image chip = existing.GetComponent<Image>();
                if (chip != null)
                {
                    chip.enabled = false;
                }

                var plateOutline = existing.GetComponent<Outline>();
                if (plateOutline != null)
                {
                    plateOutline.enabled = false;
                }
            }

            if (badge == null)
            {
                badge = UIFactory.CreateText(
                    "Badge",
                    button.transform,
                    "+1",
                    BadgeFontSize,
                    BadgeRed,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);
            }
            else if (badge.transform.parent != button.transform)
            {
                Transform oldRoot = badge.transform.parent;
                badge.transform.SetParent(button.transform, false);
                badge.gameObject.name = "Badge";
                if (oldRoot != null)
                {
                    oldRoot.name = "BadgeChip";
                    oldRoot.gameObject.SetActive(false);
                }
            }

            badge.text = "+1";
            badge.fontSize = BadgeFontSize;
            badge.fontStyle = FontStyles.Bold;
            badge.alignment = TextAlignmentOptions.Center;
            badge.color = BadgeRed;
            badge.raycastTarget = false;
            badge.enableWordWrapping = false;
            badge.overflowMode = TextOverflowModes.Overflow;
            badge.extraPadding = true;
            badge.outlineWidth = 0.22f;
            badge.outlineColor = BadgeRed;
            if (badge.fontMaterial != null)
            {
                badge.fontMaterial.EnableKeyword("OUTLINE_ON");
            }

            UIFactory.Anchor(
                badge.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f),
                BadgeOffset,
                BadgeSize);

            badge.gameObject.SetActive(false);
            return badge;
        }

        private static void HideChild(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static Sprite LoadBoosterSprite(string resourcePath)
        {
            if (spriteCache.TryGetValue(resourcePath, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect);
                }
            }

            if (sprite != null)
            {
                spriteCache[resourcePath] = sprite;
            }

            return sprite;
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
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);

            Button undo = CreateBoosterButton(root, "UndoButton", UndoIconPath);
            Button extra = CreateBoosterButton(root, "ExtraButton", ExtraIconPath);
            Button clear = CreateBoosterButton(root, "ClearButton", ClearIconPath);

            var bar = root.gameObject.AddComponent<BoosterBar>();
            bar.Bind(manager, undo, extra, clear);
            return bar;
        }

        private static Button CreateBoosterButton(Transform parent, string name, string iconPath)
        {
            Image image = UIFactory.CreateImage(name, parent, Color.white, rounded: false);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            StyleSpriteButton(button, iconPath);
            return button;
        }
    }
}
