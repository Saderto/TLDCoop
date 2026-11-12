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
# ================= STATIONS v2: worn materials, real parts =================
def mat_rich(name,cols,scale=6,rough=0.7,metal=0.0,stretch=(1,1,1),bump=0.5,randomize=False,grime=0.6,fine=40,rvar=0.25,soot_below=None,emit=None):
    """cols: list of (pos,(r,g,b)). Big noise picks the colour, fine noise breaks it, AO darkens the creases, roughness varies."""
    m=bpy.data.materials.new(name); n,l,b=nodes(m)
    tc=n.new('ShaderNodeTexCoord'); mp=n.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value=stretch
    src=tc.outputs['Object']
    if randomize:
        oi=n.new('ShaderNodeObjectInfo'); ad=n.new('ShaderNodeVectorMath'); ad.operation='ADD'
        mu=n.new('ShaderNodeMath'); mu.operation='MULTIPLY'; mu.inputs[1].default_value=53
        l.new(oi.outputs['Random'],mu.inputs[0]); l.new(src,ad.inputs[0]); l.new(mu.outputs[0],ad.inputs[1]); src=ad.outputs[0]
    l.new(src,mp.inputs['Vector'])
    nz=n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value=scale; nz.inputs['Detail'].default_value=10; nz.inputs['Roughness'].default_value=0.7
    l.new(mp.outputs[0],nz.inputs['Vector'])
    cr=n.new('ShaderNodeValToRGB'); e=cr.color_ramp.elements
    e[0].position=cols[0][0]; e[0].color=(*cols[0][1],1); e[1].position=cols[-1][0]; e[1].color=(*cols[-1][1],1)
    for p,c in cols[1:-1]:
        x=e.new(p); x.color=(*c,1)
    l.new(nz.outputs['Fac'],cr.inputs['Fac'])
    f2=n.new('ShaderNodeTexNoise'); f2.inputs['Scale'].default_value=fine; f2.inputs['Detail'].default_value=6; l.new(src,f2.inputs['Vector'])
    mr=n.new('ShaderNodeMapRange'); mr.inputs[1].default_value=0.3; mr.inputs[2].default_value=0.7; mr.inputs[3].default_value=0.62; mr.inputs[4].default_value=1.2
    l.new(f2.outputs['Fac'],mr.inputs[0])
    mx=n.new('ShaderNodeMixRGB'); mx.blend_type='MULTIPLY'; mx.inputs['Fac'].default_value=1
    l.new(cr.outputs['Color'],mx.inputs['Color1']); l.new(mr.outputs[0],mx.inputs['Color2'])
    colout=mx.outputs['Color']
    if randomize:
        hs=n.new('ShaderNodeHueSaturation'); m2=n.new('ShaderNodeMapRange'); m2.inputs[3].default_value=0.6; m2.inputs[4].default_value=1.2
        l.new(oi.outputs['Random'],m2.inputs[0]); l.new(m2.outputs[0],hs.inputs['Value']); l.new(colout,hs.inputs['Color']); colout=hs.outputs['Color']
    # grime in the creases
    ao=n.new('ShaderNodeAmbientOcclusion'); ao.inputs['Distance'].default_value=0.06; ao.samples=8
    pw=n.new('ShaderNodeMath'); pw.operation='POWER'; pw.inputs[1].default_value=1.0+grime*2.5; l.new(ao.outputs['AO'],pw.inputs[0])
    mg=n.new('ShaderNodeMixRGB'); mg.blend_type='MULTIPLY'; mg.inputs['Fac'].default_value=min(1,grime+0.3)
    l.new(colout,mg.inputs['Color1']); l.new(pw.outputs[0],mg.inputs['Color2']); colout=mg.outputs['Color']
    if soot_below:   # black soot below z1 fading out at z0..z1 (object space)
        sx=n.new('ShaderNodeSeparateXYZ'); l.new(tc.outputs['Object'],sx.inputs[0])
        ms=n.new('ShaderNodeMapRange'); ms.inputs[1].default_value=soot_below[0]; ms.inputs[2].default_value=soot_below[1]; ms.inputs[3].default_value=1; ms.inputs[4].default_value=0
        l.new(sx.outputs['Z'],ms.inputs[0])
        mm=n.new('ShaderNodeMath'); mm.operation='MULTIPLY'; l.new(ms.outputs[0],mm.inputs[0]); l.new(mr.outputs[0],mm.inputs[1]); mm.use_clamp=True
        mk=n.new('ShaderNodeMixRGB'); mk.inputs['Color2'].default_value=(0.006,0.005,0.005,1)
        l.new(mm.outputs[0],mk.inputs['Fac']); l.new(colout,mk.inputs['Color1']); colout=mk.outputs['Color']
    l.new(colout,b.inputs['Base Color'])
    rr=n.new('ShaderNodeMapRange'); rr.inputs[3].default_value=max(0.05,rough-rvar); rr.inputs[4].default_value=min(1,rough+rvar)
    l.new(nz.outputs['Fac'],rr.inputs[0]); l.new(rr.outputs[0],b.inputs['Roughness'])
    if metal:
        im=n.new('ShaderNodeMapRange'); im.inputs[1].default_value=cols[0][0]; im.inputs[2].default_value=cols[-1][0]; im.inputs[3].default_value=metal; im.inputs[4].default_value=0.05
        l.new(nz.outputs['Fac'],im.inputs[0]); l.new(im.outputs[0],b.inputs['Metallic'])
    b1=n.new('ShaderNodeBump'); b1.inputs['Strength'].default_value=bump; b1.inputs['Distance'].default_value=0.012; l.new(nz.outputs['Fac'],b1.inputs['Height'])
    b2=n.new('ShaderNodeBump'); b2.inputs['Strength'].default_value=bump*0.6; b2.inputs['Distance'].default_value=0.003; l.new(f2.outputs['Fac'],b2.inputs['Height']); l.new(b1.outputs[0],b2.inputs['Normal'])
    l.new(b2.outputs[0],b.inputs['Normal'])
    return m
