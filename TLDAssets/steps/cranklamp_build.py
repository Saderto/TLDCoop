# Builds Mods/TLDCrankLamp/tldcranklamp.bundle from the WINTERMUTE ep5 bundles.
# The ep5 crank lamp runs on ep5-only scripts (CrankLampItem, FirstPersonCrankLamp) that survival does not have,
# so they are replaced with survival scripts: FlashlightItem on the world item (+ a copy of the flashlight's
# AuroraField child) and FirstPersonFlashlight on the first-person hands. Charging by crank is done in mod code.
# Ep5-only mesh/material/textures come from the PC extract (tools/ep5_extract_lamp.py).
import UnityPy, json, copy, hashlib, sys, os, collections

EP5 = os.environ['TLD_EP5']
SURV = os.environ['TLD_SURV']
EXT = os.environ['TLD_EXT']
OUT = os.environ['TLD_OUT']

NEW_CAB = 'CAB-' + hashlib.md5(b'TLDCrankLampBySeagull-1').hexdigest()
BUNDLE_NAME = 'tldcranklamp.bundle'
ROOTS = {
    'Assets/Prefabs/Gear/LightSources/GEAR_Flashlight_CrankLamp.prefab': 2013646772081545758,
    'Assets/Prefabs/Characters/FPSItems/FPSWeapon_Flashlight_CrankLamp.prefab': 8720242184326922116,
    'Assets/ScriptableObjects/Gameplay/FPSItems/FPSItem_Flashlight_CrankLamp.asset': 5203530084806023140,
}
GID_PATH = 'Assets/Prefabs/Gear/LightSources/GearItemData_Flashlight_CrankLamp.asset'
GICON_PATH = 'Assets/ScriptableObjects/Gear/IconData/GearIcon_Flashlight_CrankLamp.asset'
ICON_PATH = 'Assets/ArtAssets/UI/InventoryGridIcons/ico_GearItem__Flashlight_CrankLamp.png'
GID_PID, GICON_PID = -1439960313754633993, 3762451260332112697

CAB_SURV_GEAR = 'CAB-80618208c19940e615a2b62a77106f1c'
CAB_EP5_MATS = 'CAB-71edd1c7bb1ddeb20c4ae08cfa2797c8'
CAB_EP5_CHAR = 'CAB-8c4acaa2cde04020e6d8730e6224ed50'   # ep5-only: world mesh + TLD_StandardSkinned
CAB_GID = 'CAB-ef9101aa852e21a0413b7dfdfe837d07'
CAB_ICONS = 'CAB-5cd06551ef14a57a621063cf89621a0d'
CAB_SHADERS = 'CAB-89d3c19d570658046701235aab231114'
CAB_DEFMAT = 'CAB-d0edd5dc10bd0052c9bc52bb710781be'
SURV_SKINNED_SHADER = -2936371289961251946
WORLD_MESH = -2723685747808342017
MAT = 425015589523740178

# ep5 crank lamp objects
GEAR_GO = 2013646772081545758
CRANK_ITEM = 2013646772543621950          # CrankLampItem (ep5 only) -> FlashlightItem
GEAR_ITEM = 2013646772961746830
FPH_GO = 6968026701446406861
FP_CRANK = 6968026698910241743            # FirstPersonCrankLamp (ep5 only) -> FirstPersonFlashlight
# survival flashlight templates (present in the ep5 file too, same ids)
TPL_FLASHLIGHT_ITEM = 6562798912663514986
TPL_FP_FLASHLIGHT = -4910152090815464356
TPL_AURORA_GO = 6562798913358826806


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


env = UnityPy.load(f'{EP5}/5c21e6c455f81af915584c6a69f43eda.bundle')
bf, old_cab, sf = sfile(env)
assert old_cab == CAB_SURV_GEAR
ress = resS(bf, old_cab)
senv = UnityPy.load(f'{SURV}/5c21e6c455f81af915584c6a69f43eda.bundle')
_, scab, ssf = sfile(senv)
assert scab == CAB_SURV_GEAR
surv_ids = set(ssf.objects.keys())
for p in ROOTS.values(): assert p in sf.objects and p not in surv_ids

trees = {}
def tree(pid):
    if pid not in trees: trees[pid] = sf.objects[pid].read_typetree()
    return trees[pid]

def name_of(pid):
    return tree(pid).get('m_Name')

def comp_of(go_pid, type_name):
    for c in tree(go_pid)['m_Component']:
        p = c['component']['m_PathID']
        if sf.objects[p].type.name == type_name: return p

new_objs = {}     # pid -> (template ObjectReader, tree)
objs_override = {}  # pid -> ObjectReader with another type (replaced scripts)
_next = [0x7C4A0000000000]
def new_pid():
    _next[0] += 1
    return _next[0]

