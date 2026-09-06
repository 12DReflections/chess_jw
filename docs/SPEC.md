# 4D Chess — Build Specification

**Read this file in full at the start of every session. Read `PROGRESS.md` next.**

This project extends an existing 2D Unity chess game into a fully playable
four-dimensional chess game, with an engine, an endgame position editor, and
(later) an endgame tablebase generator.

The work spans multiple sessions. Do not attempt more than one stage per session.
Do not begin a stage until the previous stage's exit gate passes.

---

## 0. Non-negotiable ground rules

1. **Never change a rule in section 2.** They are settled. If a rule appears
   wrong or impossible, stop and write the problem into `PROGRESS.md` under
   "Blocked". Do not improvise a replacement.
2. **The core library must never reference `UnityEngine`.** If you find yourself
   wanting to, the design is wrong.
3. **Correctness before performance.** No optimisation until the stage gate passes.
4. **Every stage ends with a passing test run and a commit.** No exceptions.
5. **The published perft table in Stage 1 is the rules reference. The existing
   2D code is not.** It lacks en passant and stalemate, promotes only to Queen,
   and castles through check, so it cannot serve as a rules oracle. Keep it
   untouched through Stages 0 to 2 as a source of Unity patterns (input,
   tweeners, prefabs, materials, scene wiring). It is deleted at the start of
   Stage 3, when its replacement begins.
6. **A stage may span several sessions.** If you run out of context mid-stage,
   commit what works, write exactly where you stopped and what is half-finished
   into `PROGRESS.md`, and stop. Never rush to a fake finish.

---

## 1. Architecture

Three separate projects. The dependency arrows point one way only.

```
Chess4D.Core        pure C#, netstandard2.1, zero Unity references
   ├── board representation, move generation, legality, game state
   ├── dimension-generic (works at n=2, n=4, later n=6)
   └── consumed by everything below

Chess4D.Engine      pure C#, depends on Core
   ├── AttackMapService   (move generation only, no search, always-on)
   └── SearchEngine       (alpha-beta, single-threaded)

Chess4D.Tablebase   pure C# console executable, depends on Core
   └── retrograde analysis, symmetry reduction, never ships to the game

Chess4D.Unity       the Unity project, depends on Core and Engine
   └── rendering, input, UI only. No game rules here.
```

**Single-threaded rule, and its exact scope.** `SearchEngine` must be
single-threaded. WebGL is a likely future target and it has no usable
threading. If desktop parallelism is wanted later it is added as an optional
layer, never baked into the core.

The rule applies to `SearchEngine` and `AttackMapService` only. `Chess4D.Tablebase`
is a console tool that never ships to a browser, and its retrograde passes
**may and should be parallelised**. Do not let the search rule leak into the
tablebase; that mistake is easy to make and costs days of compute.

**No file I/O in Core or Engine.** Tablebase access goes behind an interface
with a null implementation, so a browser build simply has no tablebase.

---

## 2. Game rules (SETTLED — do not alter)

### Board

- Four axes: `x, y, z, w`. (`w` is the fourth spatial axis; the research
  documents call it theta.)
- Side length 8 on every axis. 8x8x8x8 = 4096 cells.
- Coordinates are 0-indexed, 0 to 7 inclusive.
- `y` is the axis of advance. White advances +y, Black advances -y.

### Piece movement

Directions are vectors in Z^4. "Slide" means repeat the direction until blocked
or off-board. Standard capture rules: a slide stops on the first occupied cell,
capturing it if it is an enemy.

| Piece  | Rule | Direction count |
|--------|------|-----------------|
| Rook   | +/-1 on exactly one axis, slide | 8 |
| Bishop | +/-1 on exactly two axes, slide | 24 |
| Queen  | union of Rook and Bishop, slide | 32 |
| King   | the Queen's 32 directions, one step only | 32 |
| Knight | +/-2 on one axis and +/-1 on a different axis, jumps | 48 |

Derivations, for your test assertions:
- Bishop: C(4,2) axis pairs x 2^2 sign combinations = 6 x 4 = 24
- Knight: 4 choices of the "2" axis x 3 remaining axes x 4 sign combinations = 48
- Queen and King: 8 + 24 = 32

The Knight jumps and cannot be blocked.

### Pawns

