"""Cover bound for the owner's King (2D king on its x-y board, straight steps between boards).

For an interior black King b with escape set E(b) (12 cells), enumerate every placement of the
white King and the settled 32-line Queen and count how many escapes are attacked or safely
occupied (the Queen's own cell counts only when the white King defends it; the white King may
not stand adjacent to b). The Queen is blocked only by the black King, which over-counts in
White's favour. Result on 8x8x8x8: at most 11 of 12 escapes are ever covered, so no interior
checkmate exists. Run: python3 docs/cover_bound.py
"""
from itertools import product
S, n = 8, 4
cells = list(product(range(S), repeat=n))
def add(a, d): return tuple(x + y for x, y in zip(a, d))
def on(c): return all(0 <= x < S for x in c)
nz = lambda v: sum(1 for x in v if x)
KSTEP = [v for v in product((-1, 0, 1), repeat=n) if any(v) and (nz(v) == 1 or (nz(v) == 2 and v[0] and v[1]))]
QDIR = [v for v in product((-1, 0, 1), repeat=n) if any(v) and nz(v) <= 2]
b = (3, 3, 3, 3)
E = {add(b, d): 1 << i for i, d in enumerate(KSTEP)}
FULL = (1 << len(E)) - 1
kadj = lambda a, c: tuple(x - y for x, y in zip(a, c)) in KSTEP
qcov, qchk = {}, {}
for q in cells:
    if q == b: continue
    m, chk = 0, False
    for d in QDIR:
        c = add(q, d)
        while on(c):
            if c == b: chk = True; break
            m |= E.get(c, 0)
            c = add(c, d)
    qcov[q], qchk[q] = m, chk
kcov = {k: sum(E.get(add(k, d), 0) for d in KSTEP) for k in cells if k != b and not kadj(k, b)}
best, full = 0, 0
for q, qm in qcov.items():
    own = E.get(q, 0)
    for k, km in kcov.items():
        if k == q: continue
        m = qm | km | (own if own and kadj(k, q) else 0)
        best = max(best, bin(m).count("1"))
        full += m == FULL and qchk[q]
print("escapes", len(E), "| max covered", best, "| placements covering all with check", full)
