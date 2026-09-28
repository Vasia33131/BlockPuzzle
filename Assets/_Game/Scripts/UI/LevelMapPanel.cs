using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Levels;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The level map: a full-screen, vertically scrolling trail of 100 nodes (plus a trophy) that opens from
    /// the "Levels" button of the main menu. It is an overlay of the menu state, not a game state of its own:
    /// it opens over the menu, "Back" closes it, and starting a level (any state but the menu) closes it too.
    ///
    /// Only what is on screen exists. The content is one tall rect inside a <see cref="ScrollRect"/>; a small
    /// pool of <see cref="LevelMapNode"/> and <see cref="LevelMapSegment"/> objects is bound to the levels in
    /// view (plus a margin) whenever the scroll range changes. The content has its own canvas, so scrolling
    /// does not rebuild the rest of the interface.
    ///
    /// After a passed level the "Menu" tap of the result screen opens the map through the state change: the
    /// view scrolls to the level, its stars pop, the dotted trail to the new level is drawn dot by dot and the
    /// new node opens with an effect (the trophy at the end of the campaign). Tapping an open node opens
    /// <see cref="LevelCardPanel"/>.
    /// </summary>
    public sealed class LevelMapPanel : MonoBehaviour
    {
        public const string ObjectName = "LevelMapPanel";

        private const float Margin = 36f;
        private const float BarHeight = 88f;
        private const float TopBarHeight = Margin + BarHeight + 24f;
        private static readonly Vector2 BackSize = new Vector2(230f, BarHeight);
        private static readonly Vector2 StarCounterSize = new Vector2(270f, BarHeight);
        private static readonly Vector2 BannerSize = new Vector2(640f, 84f);

        /// <summary>How far past the viewport nodes stay bound, so stars, the avatar and banners never pop in on screen.</summary>
        private const float VisibleMargin = 300f;

        /// <summary>Share of the viewport height, from the bottom, where the focused node is put.</summary>
        private const float FocusFraction = 0.42f;

        private const float DotStep = 0.09f;
        private const float BlobSize = 1300f;
        private const float AvatarScale = 0.45f;
        private const float BobHeight = 8f;
        private const float BobSpeed = 3.2f;

        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);

        private readonly Dictionary<int, LevelMapNode> activeNodes = new Dictionary<int, LevelMapNode>();
        private readonly Stack<LevelMapNode> nodePool = new Stack<LevelMapNode>();
        private readonly Dictionary<int, LevelMapSegment> activeSegments = new Dictionary<int, LevelMapSegment>();
        private readonly Stack<LevelMapSegment> segmentPool = new Stack<LevelMapSegment>();
        private readonly List<int> scratch = new List<int>(16);

        private readonly Image[] bannerPlates = new Image[LevelMapLayout.ChapterCount];
        private readonly TMP_Text[] bannerTexts = new TMP_Text[LevelMapLayout.ChapterCount];

        private GameManager gameManager;
        private LevelCardPanel card;
        private LevelDatabase database;

        private CanvasGroup group;
        private GameObject body;
        private RectTransform viewport;
        private RectTransform content;
        private ScrollRect scroll;
        private RectTransform nodeLayer;
        private RectTransform segmentLayer;
        private RectTransform badgeHolder;
        private RectTransform badge;
        private TMP_Text starText;
        private TMP_Text trophyLabel;
        private ConfettiBurst confetti;

        private int shownLow;
        private int shownHigh;
        private float lastViewportHeight;

        private GameState lastState = GameState.Boot;
        private int wonLevel;

        /// <summary>Highest level the view has already shown as open; a larger one after a win is what gets animated.</summary>
        private int lastShownUnlocked;

        /// <summary>While a new node is being revealed, the node that is still drawn closed; 0 otherwise.</summary>
        private int animTarget;

        private bool animating;
        private Coroutine sequence;

        /// <summary>The map of the running game; the menu button opens it through this.</summary>
        public static LevelMapPanel Instance { get; private set; }

        /// <summary>True while the map is on screen and takes the taps.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Builds the map (and its level card) under <paramref name="canvasRect"/>, or rebinds the one already there.</summary>
        public static LevelMapPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            LevelMapPanel existing = FindObjectOfType<LevelMapPanel>(true);
            if (existing != null)
            {
                existing.Bind(manager);
                existing.card = LevelCardPanel.Ensure(canvasRect, manager);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            RectTransform root = UIFactory.CreateRect(ObjectName, canvasRect);
            UIFactory.Stretch(root);
            var panel = root.gameObject.AddComponent<LevelMapPanel>();
            panel.Build(root);
            panel.Bind(manager);

            // Created after the map, so the card is always drawn over it.
            panel.card = LevelCardPanel.Ensure(canvasRect, manager);
            return panel;
        }

        private void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            LevelProgress.Changed += HandleProgressChanged;
            GameLocalization.LanguageChanged += HandleLanguageChanged;

            if (gameManager != null)
            {
                gameManager.StateChanged += HandleStateChanged;
                lastState = gameManager.State;
            }

            lastShownUnlocked = EffectiveUnlocked();
        }

        private void Unbind()
        {
            LevelProgress.Changed -= HandleProgressChanged;
            GameLocalization.LanguageChanged -= HandleLanguageChanged;

            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }
        }

        private void OnDestroy()
        {
            Unbind();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- progress

        private static bool IsComplete => LevelProgress.IsCampaignComplete;

        /// <summary>Highest node that is open: the trophy once the last level is passed.</summary>
        private static int EffectiveUnlocked() => IsComplete ? LevelMapLayout.TrophyIndex : LevelProgress.Unlocked;

        /// <summary>Last node that exists: the trophy is only there once the campaign is complete.</summary>
        private static int MaxIndex() => IsComplete ? LevelMapLayout.TrophyIndex : LevelMapLayout.LevelCount;

        /// <summary>The open range as drawn: one step behind while the new node is being revealed.</summary>
        private int ShownUnlocked() => animTarget > 0 ? animTarget - 1 : EffectiveUnlocked();

        private int CurrentIndex() => Mathf.Min(ShownUnlocked(), MaxIndex());

        private LevelMapNodeState StateOf(int index)
        {
            int unlocked = ShownUnlocked();
            if (index >= LevelMapLayout.TrophyIndex)
            {
                return index <= unlocked ? LevelMapNodeState.Trophy : LevelMapNodeState.TrophyLocked;
            }

            if (index > unlocked)
            {
                return LevelMapNodeState.Locked;
            }

            if (LevelProgress.GetStars(index) > 0)
            {
                return LevelMapNodeState.Passed;
            }

            return index == unlocked ? LevelMapNodeState.Current : LevelMapNodeState.Open;
        }

        private bool IsSpecial(int level)
        {
            if (database == null)
            {
                database = LevelDatabase.Load();
            }

            LevelDefinition definition = database != null ? database.GetByNumber(level) : null;
            return definition != null ? definition.IsSpecial : level % 5 == 0;
        }

        // ---------------------------------------------------------------- events

        private void HandleStateChanged(GameState state)
        {
            GameState previous = lastState;
            lastState = state;

            if (state == GameState.LevelWon)
            {
                LevelResult result = gameManager != null && gameManager.LevelRun != null ? gameManager.LevelRun.Result : null;
                wonLevel = result != null ? result.Number : 0;
            }

            if (state == GameState.MainMenu)
            {
                // "Menu" on the result screen of a passed level: show what that level changed.
                if (previous == GameState.LevelWon)
                {
                    Open(true, wonLevel);
                }
            }
            else if (IsOpen)
            {
                Close();
            }
        }

        private void HandleProgressChanged()
        {
            if (!IsOpen)
            {
                return;
            }

            if (animating)
            {
                UpdateStarCounter();
            }
            else
            {
                RefreshAll(false);
            }
        }

        private void HandleLanguageChanged()
        {
            if (IsOpen)
            {
                UpdateBanners();
                UpdateTrophyLabel();
                UpdateBackLabel();
            }
        }

        private void HandleBackClicked() => Close();

        private void HandleScrolled(Vector2 position) => SyncVisible(false);

        private void HandleNodeClicked(int index)
        {
            if (!IsOpen || animating)
            {
                return;
            }

            activeNodes.TryGetValue(index, out LevelMapNode node);

            if (index == LevelMapLayout.TrophyIndex)
            {
                MetaToast.ShowText(GameLocalization.LevelsSoon);
                node?.Punch();
                return;
            }

            if (!LevelProgress.IsUnlocked(index))
            {
                MetaToast.ShowText(GameLocalization.LevelLockedHint);
                node?.Punch();
                return;
            }

            if (card != null)
            {
                card.Show(index);
            }
        }

        // ---------------------------------------------------------------- open / close

        /// <summary>Opens the map over the main menu, scrolled to the level the player is up to.</summary>
        public void Open() => Open(false, 0);

        private void Open(bool afterWin, int playedLevel)
        {
            if (IsOpen)
            {
                return;
            }

            if (database == null)
            {
                database = LevelDatabase.Load();
            }

            if (database == null)
            {
                MetaToast.ShowText(GameLocalization.ComingSoon);
                return;
            }

            IsOpen = true;
            body.SetActive(true);
            transform.SetAsLastSibling();
            ApplyViewportInsets();
            Canvas.ForceUpdateCanvases();

            GameTween.Kill(group);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            group.interactable = true;
            GameTween.Fade(group, 1f, 0.2f, TweenEase.OutQuad, unscaled: true);

            int effective = EffectiveUnlocked();
            bool reveal = afterWin && effective > lastShownUnlocked;
            animTarget = reveal ? effective : 0;

            int focus = CurrentIndex();
            if (afterWin && !reveal && playedLevel > 0)
            {
                focus = Mathf.Min(playedLevel, MaxIndex());
            }

            lastViewportHeight = viewport.rect.height;
            SetScroll(FocusPosition(focus));
            RefreshAll(false);

            if (reveal)
            {
                sequence = StartCoroutine(PlayUnlock(effective));
                return;
            }

            lastShownUnlocked = effective;
            if (afterWin && activeNodes.TryGetValue(focus, out LevelMapNode played))
            {
                LevelMapNode node = played;
                GameTween.Delay(this, 0.35f, true, () =>
                {
                    if (IsOpen && node != null && node.Index == focus)
                    {
                        node.PlayStarsPop();
                    }
                });
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            StopAnimation();
            if (card != null)
            {
                card.Hide();
            }

            IsOpen = false;
            GameTween.Kill(group);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            body.SetActive(false);
        }

        private void ApplyViewportInsets()
        {
            // The sticky banner covers the bottom of the screen while ads are on.
            viewport.offsetMin = new Vector2(0f, GameTheme.ActiveBannerReserve);
            viewport.offsetMax = new Vector2(0f, -TopBarHeight);
        }

        // ---------------------------------------------------------------- scrolling and pooling

        private float FocusPosition(int index)
        {
            float viewHeight = viewport.rect.height;
            float maxOffset = Mathf.Max(0f, LevelMapLayout.ContentHeight - viewHeight);
            float offset = Mathf.Clamp(LevelMapLayout.Point(index).y - viewHeight * FocusFraction, 0f, maxOffset);
            return -offset;
        }

        private void SetScroll(float anchoredY)
        {
            scroll.StopMovement();
            content.anchoredPosition = new Vector2(0f, anchoredY);
            SyncVisible(false);
        }

        /// <summary>
        /// Binds the pooled nodes and trails to the levels in view. Cheap when the visible range did not
        /// change; <paramref name="force"/> rebinds everything (state, stars, colours).
        /// </summary>
        private void SyncVisible(bool force)
        {
            if (!IsOpen)
            {
                return;
            }

            float viewHeight = viewport.rect.height;
            float low = -content.anchoredPosition.y - VisibleMargin;
            float high = -content.anchoredPosition.y + viewHeight + VisibleMargin;
            int max = MaxIndex();
            int first = Mathf.Clamp(Mathf.FloorToInt((low - LevelMapLayout.BottomPadding) / LevelMapLayout.Step) + 1, 1, max);
            int last = Mathf.Clamp(Mathf.CeilToInt((high - LevelMapLayout.BottomPadding) / LevelMapLayout.Step) + 1, 1, max);

            if (!force && first == shownLow && last == shownHigh)
            {
                return;
            }

            shownLow = first;
            shownHigh = last;

            scratch.Clear();
            foreach (KeyValuePair<int, LevelMapNode> pair in activeNodes)
            {
                if (pair.Key < first || pair.Key > last)
                {
                    scratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratch.Count; i++)
            {
                LevelMapNode node = activeNodes[scratch[i]];
                activeNodes.Remove(scratch[i]);
                node.Release();
                nodePool.Push(node);
            }

            for (int index = first; index <= last; index++)
            {
                if (activeNodes.TryGetValue(index, out LevelMapNode node))
                {
                    if (force)
                    {
                        BindNode(node, index);
                    }
                }
                else
                {
                    node = nodePool.Count > 0 ? nodePool.Pop() : LevelMapNode.Create(nodeLayer, HandleNodeClicked);
                    activeNodes[index] = node;
                    BindNode(node, index);
                }
            }

            // The trail from node s to s + 1 is in view when either end is.
            int segmentFirst = Mathf.Max(1, first - 1);
            int segmentLast = Mathf.Min(max - 1, last);

            scratch.Clear();
            foreach (KeyValuePair<int, LevelMapSegment> pair in activeSegments)
            {
                if (pair.Key < segmentFirst || pair.Key > segmentLast)
                {
                    scratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < scratch.Count; i++)
            {
                LevelMapSegment segment = activeSegments[scratch[i]];
                activeSegments.Remove(scratch[i]);
                segment.Release();
                segmentPool.Push(segment);
            }

            for (int index = segmentFirst; index <= segmentLast; index++)
            {
                if (activeSegments.TryGetValue(index, out LevelMapSegment segment))
                {
                    if (force)
                    {
                        BindSegment(segment, index);
                    }
                }
                else
                {
                    segment = segmentPool.Count > 0 ? segmentPool.Pop() : LevelMapSegment.Create(segmentLayer);
                    activeSegments[index] = segment;
                    BindSegment(segment, index);
                }
            }
        }

        private void BindNode(LevelMapNode node, int index)
        {
            bool isLevel = index <= LevelMapLayout.LevelCount;
            node.Bind(
                index,
                StateOf(index),
                isLevel ? LevelProgress.GetStars(index) : 0,
                isLevel && IsSpecial(index),
                LevelMapLayout.ChapterColor(LevelMapLayout.ChapterOf(index)));
        }

        private void BindSegment(LevelMapSegment segment, int index)
        {
            segment.Bind(index, ShownUnlocked() > index, TrailColor(index));
        }

        private static Color TrailColor(int index) =>
            GameTheme.Lighten(LevelMapLayout.ChapterColor(LevelMapLayout.ChapterOf(index)), 0.25f);

        // ---------------------------------------------------------------- refresh

        private void RefreshAll(bool hopBadge)
        {
            UpdateBanners();
            UpdateTrophyLabel();
            UpdateStarCounter();
            UpdateBackLabel();
            SyncVisible(true);
            PlaceBadge(hopBadge);
        }

        private void UpdateStarCounter()
        {
            starText.text = LevelProgress.TotalStars + "/" + LevelProgress.MaxTotalStars;
        }

        private void UpdateBackLabel()
        {
            UIFactory.SetButtonText(backButtonRef, GameLocalization.Back);
        }

        private void UpdateTrophyLabel()
        {
            trophyLabel.text = GameLocalization.LevelsSoon;
            trophyLabel.gameObject.SetActive(ShownUnlocked() >= LevelMapLayout.TrophyIndex);
        }

        /// <summary>A chapter banner lights up once the player has entered that chapter.</summary>
        private void UpdateBanners()
        {
            int unlocked = ShownUnlocked();
            for (int chapter = 0; chapter < bannerPlates.Length; chapter++)
            {
                bool reached = unlocked >= chapter * LevelMapLayout.ChapterSize + 1;
                Color color = LevelMapLayout.ChapterColor(chapter);
                bannerPlates[chapter].color = reached
                    ? GameTheme.WithAlpha(GameTheme.Darken(color, 0.15f), 0.95f)
                    : new Color(1f, 1f, 1f, 0.08f);
                bannerTexts[chapter].color = reached ? Color.white : GameTheme.TextSecondary;
                bannerTexts[chapter].text = GameLocalization.ChapterTitle(chapter + 1);
            }
        }

        /// <summary>The avatar stands on top of the node the player is up to.</summary>
        private void PlaceBadge(bool hop)
        {
            int index = CurrentIndex();
            float radius = LevelMapNode.RadiusOf(StateOf(index));
            Vector2 target = LevelMapLayout.Point(index) + new Vector2(0f, radius - 14f);

            GameTween.Kill(badgeHolder);
            if (hop)
            {
                GameTween.MoveAnchored(badgeHolder, target, 0.5f, TweenEase.OutBack, unscaled: true);
            }
            else
            {
                badgeHolder.anchoredPosition = target;
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            float viewHeight = viewport.rect.height;
            if (!Mathf.Approximately(viewHeight, lastViewportHeight))
            {
                lastViewportHeight = viewHeight;
                SyncVisible(false);
            }

            float time = Time.unscaledTime;
            if (activeNodes.TryGetValue(CurrentIndex(), out LevelMapNode current))
            {
                current.Tick(time);
            }

            badge.anchoredPosition = new Vector2(0f, Mathf.Sin(time * BobSpeed) * BobHeight);
        }

        // ---------------------------------------------------------------- new level animation

        /// <summary>
        /// Scrolls to the level that was just passed, pops its stars, draws the trail to the new node dot by
        /// dot while the view follows it, then opens the node. Runs on unscaled time; the map ignores taps meanwhile.
        /// </summary>
        private IEnumerator PlayUnlock(int target)
        {
            animating = true;
            scroll.StopMovement();
            scroll.enabled = false;

            int from = target - 1;
            yield return WaitUnscaled(0.4f);

            if (activeNodes.TryGetValue(from, out LevelMapNode passed))
            {
                passed.PlayStarsPop();
            }

            yield return WaitUnscaled(0.55f);

            int dots = LevelMapLayout.DotPositions(from).Length;
            if (activeSegments.TryGetValue(from, out LevelMapSegment trail))
            {
                trail.PlayReveal(TrailColor(from), DotStep);
            }

            float startPosition = content.anchoredPosition.y;
            float endPosition = FocusPosition(target);
            float duration = Mathf.Max(0.7f, dots * DotStep + 0.3f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                content.anchoredPosition = new Vector2(0f, Mathf.Lerp(startPosition, endPosition, t));
                SyncVisible(false);
                yield return null;
            }

            // The node opens: from here on it is drawn as an open level and the avatar hops onto it.
            animTarget = 0;
            lastShownUnlocked = target;
            RefreshAll(true);

            bool trophy = target >= LevelMapLayout.TrophyIndex;
            if (activeNodes.TryGetValue(target, out LevelMapNode opened))
            {
                opened.PlayOpenEffect();
                if (trophy)
                {
                    PlayConfetti(opened);
                }
            }

            if (trophy)
            {
                trophyLabel.rectTransform.localScale = Vector3.zero;
                GameTween.Scale(trophyLabel.rectTransform, Vector3.one, 0.45f, TweenEase.OutBack, 0.2f, unscaled: true);
                gameManager?.Audio?.PlayBoardClear();
            }
            else
            {
                gameManager?.Audio?.PlayLineClear(1, 3);
            }

            yield return WaitUnscaled(0.6f);

            sequence = null;
            animating = false;
            scroll.enabled = true;
        }

        private void PlayConfetti(LevelMapNode node)
        {
            if (confetti == null)
            {
                return;
            }

            RectTransform layer = (RectTransform)confetti.transform;
            Vector2 origin = layer.InverseTransformPoint(node.Rect.TransformPoint(Vector3.zero));
            confetti.Play(origin);
        }

        /// <summary>Ends a running reveal at once: the map shows its final state.</summary>
        private void StopAnimation()
        {
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            if (confetti != null)
            {
                confetti.Stop();
            }

            if (animTarget > 0)
            {
                lastShownUnlocked = animTarget;
                animTarget = 0;
            }

            animating = false;
            if (scroll != null)
            {
                scroll.enabled = true;
            }
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ---------------------------------------------------------------- build

        private Button backButtonRef;

        private void Build(RectTransform root)
        {
            Instance = this;

            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            body = UIFactory.CreateRect("Body", root).gameObject;
            var bodyRect = (RectTransform)body.transform;
            UIFactory.Stretch(bodyRect);

            // Opaque and a raycast target: nothing of the menu underneath can be tapped through the map.
            Image background = UIFactory.CreateImage("Background", bodyRect, Color.white, false);
            UIFactory.Stretch(background.rectTransform);
            background.gameObject.AddComponent<VerticalGradient>().SetColors(MenuArt.BackgroundTop, MenuArt.BackgroundBottom);

            RectTransform safe = UIFactory.CreateRect("Safe", bodyRect);
            UIFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildScroll(safe);
            BuildDecor();
            BuildTrailAndNodes();
            BuildBadge();
            BuildTopBar(safe);

            confetti = ConfettiBurst.Create(bodyRect);

            body.SetActive(false);
        }

        private void BuildScroll(RectTransform safe)
        {
            viewport = UIFactory.CreateRect("Viewport", safe);
            UIFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            ApplyViewportInsets();

            content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = BottomCenter;
            content.sizeDelta = new Vector2(0f, LevelMapLayout.ContentHeight);
            content.anchoredPosition = Vector2.zero;

            // A transparent hit area, so a drag that starts between the nodes still scrolls.
            Image hit = content.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.canvasRenderer.cullTransparentMesh = false;

            // The content has its own canvas: scrolling and the pulse only rebuild this one, not the whole interface.
            content.gameObject.AddComponent<Canvas>();
            content.gameObject.AddComponent<GraphicRaycaster>();

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 60f;
            scroll.onValueChanged.AddListener(HandleScrolled);
        }

        private RectTransform CreateLayer(string name)
        {
            RectTransform layer = UIFactory.CreateRect(name, content);
            UIFactory.Anchor(layer, BottomCenter, BottomCenter, Vector2.zero, Vector2.zero);
            return layer;
        }

        /// <summary>Soft colour patches per chapter, the chapter banners and the caption over the trophy. All static.</summary>
        private void BuildDecor()
        {
            RectTransform blobs = CreateLayer("Blobs");
            for (int chapter = 0; chapter < LevelMapLayout.ChapterCount; chapter++)
            {
                float bottom = LevelMapLayout.Point(chapter * LevelMapLayout.ChapterSize + 1).y - 160f;
                float top = LevelMapLayout.Point((chapter + 1) * LevelMapLayout.ChapterSize).y + 160f;
                Color color = GameTheme.WithAlpha(LevelMapLayout.ChapterColor(chapter), 0.3f);

                for (int i = 0; i < 2; i++)
                {
                    Image blob = UIFactory.CreateImage($"Blob_{chapter}_{i}", blobs, color, false);
                    blob.sprite = MenuArt.GlowSprite;
                    blob.raycastTarget = false;
                    UIFactory.Anchor(
                        blob.rectTransform, Half, Half,
                        new Vector2(i == 0 ? -220f : 220f, Mathf.Lerp(bottom, top, i == 0 ? 0.25f : 0.75f)),
                        new Vector2(BlobSize, BlobSize));
                }
            }

            RectTransform banners = CreateLayer("Banners");
            for (int chapter = 0; chapter < LevelMapLayout.ChapterCount; chapter++)
            {
                // Chapter 1 sits under level 1; the others take the place of the trail between two chapters.
                float y = chapter == 0
                    ? LevelMapLayout.Point(1).y - 190f
                    : LevelMapLayout.Point(chapter * LevelMapLayout.ChapterSize + 0.42f).y;

                Image plate = UIFactory.CreateImage($"Banner_{chapter}", banners, Color.white);
                plate.raycastTarget = false;
                UIFactory.Anchor(plate.rectTransform, Half, Half, new Vector2(0f, y), BannerSize);
                bannerPlates[chapter] = plate;

                TextMeshProUGUI label = UIFactory.CreateText(
                    "Label", plate.rectTransform, string.Empty, 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                label.enableAutoSizing = true;
                label.fontSizeMin = 24f;
                label.fontSizeMax = 40f;
                UIFactory.Stretch(label.rectTransform, 16f);
                bannerTexts[chapter] = label;
            }

            trophyLabel = UIFactory.CreateText(
                "TrophyLabel", content, string.Empty, 56f, LevelArt.StarGold, TextAlignmentOptions.Center, FontStyles.Bold);
            trophyLabel.enableWordWrapping = true;
            MenuArt.ApplyOutlinedMaterial(trophyLabel);
            UIFactory.Anchor(
                trophyLabel.rectTransform, BottomCenter, Half,
                new Vector2(0f, LevelMapLayout.Point(LevelMapLayout.TrophyIndex).y + 300f), new Vector2(900f, 140f));
            trophyLabel.gameObject.SetActive(false);
        }

        private void BuildTrailAndNodes()
        {
            segmentLayer = CreateLayer("Trail");
            nodeLayer = CreateLayer("Nodes");
        }

        /// <summary>One shared avatar, small, that stands on the current node.</summary>
        private void BuildBadge()
        {
            badgeHolder = UIFactory.CreateRect("BadgeHolder", content);
            UIFactory.Anchor(badgeHolder, BottomCenter, BottomCenter, Vector2.zero, Vector2.zero);

            AvatarBadgeView view = AvatarBadgeView.Create(badgeHolder);
            badge = (RectTransform)view.transform;
            UIFactory.Anchor(badge, Half, BottomCenter, Vector2.zero, AvatarBadgeView.Size);
            badge.localScale = Vector3.one * AvatarScale;
        }

        private void BuildTopBar(RectTransform safe)
        {
            Button back = UIFactory.CreateButton(
                "BackButton", safe, GameLocalization.Back, GameTheme.FromHex("#3550D8"), Color.white, 40f);
            UIFactory.Anchor((RectTransform)back.transform, TopLeft, TopLeft, new Vector2(Margin, -Margin), BackSize);
            back.onClick.AddListener(HandleBackClicked);
            backButtonRef = back;

            RectTransform starSlot = UIFactory.CreateRect("StarSlot", safe);
            UIFactory.Anchor(starSlot, TopCenter, TopCenter, new Vector2(-50f, -Margin), StarCounterSize);

            Image plate = UIFactory.CreateImage("Plate", starSlot, GameTheme.WithAlpha(GameTheme.CardBackground, 0.92f));
            plate.raycastTarget = false;
            UIFactory.Stretch(plate.rectTransform);
            var outline = plate.gameObject.AddComponent<Outline>();
            outline.effectColor = GameTheme.WithAlpha(LevelArt.StarGold, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            Image icon = UIFactory.CreateImage("Star", starSlot, LevelArt.StarGold, false);
            icon.sprite = LevelArt.StarSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIFactory.Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(56f, 56f));

            starText = UIFactory.CreateText(
                "Stars", starSlot, "0/0", 42f, LevelArt.StarGold, TextAlignmentOptions.Center, FontStyles.Bold);
            starText.enableAutoSizing = true;
            starText.fontSizeMin = 26f;
            starText.fontSizeMax = 42f;
            UIFactory.Stretch(starText.rectTransform);
            starText.rectTransform.offsetMin = new Vector2(84f, 0f);
            starText.rectTransform.offsetMax = new Vector2(-14f, 0f);

            RectTransform coinSlot = UIFactory.CreateRect("CoinSlot", safe);
            UIFactory.Anchor(coinSlot, TopRight, TopRight, new Vector2(-Margin, -Margin), CoinCounterView.Size);
            CoinCounterView.Create(coinSlot);
        }
    }
}
