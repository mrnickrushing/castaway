"""The shared humanoid clips: Quaternius' Universal Animation Library (CC0) cut down to the moves the game plays.

  blender -b --factory-startup -P Tools/blender/cast_moves.py -- <UAL1_Standard.fbx> <out.fbx>

Keeps the skeleton (no mesh) and the clips in KEEP, renamed to their short names; Unity imports the file as
Humanoid (Assets/Editor/ArtImport.cs) and every cast member (Tools/blender/cast_import.py) plays them.
"""
import sys

import bpy

a = sys.argv[sys.argv.index("--") + 1:]
src, out = a[0], a[1]
KEEP = ["Idle_Loop", "Idle_Talking_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Crouch_Idle_Loop", "Crouch_Fwd_Loop",
        "Sword_Idle", "Sword_Attack", "Punch_Jab", "Punch_Cross", "Hit_Chest", "Hit_Head", "Death01", "Interact",
        "PickUp_Table", "Fixing_Kneeling", "Sitting_Idle_Loop", "Swim_Fwd_Loop", "Swim_Idle_Loop", "Jump_Start",
        "Jump_Loop", "Jump_Land", "Roll", "Push_Loop", "Idle_Torch_Loop"]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
for o in [o for o in bpy.context.scene.objects if o.type != "ARMATURE"]:
    bpy.data.objects.remove(o)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
arm.name = "Armature"
for act in list(bpy.data.actions):
    short = act.name.split("|")[-1]
    if short in KEEP:
        act.name = short
        act.use_fake_user = True
    else:
        bpy.data.actions.remove(act)
arm.animation_data_create()
arm.animation_data.action = bpy.data.actions[KEEP[0]]
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", object_types={"ARMATURE"}, add_leaf_bones=False,
                         primary_bone_axis="Y", secondary_bone_axis="X", armature_nodetype="NULL",
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5)
print("[moves]", sorted(a.name for a in bpy.data.actions))
