import bpy, bmesh, math, random, sys, os
from mathutils import Vector, Matrix
random.seed(7)
out=sys.argv[sys.argv.index('--')+1]; views=sys.argv[sys.argv.index('--')+2].split(',')
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene
def coll(n):
    c=bpy.data.collections.new(n); sc.collection.children.link(c); return c
C={n:coll(n) for n in ['env','kettle','table','tub']}
def add(me,name,c,loc=(0,0,0),rot=(0,0,0),smooth=False,mat=None):
    o=bpy.data.objects.new(name,me); C[c].objects.link(o); o.location=loc; o.rotation_euler=rot
    if smooth:
        for p in me.polygons: p.use_smooth=True
    if mat: me.materials.append(mat)
    return o
def bm_mesh(bm,name):
    me=bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); return me
# ---------- materials
def nodes(m): 
    m.use_nodes=True; return m.node_tree.nodes, m.node_tree.links, m.node_tree.nodes['Principled BSDF']
def mat_simple(name,col,rough=0.6,metal=0.0,emit=None,es=0):
    m=bpy.data.materials.new(name); n,l,b=nodes(m)
    b.inputs['Base Color'].default_value=(*col,1); b.inputs['Roughness'].default_value=rough; b.inputs['Metallic'].default_value=metal
    if emit: b.inputs['Emission Color'].default_value=(*emit,1); b.inputs['Emission Strength'].default_value=es
    return m
def mat_noise(name,c1,c2,scale=8,rough=0.7,metal=0.0,stretch=(1,1,1),bump=0.3,detail=8,ramp=(0.35,0.65),randomize=False,soot=None):
    m=bpy.data.materials.new(name); n,l,b=nodes(m)
    tc=n.new('ShaderNodeTexCoord'); mp=n.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value=stretch
    l.new(tc.outputs['Object'],mp.inputs['Vector'])
    if randomize:
        oi=n.new('ShaderNodeObjectInfo'); ad=n.new('ShaderNodeVectorMath'); ad.operation='ADD'
        mu=n.new('ShaderNodeMath'); mu.operation='MULTIPLY'; mu.inputs[1].default_value=37
        l.new(oi.outputs['Random'],mu.inputs[0]); l.new(tc.outputs['Object'],ad.inputs[0]); l.new(mu.outputs[0],ad.inputs[1]); l.new(ad.outputs[0],mp.inputs['Vector'])
    nz=n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value=scale; nz.inputs['Detail'].default_value=detail; nz.inputs['Roughness'].default_value=0.65
    l.new(mp.outputs[0],nz.inputs['Vector'])
    cr=n.new('ShaderNodeValToRGB'); cr.color_ramp.elements[0].position=ramp[0]; cr.color_ramp.elements[1].position=ramp[1]
    cr.color_ramp.elements[0].color=(*c1,1); cr.color_ramp.elements[1].color=(*c2,1)
    l.new(nz.outputs['Fac'],cr.inputs['Fac'])
    colout=cr.outputs['Color']
    if randomize:
        hs=n.new('ShaderNodeHueSaturation'); mr=n.new('ShaderNodeMapRange'); mr.inputs[3].default_value=0.6; mr.inputs[4].default_value=1.25
        l.new(oi.outputs['Random'],mr.inputs[0]); l.new(mr.outputs[0],hs.inputs['Value']); l.new(colout,hs.inputs['Color']); colout=hs.outputs['Color']
    if soot:  # darken above z0..z1 (object space)
        sx=n.new('ShaderNodeSeparateXYZ'); l.new(tc.outputs['Object'],sx.inputs[0])
        mr2=n.new('ShaderNodeMapRange'); mr2.inputs[1].default_value=soot[0]; mr2.inputs[2].default_value=soot[1]
        l.new(sx.outputs['Z'],mr2.inputs[0])
        nz2=n.new('ShaderNodeTexNoise'); nz2.inputs['Scale'].default_value=5; l.new(tc.outputs['Object'],nz2.inputs['Vector'])
        mm=n.new('ShaderNodeMath'); mm.operation='MULTIPLY'; l.new(mr2.outputs[0],mm.inputs[0]); l.new(nz2.outputs['Fac'],mm.inputs[1])
        mm2=n.new('ShaderNodeMath'); mm2.operation='MULTIPLY'; mm2.inputs[1].default_value=2.6; mm2.use_clamp=True; l.new(mm.outputs[0],mm2.inputs[0])
        mx=n.new('ShaderNodeMixRGB'); mx.inputs['Color2'].default_value=(0.012,0.010,0.009,1)
        l.new(mm2.outputs[0],mx.inputs['Fac']); l.new(colout,mx.inputs['Color1']); colout=mx.outputs['Color']
    l.new(colout,b.inputs['Base Color'])
    b.inputs['Roughness'].default_value=rough
    if metal:
        inv=n.new('ShaderNodeMapRange'); inv.inputs[1].default_value=ramp[0]; inv.inputs[2].default_value=ramp[1]; inv.inputs[3].default_value=metal; inv.inputs[4].default_value=0.0
        l.new(nz.outputs['Fac'],inv.inputs[0]); l.new(inv.outputs[0],b.inputs['Metallic'])
    bp=n.new('ShaderNodeBump'); bp.inputs['Strength'].default_value=bump; bp.inputs['Distance'].default_value=0.01
    l.new(nz.outputs['Fac'],bp.inputs['Height']); l.new(bp.outputs[0],b.inputs['Normal'])
    return m
