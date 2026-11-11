# Builds Mods/TLDMilitaryStim/tldmilitarystim.bundle from the WINTERMUTE ep5 bundles.
# Keeps only the military stim objects (world item, FP hands, FPS weapon/item), gives the file a new CAB name,
# points references at survival bundles holding the same objects, and embeds the ep5-only objects:
# military texture (extracted on the PC), GearItemData_MilitaryStim (ep5 4940df3f), inventory icon (ep5 ddba3fd7).
import UnityPy, json, copy, hashlib, sys, os, collections

B = os.environ['TLD_EP5']; SURV = os.environ['TLD_SURV']            # folder with ep5 bundles: 5c21e6.., 4940df3f.., ddba3fd7.. + surv_5c21e6.bundle
EXT = os.environ['TLD_EXT']          # ep5_extract folder (texture 8747020503677287537)
OUT = os.environ['TLD_OUT']

NEW_CAB = 'CAB-' + hashlib.md5(b'TLDMilitaryStimBySeagull-1').hexdigest()
BUNDLE_NAME = 'tldmilitarystim.bundle'
ROOTS = [
    'Assets/Prefabs/Gear/FirstAid/GEAR_MilitaryStim.prefab',
    'Assets/Prefabs/Gear/FirstAid/FPH_EmergencyStim_Military.prefab',
    'Assets/Prefabs/Characters/FPSItems/FPSWeapon_EmergencyStim_Military.prefab',
    'Assets/ScriptableObjects/Gameplay/FPSItems/FPSItem_EmergencyStim_Military.asset',
]
GID_PATH = 'Assets/Prefabs/Gear/FirstAid/GearItemData_MilitaryStim.asset'
ICON_PATH = 'Assets/ArtAssets/UI/InventoryGridIcons/ico_GearItem__MilitaryStim.png'
CAB_SURV_GEAR = 'CAB-80618208c19940e615a2b62a77106f1c'
CAB_EP5_MATS = 'CAB-71edd1c7bb1ddeb20c4ae08cfa2797c8'
CAB_GID = 'CAB-ef9101aa852e21a0413b7dfdfe837d07'
GID_PID = 398354439227846722
TEX_PID = 8747020503677287537


def sfile(env):
    bf = list(env.files.values())[0]
    for n, f in bf.files.items():
        if hasattr(f, 'objects'):
            return bf, n, f


def resS(bf, cab):
    r = bf.files.get(cab + '.resS')
    if r is None: return b''
    return bytes(r.bytes if hasattr(r, 'bytes') else r.read_bytes(r.Length))


def walk_pptr(t, fn):
    if isinstance(t, dict):
        if 'm_FileID' in t and 'm_PathID' in t and len(t) == 2:
            fn(t)
        else:
            for v in t.values(): walk_pptr(v, fn)
    elif isinstance(t, (list, tuple)):
        for v in t: walk_pptr(v, fn)


env = UnityPy.load(f'{B}/5c21e6c455f81af915584c6a69f43eda.bundle')
bf, old_cab, sf = sfile(env)
assert old_cab == CAB_SURV_GEAR
ress = resS(bf, old_cab)
senv = UnityPy.load(f'{SURV}/5c21e6c455f81af915584c6a69f43eda.bundle')
_, scab, ssf = sfile(senv)
assert scab == CAB_SURV_GEAR
surv_ids = set(ssf.objects.keys())

root_ids = {p: o.path_id for p, o in env.container.items() if p in ROOTS and o.type.name in ('GameObject', 'MonoBehaviour')}
assert len(root_ids) == len(ROOTS), set(ROOTS) - set(root_ids)

trees = {}
def tree(pid):
    if pid not in trees: trees[pid] = sf.objects[pid].read_typetree()
    return trees[pid]

keep = set(); q = list(root_ids.values())
while q:
    pid = q.pop()
    if pid in keep or pid in surv_ids: continue
    keep.add(pid)
    def add(pp):
        if pp['m_FileID'] == 0 and pp['m_PathID']: q.append(pp['m_PathID'])
    walk_pptr(tree(pid), add)

new_ext = []
def ext_index(cab_or_path):
    path = cab_or_path if cab_or_path.startswith('Library/') else f'archive:/{cab_or_path}/{cab_or_path}'
    for i, e in enumerate(new_ext):
        if e.path == path: return i + 1
    fi = copy.copy(sf.externals[0]); fi.path = path
    new_ext.append(fi)
    return len(new_ext)

def cab_of(file, fid):
    e = file.externals[fid - 1]
    return e.path if e.path.startswith('Library/') else e.path.split('/')[-1]

stats = collections.Counter()
def remap(pp):
    fid, pid = pp['m_FileID'], pp['m_PathID']
    if not pid: pp['m_FileID'] = 0; return
    if fid == 0:
        if pid in keep: return
        if pid in surv_ids: pp['m_FileID'] = ext_index(CAB_SURV_GEAR); stats['->survgear'] += 1; return
        raise RuntimeError(f'local ref {pid} not kept')
    cab = cab_of(sf, fid)
    if cab == CAB_EP5_MATS:
        assert pid == TEX_PID, pid
        pp['m_FileID'] = 0; stats['->military tex'] += 1; return
    if cab == CAB_GID:
        assert pid == GID_PID, pid
        pp['m_FileID'] = 0; stats['->gearitemdata'] += 1; return
    pp['m_FileID'] = ext_index(cab); stats['ext ' + cab[:12]] += 1

for pid in keep:
    walk_pptr(trees[pid], remap)

