# Scrum Backlog & Projectplanning: Depression VR

## 1. Teamoverzicht & Huidige Focus
De taken in deze backlog zijn opgesplitst in twee hoofdsporen: **Development** en **Medisch / Inhoudelijk**. Binnen het development-spoor kan iedereen vrij taken oppakken van het Scrum-bord, maar hieronder staat de huidige verdeling en een suggestie voor de openstaande rollen:

* **Development Team (5 personen):**
  * **Robbe:** Focus op documentatie, projectmanagement, Scrum-bord beheer & UI/tekst-integratie.
  * **Nick:** Focus op 3D-omgeving, Blender-modellen en het opbouwen van de Unity-scène (Level Design).
  * **Sam:** Focus op Sound Design (omgevingsgeluiden, vervreemdende audio-effecten en voice-over integratie).
  * **Nathan:** *Suggestie focus:* VR-setup, controller-interactie (deuren, joysticks) en speler-fysica.
  * **Laurens:** *Suggestie focus:* C# Scripting voor de veranderende doolhof-mechanieken, lichttransities en triggers.
* **Medisch & Psychologisch Expert (1 persoon):**
  * **Hind:** Focus op de psychologische onderbouwing, vertaling van depressie-symptomen naar de ervaring, scripts voor de stemmen en de afsluitende boodschap.

---

## 2. Product Backlog (To-Do's per Categorie)

### Spoor A: Medisch & Inhoudelijk (Hind)
* **Epic 1: Psychologische Vertaalslag & Richtlijnen**
  * [ ] Onderzoeken hoe *foutieve denkpatronen* en *verstoorde cognitieve schema's* (uit de projectbeschrijving) subtiel voelbaar kunnen zijn in het doolhof (bijv. ideeën aanleveren voor desoriëntatie of geheugenverlies-effecten).
  * [ ] Richtlijnen opstellen voor de sfeer: ervoor zorgen dat de ervaring confronterend en realistisch is, maar ethisch verantwoord blijft voor mensen die zelf met depressie kampen.
* **Epic 2: Verhaallijn, Stemmen & Quotes**
  * [ ] Uitschrijven van de zinnen/scripts voor **Fase 5 (De Lichtpunten)**: wat zeggen de helpende stemmen precies? Welke toon en woorden werken echt ondersteunend bij depressie?
  * [ ] Verzamelen van een shortlist met krachtige, wetenschappelijk of psychologisch onderbouwde **eindquotes (Fase 6)** die aanzetten tot nadenken.
  * [ ] Beslissen (samen met het team) of en waar er extra psychologische informatie wordt toegevoegd (bijv. een kort informatiescherm aan het einde of subtiele visuele metaforen in de school).
* **Epic 3: Validatie & Gebruikerstesten**
  * [ ] Opstellen van een korte vragenlijst voor playtesters om te meten of de VR-ervaring daadwerkelijk zorgt voor meer begrip en *awareness*.

---

### Spoor B: Development Team (Robbe, Nick, Sam, Nathan, Laurens)

#### Categorie 1: Documentatie & Projectmanagement (Startfocus: Robbe)
* [ ] Opzetten en bijhouden van het Scrum-bord (bijv. in Trello, Jira of GitHub Projects).
* [ ] Uitwerken van een **Game Design Document (GDD)** op basis van het projectconcept (inclusief plattegrond/schets van de school en flowcharts van de doolhof-logica).
* [ ] Verslaglegging van sprints, keuzes en voortgang voor de uiteindelijke projectoplevering en presentatie.
* [ ] Opzetten van de Git-repository (met juiste `.gitignore` voor Unity) zodat het hele dev-team zonder merge-conflicten kan samenwerken.

#### Categorie 2: 3D Omgeving & Level Design (Startfocus: Nick)
* [ ] **Greyboxing (Unity):** Een eerste ruwe blokken-versie van het schoolplein en de schoolgangen bouwen om de schaal in VR te testen.
* [ ] **3D Modelling (Blender):** Modelleren van modulaire school-onderdelen (muren, vloeren, deuren, ramen, kluisjes, schoolborden, banken).
* [ ] **Fase 1 Omgeving (Buiten):** Inrichten van de schoolpoort en het schoolplein met een heldere, zonnige en uitnodigende uitstraling.
* [ ] **Fase 3 Omgeving (Binnen):** Inrichten van de verlaten schoolgangen als *liminal space* (leeg, repetitief en vervreemdend).
* [ ] **Fase 4 & 5 Omgeving (Het Lokaal / Zwarte Ruimte):** Ontwerpen van het eindlokaal en de volledig zwarte ruimte waarin de lichtpunten verschijnen.

#### Categorie 3: Audio Design & Implementatie (Startfocus: Sam)
* [ ] **Fase 1 Audio:** Vrolijke, normale buitengeluiden zoeken/maken (vogels, lichte wind, zachte achtergrondgeluiden).
* [ ] **Fase 2 Audio:** Geluidseffect voor het dichtslaan van de schooldeur en het abrupt wegvallen van de buitengeluiden.
* [ ] **Fase 3 Audio (Liminal Space & Doolhof):** 
  * [ ] Creëren van een drukkende, eenzame *ambient drone* (zoemende tl-lampen, leegte).
  * [ ] Maken van ruimtelijke (3D Spatial Audio) vervreemdende geluiden in Unity (voetstappen in de verte, gefluister, een deur die ergens dichtslaat).
