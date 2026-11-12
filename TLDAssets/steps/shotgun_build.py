# Builds Mods/ShotgunByEMPEROR/shotgunbyemperor.bundle (the file name; inside it is still tldshotgun.bundle) from the WINTERMUTE ep5 bundle.
# Keeps only the shotgun objects, gives the file a new CAB name (no clash with survival bundles),
# points references at survival bundles that hold the same objects, and embeds the few
# ep5-only textures/materials/meshes (extracted on the PC by ep5_extract.py).
import UnityPy, json, copy, hashlib, sys, os, collections
from UnityPy.files.SerializedFile import FileIdentifier

EP5 = os.environ['TLD_EP5']
SURV = os.environ['TLD_SURV']
EXT = os.environ['TLD_EXT']
OUT = os.environ['TLD_OUT']

NEW_CAB = 'CAB-' + hashlib.md5(b'TLDShotgunBySeagull-1').hexdigest()
BUNDLE_NAME = 'tldshotgun.bundle'
ROOTS = [
    'Assets/Prefabs/Gear/Weapons/GEAR_Shotgun.prefab',
    'Assets/Prefabs/Gear/Weapons/FPH_Shotgun.prefab',
    'Assets/Prefabs/Characters/FPSItems/FPSWeapon_Shotgun.prefab',
    'Assets/ScriptableObjects/Gameplay/FPSItems/FPSItem_Shotgun.asset',
    'Assets/Prefabs/Gear/Ammo/GEAR_ShotgunAmmoBox.prefab',
    'Assets/Prefabs/Gear/Ammo/GEAR_ShotgunAmmoSingle.prefab',
    'Assets/Prefabs/Gear/Ammo/GEAR_ShotgunAmmoCasing.prefab',
    'Assets/ArtAssets/Characters/FP_Rig/ShotgunStateTransitions.asset',
]
CAB_SURV_GEAR = 'CAB-80618208c19940e615a2b62a77106f1c'   # same name in ep5 and survival (we replace it)
CAB_EP5_MATS = 'CAB-71edd1c7bb1ddeb20c4ae08cfa2797c8'    # ep5-only: shotgun materials/textures
CAB_EP5_SHADER = 'CAB-8c4acaa2cde04020e6d8730e6224ed50'  # ep5-only: TLD_StandardSkinned (Characters)
CAB_EP5_MESH = 'CAB-173855c66cac2ad239b699d7acb4c08b'    # ep5 version lacks the shell mesh in survival
CAB_DEFMAT = 'CAB-d0edd5dc10bd0052c9bc52bb710781be'      # Default-Material, not in survival
CAB_SHADERS = 'CAB-89d3c19d570658046701235aab231114'
SURV_SKINNED_SHADER = -2936371289961251946               # TLD_StandardSkinned in CAB_SHADERS
DROP_SCRIPTS = {'BipedBulletWeapon', 'AkBank'}          # not in survival / bank loaded by our code
IMPORT_FROM_EXT = [-4090140187055288879, 6989890098493347211, -499929211162636386,
                   -1353090822767189807, 3719040124481894599, 6596921083635724480]
EXT_CAB_EXTERNALS = ['CAB-89d3c19d570658046701235aab231114', 'CAB-25d5ca01e358006e4d5d3105d764f647',
                     'CAB-80618208c19940e615a2b62a77106f1c', 'CAB-deb7c3375498ac54d2f39b8d832d46fd',
                     'CAB-ce50778b17a5229a96b48cbc56247f77', 'CAB-34e2f908a97bb83f72310d4e8dbfbc4f',
                     'CAB-8c4acaa2cde04020e6d8730e6224ed50', 'CAB-d0edd5dc10bd0052c9bc52bb710781be',
                     'CAB-4c597e7b63a27e3cdbcb25e8fa05d677', 'CAB-6a91c34c103b052aefd54a751345ede5']


def sfile(env):
    bf = list(env.files.values())[0]
    for n, f in bf.files.items():
        if hasattr(f, 'objects'):
            return bf, n, f


def walk_pptr(t, fn):
    """Calls fn(dict) for every PPtr dict, in place."""
    if isinstance(t, dict):
        if 'm_FileID' in t and 'm_PathID' in t and len(t) == 2:
            fn(t)
        else:
            for v in t.values():
                walk_pptr(v, fn)
    elif isinstance(t, (list, tuple)):
        for v in t:
            walk_pptr(v, fn)


env = UnityPy.load(f'{EP5}/5c21e6c455f81af915584c6a69f43eda.bundle')
bf, old_cab, sf = sfile(env)
ress = bf.files.get(old_cab + '.resS')
ress_bytes = bytes(ress.bytes if hasattr(ress, 'bytes') else ress.read_bytes(ress.Length)) if ress is not None else b''
old_ext = [e.path.split('/')[-1] for e in sf.externals]

# survival copy of the same CAB: objects that already exist there are referenced, not copied
senv = UnityPy.load(f'{SURV}/5c21e6c455f81af915584c6a69f43eda.bundle')
_, scab, ssf = sfile(senv)
assert scab == CAB_SURV_GEAR, scab
surv_ids = set(ssf.objects.keys())

