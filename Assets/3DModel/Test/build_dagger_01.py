import bpy
import bmesh
import math
import os
from mathutils import Vector


OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else bpy.path.abspath('//')
BLEND_PATH = os.path.join(OUT_DIR, 'WP_Dagger_01.blend')
FBX_PATH = os.path.join(OUT_DIR, 'WP_Dagger_01.fbx')
GLB_PATH = os.path.join(OUT_DIR, 'WP_Dagger_01.glb')
PREVIEW_PATH = os.path.join(OUT_DIR, 'WP_Dagger_01_Preview.png')
FRONT_PATH = os.path.join(OUT_DIR, 'WP_Dagger_01_Front.png')


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


def blade_section(z, width, thickness):
    # Diamond cross-section with a central ridge on each broad side.
    return [(thickness/2, 0, z), (0, width, z),
            (-thickness/2, 0, z), (0, -width, z)]


def make_blade(steel):
    # Stage 1/2: measured front silhouette from weapon #1.  The blade is broad
    # at the guard, holds that mass briefly, then follows one continuous taper.
    # Stage 3/4: the X values give real thickness and a simple diamond section.
    stations = [(0.073, .0285, .0076), (0.135, .0270, .0073),
                (0.215, .0220, .0068), (0.295, .0148, .0060),
                (0.355, .0078, .0048)]
    verts = []
    for station in stations:
        verts.extend(blade_section(*station))
    tip = len(verts)
    verts.append((0, 0, .405))
    faces = [tuple(range(3, -1, -1))]
    for ring in range(len(stations)-1):
        a, b = ring*4, (ring+1)*4
        for i in range(4):
            j = (i+1) % 4
            faces.append((a+i, a+j, b+j, b+i))
    last = (len(stations)-1)*4
    for i in range(4):
        faces.append((last+i, last+(i+1)%4, tip))
    mesh = bpy.data.meshes.new('Blade_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(steel)
    obj = bpy.data.objects.new('Blade', mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def extrude_outline(name, outline, thickness, mat):
    verts = []
    for x in (-thickness/2, thickness/2):
        verts.extend((x, y, z) for y, z in outline)
    count = len(outline)
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


def make_guard_half(brass):
    # Positive-Y half: thin, nearly horizontal, subtly drooping and widening at the end.
    outline = [(0.000, .066), (.025, .065), (.050, .061), (.069, .056),
               (.071, .043), (.060, .041), (.048, .049), (.024, .054),
               (0.000, .055)]
    return extrude_outline('GuardHalf_Master', outline, .010, brass)


def mirror_guard(master):
    duplicate = master.copy()
    duplicate.data = master.data.copy()
    duplicate.name = 'GuardHalf_Mirrored'
    duplicate.scale.y = -1
    bpy.context.collection.objects.link(duplicate)
    bpy.context.view_layer.objects.active = duplicate
    duplicate.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    duplicate.select_set(False)
    return duplicate


def revolved(name, levels, mat, sides=8):
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
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def make_grip(leather):
    z0, z1 = -.052, .050
    levels = [(z0, .0145)]
    # Five broad, shallow low-poly wrap bands: readable, but never a dense coil.
    for i in range(5):
        center = z0 + (i+.5)*(z1-z0)/5
        levels += [(center-.0070, .0142), (center, .0160),
                   (center+.0070, .0142)]
    levels.append((z1, .0145))
    return revolved('Grip', sorted(levels), leather)


def make_guard_center(brass):
    return revolved('GuardCenter', [(.0505, .020), (.0725, .020)], brass)


def make_pommel(steel, brass):
    # Low-poly round disc in the YZ plane with a brass rim and steel center cap.
    sides = 10
    cx, radius, inner = -.074, .024, .0145
    x0, x1 = -.006, .006
    verts = []
    for x in (x0, x1):
        for r in (radius, inner):
            for i in range(sides):
                angle = 2*math.pi*i/sides
                verts.append((x, r*math.cos(angle), cx+r*math.sin(angle)))
    faces, mats = [], []
    # Outer cylindrical side.
    for i in range(sides):
        j = (i+1) % sides
        faces.append((i, j, 2*sides+j, 2*sides+i)); mats.append(1)
    # Front/back brass annuli between outer and inner rings.
    for side in range(2):
        outer = side*2*sides
        inner0 = outer+sides
        for i in range(sides):
            j = (i+1) % sides
            face = (outer+i, outer+j, inner0+j, inner0+i)
            if side == 0:
                face = tuple(reversed(face))
            faces.append(face); mats.append(1)
    # Steel center caps close the inner rings; no overlapping geometry.
    faces.append(tuple(range(sides, 2*sides))[::-1]); mats.append(0)
    faces.append(tuple(range(3*sides, 4*sides))); mats.append(0)
    mesh = bpy.data.meshes.new('Pommel_Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(steel)
    mesh.materials.append(brass)
    for poly, index in zip(mesh.polygons, mats):
        poly.material_index = index
    obj = bpy.data.objects.new('Pommel', mesh)
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
    dagger = bpy.context.object
    dagger.name = 'WP_Dagger_01'
    dagger.data.name = 'WP_Dagger_01_Mesh'
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    dagger.location = (0, 0, 0)
    return dagger


def validate(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    stats = len(mesh.vertices), len(mesh.polygons), len(mesh.loop_triangles)
    evaluated.to_mesh_clear()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    print('=== WP_Dagger_01 FINAL REPORT ===')
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
    bg = world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (.74, .74, .74, 1)
    bg.inputs['Strength'].default_value = .30
    target = Vector((0, 0, .15))
    lights = []
    for name, location, energy, size in (
        ('TEMP_Key', (.8, -.9, .8), 110, .7),
        ('TEMP_Fill', (-.7, -.5, .35), 45, .8),
        ('TEMP_Rim', (.3, .7, .7), 75, .6)):
        bpy.ops.object.light_add(type='AREA', location=location)
        lamp = bpy.context.object
        lamp.name = name
        lamp.data.energy = energy
        lamp.data.size = size
        lamp.rotation_euler = (target-lamp.location).to_track_quat('-Z', 'Y').to_euler()
        lights.append(lamp)
    bpy.ops.object.camera_add(location=(.72, -.88, .30))
    camera = bpy.context.object
    camera.name = 'TEMP_PreviewCamera'
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = .58
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
    camera.location = (.9, 0, .15)
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = .58
    scene.render.filepath = FRONT_PATH
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    for lamp in lights:
        bpy.data.objects.remove(lamp, do_unlink=True)
    scene.camera = None


def main():
    clear_scene()
    steel = material('MAT_Steel', (.22, .24, .26), .72, .56)
    leather = material('MAT_DarkLeather', (.105, .038, .020), 0, .84)
    brass = material('MAT_Brass', (.28, .20, .065), .62, .60)
    blade = make_blade(steel)
    guard = make_guard_half(brass)
    mirrored = mirror_guard(guard)
    parts = [blade, guard, mirrored, make_guard_center(brass),
             make_grip(leather), make_pommel(steel, brass)]
    for obj in parts:
        prepare(obj)
    dagger = join_parts(parts)
    stats, non_manifold = validate(dagger)
    if not 250 <= stats[2] <= 700:
        print('WARNING: triangle count outside target')
    if non_manifold:
        print('WARNING: non-manifold geometry detected')
    bpy.ops.object.select_all(action='DESELECT')
    dagger.select_set(True)
    bpy.context.view_layer.objects.active = dagger
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
