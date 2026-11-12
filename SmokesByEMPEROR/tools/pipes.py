"""Four pipes + improved cigars (bpy). python3 pipes.py <texdir> <out.png>"""
import sys, os, math, random
import bpy, bmesh
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import models
from models import lathe, mat_principled, disc_mat, group


def sweep(name, path, radii, seg=32, flat=1.0, bore=0.0, bore_depth=0.02, bore_flat=0.3):
    """Tube along path (list of Vector in XZ plane). radii per point; flat squashes the Y... no: cross-section width along Y = r, height = r*flat."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new('UVMap')
    n = len(path)
    acc = [0.0]
    for a, b in zip(path, path[1:]):
        acc.append(acc[-1] + (b - a).length)
    rings = []
    side = Vector((0, 1, 0))
    for i, p in enumerate(path):
        t = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
        up = t.cross(side).normalized()
        r = radii[i]
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            ring.append(bm.verts.new(p + side * (math.cos(a) * r) + up * (math.sin(a) * r * flat)))
        rings.append(ring)
    for i in range(n - 1):
        for k in range(seg):
            k2 = (k + 1) % seg
            f = bm.faces.new((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]))
            for loop, (u, v) in zip(f.loops, ((k / seg, acc[i]), ((k + 1) / seg, acc[i]), ((k + 1) / seg, acc[i + 1]), (k / seg, acc[i + 1]))):
                loop[uvl].uv = (u, v / acc[-1])
    # caps; with bore > 0 the far end is open: an air channel of that radius going back into the stem (material slot 1, dark)
    caps = (rings[0], rings[-1]) if bore <= 0 else (rings[0],)
    if bore > 0:
        p = path[-1]
        t = (path[-1] - path[-2]).normalized()
        up = t.cross(side).normalized()
        def ring_at(c, r):
            return [bm.verts.new(c + side * (math.cos(2 * math.pi * k / seg) * r) + up * (math.sin(2 * math.pi * k / seg) * r * bore_flat)) for k in range(seg)]
        # the lip slot: a wide flat opening narrowing into the round air channel
        inner = ring_at(p, bore * 2.6)
        mid = ring_at(p - t * 0.003, bore * 1.4)
        deep = ring_at(p - t * bore_depth, bore)
        for k in range(seg):
            k2 = (k + 1) % seg
            bm.faces.new((rings[-1][k], rings[-1][k2], inner[k2], inner[k]))
            f = bm.faces.new((inner[k], inner[k2], mid[k2], mid[k]))
            f.material_index = 1
            f = bm.faces.new((mid[k], mid[k2], deep[k2], deep[k]))
            f.material_index = 1
        c = bm.verts.new(p - t * bore_depth)
        for k in range(seg):
            f = bm.faces.new((deep[k], deep[(k + 1) % seg], c))
            f.material_index = 1
    for ring in caps:
        c = bm.verts.new(sum((v.co for v in ring), Vector()) / len(ring))
        for k in range(seg):
            try:
                bm.faces.new((ring[k], ring[(k + 1) % seg], c) if ring is rings[-1] else (ring[(k + 1) % seg], ring[k], c))
            except ValueError:
                pass
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def bezier(p0, p1, p2, n=24):
    return [p0 * (1 - t) ** 2 + p1 * 2 * (1 - t) * t + p2 * t * t for t in (i / (n - 1) for i in range(n))]


def glossy(name, img, rough=0.16, coat=0.6):
    m = mat_principled(name, img=img, rough=rough)
    b = m.node_tree.nodes['Principled BSDF']
    try:
        b.inputs['Coat Weight'].default_value = coat
        b.inputs['Coat Roughness'].default_value = 0.08
    except KeyError:
        pass
    return m


def char_mat():
    return models.mat_noise('char', (0.02, 0.012, 0.008), (0.09, 0.05, 0.03), 300, 0.95)


def chamber_mat(name='char_deep'):
    """Charred chamber: noise char shaded darker with depth from the 'cav' vertex colour (1 at the rim, 0 at the floor), so the hole
    reads as deep even under flat game light (the bake keeps base colour only)."""
    m = models.mat_noise(name, (0.035, 0.02, 0.012), (0.12, 0.07, 0.04), 300, 0.95)
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    src = b.inputs['Base Color'].links[0].from_socket
    at = nt.nodes.new('ShaderNodeVertexColor')
    at.layer_name = 'cav'
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0.0, 0.0, 0.0, 1)
    ramp.color_ramp.elements[1].position = 1.0
    ramp.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1)
    nt.links.new(at.outputs['Color'], ramp.inputs['Fac'])
    mul = nt.nodes.new('ShaderNodeMixRGB')
    mul.blend_type = 'MULTIPLY'
    mul.inputs['Fac'].default_value = 1.0
    nt.links.new(src, mul.inputs['Color1'])
    nt.links.new(ramp.outputs['Color'], mul.inputs['Color2'])
    nt.links.new(mul.outputs['Color'], b.inputs['Base Color'])
    return m


def cavity(ob, z_top, z_bottom, power=1.6):
    """Per-vertex 'cav' colour: 1 at z_top falling to 0 at z_bottom."""
    me = ob.data
    attr = me.color_attributes.new('cav', 'FLOAT_COLOR', 'POINT')
    for i, v in enumerate(me.vertices):
        t = max(0.0, min(1.0, (v.co.z - z_bottom) / max(1e-6, z_top - z_bottom)))
        c = 0.30 + 0.70 * t ** power   # 11-09: floor lighter (was 0.06, black) so the draft hole shows on it
        attr.data[i].color = (c, c, c, 1.0)


def bowl(name, outer, rim_z, inner_r, depth, wood):
    """outer: (r,z) from bottom centre up to rim outer edge. Adds a rounded rim and a charred chamber that darkens with depth."""
    o = lathe(name, outer)
    o.data.materials.append(wood)
    r_out = outer[-1][0]
    # rim: flat top rounding over into the chamber
    # rim top in the bowl's own material, only its inner edge charred
    top = lathe(name + '_top', [(r_out, rim_z), (r_out - 0.001, rim_z + 0.0006), (inner_r + 0.0024, rim_z + 0.0007)])
    top.data.materials.append(wood)
    lip = lathe(name + '_lip', [(inner_r + 0.0024, rim_z + 0.0007), (inner_r + 0.0006, rim_z + 0.0002), (inner_r, rim_z - 0.0008)])
    lip.data.materials.append(models.mat_noise('rimchar', (0.05, 0.028, 0.015), (0.16, 0.09, 0.05), 300, 0.9))
    # chamber: straight walls, a rounded floor (many rings so the depth shading is smooth)
    prof = [(inner_r, rim_z - 0.0008)]
    for k in range(1, 9):
        prof.append((inner_r * (1 - 0.06 * k / 8), rim_z - 0.0008 - (depth - 0.006) * k / 8))
    for k in range(1, 7):
        a = math.pi / 2 * k / 6
        prof.append((inner_r * 0.94 * math.cos(a), rim_z - depth + 0.006 - 0.006 * math.sin(a)))
    rim = lathe(name + '_rim', prof)
    rim.data.materials.append(chamber_mat())
    cavity(rim, rim_z, rim_z - depth * 0.85)
    # 11-09 user: the draft hole at the bottom of the chamber, where the smoke goes into the shank (stem side = -X)
    a = math.radians(32)
    hr = inner_r * 0.94 * math.cos(a) - 0.0004
    hz = rim_z - depth + 0.006 - 0.006 * math.sin(a) + 0.0006
    bpy.ops.mesh.primitive_circle_add(vertices=20, radius=min(0.0026, inner_r * 0.27), fill_type='NGON', location=(-hr, 0.0, hz))
    hole = bpy.context.active_object
    hole.name = name + '_draft'
    # facing into the chamber (towards +X) and a little up, lying on the curved wall
    hole.rotation_euler = (0.0, math.radians(90 - 32), 0.0)
    hole.data.materials.append(mat_principled('drafthole', (0.004, 0.003, 0.002), 0.95))
    return [o, top, lip, rim, hole]


def vulcanite():
    return mat_principled('vulcanite', (0.015, 0.015, 0.016), 0.18)


def _nodes(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    return m, nt, nt.nodes['Principled BSDF'], nt.nodes.new('ShaderNodeTexCoord')


def rustic_mat():
    """Sandblasted / rusticated briar: deep carved pits, dark matte with lighter worn ridges."""
    m, nt, b, tc = _nodes('rustic')
    vor = nt.nodes.new('ShaderNodeTexVoronoi')
    vor.inputs['Scale'].default_value = 260
    vor.feature = 'SMOOTH_F1'
    noi = nt.nodes.new('ShaderNodeTexNoise')
    noi.inputs['Scale'].default_value = 500
    noi.inputs['Detail'].default_value = 10
    nt.links.new(tc.outputs['Object'], vor.inputs['Vector'])
    nt.links.new(tc.outputs['Object'], noi.inputs['Vector'])
    mix = nt.nodes.new('ShaderNodeMath'); mix.operation = 'MULTIPLY'
    nt.links.new(vor.outputs['Distance'], mix.inputs[0]); nt.links.new(noi.outputs['Fac'], mix.inputs[1])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.012, 0.007, 0.004, 1)
    ramp.color_ramp.elements[1].color = (0.16, 0.075, 0.035, 1)
    ramp.color_ramp.elements[1].position = 0.35
    nt.links.new(mix.outputs[0], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.9; bump.inputs['Distance'].default_value = 0.0006
    nt.links.new(mix.outputs[0], bump.inputs['Height'])
    nt.links.new(bump.outputs['Normal'], b.inputs['Normal'])
    rr = nt.nodes.new('ShaderNodeMapRange'); rr.inputs['To Min'].default_value = 0.75; rr.inputs['To Max'].default_value = 0.45
    nt.links.new(mix.outputs[0], rr.inputs['Value']); nt.links.new(rr.outputs['Result'], b.inputs['Roughness'])
    return m


def meerschaum_mat(z_low=0.0, z_high=0.07):
    """Meerschaum: creamy white mineral, honey patina growing from the shank and bottom, waxy sheen."""
    m, nt, b, tc = _nodes('meerschaum')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], sep.inputs['Vector'])
    noi = nt.nodes.new('ShaderNodeTexNoise'); noi.inputs['Scale'].default_value = 40; noi.inputs['Detail'].default_value = 6
    nt.links.new(tc.outputs['Object'], noi.inputs['Vector'])
    zr = nt.nodes.new('ShaderNodeMapRange'); zr.inputs['From Min'].default_value = z_low; zr.inputs['From Max'].default_value = z_high
    zr.inputs['To Min'].default_value = 0.0; zr.inputs['To Max'].default_value = 1.0
    nt.links.new(sep.outputs['Z'], zr.inputs['Value'])
    add = nt.nodes.new('ShaderNodeMath'); add.operation = 'ADD'
    nt.links.new(zr.outputs['Result'], add.inputs[0])
    sub = nt.nodes.new('ShaderNodeMath'); sub.operation = 'MULTIPLY'; sub.inputs[1].default_value = 0.5
    nt.links.new(noi.outputs['Fac'], sub.inputs[0]); nt.links.new(sub.outputs[0], add.inputs[1])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.42, 0.20, 0.06, 1)   # honey patina low
    ramp.color_ramp.elements[1].color = (0.86, 0.80, 0.66, 1)   # cream top
    ramp.color_ramp.elements[0].position = 0.40
    ramp.color_ramp.elements[1].position = 0.95
    nt.links.new(add.outputs[0], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = 0.42
    b.inputs['Subsurface Weight'].default_value = 0.15
    b.inputs['Subsurface Radius'].default_value = (0.002, 0.0015, 0.001)
    bn = nt.nodes.new('ShaderNodeTexNoise'); bn.inputs['Scale'].default_value = 900
    nt.links.new(tc.outputs['Object'], bn.inputs['Vector'])
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.08
    nt.links.new(bn.outputs['Fac'], bump.inputs['Height']); nt.links.new(bump.outputs['Normal'], b.inputs['Normal'])
    return m


def amber_mat():
    m, nt, b, tc = _nodes('amber')
    b.inputs['Base Color'].default_value = (0.75, 0.32, 0.04, 1)
    b.inputs['Roughness'].default_value = 0.2
    b.inputs['Transmission Weight'].default_value = 0.75
    b.inputs['IOR'].default_value = 1.54
    return m


def clay_mat():
    """White clay: chalky, matte, grey grime and brown tobacco stain toward the bowl top and mouth end."""
    m, nt, b, tc = _nodes('clay')
    noi = nt.nodes.new('ShaderNodeTexNoise'); noi.inputs['Scale'].default_value = 90; noi.inputs['Detail'].default_value = 8
    nt.links.new(tc.outputs['Object'], noi.inputs['Vector'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.50, 0.44, 0.36, 1)
    ramp.color_ramp.elements[1].color = (0.84, 0.82, 0.77, 1)
    ramp.color_ramp.elements[0].position = 0.3
    ramp.color_ramp.elements[1].position = 0.62
    nt.links.new(noi.outputs['Fac'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = 0.9
    bn = nt.nodes.new('ShaderNodeTexNoise'); bn.inputs['Scale'].default_value = 1500
    nt.links.new(tc.outputs['Object'], bn.inputs['Vector'])
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.15
    nt.links.new(bn.outputs['Fac'], bump.inputs['Height']); nt.links.new(bump.outputs['Normal'], b.inputs['Normal'])
    return m


def worn_vulcanite():
    m, nt, b, tc = _nodes('vulc_worn')
    noi = nt.nodes.new('ShaderNodeTexNoise'); noi.inputs['Scale'].default_value = 60
    nt.links.new(tc.outputs['Object'], noi.inputs['Vector'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.012, 0.012, 0.012, 1)
    ramp.color_ramp.elements[1].color = (0.05, 0.045, 0.03, 1)   # slight oxidised olive
    nt.links.new(noi.outputs['Fac'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = 0.45
    return m


def pipe_billiard(tex):
    wood = rustic_mat()
    parts = bowl('billiard', [(0.0, 0.0), (0.008, 0.0006), (0.0138, 0.005), (0.0158, 0.014), (0.0162, 0.030), (0.0160, 0.044)], 0.044, 0.0098, 0.032, wood)
    path = [Vector((-0.012 - t * 0.04, 0, 0.011)) for t in (i / 10 for i in range(11))]
    sh = sweep('billiard_shank', path, [0.0072 - 0.0005 * i / 10 for i in range(11)])
    sh.data.materials.append(wood)
    stem = sweep('billiard_stem', [Vector((-0.052 - t * 0.068, 0, 0.011 + t * 0.004)) for t in (i / 20 for i in range(21))],
                 [0.0062 - 0.0022 * (i / 20) + (0.0006 if i >= 18 else 0) for i in range(21)], flat=0.62, bore=0.0011)
    stem.data.materials.append(worn_vulcanite())
    stem.data.materials.append(mat_principled('bore', (0.0015, 0.0012, 0.001), 0.9))
    return parts + [sh, stem]


def pipe_bent(tex):
    wood = meerschaum_mat()
    prof = [(0.0, 0.0), (0.009, 0.0008), (0.0145, 0.005), (0.0175, 0.013), (0.0183, 0.022), (0.0178, 0.032), (0.0166, 0.040), (0.0156, 0.045)]
    parts = bowl('bent', prof, 0.045, 0.0098, 0.034, wood)
    p0, p1, p2 = Vector((-0.012, 0, 0.010)), Vector((-0.05, 0, 0.004)), Vector((-0.075, 0, 0.022))
    sh = sweep('bent_shank', bezier(p0, Vector((-0.03, 0, 0.007)), Vector((-0.045, 0, 0.008)), 12), [0.0074 - 0.0006 * i / 11 for i in range(12)])
    sh.data.materials.append(wood)
    band = sweep('bent_band', bezier(Vector((-0.0445, 0, 0.008)), Vector((-0.0465, 0, 0.0079)), Vector((-0.0485, 0, 0.0078)), 4), [0.0071] * 4)
    band.data.materials.append(mat_principled('gold', (0.85, 0.62, 0.28), 0.3, 1.0))
    stem_path = bezier(Vector((-0.048, 0, 0.0078)), Vector((-0.085, 0, 0.004)), Vector((-0.112, 0, 0.032)), 26)
    stem = sweep('bent_stem', stem_path, [0.0062 - 0.0024 * (i / 25) + (0.0006 if i >= 23 else 0) for i in range(26)], flat=0.62, bore=0.0011)
    stem.data.materials.append(amber_mat())
    stem.data.materials.append(mat_principled('bore', (0.0015, 0.0012, 0.001), 0.9))
    return parts + [sh, band, stem]


def pipe_churchwarden(tex):
    wood = clay_mat()
    parts = bowl('church', [(0.0, 0.0), (0.006, 0.0006), (0.0118, 0.006), (0.0136, 0.016), (0.0140, 0.030), (0.0134, 0.038)], 0.038, 0.0082, 0.027, wood)
    sh = sweep('church_shank', [Vector((-0.010 - t * 0.03, 0, 0.010 + t * 0.002)) for t in (i / 8 for i in range(9))], [0.0062 - 0.0008 * i / 8 for i in range(9)])
    sh.data.materials.append(wood)
    stem_path = bezier(Vector((-0.040, 0, 0.012)), Vector((-0.13, 0, 0.012)), Vector((-0.235, 0, 0.034)), 40)
    stem = sweep('church_stem', stem_path, [0.0052 - 0.0020 * (i / 39) + (0.0005 if i >= 37 else 0) for i in range(40)], flat=0.7, bore=0.0010)
    stem.data.materials.append(wood)
    stem.data.materials.append(mat_principled('bore', (0.0015, 0.0012, 0.001), 0.9))
    return parts + [sh, stem]


def pipe_corncob(tex):
    cob = mat_principled('cob', img=os.path.join(tex, 'pipe_cob.png'), rough=0.85)
    parts = bowl('cob', [(0.0, 0.0), (0.014, 0.0), (0.0172, 0.003), (0.0176, 0.020), (0.0176, 0.050)], 0.050, 0.0110, 0.040, cob)
    plug = lathe('cob_plug', [(0.0, -0.0001), (0.0145, -0.0001)])
    plug.data.materials.append(mat_principled('plaster', (0.72, 0.68, 0.6), 0.9))
    ash = mat_principled('ashwood', img=os.path.join(tex, 'pipe_ash.png'), rough=0.6)
    sh = sweep('cob_shank', [Vector((-0.012 - t * 0.055, 0, 0.012)) for t in (i / 10 for i in range(11))], [0.0058] * 11)
    sh.data.materials.append(ash)
    stem = sweep('cob_stem', [Vector((-0.067 - t * 0.06, 0, 0.012 + t * 0.003)) for t in (i / 16 for i in range(17))],
                 [0.0050 - 0.0016 * (i / 16) for i in range(17)], flat=0.6, bore=0.0010)
    stem.data.materials.append(mat_principled('cobstem', (0.05, 0.035, 0.025), 0.3))
    stem.data.materials.append(mat_principled('bore', (0.0015, 0.0012, 0.001), 0.9))
    return parts + [plug, sh, stem]


def _press(objs, n=4.0, k=0.93):
    """Box-pressed: every round section squared off (superellipse), same for all parts so they keep fitting."""
    for o in objs:
        for v in o.data.vertices:
            x, y = v.co.x, v.co.y
            r = math.hypot(x, y)
            if r < 1e-7:
                continue
            c, s_ = abs(x) / r, abs(y) / r
            f = k / ((c ** n + s_ ** n) ** (1 / n))
            v.co.x, v.co.y = x * f, y * f


def _head_r(R, L, cap, head, z):
    """Radius of the head part at z (z from L - taper .. L)."""
    if head == 'torpedo':
        T = 0.024
        t = (z - (L - T)) / T
        if t <= 0: return R
        tip = 0.2
        if t < 0.85:
            return R * (1 - (1 - tip) * (t / 0.85) ** 1.3)
        t2 = (t - 0.85) / 0.15
        return R * tip * (1 - t2 ** 2) ** 0.5
    t = (z - (L - cap)) / cap
    if t <= 0: return R
    return R * max(0.0, 1 - min(1.0, t) ** 2.4) ** (1 / 2.4)


def cigar(tex, brand, L=0.14, R=0.0095, seed=0, cut=False, head='round', press=False):
    rnd = random.Random(seed)
    cap = 0.008
    hz = L - (0.024 if head == 'torpedo' else cap)
    prof = [(R * 0.98, 0.0)]
    z = 0.004
    while z < hz:
        prof.append((R * (1 + rnd.uniform(-0.012, 0.012)), z))
        z += 0.006
    nh = 22 if head == 'torpedo' else 11
    for k in range(1, nh + 1):
        zz = hz + (L - hz) * k / nh
        prof.append((_head_r(R, L, cap, head, zz), zz))
    prof[-1] = (0.0, L)
    cut_disc = None
    if cut:
        # the cap cut off by a guillotine: a clean flat end showing the filler, a little below the cap's top
        zc = L - (0.006 if head == 'torpedo' else cap * 0.65)
        prof = [q for q in prof if q[1] < zc - 1e-6]
        rc = _head_r(R, L, cap, head, zc)
        prof.append((rc, zc))
        cut_disc = lathe('cigarcut_' + brand, [(rc, zc), (0.0, zc + 0.0001)], seg=64)
        cut_disc.data.materials.append(disc_mat('cutend', os.path.join(tex, 'cigar_foot.png'), rc))
    body = lathe('cigar_' + brand, prof, seg=64, v_range=(0.0, L))
    leaf = mat_principled('wrap_' + brand, img=os.path.join(tex, 'cigar_wrapper_%s.png' % brand), rough=0.55)
    body.data.materials.append(leaf)
    foot = lathe('cigarfoot_' + brand, [(R * 0.98, 0.0), (R * 0.9, 0.0005), (0.0, 0.0006)], seg=64)
    foot.data.materials.append(disc_mat('foot', os.path.join(tex, 'cigar_foot.png'), R))
    band = lathe('band_' + brand, [(R * 1.03, L - 0.045), (R * 1.03, L - 0.028)], v_range=(L - 0.045, L - 0.028), seg=64)
    band.data.materials.append(mat_principled('band_' + brand, img=os.path.join(tex, 'band_%s.png' % brand), rough=0.35))
    parts = [body, foot, band] + ([cut_disc] if cut_disc else [])
    if press:
        _press(parts)
    return parts


def textures(tex):
    from PIL import Image, ImageDraw, ImageFilter
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import wood_leaf_tex as wl
    # lighter briar for the churchwarden
    im = Image.open(os.path.join(tex, 'pipe_briar.png')).convert('RGB')
    im = Image.eval(im, lambda v: min(255, int(v * 1.35)))
    im.save(os.path.join(tex, 'pipe_briar_light.png'))
    # corn cob: rows of pitted cells
    W, H = 1024, 1024
    rnd = random.Random(9)
    cob = Image.new('RGB', (W, H), (196, 166, 110))
    d = ImageDraw.Draw(cob)
    cw, ch = 32, 26
    for row in range(H // ch + 1):
        off = (row % 2) * cw / 2
        for col in range(W // cw + 1):
            x, y = col * cw + off, row * ch
            c = rnd.randint(-14, 14)
            d.rounded_rectangle([x + 3, y + 3, x + cw - 3, y + ch - 3], radius=8, fill=(150 + c, 118 + c, 70 + c))
            d.ellipse([x + 9, y + 8, x + cw - 9, y + ch - 8], fill=(112 + c, 84 + c, 46 + c))
    cob = cob.filter(ImageFilter.GaussianBlur(1.2))
    cob.save(os.path.join(tex, 'pipe_cob.png'))
    ash = Image.new('RGB', (512, 512), (186, 150, 100))
    d = ImageDraw.Draw(ash)
    for x in range(0, 512, 3):
        c = rnd.randint(-18, 10)
        d.line([(x, 0), (x + rnd.randint(-6, 6), 512)], fill=(186 + c, 150 + c, 100 + c), width=2)
    ash.filter(ImageFilter.GaussianBlur(0.8)).save(os.path.join(tex, 'pipe_ash.png'))


if __name__ == '__main__':
    tex, out = sys.argv[1], sys.argv[2]
    textures(tex)
    sc = models.scene_setup((1800, 1000))
    group('p1', pipe_billiard(tex), (-0.13, 0.13, 0.0), (0, 0, math.radians(-10)))
    group('p2', pipe_bent(tex), (0.02, 0.03, 0.0), (0, 0, math.radians(-12)))
    group('p3', pipe_churchwarden(tex), (0.18, 0.16, 0.0), (0, 0, math.radians(-12)))
    group('p4', pipe_corncob(tex), (0.21, -0.07, 0.0), (0, 0, math.radians(-35)))
    group('c1', cigar(tex, 'cohibra', seed=1), (-0.16, -0.06, 0.0095), (0, math.radians(90), math.radians(-6)))
    group('c2', cigar(tex, 'montecrusto', L=0.15, R=0.0098, seed=2), (-0.15, -0.10, 0.0098), (0, math.radians(90), math.radians(4)))
    models.camera(sc, (0.02, -0.50, 0.30), (0.02, 0.01, 0.0), 40)
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
