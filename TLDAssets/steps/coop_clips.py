# Appends our own clips (own/coop_own_clips.bin: int count + clips in characters.bin clip format) to characters.bin.
import zlib, struct

def _clips_end(raw):
    p = 8; n = struct.unpack_from('<i', raw, p)[0]; p += 4
    def s():
        nonlocal p
        L = struct.unpack_from('<i', raw, p)[0]; p += 4 + L
    def i():
        nonlocal p
        v = struct.unpack_from('<i', raw, p)[0]; p += 4; return v
    for _ in range(n):
        s(); p += 4; fr = i(); nt = i()
        for _ in range(nt):
            s(); fl = i()
            if fl & 1: p += fr * 16
            if fl & 2: p += fr * 12
    return n, p

def append(packed, own):
    raw = zlib.decompress(packed, -15)
    assert raw[:4] == b'TLDC'
    n, ce = _clips_end(raw)
    k = struct.unpack_from('<i', own, 0)[0]
    new = raw[:8] + struct.pack('<i', n + k) + raw[12:ce] + own[4:] + raw[ce:]
    c = zlib.compressobj(9, zlib.DEFLATED, -15)
    return c.compress(new) + c.flush()