# script names for MonoBehaviours (MonoScripts live in c0ce487c)
senv_scripts = UnityPy.load(f'{EP5}/c0ce487c0d1b29956413945bee0cefce.bundle')
_, _, scr = sfile(senv_scripts)
script_name = {pid: o.read().m_ClassName for pid, o in scr.objects.items() if o.type.name == 'MonoScript'}

path_of = {}
for p, o in env.container.items():
    path_of.setdefault(o.path_id, p)
root_ids = {}
for p, o in env.container.items():
    if p in ROOTS and o.type.name in ('GameObject', 'MonoBehaviour'):
        root_ids[p] = o.path_id
assert len(root_ids) == len(ROOTS), set(ROOTS) - set(root_ids)

# ---- closure, dropping components we do not want
trees = {}
dropped = set()
def tree(pid):
    if pid not in trees:
        trees[pid] = sf.objects[pid].read_typetree()
    return trees[pid]

for pid, o in sf.objects.items():
    pass

keep = set(); q = list(root_ids.values())
while q:
    pid = q.pop()
    if pid in keep or pid in dropped:
        continue
    if pid in surv_ids:
        continue  # referenced from survival
    o = sf.objects[pid]
    t = tree(pid)
    if o.type.name == 'MonoBehaviour':
        s = t.get('m_Script', {})
        if s.get('m_FileID') == 1 and script_name.get(s.get('m_PathID')) in DROP_SCRIPTS:
            dropped.add(pid); continue
    keep.add(pid)
    def add(pp):
        if pp['m_FileID'] == 0 and pp['m_PathID']:
            q.append(pp['m_PathID'])
    walk_pptr(t, add)

# remove dropped components from their GameObjects
for pid in list(keep):
    o = sf.objects[pid]
    if o.type.name == 'GameObject':
        t = tree(pid)
        before = len(t['m_Component'])
        t['m_Component'] = [c for c in t['m_Component'] if c['component']['m_PathID'] not in dropped]
        if len(t['m_Component']) != before:
            print('removed', before - len(t['m_Component']), 'component(s) from', t['m_Name'])

# ---- new externals
new_ext = []
def ext_index(cab_or_path):
    path = cab_or_path if cab_or_path.startswith('Library/') else f'archive:/{cab_or_path}/{cab_or_path}'
    for i, e in enumerate(new_ext):
        if e.path == path:
            return i + 1
    fi = copy.copy(sf.externals[0]); fi.path = path
    new_ext.append(fi)
    return len(new_ext)

def ext_path_for_old(fid):
    e = sf.externals[fid - 1]
    return e.path if e.path.startswith('Library/') else e.path.split('/')[-1]

imported_mesh = 7499854530098334874
stats = collections.Counter()
def remap_ep5(pp):
    fid, pid = pp['m_FileID'], pp['m_PathID']
    if not pid:
        pp['m_FileID'] = 0; return
    if fid == 0:
        if pid in keep:
            return
        if pid in surv_ids:
            pp['m_FileID'] = ext_index(CAB_SURV_GEAR); stats['->survgear'] += 1; return
        if pid in dropped:
            pp['m_FileID'] = 0; pp['m_PathID'] = 0; stats['dropped ref'] += 1; return
        raise RuntimeError(f'local ref {pid} not kept')
    cab = ext_path_for_old(fid)
    if cab == CAB_EP5_MATS:
        assert pid in IMPORT_FROM_EXT, pid
        pp['m_FileID'] = 0; stats['->imported mat/tex'] += 1; return
    if cab == CAB_EP5_SHADER:
        pp['m_FileID'] = ext_index(CAB_SHADERS); pp['m_PathID'] = SURV_SKINNED_SHADER; stats['->surv shader'] += 1; return
    if cab == CAB_EP5_MESH:
        assert pid == imported_mesh
        pp['m_FileID'] = 0; stats['->imported mesh'] += 1; return
    if cab == CAB_DEFMAT:
        pp['m_FileID'] = 0; pp['m_PathID'] = 0; stats['defmat dropped'] += 1; return
    pp['m_FileID'] = ext_index(cab); stats['ext ' + cab[:12]] += 1

for pid in keep:
    walk_pptr(trees[pid], remap_ep5)

# inline streamed data (meshes/textures that point into the ep5 .resS)
def inline_stream(t, data_src):
    sd = t.get('m_StreamData')
    if not sd or not sd.get('path'):
        return False
    blob = data_src[sd['offset']: sd['offset'] + sd['size']]
    assert len(blob) == sd['size']
    if 'image data' in t:
        t['image data'] = blob
    elif 'm_VertexData' in t:
        t['m_VertexData']['m_DataSize'] = blob
    else:
        raise RuntimeError('unknown streamed type')
    t['m_StreamData'] = {'offset': 0, 'size': 0, 'path': ''}
    return True

for pid in keep:
    if inline_stream(trees[pid], ress_bytes):
        stats['inlined'] += 1

# ---- imported objects
TEMPLATES = {}
for _o in sf.objects.values():
    TEMPLATES.setdefault(_o.type.name, _o)