M_barrel=mat_noise('barrel',(0.05,0.085,0.095),(0.15,0.065,0.03),scale=9,rough=0.75,metal=0.5,ramp=(0.44,0.6),bump=0.25,soot=(0.55,1.05))
M_rust=mat_noise('rust',(0.09,0.07,0.06),(0.32,0.13,0.05),scale=14,rough=0.8,metal=0.5,bump=0.4)
M_steel=mat_noise('steel',(0.05,0.045,0.04),(0.16,0.12,0.09),scale=30,rough=0.55,metal=0.9,bump=0.1)
M_plank=mat_noise('plank',(0.06,0.05,0.042),(0.22,0.20,0.18),scale=5,rough=0.85,stretch=(14,14,0.9),bump=0.6,randomize=True,ramp=(0.3,0.7))
M_plank_in=mat_noise('plank_in',(0.02,0.015,0.012),(0.10,0.07,0.05),scale=5,rough=0.9,stretch=(14,14,0.9),bump=0.6,randomize=True)
M_bark=mat_noise('bark',(0.05,0.035,0.025),(0.20,0.15,0.11),scale=9,rough=0.95,stretch=(6,6,1),bump=1.0,randomize=True)
M_woodend=mat_noise('woodend',(0.50,0.36,0.20),(0.72,0.56,0.36),scale=20,rough=0.8)
M_stone=mat_noise('stone',(0.12,0.12,0.12),(0.40,0.39,0.37),scale=7,rough=0.9,bump=0.8,randomize=True)
M_fish=mat_noise('fish',(0.50,0.24,0.035),(0.20,0.075,0.015),scale=7,rough=0.32,bump=0.12,stretch=(3,3,10),randomize=True)
M_meat=mat_noise('meat',(0.20,0.045,0.025),(0.07,0.02,0.012),scale=12,rough=0.38,bump=0.5,randomize=True)
M_snow=bpy.data.materials.new('snow'); n,l,b=nodes(M_snow); b.inputs['Base Color'].default_value=(0.86,0.9,0.96,1); b.inputs['Roughness'].default_value=0.6
b.inputs['Subsurface Weight'].default_value=0.4; b.inputs['Subsurface Radius'].default_value=(0.3,0.3,0.4)
nz=n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value=60; bp=n.new('ShaderNodeBump'); bp.inputs['Strength'].default_value=0.15
l.new(nz.outputs['Fac'],bp.inputs['Height']); l.new(bp.outputs[0],b.inputs['Normal'])
M_ember=bpy.data.materials.new('ember'); n,l,b=nodes(M_ember)
nz=n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value=25; cr=n.new('ShaderNodeValToRGB')
cr.color_ramp.elements[0].position=0.4; cr.color_ramp.elements[0].color=(0.02,0.01,0.01,1); cr.color_ramp.elements[1].position=0.62; cr.color_ramp.elements[1].color=(1,0.25,0.03,1)
l.new(nz.outputs['Fac'],cr.inputs['Fac']); l.new(cr.outputs[0],b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value=3.5; b.inputs['Base Color'].default_value=(0.02,0.02,0.02,1)
M_string=mat_simple('string',(0.25,0.2,0.13),0.9)
M_smoke=bpy.data.materials.new('smoke'); M_smoke.use_nodes=True; nt=M_smoke.node_tree; nt.nodes.clear()
vo=nt.nodes.new('ShaderNodeOutputMaterial'); pv=nt.nodes.new('ShaderNodeVolumePrincipled'); pv.inputs['Color'].default_value=(0.8,0.8,0.82,1)
tc=nt.nodes.new('ShaderNodeTexCoord'); nz=nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value=3.2; nz.inputs['Detail'].default_value=5
gr=nt.nodes.new('ShaderNodeTexGradient'); gr.gradient_type='SPHERICAL'
mr=nt.nodes.new('ShaderNodeMapRange'); mr.inputs[1].default_value=0.42; mr.inputs[2].default_value=0.68; mr.inputs[3].default_value=0; mr.inputs[4].default_value=1
mu=nt.nodes.new('ShaderNodeMath'); mu.operation='MULTIPLY'; mu2=nt.nodes.new('ShaderNodeMath'); mu2.operation='MULTIPLY'; mu2.inputs[1].default_value=5
nt.links.new(tc.outputs['Object'],nz.inputs['Vector']); nt.links.new(tc.outputs['Object'],gr.inputs['Vector']); nt.links.new(nz.outputs['Fac'],mr.inputs[0])
nt.links.new(mr.outputs[0],mu.inputs[0]); pw=nt.nodes.new('ShaderNodeMath'); pw.operation='POWER'; pw.inputs[1].default_value=2.5; nt.links.new(gr.outputs['Fac'],pw.inputs[0]); nt.links.new(pw.outputs[0],mu.inputs[1]); nt.links.new(mu.outputs[0],mu2.inputs[0]); nt.links.new(mu2.outputs[0],pv.inputs['Density']); nt.links.new(pv.outputs[0],vo.inputs['Volume'])
# ---------- primitives
def box(name,c,size,loc,rot=(0,0,0),mat=None,bevel=0.0):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1); 
    for v in bm.verts: v.co=Vector((v.co.x*size[0],v.co.y*size[1],v.co.z*size[2]))
    if bevel: bmesh.ops.bevel(bm,geom=bm.edges[:],offset=bevel,segments=2,affect='EDGES')
    return add(bm_mesh(bm,name),name,c,loc,rot,mat=mat)
