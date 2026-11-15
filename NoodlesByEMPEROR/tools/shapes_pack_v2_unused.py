# Tried 11-14 (cake plateau, folds, zigzag seals); the user said to drop it. Not used.
import math
import numpy as np
from shapes import Mesh, _noise


def _sm(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


def pillow_bag_v2(W, H, T, seal=.06, side_seal=0.0, seed=1, wrinkle=.0025, res=(72, 56), uv_front=(0, 0, .5, 1), uv_back=(.5, 0, 1, 1), squash=1.0, block='rect'):
    """A flow-wrap noodle pack lying on its back. W across (x), H along (z, label's up = -z), T thickness.
    The noodle cake (block: 'round' | 'rect' | 'stick') lifts a plateau in the middle, the film drapes from it to the flat
    end seals in radial folds, the sides pull in where the pack is full so the seals flare out as ears, and the seal
    ends are cut in a zigzag. Front faces +y with uv_front (u0,v0,u1,v1); back faces -y."""
    m = Mesh()
    r = np.random.RandomState(seed)
    ph = r.rand(8) * 6.28
    Hin = H * (1 - 2 * seal)
    nfold = int(r.randint(9, 14))
    curl = (r.rand(2) - .3) * .004
    def cake(x, z):
        if block == 'round':
            R = .40 * min(W, Hin)
            return math.hypot(x, z) / R
        bx, bz = (.37 * W, .38 * Hin) if block == 'rect' else (.36 * W, .44 * Hin)
        return ((abs(x) / bx) ** 4 + (abs(z) / bz) ** 4) ** .25
    def pos(s, t, side):
        tt = (t - seal) / (1 - 2 * seal)
        inner = 0 < tt < 1
        ttc = min(max(tt, 0.0), 1.0)
        full = math.sin(math.pi * ttc) ** .8
        x = (s - .5) * W * (1 - .085 * full)                    # the sides pull in, the seal ends stay wide
        z = -(t - .5) * H
        y = 0.0
        if inner:
            edge = math.sin(math.pi * s) ** .5 * math.sin(math.pi * tt) ** .42
            d = cake(x, z)
            lift = .5 + .5 * _sm(1 - (d - .95) / .55)             # plateau over the cake, lower film around it
            y = T / 2 * edge * lift
            if side > 0:
                ang = math.atan2(z, x)
                out = _sm((d - .9) / .5) * edge                    # folds only in the loose film around the cake
                y += out * (.0030 * math.sin(nfold * ang + ph[0]) + .0016 * math.sin((nfold * 2 + 3) * ang + ph[1]))
                y += (1 - _sm((d - .8) / .3)) * edge * .0007 * (math.sin(x * 420 + ph[2]) * math.sin(z * 380 + ph[3]))   # the cake's waves showing through
                y += _noise(s, t, seed, wrinkle * .5) * edge
            else:
                y = -(y * squash + _noise(s, t, seed + 5, wrinkle * .4) * edge)
        else:
            k = (-tt if tt <= 0 else tt - 1) / (seal / (1 - 2 * seal))        # 0 at the seal line, 1 at the cut edge
            y = curl[0 if tt <= 0 else 1] * k * k * (1 + .5 * math.sin(s * 7 + ph[4])) + side * .0002
            if k > .97:                                                        # zigzag cut
                tri = abs(((s * W / .0045) % 1) * 2 - 1)
                z += (1 if tt <= 0 else -1) * .0022 * (tri - .5)
        return (x, y, z)
    u0, v0, u1, v1 = uv_front
    m.grid(lambda s, t: pos(s, t, 1), res[0], res[1], lambda s, t: (u0 + s * (u1 - u0), v0 + t * (v1 - v0)), flip=False)
    b0, c0, b1, c1 = uv_back
    back = Mesh().grid(lambda s, t: pos(s, t, -1), res[0] // 2, res[1] // 2, lambda s, t: (b1 - s * (b1 - b0), c0 + t * (c1 - c0)), flip=True)
    m.merge(back)
    m.drop_to_floor()
    return m


