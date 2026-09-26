# Findings

Research results from the 4D chess project, recorded regardless of outcome.
Generated 2026-09-08 with the Stage 6 tablebase generator
(`src/Chess4D.Tablebase`), which was first validated at two dimensions against
the published results: K+Q vs K mates in at most 10 moves (19 plies white to
move), K+R vs K in at most 16 (31 plies), K+B and K+N cannot mate. All four 2D
tables reproduced those numbers exactly, with zero one-ply consistency
failures and the engine's own search agreeing on every sampled short mate.

Rules: `SPEC.md` section 2. Board 8x8x8x8. Fifty-move rule off. The symmetry
group is the hyperoctahedral group of order 384; tables are indexed by
canonical position (white king in a fundamental domain of 35 cells, white
piece an orbit representative under the king's stabiliser, black king
unreduced, side to move). Each table has 428,933,120 two-byte entries of which
62,388,368 are duplicate slots marked illegal.

## The three research questions

**1. Can King and Queen force mate against a lone King in 4D?** No.

Up to symmetry there is exactly **one** checkmate position in K+Q vs K on the
4D board: black king on 0000, white king on 0111, white queen on 2000, Black
to move. The white king, three axes away from the black king so the kings are
not adjacent, covers ten of the corner's fifteen neighbour cells; the queen
gives check along the x axis and covers the remaining four neighbours along
its two-axis diagonals; the queen is two cells away so it cannot be captured.
Exactly **18** white-to-move positions are won, all mate in one, all with the
kings on those cells and the queen on a cell from which it can reach 2000:
the x-y, x-z and x-w diagonals through 2000 and the y line through it. No
position at distance 2 or more exists: Black can always avoid the corner
configuration, so nothing is forced. Every other legal position, 178,112,490
of the 178,112,508 white-to-move positions, is a draw. There is exactly one
stalemate position class.

**2. What is the longest forced mate?** One ply. There is no forced mate of
any length beyond the 18 mate-in-one positions.

**3. What is the minimum material that forces mate?** More than any single
piece. K+R vs K, K+B vs K and K+N vs K have **no checkmate positions at all**
on the 4D board, not even unforced ones: zero mates, zero stalemates, zero
wins. K+Q vs K has mates but none can be forced. Forcing mate in 4D therefore
needs at least two pieces beyond the king; which two, and whether any
combination suffices, was the four-piece question the spec deferred; it is
answered below without the 1.5 TB table: no two pieces suffice.

## Four pieces: can King, Queen and Rook force mate? (2026-09-19)

**No.** On the full 8x8x8x8 board K+Q+R vs K is a draw from every position
except a thin set in which the black king already stands on an edge of the
board, and the same holds for every other pair of pieces: all ten
combinations of two pieces from Q, R, B, N were solved exactly, not sampled.

| Ending (4D, side 8) | Checkmates | WTM wins | BTM losses | Longest forced mate | Wins with the black king off the edge |
|---|---|---|---|---|---|
| K+Q+R vs K | 1,282 | 51,625 | 1,331 | 7 plies (4 moves) | 0 |
| K+Q+Q vs K | 2,888 | 106,000 | 3,069 | 9 plies (5 moves) | 0 |
| K+Q+B vs K | 1,364 | 56,040 | 1,416 | 5 plies | 0 |
| K+Q+N vs K | 1,102 | 46,588 | 1,109 | 3 plies | 0 |
| K+R+R vs K | 16 | 49 | 16 | 1 ply | 0 |
| K+R+B vs K | 32 | 400 | 32 | 1 ply | 0 |
| K+R+N vs K | 5 | 70 | 5 | 1 ply | 0 |
| K+B+B, K+B+N, K+N+N vs K | 0 | 0 | 0 | none | 0 |

Counts are positions up to the 384 board symmetries. For scale, K+Q+R vs K
has roughly 6 x 10^11 such positions with White to move, so the won
fraction is below one in ten million. About 95% of the K+Q+R wins are mates in
one; the deepest, for example K2111 Q1110 R1003 against k0000, take four
moves and all happen in a corner. No position is won with the black king one
step or more away from every edge. Since the black king starts centralised in
any real game and cannot be driven to an edge (the three-piece result already
showed why: lines do not wall off a 4D board), these endings are drawn in
practice and in theory.