- **Non-capturing move:** one step forward along y.
- **Double step:** two steps forward, only from the pawn's own starting cell,
  only if both the passed-through cell and the landing cell are empty. Track
  `hasMoved` per pawn.
- **Capture:** one step forward along y, plus one step of +/-1 on exactly one
  of `x`, `z`, `w`. That is **6 capture cells**, not 2.
- **Promotion:** a White pawn reaching y=7, or a Black pawn reaching y=0,
  promotes to Queen, Rook, Bishop or Knight at the player's choice.
- **En passant:** if a pawn double-steps and passes through a cell covered by
  an enemy pawn's capture pattern, that enemy pawn may capture it on the
  immediately following move by moving to the passed-through cell.

### Starting position

Both armies sit at `z=3, w=3`, facing each other along y.

**Back rank** (8 pieces, a line along x, exactly as in standard chess):

```
White:  (x, 0, 3, 3) for x = 0..7   ->  R N B Q K B N R
Black:  (x, 7, 3, 3) for x = 0..7   ->  r n b q k b n r
```

King is at x=4. Queen is at x=3.

**Pawn shell.** Pawns occupy every cell within Chebyshev distance 1 of the back
rank that is on the board and not occupied by a back-rank piece.

For White that is the set:
```
{ (x, y, z, w) : x in 0..7, y in {0,1}, z in {2,3,4}, w in {2,3,4} }
minus the 8 back-rank cells
```
Count: 8 x 2 x 3 x 3 = 144, minus 8 = **136 pawns**.

For Black, the same with y in {6,7}. Also 136.

Total per side: 8 + 136 = **144 pieces**. 288 on the board.

**Consequences of the shell. These are expected and are not bugs.**

- The shell is two layers deep along y. 72 pawns sit at y=1 and 64 sit at y=0
  beside the back rank. Every y=0 pawn is initially blocked by the y=1 pawn in
  front of it and cannot move until that one advances. This is intended.
- No piece of either side can reach the enemy camp in fewer than four moves. The
  armies are 7 apart along y and the fastest mover, the Knight, covers 2 per
  move. **Correction recorded at the Stage 2 gate (2026-09-05):** reaching the
  camp is not needed for check. Sliders check from a distance once a shell
  pawn vacates, and check is possible at ply 3 exactly as in 2D chess:
  `P(2,1,3,3)-(2,3,3,3)`, `P(3,6,3,3)-(3,4,3,3)`, `Q(3,0,3,3)-(0,3,3,3)+`.
  The original sentence "early check is impossible by construction" was wrong.
  This is a consequence note, not a rule; no rule changed. See
  `PROGRESS.md` Findings.
- Verify once, by hand, that every cell at Chebyshev distance 1 from the White
  King at (4,0,3,3) is occupied at game start. It should be. If it is not, the
  shell generator is wrong.

**Generate the shell programmatically from the back rank.** Do not hardcode 136
positions. The same code, run with 2 dimensions, must produce standard chess's
8 pawns on the second rank. That equivalence is a required unit test.

### Castling

Works exactly as in standard chess, along the x axis at fixed y, z, w.

- White kingside: King (4,0,3,3) -> (6,0,3,3); Rook (7,0,3,3) -> (5,0,3,3)
- White queenside: King (4,0,3,3) -> (2,0,3,3); Rook (0,0,3,3) -> (3,0,3,3)
- Black: the same with y=7.

Conditions, unchanged: neither King nor the chosen Rook has moved; all cells
between them are empty; the King is not currently in check; the King does not
pass through or land on a cell attacked by the enemy.

"Attacked" means attacked from any of the four dimensions. This falls out of the
attack map automatically. Do not special-case it.

### Game end

- **Check:** the side to move's King is attacked.
- **Checkmate:** in check with no legal move. That side loses.
- **Stalemate:** not in check with no legal move. Draw.
- **Fifty-move rule** and **threefold repetition**: implement them, but expose
  them as configuration flags defaulting to **off**. The tablebase work needs
  them off.

A move is legal if and only if, after making it, your own King is not attacked.
Implement this with make/unmake and a real attack test. Do not approximate it.

### Performance budget

