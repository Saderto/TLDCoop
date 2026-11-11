"""Cigars, tubes, pipe, tobacco pouch, lighter (bpy). Builds meshes with explicit UVs.
Usage: python3 models.py <texdir> <out.png>     (preview render)
       import models; models.build_all(texdir)  (from other scripts)
"""
import sys, os, math
import bpy, bmesh
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, '..', 'fonts')


# ---------------------------------------------------------------- helpers
def lathe(name, prof, seg=48, v_range=None, cap_bottom=False, cap_top=False):
    """prof: list of (r, z). UV u = around (u=0.5 faces -Y), v = z mapped by v_range or by profile length."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new('UVMap')
    if v_range:
        z0, z1 = v_range
        vs = [(z - z0) / (z1 - z0) for _, z in prof]
    else:
        acc = [0.0]
        for a, b in zip(prof, prof[1:]):
            acc.append(acc[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        vs = [a / acc[-1] for a in acc]
    rings = []
    for (r, z) in prof:
        ring = []
        for i in range(seg + 1):
            u = i / seg
            th = 2 * math.pi * u + math.pi / 2
            ring.append(bm.verts.new((r * math.cos(th), r * math.sin(th), z)) if i < seg else None)
        ring[seg] = ring[0]
        rings.append(ring)
    for k in range(len(prof) - 1):
        for i in range(seg):
            a, b = rings[k][i], rings[k][i + 1]
            c, d = rings[k + 1][i + 1], rings[k + 1][i]
            try:
                f = bm.faces.new((a, b, c, d))
            except ValueError:
                continue
            for loop, (u, v) in zip(f.loops, ((i / seg, vs[k]), ((i + 1) / seg, vs[k]), ((i + 1) / seg, vs[k + 1]), (i / seg, vs[k + 1]))):
                loop[uvl].uv = (u, v)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def mat_principled(name, rgb=(0.5, 0.5, 0.5), rough=0.5, metal=0.0, img=None, emission=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if img:
        t = nt.nodes.new('ShaderNodeTexImage')
        t.image = bpy.data.images.load(img, check_existing=True)
        nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
    if emission:
        b.inputs['Emission Color'].default_value = (*emission[0], 1)
        b.inputs['Emission Strength'].default_value = emission[1]
    return m


def mat_noise(name, c0, c1, scale, rough=0.6, wave=None, coord='Object'):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    tc = nt.nodes.new('ShaderNodeTexCoord')
    n = nt.nodes.new('ShaderNodeTexNoise')
    n.inputs['Scale'].default_value = scale
    n.inputs['Detail'].default_value = 8
    nt.links.new(tc.outputs[coord], n.inputs['Vector'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (*c0, 1)
    ramp.color_ramp.elements[1].color = (*c1, 1)
    src = n.outputs['Fac']
    if wave:
        w = nt.nodes.new('ShaderNodeTexWave')
        w.wave_type = wave.get('type', 'BANDS')
        if 'dir' in wave:
            w.bands_direction = wave['dir']
        w.inputs['Scale'].default_value = wave['scale']
        w.inputs['Distortion'].default_value = wave.get('dist', 4)
        w.inputs['Detail'].default_value = 4
        nt.links.new(tc.outputs[coord], w.inputs['Vector'])
        mix = nt.nodes.new('ShaderNodeMath')
        mix.operation = 'MULTIPLY'
        nt.links.new(n.outputs['Fac'], mix.inputs[0])
        nt.links.new(w.outputs['Fac'], mix.inputs[1])
        add = nt.nodes.new('ShaderNodeMath')
        add.operation = 'ADD'
        nt.links.new(mix.outputs[0], add.inputs[0])
        add.inputs[1].default_value = 0.15
        src = add.outputs[0]
    nt.links.new(src, ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = rough
    return m


def group(name, objs, loc=(0, 0, 0), rot=(0, 0, 0)):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    for o in objs:
        o.parent = root
    root.location = loc
    root.rotation_euler = rot
    return root


# ---------------------------------------------------------------- items
def cigar(tex, brand, L=0.14, R=0.0095):
    prof = [(0.0, 0.0), (R * 0.93, 0.0), (R, 0.002)]
    cap = 0.009
    prof += [(R, z) for z in (0.03, 0.08, L - cap)]
    for k in range(1, 11):
        t = k / 10
        prof.append((R * (1 - t ** 2.6) ** (1 / 2.6), L - cap + cap * t))
    prof[-1] = (0.0, L)
    body = lathe('cigar_' + brand, prof)
    leaf = mat_noise('leaf_' + brand, (0.05, 0.024, 0.01), (0.15, 0.075, 0.03), 140, 0.7,
                     wave={'type': 'BANDS', 'dir': 'DIAGONAL', 'scale': 18, 'dist': 3})
    body.data.materials.append(leaf)
    foot = lathe('cigarfoot_' + brand, [(0.0, -0.0001), (R * 0.93, -0.0001)])
    foot.data.materials.append(mat_noise('tobcut', (0.12, 0.06, 0.02), (0.32, 0.18, 0.08), 900, 0.95))
    band = lathe('band_' + brand, [(R + 0.0003, L - 0.045), (R + 0.0003, L - 0.028)], v_range=(L - 0.045, L - 0.028))
    band.data.materials.append(mat_principled('band_' + brand, img=os.path.join(tex, 'band_%s.png' % brand), rough=0.35))
    return [body, foot, band]


def painted_metal(name, rgb=None, img=None, gloss=1.0):
    """Painted or bare aluminium as an albedo: the colour (or label image) shaded round the tube with two thin bright reflections running
    along it and darker flanks, so it reads as metal under the game's plain shader. Tube axis is Z."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Roughness'].default_value = 0.3
    geo = nt.nodes.new('ShaderNodeNewGeometry')

    def dot(dx, dy):
        d = nt.nodes.new('ShaderNodeVectorMath')
        d.operation = 'DOT_PRODUCT'
        nt.links.new(geo.outputs['Normal'], d.inputs[0])
        l = math.hypot(dx, dy)
        d.inputs[1].default_value = (dx / l, dy / l, 0.0)
        return d.outputs['Value']

    def mnode(op, a, bb=None, cc=None):
        n = nt.nodes.new('ShaderNodeMath')
        n.operation = op
        for i, v in enumerate((a, bb, cc)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                n.inputs[i].default_value = v
            else:
                nt.links.new(v, n.inputs[i])
        return n.outputs['Value']
    # soft light from the front-left, flanks falling off
    shade = mnode('MULTIPLY_ADD', dot(-0.45, -1.0), 0.32, 0.78)
    shade = mnode('MAXIMUM', shade, 0.42)
    # two thin reflections along the tube
    s1 = mnode('POWER', mnode('MAXIMUM', dot(-0.75, -0.66), 0.0), 60.0)
    s2 = mnode('POWER', mnode('MAXIMUM', dot(0.85, -0.35), 0.0), 90.0)
    spec = mnode('MULTIPLY', mnode('ADD', s1, mnode('MULTIPLY', s2, 0.6)), 0.85 * gloss)
    if img:
        t = nt.nodes.new('ShaderNodeTexImage')
        t.image = bpy.data.images.load(img, check_existing=True)
        col = t.outputs['Color']
    else:
        rgbn = nt.nodes.new('ShaderNodeRGB')
        rgbn.outputs[0].default_value = (*rgb, 1)
        col = rgbn.outputs[0]
    mul = nt.nodes.new('ShaderNodeMix')
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs['Factor'].default_value = 1.0
    nt.links.new(col, mul.inputs['A'])
    comb = nt.nodes.new('ShaderNodeCombineXYZ')
    for c in ('X', 'Y', 'Z'):
        nt.links.new(shade, comb.inputs[c])
    nt.links.new(comb.outputs['Vector'], mul.inputs['B'])
    hl = nt.nodes.new('ShaderNodeMix')
    hl.data_type = 'RGBA'
    nt.links.new(mnode('MINIMUM', spec, 1.0), hl.inputs['Factor'])
    nt.links.new(mul.outputs['Result'], hl.inputs['A'])
    hl.inputs['B'].default_value = (0.95, 0.95, 0.97, 1)
    nt.links.new(hl.outputs['Result'], b.inputs['Base Color'])
    return m


def tube(tex, brand, L=0.155, R=0.0112, colors=None):
    """An aluminium cigar tube: painted body with the label, rounded closed bottom, and a screw cap a touch wider with a seam, fine
    grip ridges and a domed top."""
    capL = 0.026
    zc = L - capL                      # where the cap starts
    if colors:
        paint, capc, ringc = colors
    elif brand == 'cohibra':
        paint = (0.009, 0.008, 0.007)  # the label's dark ground (linear)
        capc = (0.012, 0.011, 0.010)
        ringc = (0.62, 0.42, 0.08)
    else:
        paint = (0.50, 0.51, 0.53)
        capc = (0.55, 0.56, 0.58)
        ringc = (0.25, 0.02, 0.025)
    body = lathe('tube_' + brand, [(R, 0.006), (R, zc - 0.0004)], v_range=(0.006, zc - 0.0004), seg=64)
    body.data.materials.append(painted_metal('tubelab_' + brand, img=os.path.join(tex, 'tube_%s.png' % brand)))
    # rounded closed bottom in the paint colour
    bot_prof = [(0.0, 0.0)]
    for k in range(1, 9):
        a = k / 8 * math.pi / 2
        bot_prof.append((R * math.sin(a), 0.006 * (1 - math.cos(a))))
    bot_prof.append((R, 0.0062))
    bot = lathe('tubebot_' + brand, bot_prof, seg=64)
    bot.data.materials.append(painted_metal('tubebot_' + brand, rgb=paint))
    # the cap: seam shadow, a thin coloured ring, ridged grip, domed top
    Rc = R + 0.00035
    seam = lathe('tubeseam_' + brand, [(R - 0.0002, zc - 0.0005), (R - 0.0002, zc + 0.0003)], seg=64)
    seam.data.materials.append(mat_principled('tubeseam', (0.01, 0.01, 0.01), 0.6))
    cap_prof = [(Rc, zc), (Rc, zc + 0.0018)]
    z = zc + 0.0022
    for i in range(7):  # grip ridges
        cap_prof += [(Rc + 0.00028, z), (Rc + 0.00028, z + 0.0006), (Rc, z + 0.0010)]
        z += 0.0014
    cap_prof += [(Rc, L - 0.004)]
    for k in range(1, 7):
        a = k / 6 * math.pi / 2
        cap_prof.append((Rc * math.cos(a) * 0.98 + 0.0001, L - 0.004 + 0.004 * math.sin(a)))
    cap_prof.append((0.0, L))
    cap = lathe('tubecap_' + brand, cap_prof, seg=64)
    cap.data.materials.append(painted_metal('tubecap_' + brand, rgb=capc, gloss=1.2))
    ring = lathe('tubering_' + brand, [(Rc + 0.00005, L - 0.0075), (Rc + 0.00005, L - 0.0060)], seg=64)
    ring.data.materials.append(painted_metal('tubering_' + brand, rgb=ringc))
    return [body, bot, seam, cap, ring]


def pipe():
    briar = mat_noise('briar', (0.06, 0.02, 0.008), (0.22, 0.08, 0.03), 35, 0.18,
                      wave={'type': 'RINGS', 'scale': 90, 'dist': 25})
    bowl = lathe('pipe_bowl', [(0.0, 0.0), (0.008, 0.0), (0.0145, 0.006), (0.0172, 0.020), (0.0176, 0.034), (0.0166, 0.045), (0.0156, 0.047),
                               (0.0104, 0.047), (0.0096, 0.016), (0.006, 0.0125), (0.0, 0.012)])
    bowl.data.materials.append(briar)
    shank = lathe('pipe_shank', [(0.0, 0.0), (0.0068, 0.0), (0.0066, 0.045), (0.0, 0.045)])
    shank.rotation_euler = (0, math.radians(-90), 0)
    shank.location = (-0.010, 0, 0.012)
    shank.data.materials.append(briar)
    ring = lathe('pipe_ring', [(0.0069, 0.0), (0.0069, 0.003)])
    ring.rotation_euler = shank.rotation_euler
    ring.location = (-0.052, 0, 0.012)
    ring.data.materials.append(mat_principled('silver', (0.8, 0.8, 0.82), 0.25, 1.0))
    stem = lathe('pipe_stem', [(0.0, 0.0), (0.0055, 0.0), (0.0042, 0.055), (0.0048, 0.062), (0.0046, 0.066), (0.0, 0.066)])
    stem.rotation_euler = (0, math.radians(-90), 0)
    stem.location = (-0.055, 0, 0.012)
    stem.scale = (0.75, 1, 1)
    stem.data.materials.append(mat_principled('vulcanite', (0.02, 0.02, 0.02), 0.2))
    ash = lathe('pipe_tob', [(0.0, 0.040), (0.0099, 0.040)])
    ash.data.materials.append(mat_noise('pipetob', (0.10, 0.05, 0.02), (0.3, 0.17, 0.07), 600, 0.95))
    return [bowl, shank, ring, stem, ash]


def pouch(tex, Wp=0.09, Hp=0.12, T=0.017, n=32):
    me = bpy.data.meshes.new('pouch')
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new('UVMap')
    verts = {}
    for side in (0, 1):
        for j in range(n + 1):
            for i in range(n + 1):
                x = -Wp / 2 + Wp * i / n
                z = Hp * j / n
                ex, ez = abs(2 * i / n - 1), abs(2 * j / n - 1)
                puff = T * (1 - ex ** 6) * (1 - ez ** 4)
                if j > n * 0.92:
                    puff *= 0.25  # crimped top
                y = -puff if side == 0 else puff
                if i in (0, n) or j in (0, n):
                    y = 0.0
                key = (side, i, j)
                if side == 1 and (i in (0, n) or j in (0, n)):
                    verts[key] = verts[(0, i, j)]
                else:
                    verts[key] = bm.verts.new((x, y, z))
    for side in (0, 1):
        for j in range(n):
            for i in range(n):
                q = [verts[(side, i, j)], verts[(side, i + 1, j)], verts[(side, i + 1, j + 1)], verts[(side, i, j + 1)]]
                if side == 1:
                    q.reverse()
                f = bm.faces.new(q)
                for loop in f.loops:
                    co = loop.vert.co
                    u = (co.x + Wp / 2) / Wp
                    v = co.z / Hp
                    if side == 0:
                        loop[uvl].uv = (u * 0.5, v)
                    else:
                        loop[uvl].uv = (0.5 + (1 - u) * 0.5, v)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new('pouch', me)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(mat_principled('pouch', img=os.path.join(tex, 'pouch_captainblock.png'), rough=0.55))
    return [ob]


def rbox(name, size, loc, radius, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    m = o.modifiers.new('bev', 'BEVEL')
    m.width = radius
    m.segments = 6
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def fake_chrome(name, W, D, dark=1.0):
    """Polished chrome as an albedo the game can show: the reflections of a studio, painted across each face (the game's shader has no metal)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Roughness'].default_value = 0.3
    tc = nt.nodes.new('ShaderNodeTexCoord')
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sp = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], sp.inputs['Vector'])
    sn = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(geo.outputs['Normal'], sn.inputs['Vector'])

    def math_node(op, a, bb=None, cc=None):
        n = nt.nodes.new('ShaderNodeMath')
        n.operation = op
        for i, v in enumerate((a, bb, cc)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                n.inputs[i].default_value = v
            else:
                nt.links.new(v, n.inputs[i])
        return n.outputs['Value']
    # across the face: x for the front, back and top; y for the narrow sides
    ux = math_node('ADD', math_node('DIVIDE', sp.outputs['X'], W), 0.5)
    uy = math_node('ADD', math_node('DIVIDE', sp.outputs['Y'], D), 0.5)
    side = math_node('GREATER_THAN', math_node('ABSOLUTE', sn.outputs['X']), 0.6)
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'FLOAT'
    nt.links.new(side, mix.inputs['Factor'])
    nt.links.new(ux, mix.inputs['A'])
    nt.links.new(uy, mix.inputs['B'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    el = ramp.color_ramp.elements
    stops = [(0.0, 0.07), (0.06, 0.15), (0.18, 0.24), (0.36, 0.52), (0.60, 0.66), (0.84, 0.60), (0.94, 0.34), (1.0, 0.12)]
    el[0].position, el[0].color = stops[0][0], (stops[0][1] * dark,) * 3 + (1,)
    el[1].position, el[1].color = stops[-1][0], (stops[-1][1] * dark,) * 3 + (1,)
    for pos, v in stops[1:-1]:
        e = el.new(pos)
        e.color = (v * dark, v * dark, v * 1.02 * dark, 1)
    nt.links.new(mix.outputs['Result'], ramp.inputs['Fac'])
    # the top face catches the most light, the bottom the least
    up = math_node('MULTIPLY_ADD', sn.outputs['Z'], 0.25, 1.0)
    # fine vertical brushing
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (1.0, 1.0, 0.03)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    nz = nt.nodes.new('ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value = 2500
    nz.inputs['Detail'].default_value = 2
    nt.links.new(mp.outputs['Vector'], nz.inputs['Vector'])
    brush = math_node('MULTIPLY_ADD', nz.outputs['Fac'], 0.05, 0.975)
    k = math_node('MULTIPLY', up, brush)
    mul = nt.nodes.new('ShaderNodeMix')
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs['Factor'].default_value = 1.0
    nt.links.new(ramp.outputs['Color'], mul.inputs['A'])
    comb = nt.nodes.new('ShaderNodeCombineXYZ')
    for c in ('X', 'Y', 'Z'):
        nt.links.new(k, comb.inputs[c])
    nt.links.new(comb.outputs['Vector'], mul.inputs['B'])
    # polished bevels catch a sharp highlight
    ax = math_node('ABSOLUTE', sn.outputs['X'])
    ay = math_node('ABSOLUTE', sn.outputs['Y'])
    az = math_node('ABSOLUTE', sn.outputs['Z'])
    edge = math_node('MULTIPLY', math_node('MULTIPLY', math_node('ADD', ax, az), ay), 3.5)
    edge = math_node('MINIMUM', edge, 1.0)
    hl = nt.nodes.new('ShaderNodeMix')
    hl.data_type = 'RGBA'
    nt.links.new(edge, hl.inputs['Factor'])
    nt.links.new(mul.outputs['Result'], hl.inputs['A'])
    hl.inputs['B'].default_value = (0.85, 0.86, 0.9, 1)
    nt.links.new(hl.outputs['Result'], b.inputs['Base Color'])
    return m


def flat_shape(name, pts, y, mat):
    """A flat polygon facing -Y (towards the viewer of the front), points (x, z) in metres."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z)) for x, z in pts]
    bm.faces.new(vs)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    for poly in me.polygons:
        if poly.normal.y > 0:
            poly.flip()
    me.update()
    o.data.materials.append(mat)
    return o


def smooth_loop(pts, n=8):
    """Closed Catmull-Rom through the points."""
    out = []
    m = len(pts)
    for i in range(m):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % m], pts[(i + 2) % m]
        for k in range(n):
            t = k / n
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t * t + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t ** 3) for j in range(2)))
    return out


