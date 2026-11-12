"""Collectible lighters (11-10, user: not only the Zappo; fun, varied, premium kinds).
Ten kinds of body, each with finishes; engravings from engrave.py. bpy builders return lists of objects, front faces -Y, Z up, metres.
KINDS: zappo, dupant, ronsan, trench, imco, soviet, kolibri, pipe, table, bik."""
import math, os, tempfile
import bpy, bmesh
import models
from models import rbox, lathe, mat_principled, fake_chrome
import engrave as eg

TMP = os.path.join(tempfile.gettempdir(), 'lighter_decals')
os.makedirs(TMP, exist_ok=True)
MM = 0.001

GOLD = (1.0, .74, .34)
BRASS = (.92, .64, .24)
COPPER = (.98, .52, .34)
SILVER = (.97, .97, 1.0)
GUN = (.42, .43, .46)


# ------------------------------------------------------------------ materials
def _links_to(nt, node, sock):
    for l in nt.links:
        if l.to_node == node and l.to_socket.name == sock:
            return l
    return None


def _mix(nt, a, b, fac, blend='MIX'):
    n = nt.nodes.new('ShaderNodeMix')
    n.data_type = 'RGBA'
    n.blend_type = blend
    if isinstance(fac, (int, float)):
        n.inputs['Factor'].default_value = fac
    else:
        nt.links.new(fac, n.inputs['Factor'])
    for s, v in (('A', a), ('B', b)):
        if isinstance(v, tuple):
            n.inputs[s].default_value = v + (1,) if len(v) == 3 else v
        else:
            nt.links.new(v, n.inputs[s])
    return n.outputs['Result']


def _math(nt, op, a, b=None, c=None):
    n = nt.nodes.new('ShaderNodeMath')
    n.operation = op
    for i, v in enumerate((a, b, c)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            n.inputs[i].default_value = v
        else:
            nt.links.new(v, n.inputs[i])
    return n.outputs['Value']


def _face_mask(nt, axis='Y', sign=-1):
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sp = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(geo.outputs['Normal'], sp.inputs['Vector'])
    return _math(nt, 'GREATER_THAN', _math(nt, 'MULTIPLY', sp.outputs[axis], float(sign)), .5)


def _projected(nt, img_path, rect, extension='CLIP', ucoord='X'):
    """image on the XZ plane: rect = (x0, z0, w, h) in object metres; returns (color, alpha) sockets"""
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sp = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], sp.inputs['Vector'])
    x0, z0, w, h = rect
    u = _math(nt, 'DIVIDE', _math(nt, 'SUBTRACT', sp.outputs[ucoord], x0), w)
    v = _math(nt, 'DIVIDE', _math(nt, 'SUBTRACT', sp.outputs['Z'], z0), h)
    cb = nt.nodes.new('ShaderNodeCombineXYZ')
    nt.links.new(u, cb.inputs['X'])
    nt.links.new(v, cb.inputs['Y'])
    t = nt.nodes.new('ShaderNodeTexImage')
    t.image = bpy.data.images.load(img_path, check_existing=False)
    t.image.colorspace_settings.name = 'Non-Color' if img_path.endswith('_cut.png') else 'sRGB'
    t.extension = 'EXTEND' if img_path.endswith('_cut.png') else extension  # cut edges are 'untouched', no dark border from filtering
    t.interpolation = 'Cubic'
    nt.links.new(cb.outputs['Vector'], t.inputs['Vector'])
    return t.outputs['Color'], t.outputs['Alpha']


def _ramp(nt, fac, stops):
    """stops: list of (pos, value or rgb)"""
    r = nt.nodes.new('ShaderNodeValToRGB')
    el = r.color_ramp.elements
    def rgba(v):
        return (v, v, v, 1) if isinstance(v, (int, float)) else tuple(v) + (1,)
    el[0].position, el[0].color = stops[0][0], rgba(stops[0][1])
    el[1].position, el[1].color = stops[-1][0], rgba(stops[-1][1])
    for p, v in stops[1:-1]:
        el.new(p).color = rgba(v)
    nt.links.new(fac, r.inputs['Fac'])
    return r.outputs['Color']


def _normal(nt):
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sp = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(geo.outputs['Normal'], sp.inputs['Vector'])
    return sp


