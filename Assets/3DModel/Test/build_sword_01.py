import bpy
import bmesh
import math
import os
from mathutils import Vector


OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'WP_Sword_01.blend')
FBX_PATH = os.path.join(OUT_DIR, 'WP_Sword_01.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'WP_Sword_01.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'WP_Sword_01_Preview.png')
FRONT_PATH = os.path.join(OUT_DIR, 'WP_Sword_01_Front.png')


def clean_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return mat


def make_blade(steel):
    # Front silhouette is defined first as half-width stations along Z.
    # The last station is the integrated tip, not a separate object.
    stations = [
        (0.115, 0.0380, 0.0100),
        (0.600, 0.0370, 0.0092),
        (0.735, 0.0355, 0.0085),
        (0.815, 0.0315, 0.0072),
    ]
    verts = []
    for z, width, thick in stations:
        # Eight-point shallow hex/diamond cross-section in the XY plane.
        verts.extend([
            ( thick/2, 0.0, z),
            ( thick*0.16,  width*0.88, z),
            ( 0.0,         width, z),
            (-thick*0.16,  width*0.88, z),
            (-thick/2, 0.0, z),
            (-thick*0.16, -width*0.88, z),
            ( 0.0,        -width, z),
            ( thick*0.16, -width*0.88, z),
        ])
    tip_index = len(verts)
    verts.append((0.0, 0.0, 0.905))
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


def extruded_outline(name, outline_yz, thickness, mat):
    verts = []
    for x in (-thickness/2, thickness/2):
        verts.extend((x, y, z) for y, z in outline_yz)
    count = len(outline_yz)
    faces = [tuple(range(count-1, -1, -1)), tuple(range(count, 2*count))]
    for i in range(count):
        j = (i+1) % count
        faces.append((i, j, count+j, count+i))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_guard(guard_mat):
    # Straight central bar, gently widening into restrained flared end caps.
    outline = [
        (-0.144, 0.074), (-0.141, 0.066), (-0.130, 0.063),
        (-0.116, 0.078), (-0.038, 0.083), (0.0, 0.083),
        (0.038, 0.083), (0.116, 0.078), (0.130, 0.063),
        (0.141, 0.066), (0.144, 0.074), (0.144, 0.112),
        (0.141, 0.120), (0.130, 0.123), (0.116, 0.107),
        (0.038, 0.101), (0.0, 0.101), (-0.038, 0.101),
        (-0.116, 0.107), (-0.130, 0.123), (-0.141, 0.120),
        (-0.144, 0.112),
    ]
    return extruded_outline('Crossguard', outline, 0.018, guard_mat)


def cylinder(name, radii, z_values, mat, sides=8, rotation=0.0):
    verts, faces = [], []
    for z, radius in zip(z_values, radii):
        for i in range(sides):
            a = 2*math.pi*i/sides + rotation
            verts.append((radius*math.cos(a), radius*math.sin(a), z))
    faces.append(tuple(range(sides-1, -1, -1)))
    rings = len(z_values)
    for ring in range(rings-1):
        a0, b0 = ring*sides, (ring+1)*sides
        for i in range(sides):
            j = (i+1) % sides
            faces.append((a0+i, a0+j, b0+j, b0+i))
    faces.append(tuple(range((rings-1)*sides, rings*sides)))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_grip(leather):
    # One connected faceted grip mesh with six shallow wrap ridges—no overlapping rings.
    z0, z1 = -0.078, 0.076
    levels = [(z0, 0.0165)]
    band_count = 6
    for i in range(band_count):
        center = z0 + (i+0.5)*(z1-z0)/band_count
        levels.extend([(center-0.008, 0.0162), (center-0.005, 0.0182),
                       (center+0.005, 0.0182), (center+0.008, 0.0162)])
    levels.append((z1, 0.0165))
    levels.sort(key=lambda value: value[0])
    return cylinder('Grip', [r for z, r in levels], [z for z, r in levels], leather, 8, math.pi/8)


def make_collar(guard_mat):
    return cylinder('GuardCollar', [0.027, 0.027], [0.0765, 0.1135], guard_mat, 8, math.pi/8)


def make_pommel(guard_mat):
    # Compact polygonal stopper matching the small faceted pommel in the reference.
    return cylinder('Pommel', [0.014, 0.023, 0.025, 0.020, 0.017],
                    [-0.132, -0.129, -0.110, -0.083, -0.079], guard_mat, 8, math.pi/8)


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
    sword.name = 'WP_Sword_01'
    sword.data.name = 'WP_Sword_01_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # Geometry is authored around the grip center, so origin and object location remain at world zero.
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
    uv_name = obj.data.uv_layers.active.name if obj.data.uv_layers.active else 'NONE'
    print('=== WP_Sword_01 FINAL REPORT ===')
    print(f'Object: {obj.name}')
    print(f'Dimensions (m): {tuple(round(v, 4) for v in obj.dimensions)}')
    print(f'Vertices: {stats[0]}')
    print(f'Polygons: {stats[1]}')
    print(f'Triangles: {stats[2]}')
    print(f'Materials: {[m.name for m in obj.data.materials]}')
    print(f'UV map: {uv_name}')
    print(f'Origin coordinates: {tuple(round(v, 4) for v in obj.location)}')
    print(f'Non-manifold edges: {non_manifold}')
    print(f'Blend: {BLEND_PATH}')
    print(f'FBX: {FBX_PATH}')
    print(f'GLB: {GLB_PATH}')
    print(f'Preview: {PREVIEW_PATH}')
    print(f'Front preview: {FRONT_PATH}')
    return stats, non_manifold


def add_lights(target):
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
    return lights


def configure_render():
    scene = bpy.context.scene
    world = scene.world or bpy.data.worlds.new('World')
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes['Background']
    background.inputs['Color'].default_value = (0.74, 0.74, 0.74, 1)
    background.inputs['Strength'].default_value = 0.30
    try:
        scene.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.view_settings.look = 'AgX - Medium High Contrast'
    return scene


def render_previews(sword):
    scene = configure_render()
    target = Vector((0, 0, 0.38))
    lights = add_lights(target)
    bpy.ops.object.camera_add(location=(1.18, -1.42, 0.78))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.20
    scene.camera = camera
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    # Broad face lies in YZ; front comparison camera looks along -X.
    camera.location = (1.8, 0.0, 0.38)
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = 1.20
    scene.render.filepath = FRONT_PATH
    bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(camera, do_unlink=True)
    for lamp in lights:
        bpy.data.objects.remove(lamp, do_unlink=True)
    scene.camera = None


def main():
    clean_scene()
    steel = material('MAT_SwordSteel', (0.24, 0.27, 0.30), 0.72, 0.55)
    leather = material('MAT_DarkLeather', (0.105, 0.045, 0.022), 0.0, 0.82)
    guard = material('MAT_GuardMetal', (0.12, 0.135, 0.15), 0.60, 0.64)

    parts = [make_blade(steel), make_guard(guard), make_collar(guard),
             make_grip(leather), make_pommel(guard)]
    for obj in parts:
        flat_uv(obj)
    sword = join_parts(parts)
    stats, non_manifold = validate(sword)
    if not (350 <= stats[2] <= 1000):
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
    render_previews(sword)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print('=== EXPORT COMPLETE ===')


if __name__ == '__main__':
    main()