The longest mate does not grow with the board: K+Q+R gives 7 plies on sides 4,
5 and 8. The wins are local corner patterns, not a technique.

| K+Q+R vs K, 4D | Side 4 | Side 5 | Side 8 |
|---|---|---|---|
| Checkmates | 154 | 296 | 1,282 |
| WTM wins | 1,692 of 8,680,855 (0.02%) | 5,256 of 350,660,967 (0.0015%) | 51,625 |
| Longest forced mate | 7 plies | 7 plies | 7 plies |
| Wins with the black king off the edge | 0 | 0 | 0 |

Even the side-4 board, where every cell is at most one step from an edge, is
drawn; so is K+Q+Q there (9,539 of 7,919,398, longest 11 plies).

Minimum mating material in 4D is therefore more than two pieces beyond the
king. Three pieces (K+Q+Q+R and the like) is the next open question, and it
needs a new idea: with a third piece every two-piece win reappears with the
extra piece standing anywhere, so the decided set is no longer small (about
2 x 10^8 classes before anything new is found) and the sparse method below
stops being cheap.

### How it was computed

Three tools in `src/Chess4D.Tablebase`, each checked against the others:

1. `generate4` (`FourPieceGenerator`): the dense retrograde generator extended
   to K+A+B vs K, with captures by the black king resolved against the
   three-piece tables. Validated at two dimensions against the published
   results before use: K+B+N longest mate 33 moves (65 plies) with 99.51% of
   white-to-move positions won, K+B+B 19 moves (37 plies), K+N+N nothing
   beyond mates in one, K+Q+R every white-to-move position won. Zero failures
   in 5,000 sampled one-ply consistency checks per table. It fits in memory up
   to side 5 in 4D (1.4 billion entries); side 8 would be about 1.5 TB.
2. `sparse` (`SparseSolver`): an exact solver that stores only decided
   positions, which is what makes side 8 possible when almost nothing is won.
   It enumerates every checkmate (black king over the fundamental domain,
   White's cells searched with a cover bound, each hit confirmed by the Core
   rules), then closes the won set by retrograde steps in distance order;
   a black-to-move position is lost only when every legal move, captures
   included, lands in a won position. Everything outside the two sets is a
   draw because every predecessor of every decided position was examined.
   It takes its rules from `Board`, not from the generator's fast geometry,
   and reproduces the dense tables exactly: 2D K+Q (18,081 wins, 19 plies),
   4D side 4 (154 / 1,692 / 166 / 7) and side 5 (296 / 5,256 / 318 / 7).
   Every stored position of all ten side-8 endings (for K+Q+R, 51,625 wins
   and 1,331 losses) was then re-checked one ply deep against the full
   rules: zero failures. The engine
   finds the same mate on sampled three-ply wins; seven plies is beyond its
   search at this branching.
3. `safe` (`SafeRegion`): a table-free drawing certificate. A set S of cells
   is safe if, for every placement of White's pieces, a black king in S has
   an unattacked move that stays in S (attacks computed without blockers,
   occupied cells counted as covered, so every approximation favours White).
   For K+Q the greatest safe set is 3,792 of the 4,096 cells, which proves
   the three-piece result again without a table: White can cover at most 20
   of a central king's 32 moves. For K+Q+R the certificate is inconclusive
   (White could cover 25 of 32 if its pieces could teleport between moves),
   which is why the exact solve above was needed.

Reproduce: `Chess4D.Tablebase sparse QR` (about a minute), `generate4 QR
--side 5` (about 90 seconds, 6 GB), `safe Q`. Tests: `FourPieceTablebaseTests`.
Summaries are under `docs/tablebase/`.

## Rule variants: what makes King and Queen mate again (2026-09-26)

