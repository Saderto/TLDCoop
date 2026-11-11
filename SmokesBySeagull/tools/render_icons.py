"""Inventory icons (256x256, transparent) from the baked meshes + albedo, as the game shows them.
python3 render_icons.py <bakedir> <texdir> <outdir>"""
import sys, os, math
import numpy as np
import bpy
from mathutils import Vector, Matrix, Euler

B, TEX, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(OUT, exist_ok=True)

def scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = 256
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Punchy'
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.6, 0.62, 0.66, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6
    for name, e, rot in [('key', 3.0, (50, -10, -35)), ('rim', 1.2, (120, 0, 160))]:
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN'))
        l.data.energy = e; l.rotation_euler = [math.radians(a) for a in rot]
        bpy.context.collection.objects.link(l)
    return sc

def mesh_obj(item, tex):
    d = np.load(os.path.join(B, item + '.npz'))
    v, uv, t = d['verts'], d['uvs'], d['tris']
    me = bpy.data.meshes.new(item)
    me.from_pydata([tuple(x) for x in v], [], [tuple(x) for x in t])
    lay = me.uv_layers.new(name='UV')
    for poly in me.polygons:
        for li in poly.loop_indices:
            lay.data[li].uv = tuple(uv[me.loops[li].vertex_index])
    me.normals_split_custom_set_from_vertices([tuple(x) for x in d['normals']])
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(item, me); bpy.context.collection.objects.link(ob)
    m = bpy.data.materials.new(item); m.use_nodes = True
    nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    im = nt.nodes.new('ShaderNodeTexImage'); im.image = bpy.data.images.load(tex)
    nt.links.new(im.outputs['Color'], bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value = 0.55
    me.materials.append(m)
    return ob

def render(item, tex, rot, out, elev=18):
    sc = scene()
    ob = mesh_obj(item, tex)
    ob.rotation_euler = Euler([math.radians(a) for a in rot], 'XYZ')
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    c = sum(pts, Vector()) / 8
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    e = math.radians(elev)
    d = Vector((0, -math.cos(e), math.sin(e)))
    cam.location = c + d * 2
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update()
    # fit: project bound corners onto camera plane
    inv = cam.matrix_world.inverted()
    ps = [inv @ p for p in pts]
    span = max(max(p.x for p in ps) - min(p.x for p in ps), max(p.y for p in ps) - min(p.y for p in ps))
    cx = (max(p.x for p in ps) + min(p.x for p in ps)) / 2; cy = (max(p.y for p in ps) + min(p.y for p in ps)) / 2
    cam.location = cam.matrix_world @ Vector((cx, cy, 0))
    cam.data.ortho_scale = span * 1.12
    sc.camera = cam
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)

J = os.path.join
jobs = []
for b in ['chapgirl', 'milston', 'marlbrrro', 'parliamint', 'bent', 'caramel']:
    jobs.append(('Pack' + b.capitalize(), 'pack_' + b, J(TEX, 'pack_%s.png' % b), (0, 0, 22)))
diag = (0, 55, 0)
for c in ['cohibra', 'montecrusto']:
    C = c.capitalize()
    jobs += [('Tube' + C, 'tube_' + c, J(B, 'tube_%s_albedo.png' % c), diag),
             ('Cigar' + C, 'cigar_' + c, J(B, 'cigar_%s_albedo.png' % c), diag),
             ('CigarCut' + C, 'cigar_cut_' + c, J(B, 'cigar_cut_%s_albedo.png' % c), diag),
             ('LitCigar' + C, 'cigar_cut_' + c, J(B, 'cigar_cut_%s_albedo.png' % c), diag)]
for p in ['billiard', 'bent', 'churchwarden', 'corncob']:
    P = p.capitalize()
    jobs += [('Pipe' + P, 'pipe_' + p, J(B, 'pipe_%s_albedo.png' % p), (0, 0, 12)),
             ('LitPipe' + P, 'pipe_' + p, J(B, 'pipe_%s_albedo.png' % p), (0, 0, 12))]
jobs += [('LitCigarette', 'cigarette', J(B, 'cigarette_albedo.png'), diag),
         ('PouchCaptainBlock', 'pouch_captainblock', J(TEX, 'pouch_captainblock.png'), (0, 0, 12)),
         ('LighterZappo', 'lighter_zappo', J(B, 'lighter_zappo_albedo.png'), (0, 0, 20)),
         ('CutterZikar', 'cutter_zikar', J(B, 'cutter_zikar_albedo.png'), (90, 0, 0))]
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import catalog
_pm = {'chapgirl': 'pack_chapgirl', 'sobrania': 'pack_milston'}
for bkey, bname, _, v in catalog.all_varieties():
    if True:
        jobs = [j for j in jobs if j[0] != catalog.item_id(bname, v)]
        jobs.append((catalog.item_id(bname, v), catalog.pack_mesh(bkey, v), J(TEX, 'pack_%s.jpg' % catalog.tex_key(bkey, v)), (0, 0, 22)))
import cigars as cg
for c in cg.CIGARS:
    k, K = cg.lc(c), c['key']
    jobs += [('Tube' + K, 'tube_' + k, J(B, 'tube_%s_albedo.png' % k), diag),
             ('Cigar' + K, 'cigar_' + k, J(B, 'cigar_%s_albedo.png' % k), diag),
             ('CigarCut' + K, 'cigar_cut_' + k, J(B, 'cigar_cut_%s_albedo.png' % k), diag),
             ('LitCigar' + K, 'cigar_cut_' + k, J(B, 'cigar_cut_%s_albedo.png' % k), diag)]
for c in cg.CIGARS + cg.OLD:
    jobs.append(('Box' + c['key'], 'box_' + cg.lc(c), J(TEX, 'box_%s.jpg' % cg.lc(c)), (0, 0, 15)))
import pouches as pz
for p in pz.POUCHES:
    jobs.append(('Pouch' + p['key'], 'pouch_captainblock', J(TEX, 'pouch_%s.jpg' % p['key'].lower()), (0, 0, 12)))
only = set(sys.argv[4:])
for name, item, tex, rot in jobs:
    if only and name not in only: continue
    render(item, tex, rot, J(OUT, name + '.png'))
    print('icon', name)
