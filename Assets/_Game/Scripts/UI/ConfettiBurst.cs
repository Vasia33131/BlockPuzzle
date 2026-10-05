using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Celebration confetti for the game-over screen: a pooled set of small uGUI rects
    /// thrown up from a point, then falling, spinning and fading out. Runs on unscaled
    /// time and never blocks raycasts, so it can sit on top of the buttons.
    /// </summary>
    public sealed class ConfettiBurst : MonoBehaviour
    {
        private const int PieceCount = 70;
        private const float Gravity = -2600f;
        private const float Lifetime = 2.2f;

        private readonly List<Piece> pieces = new List<Piece>();
        private RectTransform root;
        private bool running;

        private sealed class Piece
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Spin;
            public float Wobble;
            public float Age;
            public float Life;
        }

        /// <summary>Stretched, non-interactive layer under <paramref name="parent"/>.</summary>
        public static ConfettiBurst Create(Transform parent)
        {
            RectTransform rect = UIFactory.CreateRect("Confetti", parent);
            UIFactory.Stretch(rect);
            return rect.gameObject.AddComponent<ConfettiBurst>();
        }

        /// <summary>Fires a burst from <paramref name="origin"/> in this layer's local space.</summary>
        public void Play(Vector2 origin)
        {
            root = (RectTransform)transform;
            transform.SetAsLastSibling();
            EnsurePool();

            Color[] palette = GameTheme.PastelPalette;
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece piece = pieces[i];
                float angle = Random.Range(55f, 125f) * Mathf.Deg2Rad;
                float speed = Random.Range(1300f, 2300f);
                piece.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                piece.Spin = Random.Range(-540f, 540f);
                piece.Wobble = Random.Range(0f, Mathf.PI * 2f);
                piece.Age = 0f;
                piece.Life = Lifetime * Random.Range(0.75f, 1.1f);

                Color color = palette != null && palette.Length > 0
                    ? palette[Random.Range(0, palette.Length)]
                    : Color.white;
                color.a = 1f;
                piece.Image.color = i % 5 == 0 ? GameTheme.Accent : color;

                piece.Rect.anchoredPosition = origin + Random.insideUnitCircle * 30f;
                piece.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                piece.Rect.sizeDelta = new Vector2(Random.Range(14f, 24f), Random.Range(22f, 36f));
                piece.Rect.gameObject.SetActive(true);
            }

            running = true;
        }

        public void Stop()
        {
            running = false;
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i].Rect.gameObject.SetActive(false);
            }
        }

        private void OnDisable() => Stop();

        private void Update()
        {
            if (!running)
            {
                return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            bool anyAlive = false;

            for (int i = 0; i < pieces.Count; i++)
            {
                Piece piece = pieces[i];
                if (!piece.Rect.gameObject.activeSelf)
                {
                    continue;
                }

                piece.Age += dt;
                if (piece.Age >= piece.Life)
                {
                    piece.Rect.gameObject.SetActive(false);
                    continue;
                }

                anyAlive = true;
                piece.Velocity.y += Gravity * dt;
                // Air drag caps the fall so the pieces flutter instead of dropping like stones.
                piece.Velocity *= 1f - Mathf.Clamp01(1.6f * dt);
                piece.Wobble += dt * 7f;

                Vector2 position = piece.Rect.anchoredPosition + piece.Velocity * dt;
                position.x += Mathf.Sin(piece.Wobble) * 60f * dt;
                piece.Rect.anchoredPosition = position;
                piece.Rect.localRotation *= Quaternion.Euler(0f, 0f, piece.Spin * dt);

                float fade = Mathf.Clamp01((piece.Life - piece.Age) / 0.5f);
                Color color = piece.Image.color;
                color.a = fade;
                piece.Image.color = color;
            }

            running = anyAlive;
        }

        private void EnsurePool()
        {
            while (pieces.Count < PieceCount)
            {
                Image image = UIFactory.CreateImage("Piece", root, Color.white, false);
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.gameObject.SetActive(false);
                pieces.Add(new Piece { Rect = rect, Image = image });
            }
        }
    }
}
