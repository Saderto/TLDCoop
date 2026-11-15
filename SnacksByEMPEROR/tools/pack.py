"""Packs ../tex, ../meshes, ../icons into assets1.pak, assets2.pak… (each under 20 MB: the copy to the player's PC takes at most 25 MB a file).
Each: int count; per file ushort name length, utf-8 name, long offset, int size; then the bytes. python3 pack.py <out dir>"""
import os, struct, sys
HERE = os.path.dirname(os.path.abspath(__file__))
out = sys.argv[1]
files = []
for src, dst in (('tex', 'textures'), ('meshes', 'meshes'), ('icons', 'icons')):
    d = os.path.join(HERE, '..', src)
    for f in sorted(os.listdir(d)): files.append((dst + '/' + f, os.path.join(d, f)))
parts, cur, size = [], [], 0
for f in files:
    s = os.path.getsize(f[1])
    if cur and size + s > 20e6: parts.append(cur); cur, size = [], 0
    cur.append(f); size += s
parts.append(cur)
for k, part in enumerate(parts, 1):
    head = 4 + sum(2 + len(n.encode()) + 12 for n, _ in part)
    path = os.path.join(out, f'assets{k}.pak')
    with open(path, 'wb') as o:
        o.write(struct.pack('<i', len(part))); at = head
        for n, p in part:
            b = n.encode(); o.write(struct.pack('<H', len(b)) + b + struct.pack('<qi', at, os.path.getsize(p))); at += os.path.getsize(p)
        for n, p in part: o.write(open(p, 'rb').read())
    print(path, len(part), 'files', os.path.getsize(path) // 1024, 'KB')
