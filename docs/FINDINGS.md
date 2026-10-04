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

### The owner's King: 2D king on its board, straight steps between boards (2026-09-27)

The owner proposed a King that keeps the ordinary chess king's 8 moves
within its own x-y board (diagonals included) and steps straight along z
or w to a neighbouring board, but never diagonally across boards: 12 moves
in the open instead of 32 (7 on a face, 4 in a corner). Nothing else
changes: the board is still 8x8x8x8 and every other piece keeps its rule.
This is the `boardKing` option of `BoardGeometry` (`--boardking`). It
distinguishes the x-y board from the other axes, so the symmetry group
drops from 384 to 64 elements (the generator's fundamental domain was
generalised accordingly and re-tested). Exact results:

| Ending | Queen rule | Board | Won | Longest mate |
|---|---|---|---|---|
| K+Q vs K | settled (32 dirs) | 8x8x8x8 | 14,358 classes, all with the King on an edge | 9 moves |
| K+Q vs K | settled | 6^4 / 7^4 | 0.01% / 0.00% | 9 moves |
| K+Q vs K | 3-axis (64 dirs) | 6^4 | **100%** | 7 moves |
| K+Q vs K | 3-axis | 7^4 | **100%** | **8 moves** |
| K+B vs K | Bishop 3-axis (56 dirs) | 7^4 | 5,751 (0.00%) | 7 moves |
| K+R vs K | settled | 7^4 | 0, no checkmate exists | none |
| K+Q+R vs K | settled | 4^4 | **100%** (53,261,721 of 53,261,721) | 19 moves |
| K+Q+R vs K | settled | 8x8x8x8 | more than 20 million won classes (sparse solver's cap); exact count not computed | unknown |

(The dense generator overflows its array limit at side 8 under the 64-element
group, so the side-8 K+Q table with the settled Queen was solved with the
sparse solver instead, which is exact when little is won; the 3-axis Queen
is a full win at sides 6 and 7 with the mate length growing by one move per
side, and the sparse solver cannot hold a full-board win set. The side-8
figure for the winning combination is therefore inferred, not tabulated.)

Comparison with the earlier King variants:

| King | Moves in the open | K+Q, settled Queen | K+Q, 3-axis Queen | Lone Bishop (3-axis) mates? |
|---|---|---|---|---|
| settled (any 2 axes) | 32 | draw (18 wins) | draw (0.01%) | no |
| orthogonal only | 8 | draw (0.02%) | **win, 8 moves** | **yes, 80 moves** |
| owner's board-King | 12 | draw (14,358 wins) | **win, 8 moves at side 7** | **no** |

So the owner's King does what the orthogonal King does for the Queen, and
does not hand the Bishop a forced mate: the best balance of the three. The
extra 3-axis Queen lines are still required for K+Q alone; with the settled
Queen every King tried is a draw. The Rook remains mate-less against every
King tried. K+Q+R with the settled Queen, however, does win against this
King: every position on the 4x4x4x4 board (the settled King's figure there
is 0.02%), and on the full board the won set passed 20 million classes,
sixty times the settled rules' total, before the sparse solver stopped. A
full-board four-piece table under the 64-element group is about 9 trillion
entries at the current indexing (white King in the domain, one piece
canonical, the rest unreduced), some 18 TB, beyond any single machine.

### Endgame table: which material forces mate (2026-09-27)

Longest forced mate from any legal position; "not possible" means mate
cannot be forced (only cooperative or edge-trick mates exist). Exact unless
stated. The owner's King is the 12-move board-King above.

| White vs lone King | Settled King (32), 8^4 | Owner's King (12) | 2D chess |
|---|---|---|---|
| K+Q | not possible | not possible (8^4 exact); win in 8 moves with a 3-axis Queen | 10 moves |
| K+Q+R | not possible | **win on 4^4 (19 moves) and on 5^4 (36 moves, every one of 2.13 billion positions, central ones included)**; 8^4 not computable here (about 9 trillion entries), but over 2 million mates in one exist there, about 950 with the King 2-3 cells from every edge | 6 moves |
| K+R+R | not possible | not possible (8^4 exact: 5,764 mates in one, edge only) | 7 moves |
| K+R+B | not possible | not forced on 4^4 (0.04%); 8^4 unresolved (won set passed 2 million) | 16 moves |
| K+Q+B | not possible | **win on 4^4 (11 moves) and on 5^4 (15 moves, every one of 2.08 billion positions)**; 8^4 not computable here, likely win | 8 moves |
| K+B+B | not possible (no mate exists) | not possible on 8^4 (exact: 1,081,121 won classes, all on an edge, longest 11 moves), although 4^4 is a 25-move win with opposite-parity Bishops | 19 moves (opposite colours) |
| K+N+N | not possible | not possible (8^4 exact: 53,425 won, edge only) | not possible |
| K+B+N | not possible | not forced on 4^4 (0.06%); 8^4 unresolved (won set passed 2 million) | 33 moves |

The 2D column reproduces the published values (K+Q 10, K+B+B 19, K+B+N
33, K+N+N drawn), which is the validation for the generator that produced
the other columns. The K+B+B row is the warning against reading 4^4 as 8^4:
a small-board win can vanish on the full board, so the K+Q+R and K+Q+B
wins are marked likely, not confirmed, until a full-board table is run.

The 5^4 tables (2026-10-01, one byte per position, 6.5 billion entries
each, 36 and 35 minutes) strengthen both: at side 5 every legal position is
still won, including the 3.4 million with the King two cells from every
edge, so the win is not an edge effect. The mate lengths differ sharply.
K+Q+B grows slowly (11 moves at side 4, 15 at side 5) and should stay
practical at side 8. K+Q+R nearly doubles (19 then 36 moves): the Rook's
lines are nearly useless in 4D, so the Queen does the herding alone and the
Rook's job reduces to covering the last cells; extrapolated to side 8 the
longest K+Q+R mate would exceed a fifty-move rule. Under the owner's King,
Queen and Bishop is the practical mating pair, not Queen and Rook.
Summaries under `docs/tablebase/variants/`.

### The cover inequality: why this King cannot be mated away from an edge

A general rule, stated for any King rule and any attacking material M, on
any board:

> Let E(b) be the cells the black King may step to from b. A checkmate at b
> needs every cell of E(b) attacked or safely occupied, and b attacked. Let
> cover(M, b) be the largest number of cells of E(b) that M can attack or
> safely occupy over all legal placements (a piece standing on an escape
> cell counts only if defended; the white King may not stand next to the
> black one). If cover(M, b) < |E(b)|, no checkmate exists at b, by any
> play. If that holds for every cell b off the edge, every checkmate has
> the black King on an edge.

For the owner's King and the settled Queen on 8x8x8x8: |E(b)| = 12 for
every interior b, and **cover(K+Q, b) = 11**. Checked exhaustively over all
16.7 million (Queen, King) placements against a central King, with the
Queen blocked only by the black King (an over-count in White's favour), in
`docs/cover_bound.py`. No placement covers all 12, with or without check.
The interior is uniform for this purpose (every escape of an interior King
is on the board and blocking geometry does not change), so the central
King is the worst case and the bound holds at every interior cell.

By hand, the 11 comes from three facts: a Queen one step from the King
along an axis covers 8 escapes (the 6 straight ones off its line and the 2
board diagonals on its side) plus its own cell; the white King covers at
most 3 (the three cells on the far side, from two steps away on the axis);
and the Queen's own cell counts only if the white King defends it, which
puts him next to the Queen and away from the far side. So 8 + 3 = 11 with
the Queen undefended, or 9 + at most 2 with it defended. Every other Queen
placement covers fewer (6 from the z or w side, 4 from a board diagonal, 4
from a non-capturable 3-axis neighbour, 3 from two steps away).

The same inequality is what proves the settled-rules results: |E| = 32 and
cover is 20 for K+Q and 25 for K+Q+R (the `safe` tool computes it), so
neither can mate an interior King. It is also where the orthogonal King
fails Black: |E| = 8 and a 3-axis Queen alone covers 6 with the white King
supplying the rest, so interior mates exist and, as the table shows, are
forced. For the owner's King with the 3-axis Queen the inequality is not
the mechanism: the win comes from herding to an edge, not from interior
mates.

What the inequality does not prove is that Black can stay off the edge. A
forcing sequence could in principle drive the King to an edge and mate it
there. For the owner's King with the settled Queen the full table answers
that part: every one of the 14,358 won positions already has the black
King on an edge, so from any position with the King off the edge Black
holds the draw. The inequality is the hand-checkable half; the table is
the other half.

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
**the owner's board-King (2D king on its board, straight steps between
boards) with a Queen that also slides on 3-axis diagonals.** It yields a
4D K+Q ending with the same shape as 2D and an 8-move longest mate, keeps
the Bishop unable to mate alone, and leaves the Rook, Knight and pawns
untouched. Whether the Bishop should share the 3-axis lines is a balance
choice for self-play, not a mating question. The tables for the variant are reproducible
with `Chess4D.Tablebase generate Q --diag 3 --king 1` (about a minute);
summaries for every variant tried are under `docs/tablebase/variants/`.

## The counting theorem: mating material grows with dimension (2026-10-04)

Generalising the cover inequality. A King that steps along one or two axes
has 2d^2 escape cells in the open. A line-piece (rook, two-axis bishop,
their union the line-queen) attacks along lines through its own cell, so
from any placement it covers only a linear number of those escapes.
`docs/cover_bound.py` enumerates every placement of the white King and one
piece around a central black King (side 7, so every cell within distance 3
exists; the piece is blocked only by the black King, an over-count for
White; a piece on an escape cell counts only if defended):

| d | escapes 2d^2 | queen alone | rook alone | bishop alone | king alone | best K+Q | best K+R | best K+B |
|---|---|---|---|---|---|---|---|---|
| 2 | 8 | 5 | 4 | 2 | 3 | 7 | 6 | 5 |
| 3 | 18 | 8 | 4 | 4 | 6 | 14 | 10 | 10 |
| 4 | 32 | 12 | 6 | 8 | 7 | 19 | 13 | 15 |
| 5 | 50 | 16 | 8 | 12 | 9 | 25 | 17 | 21 |

The queen covers 4d - 4 escapes, the best K+Q pair about 6d - 5, against
2d^2 to cover: already in 2D the pair falls one short (7 of 8), which is
why 2D's K+Q mates only on the edge, and the shortfall grows as d^2. Since
each additional line-piece adds O(d), **the number of line-pieces needed to
cover a central King's escapes grows at least linearly in d**, roughly
d/3 pieces. This is the theorem behind "lines are not walls": the attack
set of a line-piece has codimension d - 1, and confinement needs
codimension 1. A Chebyshev King (3^d - 1 escapes) against line-pieces
widens the gap to exponential; a hyperplane-attacking piece closes it.

Dimension sweep under the settled convention (K and Q change at most two
axes), exact tables:

| d | board | K+Q | K+R |
|---|---|---|---|
| 2 | 8^2 | win, 10 moves | win, 16 moves |
| 3 | 8^3 | draw (0.02%, 3 moves) | draw (mates in one only) |
| 4 | 8^4 | draw (18 positions, mate in one) | no checkmate exists |
| 5 | 5^5 and 6^5 | **no checkmate exists** | no checkmate exists |
| 6 | 4^6 and 5^6 | **no checkmate exists** | no checkmate exists |

The d = 5 and d = 6 results confirm the corner bound derived on 2026-09-19 (a corner
King has d + C(d,2) flights, the white King covers at most 6 and the Queen
d, so mate needs C(d,2) <= 6, i.e. d <= 4): from five dimensions on, King
and Queen cannot checkmate a lone King at all, not even with cooperation.

## Cross-validation matrix: every ruleset in the literature, every material set (2026-10-04)

Built by `Chess4D.Tablebase matrix` from the registry `docs/rulesets.json`
into `docs/tablebase/matrix.csv` (126 cells, 172 minutes on the laptop,
zero verification failures; each row carries its regenerating command).
Rulesets are the tuples (d, n; D_Q; D_K) of `docs/LITERATURE.md`. Every
cell is exact on the stated board: three-piece endings by the dense
generator (or the sparse solver where the dense table overflows),
two-piece endings by the dense four-piece generator up to about 7 x 10^9
entries and by the sparse solver otherwise.

**WIN n** = forced from every legal position, longest n moves. A percentage
in brackets (99.9%) marks a general win where the few unwon positions are
those with a piece immediately lost, exactly as 2D's K+B+N (99.5%).
**WIN\*** = forced with Bishops of opposite parity (about half of all
positions), as in 2D. **draw (p%, n)** or **draw (c cl, n)** = not
forceable; p% of positions, or c classes up to symmetry, won; longest n
moves. **none** = no checkmate position exists at all. **cap** = the won
set exceeded the sparse solver's cap on 8^4 (these are wins where tested
on smaller boards). **WIN (implied by K+Q)** = not separately tabulated
because K+Q alone is a forced win under that ruleset.

