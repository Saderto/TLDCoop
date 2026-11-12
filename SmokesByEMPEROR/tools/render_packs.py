"""Blender (bpy) preview render of the cigarette packs + one cigarette.
python3 render_packs.py <texdir> <out.png> [view]
"""
import sys, os, math
import bpy, bmesh
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import models
from mathutils import Vector

TEX = sys.argv[1]
OUT = sys.argv[2]
VIEW = sys.argv[3] if len(sys.argv) > 3 else 'front'
S = 13
W, H, D = 0.055, 0.088, 0.022
AW, AH = 2048, 2048
BRANDS = ['chapgirl', 'milston', 'marlbrrro', 'parliamint', 'bent', 'caramel']


def uv(xmm, ymm):
    return (xmm * S / AW, 1 - ymm * S / AH)


def pack_mesh(name, dims=(55, 88, 22), s=13):
    """Box centred on x, z from 0..H; front faces -Y. UV per face from atlas layout (dims in mm, s px per mm on the 2048 atlas)."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    w, h, d = dims
    x0, x1, y0, y1, z0, z1 = -w / 2000, w / 2000, -d / 2000, d / 2000, 0, h / 1000

    def uv(xmm, ymm):
        return (xmm * s / AW, 1 - ymm * s / AH)
    faces = {
        # verts (CCW from outside), uvs (atlas mm)
        'front': ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], [(d, d + h), (d + w, d + h), (d + w, d), (d, d)]),
        'right': ([(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], [(d + w, d + h), (2 * d + w, d + h), (2 * d + w, d), (d + w, d)]),
        'back': ([(x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1)], [(2 * d + w, d + h), (2 * d + 2 * w, d + h), (2 * d + 2 * w, d), (2 * d + w, d)]),
        'left': ([(x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1)], [(0, d + h), (d, d + h), (d, d), (0, d)]),
        'top': ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], [(d, d), (d + w, d), (d + w, 0), (d, 0)]),
        'bottom': ([(x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (x0, y0, z0)], [(d, d + h + d), (d + w, d + h + d), (d + w, d + h), (d, d + h)]),
    }
    uvl = bm.loops.layers.uv.new('UVMap')
    vcache = {}
    for k, (vs, uvs) in faces.items():
        bv = []
        for v in vs:
            key = tuple(round(c, 6) for c in v)
            if key not in vcache:
                vcache[key] = bm.verts.new(v)
            bv.append(vcache[key])
        f = bm.faces.new(bv)
        for loop, u in zip(f.loops, uvs):
            loop[uvl].uv = uv(*u)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    mod = ob.modifiers.new('bev', 'BEVEL')
    mod.width = 0.0009
    mod.segments = 3
    mod.harden_normals = True
    bpy.ops.object.select_all(action='DESELECT')
    return ob


def tex_mat(name, path, rough=0.55):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes['Principled BSDF']
    t = nt.nodes.new('ShaderNodeTexImage')
    t.image = bpy.data.images.load(path)
    nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = rough
    return m


def color_mat(name, rgb, rough=0.6, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    return m


def wood_mat():
    m = bpy.data.materials.new('wood')
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    wave = nt.nodes.new('ShaderNodeTexWave')
    wave.inputs['Scale'].default_value = 6
    wave.inputs['Distortion'].default_value = 8
    wave.inputs['Detail'].default_value = 6
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.10, 0.055, 0.03, 1)
    ramp.color_ramp.elements[1].color = (0.24, 0.14, 0.075, 1)
    nt.links.new(wave.outputs['Fac'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = 0.7
    return m


def cigarette(loc, rot):
    L, R, FL = 0.084, 0.004, 0.025
    paper = color_mat('paper', (0.86, 0.85, 0.82), 0.7)
    cork = bpy.data.materials.new('cork')
    cork.use_nodes = True
    nt = cork.node_tree
    b = nt.nodes['Principled BSDF']
    n = nt.nodes.new('ShaderNodeTexNoise')
    n.inputs['Scale'].default_value = 2500
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.42, 0.17, 0.03, 1)
    ramp.color_ramp.elements[1].color = (0.78, 0.42, 0.12, 1)
    nt.links.new(n.outputs['Fac'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    gold = color_mat('ring', (0.75, 0.58, 0.25), 0.35, 1.0)
    tob = color_mat('tob', (0.33, 0.2, 0.09), 0.9)
    parts = [(FL, cork, 0), (0.0015, gold, FL), (L - FL - 0.0015, paper, FL + 0.0015)]
    objs = []
    for length, mat, start in parts:
        bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=R, depth=length, location=(0, 0, start + length / 2))
        o = bpy.context.active_object
        o.data.materials.append(mat)
        bpy.ops.object.shade_smooth()
        objs.append(o)
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=R * 0.97, depth=0.0006, location=(0, 0, L + 0.0002))
    t = bpy.context.active_object
    t.data.materials.append(tob)
    objs.append(t)
    root = bpy.data.objects.new('cig', None)
    bpy.context.collection.objects.link(root)
    for o in objs:
        o.parent = root
    root.location = loc
    root.rotation_euler = rot
    return root


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 64
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 1800, 900
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Punchy'
    world = bpy.data.worlds.new('w')
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.42, 0.48, 0.58, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.35

    bpy.ops.mesh.primitive_plane_add(size=2, location=(0, 0, 0))
    bpy.context.active_object.data.materials.append(wood_mat())

    n = len(BRANDS)
    gap = 0.075
    for i, k in enumerate(BRANDS):
        ob = pack_mesh('pack_' + k)
        ob.data.materials.append(tex_mat('m_' + k, os.path.join(TEX, 'pack_%s.png' % k)))
        x = (i - (n - 1) / 2) * gap
        ob.location = (x, 0, 0)
        if VIEW == 'back':
            ob.rotation_euler = (0, 0, math.radians(180 + 18))
        else:
            ob.rotation_euler = (0, 0, math.radians(-18))
    models.group('cig1', models.cigarette(TEX), (0.03, -0.09, 0.0039), (0, math.radians(90), math.radians(15)))
    models.group('cig2', models.cigarette(TEX), (-0.07, -0.10, 0.0039), (0, math.radians(-90), math.radians(-25)))

    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    bpy.context.collection.objects.link(cam)
    cam.location = (0.0, -0.66, 0.21)
    cam.data.lens = 50
    tgt = Vector((0, 0, 0.04))
    cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam

    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.9, 0.78)
    sun.data.angle = math.radians(8)
    sun.rotation_euler = (math.radians(50), math.radians(-20), math.radians(-30))
    bpy.context.collection.objects.link(sun)
    fill = bpy.data.objects.new('fill', bpy.data.lights.new('fill', 'AREA'))
    fill.data.energy = 25
    fill.data.size = 1.0
    fill.data.color = (0.75, 0.85, 1.0)
    fill.location = (0.4, -0.5, 0.5)
    fill.rotation_euler = (Vector((0, 0, 0)) - fill.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(fill)

    sc.render.filepath = OUT
    bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    main()
