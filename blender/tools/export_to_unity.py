"""
Exporteert elk geselecteerd object als aparte FBX naar het Unity-project.

Gebruik:
  1. Sla je .blend op ergens onder <repo>/blender/
  2. Selecteer de object(en) die je wilt exporteren
  3. Scripting-tab -> Open dit bestand -> Run Script

Resultaat: <repo>/mixed_reality/Assets/_Project/Models/<ObjectNaam>.fbx
Children van een geselecteerd object gaan mee in dezelfde FBX.
"""

import os
import bpy

UNITY_PROJECT = "mixed_reality"
MODELS_SUBDIR = os.path.join("Assets", "_Project", "Models")


def find_repo_root(start):
    """Loop omhoog tot we een map vinden waarin het Unity-project zit."""
    folder = start
    while True:
        if os.path.isdir(os.path.join(folder, UNITY_PROJECT, "Assets")):
            return folder
        parent = os.path.dirname(folder)
        if parent == folder:
            return None
        folder = parent


def collect_hierarchy(obj):
    result = [obj]
    for child in obj.children_recursive:
        result.append(child)
    return result


def main():
    if not bpy.data.filepath:
        raise RuntimeError("Sla het .blend-bestand eerst op (onder <repo>/blender/).")

    repo_root = find_repo_root(os.path.dirname(bpy.data.filepath))
    if repo_root is None:
        raise RuntimeError(
            f"Geen map '{UNITY_PROJECT}/Assets' gevonden boven {bpy.data.filepath}."
        )

    out_dir = os.path.join(repo_root, UNITY_PROJECT, MODELS_SUBDIR)
    os.makedirs(out_dir, exist_ok=True)

    selected = [o for o in bpy.context.selected_objects]
    # Alleen top-level objecten van de selectie (children gaan automatisch mee)
    roots = [o for o in selected if o.parent not in selected]
    if not roots:
        raise RuntimeError("Niets geselecteerd.")

    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    exported = []
    for root in roots:
        # Tijdelijk naar de wereldoorsprong zodat de pivot in Unity klopt
        original_location = root.location.copy()
        root.location = (0.0, 0.0, 0.0)

        bpy.ops.object.select_all(action='DESELECT')
        for o in collect_hierarchy(root):
            o.select_set(True)
        bpy.context.view_layer.objects.active = root

        path = os.path.join(out_dir, f"{root.name}.fbx")
        bpy.ops.export_scene.fbx(
            filepath=path,
            use_selection=True,
            object_types={'MESH', 'ARMATURE', 'EMPTY'},
            global_scale=1.0,
            apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL',
            axis_forward='-Z',
            axis_up='Y',
            use_space_transform=True,
            bake_space_transform=True,
            use_mesh_modifiers=True,
            mesh_smooth_type='FACE',
            add_leaf_bones=False,
            use_armature_deform_only=True,
            bake_anim=True,
            path_mode='AUTO',
            embed_textures=False,
        )

        root.location = original_location
        exported.append(path)

    # Oorspronkelijke selectie herstellen
    bpy.ops.object.select_all(action='DESELECT')
    for o in selected:
        o.select_set(True)

    for p in exported:
        print(f"[export_to_unity] -> {p}")
    print(f"[export_to_unity] {len(exported)} object(en) geëxporteerd.")


main()
