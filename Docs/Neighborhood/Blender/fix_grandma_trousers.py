"""Fixes Grandma's trousers in Street_Grandma.fbx (DroneDDW Neighborhood).

Her trousers (the mesh "Grandma_Shorts") were stretched from shorts. Each leg has rings from the hip to mid-thigh,
then one straight segment of about 54 cm down to the ankle hem: no loop at the knee and no weights on the shin
bones. She sits in every pose, so the trousers stay straight tubes, her shins come out underneath and her thighs
poke through on top.

This script:
1. imports the FBX into an empty Blender scene;
2. splits every trouser edge longer than 30 cm (top to bottom) into 8 segments, so the legs can bend at the knee;
3. weights the trousers from the body's hip and leg triangles (nearest point, barycentric, at most 4 bones);
4. exports with the settings the original was made with and checks the new file against it (street_fbx.py): the
   same nodes, meshes (but the trousers), clips and global settings, every bone within 0.5 mm in every clip, and
   the body no more than POKE_LIMIT_METRES outside the trousers in any clip. Only then does it replace the FBX.

Run it with Blender 5.x (Unity may stay open, it only writes the FBX; Unity reimports it on focus):
    "C:\\Blender\\blender.exe" -b --factory-startup --python Docs/Neighborhood/Blender/fix_grandma_trousers.py
Options, after "--":
    --fbx <path>        the FBX to fix (default: Assets/Content/Neighborhood/Models/Street_Grandma.fbx here)
    --check             only measure how far the body pokes through the trousers now; change nothing
    --renders <folder>  also save side and front views of the sitting poses before and after
A file that was fixed already is left alone (its trousers have no long edges any more).
"""

import argparse
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import street_fbx  # noqa: E402
from street_fbx import clip_name, fail, find_mesh, import_fbx, set_action  # noqa: E402

BODY = "Grandma_Body"
TROUSERS = "Grandma_Shorts"

