using System.Collections;
using UnityEngine;

namespace VRSchool.Notifications
{
    /// <summary>
    /// Plak dit component op een GameObject met een BoxCollider (Is Trigger = true)
    /// om een SMS te activeren wanneer de speler erdoorheen loopt.
    /// Kan ook handmatig aangeroepen worden via TriggerManually().
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NotificationTrigger : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("De te tonen SMS-notificatie")]
        [SerializeField] private NotificationData notification;

        [Header("Trigger Instellingen")]
        [Tooltip("Activeer automatisch bij het binnengaan van de trigger")]
        [SerializeField] private bool triggerOnEnter = true;

        [Tooltip("Slechts eenmalig afspelen tijdens de ervaring")]
        [SerializeField] private bool oneShot = true;

        [Tooltip("Optionele vertraging in seconden alvorens de SMS verschijnt")]
        [SerializeField] private float delay = 0.5f;

        private bool hasTriggered = false;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnEnter || (oneShot && hasTriggered)) return;

            // Controleer of de collider bij de speler hoort
            if (IsPlayer(other))
            {
                hasTriggered = true;
                StartCoroutine(TriggerRoutine());
            }
        }

        /// <summary>
        /// Handmatig aanroepen via UnityEvent, deur-interactie of ander script.
        /// </summary>
        public void TriggerManually()
        {
            if (oneShot && hasTriggered) return;
            hasTriggered = true;
            StartCoroutine(TriggerRoutine());
        }

        private IEnumerator TriggerRoutine()
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (notification != null)
            {
                NotificationManager.Show(notification);
            }
            else
            {
                Debug.LogWarning($"[NotificationTrigger] Geen NotificationData gekoppeld aan {gameObject.name}!");
            }
        }

        private bool IsPlayer(Collider col)
        {
            if (col.CompareTag("Player") || col.CompareTag("MainCamera")) return true;
            if (col.GetComponentInParent<Camera>() != null) return true;
            return false;
        }
    }
}
