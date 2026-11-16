#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using VRSchool.Notifications;

namespace VRSchool.Editor
{
    public static class NotificationSampleCreator
    {
        private const string TargetFolder = "Assets/_Project/Data/Notifications";

        [MenuItem("Tools/VRSchool/Maak Voorbeeld SMS Notificaties Aan")]
        public static void CreateSampleNotifications()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            }
            if (!AssetDatabase.IsValidFolder(TargetFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Notifications");
            }

            CreateOrUpdateAsset("SMS_Fase1_Mama_Ochtend", "Mama", new Color(0.3f, 0.7f, 1f), "Vergeet je lunchpakket niet! Veel succes vandaag op school x", 4.5f);
            CreateOrUpdateAsset("SMS_Fase2_Lucas_Gang", "Lucas", new Color(0.4f, 0.9f, 0.5f), "Waar zit je? De bel is al gegaan, les begint!", 4f);
            CreateOrUpdateAsset("SMS_Fase3_Groepswerk_Stress", "Klasgroep", new Color(1f, 0.45f, 0.35f), "Heb je jouw deel van de presentatie al af? We wachten nog steeds...", 5f);
            CreateOrUpdateAsset("SMS_Fase3_Mama_Zorgen", "Mama", new Color(0.8f, 0.6f, 1f), "Alles wel goed lieverd? Je leek zo afwezig vanochtend. Laat iets weten.", 5f);
            CreateOrUpdateAsset("SMS_Fase5_Lichtpunt_Steun", "Vriend(in)", new Color(1f, 0.85f, 0.4f), "Geen stress voor vandaag. Laat maar weten als je even wilt praten, er is geen haast.", 5.5f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VRSchool] 5 voorbeeld SMS-notificaties succesvol gegenereerd in {TargetFolder}!");
        }

        private static void CreateOrUpdateAsset(string assetName, string sender, Color color, string message, float duration)
        {
            string path = $"{TargetFolder}/{assetName}.asset";
            NotificationData data = AssetDatabase.LoadAssetAtPath<NotificationData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<NotificationData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.sender = sender;
            data.senderColor = color;
            data.message = message;
            data.displayDuration = duration;
            data.triggerHaptics = true;
            data.hapticAmplitude = 0.45f;
            data.hapticDuration = 0.2f;

            EditorUtility.SetDirty(data);
        }
    }
}
#endif
