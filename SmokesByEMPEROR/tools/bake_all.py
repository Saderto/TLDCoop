"""Bake every item to one mesh + one albedo texture for the game.
python3 bake_all.py <texdir> <outdir> [only_id ...]
Writes <outdir>/<id>.npz (verts, normals, uvs, tris in Blender metres, Z up; per-loop split) and <id>_albedo.png.
Items whose parts already share one UV atlas (packs, pouch) keep it; others get a smart-UV atlas and a Cycles DIFFUSE colour bake.
"""
import sys, os, math
import numpy as np
import bpy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import models, pipes, cutter

TEX, OUT = sys.argv[1], sys.argv[2]
ONLY = set(sys.argv[3:])
os.makedirs(OUT, exist_ok=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 16
    sc.cycles.device = 'CPU'
    return sc


def flatten(root_or_objs):
    objs = root_or_objs if isinstance(root_or_objs, list) else [root_or_objs]
    meshes = []
    for o in objs:
        if o.type == 'MESH':
            meshes.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
        if o.parent:
            mw = o.matrix_world.copy()
            o.parent = None
            o.matrix_world = mw
    bpy.context.view_layer.objects.active = meshes[0]
    # apply modifiers + transforms
    for o in meshes:
        bpy.context.view_layer.objects.active = o
        for m in list(o.modifiers):
            try:
                bpy.ops.object.modifier_apply(modifier=m.name)
            except Exception:
                pass
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    return bpy.context.view_layer.objects.active


def bake(ob, item_id, size=1024, margin=0.004):
    me = ob.data
    src_uv = me.uv_layers.active.name if me.uv_layers else None
    bake_uv = me.uv_layers.new(name='bake')
    me.uv_layers.active = bake_uv
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=margin, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=margin, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    img = bpy.data.images.new(item_id + '_albedo', size, size, alpha=False)
    # each material gets an image node (active) pointing at the target image; its own textures keep reading their UV via the
    # node's explicit UVMap (we set render UV to the source layer)
    if src_uv:
        for layer in me.uv_layers:
            layer.active_render = (layer.name == src_uv)
    for slot in ob.material_slots:
        m = slot.material
        if m is None:
            continue
        nt = m.node_tree
        # albedo only: metals and glassy parts bake black otherwise
        for n in nt.nodes:
            if n.type == 'BSDF_PRINCIPLED':
                for k in ('Metallic', 'Transmission Weight', 'Subsurface Weight', 'Coat Weight'):
                    if k in n.inputs and not n.inputs[k].is_linked:
                        n.inputs[k].default_value = 0.0
        # image textures without explicit vector: feed them the source UV
        uvn = nt.nodes.new('ShaderNodeUVMap')
        uvn.uv_map = src_uv or 'UVMap'
        for n in list(nt.nodes):
            if n.type == 'TEX_IMAGE' and not n.inputs['Vector'].is_linked:
                nt.links.new(uvn.outputs['UV'], n.inputs['Vector'])
        tgt = nt.nodes.new('ShaderNodeTexImage')
        tgt.image = img
        tuv = nt.nodes.new('ShaderNodeUVMap')
        tuv.uv_map = 'bake'
        nt.links.new(tuv.outputs['UV'], tgt.inputs['Vector'])
        nt.nodes.active = tgt
    sc = bpy.context.scene
    sc.render.bake.use_pass_direct = False
    sc.render.bake.use_pass_indirect = False
    sc.render.bake.use_pass_color = True
    sc.render.bake.margin = 8
    bpy.ops.object.bake(type='DIFFUSE', uv_layer='bake')
    path = os.path.join(OUT, item_id + '_albedo.png')
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    return 'bake'


def save_mesh(ob, item_id, uv_name):
    me = ob.data
    me.calc_loop_triangles()
    uvl = me.uv_layers[uv_name].data
    verts, norms, uvs, tris = [], [], [], []
    try:
        me.split_normals_calc  # noqa (Blender < 4.1)
    except AttributeError:
        pass
    corner_normals = me.corner_normals if hasattr(me, 'corner_normals') else None
    for t in me.loop_triangles:
        tri = []
        for li in t.loops:
            l = me.loops[li]
            v = me.vertices[l.vertex_index].co
            n = corner_normals[li].vector if corner_normals is not None else l.normal
            verts.append((v.x, v.y, v.z))
            norms.append((n.x, n.y, n.z))
            uvs.append(tuple(uvl[li].uv))
            tri.append(len(verts) - 1)
        tris.append(tri)
    v = np.array(verts, np.float32)
    n = np.array(norms, np.float32)
    u = np.array(uvs, np.float32)
    t = np.array(tris, np.int32)
    # weld identical corners
    key = np.concatenate([np.round(v, 6), np.round(n, 4), np.round(u, 6)], 1)
    _, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    np.savez(os.path.join(OUT, item_id + '.npz'), verts=v[first], normals=n[first], uvs=u[first], tris=inv.reshape(-1)[t.reshape(-1)].reshape(-1, 3) if False else inv.reshape(-1)[t].astype(np.int32))
    lo, hi = v.min(0), v.max(0)
    print(f'{item_id}: {len(first)} verts, {len(t)} tris, size {hi - lo}')


def item(item_id, build, keep_uv=False):
    if ONLY and item_id not in ONLY:
        return
    reset()
    objs = build()
    if not isinstance(objs, list):
        objs = [objs]
    ob = flatten(objs)
    ob.name = item_id
    if keep_uv:
        uv_name = ob.data.uv_layers.active.name
    else:
        uv_name = bake(ob, item_id)
    save_mesh(ob, item_id, uv_name)


import render_packs as rp  # noqa: E402  (module-level main() guarded below)

if __name__ == '__main__':
    for b in ['chapgirl', 'milston', 'marlbrrro', 'parliamint', 'bent', 'caramel']:
        def mk(b=b):
            o = rp.pack_mesh('pack_' + b)
            o.data.materials.append(rp.tex_mat('m_' + b, os.path.join(TEX, 'pack_%s.png' % b)))
            return [o]
        item('pack_' + b, mk, keep_uv=True)
    item('pack_wide', lambda: [rp.pack_mesh('pack_wide', (92, 86, 17), 9)], keep_uv=True)
    item('pack_slim', lambda: [rp.pack_mesh('pack_slim', (46, 98, 17), 13)], keep_uv=True)  # narrow tall box (superslims)  # flat landscape flip box (wide varieties)
    item('cigarette', lambda: models.cigarette(TEX))
    item('cigar_cohibra', lambda: pipes.cigar(TEX, 'cohibra', seed=1))
    item('cigar_montecrusto', lambda: pipes.cigar(TEX, 'montecrusto', L=0.15, R=0.0098, seed=2))
    item('cigar_cut_cohibra', lambda: pipes.cigar(TEX, 'cohibra', seed=1, cut=True))
    item('cigar_cut_montecrusto', lambda: pipes.cigar(TEX, 'montecrusto', L=0.15, R=0.0098, seed=2, cut=True))
    item('tube_cohibra', lambda: models.tube(TEX, 'cohibra'))
    item('tube_montecrusto', lambda: models.tube(TEX, 'montecrusto'))
    import cigars as cg

    def lin(c, k=1.0):
        return tuple(min(1.0, (v / 255.0 * k)) ** 2.2 for v in c)
    for c in cg.CIGARS:
        k = cg.lc(c)
        L, R, head = cg.SHAPES[c['shape']]
        kw = dict(L=L, R=R, seed=c['seed'], head=head, press=c['press'])
        item('cigar_' + k, lambda kw=kw, k=k: pipes.cigar(TEX, k, **kw))
        item('cigar_cut_' + k, lambda kw=kw, k=k: pipes.cigar(TEX, k, cut=True, **kw))
        b = c['band']
        bg = b['bg'] if b['bg'] not in (cg.WHITE, cg.CREAM) else (186, 188, 192)
        if b['bg'] == cg.BLACK: bg = (34, 32, 30)
        cols = (lin(bg), lin(bg, .85), lin(b['trim']))
        item('tube_' + k, lambda k=k, L=L, R=R, cols=cols, pr=c['press']: models.tube(TEX, k, L=L + .015, R=R * (1.12 if pr else 1.0) + .0017, colors=cols))
    for c in cg.CIGARS + cg.OLD:
        k = cg.lc(c)
        def mkb(c=c, k=k):
            o = rp.pack_mesh('box_' + k, cg.box_dims(c), cg.box_scale(c))
            o.data.materials.append(rp.tex_mat('mb_' + k, os.path.join(TEX, 'box_%s.jpg' % k)))
            return [o]
        item('box_' + k, mkb, keep_uv=True)
    item('pipe_billiard', lambda: pipes.pipe_billiard(TEX))
    item('pipe_bent', lambda: pipes.pipe_bent(TEX))
    item('pipe_churchwarden', lambda: pipes.pipe_churchwarden(TEX))
    item('pipe_corncob', lambda: pipes.pipe_corncob(TEX))
    item('pouch_captainblock', lambda: models.pouch(TEX), keep_uv=True)
    item('lighter_zappo', lambda: models.lighter())
    item('cutter_zikar', lambda: cutter.cutter())
