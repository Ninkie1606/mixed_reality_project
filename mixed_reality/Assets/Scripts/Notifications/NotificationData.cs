using UnityEngine;

namespace VRSchool.Notifications
{
    /// <summary>
    /// ScriptableObject voor het definiëren van een SMS-notificatie.
    /// Eenvoudig aan te maken in de Unity Inspector via Assets > Create > VRSchool > Notification Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewNotification", menuName = "VRSchool/Notification Data")]
    public class NotificationData : ScriptableObject
    {
        [Header("Afzender")]
        [Tooltip("Naam van de afzender (bijv. 'Mama', 'Lucas', 'Groepschat')")]
        public string sender = "Mama";

        [Tooltip("Kleur van de afzendernaam ter herkenning")]
        public Color senderColor = new Color(0.2f, 0.7f, 1f, 1f);

        [Header("Bericht")]
        [TextArea(2, 5)]
        [Tooltip("De inhoud van het SMS-bericht")]
        public string message = "Vergeet je lunch niet! Veel succes vandaag.";

        [Header("Weergave")]
        [Tooltip("Hoeveel seconden het bericht op het scherm blijft")]
        public float displayDuration = 4.5f;

        [Header("Feedback (Audio & Haptics)")]
        [Tooltip("Optioneel geluidssignaal bij ontvangst")]
        public AudioClip notificationSound;

        [Tooltip("Trilsignaal sturen naar de VR controllers")]
        public bool triggerHaptics = true;

        [Range(0f, 1f)]
        public float hapticAmplitude = 0.4f;

        [Range(0.05f, 1f)]
        public float hapticDuration = 0.2f;
    }
}
