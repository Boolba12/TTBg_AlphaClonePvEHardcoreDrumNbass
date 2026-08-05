import bpy
import bmesh
import math
import os
from mathutils import Vector


OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'Weapon_07.blend')
FBX_PATH = os.path.join(OUT_DIR, 'Weapon_07.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'Weapon_07.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'Weapon_07_Preview.png')


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


def cylinder(name, r0, r1, depth, z, mat, sides=8):
    verts, faces = [], []
    for zz, rr in ((z-depth/2, r0), (z+depth/2, r1)):
        for i in range(sides):
            a = 2*math.pi*i/sides
            verts.append((rr*math.cos(a), rr*math.sin(a), zz))
    faces.extend([tuple(range(sides-1, -1, -1)), tuple(range(sides, sides*2))])
    for i in range(sides):
        j = (i+1) % sides
        faces.append((i, j, sides+j, sides+i))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def beveled_block(name, center_z, mat):
    bpy.ops.mesh.primitive_cube_add(location=(0, 0, center_z))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (0.088, 0.088, 0.050)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new('SilhouetteBevel', 'BEVEL')
    bevel.width = 0.004
    bevel.segments = 1
    bevel.affect = 'EDGES'
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    return obj


def spike_mesh(name, direction, center_z, mat):
    # One reusable square-pyramid spike; its base slightly overlaps the block.
    dx, dy = direction
    tangent = Vector((-dy, dx, 0))
    vertical = Vector((0, 0, 1))
    radial = Vector((dx, dy, 0))
    base_center = radial * 0.042
    half = 0.017
    verts = []
    for ts, zs in ((-half, -half), (half, -half), (half, half), (-half, half)):
        p = base_center + tangent*ts + vertical*zs
        verts.append((p.x, p.y, center_z+p.z))
    tip = radial * 0.083
    verts.append((tip.x, tip.y, center_z))
    faces = [(0, 3, 2, 1), (0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)]
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def boolean_union(base, cutter):
    bpy.context.view_layer.objects.active = base
    modifier = base.modifiers.new('ModuleUnion', 'BOOLEAN')
    modifier.operation = 'UNION'
    modifier.solver = 'EXACT'
    modifier.object = cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def build_hammer_module(center_z, iron):
    block = beveled_block('HammerModule_Master', center_z, iron)
    for i, direction in enumerate(((1, 0), (-1, 0), (0, 1), (0, -1)), start=1):
        spike = spike_mesh(f'SpikeModule_{i}', direction, center_z, iron)
        boolean_union(block, spike)
    block.name = 'HammerModule_Master'
    return block


def duplicate_module(master, name, z_offset):
    duplicate = master.copy()
    duplicate.data = master.data.copy()
    duplicate.name = name
    duplicate.location.z += z_offset
    bpy.context.collection.objects.link(duplicate)
    return duplicate


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
    weapon = bpy.context.object
    weapon.name = 'Weapon_07'
    weapon.data.name = 'Weapon_07_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    weapon.location = (0, 0, 0)
    return weapon


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
    print('=== Weapon_07 FINAL CHECK ===')
    print(f'Vertices: {stats[0]}')
    print(f'Polygons: {stats[1]}')
    print(f'Triangles: {stats[2]}')
    print(f'Dimensions (m): {tuple(round(v, 4) for v in obj.dimensions)}')
    print(f'Materials: {[m.name for m in obj.data.materials]}')
    print(f'Non-manifold edges: {non_manifold}')
    return stats, non_manifold


def preview_setup():
    scene = bpy.context.scene
    world = scene.world or bpy.data.worlds.new('World')
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes['Background']
    background.inputs['Color'].default_value = (0.72, 0.72, 0.72, 1)
    background.inputs['Strength'].default_value = 0.28
    target = Vector((0, 0, 0.33))
    bpy.ops.object.camera_add(location=(1.18, -1.42, 0.70))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.10
    scene.camera = camera
    lights = []
    for name, location, energy, size in (
        ('TEMP_Key', (1.6, -1.8, 1.6), 125, 1.1),
        ('TEMP_Fill', (-1.4, -0.8, 0.9), 55, 1.3),
        ('TEMP_Rim', (0.5, 1.2, 1.5), 95, 0.9),
    ):
        bpy.ops.object.light_add(type='AREA', location=location)
        lamp = bpy.context.object
        lamp.name = name
        lamp.data.energy = energy
        lamp.data.shape = 'DISK'
        lamp.data.size = size
        lamp.rotation_euler = (target-lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lights.append(lamp)
    return camera, lights


def main():
    clean_scene()
    iron = make_material('MAT_DarkIron', (0.095, 0.11, 0.125), 0.58, 0.72)
    wood = make_material('MAT_DarkWood', (0.10, 0.042, 0.020), 0.0, 0.82)
    leather = make_material('MAT_WornLeather', (0.15, 0.068, 0.034), 0.0, 0.88)

    parts = [
        cylinder('Pommel', 0.023, 0.020, 0.030, -0.105, iron),
        cylinder('GripCore', 0.017, 0.016, 0.240, 0.030, wood),
    ]
    for i in range(6):
        parts.append(cylinder(f'LeatherWrap_{i+1}', 0.0205, 0.0205, 0.030, -0.075+i*0.038, leather))
    parts.extend([
        cylinder('Shaft', 0.0155, 0.0135, 0.430, 0.300, wood),
        cylinder('MetalConnector', 0.025, 0.025, 0.035, 0.5325, iron),
    ])

    # One finished module is reused twice; all three sections remain identical.
    module_low = build_hammer_module(0.590, iron)
    module_mid = duplicate_module(module_low, 'HammerModule_Middle', 0.080)
    module_top = duplicate_module(module_low, 'HammerModule_Top', 0.160)
    parts.extend([module_low, module_mid, module_top])

    for obj in parts:
        flat_uv(obj)
    weapon = join_parts(parts)
    stats, non_manifold = validate(weapon)
    if not (400 <= stats[2] <= 1200):
        print('WARNING: triangle count outside requested range')
    if non_manifold:
        print('WARNING: non-manifold geometry detected')

    bpy.ops.object.select_all(action='DESELECT')
    weapon.select_set(True)
    bpy.context.view_layer.objects.active = weapon
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             add_leaf_bones=False, bake_anim=False)
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format='GLB', use_selection=True,
                              export_apply=True, export_yup=True)

    camera, lights = preview_setup()
    scene = bpy.context.scene
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
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print(f'Saved: {BLEND_PATH}')
    print(f'Exported: {FBX_PATH}')
    print(f'Exported: {GLB_PATH}')
    print(f'Rendered: {PREVIEW_PATH}')


if __name__ == '__main__':
    main()