# ---- 1. world item: CrankLampItem -> FlashlightItem
ck = tree(CRANK_ITEM)
fl = copy.deepcopy(sf.objects[TPL_FLASHLIGHT_ITEM].read_typetree())
fl['m_GameObject'] = {'m_FileID': 0, 'm_PathID': GEAR_GO}
fl['m_FxObjectLow'] = ck['m_FxObjectLow']
fl['m_FxObjectHigh'] = ck['m_FxObjectLow']          # no high beam on the crank lamp (blocked in code)
fl['m_LightIndoor'] = ck['m_LightIndoor']
fl['m_LightOutdoor'] = ck['m_LightOutdoor']
fl['m_LightIndoorHigh'] = ck['m_LightIndoor']
fl['m_LightOutdoorHigh'] = ck['m_LightOutdoor']
fl['m_Color'] = ck['m_Color']
fl['m_HUD'] = ck['m_HUD']
FL_PID = new_pid()

# copy of the flashlight's AuroraField child (FlashlightItem expects one)
aur = {}
def clone_go(go_pid, parent_tr):
    g = copy.deepcopy(sf.objects[go_pid].read_typetree())
    gp = new_pid(); aur[go_pid] = gp
    comps = []
    tr_pid = None
    for c in g['m_Component']:
        op = c['component']['m_PathID']
        o = sf.objects[op]
        t = copy.deepcopy(o.read_typetree())
        np_ = new_pid()
        t['m_GameObject'] = {'m_FileID': 0, 'm_PathID': gp}
        if o.type.name == 'Transform':
            t['m_Father'] = {'m_FileID': 0, 'm_PathID': parent_tr}
            assert not t['m_Children']
            tr_pid = np_
        new_objs[np_] = (o, t)
        comps.append({'component': {'m_FileID': 0, 'm_PathID': np_}})
        aur[op] = np_
    g['m_Component'] = comps
    new_objs[gp] = (sf.objects[go_pid], g)
    return gp, tr_pid

gear_tr = comp_of(GEAR_GO, 'Transform')
aur_go, aur_tr = clone_go(TPL_AURORA_GO, gear_tr)
tree(gear_tr)['m_Children'].append({'m_FileID': 0, 'm_PathID': aur_tr})
fl['m_AuroraField'] = {'m_FileID': 0, 'm_PathID': aur[fl['m_AuroraField']['m_PathID']]}
new_objs[FL_PID] = (sf.objects[TPL_FLASHLIGHT_ITEM], fl)

g = tree(GEAR_GO)
for c in g['m_Component']:
    if c['component']['m_PathID'] == CRANK_ITEM: c['component']['m_PathID'] = FL_PID
gi = tree(GEAR_ITEM)
gi['m_FlashlightItem'] = {'m_FileID': 0, 'm_PathID': FL_PID}
if 'm_CrankLampItem' in gi: gi['m_CrankLampItem'] = {'m_FileID': 0, 'm_PathID': 0}

# ---- 2. first-person hands: FirstPersonCrankLamp -> FirstPersonFlashlight
fc = tree(FP_CRANK)
ff = copy.deepcopy(sf.objects[TPL_FP_FLASHLIGHT].read_typetree())
ff['m_GameObject'] = {'m_FileID': 0, 'm_PathID': FPH_GO}
ff['m_LowFxGameObject'] = fc['m_LowFxGameObject']
ff['m_HighFxGameObject'] = fc['m_LowFxGameObject']
ff['m_LightIndoorsLow'] = fc['m_LightIndoorsLow']
ff['m_LightOutdoorsLow'] = fc['m_LightOutdoorsLow']
ff['m_LightIndoorsHigh'] = {'m_FileID': 0, 'm_PathID': 0}
ff['m_LightOutdoorsHigh'] = {'m_FileID': 0, 'm_PathID': 0}
FF_PID = new_pid()
new_objs[FF_PID] = (sf.objects[TPL_FP_FLASHLIGHT], ff)
for c in tree(FPH_GO)['m_Component']:
    if c['component']['m_PathID'] == FP_CRANK: c['component']['m_PathID'] = FF_PID
