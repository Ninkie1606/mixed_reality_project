# VR School — Asset-plan voor Claude Code (Blender MCP + Unity CLI)

Dit document is het werkplan voor Claude Code om de assets voor een VR-school te bouwen.
Blender maakt de modellen via de **Blender MCP**, Unity importeert en zet ze klaar via de **Unity CLI + Pipeline** (`unity command ...`, live in de open Editor).
Werk de fases in volgorde af en vink items af (`- [x]`) zodra ze klaar en gecontroleerd zijn.

---

## 0. Setup & context

- Paden:
  - Repo: `C:\0_school\3deJaar\Mixed_Reality\project`
  - Unity-project: `project/mixed_reality` (Unity **6000.4.3f1**, URP 17.4, XRI 3.4.1, XR Hands 1.7.3, OpenXR 1.16 + Meta OpenXR)
  - Blender-bronbestanden: `project/blender/models/<props|environment|characters>/` (Blender **5.2.2 LTS**)
  - FBX-export: `mixed_reality/Assets/Art/<Categorie>/` — prefabs: `mixed_reality/Assets/Prefabs/<Categorie>/`
  - Unity CLI: `C:\Users\reuln\AppData\Local\Unity\bin\unity.exe` (`com.unity.pipeline` 0.9.1-exp.1 in het project)
- Unity-versie: Unity 6, **URP**, XR Interaction Toolkit.
- Controleer bij de start:
  1. Blender MCP reageert (bv. `get_objects_summary`).
  2. `unity status` → Editor in state `ready`; `unity command scene list_open`.
  3. Exportmap bestaat en is schrijfbaar.

### Tooling
Blender (procedureel, opnieuw draaien = alles opnieuw bouwen + exporteren):
- `blender/tools/build_modular_kit.py` — Fase 1 gebouwkit → `models/environment/SM_Build_Kit.blend`. Bevat ook de gedeelde helpers:
  `MB` (box, cyl, lathe, grid_panel, profile_x, mark/transform_since) en `build_kit(...)` (unwrap UV0/UV1, `uv_fill`, rapport, export).
- `blender/tools/build_classroom_kit.py` — Fase 2 klaslokaal → `models/props/SM_Classroom_Kit.blend`; exporteert naar `Art/Classroom` en `Art/Props`.
- `blender/tools/build_hallway_kit.py` — Fase 3 gang → `models/props/SM_Hallway_Kit.blend`; exporteert naar `Art/Hallway` en `Art/Props`
  (laadt `build_classroom_kit.py` in, dus alle materialen/helpers van Fase 1 + 2).
- Elke run schrijft `<blend>_report.json` (tris, materialen, maten per asset).

