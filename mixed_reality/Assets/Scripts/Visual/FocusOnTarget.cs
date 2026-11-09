using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRSchool.Visual
{
    /// <summary>
    /// Houdt een doel (bv. de schoolingang) scherp en laat alles wat verder ligt vervagen:
    /// de far-blur van Depth of Field (Gaussian) en de lineaire mist beginnen telkens net achter het doel,
    /// gemeten vanaf de camera. Zo blijft het doel helder, waar de speler ook staat.
    /// </summary>
    public class FocusOnTarget : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Volume volume;
        [Tooltip("Afstand achter het doel die nog volledig scherp blijft (m).")]
        [SerializeField] float sharpMargin = 0.5f;
        [Tooltip("Overgang van scherp naar maximaal vaag (m).")]
        [SerializeField] float blurFalloff = 5f;
        [SerializeField] bool driveFog = true;
        [Tooltip("Afstand achter het doel waarover de mist volledig dicht wordt (m).")]
        [SerializeField] float fogFalloff = 25f;

        DepthOfField dof;
        Camera cam;

        void Start()
        {
            // .profile maakt een runtime-kopie: het profiel-asset zelf blijft onaangeroerd.
            if (volume != null)
                volume.profile.TryGet(out dof);
        }

        void LateUpdate()
        {
            if (target == null)
                return;
            if (cam == null || !cam.isActiveAndEnabled)
                cam = Camera.main;
            if (cam == null)
                return;

            var start = Vector3.Distance(cam.transform.position, target.position) + sharpMargin;
            if (dof != null)
            {
                dof.gaussianStart.value = start;
                dof.gaussianEnd.value = start + blurFalloff;
            }
            if (driveFog)
            {
                RenderSettings.fogStartDistance = start;
                RenderSettings.fogEndDistance = start + fogFalloff;
            }
        }
    }
}
