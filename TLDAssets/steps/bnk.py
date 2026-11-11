import struct, collections
def chunks(d):
    p=0; out=[]
    while p<len(d):
        tag,ln=struct.unpack('<4sI',d[p:p+8]); out.append((tag,d[p+8:p+8+ln])); p+=8+ln
    return out
def hirc(body):
    n=struct.unpack('<I',body[:4])[0]; q=4; objs=[]
    for i in range(n):
        t=body[q]; l=struct.unpack('<I',body[q+1:q+5])[0]; b=body[q+5:q+5+l]
        objs.append((t,struct.unpack('<I',b[:4])[0],b)); q+=5+l
    return objs
def load(f):
    c=chunks(open(f,'rb').read())
    h=[b for t,b in c if t==b'HIRC']
    return c, (hirc(h[0]) if h else [])
def fnv(s):
    h=2166136261
    for ch in s.lower().encode(): h=(h*16777619)&0xffffffff; h^=ch
    return h
