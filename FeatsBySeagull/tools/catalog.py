# Decode an Addressables catalog.json: catalog.py <catalog.json> <substring>  -> asset internalId + bundle(s)
import json, base64, struct, sys
c = json.load(open(sys.argv[1]))
ids = c['m_InternalIds']
kd = base64.b64decode(c['m_KeyDataString']); bd = base64.b64decode(c['m_BucketDataString']); ed = base64.b64decode(c['m_EntryDataString'])
def keys():
    n = struct.unpack_from('<i', kd, 0)[0]; out = []; p = 4
    return n
def readkey(off):
    t = kd[off]; off += 1
    if t == 0: l = struct.unpack_from('<i', kd, off)[0]; return kd[off+4:off+4+l].decode('ascii', 'replace')
    if t == 1: l = struct.unpack_from('<i', kd, off)[0]; return kd[off+4:off+4+l].decode('utf-16le', 'replace')
    if t in (2,): return struct.unpack_from('<H', kd, off)[0]
    if t in (3,4): return struct.unpack_from('<i', kd, off)[0]
    return ('t%d' % t)
nb = struct.unpack_from('<i', bd, 0)[0]; p = 4; buckets = []
for i in range(nb):
    off, cnt = struct.unpack_from('<ii', bd, p); p += 8
    ents = struct.unpack_from('<%di' % cnt, bd, p); p += 4*cnt
    buckets.append((off, ents))
ne = struct.unpack_from('<i', ed, 0)[0]
E = [struct.unpack_from('<7i', ed, 4 + 28*i) for i in range(ne)]
def iid(e): return ids[E[e][0]]
sub = sys.argv[2].lower()
for i, e in enumerate(E):
    if sub in ids[e[0]].lower():
        dk = e[2]
        deps = [iid(x) for x in buckets[dk][1]] if dk >= 0 else []
        print(ids[e[0]], '| primary', readkey(buckets[e[5]][0]), '| deps', deps)
