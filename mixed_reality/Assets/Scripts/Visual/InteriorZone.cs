using UnityEngine;
using UnityEngine.Rendering;

namespace VRSchool.Visual
{
    /// <summary>
    /// Zet de buiten-focus (Depth of Field + mist van FocusOnTarget) uit zodra de camera binnen de box van deze collider is,
    /// en weer aan buiten. Zonder dit vervaagt de gang na een paar meter, omdat de focus op de ingang blijft staan.
    /// Meet de camerapositie, dus werkt zonder Rigidbody of CharacterController.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class InteriorZone : MonoBehaviour
    {
        [SerializeField] FocusOnTarget outdoorFocus;
        [SerializeField] Volume outdoorVolume;

        BoxCollider zone;
        Camera cam;
        bool fogOutside;
        bool? inside;

        public bool IsInside => inside == true;

        void Awake()
        {
            zone = GetComponent<BoxCollider>();
            zone.isTrigger = true;
            fogOutside = RenderSettings.fog;
        }

        void LateUpdate()
        {
            if (cam == null || !cam.isActiveAndEnabled)
                cam = Camera.main;
            if (cam == null)
                return;

            var nowInside = zone.bounds.Contains(cam.transform.position);
            if (nowInside == inside)
                return;
            inside = nowInside;
            if (outdoorFocus != null)
                outdoorFocus.enabled = !nowInside;
            if (outdoorVolume != null)
                outdoorVolume.weight = nowInside ? 0f : 1f;
            RenderSettings.fog = !nowInside && fogOutside;
        }
    }
}
