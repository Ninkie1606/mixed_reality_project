using System;
using UnityEngine;
using UnityEngine.Events;
using VRSchool.Notifications;

namespace VRSchool.Gameplay
{
    /// <summary>
    /// De schooldag als een rij stappen (scenario: gang → klas → eetzaal → toilet → gang).
    /// Elke stap stuurt bij de start berichten naar de telefoon (het doel: waar moet je naartoe),
    /// stuurt nieuwe berichten zodra de camera in het doelgebied staat, en is klaar als de speler daar
    /// holdSeconds blijft staan (tijdelijk, tot er echte interacties zijn: zitten, deur op slot, bericht sturen).
    /// Een stap met holdSeconds &lt; 0 wacht op CompleteCurrentStep() van een ander script.
    /// Begin() start de dag; in Main doet de Fase 2-overgang dat.
    /// </summary>
    public class SchoolDay : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            public string name;
            [Tooltip("Bij de start van de stap: zegt waar de speler naartoe moet.")]
            public NotificationData[] onStart;
            [Tooltip("Waar de stap gebeurt (BoxCollider, wordt als trigger gezet).")]
            public BoxCollider area;
            [Tooltip("Zodra de speler in het gebied aankomt.")]
            public NotificationData[] onArrive;
            [Tooltip("Seconden in het gebied blijven om de stap af te ronden. Negatief = wachten op CompleteCurrentStep().")]
            public float holdSeconds = 3f;
            public UnityEvent onCompleted;
        }

        [SerializeField] Step[] steps;
        [Tooltip("Na de laatste stap.")]
        [SerializeField] NotificationData[] endMessages;
        [SerializeField] bool beginOnStart;

        public UnityEvent onDayCompleted;

        int index = -1;
        bool arrived;
        float held;
        Camera cam;

        public int CurrentIndex => index;
        public Step Current => index >= 0 && index < steps.Length ? steps[index] : null;
        public bool IsRunning => Current != null;
        public bool IsFinished => index >= steps.Length;

        void Awake()
        {
            foreach (var step in steps)
                if (step.area != null)
                    step.area.isTrigger = true;
        }

        void Start()
        {
            if (beginOnStart)
                Begin();
        }

        /// <summary>Start de dag bij de eerste stap. Doet niets als de dag al loopt of klaar is.</summary>
        public void Begin()
        {
            if (index >= 0)
                return;
            StartStep(0);
        }

        /// <summary>Rondt de huidige stap af (voor echte interacties: gaan zitten, bericht versturen, ...).</summary>
        public void CompleteCurrentStep()
        {
            var step = Current;
            if (step == null)
                return;
            step.onCompleted?.Invoke();
            StartStep(index + 1);
        }

        /// <summary>Toont de doelberichten van de huidige stap opnieuw.</summary>
        public void ReshowGoal()
        {
            if (Current != null)
                Send(Current.onStart);
        }

        void StartStep(int i)
        {
            index = i;
            arrived = false;
            held = 0f;
            if (Current != null)
            {
                Send(Current.onStart);
                return;
            }
            Send(endMessages);
            onDayCompleted?.Invoke();
        }

        void LateUpdate()
        {
            var step = Current;
            if (step == null || step.area == null)
                return;
            if (cam == null || !cam.isActiveAndEnabled)
                cam = Camera.main;
            if (cam == null)
                return;

            if (!step.area.bounds.Contains(cam.transform.position))
            {
                held = 0f;
                return;
            }
            if (!arrived)
            {
                arrived = true;
                Send(step.onArrive);
            }
            if (step.holdSeconds < 0f)
                return;
            held += Time.deltaTime;
            if (held >= step.holdSeconds)
                CompleteCurrentStep();
        }

        static void Send(NotificationData[] messages)
        {
            if (messages == null)
                return;
            foreach (var message in messages)
                if (message != null)
                    NotificationManager.Show(message);
        }
    }
}
