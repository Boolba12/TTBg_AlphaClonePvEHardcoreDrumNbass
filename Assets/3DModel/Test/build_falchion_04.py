import bpy
import bmesh
import math
import os
from mathutils import Vector

OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'WP_Falchion_04.blend')
FBX_PATH = os.path.join(OUT_DIR, 'WP_Falchion_04.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'WP_Falchion_04.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'WP_Falchion_04_Preview.png')
FRONT_PATH = os.path.join(OUT_DIR, 'WP_Falchion_04_Front.png')


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


def blade_ring(z, spine_y, edge_y, thickness):
    # Thick flat spine, broad single primary bevel, and zero-thickness cutting line.
    t = thickness / 2
    bevel_y = edge_y - max(.012, (edge_y - spine_y) * .30)
    return [(t, spine_y, z), (t, bevel_y, z), (0, edge_y, z),
            (-t, bevel_y, z), (-t, spine_y, z), (0, spine_y - .001, z)]


def make_blade(steel):
    # Reference silhouette: straight spine, forward widening edge, short false edge.
    stations = [
        (.142, -.033, .030, .0125),
        (.430, -.034, .032, .0120),
        (.650, -.034, .043, .0115),
        (.770, -.032, .055, .0108),
        (.850, -.023, .052, .0095),
        (.902, -.010, .035, .0075),
    ]
    verts = []
    for station in stations:
        verts.extend(blade_ring(*station))
    # Robust, very short integrated point rather than a long needle tip.
    tip = len(verts)
    verts.append((0, .006, .932))
    n, faces = 6, [tuple(range(5, -1, -1))]
    for r in range(len(stations) - 1):
        a, b = r * n, (r + 1) * n
        for i in range(n):
            j = (i + 1) % n
            faces.append((a + i, a + j, b + j, b + i))
    last = (len(stations) - 1) * n
    for i in range(n):
        faces.append((last + i, last + (i + 1) % n, tip))
    mesh = bpy.data.meshes.new('Falchion_Blade_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(steel)
    obj = bpy.data.objects.new('Single_Edge_Blade', mesh)
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
    outline = [(-.092, .118), (-.090, .138), (-.028, .142),
               (0, .149), (.028, .142), (.090, .138), (.092, .118),
               (.028, .122), (0, .115), (-.028, .122)]
    return extrude_outline('Crossguard', outline, .012, brass)


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
    z0, z1 = -.110, .112
    levels = [(z0, .0165)]
    for i in range(5):
        center = z0 + (i + .5) * (z1 - z0) / 5
        levels.extend([(center - .014, .0160), (center, .0180),
                       (center + .014, .0160)])
    levels.append((z1, .0165))
    return revolved('Leather_Grip', sorted(levels), leather, 8)


def make_collar(brass):
    return revolved('Guard_Collar', [(.110, .023), (.142, .023)], brass, 8)


def make_ring_pommel(brass):
    sides, center, outer, inner = 10, -.148, .038, .022
    x0, x1 = -.008, .008
    verts = []
    for x in (x0, x1):
        for radius in (outer, inner):
            for i in range(sides):
                a = 2 * math.pi * i / sides
                verts.append((x, radius * math.cos(a), center + radius * math.sin(a)))
    faces = []
    for i in range(sides):
        j = (i + 1) % sides
        faces.extend([(i, j, 2 * sides + j, 2 * sides + i),
                      (sides + j, sides + i, 3 * sides + i, 3 * sides + j),
                      (i, sides + i, sides + j, j),
                      (2 * sides + j, 3 * sides + j, 3 * sides + i, 2 * sides + i)])
    mesh = bpy.data.meshes.new('Ring_Pommel_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(brass)
    obj = bpy.data.objects.new('Ring_Pommel', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


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
    falchion = bpy.context.object
    falchion.name = 'WP_Falchion_04'
    falchion.data.name = 'WP_Falchion_04_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    falchion.location = (0, 0, 0)
    return falchion


def validate(obj):
    obj.data.calc_loop_triangles()
    stats = len(obj.data.vertices), len(obj.data.polygons), len(obj.data.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    print('=== WP_Falchion_04 FINAL REPORT ===')
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
    target = Vector((0, 0, .34))
    lights = []
    for name, location, energy, size in (
        ('TEMP_Key', (1.1, -1.3, 1.2), 160, .9),
        ('TEMP_Fill', (-.9, -.7, .5), 65, 1.0),
        ('TEMP_Rim', (.4, .9, 1.1), 110, .8)):
        bpy.ops.object.light_add(type='AREA', location=location)
        lamp = bpy.context.object
        lamp.name = name
        lamp.data.energy = energy
        lamp.data.size = size
        lamp.rotation_euler = (target - lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lights.append(lamp)
    bpy.ops.object.camera_add(location=(1.15, -1.45, .55))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 1.19
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
    camera.location = (1.8, 0, .34)
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = FRONT_PATH
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    for lamp in lights:
        bpy.data.objects.remove(lamp, do_unlink=True)
    scene.camera = None


def main():
    clear_scene()
    steel = material('MAT_Steel', (.22, .24, .26), .72, .56)
    leather = material('MAT_DarkLeather', (.035, .040, .075), 0, .84)
    brass = material('MAT_Brass', (.30, .22, .075), .62, .60)
    parts = [make_blade(steel), make_guard(brass), make_grip(leather),
             make_collar(brass), make_ring_pommel(brass)]
    for obj in parts:
        prepare(obj)
    falchion = join_parts(parts)
    stats, non_manifold = validate(falchion)
    if not 350 <= stats[2] <= 850:
        print('WARNING: triangle count outside requested range')
    if non_manifold:
        print('WARNING: non-manifold geometry detected')
    bpy.ops.object.select_all(action='DESELECT')
    falchion.select_set(True)
    bpy.context.view_layer.objects.active = falchion
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
