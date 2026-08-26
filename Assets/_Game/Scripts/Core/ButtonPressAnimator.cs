using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Squashes a uGUI <see cref="Button"/> while the pointer is down and springs it
    /// back on release. Uses unscaled time so pause / game-over overlays still feel
    /// the press. Attach once per button; <see cref="AttachAll"/> covers baked prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonPressAnimator : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private const float PressedScale = 0.92f;
        private const float PressDuration = 0.06f;
        private const float ReleaseDuration = 0.12f;

        private Button button;
        private Vector3 restScale = Vector3.one;
        private bool pointerDown;
        private bool visuallyPressed;

        /// <summary>Adds the animator when it is missing, so every creation path can share one call.</summary>
        public static ButtonPressAnimator Attach(Component host)
        {
            if (host == null)
            {
                return null;
            }

            ButtonPressAnimator existing = host.GetComponent<ButtonPressAnimator>();
            return existing != null ? existing : host.gameObject.AddComponent<ButtonPressAnimator>();
        }

        /// <summary>Wires every <see cref="Button"/> under <paramref name="root"/>, including inactive ones.</summary>
        public static void AttachAll(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Attach(buttons[i]);
            }
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            restScale = transform.localScale;
            if (restScale.sqrMagnitude < 0.0001f)
            {
                restScale = Vector3.one;
            }
        }

        private void OnDisable()
        {
            pointerDown = false;
            visuallyPressed = false;
            GameTween.Kill(transform);
            transform.localScale = restScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDown = true;
            if (CanPress())
            {
                SetPressed(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerDown = false;
            SetPressed(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (pointerDown && CanPress())
            {
                SetPressed(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (pointerDown)
            {
                SetPressed(false);
            }
        }

        private bool CanPress()
        {
            return button == null || button.IsInteractable();
        }

        private void SetPressed(bool pressed)
        {
            if (visuallyPressed == pressed)
            {
                return;
            }

            visuallyPressed = pressed;
            GameTween.Kill(transform);
            GameTween.Scale(
                transform,
                pressed ? restScale * PressedScale : restScale,
                pressed ? PressDuration : ReleaseDuration,
                pressed ? TweenEase.OutQuad : TweenEase.OutBack,
                unscaled: true);
        }
    }
}
