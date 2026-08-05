import bpy
import bmesh
import math
import os
from mathutils import Vector


OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'WP_Sword_04.blend')
FBX_PATH = os.path.join(OUT_DIR, 'WP_Sword_04.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'WP_Sword_04.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'WP_Sword_04_Preview.png')
FRONT_PATH = os.path.join(OUT_DIR, 'WP_Sword_04_Front.png')


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)


def make_material(name, color, metallic, roughness):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return material


def blade_cross_section(z, width, thickness):
    return [(thickness/2, 0, z), (thickness*.16, width*.88, z), (0, width, z),
            (-thickness*.16, width*.88, z), (-thickness/2, 0, z),
            (-thickness*.16, -width*.88, z), (0, -width, z),
            (thickness*.16, -width*.88, z)]


def make_blade(steel):
    # Continuous leaf-like silhouette: narrow root, gentle expansion, long taper.
    stations = [(0.110, .0250, .0090), (0.155, .0340, .0092),
                (0.225, .0405, .0093), (0.540, .0400, .0088),
                (0.690, .0380, .0082), (0.790, .0330, .0075),
                (0.860, .0220, .0062)]
    verts = []
    for station in stations:
        verts.extend(blade_cross_section(*station))
    tip = len(verts)
    verts.append((0, 0, .905))
    faces = [tuple(range(7, -1, -1))]
    for ring in range(len(stations)-1):
        a, b = ring*8, (ring+1)*8
        for i in range(8):
            j = (i+1) % 8
            faces.append((a+i, a+j, b+j, b+i))
    last = (len(stations)-1)*8
    for i in range(8):
        faces.append((last+i, last+(i+1)%8, tip))
    mesh = bpy.data.meshes.new('Blade_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(steel)
    blade = bpy.data.objects.new('Blade', mesh)
    bpy.context.collection.objects.link(blade)
    return blade


def extrude_outline(name, outline, thickness, material):
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
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_guard_half(dark_steel):
    # Thin forged plate. Positive-Y half gently sweeps away from the blade toward -Z.
    outline = [
        (0.000, 0.096),
        (0.026, 0.094),
        (0.052, 0.090),
        (0.078, 0.084),
        (0.102, 0.075),
        (0.123, 0.064),
        (0.137, 0.052),
        (0.141, 0.047),
        (0.134, 0.052),
        (0.119, 0.061),
        (0.099, 0.069),
        (0.075, 0.075),
        (0.050, 0.079),
        (0.024, 0.082),
        (0.000, 0.083),
    ]
    return extrude_outline('GuardHalf_Master', outline, .010, dark_steel)


def mirror_guard(master):
    mirrored = master.copy()
    mirrored.data = master.data.copy()
    mirrored.name = 'GuardHalf_Mirrored'
    mirrored.scale.y = -1
    bpy.context.collection.objects.link(mirrored)
    bpy.context.view_layer.objects.active = mirrored
    mirrored.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mirrored.select_set(False)
    return mirrored


def revolved(name, levels, material, sides=8):
    verts, faces = [], []
    for z, radius in levels:
        for i in range(sides):
            angle = 2*math.pi*i/sides + math.pi/8
            verts.append((radius*math.cos(angle), radius*math.sin(angle), z))
    faces.append(tuple(range(sides-1, -1, -1)))
    for ring in range(len(levels)-1):
        a, b = ring*sides, (ring+1)*sides
        for i in range(sides):
            j = (i+1) % sides
            faces.append((a+i, a+j, b+j, b+i))
    last = (len(levels)-1)*sides
    faces.append(tuple(range(last, last+sides)))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_grip(leather):
    z0, z1 = -.078, .064
    levels = [(z0, .016)]
    for i in range(6):
        center = z0 + (i+.5)*(z1-z0)/6
        levels += [(center-.0075, .0158), (center-.0045, .0177),
                   (center+.0045, .0177), (center+.0075, .0158)]
    levels.append((z1, .016))
    return revolved('Grip', sorted(levels), leather)


def make_guard_center(dark_steel):
    return revolved('GuardCenter', [(.0645, .023), (.1095, .023)], dark_steel)


def make_pommel(dark_steel):
    # Small cylindrical pommel with two restrained collar transitions.
    return revolved('Pommel', [(-.128, .015), (-.124, .020), (-.100, .020),
                               (-.083, .017), (-.079, .015)], dark_steel)


def prepare_mesh(obj):
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
    sword.name = 'WP_Sword_04'
    sword.data.name = 'WP_Sword_04_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    sword.location = (0, 0, 0)
    return sword


def validate(sword):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = sword.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    stats = len(mesh.vertices), len(mesh.polygons), len(mesh.loop_triangles)
    evaluated.to_mesh_clear()
    bm = bmesh.new()
    bm.from_mesh(sword.data)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    print('=== WP_Sword_04 FINAL REPORT ===')
    print(f'Object: {sword.name}')
    print(f'Dimensions (m): {tuple(round(v, 4) for v in sword.dimensions)}')
    print(f'Vertices: {stats[0]}')
    print(f'Polygons: {stats[1]}')
    print(f'Triangles: {stats[2]}')
    print(f'Materials: {[m.name for m in sword.data.materials]}')
    print(f'UV map: {sword.data.uv_layers.active.name if sword.data.uv_layers.active else "NONE"}')
    print(f'Origin: {tuple(round(v, 4) for v in sword.location)}')
    print(f'Non-manifold edges: {non_manifold}')
    print(f'Blend: {BLEND_PATH}')
    print(f'FBX: {FBX_PATH}')
    print(f'GLB: {GLB_PATH}')
    print(f'Preview: {PREVIEW_PATH}')
    print(f'Front: {FRONT_PATH}')
    return stats, non_manifold


def configure_scene():
    scene = bpy.context.scene
    world = scene.world or bpy.data.worlds.new('World')
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes['Background']
    background.inputs['Color'].default_value = (.74, .74, .74, 1)
    background.inputs['Strength'].default_value = .30
    try:
        scene.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    return scene


def render_previews():
    scene = configure_scene()
    target = Vector((0, 0, .38))
    lights = []
    for name, location, energy, size in (
        ('TEMP_Key', (1.4, -1.5, 1.6), 135, 1.1),
        ('TEMP_Fill', (-1.2, -.8, .8), 55, 1.3),
        ('TEMP_Rim', (.4, 1.2, 1.4), 100, .9)):
        bpy.ops.object.light_add(type='AREA', location=location)
        lamp = bpy.context.object
        lamp.name = name
        lamp.data.energy = energy
        lamp.data.shape = 'DISK'
        lamp.data.size = size
        lamp.rotation_euler = (target-lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lights.append(lamp)
    bpy.ops.object.camera_add(location=(1.18, -1.42, .78))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.18
    scene.camera = camera
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    # Orthographic broad-face view for direct silhouette validation.
    camera.location = (1.8, 0, .38)
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = 1.18
    scene.render.filepath = FRONT_PATH
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    for lamp in lights:
        bpy.data.objects.remove(lamp, do_unlink=True)
    scene.camera = None


def main():
    clear_scene()
    steel = make_material('MAT_Steel', (.24, .27, .30), .72, .55)
    dark_steel = make_material('MAT_DarkSteel', (.11, .125, .14), .62, .64)
    leather = make_material('MAT_DarkLeather', (.105, .045, .022), 0, .82)
    blade = make_blade(steel)
    half = make_guard_half(dark_steel)
    mirrored = mirror_guard(half)
    parts = [blade, half, mirrored, make_guard_center(dark_steel),
             make_grip(leather), make_pommel(dark_steel)]
    for obj in parts:
        prepare_mesh(obj)
    sword = join_parts(parts)
    stats, non_manifold = validate(sword)
    if not 400 <= stats[2] <= 1000:
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
    render_previews()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print('=== EXPORT COMPLETE ===')


if __name__ == '__main__':
    main()