def cyl(name,c,r,h,loc,rot=(0,0,0),mat=None,seg=20,smooth=True,capmat=None):
    bm=bmesh.new(); bmesh.ops.create_cone(bm,cap_ends=True,segments=seg,radius1=r,radius2=r,depth=h)
    me=bm_mesh(bm,name); o=add(me,name,c,loc,rot,mat=mat)
    for p in me.polygons: p.use_smooth = smooth and abs(p.normal.z)<0.9
    if capmat:
        me.materials.append(capmat)
        for p in me.polygons:
            if abs(p.normal.z)>0.9: p.material_index=1
    return o
def lathe(name,c,prof,loc,mat,seg=48,solid=0.0):
    bm=bmesh.new(); rings=[]
    for r,z in prof:
        if r==0: rings.append([bm.verts.new((0,0,z))])
        else: rings.append([bm.verts.new((r*math.cos(2*math.pi*i/seg),r*math.sin(2*math.pi*i/seg),z)) for i in range(seg)])
    for a,b2 in zip(rings,rings[1:]):
        for i in range(seg):
            j=(i+1)%seg
            if len(a)==1: bm.faces.new((a[0],b2[i],b2[j]))
            elif len(b2)==1: bm.faces.new((a[i],b2[0],a[j]))
            else: bm.faces.new((a[i],b2[i],b2[j],a[j]))
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
    o=add(bm_mesh(bm,name),name,c,loc,smooth=True,mat=mat)
    if solid:
        s=o.modifiers.new('s','SOLIDIFY'); s.thickness=solid; s.offset=-1
    return o
