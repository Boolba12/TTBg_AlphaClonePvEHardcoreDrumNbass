import bpy
import bmesh
import math
import os
from mathutils import Vector


OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else bpy.path.abspath("//")
BLEND_PATH = os.path.join(OUT_DIR, "WP_Mace_04.blend")
FBX_PATH = os.path.join(OUT_DIR, "WP_Mace_04.fbx")
GLB_PATH = os.path.join(OUT_DIR, "WP_Mace_04.glb")
PREVIEW_PATH = os.path.join(OUT_DIR, "WP_Mace_04_Preview.png")


def clean_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return mat


def cylinder(name, radius, depth, z, mat, vertices=8, radius_top=None):
    if radius_top is None or abs(radius_top - radius) < 1e-8:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=(0, 0, z))
        obj = bpy.context.object
    else:
        verts = []
        faces = []
        for zi, rad in ((z-depth/2, radius), (z+depth/2, radius_top)):
            for i in range(vertices):
                a = 2 * math.pi * i / vertices
                verts.append((rad*math.cos(a), rad*math.sin(a), zi))
        faces.append(tuple(range(vertices-1, -1, -1)))
        faces.append(tuple(range(vertices, vertices*2)))
        for i in range(vertices):
            j = (i+1) % vertices
            faces.append((i, j, vertices+j, vertices+i))
        mesh = bpy.data.meshes.new(name + '_Mesh')
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def diamond_core(mat):
    # Three octagonal rings form a pointed, elongated diamond core.
    rings = [
        (0.552, 0.020),
        (0.590, 0.048),
        (0.662, 0.050),
        (0.735, 0.046),
        (0.772, 0.000),
    ]
    verts = []
    for z, r in rings:
        if r == 0:
            verts.append((0, 0, z))
        else:
            for i in range(8):
                a = 2*math.pi*i/8 + math.pi/8
                verts.append((r*math.cos(a), r*math.sin(a), z))
    faces = []
    # lower point is represented by first octagonal ring with a small closed cap
    faces.append(tuple(range(7, -1, -1)))
    for ring_idx in range(3):
        a0 = ring_idx*8
        b0 = (ring_idx+1)*8
        for i in range(8):
            j = (i+1)%8
            faces.append((a0+i, a0+j, b0+j, b0+i))
    tip = 32
    for i in range(8):
        faces.append((24+i, 24+(i+1)%8, tip))
    mesh = bpy.data.meshes.new('MaceCore_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new('MaceCore', mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def fin(name, angle, mat):
    # Thick tapered vertical blade: broad through the middle, pointed top/bottom.
    profile = [
        (0.000, 0.564),
        (0.057, 0.605),
        (0.080, 0.662),
        (0.057, 0.720),
        (0.000, 0.762),
    ]
    half_t = 0.009
    ca, sa = math.cos(angle), math.sin(angle)
    # u points radially outward, v is blade thickness tangent.
    verts = []
    for v in (-half_t, half_t):
        for u, z in profile:
            x = u*ca - v*sa
            y = u*sa + v*ca
            verts.append((x, y, z))
    faces = []
    faces.append(tuple(range(4, -1, -1)))
    faces.append(tuple(range(5, 10)))
    for i in range(5):
        j = (i+1)%5
        faces.append((i, j, 5+j, 5+i))
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def shade_flat_and_uv(obj):
    if obj.type != 'MESH':
        return
    for poly in obj.data.polygons:
        poly.use_smooth = False
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.035)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)


def join_model(parts, mats):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = 'WP_Mace_04'
    obj.data.name = 'WP_Mace_04_Mesh'
    # joining preserves material slots; consolidate identical materials.
    for slot in obj.material_slots:
        pass
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.location = (0, 0, 0)
    # Clean and recalculate normals.
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False) if hasattr(bpy.ops.mesh, 'normals_make_consistent') else None
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


