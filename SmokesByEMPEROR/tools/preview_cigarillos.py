"""Preview of the cigarillos: python3 preview_cigarillos.py <bakedir> <texdir> <out.png>"""
import sys, os, math
import numpy as np
import bpy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cigarillos as C
from mathutils import Vector, Euler
B, TEX, OUT = sys.argv[1:4]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 64; sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 1800, 900
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Punchy'
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.5, 0.52, 0.56, 1)
w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.7
for name, e, rot in [('key', 4.6, (48, -8, -30)), ('rim', 1.0, (120, 0, 160))]:
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN')); l.data.energy = e; l.data.angle = .2
    l.rotation_euler = [math.radians(a) for a in rot]; bpy.context.collection.objects.link(l)

def obj(item, tex, loc, rot):
    d = np.load(os.path.join(B, item + '.npz'))
    me = bpy.data.meshes.new(item)
    me.from_pydata([tuple(x) for x in d['verts']], [], [tuple(x) for x in d['tris']])
    lay = me.uv_layers.new(name='UV')
    for poly in me.polygons:
        for li in poly.loop_indices:
            lay.data[li].uv = tuple(d['uvs'][me.loops[li].vertex_index])
    me.normals_split_custom_set_from_vertices([tuple(x) for x in d['normals']])
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(item, me); bpy.context.collection.objects.link(ob)
    m = bpy.data.materials.new(item); m.use_nodes = True
    bs = m.node_tree.nodes['Principled BSDF']
    im = m.node_tree.nodes.new('ShaderNodeTexImage'); im.image = bpy.data.images.load(os.path.join(TEX, tex))
    m.node_tree.links.new(im.outputs['Color'], bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = .32 if 'tin' in item or 'pouch' in item else .6
    if 'tin' in item: bs.inputs['Metallic'].default_value = .55
    me.materials.append(m)
    ob.location = loc; ob.rotation_euler = Euler([math.radians(a) for a in rot], 'XYZ')
    return ob

pl = bpy.data.meshes.new('floor'); pl.from_pydata([(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [], [(0, 1, 2, 3)])
fo = bpy.data.objects.new('floor', pl); bpy.context.collection.objects.link(fo)
fm = bpy.data.materials.new('f'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.16, .13, .11, 1)
fm.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .8; pl.materials.append(fm)

packs = [('tin', 'kafekreme_original'), ('tin', 'cubanmini_cohibra'), ('pack10', 'captainblockminis_cherry'), ('box5tip', 'blackwild_wine'), ('box5wide', 'williger_export'),
         ('box5', 'whitehowl_newyorker'), ('box5short', 'toskanello_classico'), ('pouch', 'backwuds_original'), ('bundle', 'mandalai_greenleaf')]
x = -.36
for kind, t in packs:
    wdt = C.PACKS[kind][0] / 1000
    obj('cglpack_' + kind, 'cglpack_%s.jpg' % t, (x + wdt / 2, .08, 0), (-14, 0, 0))
    x += wdt + .014
# a tin lying flat, the lid up
obj('cglpack_tin', 'cglpack_dannemenmoods_original.jpg', (.05, -.21, .0135), (-90, 0, 0))
obj('cglpack_pouch', 'cglpack_swishasweets_original.jpg', (.20, -.265, .014), (-90, 0, 0))
obj('cglpack_bundle', 'cglpack_mandalai_greenleaf.jpg', (.36, -.24, .012), (-90, 0, 0))
cigs = [('mini', 'kafekreme_original'), ('mini', 'cubanmini_cohibra'), ('mini', 'captainblockminis_cherry'), ('tip', 'blackwild_wine'), ('tip', 'blackwild_woodtip'),
        ('square', 'williger_export'), ('classic', 'whitehowl_newyorker'), ('toscano', 'toskanello_classico'), ('rustic', 'backwuds_original'), ('rustic', 'mandalai_greenleaf')]
for i, (fmt, t) in enumerate(cigs):
    r = C.FORMATS[fmt][1]
    obj('cgl_' + fmt, 'cgl_%s.jpg' % t, (-.33 + (i % 5) * .14, -.035 - (i // 5) * .035, r + .0003), (0, 90, 0))
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.collection.objects.link(cam)
cam.data.type = 'ORTHO'; cam.data.ortho_scale = .80
e = math.radians(38); dvec = Vector((0, -math.cos(e), math.sin(e)))
cam.location = Vector((.03, -.045, .03)) + dvec * 2
cam.rotation_euler = (-dvec).to_track_quat('-Z', 'Y').to_euler()
sc.camera = cam; sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
