import bpy, bmesh, math, random, sys, os
from mathutils import Vector, Matrix
random.seed(7)
out=sys.argv[sys.argv.index('--')+1]; views=sys.argv[sys.argv.index('--')+2].split(',')
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene
def coll(n):
    c=bpy.data.collections.new(n); sc.collection.children.link(c); return c
C={n:coll(n) for n in ['env','barrel','lid_on','lid_off','hang','shed']}
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
bm=bmesh.new(); bmesh.ops.create_grid(bm,x_segments=160,y_segments=160,size=14)
import mathutils.noise as N
for v in bm.verts:
    d1=math.hypot(v.co.x,v.co.y); d2=math.hypot(v.co.x-6,v.co.y)
    flat=min(1,max(0,(min(d1,d2)-0.7)/1.6))
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
    a=random.uniform(0,2*math.pi); r=random.uniform(8,13); x=3+r*math.cos(a); y=r*math.sin(a)
    if y<-2 and abs(x-3)<9: continue
    h=random.uniform(4,7)
    cyl('trunk','env',0.09,h*0.3,(x,y,h*0.15),mat=M_bark,seg=8)
    for k in range(5):
        bm=bmesh.new(); bmesh.ops.create_cone(bm,cap_ends=True,segments=10,radius1=(1.25-0.2*k)*h/6,radius2=0.02,depth=h*0.32)
        add(bm_mesh(bm,'fir'),'fir','env',(x,y,h*(0.3+0.15*k)),(0,0,random.uniform(0,3)),mat=M_fir)
# ---------- BARREL smokehouse at origin
Z0=0.13
for i in range(4):
    a=math.pi/4+i*math.pi/2; rock('st','barrel',0.11,(0.25*math.cos(a),0.25*math.sin(a),0.05))
prof=[(0,0),(0.28,0),(0.285,0.012),(0.285,0.27),(0.296,0.285),(0.296,0.30),(0.285,0.315),(0.285,0.565),(0.296,0.58),(0.296,0.595),(0.285,0.61),(0.285,0.868),(0.292,0.88)]
bar=lathe('barrel','barrel',prof,(0,0,Z0),M_barrel,solid=0.004)
cut=box('cut','barrel',(0.24,0.3,0.19),(0,-0.28,Z0+0.14)); cut.hide_render=True
bo=bar.modifiers.new('bool','BOOLEAN'); bo.object=cut; bo.operation='DIFFERENCE'
# firebox door (open, hinged left) + frame
box('door','barrel',(0.25,0.006,0.20),(-0.19,-0.385,Z0+0.14),(0,0,math.radians(60)),M_rust,bevel=0.002)
cyl('hinge','barrel',0.008,0.2,(-0.125,-0.262,Z0+0.14),mat=M_steel,seg=8)
# embers & logs inside
cyl('emb','barrel',0.24,0.03,(0,0,Z0+0.03),mat=M_ember,seg=24)
for i in range(3):
    cyl('lg','barrel',0.035,0.3,(random.uniform(-.05,.05),-0.08+0.07*i,Z0+0.08),(0,math.pi/2,random.uniform(-.5,.5)),M_bark,seg=10,capmat=M_woodend)
pl=bpy.data.lights.new('fire','POINT'); pl.energy=30; pl.color=(1,0.35,0.08); pl.shadow_soft_size=0.08
po=bpy.data.objects.new('fire',pl); C['barrel'].objects.link(po); po.location=(0,-0.12,Z0+0.13)
# drip tray / baffle plate inside
cyl('baffle','barrel',0.27,0.004,(0,0,Z0+0.34),mat=M_steel,seg=32)
# rods + food
ztop=Z0+0.875
for y in (-0.13,0.0,0.13):
    hw=math.sqrt(0.30**2-y**2)
    hang_row('hang',(-hw,y,ztop+0.008),(hw,y,ztop+0.008),4 if y==0 else 3,0.04,['f','m'] if y==0 else ['m','f'],math.pi/2,scale=0.95)
# lid (on)
lidp=[(0,0.012),(0.27,0.012),(0.30,0.0),(0.305,-0.02)]
def make_lid(c,loc,rot):
    e=bpy.data.objects.new('lid_'+c,None); C[c].objects.link(e); e.location=loc; e.rotation_euler=rot
    l1=lathe('lid',c,lidp,(0,0,0),M_barrel,solid=0.004); l1.parent=e
    p=lathe('pipe',c,[(0.045,0.0),(0.045,0.22),(0.05,0.225)],(0.12,0.05,0.012),M_rust,seg=20,solid=0.003); p.parent=e
    cp=lathe('cap',c,[(0,0.30),(0.08,0.26)],(0.12,0.05,0.012),M_rust,seg=20,solid=0.003); cp.parent=e
    for dx in(-0.03,0.03): cyl('capleg',c,0.003,0.06,(0.12+dx,0.05,0.25),mat=M_steel,seg=6).parent=e
    h=cyl('handle',c,0.008,0.14,(-0.1,-0.08,0.045),(0,math.pi/2,0.5),M_steel,seg=8); h.parent=e
    return e
