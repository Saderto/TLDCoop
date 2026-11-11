"""python3 lighters_to_fpmesh.py <bakedir> <outdir>: every lighter_*/butane_can npz -> .fpmesh lying at rest, front up"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, 'npz_to_fpmesh.py'), encoding='utf-8').read().split('\nos.makedirs(OUT')[0]
exec(compile(src, 'npz_head', 'exec'))
os.makedirs(OUT, exist_ok=True)
flat = rz(math.pi) @ rx(-math.pi / 2)
for f in sorted(os.listdir(B)):
    if f.endswith('.npz'):
        rest(f[:-4], flat)
