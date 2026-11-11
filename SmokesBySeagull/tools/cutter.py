"""Cigar guillotine (double blade) model + preview. python3 cutter.py <texdir> <out.png>"""
import sys, os, math
import bpy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import models, pipes
from models import mat_principled, group


def steel():
    m, nt, b, tc = pipes._nodes('steel')
    b.inputs['Base Color'].default_value = (0.55, 0.55, 0.57, 1)
    b.inputs['Metallic'].default_value = 1.0
    b.inputs['Anisotropic'].default_value = 0.6
    n = nt.nodes.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = 2500
    mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (1.0, 0.02, 1.0)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector']); nt.links.new(mp.outputs['Vector'], n.inputs['Vector'])
    rr = nt.nodes.new('ShaderNodeMapRange'); rr.inputs['To Min'].default_value = 0.25; rr.inputs['To Max'].default_value = 0.45
    nt.links.new(n.outputs['Fac'], rr.inputs['Value']); nt.links.new(rr.outputs['Result'], b.inputs['Roughness'])
    return m


def plate(name, z, shift, mat, black=False):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(shift, 0, z))
    p = bpy.context.active_object
    p.name = name
    p.scale = (0.092, 0.036, 0.0028)
    bpy.ops.object.transform_apply(scale=True)
    import bmesh
    bm = bmesh.new(); bm.from_mesh(p.data)
    vert_edges = [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 1e-4]
    bmesh.ops.bevel(bm, geom=vert_edges, offset=0.014, segments=10, affect='EDGES', profile=0.5)
    bm.to_mesh(p.data); bm.free()
    for (x, r) in ((0.0, 0.0118), (-0.034 + shift * 0, 0.0085), (0.034, 0.0085)):
        bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=r, depth=0.02, location=(x + shift + (-shift if x == 0 else 0) * 0 , 0, z))
        c = bpy.context.active_object
        if x == 0:
            c.location.x = 0.0  # cigar hole stays centred, the plates slide
        mod = p.modifiers.new('hole', 'BOOLEAN'); mod.object = c; mod.operation = 'DIFFERENCE'
        bpy.context.view_layer.objects.active = p
        bpy.ops.object.modifier_apply(modifier='hole')
        bpy.data.objects.remove(c)
    ev = p.modifiers.new('edge', 'BEVEL'); ev.width = 0.0007; ev.segments = 2
    p.data.materials.clear()
    p.data.materials.append(mat)
    for poly in p.data.polygons:
        poly.material_index = 0
    bpy.ops.object.shade_smooth()
    return p


def cutter():
    st = steel()
    blk = mat_principled('blackcoat', (0.02, 0.02, 0.022), 0.4, 0.3)
    # dark body on both sides, steel blade between them (slid out a little so its edge shows)
    a = plate('cut_bottom', 0.0014, 0.0, blk)
    b = plate('cut_blade', 0.0042, -0.006, st)
    c = plate('cut_top', 0.0070, 0.0, blk)
    bpy.ops.object.text_add(location=(0.03, -0.012, 0.0085))
    t = bpy.context.active_object
    t.data.body = 'ZIKAR'
    try:
        t.data.font = bpy.data.fonts.load(os.path.join(models.FONTS, 'cinzel-latin-700-normal.woff'))
    except Exception:
        pass
    t.data.size = 0.0045
    t.data.align_x = 'CENTER'
    t.data.extrude = 0.00004
    t.data.materials.append(mat_principled('gold', (0.8, 0.6, 0.28), 0.3, 1.0))
    bpy.ops.object.convert(target='MESH')
    return [a, b, c, t]


if __name__ == '__main__':
    tex, out = sys.argv[1], sys.argv[2]
    sc = models.scene_setup((1400, 800))
    group('cutter', cutter(), (0.03, 0.0, 0.0), (0, 0, math.radians(-15)))
    group('cig', pipes.cigar(tex, 'cohibra', seed=1), (-0.06, -0.06, 0.0095), (0, math.radians(90), math.radians(10)))
    models.camera(sc, (0.0, -0.26, 0.20), (0.0, -0.01, 0.0), 45)
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