def rock(name,c,r,loc,sq=(1,1,0.6)):
    bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=2,radius=r)
    for v in bm.verts:
        k=1+random.uniform(-0.18,0.18); v.co=Vector((v.co.x*sq[0]*k,v.co.y*sq[1]*k,v.co.z*sq[2]*k))
    o=add(bm_mesh(bm,name),name,c,loc,(0,0,random.uniform(0,6)),mat=M_stone)
    m=o.modifiers.new('b','BEVEL'); m.width=r*0.12; m.segments=2
    return o
def fish(name,c,L,top,yaw=0.0):
    # hangs head-up: head at top (z), tail down
    T=[0,0.04,0.12,0.28,0.5,0.7,0.85,0.92,0.965,1.0]; H=[0.03,0.11,0.19,0.23,0.21,0.15,0.07,0.06,0.20,0.30]; W=[0.02,0.07,0.10,0.115,0.10,0.06,0.022,0.012,0.006,0.003]
    seg=12; bm=bmesh.new(); rings=[]
    for t,h,w in zip(T,H,W):
        rings.append([bm.verts.new((w*L*0.5*math.cos(2*math.pi*i/seg),h*L*0.5*math.sin(2*math.pi*i/seg),-t*L)) for i in range(seg)])
    for a,b2 in zip(rings,rings[1:]):
        for i in range(seg): bm.faces.new((a[i],b2[i],b2[(i+1)%seg],a[(i+1)%seg]))
    bm.faces.new(rings[0]); bm.faces.new(rings[-1][::-1])
    # tail notch
    for v in rings[-1]:
        if abs(v.co.y)<0.03*L: v.co.z+=0.05*L
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
    o=add(bm_mesh(bm,name),name,c,top,(random.uniform(-0.06,0.06),random.uniform(-0.06,0.06),yaw),smooth=True,mat=M_fish)
    m=o.modifiers.new('ss','SUBSURF'); m.levels=1; m.render_levels=2
    return o
def meat(name,c,size,top,yaw=0.0):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1); bmesh.ops.subdivide_edges(bm,edges=bm.edges[:],cuts=3,use_grid_fill=True)
    for v in bm.verts:
        p=v.co; taper=1-0.35*(0.5-p.z)  # narrower at bottom
        v.co=Vector((p.x*size[0]*taper*(1+random.uniform(-.12,.12)),p.y*size[1]*(1+random.uniform(-.25,.25)),(p.z-0.5)*size[2]+random.uniform(-.008,.008)))
    o=add(bm_mesh(bm,name),name,c,top,(random.uniform(-0.05,0.05),random.uniform(-0.05,0.05),yaw),smooth=True,mat=M_meat)
    m=o.modifiers.new('ss','SUBSURF'); m.levels=1; m.render_levels=2
    return o
def hang_row(c,p0,p1,n,drop,kinds,yaw,rod_r=0.008,scale=1.0):
    p0=Vector(p0); p1=Vector(p1); d=p1-p0
    ang=math.atan2(d.y,d.x)
    cyl('rod',c,rod_r,d.length,(p0+p1)/2,(0,math.pi/2,ang),M_steel,seg=10)
    for i in range(n):
        t=(i+0.5)/n; p=p0+d*t; k=kinds[i%len(kinds)]
        s=drop*random.uniform(0.6,1.0)
        cyl('str',c,0.0018,s,(p.x,p.y,p.z-s/2),mat=M_string,seg=6)
        top=(p.x,p.y,p.z-s)
        if k=='f': fish('fish',c,0.34*scale*random.uniform(0.85,1.1),top,yaw+random.uniform(-.3,.3))
        else: meat('meat',c,(0.10*scale,0.028*scale,0.24*scale*random.uniform(0.8,1.1)),top,yaw+random.uniform(-.3,.3))