M_iron=mat_rich('iron',[(0.30,(0.012,0.012,0.014)),(0.55,(0.035,0.032,0.03)),(0.68,(0.13,0.055,0.025)),(0.8,(0.20,0.085,0.035))],scale=7,rough=0.72,metal=0.55,bump=0.9,grime=0.7,soot_below=(0.05,0.30))
M_ironc=mat_rich('ironc',[(0.35,(0.015,0.015,0.017)),(0.7,(0.06,0.05,0.045))],scale=18,rough=0.6,metal=0.8,bump=0.5,grime=0.8)
M_tin=mat_rich('tin',[(0.30,(0.30,0.31,0.32)),(0.55,(0.50,0.50,0.49)),(0.72,(0.30,0.16,0.08))],scale=16,rough=0.38,metal=0.9,bump=0.35,grime=0.9,randomize=True)
M_paper=mat_rich('paper',[(0.3,(0.42,0.33,0.20)),(0.7,(0.66,0.57,0.40))],scale=9,rough=0.92,bump=0.25,grime=0.5,randomize=True)
M_ink=mat_simple('ink',(0.07,0.03,0.02),0.9)
M_water=mat_simple('water',(0.07,0.055,0.035),0.04)
M_table=mat_rich('tablew',[(0.25,(0.045,0.033,0.024)),(0.5,(0.13,0.095,0.062)),(0.75,(0.26,0.20,0.14))],scale=3.5,rough=0.82,stretch=(0.7,14,14),bump=1.0,grime=0.9,randomize=True,fine=70)
M_tleg=mat_rich('tleg',[(0.25,(0.04,0.03,0.022)),(0.5,(0.12,0.088,0.058)),(0.75,(0.23,0.18,0.125))],scale=3.5,rough=0.85,stretch=(14,14,0.7),bump=1.0,grime=0.9,randomize=True,fine=70)
M_cut=mat_rich('cut',[(0.3,(0.20,0.13,0.08)),(0.7,(0.42,0.31,0.20))],scale=5,rough=0.7,stretch=(1,10,10),bump=0.7,grime=0.8)
M_saus=mat_rich('saus',[(0.3,(0.26,0.045,0.035)),(0.55,(0.38,0.10,0.07)),(0.75,(0.50,0.22,0.16))],scale=45,rough=0.34,bump=0.5,grime=0.4,randomize=True,rvar=0.15)
M_sausdry=mat_rich('sausdry',[(0.3,(0.045,0.012,0.010)),(0.55,(0.13,0.035,0.025)),(0.78,(0.34,0.28,0.24))],scale=55,rough=0.62,bump=1.0,grime=0.6,randomize=True)
M_boil=mat_rich('boil',[(0.3,(0.42,0.20,0.15)),(0.7,(0.60,0.36,0.28))],scale=40,rough=0.42,bump=0.4,grime=0.4,randomize=True)
M_twine=mat_rich('twine',[(0.3,(0.30,0.24,0.15)),(0.7,(0.52,0.44,0.30))],scale=120,rough=0.95,bump=0.8,grime=0.3)
M_sack=mat_rich('sack',[(0.3,(0.26,0.21,0.14)),(0.7,(0.50,0.43,0.31))],scale=6,rough=0.97,bump=0.9,grime=0.8,fine=160)
M_stone=mat_rich('stone2',[(0.3,(0.06,0.06,0.062)),(0.55,(0.20,0.20,0.20)),(0.75,(0.38,0.37,0.35))],scale=5,rough=0.92,bump=1.2,grime=0.9,randomize=True,soot_below=None)
M_stonesoot=mat_rich('stonesoot',[(0.3,(0.015,0.014,0.014)),(0.55,(0.07,0.065,0.06)),(0.8,(0.22,0.21,0.2))],scale=5,rough=0.95,bump=1.2,grime=0.9,randomize=True)
M_ash=mat_rich('ash',[(0.3,(0.03,0.03,0.03)),(0.6,(0.16,0.155,0.15)),(0.8,(0.42,0.41,0.40))],scale=22,rough=1.0,bump=0.8,grime=0.2)
M_char=mat_rich('char',[(0.4,(0.006,0.005,0.005)),(0.7,(0.05,0.04,0.035))],scale=20,rough=0.9,bump=1.2,grime=0.5,randomize=True)
M_bark=mat_rich('bark2',[(0.3,(0.03,0.022,0.016)),(0.6,(0.11,0.08,0.055)),(0.8,(0.22,0.17,0.12))],scale=7,rough=0.95,stretch=(7,7,0.8),bump=1.6,grime=0.9,randomize=True)
M_meat=mat_rich('meat2',[(0.3,(0.16,0.022,0.018)),(0.6,(0.30,0.05,0.04)),(0.8,(0.55,0.36,0.30))],scale=9,rough=0.3,bump=0.6,grime=0.5,randomize=True,rvar=0.12)

