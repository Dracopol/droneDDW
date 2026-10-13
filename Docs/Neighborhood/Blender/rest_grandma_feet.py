"""Puts Grandma's feet flat on the ground in Street_Grandma.fbx (DroneDDW Neighborhood).

She sits in all three poses, her origin on the ground under her bench. Her soles reach below it: 1.6 cm in Nature and
Balanced, 6.6 cm in Efficiency (that pose sits her 5 cm lower, feet and all, for the lower bench), so in the street her
shoes sink into the ground. This script re-poses only her legs, frame by frame, keeping everything else (and the
breathing) as it is:
1. per clip, how far her lowest point (the soles) is below her origin, at the clip's first frame;
2. each ankle goes up by that much, and thigh and shin reach it with two-bone IK, the knee where it was;
3. each foot keeps its turn in the world, so the shoe stays flat.
Then it exports with the original's settings and checks the new file against it (street_fbx.py): the same nodes,
meshes and clips, every bone outside her legs unchanged in every clip, and her soles within 3 mm above the ground in
every 10th frame of the sitting poses. Only then does it replace the FBX.

Run it with Blender 5.x (Unity may stay open, it only writes the FBX; Unity reimports it on focus):
    "C:\\Blender\\blender.exe" -b --factory-startup --python Docs/Neighborhood/Blender/rest_grandma_feet.py
Options, after "--":
    --fbx <path>        the FBX to change (default: Assets/Content/Neighborhood/Models/Street_Grandma.fbx here)
    --renders <folder>  also save side views of the sitting poses before and after
A file whose feet rest on the ground already is left alone. Run it after rest_grandma_hands.py on a fresh export.
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import street_fbx  # noqa: E402
from street_fbx import fail, find_action, import_fbx, set_action  # noqa: E402

SIDES = ("Left", "Right")
LEG_PARTS = ("UpLeg", "Leg", "Foot")
SITTING_CLIPS = ("Pose_Nature", "Pose_Balanced", "Pose_Efficiency")

# The soles end up this far above the ground, and may be at most this far off it after the change
CLEARANCE_METRES = 0.001
REST_LIMIT_METRES = 0.003


def main():
    street_fbx.log_prefix = "[Feet]"
    options = parse_arguments()
    source = os.path.abspath(options.fbx)
    if not os.path.isfile(source):
        fail(f"{source} doesn't exist.")

    street_fbx.log(source)
    scene = import_fbx(source)
    before = {clip: lowest_point(scene, find_action(clip), 1) for clip in SITTING_CLIPS}
    street_fbx.log("Soles now " + ", ".join(f"{clip} {height * 100:.1f} cm" for clip, height in before.items())
                   + " from the ground.")
    if all(abs(height) <= REST_LIMIT_METRES for height in before.values()):
        street_fbx.log("Her feet rest on the ground already. Nothing to do.")
        return

    if options.renders:
        # Rendering changes visibility, so the change starts again from a clean import.
        render_sitting_poses(scene, options.renders, "before")
        scene = import_fbx(source)

    for clip in SITTING_CLIPS:
        raise_feet(scene, find_action(clip), CLEARANCE_METRES - before[clip])
    street_fbx.log(f"Re-posed her legs in {', '.join(SITTING_CLIPS)}.")
    folder, fixed = street_fbx.export_for_check(source, scene["armature"])

    original_bones = street_fbx.sample_bones(import_fbx(source))
    fixed_scene = import_fbx(fixed)
    leg_bones = collect_leg_bones(fixed_scene["armature"])
    problems = street_fbx.compare_files(source, fixed, changed_nodes=leg_bones)
    problems += street_fbx.compare_bones(original_bones, street_fbx.sample_bones(fixed_scene),
                                         lambda clip, bone: clip in SITTING_CLIPS and bone in leg_bones)
    lowest, highest = sole_range(fixed_scene)
    street_fbx.log(f"Soles now {lowest * 100:.2f} to {highest * 100:.2f} cm above the ground in every 10th sitting frame.")
    if lowest < -REST_LIMIT_METRES or highest > REST_LIMIT_METRES:
        problems.append(f"the soles are {lowest * 100:.2f} to {highest * 100:.2f} cm from the ground")

    if options.renders and not problems:
        render_sitting_poses(fixed_scene, options.renders, "after")
    street_fbx.replace_or_fail(source, fixed, folder, problems)


def parse_arguments():
    parser = argparse.ArgumentParser(description="Puts Grandma's feet flat on the ground in Street_Grandma.fbx.")
    parser.add_argument("--fbx", default=street_fbx.GRANDMA_FBX)
    parser.add_argument("--renders")
    return parser.parse_args(street_fbx.arguments())


# The pose

def raise_feet(scene, action, rise):
    """Keys new leg rotations on every frame of a sitting clip, each ankle `rise` metres higher."""
    if rise <= 0.0:
        return

    armature = scene["armature"]
    bones = armature.pose.bones
    set_action(armature, action)
    # The rise in armature space (the armature may be scaled and turned against the world)
    up = armature.matrix_world.inverted().to_3x3() @ Vector((0.0, 0.0, rise))
    first, last = (int(frame) for frame in action.frame_range)
    for frame in range(first, last + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        for side in SIDES:
            thigh = bones[side + "UpLeg"]
            shin = bones[side + "Leg"]
            foot = bones[side + "Foot"]
            foot_turn = foot.matrix.to_3x3().copy()
            ankle = foot.head + up
            # The knee stays on its side of the leg: pole ahead of the knee, away from the hip-ankle line
            knee = shin.head.copy()
            line = (foot.head - thigh.head).normalized()
            pole = knee + (knee - thigh.head - line * (knee - thigh.head).dot(line)).normalized() * 0.3
            reach(armature, side, ankle, pole)
            turned = foot_turn.to_4x4()
            turned.translation = foot.matrix.translation
            foot.matrix = turned
            bpy.context.view_layer.update()
            for part in LEG_PARTS:
                bones[side + part].keyframe_insert("rotation_quaternion", frame=frame)


def reach(armature, side, ankle, pole):
    """Two-bone IK: turns the thigh and shin so the ankle lands on the target, the knee toward the pole."""
    bones = armature.pose.bones
    upper = bones[side + "UpLeg"]
    lower = bones[side + "Leg"]
    hip = upper.head.copy()
    upper_length = upper.bone.length
    lower_length = lower.bone.length
    offset = ankle - hip
    distance = min(offset.length, (upper_length + lower_length) * 0.999)
    direction = offset.normalized()
    cosine = (upper_length ** 2 + distance ** 2 - lower_length ** 2) / (2.0 * upper_length * distance)
    angle = math.acos(max(-1.0, min(1.0, cosine)))
    bend = pole - hip
    bend = (bend - direction * bend.dot(direction)).normalized()
    knee = hip + (direction * math.cos(angle) + bend * math.sin(angle)) * upper_length
    aim(upper, knee)
    aim(lower, ankle)


def aim(bone, target):
    """Turns a bone the shortest way so it points at a target (keeping its twist as far as possible)."""
    matrix = bone.matrix.copy()
    along = matrix.to_3x3().col[1].normalized()
    turn = along.rotation_difference((target - matrix.translation).normalized())
    aimed = (turn.to_matrix() @ matrix.to_3x3()).to_4x4()
    aimed.translation = matrix.translation
    bone.matrix = aimed
    bpy.context.view_layer.update()


def collect_leg_bones(armature):
    """The leg chains and everything under them (the toes): what may move in the sitting clips."""
    names = set()
    for side in SIDES:
        root = armature.data.bones[side + "UpLeg"]
        names.add(root.name)
        names.update(bone.name for bone in root.children_recursive)
    return names


# Checks and renders

def lowest_point(scene, action, frame):
    """Height of her lowest point (as posed at a frame) above her origin (the file's origin, Unity's too), in metres."""
    armature = scene["armature"]
    set_action(armature, action)
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    lowest = None
    for obj in scene["meshes"].values():
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        for vertex in mesh.vertices:
            height = (evaluated.matrix_world @ vertex.co).z
            if lowest is None or height < lowest:
                lowest = height
        evaluated.to_mesh_clear()
    if lowest is None:
        fail("Grandma has no mesh.")
    return lowest


def sole_range(scene):
    """The lowest and highest the soles get over every 10th frame of the sitting clips."""
    lowest, highest = None, None
    for clip in SITTING_CLIPS:
        action = find_action(clip)
        first, last = (int(frame) for frame in action.frame_range)
        for frame in range(first, last + 1, 10):
            height = lowest_point(scene, action, frame)
            lowest = height if lowest is None else min(lowest, height)
            highest = height if highest is None else max(highest, height)
    return lowest, highest


def render_sitting_poses(scene, folder, label):
    """A side view of each sitting pose with her own colours, the ground line at her origin."""
    os.makedirs(folder, exist_ok=True)
    armature = scene["armature"]
    view = bpy.context.scene
    for obj in scene["meshes"].values():
        is_spare_face = ("_eyes_" in obj.name and not obj.name.endswith("_usual")) \
            or ("_mouth_" in obj.name and not obj.name.endswith("_smile"))
        obj.hide_render = is_spare_face

    bpy.ops.mesh.primitive_plane_add(size=3.0, location=armature.matrix_world.translation)
    view.render.engine = "BLENDER_WORKBENCH"
    view.display.shading.light = "STUDIO"
    view.display.shading.color_type = "MATERIAL"
    view.display.shading.show_object_outline = True
    view.render.resolution_x = 500
    view.render.resolution_y = 600
    camera = bpy.data.objects.new("Feet camera", bpy.data.cameras.new("Feet camera"))
    view.collection.objects.link(camera)
    camera.data.lens = 50
    view.camera = camera
    for clip in SITTING_CLIPS:
        set_action(armature, find_action(clip))
        view.frame_set(1)
        target = armature.matrix_world.translation + Vector((0.0, 0.0, 0.35))
        camera.location = target + Vector((1.0, -0.2, 0.0)).normalized() * 2.6 + Vector((0.0, 0.0, 0.1))
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        view.render.filepath = os.path.join(folder, f"grandma_feet_{label}_{clip}.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
