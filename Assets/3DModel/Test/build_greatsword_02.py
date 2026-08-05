import bpy
import bmesh
import math
import os
from mathutils import Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'WP_Greatsword_02.blend')
FBX_PATH = os.path.join(OUT_DIR, 'WP_Greatsword_02.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'WP_Greatsword_02.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'WP_Greatsword_02_Preview.png')
FRONT_PATH = os.path.join(OUT_DIR, 'WP_Greatsword_02_Front.png')


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(blocks):
            if block.users == 0:
                blocks.remove(block)


def make_material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return mat


def blade_ring(z, half_width, thickness, fuller_half):
    # Closed low-poly section: broad edge bevels and a 1 mm recessed fuller.
    t = thickness / 2
    bevel_y = max(fuller_half + .010, half_width * .72)
    return [
        (0, half_width, z), (t, bevel_y, z), (t - .001, fuller_half, z),
        (t - .001, -fuller_half, z), (t, -bevel_y, z), (0, -half_width, z),
        (-t, -bevel_y, z), (-t + .001, -fuller_half, z),
        (-t + .001, fuller_half, z), (-t, bevel_y, z)
    ]


def make_blade(steel):
    # Reference-first silhouette: almost parallel for 80%, late polygonal rounding.
    stations = [
        (.175, .086, .0130, .031),
        (.420, .086, .0125, .031),
        (.760, .085, .0118, .030),
        (1.000, .083, .0108, .028),
        (1.105, .070, .0098, .024),
        (1.165, .046, .0085, .018),
        (1.190, .020, .0070, .009),
    ]
    verts = []
    for args in stations:
        verts.extend(blade_ring(*args))
    n = 10
    faces = [tuple(range(n - 1, -1, -1))]
    for r in range(len(stations) - 1):
        a, b = r * n, (r + 1) * n
        for i in range(n):
            j = (i + 1) % n
            faces.append((a + i, a + j, b + j, b + i))
    last = (len(stations) - 1) * n
    faces.append(tuple(range(last, last + n)))
    mesh = bpy.data.meshes.new('Blade_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(steel)
    obj = bpy.data.objects.new('Blade_With_Integrated_Fuller', mesh)
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


def make_guard(brass):
    # Thin, straight forged bar with only a restrained widening at both ends.
    outline = [(-.175, .158), (-.170, .181), (-.105, .186), (-.030, .184),
               (0, .193), (.030, .184), (.105, .186), (.170, .181),
               (.175, .158), (.105, .154), (.030, .160), (0, .153),
               (-.030, .160), (-.105, .154)]
    return extrude_outline('Crossguard', outline, .014, brass)


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
    z0, z1 = -.140, .145
    levels = [(z0, .021)]
    for i in range(5):
        c = z0 + (i + .5) * (z1 - z0) / 5
        levels.extend([(c - .018, .0205), (c, .0230), (c + .018, .0205)])
    levels.append((z1, .021))
    return revolved('Leather_Grip', sorted(levels), leather, 8)


def make_collar(brass):
    return revolved('Grip_Collar', [(.140, .028), (.175, .028)], brass, 8)


def make_ring_pommel(brass):
    sides, cx, outer, inner = 10, -.198, .059, .035
    x0, x1 = -.010, .010
    verts = []
    for x in (x0, x1):
        for radius in (outer, inner):
            for i in range(sides):
                a = 2 * math.pi * i / sides
                verts.append((x, radius * math.cos(a), cx + radius * math.sin(a)))
    faces = []
    for i in range(sides):
        j = (i + 1) % sides
        faces.append((i, j, 2 * sides + j, 2 * sides + i))
        faces.append((sides + j, sides + i, 3 * sides + i, 3 * sides + j))
        faces.append((i, sides + i, sides + j, j))
        faces.append((2 * sides + j, 3 * sides + j, 3 * sides + i, 2 * sides + i))
    mesh = bpy.data.meshes.new('Ring_Pommel_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(brass)
    obj = bpy.data.objects.new('Ring_Pommel', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def prepare(obj):
    for p in obj.data.polygons:
        p.use_smooth = False
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
    sword.name = 'WP_Greatsword_02'
    sword.data.name = 'WP_Greatsword_02_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    sword.location = (0, 0, 0)
    return sword


def validate(obj):
    obj.data.calc_loop_triangles()
    stats = (len(obj.data.vertices), len(obj.data.polygons), len(obj.data.loop_triangles))
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(1 for e in bm.edges if not e.is_manifold)
    bm.free()
    print('=== WP_Greatsword_02 FINAL REPORT ===')
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
    target = Vector((0, 0, .47))
    lights = []
    for name, loc, energy, size in (
        ('TEMP_Key', (1.5, -1.6, 1.6), 210, 1.2),
        ('TEMP_Fill', (-1.2, -.8, .7), 90, 1.3),
        ('TEMP_Rim', (.6, 1.1, 1.5), 150, 1.0)):
        bpy.ops.object.light_add(type='AREA', location=loc)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.size = size
        light.rotation_euler = (target - light.location).to_track_quat('-Z', 'Y').to_euler()
        lights.append(light)
    bpy.ops.object.camera_add(location=(1.65, -2.15, .80))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.62
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
    camera.location = (2.2, 0, .47)
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = FRONT_PATH
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    for light in lights:
        bpy.data.objects.remove(light, do_unlink=True)
    scene.camera = None


def main():
    clear_scene()
    steel = make_material('MAT_Steel', (.22, .24, .26), .72, .56)
    brass = make_material('MAT_Brass', (.30, .22, .075), .62, .60)
    leather = make_material('MAT_DarkLeather', (.085, .025, .018), 0, .84)
    parts = [make_blade(steel), make_guard(brass), make_grip(leather),
             make_collar(brass), make_ring_pommel(brass)]
    for obj in parts:
        prepare(obj)
    sword = join_parts(parts)
    stats, non_manifold = validate(sword)
    if not 400 <= stats[2] <= 900:
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
