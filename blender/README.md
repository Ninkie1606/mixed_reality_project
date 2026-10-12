# Blender-bronbestanden

Hier staan de **bronbestanden** (`.blend`) van alle 3D-modellen.
Unity leest deze map niet: modellen gaan als **FBX** naar
`mixed_reality/Assets/_Project/Models/`.

```
blender/
├── models/
│   ├── props/          ← losse objecten (tafel, beker, knop, ...)
│   ├── environment/    ← omgeving, muren, vloer, grote stukken
│   └── characters/     ← personages / rigs (indien nodig)
├── textures/
│   └── source/         ← brontextures op hoge resolutie (.psd, .kra, 4K)
├── references/         ← referentiebeelden, schetsen, moodboard
├── tools/
│   ├── export_to_unity.py              ← exportscript (zie onder)
│   └── presets/Unity_FBX.py            ← FBX-exportpreset
└── _archive/           ← oude versies die je niet meer gebruikt
```

## Afspraken

| Wat | Afspraak |
|---|---|
| Blender-versie | **Blender 4.x LTS** (vul exacte versie in: `____`) |
| Eenheden | Metric, Unit Scale **1.0** → 1 unit = 1 meter |
| Eén bestand per | asset (of kleine groep die samen hoort) |
| Eigenaar | Eén persoon bewerkt een `.blend` tegelijk (`git lfs lock`) |
| Textures | relatieve paden: *File → External Data → Make Paths Relative* |
| Origin | onderaan in het midden (objecten die ergens op staan) |
| Vóór export | *Ctrl+A → All Transforms* |

### Naamgeving

| Type | Prefix | Voorbeeld |
|---|---|---|
| Static mesh | `SM_` | `SM_Tafel` |
| Skeletal mesh | `SK_` | `SK_Robot` |
| Material | `M_` | `M_Hout` |
| Texture | `T_` + suffix | `T_Hout_Albedo`, `T_Hout_Normal`, `T_Hout_ORM` |
| Collider-mesh | `UCX_` | `UCX_Tafel` |

Gebruik PascalCase, geen spaties en geen leestekens. De objectnaam in Blender is de
bestandsnaam van de FBX.

### Budget voor Quest (VR)

- Prop: < 5k driehoeken, groot object/omgeving: < 20k
- Max. 1–2 materialen per object
- Textures 1024 of 2048 (macht van 2) in Unity; bron mag groter
- Principled BSDF gebruiken (wordt URP Lit in Unity)

## Exporteren naar Unity

### Optie 1: script (aanbevolen)
1. Open je `.blend` (opgeslagen ergens onder `blender/`).
2. Selecteer de object(en) die je wilt exporteren.
3. Ga naar de **Scripting**-tab → *Open* → `blender/tools/export_to_unity.py` → **Run Script**.

Elk geselecteerd object wordt een aparte FBX in
`mixed_reality/Assets/_Project/Models/<NaamVanObject>.fbx`, rond de wereldoorsprong
en met de juiste assen en schaal voor Unity.

### Optie 2: handmatig met de preset
1. Kopieer `tools/presets/Unity_FBX.py` naar:
   - Windows: `%APPDATA%\Blender Foundation\Blender\<versie>\scripts\presets\operator\export_scene.fbx\`
   - (maak de map `export_scene.fbx` aan als die niet bestaat)
2. *File → Export → FBX* → kies bovenaan de preset **Unity_FBX**.
3. Sla op in `mixed_reality/Assets/_Project/Models/`.

## Git

- `.blend`, `.fbx`, textures en audio gaan via **Git LFS** (zie `.gitattributes` in de root).
- Lock een bestand voordat je eraan werkt:
  ```bash
  git lfs lock blender/models/props/Tafel.blend
  git lfs unlock blender/models/props/Tafel.blend   # na je push
  git lfs locks                                    # wie werkt waaraan?
  ```
- `.blend1`-back-ups worden genegeerd.