def rough_up(o,amount,size=0.2,sub=0):
    if sub:
        s=o.modifiers.new('ss','SUBSURF'); s.levels=sub; s.render_levels=sub
    t=bpy.data.textures.new('t','CLOUDS'); t.noise_scale=size; t.noise_depth=3
    d=o.modifiers.new('d','DISPLACE'); d.texture=t; d.strength=amount; d.texture_coords='GLOBAL'; d.mid_level=0.5
    return o
def tube(name,c,pts,radii,mat,res=10,cyclic=False,cap=True):
    """A rounded tube through points with its own radius at each (sausages, twine, bail handles)."""
    cu=bpy.data.curves.new(name,'CURVE'); cu.dimensions='3D'; cu.bevel_depth=1.0; cu.bevel_resolution=res; cu.use_fill_caps=cap; cu.resolution_u=10
    sp=cu.splines.new('NURBS'); sp.points.add(len(pts)-1); sp.use_endpoint_u=not cyclic; sp.use_cyclic_u=cyclic; sp.order_u=min(4,len(pts))
    for p,q,r in zip(sp.points,pts,radii if hasattr(radii,'__len__') else [radii]*len(pts)):
        p.co=(*q,1); p.radius=r
    o=bpy.data.objects.new(name,cu); C[c].objects.link(o); cu.materials.append(mat); return o
