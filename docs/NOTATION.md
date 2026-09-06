# 4D move notation

Status: **APPROVED by the owner, 2026-09-06.** The compact form below is the
move notation. Save files keep the long tuple form.

## 1. Compact form (moves, history, typed input)

A cell is its coordinates written as digits with nothing between them, one
digit per axis in axis order `x y z w`, all 0-indexed exactly as the code
counts. `(3,0,3,3)` is `3033`. At six dimensions a cell is six digits.

A move is:

```
[piece letter] from [-|x] to [=promotion] [ e.p.] [+|#]
```

- Piece letters `K Q R B N`; pawns have no letter.
- `-` quiet move, `x` capture.
- `=Q`, `=R`, `=B`, `=N` after a promotion.
- ` e.p.` after an en passant capture.
- Castling is `O-O` toward +x and `O-O-O` toward -x.
- `+` check, `#` checkmate.

Examples: `2133-2333`, `Q3033-0333+`, `N4200-2100#`, `0633-0733=N`,
`3324x4224 e.p.`, `O-O`.

Why this and not a letters-and-digits form: the axes are all the same kind of
thing, so they get the same encoding; there is nothing to learn; the numbering
matches the code so no off-by-one bugs; and it extends to six dimensions as six
digits. A form that mimicked 2D chess was rejected because the pawn shell
spreads pawns across z and w, so most opening moves would not have looked like
chess anyway.

History lines are numbered per move pair with the side marked:
`2. W Q3033-0333+`.

Typed input accepts the compact form, the long tuple form, or a mix:
`3033 0333`, `3033-0333`, `Q3033x0333`, `(3,0,3,3) (0,3,3,3)`, with an
optional `=X`. Flags such as double step and en passant are resolved against
the legal move list, so the player never types them. A promotion without a
suffix promotes to Queen.

## 2. Long form (save files)

A cell is the plain tuple `(x,y,z,w)`. The move grammar is the same as
section 1 with tuples in place of digit runs: `Q(3,0,3,3)-(0,3,3,3)+`. Save
files use tuples throughout.

## 3. Position text format, version 1

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
