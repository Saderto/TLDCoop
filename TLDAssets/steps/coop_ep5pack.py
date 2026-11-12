# Builds CoopByEMPEROR characters.bin from the game's Episode 5 (WINTERMUTE) bundles.
# usage: python3 -I ep5pack.py <dir with bundles> <out file>
import sys, struct, zlib, math, bisect
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from PIL import Image

BDIR, OUT = sys.argv[1], sys.argv[2]
CHARS = [
    # (renderer GO name, id, display name, female)
    ('Astrid_outdoor_Ep5', 'astrid', 'Астрид', True),
    ('Mackenzie_LO', 'mackenzie', 'Маккензи', False),
    ('Jeremiah_Ep5', 'jeremiah', 'Иеремия', False),
    ('JaneYang_Outdoor', 'jane', 'Джейн', True),
    ('Terry_EP5', 'terry', 'Терри', False),
    ('MESH_JACE', 'jace', 'Джейс', True),
    ('geo_Dr_Atwood_Ep5', 'atwood', 'Доктор Этвуд', False),
    ('Convict_C', 'convict', 'Заключённый', False),
]
CLIPS = {  # our name: (male clip, female clip)
    'idle': ('NPC_M_Idle_01', 'NPC_F_Idle_01'),
    'walk': ('NPC_M_Walk', 'NPC_F_Walk'),
    'run': ('NPC_M_Run', 'NPC_F_Run'),
    'sit': ('NPC_Sit_Ground_Idle_01', 'NPC_Sit_Ground_Idle_01'),
    # с вещами в руках и прочее (у ружья только мужские клипы, скелет общий)
    'rifle': ('NPC_M_Idle_Rifle_01', 'NPC_M_Idle_Rifle_01'),
    'rifle_aim': ('NPC_M_Idle_Rifle_Aim_0', 'NPC_M_Idle_Rifle_Aim_0'),
    'rifle_fire': ('NPC_M_Idle_Rifle_Fire_0', 'NPC_M_Idle_Rifle_Fire_0'),
    'lantern': ('NPC_F_Idle_01_Lantern', 'NPC_F_Idle_01_Lantern'),
    'throw': ('NPC_Item_Throw', 'NPC_Item_Throw'),
    'chair': ('NPC_Sit_Chair_Idle_02', 'NPC_Sit_Chair_Idle_02'),
    'fist': ('NPC_M_Hands_Fist', 'NPC_F_Hands_Fist'),
}
SKIP_MATS = ('Lashes', 'Tears', 'Lenses', 'Glasses')
MAXTEX = 512

envs = [UnityPy.load(BDIR + '/0fdef26068bbfb5471e1d85601e34df1.bundle'),
        UnityPy.load(BDIR + '/e4a1318e45f0268ca7ebdba8f55264da.bundle')]

out = bytearray()
def w_i(v): out.extend(struct.pack('<i', v))
def w_f(v): out.extend(struct.pack('<f', v))
def w_fs(vs): out.extend(struct.pack('<%df' % len(vs), *vs))
def w_s(s):
    b = s.encode('utf-8'); w_i(len(b)); out.extend(b)

# ---------- clips ----------
def clip_data(name):
    for env in envs:
        for o in env.objects:
            if o.type.name == 'AnimationClip' and o.peek_name() == name:
                return o.read_typetree()
    return None

def ncomp(b):
    if b['typeID'] == 4: return {1: 3, 2: 4, 3: 3, 4: 3}.get(b['attribute'], 1)
    return 1