| Ruleset | Q | R | B | N | QQ | QR | QB | QN | RR | RB | RN | BB | BN | NN |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Settled (4, 8; {1,2}; {1,2}) | draw (0.000%, 1) | none | none | none | draw (106000 cl, 5) | draw (51625 cl, 4) | draw (56040 cl, 3) | draw (46588 cl, 2) | draw (49 cl, 1) | draw (400 cl, 1) | draw (70 cl, 1) | none | none | none |
| Rinaldi-Chiru (4, 8; {1,2}; {1..4}) | none | none | none | none | draw (3311 cl, 3) | none | none | none | none | none | none | none | none | none |
| Dawson Normal Form / 4\*Chess (4, 4; {1..4}; {1..4}) | **WIN 4** | none | none | none | **WIN 4** | **WIN 5** | **WIN 4** | **WIN 5** | none | **WIN 13** (99.95%) | none | **WIN 7** | **WIN 13** (99.96%) | none |
| Chesseract (4, 4; {1,2}; {1}) | **WIN 20** | none | none | none | **WIN 8** | **WIN 15** | **WIN 10** | **WIN 10** | draw (0.005%, 1) | **WIN 33** (100.00%) | draw (0.011%, 2) | **WIN\* 22** | draw (0.016%, 2) | draw (0.014%, 1) |
| Owner's board-King (4, 8; {1,2}; {1}+xy) | draw (14358 cl, 9) | none | none | none | see 5^4 row | see 5^4 row | see 5^4 row | see 5^4 row | see 5^4 row | see 5^4 row | see 5^4 row | see 5^4 row | see 5^4 row | see 5^4 row |
| Owner's board-King, 5^4 (two-piece endings) | draw (0.094%, 14) | none | none | none | **WIN 12** | **WIN 36** | **WIN 15** | **WIN 27** | draw (0.000%, 1) | draw (0.005%, 11) | draw (0.000%, 2) | draw (0.007%, 11) | draw (0.005%, 15) | draw (0.001%, 5) |
| Hyperchess (4, 4; pair diagonals) | none | none | none | none | draw (0.004%, 1) | draw (0.001%, 1) | none | draw (0.002%, 1) | none | none | draw (0.000%, 1) | none | draw (0.000%, 1) | draw (0.001%, 1) |
| Raumschach (3, 5; {1,2,3}; {1,2,3}) | **WIN 8** | none | none | none | **WIN 5** | **WIN 8** | **WIN 6** | **WIN 8** | draw (0.810%, 10) | **WIN 17** (99.90%) | draw (0.382%, 16) | **WIN 12** | **WIN 18** (99.88%) | draw (0.005%, 1) |
| Orthogonal King + 3-axis Queen (4, 8; {1,2,3}; {1}) | **WIN 8** | none | **WIN 80** | none | WIN (implied by K+Q) | WIN (implied by K+Q) | WIN (implied by K+Q) | WIN (implied by K+Q) | draw (8416 cl, 1) | cap | cap | cap | cap | draw (14681 cl, 1) |

