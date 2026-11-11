"""Inventory icons (256x256, transparent) from bake/*.npz + the mod textures. python3 render_icons.py [names...]"""
import sys, os, math, glob
import numpy as np
import bpy
from mathutils import Vector, Euler
R = './'
B, TEX, OUT = R + 'bake/', R + 'mod/SmokehouseBySeagull/textures/', R + 'mod/SmokehouseBySeagull/icons/'
def scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.samples = int(os.environ.get('SAMPLES', 40)); sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = 256; sc.render.film_transparent = True
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Punchy'
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.6, 0.62, 0.66, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0
    for name, e, rot in [('key', 4.2, (40, -10, -35)), ('rim', 1.2, (120, 0, 160))]:
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN')); l.data.energy = e; l.rotation_euler = [math.radians(a) for a in rot]
        bpy.context.collection.objects.link(l)
    return sc
def render(name, rough, elev=52, turn=-38):
    sc = scene(); d = np.load(B + name + '.npz'); v, uv, t = d['verts'], d['uvs'], d['tris']
    me = bpy.data.meshes.new(name); me.from_pydata([tuple(x) for x in v], [], [tuple(x) for x in t])
    lay = me.uv_layers.new(name='UV')
    for poly in me.polygons:
        for li in poly.loop_indices: lay.data[li].uv = tuple(uv[me.loops[li].vertex_index])
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    im = nt.nodes.new('ShaderNodeTexImage'); im.image = bpy.data.images.load(TEX + (name if os.path.exists(TEX + name + '.png') else os.environ.get('TEXNAME', name)) + '.png')
    nt.links.new(im.outputs['Color'], bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = rough; me.materials.append(m)
    ob.rotation_euler = Euler((0, 0, math.radians(turn)), 'XYZ'); bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]; c = sum(pts, Vector()) / 8
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.collection.objects.link(cam); cam.data.type = 'ORTHO'
    e = math.radians(elev); dv = Vector((0, -math.cos(e), math.sin(e))); cam.location = c + dv * 3; cam.rotation_euler = (-dv).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update(); inv = cam.matrix_world.inverted(); ps = [inv @ (ob.matrix_world @ Vector(x)) for x in v[::3]]
    xs = [p.x for p in ps]; ys = [p.y for p in ps]; span = max(max(xs) - min(xs), max(ys) - min(ys))
    cam.location = cam.matrix_world @ Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, 0)); cam.data.ortho_scale = span * 1.1
    sc.camera = cam; sc.render.filepath = OUT + name + '.png'; bpy.ops.render.render(write_still=True); print('icon', name)
only = sys.argv[1:]
for f in sorted(glob.glob(B + '*.npz')):
    n = os.path.basename(f)[:-4]
    if only and n not in only: continue
    if n == 'Barrel': render(n, 0.75, elev=22, turn=-30)
    else: render(n, 0.4 if n.startswith('Hot') else 0.7)
