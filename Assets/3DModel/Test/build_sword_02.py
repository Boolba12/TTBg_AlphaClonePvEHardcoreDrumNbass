import bpy
import bmesh
import math
import os
from mathutils import Vector


OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'WP_Sword_02.blend')
FBX_PATH = os.path.join(OUT_DIR, 'WP_Sword_02.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'WP_Sword_02.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'WP_Sword_02_Preview.png')


def clean_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)


def make_material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return mat


def blade_ring(z, width, thickness):
    return [
        ( thickness/2, 0.0, z),
        ( thickness*0.16,  width*0.88, z),
        ( 0.0,             width, z),
        (-thickness*0.16,  width*0.88, z),
        (-thickness/2, 0.0, z),
        (-thickness*0.16, -width*0.88, z),
        ( 0.0,            -width, z),
        ( thickness*0.16, -width*0.88, z),
    ]


def make_blade(steel):
    # Abrupt width changes at the first stations create the two restrained stepped recesses.
    stations = [
        (0.118, 0.027, 0.0095),
        (0.137, 0.027, 0.0095),
        (0.147, 0.036, 0.0095),
        (0.158, 0.042, 0.0095),
        (0.690, 0.042, 0.0088),
        (0.785, 0.039, 0.0080),
        (0.855, 0.028, 0.0068),
    ]
    verts = []
    for station in stations:
        verts.extend(blade_ring(*station))
    tip_index = len(verts)
    verts.append((0, 0, 0.895))
    faces = [tuple(range(7, -1, -1))]
    for ring in range(len(stations)-1):
        a0, b0 = ring*8, (ring+1)*8
        for i in range(8):
            j = (i+1) % 8
            faces.append((a0+i, a0+j, b0+j, b0+i))
    last = (len(stations)-1)*8
    for i in range(8):
        faces.append((last+i, last+(i+1)%8, tip_index))
    mesh = bpy.data.meshes.new('Blade_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(steel)
    obj = bpy.data.objects.new('Blade', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def extrude_yz(name, outline, thickness, mat):
    verts = []
    for x in (-thickness/2, thickness/2):
        verts.extend((x, y, z) for y, z in outline)
    n = len(outline)
    faces = [tuple(range(n-1, -1, -1)), tuple(range(n, 2*n))]
    for i in range(n):
        j = (i+1) % n
        faces.append((i, j, n+j, n+i))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_guard_arm(dark_metal):
    # One continuous positive-Y arm: thick at center, smoothly swept toward +Z, pointed at end.
    outline = [
        (0.000, 0.078),
        (0.038, 0.087),
        (0.074, 0.103),
        (0.105, 0.124),
        (0.132, 0.151),
        (0.123, 0.118),
        (0.095, 0.093),
        (0.058, 0.074),
        (0.022, 0.064),
        (0.000, 0.066),
    ]
    return extrude_yz('GuardArm_Master', outline, 0.018, dark_metal)


def mirror_guard_arm(master):
    mirrored = master.copy()
    mirrored.data = master.data.copy()
    mirrored.name = 'GuardArm_Mirrored'
    mirrored.scale.y = -1.0
    bpy.context.collection.objects.link(mirrored)
    bpy.context.view_layer.objects.active = mirrored
    mirrored.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mirrored.select_set(False)
    return mirrored


def cylinder(name, levels, mat, sides=8):
    verts, faces = [], []
    for z, radius in levels:
        for i in range(sides):
            a = 2*math.pi*i/sides + math.pi/8
            verts.append((radius*math.cos(a), radius*math.sin(a), z))
    faces.append(tuple(range(sides-1, -1, -1)))
    for ring in range(len(levels)-1):
        a0, b0 = ring*sides, (ring+1)*sides
        for i in range(sides):
            j = (i+1) % sides
            faces.append((a0+i, a0+j, b0+j, b0+i))
    last = (len(levels)-1)*sides
    faces.append(tuple(range(last, last+sides)))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_grip(leather):
    z0, z1 = -0.078, 0.060
    levels = [(z0, 0.0165)]
    for i in range(6):
        center = z0 + (i+0.5)*(z1-z0)/6
        levels.extend([(center-0.0075, 0.0160), (center-0.0045, 0.0180),
                       (center+0.0045, 0.0180), (center+0.0075, 0.0160)])
    levels.append((z1, 0.0165))
    return cylinder('Grip', sorted(levels), leather)


def make_guard_center(dark_metal):
    return cylinder('GuardCenter', [(0.0605, 0.025), (0.1175, 0.025)], dark_metal)


def make_pommel(dark_metal):
    # Small faceted diamond/stopper visible on the second sword.
    return cylinder('Pommel', [(-0.130, 0.006), (-0.112, 0.024),
                               (-0.090, 0.015), (-0.079, 0.015)], dark_metal)


def flat_uv(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = False
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.00001)
    try:
        bpy.ops.mesh.normals_make_consistent(inside=False)
    except AttributeError:
        pass
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.035)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)


