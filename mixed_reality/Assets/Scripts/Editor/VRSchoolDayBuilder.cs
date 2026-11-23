using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using VRSchool.Gameplay;
using VRSchool.Notifications;

namespace VRSchool.Editor
{
    /// <summary>
    /// Zet de schooldag (scenario "Mixed Reality - Ideeën") in Main: SchoolDay met 4 stappen
    /// (klas → eetzaal → toilet → gang), de berichten als NotificationData en een telefoon-HUD met de
    /// notificatie-componenten (NotificationManager + NotificationHUDView + LazyFollowHUD).
    /// Berichten in Assets/_Project/Data/SchoolDay worden alleen aangemaakt als ze nog niet bestaan:
    /// tekst die het team in de Inspector aanpast, blijft bij een rebuild staan.
    /// Teksten zonder bron in het scenario (Agenda, enkele gedachten) zijn voorlopig en mogen vervangen worden.
    /// </summary>
    public static class VRSchoolDayBuilder
    {
        const string Folder = "Assets/_Project/Data/SchoolDay";

        static readonly Color Mama = new(1f, 0.55f, 0.7f);
        static readonly Color Friend = new(0.4f, 0.9f, 0.5f);
        static readonly Color Classmate = new(1f, 0.8f, 0.35f);
        static readonly Color Agenda = new(0.7f, 0.7f, 0.75f);
        static readonly Color Thought = new(0.6f, 0.75f, 0.95f);

        public static void Add(Transform systems, StringBuilder log)
        {
            EnsureFolder();
            AddPhoneHud(systems);

            var go = new GameObject("SchoolDay");
            go.transform.SetParent(systems, false);
            var areas = new GameObject("Areas").transform;
            areas.SetParent(go.transform, false);
            var day = go.AddComponent<SchoolDay>();

            var so = new SerializedObject(day);
            var steps = so.FindProperty("steps");
            steps.arraySize = 4;

            // Scène 1 + 2: aankomen en naar de klas, daar de les uitzitten
            SetStep(steps.GetArrayElementAtIndex(0), "Klas",
                new[]
                {
                    Msg("1_Gedachte_Moe", "Gedachte", Thought, "Ik ben nu al moe en de dag is nog maar net begonnen."),
                    Msg("1_Mama_Ochtend", "Mama", Mama, "Ben je goed opgestaan? Vergeet niet iets te eten, hé."),   // geen emoji: LiberationSans heeft ze niet
                    Msg("1_Lucas_WaarBlijfJe", "Lucas", Friend, "Bro, waar blijf je? We zijn al op school."),
                    Msg("1_Agenda_Les", "Agenda", Agenda, "08:30 · Les in lokaal A"),
                },
                Area(areas, "Area_Klas_Bank", new Vector3(-4.5f, 0f, 20.65f), new Vector2(0.9f, 0.9f)),
                new[]
                {
                    Msg("2_Klasgenoot_Opdracht", "Klasgenoot", Classmate, "Heb jij de opdracht begrepen?"),
                    Msg("2_Gedachte_Uitleg", "Gedachte", Thought, "Ik heb de uitleg gehoord, maar ik weet niet meer wat er gezegd is."),
                },
                20f);

            // Scène 3: middagpauze, alleen aan een lege tafel
            SetStep(steps.GetArrayElementAtIndex(1), "Eetzaal",
                new[]
                {
                    Msg("3_Agenda_Middag", "Agenda", Agenda, "12:05 · Middagpauze, refter"),
                    Msg("3_Lucas_KomZitten", "Lucas", Friend, "Kom je bij ons zitten?"),
                },
                Area(areas, "Area_Eetzaal_LegeTafel", new Vector3(-14.4f, 0f, 39.7f), new Vector2(2.4f, 3f)),
                new[]
                {
                    Msg("3_Lucas_Anders", "Lucas", Friend, "Je bent de laatste tijd precies anders. Is er iets?"),
                    Msg("3_Gedachte_Alleen", "Gedachte", Thought, "Zelfs wanneer ik tussen mensen zit, voel ik mij alleen."),
                },
                15f);

            // Scène 4: toilethokje
            SetStep(steps.GetArrayElementAtIndex(2), "Toilet",
                new[]
                {
                    Msg("4_Gedachte_Weg", "Gedachte", Thought, "Ik moet hier even weg."),
                },
                Area(areas, "Area_Toilet_Hokje", new Vector3(-5.0f, 0f, 37.9f), new Vector2(1.3f, 0.85f)),
                new[]
                {
                    Msg("4_Mama_HoeGaatHet", "Mama", Mama, "Hoe gaat het op school, lieverd?"),
                    Msg("4_Mama_Stil", "Mama", Mama, "Je bent de laatste tijd zo stil. Is er iets dat je dwarszit?"),
                    Msg("4_Gedachte_Aanstellen", "Gedachte", Thought, "Misschien stel ik mij gewoon aan."),
                },
                15f);

            // Scène 5: de gang zonder uitgang; vasthouden staat voorlopig voor "het eerlijke bericht sturen"
            SetStep(steps.GetArrayElementAtIndex(3), "Gang",
                new[]
                {
                    Msg("5_Agenda_Einde", "Agenda", Agenda, "15:40 · Einde lesdag"),
                    Msg("5_Lucas_Voetbal", "Lucas", Friend, "Ga je straks mee voetballen?"),
                    Msg("5_Lucas_Missen", "Lucas", Friend, "Je zegt tegenwoordig altijd nee. We missen je, man."),
                },
                Area(areas, "Area_Gang_Oost", new Vector3(13.5f, 0f, 28.1f), new Vector2(3f, 4f)),
                new[]
                {
                    Msg("5_Gedachte_NietAlleen", "Gedachte", Thought, "Misschien hoef ik dit niet alleen te dragen."),
                },
                8f);

            SetArray(so.FindProperty("endMessages"), new Object[]
            {
                Msg("6_Lucas_ErVoorJe", "Lucas", Friend, "Je hoeft het niet allemaal uit te leggen. Ik ben er voor je."),
                Msg("6_Lucas_Praten", "Lucas", Friend, "Zullen we even praten?"),
            });
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            // De dag begint zodra de voordeur achter de speler dichtslaat.
            var transition = systems.GetComponentInChildren<Phase2Transition>();
            if (transition != null)
                UnityEventTools.AddPersistentListener(transition.onTriggered, day.Begin);
            log.AppendLine($"Schooldag: 4 stappen, berichten in {Folder}, start {(transition != null ? "bij de Fase 2-overgang" : "NIET gekoppeld")}");
        }