# Bones the trousers may follow. Taking weights only from body triangles fully on these keeps the hands and arms
# (which hang near the hips in some poses) out of the trousers.
LEG_BONES = {"Hips", "Spine", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot"}
LEG_SHARE = 0.95
LONG_EDGE_METRES = 0.3
CUTS = 7
MAX_INFLUENCES = 4
SHARP_NORMAL_DEGREES = 1.0

POKE_LIMIT_METRES = 0.006
POKE_COUNT_METRES = 0.003
COVER_SEARCH_METRES = 0.05


def main():
    street_fbx.log_prefix = "[Trousers]"
    options = parse_arguments()
    source = os.path.abspath(options.fbx)
    if not os.path.isfile(source):
        fail(f"{source} doesn't exist.")

    street_fbx.log(source)
    scene = import_fbx(source)
    long_edges = count_long_edges(find_mesh(scene, TROUSERS))
    print_poke_through("now", measure_poke_through(scene))
    if options.check:
        return

    if long_edges == 0:
        street_fbx.log("The trousers have no long edges any more: this file was fixed already. Nothing to do.")
        return

    if options.renders:
        # Rendering changes colours and visibility, so the fix starts again from a clean import.
        render_sitting_poses(scene, options.renders, "before")
        scene = import_fbx(source)

    trousers = find_mesh(scene, TROUSERS)
    sharp_edges = mark_split_normals_sharp(trousers)
    split_long_edges(trousers)
    weight_from_body(trousers, find_mesh(scene, BODY), scene["armature"])
    street_fbx.log(f"Split {long_edges} long edges into {CUTS + 1} segments each ({len(trousers.data.vertices)} "
                   f"vertices now), kept {sharp_edges} hard edges, weighted from the body.")
    folder, fixed = street_fbx.export_for_check(source, scene["armature"])

    original_bones = street_fbx.sample_bones(import_fbx(source))
    fixed_scene = import_fbx(fixed)
    problems = street_fbx.compare_files(source, fixed, changed_meshes=(TROUSERS,))
    problems += street_fbx.compare_bones(original_bones, street_fbx.sample_bones(fixed_scene))
    after = measure_poke_through(fixed_scene)
    print_poke_through("fixed", after)
    for clip, (deepest, _) in after.items():
        if deepest > POKE_LIMIT_METRES:
            problems.append(f"{clip}: the body still pokes {deepest * 1000:.1f} mm through the trousers")

    if options.renders and not problems:
        render_sitting_poses(fixed_scene, options.renders, "after")
    street_fbx.replace_or_fail(source, fixed, folder, problems)


def parse_arguments():
    parser = argparse.ArgumentParser(description="Fixes Grandma's trousers in Street_Grandma.fbx.")
    parser.add_argument("--fbx", default=street_fbx.GRANDMA_FBX)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--renders")
    return parser.parse_args(street_fbx.arguments())


# Lookups

def bone_weights(obj, bone_names):
    """Per vertex: [(weight, bone)] for deforming groups, heaviest first."""
    group_names = {group.index: group.name for group in obj.vertex_groups}
    weights = []
    for vertex in obj.data.vertices:
        pairs = [(group.weight, group_names[group.group]) for group in vertex.groups
                 if group.weight > 0.001 and group_names[group.group] in bone_names]
        weights.append(sorted(pairs, reverse=True))
    return weights


def world_rest_positions(obj):
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


# The fix

def count_long_edges(obj):
    world = obj.matrix_world
    count = 0
    for edge in obj.data.edges:
        top = world @ obj.data.vertices[edge.vertices[0]].co
        bottom = world @ obj.data.vertices[edge.vertices[1]].co
        if abs(top.z - bottom.z) > LONG_EDGE_METRES:
            count += 1
    return count


def mark_split_normals_sharp(obj):
    """
    The imported trousers carry custom normals, which new vertices wouldn't get. Keeps their look by marking every
    edge where the normals split as sharp, then lets Blender compute the normals (smooth, sharp where marked).
    """
    mesh = obj.data
    corner_normals = [Vector(normal.vector) for normal in mesh.corner_normals]
    corners_by_vertex = {}
    for polygon in mesh.polygons:
        for corner in polygon.loop_indices:
            corners_by_vertex.setdefault(mesh.loops[corner].vertex_index, []).append((polygon.index, corner))

    faces_by_edge = {}
    for polygon in mesh.polygons:
        for key in polygon.edge_keys:
            faces_by_edge.setdefault(key, []).append(polygon.index)

    sharp = mesh.attributes.get("sharp_edge") or mesh.attributes.new("sharp_edge", "BOOLEAN", "EDGE")
    limit = math.cos(math.radians(SHARP_NORMAL_DEGREES))
    marked = 0
    for edge in mesh.edges:
        faces = faces_by_edge.get(tuple(sorted(edge.vertices)), [])
        is_sharp = False
        if len(faces) == 2:
            for vertex in edge.vertices:
                normals = [corner_normals[corner] for face, corner in corners_by_vertex[vertex] if face in faces]
                if len(normals) == 2 and normals[0].dot(normals[1]) < limit:
                    is_sharp = True
        sharp.data[edge.index].value = is_sharp
        marked += is_sharp

    if mesh.has_custom_normals:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return marked


def split_long_edges(obj):
    world = obj.matrix_world
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    edges = [edge for edge in bm.edges
             if abs((world @ edge.verts[0].co).z - (world @ edge.verts[1].co).z) > LONG_EDGE_METRES]
    bmesh.ops.subdivide_edges(bm, edges=edges, cuts=CUTS, use_grid_fill=True)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def weight_from_body(trousers, body, armature):
    """Each trouser vertex takes the weights of the nearest point on the body's hip and leg triangles."""
    bone_names = {bone.name for bone in armature.data.bones}
    body_weights = bone_weights(body, bone_names)
    body_points = world_rest_positions(body)

    def leg_share(index):
        total = sum(weight for weight, _ in body_weights[index]) or 1.0
        return sum(weight for weight, bone in body_weights[index] if bone in LEG_BONES) / total

    body.data.calc_loop_triangles()
    triangles = [tuple(triangle.vertices) for triangle in body.data.loop_triangles
                 if all(leg_share(index) >= LEG_SHARE for index in triangle.vertices)]
    tree = BVHTree.FromPolygons(body_points, triangles)

    new_weights = []
    for point in world_rest_positions(trousers):
        nearest, _, triangle_index, _ = tree.find_nearest(point)
        corners = triangles[triangle_index]
        blend = {}
        for share, corner in zip(barycentric(nearest, *(body_points[index] for index in corners)), corners):
            total = sum(weight for weight, _ in body_weights[corner]) or 1.0
            for weight, bone in body_weights[corner]:
                blend[bone] = blend.get(bone, 0.0) + share * weight / total
        strongest = sorted(((weight, bone) for bone, weight in blend.items()), reverse=True)[:MAX_INFLUENCES]
        total = sum(weight for weight, _ in strongest) or 1.0
        new_weights.append([(weight / total, bone) for weight, bone in strongest])

    for group in list(trousers.vertex_groups):
        trousers.vertex_groups.remove(group)
    for index, pairs in enumerate(new_weights):
        for weight, bone in pairs:
            group = trousers.vertex_groups.get(bone) or trousers.vertex_groups.new(name=bone)
            group.add([index], weight, "REPLACE")


def barycentric(point, a, b, c):
    """Barycentric shares of a point on triangle abc, clamped to the triangle."""
    ab, ac, ap = b - a, c - a, point - a
    d00, d01, d11 = ab.dot(ab), ab.dot(ac), ac.dot(ac)
    d20, d21 = ap.dot(ab), ap.dot(ac)
    denominator = d00 * d11 - d01 * d01
    if abs(denominator) < 1e-12:
        return 1.0, 0.0, 0.0
    v = (d11 * d20 - d01 * d21) / denominator
    w = (d00 * d21 - d01 * d20) / denominator
    shares = [max(1.0 - v - w, 0.0), max(v, 0.0), max(w, 0.0)]
    total = sum(shares) or 1.0
    return tuple(share / total for share in shares)


# Checks

def skinning_matrices(armature):
    world = armature.matrix_world
    inverse = world.inverted()
    return {bone.name: world @ bone.matrix @ bone.bone.matrix_local.inverted() @ inverse for bone in armature.pose.bones}


def skin(points, weights, matrices):
    """Linear blend skinning with the strongest MAX_INFLUENCES bones, as Unity imports it."""
    skinned = []
    for point, pairs in zip(points, weights):
        pairs = pairs[:MAX_INFLUENCES]
        total = sum(weight for weight, _ in pairs)
        if total < 1e-6:
            skinned.append(point.copy())
            continue
        position = Vector((0.0, 0.0, 0.0))
        for weight, bone in pairs:
            position += (matrices[bone] @ point) * (weight / total)
        skinned.append(position)
    return skinned


def measure_poke_through(scene):
    """Per clip: (deepest, average count above POKE_COUNT_METRES) of body vertices outside the trousers that cover
    them when standing in the rest pose. Hem faces are left out of the "covered" test."""
    armature = scene["armature"]
    body = find_mesh(scene, BODY)
    trousers = find_mesh(scene, TROUSERS)
    bone_names = {bone.name for bone in armature.data.bones}
    body_weights = bone_weights(body, bone_names)
    trouser_weights = bone_weights(trousers, bone_names)
    body_points = world_rest_positions(body)
    trouser_points = world_rest_positions(trousers)
    polygons = [tuple(polygon.vertices) for polygon in trousers.data.polygons]

    bm = bmesh.new()
    bm.from_mesh(trousers.data)
    hem_faces = {face.index for face in bm.faces if any(edge.is_boundary for edge in face.edges)}
    bm.free()

    rest_tree = BVHTree.FromPolygons(trouser_points, polygons)
    covered = []
    for index, point in enumerate(body_points):
        nearest, normal, face, _ = rest_tree.find_nearest(point, COVER_SEARCH_METRES)
        if nearest is not None and face not in hem_faces and (point - nearest).dot(normal) <= POKE_COUNT_METRES:
            covered.append(index)

    covered_points = [body_points[index] for index in covered]
    covered_weights = [body_weights[index] for index in covered]
    results = {}
    for action in sorted(bpy.data.actions, key=lambda item: item.name):
        set_action(armature, action)
        first, last = (int(frame) for frame in action.frame_range)
        deepest = 0.0
        counted = 0
        frames = list(range(first, last + 1, 10))
        for frame in frames:
            bpy.context.scene.frame_set(frame)
            matrices = skinning_matrices(armature)
            tree = BVHTree.FromPolygons(skin(trouser_points, trouser_weights, matrices), polygons)
            for point in skin(covered_points, covered_weights, matrices):
                nearest, normal, _, _ = tree.find_nearest(point)
                if nearest is None:
                    continue
                depth = (point - nearest).dot(normal)
                deepest = max(deepest, depth)
                counted += depth > POKE_COUNT_METRES
        results[clip_name(action)] = (deepest, counted / len(frames))
    return results


def print_poke_through(label, results):
    for clip, (deepest, average) in results.items():
        street_fbx.log(f"{label:5} {clip:16} deepest {deepest * 1000:6.1f} mm, "
              f"{average:5.1f} body vertices more than {POKE_COUNT_METRES * 1000:.0f} mm out per frame")


# Renders

def render_sitting_poses(scene, folder, label):
    """Side and front-left views of the sitting poses: body red, trousers blue."""
    os.makedirs(folder, exist_ok=True)
    armature = scene["armature"]
    view = bpy.context.scene
    for obj in scene["meshes"].values():
        if obj.name == BODY:
            obj.color = (0.95, 0.25, 0.2, 1.0)
        elif obj.name == TROUSERS:
            obj.color = (0.2, 0.35, 0.95, 1.0)
        else:
            obj.color = (0.8, 0.8, 0.8, 1.0)
        obj.hide_render = "_eyes_" in obj.name or "_mouth_" in obj.name

    view.render.engine = "BLENDER_WORKBENCH"
    view.display.shading.light = "STUDIO"
    view.display.shading.color_type = "OBJECT"
    view.display.shading.show_object_outline = True
    view.render.resolution_x = 600
    view.render.resolution_y = 600
    camera = bpy.data.objects.get("Trousers camera")
    if camera is None:
        camera = bpy.data.objects.new("Trousers camera", bpy.data.cameras.new("Trousers camera"))
        view.collection.objects.link(camera)
    camera.data.lens = 60
    view.camera = camera

    for clip in ("Pose_Nature", "Pose_Balanced", "Pose_Efficiency"):
        action = next(item for item in bpy.data.actions if clip_name(item) == clip)
        set_action(armature, action)
        view.frame_set(int(action.frame_range[0]))
        bones = armature.pose.bones
        head = lambda name: armature.matrix_world @ bones[name].head
        hips = head("Hips")
        knees = (head("LeftLeg") + head("RightLeg")) / 2
        toes = (head("LeftToeBase") + head("RightToeBase")) / 2
        forward = Vector((toes.x - hips.x, toes.y - hips.y, 0.0)).normalized()
        side = Vector((-forward.y, forward.x, 0.0))
        target = (hips + knees) / 2 + Vector((0.0, 0.0, -0.1))
        for name, direction in (("side", side), ("frontleft", (forward + side).normalized())):
            camera.location = target + direction * 2.2 + Vector((0.0, 0.0, 0.35))
            camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
            view.render.filepath = os.path.join(folder, f"grandma_{label}_{clip}_{name}.png")
            bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()