def template(type_name):
    return TEMPLATES[type_name]

def add_object(pid, type_name, t):
    o = copy.copy(template(type_name))
    o.path_id = pid
    o.save_typetree(t)
    sf.objects[pid] = o

ext_old = EXT_CAB_EXTERNALS
def remap_ext(pp):
    fid, pid = pp['m_FileID'], pp['m_PathID']
    if not pid:
        pp['m_FileID'] = 0; return
    if fid == 0:
        assert pid in IMPORT_FROM_EXT, pid; return
    pp['m_FileID'] = ext_index(ext_old[fid - 1])

new_objs = {}
for pid in IMPORT_FROM_EXT:
    t = json.load(open(f'{EXT}/{pid}.json'))
    walk_pptr(t, remap_ext)
    if 'm_TextureFormat' in t:
        t['image data'] = open(f'{EXT}/{pid}.texdata', 'rb').read()
        assert len(t['image data']) == t['m_CompleteImageSize'], (pid, len(t['image data']))
        t['m_StreamData'] = {'offset': 0, 'size': 0, 'path': ''}
        t['m_PlatformBlob'] = b'' if isinstance(t.get('m_PlatformBlob'), str) else t.get('m_PlatformBlob', [])
        new_objs[pid] = ('Texture2D', t)
    else:
        # json turned tex env pairs into lists, which the writer accepts
        new_objs[pid] = ('Material', t)

menv = UnityPy.load(f'{EP5}/5fd4b9881401a1dcd9b3938104930784.bundle')
mbf, mcab, msf = sfile(menv)
mres = mbf.files.get(mcab + '.resS')
mres_bytes = bytes(mres.bytes if hasattr(mres, 'bytes') else mres.read_bytes(mres.Length)) if mres is not None else b''
mt = msf.objects[imported_mesh].read_typetree()
mext = [e.path.split('/')[-1] for e in msf.externals]
def remap_mesh(pp):
    if pp['m_PathID'] == 0:
        pp['m_FileID'] = 0; return
    raise RuntimeError('mesh has refs %r' % pp)
walk_pptr(mt, remap_mesh)
inline_stream(mt, mres_bytes)
new_objs[imported_mesh] = ('Mesh', mt)

# ---- write kept objects, delete the rest
for pid in list(sf.objects.keys()):
    o = sf.objects[pid]
    if o.type.name == 'AssetBundle':
        ab_pid = pid; continue
    if pid in keep:
        o.save_typetree(trees[pid])
    else:
        del sf.objects[pid]
for pid, (tn, t) in new_objs.items():
    add_object(pid, tn, t)

# ---- AssetBundle object: container + preload table
ab = sf.objects[ab_pid]
abt = ab.read_typetree()
all_ids = sorted(keep | set(new_objs))
preload = []
container = []
local_deps = collections.defaultdict(set)
for path in ROOTS:
    rid = root_ids[path]
    # dependencies of this asset (local closure + external refs)
    seen = set(); q = [rid]; ext_refs = set()
    while q:
        p = q.pop()
        if p in seen: continue
        seen.add(p)
        if p in trees:
            t = trees[p]
        else:
            t = new_objs[p][1]
        def f(pp):
            if pp['m_PathID'] == 0: return
            if pp['m_FileID'] == 0: q.append(pp['m_PathID'])
            else: ext_refs.add((pp['m_FileID'], pp['m_PathID']))
        walk_pptr(t, f)
    deps = [{'m_FileID': 0, 'm_PathID': p} for p in sorted(seen)] + \
           [{'m_FileID': fi, 'm_PathID': pi} for fi, pi in sorted(ext_refs)]
    idx = len(preload); preload += deps
    container.append((path, {'preloadIndex': idx, 'preloadSize': len(deps), 'asset': {'m_FileID': 0, 'm_PathID': rid}}))
abt['m_Container'] = container
abt['m_PreloadTable'] = preload
abt['m_Name'] = BUNDLE_NAME
abt['m_AssetBundleName'] = BUNDLE_NAME
abt['m_Dependencies'] = sorted({e.path.split('/')[-1].lower() for e in new_ext if e.path.startswith('archive:')})
ab.save_typetree(abt)

# ---- externals + rename CAB, drop .resS
sf.externals = new_ext
new_files = {}
for n, f in bf.files.items():
    if n == old_cab:
        new_files[NEW_CAB] = f
    elif n == old_cab + '.resS':
        continue
    else:
        new_files[n] = f
bf.files = new_files
sf.name = NEW_CAB
sf.mark_changed() if hasattr(sf, 'mark_changed') else None

os.makedirs(os.path.dirname(OUT), exist_ok=True)
data = bf.save(packer='lz4')
open(OUT, 'wb').write(data)
print('kept', len(keep), 'imported', len(new_objs), 'dropped comps', len(dropped))
print('externals:'); [print('  ', i + 1, e.path) for i, e in enumerate(new_ext)]
print(dict(stats))
print('wrote', OUT, len(data), 'bytes; CAB', NEW_CAB)
