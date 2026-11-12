"""Preview sheet and inventory icons for JellyfishByEMPEROR (bpy, Cycles CPU).
python3 render.py <out_dir> sheet|icons [names...]"""
import sys, os, json, math
import numpy as np
import bpy

OUT = sys.argv[1]; MODE = sys.argv[2]; ONLY = sys.argv[3:]
info = json.load(open(os.path.join(OUT, 'models.json')))


_imgs = {}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _imgs.clear()
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 48
    sc.cycles.use_denoising = False
    sc.view_settings.view_transform = 'Standard'
    sc.cycles.transparent_max_bounces = 64; sc.cycles.max_bounces = 16
    return sc


def mesh(name, key):
    d = np.load(os.path.join(OUT, 'cache', key + '.npz'))
    me = bpy.data.meshes.new(name)
    me.from_pydata(d['v'].tolist(), [], d['t'].tolist())
    uvl = me.uv_layers.new()
    loops = np.zeros(len(me.loops), np.int64); me.loops.foreach_get('vertex_index', loops)
    uvl.data.foreach_set('uv', d['uv'][loops].reshape(-1))
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    return ob




def img(path):
    if path not in _imgs:
        _imgs[path] = bpy.data.images.load(path)
    return _imgs[path]


def mat_jelly(tex, strength=1.0):
    m = bpy.data.materials.new('jelly'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o = nt.nodes.new('ShaderNodeOutputMaterial'); t = nt.nodes.new('ShaderNodeTexImage'); t.image = img(tex)
    e = nt.nodes.new('ShaderNodeEmission'); e.inputs['Strength'].default_value = strength
    tr = nt.nodes.new('ShaderNodeBsdfTransparent'); mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(t.outputs['Color'], e.inputs['Color']); nt.links.new(t.outputs['Alpha'], mix.inputs['Fac'])
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(e.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], o.inputs['Surface'])
    return m


def mat_glass(rgba):
    m = bpy.data.materials.new('glass'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o = nt.nodes.new('ShaderNodeOutputMaterial'); tr = nt.nodes.new('ShaderNodeBsdfTransparent'); tr.inputs[0].default_value = (1, 1, 1, 1)
    g = nt.nodes.new('ShaderNodeBsdfGlossy'); g.inputs['Roughness'].default_value = .05
    e = nt.nodes.new('ShaderNodeEmission'); e.inputs['Color'].default_value = rgba[:3] + (1,); e.inputs['Strength'].default_value = 1
    a1 = nt.nodes.new('ShaderNodeAddShader'); mix = nt.nodes.new('ShaderNodeMixShader'); mix.inputs['Fac'].default_value = rgba[3]
    nt.links.new(e.outputs[0], a1.inputs[0]); nt.links.new(g.outputs[0], a1.inputs[1])
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(a1.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], o.inputs['Surface'])
    return m


def mat_glass_tex(tex, bright=1.0, gloss=True):
    """Glass or water by its RGBA texture: alpha is how much of it shows, as in the game's Sprites/Default."""
    m = bpy.data.materials.new('glasst'); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o = nt.nodes.new('ShaderNodeOutputMaterial'); tr = nt.nodes.new('ShaderNodeBsdfTransparent'); tr.inputs[0].default_value = (1, 1, 1, 1)
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = img(tex)
    e = nt.nodes.new('ShaderNodeEmission'); e.inputs['Strength'].default_value = bright
    nt.links.new(t.outputs['Color'], e.inputs['Color'])
    mix = nt.nodes.new('ShaderNodeMixShader'); nt.links.new(t.outputs['Alpha'], mix.inputs['Fac'])
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(e.outputs[0], mix.inputs[2])
    if gloss:
        g = nt.nodes.new('ShaderNodeBsdfGlossy'); g.inputs['Roughness'].default_value = .04
        f = nt.nodes.new('ShaderNodeFresnel'); f.inputs['IOR'].default_value = 1.5
        m2 = nt.nodes.new('ShaderNodeMixShader'); hm = nt.nodes.new('ShaderNodeMath'); hm.operation = 'MULTIPLY'; hm.inputs[1].default_value = .45
        nt.links.new(f.outputs[0], hm.inputs[0]); nt.links.new(hm.outputs[0], m2.inputs['Fac'])
        nt.links.new(mix.outputs[0], m2.inputs[1]); nt.links.new(g.outputs[0], m2.inputs[2]); nt.links.new(m2.outputs[0], o.inputs['Surface'])
    else:
        nt.links.new(mix.outputs[0], o.inputs['Surface'])
    return m


FIT = {'LionsMane': (.99, .1032), 'Aurelia': (1.25, .1113), 'Crystal': (1.05, .1115), 'SeaNettle': (1.08, .0993), 'Periphylla': (1.01, .0858), 'CombJelly': (1.34, .1025)}