Unity (`Assets/Scripts/`):
- `Editor/VRSchoolModelPostprocessor.cs` — import-instellingen voor alle FBX onder `Assets/Art/`, `UCX_` → onzichtbare BoxCollider-children.
- `Editor/VRSchoolBuildCore.cs` — gedeeld: materiaaltabel (URP/Lit), material-remap, prefab-inrichting per `Kind`, asconversie, plaatsingshulpen (`Run`, `Spawn`, `Facing`, showcase, XR Origin).
- `Editor/VRSchoolKitBuilder.cs` — menu **VR School → Build Phase 1 - Modular Kit** (prefabs + `Test_Modular`).
- `Editor/VRSchoolClassroomBuilder.cs` — menu **VR School → Build Phase 2 - Classroom** (prefabs + `Classroom_01`). Vereist Fase 1.
- `Editor/VRSchoolHallwayBuilder.cs` — menu **VR School → Build Phase 3 - Hallway** (prefabs + `Hallway_01`). Vereist Fase 1.
- `Interaction/SwingDoor.cs` — deur draait rond scharnierpivot; elke XR-interactable in de deur togglet open/dicht.
- `Interaction/WallClock.cs` — klokwijzers op systeemtijd of een vaste tijd (`fixedTime`, bv. `08:25`).
- Prefabs zijn *model-variants*: een nieuwe FBX-export uit Blender werkt ze automatisch bij. Builders opnieuw draaien is veilig (GUID's blijven).
- ⚠ Een builder bewaart zijn scene opnieuw: open dan eerst een andere scene (anders botst het met de open scene).

---

## 1. Conventies (gelden voor élk asset)

### Schaal & oriëntatie
- 1 Blender-unit = **1 meter**. Realistische maten (deur 2,1 m hoog, bank 0,75 m hoog, plafond 3 m).
- Modulaire bouwstukken op een **grid van 1 m** (muren 2 m of 4 m breed, 3 m hoog).
- **Pivot/origin**: onderaan in het midden voor props; voor muren in de hoek op de vloer (snapt makkelijk).
  - Muren: dikte 0,2 m **gecentreerd op de gridlijn**; pivot = begin van de muur op de gridlijn, vloerniveau.
  - Wandprops (radiator, stopcontact, schakelaar, nooduitgang, luidspreker): pivot **achteraan-midden** (tegen de muur).
  - Plafondprops (TL, rookmelder): pivot **bovenaan-midden**. Plafondtegel: pivot hoek onderaan → op y = 3 m plaatsen.
  - Deuren: pivot op de **scharnierzijde onderaan**.
- **Kamerzijde = Blender −Y.**
- Asconversie (gemeten): Blender (x, y, z) → Unity lokaal (**−x, z, −y**).
- Apply **scale & rotation** vóór export (`Ctrl+A` equivalent in code).
- FBX-export: `axis_forward='-Z'`, `axis_up='Y'`, `apply_scale_options='FBX_SCALE_ALL'`, `use_selection=True`, `bake_space_transform=True`.

### Naamgeving
- Objecten/bestanden: `SM_<Categorie>_<Naam>_<Variant>` (bv. `SM_Class_Desk_A`).
- Materialen: `M_<Naam>` (bv. `M_Wood_Light`).
- Textures: `T_<Naam>_<Type>` (`_BaseColor`, `_Normal`, `_ORM`).
- Collision-meshes: `UCX_<Objectnaam>_01` (simpele boxen/convex).
- LOD's: `<Naam>_LOD0`, `_LOD1`, `_LOD2`.

### Performance-budget (VR: 72–90 fps op standalone headset)
| Type | Tris LOD0 | LOD's |
|---|---|---|
| Kleine prop (pen, boek) | < 500 | geen |
| Middelgrote prop (stoel, bank) | < 2 000 | LOD1 |
| Groot meubel (kast, locker-rij) | < 5 000 | LOD1 + LOD2 |
| Modulair muur/vloerstuk | < 200 | geen |
| Personage | < 15 000 | LOD1 + LOD2 |

- Liefst **1 materiaal per object**, gedeelde materialen/atlassen waar het kan.
- Geen n-gons in export; smooth shading + auto smooth/weighted normals.
- **UV's**: UV0 voor textures, **UV1 voor lightmaps** (geen overlap).

### Werkwijze per asset (loop)
1. Nieuwe collection `<AssetNaam>` in Blender.
2. Model bouwen via `execute_blender_code` (bmesh/ops, modifiers; modifiers applyen vóór export).
3. Materiaal toewijzen (Principled BSDF met basiskleur; textures later).
   ⚠ Blender 5: materiaalslots toevoegen **vóór** `bm.to_mesh()`, anders klemt Blender alle `material_index` op 0.
4. UV unwrap (smart project of cube project) + lightmap-UV.
5. Collision-mesh (`UCX_`) en eventuele LOD's maken.
6. **Controle**: render (workbench-camera; window-screenshot is zwart als Blender geminimaliseerd is) → visueel checken, tris tellen, afmetingen checken.
7. `.blend` opslaan in de Blender-map, FBX exporteren naar `Assets/Art/<Categorie>/`.
8. In Unity (via `unity command script eval_file ...`): import-instellingen (postprocessor), materiaal (URP/Lit) koppelen, **prefab** maken in `Assets/Prefabs/<Categorie>/`.
9. Item afvinken in dit document.

---

## 2. Fase 1 — Modulaire gebouwkit (prioriteit HOOG) ✅

Doel: daarmee kan je élke ruimte opbouwen.

- [x] `SM_Build_Wall_2m`, `SM_Build_Wall_4m`
- [x] `SM_Build_Wall_Corner_In`, `SM_Build_Wall_Corner_Out`
- [x] `SM_Build_Wall_Window` (raam + vensterbank)
- [x] `SM_Build_Wall_Door` (deuropening + kozijn)
- [x] `SM_Build_Floor_2x2`, `SM_Build_Ceiling_2x2`
- [x] `SM_Build_Stairs_Straight` + `SM_Build_Railing`
- [x] `SM_Build_Door_Classroom` (apart object, pivot op scharnier → kan open in VR)
- [x] `SM_Build_Door_Double` (gang), `SM_Build_Door_Emergency`
- [x] `SM_Build_DoorHandle` (aparte grab-handle)
- [x] `SM_Build_Window_Glass` (apart object, transparant materiaal)
- [x] `SM_Build_Light_TL` (plafond-tl-armatuur)
- [x] `SM_Build_Radiator`, `SM_Build_Socket`, `SM_Build_LightSwitch`
- [x] `SM_Build_FireExtinguisher`, `SM_Build_ExitSign`, `SM_Build_SmokeDetector`, `SM_Build_Speaker`
- [x] *(extra)* `SM_Build_Wall_Door_Double` — 4 m muur met opening van 1,8 m voor `SM_Build_Door_Double`

**Unity**: [x] testscene `Scenes/Test_Modular` met één kamer van 8×6 m opgebouwd uit de kit (snapping gecontroleerd) + showcase-rij met alle prefabs naast een 1,8 m referentiecapsule + XR Origin.

### Kwaliteitscheck Fase 1
| Asset | Tris | Materialen | Maat (m) | Unity |
|---|---|---|---|---|
| Wall_2m / Wall_4m | 10 / 10 | 1 | 2×0,2×3 / 4×0,2×3 | static, BoxCollider |
| Wall_Corner_In / Out | 10 / 28 | 1 / 2 | 0,2×0,2×3 | static, BoxCollider |
| Wall_Window | 94 | 3 (muur, kader, vensterbank) | 2×0,25×3 | static, 4× UCX |
| Wall_Door / Wall_Door_Double | 58 / 58 | 2 | 2×0,2×3 / 4×0,2×3 | static, 3× UCX |
| Floor_2x2 / Ceiling_2x2 | 10 / 46 | 1 / 2 | 2×2×0,1 / 2×2×0,03 | static; vloer + TeleportationArea |
| Stairs_Straight | 148 | 1 | 1,2×4,76×3 (18 × 16,7 cm) | static, MeshCollider, TeleportationArea |
| Railing | 76 | 1 | 2,06×0,05×1,03 | static |
| Door_Classroom | 68 | 2 (hout, glas) | 0,89×0,04×2,04 | kinematic RB, SwingDoor (+95°), 2× genest DoorHandle |
| Door_Double (L+R) | 156 | 3 | 1,7×0,09×2,04 | per blad SwingDoor (±95°) + XRSimpleInteractable |
| Door_Emergency | 60 | 2 | 0,89×0,12×2,04 | SwingDoor (−95°, opent naar buiten) + XRSimpleInteractable |
| DoorHandle | 100 | 1 | 0,14×0,07×0,05 | XRSimpleInteractable |
| Window_Glass | 12 | 1 (transparant) | 1,3×0,01×1,1 | static (geen occluder) |
| Light_TL | 20 | 2 (emissive) | 1,24×0,32×0,08 | static + baked Point Light |
| Radiator | 252 | 3 | 1×0,12×0,74 | static |
| Socket / LightSwitch | 64 / 20 | 2 / 1 | 0,08×0,02×0,08 | static |
| FireExtinguisher | 188 | 4 | 0,17×0,17×0,58 | Rigidbody 6 kg + XRGrabInteractable |
| ExitSign / SmokeDetector / Speaker | 20 / 90 / 20 | 2 | — | static |

Getest: compile zonder fouten, Play mode zonder fouten, alle 6 SwingDoors draaien correct open (via script getriggerd, nog **niet** met controller/hand in VR).

---

## 3. Fase 2 — Klaslokaal (prioriteit HOOG) ✅

- [x] `SM_Class_Desk_A` (leerlingenbank), `SM_Class_Chair_A`
- [x] `SM_Class_TeacherDesk`, `SM_Class_OfficeChair`
- [x] `SM_Class_Chalkboard`, `SM_Class_Whiteboard`, `SM_Class_SmartBoard`
- [x] `SM_Class_Projector`, `SM_Class_ProjectorScreen`
- [x] `SM_Class_Cabinet`, `SM_Class_Bookshelf`, `SM_Class_CoatRack`
- [x] `SM_Class_TrashBin`, `SM_Class_WallClock`
- [x] `SM_Class_Laptop` (open/dicht), `SM_Class_PC_Monitor`, `SM_Class_Keyboard`
- [x] `SM_Class_Corkboard` + `SM_Class_PaperNote` (losse briefjes)
- [x] `SM_Class_Poster_Frame` (vlak voor wisselbare poster-textures), `SM_Class_WorldMap`

**Kleine grab-props** (moeten oppakbaar zijn in VR → simpele box/capsule-collider):
- [x] `SM_Prop_Book_A/B/C`, `SM_Prop_Notebook`
- [x] `SM_Prop_Pen`, `SM_Prop_Pencil`, `SM_Prop_Eraser`, `SM_Prop_Ruler`
- [x] `SM_Prop_Calculator`, `SM_Prop_PencilCase`
- [x] `SM_Prop_Backpack_A/B`, `SM_Prop_WaterBottle`
- [x] `SM_Prop_Chalk`, `SM_Prop_Marker`

**Unity**: [x] scene `Scenes/Classroom_01` (10 × 8 m) met 24 banken (3 duo-kolommen × 4 rijen) + leraarsplek, bordwand
(smartboard, krijtbord, whiteboard + projectiescherm, klok), 4 ramen met radiatoren, deur, prikbord met briefjes, kapstok,
boekenrekken/kast/wereldkaart/posters achteraan, 82 losse props (reproduceerbaar verspreid, seed 42) + showcase-rij + XR Origin.

### Ontwerpkeuzes Fase 2
- **Voorkant = −Y in Blender = +Z in Unity** voor alles: zitkant van bank/bureau, kijkrichting van stoelen, beeldvlak van borden/schermen.
  In de scene plaats je met `Facing(richting)`.
- Wandobjecten: pivot achteraan-midden; **borden/smartboard aan de onderrand** (y = montagehoogte, hier 0,90 m).
- Bewegende delen = aparte children: laptopdeksel (scharnier), klokwijzers, projectiedoek (pivot bovenrand → lokaal Y schalen = oprollen).
- Laptop: één model, twee prefabs (`SM_Class_Laptop` open 110°, `SM_Class_Laptop_Closed`).
- Poster en wereldkaart: het beeldvlak is een apart child met UV 0..1 (`M_Poster`, `M_WorldMap`) → texture/materiaal wisselen zonder het kader te raken.
- Colliders: borden en boekenrek krijgen een **MeshCollider** (krijt/stiften liggen in de goot, boeken op de planken); andere meubels een BoxCollider.
- **Geen LOD's**: alle meubels zitten ver onder het budget (max 394 tris); een LOD1 op ~100 tris levert niets op en kost een extra draw-pad.

### Kwaliteitscheck Fase 2
| Asset | Tris | Mat. | Maat (m) | Unity |
|---|---|---|---|---|
| Desk_A / Chair_A | 90 / 84 | 2 / 2 | 0,7×0,5×0,76 / 0,4×0,46×0,8 | static, box |
| TeacherDesk / OfficeChair | 98 / 296 | 2 / 3 | 1,4×0,7×0,75 / 0,6×0,6×1,06 | static, box |
| Chalkboard / Whiteboard / SmartBoard | 64 / 64 / 30 | 2 | 3,08×1,3 / 2,44×1,26 / 1,95×1,23 | static, MeshCollider |
| Projector / ProjectorScreen | 72 / 66 | 3 | 0,3×0,27×0,42 / 2×1,52 | static (doek niet) |
| Cabinet / Bookshelf / CoatRack | 72 / 66 / 178 | 3 / 1 / 2 | 1×0,48×1 / 0,9×0,32×1,8 / 1,2 | static |
| TrashBin | 128 | 1 | 0,32×0,36 | grab 1,5 kg |
| WallClock | 394 | 3 | Ø 0,31 | static + WallClock (wijzers dynamisch) |
| Laptop (open/dicht) / PC_Monitor / Keyboard | 54 / 44 / 162 | 3 / 2 / 2 | 0,32×0,22 / 0,54×0,44 / 0,44×0,14 | grab 1,5 kg / static / static |
| Corkboard / PaperNote | 42 / 34 | 2 | 1,26×0,96 / A6 | static / grab, kinematic (blijft hangen) |
| Poster_Frame / WorldMap | 34 / 58 | 2 | A1 / 1,72×1,06 | static |
| Book_A/B/C / Notebook | 38 | 2 | 17×24 / A4 / 15×21 cm / A4 | grab |
| Pen / Pencil / Eraser / Ruler | 48 / 52 / 12 / 12 | 2 / 4 / 1 / 1 | 14 / 18 / 5 / 30 cm | grab (capsule / box) |
| Calculator / PencilCase | 222 / 52 | 3 / 2 | 8×16 / 20×7 cm | grab |
| Backpack_A/B / WaterBottle | 100 / 164 | 3 / 2 | 0,3×0,25×0,52 / 0,24 | grab 3 kg / 0,6 kg |
| Chalk / Marker | 28 / 88 | 1 / 2 | 8 / 15 cm | grab (capsule) |

Getest: compile zonder fouten, Play mode 0 fouten, 73 dynamische props blijven liggen (57 op bankhoogte, niets onder 0,3 m behalve
rugzakken/vuilbak/brandblusser), klok draait met de klok mee (3 uur = rechts). **Nog niet in de headset getest.**

---

## 4. Fase 3 — Gang (prioriteit MIDDEL) ✅

- [x] `SM_Hall_Locker_Single` (deur apart, scharnierpivot) + `SM_Hall_Locker_Row_5`
- [x] `SM_Hall_Bench`
- [x] `SM_Hall_InfoScreen`
- [x] `SM_Hall_TrophyCase` + `SM_Prop_Trophy_A/B`
- [x] `SM_Hall_PictureFrame`
- [x] `SM_Hall_Planter` + `SM_Hall_Plant`
- [x] `SM_Hall_WaterFountain`, `SM_Hall_VendingMachine`

**Unity**: [x] scene `Scenes/Hallway_01` (16 × 4 m): noordwand met 2 klasdeuren en 22 lockers (4 × Row_5 + 2 × Single),
zuidwand met 4 ramen + radiatoren, 2 banken, trofeeënkast (9 trofeeën op onderkast en glasplaten), infoscherm, drankautomaat
en fontein; west dubbele deur (ingang), oost nooddeur, exit-borden, 3 plantenbakken, TL's, rookmelders + showcase-rij + XR Origin.

### Ontwerpkeuzes Fase 3
- **Tegen de muur staande meubels** (lockers, vitrine, fontein, automaat): pivot **achteraan-midden op de vloer** → direct op het
  muuroppervlak plaatsen met `Facing(...)`. Bank, plantenbak en plant: onderaan-midden. Infoscherm en fotokader: achteraan-midden in het centrum.
- **Lockers zijn open kasten**: elk vak heeft een binnenruimte + hoedenplank (MeshCollider) → er kan later iets in. Deuren zijn children
  `<naam>_Door` / `_Door_N` met pivot op het scharnier; in Unity krijgt elke deur `SwingDoor` (+95°, opent naar de gang) +
  `XRSimpleInteractable` + kinematic Rigidbody (nieuw `Kind.Locker`).
- Beeldvlakken (`_Screen` van het infoscherm, `_Picture` van het fotokader) zijn aparte children met UV 0..1, zoals bij de poster.
- Plant staat los van de bak (zet hem op bak-hoogte + 0,40 m); zo kan dezelfde plant ook elders staan.
- Nieuwe materialen: `M_Metal_Gold`, `M_Soil`, `M_Plant_Leaf` (Blender + `VRSchoolBuildCore`).

### Kwaliteitscheck Fase 3
| Asset | Tris | Mat. | Maat (m) | Unity |
|---|---|---|---|---|
| Locker_Single / Locker_Row_5 | 140 / 516 | 3 | 0,3 / 1,5 × 0,49 × 1,8 | kast static + MeshCollider; deuren SwingDoor + XRSimpleInteractable |
| Bench | 120 | 2 | 1,6×0,4×0,45 | static, box |
| InfoScreen | 24 | 2 | 1,25×0,73 | static |
| TrophyCase | 106 | 3 (hout, glas, chroom) | 1,2×0,47×1,9 | static, MeshCollider (trofeeën op de glasplaten) |
| PictureFrame | 34 | 2 | 0,6×0,45 | static |
| Planter / Plant | 28 / 204 | 2 / 2 | 0,5×0,5×0,45 / Ø 0,6 × 1,03 | static |
| WaterFountain | 182 | 3 | 0,4×0,38×0,99 | static |
| VendingMachine | 516 | 7 | 0,9×0,81×1,85 | static |
| Trophy_A / Trophy_B | 288 / 92 | 2 | 25 / 22 cm | grab 0,8 / 0,5 kg |

Getest: compile zonder fouten, alle 22 lockerdeuren in de gang draaien naar de gang toe (gemeten), Play mode 0 fouten:
alle 33 SwingDoors (lockers, klas-, dubbele en nooddeur) openen via script, de 9 trofeeën blijven op hun plank liggen.
**Nog niet in de headset getest.**

---

## 5. Fase 4 — Overige ruimtes (prioriteit MIDDEL/LAAG)

### Refter
- [ ] `SM_Cafe_LongTable`, `SM_Cafe_Chair`, `SM_Cafe_Counter`
- [ ] `SM_Prop_Tray`, `SM_Prop_LunchBox`, `SM_Cafe_Microwave`

### Toiletten
- [ ] `SM_WC_Stall` (deur apart), `SM_WC_Toilet`, `SM_WC_Sink`, `SM_WC_Mirror`
- [ ] `SM_WC_HandDryer`, `SM_WC_SoapDispenser`

### Sportzaal
- [ ] `SM_Gym_BasketHoop`, `SM_Gym_Mat`, `SM_Gym_WallBars`, `SM_Gym_Bleachers`
- [ ] `SM_Prop_Ball_Basket`, `SM_Prop_Ball_Foot`, `SM_Prop_Cone`
- [ ] `SM_Gym_LockerBench`

### Bibliotheek / aula
- [ ] `SM_Lib_Bookshelf_Tall`, `SM_Lib_ReadingTable`, `SM_Lib_Beanbag`
- [ ] `SM_Aula_Stage`, `SM_Aula_SeatRow`

### Labo
- [ ] `SM_Lab_Table`, `SM_Lab_Stool`, `SM_Prop_Flask`, `SM_Prop_Beaker`, `SM_Prop_Microscope`

### Lerarenkamer / secretariaat
- [ ] `SM_Office_Table`, `SM_Office_CoffeeMachine`, `SM_Office_Printer`, `SM_Office_Desk`

---

## 6. Fase 5 — Buiten (prioriteit LAAG)

- [ ] `SM_Out_Ground_Playground` (met belijning als texture/decal)
- [ ] `SM_Out_BikeRack` + `SM_Out_Bike`
- [ ] `SM_Out_Bench`, `SM_Out_Shelter` (afdak)
- [ ] `SM_Out_Fence_Segment`, `SM_Out_Gate`, `SM_Out_SchoolSign`
- [ ] `SM_Out_Tree_A/B` (low-poly, of uit gratis pack)
- [ ] Skybox/HDRI → niet in Blender maken: gebruik Poly Haven HDRI

---

## 7. Materialen & textures

Basisset materialen in Blender (gedeeld over assets) én als URP/Lit in Unity in `Assets/Art/Materials/`
(nu nog effen kleuren, textures volgen):

- [ ] `M_Floor_Linoleum` ✔, `M_Floor_Tile`, `M_Floor_Carpet`
- [ ] `M_Wall_Paint_White` ✔, `M_Wall_Paint_Accent`, `M_Concrete` ✔
- [x] `M_Wood_Light`, `M_Wood_Dark`
- [x] `M_Metal_Painted` (lockers), `M_Metal_Chrome`
- [x] `M_Glass` (transparant)
- [x] `M_Chalkboard`, `M_Whiteboard`
- [x] `M_Plastic_Chair`
- [ ] `M_Grass`, `M_Asphalt`
- *(extra, Fase 1)* `M_Metal_White`, `M_Plastic_White`, `M_Rubber_Black`, `M_Paint_Red`, `M_Emissive_Light`, `M_ExitSign`
- *(extra, Fase 2)* `M_Plastic_Black/Yellow/Green/Pink`, `M_Screen`, `M_Paper`, `M_Cork`, `M_Fabric_Blue/Red/Dark`, `M_Poster`, `M_WorldMap`
- *(extra, Fase 3)* `M_Metal_Gold`, `M_Soil`, `M_Plant_Leaf`
- Alle 32 materialen zijn nog effen kleuren (geen textures).

Bronnen voor PBR-textures: Poly Haven / ambientCG (CC0). Download handmatig of via script naar `Assets/Art/Textures/`.
Architectuur heeft UV0 op **wereldschaal (1 UV = 1 m)** → tileable textures lopen door over modules.

**Decals** (URP Decal Projector): graffiti op tafels, kauwgom, vlekken, posters.

---

## 8. Personages (niet in Blender modelleren)

- Gebruik **Mixamo** (gratis rigged personages + animaties) of een asset pack.
- Nodig: 4–6 leerlingen, 2 leerkrachten.
- Animaties: `Idle`, `Walk`, `Sit_Idle`, `Talk`, `Write`, `Sit_To_Stand`.
- Unity: Humanoid rig, Animator Controller `AC_Student`, `AC_Teacher`.

---

## 9. Unity-zijde (via Unity CLI)

### Projectstructuur
```
Assets/
  Art/
    Building/ Classroom/ Hallway/ Cafeteria/ Toilets/ Gym/ Library/ Lab/ Office/ Outdoor/ Props/
    Materials/ Textures/ Characters/ Audio/
  Prefabs/  (zelfde submappen als Art)
  Scenes/
  Scripts/  (Editor/, Interaction/)
```

### Import-instellingen per FBX (automatisch via `VRSchoolModelPostprocessor`)
- Scale Factor 1, **Convert Units aan**
- **Generate Lightmap UVs uit** (we leveren UV1 zelf) — of aan als UV1 ontbreekt
- Read/Write uit, Mesh Compression Medium
- Static meshes: **Static** aanvinken (lightmapping + batching) — gebeurt in de prefab-builder
- `UCX_`-meshes → onzichtbaar child met BoxCollider (renderer verwijderd)

### Prefabs
- Elke asset een prefab met juiste collider.
- **Grab-props**: `Rigidbody` + `XRGrabInteractable`.
- **Deuren/lockerdeuren**: `SwingDoor` + `XRSimpleInteractable` op de handle (of op het blad).
- Statische meubels: `Static`, geen Rigidbody.

### XR-setup (eenmalig)
- [x] Packages: XR Interaction Toolkit, XR Hands, OpenXR (+ Meta/Quest feature) — waren al aanwezig
- [x] XR Origin (XR Rig) met controllers of handen — `Complete XR Origin Set Up Variant` in Test_Modular
- [ ] Locomotion: teleport (TeleportationArea op vloeren ✔) + snap turn — nog testen in VR
- [ ] World-space UI canvas voor menu/dialogen
- [ ] Lighting: baked lightmaps, reflection probes per ruimte, URP-asset getuned voor VR (MSAA 4x, single-pass instanced)

---

## 10. Audio (downloaden, niet maken)

Bron: Freesound (let op licentie) → `Assets/Art/Audio/`.
- [ ] Schoolbel
- [ ] Voetstappen: linoleum, tegels, buiten
- [ ] Deur open/dicht, locker dichtslaan
- [ ] Stoel schuiven, krijt op bord
- [ ] Ambience: klas, gang, refter, buiten
- Unity: Audio Sources 3D (spatial blend 1) voor objecten, ambience 2D per ruimte.

---

## 11. Sfeer / lichtvarianten

Voorzie per scene meerdere **Volume-profielen** (URP post-processing) en lichtpresets:
- [ ] `Mood_Neutral` — normaal daglicht
- [ ] `Mood_Warm` — warm, zacht licht
- [ ] `Mood_Cold` — kil, grauw, lagere saturatie, vignet
- [ ] `Mood_Evening` — avond, kunstlicht

Zo kan dezelfde ruimte een andere emotionele lading krijgen zonder nieuwe modellen.

---

## 12. Kwaliteitscheck per fase

Na elke fase:
1. Tris-telling en materiaal-telling per asset noteren (Blender).
2. Alle prefabs in een testscene plaatsen, schaal naast een 1,8 m referentiecapsule checken.
3. In VR testen (of XR Device Simulator): grab-props oppakbaar, deuren openbaar, teleport werkt.
4. Frame Debugger / Profiler: draw calls en fps binnen budget.
5. Problemen noteren onder **Open punten** hieronder.

---

## Open punten

- **VR-test nog te doen** (Fase 1, check 3): klink/paniekbalk met controller of hand, brandblusser oppakken, teleport op vloer/trap. Alleen via script getest.
- **Lightmaps nog niet gebakken**: TL-lampen staan op *Baked*, dus zonder bake is de kamer enkel door de directional light + ambient verlicht.
- **Profiler/Frame Debugger** (check 4) nog niet gedaan.
- **ExitSign**: effen groen emissive vlak; pictogram (lopend mannetje) nog toevoegen als texture of decal.
- **Railing** is enkel recht (horizontaal); voor de trap is een schuine variant `SM_Build_Railing_Stairs` nodig.
- **Wall_Window** gebruikt 3 materialen en de deuren 2–4 (budget: liefst 1) → oplossen met een gedeelde atlas zodra er textures zijn.
- **Corner_In/Out**: wanden zijn gecentreerd op de gridlijn; de hoekkolom (0,2 × 0,2 m) vult het buitenste hoekvak. Corner_Out heeft een chromen hoekbeschermer.
- **Door_Double**: root is een empty op het linkerscharnier; plaats op `Wall_Door_Double`-pivot + (1,15 m, 0, 0) in Blender-coördinaten. Nog geen voorbeeld in de testscene.
- **Radiator**: 252 tris zonder LOD; prima voor nu, LOD1 alleen als profiler erom vraagt.
- Bestaande waarschuwing *"Importer generated inconsistent result for OpenXRPackageSettings.asset"* staat los van deze kit.

### Fase 2
- **VR-test**: props oppakken (vooral kleine: krijt, potlood, gom), laptop, briefjes van het prikbord trekken.
- **Projectiescherm** hangt volledig uitgerold voor het whiteboard; een `ScreenRoller`-script (lokaal Y schalen van `_Cloth`) ontbreekt nog.
- **Schermen** (`M_Screen`) zijn zwart; voor een "aan"-stand een emissive materiaal of RenderTexture voorzien (smartboard, monitor, laptop).
- **Posters/wereldkaart/briefjes** hebben UV 0..1 klaar maar nog geen textures.
- **Boekenrekken** zijn leeg; vullen met `SM_Prop_Book_*` (MeshCollider laat dat toe) of een gecombineerde "boekenrij"-mesh voor minder draw calls.
- **Draw calls**: 82 losse props met elk 1–4 materialen in één klas → in de Profiler nakijken; eventueel GPU instancing aanzetten op de materialen of props statisch batchen tot ze opgepakt worden.
- **Laptopdeksel** zit vast in de prefab (open of dicht); open/dicht klappen in VR vraagt een hinge-script zoals `SwingDoor`.

### Fase 3
- **VR-test**: lockerdeuren openen met controller/hand (hele deur is de interactable, geen aparte klink), trofeeën uit de vitrine halen.
- **Vitrine** heeft geen deurtjes in het glas: trofeeën zijn alleen met de ray-interactor (door het glas) te pakken. Glazen schuifdeur toevoegen indien nodig.
- **Drankautomaat** gebruikt 7 materialen (producten geel/groen, glas, scherm, toetsen) → kandidaat nr. 1 voor een atlas zodra er textures zijn.
- **Infoscherm** (`M_Screen`) is zwart en het **fotokader** deelt `M_Poster` met de posterkaders → eigen texture/RenderTexture per instantie voorzien.
- **Klasdeuren in de gang** openen naar de gangzijde (zelfde prefab als in Classroom_01); de gang hangt nog niet aan een klaslokaal vast.
- Geen geluid/feedback bij lockers (locker dichtslaan staat bij §10 Audio).
