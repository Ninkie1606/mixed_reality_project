import bpy
op = bpy.context.active_operator

# --- Wat exporteren ---
op.use_selection = True
op.use_visible = False
op.use_active_collection = False
op.object_types = {'MESH', 'ARMATURE', 'EMPTY'}
op.use_custom_props = False

# --- Schaal en assen (Blender -> Unity) ---
op.global_scale = 1.0
op.apply_unit_scale = True
op.apply_scale_options = 'FBX_SCALE_ALL'
op.axis_forward = '-Z'
op.axis_up = 'Y'
op.use_space_transform = True
op.bake_space_transform = True   # "Apply Transform": geen -90° rotatie in Unity

# --- Geometrie ---
op.use_mesh_modifiers = True
op.use_mesh_modifiers_render = True
op.mesh_smooth_type = 'FACE'
op.use_subsurf = False
op.use_mesh_edges = False
op.use_tspace = False
op.use_triangles = False

# --- Armature ---
op.add_leaf_bones = False
op.primary_bone_axis = 'Y'
op.secondary_bone_axis = 'X'
op.use_armature_deform_only = True
op.armature_nodetype = 'NULL'

# --- Animatie ---
op.bake_anim = True
op.bake_anim_use_all_bones = True
op.bake_anim_use_nla_strips = False
op.bake_anim_use_all_actions = True
op.bake_anim_force_startend_keying = True
op.bake_anim_step = 1.0
op.bake_anim_simplify_factor = 1.0

# --- Overig ---
op.path_mode = 'AUTO'
op.embed_textures = False
op.batch_mode = 'OFF'
