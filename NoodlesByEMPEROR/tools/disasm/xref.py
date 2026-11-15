import sys, struct, bisect, pickle, os, pefile
D=os.path.dirname(os.path.abspath(__file__)); M=pickle.load(open(D+'/map.pkl','rb'))
pe=pefile.PE('/mnt/user-data/uploads/TheLongDark/GameAssembly.dll', fast_load=True); data=pe.get_memory_mapped_image()
starts=sorted(M); byname={}
for r,(n,l) in M.items(): byname.setdefault(n,[]).append(r)
lo=min(starts); hi=max(starts)+0x4000
import re
for name in sys.argv[1:]:
    for t in byname[name]:
        out=set()
        i=lo
        blob=bytes(data[lo:hi])
        for m in re.finditer(b'[\xe8\xe9]', blob):
            p=m.start()
            if p+5>len(blob): break
            rel=struct.unpack_from('<i', blob, p+1)[0]
            if lo+p+5+rel==t:
                k=bisect.bisect_right(starts, lo+p)-1; s=starts[k]
                out.add(M[s][0])
        print(name, hex(t), '<-', sorted(out))
