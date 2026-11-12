# Свои анимации для персонажей CoopByEMPEROR: жесты, разделка, еда, питьё.
# Строятся по ключевым кадрам рук (двухзвенная IK на цель кисти + направление ладони) поверх позы игры
# и дописываются в characters.bin как обычные клипы (только кости рук, спины и головы).
# usage: python3 -I synth.py <characters.bin in> <characters.bin out> [preview.png]
import sys, zlib, struct, math, numpy as np
SRC, DST = sys.argv[1], sys.argv[2]
PREVIEW = sys.argv[3] if len(sys.argv) > 3 else None
raw = zlib.decompress(open(SRC, 'rb').read(), -15)
p = 0
def i():
    global p; v = struct.unpack_from('<i', raw, p)[0]; p += 4; return v
def f(n):
    global p; v = np.frombuffer(raw, dtype='<f4', count=n, offset=p).copy(); p += 4 * n; return v
def s():
    n = i(); global p; v = raw[p:p + n].decode(); p += n; return v
assert raw[:4] == b'TLDC'; p = 4; ver = i()
nclips_pos = p
nclips = i()
clips = {}
for _ in range(nclips):
    name = s(); fps = f(1)[0]; fr = i(); nt = i(); tr = {}
    for _ in range(nt):
        path = s(); fl = i()
        rot = f(fr * 4).reshape(fr, 4) if fl & 1 else None
        pos = f(fr * 3).reshape(fr, 3) if fl & 2 else None
        tr[path] = (rot, pos)
    clips[name] = (fps, fr, tr)
clips_end = p
# первый персонаж: скелет
i(); s(); s(); i(); n = i()
T = [(s(), i(), f(10)) for _ in range(n)]
paths = []
for k, (tn, par, l) in enumerate(T):
    paths.append('' if par < 0 else (tn if paths[par] == '' else paths[par] + '/' + tn))
idx = {tn: k for k, (tn, _, _) in enumerate(T)}
# скин первого персонажа для картинок
rt = i(); nv = i(); V = f(nv * 3).reshape(nv, 3); f(nv * 3); f(nv * 2)
BI = np.zeros((nv, 4), int); BW = np.zeros((nv, 4))
for k in range(nv):
    BI[k] = np.frombuffer(raw, dtype='u1', count=4, offset=p); p += 4; BW[k] = f(4)
nb = i(); bones = [i() for _ in range(nb)]; BP = [f(16).reshape(4, 4) for _ in range(nb)]