def decode_clip(d):
    c = d['m_MuscleClip']['m_Clip']
    c = c.get('data', c)
    s, de, co = c['m_StreamedClip'], c['m_DenseClip'], c['m_ConstantClip']
    nS, nD = s['curveCount'], de['m_CurveCount']
    raw = struct.pack('<%dI' % len(s['data']), *s['data'])
    keys = [[] for _ in range(nS)]   # per curve: list of (time, coeffs)
    p = 0
    while p < len(raw):
        t, n = struct.unpack_from('<fI', raw, p); p += 8
        for _ in range(n):
            idx = struct.unpack_from('<I', raw, p)[0]
            cf = struct.unpack_from('<4f', raw, p + 4); p += 20
            if idx < nS: keys[idx].append((t, cf))
    times = [[k[0] for k in ks] for ks in keys]
    stop = d['m_MuscleClip']['m_StopTime']; start = d['m_MuscleClip']['m_StartTime']
    def ev(ci, t):
        if ci < nS:
            ks = keys[ci]
            if not ks: return 0.0
            i = bisect.bisect_right(times[ci], t) - 1
            if i < 0: i = 0
            kt, (a, b, cc, dd) = ks[i]
            if kt < -1e30: dt = 0.0
            else: dt = t - kt
            return ((a * dt + b) * dt + cc) * dt + dd
        ci -= nS
        if ci < nD:
            fr = (t - de['m_BeginTime']) * de['m_SampleRate']
            f0 = max(0, min(de['m_FrameCount'] - 1, int(math.floor(fr))))
            f1 = min(de['m_FrameCount'] - 1, f0 + 1)
            u = max(0.0, min(1.0, fr - f0))
            arr = de['m_SampleArray']
            v0 = arr[f0 * nD + ci]; v1 = arr[f1 * nD + ci]
            return v0 + (v1 - v0) * u
        ci -= nD
        return co['data'][ci]
    return ev, start, stop

def bone_paths(root):
    paths = {}
    stack = [(root, '')]
    while stack:
        t, p = stack.pop()
        paths[zlib.crc32(p.encode())] = p
        for ch in t.m_Children:
            c = ch.read()
            n = c.m_GameObject.read().m_Name
            stack.append((c, (p + '/' + n) if p else n))
    return paths

def write_clip(cname, d, paths, fps=30.0):
    ev, start, stop = decode_clip(d)
    gb = d['m_ClipBindingConstant']['genericBindings']
    tracks = {}  # path -> {'rot':ci, 'pos':ci}
    ci = 0
    for b in gb:
        n = ncomp(b)
        if b['typeID'] == 4 and b['path'] in paths and b['attribute'] in (1, 2):
            tracks.setdefault(paths[b['path']], {})['pos' if b['attribute'] == 1 else 'rot'] = ci
        ci += n
    dur = max(1.0 / fps, stop - start)
    frames = max(2, int(round(dur * fps)) + 1)
    w_s(cname); w_f(fps); w_i(frames); w_i(len(tracks))
    for path, tr in tracks.items():
        w_s(path)
        flags = (1 if 'rot' in tr else 0) | (2 if 'pos' in tr else 0)
        w_i(flags)
        if 'rot' in tr:
            vals = []
            for f in range(frames):
                t = start + min(dur, f / fps)
                q = [ev(tr['rot'] + k, t) for k in range(4)]
                l = math.sqrt(sum(x * x for x in q)) or 1.0
                vals += [x / l for x in q]
            w_fs(vals)
        if 'pos' in tr:
            vals = []
            for f in range(frames):
                t = start + min(dur, f / fps)
                vals += [ev(tr['pos'] + k, t) for k in range(3)]
            w_fs(vals)
    return len(tracks), frames

# ---------- characters ----------
def find_renderer(name):
    for o in envs[0].objects:
        if o.type.name == 'SkinnedMeshRenderer':
            d = o.read()
            if d.m_GameObject.read().m_Name == name: return d
    return None

def texture_rgba(tex):
    img = tex.image.convert('RGBA')
    w, h = img.size
    sc = min(1.0, MAXTEX / max(w, h))
    if sc < 1.0: img = img.resize((max(4, int(w * sc)), max(4, int(h * sc))), Image.LANCZOS)
    # Unity textures go bottom-up
    img = img.transpose(Image.FLIP_TOP_BOTTOM)
    return img

out.extend(b'TLDC'); w_i(1)
# clips first: need any character's bone paths -> use first char root
first = find_renderer(CHARS[0][0])
t0 = first.m_GameObject.read().m_Transform.read()
while t0.m_Father and t0.m_Father.path_id: t0 = t0.m_Father.read()
paths = bone_paths(t0)
cliplist = []
for our, (m, f) in CLIPS.items():
    for variant, src in (('m', m), ('f', f)):
        if variant == 'f' and f == m: continue
        d = clip_data(src)
        if d is None: print('no clip', src); continue
        cliplist.append((our + ('' if f == m else '_' + variant), d))
