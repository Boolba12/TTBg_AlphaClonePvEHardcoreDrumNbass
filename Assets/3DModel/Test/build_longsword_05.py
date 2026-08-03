import bpy
import bmesh
import math
import os
from mathutils import Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'WP_Longsword_05.blend')
FBX_PATH = os.path.join(OUT_DIR, 'WP_Longsword_05.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'WP_Longsword_05.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'WP_Longsword_05_Preview.png')
FRONT_PATH = os.path.join(OUT_DIR, 'WP_Longsword_05_Front.png')


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return mat


def blade_ring(z, width, thickness):
    return [(thickness / 2, 0, z), (0, width, z),
            (-thickness / 2, 0, z), (0, -width, z)]


def make_blade(steel):
    stations = [(.180, .0260, .0105), (.450, .0255, .0100),
                (.760, .0230, .0092), (1.020, .0160, .0078),
                (1.105, .0075, .0058)]
    verts = []
    for station in stations:
        verts.extend(blade_ring(*station))
    tip = len(verts)
    verts.append((0, 0, 1.150))
    faces = [tuple(range(3, -1, -1))]
    for r in range(len(stations) - 1):
        a, b = r * 4, (r + 1) * 4
        for i in range(4):
            j = (i + 1) % 4
            faces.append((a + i, a + j, b + j, b + i))
    last = (len(stations) - 1) * 4
    for i in range(4):
        faces.append((last + i, last + (i + 1) % 4, tip))
    mesh = bpy.data.meshes.new('Longsword_Blade_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(steel)
    obj = bpy.data.objects.new('Blade', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def extrude_outline(name, outline, thickness, mat):
    verts = []
    for x in (-thickness / 2, thickness / 2):
        verts.extend((x, y, z) for y, z in outline)
    n = len(outline)
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_guard_center(dark_steel):
    # One simplified faceted oval block: only the large visible central mass.
    outline = [(0, .220), (.027, .208), (.041, .180), (.039, .137),
               (.024, .112), (0, .102), (-.024, .112), (-.039, .137),
               (-.041, .180), (-.027, .208)]
    obj = extrude_outline('Guard_Center_Block', outline, .020, dark_steel)
    for vertex in obj.data.vertices:
        vertex.co.y *= .85
    return obj


def make_guard_arm(dark_steel):
    # Positive-Y master arm: thin flowing forged profile and restrained hook end.
    outline = [(0, .173), (.050, .171), (.105, .165), (.143, .166),
               (.157, .177), (.169, .171), (.170, .154), (.159, .143),
               (.147, .148), (.108, .151), (.050, .154), (0, .148)]
    obj = extrude_outline('Guard_Arm_Master', outline, .012, dark_steel)
    for vertex in obj.data.vertices:
        vertex.co.y *= .85
    return obj


def mirror_arm(master):
    mirrored = master.copy()
    mirrored.data = master.data.copy()
    mirrored.name = 'Guard_Arm_Mirrored'
    mirrored.scale.y = -1
    bpy.context.collection.objects.link(mirrored)
    bpy.context.view_layer.objects.active = mirrored
    mirrored.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mirrored.select_set(False)
    return mirrored


def revolved(name, levels, mat, sides=8):
    verts, faces = [], []
    for z, radius in levels:
        for i in range(sides):
            a = 2 * math.pi * i / sides + math.pi / 8
            verts.append((radius * math.cos(a), radius * math.sin(a), z))
    faces.append(tuple(range(sides - 1, -1, -1)))
    for r in range(len(levels) - 1):
        a, b = r * sides, (r + 1) * sides
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((a + i, a + j, b + j, b + i))
    last = (len(levels) - 1) * sides
    faces.append(tuple(range(last, last + sides)))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_grip(leather):
    z0, z1 = -.128, .130
    levels = [(z0, .0165)]
    for i in range(5):
        center = z0 + (i + .5) * (z1 - z0) / 5
        levels.extend([(center - .016, .0160), (center, .0180),
                       (center + .016, .0160)])
    levels.append((z1, .0165))
    return revolved('Leather_Grip', sorted(levels), leather, 8)


def make_pommel(dark_steel):
    # Large visible spear-like facets only; no small grooves.
    return revolved('Faceted_Pommel', [(-.128, .018), (-.151, .023),
                                      (-.187, .032), (-.221, 0.0)], dark_steel, 8)


def prepare(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=.00001)
    try:
        bpy.ops.mesh.normals_make_consistent(inside=False)
    except AttributeError:
        pass
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.035)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)