The owner asked whether the rules could change so that K+Q vs K is a win,
believably and without wrecking balance. The move rules were parametrised
(`BoardGeometry(dims, side, diagonalAxes, kingAxes)`: how many axes a
diagonal slide may change, 2 in the settled rules, and how many a King step
may change, 2 in the settled rules) and the three-piece tables regenerated
for each variant. All exact; 5,000-sample one-ply consistency with the full
rules, zero failures, on every table. SPEC section 6 lists the 80-direction
Queen and 72-direction Bishop as open items; those are `diagonalAxes = 4`.

### The finding: it is the King, not the Queen

K+Q vs K on the 8x8x8x8 board, white-to-move positions won:

| Queen diagonals | King step | Queen dirs / King moves | Won | Longest mate |
|---|---|---|---|---|
| 2 axes (settled) | 2 axes (settled) | 32 / 32 | 18 (0.00001%) | 1 ply |
| 3 axes | 2 axes | 64 / 32 | 22,479 (0.01%) | 9 moves |
| 4 axes (SPEC 6) | 2 axes | 80 / 32 | 757,000 (0.43%) | 17 moves |
| 3 axes | 3 axes | 64 / 64 | 783 (0.0004%) | 3 moves |
| 4 axes | 3 axes | 80 / 64 | 142,052 (0.08%) | 4 moves |
| 4 axes | 4 axes | 80 / 80 | 182,388 (0.11%) | 4 moves |
| 2 axes | **1 axis** | 32 / 8 | 36,224 (0.02%) | 22 moves |
| 3 axes | **1 axis** | 64 / 8 | **100%** | **8 moves** |
| 4 axes | **1 axis** | 80 / 8 | **100%** | **8 moves** |

- Giving the Queen more directions while the King keeps 32 moves does
  almost nothing: even the 80-direction Queen wins from 0.43% of positions.
  The "natural" generalisation, 80 directions for both, is no better than
  the settled rules (0.11%, four-move corner tricks only).
- Restricting the King to orthogonal steps (8 moves, the wazir of fairy
  chess) with the settled 32-direction Queen gives real technique (22-move
  mates exist) but still only 0.02% won: the Queen's lines cannot pin down
  even a slow King.
- Both together, an orthogonal King and a Queen with at least 3-axis
  diagonals, make K+Q vs K a forced win from **every** legal position,
  longest mate 8 moves (15 plies), shorter than 2D's 10. The 4-axis
  diagonals add nothing to this (same 8 moves), so the smaller change,
  3-axis diagonals (64 Queen directions), suffices. On the 6x6x6x6 board
  the same variant mates in at most 6 moves.

Why, as far as it is understood: a checkmate must cover every King move
plus the King's cell. Against an orthogonal King in the open (8 escapes), a
Queen one step away along an axis covers 6 of the 8 whether or not it has
3-axis lines (the six escapes off the Queen's line all differ from it on
exactly two axes); the other two are the Queen's own cell, defended by the
white King, and the cell behind the black King, which an edge or the white
King covers. So the final mate pattern exists under both Queens, and the
tables agree (611 checkmate classes with the settled Queen, 24,398 with the
80-direction one). The difference is in the forcing, not the mate: the
settled Queen cannot drive an orthogonal King to an edge (0.02% won,
though 22-move mates exist), and the 3-axis Queen can (100%). A hand
explanation of why the extra lines restore the herding technique is not yet
written; treat "it is the King" as the empirical finding and the mechanism
as open.

### The other pieces under the winning variant (orthogonal King)

| Ending, 4D side 8, King 1 axis | Won | Longest mate |
|---|---|---|
| K+R vs K (any diagonal rule; Rook unaffected) | 0, no checkmate exists | none |
| K+N vs K | 0, no checkmate exists | none |
| K+R+R vs K | 8,416 classes, all with the King on an edge | 1 ply |
| K+B vs K, Bishop 3-axis (56 directions) | **100%** | **80 moves** |
| K+B vs K, Bishop 4-axis (72 directions) | **100%** | **14 moves** |

