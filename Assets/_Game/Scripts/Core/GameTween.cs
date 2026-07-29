using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif

namespace BlockPuzzle.Core
{
    /// <summary>Easing curves the game animates with, mapped onto DOTween's own set.</summary>
    public enum TweenEase
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        OutCubic,
        InBack,
        OutBack
    }

    /// <summary>
    /// Every animation in the game goes through this class, and it hands the work to
    /// DOTween (free edition) whenever DOTween is present in the project. Import DOTween
    /// and <c>DOTweenSetup</c> defines the <c>DOTWEEN</c> symbol, which switches all of
    /// the calls below onto real tweens. Without DOTween the same calls fall back to
    /// coroutines, so a fresh clone still runs and looks identical — no call site ever
    /// has to know which of the two is driving it.
    /// </summary>
    public static class GameTween
    {
        /// <summary>True when the animations are being driven by DOTween.</summary>
#if DOTWEEN
        public const bool UsesDoTween = true;
#else
        public const bool UsesDoTween = false;
#endif

        /// <summary>
        /// Prepares the tween engine, called once from the bootstrap. A full board clear can
        /// put a few hundred tweens in flight at once, so the pool is grown up front instead
        /// of letting DOTween resize it mid-effect.
        /// </summary>
        public static void Initialize()
        {
#if DOTWEEN
            DOTween.SetTweensCapacity(600, 80);
#endif
        }

        /// <summary>Stops every animation running on <paramref name="target"/>.</summary>
        public static void Kill(object target)
        {
            if (target == null)
            {
                return;
            }

#if DOTWEEN
            DOTween.Kill(target);
#else
            TweenRunner.KillTarget(target);
#endif
        }

        public static void Scale(
            Transform target,
            Vector3 to,
            float duration,
            TweenEase ease = TweenEase.OutQuad,
            float delay = 0f,
            bool unscaled = false,
            Action onComplete = null)
        {
            if (target == null)
            {
                return;
            }

            if (!CanAnimate(duration))
            {
                target.localScale = to;
                onComplete?.Invoke();
                return;
            }

#if DOTWEEN
            target.DOScale(to, duration)
                .SetEase(Convert(ease))
                .SetDelay(delay)
                .SetUpdate(unscaled)
                .OnComplete(() => onComplete?.Invoke());
#else
            Vector3 from = target.localScale;
            TweenRunner.Run(target, target, duration, delay, unscaled, ease,
                t => target.localScale = Vector3.LerpUnclamped(from, to, t), onComplete);
#endif
        }

        /// <summary>Quick "pop" of a rect: overshoot the scale and settle back to normal.</summary>
        public static void Punch(Transform target, float strength, float duration, bool unscaled = false)
        {
            if (target == null)
            {
                return;
            }

            if (!CanAnimate(duration))
            {
                target.localScale = Vector3.one;
                return;
            }

            Kill(target);

#if DOTWEEN
            target.localScale = Vector3.one;
            target.DOPunchScale(Vector3.one * strength, duration, 6, 0.9f)
                .SetUpdate(unscaled)
                .OnComplete(() => target.localScale = Vector3.one);
#else
            TweenRunner.Run(target, target, duration, 0f, unscaled, TweenEase.Linear,
                t => target.localScale = Vector3.one * (1f + strength * Mathf.Sin(Mathf.PI * t)),
                () => target.localScale = Vector3.one);
#endif
        }

        public static void MoveAnchored(
            RectTransform target,
            Vector2 to,
            float duration,
            TweenEase ease = TweenEase.OutQuad,
            float delay = 0f,
            bool unscaled = false,
            Action onComplete = null)
        {
            if (target == null)
            {
                return;
            }

            if (!CanAnimate(duration))
            {
                target.anchoredPosition = to;
                onComplete?.Invoke();
                return;
            }

#if DOTWEEN
            target.DOAnchorPos(to, duration)
                .SetEase(Convert(ease))
                .SetDelay(delay)
                .SetUpdate(unscaled)
                .OnComplete(() => onComplete?.Invoke());
#else
            Vector2 from = target.anchoredPosition;
            TweenRunner.Run(target, target, duration, delay, unscaled, ease,
                t => target.anchoredPosition = Vector2.LerpUnclamped(from, to, t), onComplete);
#endif
        }

        public static void Fade(
            CanvasGroup target,
            float to,
            float duration,
            TweenEase ease = TweenEase.OutQuad,
            float delay = 0f,
            bool unscaled = false,
            Action onComplete = null)
        {
            if (target == null)
            {
                return;
            }

            if (!CanAnimate(duration))
            {
                target.alpha = to;
                onComplete?.Invoke();
                return;
            }

#if DOTWEEN
            target.DOFade(to, duration)
                .SetEase(Convert(ease))
                .SetDelay(delay)
                .SetUpdate(unscaled)
                .OnComplete(() => onComplete?.Invoke());
#else
            float from = target.alpha;
            TweenRunner.Run(target, target, duration, delay, unscaled, ease,
                t => target.alpha = Mathf.LerpUnclamped(from, to, t), onComplete);
#endif
        }

        public static void Fade(
            Graphic target,
            float to,
            float duration,
            TweenEase ease = TweenEase.OutQuad,
            float delay = 0f,
            bool unscaled = false,
            Action onComplete = null)
        {
            if (target == null)
            {
                return;
            }

            Color destination = target.color;
            destination.a = to;
            Tint(target, destination, duration, ease, delay, unscaled, onComplete);
        }

        public static void Tint(
            Graphic target,
            Color to,
            float duration,
            TweenEase ease = TweenEase.OutQuad,
            float delay = 0f,
            bool unscaled = false,
            Action onComplete = null)
        {
            if (target == null)
            {
                return;
            }

            if (!CanAnimate(duration))
            {
                target.color = to;
                onComplete?.Invoke();
                return;
            }

#if DOTWEEN
            target.DOColor(to, duration)
                .SetEase(Convert(ease))
                .SetDelay(delay)
                .SetUpdate(unscaled)
                .OnComplete(() => onComplete?.Invoke());
#else
            Color from = target.color;
            TweenRunner.Run(target, target, duration, delay, unscaled, ease,
                t => target.color = Color.LerpUnclamped(from, to, t), onComplete);
#endif
        }

        /// <summary>Runs <paramref name="action"/> after <paramref name="delay"/> seconds.</summary>
        public static void Delay(object owner, float delay, bool unscaled, Action action)
        {
            if (action == null)
            {
                return;
            }

            if (!CanAnimate(delay))
            {
                action();
                return;
            }

#if DOTWEEN
            DOVirtual.DelayedCall(delay, () => action(), unscaled).SetId(owner ?? action);
#else
            TweenRunner.Run(owner ?? action, null, 0f, delay, unscaled, TweenEase.Linear, null, action);
#endif
        }

        /// <summary>
        /// Outside play mode there is nothing to animate: the scene factory builds the
        /// hierarchy in the editor, so every animated value is applied instantly instead.
        /// </summary>
        private static bool CanAnimate(float duration) => Application.isPlaying && duration > 0f;

#if DOTWEEN
        private static Ease Convert(TweenEase ease)
        {
            switch (ease)
            {
                case TweenEase.Linear: return Ease.Linear;
                case TweenEase.InQuad: return Ease.InQuad;
                case TweenEase.InOutQuad: return Ease.InOutQuad;
                case TweenEase.OutCubic: return Ease.OutCubic;
                case TweenEase.InBack: return Ease.InBack;
                case TweenEase.OutBack: return Ease.OutBack;
                default: return Ease.OutQuad;
            }
        }
#else
        /// <summary>
        /// Stand-in for DOTween while it is not installed: one hidden object runs every
        /// animation as a coroutine and keeps them grouped by target so that
        /// <see cref="GameTween.Kill"/> can cancel them the same way DOTween does.
        /// </summary>
        private sealed class TweenRunner : MonoBehaviour
        {
            private static TweenRunner instance;

            private readonly Dictionary<object, List<Coroutine>> byTarget =
                new Dictionary<object, List<Coroutine>>();

            public static void Run(
                object id,
                UnityEngine.Object target,
                float duration,
                float delay,
                bool unscaled,
                TweenEase ease,
                Action<float> apply,
                Action onComplete)
            {
                TweenRunner runner = Instance;
                if (runner == null)
                {
                    apply?.Invoke(1f);
                    onComplete?.Invoke();
                    return;
                }

                Coroutine routine = runner.StartCoroutine(
                    runner.Animate(id, target, duration, delay, unscaled, ease, apply, onComplete));

                if (!runner.byTarget.TryGetValue(id, out List<Coroutine> list))
                {
                    list = new List<Coroutine>();
                    runner.byTarget[id] = list;
                }

                list.Add(routine);
            }

            public static void KillTarget(object id)
            {
                if (instance == null || !instance.byTarget.TryGetValue(id, out List<Coroutine> list))
                {
                    return;
                }

                foreach (Coroutine routine in list)
                {
                    if (routine != null)
                    {
                        instance.StopCoroutine(routine);
                    }
                }

                instance.byTarget.Remove(id);
            }

            private static TweenRunner Instance
            {
                get
                {
                    if (instance != null || !Application.isPlaying)
                    {
                        return instance;
                    }

                    var go = new GameObject("[Tween]") { hideFlags = HideFlags.HideAndDontSave };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<TweenRunner>();
                    return instance;
                }
            }

            private IEnumerator Animate(
                object id,
                UnityEngine.Object target,
                float duration,
                float delay,
                bool unscaled,
                TweenEase ease,
                Action<float> apply,
                Action onComplete)
            {
                float waited = 0f;
                while (waited < delay)
                {
                    waited += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    // A destroyed target simply ends the animation, mirroring DOTween's safe mode.
                    if (apply != null && target == null)
                    {
                        yield break;
                    }

                    elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                    apply?.Invoke(Evaluate(ease, Mathf.Clamp01(elapsed / duration)));
                    yield return null;
                }

                if (apply != null && target == null)
                {
                    yield break;
                }

                apply?.Invoke(1f);
                byTarget.Remove(id);
                onComplete?.Invoke();
            }

            private static float Evaluate(TweenEase ease, float t)
            {
                const float back = 1.70158f;

                switch (ease)
                {
                    case TweenEase.Linear: return t;
                    case TweenEase.InQuad: return t * t;
                    case TweenEase.InOutQuad:
                        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                    case TweenEase.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                    case TweenEase.InBack: return (back + 1f) * t * t * t - back * t * t;
                    case TweenEase.OutBack:
                        float p = t - 1f;
                        return 1f + (back + 1f) * p * p * p + back * p * p;
                    default: return 1f - (1f - t) * (1f - t);
                }
            }
        }
#endif
    }
}