def mat_solid(tex, rough=.5, metal=0.0):
    m = bpy.data.materials.new('solid'); m.use_nodes = True; nt = m.node_tree
    b = nt.nodes['Principled BSDF']; t = nt.nodes.new('ShaderNodeTexImage'); t.image = img(tex)
    nt.links.new(t.outputs['Color'], b.inputs['Base Color']); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metal
    return m


def put(ob, m, loc=(0, 0, 0), scale=1.0, rot=0.0):
    ob.data.materials.append(m); ob.location = loc; ob.scale = (scale,) * 3; ob.rotation_euler = (0, 0, rot)
    return ob


def jar_at(sp, col, x, y, n=0, glass_a=None):
    T = os.path.join(OUT, 'textures')
    put(mesh(f'g{n}', 'JarGlass'), mat_glass_tex(os.path.join(T, 'JarGlass.png'), 1.0), (x, y, 0))
    if sp: put(mesh(f'w{n}', 'JarWater'), mat_glass_tex(os.path.join(T, 'JarWater.png'), .9, gloss=False), (x, y, 0))
    put(mesh(f'l{n}', 'JarLid'), mat_solid(os.path.join(T, 'JarLid.png'), .5, .15), (x, y, 0))
    if sp:
        k, z = FIT[sp]
        put(mesh(f'j{n}', 'Jelly_' + sp), mat_jelly(os.path.join(T, f'Jelly_{sp}_{col}.png')), (x, y, z), k, .4 * n)


def lights(sc, night=True):
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (.010, .014, .022, 1) if night else (.35, .38, .42, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 1
    for rot, e in [((math.radians(40), 0, math.radians(30)), 3.0), ((math.radians(60), 0, math.radians(-120)), 1.2)]:
        l = bpy.data.lights.new('l', 'SUN'); l.energy = e * (.35 if night else 1)
        o = bpy.data.objects.new('l', l); o.rotation_euler = rot; bpy.context.collection.objects.link(o)


def camera(sc, loc, target, lens=50, ortho=None):
    c = bpy.data.cameras.new('c'); c.lens = lens
    if ortho: c.type = 'ORTHO'; c.ortho_scale = ortho
    o = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(o); o.location = loc
    d = np.array(target) - np.array(loc)
    o.rotation_euler = (math.atan2(math.hypot(d[0], d[1]), -d[2]), 0, math.atan2(d[1], d[0]) - math.pi / 2)
    sc.camera = o


def floor(sc):
    bpy.ops.mesh.primitive_plane_add(size=20)
    p = bpy.context.object; m = bpy.data.materials.new('f'); m.use_nodes = True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.12, .10, .08, 1)
    p.data.materials.append(m)


if MODE == 'sheet':
    sc = reset(); lights(sc); floor(sc)
    order = list(info['colours'].items())
    for r, (sp, cols) in enumerate(order):
        for c, col in enumerate(cols):
            jar_at(sp, col, c * .14, -r * .14, r * 10 + c)
    camera(sc, (.21, -.35 - .9, 1.05), (.21, -.38, .05), 38)
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1500
    sc.render.filepath = os.path.join(OUT, 'sheet_jars.png'); bpy.ops.render.render(write_still=True)
elif MODE == 'species':
    sc = reset(); lights(sc); sc.cycles.samples = 32
    T = os.path.join(OUT, 'textures')
    for r, (sp, cols) in enumerate(info['colours'].items()):
        for c, col in enumerate(cols):
            put(mesh(f'j{r}{c}', 'Jelly_' + sp), mat_jelly(os.path.join(T, f'Jelly_{sp}_{col}.png')), (c * .1, 0, -r * .1 + (.03 if sp != 'Periphylla' else .015)), 1.0, .5)
    camera(sc, (.15, -1.0, -.25 - .12), (.15, 0, -.25), 50, ortho=.62)
    sc.render.resolution_x, sc.render.resolution_y = 1000, 1450
    sc.render.filepath = os.path.join(OUT, 'sheet_species.png'); bpy.ops.render.render(write_still=True)
elif MODE == 'close':
    sc = reset(); lights(sc); sc.cycles.samples = 48
    T = os.path.join(OUT, 'textures')
    for c, sp in enumerate(['LionsMane', 'Aurelia', 'SeaNettle', 'Crystal']):
        col = info['colours'][sp][0]
        put(mesh(f'c{c}', 'Jelly_' + sp), mat_jelly(os.path.join(T, f'Jelly_{sp}_{col}.png')), (c * .09, 0, 0), 1.0, .5)
    camera(sc, (.135, -.6, -.30), (.135, 0, -.01), 50, ortho=.38)
    sc.render.resolution_x, sc.render.resolution_y = 1400, 700
    sc.render.filepath = os.path.join(OUT, 'sheet_close.png'); bpy.ops.render.render(write_still=True)
