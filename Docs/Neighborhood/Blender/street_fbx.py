"""Shared steps for the scripts that fix the Neighborhood's Street_*.fbx files in Blender (DroneDDW).

The FBX files were exported by Blender (5.0.1) from a generator that isn't in this repo. A fix imports a file, changes
it, exports it with the settings the original was made with, compares the new file with the original, and only
replaces the original when everything that should stay the same did:
- what Unity reads: global settings (axes, units, frame rate), nodes and their transforms, meshes, the skin's bind
  matrices, the clips and their key count (compare_files);
- every bone's world position and rotation at five frames of every clip (sample_bones, compare_bones).
Each script names what it changes on purpose (meshes, nodes, or bones in some clips), and only those may differ.
"""

import math
import os
import shutil
import sys
import tempfile

import bpy
from io_scene_fbx import parse_fbx
from mathutils import Euler

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
GRANDMA_FBX = os.path.join(REPO_ROOT, "Assets", "Content", "Neighborhood", "Models", "Street_Grandma.fbx")

# The pose the original files were saved in (their nodes' default transforms); Unity shows it until a clip plays.
SAVED_POSE_ACTION = "Pose_Efficiency"
FRAMES_PER_SECOND = 30

# Reproduces the originals: same global settings, nodes, meshes and bind matrices, every frame of every clip keyed
EXPORT_SETTINGS = dict(
    object_types={"ARMATURE", "MESH"},
    apply_scale_options="FBX_SCALE_UNITS",
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0.0,
    path_mode="STRIP",
)

BONE_LIMIT_METRES = 0.0005
BONE_LIMIT_DEGREES = 0.05
GEOMETRY_LIMIT = 1e-5
MATRIX_LIMIT = 1e-4

# The calling script's tag in front of every message, e.g. "[Trousers]"
log_prefix = "[Street FBX]"


def log(message):
    print(f"{log_prefix} {message}")


def fail(message):
    log(message)
    sys.exit(1)


def arguments():
    """The script's own arguments: everything after "--" on Blender's command line."""
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


# Import, lookups and export

