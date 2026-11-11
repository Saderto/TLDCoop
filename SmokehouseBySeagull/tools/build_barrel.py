"""The barrel smokehouse as one game mesh with one baked texture.
Writes mod/SmokehouseBySeagull/meshes/Barrel.fpmesh, textures/Barrel.png, bake/Barrel.npz. python3 build_barrel.py"""
import bpy, bmesh, math, random, sys, os, struct
import numpy as np
R = './'
src = open(R + 'blender/scene.py').read()
sys.argv = ['x', '--', '/tmp/none', 'none']
exec(src[:src.index('# ---------- environment')])       # materials and primitives of the approved scene
C['barrel'].hide_render = False
random.seed(11)
Z0 = 0.13
parts = []
for i in range(4):
    a = math.pi / 4 + i * math.pi / 2
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.11)
    for v in bm.verts:
        k = 1 + random.uniform(-0.18, 0.18); v.co = Vector((v.co.x * k, v.co.y * k, v.co.z * 0.6 * k))
    parts.append(add(bm_mesh(bm, 'st'), 'st', 'barrel', (0.25 * math.cos(a), 0.25 * math.sin(a), 0.05), (0, 0, random.uniform(0, 6)), smooth=True, mat=M_stone))
prof = [(0, 0), (0.28, 0), (0.285, 0.012), (0.285, 0.27), (0.296, 0.285), (0.296, 0.30), (0.285, 0.315), (0.285, 0.565), (0.296, 0.58), (0.296, 0.595), (0.285, 0.61), (0.285, 0.868), (0.292, 0.88)]
parts.append(lathe('barrel', 'barrel', prof, (0, 0, Z0), M_barrel, seg=32))
# firebox door, shut: a plate bent to the wall, frame strips, hinges and a latch
def bent_plate(name, r, a0, a1, z0, z1, mat, seg=6):
    bm = bmesh.new(); cols = []
    for i in range(seg + 1):
        a = a0 + (a1 - a0) * i / seg
        cols.append([bm.verts.new((rr * math.cos(a), rr * math.sin(a), z)) for rr in (r - 0.004, r) for z in (z0, z1)])
    for p, q in zip(cols, cols[1:]):
        bm.faces.new((p[2], q[2], q[3], p[3])); bm.faces.new((p[3], q[3], q[1], p[1])); bm.faces.new((p[0], q[0], q[2], p[2])[::-1])
    bm.faces.new((cols[0][0], cols[0][2], cols[0][3], cols[0][1])); bm.faces.new((cols[-1][0], cols[-1][1], cols[-1][3], cols[-1][2]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return add(bm_mesh(bm, name), name, 'barrel', (0, 0, 0), smooth=False, mat=mat)
mid = -math.pi / 2
parts.append(bent_plate('door', 0.297, mid - 0.42, mid + 0.42, Z0 + 0.045, Z0 + 0.235, M_rust))
for a in (mid - 0.36,):
    for z in (Z0 + 0.08, Z0 + 0.2):
        parts.append(cyl('hinge', 'barrel', 0.009, 0.045, (0.302 * math.cos(a), 0.302 * math.sin(a), z), mat=M_steel, seg=8))
parts.append(box('latch', 'barrel', (0.05, 0.012, 0.014), (0.302 * math.cos(mid + 0.38), 0.302 * math.sin(mid + 0.38) - 0.002, Z0 + 0.14), (0, 0, 0.38), M_steel))
# air slots in the door: dark recessed strips (painted by a darker material)
M_slot = mat_simple('slot', (0.01, 0.008, 0.007), 0.9)
for k in (-1, 0, 1):
    parts.append(bent_plate('slot', 0.2985, mid - 0.03 + k * 0.16, mid + 0.03 + k * 0.16, Z0 + 0.07, Z0 + 0.13, M_slot, seg=2))
# lid, pipe and its cap
parts.append(lathe('lid', 'barrel', [(0, 0.012), (0.27, 0.012), (0.30, 0.0), (0.305, -0.02), (0.297, -0.02)], (0, 0, Z0 + 0.895), M_barrel, seg=32))
parts.append(lathe('pipe', 'barrel', [(0.045, 0.0), (0.045, 0.22), (0.05, 0.225), (0.038, 0.225), (0.038, 0.16)], (0.12, 0.05, Z0 + 0.905), M_rust, seg=14))
parts.append(lathe('cap', 'barrel', [(0, 0.30), (0.08, 0.26), (0, 0.262)], (0.12, 0.05, Z0 + 0.905), M_rust, seg=14))
for dx in (-0.03, 0.03):
    parts.append(cyl('capleg', 'barrel', 0.004, 0.06, (0.12 + dx, 0.05, Z0 + 0.905 + 0.25), mat=M_steel, seg=5))
# a handle on the lid
parts.append(box('handle', 'barrel', (0.12, 0.016, 0.012), (-0.1, -0.05, Z0 + 0.94), (0, 0, 0.5), M_steel))
for s in (-1, 1):
    parts.append(box('hleg', 'barrel', (0.012, 0.016, 0.03), (-0.1 + s * 0.05 * math.cos(0.5), -0.05 + s * 0.05 * math.sin(0.5), Z0 + 0.922), (0, 0, 0.5), M_steel))
bpy.context.view_layer.update()
# bake colours without the metal darkening
for m in bpy.data.materials:
    if not m.use_nodes: continue
    b = m.node_tree.nodes.get('Principled BSDF')
    if b is None: continue
    for l in list(m.node_tree.links):
        if l.to_socket == b.inputs['Metallic'] or l.to_socket == b.inputs['Normal']: m.node_tree.links.remove(l)
    b.inputs['Metallic'].default_value = 0
for o in bpy.data.objects: o.select_set(False)
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[4]
# object-space noise is per part: bake each part on its own island set after one common unwrap
bpy.ops.object.convert(target='MESH')
for o in parts:
    o.data.uv_layers.new(name='UV')
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.012, scale_to_bounds=False)
bpy.ops.object.mode_set(mode='OBJECT')
SZ = 1024
img = bpy.data.images.new('bake', SZ, SZ); ao = bpy.data.images.new('ao', SZ, SZ)
sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.render.bake.margin = 8
def target(image):
    for m in bpy.data.materials:
        if not m.use_nodes: continue
        n = m.node_tree.nodes.get('BAKE') or m.node_tree.nodes.new('ShaderNodeTexImage'); n.name = 'BAKE'; n.image = image
        for x in m.node_tree.nodes: x.select = False
        n.select = True; m.node_tree.nodes.active = n
w = bpy.data.worlds.new('w'); sc.world = w
target(img); sc.render.bake.use_clear = False
for o in parts:
    for x in bpy.data.objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_clear=False)