def chain_links(c,path,n_links,r,mat,z,twine=True,sag=0.0):
    """Sausage links along a 2D path function t->(x,y): fat in the middle, pinched and tied between links."""
    per=7; pts=[]; rad=[]
    for i in range(n_links*per+1):
        t=i/(n_links*per); x,y=path(t); k=i%per
        pin=abs(math.sin(math.pi*k/per))**0.35
        rr=r*(0.16+0.84*pin)*(1+random.uniform(-.05,.05))
        pts.append((x,y,z+rr*0.0+ (r-rr)*-0.0)); rad.append(rr)
    pts=[(x,y,z-r+rr) for (x,y,_),rr in zip(pts,rad)]   # lies on the board
    o=tube('links',c,pts,rad,mat,res=8)
    if twine:
        for j in range(0,n_links+1):
            i=min(j*per,len(pts)-1); x,y,zz=pts[i]
            cyl('knot',c,r*0.26,r*0.2,(x,y,zz),(math.pi/2,0,math.atan2(path(min(1,(i+1)/(n_links*per)))[1]-path(max(0,(i-1)/(n_links*per)))[1],path(min(1,(i+1)/(n_links*per)))[0]-path(max(0,(i-1)/(n_links*per)))[0])+math.pi/2),M_twine,seg=8)
    return o
def can(c,loc,rot=(0,0,0),label=True,dent=0.004):
    e=bpy.data.objects.new('can',None); C[c].objects.link(e); e.location=loc; e.rotation_euler=rot
    prof=[(0,0.004),(0.036,0.004),(0.0415,0.0),(0.043,0.004),(0.0415,0.008)]
    for k in range(1,7): prof+= [(0.0415,0.008+k*0.014-0.004),(0.0408,0.008+k*0.014)]
    prof+=[(0.0415,0.104),(0.043,0.108),(0.0415,0.112),(0.036,0.108),(0,0.108)]
    b=lathe('canb',c,prof,(0,0,0),M_tin,seg=28); b.parent=e; rough_up(b,dent,0.05)
    if label:
        l=lathe('lab',c,[(0.0422,0.022),(0.0422,0.092)],(0,0,0),M_paper,seg=28); l.parent=e
        for z,w in ((0.074,0.006),(0.060,0.004),(0.050,0.004),(0.036,0.008)):
            s=lathe('ink',c,[(0.0425,z),(0.0425,z+w)],(0,0,0),M_ink,seg=28); s.parent=e
        tw=tube('tw',c,[(0.0432*math.cos(a),0.0432*math.sin(a),0.085+0.002*math.sin(3*a)) for a in [i*2*math.pi/12 for i in range(12)]],0.0011,M_twine,res=3,cyclic=True); tw.parent=e
    return e