make_lid('lid_on',(0,0,ztop+0.03),(0,0,0))
make_lid('lid_off',(0.52,-0.25,0.30),(math.radians(72),0,math.radians(65)))
sm=box('smoke','lid_on',(1,1,1),(0.2,0.05,ztop+0.72),mat=M_smoke); sm.scale=(0.8,0.7,1.1)
al=bpy.data.lights.new('top','AREA'); al.energy=22; al.size=0.4; al.color=(1,0.9,0.8)
ao=bpy.data.objects.new('top',al); C['lid_off'].objects.link(ao); ao.location=(0.1,-0.1,ztop+0.7); ao.rotation_euler=(math.radians(8),math.radians(6),0)
# wood pile + sticks
for i in range(7):
    row=i//4; cyl('wp','barrel',random.uniform(0.045,0.06),0.42,(-0.75+ (i%4)*0.115+row*0.06,0.25,0.07+row*0.1),(math.pi/2,0,random.uniform(-.08,.08)),M_bark,seg=10,capmat=M_woodend)
# ---------- OLD SHED at x=6
SX=6.0; Wd=1.15; Dp=1.05; Hh=1.85; th=0.022
def wall(c,p0,p1,h,skip=None,mat=M_plank,h_fn=None):
    p0=Vector(p0); p1=Vector(p1); d=p1-p0; L=d.length; ang=math.atan2(d.y,d.x); x=0
    while x<L-0.01:
        w=min(random.uniform(0.11,0.16),L-x); cx=x+w/2
        if not(skip and skip[0]<cx<skip[1]):
            hh=h_fn(cx/L) if h_fn else h
            hh+=random.uniform(-0.015,0.0)
            p=p0+d*(cx/L)
            box('pl',c,(w-0.006,th,hh),(p.x,p.y,hh/2),(random.uniform(-.006,.006),0,ang),mat,bevel=0.002)
        x+=w
gable=lambda t: Hh+ (0.5-abs(t-0.5))*2*0.42
wall('shed',(SX-Wd/2,-Dp/2,0),(SX+Wd/2,-Dp/2,0),Hh,skip=(0.22,0.93),h_fn=gable)   # front with door gap
wall('shed',(SX-Wd/2,Dp/2,0),(SX+Wd/2,Dp/2,0),Hh,h_fn=gable)
wall('shed',(SX-Wd/2,-Dp/2,0),(SX-Wd/2,Dp/2,0),Hh)
wall('shed',(SX+Wd/2,-Dp/2,0),(SX+Wd/2,Dp/2,0),Hh)
# lintel above door
box('lintel','shed',(0.75,th,0.5),(SX+0.0,-Dp/2,1.62+0.25),mat=M_plank)
box('lintel2','shed',(0.75,0.03,0.08),(SX,-Dp/2-0.02,1.60),mat=M_plank)
# door: open ~105deg, hinged at right edge
hx=SX+0.36; hy=-Dp/2-0.02; a=math.radians(100)
e=bpy.data.objects.new('doorpivot',None); C['shed'].objects.link(e); e.location=(hx,hy,0); e.rotation_euler=(0,0,a)
for i in range(5):
    b=box('dp','shed',(0.138,th,1.56),(-0.07-0.142*i,0,0.80),mat=M_plank,bevel=0.002); b.parent=e
for z in (0.3,1.3):
    b=box('db','shed',(0.70,0.02,0.09),(-0.355,-0.02,z),mat=M_plank); b.parent=e
b=box('dz','shed',(1.18,0.02,0.08),(-0.355,-0.02,0.8),(0,math.radians(58),0),M_plank); b.parent=e
# roof: two slopes + snow
rl=math.hypot(Wd/2+0.15,0.42+0.1); ra=math.atan2(0.42,Wd/2)
for s in (-1,1):
    cx=SX+s*(Wd/4+0.06); cz=Hh+0.21-0.035
    box('roof','shed',(rl,Dp+0.3,0.03),(cx,0,cz),(0,s*ra,0),M_plank_in)
    sn=box('rsnow','shed',(rl*0.97,Dp+0.26,0.10),(cx-s*0.0,0,cz+0.07),(0,s*ra,0),M_snow,bevel=0.04)
    for p in sn.data.polygons: p.use_smooth=True
