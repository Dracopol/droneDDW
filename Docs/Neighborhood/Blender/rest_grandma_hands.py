"""Rests Grandma's hands on her thighs in Street_Grandma.fbx (DroneDDW Neighborhood).

In her three sitting poses (Pose_Nature, Pose_Balanced, Pose_Efficiency) her arms hover in the air: in Nature one hand
reaches out toward the ducks with nothing in it, in the others both float in front of her belly. That reads as stiff,
so this script re-poses only her arms, frame by frame, keeping everything else (and the breathing) as it is:
1. each wrist goes on top of its thigh, halfway between hip and knee, a little to the outside, and follows the thigh
   as she breathes (the target is measured once per clip in the thigh bone's own space);
2. upper arm and forearm reach it with two-bone IK, the elbow pointing out and back;
3. the hand lies palm down, fingers toward the knee and turned a little inward (the thumb points to her middle).
Then it exports with the original's settings and checks the new file against it (street_fbx.py): the same nodes,
meshes and clips, every bone outside her arms unchanged in every clip, her arms unchanged in the other clips, and
each wrist within 2 cm of its spot in every frame of the sitting poses. Only then does it replace the FBX.

Run it with Blender 5.x (Unity may stay open, it only writes the FBX; Unity reimports it on focus):
    "C:\\Blender\\blender.exe" -b --factory-startup --python Docs/Neighborhood/Blender/rest_grandma_hands.py
Options, after "--":
    --fbx <path>        the FBX to change (default: Assets/Content/Neighborhood/Models/Street_Grandma.fbx here)
    --renders <folder>  also save front views of the sitting poses before and after
A file whose hands rest already is left alone.
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import street_fbx  # noqa: E402
from street_fbx import fail, find_action, find_mesh, import_fbx, set_action  # noqa: E402

TROUSERS = "Grandma_Shorts"
SIDES = ("Left", "Right")
ARM_PARTS = ("Arm", "ForeArm", "Hand")
SITTING_CLIPS = ("Pose_Nature", "Pose_Balanced", "Pose_Efficiency")

# Where the wrist rests: along the thigh (0 hip, 1 knee), above the trousers' top, toward the outside of the leg
ALONG_THIGH = 0.5
ABOVE_TROUSERS_METRES = 0.035
OUTWARD_METRES = 0.03
SURFACE_SEARCH_METRES = 0.07

# The hand: fingers turned this far in from straight ahead, and tipped down to follow the thigh
INWARD_DEGREES = 15.0
FINGER_DROP = 0.15

# The elbow points out, back and down from the shoulder
POLE_OUT_METRES = 0.3
POLE_BACK_METRES = 0.3
POLE_DOWN_METRES = 0.2

REST_LIMIT_METRES = 0.02


def main():
    street_fbx.log_prefix = "[Hands]"
    options = parse_arguments()
    source = os.path.abspath(options.fbx)
    if not os.path.isfile(source):
        fail(f"{source} doesn't exist.")

    street_fbx.log(source)
    scene = import_fbx(source)
    before = measure_rest(scene)
    street_fbx.log(f"Wrists now up to {before * 100:.0f} cm from their spot on the thighs.")
    if before <= REST_LIMIT_METRES:
        street_fbx.log("Her hands rest on her thighs already. Nothing to do.")
        return

    if options.renders:
        # Rendering changes visibility, so the change starts again from a clean import.
        render_sitting_poses(scene, options.renders, "before")
        scene = import_fbx(source)

    for clip in SITTING_CLIPS:
        rest_hands(scene, find_action(clip))
    street_fbx.log(f"Re-posed her arms in {', '.join(SITTING_CLIPS)}.")
    folder, fixed = street_fbx.export_for_check(source, scene["armature"])

    original_bones = street_fbx.sample_bones(import_fbx(source))
    fixed_scene = import_fbx(fixed)
    arm_bones = collect_arm_bones(fixed_scene["armature"])
    problems = street_fbx.compare_files(source, fixed,
                                        changed_nodes={side + part for side in SIDES for part in ARM_PARTS})
    problems += street_fbx.compare_bones(original_bones, street_fbx.sample_bones(fixed_scene),
                                         lambda clip, bone: clip in SITTING_CLIPS and bone in arm_bones)
    after = measure_rest(fixed_scene)
    street_fbx.log(f"Wrists now at most {after * 100:.1f} cm from their spot on the thighs in every sitting frame.")
    if after > REST_LIMIT_METRES:
        problems.append(f"a wrist is {after * 100:.1f} cm from its spot on the thigh")

    if options.renders and not problems:
        render_sitting_poses(fixed_scene, options.renders, "after")
    street_fbx.replace_or_fail(source, fixed, folder, problems)


def parse_arguments():
    parser = argparse.ArgumentParser(description="Rests Grandma's hands on her thighs in Street_Grandma.fbx.")
    parser.add_argument("--fbx", default=street_fbx.GRANDMA_FBX)
    parser.add_argument("--renders")
    return parser.parse_args(street_fbx.arguments())


# The pose

def rest_hands(scene, action):
    """Keys new arm rotations on every frame of a sitting clip."""
    armature = scene["armature"]
    bones = armature.pose.bones
    thumbs = {side: thumb_direction(armature, side) for side in SIDES}
    set_action(armature, action)
    first, last = (int(frame) for frame in action.frame_range)
    bpy.context.scene.frame_set(first)
    bpy.context.view_layer.update()
    spots = {side: bones[side + "UpLeg"].matrix.inverted() @ wrist_spot(scene, side) for side in SIDES}
    for frame in range(first, last + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        up = world_up(armature)
        middle = (bones["LeftUpLeg"].head + bones["RightUpLeg"].head) / 2
        for side in SIDES:
            thigh = bones[side + "UpLeg"]
            forward = flatten(bones[side + "Leg"].head - thigh.head, up)
            outward = flatten(thigh.head - middle, up)
            wrist = thigh.matrix @ spots[side]
            shoulder = bones[side + "Arm"].head
            pole = shoulder + outward * POLE_OUT_METRES - forward * POLE_BACK_METRES - up * POLE_DOWN_METRES
            reach(armature, side, wrist, pole)
            lay_hand(armature, side, forward, outward, up, thumbs[side])
            for part in ARM_PARTS:
                bones[side + part].keyframe_insert("rotation_quaternion", frame=frame)


def wrist_spot(scene, side):
    """Where the wrist goes, in armature space, for the pose shown now."""
    armature = scene["armature"]
    bones = armature.pose.bones
    up = world_up(armature)
    middle = (bones["LeftUpLeg"].head + bones["RightUpLeg"].head) / 2
    hip = bones[side + "UpLeg"].head
    point = hip.lerp(bones[side + "Leg"].head, ALONG_THIGH)
    outward = flatten(point - middle, up)
    surface = trouser_top(scene, point, up)
    return point + up * (surface + ABOVE_TROUSERS_METRES) + outward * OUTWARD_METRES


def trouser_top(scene, point, up):
    """Height of the highest trouser point (as posed now) within SURFACE_SEARCH_METRES of a point, seen from above."""
    armature = scene["armature"]
    trousers = find_mesh(scene, TROUSERS)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = trousers.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    to_armature = armature.matrix_world.inverted() @ evaluated.matrix_world
    highest = None
    for vertex in mesh.vertices:
        offset = to_armature @ vertex.co - point
        height = offset.dot(up)
        if (offset - up * height).length < SURFACE_SEARCH_METRES and (highest is None or height > highest):
            highest = height
    evaluated.to_mesh_clear()
    if highest is None:
        fail(f"No trousers above the {point} on the thigh.")
    return highest


def reach(armature, side, wrist, pole):
    """Two-bone IK: turns the upper arm and forearm so the wrist lands on the target, the elbow toward the pole."""
    bones = armature.pose.bones
    upper = bones[side + "Arm"]
    lower = bones[side + "ForeArm"]
    shoulder = upper.head.copy()
    upper_length = upper.bone.length
    lower_length = lower.bone.length
    offset = wrist - shoulder
    distance = min(offset.length, (upper_length + lower_length) * 0.999)
    direction = offset.normalized()
    cosine = (upper_length ** 2 + distance ** 2 - lower_length ** 2) / (2.0 * upper_length * distance)
    angle = math.acos(max(-1.0, min(1.0, cosine)))
    bend = pole - shoulder
    bend = (bend - direction * bend.dot(direction)).normalized()
    elbow = shoulder + (direction * math.cos(angle) + bend * math.sin(angle)) * upper_length
    aim(upper, elbow)
    aim(lower, wrist)


def aim(bone, target):
    """Turns a bone the shortest way so it points at a target (keeping its twist as far as possible)."""
    matrix = bone.matrix.copy()
    along = matrix.to_3x3().col[1].normalized()
    turn = along.rotation_difference((target - matrix.translation).normalized())
    aimed = (turn.to_matrix() @ matrix.to_3x3()).to_4x4()
    aimed.translation = matrix.translation
    bone.matrix = aimed
    bpy.context.view_layer.update()


def lay_hand(armature, side, forward, outward, up, thumb):
    """Palm down on the thigh: fingers (the bone's Y axis) toward the knee, turned inward, the thumb to her middle."""
    hand = armature.pose.bones[side + "Hand"]
    turn = math.radians(INWARD_DEGREES) * (1.0 if side == "Left" else -1.0)
    fingers = (Matrix.Rotation(turn, 3, up) @ forward - up * FINGER_DROP).normalized()
    inward = -outward
    thumb_side = (inward - fingers * inward.dot(fingers)).normalized()
    source = Matrix((thumb, Vector((0.0, 1.0, 0.0)), Vector((0.0, 1.0, 0.0)).cross(thumb))).transposed()
    target = Matrix((thumb_side, fingers, fingers.cross(thumb_side))).transposed()
    laid = (target @ source.inverted()).to_4x4()
    laid.translation = hand.matrix.translation
    hand.matrix = laid
    bpy.context.view_layer.update()


def thumb_direction(armature, side):
    """Which way the thumb sits from the hand, in the hand bone's own axes (rest pose), across the bone."""
    hand = armature.data.bones[side + "Hand"]
    thumb = armature.data.bones[side + "HandThumb1"]
    direction = hand.matrix_local.inverted().to_3x3() @ (thumb.head_local - hand.head_local)
    direction.y = 0.0
    return direction.normalized()


def world_up(armature):
    """World up in armature space."""
    return (armature.matrix_world.inverted().to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()


def flatten(direction, up):
    return (direction - up * direction.dot(up)).normalized()


def collect_arm_bones(armature):
    """The arm chains and everything under them (fingers, the prop bones): what may move in the sitting clips."""
    names = set()
    for side in SIDES:
        root = armature.data.bones[side + "Arm"]
        names.add(root.name)
        names.update(bone.name for bone in root.children_recursive)
    return names


# Checks and renders

def measure_rest(scene):
    """The furthest any wrist is from its spot on the thigh, over every 10th frame of the sitting clips."""
    armature = scene["armature"]
    bones = armature.pose.bones
    furthest = 0.0
    for clip in SITTING_CLIPS:
        action = find_action(clip)
        set_action(armature, action)
        first, last = (int(frame) for frame in action.frame_range)
        bpy.context.scene.frame_set(first)
        bpy.context.view_layer.update()
        spots = {side: bones[side + "UpLeg"].matrix.inverted() @ wrist_spot(scene, side) for side in SIDES}
        for frame in range(first, last + 1, 10):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            for side in SIDES:
                spot = bones[side + "UpLeg"].matrix @ spots[side]
                furthest = max(furthest, (bones[side + "Hand"].head - spot).length)
    return furthest


def render_sitting_poses(scene, folder, label):
    """A front view, a little from her left, of each sitting pose with her own colours."""
    os.makedirs(folder, exist_ok=True)
    armature = scene["armature"]
    view = bpy.context.scene
    for obj in scene["meshes"].values():
        is_spare_face = ("_eyes_" in obj.name and not obj.name.endswith("_usual")) \
            or ("_mouth_" in obj.name and not obj.name.endswith("_smile"))
        obj.hide_render = is_spare_face

    view.render.engine = "BLENDER_WORKBENCH"
    view.display.shading.light = "STUDIO"
    view.display.shading.color_type = "MATERIAL"
    view.display.shading.show_object_outline = True
    view.render.resolution_x = 500
    view.render.resolution_y = 600
    camera = bpy.data.objects.new("Hands camera", bpy.data.cameras.new("Hands camera"))
    view.collection.objects.link(camera)
    camera.data.lens = 50
    view.camera = camera
    for clip in SITTING_CLIPS:
        set_action(armature, find_action(clip))
        view.frame_set(1)
        hips = armature.matrix_world @ armature.pose.bones["Hips"].head
        target = hips + Vector((0.0, 0.0, 0.25))
        camera.location = target + Vector((0.5, -1.0, 0.0)).normalized() * 3.0 + Vector((0.0, 0.0, 0.3))
        camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
        view.render.filepath = os.path.join(folder, f"grandma_hands_{label}_{clip}.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
