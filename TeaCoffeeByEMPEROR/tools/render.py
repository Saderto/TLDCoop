"""Preview sheets and inventory icons for TeaCoffeeByEMPEROR (bpy, Cycles CPU).
python3 render.py <out_dir> sheet <sheet.png> item:model:texture[:yaw] ...     one preview with several items in a row
python3 render.py <out_dir> icon <icon.png> model texture [yaw]                  one 512x512 transparent icon"""
import sys, os, math
import numpy as np
import bpy

OUT = sys.argv[1]; MODE = sys.argv[2]; ZOOM = float(__import__("os").environ.get("ZOOM", "1.05"))
_imgs = {}


def reset(w, h, samples=64, transparent=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _imgs.clear()
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples
    sc.cycles.use_denoising = False
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.render.resolution_percentage = 100
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA' if transparent else 'RGB'
    wd = bpy.data.worlds.new('w'); sc.world = wd; wd.use_nodes = True
    bg = wd.node_tree.nodes['Background']; bg.inputs[0].default_value = (.32, .33, .35, 1); bg.inputs[1].default_value = .25
    return sc


def img(path):
    if path not in _imgs:
        _imgs[path] = bpy.data.images.load(path)
    return _imgs[path]


def mat(tex, rough=.55, metal=0.0):
    m = bpy.data.materials.new('m'); m.use_nodes = True; nt = m.node_tree
    b = nt.nodes['Principled BSDF']; t = nt.nodes.new('ShaderNodeTexImage'); t.image = img(tex)
    t.interpolation = 'Cubic'
    nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    return m


def obj(model, tex, loc=(0, 0, 0), yaw=0.0, rough=.55):
    d = np.load(os.path.join(OUT, 'cache', model + '.npz'))
    me = bpy.data.meshes.new(model)
    me.from_pydata(d['v'].tolist(), [], d['t'].tolist())
    uvl = me.uv_layers.new()
    loops = np.zeros(len(me.loops), np.int64); me.loops.foreach_get('vertex_index', loops)
    uvl.data.foreach_set('uv', d['uv'][loops].reshape(-1))
    me.normals_split_custom_set_from_vertices(d['n'].tolist()) if hasattr(me, 'normals_split_custom_set_from_vertices') else None
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(model, me); bpy.context.collection.objects.link(ob)
    ob.location = loc; ob.rotation_euler = (0, 0, math.radians(yaw))
    ob.data.materials.append(mat(tex, rough))
    return ob, d['v']


def lights(target, dist):
    for name, loc, power, size in (('key', (-1.2, -1.6, 1.8), 70, 1.2), ('fill', (1.6, -1.0, .8), 22, 1.5), ('rim', (.4, 1.8, 1.4), 45, 1.0)):
        l = bpy.data.lights.new(name, 'AREA'); l.energy = power * dist * dist; l.size = size * dist
        o = bpy.data.objects.new(name, l); bpy.context.collection.objects.link(o)
        o.location = (target[0] + loc[0] * dist, target[1] + loc[1] * dist, target[2] + loc[2] * dist)
        d = np.array(target) - np.array(o.location)
        o.rotation_euler = (math.atan2(math.hypot(d[0], d[1]), -d[2]), 0, math.atan2(d[1], d[0]) - math.pi / 2)


def camera(target, dist, elev=22, azim=-28, ortho=None):
    c = bpy.data.cameras.new('c'); c.lens = 85
    if ortho: c.type = 'ORTHO'; c.ortho_scale = ortho
    o = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(o)
    e, a = math.radians(elev), math.radians(azim)
    o.location = (target[0] + dist * math.cos(e) * math.sin(a), target[1] - dist * math.cos(e) * math.cos(a), target[2] + dist * math.sin(e))
    d = np.array(target) - np.array(o.location)
    o.rotation_euler = (math.atan2(math.hypot(d[0], d[1]), -d[2]), 0, math.atan2(d[1], d[0]) - math.pi / 2)
    bpy.context.scene.camera = o


def floor(z=0.0, size=4.0, col=(.20, .19, .18)):
    me = bpy.data.meshes.new('f'); me.from_pydata([(-size, -size, z), (size, -size, z), (size, size, z), (-size, size, z)], [], [(0, 1, 2, 3)])
    o = bpy.data.objects.new('f', me); bpy.context.collection.objects.link(o)
    m = bpy.data.materials.new('f'); m.use_nodes = True; m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = col + (1,)
    m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .8
    o.data.materials.append(m)


if MODE == 'sheet':
    path = sys.argv[3]; items = [a.split(':') for a in sys.argv[4:]]
    sc = reset(1600, 700, 96)
    x = 0.0; xs = []
    for it in items:
        model, tex = it[1], it[2]; yaw = float(it[3]) if len(it) > 3 else 0.0
        d = np.load(os.path.join(OUT, 'cache', model + '.npz'))['v']
        wdt = (d[:, 0].max() - d[:, 0].min()) if abs(yaw) < 45 or abs(yaw) > 135 else (d[:, 1].max() - d[:, 1].min())
        wdt = max(wdt, .06)
        cx = x + wdt / 2
        obj(model, tex, (cx, 0, 0), yaw)
        xs.append(cx); x += wdt + .03
    span = x - .03
    hmax = .22
    tgt = (span / 2, 0, .07)
    floor()
    dist = max(span, .3) * 2.6
    camera(tgt, dist, 14, -4)
    bpy.context.scene.camera.data.lens = 85
    lights(tgt, max(span, .4))
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
elif MODE == 'icon':
    path, model, tex = sys.argv[3], sys.argv[4], sys.argv[5]
    yaw = float(sys.argv[6]) if len(sys.argv) > 6 else -20
    elev = float(sys.argv[7]) if len(sys.argv) > 7 else 24
    sc = reset(512, 512, 96, transparent=True)
    ob, v = obj(model, tex, (0, 0, 0), yaw)
    c = (v.max(0) + v.min(0)) / 2
    size = np.linalg.norm(v.max(0) - v.min(0))
    camera(tuple(c), size * 4.2, elev, 0, ortho=size * ZOOM)
    lights(tuple(c), size * 3)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
elif MODE == 'icons':
    # a list file: one icon per line "png model texture yaw elev"; one Blender for all of them
    for line in open(sys.argv[3]).read().split('\n'):
        if not line.strip(): continue
        path, model, tex, yaw, elev = line.split('\t')
        if os.path.exists(path): continue
        sc = reset(512, 512, 40, transparent=True)
        sc.render.threads_mode = 'FIXED'; sc.render.threads = 1
        sc.cycles.use_denoising = True
        ob, v = obj(model, tex, (0, 0, 0), float(yaw))
        c = (v.max(0) + v.min(0)) / 2
        size = np.linalg.norm(v.max(0) - v.min(0))
        camera(tuple(c), size * 4.2, float(elev), 0, ortho=size * ZOOM)
        lights(tuple(c), size * 3)
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