cliplist = [c for c in cliplist if c[0] != 'lie']
w_i(len(cliplist))
for cname, d in cliplist:
    n, fr = write_clip(cname, d, paths)
    print('clip', cname, 'tracks', n, 'frames', fr)

w_i(len(CHARS))
for rname, cid, disp, female in CHARS:
    r = find_renderer(rname)
    go = r.m_GameObject.read()
    t = go.m_Transform.read()
    root = t
    while root.m_Father and root.m_Father.path_id: root = root.m_Father.read()
    # transforms list (BFS) with parents
    tlist, index = [], {}
    queue = [(root, -1)]
    while queue:
        x, par = queue.pop(0)
        index[x.object_reader.path_id] = len(tlist)
        tlist.append((x, par))
        for ch in x.m_Children: queue.append((ch.read(), len(tlist) - 1))
    w_s(cid); w_s(disp); w_i(1 if female else 0)
    w_i(len(tlist))
    for x, par in tlist:
        w_s(x.m_GameObject.read().m_Name); w_i(par)
        p, q, s = x.m_LocalPosition, x.m_LocalRotation, x.m_LocalScale
        w_fs([p.x, p.y, p.z, q.x, q.y, q.z, q.w, s.x, s.y, s.z])
    w_i(index[t.object_reader.path_id])   # renderer's transform
    mesh = r.m_Mesh.read()
    h = MeshHandler(mesh); h.process()
    nv = len(h.m_Vertices)
    w_i(nv)
    w_fs([c for v in h.m_Vertices for c in v[:3]])
    w_fs([c for v in h.m_Normals for c in v[:3]])
    w_fs([c for v in h.m_UV0 for c in v[:2]])
    for bi, bw in zip(h.m_BoneIndices, h.m_BoneWeights):
        out.extend(struct.pack('<4B', *[min(255, int(i)) for i in bi[:4]]))
        w_fs(list(bw[:4]))
    bones = r.m_Bones
    w_i(len(bones))
    for b in bones:
        w_i(index.get(b.path_id, -1))
    for m in mesh.m_BindPose:
        w_fs([m.e00, m.e01, m.e02, m.e03, m.e10, m.e11, m.e12, m.e13, m.e20, m.e21, m.e22, m.e23, m.e30, m.e31, m.e32, m.e33])
    tris = h.get_triangles()
    mats = list(r.m_Materials)
    subs = []
    texcache = {}
    texlist = []
    for si, tri in enumerate(tris):
        mat = mats[si].read() if si < len(mats) else None
        if mat is None or any(k in mat.m_Name for k in SKIP_MATS): continue
        texid = -1
        for k, v in mat.m_SavedProperties.m_TexEnvs:
            if k == '_MainTex' and v.m_Texture.path_id:
                pid = v.m_Texture.path_id
                if pid not in texcache:
                    try:
                        img = texture_rgba(v.m_Texture.read())
                        texcache[pid] = len(texlist); texlist.append((img, 'Hair' in mat.m_Name))
                    except Exception as e:
                        print('  tex fail', mat.m_Name, e); texcache[pid] = -1
                texid = texcache[pid]
        subs.append(([i for tr in tri for i in tr], texid, mat.m_Name))
    w_i(len(subs))
    for idx, texid, mn in subs:
        w_s(mn); w_i(texid); w_i(len(idx)); out.extend(struct.pack('<%di' % len(idx), *idx))
    w_i(len(texlist))
    for img, alpha in texlist:
        wdt, hgt = img.size
        w_i(wdt); w_i(hgt); w_i(1 if alpha else 0)
        data = img.tobytes() if alpha else img.convert('RGB').tobytes()
        out.extend(data)
    print('char', cid, 'transforms', len(tlist), 'verts', nv, 'bones', len(bones), 'subs', len(subs), 'tex', len(texlist))

comp = zlib.compressobj(9, zlib.DEFLATED, -15)
data = comp.compress(bytes(out)) + comp.flush()
open(OUT, 'wb').write(data)
print('raw', len(out), 'packed', len(data))