for x in bpy.data.objects: x.select_set(False)
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[4]
bpy.ops.object.join(); ob = bpy.context.view_layer.objects.active
target(ao); sc.cycles.samples = 64
bpy.ops.object.bake(type='AO', use_clear=True)
A = np.array(img.pixels[:]).reshape(SZ, SZ, 4); O = np.array(ao.pixels[:]).reshape(SZ, SZ, 4)[:, :, 0]
col = A[:, :, :3] * (0.45 + 0.55 * O[:, :, None])
srgb = np.clip(col * 1.2, 0, 1)        # byte images hold display values already; the game's lighting is dimmer than the preview sun
from PIL import Image
Image.fromarray((srgb[::-1] * 255).astype(np.uint8)).save(R + 'mod/SmokehouseBySeagull/textures/Barrel.png', optimize=True)
# mesh out: triangulate, split by loop (uv / normal seams)
me = ob.data; bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
try: me.calc_normals_split()
except Exception: pass
uvl = me.uv_layers.active.data; M = ob.matrix_world
verts, norms, uvs, tris, seen = [], [], [], [], {}
for p in me.polygons:
    tri = []
    for li in p.loop_indices:
        lp = me.loops[li]; co = M @ me.vertices[lp.vertex_index].co; no = (M.to_3x3() @ lp.normal).normalized(); uv = uvl[li].uv
        key = (lp.vertex_index, round(no.x, 3), round(no.y, 3), round(no.z, 3), round(uv.x, 4), round(uv.y, 4))
        if key not in seen:
            seen[key] = len(verts); verts.append(tuple(co)); norms.append(tuple(no)); uvs.append(tuple(uv))
        tri.append(seen[key])
    tris.append(tri)
v, n, uv, t = np.array(verts, np.float32), np.array(norms, np.float32), np.array(uvs, np.float32), np.array(tris)
np.savez(R + 'bake/Barrel.npz', verts=v, normals=n, uvs=uv, tris=t)
U = np.stack([-v[:, 0], v[:, 2], -v[:, 1]], 1); N = np.stack([-n[:, 0], n[:, 2], -n[:, 1]], 1); tt = t[:, [0, 2, 1]].reshape(-1)
with open(R + 'mod/SmokehouseBySeagull/meshes/Barrel.fpmesh', 'wb') as f:
    f.write(struct.pack('<ii', len(U), len(tt))); f.write(U.astype('<f4').tobytes()); f.write(N.astype('<f4').tobytes()); f.write(uv.astype('<f4').tobytes()); f.write(tt.astype('<i4').tobytes())
print('Barrel', len(U), 'verts', len(tt) // 3, 'tris, unity size', np.round(U.max(0) - U.min(0), 3), 'min', np.round(U.min(0), 3))