# ---------- environment
bm=bmesh.new(); bmesh.ops.create_grid(bm,x_segments=160,y_segments=160,size=20)
import mathutils.noise as N
for v in bm.verts:
    d1=math.hypot(v.co.x,v.co.y); d2=min(math.hypot(v.co.x-4,v.co.y),math.hypot(v.co.x-8,v.co.y))
    flat=min(1,max(0,(min(d1,d2)-1.3)/1.6))
    v.co.z=(N.noise(v.co*0.35)*0.28+N.noise(v.co*1.6)*0.05)*flat+ (0.02*N.noise(v.co*6))- (1-flat)*0.03
g=add(bm_mesh(bm,'ground'),'ground','env',smooth=True,mat=M_snow)
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; wn=w.node_tree.nodes; wl=w.node_tree.links
sky=wn.new('ShaderNodeTexGradient'); tcw=wn.new('ShaderNodeTexCoord'); mpw=wn.new('ShaderNodeMapping'); mpw.inputs['Rotation'].default_value=(0,-math.pi/2,0)
crw=wn.new('ShaderNodeValToRGB'); crw.color_ramp.elements[0].position=0.47; crw.color_ramp.elements[0].color=(0.62,0.70,0.80,1); crw.color_ramp.elements[1].position=0.75; crw.color_ramp.elements[1].color=(0.22,0.36,0.58,1)
wl.new(tcw.outputs['Generated'],mpw.inputs[0]); wl.new(mpw.outputs[0],sky.inputs[0]); wl.new(sky.outputs['Fac'],crw.inputs['Fac']); wl.new(crw.outputs[0],wn['Background'].inputs['Color']); wn['Background'].inputs['Strength'].default_value=0.9
sun=bpy.data.lights.new('sun','SUN'); sun.energy=3.2; sun.angle=math.radians(4); sun.color=(1,0.86,0.7)
so=bpy.data.objects.new('sun',sun); C['env'].objects.link(so); so.rotation_euler=(math.radians(62),0,math.radians(-55))
# simple fir trees in the background
M_fir=mat_noise('fir',(0.015,0.04,0.025),(0.05,0.09,0.06),scale=10,rough=0.9,randomize=True)
for i in range(26):
    a=random.uniform(0,2*math.pi); r=random.uniform(8,13); x=4+r*math.cos(a); y=r*math.sin(a)
    if y<-2 and abs(x-3)<9: continue
    h=random.uniform(4,7)
    cyl('trunk','env',0.09,h*0.3,(x,y,h*0.15),mat=M_bark,seg=8)
    for k in range(5):
        bm=bmesh.new(); bmesh.ops.create_cone(bm,cap_ends=True,segments=10,radius1=(1.25-0.2*k)*h/6,radius2=0.02,depth=h*0.32)
        add(bm_mesh(bm,'fir'),'fir','env',(x,y,h*(0.3+0.15*k)),(0,0,random.uniform(0,3)),mat=M_fir)
# ================= STATIONS =================
M_iron=mat_noise('iron',(0.015,0.015,0.017),(0.07,0.06,0.055),scale=22,rough=0.6,metal=0.8,bump=0.25)
M_tin=mat_noise('tin',(0.42,0.43,0.44),(0.62,0.62,0.6),scale=40,rough=0.32,metal=0.95,bump=0.05)
M_paper=mat_noise('paper',(0.55,0.47,0.33),(0.72,0.64,0.48),scale=12,rough=0.9,bump=0.1,randomize=True)
M_water=mat_simple('water',(0.10,0.08,0.05),0.05)
M_brine=mat_simple('brine',(0.16,0.12,0.09),0.08)
M_stave=mat_noise('stave',(0.10,0.07,0.045),(0.30,0.22,0.14),scale=4,rough=0.8,stretch=(10,10,0.7),bump=0.5,randomize=True,ramp=(0.3,0.7))
M_table=mat_noise('tablew',(0.16,0.11,0.07),(0.36,0.27,0.17),scale=4,rough=0.75,stretch=(1.2,12,12),bump=0.5,randomize=True,ramp=(0.3,0.7))
M_saus=mat_noise('saus',(0.30,0.07,0.05),(0.45,0.16,0.11),scale=30,rough=0.35,bump=0.15,randomize=True)
M_sausdry=mat_noise('sausdry',(0.10,0.025,0.02),(0.22,0.07,0.045),scale=40,rough=0.5,bump=0.3,randomize=True)
M_salt=mat_noise('salt',(0.75,0.75,0.76),(0.95,0.95,0.96),scale=60,rough=0.8,bump=0.6)
M_sack=mat_noise('sack',(0.42,0.36,0.26),(0.62,0.55,0.42),scale=50,rough=0.95,bump=0.5)
def capsule(name,c,r,L,loc,rot,mat,seg=12):
    prof=[(0,0)]+[(r*math.sin(a),r-r*math.cos(a)) for a in (0.5,1.0,math.pi/2)]+[(r,L-r)]+[(r*math.sin(a),L-r+r*math.cos(a)) for a in (1.0,0.5)]+[(0,L)]
    o=lathe(name,c,prof,loc,mat,seg=seg); o.rotation_euler=rot; return o
