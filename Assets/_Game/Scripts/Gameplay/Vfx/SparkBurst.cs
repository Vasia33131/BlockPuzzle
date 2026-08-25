using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.Vfx
{
    /// <summary>
    /// Emits the small sparks that fly out of a line as it is cleared, painted in the colour
    /// of the blocks that were removed. The board lives on a Screen Space Overlay canvas, so
    /// a regular particle system would be drawn underneath it — these are uGUI images
    /// animated by <see cref="GameTween"/> instead, recycled through a pool so a full clear
    /// allocates nothing.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SparkBurst : MonoBehaviour
    {
        [Tooltip("Optional spark image. When empty a plain rounded dot is built in code.")]
        [SerializeField] private Image sparkPrefab;

        [Header("Emission")]
        [SerializeField, Min(1)] private int sparksPerCell = 3;

        [Tooltip("Sparks alive at once. Emission thins out rather than stalls when this is hit.")]
        [SerializeField, Min(1)] private int maxLiveSparks = 90;

        [Header("Flight")]
        [SerializeField] private float minDistance = 26f;
        [SerializeField] private float maxDistance = 92f;
        [SerializeField] private float minDuration = 0.3f;
        [SerializeField] private float maxDuration = 0.55f;
        [SerializeField] private float sparkSize = 15f;

        private readonly Stack<Image> pool = new Stack<Image>();
        private readonly List<Image> live = new List<Image>();
        private RectTransform rect;

        /// <summary>Adds the effect layer on top of the board it draws over.</summary>
        public static SparkBurst Create(RectTransform boardRoot)
        {
            RectTransform root = UIFactory.CreateRect("SparkLayer", boardRoot);
            UIFactory.Stretch(root);

            // The sparks are decoration only and must never swallow a drag.
            root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            var burst = root.gameObject.AddComponent<SparkBurst>();
            burst.rect = root;
            return burst;
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
        }

        /// <summary>
        /// Swaps in an authored spark. Pooled instances built from the previous look are
        /// dropped so the change takes effect on the very next burst.
        /// </summary>
        public void SetPrefab(Image prefab)
        {
            if (sparkPrefab == prefab)
            {
                return;
            }

            sparkPrefab = prefab;
            pool.Clear();
        }

        /// <summary>
        /// Throws a handful of sparks out of <paramref name="anchoredPosition"/>, which is
        /// expected in the board's own top-left anchored space, exactly like a cell.
        /// </summary>
        public void Emit(Vector2 anchoredPosition, Color color)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            int budget = Mathf.Min(sparksPerCell, maxLiveSparks - live.Count);
            for (int i = 0; i < budget; i++)
            {
                Launch(anchoredPosition, color);
            }
        }

        /// <summary>Cancels every spark in flight, used when a run restarts.</summary>
        public void Clear()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Image spark = live[i];
                if (spark == null)
                {
                    continue;
                }

                GameTween.Kill(spark.rectTransform);
                GameTween.Kill(spark);
                Recycle(spark);
            }

            live.Clear();
        }

        private void Launch(Vector2 origin, Color color)
        {
            Image spark = Rent();
            RectTransform sparkRect = spark.rectTransform;

            Vector2 jitter = Random.insideUnitCircle * (sparkSize * 0.9f);
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction == Vector2.zero)
            {
                direction = Vector2.up;
            }

            float distance = Random.Range(minDistance, maxDistance);
            float duration = Random.Range(minDuration, maxDuration);
            float size = sparkSize * Random.Range(0.6f, 1.15f);

            sparkRect.sizeDelta = new Vector2(size, size);
            sparkRect.anchoredPosition = origin + jitter;
            sparkRect.localScale = Vector3.one;

            spark.color = color;
            spark.gameObject.SetActive(true);
            live.Add(spark);

            // Sparks drift slightly upwards as they scatter, which reads as a spray rather
            // than as an even ring.
            Vector2 target = origin + jitter + direction * distance + Vector2.up * (distance * 0.25f);

            GameTween.MoveAnchored(sparkRect, target, duration, TweenEase.OutCubic);
            GameTween.Scale(sparkRect, Vector3.one * 0.25f, duration, TweenEase.InQuad);
            GameTween.Fade(spark, 0f, duration, TweenEase.InQuad, onComplete: () =>
            {
                live.Remove(spark);
                Recycle(spark);
            });
        }

        private Image Rent()
        {
            while (pool.Count > 0)
            {
                Image pooled = pool.Pop();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            return CreateSpark();
        }

        private void Recycle(Image spark)
        {
            if (spark == null)
            {
                return;
            }

            spark.gameObject.SetActive(false);
            pool.Push(spark);
        }

        private Image CreateSpark()
        {
            Image spark = sparkPrefab != null
                ? Instantiate(sparkPrefab, rect)
                : UIFactory.CreateImage($"Spark_{pool.Count + live.Count}", rect, Color.white);

            spark.raycastTarget = false;

            RectTransform sparkRect = spark.rectTransform;
            sparkRect.anchorMin = new Vector2(0f, 1f);
            sparkRect.anchorMax = new Vector2(0f, 1f);
            sparkRect.pivot = new Vector2(0.5f, 0.5f);
            sparkRect.sizeDelta = new Vector2(sparkSize, sparkSize);

            spark.gameObject.SetActive(false);
            return spark;
        }
    }
}