def join_parts(parts):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    sword = bpy.context.object
    sword.name = 'WP_Sword_02'
    sword.data.name = 'WP_Sword_02_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    sword.location = (0, 0, 0)
    return sword


def validate(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    stats = (len(mesh.vertices), len(mesh.polygons), len(mesh.loop_triangles))
    evaluated.to_mesh_clear()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    print('=== WP_Sword_02 FINAL REPORT ===')
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
    return stats, non_manifold


def render_preview():
    scene = bpy.context.scene
    world = scene.world or bpy.data.worlds.new('World')
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (0.74, 0.74, 0.74, 1)
    bg.inputs['Strength'].default_value = 0.30
    target = Vector((0, 0, 0.38))
    bpy.ops.object.camera_add(location=(1.18, -1.42, 0.78))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.18
    scene.camera = camera
    lights = []
    for name, location, energy, size in (
        ('TEMP_Key', (1.4, -1.5, 1.6), 135, 1.1),
        ('TEMP_Fill', (-1.2, -0.8, 0.8), 55, 1.3),
        ('TEMP_Rim', (0.4, 1.2, 1.4), 100, 0.9),
    ):
        bpy.ops.object.light_add(type='AREA', location=location)
        lamp = bpy.context.object
        lamp.name = name
        lamp.data.energy = energy
        lamp.data.shape = 'DISK'
        lamp.data.size = size
        lamp.rotation_euler = (target-lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lights.append(lamp)
    try:
        scene.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = PREVIEW_PATH
    scene.render.film_transparent = False
    scene.view_settings.look = 'AgX - Medium High Contrast'
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    for lamp in lights:
        bpy.data.objects.remove(lamp, do_unlink=True)
    scene.camera = None


def main():
    clean_scene()
    steel = make_material('MAT_Steel', (0.24, 0.27, 0.30), 0.72, 0.55)
    leather = make_material('MAT_DarkLeather', (0.105, 0.045, 0.022), 0.0, 0.82)
    dark_metal = make_material('MAT_DarkMetal', (0.11, 0.125, 0.14), 0.60, 0.65)

    blade = make_blade(steel)
    arm_master = make_guard_arm(dark_metal)
    arm_mirror = mirror_guard_arm(arm_master)
    grip = make_grip(leather)
    center = make_guard_center(dark_metal)
    pommel = make_pommel(dark_metal)
    parts = [blade, arm_master, arm_mirror, center, grip, pommel]
    for obj in parts:
        flat_uv(obj)
    sword = join_parts(parts)
    stats, non_manifold = validate(sword)
    if not (400 <= stats[2] <= 1000):
        print('WARNING: triangle count outside target')
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
    render_preview()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print('=== EXPORT COMPLETE ===')


if __name__ == '__main__':
    main()