def fire_ring(c,cx,cy,r,n=10):
    cyl('ashbed',c,r+0.12,0.012,(cx,cy,0.006),mat=M_ash,seg=28)
    for i in range(n):
        a=i*2*math.pi/n+random.uniform(-.12,.12); rr=random.uniform(0.075,0.11)
        o=rock('fr',c,rr,(cx+r*math.cos(a),cy+r*math.sin(a),rr*0.55),(1,random.uniform(.8,1.1),random.uniform(.7,.95))); o.data.materials[0]=M_stonesoot if i%2 else M_stone
    em=cyl('emb',c,r-0.07,0.05,(cx,cy,0.04),mat=M_ember,seg=20); rough_up(em,0.03,0.06,sub=1)
    for i in range(4):
        o=cyl('lg',c,random.uniform(0.03,0.045),random.uniform(0.3,0.4),(cx+random.uniform(-.05,.05),cy-0.1+0.065*i,0.09+0.01*(i%2)),(0,math.pi/2+random.uniform(-.12,.12),random.uniform(-.7,.7)),M_char,seg=10)
        rough_up(o,0.012,0.05)
    pl=bpy.data.lights.new('fire','POINT'); pl.energy=22; pl.color=(1,0.38,0.08); pl.shadow_soft_size=0.12
    po=bpy.data.objects.new('fire',pl); C[c].objects.link(po); po.location=(cx,cy-0.05,0.16)
def pile(c,x,y,n=7):
    for i in range(n):
        row=i//3; L=random.uniform(0.38,0.48)
        o=cyl('wp',c,random.uniform(0.045,0.068),L,(x+(i%3)*0.13+row*0.065+random.uniform(-.01,.01),y+random.uniform(-.03,.03),0.07+row*0.11),(math.pi/2,random.uniform(-.05,.05),random.uniform(-.1,.1)),M_bark,seg=12,capmat=M_woodend)
        rough_up(o,0.01,0.08)

# ---------- 1. CANNING KETTLE at x=0 (v3: no stone ring, no board, no wood pile; the kettle sits on four big stones)
KZ=0.26
# ash and charcoal on the trodden snow, an uneven patch
for i in range(14):
    a=random.uniform(0,2*math.pi); r=random.uniform(0,0.42)*random.uniform(0.6,1)
    o=rock('ash','kettle',random.uniform(0.07,0.16),(r*math.cos(a)*1.15,r*math.sin(a)*0.9,0.004),(1,random.uniform(.6,1),0.07)); o.data.materials[0]=M_ash if i%3 else M_char
# four support stones, the kettle's belly rests in them
for i in range(4):
    a=math.pi/4+i*math.pi/2+random.uniform(-.1,.1)
    o=rock('ks','kettle',0.17,(0.275*math.cos(a),0.275*math.sin(a),0.17),(random.uniform(.85,1),random.uniform(.8,.95),1.3)); o.data.materials[0]=M_stonesoot if i%2 else M_stone
# fire between the stones
em=cyl('emb','kettle',0.17,0.05,(0,0,0.035),mat=M_ember,seg=16); rough_up(em,0.03,0.06,sub=1)
for i in range(5):
    a=i*math.pi/2.5+0.4
    o=cyl('lg','kettle',random.uniform(0.028,0.04),random.uniform(0.42,0.55),(0.10*math.cos(a),0.10*math.sin(a),0.07+0.012*(i%2)),(0,math.pi/2+random.uniform(-.1,.1),a),M_char,seg=10)
    rough_up(o,0.012,0.05)
pl=bpy.data.lights.new('fire','POINT'); pl.energy=20; pl.color=(1,0.38,0.08); pl.shadow_soft_size=0.12
po=bpy.data.objects.new('fire',pl); C['kettle'].objects.link(po); po.location=(0,-0.02,0.13)
kp=[(0,0.0),(0.10,0.0),(0.20,0.025),(0.28,0.08),(0.325,0.16),(0.34,0.26),(0.335,0.35),(0.325,0.40),(0.33,0.415),(0.352,0.42),(0.356,0.432),(0.35,0.445)]
kt=lathe('kettle','kettle',kp,(0,0,KZ),M_iron,seg=56,solid=0.014); rough_up(kt,0.008,0.16)
for z in (0.20,0.33):
    lathe('band','kettle',[(0.3405,z-0.006),(0.346,z),(0.3405,z+0.006)],(0,0,KZ),M_ironc,seg=56)