def round_chrome(name, dark=1.0):
    """polished metal on a curved surface: vertical studio bands follow the normal, so a tube reads as metal from any side"""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Roughness'].default_value = 0.3
    n = _normal(nt)
    t = _math(nt, 'MULTIPLY_ADD', n.outputs['X'], .5, .5)
    k = dark
    band = _ramp(nt, t, [(0.0, .06 * k), (0.10, .16 * k), (0.24, .62 * k), (0.30, .97 * k), (0.36, .62 * k), (0.48, .30 * k),
                          (0.62, .44 * k), (0.74, .80 * k), (0.80, .56 * k), (0.92, .16 * k), (1.0, .07 * k)])
    up = _math(nt, 'MULTIPLY_ADD', n.outputs['Z'], .35, 1.0)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (1.0, 1.0, 0.03)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    nz = nt.nodes.new('ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value = 2500
    nt.links.new(mp.outputs['Vector'], nz.inputs['Vector'])
    br = _math(nt, 'MULTIPLY', up, _math(nt, 'MULTIPLY_ADD', nz.outputs['Fac'], .06, .97))
    cb = nt.nodes.new('ShaderNodeCombineXYZ')
    for c in 'XYZ':
        nt.links.new(br, cb.inputs[c])
    col = _mix(nt, band, cb.outputs['Vector'], 1.0, 'MULTIPLY')
    nt.links.new(col, b.inputs['Base Color'])
    return m


def finish_mat(name, fin, W, D, decals=(), round=False):
    """fin: ('chrome'|'brushed', tint) | ('paint', rgb, wear) | ('lacquer', rgb) | ('plastic', rgb) | ('stone', rgb0, rgb1)
    decals: list of (kind 'cut'|'inlay', img_path, rect (x0, z0, w, h), face ('Y', -1))"""
    kind = fin[0]
    if kind in ('chrome', 'brushed', 'paint', 'lacquer'):
        k = .8 if kind == 'brushed' else 1.0
        m = round_chrome(name, k) if round else fake_chrome(name, W, D, k)
        nt = m.node_tree
        b = nt.nodes['Principled BSDF']
        col = _links_to(nt, b, 'Base Color').from_socket
        if kind == 'brushed':
            # flatter reflections: pulled towards a mid grey
            col = _mix(nt, col, (.55, .56, .58), .45)
            if min(fin[1]) >= .9:
                # 11-10 plain brushed chrome (Zappo): satin grey, darker than polished, fine horizontal brushing lines
                col = _mix(nt, col, (.46, .47, .49), .55)
                tcb = nt.nodes.new('ShaderNodeTexCoord')
                mp = nt.nodes.new('ShaderNodeMapping')
                mp.inputs['Scale'].default_value = (25, 25, 2600)
                nt.links.new(tcb.outputs['Object'], mp.inputs['Vector'])
                nb = nt.nodes.new('ShaderNodeTexNoise')
                nb.inputs['Scale'].default_value = 1.0
                nb.inputs['Detail'].default_value = 3
                nt.links.new(mp.outputs['Vector'], nb.inputs['Vector'])
                streak = _math(nt, 'ADD', _math(nt, 'MULTIPLY', nb.outputs['Fac'], .55), .72)
                cs = nt.nodes.new('ShaderNodeCombineXYZ')
                for k in 'XYZ':
                    nt.links.new(streak, cs.inputs[k])
                col = _mix(nt, col, cs.outputs['Vector'], 1.0, 'MULTIPLY')
        if kind in ('chrome', 'brushed'):
            tint = tuple(fin[1])
            if min(tint) < .9:
                # coloured metal (gold, brass, copper): lift the mids so it reads bright and warm, not khaki
                g = nt.nodes.new('ShaderNodeGamma')
                g.inputs['Gamma'].default_value = .72
                nt.links.new(col, g.inputs['Color'])
                col = _mix(nt, g.outputs['Color'], (min(1.0, tint[0] * 1.12), tint[1] * 1.0, tint[2] * .85), 1.0, 'MULTIPLY')
            else:
                col = _mix(nt, col, tint, 1.0, 'MULTIPLY')
        elif kind == 'lacquer':
            # deep colour with the studio reflections showing through faintly
            lac = _mix(nt, col, tuple(fin[1]), 1.0, 'MULTIPLY')
            col = _mix(nt, tuple(c * .9 for c in fin[1]), lac, .55)
            col = _mix(nt, col, (.08, .08, .09), .25, 'ADD')
        else:
            # paint worn through to the metal in patches and on the edges
            tc = nt.nodes.new('ShaderNodeTexCoord')
            nz = nt.nodes.new('ShaderNodeTexNoise')
            nz.inputs['Scale'].default_value = 900
            nz.inputs['Detail'].default_value = 6
            nt.links.new(tc.outputs['Object'], nz.inputs['Vector'])
            # 11-10: worn patches big enough to see, and the paint rubbed off along the vertical edges
            w = float(fin[2])
            nz2 = nt.nodes.new('ShaderNodeTexNoise')
            nz2.inputs['Scale'].default_value = 140
            nz2.inputs['Detail'].default_value = 3
            nz2.inputs['Roughness'].default_value = .5
            nt.links.new(tc.outputs['Object'], nz2.inputs['Vector'])
            patch = _math(nt, 'GREATER_THAN', nz2.outputs['Fac'], .66 - w * .25)
            sp = nt.nodes.new('ShaderNodeSeparateXYZ')
            nt.links.new(tc.outputs['Object'], sp.inputs['Vector'])
            ex = _math(nt, 'GREATER_THAN', _math(nt, 'ABSOLUTE', sp.outputs['X']), W / 2 - .0021)
            ey = _math(nt, 'GREATER_THAN', _math(nt, 'ABSOLUTE', sp.outputs['Y']), D / 2 - .0021)
            edge = _math(nt, 'MULTIPLY', _math(nt, 'MULTIPLY', ex, ey), _math(nt, 'GREATER_THAN', nz.outputs['Fac'], .52))
            wear = _math(nt, 'MULTIPLY', _math(nt, 'MAXIMUM', patch, edge), 1.0 if w > 0 else 0.0)
            metal = col
            paint = _mix(nt, tuple(fin[1]), tuple(c * .8 for c in fin[1]), _math(nt, 'MULTIPLY', nz.outputs['Fac'], .6))
            col = _mix(nt, paint, col, wear)
    elif kind == 'stone':
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        b = nt.nodes['Principled BSDF']
        tc = nt.nodes.new('ShaderNodeTexCoord')
        if fin[1] == 'malachite':
            # concentric bands of three greens, wavy like a cut stone
            w = nt.nodes.new('ShaderNodeTexWave')
            w.wave_type = 'RINGS'
            w.wave_profile = 'SAW'
            w.inputs['Scale'].default_value = 26
            w.inputs['Distortion'].default_value = 9
            w.inputs['Detail'].default_value = 8
            w.inputs['Detail Scale'].default_value = 1.6
            nt.links.new(tc.outputs['Object'], w.inputs['Vector'])
            col = _ramp(nt, w.outputs['Fac'], [(0.0, (.004, .05, .02)), (0.18, (.01, .12, .05)), (0.36, (.03, .30, .13)), (0.46, (.12, .55, .30)),
                                               (0.52, (.02, .20, .09)), (0.74, (.01, .10, .045)), (0.86, (.05, .36, .17)), (1.0, (.006, .06, .025))])
        else:
            # white marble with thin grey veins and a faint cloud
            nz = nt.nodes.new('ShaderNodeTexNoise')
            nz.inputs['Scale'].default_value = 28
            nz.inputs['Detail'].default_value = 12
            nz.inputs['Distortion'].default_value = 3
            nt.links.new(tc.outputs['Object'], nz.inputs['Vector'])
            vein = _math(nt, 'ABSOLUTE', _math(nt, 'SUBTRACT', nz.outputs['Fac'], .5))
            cl = nt.nodes.new('ShaderNodeTexNoise')
            cl.inputs['Scale'].default_value = 60
            nt.links.new(tc.outputs['Object'], cl.inputs['Vector'])
            base = _mix(nt, (.90, .89, .86), (.78, .78, .77), cl.outputs['Fac'])
            col = _mix(nt, base, (.30, .30, .33), _math(nt, 'MULTIPLY', _ramp(nt, vein, [(0.0, 1.0), (0.03, 0.0), (1.0, 0.0)]), .85))
    else:  # glossy plastic: shaded across the roundness, a long highlight stripe on the lit side
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        b = nt.nodes['Principled BSDF']
        n = _normal(nt)
        t = _math(nt, 'MULTIPLY_ADD', n.outputs['X'], .5, .5)
        rgb = tuple(fin[1])
        shade = _ramp(nt, t, [(0.0, .45), (0.2, .85), (0.5, 1.0), (0.8, .8), (1.0, .4)])
        col = _mix(nt, rgb, shade, 1.0, 'MULTIPLY')
        hl = _ramp(nt, t, [(0.0, 0), (0.20, 0), (0.27, .20), (0.34, 0), (0.70, 0), (0.76, .07), (0.82, 0), (1.0, 0)])
        col = _mix(nt, col, hl, 1.0, 'ADD')
        top = _math(nt, 'MULTIPLY', _math(nt, 'MAXIMUM', n.outputs['Z'], 0.0), .18)
        col = _mix(nt, col, (1, 1, 1), top)
    for kind_d, path, rect, face in decals:
        c, a = _projected(nt, path, rect, ucoord='Y' if face[0] == 'X' else 'X')
        mask = _face_mask(nt, *face)
        if kind_d == 'cut' and kind == 'paint':
            # a cut through paint shows the bright metal underneath
            groove = _math(nt, 'MINIMUM', _math(nt, 'MAXIMUM', _math(nt, 'MULTIPLY', _math(nt, 'SUBTRACT', .46, c), 6.0), 0.0), 1.0)
            col = _mix(nt, col, _mix(nt, metal, (.75, .75, .75), .3, 'MULTIPLY'), _math(nt, 'MULTIPLY', groove, _math(nt, 'MULTIPLY', a, mask)))
        elif kind_d == 'cut':
            twice = _mix(nt, c, (2, 2, 2), 1.0, 'MULTIPLY')
            f = _math(nt, 'MULTIPLY', a, mask)
            col = _mix(nt, col, _mix(nt, col, twice, 1.0, 'MULTIPLY'), f)
        else:
            col = _mix(nt, col, c, _math(nt, 'MULTIPLY', a, mask))
    nt.links.new(col, b.inputs['Base Color'])
    return m


def save(img, key):
    p = os.path.join(TMP, key + '.png')
    img.save(p)
    return p


def cut_decal(key, size_mm, draw):
    return save(eg.decal(size_mm, draw), key + '_cut')


def cyl(name, r, z0, z1, mat, seg=40, cap=True):
    o = lathe(name, [(0, z0), (r, z0), (r, z1), (0, z1)] if cap else [(r, z0), (r, z1)], seg=seg)
    o.data.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT')
    return o


def wheel(name, r, w, loc, mat, axis='Y'):
    o = cyl(name, r, -w / 2, w / 2, mat, seg=24)
    o.rotation_euler = (math.radians(90), 0, 0) if axis == 'Y' else (0, math.radians(90), 0)
    o.location = loc
    return o


# ------------------------------------------------------------------ kinds
def zappo(fin, emblem=None, flame=False, back=None):
    """the classic windproof case. emblem: draw(m, W, H, ppm) for the body front"""
    Wl, Dl, Hl, lid = 0.038, 0.013, 0.057, 0.0205
    hb = Hl - lid - 0.0004
    decals = []
    if emblem:
        p = cut_decal('zappo_' + str(abs(hash(emblem.__name__ + str(fin)))), (32, 30), emblem)
        decals.append(('cut', p, (-0.016, 0.003, 0.032, 0.030), ('Y', -1)))  # rbox meshes keep world coords (origin 0)
    body = rbox('lighter_body', (Wl, Dl, Hl), (0, 0, Hl / 2), 0.0030, finish_mat('zbody', fin, Wl, Dl, decals))
    seam = rbox('lighter_seam', (Wl + 0.00016, Dl + 0.00016, 0.00035), (0, 0, hb + 0.0002), 0.0031, mat_principled('seam', (0.03, 0.03, 0.035), 0.6))
    hm = finish_mat('zhinge', fin if fin[0] != 'paint' else ('chrome', (.8, .8, .82)), 0.004, 0.004)
    parts = [body, seam]
    hr, kl = 0.0015, (Dl - 0.0010) / 5
    for i in range(5):
        k = lathe('kn%d' % i, [(0.0, 0.0), (hr, 0.0), (hr, kl - 0.00022), (0.0, kl - 0.00022)], seg=20)
        k.rotation_euler = (math.radians(90), 0, 0)
        k.location = (-Wl / 2 - 0.0002, Dl / 2 - 0.0005 - i * kl, hb + 0.0002)
        k.data.materials.append(hm)
        parts.append(k)
    if flame:
        parts += models.lighter()[3:5]
    return parts


DUP = dict(W=0.036, D=0.0125, H=0.060, lid=0.0150, band=0.0080, colx=0.0105, colw=0.0062, cyl0=0.0195, frame=0.0010)


def diamond_points(size_mm, pitch_mm=1.8, ppm=24):
    """the «pointe de diamant» cut: rows of little four-sided pyramids, each facet catching the light differently (multiply, x2 encoded)"""
    import numpy as np
    from PIL import Image
    W, H = int(size_mm[0] * ppm), int(size_mm[1] * ppm)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    p = pitch_mm * ppm
    fx = (xx % p) / p - .5
    fy = (yy % p) / p - .5
    top = (fy < 0) & (abs(fx) < -fy)
    bot = (fy > 0) & (abs(fx) < fy)
    left = (fx < 0) & ~top & ~bot
    v = np.where(top, 1.45, np.where(bot, .42, np.where(left, 1.05, .70)))
    edge = np.maximum(abs(fx), abs(fy))
    v = np.where(edge > .46, .30, v)  # the cut grooves between the points
    ridge = np.minimum(abs(abs(fx) - abs(fy)), 1)
    v = v * (1 - .18 * (ridge < .03))
    img = (np.clip(v, 0, 2) / 2 * 255).astype(np.uint8)
    return Image.fromarray(np.stack([img] * 3, -1), 'RGB')


def dupant_panels(key, rgb, lid=True, body=True, column=True):
    """lacquer panels inside the thin metal frame: an L-shaped panel below the band, the corner panel under the roller, the lid panel.
    The lacquer is deep and glossy: darker at the bottom, a soft diagonal sheen."""
    import numpy as np
    from PIL import Image
    d = DUP
    ppm = 20
    W, H = int(d['W'] * 1000 * ppm), int(d['H'] * 1000 * ppm)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    xm = xx / ppm - d['W'] * 500  # mm from centre
    zm = (H - yy) / ppm  # mm from bottom
    f, Wm, Hm = d['frame'] * 1000, d['W'] * 1000, d['H'] * 1000
    zl = (d['H'] - d['lid']) * 1000
    zb = zl - d['band'] * 1000
    cx0 = d['colx'] * 1000
    cz0 = d['cyl0'] * 1000
    inside = (abs(xm) < Wm / 2 - f)
    m = np.zeros_like(xm, bool)
    if body:
        # the back (column=False): lacquer from the bottom frame right up to the lid, no band (11-10)
        m |= inside & (zm > f) & (((zm < zb) & ((xm < cx0 - .4) | (zm < cz0 - .6))) if column else (zm < zl - .4))
    if lid:
        m |= inside & (zm > zl + .8) & (zm < Hm - f)
    g = .78 + .30 * (zm / Hm)
    sheen = np.exp(-((xm * .55 + zm * .9 - Hm * .55) / 4.0) ** 2) * .35
    col = np.stack([np.clip(c * g + sheen * (.25 + c), 0, 1) for c in rgb], -1)
    a = (m * 255).astype(np.uint8)
    im = Image.fromarray(np.dstack([(col * 255).astype(np.uint8), a]), 'RGBA')
    return save(im, key)


def dupant_cuts(key, guilloche=False, lid_guilloche=False, back=False):
    """the engraved script on the band, the hinge pin, and (for the cut finishes) the diamond points everywhere but the band and roller column"""
    import numpy as np
    from PIL import Image, ImageDraw
    d = DUP
    ppm = 20
    Wm, Hm = d['W'] * 1000, d['H'] * 1000
    W, H = int(Wm * ppm), int(Hm * ppm)
    base = np.full((H, W), 127, np.uint8)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    xm = xx / ppm - Wm / 2
    zm = (H - yy) / ppm
    zl = (d['H'] - d['lid']) * 1000
    zb = zl - d['band'] * 1000
    cx0 = d['colx'] * 1000
    if guilloche or lid_guilloche:
        dp = np.array(diamond_points((Wm, Hm), 1.8, ppm).convert('L'))
        sel = np.zeros_like(xm, bool)
        if guilloche:
            # the back: the points go from the bottom right up to the lid, no smooth band (11-10)
            sel |= (zm < zl - .4) if back else ((zm < zb) & ((xm < cx0 - .4) | (zm < d['cyl0'] * 1000 - .6)))
        sel |= (zm > zl + .5)
        sel &= (abs(xm) < Wm / 2 - .5) & (zm > .5) & (zm < Hm - .5)
        base = np.where(sel, dp, base).astype(np.uint8)
    im = Image.fromarray(np.stack([base] * 3, -1), 'RGB')
    if back:   # the back: no roller column, no script (11-10: it showed a smooth corner and mirrored writing)
        return save(im, key + '_cut')
    # script name on the band, engraved
    m = Image.new('L', (W, H), 0)
    eg.text(m, 'S.T. Dupant', int((Wm / 2 - 3.2) * ppm), int((Hm - zb - 4.4) * ppm), 4.4 * ppm, 'great-vibes-latin-400-normal.woff')
    eg.text(m, 'PARIS', int((Wm / 2 + 3.6) * ppm), int((Hm - zb - 1.5) * ppm), 1.3 * ppm, 'oswald-latin-400-normal.woff')
    dr = ImageDraw.Draw(m)
    px, pz = 1.6 * ppm, (Hm - zl + 1.3) * ppm
    dr.ellipse([px - .5 * ppm, pz - .5 * ppm, px + .5 * ppm, pz + .5 * ppm], fill=255)
    e = eg.engraving(m, depth=.5, hatch=False)
    e = (np.clip(e, 0, 2) / 2 * 255)
    bm = np.array(m, np.float32)[..., None] / 255
    arr = np.array(im, np.float32) * (1 - bm) + e * bm
    return save(Image.fromarray(arr.astype(np.uint8), 'RGB'), key + '_cut')


def dupant(lacquer=None, metal=GOLD, guilloche=False, lid_guilloche=False):
    """after the S.T. Dupont Ligne 2 (photos 11-10): a slim gold case, lid a quarter of the height, a smooth band under the lid with
    the engraved script, the fluted roller in a column at the right; lacquer panels inside a thin frame, or diamond-point cuts all over"""
    d = DUP
    W, D, H = d['W'], d['D'], d['H']
    zl = H - d['lid']
    decals = []
    if lacquer:
        k = abs(hash(str(lacquer) + str(lid_guilloche)))
        p = dupant_panels('dup_lac_%d' % k, lacquer, lid=not lid_guilloche)
        # the back has no roller column: its body panel is whole (11-10: the gold corner there looked unpainted)
        pb = dupant_panels('dup_lacb_%d' % k, lacquer, lid=not lid_guilloche, column=False)
        decals += [('inlay', p, (-W / 2, 0, W, H), ('Y', -1)), ('inlay', pb, (-W / 2, 0, W, H), ('Y', 1))]
    c = dupant_cuts('dup_%d%d' % (guilloche, lid_guilloche), guilloche, lid_guilloche)
    decals += [('cut', c, (-W / 2, 0, W, H), ('Y', -1))]
    if guilloche or lid_guilloche:
        side = save(diamond_points((12.5, 60), 1.8, 20), 'dup_side_%d%d_cut' % (guilloche, lid_guilloche))
        if guilloche:
            decals += [('cut', side, (-D / 2, 0, D, H), ('X', -1)), ('cut', side, (-D / 2, 0, D, H), ('X', 1))]
        # the back gets its points too (the lid of the blue one as well, 11-10)
        decals += [('cut', dupant_cuts('dupb_%d%d' % (guilloche, lid_guilloche), guilloche, lid_guilloche, back=True), (-W / 2, 0, W, H), ('Y', 1))]
    mt = finish_mat('dmetal', ('chrome', metal), W, D, decals)
    body = rbox('d_body', (W, D, H), (0, 0, H / 2), 0.0009, mt)
    seam = rbox('d_seam', (W + .0001, D + .0001, .00035), (0, 0, zl), .0009, mat_principled('dseam', (.03, .025, .02), .5))
    rm = finish_mat('droll', ('chrome', metal), .01, .01, round=True)
    roll = knurl('d_roller', 0.0029, zl - d['cyl0'] - .0006, (d['colx'] + d['colw'] / 2, -D / 2 + .0007, (zl + d['cyl0']) / 2), axis='Z', teeth=22, rgb=metal)
    for o in roll:
        o.data.materials.clear()
        o.data.materials.append(rm)
    caps = [cyl('d_rcap%d' % i, .0030, z - .0006, z + .0006, rm, seg=32) for i, z in enumerate((d['cyl0'] + .0003, zl - .0009))]
    for o in caps:
        o.location = (d['colx'] + d['colw'] / 2, -D / 2 + .0007, 0)
    return [body, seam] + roll + caps


def knurl(name, r, w, loc, axis='Y', teeth=32, rgb=(.62, .62, .66), depth=.84):
    """a striker wheel: a ring of fine teeth, with the axle showing at both ends"""
    bm = bmesh.new()
    n = teeth * 2
    rings = []
    for z in (-w / 2, w / 2):
        rings.append([bm.verts.new(((r if i % 2 == 0 else r * depth) * math.cos(2 * math.pi * i / n),
                                    (r if i % 2 == 0 else r * depth) * math.sin(2 * math.pi * i / n), z)) for i in range(n)])
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[1])
    for i in range(n):
        bm.faces.new((rings[0][i], rings[0][(i + 1) % n], rings[1][(i + 1) % n], rings[1][i]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(finish_mat(name + '_m', ('chrome', rgb), .01, .01, round=True))
    if axis == 'Z':
        o.location = loc
        return [o]
    ax = cyl(name + '_ax', r * .42, -w / 2 - .0006, w / 2 + .0006, mat_principled(name + '_axm', (.25, .25, .27), .4), seg=16)
    out = [o, ax]
    for p in out:
        p.rotation_euler = (math.radians(90), 0, 0) if axis == 'Y' else (0, math.radians(90), 0)
        p.location = loc
    return out


def dots(key, size_mm, rows, cols, r_mm, rgb=(.03, .03, .035), slot=False):
    """dark holes (chimney, hood) as an inlay picture"""
    def draw(d, W, H, ppm):
        r = r_mm * ppm
        for i in range(rows):
            for j in range(cols):
                cx = W * (j + .5) / cols
                cy = H * (i + .5) / rows
                if slot:
                    d.rounded_rectangle([cx - r, cy - H / rows * .38, cx + r, cy + H / rows * .38], radius=r, fill=tuple(int(c * 255) for c in rgb) + (255,))
                else:
                    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=tuple(int(c * 255) for c in rgb) + (255,))
    return save(eg.enamel(size_mm, draw, 20), key)


def _apply(o, rot=False, scale=False):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=rot, scale=scale)