def can(c,loc,rot=(0,0,0),label=True):
    e=bpy.data.objects.new('can',None); C[c].objects.link(e); e.location=loc; e.rotation_euler=rot
    b=cyl('canb',c,0.042,0.115,(0,0,0.0575),mat=M_tin,seg=24); b.parent=e
    for z in (0.004,0.111):
        r=cyl('rim',c,0.044,0.006,(0,0,z),mat=M_tin,seg=24); r.parent=e
    if label:
        l=cyl('lab',c,0.0428,0.07,(0,0,0.058),mat=M_paper,seg=24); l.parent=e
    return e
def fire_ring(c,cx,cy,r,n=9,logs=True):
    for i in range(n):
        a=i*2*math.pi/n; rock('fr',c,0.085,(cx+r*math.cos(a),cy+r*math.sin(a),0.06),(1,1,0.85))
    cyl('emb',c,r-0.05,0.04,(cx,cy,0.04),mat=M_ember,seg=20)
    if logs:
        for i in range(3):
            cyl('lg',c,0.04,0.36,(cx+random.uniform(-.04,.04),cy-0.08+0.08*i,0.10),(0,math.pi/2,random.uniform(-.6,.6)),M_bark,seg=10,capmat=M_woodend)
    pl=bpy.data.lights.new('fire','POINT'); pl.energy=25; pl.color=(1,0.4,0.1); pl.shadow_soft_size=0.1
    po=bpy.data.objects.new('fire',pl); C[c].objects.link(po); po.location=(cx,cy-0.25,0.2)
def pile(c,x,y,n=6):
    for i in range(n):
        row=i//3; cyl('wp',c,random.uniform(0.05,0.065),0.45,(x+(i%3)*0.125+row*0.06,y,0.08+row*0.11),(math.pi/2,0,random.uniform(-.08,.08)),M_bark,seg=10,capmat=M_woodend)

# ---------- 1. CANNING KETTLE at x=0
fire_ring('kettle',0,0,0.33)
# three flat stones carry the kettle
for i in range(3):
    a=math.pi/6+i*2*math.pi/3; rock('ks','kettle',0.13,(0.24*math.cos(a),0.24*math.sin(a),0.16),(1,1,1.2))
KZ=0.30
kp=[(0,0),(0.16,0.0),(0.27,0.05),(0.33,0.16),(0.34,0.30),(0.33,0.40),(0.345,0.415),(0.35,0.43)]
lathe('kettle','kettle',kp,(0,0,KZ),M_iron,solid=0.012)
for s in (-1,1):
    h=cyl('ear','kettle',0.012,0.12,(s*0.365,0,KZ+0.36),(math.pi/2,0,0),M_iron,seg=8)
cyl('water','kettle',0.325,0.01,(0,0,KZ+0.33),mat=M_water,seg=32)
for i in range(6):
    a=i*math.pi/3+0.3; can('kettle',(0.19*math.cos(a),0.19*math.sin(a),KZ+0.25),(0,0,random.uniform(0,6)),label=False)