288 pieces start on the board and a Queen has 224 pseudo-moves on an open board.
Naive legality filtering will be slow. Targets apply to **two** positions, the
starting position and a fixed mid-game open position with at least one Queen
on an unobstructed line. The starting position is the easy case, since most
pieces are blocked; the open position is the one that matters for the UI.

- Pseudo-legal move generation: under 5 ms
- Full legal move generation: under 50 ms
- Attack-map query for a single cell: under 1 ms

Define the open benchmark position once, in a test fixture, and record it in
`PROGRESS.md` so the numbers are comparable across sessions. Write these as
benchmark tests in Stage 1 and keep them passing. If you cannot meet them,
write that into `PROGRESS.md` rather than silently accepting 500 ms. The
always-on attack map in the UI depends on these numbers.

---

## 3. Stages

Each stage has a hard exit gate. Do not proceed past a failing gate.
Record completion in `PROGRESS.md` with the date and the test output.

---

### Stage 0 — Project setup

1. Create branch `4d-rewrite` from `main`. Do all work there.
2. Upgrade the Unity project from 2020.3.19f1 to the editor version recorded
   under "Environment" in `PROGRESS.md`. The choice is the owner's: **Unity 6
   LTS (6000.x)** if the WebGL target matters, since its WebGL output is
   materially better; otherwise **2022.3 LTS**, which is already installed and
   is sufficient for every stage in this document. Open the project, resolve
   the console errors, confirm the existing 2D game still runs.
3. Install the .NET SDK if `dotnet` is not on the path. Create the solution
   structure: `Chess4D.Core`, `Chess4D.Core.Tests`, `Chess4D.Engine`,
   `Chess4D.Tablebase`, as a .NET solution under `/src`. Target netstandard2.1
   for libraries so Unity can consume them. The root `.gitignore` ignores
   `*.sln` and `*.csproj` project-wide for Unity's sake; it carries explicit
   negations for `/src` so the solution is tracked. Confirm with `git status`
   that the new files are not ignored before committing.
4. `docs/PROGRESS.md` already exists with a section per stage. Fill in its
   "Environment" section.
5. **How Unity consumes the libraries (decided):** the Core and Engine source
   lives in a local UPM package under `Packages/com.chess4d.core` (and
   `Packages/com.chess4d.engine`) with an assembly definition each. The .NET
   projects under `/src` do not contain source; they include the same files by
   glob (`<Compile Include="../../Packages/com.chess4d.core/Runtime/**/*.cs" />`).
   One source tree, compiled twice, no copy step, no drift, and in-editor
   debugging works. The asmdef must reference no Unity assemblies, which is how
   ground rule 2 is enforced mechanically. Do not build DLLs into
   `Assets/Plugins`.

**Exit gate:** `dotnet build` succeeds on the solution, an empty test in
`Chess4D.Core.Tests` runs green, and Unity compiles the package without
errors. The existing 2D Unity game still launches and plays after the upgrade.
Commit.

---

### Stage 1 — Dimension-generic rules core, validated at n=2

This is the most important stage. Everything downstream depends on it being
exactly right, and it is verifiable against known published numbers.

Build in `Chess4D.Core`:

- A `Coord` struct holding up to 6 axis values plus the active dimension count.
  Value type, no allocation.
- A `Board` holding a mailbox array of size `side^dimensions`, plus a piece list.
  Make/unmake on a single board instance. Never copy the board to search.
- Direction generators computed from the dimension count, not hardcoded:
  rook, bishop, queen, king, knight, pawn-forward, pawn-capture.
- Move generation, including castling, en passant, promotion, double-step.
- Legality filtering via make, attack test, unmake.
- Check, checkmate and stalemate detection.
- **Zobrist hashing in Core**, maintained incrementally through make/unmake.
  Threefold repetition needs it here, and the engine and tablebase consume it
  later. Keys: `side^dimensions` cells x 6 piece types x 2 colours, plus side
  to move, castling rights and the en passant cell. Generate them once from a
  fixed seed so runs are reproducible. At n=4 that is 49,152 cell keys.
- Fifty-move rule and threefold repetition, behind flags defaulting to off.
- The starting-position generator: back rank plus programmatic pawn shell.

**Exit gate, and it is strict.** Configure the core for `dimensions = 2`,
`side = 8`. It must then be standard chess. Run perft from the standard opening
position and match these published values exactly:

```
depth 1:            20
depth 2:           400
depth 3:         8,902
depth 4:       197,281
depth 5:     4,865,609
```

Also assert:
- the n=2 shell generator produces exactly 8 pawns on rank 2
- direction counts at n=2 are rook 4, bishop 4, queen 8, king 8, knight 8
- castling, en passant and promotion all appear in the n=2 move list

If perft does not match, the move generator is wrong. Do not proceed. Do not
adjust the expected numbers. They are correct.

Also assert that a make/unmake round trip restores the Zobrist key exactly,
over every move in a perft-3 traversal.

Commit only when all five depths match.

Leave the old 2D code under `Assets/Scripts/ChessGame` in place. Deleting it
now would leave the Unity scene with missing-script errors through Stage 2,
with nothing to replace it. It is removed at the start of Stage 3.

---

### Stage 2 — Four dimensions

Switch the core to `dimensions = 4`.

- Assert direction counts: rook 8, bishop 24, queen 32, king 32, knight 48.
- Assert the starting position has 144 pieces per side, 136 of them pawns.
- **First-check finding.** In the starting position no slider has any move at
  all (see the depth-1 derivation below), so asserting "no slider gives check"
  would pass trivially and measure nothing. Instead, over at least 1,000
  random legal games, record the ply at which a check first becomes
  *available* to the side to move, split by the checking piece type. Report
  the minimum, median and distribution in `PROGRESS.md` under Findings. The
  spec originally predicted no check before ply 7; the Stage 2 data showed a
  check is possible at ply 3 (recorded in `PROGRESS.md` Findings). It is a
  research finding and not a bug.
- Generate perft numbers at 4D depths 1, 2 and 3 and record them in
  `docs/PERFT_4D.md`. Nobody has published these. They become the reference for
  anyone reimplementing this.
- Property test over random legal games: a Bishop's coordinate sum parity must
  never change. If it does, the bishop generator is wrong.

- **Depth-1 perft must equal 196.** This has been derived by hand and it is the
  only external correctness check available at 4D. The derivation:

  - **Back rank, 8 pieces: 0 moves.** Every cell at Chebyshev distance 1 from the
    back rank is occupied by the shell, so the King, Queen, Rooks and Bishops are
    completely blocked on their first step.
  - **Knights: 26 each, 52 total.** Take the Knight at (1,0,3,3). Offsets with a
    negative y component leave the board. Of the rest: 6 land at y=2 with the
    2-step along y; 4 land at y=1 with a 2-step along z or w, escaping the shell's
    z,w range of {2,3,4}; 16 stay at y=0 with a 2-step along z or w for the same
    reason. 6 + 4 + 16 = 26. The Knight at (6,0,3,3) is the mirror image.
  - **Pawns at y=1, 72 of them: 144 moves.** Each has a single step to the empty
    y=2 layer and a double step to y=3, both unobstructed.
  - **Pawns at y=0, 64 of them: 0 moves.** Each is blocked by the y=1 pawn
    directly in front of it.
  - **Captures: 0.** No enemy piece is within reach on move one.

  Total: 0 + 52 + 144 + 0 = **196**.

  Re-derive this independently before running the generator, and only then
  compare. If your generator does not produce 196, it is wrong. Do not change the
  expected value. If you become convinced 196 is itself wrong, stop and write
  your reasoning into `PROGRESS.md` under Blocked rather than proceeding.

**Exit gate:** all assertions pass, 4D perft numbers recorded, parity property
holds over at least 10,000 random legal moves. Commit.

---

### Stage 3 — Rendering and perspective

Unity work begins. The core is now frozen except for bug fixes.

**First step of this stage:** delete the old 2D rules code under
`Assets/Scripts/ChessGame` and the `BoardLayout` ScriptableObject. Keep the
input system, tweeners, materials, prefabs, models and the scene. Wire the
scene to Core through a thin adapter. The game is unplayable until this stage's
gate passes; that is expected.

The player views **three of the four axes at a time**. There are C(4,3) = 4
perspectives: (x,y,z), (x,y,w), (x,z,w), (y,z,w).

Two distinct operations, and they must not be conflated in the UI:

