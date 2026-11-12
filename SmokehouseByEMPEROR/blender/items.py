import bpy, bmesh, math, sys, os
import numpy as np
from mathutils import Vector
G='/mnt/project-files/smokehouse/game_models/'; T='/mnt/project-files/smokehouse/tex/'
out=sys.argv[sys.argv.index('--')+1]
ONLY=os.environ.get('ONLY','').split(',')
ITEMS=[('cougar','GEAR_CookedMeatCougar__CookedMeatMesh','GEAR_CookedMeatCougar__CookedMeatMesh','GEAR_MeatCougar'),('ptarmigan','GEAR_CookedMeatPtarmigan__OBJ_CookedMeatPtarmigan','GEAR_RawMeatPtarmigan__OBJ_RawMeatPtarmigan','GEAR_MeatPtarmigan'),
       ('coho','GEAR_CookedCohoSalmon__OBJ_CookedLakeWhiteFish',None,'GEAR_CookedCohoSalmon'),('whitefish','GEAR_CookedLakeWhiteFish__OBJ_CookedLakeWhiteFish',None,'GEAR_CookedLakeWhiteFish'),('bass','GEAR_CookedSmallMouthBass__OBJ_CookedLakeWhiteFish',None,'GEAR_CookedSmallMouthBass'),
       ('rockfish','GEAR_CookedRockfish__GEAR_CookedCopperFish',None,'GEAR_CookedCopperFish'),('goldeye','GEAR_CookedGoldeye__GEAR_CookedLakeGoldeye',None,'GEAR_CookedLakeGoldeye'),('irishlord','GEAR_CookedRedIrishLord__OBJ_CookedRedIrishLord',None,'GEAR_CookedRedIrishLord'),
       ('wolf','GEAR_CookedMeatWolf__RawMeatMesh','GEAR_RawMeatWolf__RawMeatMesh','GEAR_MeatWolf'),('bear','GEAR_CookedMeatBear__CookedMeatMesh','GEAR_RawMeatBear__RawMeatMesh','GEAR_MeatBear'),('moose','GEAR_CookedMeatMoose__CookedMeatMesh','GEAR_RawMeatMoose__RawMeatMesh','GEAR_MeatMoose'),
       ('venison','GEAR_CookedMeatDeer__RawMeatMesh','GEAR_RawMeatDeer__RawMeatMesh','GEAR_MeatVenison'),
       ('rabbit','GEAR_CookedMeatRabbit__RawMeatMesh','GEAR_RawMeatRabbit__RawMeatMesh','GEAR_MeatRabbit'),
       ('trout','GEAR_CookedRainbowTrout__OBJ_CookedLakeWhiteFish',None,'GEAR_CookedRainbowTrout'),
       ('burbot','GEAR_CookedBurbot__GEAR_CookedLakeBurbot',None,'GEAR_CookedLakeBurbot')]
bpy.ops.wm.read_factory_settings(use_empty=True); sc=bpy.context.scene
sc.render.engine='CYCLES'; sc.cycles.samples=48; sc.cycles.use_denoising=True; sc.render.resolution_x=560; sc.render.resolution_y=420
sc.view_settings.view_transform='AgX'; sc.view_settings.look='AgX - Medium High Contrast'
w=bpy.data.worlds.new('w'); sc.world=w; w.use_nodes=True; w.node_tree.nodes['Background'].inputs['Color'].default_value=(0.5,0.55,0.62,1); w.node_tree.nodes['Background'].inputs['Strength'].default_value=0.5
bpy.ops.mesh.primitive_plane_add(size=6); pl=bpy.context.object
m=bpy.data.materials.new('table'); m.use_nodes=True; n=m.node_tree.nodes; l=m.node_tree.links; b=n['Principled BSDF']
tc=n.new('ShaderNodeTexCoord'); mp=n.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value=(2,40,1); nz=n.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value=3; nz.inputs['Detail'].default_value=8
cr=n.new('ShaderNodeValToRGB'); cr.color_ramp.elements[0].color=(0.02,0.014,0.01,1); cr.color_ramp.elements[1].color=(0.07,0.05,0.035,1)
l.new(tc.outputs['Object'],mp.inputs[0]); l.new(mp.outputs[0],nz.inputs[0]); l.new(nz.outputs['Fac'],cr.inputs[0]); l.new(cr.outputs[0],b.inputs['Base Color']); b.inputs['Roughness'].default_value=0.8
pl.data.materials.append(m)
def light(loc,e,s,c=(1,1,1)):
    d=bpy.data.lights.new('l','AREA'); d.energy=e; d.size=s; d.color=c; o=bpy.data.objects.new('l',d); sc.collection.objects.link(o); o.location=loc
    o.rotation_euler=(Vector((0,0,0))-Vector(loc)).to_track_quat('-Z','Y').to_euler()
