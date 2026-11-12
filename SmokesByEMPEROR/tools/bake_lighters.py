"""Bake the collectible lighters and their refills: python3 bake_lighters.py <outdir> [keys]
Each DESIGNS entry -> <outdir>/<mesh>.npz + <mesh>_albedo.png (512 px; the metal's look baked into the colour)."""
import sys, os, re
OUT = sys.argv[1]
KEYS = set(sys.argv[2:])
sys.argv = [sys.argv[0], OUT, OUT]   # bake_all reads TEX, OUT from argv at import
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bake_all as BA
import lighter_designs as LD


from bake_lighters_names import mesh_name
def _unused(key):
    return re.sub(r'(?<!^)(?=[A-Z])', '_', key).lower()


for key, ru, en, kind, build, rar in LD.DESIGNS:
    if KEYS and key not in KEYS:
        continue
    BA.reset()
    objs = build()
    objs = objs if isinstance(objs, list) else [objs]
    meshes = [o for o in objs if o.type == 'MESH']
    # every part needs the same first UV layer, or the join leaves none active
    for o in meshes:
        if not o.data.uv_layers:
            o.data.uv_layers.new(name='UVMap')
        o.data.uv_layers[0].name = 'UVMap'
    ob = BA.flatten(meshes)
    if ob.data.uv_layers and ob.data.uv_layers.active is None:
        ob.data.uv_layers.active = ob.data.uv_layers[0]
    name = mesh_name(key)
    ob.name = name
    uv = BA.bake(ob, name, size=512, margin=.006)
    BA.save_mesh(ob, name, uv)
    print('BAKED', key, name, flush=True)