for s2 in (-1,1):
    tube('ear','kettle',[(s2*0.335,-0.05,KZ+0.36),(s2*0.385,-0.045,KZ+0.365),(s2*0.395,0,KZ+0.37),(s2*0.385,0.045,KZ+0.365),(s2*0.335,0.05,KZ+0.36)],0.009,M_ironc,res=4)
tube('bail','kettle',[(-0.39,0,KZ+0.37),(-0.37,0.16,KZ+0.46),(-0.2,0.33,KZ+0.50),(0,0.38,KZ+0.51),(0.2,0.33,KZ+0.50),(0.37,0.16,KZ+0.46),(0.39,0,KZ+0.37)],0.008,M_ironc,res=4)
cyl('water','kettle',0.322,0.01,(0,0,KZ+0.345),mat=M_water,seg=40)
for i in range(6):
    a=i*math.pi/3+0.3; can('kettle',(0.19*math.cos(a)+random.uniform(-.01,.01),0.19*math.sin(a),KZ+0.262),(random.uniform(-.04,.04),0,random.uniform(0,6)),label=False)
can('kettle',(0,0,KZ+0.262),label=False)
# the lid lies on the snow beside, two finished cans
can('kettle',(0.62,-0.42,0.0),(0,0,1.0))
can('kettle',(0.74,-0.36,0.0),(0,0,2.4))

# ---------- 2. SAUSAGE TABLE at x=4
TX=4.0; TH=0.86
for i in range(5):   # five boards, uneven ends, gaps
    L=1.30+random.uniform(-0.03,0.03)
    b=box('board','table',(L,0.122,0.042),(TX+random.uniform(-.012,.012),-0.256+0.128*i,TH+random.uniform(-.002,.002)),(random.uniform(-.004,.004),0,random.uniform(-.004,.004)),M_table,bevel=0.005); rough_up(b,0.004,0.12)
for sx in (-1,1):
    for sy in (-1,1):
        lg=box('leg','table',(0.075,0.075,TH-0.02),(TX+sx*0.56,sy*0.25,(TH-0.02)/2),(0,0,0),M_tleg,bevel=0.008); rough_up(lg,0.004,0.1)
    box('apron','table',(0.03,0.50,0.10),(TX+sx*0.56,0,TH-0.075),mat=M_table,bevel=0.003)
    box('brace','table',(0.035,0.50,0.07),(TX+sx*0.56,0,0.24),mat=M_table,bevel=0.003)
for sy in (-1,1):
    box('apronl','table',(1.12,0.03,0.10),(TX,sy*0.25,TH-0.075),mat=M_tleg,bevel=0.003)
for i in range(4):
    b=box('shelf','table',(1.10,0.118,0.026),(TX,-0.186+0.124*i,0.288),(0,0,random.uniform(-.004,.004)),M_table,bevel=0.004)
# back frame with a pole: finished sausages hang to dry
for sx in (-1,1):
    box('post','table',(0.05,0.05,0.95),(TX+sx*0.60,0.28,TH+0.475),mat=M_tleg,bevel=0.006)
pole=cyl('pole','table',0.016,1.34,(TX,0.28,TH+0.90),(0,math.pi/2,0),M_bark,seg=10); rough_up(pole,0.004,0.1)
def hang_ring(x,dry=True,drop=0.30,w=0.085):
    pts=[]; rad=[]; N=32
    for i in range(N):
        a=i*2*math.pi/N
        px=x+w*math.sin(a)*(0.75+0.25*math.cos(a)); pz=TH+0.90-0.02-drop*(1-math.cos(a))/2
        k=i%8; pin=abs(math.sin(math.pi*k/8))**0.4
        pts.append((px,0.28+0.004*math.sin(5*a),pz)); rad.append(0.019*(0.3+0.7*pin) if dry else 0.022*(0.3+0.7*pin))
    tube('hring','table',pts,rad,M_sausdry if dry else M_saus,res=6,cyclic=True)
    tube('hstr','table',[(x,0.28,TH+0.92),(x+0.012,0.262,TH+0.90),(x,0.28,TH+0.875),(x-0.012,0.298,TH+0.90)],0.0018,M_twine,res=3,cyclic=True)