- **Rotate** — change which three axes are on screen, for example (x,y,z) to
  (w,y,z). Implement as a continuous 90 degree rotation in the plane spanned by
  the outgoing and incoming axes, applying the 4D rotation to every piece
  coordinate and projecting to 3D each frame. This gives object permanence and it
  is the project's distinguishing feature. Do not implement it as a cut or a fade.

  **The rotation is interactive and scrubbable, not a fixed animation.** The
  player arms a rotation by selecting a target perspective, then drags to drive
  phi continuously from 0 to 90 degrees. They can stop at any intermediate angle,
  hold there, reverse, and inspect the partially rotated 4D state. Releasing past
  45 degrees snaps forward to the target; below 45 it springs back to the origin.
  A button press or keyboard shortcut performs the same rotation as a timed sweep
  of about 0.6s for players who do not want to drag.

  Bind scrubbing to a modifier plus drag, or to a dedicated slider. Never bind it
  to plain left-drag, which is camera orbit.

  At intermediate angles the projected positions are not on lattice points. Cells
  and pieces will overlap and pass through one another. That is correct, it is
  the visually interesting part, and you must not snap or quantise the
  intermediate frames to hide it. Disable cell picking entirely whenever phi is
  neither 0 nor 90, since there is nothing well-defined to click.

  **The rotation must be mathematically exact, not merely plausible.** Swapping
  axis `a` out of view for axis `b` is a rotation by phi in the a-b plane:

  ```
  a' = a*cos(phi) - b*sin(phi)
  b' = a*sin(phi) + b*cos(phi)
  ```

  Animate phi from 0 to 90 degrees. At 90 degrees this maps a to b and b to -a,
  the axis swap up to a reflection. Choose the sign of phi for the handedness you
  want and keep it identical across all four buttons.

  **Rotate about the board centre, not the origin.** Coordinates run 0 to 7,
  so subtract 3.5 from each axis before rotating and add it back after.
  Otherwise one axis lands on negative values. At phi = 90 the cell value `v`
  on the outgoing axis therefore maps to `7 - v` on the incoming axis, and
  that reflected value is the "swapped counterpart" the test must expect.

  **Endpoints are exact integer permutations, not trig.** `cos(90°)` in double
  precision is about 6e-17, not zero. The utility must special-case phi = 0 and
  phi = 90 and return the integer permutation directly, so pieces land on
  lattice points with no drift and cell picking can be re-enabled by exact
  comparison. Intermediate angles use float and are never snapped.

  Put this in a testable math utility inside Core, not in a MonoBehaviour, and
  unit test that at phi = 90 every one of the 4096 board coordinates maps
  exactly onto its reflected swapped counterpart, and that phi = 0 is the
  identity, both with integer equality. Also test that the float path at
  phi = 89.999 is within 1e-3 of the integer endpoint. A subtly wrong rotation
  looks perfectly fine on screen and corrupts everything downstream.

  **What the hidden layers look like during rotation (decided).** At phi = 0
  the hidden axis has zero projected extent, so all eight layers land on
  exactly the same screen positions; showing them all is geometrically
  meaningless, which is why the corner strip exists. So:

  - Piece opacity is `max(base, sin(phi))`, where `base` is 1 if the piece lies
    in the current layer of the *nearer* endpoint and 0 otherwise.
  - At phi = 0 the current layer is solid and the other seven are fully
    transparent. They fade in continuously from zero, so there is no pop.
  - At phi = 45 everything is visible at about 0.7 opacity and the volume has
    opened out. This is the striking part and the whole point of the feature.
  - At phi = 90 the layer that has become current is solid and the rest fade
    back out.
  - All 288 pieces are therefore always in the render list; only their opacity
    changes. The "visible volume" rule below applies to cell geometry, not
    pieces.

  The projected volume grows from 8 cells across to about 9.9 at phi = 45,
  since both rotating axes sit at cos 45. Frame the camera for 10 cells so
  there is no zoom lurch mid-rotation.
- **Page** — stay in the same three axes, step the hidden axis value up or down.
  Instant, no animation.
- **Camera orbit** — orbit and zoom the camera around the visible volume with
  left-drag and scroll, as in any 3D chess game. This changes the viewing angle,
  not which axes are in view. It is a third, entirely separate control. Never
  conflate it with Rotate. Confusing the two makes the interface incomprehensible
  and it is the single easiest way to ruin this stage.