light((0.5,-0.5,0.7),45,0.6,(1,0.95,0.88)); light((-0.6,0.3,0.5),15,0.8,(0.85,0.9,1))
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
cam.location=(0.0,-0.42,0.52); cam.data.angle=math.radians(36); cam.rotation_euler=(Vector((0,0,0.01))-Vector(cam.location)).to_track_quat('-Z','Y').to_euler()
TW=bpy.data.materials.new('twine'); TW.use_nodes=True; TW.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(0.55,0.43,0.26,1); TW.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=0.9
def hull(P):
    P=sorted(set(map(tuple,np.round(P,5))))
    def cr(o,a,b): return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lo=[]
    for p in P:
        while len(lo)>=2 and cr(lo[-2],lo[-1],p)<=0: lo.pop()
        lo.append(p)
    up=[]
    for p in reversed(P):
        while len(up)>=2 and cr(up[-2],up[-1],p)<=0: up.pop()
        up.append(p)
    return lo[:-1]+up[:-1]
def loop(pts,cyc=True,r=0.0016):
    cu=bpy.data.curves.new('tw','CURVE'); cu.dimensions='3D'; sp=cu.splines.new('POLY'); sp.points.add(len(pts)-1)
    for p,q in zip(sp.points,pts): p.co=(*q,1)
    sp.use_cyclic_u=cyc; cu.bevel_depth=r; cu.bevel_resolution=3
    o=bpy.data.objects.new('tw',cu); sc.collection.objects.link(o); o.data.materials.append(TW); return o
def twine(o):
    dg=bpy.context.evaluated_depsgraph_get(); V=np.array([o.matrix_world@v.co for v in o.data.vertices]); mn=V.min(0); mx=V.max(0)
    ax=0 if (mx[0]-mn[0])>(mx[1]-mn[1]) else 1; ot=1-ax; made=[]
    def section(axis,pos):
        bm=bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
        co=[0,0,0]; co[axis]=pos; no=[0,0,0]; no[axis]=1
        r=bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=co,plane_no=no)
        pts=[v.co.copy() for v in r['geom_cut'] if isinstance(v,bmesh.types.BMVert)]; bm.free(); return pts
    L=mx[ax]-mn[ax]
    for t in(0.2,0.4,0.6,0.8):
        pts=section(ax,mn[ax]+L*t)
        if len(pts)<3: continue
        h=hull([(p[ot],p[2]) for p in pts]); c=np.mean(h,0)
        h2=[]
        for a,b in h:
            d=np.array([a,b])-c; d=d/max(np.linalg.norm(d),1e-6)*0.0015; h2.append((a+d[0],b+d[1]))
        P=[]
        for a,b in h2:
            q=[0,0,0]; q[ax]=mn[ax]+L*t; q[ot]=a; q[2]=b; P.append(q)
        made.append(loop(P))
    # lengthwise loop through the middle + hanging loop
    pts=section(ot,(mn[ot]+mx[ot])/2)
    if len(pts)>=3:
        h=hull([(p[ax],p[2]) for p in pts]); c=np.mean(h,0); P=[]
        for a,b in h:
            d=np.array([a,b])-c; d=d/max(np.linalg.norm(d),1e-6)*0.0025; q=[0,0,0]; q[ax]=a+d[0]; q[ot]=(mn[ot]+mx[ot])/2; q[2]=b+d[1]; P.append(q)
        made.append(loop(P))
        k=int(np.argmax([p[ax] for p in P])); pe=P[k][ax]; ze=P[k][2]; mid=(mn[ot]+mx[ot])/2; Q=[]
        for i in range(21):
            a=math.pi*2*i/20; q=[0,0,0]; q[ax]=pe+0.034*(1-math.cos(a))/2; q[ot]=mid+0.011*math.sin(a); q[2]=ze+0.014*(1-math.cos(a))/2; Q.append(q)
        made.append(loop(Q,False))
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.0035,segments=10,ring_count=6); kn=bpy.context.object; c=[0,0,0]; c[ax]=pe+0.001; c[ot]=mid; c[2]=ze; kn.location=c; kn.data.materials.append(TW); made.append(kn)
    return made
