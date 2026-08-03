import bpy
import bmesh
import math
import os
from mathutils import Vector


OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'WP_Mace_10.blend')
FBX_PATH = os.path.join(OUT_DIR, 'WP_Mace_10.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'WP_Mace_10.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'WP_Mace_10_Preview.png')


def clean_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)


def make_material(name, color, metallic=0.0, roughness=0.8):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return mat


def cylinder(name, r0, r1, depth, z, mat, sides=8):
    verts = []
    faces = []
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


def central_head(mat):
    # Faceted elongated core, narrow at both ends and broadest around the middle.
    rings = [(0.555, 0.022), (0.590, 0.041), (0.665, 0.046), (0.735, 0.038), (0.775, 0.012)]
    verts = []
    for z, r in rings:
        for i in range(8):
            a = 2*math.pi*i/8 + math.pi/8
            verts.append((r*math.cos(a), r*math.sin(a), z))
    faces = [tuple(range(7, -1, -1)), tuple(range(32, 40))]
    for ring in range(4):
        a0, b0 = ring*8, (ring+1)*8
        for i in range(8):
            j = (i+1) % 8
            faces.append((a0+i, a0+j, b0+j, b0+i))
    mesh = bpy.data.meshes.new('HeadCore_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new('HeadCore', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def axe_blade(name, angle, iron, edge_mat):
    # Broad axe-like plate; the outer edge is vertical with simple clipped corners.
    profile = [
        (0.027, 0.577),
        (0.058, 0.588),
        (0.084, 0.620),
        (0.084, 0.704),
        (0.067, 0.742),
        (0.028, 0.763),
    ]
    half_t = 0.011
    ca, sa = math.cos(angle), math.sin(angle)
    verts = []
    for tangent in (-half_t, half_t):
        for radial, z in profile:
            verts.append((radial*ca - tangent*sa, radial*sa + tangent*ca, z))
    n = len(profile)
    faces = [tuple(range(n-1, -1, -1)), tuple(range(n, 2*n))]
    for i in range(n):
        j = (i+1) % n
        faces.append((i, j, n+j, n+i))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(iron)
    mesh.materials.append(edge_mat)
    # Assign only the broad outer cutting-edge wall to the worn-edge material.
    mesh.polygons[2 + 2].material_index = 1
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def flat_uv(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = False
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.035)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)


def join_parts(parts):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    mace = bpy.context.object
    mace.name = 'WP_Mace_10'
    mace.data.name = 'WP_Mace_10_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    mace.location = (0, 0, 0)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    try:
        bpy.ops.mesh.normals_make_consistent(inside=False)
    except AttributeError:
        pass
    bpy.ops.object.mode_set(mode='OBJECT')
    return mace


def validate(obj):
    mesh = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    mesh.calc_loop_triangles()
    stats = (len(mesh.vertices), len(mesh.polygons), len(mesh.loop_triangles))
    obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    print('=== WP_Mace_10 FINAL CHECK ===')
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
    bg = world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (0.72, 0.72, 0.72, 1)
    bg.inputs['Strength'].default_value = 0.28
    target = Vector((0, 0, 0.33))
    bpy.ops.object.camera_add(location=(1.22, -1.48, 0.70))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.10
    scene.camera = camera
    lights = []
    for name, loc, energy, size in (
        ('TEMP_Key', (1.6, -1.8, 1.6), 125, 1.1),
        ('TEMP_Fill', (-1.4, -0.8, 0.9), 55, 1.3),
        ('TEMP_Rim', (0.5, 1.2, 1.5), 95, 0.9),
    ):
        bpy.ops.object.light_add(type='AREA', location=loc)
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
    iron = make_material('MAT_DarkIron', (0.095, 0.11, 0.125), 0.55, 0.72)
    worn = make_material('MAT_WornIronEdge', (0.19, 0.21, 0.23), 0.65, 0.58)
    wood = make_material('MAT_DarkWood', (0.10, 0.042, 0.020), 0.0, 0.82)
    leather = make_material('MAT_WornLeather', (0.15, 0.068, 0.034), 0.0, 0.88)

    parts = [
        cylinder('Pommel', 0.023, 0.020, 0.030, -0.105, iron),
        cylinder('GripCore', 0.017, 0.016, 0.240, 0.030, wood),
    ]
    for i in range(6):
        parts.append(cylinder(f'LeatherWrap_{i+1}', 0.0205, 0.0205, 0.030, -0.075+i*0.038, leather))
    parts.extend([
        cylinder('Shaft', 0.0155, 0.0135, 0.435, 0.3025, wood),
        cylinder('Collar_1', 0.020, 0.020, 0.018, 0.527, iron),
        cylinder('Collar_2', 0.024, 0.024, 0.018, 0.544, iron),
        cylinder('Collar_3', 0.021, 0.021, 0.014, 0.560, iron),
        central_head(iron),
    ])
    for i in range(4):
        parts.append(axe_blade(f'AxeBlade_{i+1}', i*math.pi/2, iron, worn))
    for obj in parts:
        flat_uv(obj)
    mace = join_parts(parts)
    validate(mace)
    bpy.ops.object.select_all(action='DESELECT')
    mace.select_set(True)
    bpy.context.view_layer.objects.active = mace
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