UI requirements:

- Four perspective buttons, one per axis triple. Current one clearly active.
- A picture-in-picture panel in a corner showing the **hidden axis strip**: 8
  cells, one per value of the hidden axis, for the currently selected or hovered
  board cell. Occupied cells coloured by team, current layer highlighted.
  Clicking a cell in the strip pages the main view to that layer.
- A density bar beside the strip showing how populated each hidden layer is.
- **In this stage the strip shows occupancy only, never threats.** Threat display
  needs the attack map service, which is Stage 5. Do not attempt it here and do
  not stub it with a placeholder that lies.
- Build the PiP as a **list of hidden-axis widgets**, not one hardcoded widget.
  At 6 dimensions there will be three hidden axes and this must not need a rewrite.
- Render cell geometry for the visible 8x8x8 volume only, 512 cells. Use GPU
  instancing, not one GameObject per cell. Pieces follow the opacity rule
  above and are not culled by layer.

**Occlusion.** The visible volume is 8x8x8 = 512 cells and from outside you see
only its shell; the interior 6x6x6 = 216 cells sit hidden behind it. Overall
occupancy is about 7 percent so this is workable, but it must be handled
deliberately:

- Render only occupied cells as solid. Empty cells get a faint wireframe or
  nothing. Never render 512 opaque cubes; the board becomes unreadable.
- Provide a layer isolation control that hides everything above or below the
  currently selected layer along one visible axis.
- Fade pieces by distance from the camera so foreground and background separate.

**Critical input constraint:** the player cannot click a cell that is not in the
current view. Every action that needs a target cell must also accept typed
coordinates. Do not build any flow that depends on clicking alone.

**Exit gate:** you can rotate through all four perspectives with animation, page
through all 8 layers, and the strip correctly shows what occupies the hidden
axis. Verified by screenshot. Commit.

---

### Stage 4 — Game management and the position editor

- Turn order, move input by click or typed coordinate, legal move highlighting.
- Check, checkmate, stalemate detection surfaced in the UI.
- Promotion dialog.
- Move history in a readable 4D notation. Define one and document it in
  `docs/NOTATION.md`.
- Undo and redo.
- Rotating, paging, orbiting the camera and isolating layers are **not moves**.
  They must never enter move history, must not be reachable by move undo, and
  must not change whose turn it is.
- **Endgame setup mode**, reachable by a button from the main game. Place and
  remove arbitrary pieces at arbitrary coordinates, set side to move, clear the
  board, then play on from that position. This must accept typed coordinates,
  because most cells are not visible.
- Save and load positions to a text format.
- Notation decided 2026-09-06 (`docs/NOTATION.md`): moves are written in the
  compact digit form `Q3033-0333+`; typed input accepts that or tuples; save
  files use tuples.

**Exit gate:** a full game is playable start to finish. A K+Q vs K position can
be set up in the editor and played out. Commit.

---

### Stage 5 — Engine

Two separate components. Do not merge them.

**AttackMapService.** Move generation only, no search. Answers: what attacks this
cell, is this piece attacked, is the King in check, what are all legal moves.
Runs every turn for both sides. Must complete in well under a frame. This is what
makes threats along hidden axes visible to the player and it is a required part
of the UI, not an optional analysis tool.

**SearchEngine.** Single-threaded alpha-beta with:
- iterative deepening
- transposition table keyed by the Core Zobrist hash from Stage 1
- move ordering: captures first by MVV-LVA, then killers, then history
- quiescence search on captures
- evaluation: material plus mobility. Piece values are unknown in 4D — start
  with 2D values as a placeholder and mark them clearly as provisional in the code.

Expect a branching factor in the thousands and a practical depth of 4 to 6.
That is acceptable. With 288 pieces on the board expect a practical depth of 2
to 4, not more. Do not attempt to make it strong, and do not multithread it to
compensate.

Both players may have an engine attached, independently configurable as human or
engine, so the game supports human vs human, human vs engine and engine vs engine.