for i,x in enumerate((-0.36,-0.20,-0.04,0.13,0.30,0.46)):
    hang_ring(TX+x,dry=(i!=2 and i!=5),drop=random.uniform(0.27,0.34))
ZT=TH+0.021
# meat grinder clamped at the left end
gx=TX-0.50; gy=-0.19
gb=lathe('gbody','table',[(0,0),(0.044,0.0),(0.05,0.02),(0.05,0.18),(0.04,0.21),(0,0.21)],(gx,gy+0.105,ZT+0.20),M_ironc,seg=20); gb.rotation_euler=(math.pi/2,0,0)
lathe('gfun','table',[(0.032,0.0),(0.036,0.03),(0.08,0.115),(0.085,0.118),(0.08,0.121)],(gx,gy+0.02,ZT+0.235),M_ironc,seg=24,solid=0.004)
lathe('gneck','table',[(0.036,0),(0.03,0.03),(0.026,0.10),(0.03,0.15)],(gx,gy,ZT+0.045),M_ironc,seg=16)
box('gfoot','table',(0.10,0.085,0.016),(gx,gy,ZT+0.008),mat=M_ironc,bevel=0.004)
box('gclamp','table',(0.03,0.05,0.12),(gx,gy-0.075,ZT-0.04),mat=M_ironc,bevel=0.004)
cyl('gscrew','table',0.008,0.11,(gx,gy-0.04,ZT-0.085),mat=M_ironc,seg=8)
cyl('gwing','table',0.006,0.07,(gx,gy-0.04,ZT-0.14),(0,math.pi/2,0),M_ironc,seg=6)
lathe('gring','table',[(0.052,0.0),(0.056,0.004),(0.056,0.024),(0.052,0.028)],(gx,gy-0.105,ZT+0.20),M_ironc,seg=20).rotation_euler=(math.pi/2,0,0)
lathe('ghorn','table',[(0.03,0.0),(0.016,0.04),(0.014,0.13)],(gx,gy-0.13,ZT+0.20),M_tin,seg=14).rotation_euler=(math.pi/2,0,0)
tube('gcrank','table',[(gx,gy+0.10,ZT+0.20),(gx,gy+0.15,ZT+0.20),(gx+0.01,gy+0.16,ZT+0.19),(gx+0.09,gy+0.165,ZT+0.115),(gx+0.10,gy+0.17,ZT+0.105),(gx+0.10,gy+0.20,ZT+0.105)],0.008,M_ironc,res=4)
lathe('gknob','table',[(0,0),(0.014,0.004),(0.018,0.035),(0.012,0.07),(0,0.074)],(gx+0.10,gy+0.20,ZT+0.105),M_cut,seg=12).rotation_euler=(-math.pi/2,0,0)
# raw sausage coming off the horn in an S, tied links
def path_s(t):
    x=TX-0.50+0.02+0.62*t; y=-0.33+0.10*math.sin(t*2*math.pi*1.1)+0.10*t
    return x,y
chain_links('table',path_s,7,0.023,M_saus,ZT+0.023)
# a coil of dry sausage, three boiled, cut one
def path_c(t):
    a=t*2*math.pi*1.85; R=0.05+0.065*t
    return TX+0.42+R*math.cos(a), 0.10+R*math.sin(a)
chain_links('table',path_c,9,0.019,M_sausdry,ZT+0.019)
for i in range(3):
    L=0.20; a=1.35+0.12*i; cx=TX+0.12+0.07*i; cy=-0.02+0.01*i
    pts=[(cx+(t-0.5)*L*math.cos(a)+0.012*math.sin(t*math.pi)*math.sin(a),cy+(t-0.5)*L*math.sin(a)-0.012*math.sin(t*math.pi)*math.cos(a),ZT+0.027) for t in [k/6 for k in range(7)]]
    tube('boil','table',pts,[0.012,0.026,0.028,0.028,0.028,0.026,0.012],M_boil,res=8)
    for t in (0,6): cyl('bk','table',0.006,0.012,pts[t],(math.pi/2,0,a+math.pi/2),M_twine,seg=6)