        static void SetStep(SerializedProperty step, string name, Object[] onStart, BoxCollider area, Object[] onArrive, float holdSeconds)
        {
            step.FindPropertyRelative("name").stringValue = name;
            SetArray(step.FindPropertyRelative("onStart"), onStart);
            step.FindPropertyRelative("area").objectReferenceValue = area;
            SetArray(step.FindPropertyRelative("onArrive"), onArrive);
            step.FindPropertyRelative("holdSeconds").floatValue = holdSeconds;
        }

        static void SetArray(SerializedProperty array, Object[] items)
        {
            array.arraySize = items.Length;
            for (var i = 0; i < items.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static BoxCollider Area(Transform parent, string name, Vector3 floor, Vector2 sizeXZ)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = floor + Vector3.up * 1.1f;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(sizeXZ.x, 2.2f, sizeXZ.y);
            return box;
        }

        static NotificationData Msg(string file, string sender, Color color, string text)
        {
            var path = $"{Folder}/{file}.asset";
            var data = AssetDatabase.LoadAssetAtPath<NotificationData>(path);
            if (data != null)
                return data;
            data = ScriptableObject.CreateInstance<NotificationData>();
            data.sender = sender;
            data.senderColor = color;
            data.message = text;
            data.displayDuration = Mathf.Clamp(2.5f + text.Length * 0.05f, 3.5f, 6.5f);
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        static void EnsureFolder()
        {
            var parts = Folder.Split('/');
            var current = parts[0];
            foreach (var part in parts.Skip(1))
            {
                if (!AssetDatabase.IsValidFolder($"{current}/{part}"))
                    AssetDatabase.CreateFolder(current, part);
                current = $"{current}/{part}";
            }
        }

        /// <summary>
        /// Telefoon-HUD: wereld-canvas dat zacht voor de speler meebeweegt (LazyFollowHUD), met één berichtpaneel
        /// (NotificationHUDView) en de NotificationManager die de wachtrij afhandelt.
        /// </summary>
        static void AddPhoneHud(Transform systems)
        {
            var root = new GameObject("PhoneHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(systems, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(420f, 150f);
            // Dichtbij (0,55 m): op 1,25 m (standaard) verdwijnt het paneel achter een deur of muur vlak voor de speler.
            // Paneel 0,25 m breed: ongeveer 25° in beeld.
            rect.localScale = Vector3.one * 0.0006f;
            var follow = new SerializedObject(root.AddComponent<LazyFollowHUD>());
            follow.FindProperty("distance").floatValue = 0.55f;
            follow.FindProperty("heightOffset").floatValue = -0.1f;
            follow.FindProperty("lateralOffset").floatValue = 0.07f;
            follow.ApplyModifiedPropertiesWithoutUndo();

            var panel = new GameObject("Message", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.9f);

            var sender = Text(panel.transform, "Sender", 30f, FontStyles.Bold, new Vector2(0f, 0.62f), Vector2.one);
            var message = Text(panel.transform, "Body", 24f, FontStyles.Normal, Vector2.zero, new Vector2(1f, 0.64f));

            var view = panel.AddComponent<NotificationHUDView>();
            var vso = new SerializedObject(view);
            vso.FindProperty("canvasGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            vso.FindProperty("panelTransform").objectReferenceValue = panelRect;
            vso.FindProperty("senderText").objectReferenceValue = sender;
            vso.FindProperty("messageText").objectReferenceValue = message;
            vso.ApplyModifiedPropertiesWithoutUndo();

            var manager = root.AddComponent<NotificationManager>();
            var mso = new SerializedObject(manager);
            mso.FindProperty("hudView").objectReferenceValue = view;
            mso.ApplyModifiedPropertiesWithoutUndo();
        }

        static TextMeshProUGUI Text(Transform parent, string name, float size, FontStyles style, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(20f, 12f);
            rect.offsetMax = new Vector2(-20f, -10f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }
    }
}