This inverts 2D: a lone Bishop mates and a lone Rook does not, nor do two
Rooks. If Bishop and Queen share the diagonal rule, the Bishop is the
second-strongest piece by a wide margin (56 or 72 directions against the
Rook's 8) and every 2D piece value is wrong. The 3-axis Bishop's 80-move
mates would also fall to a fifty-move rule. Two sensible repairs, both
untested: keep the Bishop at 2-axis diagonals (24) while only the Queen
gains 3-axis lines, or accept the Bishop's strength and retune values by
self-play. Testing the first needs the Bishop's diagonal rule split from
the Queen's in `BoardGeometry`.

### Three dimensions for comparison (8x8x8)

| Ending | Rules | Won | Longest mate |
|---|---|---|---|
| K+Q vs K | settled (Queen 18 dirs, King 18 moves) | 609 (0.02%) | 3 moves |
| K+R, K+B, K+N vs K | settled | mates in one only (28, 24, 11) | 1 ply |
| K+Q+R vs K | settled | 3.65 million classes, none with the King 2+ from an edge | 31 moves |
| K+Q vs K | King 1 axis (6 moves) | **100%** | **20 moves** |
| K+Q vs K | Queen 3-axis (26 dirs), King 2 axes (18) | **100%** | **36 moves** |
| K+Q vs K | Queen 3-axis, King 1 axis | **100%** | **8 moves** |

Three dimensions with the settled rules are drawn like four. In 3D either
change alone suffices, because 3-axis diagonals are the full 26-direction
neighbourhood there; in 4D both are needed.

### Variants that need no table

- **Bare king loses** (shatranj). Any material advantage then wins, the
  whole minimum-mating-material problem disappears, and it is historically
  grounded. Cheapest fix by far; changes the game's character (trading down
  becomes decisive).
- **Stalemate is a win** for the side delivering it. Untested here; the
  settled tables show very few stalemates (1 class for K+Q), so it is
  unlikely to help on its own.
- **Smaller boards** do not help: the settled K+Q on 4x4x4x4 has 6 wins.

### Recommendation

If the aim is a game that ends, the smallest believable change is:
**King steps along one axis only; Queen (and possibly Bishop) slide on
diagonals of up to three axes.** It yields a 4D K+Q ending with the same
shape as 2D and a shorter mate. Its cost is the piece-value inversion above,
which self-play can measure. The tables for the variant are reproducible
with `Chess4D.Tablebase generate Q --diag 3 --king 1` (about a minute);
summaries for every variant tried are under `docs/tablebase/variants/`.

## Formal statement and the counting bound (2026-09-19)

Recorded from the owner's questions on what is proved and what generalises.
Status is marked on each item.

**Existence versus forcing.** Let P be the legal positions, split into P_W
(White to move) and P_B (Black to move), with an edge for each legal move,
and let M in P_B be the checkmates.

- "A mate exists" is reachability: M is not empty. All quantifiers are
  existential (a helpmate).
- "Mate can be forced" is an attractor. W_0 = M; W_{i+1} adds each p in P_W
  with some move into W_i, and each p in P_B with at least one legal move and
  every move into W_i. W is the union. White forces mate from p exactly when
  p is in W, and the distance is the least i. Quantifiers alternate (a
  directmate).
- K+Q vs K in 4D: M is one orbit under the hyperoctahedral group of order
  384 (k0000, K0111, Q2000); W_1 \ W_0 has 18 positions; W_2 = W_1. The
  complement is closed in Black's favour, so Black stays in it forever.
  The 2D analogue is K+N+N vs K. *Status: computed exhaustively; a
  computer-assisted proof conditional on the generator, which is validated
  at 2D and cross-checked by the table-free safe-region certificate.*

**King mobility.** Pieces change at most two coordinates per move. An
interior king has 2d + 4 C(d,2) = 2d^2 moves (32 at d = 4, 8 at d = 2); a
corner king has d + C(d,2) (10 at d = 4, 3 at d = 2).

