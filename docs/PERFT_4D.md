# 4D perft reference numbers

Generated 2026-09-05 by `Chess4D.Core` at the Stage 2 gate. Nobody has published
perft values for this game; these are the reference for anyone reimplementing
it. They are asserted by `Stage2FourDimensionsTests.FourDimensionalPerft` and
must not be adjusted to match a new generator. If a reimplementation disagrees,
one of the two generators is wrong; use `Perft.Divide` to find the diverging
root move.

## Rules the numbers depend on

Exactly the rules in `SPEC.md` section 2. In brief:

- Board 8x8x8x8, axes `x, y, z, w`, 0-indexed. `y` is the axis of advance;
  White moves +y, Black moves -y.
- Rook: +/-1 on one axis, slide (8 directions). Bishop: +/-1 on exactly two axes,
  slide (24). Queen: union (32). King: the Queen's 32 directions, one step.
  Knight: +/-2 on one axis and +/-1 on a different axis, jumps (48).
- Pawn: one step +y (White); double step from an unmoved pawn if both the
  passed-through and landing cells are empty; captures one step forward plus
  +/-1 on exactly one of `x, z, w` (6 capture cells); promotes on y=7 (White) or
  y=0 (Black) to Queen, Rook, Bishop or Knight, four moves each; en passant onto
  the passed-through cell of a double step, on the immediately following move.
- Castling along `x` at the King's own `y, z, w`, standard conditions, King two
  cells toward the Rook, Rook to the cell the King crossed.
- A move is legal if and only if the mover's own King is not attacked afterward.
- Starting position: back rank `R N B Q K B N R` at `(x, 0, 3, 3)` for White and
  `(x, 7, 3, 3)` for Black, King at x=4, Queen at x=3; pawns on every on-board
  cell at Chebyshev distance 1 from a back-rank cell that is not itself a
  back-rank cell (136 per side). 144 pieces per side, 288 total.
- Fifty-move rule and threefold repetition off.

Perft counts leaf nodes: `perft(1)` is the number of legal moves, and each
promotion choice is a separate move.

## Results

| Depth | Nodes | Time (Debug build, Apple Silicon, single thread) |
|---|---|---|
| 1 | 196 | 1 ms |
| 2 | 38,416 | 75 ms |
| 3 | 7,584,070 | 14.7 s |

## Cross-checks

- **Depth 1 = 196** matches the hand derivation in `SPEC.md` Stage 2: 0 back-rank
  moves (every Chebyshev-1 neighbour of the back rank is a friendly pawn), 26 per
  Knight for 52, 144 from the 72 pawns at y=1 (single and double step), 0 from
  the 64 pawns at y=0, 0 captures. The generator's breakdown is asserted
  category by category in `DepthOnePerftIs196AndMatchesTheHandDerivation`.
- **Depth 2 = 38,416 = 196 squared.** No White first move interacts with any
  Black first move: the armies are seven layers apart along y and the deepest
  first move reaches y=3, so Black's 196 replies are available after every one
  of White's 196 moves. Any other depth-2 figure would indicate a bug.
- **Depth 3 = 7,584,070**, an average of 197.4 White second moves per depth-2
  node. Slightly above 196 because opening a pawn or knight unblocks back-rank
  pieces; slightly below the open-board maximum because pawn double steps are
  consumed. No independent value exists yet.

## Reproducing

```bash
cd src && dotnet test --filter "FullyQualifiedName~FourDimensionalPerft" --logger "console;verbosity=normal"
```
