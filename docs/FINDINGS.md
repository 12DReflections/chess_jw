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
combination suffices, is the four-piece question the spec defers (about 1.5 TB
per table at this indexing, cloud compute).

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