**Move animation (added by the owner after Stage 4; part of this stage's gate).**
Without it an engine move between two hidden layers changes nothing on screen
and the game feels broken. Three cases, decided by which of the move's cells
are in the visible volume:

- **Both visible:** animate the piece from cell to cell.
- **One visible:** animate the half you can see. A piece leaving the volume
  slides from its cell toward the volume boundary and fades out; a piece
  arriving fades in at the boundary and slides into its cell.
- **Neither visible:** nothing moves on the board. Flash the history line and
  mark which hidden layer changed in the hidden-axis widgets (the from layer
  and the to layer of the hidden axis).

Add a **jump-to-last-move** button that pages the view so the last move's
destination is visible. The view must never move on its own; rotation, paging
and orbit remain the player's actions only.

**Exit gate:** engine vs engine self-play completes 100 games without a crash,
an illegal move, or a state desync, plus a fuzz test playing 10,000 random legal
moves across random positions with no make/unmake mismatch. If 100 games takes
more than a few hours, reduce to 20 and log the number actually run. The three
animation cases and the jump-to-last-move button are demonstrated by
screenshot. The hidden-axis strip shows threats from the attack map. Commit.

---

### Stage 6 — Tablebase generator

Separate console executable. Never shipped in the game build.

- Three-piece endings on the 4D board: K+Q vs K, K+R vs K, K+B vs K, K+N vs K.
- **Symmetry canonicalisation is mandatory.** The hypercube symmetry group at
  4 dimensions has order 2^4 x 4! = 384. Without reduction the K+Q vs K table is
  137 billion positions. With it, roughly 358 million, about 358 MB. Implement
  canonicalisation first and unit test it before generating anything.
- Retrograde analysis, depth-to-mate. Fifty-move rule off.
- **Two bytes per position for depth-to-mate.** Nobody knows the longest
  forced mate in 4D and one byte caps at 255 plies. Assert on overflow anyway.
- **Checkpoint and resume from the start.** Even parallelised, a pass over
  358 million positions with 4D move generation runs for hours. Write the
  table state to disk at the end of every retrograde iteration and make the
  tool resume from the last complete iteration on restart. Do not design this
  in after the first crash.
- **Parallelise the retrograde passes.** The single-threaded rule in section 1
  does not apply here; see its scope note.
- Output a compact indexed binary plus a human-readable summary.

**Research questions this answers, and they are the point of the whole project:**
- Can King and Queen force mate against a lone King in 4D?
- If so, what is the longest forced mate?
- What is the minimum material that forces mate?

Record the answers in `docs/FINDINGS.md` regardless of what they are. A negative
result is a result.

**Exit gate:** at least one three-piece table generated, verified by spot-checking
positions against the engine's own search, findings written up. Commit.

---

## 4. Deferred, do not build yet

- Six dimensions. The core carries a dimension count from Stage 1, so this is a
  configuration change plus UI work, not a rewrite. Do not attempt it until
  Stage 6 is complete.
- WebGL build. Follow the single-threaded and no-file-IO rules and it stays cheap.
- Four-piece tablebases. Roughly 1.5 TB at 4D after symmetry reduction, at one
  byte per position including side to move, and more at two bytes. Needs
  cloud compute.
- Any human user study.

---

## 5. Things that will tempt you, and must not happen

- Adding a rule "to make the game work better". The rules in section 2 are final.
- Putting game logic in the Unity layer. It goes in Core.
- Hardcoding 4 as the dimension count. It is always a parameter.
- Hardcoding the 136 pawn positions. They are generated.
- Copying the board to search instead of make/unmake.
- Multithreading the search or the attack map. (The tablebase tool is the one
  place threads are welcome.)
- Adjusting the perft expectations in Stage 1 to match your output.
- Starting a later stage because an earlier gate "nearly" passes.

---

## 6. Open items

None of these block any stage. Ask before deciding.

- Whether the Queen should instead use all 80 directions in {-1,0,1}^4 rather
  than the 32 of rook-union-bishop. Currently 32. This would change the game
  substantially.
- Whether the Bishop should also move on 3-axis and 4-axis diagonals, which
  would take it from 24 directions to 72 (24 + 32 + 16; equivalently the
  80-direction Queen minus the 8 Rook directions). Currently 24.
- ~~A 4D move notation. Propose one in Stage 4 and get it approved.~~ Decided
  2026-09-06: compact digit form `Q3033-0333+` for moves, history and typed
  input; tuples stay in save files. See `docs/NOTATION.md`.