def flame_mat(name):
    """Engraved flame: dark grooves and bright ridges running diagonally."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    tc = nt.nodes.new('ShaderNodeTexCoord')
    w = nt.nodes.new('ShaderNodeTexWave')
    w.wave_type = 'BANDS'
    w.bands_direction = 'DIAGONAL'
    w.inputs['Scale'].default_value = 260.0
    w.inputs['Distortion'].default_value = 0.0
    nt.links.new(tc.outputs['Object'], w.inputs['Vector'])
    r = nt.nodes.new('ShaderNodeValToRGB')
    r.color_ramp.elements[0].position = 0.35
    r.color_ramp.elements[0].color = (0.08, 0.08, 0.09, 1)
    r.color_ramp.elements[1].position = 0.65
    r.color_ramp.elements[1].color = (0.82, 0.82, 0.84, 1)
    nt.links.new(w.outputs['Fac'], r.inputs['Fac'])
    nt.links.new(r.outputs['Color'], b.inputs['Base Color'])
    return m


def lighter():
    """A pocket windproof lighter after the classic one: high-polish chrome case, lid a third of the height, seam, hinge barrel standing out
    at the side, an engraved flame emblem low on the right of the front."""
    Wl, Dl, Hl, lid = 0.038, 0.013, 0.057, 0.0205
    gap = 0.0004
    hb = Hl - lid - gap
    chrome = fake_chrome('chrome', Wl, Dl)
    # one case with the lid shut: the seam is only a fine dark line around it, as on the real one
    body = rbox('lighter_body', (Wl, Dl, Hl), (0, 0, Hl / 2), 0.0030, chrome)
    top = rbox('lighter_seam', (Wl + 0.00016, Dl + 0.00016, 0.00035), (0, 0, hb + gap / 2), 0.0031, mat_principled('seam', (0.03, 0.03, 0.035), 0.6))
    seam = None
    # hinge on the left side at the seam: the lid swings sideways, so the barrel runs front to back (along Y), five knuckles across the depth
    knuckles = []
    hr = 0.0015
    kl = (Dl - 0.0010) / 5
    hx, hz = -Wl / 2 - 0.0002, hb + gap / 2
    y0 = Dl / 2 - 0.0005
    hinge_mat = fake_chrome('chrome_hinge', 0.004, 0.004, 0.95)
    for i in range(5):
        k = lathe('lighter_knuckle%d' % i, [(0.0, 0.0), (hr, 0.0), (hr, kl - 0.00022), (0.0, kl - 0.00022)], seg=20)
        k.rotation_euler = (math.radians(90), 0, 0)
        k.location = (hx, y0 - i * kl, hz)
        k.data.materials.append(hinge_mat)
        knuckles.append(k)
    pin = lathe('lighter_pin', [(0.0, 0.0), (hr * 0.75, 0.0), (hr * 0.75, 5 * kl), (0.0, 5 * kl)], seg=12)
    pin.rotation_euler = (math.radians(90), 0, 0)
    pin.location = (hx, y0, hz)
    pin.data.materials.append(mat_principled('hingegap', (0.12, 0.12, 0.13), 0.6))
    # flame emblem, lower right of the front: a dark rim and the engraved flame inside
    mm = 0.001
    cx, cz = Wl / 2 - 0.0090, 0.0070
    outline = [(0.0, 0.0), (-2.4, 0.8), (-3.2, 3.0), (-2.9, 5.8), (-1.6, 8.6), (0.6, 11.0), (2.4, 12.4), (1.7, 10.4), (1.5, 8.6), (2.7, 7.2), (3.3, 5.0), (3.2, 2.4), (2.0, 0.5)]
    loop = smooth_loop(outline, 8)
    rim = flat_shape('flame_rim', [(cx + x * mm * 1.12, cz + (z - 0.4) * mm * 1.08) for x, z in loop], -Dl / 2 - 0.00006, mat_principled('flamerim', (0.07, 0.07, 0.08), 0.5))
    flame = flat_shape('flame', [(cx + x * mm, cz + z * mm) for x, z in loop], -Dl / 2 - 0.00010, flame_mat('flame'))
    return [body, top, pin, rim, flame] + knuckles


def disc_mat(name, img, R):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (1 / (2 * R), 1 / (2 * R), 1)
    mp.inputs['Location'].default_value = (0.5, 0.5, 0)
    t = nt.nodes.new('ShaderNodeTexImage')
    t.image = bpy.data.images.load(img, check_existing=True)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    nt.links.new(mp.outputs['Vector'], t.inputs['Vector'])
    nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = 0.9
    return m


def cigarette(tex, brand='milston', L=0.084, R=0.0039):
    body = lathe('cig_' + brand, [(R, 0.0), (R, L)], seg=40, v_range=(0.0, L))
    body.data.materials.append(mat_principled('cig_' + brand, img=os.path.join(tex, 'cigarette_%s.png' % brand), rough=0.6))
    fend = lathe('cig_fend', [(R, 0.0), (R * 0.9, -0.0002), (0.0, -0.0003)], seg=40)
    fend.data.materials.append(disc_mat('cig_fend', os.path.join(tex, 'cig_filter_end.png'), R))
    lip = lathe('cig_lip', [(R, L), (R * 0.95, L), (R * 0.93, L - 0.0006), (0.0, L - 0.0008)], seg=40)
    lip.data.materials.append(disc_mat('cig_tend', os.path.join(tex, 'cig_tobacco_end.png'), R))
    return [body, fend, lip]


# ---------------------------------------------------------------- scene
def scene_setup(res=(1800, 1000)):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 64
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Punchy'
    world = bpy.data.worlds.new('w')
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.42, 0.48, 0.58, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.35
    bpy.ops.mesh.primitive_plane_add(size=2, location=(0, 0, 0))
    bpy.context.active_object.data.materials.append(
        mat_noise('wood', (0.10, 0.055, 0.03), (0.24, 0.14, 0.075), 3, 0.7, wave={'type': 'BANDS', 'scale': 6, 'dist': 8}))
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.9, 0.78)
    sun.data.angle = math.radians(8)
    sun.rotation_euler = (math.radians(50), math.radians(-20), math.radians(-30))
    bpy.context.collection.objects.link(sun)
    fill = bpy.data.objects.new('fill', bpy.data.lights.new('fill', 'AREA'))
    fill.data.energy = 20
    fill.data.size = 1.0
    fill.data.color = (0.75, 0.85, 1.0)
    fill.location = (0.4, -0.5, 0.5)
    fill.rotation_euler = (Vector((0, 0, 0)) - fill.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(fill)
    return sc


def camera(sc, loc, tgt, lens=50):
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    bpy.context.collection.objects.link(cam)
    cam.location = loc
    cam.data.lens = lens
    cam.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam


if __name__ == '__main__':
    tex, out = sys.argv[1], sys.argv[2]
    sc = scene_setup()
    R = 0.0095
    group('cig_coh', cigar(tex, 'cohibra'), (-0.13, -0.03, R), (0, math.radians(90), math.radians(-10)))
    group('cig_mon', cigar(tex, 'montecrusto', L=0.15, R=0.0098), (-0.14, -0.075, 0.0098), (0, math.radians(90), math.radians(-4)))
    group('tube_coh', tube(tex, 'cohibra'), (-0.14, 0.06, 0.0112), (0, math.radians(90), math.radians(-8)))
    group('tube_mon', tube(tex, 'montecrusto'), (-0.13, 0.10, 0.0112), (0, math.radians(90), math.radians(-14)))
    group('pipe', pipe(), (0.10, -0.06, 0.0), (0, 0, math.radians(-30)))
    group('pouch', pouch(tex), (0.07, 0.07, 0.0), (math.radians(-80), 0, math.radians(-8)))
    group('lighter', lighter(), (0.17, 0.02, 0.0), (0, 0, math.radians(-20)))
    camera(sc, (0.0, -0.42, 0.24), (0.0, 0.0, 0.0), 45)
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
