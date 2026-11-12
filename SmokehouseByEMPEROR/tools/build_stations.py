"""Game meshes + baked textures for the canning kettle, the sausage table, the cans and the sausages.
Look = blender/stations2.py (approved previews). python3 build_stations.py [Kettle,Table,Can,Saus]"""
import bpy, bmesh, math, random, sys, os, struct
import numpy as np
from PIL import Image, ImageDraw
R = '/mnt/project-files/smokehouse/'
MOD = R + 'mod/SmokehouseByEMPEROR/'
only = sys.argv[1].split(',') if len(sys.argv) > 1 else ['Kettle', 'Table', 'Can', 'Saus']
src = open(R + 'blender/stations2.py').read()
sys.argv = ['x', '--', '/tmp/none', 'none']
iA = src.index('# ---------- environment'); iB = src.index('# ================= STATIONS v2')
iK = src.index('# ---------- 1. CANNING KETTLE'); iT = src.index('# ---------- 2. SAUSAGE TABLE'); iL = src.index('# ---------- light & render')
exec(src[:iA]); exec(src[iB:iK])
sc.world = bpy.data.worlds.new('w')

def wipe():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)

def target(image):
    for m in bpy.data.materials:
        if not m.use_nodes: continue
        n = m.node_tree.nodes.get('BAKE') or m.node_tree.nodes.new('ShaderNodeTexImage'); n.name = 'BAKE'; n.image = image
        for x in m.node_tree.nodes: x.select = False
        n.select = True; m.node_tree.nodes.active = n

def flatten_metal():
    for m in bpy.data.materials:
        if not m.use_nodes: continue
        b = m.node_tree.nodes.get('Principled BSDF')
        if b is None: continue
        for l in list(m.node_tree.links):
            if l.to_socket == b.inputs['Metallic'] or l.to_socket == b.inputs['Normal']: m.node_tree.links.remove(l)
        b.inputs['Metallic'].default_value = 0

def painterly(col):
    """A light wash only. The grain the owner disliked in the game was the source item's noise overlay (_DetailOverlay), taken off in 0.4.9;
    the heavy blur of 0.4.6 then left the table bare and plastic, so the baked wood and meat detail is kept and only the finest speckle is eased."""
    from PIL import ImageFilter
    im = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8))
    s = im.size[0] / 1024.0
    im = im.filter(ImageFilter.GaussianBlur(0.45 * s))
    return np.asarray(im).astype(np.float32) / 255.0

