"""Packs dist/Mods/MatchboxesByEMPEROR/{icons,labels,meshes,textures} into matchboxes1.pak, matchboxes2.pak… each under 24 MB
(the install tool takes files up to 25 MB); format in src/Assets.cs. Usage: pak.py <dist MatchboxesByEMPEROR dir> <out dir>"""
import os, struct, sys
root, outdir = sys.argv[1], sys.argv[2]
files = []
for d in ('icons', 'labels', 'meshes', 'textures'):
    for f in sorted(os.listdir(os.path.join(root, d))):
        files.append(d + '/' + f)
parts, cur, size = [], [], 0
for f in files:
    n = os.path.getsize(os.path.join(root, f))
    if cur and size + n > 23_000_000:
        parts.append(cur); cur, size = [], 0
    cur.append(f); size += n
parts.append(cur)
for k, part in enumerate(parts, 1):
    with open(os.path.join(outdir, 'matchboxes%d.pak' % k), 'wb') as o:
        o.write(struct.pack('<i', len(part)))
        for f in part:
            b = open(os.path.join(root, f), 'rb').read()
            n = f.encode()
            o.write(struct.pack('<i', len(n))); o.write(n); o.write(struct.pack('<i', len(b))); o.write(b)
    print('matchboxes%d.pak' % k, len(part), 'files')
