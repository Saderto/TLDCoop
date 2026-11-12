# Builds Mods/FeatsByEMPEROR/featsbyemperor.bundle: 12 badge textures (DXT5, 512x512) in an asset bundle made from the
# game's own badge bundle (same Unity version and serialization), everything else in it removed.
import UnityPy, copy, hashlib, sys, os, glob, struct
import numpy as np, etcpak
from PIL import Image

SRC = '<GAME>/tld_Data/StreamingAssets/aa/StandaloneWindows64/398769bf90584cf8d875e73288abd3ef.bundle'
ICONS = sys.argv[1] if len(sys.argv) > 1 else './icons'
OUT = sys.argv[2] if len(sys.argv) > 2 else './build/featsbyemperor.bundle'
NAME = 'featsbyemperor.bundle'
NEW_CAB = 'CAB-' + hashlib.md5(b'FeatsByEMPEROR-badges-1').hexdigest()

env = UnityPy.load(SRC)
bf = list(env.files.values())[0]
old_cab, sf = None, None
for n, f in bf.files.items():
    if hasattr(f, 'objects'): old_cab, sf = n, f
tpl = None; ab_pid = None
for pid, o in sf.objects.items():
    if o.type.name == 'AssetBundle': ab_pid = pid
    elif o.type.name == 'Texture2D' and tpl is None:
        t = o.read_typetree()
        if t['m_Name'] == 'badge_feat_firemaster': tpl = o
assert tpl is not None and ab_pid is not None
tt = tpl.read_typetree()
print('template', tt['m_Name'], tt['m_TextureFormat'], tt['m_MipCount'], tt.get('m_StreamData'))

new = {}
for path in sorted(glob.glob(f'{ICONS}/badge_feat_sgl_*.png')):
    name = os.path.splitext(os.path.basename(path))[0]
    im = Image.open(path).convert('RGBA').resize((512, 512), Image.LANCZOS)
    arr = np.ascontiguousarray(np.array(im.transpose(Image.FLIP_TOP_BOTTOM)))
    # BC3 wants BGRA in etcpak
    bgra = arr  # etcpak takes RGBA
    data = etcpak.compress_bc3(bgra.tobytes(), 512, 512)
    t = copy.deepcopy(tt)
    t['m_Name'] = name
    t['m_Width'] = 512; t['m_Height'] = 512
    t['m_TextureFormat'] = 12
    t['m_MipCount'] = 1
    t['m_CompleteImageSize'] = len(data)
    t['image data'] = data
    t['m_StreamData'] = {'offset': 0, 'size': 0, 'path': ''}
    if 'm_IsReadable' in t: t['m_IsReadable'] = False
    pid = struct.unpack('<q', hashlib.md5(name.encode()).digest()[:8])[0]
    new[pid] = (name, t)

for pid in list(sf.objects.keys()):
    if pid != ab_pid: del sf.objects[pid]
for pid, (name, t) in new.items():
    o = copy.copy(tpl)
    o.path_id = pid
    o.save_typetree(t)
    sf.objects[pid] = o

ab = sf.objects[ab_pid]
abt = ab.read_typetree()
preload, container = [], []
for pid, (name, t) in sorted(new.items(), key=lambda x: x[1][0]):
    container.append((f'Assets/ArtAssets/UI/BadgeIcons/{name}.png',
                      {'preloadIndex': len(preload), 'preloadSize': 1, 'asset': {'m_FileID': 0, 'm_PathID': pid}}))
    preload.append({'m_FileID': 0, 'm_PathID': pid})
abt['m_Container'] = container
abt['m_PreloadTable'] = preload
abt['m_Name'] = NAME
abt['m_AssetBundleName'] = NAME
abt['m_Dependencies'] = []
ab.save_typetree(abt)
sf.externals = []

new_files = {}
for n, f in bf.files.items():
    if n == old_cab: new_files[NEW_CAB] = f
    elif n == old_cab + '.resS': continue
    else: new_files[n] = f
bf.files = new_files
sf.name = NEW_CAB
os.makedirs(os.path.dirname(OUT), exist_ok=True)
out = bf.save(packer='lz4')
open(OUT, 'wb').write(out)
print('wrote', OUT, len(out), 'bytes', NEW_CAB, len(new), 'textures')

# check: read back
e2 = UnityPy.load(OUT)
for p, o in e2.container.items():
    d = o.read()
    print(p, d.m_Width, d.m_Height, d.m_TextureFormat)
    d.image.save('/tmp/check_' + d.m_Name + '.png')
