"""Inventory icons of the cigarillo packs + LitCigarillo: python3 render_cgl_icons.py <bakedir> <texdir> <outdir>"""
import sys, os
here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, 'render_icons.py')).read()
exec(src[:src.index('J = os.path.join')])
sys.path.insert(0, here)
import cigarillos as C
J = os.path.join
only = set(sys.argv[4:])
for b, v in C.all_varieties():
    if only and b['key'] not in only: continue
    render(C.pack_mesh_name(b), J(TEX, C.pack_tex(b, v)), (0, 0, 22), J(OUT, C.item_id(b, v) + '.png'))
if not only: render('cgl_classic', J(TEX, 'cgl_whitehowl_newyorker.jpg'), (0, 55, 0), J(OUT, 'LitCigarillo.png'))