# cutting board with meat, knife, bowl of mince, salt, twine ball
cb=box('cboard','table',(0.34,0.24,0.022),(TX-0.12,0.10,ZT+0.011),(0,0,0.18),M_cut,bevel=0.004)
for i in range(4):
    m=meat('mt','table',(0.085,0.055,0.045),(TX-0.20+0.06*i,0.08+0.03*(i%2),ZT+0.07),0.4*i); m.data.materials[0]=M_meat
box('blade','table',(0.19,0.034,0.0025),(TX+0.02,0.20,ZT+0.024),(0.02,0,0.22),M_tin)
hf=box('haft','table',(0.11,0.026,0.02),(TX+0.165,0.233,ZT+0.03),(0,0,0.22),M_tleg,bevel=0.005)
lathe('bowl','table',[(0,0.004),(0.06,0.0),(0.075,0.006),(0.115,0.055),(0.122,0.058),(0.118,0.062)],(TX-0.36,0.13,ZT),M_tin,seg=28,solid=0.004)
mn=lathe('mince','table',[(0,0.058),(0.04,0.056),(0.08,0.046),(0.105,0.04)],(TX-0.36,0.13,ZT),M_meat,seg=20); rough_up(mn,0.014,0.025,sub=1)
tb=lathe('twball','table',[(0,0),(0.02,0.006),(0.035,0.03),(0.02,0.056),(0,0.062)],(TX+0.30,-0.20,ZT),M_twine,seg=14); rough_up(tb,0.004,0.01)
lathe('saltbowl','table',[(0,0.003),(0.03,0.0),(0.05,0.03),(0.052,0.032)],(TX+0.50,-0.20,ZT),M_cut,seg=18,solid=0.004)
lathe('saltin','table',[(0,0.03),(0.03,0.026),(0.046,0.022)],(TX+0.50,-0.20,ZT),mat_rich('salt',[(0.3,(0.6,0.6,0.62)),(0.7,(0.9,0.9,0.92))],scale=90,rough=0.8,bump=0.8,grime=0.2),seg=14)
# under the table: a crock with guts, a sack, a bucket
lathe('crock','table',[(0,0),(0.08,0),(0.095,0.06),(0.095,0.15),(0.075,0.19),(0.08,0.2)],(TX+0.33,0,0.301),mat_rich('crock',[(0.3,(0.16,0.10,0.06)),(0.7,(0.34,0.25,0.16))],scale=10,rough=0.35,bump=0.3,grime=0.7),seg=24,solid=0.006)
# ---------- light & render
so.rotation_euler=(math.radians(68),0,math.radians(-48)); sun.energy=4.2; sun.angle=math.radians(2.5)
wn['Background'].inputs['Strength'].default_value=0.55
sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=int(os.environ.get('SAMPLES','70')); sc.cycles.use_denoising=True
sc.cycles.volume_step_rate=2.0; sc.cycles.max_bounces=6; sc.cycles.volume_bounces=0
sc.render.resolution_x=int(os.environ.get('RES','900')); sc.render.resolution_y=int(os.environ.get('RES','900'))
sc.view_settings.view_transform='AgX'; sc.view_settings.look='AgX - High Contrast'
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
V={'kettle':((1.45,-1.95,1.2),(0.0,-0.1,0.36),40),
   'table':((TX+1.35,-2.15,1.75),(TX,0.02,0.95),42)}
for v in views:
    loc,tgt,fov=V[v]
    cam.location=loc; cam.data.angle=math.radians(fov); cam.rotation_euler=(Vector(tgt)-Vector(loc)).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=out+'_'+v+'.png'; bpy.ops.render.render(write_still=True)
