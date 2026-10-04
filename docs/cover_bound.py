"""Cover bound for line-pieces against a two-axis King in d dimensions.

A King that steps along one or two axes has 2d^2 escape cells in the open. For d = 2..6 this
enumerates every placement of a white King (same rule) and one line-piece (queen = rook + two-axis
bishop; rook; bishop) around a central black King on a board of side 7 (so every cell within
Chebyshev distance 3 exists; farther cells add nothing new), and reports the most escape cells
that can be attacked or safely occupied: a piece standing on an escape cell counts only when the
white King defends it; the white King may not stand next to the black one; the piece is blocked
only by the black King (an over-count in White's favour). The gap between 2d^2 and the cover is
the arithmetic behind "no interior mate", and the per-piece numbers grow linearly in d while the
escapes grow quadratically. Run: python3 docs/cover_bound.py [maxd]
"""
import sys
from itertools import product

def run(d, piece):
    S = 7; c = S // 2
    centre = (c,) * d
    nz = lambda v: sum(1 for x in v if x)
    KSTEP = [v for v in product((-1, 0, 1), repeat=d) if 1 <= nz(v) <= 2]
    ROOK = [v for v in product((-1, 0, 1), repeat=d) if nz(v) == 1]
    BISH = [v for v in product((-1, 0, 1), repeat=d) if nz(v) == 2]
    DIRS = {'Q': ROOK + BISH, 'R': ROOK, 'B': BISH}[piece]
    add = lambda a, v: tuple(x + y for x, y in zip(a, v))
    on = lambda p: all(0 <= x < S for x in p)
    E = {add(centre, v): 1 << i for i, v in enumerate(KSTEP)}
    kadj = lambda a, b: tuple(x - y for x, y in zip(a, b)) in KSTEP
    cells = [p for p in product(range(S), repeat=d) if p != centre]
    # white King masks, grouped by whether the King is adjacent to a given escape cell (needed for defence)
    kmask = {}
    for k in cells:
        if kadj(k, centre): continue
        kmask[k] = sum(E.get(add(k, v), 0) for v in KSTEP)
    distinct_k = set(kmask.values())
    best = 0; best_piece = 0
    for q in cells:
        m = 0
        for v in DIRS:
            p = add(q, v)
            while on(p):
                if p == centre: break
                m |= E.get(p, 0)
                p = add(p, v)
        best_piece = max(best_piece, bin(m).count('1'))
        own = E.get(q, 0)
        if own:
            # the piece stands on an escape cell: its cell counts only if the King defends it
            for k, km in kmask.items():
                if k == q: continue
                tot = m | km | (own if kadj(k, q) else 0)
                best = max(best, bin(tot).count('1'))
        else:
            for km in distinct_k:
                best = max(best, bin(m | km).count('1'))
    return 2 * d * d, best_piece, max(bin(km).count('1') for km in distinct_k), best

if __name__ == '__main__':
    maxd = int(sys.argv[1]) if len(sys.argv) > 1 else 5
    print('d | escapes 2d^2 | queen alone | rook alone | bishop alone | king alone | best K+Q | best K+R | best K+B')
    for d in range(2, maxd + 1):
        e, q, k, kq = run(d, 'Q'); _, r, _, kr = run(d, 'R'); _, b, _, kb = run(d, 'B')
        print(f'{d} | {e} | {q} | {r} | {b} | {k} | {kq} | {kr} | {kb}')