elif MODE == 'show':
    sc = reset(); lights(sc, night=True); floor(sc); sc.cycles.samples = 64
    glow = info['glow']
    for k, (sp, col, x) in enumerate([('LionsMane', 'Amber', -.15), ('Crystal', 'Green', 0), ('Periphylla', 'Red', .15)]):
        jar_at(sp, col, x, 0, k)
        l = bpy.data.lights.new('p', 'POINT'); l.energy = 1.2; l.color = glow[f'{sp}_{col}']; l.shadow_soft_size = .02
        o = bpy.data.objects.new('p', l); o.location = (x, 0, .09); bpy.context.collection.objects.link(o)
        o.visible_glossy = False; o.visible_transmission = False; o.visible_camera = False
    camera(sc, (0, -.62, .22), (0, 0, .08), 50)
    sc.render.resolution_x, sc.render.resolution_y = 1400, 800
    sc.render.filepath = os.path.join(OUT, 'sheet_show.png'); bpy.ops.render.render(write_still=True)
elif MODE == 'aqua':
    sc = reset(); lights(sc); floor(sc)
    T = os.path.join(OUT, 'textures')
    put(mesh('f', 'AquaFrame'), mat_solid(os.path.join(T, 'AquaFrame.png'), .6))
    put(mesh('g', 'AquaGlass'), mat_glass_tex(os.path.join(T, 'AquaGlass.png')))
    put(mesh('w', 'AquaWater'), mat_glass_tex(os.path.join(T, 'AquaWater.png'), .9, gloss=False))
    for k, (sp, col, x, y, z) in enumerate([('LionsMane', 'Amber', -.12, .0, .26), ('Crystal', 'Green', .14, -.03, .31), ('Aurelia', 'Pink', .03, .05, .20)]):
        put(mesh(f'j{k}', 'Jelly_' + sp), mat_jelly(os.path.join(T, f'Jelly_{sp}_{col}.png')), (x, y, z), 2.4, k)
    camera(sc, (.25, -1.15, .62), (0, 0, .2), 40)
    sc.render.resolution_x, sc.render.resolution_y = 1400, 900
    sc.render.filepath = os.path.join(OUT, 'sheet_aquarium.png'); bpy.ops.render.render(write_still=True)
elif MODE == 'icons':
    os.makedirs(os.path.join(OUT, 'icons'), exist_ok=True)
    T = os.path.join(OUT, 'textures')
    jobs = [('Jar', None, None)] + [(f'Jelly_{sp}_{c}', sp, c) for sp, cs in info['colours'].items() for c in cs] + [('Aquarium', 'A', None)]
    for name, sp, col in jobs:
        if ONLY and name not in ONLY: continue
        sc = reset(); lights(sc, night=False)
        sc.render.film_transparent = True; sc.render.resolution_x = sc.render.resolution_y = 256
        if sp == 'A':
            put(mesh('f', 'AquaFrame'), mat_solid(os.path.join(T, 'AquaFrame.png'), .6)); put(mesh('g', 'AquaGlass'), mat_glass_tex(os.path.join(T, 'AquaGlass.png')))
            put(mesh('w', 'AquaWater'), mat_glass_tex(os.path.join(T, 'AquaWater.png'), .9, gloss=False))
            put(mesh('j', 'Jelly_LionsMane'), mat_jelly(os.path.join(T, 'Jelly_LionsMane_Amber.png')), (0, 0, .25), 2.4)
            camera(sc, (.55, -1.1, .75), (0, 0, .2), 50, ortho=.85)
        elif sp in ('D', 'R', 'B'):
            key = {'D': 'Dead', 'R': 'Dried', 'B': 'Bait'}[sp]
            put(mesh('f', key), mat_solid(os.path.join(T, 'Food.png'), .35))
            camera(sc, (.12, -.20, .25), (0, 0, .0), 50, ortho=.15 if sp != 'B' else .07)
        else:
            jar_at(sp, col, 0, 0, glass_a=.07)
            camera(sc, (.16, -.42, .26), (0, 0, .09), 50, ortho=.22)
        # two passes: on transparent film (alpha of the solid parts) and on a dark backdrop (the glow and the glass), mixed into one icon
        fa = os.path.join(OUT, 'icons', '_a.png'); fb = os.path.join(OUT, 'icons', '_b.png')
        sc.render.filepath = fa; bpy.ops.render.render(write_still=True)
        sc.render.film_transparent = False
        sc.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.02, .025, .035, 1)
        sc.render.filepath = fb; bpy.ops.render.render(write_still=True)
        from PIL import Image
        A = np.asarray(Image.open(fa).convert('RGBA'), float) / 255; B = np.asarray(Image.open(fb).convert('RGB'), float) / 255
        bg = np.array([.02, .025, .035]) ** (1 / 2.2)
        lum = (B - bg).clip(0).max(axis=2)
        alpha = np.clip(A[..., 3] + lum * 1.6, 0, 1)
        Image.fromarray((np.dstack([B, alpha]) * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'icons', name + '.png'))
