using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VRSchool.Interaction;
using VRSchool.Visual;

namespace VRSchool.Gameplay
{
    /// <summary>
    /// Fase 2, de overgang: zodra de camera voorbij de voordeur in de inkomhal staat, slaat de voordeur dicht en gaat op slot
    /// (de deurbladen reageren niet meer op de controllers), en slaat het licht in één klap om van zonnig naar donker:
    /// zon en buitenlampen uit, zwarte lucht, donker omgevingslicht en de zwarte mist van InteriorZone overal.
    /// Gebeurt één keer. Geluid (deur, buitengeluid weg) hangt later aan onTriggered.
    /// Meet de camerapositie zoals InteriorZone, dus werkt zonder Rigidbody of CharacterController.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Phase2Transition : MonoBehaviour
    {
        [SerializeField] SwingDoor[] frontDoors;
        [Tooltip("Zon, lampen aan de ingang, lantaarnpalen: gaan uit.")]
        [SerializeField] Light[] outdoorLights;
        [SerializeField] InteriorZone interiorZone;

        [Header("Donker")]
        [Tooltip("Zonder gebakken licht is dit het enige licht binnen: donker, maar de gang blijft te zien.")]
        [SerializeField] Color ambientColor = new(0.2f, 0.21f, 0.25f);
        [SerializeField] Color skyColor = Color.black;
        [SerializeField, Range(0f, 1f)] float reflectionIntensity = 0.1f;

        [Tooltip("Voor later: geluid van de dichtslaande deur, buitengeluid uit, drone aan.")]
        public UnityEvent onTriggered;

        BoxCollider area;
        Camera cam;

        public bool Triggered { get; private set; }

        void Awake()
        {
            area = GetComponent<BoxCollider>();
            area.isTrigger = true;
        }

        void LateUpdate()
        {
            if (Triggered)
                return;
            if (cam == null || !cam.isActiveAndEnabled)
                cam = Camera.main;
            if (cam != null && area.bounds.Contains(cam.transform.position))
                Trigger();
        }

        public void Trigger()
        {
            if (Triggered)
                return;
            Triggered = true;

            foreach (var door in frontDoors)
            {
                if (door == null)
                    continue;
                door.SetOpen(false);
                foreach (var interactable in door.GetComponentsInChildren<XRBaseInteractable>())
                    interactable.enabled = false;
            }

            foreach (var light in outdoorLights)
                if (light != null)
                    light.enabled = false;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            foreach (var camera in Camera.allCameras)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = skyColor;
            }
            DynamicGI.UpdateEnvironment();

            if (interiorZone != null)
                interiorZone.ForceInside();

            onTriggered?.Invoke();
        }
    }
}