# inside: dark back, rods, food, firebox
box('floor','shed',(Wd,Dp,0.02),(SX,0,0.01),mat=M_plank_in)
for z,kinds,n in ((1.62,['f','f','m'],5),(1.62,['m','f'],5)):
    pass
hang_row('shed',(SX-Wd/2+0.03,-0.22,1.66),(SX+Wd/2-0.03,-0.22,1.66),6,0.05,['f','f','m'],math.pi/2,rod_r=0.012,scale=1.15)
hang_row('shed',(SX-Wd/2+0.03,0.05,1.66),(SX+Wd/2-0.03,0.05,1.66),6,0.05,['m','f','m'],math.pi/2,rod_r=0.012,scale=1.15)
hang_row('shed',(SX-Wd/2+0.03,0.30,1.66),(SX+Wd/2-0.03,0.30,1.66),6,0.05,['f','m'],math.pi/2,rod_r=0.012,scale=1.15)
hang_row('shed',(SX-Wd/2+0.03,-0.08,1.12),(SX+Wd/2-0.03,-0.08,1.12),5,0.04,['m','f'],math.pi/2,rod_r=0.012,scale=1.1)
# stone fire ring on the floor with embers
for i in range(9):
    a=i*2*math.pi/9; rock('fr','shed',0.075,(SX+0.27*math.cos(a),0.02+0.27*math.sin(a),0.06),(1,1,0.8))
cyl('emb2','shed',0.22,0.04,(SX,0.02,0.04),mat=M_ember,seg=20)
for i in range(3):
    cyl('lg2','shed',0.04,0.34,(SX+random.uniform(-.04,.04),0.02-0.08+0.08*i,0.10),(0,math.pi/2,random.uniform(-.6,.6)),M_bark,seg=10,capmat=M_woodend)
pl=bpy.data.lights.new('fire2','POINT'); pl.energy=30; pl.color=(1,0.4,0.1); pl.shadow_soft_size=0.1
po=bpy.data.objects.new('fire2',pl); C['shed'].objects.link(po); po.location=(SX,0.0,0.3)
fl=bpy.data.lights.new('fill','AREA'); fl.energy=28; fl.size=0.6; fl.color=(1,0.9,0.8)
fo=bpy.data.objects.new('fill',fl); C['shed'].objects.link(fo); fo.location=(SX-0.15,-1.9,1.3); fo.rotation_euler=(math.radians(88),0,math.radians(-4))
sm3=box('smoke3','shed',(1,1,1),(SX+0.1,-0.2,Hh+0.45),mat=M_smoke); sm3.scale=(2.0,1.7,1.5)
# snow drift at shed base & chopping block
for s in (-1,1):
    d=box('drift','shed',(0.5,Dp+0.4,0.22),(SX+s*(Wd/2+0.2),0,0.03),(0,s*-0.35,0),M_snow,bevel=0.09)
    for p in d.data.polygons: p.use_smooth=True
cyl('block','shed',0.2,0.38,(SX-1.15,-0.75,0.19),mat=M_bark,seg=16,capmat=M_woodend)
for i in range(6):
    row=i//3; cyl('wp2','shed',random.uniform(0.05,0.065),0.45,(SX+0.95+(i%3)*0.125+row*0.06,-0.35,0.08+row*0.11),(math.pi/2,0,random.uniform(-.08,.08)),M_bark,seg=10,capmat=M_woodend)
# ---------- render
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=int(os.environ.get('SAMPLES','90')); sc.cycles.use_denoising=True
sc.cycles.volume_step_rate=2.0; sc.cycles.max_bounces=6; sc.cycles.volume_bounces=0
sc.render.resolution_x=int(os.environ.get('RES','1000')); sc.render.resolution_y=int(os.environ.get('RES','1000'))
sc.view_settings.view_transform='AgX'; sc.view_settings.look='AgX - Medium High Contrast'
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
V={'barrel':((1.75,-2.55,1.3),(0.08,0,0.78),36,['lid_on']),
   'open':((1.25,-1.55,2.2),(0.12,-0.05,0.72),40,['lid_off']),
   'shed':((SX-1.6,-4.4,1.5),(SX+0.1,0,1.22),37,[])}
for v in views:
    loc,tgt,fov,show=V[v]
    C['lid_on'].hide_render='lid_on' not in show; C['lid_off'].hide_render='lid_off' not in show
    cam.location=loc; cam.data.angle=math.radians(fov); cam.rotation_euler=(Vector(tgt)-Vector(loc)).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=out+'_'+v+'.png'; bpy.ops.render.render(write_still=True)
