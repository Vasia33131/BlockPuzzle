using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Shared skeleton of the levels overlays (goal card, result screens, leave confirmation): a dim
    /// layer with a card in the middle that fades in on unscaled time, blocks the input beneath it
    /// while it is up, and shrinks to fit a short (landscape) screen. Every overlay builds itself in
    /// code and is bound to the <see cref="GameManager"/> that owns the state.
    /// </summary>
    public abstract class LevelOverlayPanel : MonoBehaviour
    {
        protected static readonly Color DarkLabel = GameTheme.FromHex("#1a1a2e");

        private const float ShowDuration = 0.28f;
        private const float HideDuration = 0.16f;

        private Image cardImage;
        private Vector2 cardSize;
        private float fitScale = 1f;

        protected GameManager Game { get; private set; }
        protected CanvasGroup Group { get; private set; }
        protected RectTransform Card { get; private set; }

        /// <summary>True while the overlay is on screen and takes the taps.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Builds the dim layer and the card under <paramref name="parent"/> and adds the overlay component.</summary>
        protected static T CreateOverlay<T>(
            RectTransform parent, string objectName, Vector2 size, float dimAlpha) where T : LevelOverlayPanel
        {
            RectTransform root = UIFactory.CreateRect(objectName, parent);
            UIFactory.Stretch(root);

            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image dim = UIFactory.CreateImage("Dim", root, new Color(0.03f, 0.03f, 0.08f, dimAlpha), false);
            UIFactory.Stretch(dim.rectTransform);

            Image card = UIFactory.CreateImage("Card", root, GameTheme.CardBackground);
            UIFactory.Anchor(
                card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);

            T panel = root.gameObject.AddComponent<T>();
            panel.Group = group;
            panel.Card = card.rectTransform;
            panel.cardImage = card;
            panel.cardSize = size;
            panel.Card.localScale = Vector3.zero;
            return panel;
        }

        protected void BindGame(GameManager manager)
        {
            UnbindGame();
            Game = manager;
            if (Game != null)
            {
                Game.StateChanged += HandleStateChanged;
                GameLocalization.LanguageChanged += HandleLanguageChanged;
                HandleStateChanged(Game.State);
            }
        }

        private void UnbindGame()
        {
            if (Game != null)
            {
                Game.StateChanged -= HandleStateChanged;
                GameLocalization.LanguageChanged -= HandleLanguageChanged;
                Game = null;
            }
        }

        protected virtual void OnDestroy() => UnbindGame();

        /// <summary>The game state changed; overlays open and close from here.</summary>
        protected abstract void HandleStateChanged(GameState state);

        /// <summary>The language changed; rewrite every caption.</summary>
        protected virtual void HandleLanguageChanged()
        {
        }

        /// <summary>Repaints what depends on the active theme. Runs every time the overlay opens.</summary>
        protected virtual void ApplyTheme()
        {
            if (cardImage != null)
            {
                cardImage.color = GameTheme.CardBackground;
            }
        }

        /// <summary>Changes the height of the card, for a card whose button stack varies.</summary>
        protected void ResizeCard(float height)
        {
            cardSize = new Vector2(cardSize.x, height);
            Card.sizeDelta = cardSize;
        }

        protected void Open()
        {
            ApplyTheme();
            IsOpen = true;
            UpdateFitScale();

            transform.SetAsLastSibling();
            GameTween.Kill(Group);
            GameTween.Kill(Card);
            Group.blocksRaycasts = true;
            Group.interactable = true;
            Group.alpha = 0f;
            Card.localScale = Vector3.one * (fitScale * 0.85f);

            SfxHub.Play(SfxId.UiOpen);
            GameTween.Fade(Group, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);
            GameTween.Scale(Card, Vector3.one * fitScale, ShowDuration, TweenEase.OutBack, unscaled: true);
        }

        protected void Close()
        {
            IsOpen = false;
            if (Group == null)
            {
                return;
            }

            // Input is blocked at once so a tap cannot slip through the fade-out.
            Group.blocksRaycasts = false;
            Group.interactable = false;

            GameTween.Kill(Group);
            GameTween.Kill(Card);
            if (Group.alpha <= 0f)
            {
                return;
            }

            SfxHub.Play(SfxId.UiClose);
            GameTween.Fade(Group, 0f, HideDuration, TweenEase.InQuad, unscaled: true);
            GameTween.Scale(Card, Vector3.one * (fitScale * 0.85f), HideDuration, TweenEase.InQuad, unscaled: true);
        }

        /// <summary>Shrinks a card that would not fit the screen; never blows it up.</summary>
        private void UpdateFitScale()
        {
            Rect area = ((RectTransform)transform).rect;
            float fit = 1f;
            if (area.height > 1f && area.width > 1f)
            {
                fit = Mathf.Min(1f, (area.height - 60f) / cardSize.y, (area.width - 40f) / cardSize.x);
            }

            fitScale = Mathf.Max(0.5f, fit);
        }

        // ---------------------------------------------------------------- builders

        protected TextMeshProUGUI AddText(
            string objectName,
            string content,
            float fontSize,
            Color color,
            float y,
            Vector2 size,
            FontStyles style = FontStyles.Bold)
        {
            // Big lines are headings; the small grey captions and hints are body text.
            FontRole role = fontSize <= 40f ? FontRole.Body : FontRole.Heading;
            TextMeshProUGUI text = UIFactory.CreateText(
                objectName, Card, content, fontSize, color, TextAlignmentOptions.Center, style, role);
            UIFactory.Anchor(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), size);
            UIFactory.FitText(text);
            return text;
        }

        protected Button AddButton(
            string objectName, string caption, Color background, Color labelColor, float fontSize, float y, Vector2 size)
        {
            Button button = UIFactory.CreateButton(objectName, Card, caption, background, labelColor, fontSize);
            UIFactory.Anchor(
                (RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), size);
            UIFactory.FitText(button.GetComponentInChildren<TMP_Text>(true));
            return button;
        }

        protected static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        /// <summary>Recolours a button and rewrites its caption.</summary>
        protected static void StyleButton(Button button, string caption, Color background, Color labelColor)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = background;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = caption;
                label.color = labelColor;
            }
        }
    }
}
