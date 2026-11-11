# Crank lamp sound banks, made from the game's own WINTERMUTE banks.
#  - _StoryStructures.bnk: only the lamp's 54 sound objects (the full bank references buses survival does not have),
#    mixers' child lists trimmed to kept objects, tick container volume -50 -> 0, voice limits removed.
#  - TLDCrankLamp_Events.bnk: our own events playing the crank tick and start sounds directly.
#  - FlashlighCrankLamp.bnk (media) and _StoryEvents.bnk are copied as they are.
import struct, shutil, os
from bnk import chunks, hirc, load, fnv

NAMES = ["Play_Gear_FlashlightCrankLamp_Equip_Equip", "Play_Gear_FlashlightCrankLamp_Equip_Unequip",
         "Play_Gear_FlashlightCrankLamp_Gear_TurnOn", "Play_Gear_FlashlightCrankLamp_Gear_TurnOff",
         "Play_Gear_FlashlightCrankLamp_Action_Start", "Play_Gear_FlashlightCrankLamp_Action_Loop_Crank",
         "Stop_Gear_FlashlightCrankLamp_Crank", "Play_Gear_FlashlightCrankLamp_Action_Exit"]

def _refs(b, pool):
    r = []
    for o in range(4, len(b) - 3):
        v = struct.unpack_from('<I', b, o)[0]
        if v in pool and v not in r: r.append(v)
    return r

def _keep(st, ev):
    evo = {i: (t, b) for t, i, b in ev}; sto = {i: (t, b) for t, i, b in st}
    targets = set()
    for n in NAMES:
        h = fnv(n)
        if h not in evo: continue
        for a in _refs(evo[h][1], evo):
            if evo[a][0] == 3: targets |= set(_refs(evo[a][1], sto))
    keep = set(); q = list(targets)
    def parent(i):
        for v in _refs(sto[i][1], sto):
            if sto[v][0] in (7, 6, 5, 9) and i in _refs(sto[v][1], sto): return v
        return None
    while q:
        i = q.pop()
        if i in keep: continue
        keep.add(i); t, b = sto[i]
        if t == 7: continue
        for v in _refs(b, sto):
            if sto[v][0] == 7: continue
            q.append(v)
    for i in list(keep):
        p = parent(i)
        while p is not None and p not in keep and sto[p][0] == 7:
            keep.add(p); p = parent(p)
    chg = True
    while chg:
        chg = False
        for i in list(keep):
            p = parent(i)
            if p is not None and p not in keep: keep.add(p); chg = True
    return keep

def _pack(c, objs):
    h = struct.pack('<I', len(objs)) + b''.join(bytes([t]) + struct.pack('<I', len(b)) + b for t, i, b in objs)
    d = b''
    for t, b in c:
        d += (b'HIRC' + struct.pack('<I', len(h)) + h) if t == b'HIRC' else t + struct.pack('<I', len(b)) + b
    return d

def structures(full_structures, events):
    c, st = load(full_structures); _, ev = load(events)
    keep = _keep(st, ev)
    allids = {i for t, i, b in st}
    def trim(b):
        for n in range(0, 64):
            off = len(b) - 4 - 4 * n
            if off < 8: break
            if struct.unpack_from('<I', b, off)[0] == n:
                kids = [struct.unpack_from('<I', b, off + 4 + 4 * k)[0] for k in range(n)]
                if all(k in allids for k in kids):
                    kk = [k for k in kids if k in keep]
                    return b[:off] + struct.pack('<I', len(kk)) + b''.join(struct.pack('<I', k) for k in kk)
        raise Exception('no child list')
    d = _pack(c, [(t, i, trim(b) if t == 7 else b) for t, i, b in st if i in keep])
    a = bytes.fromhex('43b0890700000000000000007af9e209010101000048c2')
    assert d.count(a) == 1; d = d.replace(a, a[:-2] + b'\0\0')
    a = bytes.fromhex('7af9e2090000000000000000f8c01416000000000000000000'); b = bytes.fromhex('0c010200')
    assert d.count(a + b) == 1; d = d.replace(a + b, a + bytes.fromhex('00010000'))
    a = bytes.fromhex('43b0890700000000000000007af9e2090101010000000000000200000000')
    assert d.count(a + b'\x04') == 1; d = d.replace(a + b'\x04', a + b'\x00')
    return d

def events_bank(full_structures, events):
    c, st = load(full_structures); sto = {i: (t, b) for t, i, b in st}
    ec = chunks(open(events, 'rb').read())
    def children(cid):
        r = []
        for i, (t, bb) in sto.items():
            if t == 2 and struct.pack('<I', cid) in bb[17:60] and struct.pack('<I', i) in sto[cid][1][40:]: r.append(i)
        return sorted(r)
    ticks = children(126464067); starts = children(685954182)
    objs = []
    def add(name, sound, bank):
        eid = fnv(name); aid = fnv(name + '_action') & 0x3fffffff
        act = struct.pack('<I', aid) + bytes([0x03, 0x04]) + struct.pack('<I', sound) + bytes([0, 0, 0, 4]) + struct.pack('<I', bank) + struct.pack('<I', 0)
        objs.append((3, act)); objs.append((4, struct.pack('<I', eid) + bytes([1]) + struct.pack('<I', aid)))
    MY_BANK = fnv('TLDCrankLamp_Events'); STRUCT_BANK = 2678021237
    tmpl = sto[starts[0]][1]
    for k, s in enumerate(ticks):
        tb = sto[s][1]; nid = fnv('TLDCrank_TickSound_%02d' % k) & 0x3fffffff
        body = bytearray(tmpl); body[0:4] = struct.pack('<I', nid); body[9:17] = tb[9:17]
        objs.append((2, bytes(body)))
        add('TLDCrank_Tick_%02d' % k, nid, MY_BANK)
    for k, s in enumerate(starts): add('TLDCrank_Start_%d' % k, s, STRUCT_BANK)
    objs.sort(key=lambda o: o[0])
    hdr = bytearray([b for t, b in ec if t == b'BKHD'][0]); struct.pack_into('<I', hdr, 4, MY_BANK)
    h = struct.pack('<I', len(objs)) + b''.join(bytes([t]) + struct.pack('<I', len(b)) + b for t, b in objs)
    return b'BKHD' + struct.pack('<I', len(hdr)) + bytes(hdr) + b'HIRC' + struct.pack('<I', len(h)) + h