**Counting bound, tight at d = 4.** A corner king has d + C(d,2) flight
cells. A white king that is not adjacent covers at most 6 of them (from a
cell like 0111), independent of d. A queen checking from a safe distance
(a cell like 2000) covers d of them. Mate needs d + C(d,2) <= 6 + d, that
is C(d,2) <= 6, so d <= 4, with equality at d = 4 (10 = 6 + 4), which is why
exactly one mate class exists there. *Status: derived by hand, not checked
exhaustively. Gap: a queen adjacent to the black king and defended by the
white king covers more and needs its own bound; at d = 4 the table shows it
yields no mate.* Conjecture: for d >= 5, K+Q vs K has no checkmate position
at all. Testable: d = 3 should have slack (6 flight cells), and d = 5 at
side 6 is about 2.4 x 10^8 entries.

**Why lines are not walls.** In 2D a rook's line has codimension 1 and cuts
the board; in 4D a line has codimension 3 and separates nothing, so the
union of lines White attacks never confines the king. *Status: heuristic.
The safe-region certificate makes it rigorous for K+Q (White covers at most
20 of a central king's 32 moves); for two pieces the exact solve stands in
for it (teleporting K+Q+R could cover 25 of 32, so the one-ply argument
fails there).*

**A programme.** Define the mating number m(d, n, k): the least material
that forces mate on the side-n board in d dimensions with pieces changing
at most k coordinates. Known here: m > two pieces at (4, 8, 2); 2D chess has
m = one rook. Related established work, cited from memory and to be checked
before use: cops and robbers on graphs (cop number of a product of d paths
grows like (d+1)/2), Conway's angel problem (evader wins; 3D settled before
2D), Hamkins and co-authors on infinite and 3D chess, and queens domination
in higher dimensions. No literature search has been done for prior 4D
mating-material results.

## Why the 4D lone king is so hard to mate

A 4D king has 32 escape directions and even in a corner keeps 15 neighbour
cells. A queen attacks along lines that change one or two coordinates, so
from any cell it covers at most a small fraction of a corner's neighbourhood;
a rook covers even fewer and the knight and bishop cannot both check and cover
enough at once. The single K+Q mate needs the white king on the "three-axis"
cell 0111, which is the only cell adjacent (in the Chebyshev sense) to ten of
the fifteen neighbours while not itself being a king move from the corner.
Nothing forces the black king into that corner: away from the boundary it
always has an unattacked neighbour.

## Table summaries

| Ending | Legal WTM | Legal BTM | Checkmates | Stalemates | WTM wins | Longest WTM mate |
|---|---|---|---|---|---|---|
| K+Q vs K | 178,112,508 | 181,847,414 | 1 | 1 | 18 | 1 ply |
| K+R vs K | 180,518,184 | 181,847,414 | 0 | 0 | 0 | none |
| K+B vs K | 179,441,738 | 181,847,414 | 0 | 0 | 0 | none |
| K+N vs K | 180,438,662 | 181,847,414 | 0 | 0 | 0 | none |

Each table took about 22 seconds to initialise and under a second to solve on
12 cores; the difficulty the spec anticipated never arose because almost
nothing propagates. The per-table summaries and the K+Q log are under
`docs/tablebase/`; the 858 MB binary tables live under `Builds/tablebase/`
and are not committed.

## Verification

- One-ply consistency: 5,000 sampled positions per 4D table, full Core rules,
  zero failures.
- Engine cross-check: from each of the 18 winning positions the search finds
  the mate in one with the same distance; from the mated position the game
  reports checkmate; with the queen one cell further away the search finds no
  mate. Random sampling for engine checks cannot hit 18 wins in 178 million,
  so those positions are checked directly (`EngineConfirmsTheUniqueFourDimensionalQueenMate`).
- Reproduce: `dotnet test -c Release --filter TablebaseFourDimensionsGate`
  (explicit, about two minutes and 1.7 GB per table), or
  `Chess4D.Tablebase generate Q` followed by `list Q`.

## Earlier findings

- Check is possible at ply 3 in 4D, exactly as in 2D (Stage 2). The spec's
  "early check is impossible" was wrong; a slider checks from a distance
  once one shell pawn vacates.
- 4D perft: 196 / 38,416 / 7,584,070 at depths 1 to 3 (`PERFT_4D.md`).
- Over 1,000 random games a check first became available at median ply 41,
  earliest ply 5, mostly by queen, bishop and knight.
