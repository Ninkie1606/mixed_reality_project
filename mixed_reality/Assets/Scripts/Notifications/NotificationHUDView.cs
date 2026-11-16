using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.XR;

namespace VRSchool.Notifications
{
    /// <summary>
    /// Verzorgt de weergave, animatie (fade + slide), audio en controller-haptics van een SMS-notificatie.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class NotificationHUDView : MonoBehaviour
    {
        [Header("UI Elementen")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelTransform;
        [SerializeField] private TextMeshProUGUI senderText;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip defaultSound;

        [Header("Animatie-instellingen")]
        [Tooltip("Duur van de in- en uitfade in seconden")]
        [SerializeField] private float fadeDuration = 0.35f;

        [Tooltip("Verticale verschuiving in pixels tijdens de slide-animatie")]
        [SerializeField] private float slideOffset = 25f;

        private LazyFollowHUD lazyFollow;
        private Vector2 originalAnchoredPosition;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            lazyFollow = GetComponentInParent<LazyFollowHUD>();

            if (panelTransform != null)
            {
                originalAnchoredPosition = panelTransform.anchoredPosition;
            }
            else
            {
                panelTransform = GetComponent<RectTransform>();
                if (panelTransform != null) originalAnchoredPosition = panelTransform.anchoredPosition;
            }

            // Begin volledig onzichtbaar
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// Coroutine die het hele weergavetraject van een notificatie doorloopt.
        /// </summary>
        public IEnumerator DisplayNotificationRoutine(NotificationData data)
        {
            if (data == null) yield break;

            // Zorg dat het HUD-element direct op de juiste plek voor de speler staat
            if (lazyFollow != null)
            {
                lazyFollow.SnapToTarget();
            }

            // Data koppelen aan UI
            if (senderText != null)
            {
                senderText.text = data.sender;
                senderText.color = data.senderColor;
            }

            if (messageText != null)
            {
                messageText.text = data.message;
            }

            // Audio afspelen
            AudioClip clip = data.notificationSound != null ? data.notificationSound : defaultSound;
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }

            // Haptische trilling op VR controllers
            if (data.triggerHaptics)
            {
                SendHaptics(data.hapticAmplitude, data.hapticDuration);
            }

            // Fade + Slide In
            yield return AnimateFade(0f, 1f, -slideOffset, 0f, fadeDuration);

            // Wachten zolang het bericht getoond moet worden
            yield return new WaitForSeconds(data.displayDuration);

            // Fade + Slide Out
            yield return AnimateFade(1f, 0f, 0f, slideOffset, fadeDuration);
        }

        private IEnumerator AnimateFade(float startAlpha, float targetAlpha, float startOffsetY, float targetOffsetY, float duration)
        {
            float elapsed = 0f;
            Vector2 basePos = originalAnchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
                }

                if (panelTransform != null)
                {
                    float currentOffsetY = Mathf.Lerp(startOffsetY, targetOffsetY, smoothT);
                    panelTransform.anchoredPosition = new Vector2(basePos.x, basePos.y + currentOffsetY);
                }

                yield return null;
            }

            if (canvasGroup != null) canvasGroup.alpha = targetAlpha;
            if (panelTransform != null) panelTransform.anchoredPosition = basePos;
        }

        private void SendHaptics(float amplitude, float duration)
        {
            TriggerNodeHaptics(XRNode.RightHand, amplitude, duration);
            TriggerNodeHaptics(XRNode.LeftHand, amplitude, duration);
        }

        private void TriggerNodeHaptics(XRNode node, float amplitude, float duration)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities capabilities))
            {
                if (capabilities.supportsImpulse)
                {
                    device.SendHapticImpulse(0u, amplitude, duration);
                }
            }
        }
    }
}
