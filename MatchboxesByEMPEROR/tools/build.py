"""Builds the mod's data: meshes/<kind>.fpmesh, textures/<id>.jpg (1024 atlas), labels/<id>.jpg (flat label for the album),
icons/<id>.png (256 inventory icon), icons/coll_*.png (journal art). Usage: build.py <dist Mods/MatchboxesByEMPEROR dir> [ids...]"""
import os, sys, struct, math
import numpy as np
from PIL import Image, ImageFilter, ImageDraw
from catalog import KINDS, labels
import labels as LB

# atlas regions in pixels (x0, y0, x1, y1)
TOP, BACK, STRIKE, END = (0, 0, 1024, 700), (0, 700, 600, 1024), (600, 700, 1024, 860), (600, 860, 1024, 1024)


def face(o, U, V, reg, inset=1.5):
    """quad a=o, b=o+U, c=o+U+V, d=o+V seen from outside with U right, V up; uv from the region (image y down)"""
    x0, y0, x1, y1 = reg
    x0 += inset; y0 += inset; x1 -= inset; y1 -= inset
    o, U, V = map(np.array, (o, U, V))
    P = [o, o + U, o + U + V, o + V]
    uv = [(x0, y1), (x1, y1), (x1, y0), (x0, y0)]
    uv = [(x / 1024, 1 - y / 1024) for x, y in uv]
    n = np.cross(V, U)          # outward for Unity's left-handed clockwise faces (checked on the top face: +y)
    n = n / np.linalg.norm(n)
    return P, uv, n


def box_faces(L, W, H):
    X, Z = L / 2, W / 2
    return [
        face((-X, H, -Z), (L, 0, 0), (0, 0, W), TOP),        # top, label
        face((X, 0, -Z), (-L, 0, 0), (0, 0, W), BACK),       # bottom, seen from below
        face((-X, 0, -Z), (L, 0, 0), (0, H, 0), STRIKE),     # long side -z
        face((X, 0, Z), (-L, 0, 0), (0, H, 0), STRIKE),      # long side +z
        face((X, 0, -Z), (0, 0, W), (0, H, 0), END),         # end +x
        face((-X, 0, Z), (0, 0, -W), (0, H, 0), END),        # end -x
    ]


def write_mesh(path, L, W, H):
    vs, ns, uvs, idx = [], [], [], []
    for P, uv, n in box_faces(L, W, H):
        b = len(vs)
        for p, t in zip(P, uv):
            vs.append(p); ns.append(n); uvs.append(t)
        idx += [b, b + 3, b + 2, b, b + 2, b + 1]     # a,d,c / a,c,b: clockwise from outside
    with open(path, 'wb') as f:
        f.write(struct.pack('<ii', len(vs), len(idx)))
        for v in vs: f.write(struct.pack('<3f', *v))
        for v in ns: f.write(struct.pack('<3f', *v))
        for v in uvs: f.write(struct.pack('<2f', *v))
        for i in idx: f.write(struct.pack('<i', i))


# ---------------------------------------------------------------- icon: the box in three-quarter view
def coeffs(dst, src):
    """PIL PERSPECTIVE coefficients mapping output points dst -> input points src"""
    A, B = [], []
    for (x, y), (u, v) in zip(dst, src):
        A.append([x, y, 1, 0, 0, 0, -u * x, -u * y]); B.append(u)
        A.append([0, 0, 0, x, y, 1, -v * x, -v * y]); B.append(v)
    return np.linalg.solve(np.array(A, float), np.array(B, float)).tolist()