Hyperchess K+3Q (not in the table): draw, 0.6% won, longest 70 moves; the
position Joyce gave is a draw with either side to move.

Readings:

- **Agreement with the only prior computation.** Muller (2014) reported
  for Raumschach that K+R+R has wins with longest mate in 10 and K+R+N
  longest mate in 16, and that 4-men endings without a Queen are general
  draws. The Raumschach row gives exactly 10 and 16 moves, 0.81% and 0.38%
  won. Two independent generators, eleven years apart, agree.
- **The 80-move King (Rinaldi and Chiru) is unmatable by two-axis
  pieces.** On 8x8x8x8 no single piece and no pair has a checkmate
  position, except 3,311 cooperative K+Q+Q corner mates.
- **The owner's King on 5^4**: every pair containing a Queen is a forced
  win (K+Q+Q 12 moves, K+Q+B 15, K+Q+N 27, K+Q+R 36); every queenless pair
  is a draw. K+Q alone remains a draw. This is the cleanest "Queen plus
  one" rule found in any 4D ruleset.
- **Small boards flatter the attacker.** Everything with a Queen wins on
  4^4 under the Dawson rules and under Chesseract; the settled rules'
  draws on 8^4 are a large-board phenomenon (see the 80/80 threshold
  between sides 6 and 7 in the variants section).
