using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRSchool.Notifications
{
    /// <summary>
    /// Centrale beheerder voor SMS-notificaties.
    /// Beheert een wachtrij zodat berichten netjes achter elkaar worden getoond.
    /// Biedt tevens een statische hulpmethode: NotificationManager.Show(data).
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }

        [Header("Weergave Component")]
        [Tooltip("Referentie naar de NotificationHUDView.")]
        [SerializeField] private NotificationHUDView hudView;

        [Header("Editor Testen")]
        [Tooltip("Optionele test-notificatie voor snelle controle in de Editor via de Inspector context menu.")]
        [SerializeField] private NotificationData testNotification;

        private readonly Queue<NotificationData> queue = new Queue<NotificationData>();
        private bool isDisplaying = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (hudView == null)
            {
                hudView = GetComponentInChildren<NotificationHUDView>(true);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Statische hulpmethode om overal vanuit code of triggers een notificatie te tonen.
        /// Voorbeeld: NotificationManager.Show(mijnSmsData);
        /// </summary>
        public static void Show(NotificationData data)
        {
            if (Instance != null)
            {
                Instance.EnqueueNotification(data);
            }
            else
            {
                Debug.LogWarning("[NotificationManager] Geen actieve NotificationManager in de scene gevonden!");
            }
        }

        /// <summary>
        /// Plaatst een notificatie in de wachtrij.
        /// </summary>
        public void EnqueueNotification(NotificationData data)
        {
            if (data == null) return;

            queue.Enqueue(data);

            if (!isDisplaying)
            {
                StartCoroutine(ProcessQueueRoutine());
            }
        }

        private IEnumerator ProcessQueueRoutine()
        {
            isDisplaying = true;

            while (queue.Count > 0)
            {
                NotificationData current = queue.Dequeue();

                if (hudView != null)
                {
                    yield return hudView.DisplayNotificationRoutine(current);
                }
                else
                {
                    Debug.Log($"[Notification SMS] {current.sender}: {current.message}");
                    yield return new WaitForSeconds(current.displayDuration);
                }

                // Korte rustpauze tussen twee berichten
                if (queue.Count > 0)
                {
                    yield return new WaitForSeconds(0.4f);
                }
            }

            isDisplaying = false;
        }

        [ContextMenu("Test Notificatie Afspelen")]
        public void TestNotificationInEditor()
        {
            if (testNotification != null)
            {
                EnqueueNotification(testNotification);
            }
            else
            {
                var dummy = ScriptableObject.CreateInstance<NotificationData>();
                dummy.sender = "Mama";
                dummy.message = "Vergeet je lunch niet! Veel succes vandaag.";
                dummy.displayDuration = 3.5f;
                EnqueueNotification(dummy);
            }
        }
    }
}
