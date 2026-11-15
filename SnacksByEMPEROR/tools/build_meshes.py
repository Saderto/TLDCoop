"""Pack meshes, one per brand (its flavours share it) -> ../meshes/<Brand>.fpmesh and ../bake/<Brand>.npz (authoring space, for icons)."""
import os, sys, zlib
import shapes as S
from catalog import BRANDS

HERE = os.path.dirname(os.path.abspath(__file__))
M, B = os.path.join(HERE, '..', 'meshes'), os.path.join(HERE, '..', 'bake')
os.makedirs(M, exist_ok=True); os.makedirs(B, exist_ok=True)


def out(name, m):
    m.compute_normals()
    S.write_fpmesh(os.path.join(M, name + '.fpmesh'), m)
    S.save_npz(os.path.join(B, name + '.npz'), m)
    lo, hi = m.bounds()
    print(name, len(m.v), 'verts', len(m.t), 'tris', 'size', [round(x, 3) for x in (hi - lo)])


POUCH = 'seeds nuts cornnuts coated mix namkeen fish seafood jerky strips seaweed drazhe'.split()
CRIMP = 'croutons rusk cracker pretzel bagel sticks sweetsticks'.split()
only = sys.argv[1:]
for b in BRANDS:
    if only and b['id'] not in only: continue
    sid, sh, sz = b['id'], b['shape'], b['size']
    if sh == 'bag':
        seed = zlib.crc32(sid.encode()) % 1000; kind = b['kind']
        if b.get('side_seal'):      # a flat sachet of its own (dried squid)
            out(sid, S.pillow_bag(*sz, seal=b.get('seal', .06), side_seal=b['side_seal'], seed=seed, squash=.6))
        elif kind in POUCH:         # a flat pouch with a header strip: the contents lie at the bottom
            out(sid, S.pillow_bag(sz[0], sz[1], sz[2] * 1.25, seal=.035, seal_top=.13, side_seal=.045, seed=seed, squash=.5, pinch=0, settle=.7, wrinkle=.0025))
        elif kind in CRIMP:         # a small flow pack: wide crimped ends cut in a zigzag, straight sides
            out(sid, S.pillow_bag(*sz, seal=.085, seed=seed, squash=.6, pinch=.03, zig=.0035, res=(96, 60)))
        else:                       # a puffed crisp bag
            out(sid, S.pillow_bag(*sz, seal=b.get('seal', .06), seed=seed, squash=.6))
    elif sh == 'tube':
        out(sid, S.tube_can(*sz))
    elif sh == 'can':
        out(sid, S.tube_can(*sz, lid_h=.006, lid_r=1.015))
    elif sh == 'box':
        out(sid, S.box(*sz))
    elif sh == 'carton':
        out(sid, S.box(*sz, taper=1.0, closed=True))
    elif sh == 'cup':
        out(sid, S.cup(sz[0] * .78, sz[0], sz[1], uv_side=(0, .3, 1, 1), uv_lid=(.15, .15, .14)))