def torus(name, R, r, z, mat, rot=(0, 0, 0), loc_xy=(0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=48, minor_segments=10, location=(loc_xy[0], loc_xy[1], z), rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    return o


def rrect_prism(name, prof, W, D, r, seg=8):
    """a rounded-rectangle prism: prof = [(offset, z)]; vertical edges rounded with radius r (+offset), flat top and bottom caps"""
    bm = bmesh.new()
    rings = []
    for off, z in prof:
        hw, hd, rr = W / 2 + off, D / 2 + off, max(.0002, r + off)
        pts = []
        for cx, cy, a0 in ((hw - rr, hd - rr, 0), (-hw + rr, hd - rr, 90), (-hw + rr, -hd + rr, 180), (hw - rr, -hd + rr, 270)):
            for i in range(seg + 1):
                a = math.radians(a0 + 90 * i / seg)
                pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        rings.append([bm.verts.new((x, y, z)) for x, y in pts])
    n = len(rings[0])
    for k in range(len(rings) - 1):
        for i in range(n):
            bm.faces.new((rings[k][i], rings[k][(i + 1) % n], rings[k + 1][(i + 1) % n], rings[k + 1][i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = abs(p.normal.z) < .5
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def stadium_prism(name, prof, W, D, seg=14, bulge=0.0):
    """a case with a stadium (rounded-ends) plan: prof = [(offset, z)], ring = half-width W/2+off, half-depth D/2+off, round ends;
    bulge makes the flat faces slightly convex"""
    bm = bmesh.new()
    rings = []
    for off, z in prof:
        hw, hd = W / 2 + off, D / 2 + off
        r = hd
        xs = hw - r
        pts = []
        for i in range(seg + 1):  # right end
            a = -math.pi / 2 + math.pi * i / seg
            pts.append((xs + r * math.cos(a), r * math.sin(a)))
        for i in range(1, 12):  # back face
            x = xs - 2 * xs * i / 12
            pts.append((x, hd + bulge * (1 - (x / xs) ** 2)))
        for i in range(seg + 1):  # left end
            a = math.pi / 2 + math.pi * i / seg
            pts.append((-xs + r * math.cos(a), r * math.sin(a)))
        for i in range(1, 12):  # front face
            x = -xs + 2 * xs * i / 12
            pts.append((x, -hd - bulge * (1 - (x / xs) ** 2)))
        rings.append([bm.verts.new((x, y, z)) for x, y in pts])
    n = len(rings[0])
    for k in range(len(rings) - 1):
        for i in range(n):
            bm.faces.new((rings[k][i], rings[k][(i + 1) % n], rings[k + 1][(i + 1) % n], rings[k + 1][i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def ronsan(fin, text=True):
    """after the Ronson Standard (photos 11-10): a brushed case with a stadium plan and slightly convex faces, lipped base and shoulder,
    stamped name; on top the round chimney with the snuffer cap on the left, the hinge block in the middle, the arm over both,
    and the knurled thumb button on its copper flint post at the right"""
    W, D, H = 0.040, 0.0145, 0.036
    decals = []
    if text:
        def draw(m, w, h, ppm):
            eg.text(m, 'RONSAN', w * .5, h * .34, h * .20, 'cinzel-latin-700-normal.woff')
            eg.text(m, 'Since 1912', w * .5, h * .56, h * .085, 'oswald-latin-400-normal.woff')
            eg.text(m, 'New Jersey', w * .5, h * .67, h * .085, 'oswald-latin-400-normal.woff')
        p = cut_decal('ronsan_txt', (30, 30), draw)
        decals.append(('cut', p, (-.015, .003, .030, .030), ('Y', -1)))
    mt = finish_mat('rb', fin, W, D, decals, round=fin[0] == 'chrome')
    prof = [(-.0012, 0), (-.0004, .0002), (.00045, .0008), (.00045, .0024), (0, .0030), (0, H - .0030), (.00035, H - .0024), (.00035, H - .0010), (-.0004, H - .0002), (-.0012, H)]
    body = stadium_prism('r_body', prof, W, D, bulge=.0007)
    body.data.materials.append(mt)
    fk = fin[1] if fin[0] in ('chrome', 'brushed') else (1, 1, 1)
    pm = finish_mat('rpol', ('chrome', fk), .01, .01, round=True)
    flat = finish_mat('rpf', ('chrome', fk), .012, .008)
    xl, xr = -0.0118, 0.0128
    chim = cyl('r_chim', .0042, H - .0005, H + .0068, pm, seg=40)
    chim.location = (xl, 0, 0)
    sn = lathe('r_snuff', [(0, H + .0062), (.0051, H + .0062), (.0053, H + .0080), (.0050, H + .0098), (.0040, H + .0106), (0, H + .0108)], seg=48)
    sn.data.materials.append(pm)
    sn.location = (xl, 0, 0)
    block = rbox('r_block', (0.0095, 0.0080, 0.0105), (0.0005, 0, H + .0050), .0008, flat)
    pin = cyl('r_pin', .0011, -.0042, .0042, mat_principled('rpin', (.25, .18, .08), .5), seg=20)
    pin.rotation_euler = (math.radians(90), 0, 0)
    pin.location = (0.0005, 0, H + .0075)
    arm = rbox('r_arm', (0.0150, 0.0062, 0.0024), (-0.0060, 0, H + .0104), .0009, flat)
    arm2 = rbox('r_arm2', (0.0130, 0.0062, 0.0024), (0.0068, 0, H + .0098), .0009, flat)
    post = cyl('r_post', .0021, H - .0005, H + .0085, finish_mat('rcu', ('chrome', COPPER), .01, .01, round=True), seg=24)
    post.location = (xr, 0, 0)
    btn = knurl('r_btn', .0056, .0030, (xr, 0, H + .0100), axis='Z', teeth=44, rgb=fk)
    top = cyl('r_btop', .0050, H + .0115, H + .0118, finish_mat('rbt', ('brushed', fk), .01, .01, [('cut', save(eg.decal((10, 10), lambda m, w, h, ppm: eg.guilloche(m, w, h, 9, 2)), 'ronsan_btn_cut'), (-.005, -.005, .010, .010), ('Z', 1))]), seg=40)
    top.location = (xr, 0, 0)
    return [body, chim, sn, block, pin, arm, arm2, post, top] + btn + knurl('r_wheel', .0030, .0040, (0.0068, 0, H + .0035))


def imco(fin, dotted=True):
    """after the IMCO Triplex Super (photos 11-10): the round fuel tube on the left with a knurled foot and a knurled grip band,
    the flat box on the right faced by the long lever plate stamped IMCO, the cap arm over the top with its round snuffer end"""
    H, D = 0.060, 0.0140
    R, xt = 0.0070, -0.0060
    tex = None
    if dotted:
        tex = save(eg.decal((40, 60), lambda m, w, h, ppm: [m.paste(255, (x, y, x + 4, y + 4)) for x in range(3, w, 9) for y in range(3, h, 9)], 14), 'imco_dots_cut')
    else:
        tex = save(eg.decal((40, 60), lambda m, w, h, ppm: [m.paste(255, (x, 0, x + 2, h)) for x in range(3, w, 8)], 14), 'imco_lines_cut')
    def mat(n, round=False, ex=()):
        return finish_mat(n, fin, .03, D, [('cut', tex, (-.020, 0, .040, .060), ('Y', -1)), ('cut', tex, (-.020, 0, .040, .060), ('Y', 1))] + list(ex), round=round)
    tube = cyl('i_tube', R, .0055, H - .0035, finish_mat('itube', fin, .01, .01, [('cut', save(eg.decal((14, 60), lambda m, w, h, ppm: [m.paste(255, (x, 0, x + 2, h)) for x in range(2, w, 6)], 14), 'imco_tube_cut'), (xt - .007, 0, .014, .060), ('Y', -1))], round=True), seg=56)
    tube.location = (xt, 0, 0)
    foot = knurl('i_foot', R + .0003, .0060, (xt, 0, .0034), axis='Z', teeth=40, rgb=fin[1])
    grip = knurl('i_grip', R + .0005, .0120, (xt, 0, H - .0170), axis='Z', teeth=48, rgb=fin[1])
    box = rbox('i_box', (0.0150, D * .86, H - .0050), (0.0060, 0, (H - .0050) / 2 + .0005), .0012, mat('ibox'))
    def lev(m, w, h, ppm):
        for i, ch in enumerate('IMKO'):
            eg.text(m, ch, w * .5, h * (.22 + .17 * i), h * .14, 'oswald-latin-700-normal.woff')
        from PIL import ImageDraw
        ImageDraw.Draw(m).ellipse([w * .4, h * .04, w * .6, h * .04 + w * .2], fill=255)
    pl = cut_decal('imco_plate', (9, 52), lev)
    plate = rbox('i_plate', (0.0090, 0.0018, H - .0080), (0.0030, -D / 2 + .0002, (H - .0080) / 2 + .0010), .0007, finish_mat('iplate', fin, .009, .002, [('cut', pl, (-.0015, .0010, .009, .052), ('Y', -1))]))
    rm = finish_mat('icap', fin, .01, .01, round=True)
    cap = cyl('i_cap', R * .98, -D * .42, D * .42, rm, seg=48)
    cap.rotation_euler = (math.radians(90), 0, 0)
    cap.location = (xt, 0, H - .0005)
    disc = cyl('i_disc', R * .55, -D * .43, D * .43, finish_mat('idisc', fin, .01, .01, round=True), seg=32)
    disc.rotation_euler = (math.radians(90), 0, 0)
    disc.location = (xt, 0, H - .0005)
    rib = save(eg.decal((12, 6), lambda m, w, h, ppm: [m.paste(255, (int(w * .55), y, w, y + 3)) for y in range(4, h, 9)], 16), 'imco_rib_cut')
    arm = rbox('i_arm', (0.0170, D * .84, 0.0060), (0.0060, 0, H - .0005), .0012, finish_mat('iarm', fin, .017, D, [('cut', rib, (.0000, H - .0035, .012, .006), ('Y', -1))]))
    return [tube, box, plate, cap, disc, arm] + foot + grip


def trench(fin, emblem=None):
    """WW1 cartridge lighter: a brass case with rolled bands, a bullet cap over the wick, a ring for the chain, the wheel in a bracket"""
    R, H = 0.0074, 0.052
    zc = H - 0.017  # where the cap begins
    decals = []
    if emblem:
        p = cut_decal('trench_%s' % emblem.__name__, (14, 22), emblem)
        decals.append(('cut', p, (-0.007, 0.007, 0.014, 0.022), ('Y', -1)))
    body = lathe('t_body', [(0, 0), (R * .9, 0), (R, .0007), (R, .0026), (R * .965, .0031), (R * .965, .0040), (R, .0045), (R, zc - .0015), (R * .975, zc - .001), (R * .975, zc), (0, zc)], seg=56)
    body.data.materials.append(finish_mat('tb', fin, 2 * R, 2 * R, decals, round=True))
    cap = lathe('t_cap', [(0, zc - .002), (R * 1.04, zc - .002), (R * 1.04, zc + .0045), (R * 1.0, zc + .007), (R * .86, zc + .0105), (R * .62, zc + .0135), (R * .34, zc + .0158), (0, zc + .0166)], seg=56)
    cm = finish_mat('tc', fin, 2 * R, 2 * R, round=True)
    cap.data.materials.append(cm)
    band = torus('t_band', R * 1.04, .00045, zc - .0015, cm)
    band2 = torus('t_band2', R * 1.0, .00045, .006, cm)
    eye = torus('t_eye', .0014, .00045, zc + .0175, cm, rot=(math.radians(90), 0, 0))
    ring = torus('t_ring', .0042, .0005, zc + .0222, cm, rot=(math.radians(90), 0, math.radians(70)))
    br = rbox('t_brk', (0.0034, 0.0068, 0.009), (R + .0010, 0, zc - .007), .0006, cm)
    return [body, cap, band, band2, eye, ring, br] + knurl('t_wheel', 0.0034, 0.0042, (R + .0034, 0, zc - .007))


def soviet(fin, emblem=None):
    """round flat «tablet» case: a rolled rim, the perforated chimney on top, the wheel and a little spring lever"""
    R, T = 0.020, 0.0085
    decals = []
    if emblem:
        p = cut_decal('sov_%s_%d' % (emblem.__name__, abs(hash(str(fin)))), (28, 28), emblem)
        decals.append(('cut', p, (-0.014, -0.014, 0.028, 0.028), ('Y', -1)))
    mt = finish_mat('sb', fin, 2 * R, T, decals)
    body = lathe('s_body', [(0, -T / 2), (R - .0012, -T / 2), (R - .0003, -T / 2 + .0005), (R, -T / 2 + .0013), (R, T / 2 - .0013), (R - .0003, T / 2 - .0005), (R - .0012, T / 2), (0, T / 2)], seg=72)
    body.rotation_euler = (math.radians(90), 0, 0)
    body.data.materials.append(mt)
    _apply(body, rot=True)
    body.location = (0, 0, R)
    rm = finish_mat('sr', fin, .01, .01, round=True)
    rims = [torus('s_rim%d' % i, R - .0014, .00055, R, rm, rot=(math.radians(90), 0, 0), loc_xy=(0, s * (T / 2 - .0001))) for i, s in enumerate((-1, 1))]
    holes = dots('sov_holes', (9, 7), 2, 3, .55)
    ch = rbox('s_chim', (0.009, 0.0062, 0.0085), (0.0035, 0, 2 * R + .0028), 0.0004, finish_mat('sc', fin, .009, .006, [('inlay', holes, (-.001, 2 * R + .0002, .009, .007), ('Y', -1))]))
    lev = rbox('s_lev', (0.012, 0.0050, 0.0016), (-0.002, 0, 2 * R + .0080), 0.0005, finish_mat('sl', fin, .01, .01))
    return [body, ch, lev] + rims + knurl('s_wheel', 0.0030, 0.0045, (-0.0060, 0, 2 * R + .0028))


def outline_prism(name, pts, D, bevel=.0010):
    """a flat case from a front outline (x, z points, counter-clockwise), depth D along Y, edges softened"""
    bm = bmesh.new()
    f = [bm.verts.new((x, -D / 2, z)) for x, z in pts]
    face = bm.faces.new(f)
    r = bmesh.ops.extrude_face_region(bm, geom=[face])
    vs = [e for e in r['geom'] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, verts=vs, vec=(0, D, 0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    mod = o.modifiers.new('bev', 'BEVEL')
    mod.width = bevel
    mod.segments = 5
    mod.limit_method = 'ANGLE'
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.shade_smooth()
    return o


def kol_outline(W, H, plate=False):
    """the Colibri Slim front outline (photos 11-10): the left side waisted, the right side nearly straight, all four corners
    flaring a little, the top dipping between its corners. plate=True: the soft panel inside the metal - a thin rim on the left,
    top and bottom, and on the right a metal strip wide at the top and the foot and narrow at the waist."""
    n = 28
    wl, wr = .0034, .0012
    def xl(u):
        return -W / 2 + wl * math.sin(math.pi * u) ** 1.1
    def xr(u):
        return W / 2 - wr * math.sin(math.pi * u) ** 1.1
    def strip(u):
        return .0022 + .0034 * (1 - math.sin(math.pi * u)) ** 1.6
    z0, z1 = .0004, H
    il, it, ib = (.0007, .0010, .0008) if plate else (0, 0, 0)
    pts = []
    for i in range(n + 1):  # right side, bottom to top
        u = i / n
        pts.append((xr(u) - (strip(u) if plate else 0), z0 + ib + (z1 - z0 - ib - it) * u))
    for i in range(1, n):  # top, right to left, dipping in the middle
        v = i / n
        x = (xr(1) - (strip(1) if plate else 0)) * (1 - v) + (xl(1) + il) * v
        pts.append((x, z1 - it - .0014 * math.sin(math.pi * v)))
    for i in range(n + 1):  # left side, top to bottom
        u = 1 - i / n
        pts.append((xl(u) + il, z0 + ib + (z1 - z0 - ib - it) * u))
    for i in range(1, n):  # bottom, left to right, slightly hollow
        v = i / n
        x = (xl(0) + il) * (1 - v) + (xr(0) - (strip(0) if plate else 0)) * v
        pts.append((x, z0 + ib + .0006 * math.sin(math.pi * v)))
    return pts


def kol_bird(size):
    """a faceted hummingbird facing left, in blues with dark seams, as RGBA"""
    from PIL import Image, ImageDraw
    im = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    P = lambda pts: [(x * size, y * size) for x, y in pts]
    parts = [
        ([(0.02, 0.37), (0.30, 0.34), (0.30, 0.38)], (40, 95, 205)),
        ([(0.28, 0.30), (0.38, 0.25), (0.45, 0.32), (0.40, 0.41), (0.30, 0.40)], (28, 70, 175)),
        ([(0.40, 0.33), (0.62, 0.40), (0.52, 0.58), (0.40, 0.42)], (45, 105, 215)),
        ([(0.62, 0.40), (0.70, 0.55), (0.52, 0.58)], (22, 58, 155)),
        ([(0.60, 0.52), (0.90, 0.80), (0.80, 0.85), (0.53, 0.59)], (35, 85, 195)),
        ([(0.44, 0.33), (0.68, 0.04), (0.64, 0.30)], (55, 120, 225)),
        ([(0.52, 0.36), (0.86, 0.12), (0.76, 0.37)], (30, 78, 185)),
        ([(0.58, 0.41), (0.98, 0.30), (0.82, 0.47)], (48, 110, 215)),
    ]
    for pts, c in parts:
        d.polygon(P(pts), fill=c + (255,), outline=(10, 22, 70, 255))
    d.ellipse(P([(0.355, 0.300), (0.385, 0.330)]), fill=(10, 20, 60, 255))
    return im


def kolibri(body_rgb, top=SILVER):
    """after the Colibri Slim jet (photos 11-10): a waisted metal frame, a soft-touch plate on each face inside a thin rim (wider at
    the right), two vent slots, the faceted blue hummingbird over the name, the refill valve under the foot, the jet slot on top right"""
    W, D, H = 0.036, 0.0112, 0.056
    frame = outline_prism('k_frame', kol_outline(W, H), D, .0008)
    frame.data.materials.append(finish_mat('kf', ('chrome', top), W, D))
    def art(dr, w, h, ppm):
        for k, (x0, x1) in enumerate(((.24, .62), (.26, .58))):
            y = h * (.15 + .04 * k)
            dr.rounded_rectangle([w * x0, y, w * x1, y + h * .016], radius=ppm * .3, fill=(6, 6, 8, 255))
        bird = kol_bird(int(w * .46))
        dr._image.alpha_composite(bird, (int(w * .20), int(h * .43)))
        f = eg.font('oswald-latin-700-normal.woff', int(h * .062))
        dr.text((w * .42, h * .81), 'KOLIBRI', font=f, fill=(200, 200, 205, 255), anchor='mm')
    p = save(eg.enamel((36, 56), art, 16), 'kol_art2_%d' % abs(hash(str(body_rgb))))
    plate = outline_prism('k_plate', kol_outline(W, H, plate=True), D + .0005, .0004)
    m = finish_mat('kb', ('plastic', body_rgb), W, D, [('inlay', p, (-W / 2, 0, W, H), ('Y', -1))])
    m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .8
    plate.data.materials.append(m)
    tm = finish_mat('kt', ('chrome', top), W, D)
    rm = finish_mat('ktr', ('chrome', top), .01, .01, round=True)
    # the jet in the middle of the top: a ring with the dark hole
    ztop = H - .0014
    ring = cyl('k_jetring', .0026, ztop - .0005, ztop + .0006, rm, seg=32)
    hole = cyl('k_jet', .0015, ztop + .0006, ztop + .0007, mat_principled('kh', (.015, .015, .02), .4), seg=24)
    # the ignition button on the right side, high up
    ub = .64
    xb = W / 2 - .0012 * math.sin(math.pi * ub) ** 1.1
    btn = rbox('k_btn', (0.0016, 0.0062, 0.0120), (xb + .0004, 0, .0004 + (H - .0004) * ub), .0007, tm)
    return [frame, plate, ring, hole, btn]


def burl(name):
    """burl wood (briar): swirling figure with dark eyes, glossy lacquer"""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Roughness'].default_value = .25
    tc = nt.nodes.new('ShaderNodeTexCoord')
    nz = nt.nodes.new('ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value = 70
    nz.inputs['Detail'].default_value = 10
    nz.inputs['Distortion'].default_value = 6
    nt.links.new(tc.outputs['Object'], nz.inputs['Vector'])
    vo = nt.nodes.new('ShaderNodeTexVoronoi')
    vo.inputs['Scale'].default_value = 260
    nt.links.new(tc.outputs['Object'], vo.inputs['Vector'])
    fig = _ramp(nt, nz.outputs['Fac'], [(0.0, (.10, .035, .012)), (.35, (.32, .12, .04)), (.5, (.55, .26, .10)), (.62, (.36, .14, .05)), (.8, (.16, .05, .02)), (1.0, (.45, .2, .08))])
    eyes = _ramp(nt, vo.outputs['Distance'], [(0.0, 1.0), (.12, .0), (1.0, 0.0)])
    col = _mix(nt, fig, (.05, .02, .01), _math(nt, 'MULTIPLY', eyes, .7))
    fine = nt.nodes.new('ShaderNodeTexNoise')
    fine.inputs['Scale'].default_value = 900
    nt.links.new(tc.outputs['Object'], fine.inputs['Vector'])
    col = _mix(nt, col, _ramp(nt, fine.outputs['Fac'], [(0, .7), (1, 1.15)]), 1.0, 'MULTIPLY')
    nt.links.new(col, b.inputs['Base Color'])
    return m


def pipe_l(metal, panel='scroll', ground=None, seed=3, frame=None):
    """after the Corona Old Boy pipe lighter (photos 11-10): a tall case with rounded corners, metal foot and shoulder bands,
    the panel between them engraved with scrolls (or set with burl wood); on top the lever bar with a knurled end, the ball joint,
    two short posts, the flint post with its knurled ring and the tall flame head on the right"""
    W, D, H = 0.029, 0.0150, 0.056
    zb, zt = .0065, H - .0060
    decals = []
    if panel == 'scroll':
        from PIL import Image
        import numpy as np
        import scrolls
        ppm = 18
        Wp, Hp = int(29 * ppm), int(56 * ppm)
        sm = scrolls.scroll_mask(Wp, Hp, seed)
        inv = Image.fromarray(255 - np.array(sm))
        e = eg.engraving(inv, depth=.55, hatch=False, outline=True)
        g = np.array(sm, np.float32)[..., None] / 255
        # the cut-away ground: dark patina (brass) or black paint (black and gold)
        e = e * g + (1 - g) * (.10 if ground else .48)
        # 11-10: the rounded ribs get plain polished metal (the front and side pictures met there in a dark seam)
        c = int(3.4 * ppm)
        e[:, :c] = 1.0
        e[:, -c:] = 1.0
        es = e[:, :int(15 * ppm)].copy()
        es[:, :c] = 1.0
        es[:, -c:] = 1.0
        p = save(eg.to_img(e), 'pipe_scroll2_%d_%d_cut' % (seed, ground is not None))
        side = save(eg.to_img(es), 'pipe_scroll_side2_%d_%d_cut' % (seed, ground is not None))
        decals = [('cut', p, (-W / 2, 0, W, H), ('Y', -1)), ('cut', p, (-W / 2, 0, W, H), ('Y', 1)),
                  ('cut', side, (-D / 2, 0, D, H), ('X', -1)), ('cut', side, (-D / 2, 0, D, H), ('X', 1))]
        pm = finish_mat('pp', ('brushed', metal), W, D, decals)
    else:
        pm = burl('pburl')
    panel_o = rrect_prism('p_panel', [(-.0003, zb), (-.0003, zt)], W, D, .0030)
    panel_o.data.materials.append(pm)
    fr = ('paint', frame, 0.0) if frame else ('chrome', metal)
    mm = finish_mat('pm', fr, W, D)
    rm = finish_mat('pr', fr, .01, .01, round=True)
    foot = rrect_prism('p_foot', [(-.0005, 0), (-.0001, .0002), (0, .0006), (0, zb - .0003), (-.0002, zb)], W, D, .0030)
    foot.data.materials.append(mm)
    sh = rrect_prism('p_shoulder', [(-.0002, zt), (0, zt + .0003), (0, H - .0005), (-.0003, H - .0001), (-.0008, H)], W, D, .0030)
    sh.data.materials.append(mm)
    seam = rrect_prism('p_seam', [(.00005, .0024), (.00005, .0028)], W, D, .0030)
    seam.data.materials.append(mat_principled('pse', (.03, .03, .03), .5))
    xr = W / 2 - .0055
    head = rbox('p_head', (0.0100, 0.0105, 0.0105), (xr, 0, H + .0110), .0016, mm)
    mouth = cyl('p_mouth', .0034, -.0001, .0001, mat_principled('pmo', (.02, .02, .02), .4), seg=24)
    mouth.rotation_euler = (0, math.radians(90), 0)
    mouth.location = (xr - .0051, 0, H + .0112)
    ring = knurl('p_ring', .0042, .0040, (xr, 0, H + .0036), axis='Z', teeth=30, rgb=frame or metal)
    post = cyl('p_fpost', .0030, H, H + .0060, rm, seg=24)
    post.location = (xr, 0, 0)
    bar = cyl('p_bar', .0019, -.0136, .0090, rm, seg=24)
    bar.rotation_euler = (0, math.radians(90), 0)
    bar.location = (0, 0, H + .0105)
    collar = cyl('p_collar', .0026, H + .0080, H + .0128, rm, seg=32)
    collar.location = (.0005, 0, 0)
    end = knurl('p_end', .0032, .0030, (-.0150, 0, H + .0105), axis='X', teeth=22, rgb=frame or metal)
    posts = []
    for i, x in enumerate((-.0075, .0005)):
        c = cyl('p_post%d' % i, .0022, H - .0002, H + .0085, rm, seg=24)
        c.location = (x, 0, 0)
        posts.append(c)
    plate = rbox('p_plate', (0.0060, 0.0060, 0.0016), (-.0080, 0, H + .0090), .0005, mm)
    return [panel_o, foot, sh, seam, head, mouth, post, bar, collar, plate] + ring + end + posts


def table(fin, foot=None):
    """table lighter: a stone urn on a gilt foot, a gilt collar and a domed lid carrying the wheel and chimney"""
    met = foot or ('chrome', GOLD)
    fm = finish_mat('tbf', met, .05, .05, round=True)
    f = lathe('tb_foot', [(0, 0), (0.023, 0), (0.0232, .0012), (0.022, .0026), (0.017, .0040), (0.0125, .0065), (0.0115, .0095), (0.0125, .0105), (0, .0105)], seg=72)
    f.data.materials.append(fm)
    st = [(0.0, .010), (0.0128, .010), (0.0175, .014), (0.0235, .021), (0.0272, .029), (0.0282, .036), (0.0270, .044), (0.0232, .052), (0.0178, .058), (0.0150, .061), (0.0, .061)]
    o = lathe('tb_urn', st, seg=96)
    o.data.materials.append(finish_mat('tbb', fin, .06, .06))
    col = lathe('tb_collar', [(0, .0602), (0.0158, .0602), (0.0162, .0618), (0.0156, .0632), (0.0130, .0640), (0.0105, .0662), (0.0068, .0680), (0.0, .0688)], seg=72)
    col.data.materials.append(fm)
    ring = torus('tb_ring', 0.0128, 0.0006, .0104, fm)
    ch = cyl('tb_chim', .0035, .0675, .0765, fm, seg=32)
    ch.location = (.0035, 0, 0)
    hole = cyl('tb_hole', .0026, .0765, .0766, mat_principled('tbh', (.02, .02, .02), .4), seg=24)
    hole.location = (.0035, 0, 0)
    return [o, f, col, ring, ch, hole] + knurl('tb_wheel', 0.0034, 0.004, (-0.0030, 0, .0725), rgb=(.9, .75, .45) if met[1] == GOLD else (.7, .7, .74))


def bik_logo(key):
    """parody logo: an orange oval with a black rim, «Bik» in heavy italic, a little round-headed boy at the left"""
    from PIL import Image, ImageDraw
    W, H = 400, 150
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([70, 22, 392, 128], radius=52, fill=(18, 14, 10, 255))
    d.rounded_rectangle([80, 32, 382, 118], radius=44, fill=(236, 140, 22, 255))
    f = eg.font('alfa-slab-one-latin-400-normal.woff', 84)
    txt = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(txt)
    td.text((236, 76), 'Bik', font=f, fill=(18, 14, 10, 255), anchor='mm')
    txt = txt.transform(txt.size, Image.AFFINE, (1, .22, -16, 0, 1, 0), resample=Image.BICUBIC)
    im.alpha_composite(txt)
    # the boy: round head, a stick body holding the pen-like lighter
    d.ellipse([12, 8, 52, 48], fill=(18, 14, 10, 255))
    d.ellipse([20, 16, 44, 40], fill=(250, 250, 250, 255))
    d.polygon([(22, 50), (44, 50), (48, 104), (18, 104)], fill=(18, 14, 10, 255))
    d.line([(40, 62), (66, 48)], fill=(18, 14, 10, 255), width=12)
    d.line([(26, 102), (16, 142)], fill=(18, 14, 10, 255), width=12)
    d.line([(40, 102), (50, 142)], fill=(18, 14, 10, 255), width=12)
    return save(im, key)


def oval_shell(name, a, b, a0, a1, z0, z1, t, seg=40):
    """part of an elliptic sleeve (semi-axes a, b) from angle a0 to a1 (degrees), wall t, open at the top and bottom"""
    bm = bmesh.new()
    rings = []
    for z in (z0, z1):
        for off in (0, -t):
            rings.append([bm.verts.new(((a + off) * math.cos(math.radians(a0 + (a1 - a0) * i / seg)), (b + off) * math.sin(math.radians(a0 + (a1 - a0) * i / seg)), z)) for i in range(seg + 1)])
    ob, ib, ot, it = rings
    for i in range(seg):
        bm.faces.new((ob[i], ob[i + 1], ot[i + 1], ot[i]))
        bm.faces.new((ib[i + 1], ib[i], it[i], it[i + 1]))
        bm.faces.new((ot[i], ot[i + 1], it[i + 1], it[i]))
        bm.faces.new((ob[i + 1], ob[i], ib[i], ib[i + 1]))
    for r0, r1 in ((ob, ib),):
        bm.faces.new((ob[0], ot[0], it[0], ib[0]))
        bm.faces.new((ob[-1], ib[-1], it[-1], ot[-1]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def bik(rgb):
    """disposable, after the classic slim pocket one (photos 11-10): a tall flat-oval glossy body; at the top the stamped chrome hood
    wraps the left half of the oval (open on top, a small window in its front), the dark double striker wheel lies across the top
    and the red oval thumb tab sits on the right end"""
    W, D, Hb = 0.0250, 0.0138, 0.0655
    a, b = W / 2, D / 2
    logo = bik_logo('bik_logo')
    lw, lh = .0135, .0051
    body = lathe('b_body', [(0, .0006), (0.0118, .0006), (0.0124, .0014), (0.0125, .0026), (0.0125, Hb), (0.0119, Hb), (0.0119, Hb - .0015), (0, Hb - .0015)], seg=72)
    body.scale = (1.0, D / W, 1.0)
    _apply(body, scale=True)
    body.data.materials.append(finish_mat('bb', ('plastic', rgb), W, D, [('inlay', logo, (-.0105, Hb - .0140, lw, lh), ('Y', -1))]))
    foot = lathe('b_foot', [(0, 0), (0.0116, 0), (0.0121, .0003), (0.0121, .0009), (0, .0009)], seg=72)
    foot.scale = (1.0, D / W, 1.0)
    _apply(foot, scale=True)
    foot.data.materials.append(finish_mat('bf', ('plastic', (.82, .82, .80)), W, D))
    inner = lathe('b_inner', [(0, Hb - .0012), (0.0118, Hb - .0012), (0.0118, Hb - .0006), (0, Hb - .0006)], seg=72)
    inner.scale = (1.0, D / W, 1.0)
    _apply(inner, scale=True)
    inner.data.materials.append(mat_principled('bin', (.03, .03, .035), .5))
    # the hood: chrome sleeve round the left half, a little window stamped in its front
    win = eg.enamel((14, 12), lambda d, w, h, ppm: (d.rectangle([w * .50, h * .52, w * .64, h * .72], fill=(10, 10, 12, 255)), d.rectangle([w * .50, h * .66, w * .74, h * .72], fill=(10, 10, 12, 255))), 20)
    pw = save(win, 'bik_win')
    hood = oval_shell('b_hood', a + .0004, b + .0004, 76, 284, Hb - .0040, Hb + .0082, .0005)
    hood.data.materials.append(finish_mat('bh', ('chrome', (1, 1, 1)), W, D, [('inlay', pw, (-.0110, Hb - .0040, .014, .012), ('Y', -1))], round=True))
    # the double striker wheel, dark steel, lying across
    wheels = []
    for k, y in enumerate((-.0024, .0024)):
        wheels += knurl('b_wheel%d' % k, 0.0044, 0.0026, (0.0010, y, Hb + .0060), teeth=64, rgb=(.30, .30, .33), depth=.93)
    # the red tab: an oval plate on a short stem at the right end
    red = finish_mat('br', ('plastic', (.72, .04, .05)), .01, .01)
    tab = cyl('b_tab', .0058, -.0017, .0017, red, seg=48)
    tab.scale = (1.0, 1.12, 1.0)
    tab.rotation_euler = (0, math.radians(8), 0)
    tab.location = (0.0070, 0, Hb + .0056)
    stem = rbox('b_stem', (0.0048, 0.0060, 0.0052), (0.0068, 0, Hb + .0018), .0006, red)
    return [body, foot, inner, hood, tab, stem] + wheels


# ------------------------------------------------------------------ refills
def _label_font(name, size):
    return eg.font(name, int(size))


def butane_label(key, W=1300, H=900):
    """wrap-around label of the butane can: navy with a big blue jet flame, the brand and «premium butane»; u=0.5 is the front"""
    from PIL import Image, ImageDraw, ImageFilter
    im = Image.new('RGB', (W, H), (12, 22, 58))
    d = ImageDraw.Draw(im)
    # metal bands top and bottom of the can (painted, the game shows no metal)
    for y0, y1 in ((0, int(H * .05)), (int(H * .90), H)):
        for y in range(y0, y1):
            t = (y - y0) / max(1, y1 - y0)
            v = int(150 + 90 * math.sin(math.pi * t))
            d.line([(0, y), (W, y)], fill=(v, v, v + 6))
    cx = W // 2
    # flame: layered tear drops
    fl = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(fl)
    for k, (s, c) in enumerate(((1.0, (30, 90, 230, 255)), (.72, (90, 170, 255, 255)), (.42, (220, 240, 255, 255)))):
        h = H * .42 * s
        w = W * .07 * s
        base = H * .52
        pts = [(cx + w * math.sin(math.pi * t) * (1 - t) ** .3 * (1 if i < 30 else -1), base - h * t) for i, t in enumerate([j / 29 for j in range(30)] + [1 - j / 29 for j in range(30)])]
        fd.polygon(pts, fill=c)
    fl = fl.filter(ImageFilter.GaussianBlur(3))
    im.paste(fl, (0, 0), fl)
    f1 = _label_font('oswald-latin-700-normal.woff', H * .11)
    f2 = _label_font('oswald-latin-400-normal.woff', H * .05)
    d.text((cx, H * .63), 'KOLIBRI', font=f1, fill=(240, 240, 245), anchor='mm')
    d.text((cx, H * .73), 'PREMIUM  BUTANE', font=f2, fill=(120, 180, 255), anchor='mm')
    d.text((cx, H * .80), 'ULTRA REFINED  ·  300 ml', font=_label_font('oswald-latin-400-normal.woff', H * .032), fill=(170, 180, 200), anchor='mm')
    # thin gold lines under and over the brand block
    for y in (H * .565, H * .845):
        d.line([(cx - W * .14, y), (cx + W * .14, y)], fill=(200, 160, 70), width=3)
    # warnings on the back half
    small = _label_font('oswald-latin-400-normal.woff', H * .022)
    # 11-10: no writing on the back at all (user)
    return save(im, key)


def butane_can():
    """a lighter refill can: a tall steel aerosol with the wrap label, a domed shoulder, the white nozzle stem and its collar"""
    R, H = .0235, .150
    lab = butane_label('butane_label')
    prof = [(0, .0030), (R * .70, .0012), (R * .93, 0), (R, .0030), (R, H - .020), (R * .96, H - .015), (R * .78, H - .008), (R * .52, H - .004), (R * .50, H - .001), (R * .44, H), (0, H)]
    body = lathe('bu_body', prof, seg=72, v_range=(0, H))
    m = mat_principled('bu_label', img=lab, rough=.35)
    body.data.materials.append(m)
    collar = cyl('bu_collar', .0062, H - .0005, H + .0030, finish_mat('buc', ('chrome', (1, 1, 1)), .01, .01, round=True), seg=40)
    stem = cyl('bu_stem', .0032, H + .0030, H + .0135, finish_mat('bus', ('plastic', (.92, .92, .9)), .01, .01), seg=32)
    tip = cyl('bu_tip', .0012, H + .0135, H + .0170, finish_mat('but', ('plastic', (.92, .92, .9)), .01, .01), seg=20)
    # 11-10: the snap-on overcap over the shoulder and the nozzle, glossy blue plastic like the label
    cr, c0, c1 = R * 1.015, H - .021, H + .022
    cprof = [(cr * .97, c0), (cr, c0 + .0015), (cr, c1 - .0040), (cr * .96, c1 - .0012), (cr * .86, c1), (0, c1)]
    cap = lathe('bu_cap', cprof, seg=72)
    cap.data.materials.append(finish_mat('bucap', ('plastic', (.10, .22, .62)), .01, .01))
    ring = cyl('bu_capring', cr * 1.004, c0 + .0030, c0 + .0042, finish_mat('bucr', ('plastic', (.06, .14, .42)), .01, .01), seg=72, cap=False)
    return [body, collar, cap, ring]


def fluid_label(key, W=720, H=980):
    """front of the lighter fluid tin: white enamel, the blue band with the brand, the red flame, the contents"""
    from PIL import Image, ImageDraw
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W, H], fill=(236, 236, 230, 255))
    d.rectangle([0, int(H * .30), W, int(H * .56)], fill=(20, 60, 150, 255))
    d.rectangle([0, int(H * .29), W, int(H * .30)], fill=(200, 30, 30, 255))
    d.rectangle([0, int(H * .56), W, int(H * .57)], fill=(200, 30, 30, 255))
    f1 = _label_font('alfa-slab-one-latin-400-normal.woff', H * .10)
    f2 = _label_font('oswald-latin-700-normal.woff', H * .055)
    f3 = _label_font('oswald-latin-400-normal.woff', H * .030)
    d.text((W / 2, H * .40), 'ZAPPO', font=f1, fill=(250, 250, 250, 255), anchor='mm')
    d.text((W / 2, H * .505), 'PREMIUM LIGHTER FLUID', font=f2, fill=(255, 210, 60, 255), anchor='mm')
    # red flame drawn as layered drops
    cx, by = W / 2, H * .25
    for s, c in ((1.0, (200, 30, 30, 255)), (.6, (250, 150, 40, 255)), (.3, (255, 230, 120, 255))):
        h, w = H * .18 * s, W * .10 * s
        pts = [(cx + w * math.sin(math.pi * t) * (1 - t) ** .35, by - h * t) for t in [j / 24 for j in range(25)]]
        pts += [(cx - w * math.sin(math.pi * t) * (1 - t) ** .35, by - h * t) for t in [1 - j / 24 for j in range(25)]]
        d.polygon(pts, fill=c)
    d.text((W / 2, H * .66), 'For all windproof lighters', font=f3, fill=(40, 40, 50, 255), anchor='mm')
    d.text((W / 2, H * .71), 'Clean burning  ·  Quick lighting', font=f3, fill=(40, 40, 50, 255), anchor='mm')
    d.text((W / 2, H * .86), '4 FL OZ  (118 ml)', font=f2, fill=(20, 60, 150, 255), anchor='mm')
    return save(im, key)


def fluid_tin():
    """lighter fluid: a flat steel tin with rounded corners and a domed top, the white enamel wrap with the blue band,
    the red snap cap on the spout at one side of the top"""
    W, D, H = .052, .026, .118
    lab = fluid_label('fluid_label')
    prof = [(-.0008, 0), (-.0002, .0004), (0, .0012), (0, H - .010), (-.0008, H - .006), (-.0030, H - .003), (-.0062, H - .0008), (-.0080, H)]
    tin = rrect_prism('fl_tin', prof, W, D, .0060)
    m = finish_mat('fl', ('paint', (.92, .92, .90), .0), W, D, [('inlay', lab, (-W / 2 + .002, .006, W - .004, H - .022), ('Y', -1))])
    tin.data.materials.append(m)
    rim = rrect_prism('fl_rim', [(.0004, .0004), (.0004, .0018)], W, D, .0060)
    rim.data.materials.append(finish_mat('flr', ('chrome', (1, 1, 1)), W, D))
    red = finish_mat('flc', ('plastic', (.72, .05, .04)), .01, .01)
    # 11-10: a ribbed screw collar on the spout and the tall pointed pipette cap over it
    collar = knurl('fl_collar', .0052, .0060, (.012, 0, H + .0020), axis='Z', teeth=28, rgb=(.72, .05, .04), depth=.90)[0]
    collar.data.materials.clear(); collar.data.materials.append(red)
    pprof = [(.0047, H + .0050), (.0047, H + .0080), (.0040, H + .0110), (.0022, H + .0200), (.0014, H + .0255), (.0008, H + .0268), (0, H + .0270)]
    pip = lathe('fl_pipette', pprof, seg=40)
    pip.data.materials.append(red)
    pip.location = (.012, 0, 0)
    return [tin, rim, collar, pip]
