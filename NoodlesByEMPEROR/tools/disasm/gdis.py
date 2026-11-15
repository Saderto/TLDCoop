import re, sys, os, glob, pickle, pefile
from capstone import *
D=os.path.dirname(os.path.abspath(__file__))
def build():
    m={}
    for f in glob.glob(D+'/src/**/*.cs', recursive=True):
        cls=os.path.basename(f)[:-3]; pend=None
        for line in open(f, errors='ignore'):
            a=re.search(r'\[Address\(RVA = "0x([0-9A-F]+)", Offset = "0x([0-9A-F]+)"(?:, Length = "0x([0-9A-F]+)")?', line)
            if a: pend=(int(a.group(1),16), int(a.group(3) or '0',16)); continue
            if pend and '(' in line and not line.strip().startswith('['):
                nm=re.search(r'([\w.<>]+)\s*\(', line)
                m.setdefault(pend[0], (cls+'.'+(nm.group(1) if nm else '?'), pend[1])); pend=None
    pickle.dump(m, open(D+'/map.pkl','wb')); return m
M=pickle.load(open(D+'/map.pkl','rb')) if os.path.exists(D+'/map.pkl') else build()
pe=pefile.PE('/mnt/user-data/uploads/TheLongDark/GameAssembly.dll', fast_load=True)
data=pe.get_memory_mapped_image()
md=Cs(CS_ARCH_X86, CS_MODE_64)
byname={}
for r,(n,l) in M.items(): byname.setdefault(n,[]).append((r,l))
def dis(name):
    for rva,ln in byname.get(name,[]):
        print(f'==== {name} rva {rva:#x} len {ln:#x}')
        for i in md.disasm(bytes(data[rva:rva+ln]), rva):
            s=f'{i.mnemonic} {i.op_str}'
            if i.mnemonic in ('call','jmp') and i.op_str.startswith('0x'):
                t=int(i.op_str,16)
                if t in M: s+=f'   ; {M[t][0]}'
                elif i.mnemonic=='jmp' and rva<=t<rva+ln: pass
            print(f'{i.address:#x}: {s}')
for n in sys.argv[1:]: dis(n)
