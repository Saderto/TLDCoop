"""One picture per cigarillo variety (for the catalogue page): python3 render_cgl_singles.py <bakedir> <texdir> <outdir>"""
import sys, os
here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, 'render_icons.py')).read()
exec(src[:src.index('J = os.path.join')])
sys.path.insert(0, here)
import cigarillos as C
J = os.path.join
for b, v in C.all_varieties():
    render(C.mesh_name(b, v), J(TEX, C.tex_name(b, v)), (0, 90, 0), J(OUT, C.item_id(b, v) + '.png'), elev=35)
