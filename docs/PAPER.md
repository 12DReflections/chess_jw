# Which material forces mate in four dimensions? Exhaustive endgame tables across the published rulesets

Draft, 2026-10-04. Paper-ordered digest of `FINDINGS.md`; every number
links to a summary file under `docs/tablebase/` and regenerates from the
commands given. Status flags: [exact] computed exhaustively and verified;
[bounded] exact on smaller boards, capped on the full board; [open].

## Abstract

Four-dimensional chess has been proposed repeatedly since Maack (1908), and
its designers have long reported that a lone king is hard to mate; the one
refereed treatment of the 8x8x8x8 board (Rinaldi and Chiru, 2026) tests the
basic endgames empirically and leaves them open. We answer the question
exhaustively. We fix a notation for the rulesets in the literature, (d, n;
D_Q; D_K): dimension, side, and the sets of axis-counts a slider or a king
step may change. For the two-axis convention on 8x8x8x8 we compute
retrograde tables for every ending with one or two pieces against a lone
king and find that none forces mate: the only checkmates are cooperative
positions with the king on an edge, and K+Q has exactly one checkmate
position up to symmetry. We explain this with a cover inequality: a king
stepping along at most two axes has 2d^2 escape cells, a line-piece covers
O(d) of them, so the material needed to mate grows at least linearly with
dimension; from d = 5 King and Queen have no checkmate position at all. We
then compute the same tables for every ruleset in the literature (seven
rulesets, fourteen material sets), reproducing all prior computed and
printed results, locating a sharp board-size threshold (with full
80-direction pieces K+Q wins up to side 6 and draws from side 7), and
showing which king rules restore the mate: an orthogonal king, or a king
that moves as a 2D king within its own board, makes K+Q (with a three-axis
queen) or K+Q+R (with the standard queen) a forced win. Two independently
written solvers agree on every table; every stored position passes a
one-ply check against the full rules. The dataset, registry and solvers
are published.

## 1. Introduction and prior work

- The question. Minimum mating material is settled in 2D (K+Q, K+R, K+B+B,
  K+B+N win; K+B, K+N, K+N+N do not) and has been proved on every n x n
  board for K+R (Janicic, Maric, Malikovic 2019). In three and four
  dimensions it has been discussed for over a century without a
  computation.
- Variant designers' observations: Maack 1908 ("entschieden schwieriger
  ... mattzusetzen"; restrict the bare king); Beasley 2007 (a rook "doesn't"
  present a barrier in 3D; on a 6D board "K+Q can checkmate a bare K, but
  the mate cannot be forced"); Joyce 2004 (K+3Q drawn in Hyperchess, with a
  position); Reiniger 2009 (the 80-move king; "crippled king" remedy);
  Aikin (Chesseract, orthogonal king). See `LITERATURE.md` section 7.
- The only prior computation: Muller 2014, partial 4-men tables for 3D
  Raumschach (KQK won; KRRK longest 10; KRNK longest 16).
- The predecessor: Rinaldi and Chiru 2026 define chess on {1..8}^4 with
  the two-axis queen used here and a Chebyshev king, and report
  engine-assisted endgame demonstrations labelled "empirical ... rather
  than formal proofs".
- Formal relatives: Evans and Hamkins 2014 (3D infinite chess with the
  same piece convention; on an edgeless board no three-piece set but K+Q+Q
  can even construct a mate); surrounding cops (Burgess et al. 2020);
  d-dimensional queen domination (Barr and Rao 2006); cover-counting for
  queen graphs (Sullivan et al. 2017; Ambrose et al. 2025); the name
  "line-queen" (Cashman et al. 2024). None applied to chess in d >= 3.

## 2. Rulesets and notation

(d, n; D_Q; D_K [; flags]). D_Q: axis-counts a rook/bishop/queen step may
change (the rook always 1; the bishop 2..max(D_Q)); D_K likewise for the
king. Knight: 2 on one axis and 1 on another. Pawns excluded from the
endgames studied. Registry: `docs/rulesets.json`.

| Name | Tuple | Source |
|---|---|---|
| settled (this project) | (4, 8; {1,2}; {1,2}) | SPEC.md; Evans-Hamkins convention |
| Rinaldi-Chiru | (4, 8; {1,2}; {1,2,3,4}) | AppliedMath 2026, Defs 7, 9 |
| Dawson Normal Form / 4*Chess | (4, 4; {1,2,3,4}; {1,2,3,4}) | Dickins 1971; Pacey |
| Chesseract | (4, 4; {1,2}; {1}) | Aikin |
| board-king (owner) | (4, 8; {1,2}; {1} + x-y diagonals) | this project |
| Hyperchess | (4, 4; pair diagonals) | Joyce 2004 |
| Raumschach | (3, 5; {1,2,3}; {1,2,3}) | Maack; Muller 2014 |
| orthogonal king + 3-axis queen | (4, 8; {1,2,3}; {1}) | this project |

## 3. Methods

- Dense retrograde generator (K+X vs K, and K+A+B vs K): positions indexed
  under the board's symmetry group (hyperoctahedral B_4, order 384; reduced
  to 64 or 128 for rules that distinguish axes), white king in a
  fundamental domain, one piece canonical, the rest unreduced; two-byte
  (three-piece) or one-byte (four-piece) distance to mate; captures resolved
  against the three-piece tables.
- Sparse solver: enumerates every checkmate (cover-mask bound, then full
  rules), closes the won set by retrograde steps in distance order, stores
  only decided positions; exact whenever the won set is small; takes its
  rules from the game's `Board`, not from the generator's geometry.
