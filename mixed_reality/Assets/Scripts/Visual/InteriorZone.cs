using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRSchool.Visual
{
    /// <summary>
    /// Zet de buiten-focus (Depth of Field + mist van FocusOnTarget) uit zodra de camera binnen een van de BoxColliders
    /// op dit object is, en weer aan buiten. Zonder dit vervaagt de gang na een paar meter, omdat de focus op de ingang blijft staan.
    /// Binnen komt er optioneel donkere lineaire mist in de plaats: die laat de eindeloze gang in het zwart verdwijnen.
    /// Meet de camerapositie, dus werkt zonder Rigidbody of CharacterController.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class InteriorZone : MonoBehaviour
    {
        [SerializeField] FocusOnTarget outdoorFocus;
        [SerializeField] Volume outdoorVolume;

        [Header("Mist binnen")]
        [SerializeField] bool interiorFog = true;
        [SerializeField] Color interiorFogColor = Color.black;
        [Tooltip("Tot hier is alles binnen het gebouw scherp; de hoofdgang is 32 m lang.")]
        [SerializeField] float interiorFogStart = 30f;
        [SerializeField] float interiorFogEnd = 100f;

        BoxCollider[] zones;
        Camera cam;
        bool fogOutside;
        FogMode fogModeOutside;
        Color fogColorOutside;
        float fogStartOutside, fogEndOutside;
        bool? inside;

        public bool IsInside => inside == true;

        void Awake()
        {
            zones = GetComponents<BoxCollider>();
            foreach (var zone in zones)
                zone.isTrigger = true;
            fogOutside = RenderSettings.fog;
            fogModeOutside = RenderSettings.fogMode;
            fogColorOutside = RenderSettings.fogColor;
            fogStartOutside = RenderSettings.fogStartDistance;
            fogEndOutside = RenderSettings.fogEndDistance;
        }

        void LateUpdate()
        {
            if (cam == null || !cam.isActiveAndEnabled)
                cam = Camera.main;
            if (cam == null)
                return;

            var position = cam.transform.position;
            var nowInside = zones.Any(z => z.bounds.Contains(position));
            if (nowInside == inside)
                return;
            inside = nowInside;
            if (outdoorFocus != null)
                outdoorFocus.enabled = !nowInside;
            if (outdoorVolume != null)
                outdoorVolume.weight = nowInside ? 0f : 1f;

            if (nowInside && interiorFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = interiorFogColor;
                RenderSettings.fogStartDistance = interiorFogStart;
                RenderSettings.fogEndDistance = interiorFogEnd;
            }
            else
            {
                // FocusOnTarget zet start/eind buiten elk frame opnieuw; kleur en modus komen hier terug.
                RenderSettings.fog = !nowInside && fogOutside;
                RenderSettings.fogMode = fogModeOutside;
                RenderSettings.fogColor = fogColorOutside;
                RenderSettings.fogStartDistance = fogStartOutside;
                RenderSettings.fogEndDistance = fogEndOutside;
            }
        }
    }
}