* [ ] **Fase 4 Audio:** Implementeren van een plotselinge, oorverdovende stilte (of een hele zachte tinnitus-toon die wegebt) zodra alles zwart wordt.
* [ ] **Fase 5 Audio:** Opnemen, bewerken en in 3D-ruimte plaatsen van de helpende stemmen (gekoppeld aan de verschijnende lichtpunten).

#### Categorie 4: VR Setup, Besturing & UI (Goed op te pakken door bijv. Nathan / Laurens)
* [ ] **Meta Quest Integratie:** Unity-project configureren met de Meta XR SDK / OpenXR en testen op de headset.
* [ ] **Locomotion:** Implementeren van soepele beweging via de controller-joysticks (inclusief opties om VR-misselijkheid te beperken, zoals *vignetting* of *snap turning*).
* [ ] **Interactie-systeem:** Maken van een simpele interactie voor de handen/controllers om deuren te kunnen openen.
* [ ] **Tutorial (Fase 1):** Een korte, intuïtieve visuele uitleg maken bij de schoolpoort die laat zien hoe je wandelt en een deur opent.
* [ ] **Eindscherm UI (Fase 6):** Een strak 3D-tekstcanvas in de zwarte ruimte bouwen waarop de eindquote mooi in- en uitfadet.

#### Categorie 5: Gameplay Mechanieken, Doolhof-logica & Belichting (Goed op te pakken door bijv. Laurens / Nathan)
* [ ] **De Overgangs-trigger (Fase 2):** Script schrijven dat bij het passeren van de voordeur de deur sluit, vergrendelt en de belichting/skybox direct omslaat van zonnig naar donker.
* [ ] **Veranderend Doolhof Systeem (Fase 3):** 
  * [ ] Script ontwikkelen dat gangen of deuren ongemerkt verandert wanneer de speler er niet naar kijkt (bijv. via *camera frustum culling* of *trigger-zones* achter een hoek).
  * [ ] Systeem maken waarbij herkenningspunten (zoals lokaalnummers of posters) veranderen om het gevoel van geheugenverlies en cognitieve verwarring na te bootsen.
* [ ] **Eindlokaal & Blackout (Fase 4):** Logica schrijven zodat het gezochte lokaal pas bereikbaar wordt na een bepaalde tijd of een bepaald aantal bezochte gangen, gevolgd door een script dat de ruimte volledig zwart maakt bij binnenkomst.
* [ ] **Lichtpunten-systeem (Fase 5):** Visueel effect (bijv. via Unity Particle System, zachte *point lights* of *emission shaders*) programmeren dat synchroon met de audio-stemmen oplicht in de zwarte ruimte.

---

## 3. Voorstel Sprint Planning (Roadmap)

### Sprint 1: Fundament & Prototype
* **Doel:** Een werkende basis in VR waarin je kunt rondlopen in een testomgeving en het inhoudelijke kader vaststaat.
* **Dev Team:** Git-repo opzetten, Meta Quest-besturing (joysticks) werkend krijgen, greyboxing van de school en eerste audio-tests.
* **Hind:** Psychologische concepten uitwerken tot concrete ideeën voor het doolhof en starten met de scripts voor de stemmen en quotes.

### Sprint 2: De Overgang & Het Doolhof (Core Experience)
* **Doel:** De overgang van buiten naar binnen en de veranderende schoolgangen zijn speelbaar.
* **Dev Team:** 3D-modellen van de school plaatsen, overgangs-trigger programmeren, het script voor de veranderende gangen bouwen en de 3D-omgevingsgeluiden koppelen.
* **Hind:** Scripts voor de helpende stemmen afronden, eindquote selecteren en tussentijdse feedback geven op de sfeer van het doolhof.

### Sprint 3: Het Dieptepunt & De Lichtpunten (Emotionele Climax)
* **Doel:** De ervaring is van begin tot eind helemaal doorloopbaar (van schoolpoort tot eindquote).
* **Dev Team:** Zwarte ruimte-transitie bouwen, lichtpunten-effecten programmeren, voice-overs opnemen/implementeren, tutorial aan de schoolpoort toevoegen en eindquote-UI plaatsen.
* **Hind:** Begeleiden van de voice-over opnames (juiste intonatie en empathie) en voorbereiden van de testvragen voor proefpersonen.

### Sprint 4: Polish, Optimalisatie & Testen
* **Doel:** Een vloeiende, bugvrije en indrukwekkende VR-ervaring klaar voor oplevering.
* **Dev Team:** Performance-optimalisatie voor de Meta headset (consistente framerate behouden voor VR-comfort), belichting *baken*, geluidsniveaus afmixen en bugs oplossen.
* **Hele Team:** Playtesting uitvoeren met testgebruikers, feedback verwerken en de eindpresentatie/documentatie afronden.