DROPPED = {CRANK_ITEM, FP_CRANK}
# AkBank (ep5 crank lamp sound bank, not in survival) and DisableObjectForXPMode (would hide it in some modes)
DROP_SCRIPTS = {8845004722290596929, 7909926044604406408}
_q = list(ROOTS.values()); _seen = set()
while _q:
    p = _q.pop()
    if p in _seen or p in surv_ids or p not in sf.objects: continue
    _seen.add(p)
    o = sf.objects[p]
    if o.type.name == 'GameObject':
        t = tree(p); keepc = []
        for c in t['m_Component']:
            cp = c['component']['m_PathID']
            co = sf.objects.get(cp)
            if co is not None and co.type.name == 'MonoBehaviour' and co.read_typetree()['m_Script']['m_PathID'] in DROP_SCRIPTS:
                DROPPED.add(cp); print('drop component', t['m_Name'], cp); continue
            keepc.append(c)
        t['m_Component'] = keepc
    def _f(pp):
        if pp['m_FileID'] == 0 and pp['m_PathID']: _q.append(pp['m_PathID'])
    walk_pptr(o.read_typetree() if p not in trees else trees[p], _f)

# ---- 3. closure of kept ep5 objects
keep = set()
q = list(ROOTS.values()) + [p for p in new_objs]
newset = set(new_objs)
while q:
    pid = q.pop()
    if pid in keep or pid in DROPPED: continue
    if pid not in newset and pid in surv_ids: continue
    keep.add(pid)
    t = new_objs[pid][1] if pid in newset else tree(pid)
    def add(pp):
        if pp['m_FileID'] == 0 and pp['m_PathID']: q.append(pp['m_PathID'])
    walk_pptr(t, add)
keep -= newset

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

# ---- 4. ep5-only objects from the PC extract (material, textures, world mesh)
idx = json.load(open(f'{EXT}/index.json', encoding='utf-8'))
imported = {}
for cab, info in idx.items():
    for it in info['items']:
        imported[it['path_id']] = (cab, info, it)
assert MAT in imported and WORLD_MESH in imported, sorted(imported)

stats = collections.Counter()
def remap(pp):
    fid, pid = pp['m_FileID'], pp['m_PathID']
    if not pid: pp['m_FileID'] = 0; return
    if fid == 0:
        if pid in keep or pid in newset: return
        if pid in surv_ids: pp['m_FileID'] = ext_index(CAB_SURV_GEAR); stats['->survgear'] += 1; return
        if pid in DROPPED: pp['m_PathID'] = 0; stats['dropped'] += 1; return
        raise RuntimeError(f'local ref {pid} not kept')
    cab = cab_of(sf, fid)
    if cab in (CAB_EP5_MATS, CAB_EP5_CHAR) and pid in imported:
        pp['m_FileID'] = 0; stats['->imported'] += 1; return
    if cab == CAB_EP5_CHAR:
        pp['m_FileID'] = ext_index(CAB_SHADERS); pp['m_PathID'] = SURV_SKINNED_SHADER; stats['->surv shader'] += 1; return
    if cab == CAB_DEFMAT:
        pp['m_FileID'] = 0; pp['m_PathID'] = 0; stats['defmat dropped'] += 1; return
    if cab == CAB_GID and pid == GID_PID:
        pp['m_FileID'] = 0; stats['->gid'] += 1; return
    pp['m_FileID'] = ext_index(cab); stats['ext ' + cab[:12]] += 1

for pid in keep: walk_pptr(trees[pid], remap)
for pid in newset: walk_pptr(new_objs[pid][1], remap)


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

for pid, (cab, info, it) in imported.items():
    t = json.load(open(f'{EXT}/{pid}.json', encoding='utf-8'))
    exts = [e.split('/')[-1] if not e.startswith('Library/') else e for e in info['externals']]
    def remap_imp(pp):
        if not pp['m_PathID']: pp['m_FileID'] = 0; return
        if pp['m_FileID'] == 0:
            assert pp['m_PathID'] in imported, pp; return
        c = exts[pp['m_FileID'] - 1]
        if c == CAB_EP5_CHAR:
            pp['m_FileID'] = ext_index(CAB_SHADERS); pp['m_PathID'] = SURV_SKINNED_SHADER; stats['imp->surv shader'] += 1; return
        if c in (CAB_EP5_MATS, CAB_EP5_CHAR):
            assert pp['m_PathID'] in imported, pp; pp['m_FileID'] = 0; return
        if c == CAB_DEFMAT:
            pp['m_FileID'] = 0; pp['m_PathID'] = 0; return
        pp['m_FileID'] = ext_index(c)
    walk_pptr(t, remap_imp)
    tn = it['type']
    if tn == 'Texture2D':
        t['image data'] = open(f'{EXT}/{it["texdata"]}', 'rb').read()
        assert len(t['image data']) == t['m_CompleteImageSize'], (pid, len(t['image data']))
        t['m_StreamData'] = {'offset': 0, 'size': 0, 'path': ''}
        t['m_PlatformBlob'] = b'' if isinstance(t.get('m_PlatformBlob'), str) else t.get('m_PlatformBlob', [])
    elif tn == 'Mesh':
        if it.get('stream'):
            t['m_VertexData']['m_DataSize'] = open(f'{EXT}/{it["stream"]}', 'rb').read()
            t['m_StreamData'] = {'offset': 0, 'size': 0, 'path': ''}
    assert tn in TEMPLATES, tn
    new_objs[pid] = (TEMPLATES[tn], t)
    stats['imported ' + tn] += 1