def join_parts(parts):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    sword = bpy.context.object
    sword.name = 'WP_Longsword_05'
    sword.data.name = 'WP_Longsword_05_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    sword.location = (0, 0, 0)
    return sword


def validate(obj):
    obj.data.calc_loop_triangles()
    stats = len(obj.data.vertices), len(obj.data.polygons), len(obj.data.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    print('=== WP_Longsword_05 FINAL REPORT ===')
    print(f'Object: {obj.name}')
    print(f'Dimensions (m): {tuple(round(v, 4) for v in obj.dimensions)}')
    print(f'Vertices: {stats[0]}')
    print(f'Polygons: {stats[1]}')
    print(f'Triangles: {stats[2]}')
    print(f'Materials: {[m.name for m in obj.data.materials]}')
    print(f'UV map: {obj.data.uv_layers.active.name if obj.data.uv_layers.active else "NONE"}')
    print(f'Origin: {tuple(round(v, 4) for v in obj.location)}')
    print(f'Non-manifold edges: {non_manifold}')
    print(f'Blend: {BLEND_PATH}')
    print(f'FBX: {FBX_PATH}')
    print(f'GLB: {GLB_PATH}')
    print(f'Preview: {PREVIEW_PATH}')
    print(f'Front: {FRONT_PATH}')
    return stats, non_manifold


def render_previews():
    scene = bpy.context.scene
    world = scene.world or bpy.data.worlds.new('World')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.74, .74, .74, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .30
    target = Vector((0, 0, .46))
    lights = []
    for name, location, energy, size in (
        ('TEMP_Key', (1.3, -1.6, 1.5), 185, 1.0),
        ('TEMP_Fill', (-1.0, -.8, .6), 75, 1.1),
        ('TEMP_Rim', (.5, 1.0, 1.4), 130, .9)):
        bpy.ops.object.light_add(type='AREA', location=location)
        lamp = bpy.context.object
        lamp.name = name
        lamp.data.energy = energy
        lamp.data.size = size
        lamp.rotation_euler = (target - lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lights.append(lamp)
    bpy.ops.object.camera_add(location=(1.45, -1.85, .72))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.50
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = camera
    try:
        scene.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    camera.location = (2.0, 0, .46)
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = FRONT_PATH
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    for lamp in lights:
        bpy.data.objects.remove(lamp, do_unlink=True)
    scene.camera = None


def main():
    clear_scene()
    steel = material('MAT_Steel', (.25, .27, .30), .72, .54)
    dark_steel = material('MAT_DarkSteel', (.075, .085, .095), .65, .64)
    leather = material('MAT_DarkLeather', (.075, .026, .016), 0, .84)
    blade = make_blade(steel)
    center = make_guard_center(dark_steel)
    arm = make_guard_arm(dark_steel)
    mirrored = mirror_arm(arm)
    parts = [blade, center, arm, mirrored, make_grip(leather), make_pommel(dark_steel)]
    for obj in parts:
        prepare(obj)
    sword = join_parts(parts)
    stats, non_manifold = validate(sword)
    if not 450 <= stats[2] <= 1000:
        print('WARNING: triangle count outside requested range')
    if non_manifold:
        print('WARNING: non-manifold geometry detected')
    bpy.ops.object.select_all(action='DESELECT')
    sword.select_set(True)
    bpy.context.view_layer.objects.active = sword
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             add_leaf_bones=False, bake_anim=False)
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format='GLB', use_selection=True,
                              export_apply=True, export_yup=True)
    render_previews()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print('=== EXPORT COMPLETE ===')


if __name__ == '__main__':
    main()
