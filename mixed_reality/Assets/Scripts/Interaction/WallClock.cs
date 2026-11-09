using System;
using UnityEngine;

namespace VRSchool.Interaction
{
    /// <summary>
    /// Zet de wijzers van SM_Class_WallClock op de systeemtijd (of een vaste tijd).
    /// Wijzers zijn children met "HandHour", "HandMinute" en "HandSecond" in hun naam; 12 uur = lokale +Y.
    /// </summary>
    public class WallClock : MonoBehaviour
    {
        [Tooltip("Leeg = systeemtijd. Anders vaste tijd, bv. 08:25 (handig voor een scene die 'stilstaat').")]
        [SerializeField] string fixedTime = "";
        [SerializeField] bool smoothSeconds;
        [Tooltip("+1 of -1: draaizin rond de lokale Z-as. +1 = met de klok mee voor SM_Class_WallClock (getest: 3 uur wijst naar rechts).")]
        [SerializeField] float direction = 1f;

        Transform hourHand, minuteHand, secondHand;
        Quaternion hour0, minute0, second0;

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name.Contains("HandHour")) hourHand = t;
                else if (t.name.Contains("HandMinute")) minuteHand = t;
                else if (t.name.Contains("HandSecond")) secondHand = t;
            }
            if (hourHand) hour0 = hourHand.localRotation;
            if (minuteHand) minute0 = minuteHand.localRotation;
            if (secondHand) second0 = secondHand.localRotation;
        }

        void Update() => Apply(CurrentTime());

        TimeSpan CurrentTime() =>
            TimeSpan.TryParse(fixedTime, out var t) && fixedTime.Length > 0 ? t : DateTime.Now.TimeOfDay;

        public void Apply(TimeSpan time)
        {
            var seconds = smoothSeconds ? (float)(time.TotalSeconds % 60.0) : time.Seconds;
            Set(hourHand, hour0, (float)(time.TotalHours % 12.0) * 30f);
            Set(minuteHand, minute0, (float)(time.TotalMinutes % 60.0) * 6f);
            Set(secondHand, second0, seconds * 6f);
        }

        void Set(Transform hand, Quaternion rest, float degrees)
        {
            if (hand)
                hand.localRotation = rest * Quaternion.AngleAxis(degrees * direction, Vector3.forward);
        }
    }
}
