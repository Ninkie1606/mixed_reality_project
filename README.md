# Mixed Reality Project

Dit project combineert 3D-modellen uit Blender met een interactieve Unity Mixed Reality / VR applicatie (gericht op Meta Quest / OpenXR).

## Mappenstructuur

- **`mixed_reality/`**: Het Unity-project met de MR/VR-interacties, scènes en assets.
- **`blender/`**: De 3D-bronbestanden (`.blend`), textures en export-tools.
  - Modellen worden conform afspraak als FBX geëxporteerd naar `mixed_reality/Assets/_Project/Models/`.
  - Raadpleeg [`blender/README.md`](blender/README.md) voor de naamgevingsconventies, Git LFS locking en het exportscript.

## Git & Bestandsbeheer

- Grote binaire bestanden (`.blend`, `.fbx`, textures, audio) worden beheerd via **Git LFS**.
- Voer bij de eerste installatie eenmalig `git lfs install` uit.