def import_fbx(path):
    """Imports into an empty scene. Returns the armature and the meshes by name (valid until the next import)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    scene = {"meshes": {}}
    for obj in bpy.context.scene.objects:
        if obj.type == "ARMATURE":
            scene["armature"] = obj
        elif obj.type == "MESH":
            scene["meshes"][obj.name] = obj

    if "armature" not in scene:
        fail(f"{path} has no armature.")
    return scene


def find_mesh(scene, name):
    mesh = scene["meshes"].get(name)
    if mesh is None:
        fail(f"No mesh called {name}.")
    return mesh


def find_action(clip):
    action = next((item for item in bpy.data.actions if clip_name(item) == clip), None)
    if action is None:
        fail(f"No clip called {clip}.")
    return action


def set_action(armature, action):
    animation = armature.animation_data or armature.animation_data_create()
    animation.action = action
    if hasattr(animation, "action_slot") and animation.action_slot is None and len(action.slots) > 0:
        animation.action_slot = action.slots[0]


def clip_name(action):
    return action.name.split("|")[-1]


def export_for_check(source, armature):
    """
    Exports the open scene next to nothing the project watches, ready to compare. The importer prefixes the object
    name to each take ("Grandma_Rig|Grandma_Rig|Pose_Nature") and the exporter would add it again, so the actions go
    back to their own names first; the scene is left in the saved pose. Returns (folder, path).
    """
    for action in bpy.data.actions:
        action.name = clip_name(action)

    bpy.context.scene.render.fps = FRAMES_PER_SECOND
    set_action(armature, bpy.data.actions[SAVED_POSE_ACTION])
    bpy.context.scene.frame_set(1)
    folder = tempfile.mkdtemp(prefix="street_fbx_")
    path = os.path.join(folder, os.path.basename(source))
    bpy.ops.export_scene.fbx(filepath=path, **EXPORT_SETTINGS)
    return folder, path


def replace_or_fail(source, fixed, folder, problems):
    if problems:
        for problem in problems:
            log(f"Check failed: {problem}")
        fail(f"Left {source} unchanged; the new file is in {fixed}.")

    shutil.copyfile(fixed, source)
    shutil.rmtree(folder, ignore_errors=True)
    log(f"All checks passed; replaced {source}.")


# Checks

def sample_bones(scene):
    """World position and rotation of every bone at five frames of every clip."""
    armature = scene["armature"]
    samples = {}
    for action in bpy.data.actions:
        set_action(armature, action)
        first, last = (int(frame) for frame in action.frame_range)
        for frame in sorted({first, first + (last - first) // 4, (first + last) // 2, last - (last - first) // 4, last}):
            bpy.context.scene.frame_set(frame)
            for bone in armature.pose.bones:
                matrix = armature.matrix_world @ bone.matrix
                samples[(clip_name(action), frame, bone.name)] = (matrix.to_translation(), matrix.to_quaternion())
    return samples


def compare_bones(original, fixed, is_changed=None):
    """Bones must match within the limits, except the (clip, bone) pairs is_changed(clip, bone) allows to differ."""
    if set(original) != set(fixed):
        return [f"different clips, frames or bones ({len(original)} samples before, {len(fixed)} after)"]

    worst_metres = 0.0
    worst_degrees = 0.0
    for key, (position, rotation) in original.items():
        if is_changed is not None and is_changed(key[0], key[2]):
            continue
        fixed_position, fixed_rotation = fixed[key]
        worst_metres = max(worst_metres, (position - fixed_position).length)
        worst_degrees = max(worst_degrees, math.degrees(rotation.rotation_difference(fixed_rotation).angle))

    log(f"Unchanged bones: at most {worst_metres * 1000:.3f} mm and {worst_degrees:.4f} degrees apart in every clip.")
    if worst_metres > BONE_LIMIT_METRES or worst_degrees > BONE_LIMIT_DEGREES:
        return [f"bones moved by up to {worst_metres * 1000:.3f} mm / {worst_degrees:.4f} degrees"]
    return []


def compare_files(original_path, fixed_path, changed_meshes=(), changed_nodes=()):
    """Compares what Unity reads. Meshes whose names start with one of changed_meshes, and the transforms of the
    changed_nodes, may differ."""
    original, _ = parse_fbx.parse(original_path)
    fixed, _ = parse_fbx.parse(fixed_path)
    problems = []

    if properties(child(original, b"GlobalSettings")) != properties(child(fixed, b"GlobalSettings")):
        problems.append("the global settings (axes, units, frame rate) differ")

    original_models = objects(original, b"Model")
    fixed_models = objects(fixed, b"Model")
    if sorted(original_models) != sorted(fixed_models):
        problems.append(f"different nodes: {sorted(set(original_models) ^ set(fixed_models))}")
    for name in set(original_models) & set(fixed_models):
        if name in changed_nodes:
            continue
        before, after = properties(original_models[name]), properties(fixed_models[name])
        for key, identity in (("Lcl Translation", (0.0, 0.0, 0.0)), ("Lcl Scaling", (1.0, 1.0, 1.0))):
            if not close(before.get(key, identity), after.get(key, identity), MATRIX_LIMIT):
                problems.append(f"node {name}: {key} {before.get(key, identity)} -> {after.get(key, identity)}")
        # Euler angles can differ in their last digits for the same rotation, so rotations compare as angles.
        turn = rotation_degrees(before.get("Lcl Rotation", (0.0, 0.0, 0.0)), after.get("Lcl Rotation", (0.0, 0.0, 0.0)))
        if turn > BONE_LIMIT_DEGREES:
            problems.append(f"node {name} turned by {turn:.4f} degrees")

    original_meshes = objects(original, b"Geometry")
    fixed_meshes = objects(fixed, b"Geometry")
    if sorted(original_meshes) != sorted(fixed_meshes):
        problems.append(f"different meshes: {sorted(set(original_meshes) ^ set(fixed_meshes))}")
    for name in set(original_meshes) & set(fixed_meshes):
        if any(name.startswith(changed) for changed in changed_meshes):
            continue
        for key in (b"Vertices", b"PolygonVertexIndex"):
            if not close(array(original_meshes[name], key), array(fixed_meshes[name], key), GEOMETRY_LIMIT):
                problems.append(f"mesh {name} changed ({key.decode()})")

    original_links = bind_matrices(original)
    fixed_links = bind_matrices(fixed)
    for bone, matrix in fixed_links.items():
        if bone not in original_links or not close(original_links[bone], matrix, MATRIX_LIMIT):
            problems.append(f"bone {bone} has a different bind matrix")

    if sorted(objects(original, b"AnimationStack")) != sorted(objects(fixed, b"AnimationStack")):
        problems.append("the clips differ")
    original_keys = sum(len(array(curve, b"KeyTime") or []) for curve in objects_list(original, b"AnimationCurve"))
    fixed_keys = sum(len(array(curve, b"KeyTime") or []) for curve in objects_list(fixed, b"AnimationCurve"))
    if original_keys != fixed_keys:
        problems.append(f"the clips have {fixed_keys} keys instead of {original_keys}")

    log(f"File check: {len(fixed_models)} nodes, {len(fixed_meshes)} meshes, "
        f"{len(objects(fixed, b'AnimationStack'))} clips, {fixed_keys} keys; {len(problems)} differences.")
    return problems


def child(element, name):
    for item in element.elems:
        if item.id == name:
            return item
    return None


def element_name(element):
    return element.props[1].decode(errors="replace").split("\x00")[0]


def objects(root, kind):
    return {element_name(item): item for item in objects_list(root, kind)}


def objects_list(root, kind):
    return [item for item in child(root, b"Objects").elems if item.id == kind]


def properties(element):
    table = child(element, b"Properties70") if element is not None else None
    if table is None:
        return {}
    return {item.props[0].decode(): tuple(item.props[4:]) for item in table.elems}


def array(element, key):
    item = child(element, key)
    return list(item.props[0]) if item is not None else None


def bind_matrices(root):
    """Bone name -> its bind matrix (a cluster's TransformLink; the same for every mesh the bone deforms)."""
    links = {}
    for deformer in objects_list(root, b"Deformer"):
        if deformer.props[2] == b"Cluster":
            links[element_name(deformer)] = array(deformer, b"TransformLink")
    return links


def rotation_degrees(first, second):
    """Angle between two FBX Lcl Rotations (XYZ Euler angles in degrees)."""
    quaternion = lambda angles: Euler([math.radians(angle) for angle in angles], "XYZ").to_quaternion()
    return math.degrees(quaternion(first).rotation_difference(quaternion(second)).angle)


def close(first, second, limit):
    if first is None or second is None:
        return first is second
    if len(first) != len(second):
        return False
    return all(abs(a - b) <= limit if isinstance(a, float) or isinstance(b, float) else a == b
               for a, b in zip(first, second))