def finish(name, SZ=1024, curve_u=4, curve_b=2, variants=None, gain=1.25, margin=0.01, uvfix=None, paint=False, boost=None, bmargin=None, cube=()):
    """Everything in the scene becomes one mesh `name` with one texture. variants: {texname: (setup(), [part names to re-bake])}."""
    for o in list(bpy.data.objects):
        if o.type not in ('MESH', 'CURVE'): bpy.data.objects.remove(o, do_unlink=True)
    for cu in bpy.data.curves:
        cu.resolution_u = min(cu.resolution_u, curve_u); cu.bevel_resolution = min(cu.bevel_resolution, curve_b)
    flatten_metal()
    parts = list(bpy.data.objects)
    bpy.context.view_layer.update()
    for o in bpy.data.objects: o.select_set(False)
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.convert(target='MESH')
    for o in parts:
        if o.data.users > 1: o.data = o.data.copy()
        for l in list(o.data.uv_layers):
            if l.name != 'lab': o.data.uv_layers.remove(l)
        u = o.data.uv_layers.new(name='UV'); o.data.uv_layers.active = o.data.uv_layers['UV']
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=margin, scale_to_bounds=False)
    if cube:   # knobbly stones: the smart projection cut them into hundreds of crumbs a few texels each, which shimmered in the game (11-12 owner). Six sides each instead
        bpy.ops.object.mode_set(mode='OBJECT')
        for o in parts:
            if o.name.split('.')[0] not in cube: continue
            me = o.data; uvd = me.uv_layers['UV'].data
            for p in me.polygons:
                n = p.center; ax = max(range(3), key=lambda i: abs(n[i])); side = ax * 2 + (1 if n[ax] > 0 else 0)
                for li in p.loop_indices:
                    c = me.vertices[me.loops[li].vertex_index].co; q = [c[i] for i in range(3) if i != ax]
                    uvd[li].uv = (q[0] + side * 10.0, q[1])
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT'); bpy.ops.uv.average_islands_scale()
    if boost:   # 11-12 owner: the food on the table looked like plastic. Its parts get more of the picture than the boards do
        bpy.ops.object.mode_set(mode='OBJECT')
        for o in parts:
            k = boost.get(o.name.split('.')[0])
            if k:
                for d in o.data.uv_layers['UV'].data: d.uv = (d.uv[0] * k, d.uv[1] * k)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')
    if uvfix: uvfix(parts)
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 12; sc.render.bake.margin = bmargin or (20 if paint else 8); sc.cycles.use_denoising = False
    def bake_parts(image, which):
        target(image)
        for o in which:
            for x in bpy.data.objects: x.select_set(False)
            o.select_set(True); bpy.context.view_layer.objects.active = o
            bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_clear=False)
    img = bpy.data.images.new('bake_' + name, SZ, SZ)
    bake_parts(img, parts)
    base = np.array(img.pixels[:])
    cols = {}
    if variants:
        for vn, (setup, names) in variants.items():
            setup()
            im2 = bpy.data.images.new('bake_' + vn, SZ, SZ); im2.pixels = base.tolist()
            bake_parts(im2, [o for o in parts if o.name.split('.')[0] in names])
            cols[vn] = np.array(im2.pixels[:]).reshape(SZ, SZ, 4)
    else:
        cols[name] = base.reshape(SZ, SZ, 4)
    for x in bpy.data.objects: x.select_set(False)
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join(); ob = bpy.context.view_layer.objects.active
    ob.data.uv_layers.active = ob.data.uv_layers['UV']
    ao = bpy.data.images.new('ao_' + name, SZ, SZ); target(ao); sc.cycles.samples = 48
    bpy.ops.object.bake(type='AO', use_clear=True)
    O = np.array(ao.pixels[:]).reshape(SZ, SZ, 4)[:, :, 0]
    for vn, A in cols.items():
        col = A[:, :, :3] * (0.5 + 0.5 * O[:, :, None])
        col = np.clip(col * gain, 0, 1)
        if paint: col = painterly(col)
        Image.fromarray((col[::-1] * 255).astype(np.uint8)).save(MOD + 'textures/' + vn + '.png', optimize=True)
    me = ob.data; bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
    uvl = me.uv_layers['UV'].data; M = ob.matrix_world; M3 = M.to_3x3()
    verts, norms, uvs, tris, seen = [], [], [], [], {}
    for p in me.polygons:
        tri = []
        for li in p.loop_indices:
            lp = me.loops[li]; co = M @ me.vertices[lp.vertex_index].co; no = (M3 @ lp.normal).normalized(); uv = uvl[li].uv
            key = (lp.vertex_index, round(no.x, 2), round(no.y, 2), round(no.z, 2), round(uv.x, 4), round(uv.y, 4))
            if key not in seen:
                seen[key] = len(verts); verts.append(tuple(co)); norms.append(tuple(no)); uvs.append(tuple(uv))
            tri.append(seen[key])
        tris.append(tri)
    v, n, uv, t = np.array(verts, np.float32), np.array(norms, np.float32), np.array(uvs, np.float32), np.array(tris)
    U = np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1); Nn = np.stack([-n[:, 0], n[:, 2], -n[:, 1]], 1); tt = t[:, [0, 2, 1]].reshape(-1)
    for vn in cols:
        np.savez(R + 'bake/' + vn + '.npz', verts=v, normals=n, uvs=uv, tris=t)
        with open(MOD + 'meshes/' + vn + '.fpmesh', 'wb') as f:
            f.write(struct.pack('<ii', len(U), len(tt))); f.write(U.astype('<f4').tobytes()); f.write(Nn.astype('<f4').tobytes()); f.write(uv.astype('<f4').tobytes()); f.write(tt.astype('<i4').tobytes())
    print('BUILT', name, list(cols), len(U), 'verts', len(tt) // 3, 'tris, unity size', np.round(U.max(0) - U.min(0), 3), 'min', np.round(U.min(0), 3), flush=True)
    wipe()

# ---------------- kettle
if 'KettleOld' in only:
    wipe(); random.seed(7)
    exec(src[iK:iT])
    for o in list(bpy.data.objects):
        nm = o.name.split('.')[0]
        if nm in ('can', 'canb', 'lab', 'ink', 'tw', 'fire', 'ash', 'ks', 'band'): bpy.data.objects.remove(o, do_unlink=True)   # 11-12 owner: the flat ash stones hung in the air, the big ones were blobs
        elif nm == 'emb': o.data.materials[0] = M_char
    finish('Kettle', 1024)

# ---------------- table
if 'TableOld' in only:
    wipe(); random.seed(7)
    exec(src[iT:iL])
    for o in bpy.data.objects:
        if o.parent is None: o.location.x -= TX
    finish('Table', 2048, curve_u=3, curve_b=1, margin=0.004)

# ---------------- cans: one mesh, a label per kind
PAPER, COAL = (0.70, 0.62, 0.46), (0.035, 0.033, 0.035)
KINDS = {k: (PAPER, COAL, v) for k, v in {'CanDeer': 'deer', 'CanMoose': 'moose', 'CanBear': 'bear', 'CanFish': 'fish', 'CanPate': 'hare',
    # 11-12 owner: more kinds for the kettle. A can of several meats carries the drawing of each
    'CanWolf': 'wolf', 'CanCougar': 'cougar', 'CanSalmon': 'salmon', 'CanSea': 'seafish',
    'CanHunter': ('deer', 'moose'), 'CanStew': ('deer', 'hare'), 'CanBeast': ('bear', 'wolf'), 'CanAngler': ('fish', 'hare'),
    'CanGreat': ('deer', 'moose', 'bear'), 'CanFishMix': 'shoal'}.items()}
if os.environ.get('CANS'): KINDS = {k: v for k, v in KINDS.items() if k in os.environ['CANS'].split(',')}
def label_image(paper, ink, pic, W=832, H=220):
    """A hand sketch in charcoal on plain paper: wobbly outline, hatched inside, paper tooth and a smudge."""
    SS = 2; w, h = W * SS, H * SS
    im = Image.new('L', (w, h), 0); d = ImageDraw.Draw(im); k = 255; pale = 0
    pics = pic if isinstance(pic, tuple) else (pic, pic)
    if pics[0] == pics[-1] and len(pics) == 2: spots = [(w // 4, 78), (3 * w // 4, 78)]
    else:
        # several meats: the drawings stand side by side, the group twice round the can so that one look shows them all
        n = len(pics); gap, sz = (0.17, 60) if n == 2 else (0.125, 44)
        spots = [(int(w * (c + (i - (n - 1) / 2) * gap)), sz) for c in (0.25, 0.75) for i in range(n)]
        pics = pics * 2
    for (cx, sz), pic in zip(spots, pics):
        cy = h // 2 + 7 * SS; s = sz * SS
        P = lambda pts: [(cx + x * s, cy - y * s) for x, y in pts]
        E = lambda x, y, rx, ry: d.ellipse([cx + (x - rx) * s, cy - (y + ry) * s, cx + (x + rx) * s, cy - (y - ry) * s], fill=k)
        Ln = lambda pts, w=7: d.line(P(pts), fill=k, width=int(w * SS * 1.6), joint='curve')
        if pic == 'fish':
            E(-0.15, 0, 0.75, 0.36); d.polygon(P([(0.45, 0), (1.05, 0.5), (0.9, 0), (1.05, -0.5)]), fill=k)
            d.polygon(P([(-0.3, 0.3), (0.1, 0.62), (0.25, 0.25)]), fill=k)
            d.ellipse([cx - 0.62 * s - 5, cy - 0.08 * s - 5, cx - 0.62 * s + 5, cy - 0.08 * s + 5], fill=pale)
        elif pic == 'bear':
            E(0, -0.3, 0.55, 0.45)
            for x, y in ((-0.62, 0.32), (-0.22, 0.62), (0.22, 0.62), (0.62, 0.32)): E(x, y, 0.19, 0.24)
        elif pic == 'deer':
            d.polygon(P([(-0.3, 0.05), (0.3, 0.05), (0.14, -0.85), (-0.14, -0.85)]), fill=k)
            d.polygon(P([(-0.3, 0.05), (-0.75, 0.2), (-0.35, -0.15)]), fill=k); d.polygon(P([(0.3, 0.05), (0.75, 0.2), (0.35, -0.15)]), fill=k)
            for sg in (-1, 1):
                Ln([(sg * 0.15, 0.0), (sg * 0.35, 0.45), (sg * 0.8, 0.85)]); Ln([(sg * 0.35, 0.45), (sg * 0.25, 0.9)]); Ln([(sg * 0.58, 0.66), (sg * 0.95, 0.55)])
        elif pic == 'moose':
            d.polygon(P([(-0.26, 0.15), (0.26, 0.15), (0.24, -0.6), (0.0, -0.9), (-0.24, -0.6)]), fill=k)
            for sg in (-1, 1):
                d.polygon(P([(sg * 0.2, 0.1), (sg * 0.45, 0.25), (sg * 0.5, 0.9), (sg * 0.66, 0.5), (sg * 0.8, 0.88), (sg * 0.92, 0.45), (sg * 1.2, 0.7), (sg * 1.15, 0.15), (sg * 0.75, -0.05), (sg * 0.3, -0.02)]), fill=k)
        elif pic == 'wolf':
            d.polygon(P([(-0.5, 0.15), (-0.66, 0.95), (-0.2, 0.5), (0.2, 0.5), (0.66, 0.95), (0.5, 0.15), (0.2, -0.8), (-0.2, -0.8)]), fill=k)
            for sg in (-1, 1): d.polygon(P([(sg * 0.12, 0.12), (sg * 0.36, 0.26), (sg * 0.3, 0.06)]), fill=pale)
            d.polygon(P([(-0.1, -0.6), (0.1, -0.6), (0, -0.74)]), fill=pale)
        elif pic == 'cougar':
            # a cat's face: pointed ears, slanted eyes, a nose and long whiskers
            E(0, -0.15, 0.62, 0.52)
            for sg in (-1, 1):
                d.polygon(P([(sg * 0.2, 0.25), (sg * 0.58, 0.95), (sg * 0.66, 0.1)]), fill=k)
                d.polygon(P([(sg * 0.14, -0.02), (sg * 0.42, 0.12), (sg * 0.4, -0.06)]), fill=pale)
                Ln([(sg * 0.3, -0.36), (sg * 1.1, -0.22)], 3); Ln([(sg * 0.3, -0.44), (sg * 1.08, -0.56)], 3)
            d.polygon(P([(-0.1, -0.26), (0.1, -0.26), (0, -0.4)]), fill=pale)
        elif pic == 'salmon':
            # a salmon leaping out of the water
            ca, sa = math.cos(math.radians(38)), math.sin(math.radians(38))
            Rt = lambda pts: P([(x * ca - y * sa, x * sa + y * ca + 0.12) for x, y in pts])
            d.polygon(Rt([(0.78 * math.cos(a) + 0.05, 0.27 * math.sin(a)) for a in [i * 2 * math.pi / 28 for i in range(28)]]), fill=k)
            d.polygon(Rt([(-0.6, 0.0), (-1.12, 0.42), (-0.92, 0.0), (-1.12, -0.42)]), fill=k)
            d.polygon(Rt([(-0.15, 0.22), (0.1, 0.52), (0.3, 0.2)]), fill=k); d.polygon(Rt([(-0.1, -0.22), (0.05, -0.45), (0.22, -0.2)]), fill=k)
            ex, ey = 0.58 * ca - 0.06 * sa, 0.58 * sa + 0.06 * ca + 0.12
            d.ellipse([cx + ex * s - 6, cy - ey * s - 6, cx + ex * s + 6, cy - ey * s + 6], fill=pale)
            Ln([(-1.05 + i * 0.21, -0.82 + 0.08 * math.sin(i * 1.9)) for i in range(11)], 4)
            for x, y in ((-0.2, -0.62), (0.15, -0.55), (0.5, -0.64)): E(x, y, 0.045, 0.07)
        elif pic == 'shoal':
            # three fish one above another, the middle one swims the other way: a medley
            for y0, sg, fs in ((0.6, 1, 0.52), (0.0, -1, 0.6), (-0.6, 1, 0.52)):
                Q = lambda pts: P([(sg * x * fs, y0 + y * fs) for x, y in pts])
                d.polygon(Q([(-0.15 + 0.75 * math.cos(t), 0.36 * math.sin(t)) for t in [i * 2 * math.pi / 24 for i in range(24)]]), fill=k)
                d.polygon(Q([(0.45, 0), (1.05, 0.5), (0.9, 0), (1.05, -0.5)]), fill=k)
                d.polygon(Q([(-0.3, 0.3), (0.1, 0.62), (0.25, 0.25)]), fill=k)
                ex = sg * -0.62 * fs; ey = y0 + 0.06 * fs
                d.ellipse([cx + ex * s - 5, cy - ey * s - 5, cx + ex * s + 5, cy - ey * s + 5], fill=pale)
        elif pic == 'fishlong':
            E(-0.1, 0.0, 0.86, 0.2); d.polygon(P([(0.6, 0.0), (1.12, 0.4), (0.9, 0.0), (1.12, -0.4)]), fill=k)
            d.polygon(P([(0.2, 0.15), (0.38, 0.42), (0.5, 0.12)]), fill=k); d.polygon(P([(0.2, -0.15), (0.38, -0.4), (0.5, -0.12)]), fill=k)
            d.polygon(P([(-0.96, 0.0), (-1.15, -0.03), (-0.9, -0.12)]), fill=k)
            d.ellipse([cx - 0.72 * s - 5, cy - 0.04 * s - 5, cx - 0.72 * s + 5, cy - 0.04 * s + 5], fill=pale)
        elif pic == 'fishround':
            E(-0.12, -0.05, 0.62, 0.45); d.polygon(P([(0.4, -0.05), (0.95, 0.45), (1.05, -0.05), (0.95, -0.55)]), fill=k)
            for i in range(6): d.polygon(P([(-0.5 + i * 0.16, 0.3), (-0.4 + i * 0.16, 0.85 - 0.05 * abs(i - 2)), (-0.32 + i * 0.16, 0.3)]), fill=k)
            d.polygon(P([(-0.2, -0.4), (0.0, -0.75), (0.15, -0.4)]), fill=k)
            d.ellipse([cx - 0.5 * s - 9, cy - 0.1 * s - 9, cx - 0.5 * s + 9, cy - 0.1 * s + 9], fill=pale)
        elif pic == 'seafish':
            # a fish and an anchor: fish of the sea
            fx, fs = -0.42, 0.62
            d.ellipse([cx + (fx - 0.15 * fs - 0.75 * fs) * s, cy - 0.36 * fs * s - 0.1 * s, cx + (fx - 0.15 * fs + 0.75 * fs) * s, cy + 0.36 * fs * s - 0.1 * s], fill=k)
            d.polygon(P([(fx + 0.45 * fs, 0.1), (fx + 1.05 * fs, 0.1 + 0.5 * fs), (fx + 0.9 * fs, 0.1), (fx + 1.05 * fs, 0.1 - 0.5 * fs)]), fill=k)
            d.polygon(P([(fx - 0.3 * fs, 0.1 + 0.3 * fs), (fx + 0.1 * fs, 0.1 + 0.62 * fs), (fx + 0.25 * fs, 0.1 + 0.25 * fs)]), fill=k)
            d.ellipse([cx + (fx - 0.62 * fs) * s - 5, cy - 0.15 * s - 5, cx + (fx - 0.62 * fs) * s + 5, cy - 0.15 * s + 5], fill=pale)
            ax = 0.72
            Ln([(ax, 0.55), (ax, -0.7)], 6); Ln([(ax - 0.22, 0.32), (ax + 0.22, 0.32)], 5)
            Ln([(ax - 0.42, -0.3), (ax - 0.3, -0.58), (ax, -0.72), (ax + 0.3, -0.58), (ax + 0.42, -0.3)], 6)
            for sg in (-1, 1): d.polygon(P([(ax + sg * 0.42, -0.12), (ax + sg * 0.56, -0.4), (ax + sg * 0.3, -0.36)]), fill=k)
            E(ax, 0.72, 0.13, 0.13); d.ellipse([cx + (ax - 0.05) * s, cy - 0.77 * s, cx + (ax + 0.05) * s, cy - 0.67 * s], fill=pale)
        elif pic == 'hare':
            E(0, -0.38, 0.42, 0.4)
            for sg in (-1, 1):
                d.polygon(P([(sg * 0.08, -0.1), (sg * 0.2, 0.9), (sg * 0.42, 0.95), (sg * 0.4, -0.1)]), fill=k)
            for sg in (-1, 1): d.ellipse([cx + sg * 0.17 * s - 4, cy + 0.3 * s - 4, cx + sg * 0.17 * s + 4, cy + 0.3 * s + 4], fill=pale)
    from PIL import ImageFilter
    pic = '_'.join(dict.fromkeys(pics))
    import zlib
    rs = np.random.RandomState(zlib.crc32(pic.encode()) % 9999 + 1)
    def field(nx, ny): return np.array(Image.fromarray((rs.rand(ny, nx) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), np.float32) / 255.0
    yy, xx = np.mgrid[0:h, 0:w]
    sx = np.clip(xx + (field(26, 7) - .5) * 9 * SS, 0, w - 1).astype(int); sy = np.clip(yy + (field(26, 7) - .5) * 9 * SS, 0, h - 1).astype(int)
    M = np.array(im)[sy, sx]; mi = Image.fromarray(M)
    A = lambda i: np.array(i, np.float32) / 255.0
    line = np.clip(A(mi.filter(ImageFilter.MaxFilter(5))) - A(mi.filter(ImageFilter.MinFilter(4 * SS + 1))), 0, 1)
    line = A(Image.fromarray((line * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(.8 * SS)))
    hatch = (np.sin((xx * .8 + yy) * 2 * math.pi / (7.5 * SS) + field(40, 10) * 7) > -.15).astype(np.float32)
    tone = np.maximum(line * (.75 + .35 * field(60, 16)), A(mi) * hatch * (.35 + .4 * field(30, 8)))
    tooth = np.array(Image.fromarray((rs.rand(h // 2, w // 2) * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR), np.float32) / 255.0
    tone = tone * np.clip(.25 + 1.3 * tooth, 0, 1)
    smudge = A(Image.fromarray((np.clip(tone, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(7 * SS)))
    tone = np.clip(tone * 1.1 + smudge * .45, 0, 1)[:, :, None]
    col = np.array(paper, np.float32)[None, None, :] * (1 - tone) + np.array(ink, np.float32)[None, None, :] * tone
    out = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
    p = '/tmp/label_%s.png' % pic; out.save(p); return p

if 'Can' in only:
    wipe(); random.seed(3)
    prof = [(0, 0.003), (0.036, 0.003), (0.0375, 0.0), (0.0405, 0.0), (0.0412, 0.003), (0.040, 0.006), (0.040, 0.011)]
    for z in (0.011, 0.018): prof += [(0.0409, z + 0.0025), (0.040, z + 0.005), (0.040, z + 0.007)]
    prof += [(0.040, 0.085)]
    for z in (0.085, 0.092): prof += [(0.0409, z + 0.0025), (0.040, z + 0.005), (0.040, z + 0.007)]
    prof += [(0.040, 0.104), (0.0412, 0.107), (0.0405, 0.110), (0.0375, 0.110), (0.036, 0.107), (0, 0.107)]
    b = lathe('canb', 'kettle', prof, (0, 0, 0), M_tin, seg=28); rough_up(b, 0.0012, 0.05)
    b.data.set_sharp_from_angle(angle=math.radians(38))
    M_label = bpy.data.materials.new('label'); nn, ll, bb = nodes(M_label)
    uvn = nn.new('ShaderNodeUVMap'); uvn.uv_map = 'lab'; ti = nn.new('ShaderNodeTexImage'); ll.new(uvn.outputs['UV'], ti.inputs['Vector'])
    tcn = nn.new('ShaderNodeTexCoord'); nz = nn.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 60; nz.inputs['Detail'].default_value = 8
    ll.new(tcn.outputs['Object'], nz.inputs['Vector'])
    mr = nn.new('ShaderNodeMapRange'); mr.inputs[1].default_value = 0.3; mr.inputs[2].default_value = 0.7; mr.inputs[3].default_value = 0.62; mr.inputs[4].default_value = 1.08; ll.new(nz.outputs['Fac'], mr.inputs[0])
    mx = nn.new('ShaderNodeMixRGB'); mx.blend_type = 'MULTIPLY'; mx.inputs['Fac'].default_value = 1
    ll.new(ti.outputs['Color'], mx.inputs['Color1']); ll.new(mr.outputs[0], mx.inputs['Color2']); ll.new(mx.outputs['Color'], bb.inputs['Base Color']); bb.inputs['Roughness'].default_value = 0.92
    Z0, Z1 = 0.027, 0.084
    lab = lathe('lab', 'kettle', [(0.0412, Z0), (0.0412, Z1)], (0, 0, 0), M_label, seg=28)
    lay = lab.data.uv_layers.new(name='lab')
    for p in lab.data.polygons:
        us = []
        for li in p.loop_indices:
            co = lab.data.vertices[lab.data.loops[li].vertex_index].co; us.append((math.atan2(co.y, co.x) / (2 * math.pi)) % 1.0)
        wrap = max(us) - min(us) > 0.5
        for li, u in zip(p.loop_indices, us):
            co = lab.data.vertices[lab.data.loops[li].vertex_index].co
            lay.data[li].uv = ((u + 1 if wrap and u < 0.5 else u), (co.z - Z0) / (Z1 - Z0))
    TR, TZ = 0.0422, 0.0805
    tube('tw', 'kettle', [(TR * math.cos(a), TR * math.sin(a), TZ + 0.0015 * math.sin(3 * a)) for a in [i * 2 * math.pi / 12 for i in range(12)]], 0.0011, M_twine, res=3, cyclic=True)
    tube('knot', 'kettle', [(TR, -0.004, TZ), (TR + 0.0035, 0.0, TZ - 0.007), (TR + 0.0025, 0.004, TZ - 0.015)], 0.001, M_twine, res=3)
    tube('knot', 'kettle', [(TR, 0.004, TZ), (TR + 0.0035, 0.003, TZ - 0.005), (TR + 0.002, -0.006, TZ - 0.012)], 0.001, M_twine, res=3)
    # a pull ring on the lid: these cans open with no tool
    tube('ring', 'kettle', [(0.017 + 0.0085 * math.cos(a), 0.0085 * math.sin(a), 0.1082) for a in [i * 2 * math.pi / 10 for i in range(10)]], 0.0009, M_tin, res=3, cyclic=True)
    box('tab', 'kettle', (0.013, 0.007, 0.0012), (0.004, 0, 0.1078), mat=M_tin)
    cyl('rivet', 'kettle', 0.0022, 0.0016, (0.0, 0, 0.1084), mat=M_tin, seg=8)
    def setter(k):
        paper, ink, pic = KINDS[k]
        def f():
            im = bpy.data.images.load(label_image(paper, ink, pic)); ti.image = im
        return f
    setter(next(iter(KINDS)))()
    SEG = 28
    def can_uv(parts):
        # the can is a lathe: the label and the tin are unrolled flat by hand, so the drawing is sharp and nothing is wasted
        small = []
        for o in parts:
            me = o.data; uv = me.uv_layers['UV'].data; nm = o.name.split('.')[0]
            if nm == 'lab':
                src = me.uv_layers['lab'].data
                for i in range(len(uv)): uv[i].uv = (src[i].uv[0] * SEG / (SEG + 1.0), 0.715 + 0.28 * src[i].uv[1])
            elif nm == 'canb':
                arc = [0.0]
                for (r0, z0), (r1, z1) in zip(prof, prof[1:]): arc.append(arc[-1] + math.hypot(r1 - r0, z1 - z0))
                ring = {}; k = 0
                for ri, (r, z) in enumerate(prof):
                    n = 1 if r == 0 else SEG
                    for j in range(n): ring[k] = (ri, j if n > 1 else None); k += 1
                for p in me.polygons:
                    info = [ring[me.loops[li].vertex_index] for li in p.loop_indices]
                    js = [j for _, j in info if j is not None]; wrap = max(js) - min(js) > SEG / 2
                    us = [((j + SEG if wrap and j < SEG / 2 else j) / SEG) if j is not None else None for _, j in info]
                    mean = sum(u for u in us if u is not None) / len(js)
                    for li, (ri, _), u in zip(p.loop_indices, info, us):
                        uv[li].uv = ((mean if u is None else u) * (SEG / (SEG + 1.0)), 0.10 + 0.60 * arc[ri] / arc[-1])
            else: small.append(o)
        pts = [d.uv for o in small for d in o.data.uv_layers['UV'].data]
        x0 = min(p[0] for p in pts); x1 = max(p[0] for p in pts); y0 = min(p[1] for p in pts); y1 = max(p[1] for p in pts)
        for p in pts:
            p[0] = 0.01 + 0.98 * (p[0] - x0) / (x1 - x0); p[1] = 0.008 + 0.075 * (p[1] - y0) / (y1 - y0)
    finish('Can', 1024, curve_u=4, curve_b=2, variants={k: (setter(k), ['lab']) for k in KINDS}, gain=1.3, uvfix=can_uv)

# ---------------- sausages
def one(name, build, seed=5):
    wipe(); random.seed(seed); build(); finish(name, 512, curve_u=6, curve_b=4, gain=1.3)
if 'Saus' in only:
    def raw():
        def path(t): return (-0.2 + 0.4 * t, 0.045 * math.sin(t * 2 * math.pi * 0.9))
        chain_links('table', path, 4, 0.023, M_saus, 0.023)
    def dry():
        pts = []; rad = []; N = 32
        for i in range(N):
            a = i * 2 * math.pi / N; k = i % 8; pin = abs(math.sin(math.pi * k / 8)) ** 0.4; r = 0.019 * (0.3 + 0.7 * pin)
            pts.append((0.085 * math.sin(a) * (0.75 + 0.25 * math.cos(a)), 0.15 * math.cos(a), r)); rad.append(r)
        tube('ring', 'table', pts, rad, M_sausdry, res=6, cyclic=True)
        tube('str', 'table', [(0.0, 0.15, 0.03), (0.012, 0.17, 0.004), (0.0, 0.2, 0.003), (-0.012, 0.17, 0.004)], 0.0018, M_twine, res=3, cyclic=True)
    def boiled():
        for i in range(2):
            L = 0.2; a = 0.1 + 0.25 * i; cx = 0; cy = -0.03 + 0.06 * i
            pts = [(cx + (t - 0.5) * L * math.cos(a) + 0.012 * math.sin(t * math.pi) * math.sin(a), cy + (t - 0.5) * L * math.sin(a) - 0.012 * math.sin(t * math.pi) * math.cos(a), 0.027) for t in [k / 6 for k in range(7)]]
            tube('boil', 'table', pts, [0.012, 0.026, 0.028, 0.028, 0.028, 0.026, 0.012], M_boil, res=8)
            for t in (0, 6): cyl('bk', 'table', 0.006, 0.012, pts[t], (math.pi / 2, 0, a + math.pi / 2), M_twine, seg=6)
    M_saushot = mat_rich('saushot', [(0.3, (0.16, 0.035, 0.012)), (0.55, (0.33, 0.09, 0.03)), (0.78, (0.50, 0.19, 0.07))], scale=38, rough=0.33, bump=0.5, grime=0.5, randomize=True, rvar=0.12)
    def hot():
        # hot-smoked: a fat glossy horseshoe, ends tied
        pts = []; rad = []; N = 15
        for i in range(N):
            a = math.pi * (0.22 + 1.56 * i / (N - 1)); t = i / (N - 1); r = 0.024 * (0.35 + 0.65 * min(1, math.sin(math.pi * t) * 3.2))
            pts.append((0.085 * math.sin(a), 0.11 * math.cos(a) + 0.02, r)); rad.append(r)
        tube('shoe', 'table', pts, rad, M_saushot, res=8)
        for q in (pts[0], pts[-1]): cyl('bk', 'table', 0.006, 0.012, (q[0], q[1], 0.012), (math.pi / 2, 0, 0.5), M_twine, seg=6)
        tube('str', 'table', [pts[0][:2] + (0.012,), (0.03, 0.15, 0.02), (0.0, 0.165, 0.004), (-0.03, 0.15, 0.02), pts[-1][:2] + (0.012,)], 0.0016, M_twine, res=3)
    if len(sys.argv) > 99 or os.environ.get('ONLY_HOT'): one('SausHot', hot)
    else:
        one('SausRaw', raw); one('SausDry', dry); one('SausBoiled', boiled); one('SausHot', hot)

# ---------------- sausages v2 (11-12): six kinds made at the table, each raw / hot-smoked / dry-cured. python3 build_stations.py Saus2 [Kind,...]
if 'Saus2' in only:
    TINT = {'Hunt': (0.38, 0.07, 0.05), 'Taiga': (0.30, 0.05, 0.045), 'Bear': (0.21, 0.04, 0.035), 'Wolf': (0.19, 0.035, 0.065), 'Game': (0.50, 0.21, 0.17), 'Fish': (0.50, 0.37, 0.31),
            'Mix': (0.34, 0.06, 0.05), 'Forest': (0.45, 0.13, 0.10), 'Angler': (0.50, 0.29, 0.24), 'Beast': (0.17, 0.035, 0.05)}
    want = os.environ.get('KINDS', ','.join(TINT)).split(',')
    def stage_mat(kind, stage):
        c = TINT[kind]; mul = lambda k, a=0.0: tuple(min(1, x * k + a) for x in c)
        if stage == 'Raw': return mat_rich('m' + kind + stage, [(0.3, mul(0.7)), (0.55, c), (0.78, mul(1.25, 0.06))], scale=45, rough=0.34, bump=0.5, grime=0.4, randomize=True, rvar=0.15)
        if stage == 'Hot':
            b = (0.34, 0.10, 0.035); m = tuple(0.65 * x + 0.35 * y for x, y in zip(b, c))
            return mat_rich('m' + kind + stage, [(0.3, tuple(x * 0.5 for x in m)), (0.55, m), (0.78, tuple(min(1, x * 1.5 + 0.02) for x in m))], scale=38, rough=0.33, bump=0.5, grime=0.5, randomize=True, rvar=0.12)
        return mat_rich('m' + kind + stage, [(0.3, mul(0.22)), (0.55, mul(0.5)), (0.8, (0.36, 0.30, 0.26))], scale=55, rough=0.64, bump=1.1, grime=0.6, randomize=True)
    taper = lambda n, r, k=3.2: [r * (0.35 + 0.65 * min(1, math.sin(math.pi * i / (n - 1)) * k)) for i in range(n)]
    def loop_at(p, d=(0, 1)):
        x, y, z = p; dx, dy = d
        tube('lp', 'table', [(x, y, z), (x + dx * 0.02 - dy * 0.012, y + dy * 0.02 + dx * 0.012, 0.004), (x + dx * 0.04, y + dy * 0.04, 0.003), (x + dx * 0.02 + dy * 0.012, y + dy * 0.02 - dx * 0.012, 0.004)], 0.0016, M_twine, res=3, cyclic=True)
    def shape(kind, mat, k):   # k: 1 fresh, <1 dried and shrunk
        if kind == 'Hunt':     # hunter's sticks: four thin ones tied in a bunch
            for i in range(4):
                r = 0.0115 * k; y = (i - 1.5) * 0.026; bend = 0.012 * (i - 1.5)
                pts = [(-0.11 + 0.22 * t, y * (0.35 + 0.65 * t) + bend * math.sin(math.pi * t), r) for t in [j / 6 for j in range(7)]]
                tube('st', 'table', pts, taper(7, r, 5), mat, res=6)
            tube('tie', 'table', [(-0.105, 0.03 * math.cos(a) * 0.55, 0.012 + 0.016 * math.sin(a)) for a in [j * 2 * math.pi / 8 for j in range(8)]], 0.0016, M_twine, res=3, cyclic=True)
            loop_at((-0.11, 0, 0.012), (-1, 0))
        elif kind == 'Taiga':  # a thick ring
            N = 18; r = 0.024 * k; pts = []; rad = []
            for i in range(N):
                a = i * 2 * math.pi / N; pin = min(1, 0.3 + abs(math.sin(a / 2)) * 4)
                pts.append((0.085 * math.sin(a), 0.105 * math.cos(a), r)); rad.append(r * pin)
            tube('ring', 'table', pts, rad, mat, res=8, cyclic=True)
            loop_at((0, 0.105, 0.012))
        elif kind == 'Bear':   # a short fat loaf in a twine net
            r = 0.034 * k; n = 9
            pts = [(-0.1 + 0.2 * j / (n - 1), 0, r) for j in range(n)]
            tube('loaf', 'table', pts, taper(n, r, 4), mat, res=10)
            for x in (-0.06, -0.02, 0.02, 0.06):
                tube('net', 'table', [(x, (r + 0.0008) * math.cos(a), r + (r + 0.0008) * math.sin(a)) for a in [j * 2 * math.pi / 10 for j in range(10)]], 0.0013, M_twine, res=3, cyclic=True)
            tube('net', 'table', [(-0.095 + 0.19 * j / 6, 0, 2 * r + 0.0008 - 0.004 * abs(j - 3) / 3) for j in range(7)], 0.0013, M_twine, res=3)
            loop_at((0.1, 0, 0.014), (1, 0))
        elif kind == 'Wolf':   # a thin dark horseshoe
            N = 15; r = 0.019 * k; pts = []
            for i in range(N):
                a = math.pi * (0.2 + 1.6 * i / (N - 1)); pts.append((0.08 * math.sin(a), 0.105 * math.cos(a) + 0.02, r))
            tube('shoe', 'table', pts, taper(N, r), mat, res=8)
            tube('str', 'table', [pts[0][:2] + (0.01,), (0.03, 0.15, 0.015), (0.0, 0.165, 0.004), (-0.03, 0.15, 0.015), pts[-1][:2] + (0.01,)], 0.0016, M_twine, res=3)
        elif kind == 'Game':   # three plump short links
            def path(t): return (-0.14 + 0.28 * t, 0.03 * math.sin(t * 2 * math.pi * 0.8))
            chain_links('table', path, 3, 0.027 * k, mat, 0.027 * k)
            loop_at((-0.14, 0, 0.008), (-1, 0))
        elif kind == 'Fish':   # a pale coil
            N = 26; r = 0.016 * k; pts = []
            for i in range(N):
                t = i / (N - 1); a = t * 2 * math.pi * 1.75; R = 0.022 + 0.07 * t
                pts.append((R * math.cos(a), R * math.sin(a), r))
            tube('coil', 'table', pts, taper(N, r, 8), mat, res=7)
            loop_at(pts[-1], (0, -1))
    _shape = shape
    def shape(kind, mat, k):
        if kind == 'Mix':      # two meats: a long straight stick tied at both ends
            r = 0.022 * k; n = 11
            pts = [(-0.15 + 0.3 * j / (n - 1), 0.006 * math.sin(j * 0.7), r) for j in range(n)]
            tube('stick', 'table', pts, taper(n, r, 6), mat, res=9)
            for q in (pts[0], pts[-1]): cyl('bk', 'table', 0.007, 0.012, (q[0], q[1], 0.011), (0, math.pi / 2, 0), M_twine, seg=6)
            loop_at((-0.15, 0, 0.01), (-1, 0))
        elif kind == 'Forest': # two small rings on one string
            for sx in (-1, 1):
                N = 14; r = 0.017 * k; pts = []; rad = []
                for i in range(N):
                    a = i * 2 * math.pi / N; pts.append((sx * 0.062 + 0.05 * math.sin(a), 0.058 * math.cos(a), r)); rad.append(r * min(1, 0.3 + abs(math.sin(a / 2)) * 4))
                tube('ring', 'table', pts, rad, mat, res=7, cyclic=True)
            tube('str', 'table', [(-0.062, 0.058, 0.012), (-0.03, 0.09, 0.004), (0, 0.1, 0.004), (0.03, 0.09, 0.004), (0.062, 0.058, 0.012)], 0.0016, M_twine, res=3)
        elif kind == 'Angler': # a short fat U
            N = 13; r = 0.023 * k; pts = []
            for i in range(N):
                a = math.pi * (0.32 + 1.36 * i / (N - 1)); pts.append((0.055 * math.sin(a), 0.085 * math.cos(a) + 0.02, r))
            tube('u', 'table', pts, taper(N, r, 4), mat, res=8)
            tube('str', 'table', [pts[0][:2] + (0.012,), (0.02, 0.12, 0.012), (0.0, 0.135, 0.004), (-0.02, 0.12, 0.012), pts[-1][:2] + (0.012,)], 0.0016, M_twine, res=3)
        elif kind == 'Beast':  # a heavy dark ring in a twine net
            N = 18; r = 0.028 * k; pts = []; rad = []
            for i in range(N):
                a = i * 2 * math.pi / N; pts.append((0.095 * math.sin(a), 0.115 * math.cos(a), r)); rad.append(r * min(1, 0.3 + abs(math.sin(a / 2)) * 4))
            tube('ring', 'table', pts, rad, mat, res=8, cyclic=True)
            for a in [math.pi * (0.25 + 0.25 * j) for j in range(7)]:
                cx, cy = 0.095 * math.sin(a), 0.115 * math.cos(a); tx, ty = 0.095 * math.cos(a), -0.115 * math.sin(a); L = math.hypot(tx, ty); nx, ny = -ty / L, tx / L
                tube('net', 'table', [(cx + nx * (r + 0.0008) * math.cos(b), cy + ny * (r + 0.0008) * math.cos(b), r + (r + 0.0008) * math.sin(b)) for b in [q * 2 * math.pi / 10 for q in range(10)]], 0.0013, M_twine, res=3, cyclic=True)
            loop_at((0, 0.115, 0.012))
        else: _shape(kind, mat, k)
    for kind in want:
        for stage in ('Raw', 'Hot', 'Dry'):
            wipe(); random.seed(5)
            shape(kind, stage_mat(kind, stage), 0.86 if stage == 'Dry' else 0.96 if stage == 'Hot' else 1.0)
            finish('Saus' + kind + stage, 512, curve_u=6, curve_b=4, gain=1.3)

# ---------------- sausages v3 (11-12, owner: v2 looked like plastic, twine sunk into the skin, loops hung in the air): real casings with tied tails,
# lumpy filling, fat seen through the skin, twine that lies on the surface. KINDS=a,b python3 build_stations.py Saus3
if any(x in only for x in ('Saus3', 'Table', 'Kettle')):
    from mathutils import Vector, noise as MN
    UP = Vector((0, 0, 1)); TW = 0.0012
    M_jute = mat_rich('jute', [(0.3, (0.16, 0.11, 0.06)), (0.7, (0.36, 0.28, 0.17))], scale=160, rough=0.95, bump=0.8, grime=0.3)
    TINT = {'Hunt': (0.38, 0.07, 0.05), 'Taiga': (0.30, 0.05, 0.045), 'Bear': (0.21, 0.04, 0.035), 'Wolf': (0.19, 0.035, 0.065), 'Cougar': (0.33, 0.09, 0.08), 'Game': (0.50, 0.21, 0.17),
            'Bird': (0.55, 0.30, 0.22), 'Fish': (0.52, 0.40, 0.33), 'Coho': (0.62, 0.22, 0.11), 'WhiteFish': (0.60, 0.52, 0.44), 'Trout': (0.62, 0.30, 0.24), 'Bass': (0.42, 0.40, 0.30),
            'Burbot': (0.50, 0.44, 0.34), 'Goldeye': (0.62, 0.46, 0.20), 'Rockfish': (0.55, 0.20, 0.14), 'IrishLord': (0.44, 0.15, 0.10),
            'Mix': (0.34, 0.06, 0.05), 'Forest': (0.45, 0.13, 0.10), 'Angler': (0.50, 0.29, 0.24), 'Beast': (0.17, 0.035, 0.05),
            'Trapper': (0.40, 0.14, 0.10), 'Trophy': (0.22, 0.04, 0.04), 'GreatBear': (0.27, 0.06, 0.045), 'Tramp': (0.36, 0.11, 0.09)}
    want = os.environ.get('KINDS', ','.join(TINT)).split(',')

    def mat_saus(name, base, fat, fat_amt, chunk=0.25, smoke=0.0, bloom=0.0, smoke_col=(0.20, 0.065, 0.02), patch=(0.35, 0.7)):
        """Minced meat under a casing: chunks of darker and lighter meat, specks of fat, smoke browning, the white bloom of a dry-cured one."""
        m = bpy.data.materials.new(name); n, l, b = nodes(m)
        tc = n.new('ShaderNodeTexCoord'); src = tc.outputs['Object']
        def node(t, **kw):
            x = n.new(t)
            for k, v in kw.items():
                if k in x.inputs: x.inputs[k].default_value = v
                else: setattr(x, k, v)
            return x
        def mixc(a, b_, fac, blend='MIX'):
            x = n.new('ShaderNodeMixRGB'); x.blend_type = blend
            for sock, v in ((x.inputs['Color1'], a), (x.inputs['Color2'], b_), (x.inputs['Fac'], fac)):
                if hasattr(v, 'links') or hasattr(v, 'is_linked'): l.new(v, sock)
                elif isinstance(v, tuple): sock.default_value = (*v, 1)
                else: sock.default_value = v
            return x.outputs['Color']
        def rng(v, a, b_, c=0.0, d=1.0):
            x = n.new('ShaderNodeMapRange'); x.inputs[1].default_value = a; x.inputs[2].default_value = b_; x.inputs[3].default_value = c; x.inputs[4].default_value = d
            l.new(v, x.inputs[0]); return x.outputs[0]
        big = node('ShaderNodeTexNoise', Scale=28.0, Detail=5.0); l.new(src, big.inputs['Vector'])
        col = mixc(tuple(x * 0.72 for x in base), tuple(min(1, x * 1.22 + 0.01) for x in base), rng(big.outputs['Fac'], 0.3, 0.7))
        # chunks of meat: every cell its own shade
        vc = node('ShaderNodeTexVoronoi', Scale=240.0); l.new(src, vc.inputs['Vector'])
        sep = n.new('ShaderNodeSeparateColor'); l.new(vc.outputs['Color'], sep.inputs[0])
        col = mixc(col, rng(sep.outputs[0], 0, 1, 1 - chunk, 1 + chunk * 0.6), 1.0, 'MULTIPLY')
        # fat: some of the cells of a coarser pattern, soft-edged
        vf = node('ShaderNodeTexVoronoi', Scale=170.0); l.new(src, vf.inputs['Vector'])
        sf = n.new('ShaderNodeSeparateColor'); l.new(vf.outputs['Color'], sf.inputs[0])
        pick = rng(sf.outputs[1], fat_amt, fat_amt - 0.04)
        near = rng(vf.outputs['Distance'], 0.0042, 0.0022)
        mu = n.new('ShaderNodeMath'); mu.operation = 'MULTIPLY'; l.new(pick, mu.inputs[0]); l.new(near, mu.inputs[1])
        mu2 = n.new('ShaderNodeMath'); mu2.operation = 'MULTIPLY'; mu2.inputs[1].default_value = 0.85; l.new(mu.outputs[0], mu2.inputs[0])
        col = mixc(col, fat, mu2.outputs[0])
        if smoke > 0:
            sn = node('ShaderNodeTexNoise', Scale=14.0, Detail=3.0); l.new(src, sn.inputs['Vector'])
            col = mixc(col, smoke_col, rng(sn.outputs['Fac'], patch[0], patch[1], smoke * 0.15, smoke))
        if bloom > 0:
            bn = node('ShaderNodeTexNoise', Scale=70.0, Detail=9.0, Roughness=0.75); l.new(src, bn.inputs['Vector'])
            col = mixc(col, (0.62, 0.59, 0.53), rng(bn.outputs['Fac'], 0.48, 0.72, 0.0, bloom))
        fine = node('ShaderNodeTexNoise', Scale=900.0, Detail=2.0); l.new(src, fine.inputs['Vector'])
        col = mixc(col, rng(fine.outputs['Fac'], 0.3, 0.7, 0.85, 1.12), 1.0, 'MULTIPLY')
        ao = n.new('ShaderNodeAmbientOcclusion'); ao.inputs['Distance'].default_value = 0.012; ao.samples = 8
        pw = n.new('ShaderNodeMath'); pw.operation = 'POWER'; pw.inputs[1].default_value = 2.2; l.new(ao.outputs['AO'], pw.inputs[0])
        col = mixc(col, pw.outputs[0], 0.8, 'MULTIPLY')
        l.new(col, b.inputs['Base Color']); b.inputs['Roughness'].default_value = 0.6
        return m

    def stage_mat(kind, stage):
        c = TINT[kind]; fishy = sum(c) > 1.0 or kind in ('Fish', 'Coho', 'WhiteFish', 'Trout', 'Bass', 'Burbot', 'Goldeye', 'Rockfish', 'IrishLord', 'Angler', 'Bird')
        if fishy:   # 11-12 owner: fish sausages were white raw and cold-smoked, and not browned enough hot
            pink = (0.60, 0.27, 0.22); r = tuple(0.88 * (0.55 * x + 0.45 * y) for x, y in zip(c, pink))
            if stage == 'Raw': return mat_saus('m' + kind + stage, tuple(0.74 * (0.35 * x + 0.65 * y) for x, y in zip(c, (0.62, 0.22, 0.19))), (0.70, 0.50, 0.44), 0.20)
            if stage == 'Hot':
                b = (0.40, 0.095, 0.018); m = tuple(0.82 * x + 0.18 * y for x, y in zip(b, r))   # browned: a golden-brown crust with darker scorched patches
                return mat_saus('m' + kind + stage, m, (0.62, 0.36, 0.14), 0.14, chunk=0.36, smoke=0.9, smoke_col=(0.085, 0.022, 0.008), patch=(0.42, 0.66))
            d = tuple(x * y for x, y in zip(r, (0.80, 0.56, 0.34)))
            return mat_saus('m' + kind + stage, d, (0.76, 0.60, 0.36), 0.28, chunk=0.3, smoke=0.3, bloom=0.06)
        if stage == 'Raw': return mat_saus('m' + kind + stage, c, (0.78, 0.62, 0.55), 0.30)
        if stage == 'Hot':
            b = (0.30, 0.085, 0.03); m = tuple(0.5 * x + 0.5 * y for x, y in zip(b, c))
            return mat_saus('m' + kind + stage, m, (0.74, 0.54, 0.33), 0.26, chunk=0.34, smoke=0.6)
        d = tuple(x * 0.5 for x in c)
        return mat_saus('m' + kind + stage, d, (0.80, 0.73, 0.64), 0.36, chunk=0.3, bloom=0.4)

    class Body:
        """A sausage along a path lying on the table: rounded ends pulled into a tied neck with a casing tail, lumps, a flat underside."""
        SEG = 16
        def __init__(s, P, r, mat, k=1.0, lump=0.05, dips=(), pinches=(), flat=0.86, seed=0, tails=(True, True)):
            s.r = r * k; s.r0 = r; s.k = k; s.lump = lump * (1.8 if k < 0.9 else 1); s.flat = flat; s.tails = tails
            s.pts, s.tot = s.resample([Vector(p) for p in P], r * 0.28)
            s.dips = dips; s.pinches = [p * s.tot for p in pinches]; s.ph = random.Random(seed).uniform(0, 90); s.tl = 0.7 * s.r; s.cap = 1.25 * s.r
            s.dry = k < 0.9
            s.mesh(mat)
        @staticmethod
        def resample(P, step):
            dense = []; n = len(P)
            for i in range(n - 1):
                p0 = P[i - 1] if i > 0 else P[0] * 2 - P[1]; p1 = P[i]; p2 = P[i + 1]; p3 = P[i + 2] if i + 2 < n else P[-1] * 2 - P[-2]
                for j in range(12):
                    t = j / 12
                    dense.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
            dense.append(P[-1])
            L = [0.0]
            for a, b in zip(dense, dense[1:]): L.append(L[-1] + (b - a).length)
            tot = L[-1]; m = max(8, int(round(tot / step))); out = []; j = 0
            for i in range(m + 1):
                d = tot * i / m
                while j < len(L) - 2 and L[j + 1] < d: j += 1
                out.append(dense[j].lerp(dense[j + 1], (d - L[j]) / max(1e-9, L[j + 1] - L[j])))
            return out, tot
        def frame(s, d):
            n = len(s.pts) - 1; x = min(max(d / s.tot, 0), 1) * n; i = min(int(x), n - 1); c = s.pts[i].lerp(s.pts[i + 1], x - i)
            a = s.pts[max(0, i - 1)]; b = s.pts[min(n, i + 2)]; t = (b - a).normalized()
            side = UP.cross(t); side = side.normalized() if side.length > 1e-5 else Vector((1, 0, 0))
            return c, t, side, t.cross(side).normalized()
        def radius(s, d):
            e = min(d, s.tot - d); end = 0 if d < s.tot / 2 else 1; r = s.r; tl = s.tl if s.tails[end] else 0.0
            if e < tl: return r * (0.15 + 0.12 * (1 - e / tl) ** 1.5), 1.0     # the casing tail, flaring to its cut end
            pr = 1.0
            if e < tl + s.cap: pr = max(0.15, math.sqrt(max(0, 1 - (1 - (e - tl) / s.cap) ** 2)))
            tw = max(0.0, 1 - (e - tl) / (1.4 * r))
            for p in s.pinches:
                q = abs(d - p); pr *= 1 - 0.84 * math.exp(-(q / (0.42 * r)) ** 2); tw = max(tw, 1 - q / (1.5 * r))
            for p in s.dips: pr *= 1 - 0.075 * math.exp(-((d - p * s.tot) / 0.0035) ** 2)
            pr *= 1 + s.lump * MN.noise(Vector((d * 55, s.ph, 0))) * min(1, pr * 1.5)
            return r * pr, max(0.0, tw)
        def off(s, d, th, rr, tw):
            a = 1 + 0.035 * MN.noise(Vector((math.cos(th) * 1.5, math.sin(th) * 1.5, d * 70 + s.ph)))
            a *= 1 - 0.11 * tw * (0.5 + 0.5 * math.cos(8 * th + s.ph))          # the casing gathers into folds at a tie
            if s.dry: a *= 1 + 0.045 * math.sin(9 * th + 4 * MN.noise(Vector((d * 30, 7, s.ph))))   # dried: wrinkled along
            y = math.cos(th) * rr * a; z = math.sin(th) * rr * a
            if z < 0: z *= s.flat
            return y, z
        def point(s, d, th, out=0.0):
            c, t, side, up = s.frame(d); rr, tw = s.radius(d); y, z = s.off(d, th, rr + out, tw)
            return c + side * y + up * z - UP * (s.r * (1 - s.flat))
        def mesh(s, mat):
            n = len(s.pts); V = []; F = []; S = s.SEG
            for i in range(n):
                d = s.tot * i / (n - 1)
                for j in range(S): V.append(tuple(s.point(d, 2 * math.pi * j / S)))
            for i in range(n - 1):
                for j in range(S): F.append((i * S + j, i * S + (j + 1) % S, (i + 1) * S + (j + 1) % S, (i + 1) * S + j))
            c0 = s.frame(0)[0] - UP * (s.r * (1 - s.flat)); c1 = s.frame(s.tot)[0] - UP * (s.r * (1 - s.flat))
            V += [tuple(c0), tuple(c1)]
            for j in range(S): F.append((len(V) - 2, (j + 1) % S, j)); F.append((len(V) - 1, (n - 1) * S + j, (n - 1) * S + (j + 1) % S))
            me = bpy.data.meshes.new('saus'); me.from_pydata(V, [], F); me.update()
            for p in me.polygons: p.use_smooth = True
            o = bpy.data.objects.new('saus', me); C['table'].objects.link(o); me.materials.append(mat); s.ob = o
        # ---- twine that lies on the skin
        def neck(s, end): return s.tl * 0.85 if end == 0 else s.tot - s.tl * 0.85
        def surf(s, d, th, out):
            """A point over the MESH (straight between its rings), not over the ideal shape: the twine cannot sink into a facet."""
            n = len(s.pts) - 1; x = min(max(d / s.tot, 0), 1) * n; i = min(int(x), n - 1); f = x - i
            return s.point(s.tot * i / n, th, out).lerp(s.point(s.tot * (i + 1) / n, th, out), f)
        @staticmethod
        def dense(P, cyclic, k=4):
            """The twine is a smooth curve that cuts every corner of its points: with points close together it keeps to the straight runs between them."""
            out = []; n = len(P)
            for i in range(n if cyclic else n - 1):
                for j in range(k): out.append(tuple(P[i].lerp(P[(i + 1) % n], j / k)))
            if not cyclic: out.append(tuple(P[-1]))
            return out
        def wrap(s, d, turns=2):
            for q in range(turns):
                dd = d + (q - (turns - 1) / 2) * TW * 1.9
                tube('tw', 'table', s.dense([s.surf(dd, 2 * math.pi * j / s.SEG, TW * 1.15) for j in range(s.SEG)], True), TW, M_jute, res=2, cyclic=True)
        def tie(s, end, loop=False):
            d = s.neck(end); s.wrap(d)
            if loop: s.hang(end)
        def tip(s, end):
            c, t, _, _ = s.frame(0 if end == 0 else s.tot); return c - UP * (s.r * (1 - s.flat)), (-t if end == 0 else t)
        def hang(s, end):
            """The loop it hangs by: off the knot, down to the table and round."""
            p0 = s.point(s.neck(end), math.pi / 2, TW * 1.6); tp, dr = s.tip(end); dr = Vector((dr.x, dr.y, 0)).normalized(); sd = UP.cross(dr)
            g = tp + dr * 0.034; g.z = TW; R = 0.013
            pts = [p0, Vector((tp.x, tp.y, max(TW, tp.z * 0.55))) + dr * 0.006]
            for j in range(13):
                a = math.pi + 2 * math.pi * j / 12
                pts.append(g + dr * (R * 1.25 * math.cos(a)) + sd * (R * math.sin(a)) + UP * (0.0015 if j == 12 else 0))
            tube('tw', 'table', [tuple(p) for p in pts], TW, M_jute, res=2)
        def along(s, th, d0=None, d1=None, n=72):
            d0 = s.neck(0) if d0 is None else d0; d1 = s.neck(1) if d1 is None else d1
            th = round(th / (2 * math.pi / s.SEG)) * (2 * math.pi / s.SEG); m = len(s.pts) - 1
            ds = [d0] + [s.tot * i / m for i in range(m + 1) if d0 < s.tot * i / m < d1] + [d1]
            tube('tw', 'table', s.dense([s.surf(d, th, TW * 1.15) for d in ds], False), TW, M_jute, res=2)

    def bind(bodies_ends, loop=True):
        """Several necks tied together with one binding (a ring's two ends, a bunch)."""
        cs = [b.frame(b.neck(e))[0] - UP * (b.r * (1 - b.flat)) for b, e in bodies_ends]; b0, e0 = bodies_ends[0]
        nr = max(b.radius(b.neck(e))[0] for b, e in bodies_ends) + TW
        mid = sum(cs, Vector()) / len(cs); ax = cs[-1] - cs[0]; half = ax.length / 2 + nr
        ax = ax.normalized() if ax.length > 1e-5 else b0.frame(b0.neck(e0))[2]
        t = b0.tip(e0)[1]
        for q in (-1, 1):
            tube('tw', 'table', [tuple(mid + t * (q * TW) + ax * (half * math.cos(a)) + UP * (nr * math.sin(a))) for a in [2 * math.pi * j / 14 for j in range(14)]], TW, M_jute, res=2, cyclic=True)
        if loop:
            dr = Vector((t.x, t.y, 0)).normalized(); sd = UP.cross(dr); tp = mid + dr * (b0.tl * 0.9); g = tp + dr * 0.034; g.z = TW; R = 0.013
            pts = [mid + UP * nr, Vector((tp.x, tp.y, max(TW, tp.z * 0.5))) + dr * 0.008]
            for j in range(13):
                a = math.pi + 2 * math.pi * j / 12
                pts.append(g + dr * (R * 1.25 * math.cos(a)) + sd * (R * math.sin(a)) + UP * (0.0015 if j == 12 else 0))
            tube('tw', 'table', [tuple(p) for p in pts], TW, M_jute, res=2)

    def string(b1, e1, b2, e2, sag=0.05):
        """One string from a neck to another, lying on the table in between."""
        p1 = b1.point(b1.neck(e1), math.pi / 2, TW * 1.6); p2 = b2.point(b2.neck(e2), math.pi / 2, TW * 1.6)
        t1, d1 = b1.tip(e1); t2, d2 = b2.tip(e2)
        a = t1 + d1 * 0.012; a.z = TW; b = t2 + d2 * 0.012; b.z = TW
        m = (a + b) / 2 + (d1 + d2).normalized() * sag if (d1 + d2).length > 0.2 else (a + b) / 2; m.z = TW
        q1 = a.lerp(m, 0.5) + d1 * sag * 0.25; q2 = b.lerp(m, 0.5) + d2 * sag * 0.25; q1.z = q2.z = TW
        tube('tw', 'table', [tuple(p) for p in (p1, a, q1, m, q2, b, p2)], TW, M_jute, res=2)

    def ring_path(Rx, Ry, r, n=22):
        """Nearly a full turn, the two ends coming together and pointing out side by side."""
        g = math.asin(min(0.9, 0.95 * r / Rx)); P = [(0.30 * r, Ry * math.cos(g) + 2.4 * r, r), (0.75 * r, Ry * math.cos(g) + 1.0 * r, r)]
        for i in range(n + 1):
            a = g + (2 * math.pi - 2 * g) * i / n; P.append((Rx * math.sin(a), Ry * math.cos(a), r))
        P += [(-0.75 * r, Ry * math.cos(g) + 1.0 * r, r), (-0.30 * r, Ry * math.cos(g) + 2.4 * r, r)]
        return P
    def arc_path(Rx, Ry, a0, a1, r, n=14, cy=0.0): return [(Rx * math.sin(a0 + (a1 - a0) * i / n), Ry * math.cos(a0 + (a1 - a0) * i / n) + cy, r) for i in range(n + 1)]
    def line_path(x0, x1, r, n=8, wob=0.004, y=0.0): return [(x0 + (x1 - x0) * i / n, y + wob * math.sin(i * 1.3), r) for i in range(n + 1)]
    def spiral(r, turns, R0, R1, n=40): return [((R0 + (R1 - R0) * i / n) * math.cos(2 * math.pi * turns * i / n), (R0 + (R1 - R0) * i / n) * math.sin(2 * math.pi * turns * i / n), r) for i in range(n + 1)]

    def links(mat, k, n, r, path, loop=True, sd=1):
        b = Body(path, r, mat, k, pinches=[i / n for i in range(1, n)], seed=sd)
        for p in b.pinches: b.wrap(p, 2)
        b.tie(0, loop); b.tie(1); return b
    def ring(mat, k, Rx, Ry, r, net=0, sd=1):
        P = ring_path(Rx, Ry, r); dips = [(i + 0.5) / net * 0.8 + 0.1 for i in range(net)]
        b = Body(P, r, mat, k, dips=dips, seed=sd)
        for d in dips: b.wrap(d * b.tot, 1)
        bind([(b, 0), (b, 1)]); return b
    def loaf(mat, k, L, r, rings, strings=(math.pi / 2,), sd=1):
        dips = [(i + 1) / (rings + 1) for i in range(rings)]
        b = Body(line_path(-L / 2, L / 2, r, wob=0.0), r, mat, k, dips=dips, seed=sd)
        for d in dips: b.wrap(d * b.tot, 1)
        for th in strings: b.along(th)
        b.tie(0, True); b.tie(1); return b

    def shape(kind, mat, k):
        if kind == 'Hunt':         # four thin sticks tied in a bunch at one end
            bs = []
            for i in range(4):
                y = (i - 1.5); P = [(-0.11 + 0.22 * t, y * (0.0065 + 0.022 * t) + 0.01 * y * math.sin(math.pi * t), 0.0115) for t in [j / 6 for j in range(7)]]
                b = Body(P, 0.0115, mat, k, seed=i, lump=0.04); b.tie(1); bs.append(b)
            bind([(b, 0) for b in bs])
        elif kind == 'Taiga': ring(mat, k, 0.085, 0.10, 0.024)
        elif kind == 'Bear': loaf(mat, k, 0.21, 0.034, 4)
        elif kind == 'Wolf':
            b = Body(arc_path(0.08, 0.105, math.pi * 0.22, math.pi * 1.78, 0.018, cy=0.02), 0.018, mat, k, seed=3); b.tie(0); b.tie(1); string(b, 0, b, 1)
        elif kind == 'Cougar':     # a long thin hairpin
            P = [(0.03, 0.13, 0.015), (0.032, 0.02, 0.015), (0.03, -0.09, 0.015), (0.0, -0.125, 0.015), (-0.03, -0.09, 0.015), (-0.032, 0.02, 0.015), (-0.03, 0.13, 0.015)]
            b = Body(P, 0.015, mat, k, seed=4); bind([(b, 0), (b, 1)])
        elif kind == 'Game': links(mat, k, 3, 0.026, [(-0.14 + 0.28 * t, 0.03 * math.sin(t * 5), 0.026) for t in [j / 9 for j in range(10)]])
        elif kind == 'Bird': links(mat, k, 5, 0.015, arc_path(0.10, 0.10, -1.9, 1.9, 0.015, n=16), sd=2)
        elif kind == 'Fish':       # all the fish of the island in one: a big coil tied across
            b = Body(spiral(0.019, 2.1, 0.02, 0.105, 46), 0.019, mat, k, seed=5, tails=(True, True)); b.tie(0); b.tie(1, True)
            for a in ():
                top = 2 * b.r * 0.93 + TW; e = 0.128
                tube('tw', 'table', [tuple(Vector((math.cos(a), math.sin(a), 0)) * x + UP * z) for x, z in ((-e, TW), (-e - 0.001, top * 0.55), (-e + 0.01, top), (0, top), (e - 0.01, top), (e + 0.001, top * 0.55), (e, TW))], TW, M_jute, res=2)
        elif kind == 'Coho':       # a straight stick with three ties
            b = Body(line_path(-0.12, 0.12, 0.024, wob=0.003), 0.024, mat, k, dips=(0.3, 0.5, 0.7), seed=6)
            for d in (0.3, 0.5, 0.7): b.wrap(d * b.tot, 1)
            b.tie(0, True); b.tie(1)
        elif kind == 'Trout': links(mat, k, 2, 0.022, [(-0.12 + 0.24 * t, 0.02 * math.sin(t * 3), 0.022) for t in [j / 7 for j in range(8)]], sd=7)
        elif kind == 'WhiteFish': ring(mat, k, 0.06, 0.07, 0.019, sd=8)
        elif kind == 'Bass':       # a short fat one
            b = Body(line_path(-0.085, 0.085, 0.032, wob=0.002), 0.032, mat, k, seed=9, lump=0.07); b.tie(0, True); b.tie(1)
        elif kind == 'Burbot':     # a long thin S
            P = [(-0.15 + 0.3 * t, 0.045 * math.sin(t * 2 * math.pi), 0.014) for t in [j / 14 for j in range(15)]]
            b = Body(P, 0.014, mat, k, seed=10); b.tie(0, True); b.tie(1)
        elif kind == 'Goldeye': links(mat, k, 4, 0.013, [(-0.11 + 0.22 * t, 0.012 * math.sin(t * 7), 0.013) for t in [j / 11 for j in range(12)]], sd=11)
        elif kind == 'Rockfish':
            b = Body(arc_path(0.06, 0.08, math.pi * 0.3, math.pi * 1.7, 0.021, cy=0.015), 0.021, mat, k, seed=12); b.tie(0); b.tie(1); string(b, 0, b, 1, 0.035)
        elif kind == 'IrishLord':  # a small coil
            b = Body(spiral(0.017, 1.3, 0.022, 0.07, 26), 0.017, mat, k, seed=13); b.tie(0); b.tie(1, True)
        elif kind == 'Mix':
            b = Body(line_path(-0.15, 0.15, 0.021), 0.021, mat, k, seed=14); b.tie(0, True); b.tie(1)
        elif kind == 'Forest':     # two small rings on one string
            bs = []
            for sx in (-1, 1):
                P = [(x * 0.62 + sx * 0.062, y * 0.62 - 0.02, z) for x, y, z in ring_path(0.075, 0.085, 0.016 / 0.62 * 0.62)]
                P = [(sx * 0.062 + x, y - 0.02, z) for x, y, z in ring_path(0.047, 0.053, 0.016)]
                b = Body(P, 0.016, mat, k, seed=15 + sx); bind([(b, 0), (b, 1)], loop=False); bs.append(b)
            string(bs[0], 0, bs[1], 1, 0.04)
        elif kind == 'Angler':
            b = Body(arc_path(0.055, 0.085, math.pi * 0.33, math.pi * 1.67, 0.023, cy=0.02), 0.023, mat, k, seed=17); b.tie(0); b.tie(1); string(b, 0, b, 1, 0.035)
        elif kind == 'Beast': ring(mat, k, 0.095, 0.11, 0.028, net=7, sd=18)
        elif kind == 'Trapper':    # three meats: a garland of four links
            links(mat, k, 4, 0.02, [(-0.16 + 0.32 * t, 0.05 * math.sin(t * 2 * math.pi * 1.0), 0.02) for t in [j / 14 for j in range(15)]], sd=19)
        elif kind == 'Trophy':     # three meats: a big loaf in a full net
            loaf(mat, k, 0.25, 0.04, 5, strings=(math.pi / 2, math.pi * 0.17, math.pi * 0.83), sd=20)
        elif kind == 'GreatBear':  # four: two thick rings, one inside the other, tied together
            b1 = ring(mat, k, 0.105, 0.115, 0.024, net=5, sd=21)
            P = [(x, y - 0.012, z) for x, y, z in arc_path(0.052, 0.058, math.pi * 0.25, math.pi * 1.75, 0.022)]
            b2 = Body(P, 0.022, mat, k, seed=22); b2.tie(0); b2.tie(1); string(b2, 0, b2, 1, 0.012)
        elif kind == 'Tramp':      # four: three short sticks bound at both ends
            bs = []
            for i in range(3):
                y = (i - 1) * 0.033; P = [(-0.1 + 0.2 * t, y * (0.25 + 0.75 * math.sin(math.pi * t)), 0.017) for t in [j / 8 for j in range(9)]]
                bs.append(Body(P, 0.017, mat, k, seed=23 + i))
            bind([(b, 0) for b in bs]); bind([(b, 1) for b in bs], loop=False)
    def join_groups():
        for cu in bpy.data.curves: cu.resolution_u = 3
        for grp in ('tw', 'saus'):
            obs = [o for o in bpy.data.objects if o.name.split('.')[0] == grp]
            if len(obs) < 1: continue
            for o in bpy.data.objects: o.select_set(False)
            for o in obs: o.select_set(True)
            bpy.context.view_layer.objects.active = obs[0]
            if grp == 'tw': bpy.ops.object.convert(target='MESH')
            if len(obs) > 1: bpy.ops.object.join()
    for kind in (want if 'Saus3' in only else []):
        for stage in os.environ.get('STAGES', 'Raw,Hot,Dry').split(','):
            wipe(); random.seed(5)
            shape(kind, stage_mat(kind, stage), 0.86 if stage == 'Dry' else 0.96 if stage == 'Hot' else 1.0)
            # all the twine as one part and all the meat as another: one bake each
            for cu in bpy.data.curves: cu.resolution_u = 3
            for grp in ('tw', 'saus'):
                obs = [o for o in bpy.data.objects if o.name.split('.')[0] == grp]
                if len(obs) < 1: continue
                for o in bpy.data.objects: o.select_set(False)
                for o in obs: o.select_set(True)
                bpy.context.view_layer.objects.active = obs[0]
                if grp == 'tw': bpy.ops.object.convert(target='MESH')
                if len(obs) > 1: bpy.ops.object.join()
            finish('Saus' + kind + stage, 512, curve_u=3, curve_b=2, gain=1.3)


# ---------------- the canning kettle, 0.4.5: stones, ash and logs cut flat at the ground (they hung in the air: the game stands an item on its lowest point)
if 'Kettle' in only:
    wipe(); random.seed(7)
    exec(src[iK:iT])
    for o in list(bpy.data.objects):
        nm = o.name.split('.')[0]
        if nm in ('can', 'canb', 'lab', 'ink', 'tw', 'fire', 'ash', 'ks', 'band'): bpy.data.objects.remove(o, do_unlink=True)   # 11-12 owner: the flat ash stones hung in the air, the big ones were blobs
        elif nm == 'emb': o.data.materials[0] = M_char
    for o in list(bpy.data.objects):
        if o.name.split('.')[0] == 'water': bpy.data.objects.remove(o, do_unlink=True)
    # 11-12 owner: gaps between the hoops and the kettle. Flat hoops sunk into the wall, their radius taken from the wall's own profile
    for z in (0.20, 0.33):
        rw = next(r0 + (r1 - r0) * (z - z0) / (z1 - z0) for (r0, z0), (r1, z1) in zip(kp, kp[1:]) if z0 <= z <= z1)
        lathe('band', 'kettle', [(rw - 0.009, z - 0.013), (rw + 0.005, z - 0.011), (rw + 0.0055, z), (rw + 0.005, z + 0.011), (rw - 0.009, z + 0.013)], (0, 0, KZ), M_ironc, seg=56)
    # stones of the hearth anew: finer, knobbly, flat-bottomed, each its own grey; the iron is lighter so that its rust and soot are seen in the game
    M_st = [mat_rich('hs%d' % i, [(0.28, tuple(x * k for x in (0.10, 0.10, 0.105))), (0.5, tuple(x * k for x in (0.30, 0.295, 0.285))), (0.72, tuple(x * k for x in (0.50, 0.48, 0.45)))],
                     scale=7, rough=0.95, bump=1.4, grime=1.0, fine=55, soot_below=None) for i, k in enumerate((1.0, 0.72, 0.9, 0.6))]
    for i in range(4):
        a_ = math.pi / 4 + i * math.pi / 2 + random.uniform(-.1, .1)
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=4, radius=0.165)
        sq = (random.uniform(.9, 1.05), random.uniform(.8, .95), 1.15); ph = Vector((random.uniform(0, 50), random.uniform(0, 50), random.uniform(0, 50)))
        for v in bm.verts:
            d = v.co.normalized()
            k = 1 + 0.16 * MN.noise(d * 1.6 + ph) + 0.07 * MN.noise(d * 4.5 + ph) + 0.025 * MN.noise(d * 12 + ph)
            v.co = Vector((v.co.x * sq[0] * k, v.co.y * sq[1] * k, v.co.z * sq[2] * k))
        me = bpy.data.meshes.new('ks'); bm.to_mesh(me); bm.free()
        for p_ in me.polygons: p_.use_smooth = True
        o = bpy.data.objects.new('ks', me); C['kettle'].objects.link(o); o.location = (0.275 * math.cos(a_), 0.275 * math.sin(a_), 0.15); o.rotation_euler = (0, 0, random.uniform(0, 6))
        me.materials.append(M_st[i])
    # a few small stones wedged between, sunk into the floor so that their flat cut is their base
    for i in range(5):
        a_ = i * 2 * math.pi / 5 + 0.9; rr = random.uniform(0.05, 0.075)
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=3, radius=rr); ph = Vector((i * 7.3, 2.1, 5.5))
        for v in bm.verts:
            d = v.co.normalized(); k = 1 + 0.2 * MN.noise(d * 2 + ph) + 0.06 * MN.noise(d * 6 + ph); v.co = Vector((v.co.x * k * 1.2, v.co.y * k, v.co.z * k * 0.8))
        me = bpy.data.meshes.new('kp'); bm.to_mesh(me); bm.free()
        for p_ in me.polygons: p_.use_smooth = True
        o = bpy.data.objects.new('kp', me); C['kettle'].objects.link(o); o.location = (0.40 * math.cos(a_), 0.40 * math.sin(a_), rr * 0.45); o.rotation_euler = (0, 0, random.uniform(0, 6))
        me.materials.append(M_st[(i + 1) % 4])
    M_iron2 = mat_rich('iron2', [(0.30, (0.035, 0.036, 0.04)), (0.5, (0.085, 0.08, 0.078)), (0.66, (0.22, 0.10, 0.045)), (0.8, (0.33, 0.15, 0.06))], scale=6, rough=0.75, bump=1.0, grime=0.9, fine=60, soot_below=(0.02, 0.26))
    M_ironc2 = mat_rich('ironc2', [(0.35, (0.03, 0.03, 0.034)), (0.7, (0.13, 0.11, 0.10))], scale=18, rough=0.6, bump=0.5, grime=0.9)
    for o in bpy.data.objects:
        if o.type in ('MESH', 'CURVE') and len(o.data.materials):
            if o.data.materials[0] == M_iron: o.data.materials[0] = M_iron2
            elif o.data.materials[0] == M_ironc: o.data.materials[0] = M_ironc2
    M_iron = M_iron2; M_ironc = M_ironc2
    # 11-12 owner: the kettle is shut with a lid. A shallow iron dome over the rim with a ring handle.
    lid = lathe('lid', 'kettle', [(0, 0.515), (0.06, 0.512), (0.16, 0.498), (0.27, 0.474), (0.345, 0.452), (0.364, 0.447), (0.368, 0.438), (0.360, 0.432)], (0, 0, KZ), M_iron, seg=56, solid=0.01); rough_up(lid, 0.005, 0.16)
    lathe('lidrim', 'kettle', [(0.362, 0.436), (0.371, 0.442), (0.362, 0.449)], (0, 0, KZ), M_ironc, seg=56)
    tube('lidh', 'kettle', [(-0.07, 0, KZ + 0.508), (-0.065, 0, KZ + 0.545), (-0.03, 0, KZ + 0.572), (0.03, 0, KZ + 0.572), (0.065, 0, KZ + 0.545), (0.07, 0, KZ + 0.508)], 0.009, M_ironc, res=4)
    bpy.context.view_layer.update()
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        if o.data.users > 1: o.data = o.data.copy()
        M = o.matrix_world.copy(); Mi = M.inverted()
        for v in o.data.vertices:
            w = M @ v.co
            if w.z < 0: w.z = 0.0; v.co = Mi @ w
    finish('Kettle', 1024, margin=0.006, paint=True, bmargin=4, cube=('ks', 'kp'))   # a wide bake margin ran over the neighbouring islands of the stones: patches (11-12 owner)

# ---------------- the sausage table, 0.4.5: the grinder stands on the top, the sausages on it are the new ones, the game's painted look
if 'Table' in only:
    wipe(); random.seed(7)
    t = src[iT:iL]
    a = t.index('def hang_ring'); b = t.index('ZT=TH+0.021'); t = t[:a] + t[b:]
    a = t.index('# meat grinder clamped'); b = t.index('# cutting board with meat'); t = t[:a] + t[b:]      # 11-12 owner: no grinder at all
    exec(t)
    for o in list(bpy.data.objects):   # 11-12 owner: the knife came out very bad, the meat was plastic
        nm = o.name.split('.')[0]
        if nm in ('blade', 'haft'): bpy.data.objects.remove(o, do_unlink=True)
    M_raw = mat_saus('rawmeat', (0.34, 0.045, 0.04), (0.74, 0.58, 0.50), 0.24, chunk=0.45)
    M_minc = mat_saus('minced', (0.44, 0.10, 0.085), (0.80, 0.66, 0.60), 0.42, chunk=0.6)
    for o in bpy.data.objects:
        nm = o.name.split('.')[0]
        if nm == 'mt': o.data.materials[0] = M_raw; rough_up(o, 0.012, 0.03)
        elif nm == 'mince': o.data.materials[0] = M_minc
    PZ = TH + 0.90
    for i, (x, kind, stage) in enumerate(((-0.40, 'Taiga', 'Dry'), (-0.24, 'Hunt', 'Dry'), (-0.08, 'Taiga', 'Hot'), (0.08, 'Beast', 'Dry'), (0.24, 'WhiteFish', 'Hot'), (0.40, 'Taiga', 'Dry'))):
        r = 0.019; P = ring_path(0.058, 0.115 + 0.012 * (i % 3), r); top = max(q[1] for q in P)
        b = Body(P, r, stage_mat(kind, stage), 0.86 if stage == 'Dry' else 0.96, flat=1.0, seed=40 + i)
        b.ob.rotation_euler = (math.pi / 2, 0, 0); b.ob.location = (TX + x, 0.28 + r, PZ - 0.03 - top)
        tube('tw', 'table', [(TX + x, 0.28 + 0.022 * math.sin(a), PZ - 0.012 + (0.022 if math.cos(a) > 0 else 0.034) * math.cos(a)) for a in [j * 2 * math.pi / 12 for j in range(12)]], 0.0016, M_jute, res=2, cyclic=True)
    r = 0.022
    b = Body([(TX - 0.30 + 0.56 * t, -0.24 + 0.035 * math.sin(t * 7), ZT + r) for t in [j / 12 for j in range(13)]], r, stage_mat('Game', 'Raw'), 1.0, pinches=[j / 5 for j in range(1, 5)], seed=50)
    for q in b.pinches: b.wrap(q, 2)
    b.tie(0); b.tie(1)
    r = 0.017
    b = Body([(TX + 0.44 + x, 0.10 + y, ZT + r) for x, y, z in spiral(r, 1.4, 0.022, 0.075, 28)], r, stage_mat('Taiga', 'Dry'), 0.86, seed=51); b.tie(0); b.tie(1)
    r = 0.024
    for i in range(3):
        b = Body([(TX + 0.12 + 0.065 * i + 0.004 * math.sin(j), -0.10 + 0.19 * j / 6, ZT + r) for j in range(7)], r, stage_mat('Bass', 'Hot'), 0.96, seed=52 + i, lump=0.06); b.tie(0); b.tie(1)
    join_groups()
    for o in bpy.data.objects:
        if o.parent is None: o.location.x -= TX
    finish('Table', 2048, curve_u=3, curve_b=2, margin=0.004, paint=True, boost={'saus': 2.2, 'mt': 2.2, 'mince': 2.0, 'tw': 1.5})