can('kettle',(0,0,KZ+0.25),label=False)
# finished cans with paper labels on a plank
box('plank','kettle',(0.62,0.26,0.03),(0.78,-0.35,0.03),(0,0,0.25),M_table,bevel=0.003)
for i in range(4):
    can('kettle',(0.60+0.115*i,-0.40+0.03*i,0.045),(0,0,random.uniform(0,6)))
can('kettle',(0.70,-0.31,0.16),(0,0,1.0))
can('kettle',(0.98,-0.55,0.045),(math.pi/2,0,0.6),label=False)
# lid leaning on a stone
ld=lathe('klid','kettle',[(0,0.03),(0.2,0.02),(0.355,0.0)],(-0.62,-0.2,0.26),M_iron,solid=0.008); ld.rotation_euler=(math.radians(70),0,math.radians(-60))
pile('kettle',-0.95,0.45)
sm=box('steam','kettle',(1,1,1),(0.05,0.0,KZ+1.0),mat=M_smoke); sm.scale=(0.7,0.7,1.0)

# ---------- 2. SAUSAGE TABLE at x=4
TX=4.0; TH=0.86
box('top','table',(1.25,0.62,0.045),(TX,0,TH),mat=M_table,bevel=0.004)
for sx in (-1,1):
    for sy in (-1,1):
        box('leg','table',(0.07,0.07,TH),(TX+sx*0.54,sy*0.24,TH/2),mat=M_table,bevel=0.003)
    box('brace','table',(0.05,0.5,0.06),(TX+sx*0.54,0,0.25),mat=M_table)
box('shelf','table',(1.1,0.5,0.03),(TX,0,0.27),mat=M_table)
ZT=TH+0.0225
# meat grinder clamped at the left end
gx=TX-0.45
cyl('gbody','table',0.045,0.20,(gx,-0.12,ZT+0.19),(math.pi/2,0,0),M_iron,seg=16)
lathe('gfun','table',[(0.035,0.0),(0.04,0.03),(0.075,0.10),(0.078,0.105)],(gx,-0.10,ZT+0.22),M_iron,seg=20,solid=0.004)
cyl('gneck','table',0.03,0.14,(gx,-0.12,ZT+0.09),mat=M_iron,seg=12)
box('gclamp','table',(0.07,0.06,0.10),(gx,-0.12,ZT-0.03),mat=M_iron,bevel=0.004)
cyl('gout','table',0.02,0.10,(gx,-0.27,ZT+0.19),(math.pi/2,0,0),M_tin,seg=12)
cyl('gaxle','table',0.01,0.08,(gx,0.01,ZT+0.19),(math.pi/2,0,0),M_iron,seg=8)
cyl('garm','table',0.008,0.13,(gx,0.05,ZT+0.135),(0,0,0),M_iron,seg=8)
cyl('gknob','table',0.016,0.06,(gx,0.08,ZT+0.075),(math.pi/2,0,0),M_table,seg=10)
# raw sausage coming out, lying in a coil of links
for i in range(7):
    a=-1.9+i*0.52; R=0.16
    capsule('link','table',0.022,0.085,(TX-0.18+R*math.cos(a),-0.08+R*math.sin(a),ZT+0.022),(math.pi/2,0,a),M_saus)
# finished: a ring of dry sausage and three boiled ones
for i in range(9):
    a=i*2*math.pi/9; R=0.105
    capsule('ring','table',0.019,0.075,(TX+0.36+R*math.cos(a),0.12+R*math.sin(a),ZT+0.019),(math.pi/2,0,a),M_sausdry)
for i in range(3):
    capsule('boil','table',0.026,0.19,(TX+0.20+0.065*i,-0.26,ZT+0.026),(math.pi/2,0,math.pi/2+0.1*i-0.1),M_saus)
# meat on a board, bowl with mince, knife
box('board','table',(0.30,0.22,0.02),(TX+0.02,0.17,ZT+0.01),(0,0,0.15),M_plank,bevel=0.003)
for i in range(3):
    m=meat('mt','table',(0.09,0.06,0.05),(TX-0.04+0.07*i,0.17+0.02*i,ZT+0.07),0.3*i)
