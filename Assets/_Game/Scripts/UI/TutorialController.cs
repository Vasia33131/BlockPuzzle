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
    /// First-run onboarding. The very first run starts on a scripted board: one row is
    /// full except for a three-cell gap, and the tray holds a single straight three that
    /// closes it. A hand keeps dragging a ghost of that figure from the tray to the gap,
    /// the target is lit on the <see cref="HighlightGrid"/> and a short hint explains the
    /// move. The board accepts the figure only on the gap, so the first drop always clears
    /// the line; then "Great!" pops, the hand goes away and the run carries on as usual.
    ///
    /// Completion is stored through <see cref="TutorialProgress"/>; the platform bridge
    /// mirrors it into the Yandex save, sends the Metrica goal and keeps ads off while
    /// <see cref="TutorialProgress.IsActive"/> is set. A restart before the line is cleared
    /// replays the tutorial.
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        /// <summary>The tutorial figure waits in the middle slot, right under the thumb.</summary>
        private const int TutorialSlot = 1;

        private const int GapLength = 3;

        [Tooltip("Optional hand/finger picture; its pivot is treated as the fingertip. Built in code when empty.")]
        [SerializeField] private Sprite handSprite;

        [Header("Hand Loop")]
        [SerializeField, Min(0.5f)] private float loopDuration = 2.4f;
        [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.55f;

        [Tooltip("Pause after a failed drag before the hand shows the move again.")]
        [SerializeField, Min(0f)] private float resumeDelay = 0.6f;

        private GameManager gameManager;
        private GridManager grid;
        private ShapeSpawner spawner;
        private ShopPanel shopPanel;
        private BoosterConfirmPanel boosterConfirm;

        private BlockShape tutorialShape;
        private Vector2Int targetOrigin;
        private bool running;
        private bool highlightShown;
        private float loopTime;
        private float idleDelay;

        private RectTransform overlay;
        private CanvasGroup overlayGroup;
        private RectTransform boardSpace;
        private RectTransform hand;
        private CanvasGroup handGroup;
        private RectTransform tapRing;
        private Image tapRingImage;
        private RectTransform ghost;
        private CanvasGroup ghostGroup;
        private readonly List<Image> ghostCells = new List<Image>();
        private CanvasGroup hintGroup;
        private TextMeshProUGUI hintLabel;

        public static TutorialController Instance { get; private set; }

        /// <summary>
        /// Attaches the controller to the game manager. Called from the bootstrap's Awake,
        /// which is before the first run starts from <c>GameManager.Start</c>.
        /// </summary>
        public static TutorialController Ensure(GameManager manager)
        {
            TutorialController existing = FindObjectOfType<TutorialController>(true);
            if (existing != null || manager == null)
            {
                return existing;
            }

            return manager.gameObject.AddComponent<TutorialController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            TryBind();
            TutorialProgress.RestoredAsDone += HandleRestoredAsDone;
            GameLocalization.LanguageChanged += RefreshTexts;
        }

        private void OnDisable()
        {
            TutorialProgress.RestoredAsDone -= HandleRestoredAsDone;
            GameLocalization.LanguageChanged -= RefreshTexts;
            StopTutorial();
            Unbind();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void TryBind()
        {
            GameManager manager = GameManager.Instance != null
                ? GameManager.Instance
                : GetComponent<GameManager>();

            if (manager == null || manager == gameManager)
            {
                return;
            }

            Unbind();
            gameManager = manager;
            gameManager.RunStarting += HandleRunStarting;
            gameManager.StateChanged += HandleStateChanged;
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.RunStarting -= HandleRunStarting;
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }
        }

        /// <summary>Leaving for the main menu abandons the tutorial; the next new run replays it.</summary>
        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.MainMenu)
            {
                StopTutorial();
            }
        }

        /// <summary>A run is about to be dealt: stage the scripted board if the tutorial is still due.</summary>
        private void HandleRunStarting()
        {
            StopTutorial();

            bool replay = TutorialProgress.ConsumeReplay();
            if ((TutorialProgress.IsDone && !replay) || gameManager == null)
            {
                return;
            }

            grid = gameManager.Grid;
            spawner = gameManager.Spawner;
            if (grid == null || spawner == null || grid.Size < GapLength + 2)
            {
                return;
            }

            StageBoard();
            BindPanels();
            EnsureOverlay();

            grid.ShapePlaced += HandleShapePlaced;
            running = true;
            loopTime = 0f;
            idleDelay = 0f;
            TutorialProgress.SetActive(true);

            RefreshTexts();
            GameTween.Kill(hintGroup);
            hintGroup.alpha = 1f;
            overlayGroup.alpha = 0f;
            overlay.gameObject.SetActive(true);
        }

        /// <summary>
        /// Middle row full except a centred three-cell gap, plus two small corner clusters so
        /// the clear does not empty the whole board (that would steal the moment with a bonus).
        /// </summary>
        private void StageBoard()
        {
            int size = grid.Size;
            int row = size / 2;
            int gapStart = (size - GapLength) / 2;
            targetOrigin = new Vector2Int(gapStart, row);

            var layout = new List<Vector2Int>(size + 6);
            for (int col = 0; col < size; col++)
            {
                if (col < gapStart || col >= gapStart + GapLength)
                {
                    layout.Add(new Vector2Int(col, row));
                }
            }

            int last = size - 1;
            layout.Add(new Vector2Int(0, last - 1));
            layout.Add(new Vector2Int(0, last));
            layout.Add(new Vector2Int(1, last));
            layout.Add(new Vector2Int(last, last - 1));
            layout.Add(new Vector2Int(last, last));
            layout.Add(new Vector2Int(last - 1, last));

            tutorialShape ??= BlockShape.Create(
                "TutorialLine3", 0, 1f, new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0));

            var batch = new BlockShape[ShapeSpawner.SlotCount];
            batch[TutorialSlot] = tutorialShape;

            grid.SetNextLayout(layout);
            spawner.SetNextBatch(new ScriptedShapeProvider(batch));
            grid.PlacementGate = GateTutorialDrop;
        }

        /// <summary>The tutorial figure may land only on the gap; anything else plays as usual.</summary>
        private bool GateTutorialDrop(BlockShape shape, Vector2Int origin)
        {
            return shape != tutorialShape || origin == targetOrigin;
        }

        private void HandleShapePlaced(PlacementResult result)
        {
            if (running && result.LinesCleared > 0)
            {
                CompleteTutorial();
            }
        }

        private void CompleteTutorial()
        {
            StopTutorial(fadeOut: true);
            TutorialProgress.MarkDone();
            grid?.Feedback?.ShowMessage(GameLocalization.TutorialDone);
        }

        private void HandleRestoredAsDone() => StopTutorial(fadeOut: true);

        /// <summary>Drops every tutorial hook; the board and tray stay as they are.</summary>
        private void StopTutorial(bool fadeOut = false)
        {
            bool wasRunning = running;
            running = false;

            if (grid != null)
            {
                grid.ShapePlaced -= HandleShapePlaced;
                if (grid.PlacementGate == (System.Func<BlockShape, Vector2Int, bool>)GateTutorialDrop)
                {
                    grid.PlacementGate = null;
                }

                if (highlightShown)
                {
                    grid.HideDropHighlight();
                }
            }

            highlightShown = false;
            TutorialProgress.SetActive(false);

            if (overlay == null)
            {
                return;
            }

            SetHandVisible(false);
            GameTween.Kill(overlayGroup);
            if (fadeOut && wasRunning && overlay.gameObject.activeSelf)
            {
                GameTween.Fade(overlayGroup, 0f, 0.35f, onComplete: () => overlay.gameObject.SetActive(false));
            }
            else
            {
                overlay.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (gameManager == null)
            {
                TryBind();
            }

            if (!running || overlay == null)
            {
                return;
            }

            bool visible = IsBoardInFront();
            overlayGroup.alpha = Mathf.MoveTowards(overlayGroup.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (!visible)
            {
                return;
            }

            FollowBoard();

            DraggableShape piece = spawner != null ? spawner.GetShape(TutorialSlot) : null;
            if (piece == null || piece.Shape != tutorialShape)
            {
                SetHandVisible(false);
                return;
            }

            if (piece.IsDragging)
            {
                // The figure draws its own drop hint while it is held.
                highlightShown = false;
                SetHandVisible(false);
                loopTime = 0f;
                idleDelay = resumeDelay;
                return;
            }

            if (!highlightShown)
            {
                grid.ShowDropHighlight(tutorialShape, targetOrigin);
                highlightShown = true;
            }

            if (idleDelay > 0f)
            {
                idleDelay -= Time.deltaTime;
                SetHandVisible(false);
                return;
            }

            AnimateHand(piece);
        }

        /// <summary>Pause, Game Over, the shop or a booster dialog hide the overlay.</summary>
        private bool IsBoardInFront()
        {
            if (gameManager == null || !gameManager.IsPlaying)
            {
                return false;
            }

            if (shopPanel != null && shopPanel.IsOpen)
            {
                return false;
            }

            return boosterConfirm == null || !boosterConfirm.IsOpen;
        }

        /// <summary>
        /// One loop: the finger lands on the tray figure, presses, drags a ghost of it to the
        /// gap, lets go and fades. The ghost grows from tray size to board size on the way,
        /// just like the real figure does when it is picked up.
        /// </summary>
        private void AnimateHand(DraggableShape piece)
        {
            loopTime = (loopTime + Time.deltaTime) % loopDuration;
            float t = loopTime / loopDuration;

            Vector2 from = boardSpace.InverseTransformPoint(piece.transform.position);
            Vector2 to = boardSpace.InverseTransformPoint(TargetCenterWorld());
            float boardScale = Mathf.Max(0.0001f, grid.BoardRoot.lossyScale.x);
            float trayScale = piece.transform.lossyScale.x / boardScale;

            const float pressEnd = 0.18f;
            const float moveEnd = 0.7f;
            const float releaseEnd = 0.82f;

            float move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pressEnd, moveEnd, t));
            Vector2 figureCenter = Vector2.Lerp(from, to, move);

            float alpha;
            float press;
            if (t < pressEnd)
            {
                alpha = Mathf.InverseLerp(0f, pressEnd * 0.6f, t);
                press = Mathf.InverseLerp(pressEnd * 0.4f, pressEnd, t);
            }
            else if (t < releaseEnd)
            {
                alpha = 1f;
                press = t < moveEnd ? 1f : 1f - Mathf.InverseLerp(moveEnd, releaseEnd, t);
            }
            else
            {
                alpha = 1f - Mathf.InverseLerp(releaseEnd, 1f, t);
                press = 0f;
            }

            SetHandVisible(true);

            // The fingertip rests on the lower part of the figure, so the ghost stays visible.
            float cell = grid.CellSize;
            hand.anchoredPosition = figureCenter + new Vector2(cell * 0.15f, -cell * 0.25f);
            hand.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, press);
            handGroup.alpha = alpha;

            tapRing.anchoredPosition = hand.anchoredPosition;
            tapRing.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.25f, press);
            tapRingImage.color = new Color(1f, 1f, 1f, 0.45f * press * alpha);

            bool ghostOn = t >= pressEnd * 0.5f && t < releaseEnd;
            ghost.gameObject.SetActive(ghostOn);
            if (ghostOn)
            {
                ghost.anchoredPosition = figureCenter;
                ghost.localScale = Vector3.one * Mathf.Lerp(trayScale, 1f, move);
                ghostGroup.alpha = ghostAlpha * alpha;
            }
        }

        private Vector3 TargetCenterWorld()
        {
            Vector3 sum = Vector3.zero;
            IReadOnlyList<Vector2Int> cells = tutorialShape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                sum += grid.GetCellWorldPosition(targetOrigin + cells[i]);
            }

            return sum / Mathf.Max(1, cells.Count);
        }

        /// <summary>Keeps the board-space root on top of the board, in board units, whatever the layout does.</summary>
        private void FollowBoard()
        {
            RectTransform board = grid.BoardRoot;
            boardSpace.position = board.TransformPoint(board.rect.center);
            boardSpace.rotation = board.rotation;

            float parentScale = Mathf.Max(0.0001f, overlay.lossyScale.x);
            boardSpace.localScale = Vector3.one * (board.lossyScale.x / parentScale);
            boardSpace.sizeDelta = board.rect.size;

            // Hint over the empty upper rows, well clear of the target line.
            float pitch = grid.Pitch;
            var hintRect = (RectTransform)hintGroup.transform;
            hintRect.sizeDelta = new Vector2(board.rect.width * 0.94f, pitch * 1.25f);
            hintRect.anchoredPosition = new Vector2(0f, board.rect.height * 0.5f - pitch * 1.6f);
            hintLabel.fontSizeMax = grid.CellSize * 0.42f;
            hintLabel.fontSizeMin = hintLabel.fontSizeMax * 0.5f;
        }

        private void SetHandVisible(bool visible)
        {
            if (hand == null)
            {
                return;
            }

            hand.gameObject.SetActive(visible);
            tapRing.gameObject.SetActive(visible);
            if (!visible)
            {
                ghost.gameObject.SetActive(false);
            }
        }

        private void RefreshTexts()
        {
            UIFactory.SetText(hintLabel, GameLocalization.TutorialHint);
        }

        private void BindPanels()
        {
            if (shopPanel == null)
            {
                shopPanel = FindObjectOfType<ShopPanel>(true);
            }

            if (boosterConfirm == null)
            {
                boosterConfirm = FindObjectOfType<BoosterConfirmPanel>(true);
            }
        }

        /// <summary>
        /// Builds the overlay once, right under the drag layer so a figure the player is
        /// holding is still drawn above the hand. Pure decoration: it never takes a touch.
        /// </summary>
        private void EnsureOverlay()
        {
            if (overlay != null)
            {
                RebuildGhost();
                return;
            }

            RectTransform dragLayer = spawner.DragLayer;
            Transform parent = dragLayer != null ? dragLayer.parent : grid.BoardRoot.root;

            overlay = UIFactory.CreateRect("TutorialOverlay", parent);
            UIFactory.Stretch(overlay);
            if (dragLayer != null)
            {
                overlay.SetSiblingIndex(dragLayer.GetSiblingIndex());
            }
            else
            {
                overlay.SetAsLastSibling();
            }

            overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable = false;

            boardSpace = UIFactory.CreateRect("BoardSpace", overlay);
            boardSpace.anchorMin = boardSpace.anchorMax = new Vector2(0.5f, 0.5f);
            boardSpace.pivot = new Vector2(0.5f, 0.5f);

            BuildHint();
            RebuildGhost();
            BuildHand();
            overlay.gameObject.SetActive(false);
        }

        private void BuildHint()
        {
            Image backdrop = UIFactory.CreateImage("TutorialHint", boardSpace, new Color(0.06f, 0.05f, 0.16f, 0.78f));
            backdrop.raycastTarget = false;
            RectTransform rect = backdrop.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            hintGroup = backdrop.gameObject.AddComponent<CanvasGroup>();

            hintLabel = UIFactory.CreateText(
                "Label", rect, GameLocalization.TutorialHint, 40f, Color.white,
                TextAlignmentOptions.Center, FontStyles.Bold);
            hintLabel.enableAutoSizing = true;
            hintLabel.enableWordWrapping = true;
            UIFactory.Stretch(hintLabel.rectTransform, 12f);
        }

        /// <summary>Ghost of the tutorial figure in board units, centred on its own pivot.</summary>
        private void RebuildGhost()
        {
            if (ghost == null)
            {
                ghost = UIFactory.CreateRect("Ghost", boardSpace);
                ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
                ghost.pivot = new Vector2(0.5f, 0.5f);
                ghostGroup = ghost.gameObject.AddComponent<CanvasGroup>();
            }

            for (int i = 0; i < ghostCells.Count; i++)
            {
                Destroy(ghostCells[i].gameObject);
            }

            ghostCells.Clear();

            float cell = grid.CellSize;
            float pitch = grid.Pitch;
            Vector2 center = tutorialShape.BoundsCenter;
            IReadOnlyList<Vector2Int> cells = tutorialShape.Cells;

            for (int i = 0; i < cells.Count; i++)
            {
                Image image = UIFactory.CreateImage($"GhostCell_{i}", ghost, tutorialShape.Color);
                image.raycastTarget = false;
                UIFactory.Anchor(
                    image.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2((cells[i].x - center.x) * pitch, -(cells[i].y - center.y) * pitch),
                    new Vector2(cell, cell));
                ghostCells.Add(image);
            }

            ghost.gameObject.SetActive(false);
        }

        /// <summary>
        /// Pointing hand. With no sprite assigned it is drawn from rounded blocks: a raised
        /// index finger over a palm, pivoted at the fingertip.
        /// </summary>
        private void BuildHand()
        {
            float cell = grid.CellSize;

            tapRing = UIFactory.CreateRect("TapRing", boardSpace);
            tapRingImage = UIFactory.CreateImage("Ring", tapRing, Color.clear);
            tapRingImage.raycastTarget = false;
            tapRingImage.fillCenter = false;
            tapRing.anchorMin = tapRing.anchorMax = new Vector2(0.5f, 0.5f);
            tapRing.sizeDelta = new Vector2(cell * 0.9f, cell * 0.9f);
            UIFactory.Stretch(tapRingImage.rectTransform);

            hand = UIFactory.CreateRect("Hand", boardSpace);
            hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.pivot = new Vector2(0.5f, 1f);
            handGroup = hand.gameObject.AddComponent<CanvasGroup>();

            if (handSprite != null)
            {
                Image picture = UIFactory.CreateImage("Picture", hand, Color.white, false);
                picture.sprite = handSprite;
                picture.preserveAspect = true;
                picture.raycastTarget = false;
                RectTransform pictureRect = picture.rectTransform;
                float height = cell * 1.6f;
                float width = height * handSprite.rect.width / Mathf.Max(1f, handSprite.rect.height);
                Vector2 pivot = handSprite.pivot / handSprite.rect.size;
                UIFactory.Anchor(pictureRect, new Vector2(0.5f, 1f), pivot, Vector2.zero, new Vector2(width, height));
            }
            else
            {
                BuildProceduralHand(cell);
            }

            SetHandVisible(false);
        }

        private void BuildProceduralHand(float cell)
        {
            var skin = new Color(1f, 0.93f, 0.86f, 1f);
            var outline = new Color(0.12f, 0.1f, 0.22f, 0.9f);

            // Slight tilt reads as a hand rather than a pin.
            RectTransform body = UIFactory.CreateRect("Body", hand);
            UIFactory.Anchor(body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            body.localRotation = Quaternion.Euler(0f, 0f, 18f);

            // The finger's top edge sits on the pivot, so the fingertip is the hand's position.
            AddHandPart(body, "Palm", new Vector2(cell * 0.12f, -cell * 0.92f), new Vector2(cell * 0.8f, cell * 0.7f), skin, outline);
            AddHandPart(body, "Thumb", new Vector2(-cell * 0.3f, -cell * 0.9f), new Vector2(cell * 0.24f, cell * 0.44f), skin, outline, -35f);
            AddHandPart(body, "Finger", new Vector2(0f, -cell * 0.36f), new Vector2(cell * 0.26f, cell * 0.72f), skin, outline);
        }

        private static void AddHandPart(
            RectTransform parent, string name, Vector2 center, Vector2 size, Color fill, Color outline, float angle = 0f)
        {
            Image border = UIFactory.CreateImage(name, parent, outline);
            border.raycastTarget = false;
            UIFactory.Anchor(border.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, size + Vector2.one * 8f);
            border.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

            Image inner = UIFactory.CreateImage("Fill", border.rectTransform, fill);
            inner.raycastTarget = false;
            UIFactory.Stretch(inner.rectTransform, 4f);
        }
    }
}