def quat(q):
    x, y, z, w = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)], [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)], [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
def mat2q(m):
    t = m[0,0] + m[1,1] + m[2,2]
    if t > 0:
        S = math.sqrt(t + 1.0) * 2; w = 0.25 * S; x = (m[2,1]-m[1,2]) / S; y = (m[0,2]-m[2,0]) / S; z = (m[1,0]-m[0,1]) / S
    elif m[0,0] > m[1,1] and m[0,0] > m[2,2]:
        S = math.sqrt(1.0 + m[0,0] - m[1,1] - m[2,2]) * 2; w = (m[2,1]-m[1,2]) / S; x = 0.25 * S; y = (m[0,1]+m[1,0]) / S; z = (m[0,2]+m[2,0]) / S
    elif m[1,1] > m[2,2]:
        S = math.sqrt(1.0 + m[1,1] - m[0,0] - m[2,2]) * 2; w = (m[0,2]-m[2,0]) / S; x = (m[0,1]+m[1,0]) / S; y = 0.25 * S; z = (m[1,2]+m[2,1]) / S
    else:
        S = math.sqrt(1.0 + m[2,2] - m[0,0] - m[1,1]) * 2; w = (m[1,0]-m[0,1]) / S; x = (m[0,2]+m[2,0]) / S; y = (m[1,2]+m[2,1]) / S; z = 0.25 * S
    q = np.array([x, y, z, w]); return q / np.linalg.norm(q)

def base_locals(clip, frame=0):
    fps, fr, tr = clips[clip]
    L = []
    for k, (tn, par, l) in enumerate(T):
        pos = l[0:3].copy(); rot = l[3:7].copy(); sc = l[7:10]
        if paths[k] in tr:
            r_, p_ = tr[paths[k]]
            if r_ is not None: rot = r_[frame % fr]
            if p_ is not None and not paths[k].endswith('Origin_jnt'): pos = p_[frame % fr]
        L.append([pos, quat(rot), sc])
    return L
def world(L):
    W = [None] * n
    for k, (tn, par, l) in enumerate(T):
        M = np.eye(4); M[:3,:3] = L[k][1] * L[k][2]; M[:3,3] = L[k][0]
        W[k] = M if par < 0 else W[par] @ M
    return W
def norm(v): v = np.asarray(v, float); return v / np.linalg.norm(v)
def align(a, b):
    a = norm(a); b = norm(b); v = np.cross(a, b); c = float(np.dot(a, b))
    if c < -0.9999: return -np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + vx + vx @ vx / (1 + c)
def rotx(d):
    a = math.radians(d); c, s_ = math.cos(a), math.sin(a); return np.array([[1,0,0],[0,c,-s_],[0,s_,c]])
def set_world_rot(L, W, k, Rw):
    par = T[k][1]
    PW = W[par][:3,:3]
    sc = L[k][2]
    # мировой поворот кости = PW_rot * local_rot (масштаб у этих костей 1)
    PWn = PW / np.linalg.norm(PW, axis=0)
    L[k][1] = PWn.T @ Rw
def rotate_world(L, k, R):
    W = world(L)
    Wk = W[k][:3,:3] / np.linalg.norm(W[k][:3,:3], axis=0)
    set_world_rot(L, W, k, R @ Wk)

def arm(L, side, target, pole, palm=None, finger=None):
    S_, E_, H_ = side + 'UpArm_jnt', side + 'Elbow_jnt', side + 'Hand_jnt'
    W = world(L)
    sp, ep, hp = W[idx[S_]][:3,3], W[idx[E_]][:3,3], W[idx[H_]][:3,3]
    l1, l2 = np.linalg.norm(ep - sp), np.linalg.norm(hp - ep)
    t = np.asarray(target, float)
    d = t - sp; dist = np.linalg.norm(d)
    dist = min(dist, (l1 + l2) * 0.999); dist = max(dist, abs(l1 - l2) * 1.01)
    dn = norm(d)
    a = (l1*l1 + dist*dist - l2*l2) / (2 * l1 * dist)
    h = math.sqrt(max(0.0, l1*l1 - (a)**2 * 0 - (a*l1)**2)) if False else math.sqrt(max(0.0, 1 - a*a)) * l1
    pl = norm(np.asarray(pole, float) - np.dot(pole, dn) * dn)
    elbow = sp + dn * a * l1 + pl * h
    hand = sp + dn * dist
    rotate_world(L, idx[S_], align(ep - sp, elbow - sp))
    W = world(L); ep2, hp2 = W[idx[E_]][:3,3], W[idx[H_]][:3,3]
    rotate_world(L, idx[E_], align(hp2 - ep2, hand - elbow))
    if palm is not None:
        W = world(L)
        fd = norm(finger) if finger is not None else norm(hand - elbow)
        y = -fd
        x = norm(np.asarray(palm, float) - np.dot(palm, y) * y)
        if side == 'Left': x = -x   # у левой кисти ладонь в другую сторону
        z = np.cross(x, y)
        set_world_rot(L, W, idx[H_], np.stack([x, y, z], 1))

def fingers(L, side, src, only=None, skip=None):
    fps, fr, tr = clips[src]
    for k, (tn, par, l) in enumerate(T):
        if not tn.startswith(side) or paths[k] not in tr or tr[paths[k]][0] is None: continue
        if 'Hand_jnt' in tn: continue
        if '/' + side + 'Hand_jnt/' not in paths[k]: continue
        if only and not any(o in tn for o in only): continue
        if skip and any(o in tn for o in skip): continue
        L[k][1] = quat(tr[paths[k]][0][0])

def lerp(a, b, u): return np.asarray(a, float) * (1 - u) + np.asarray(b, float) * u
def smooth(u): u = max(0.0, min(1.0, u)); return u * u * (3 - 2 * u)

TRACK_BONES = None
def build(name, base, dur, fn, bones, fps=30.0):
    frames = max(2, int(round(dur * fps)) + 1)
    out = {b: [] for b in bones}
    for fi in range(frames):
        t = fi / fps
        L = base_locals(base, 0)
        fn(L, t)
        for b in bones: out[b].append(mat2q(L[idx[b]][1]))
    return name, fps, frames, out

# Плечо, локоть и кисть в позе «стоит»: где что.
L0 = base_locals('idle_m'); W0 = world(L0)
SH_R = W0[idx['RightUpArm_jnt']][:3,3]; SH_L = W0[idx['LeftUpArm_jnt']][:3,3]
HEAD = W0[idx['Head_jnt']][:3,3]
MOUTH = HEAD + np.array([0.0, -0.02, 0.13])
print('shoulder R', SH_R.round(2), 'head', HEAD.round(2))
ARM_R = ['RightUpArm_jnt', 'RightElbow_jnt', 'RightHand_jnt'] + [t for t, _, _ in T if t.startswith('Right') and any(x in t for x in ('Index','Middle','Ring','Pinky','Thumb','InPinky','InThumb'))]
ARM_L = [b.replace('Right', 'Left') for b in ARM_R]
UPPER = ARM_R + ARM_L + ['Spine2_jnt', 'Spine3_jnt', 'Head_jnt']

def wave(L, t):
    a = math.radians(28 * math.sin(2 * math.pi * t / 0.8))
    tgt = SH_R + np.array([0.28 + 0.22 * math.sin(a), 0.42, 0.18])
    arm(L, 'Right', tgt, pole=[1, -0.6, -0.5], palm=[0, 0, 1], finger=[math.sin(a) * 0.6, 1, 0.1])
    fingers(L, 'Right', 'idle_m')
def beckon(L, t):
    u = 0.5 + 0.5 * math.sin(2 * math.pi * t / 0.9)
    tgt = SH_R + lerp([0.10, -0.25, 0.50], [0.08, -0.05, 0.30], u)
    arm(L, 'Right', tgt, pole=[0.8, -1, 0], palm=[0, 1, 0], finger=lerp([0, 0.1, 1], [0, 1, -0.2], u))
    fingers(L, 'Right', 'idle_m')
def point(L, t):
    tgt = SH_R + np.array([0.05, 0.02, 0.58])
    arm(L, 'Right', tgt, pole=[1, -1, 0], palm=[-1, 0, 0], finger=[0, 0, 1])
    fingers(L, 'Right', 'fist_m', skip=('Index',))
def stop(L, t):
    tgt = SH_R + np.array([0.08, 0.12, 0.45])
    arm(L, 'Right', tgt, pole=[1, -1, -0.3], palm=[0, 0, 1], finger=[0, 1, 0.15])
    fingers(L, 'Right', 'idle_m')
def eat(L, t):
    # 3 с: рука у груди → ко рту → держит → обратно
    ph = (t % 3.0) / 3.0
    u = smooth(ph / 0.3) if ph < 0.3 else 1.0 if ph < 0.6 else 1 - smooth((ph - 0.6) / 0.3) if ph < 0.9 else 0.0
    rest = SH_R + np.array([0.02, -0.38, 0.30])
    tgt = lerp(rest, MOUTH + np.array([0.02, -0.03, 0.06]), u)
    arm(L, 'Right', tgt, pole=[1, -1, -0.4], palm=lerp([-1, 0.3, 0], [-0.6, 0, -0.8], u), finger=lerp([0, 0.2, 1], [-0.7, 0.5, 0.2], u))
    fingers(L, 'Right', 'fist_m')
    rotate_world(L, idx['Head_jnt'], rotx(8 * u))
def drink(L, t):
    ph = (t % 3.5) / 3.5
    u = smooth(ph / 0.25) if ph < 0.25 else 1.0 if ph < 0.7 else 1 - smooth((ph - 0.7) / 0.25) if ph < 0.95 else 0.0
    rest = SH_R + np.array([0.02, -0.36, 0.32])
    tgt = lerp(rest, MOUTH + np.array([0.03, 0.0, 0.08]), u)
    arm(L, 'Right', tgt, pole=[1, -1, -0.4], palm=lerp([-1, 0.3, 0], [-0.7, -0.3, -0.6], u), finger=lerp([0, 0.3, 1], [-0.6, 0.8, 0.1], u))
    fingers(L, 'Right', 'fist_m')
    rotate_world(L, idx['Head_jnt'], rotx(-22 * u))
def harvest(L, t):
    # Сидя у туши: наклон вперёд, левая рука придерживает, правая режет (вперёд-назад).
    rotate_world(L, idx['Spine2_jnt'], rotx(18))
    rotate_world(L, idx['Head_jnt'], rotx(15))
    W = world(L)
    shr = W[idx['RightUpArm_jnt']][:3,3]; shl = W[idx['LeftUpArm_jnt']][:3,3]
    hip = W[idx['Hips_jnt']][:3,3]
    ground = np.array([0, hip[1] - 0.05, 0])
    saw = math.sin(2 * math.pi * t / 0.8)
    tr_ = np.array([shr[0] - 0.05, ground[1] + 0.12, shr[2] + 0.48 + 0.10 * saw])
    tl_ = np.array([shl[0] + 0.08, ground[1] + 0.10, shl[2] + 0.50])
    arm(L, 'Right', tr_, pole=[1, 0, -1], palm=[-1, 0, 0], finger=[0, -0.6, 1])
    arm(L, 'Left', tl_, pole=[-1, 0, -1], palm=[0, -1, 0], finger=[0.2, -0.8, 0.6])
    fingers(L, 'Right', 'fist_m'); fingers(L, 'Left', 'idle_m')

NEW = [
    build('wave', 'idle_m', 1.6, wave, ARM_R),
    build('beckon', 'idle_m', 1.8, beckon, ARM_R),
    build('point', 'idle_m', 1.0, point, ARM_R),
    build('stop', 'idle_m', 1.0, stop, ARM_R),
    build('eat', 'idle_m', 3.0, eat, ARM_R + ['Head_jnt']),
    build('drink', 'idle_m', 3.5, drink, ARM_R + ['Head_jnt']),
    build('harvest', 'sit', 1.6, harvest, UPPER),
]

out = bytearray()
def w_i(v): out.extend(struct.pack('<i', v))
def w_f(v): out.extend(struct.pack('<f', v))
def w_s(s_):
    b = s_.encode(); w_i(len(b)); out.extend(b)
keep = [c for c in clips if c not in {x[0] for x in NEW}]
out.extend(raw[:nclips_pos]); w_i(len(keep) + len(NEW))
# старые клипы как есть (кроме заменяемых)
q = nclips_pos + 4
for _ in range(nclips):
    st = q
    ln = struct.unpack_from('<i', raw, q)[0]; nm = raw[q+4:q+4+ln].decode(); q += 4 + ln
    fr = struct.unpack_from('<i', raw, q + 4)[0]; nt = struct.unpack_from('<i', raw, q + 8)[0]; q += 12
    for _ in range(nt):
        ln = struct.unpack_from('<i', raw, q)[0]; q += 4 + ln
        fl = struct.unpack_from('<i', raw, q)[0]; q += 4
        if fl & 1: q += fr * 16
        if fl & 2: q += fr * 12
    if nm in keep: out.extend(raw[st:q])
assert q == clips_end
for name, fps, frames, tracks in NEW:
    w_s(name); w_f(fps); w_i(frames); w_i(len(tracks))
    for b, qs in tracks.items():
        w_s(paths[idx[b]]); w_i(1)
        out.extend(np.asarray(qs, '<f4').tobytes())
    print('clip', name, 'frames', frames, 'tracks', len(tracks))
out.extend(raw[clips_end:])
c = zlib.compressobj(9, zlib.DEFLATED, -15)
open(DST, 'wb').write(c.compress(bytes(out)) + c.flush())

if PREVIEW:
    from PIL import Image, ImageDraw
    def skin(W):
        o = np.zeros((nv, 3)); Vh = np.c_[V, np.ones(nv)]
        mats = np.array([W[bones[b]] @ BP[b] for b in range(nb)])
        for j in range(4): o += np.einsum('nij,nj->ni', mats[BI[:, j]], Vh)[:, :3] * BW[:, j:j+1]
        return o
    shots = [('wave', 'idle_m', wave, 0.0), ('wave', 'idle_m', wave, 0.2), ('beckon', 'idle_m', beckon, 0.0), ('beckon', 'idle_m', beckon, 0.45),
             ('point', 'idle_m', point, 0), ('stop', 'idle_m', stop, 0), ('eat', 'idle_m', eat, 0.1), ('eat', 'idle_m', eat, 1.2),
             ('drink', 'idle_m', drink, 1.5), ('harvest', 'sit', harvest, 0.0), ('harvest', 'sit', harvest, 0.4)]
    img = Image.new('RGB', (300 * len(shots), 820), 'white'); d = ImageDraw.Draw(img)
    for ci, (nm, base, fn, t) in enumerate(shots):
        L = base_locals(base); fn(L, t); S = skin(world(L))
        for row, (a, b) in enumerate([(2, 1), (0, 1)]):
            ox = ci * 300 + 150; oy = 400 + row * 410
            for pt in S[::3]: d.point((ox + pt[a] * 200 * (1 if a == 0 else 1), oy - pt[b] * 200), fill=(90, 90, 160))
        d.text((ci * 300 + 5, 5), f'{nm} t={t} side(z>)/front(x>)', fill='black')
    img.save(PREVIEW)
