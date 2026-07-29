using UnityEngine;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Insets a full-screen rect into the device safe area so the HUD is not hidden by
    /// notches or gesture bars on Android phones.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rect;
        private Rect lastSafeArea;
        private Vector2Int lastResolution;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea ||
                Screen.width != lastResolution.x ||
                Screen.height != lastResolution.y)
            {
                Apply();
            }
        }

        public void Apply()
        {
            if (rect == null)
            {
                rect = (RectTransform)transform;
            }

            Rect safeArea = Screen.safeArea;
            lastSafeArea = safeArea;
            lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 min = safeArea.position;
            Vector2 max = safeArea.position + safeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            min.x = Mathf.Clamp01(min.x);
            min.y = Mathf.Clamp01(min.y);
            max.x = Mathf.Clamp01(Mathf.Max(max.x, min.x + 0.05f));
            max.y = Mathf.Clamp01(Mathf.Max(max.y, min.y + 0.05f));

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