# ---- 5. icon, GearIcon, GearItemData (own script types)
ienv = UnityPy.load(f'{EP5}/ddba3fd744cf883c382f3beaf47c504b.bundle')
ibf, icab, isf = sfile(ienv)
assert icab == CAB_ICONS
icon = [o for p, o in ienv.container.items() if p == ICON_PATH][0]
it_ = icon.read_typetree()
inline_stream(it_, resS(ibf, icab))
ICON_PID = icon.path_id
new_objs[ICON_PID] = (TEMPLATES['Texture2D'], it_)

genv = UnityPy.load(f'{EP5}/4940df3f81e3207f88e1c1848754f83b.bundle')
gbf, gcab, gsf = sfile(genv)
assert gcab == CAB_GID
def add_script_obj(src_obj, t):
    st = copy.copy(src_obj.serialized_type)
    sref = copy.copy(gsf.script_types[st.script_type_index])
    scab_ = cab_of(gsf, sref.local_serialized_file_index) if sref.local_serialized_file_index else gcab
    sref.local_serialized_file_index = ext_index(scab_)
    sf.script_types.append(sref)
    st.script_type_index = len(sf.script_types) - 1
    sf.types.append(st)
    o = copy.copy(src_obj)
    o.assets_file = sf
    o.type_id = len(sf.types) - 1
    o.serialized_type = st
    return o

for pid in (GID_PID, GICON_PID):
    src = gsf.objects[pid]
    t = src.read_typetree()
    def remap_g(pp):
        if not pp['m_PathID']: pp['m_FileID'] = 0; return
        if pp['m_FileID'] == 0:
            if pp['m_PathID'] in (GID_PID, GICON_PID): return
            pp['m_FileID'] = ext_index(CAB_GID); return       # e.g. gameplay tag database (also in survival)
        c = cab_of(gsf, pp['m_FileID'])
        if c == CAB_ICONS and pp['m_PathID'] == ICON_PID: pp['m_FileID'] = 0; return
        pp['m_FileID'] = ext_index(c)
    walk_pptr(t, remap_g)
    new_objs[pid] = (add_script_obj(src, t), t)

# ---- 6. WINTERMUTE first-person arm clips for cranking (events removed: survival arms have no OnAudioEvent receiver)
ANIMS = os.environ['TLD_ANIMS']
ARM_CLIPS = {959415812501684677: 'HandCrankLight_Crank_Start_FP', 1756922127720572571: 'HandCrankLight_Crank_Loop_FP',
             -3401301520838556575: 'HandCrankLight_Crank_Exit_FP'}
EXTRA_ROOTS = []
for pid, nm in ARM_CLIPS.items():
    t = json.load(open(f'{ANIMS}/AnimationClip_{pid}.json', encoding='utf-8'))
    assert t['m_Name'] == nm, t['m_Name']
    t['m_Events'] = []
    walk_pptr(t, lambda pp: (_ for _ in ()).throw(RuntimeError('clip has refs')) if pp['m_PathID'] else pp.update(m_FileID=0))
    assert pid not in sf.objects and pid not in new_objs
    new_objs[pid] = (TEMPLATES['AnimationClip'], t)
    EXTRA_ROOTS.append((f'Assets/TLDCrankLamp/{nm}.anim', pid))

# ---- write
ab_pid = None
for pid in list(sf.objects.keys()):
    o = sf.objects[pid]
    if o.type.name == 'AssetBundle': ab_pid = pid; continue
    if pid in keep: o.save_typetree(trees[pid])
    else: del sf.objects[pid]
for pid, (tpl, t) in new_objs.items():
    o = tpl if tpl.assets_file is sf and tpl.path_id not in sf.objects and pid in (GID_PID, GICON_PID) else copy.copy(tpl)
    o.path_id = pid
    o.save_typetree(t)
    sf.objects[pid] = o

def tree_of(p):
    return trees[p] if p in trees else new_objs[p][1]

ab = sf.objects[ab_pid]
abt = ab.read_typetree()
preload = []; container = []
for path, rid in list(ROOTS.items()) + [(GID_PATH, GID_PID), (GICON_PATH, GICON_PID), (ICON_PATH, ICON_PID)] + EXTRA_ROOTS:
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
    i0 = len(preload); preload += deps
    container.append((path, {'preloadIndex': i0, 'preloadSize': len(deps), 'asset': {'m_FileID': 0, 'm_PathID': rid}}))
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