- Verification: the two solvers share no rules code and agree on every
  table both can compute; the sparse solver re-checks every stored position
  one ply deep against the full rules, including terminal and sub-table
  (capture) labels, which is the anchoring Pavlov (2026) shows a
  consistency check needs; the dense generator checks 5,000 sampled
  positions. The engine confirms sampled short mates.
- Validation against known results: 2D theory (K+Q 10, K+R 16, K+B+B 19,
  K+B+N 33 moves; K+N+N drawn); Muller's Raumschach lengths (10 and 16);
  the designers' 4x4x4x4 K+Q mate.
- Reproducibility: `Chess4D.Tablebase matrix` rebuilds the dataset
  (`docs/tablebase/matrix.csv`) from the registry; each row carries its
  command.

## 4. The settled 8x8x8x8 ruleset [exact]

- Single pieces: K+Q has one checkmate class (k0000, K0111, Q2000) and 18
  won positions, all mate in one; K+R, K+B, K+N have no checkmate position.
- Pairs: all ten pairs from Q, R, B, N fail to force mate. Wins exist only
  with the black king on an edge: K+Q+R 51,625 classes (longest 7 plies),
  K+Q+Q 106,000 (9 plies), K+Q+B 56,040, K+Q+N 46,588, K+R+R 49, K+R+B 400,
  K+R+N 70; K+B+B, K+B+N, K+N+N have no checkmate at all.
- Minimum mating material is therefore at least three pieces beyond the
  king [open which].

## 5. Why: the cover inequality and the counting theorem

- Cover inequality. Let E(b) be the king's escape cells; cover(M, b) the
  most of them M can attack or safely occupy. If cover < |E(b)| off the
  edge, every checkmate has the king on an edge. Settled rules: |E| = 32;
  cover(K+Q) = 20, cover(K+Q+R) = 25. Exhaustive over placements.
- Counting theorem (d general). |E| = 2d^2; a queen covers 4d - 4, the best
  K+Q pair about 6d - 5 (d = 2..5 enumerated). Hence at least ~d/3
  line-pieces are needed to cover a central king; in 2D the pair is one
  short (7 of 8), which is why 2D mates need the edge, and the shortfall
  grows as d^2.
- Corner bound. A corner king has d + C(d,2) flights; K covers <= 6, Q
  covers d; mate needs C(d,2) <= 6, i.e. d <= 4, tight at d = 4 (the unique
  mate). Confirmed: at d = 5 (sides 5 and 6) K+Q has no checkmate position.
- What the inequality does not prove: that Black can never be driven to
  the edge. The tables supply that half.

## 6. Across the rulesets [exact on native boards]

The matrix (`FINDINGS.md`, "Cross-validation matrix"; dataset
`docs/tablebase/matrix.csv`, 126 cells, zero verification failures):
nine ruleset rows x fourteen material sets. Readings:

1. Muller's Raumschach lengths reproduced exactly (10, 16).
2. The Chebyshev (80-move) king is unmatable by any one or two two-axis
   pieces on 8^4 (only 3,311 cooperative K+Q+Q corner mates). Rinaldi and
   Chiru's section 5.3 demonstrations cannot have ended in checkmate under
   their Definitions 7 and 9; clarification invited.
3. Board size: with full 80-direction pieces K+Q wins at sides 4, 5, 6 (4,
   8, 14 moves) and collapses at 7 (0.40%) and 8 (0.11%). Reiniger's "any
   size board" conjecture fails between 6 and 7.
4. Rooks never mate alone in any ruleset tested, and no rook pair forces
   mate anywhere.
5. Bishop pairs behave as in 2D: opposite-parity pairs win where any
   bishop pair wins.
6. Joyce's Hyperchess position is a draw either side to move; K+3Q wins
   about 0.6% of positions, longest 70 moves.
7. Under the owner's King on 5^4, "Queen plus one piece wins, nothing
   else does": K+Q+Q 12, K+Q+B 15, K+Q+N 27, K+Q+R 36 moves; all
   queenless pairs draw.

## 7. Restoring the mate: the king as a dial [exact unless noted]

| King rule | Queen | K+Q | Lone bishop | K+Q+R |
|---|---|---|---|---|
| settled (32 moves) | any | draw | draw | draw |
| orthogonal (8) | settled | draw (0.02%, 22-move mates exist) | - | - |
| orthogonal (8) | 3-axis | **win, 8 moves** | **win** (80 moves 3-axis, 14 moves 4-axis) | - |
| board-king (12) | settled | draw | draw | **win** on 4^4 (19) and 5^4 (36) [bounded]; on 5^4 every Queen pair wins (Q+Q 12, Q+B 15, Q+N 27) and every queenless pair draws |
| board-king (12) | 3-axis | **win**, 7 (6^4), 8 (7^4) moves | draw | - |
| Chebyshev (80) | settled | no checkmate exists | no checkmate | no checkmate |

Recommendation for a playable 4D game: the board-king with the settled
queen (K+Q+R and K+Q+B win; the bishop does not mate alone; the rook,
knight and pawns are untouched), or the board-king with a 3-axis queen if
K+Q alone must win.

## 8. Frontier and limits

- Full-board four-piece tables under the reduced symmetry groups (about
  9 x 10^12 entries at the current indexing): K+Q+R and K+Q+B against the
  board-king are exact only to side 5; K+R+B and K+B+N open.
- Three pieces beyond the king under the settled rules [open].
- Pawns excluded.
- Prior claims that do not reproduce are reported as findings for
  clarification, not as verdicts.

## 9. Artefacts

`docs/rulesets.json`; `docs/tablebase/matrix.csv` and per-cell summaries;
`docs/cover_bound.py`; `src/Chess4D.Tablebase` (generate, generate4,
sparse, safe, matrix); 121 tests. All results regenerate on a laptop in
hours.
