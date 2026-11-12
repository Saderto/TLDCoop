"""Preview of every lighter design: one Cycles render each, then a labelled grid. python3 preview_lighters.py <outdir> [keys]"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
import lighter_designs as LD

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
only = set(sys.argv[2:])


def scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 40
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 360, 420
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.exposure = -0.35
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (.6, .62, .66, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = .7
    for name, e, rot in [('key', 3.0, (50, -10, -35)), ('rim', 1.2, (120, 0, 160))]:
        l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN'))
        l.data.energy = e; l.rotation_euler = [math.radians(a) for a in rot]
        sc.collection.objects.link(l)
    return sc


for key, ru, en, kind, build, rar in LD.DESIGNS:
    if only and key not in only:
        continue
    sc = scene()
    objs = build()
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == 'MESH' for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    c = (lo + hi) / 2
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = float(os.environ.get('ORTHO', 0)) or max(0.085, (hi - lo).z * 1.25)  # same scale for all pocket ones, sizes compare
    d = Vector((math.sin(math.radians(22 if kind == 'kolibri' else -22)), -math.cos(math.radians(22)), .32)).normalized()
    cam.location = (c if not os.environ.get('TOPZ') else Vector((c.x, c.y, hi.z - float(os.environ['TOPZ'])))) + d * .4
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam
    sc.render.filepath = os.path.join(OUT, key + '.png')
    bpy.ops.render.render(write_still=True)
    print('done', key, flush=True)
