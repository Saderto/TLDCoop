"""Inventory icons (256, transparent) from ../bake/*.npz + ../tex. python3 render_icons.py [size] [names...] ; SIZE env, OUT env."""
import sys, os, math, glob
import numpy as np
import bpy
from mathutils import Vector, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
B, TEX = os.path.join(HERE, '..', 'bake') + '/', os.path.join(HERE, '..', 'tex') + '/'
OUT = os.environ.get('OUT', os.path.join(HERE, '..', 'icons')) + '/'
SIZE = int(os.environ.get('SIZE', 256))
os.makedirs(OUT, exist_ok=True)


def scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.samples = int(os.environ.get('SAMPLES', 32)); sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = SIZE; sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.75, 0.76, 0.8, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.9
    for name, e, rot in [('key', 3.0, (40, -10, -35)), ('rim', 1.0, (120, 0, 160))]:
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN')); l.data.energy = e; l.rotation_euler = [math.radians(a) for a in rot]
        bpy.context.collection.objects.link(l)
    return sc


def render(name, tex, elev, turn, rough=.45, out=None):
    sc = scene(); d = np.load(B + name + '.npz'); v, uv, t = d['verts'], d['uvs'], d['tris']
    vb = [(x, -z, y) for x, y, z in v]
    me = bpy.data.meshes.new(name); me.from_pydata(vb, [], [tuple(int(i) for i in x) for x in t])
    lay = me.uv_layers.new(name='UV')
    for poly in me.polygons:
        for li in poly.loop_indices: lay.data[li].uv = tuple(uv[me.loops[li].vertex_index])
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    im = nt.nodes.new('ShaderNodeTexImage'); im.image = bpy.data.images.load(TEX + tex + '.png')
    nt.links.new(im.outputs['Color'], bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = rough
    me.materials.append(m)
    ob.rotation_euler = Euler((0, 0, math.radians(turn)), 'XYZ'); bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]; c = sum(pts, Vector()) / 8
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.collection.objects.link(cam); cam.data.type = 'ORTHO'
    e = math.radians(elev); dv = Vector((0, -math.cos(e), math.sin(e))); cam.location = c + dv * 3; cam.rotation_euler = (-dv).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update(); inv = cam.matrix_world.inverted(); ps = [inv @ (ob.matrix_world @ Vector(x)) for x in vb[::2]]
    xs = [p.x for p in ps]; ys = [p.y for p in ps]; span = max(max(xs) - min(xs), max(ys) - min(ys))
    cam.location = cam.matrix_world @ Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, 0)); cam.data.ortho_scale = span * 1.08
    sc.camera = cam; sc.render.filepath = OUT + (out or name) + '.png'; bpy.ops.render.render(write_still=True); print('icon', out or name)


sys.path.insert(0, HERE)
from catalog import SNACKS
only = sys.argv[1:]
for s in SNACKS:
    sid, sh, bid = s['id'], s['shape'], s['brand']['id']
    if only and sid not in only and bid not in only: continue
    if sh == 'tube': render(bid, sid, 20, -78, out=sid)
    elif sh == 'can': render(bid, sid, 30, -80, out=sid)
    elif sh == 'box': render(bid, sid, 26, -20, out=sid)
    elif sh == 'carton': render(bid, sid, 24, -24, out=sid)
    elif sh == 'cup': render(bid, sid, 28, -80, out=sid)
    else: render(bid, sid, int(os.environ.get('ELEV', 62)), int(os.environ.get('TURN', 0)), rough=.35, out=sid)
