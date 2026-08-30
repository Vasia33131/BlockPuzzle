using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Keeps the scene background ramp, tiled backdrop, HUD chrome and camera in
    /// sync with <see cref="GameTheme"/> so a shop purchase can recolor the screen
    /// without reloading. HUD shop and pause buttons follow the theme accent
    /// (yellow / turquoise / pink).
    /// </summary>
    [DisallowMultipleComponent]
    public class ThemeBinder : MonoBehaviour
    {
        [SerializeField] private VerticalGradient gradient;
        [SerializeField] private Image backgroundPattern;
        [SerializeField] private GameObject shopButton;
        [SerializeField] private GameObject pauseButton;

        public static ThemeBinder Ensure()
        {
            ThemeBinder existing = FindObjectOfType<ThemeBinder>(true);
            if (existing != null)
            {
                existing.Apply();
                return existing;
            }

            GameObject background = GameObject.Find("Background");
            if (background == null)
            {
                return null;
            }

            ThemeBinder binder = background.GetComponent<ThemeBinder>();
            if (binder == null)
            {
                binder = background.AddComponent<ThemeBinder>();
            }

            binder.Apply();
            return binder;
        }

        /// <summary>Creates the tiled backdrop child if the baked scene predates it.</summary>
        public static Image EnsureBackgroundPattern(RectTransform background)
        {
            if (background == null)
            {
                return null;
            }

            Image image = ThemePattern.EnsureChild(background, ThemePattern.BackgroundChildName, 0);
            image.raycastTarget = false;
            ThemePattern.ApplyBackgroundOverlay(image);
            return image;
        }

        private void Awake()
        {
            if (gradient == null)
            {
                gradient = GetComponent<VerticalGradient>();
            }

            EnsurePattern();
        }

        private void OnEnable()
        {
            GameTheme.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameTheme.Changed -= Apply;
        }

        private void Start() => Apply();

        public void Apply()
        {
            if (gradient == null)
            {
                gradient = GetComponent<VerticalGradient>();
            }

            gradient?.SetColors(GameTheme.BackgroundTop, GameTheme.BackgroundBottom);

            Camera sceneCamera = Camera.main;
            if (sceneCamera != null)
            {
                sceneCamera.backgroundColor = GameTheme.BackgroundBottom;
            }

            EnsurePattern();
            ThemePattern.ApplyBackgroundOverlay(backgroundPattern);

            GridManager grid = FindObjectOfType<GridManager>();
            grid?.ApplyThemeColors();
            ApplyHudButtons();
        }

        private void ApplyHudButtons()
        {
            if (shopButton == null)
            {
                shopButton = GameObject.Find("ShopButton");
            }

            if (pauseButton == null)
            {
                pauseButton = GameObject.Find("PauseButton");
            }

            PaintHudIconButton(shopButton);
            PaintHudIconButton(pauseButton);
        }

        private static void PaintHudIconButton(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null)
                {
                    continue;
                }

                image.color = image.gameObject == root ? GameTheme.HudButton : GameTheme.HudButtonIcon;
            }
        }

        private void EnsurePattern()
        {
            if (backgroundPattern == null)
            {
                backgroundPattern = EnsureBackgroundPattern((RectTransform)transform);
            }
        }
    }
}