lathe('bowl','table',[(0,0),(0.07,0.0),(0.11,0.05),(0.115,0.055)],(TX-0.22,0.2,ZT),M_tin,seg=24,solid=0.004)
lathe('mince','table',[(0,0.045),(0.06,0.04),(0.10,0.03)],(TX-0.22,0.2,ZT),M_meat,seg=16)
box('blade','table',(0.16,0.028,0.002),(TX+0.48,-0.12,ZT+0.002),(0,0,0.5),M_tin)
box('haft','table',(0.09,0.022,0.016),(TX+0.375,-0.18,ZT+0.008),(0,0,0.5),M_plank,bevel=0.003)
# guts in a jar on the shelf, salt sack under the table
lathe('jar','table',[(0,0),(0.07,0),(0.075,0.14),(0.06,0.16),(0.06,0.18)],(TX+0.3,0,0.285),M_tin,seg=20,solid=0.003)
box('sack','table',(0.34,0.24,0.42),(TX-0.2,0.02,0.49),(0.05,0.03,0.2),M_sack,bevel=0.06)

# ---------- 3. SALTING TUB at x=8
UX=8.0
# staves
nst=22; R0=0.30; R1=0.36; UH=0.62
for i in range(nst):
    a=i*2*math.pi/nst; rm=(R0+R1)/2; tilt=math.atan2(R1-R0,UH)
    w=2*math.pi*rm/nst*1.02
    b=box('stave','tub',(0.022,w,UH),(UX+rm*math.cos(a),rm*math.sin(a),UH/2+0.02),(0,tilt,a),M_stave,bevel=0.003)
    b.rotation_euler=(0,tilt,a)
for z,rr in ((0.14,R0+0.028),(0.48,R1-0.012)):
    lathe('hoop','tub',[(rr+0.0,z-0.02),(rr+0.008,z-0.02),(rr+0.014,z+0.02),(rr+0.006,z+0.02)],(UX,0,0.0),M_rust,seg=40)
cyl('bottom','tub',R0,0.03,(UX,0,0.05),mat=M_stave,seg=28)
cyl('brine','tub',R1-0.03,0.01,(UX,0,UH-0.10),mat=M_brine,seg=32)
for i in range(5):
    a=i*1.3; m=meat('sm','tub',(0.11,0.07,0.06),(UX+0.14*math.cos(a),0.14*math.sin(a),UH-0.03),a)
cyl('saltcap','tub',0.12,0.012,(UX+0.02,0.03,UH-0.085),mat=M_salt,seg=16)
# lid with a press stone, leaning; salt bag; ladle
for i in range(5):
    box('lidp','tub',(0.125,0.60-0.10*abs(i-2)**1.4,0.022),(UX-0.62+0.128*i-0.256+0.256,-0.36,0.03),(0,0,0),M_stave,bevel=0.003)
rock('press','tub',0.13,(UX-0.62,-0.36,0.13),(1,0.9,0.7))
box('saltbag','tub',(0.30,0.20,0.40),(UX+0.62,-0.15,0.20),(0.0,0.06,-0.3),M_sack,bevel=0.05)
cyl('saltheap','tub',0.11,0.02,(UX+0.55,-0.48,0.012),mat=M_salt,seg=14)
# ---------- render
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=int(os.environ.get('SAMPLES','70')); sc.cycles.use_denoising=True
sc.cycles.volume_step_rate=2.0; sc.cycles.max_bounces=6; sc.cycles.volume_bounces=0
sc.render.resolution_x=int(os.environ.get('RES','900')); sc.render.resolution_y=int(os.environ.get('RES','900'))
sc.view_settings.view_transform='AgX'; sc.view_settings.look='AgX - Medium High Contrast'
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
V={'kettle':((1.9,-2.5,1.6),(0.15,-0.1,0.42),36),
   'table':((TX+1.7,-2.3,1.9),(TX,0,0.72),36),
   'tub':((UX+1.6,-2.2,1.6),(UX,-0.1,0.38),36)}
for v in views:
    loc,tgt,fov=V[v]
    cam.location=loc; cam.data.angle=math.radians(fov); cam.rotation_euler=(Vector(tgt)-Vector(loc)).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=out+'_'+v+'.png'; bpy.ops.render.render(write_still=True)