- **Rooks never mate alone in any ruleset tested**, and two Rooks force
  mate in none of them; Beasley's 2007 remark that in three dimensions a
  Rook "doesn't" present a barrier is borne out in every column.
- **Bishop pairs behave as in 2D** wherever they win at all: half the
  positions (opposite parity) are won, half are dead draws.
- **The orthogonal King with a 3-axis Bishop**: the lone Bishop forces
  mate (80 moves), so every pair containing it is a win too (capped on
  8^4).

## Relation to the published work (2026-10-04)

`docs/LITERATURE.md` section 8 tabulates every printed claim about mating
material in 3D and 4D chess that could be tested here, with the result.
The natural predecessor of this project is Rinaldi and Chiru, *AppliedMath*
6(3):48 (2026): the same 8x8x8x8 board and the same two-axis Queen, with a
Chebyshev (80-move) King. They set up the framework, posed the endgame
question and answered it empirically, by engine-assisted play from random
placements and an informal strategy sketch, labelling the results
"empirical demonstrations rather than formal proofs". This project extends
that work to exhaustive computation at the current limit of what is
tractable: three-piece endings solved exactly on 8^4, four-piece endings
exactly on 4^4 and 5^4 and bounded on 8^4, the cover inequality as the
hand-checkable half, and the frontier (full-board four-piece tables under
reduced symmetry) stated.

One finding bears directly on their section 5.3. Under their Definitions 7
and 9 as implemented here, K+Q vs K and K+R vs K have no checkmate
position on 8^4, 4^4 or 5^4, so the engine-assisted demonstrations cannot
have ended in checkmate under those definitions; a rule or win criterion
the paper does not state must have been in play. This is reported as a
finding that invites clarification, not as a judgement of the paper.

The variant designers' printed claims all check out exactly where
testable: Muller's 2014 Raumschach "KQK is won" (8 moves on 5x5x5); the
4x4x4x4 full-King K+Q win (4 moves) of Pacey, Reiniger and Joyce, together
with the observation that it does not extend past side 6; Aikin's
orthogonal-King K+Q (20 moves on 4^4); and Joyce's 2004 position (BK 3333,
WK 1122, WQ 1111 1112 1121), which is a draw with either side to move
exactly as he said, with K+3Q winning only about 0.6% of positions under
his rules (longest mate 70 moves).

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