def inline_stream(t, data_src):
    sd = t.get('m_StreamData')
    if not sd or not sd.get('path'): return False
    blob = data_src[sd['offset']: sd['offset'] + sd['size']]
    assert len(blob) == sd['size']
    if 'image data' in t: t['image data'] = blob
    elif 'm_VertexData' in t: t['m_VertexData']['m_DataSize'] = blob
    else: raise RuntimeError('unknown streamed type')
    t['m_StreamData'] = {'offset': 0, 'size': 0, 'path': ''}
    return True

for pid in keep:
    if inline_stream(trees[pid], ress): stats['inlined'] += 1

TEMPLATES = {}
for _o in sf.objects.values(): TEMPLATES.setdefault(_o.type.name, _o)
new_objs = {}   # pid -> (ObjectReader, tree)

def add_like(pid, type_name, t):
    o = copy.copy(TEMPLATES[type_name]); o.path_id = pid
    new_objs[pid] = (o, t)

# military texture (from PC extract)
t = json.load(open(f'{EXT}/{TEX_PID}.json'))
t['image data'] = open(f'{EXT}/{TEX_PID}.texdata', 'rb').read()
assert len(t['image data']) == t['m_CompleteImageSize']
t['m_StreamData'] = {'offset': 0, 'size': 0, 'path': ''}
t['m_PlatformBlob'] = b'' if isinstance(t.get('m_PlatformBlob'), str) else t.get('m_PlatformBlob', [])
add_like(TEX_PID, 'Texture2D', t)

# icon (ep5 ddba3fd7)
ienv = UnityPy.load(f'{B}/ddba3fd744cf883c382f3beaf47c504b.bundle')
ibf, icab, isf = sfile(ienv)
icon = [o for p, o in ienv.container.items() if p == ICON_PATH][0]
it = icon.read_typetree()
inline_stream(it, resS(ibf, icab))
walk_pptr(it, lambda pp: (_ for _ in ()).throw(RuntimeError('icon has refs')) if pp['m_PathID'] else None)
ICON_PID = icon.path_id
assert ICON_PID not in sf.objects
add_like(ICON_PID, 'Texture2D', it)

# GearItemData_MilitaryStim (ep5 4940df3f): MonoBehaviour with its own script type
genv = UnityPy.load(f'{B}/4940df3f81e3207f88e1c1848754f83b.bundle')
gbf, gcab, gsf = sfile(genv)
assert gcab == CAB_GID
go = gsf.objects[GID_PID]
gt = go.read_typetree()
def remap_gid(pp):
    if not pp['m_PathID']: pp['m_FileID'] = 0; return
    assert pp['m_FileID'] != 0, pp
    pp['m_FileID'] = ext_index(cab_of(gsf, pp['m_FileID']))
walk_pptr(gt, remap_gid)
st = copy.copy(go.serialized_type)
sref = copy.copy(gsf.script_types[st.script_type_index])
# script ref: file index into OUR externals
scab = cab_of(gsf, sref.local_serialized_file_index) if sref.local_serialized_file_index else gcab
sref.local_serialized_file_index = ext_index(scab)
sf.script_types.append(sref)
st.script_type_index = len(sf.script_types) - 1
sf.types.append(st)
gnew = copy.copy(go)
gnew.assets_file = sf
gnew.type_id = len(sf.types) - 1
gnew.serialized_type = st
new_objs[GID_PID] = (gnew, gt)
print('GearItemData script', scab, sref.local_identifier_in_file)

# ---- write
ab_pid = None
for pid in list(sf.objects.keys()):
    o = sf.objects[pid]
    if o.type.name == 'AssetBundle': ab_pid = pid; continue
    if pid in keep: o.save_typetree(trees[pid])
    else: del sf.objects[pid]
for pid, (o, t) in new_objs.items():
    o.save_typetree(t)
    sf.objects[pid] = o

def tree_of(p):
    return trees[p] if p in trees else new_objs[p][1]

ab = sf.objects[ab_pid]
abt = ab.read_typetree()
preload = []; container = []
for path, rid in list(root_ids.items()) + [(GID_PATH, GID_PID), (ICON_PATH, ICON_PID)]:
    seen = set(); q = [rid]; ext_refs = set()
    while q:
        p = q.pop()
        if p in seen: continue
        seen.add(p)
        def f(pp):
            if pp['m_PathID'] == 0: return
            if pp['m_FileID'] == 0: q.append(pp['m_PathID'])
            else: ext_refs.add((pp['m_FileID'], pp['m_PathID']))
        walk_pptr(tree_of(p), f)
    deps = [{'m_FileID': 0, 'm_PathID': p} for p in sorted(seen)] + [{'m_FileID': fi, 'm_PathID': pi} for fi, pi in sorted(ext_refs)]
    idx = len(preload); preload += deps
    container.append((path, {'preloadIndex': idx, 'preloadSize': len(deps), 'asset': {'m_FileID': 0, 'm_PathID': rid}}))
abt['m_Container'] = container
abt['m_PreloadTable'] = preload
abt['m_Name'] = BUNDLE_NAME
abt['m_AssetBundleName'] = BUNDLE_NAME
abt['m_Dependencies'] = sorted({e.path.split('/')[-1].lower() for e in new_ext if e.path.startswith('archive:')})
ab.save_typetree(abt)

sf.externals = new_ext
new_files = {}
for n, f in bf.files.items():
    if n == old_cab: new_files[NEW_CAB] = f
    elif n == old_cab + '.resS': continue
    else: new_files[n] = f
bf.files = new_files
sf.name = NEW_CAB
os.makedirs(os.path.dirname(OUT), exist_ok=True)
data = bf.save(packer='lz4')
open(OUT, 'wb').write(data)
print('kept', len(keep), 'new', len(new_objs))
print('externals:'); [print('  ', i + 1, e.path) for i, e in enumerate(new_ext)]
print(dict(stats))
print('wrote', OUT, len(data), 'bytes; CAB', NEW_CAB)
