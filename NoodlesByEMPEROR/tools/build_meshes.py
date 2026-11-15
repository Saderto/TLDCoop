"""All package meshes -> ../meshes/<name>.fpmesh and ../bake/<name>.npz (authoring space, for icons). One mesh per shape (pillow packs: three wrinkle variants)."""
import os
import shapes as S
from catalog import SIZES, CUPS, TRAYS, NOODLES

HERE = os.path.dirname(os.path.abspath(__file__))
M, B = os.path.join(HERE, '..', 'meshes'), os.path.join(HERE, '..', 'bake')
os.makedirs(M, exist_ok=True); os.makedirs(B, exist_ok=True)
for d in (M, B):
    for f in os.listdir(d): os.remove(os.path.join(d, f))


def out(name, m):
    m.compute_normals()
    S.write_fpmesh(os.path.join(M, name + '.fpmesh'), m)
    S.save_npz(os.path.join(B, name + '.npz'), m)
    lo, hi = m.bounds()
    print(name, len(m.v), 'verts', 'size', [round(float(x), 3) for x in (hi - lo)])


used = {s['shape'] for s in NOODLES}
for sh in sorted(used):
    if sh in SIZES:
        W, H, T = SIZES[sh]
        for k in range(3):
            out(f'{sh}{k}', S.pillow_bag(W, H, T, seed=11 + 7 * k + len(sh), squash=.6, wrinkle=.0025 + .001 * k))
    elif sh in CUPS:
        R0, R1, H = CUPS[sh]
        out(sh, S.cup(R0, R1, H, uv_side=(0, .3, 1, 1), uv_lid=(.15, .15, .14)))
        out(sh + 'Hot', S.cup(R0, R1, H, uv_side=(0, .3, 1, 1), lid=False, open_fill=((.5, .15), .14, .8 if sh == 'ufo' else .86)))
    else:
        W, D, H = TRAYS[sh]
        out(sh, S.tray(W, D, H, uv_lid=(0, .45, .62, 1) if sh == 'tray' else (0, .45, .55, 1), uv_side=(0, 0, 1, .42)))
for k in ('Bowl', 'Donburi', 'Pot', 'Plate'):
    out(k + 'Hot', S.vessel(k))
