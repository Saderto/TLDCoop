"""Inventory icons for the lighters and refills: python3 render_lighter_icons.py <bakedir> <outdir> [keys]"""
import sys, os
B, OUT = sys.argv[1], sys.argv[2]
KEYS = set(sys.argv[3:])
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
src = open(os.path.join(HERE, 'render_icons.py'), encoding='utf-8').read().split('\nJ = os.path.join')[0]
sys.argv = [sys.argv[0], B, B, OUT]
exec(compile(src, 'render_icons_head', 'exec'))
import lighter_designs as LD
from bake_lighters_names import mesh_name
for key, ru, en, kind, build, rar in LD.DESIGNS:
    if KEYS and key not in KEYS:
        continue
    m = mesh_name(key)
    rot = (0, 0, -20) if 'Kolibri' in key else (0, 0, 20)
    render(m, os.path.join(B, m + '_albedo.png'), rot, os.path.join(OUT, key + '.png'))
