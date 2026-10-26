using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRSchool.Interaction
{
    /// <summary>
    /// Draait een deur rond zijn pivot (het scharnier) open of dicht.
    /// Elke XR-interactable in de deur (klink, paniekbalk, het blad zelf) schakelt de deur bij select.
    /// </summary>
    public class SwingDoor : MonoBehaviour
    {
        [Tooltip("Openingshoek in graden rond de lokale Y-as. Negatief = andere kant op.")]
        [SerializeField] float openAngle = 95f;
        [Tooltip("Draaisnelheid in graden per seconde.")]
        [SerializeField] float speed = 160f;
        [SerializeField] bool startOpen;

        Quaternion closedRotation;
        float current;
        float target;
        XRBaseInteractable[] interactables;

        public bool IsOpen => !Mathf.Approximately(target, 0f);

        void Awake()
        {
            closedRotation = transform.localRotation;
            current = target = startOpen ? openAngle : 0f;
            Apply();
        }

        void OnEnable()
        {
            interactables = GetComponentsInChildren<XRBaseInteractable>();
            foreach (var interactable in interactables)
                interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDisable()
        {
            foreach (var interactable in interactables)
                interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnSelectEntered(SelectEnterEventArgs args) => Toggle();

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open) => target = open ? openAngle : 0f;

        void Update()
        {
            if (Mathf.Approximately(current, target))
                return;
            current = Mathf.MoveTowards(current, target, speed * Time.deltaTime);
            Apply();
        }

        void Apply() => transform.localRotation = closedRotation * Quaternion.Euler(0f, current, 0f);
    }
}