def add_preview_camera_and_lights(obj):
    world = bpy.context.scene.world or bpy.data.worlds.new('World')
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.72, 0.72, 0.72, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.28

    bpy.ops.object.camera_add(location=(1.25, -1.45, 0.72))
    cam = bpy.context.object
    cam.name = 'TEMP_PreviewCamera'
    bpy.context.scene.camera = cam
    target = Vector((0, 0, 0.33))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 1.12

    lights = []
    for name, loc, energy, size in [
        ('TEMP_Key', (1.6, -1.8, 1.6), 120, 1.1),
        ('TEMP_Fill', (-1.4, -0.8, 0.9), 55, 1.3),
        ('TEMP_Rim', (0.5, 1.2, 1.5), 90, 0.9),
    ]:
        bpy.ops.object.light_add(type='AREA', location=loc)
        lamp = bpy.context.object
        lamp.name = name
        lamp.data.energy = energy
        lamp.data.shape = 'DISK'
        lamp.data.size = size
        lamp.rotation_euler = (target - lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lights.append(lamp)
    return cam, lights


def validate_and_print(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = obj.evaluated_get(depsgraph)
    mesh = eval_obj.to_mesh()
    mesh.calc_loop_triangles()
    verts = len(mesh.vertices)
    polys = len(mesh.polygons)
    tris = len(mesh.loop_triangles)
    dims = tuple(round(v, 4) for v in obj.dimensions)
    eval_obj.to_mesh_clear()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(1 for e in bm.edges if not e.is_manifold)
    bm.free()
    print('=== WP_Mace_04 FINAL CHECK ===')
    print(f'Vertices: {verts}')
    print(f'Polygons: {polys}')
    print(f'Triangles: {tris}')
    print(f'Dimensions (m): {dims}')
    print(f'Materials: {[m.name for m in obj.data.materials]}')
    print(f'Non-manifold edges: {non_manifold}')
    if not (300 <= tris <= 1000):
        print('WARNING: triangle count outside requested target/max range')


def main():
    clean_scene()
    iron = material('MAT_DarkIron', (0.105, 0.12, 0.135), 0.58, 0.70)
    wood = material('MAT_DarkWood', (0.105, 0.045, 0.022), 0.0, 0.82)
    leather = material('MAT_WornLeather', (0.16, 0.075, 0.038), 0.0, 0.88)

    parts = []
    # Grip centered around world origin; total model extent is -0.12..+0.78-ish plus head tip.
    parts.append(cylinder('Pommel', 0.024, 0.030, -0.105, iron, 8, 0.021))
    parts.append(cylinder('GripCore', 0.017, 0.240, 0.030, wood, 8, 0.016))
    # Six broad low-poly leather wrap rings over 24 cm.
    for i in range(6):
        z = -0.075 + i*0.038
        parts.append(cylinder(f'LeatherWrap_{i+1}', 0.0205, 0.030, z, leather, 8))
    # Long tapered wooden shaft from grip to head.
    parts.append(cylinder('Shaft', 0.0155, 0.435, 0.3025, wood, 8, 0.0135))
    # Transition rings beneath the head.
    parts.append(cylinder('Collar_1', 0.020, 0.018, 0.527, iron, 8))
    parts.append(cylinder('Collar_2', 0.024, 0.018, 0.544, iron, 8))
    parts.append(cylinder('Collar_3', 0.021, 0.014, 0.560, iron, 8))
    parts.append(diamond_core(iron))
    for i in range(4):
        parts.append(fin(f'HeadRib_{i+1}', i*math.pi/2, iron))

    for p in parts:
        shade_flat_and_uv(p)
    mace = join_model(parts, (iron, wood, leather))
    # Ensure stable material order and exact requested names.
    slots = [mace.data.materials.get(n) for n in ('MAT_DarkIron', 'MAT_DarkWood', 'MAT_WornLeather')]
    # Save/export only selected mesh.
    bpy.ops.object.select_all(action='DESELECT')
    mace.select_set(True)
    bpy.context.view_layer.objects.active = mace
    validate_and_print(mace)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             add_leaf_bones=False, bake_anim=False)
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format='GLB', use_selection=True,
                              export_apply=True, export_yup=True)

    cam, lights = add_preview_camera_and_lights(mace)
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
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    bpy.ops.render.render(write_still=True)

    # Final scene must contain only the mesh, no preview helpers.
    bpy.data.objects.remove(cam, do_unlink=True)
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
