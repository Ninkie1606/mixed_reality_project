using UnityEngine;

namespace VRSchool.Notifications
{
    /// <summary>
    /// Zorgt dat het notificatie-canvas zachtjes (lazy follow) meebeweegt
    /// met het blikveld van de speler in VR. Voorkomt motion sickness door
    /// niet stijf vast te pinnen aan de camera, maar zacht te dempen (lerp/slerp).
    /// </summary>
    public class LazyFollowHUD : MonoBehaviour
    {
        [Header("Camera Referentie")]
        [Tooltip("Indien leeg wordt Camera.main automatisch gebruikt.")]
        [SerializeField] private Transform targetCamera;

        [Header("Positie in VR")]
        [Tooltip("Afstand in meters voor de camera (aanbevolen: 1.1m tot 1.4m)")]
        [SerializeField] private float distance = 1.25f;

        [Tooltip("Verticale verschuiving (negatief = iets onder de horizon, comfortabel in VR)")]
        [SerializeField] private float heightOffset = -0.22f;

        [Tooltip("Horizontale verschuiving (positief = subtiel rechts in het perifere zicht)")]
        [SerializeField] private float lateralOffset = 0.15f;

        [Header("Vloeiendheid")]
        [Tooltip("Volgsnelheid voor positie (hoger = responsiever)")]
        [SerializeField] private float followSpeed = 6f;

        [Tooltip("Draaisnelheid voor rotatie")]
        [SerializeField] private float rotationSpeed = 6f;

        private void Start()
        {
            FindCameraIfNull();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            FindCameraIfNull();
            if (targetCamera == null) return;

            Vector3 targetPos = targetCamera.position
                              + (targetCamera.forward * distance)
                              + (targetCamera.up * heightOffset)
                              + (targetCamera.right * lateralOffset);

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

            Vector3 lookDir = transform.position - targetCamera.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, targetCamera.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
        }

        /// <summary>
        /// Plaatst het HUD-element direct op de doelpositie voor de speler (zonder vlieganimatie).
        /// </summary>
        public void SnapToTarget()
        {
            FindCameraIfNull();
            if (targetCamera == null) return;

            transform.position = targetCamera.position
                               + (targetCamera.forward * distance)
                               + (targetCamera.up * heightOffset)
                               + (targetCamera.right * lateralOffset);

            Vector3 lookDir = transform.position - targetCamera.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir, targetCamera.up);
            }
        }

        private void FindCameraIfNull()
        {
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }
        }
    }
}
