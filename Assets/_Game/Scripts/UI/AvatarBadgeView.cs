using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Reusable player badge: a round avatar in a theme-coloured ring, the level on a shield
    /// that overlaps its lower edge, and the name underneath. Reads <see cref="PlayerProfile"/>
    /// and <see cref="PlayerLevel"/>, so it has no platform dependency; a tap on the avatar
    /// of a signed-out player asks the platform layer to open the Yandex sign-in.
    /// Build it with <see cref="Create"/> and place the returned rect wherever it is needed.
    /// </summary>
    public class AvatarBadgeView : MonoBehaviour
    {
        public const string ObjectName = "AvatarBadge";

        private const float AvatarSize = 170f;
        private const float RingWidth = 8f;
        private static readonly Vector2 ShieldSize = new Vector2(80f, 92f);
        private const float ShieldOverlap = 46f;
        private const float NameGap = 8f;
        private const float NameHeight = 40f;
        private const float NameFontSize = 30f;

        /// <summary>Overall size of the badge: avatar, shield and name.</summary>
        public static readonly Vector2 Size = new Vector2(
            300f, AvatarSize - ShieldOverlap + ShieldSize.y + NameGap + NameHeight);

        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

        private const float PunchStrength = 0.6f;
        private const float PunchDuration = 0.6f;
        private const float PulseDuration = 0.65f;
        private const float PulseScale = 1.7f;
        private const float PulseAlpha = 0.55f;

        private Button avatarButton;
        private Image ring;
        private Image face;
        private RawImage photo;
        private TMP_Text question;
        private Image pulse;
        private RectTransform shield;
        private Image shieldRim;
        private Image shieldFill;
        private TMP_Text levelLabel;
        private TMP_Text nameLabel;

        private bool built;
        private int shownLevel;

        /// <summary>Builds a badge under <paramref name="parent"/> and returns it. Position the <c>transform</c> yourself.</summary>
        public static AvatarBadgeView Create(Transform parent)
        {
            RectTransform root = UIFactory.CreateRect(ObjectName, parent);
            root.sizeDelta = Size;
            var view = root.gameObject.AddComponent<AvatarBadgeView>();
            view.Build();
            return view;
        }

        private void OnEnable()
        {
            PlayerProfile.Changed += RefreshProfile;
            PlayerLevel.Changed += RefreshLevel;
            PlayerLevel.LevelUp += HandleLevelUp;
            GameTheme.Changed += ApplyTheme;
            GameLocalization.LanguageChanged += RefreshProfile;

            if (!built)
            {
                return;
            }

            ApplyTheme();
            RefreshProfile();

            // The level went up while this badge was not on screen: play it now.
            if (shownLevel > 0 && PlayerLevel.Level > shownLevel)
            {
                PlayLevelUp();
            }
            else
            {
                RefreshLevel();
            }
        }

        private void OnDisable()
        {
            PlayerProfile.Changed -= RefreshProfile;
            PlayerLevel.Changed -= RefreshLevel;
            PlayerLevel.LevelUp -= HandleLevelUp;
            GameTheme.Changed -= ApplyTheme;
            GameLocalization.LanguageChanged -= RefreshProfile;
        }

        private void HandleAvatarClicked()
        {
            if (PlayerProfile.IsAuthorized)
            {
                return;
            }

            MetaToast.ShowText(GameLocalization.SignInToSave);
            PlayerProfile.RequestAuth();
        }

        private void HandleLevelUp(int newLevel)
        {
            if (built)
            {
                PlayLevelUp();
            }
        }

        /// <summary>Scale punch of the shield and a soft ring that spreads out of the avatar.</summary>
        private void PlayLevelUp()
        {
            RefreshLevel();

            GameTween.Kill(shield);
            GameTween.Punch(shield, PunchStrength, PunchDuration, unscaled: true);

            GameTween.Kill(pulse.transform);
            GameTween.Kill(pulse);
            pulse.gameObject.SetActive(true);
            pulse.rectTransform.localScale = Vector3.one;
            pulse.color = GameTheme.WithAlpha(GameTheme.Accent, PulseAlpha);
            GameTween.Scale(pulse.transform, Vector3.one * PulseScale, PulseDuration, TweenEase.OutQuad, unscaled: true);
            GameTween.Fade(pulse, 0f, PulseDuration, TweenEase.OutQuad, unscaled: true, onComplete: () =>
            {
                if (pulse != null)
                {
                    pulse.gameObject.SetActive(false);
                }
            });
        }

        private void RefreshLevel()
        {
            if (!built)
            {
                return;
            }

            shownLevel = PlayerLevel.Level;
            levelLabel.text = shownLevel.ToString();
            levelLabel.fontSize = shownLevel >= 100 ? 28f : 38f;
        }

        private void RefreshProfile()
        {
            if (!built)
            {
                return;
            }

            nameLabel.text = PlayerProfile.DisplayName;

            Texture2D avatar = PlayerProfile.Avatar;
            bool hasPhoto = avatar != null;
            photo.gameObject.SetActive(hasPhoto);
            question.gameObject.SetActive(!hasPhoto);
            if (hasPhoto)
            {
                photo.texture = avatar;
                photo.uvRect = CoverRect(avatar);
            }

            ApplyTheme();
        }

        /// <summary>Recolors the ring, the placeholder disc and the shield with the active theme.</summary>
        private void ApplyTheme()
        {
            if (!built)
            {
                return;
            }

            Color accent = GameTheme.Accent;
            ring.color = accent;
            face.color = photo.gameObject.activeSelf ? GameTheme.CardBackground : accent;
            question.color = MetaUi.DarkLabel;
            shieldRim.color = MetaUi.DarkLabel;
            shieldFill.color = accent;
            levelLabel.color = MetaUi.DarkLabel;
            nameLabel.color = GameTheme.TextPrimary;
        }

        /// <summary>UV window that fills the round mask with the picture without stretching it.</summary>
        private static Rect CoverRect(Texture texture)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            float aspect = (float)texture.width / texture.height;
            if (aspect > 1f)
            {
                float width = 1f / aspect;
                return new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }

            float height = aspect;
            return new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }

        private void Build()
        {
            var root = (RectTransform)transform;

            // Spreads out of the avatar on a level-up, so it sits behind everything.
            pulse = UIFactory.CreateImage("Pulse", root, Color.clear, rounded: false);
            pulse.sprite = ProfileUi.CircleSprite;
            pulse.raycastTarget = false;
            UIFactory.Anchor(pulse.rectTransform, TopCenter, TopCenter, Vector2.zero, new Vector2(AvatarSize, AvatarSize));
            pulse.gameObject.SetActive(false);

            ring = UIFactory.CreateImage("Ring", root, GameTheme.Accent, rounded: false);
            ring.sprite = ProfileUi.CircleSprite;
            UIFactory.Anchor(ring.rectTransform, TopCenter, TopCenter, Vector2.zero, new Vector2(AvatarSize, AvatarSize));

            avatarButton = ring.gameObject.AddComponent<Button>();
            avatarButton.targetGraphic = ring;
            avatarButton.transition = Selectable.Transition.None;
            avatarButton.onClick.AddListener(HandleAvatarClicked);
            ButtonPressAnimator.Attach(avatarButton);

            face = UIFactory.CreateImage("Face", ring.rectTransform, GameTheme.Accent, rounded: false);
            face.sprite = ProfileUi.CircleSprite;
            face.raycastTarget = false;
            UIFactory.Stretch(face.rectTransform, RingWidth);
            var mask = face.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var photoRect = UIFactory.CreateRect("Photo", face.rectTransform);
            UIFactory.Stretch(photoRect);
            photo = photoRect.gameObject.AddComponent<RawImage>();
            photo.raycastTarget = false;
            photo.gameObject.SetActive(false);

            question = UIFactory.CreateText(
                "Question", face.rectTransform, "?", 96f, MetaUi.DarkLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(question.rectTransform);

            shieldRim = UIFactory.CreateImage("Shield", root, MetaUi.DarkLabel, rounded: false);
            shieldRim.sprite = ProfileUi.ShieldSprite;
            shieldRim.raycastTarget = false;
            shield = shieldRim.rectTransform;
            UIFactory.Anchor(
                shield, TopCenter, TopCenter, new Vector2(0f, -(AvatarSize - ShieldOverlap)), ShieldSize);

            shieldFill = UIFactory.CreateImage("Fill", shield, GameTheme.Accent, rounded: false);
            shieldFill.sprite = ProfileUi.ShieldSprite;
            shieldFill.raycastTarget = false;
            UIFactory.Stretch(shieldFill.rectTransform, 5f);

            levelLabel = UIFactory.CreateText(
                "Level", shield, "1", 38f, MetaUi.DarkLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(levelLabel.rectTransform);
            // The shield narrows towards its point, so the number rides a little above the middle.
            levelLabel.rectTransform.offsetMin = new Vector2(0f, 12f);

            nameLabel = UIFactory.CreateText(
                "Name", root, string.Empty, NameFontSize, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            UIFactory.Anchor(
                nameLabel.rectTransform,
                TopCenter,
                TopCenter,
                new Vector2(0f, -(AvatarSize - ShieldOverlap + ShieldSize.y + NameGap)),
                new Vector2(Size.x, NameHeight));

            built = true;
            RefreshProfile();
            RefreshLevel();
        }
    }
}