def render_box(atlas, kind, size=256, yaw=-28, pitch=50, bg=None):
    L, W, H = KINDS[kind][4]
    S = 4 * size
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))

    def proj(p):
        x, y, z = p
        x, z = x * cy - z * sy, x * sy + z * cy           # yaw about y
        y, z = y * cp + z * sp, -y * sp + z * cp           # pitch: look down
        return np.array([x, -y]), z
    faces = box_faces(L, W, H)
    pts = [np.array([proj(p)[0] for p in F[0]]) for F in faces]
    allp = np.concatenate(pts)
    mn, mx = allp.min(0), allp.max(0)
    k = S * .86 / max(mx - mn)
    off = np.array([S, S]) / 2 - (mn + mx) / 2 * k
    canvas = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    view = np.array([0, 0, 1.0])
    shade = {0: 1.0, 2: .78, 3: .78, 4: .64, 5: .64, 1: .5}
    order = []
    for i, (P, uv, n) in enumerate(faces):
        # visibility: rotate the normal the same way
        x, y, z = n
        x, z = x * cy - z * sy, x * sy + z * cy
        y, z = y * cp + z * sp, -y * sp + z * cp
        if z < -1e-3:      # facing the camera (camera looks along +z after rotation)
            order.append(i)
    for i in order:
        P, uv, n = faces[i]
        q = [tuple(p * k + off) for p in pts[i]]
        src = [(u * 1024, (1 - v) * 1024) for u, v in uv]
        tex = atlas.convert('RGBA')
        im = tex.transform((S, S), Image.PERSPECTIVE, coeffs(q, src), Image.BICUBIC)
        m = Image.new('L', (S, S), 0)
        ImageDraw.Draw(m).polygon(q, fill=255)
        f = shade[i]
        if f < 1:
            im = Image.fromarray((np.array(im, np.float32) * [f, f, f, 1]).astype(np.uint8), 'RGBA')
        canvas.paste(im, (0, 0), m)
        ImageDraw.Draw(canvas).line(q + [q[0]], fill=(0, 0, 0, 70), width=3)
    # soft shadow under it
    a = canvas.split()[3]
    sh = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    sh.putalpha(a.filter(ImageFilter.GaussianBlur(S / 60)).point(lambda v: int(v * .35)))
    out = Image.new('RGBA', (S, S), bg or (0, 0, 0, 0))
    out.alpha_composite(sh, (S // 70, S // 50))
    out.alpha_composite(canvas)
    return out.resize((size, size), Image.LANCZOS)


def main():
    dist = sys.argv[1]
    only = set(sys.argv[2:])
    for d in ('meshes', 'textures', 'labels', 'icons'):
        os.makedirs(os.path.join(dist, d), exist_ok=True)
    for kind, (_, _, _, _, (L, W, H), _) in KINDS.items():
        write_mesh(os.path.join(dist, 'meshes', kind + '.fpmesh'), L / 1000, W / 1000, H / 1000)
    ls = labels()
    for l in ls:
        if only and l['id'] not in only:
            continue
        A = LB.atlas(l)
        A.save(os.path.join(dist, 'textures', l['id'] + '.jpg'), quality=90)
        flat = LB.label(l, 600 if l['kind'] != 'Book' else 420)
        flat.save(os.path.join(dist, 'labels', l['id'] + '.jpg'), quality=88)
        render_box(A, l['kind']).save(os.path.join(dist, 'icons', l['id'] + '.png'))
        print(l['id'], flush=True)
    # journal art: a little pile of three boxes for the big picture and the row icon, a grey box shape for labels not found yet
    pick = [x for x in ls if x['id'] in ('Beasts_Wolf', 'Space_Rocket', 'Brands_SwenVesta')]
    big = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
    for j, l in enumerate(pick):
        b = render_box(LB.atlas(l), l["kind"], 330, yaw=-28 + j * 14)
        big.alpha_composite(b, (40 + j * 50, 150 - j * 75))
    big.save(os.path.join(dist, 'icons', 'coll_big.png'))
    big.resize((128, 128), Image.LANCZOS).save(os.path.join(dist, 'icons', 'coll_icon.png'))
    sil = render_box(LB.atlas(pick[0]), 'Std', 256)
    a = sil.split()[3]
    shape = Image.new('RGBA', sil.size, (255, 255, 255, 0)); shape.putalpha(a)
    shape.save(os.path.join(dist, 'icons', 'coll_shape.png'))


if __name__ == '__main__':
    main()