def twine_fish(o):
    V=np.array([o.matrix_world@v.co for v in o.data.vertices]); mn=V.min(0); mx=V.max(0)
    ax=0 if (mx[0]-mn[0])>(mx[1]-mn[1]) else 1; ot=1-ax; L=mx[ax]-mn[ax]; made=[]
    def slab(t0,t1):
        m=(V[:,ax]>=mn[ax]+L*t0)&(V[:,ax]<=mn[ax]+L*t1); return V[m]
    a=slab(0,0.12); b=slab(0.88,1.0)
    wa=np.ptp(a[:,ot]) if len(a) else 9; wb=np.ptp(b[:,ot]) if len(b) else 9
    end=0 if wa<wb else 1; sgn=-1 if end==0 else 1
    t=0.09 if end==0 else 0.91
    bm=bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world)
    co=[0,0,0]; co[ax]=mn[ax]+L*t; no=[0,0,0]; no[ax]=1
    r=bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],plane_co=co,plane_no=no)
    pts=[v.co.copy() for v in r['geom_cut'] if isinstance(v,bmesh.types.BMVert)]; bm.free()
    if len(pts)<3: return made
    h=hull([(p[ot],p[2]) for p in pts]); c=np.mean(h,0); P=[]
    for u,w in h:
        d=np.array([u,w])-c; d=d/max(np.linalg.norm(d),1e-6)*0.0016; q=[0,0,0]; q[ax]=co[ax]; q[ot]=u+d[0]; q[2]=w+d[1]; P.append(q)
    made.append(loop(P)); P2=[list(p) for p in P]
    for p in P2: p[ax]+=0.004*sgn
    made.append(loop(P2))
    mid=float(c[0]); ze=max(p[2] for p in P); Q=[]
    for i in range(21):
        a=math.pi*2*i/20; q=[0,0,0]; q[ax]=co[ax]+sgn*(0.002+0.05*(1-math.cos(a))/2); q[ot]=mid+0.013*math.sin(a); q[2]=ze+0.006*(1-math.cos(a))/2; Q.append(q)
    made.append(loop(Q,False))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.0035,segments=10,ring_count=6); kn=bpy.context.object; cc=[0,0,0]; cc[ax]=co[ax]+sgn*0.002; cc[ot]=mid; cc[2]=ze+0.001; kn.location=cc; kn.data.materials.append(TW); made.append(kn)
    return made
def mat(tex,rough):
    m=bpy.data.materials.new('m'); m.use_nodes=True; n=m.node_tree.nodes; b=n['Principled BSDF']; t=n.new('ShaderNodeTexImage'); t.image=bpy.data.images.load(tex)
    m.node_tree.links.new(t.outputs['Color'],b.inputs['Base Color']); b.inputs['Roughness'].default_value=rough
    bp=n.new('ShaderNodeBump'); bp.inputs['Strength'].default_value=0.25; bp.inputs['Distance'].default_value=0.004; m.node_tree.links.new(t.outputs['Color'],bp.inputs['Height']); m.node_tree.links.new(bp.outputs[0],b.inputs['Normal'])
    return m
for key,cooked,raw,tex in ITEMS:
    if ONLY!=[''] and key not in ONLY: continue
    for mode in os.environ.get('MODES','vanilla,hot,cold').split(','):
        src=cooked if (mode!='cold' or raw is None) else raw
        bpy.ops.wm.obj_import(filepath=G+src+'.obj'); o=bpy.context.selected_objects[0]
        bpy.context.view_layer.update()
        bb=[o.matrix_world@Vector(c) for c in o.bound_box]; mn=Vector((min(v[i] for v in bb) for i in range(3))); mx=Vector((max(v[i] for v in bb) for i in range(3)))
        s=0.30/max(mx-mn); 
        if mode=='cold': s*=0.9
        o.scale=o.scale*s; c=(mn+mx)/2*s; o.location=o.location-Vector((c.x,c.y,mn.z*s))
        o.rotation_euler.z+=0  
        tp=G+tex+'_Dif.png' if mode=='vanilla' else T+tex+f'_{mode}.png'
        o.data.materials.clear(); o.data.materials.append(mat(tp,{'vanilla':0.6,'hot':0.38,'cold':0.7}[mode]))
        for p in o.data.polygons: p.use_smooth=True
        extra=[]
        if mode!='vanilla' and raw is not None:
            bpy.context.view_layer.update(); extra=twine(o)
        if mode!='vanilla' and raw is None:
            bpy.context.view_layer.update(); extra=twine_fish(o)
        sc.render.filepath=f'{out}/{key}_{mode}.png'; bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(o)
        for e in extra: bpy.data.objects.remove(e)
