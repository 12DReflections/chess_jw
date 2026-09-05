# 4D move notation

Status: **PROPOSED, not approved.** Until the owner approves a compact form,
the game, the typed-move input, the move history and the save format all use
the long tuple form in section 1, exactly as `SPEC.md` Stage 4 requires. Nothing
in the code emits or parses section 2 yet.

## 1. Long form (in use now)

A cell is the plain tuple `(x,y,z,w)`, 0-indexed. A move is:

```
[piece letter] (from) [-|x] (to) [=promotion] [ e.p.] [+|#]
```

- Piece letters `K Q R B N`; pawns have no letter.
- `-` for a quiet move, `x` for a capture.
- `=Q`, `=R`, `=B`, `=N` after a promotion.
- ` e.p.` after an en passant capture.
- Castling is `O-O` toward +x and `O-O-O` toward -x.
- `+` check, `#` checkmate.

Examples: `(2,1,3,3)-(2,3,3,3)`, `Q(3,0,3,3)-(0,3,3,3)+`, `N(4,2,0,0)-(2,1,0,0)#`,
`(0,6,3,3)-(0,7,3,3)=N`, `(3,3,2,4)x(4,2,2,4) e.p.`

Typed input accepts `(from) (to)`, `(from)-(to)` or `(from)x(to)`, with an
optional `=X`; a promotion without a suffix promotes to Queen. Flags such as
double step and en passant are resolved against the legal move list, so the
player never types them.

Move history lines are numbered per move pair with the side marked:
`2. W Q(3,0,3,3)-(0,3,3,3)+`.

## 2. Compact form (proposal)

Rationale: the tuple form is unambiguous but long; a full 4D move is 26
characters. Standard chess names a square by file letter and rank digit. The
proposal keeps that for x and y so a 2D player reads it instantly, and appends
the two extra axes as letters after a dot.

```
cell := <x as a-h><y as 1-8>.<z as a-h><w as a-h>
```

- `(3,0,3,3)` becomes `d1.dd`; `(0,3,3,3)` becomes `a4.dd`; `(2,1,0,0)` is `c2.aa`.
- A move is `Qd1.dd-a4.dd+`, a capture `Nb3.dc x d4.dc`, promotion `a7.dd-a8.dd=N`.
- The `.dd` suffix is the central layer pair on a side of 8 (z = w = 3), so
  most opening moves read as 2D chess with `.dd` attached, which is a feature.
- At six dimensions the suffix grows to three letters; the form is
  dimension-generic.

Alternatives considered:

- Four letters `dadd`: shortest, but unreadable and loses the 2D echo.
- Rank digit for every axis `d1d4`... mixes letters and digits in a way that
  hides which axis is which.
- Semicolon tuples `3;0;3;3`: no shorter than parentheses.

Open questions for the owner: whether y should stay 1-based (chess habit) while
the other axes are letters; whether the dot is wanted or `d1dd` is acceptable.

## 3. Position text format, version 1 (in use now)

Tuples only, one item per line, comments start with `#`:

```
chess4d-position 1
dimensions 4
side 8
to-move white
en-passant -
halfmove 0
W K (4,0,3,3)
W Q (3,0,3,3) moved
B K (4,7,3,3)
B P (3,6,3,3) moved
```

`moved` marks a piece whose has-moved bit is set; it controls castling rights and
pawn double steps. The loader rejects a mismatched dimension count or side and
any unknown line. Implemented in `PositionText`, round-trip tested.
