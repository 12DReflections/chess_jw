# Progress Log

Update this file at the end of every session. Append, never overwrite.
Read `SPEC.md` in full before starting any work.

Format per entry: date, stage, what was done, gate result, what is next.

---

## Environment

Fill in during Stage 0. Update whenever a tool version changes.

| Item | Value |
|---|---|
| Unity editor (target, per owner's decision) | Unity 6.3 LTS, 6000.3.23f1 (owner chose Unity 6 for the WebGL target) |
| Unity editor (actually in use) | 6000.3.23f1, upgraded from 2020.3.19f1 on 2026-09-05; WebGL module installed |
| .NET SDK | 10.0.400 at `~/.dotnet` (unpacked from the Homebrew cask pkg; the cask's privileged install step never ran). `DOTNET_ROOT` and `PATH` set in `~/.zshrc`. Solution file is `src/Chess4D.slnx` (the .NET 10 default format). Tests use NUnit 4 on net10.0. |
| Machine | macOS 24.3 (Darwin), Apple Silicon, Homebrew present |
| Library packaging | Local UPM packages `Packages/com.chess4d.core` and `Packages/com.chess4d.engine`, source globbed into `/src` .NET projects. See SPEC.md Stage 0 item 5. |

---

## Stage 0 — Project setup
Status: COMPLETE (2026-09-05)

Done:
- Branch `4d-rewrite` created from `spec-review` (which is `main` plus the reviewed docs).
- Unity project upgraded to 6000.3.23f1 in batch mode. Manifest cleaned first:
  removed `com.unity.textmeshpro` (merged into ugui 2.0.0), `com.unity.ide.vscode`
  (deprecated) and `com.unity.collab-proxy`; Unity then added `com.unity.ai.navigation`
  and `com.unity.multiplayer.center` as Unity 6 defaults. Script compilation: 0 errors,
  0 warnings, including DOTween under `Assets/Plugins`.
- Embedded UPM packages `Packages/com.chess4d.core` and `Packages/com.chess4d.engine`,
  each with an asmdef that has `noEngineReferences: true` (ground rule 2, enforced
  by the compiler). Unity resolves both and compiles them: 0 errors.
- `/src/Chess4D.slnx` with `Chess4D.Core` (netstandard2.1), `Chess4D.Engine`
  (netstandard2.1), `Chess4D.Core.Tests` (net10.0, NUnit), `Chess4D.Tablebase`
  (net10.0 console). Core and Engine contain no source of their own; they glob the
  package `Runtime/**/*.cs`. `src/Directory.Build.props` pins C# 9, nullable off,
  warnings as errors, to match what Unity 6 compiles.
- `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 2 passed (link check, and a
  reflection check that Core references no `UnityEngine*` assembly).
- `.gitignore` negations verified: the slnx and csproj files are tracked.

Gate result (2026-09-05): `dotnet build` PASS (0 warnings, 0 errors). `dotnet test`
PASS (2/2). Unity 6000.3.23f1 compiles both embedded packages PASS (0 errors).
2D game launches and plays in the Unity 6.3 editor PASS: Main scene entered Play
mode, 32 pieces instantiated, console clean, e2-e4 selected with legal-square
highlighting and executed. Owner confirms the existing game is complete and both
sides play. Stage 0 gate passed. Next: Stage 1 in a fresh session.

## Stage 1 — Rules core, validated at n=2
Status: COMPLETE (2026-09-05)
Gate: perft from the standard opening must equal 20 / 400 / 8902 / 197281 / 4865609

Built in `Packages/com.chess4d.core/Runtime` (shared source, compiled by both .NET and Unity):

- `Coord`: readonly struct, up to 6 axes plus dimension count, `(x,y,z,w)` text form.
- `BoardGeometry`: side^dimensions mailbox indexing, coordinate table, and direction
  sets generated from the dimension count (rook, bishop, queen/king, knight, pawn
  forward, pawn capture, and the reversed capture set for attack queries).
- `Board`: byte mailbox (type, colour, moved bit), per-colour piece lists, king cell
  tracking, make/unmake on one instance, ray-walk attack test, pseudo-legal and legal
  move generation (castling, en passant, promotion, double step), check / checkmate /
  stalemate, castling rights derived from the moved bits of king and corner rooks,
  incremental Zobrist hash, fifty-move and threefold repetition behind `GameRules`
  flags defaulting to off, and an editor API (`PlacePiece`, `RemovePiece`, `Clear`,
  `SetSideToMove`, `SetEnPassantCell`).
- `Zobrist`: cells x 12 piece keys, side, 16 castling combinations, en passant per
  cell, SplitMix64 from a fixed seed.
- `StartPosition`: back rank plus the programmatic pawn shell (Chebyshev distance 1
  from any back-rank cell, minus the back rank). At n=2 it produces the standard
  second rank, asserted.
- `Perft` with `Divide`; `Fen` (2D only) for loading the published test positions.

Gate result (2026-09-05), `dotnet test`, Debug build, Apple Silicon:

| Depth | Expected | Got | Time |
|---|---|---|---|
| 1 | 20 | 20 | <1 ms |
| 2 | 400 | 400 | <1 ms |
| 3 | 8,902 | 8,902 | 6 ms |
| 4 | 197,281 | 197,281 | 133 ms |
| 5 | 4,865,609 | 4,865,609 | 3,270 ms |

All five match on the first run with no adjustment. Additional published positions
also match: Kiwipete to depth 4 (4,085,603), Position 3 to depth 5 (674,624),
Position 4 to depth 4 (422,333), Position 5 to depth 3 (62,379), Position 6 to
depth 3 (89,890). Also asserted: direction counts at n=2 (4/4/8/8/8), n=4
(8/24/32/32/48) and n=6 (12/60/72/72/120); castling, en passant and promotion
present in the n=2 move list and refused when illegal; pinned pieces; fool's mate
and a stalemate detected; draw flags off by default and working when on;
incremental hash equals a full recompute at every node of a perft-3 walk and every
unmake restores the FEN exactly. 52 tests, all green.

Old 2D code under `Assets/Scripts/ChessGame` left in place per the revised spec;
it goes at the start of Stage 3.

### Benchmarks (keep current; SPEC.md section 2 sets the budget)

Open benchmark position (`BenchmarkTests.OpenPosition`), White to move, all pieces
marked moved except the two rooks and the two unmoved pawns:
White K (4,0,3,3), Q (4,4,3,3), R (0,0,3,3), B (2,2,1,5), N (6,3,3,3), P (4,3,3,3), P (1,1,2,2).
Black K (4,7,3,3), Q (3,4,2,2), R (7,7,3,3), N (2,5,3,3), B (5,5,4,4), P (4,4,3,2), P (6,6,4,4).

| Date | Position | Pseudo-legal gen | Legal gen | Attack query, one cell | Budget met |
|---|---|---|---|---|---|
| 2026-09-05 | starting (196 moves) | 0.026 ms | 0.361 ms | 0.0014 ms | yes (5 / 50 / 1 ms) |
| 2026-09-05 | open (252 moves) | 0.008 ms | 0.708 ms | 0.0026 ms | yes |

Note: the 4D starting position already produces 196 pseudo-legal and 196 legal
moves, which is the Stage 2 depth-1 figure. Not claimed as the Stage 2 gate; it is
asserted there.

## Stage 2 — Four dimensions
Status: COMPLETE (2026-09-05)

No core changes were needed: the Stage 1 code is dimension-generic and the
switch is `new Board(4, 8)`. Tests in `Stage2FourDimensionsTests`:

- Direction counts at n=4: rook 8, bishop 24, queen 32, king 32, knight 48, pawn
  capture 6. PASS.
- Starting position: 144 pieces per side, 136 pawns, 72 at y=1 and 64 at y=0,
  King at x=4 and Queen at x=3 on both back ranks. PASS.
- Every on-board Chebyshev-1 neighbour of every back-rank cell, both sides, is a
  friendly piece (the spec's "verify by hand" item, now a test). PASS.
- Depth-1 perft = 196, and the breakdown matches the hand derivation category by
  category: 0 back-rank moves, 52 knight, 144 pawn from y=1, 0 pawn from y=0,
  0 captures. PASS.
- 4D perft depths 1 to 3 recorded in `docs/PERFT_4D.md` and asserted: 196 /
  38,416 / 7,584,070. Depth 2 is exactly 196 squared, as it must be since no
  first moves interact. PASS.
- Bishop coordinate-sum parity preserved over 10,000 random legal moves (1,637 of
  them bishop moves). PASS.
- First-check finding over 1,000 random games (below). PASS.

Gate result: PASS. 62 tests green (one discovery test marked Explicit).

## Stage 3 — Rendering and perspective
Status: COMPLETE (2026-09-05)

First step done: `Assets/Scripts/ChessGame`, `Assets/Data` (the BoardLayout asset),
`Assets/Scripts/Enums` and `BoardInputHandler` deleted. Input receivers, tweeners,
materials, prefabs, models and `Main.unity` kept; the scene builder strips the
five dangling script components from `Main.unity` and the six piece prefabs.

Core: `AxisView` (in the package, no Unity) is a signed permutation of axes into
view slots, three visible and the rest hidden. `Project` applies the a-b plane
rotation about (side-1)/2 and returns exact integer permutations at phi = 0 and
phi = 90 without trigonometry; `AfterQuarterTurn` gives the exact next view, with
the incoming axis reflected (a' = -b, b' = a). Seven tests: identity at 0, all
4096 cells map exactly to their reflected swapped counterpart at 90 for every
visible slot, the float path is within 1e-3 of the endpoints at 0.001 and
89.999, mid-rotation preserves distance from the centre, four quarter turns
return to the start, every reachable state is a signed permutation and all four
perspectives are reachable, six dimensions has three hidden slots.

Unity (`Assets/Scripts/Chess4D`, default assembly, references the Core package):

- `ViewState`: view, per-axis pages, arm / scrub / release (snap past 45, spring
  back below), 0.6 s smoothstep timed sweep, commit to the exact view, selection,
  hover, layer isolation. Picking is disabled whenever phi is not 0.
- `BoardView`: all 288 pieces as pooled GameObjects with a shared Fade material
  and per-renderer colour; opacity = max(base, sin phi) with base = 1 in the
  current layer of the nearer endpoint, times a distance fade; visible 8x8x8
  lattice drawn with `Graphics.RenderMeshInstanced`, occupied cells as
  translucent team-coloured cubes and empty cells as faint markers; ray-vs-cell
  picking on the lattice.
- `OrbitCamera`: left-drag orbit, scroll zoom, framed for 10 cells at 45 degrees.
- `HudUi`: four perspective buttons (click sweeps, shift+click arms; keys 1-4),
  phi slider (drives phi when armed, release snaps or springs), shift+drag
  scrub, layer isolation (mode and screen axis, keys I and O), typed coordinate
  selection which pages the view to the cell, status text, and the
  picture-in-picture list of hidden-axis widgets: one per hidden slot, an
  8-cell occupancy strip through the selected or hovered cell coloured by team
  with the current page outlined and click-to-page, plus a density bar of piece
  counts per hidden layer. Occupancy only, no threats.
- `Chess4DGame`: bootstrap and input routing. `DemoRunner`: scripted walkthrough
  for `-chess4d-demo <dir>` that captures through the camera into a render
  texture so it does not depend on window focus.
- `Assets/Editor/Chess4DSceneBuilder`: builds `Assets/Scenes/Chess4D.unity` in
  code, creates the two Fade materials as assets so their shader variant ships,
  wires the twelve piece meshes into the bootstrap, cleans the old scene and
  prefabs, and builds the macOS player (`Chess4D/Build macOS Player`, or
  `-executeMethod Chess4DSceneBuilder.BuildAll`).

Gate result (2026-09-05): PASS, verified by screenshot from the standalone
player's scripted walkthrough: 24 captures covering a scrubbed rotation held at
20, 45 and 70 degrees then released past the snap point, a scrub released below
45 that sprang back, timed sweeps through the remaining perspectives with a
mid-sweep capture each, all eight pages of the hidden axis, typed selection,
isolation and orbit. Nine of them plus the walkthrough log are kept under
`docs/screenshots/stage3/`. Player log: no errors or exceptions.

Design observation for the owner, not a defect: because each quarter turn is
exact and reflects the incoming axis, the orientation of a perspective depends
on the path taken to it. After x y z -> x y w -> x z w -> y z w -> x y z the
view reads `-y -z -x | -w`: the same three axes, but permuted and reflected on
screen. This is what the spec's "mathematically exact, up to a reflection"
rotation implies. A camera-only "reorient" cannot undo a reflection. If a
canonical orientation per perspective is wanted, it needs a rule in the spec.

## Stage 4 — Game management and position editor
Status: COMPLETE (2026-09-05)

Core (`Packages/com.chess4d.core/Runtime`):

- `Game`: board plus move history, undo and redo (redo branch discarded on a
  new move), cached legal list, status, `LegalFrom(cell)`, numbered history
  lines with `+` and `#` suffixes. View operations never touch it.
- `Notation`: the long tuple form (`Q(3,0,3,3)-(0,3,3,3)+`, `x`, `=N`, ` e.p.`,
  `O-O` / `O-O-O`) and `TryParseMove`, which resolves flags such as double step
  and en passant against the legal list so the player never types them; a
  promotion without a suffix is a queen.
- `PositionText`: the version 1 position file format, tuples only, with
  `moved` flags, side to move, en passant cell and halfmove clock. Round-trip
  tested; rejects mismatched geometry and unknown lines.
- Eight tests, including a hand-constructed 4D checkmate (corner king smothered
  by its own pawns on all 15 neighbours, knight arrives from (4,2,0,0)) and a
  K+Q vs K position set up through the editor API and played.

`docs/NOTATION.md` written: section 1 documents the long form now in use,
section 2 proposes a compact form (`Qd1.dd-a4.dd+`) **awaiting the owner's
approval**, section 3 documents the position file format. Nothing emits or
parses the compact form.

Unity (`Assets/Scripts/Chess4D`):

- Play mode: click an own piece to select it; its legal destinations are drawn
  as green cubes (red for captures) in the visible volume, and the message
  says how many destinations lie in other layers. Click a highlighted cell to
  move. Typed input `(from) (to)[=X]` plays a move; a single coordinate selects
  (and pages the view to it). Turn order is enforced by the legal list.
- Status line: side to move, legal move count, CHECK, checkmate with the
  winner, stalemate, draw flags. Promotion dialog with four choices when a
  pawn reaches the last layer.
- Move history panel (last 12 lines, redo count), Undo / Redo buttons and Z / Y
  keys, New game.
- Setup mode from a button: brush per piece type plus erase, brush colour,
  side to move, clear board, standard start, click or typed coordinate to
  place, "Done, play" resets history and plays from the position. Pawns placed
  outside their shell rows are marked moved.
- Save / Load: writes `<persistentDataPath>/positions/<name>.txt` and copies
  the text to the clipboard; Load reads the file; Paste loads from the
  clipboard.
- Rotate, page, orbit and isolate never enter the history and never change the
  side to move (they live in `ViewState`, the game lives in `Game`).

Gate result (2026-09-05): PASS, verified by the scripted game walkthrough in the
standalone player (`-chess4d-demo-game`): the ply-3 check line played by
clicks with target highlighting, undo twice and redo twice restoring the check,
a typed illegal move refused while in check and a typed legal reply accepted,
the promotion dialog and a knight promotion, setup mode building K+Q vs K,
eight plies played from it, saved to file, reloaded with an identical hash, and
a position played to checkmate with the status line reporting the winner.
Screenshots and the log are under `docs/screenshots/stage4/`. Player log clean.

Not done, by design: piece movement is not animated (pieces re-appear at the
destination). The kept tweeners can be wired in later; nothing in the gate
asks for animation.

### Stage 4 correction, found in playtest (2026-09-08)

The board orientation was wrong. The advance axis y was mapped to screen
height, so White advanced up the screen and Black down it, and the camera
never turned: Black played backwards. Chess is played forwards and backwards
with your own pieces nearest you, and the board turns between players.

Fixed as follows:

- Projection: visible slot 1 is now depth (away from the camera) and slot 2
  is up; slot 0 stays screen X. With the identity view y is depth, z is up.
  Coordinates are untouched; only the slot-to-world mapping changed.
- Board orientation added as the third view behaviour beside perspective
  rotation and camera orbit. It is a yaw offset on the orbit camera and
  nothing else.
  1. The side to move sees its pieces near, with y advancing away: yaw 0 for
     White when +y is depth, 180 for Black; 90 and 270 when y sits on screen X
     after a perspective rotation, with reflected views handled by the sign.
  2. After a move completes, the board turns 180 degrees about the screen's
     vertical axis in 0.5 s, always the same way, starting only when the move
     animation has finished. Undo and redo turn it back the same way.
  3. Suppressed when the perspective does not include y, or when y is the
     vertical axis, since there is no forwards to face; the camera stays and
     the status line says why.
  4. Auto-flip toggle in the game panel, default on. Off keeps a fixed
     orientation for playing an engine as one colour or analysing.
  5. Never a move: it lives on the camera, not in the game, and is not in the
     history or reachable by undo.
- Verified by the game walkthrough: yaw 0 with White to move, a mid-flip frame
  at 69 degrees with the flip flagged as running, 180 after White's move with
  the history still at one move, the suppression note in the (x,z,w)
  perspective, and an unchanged orientation across Black's move with
  auto-flip off. Frames under `docs/screenshots/stage4-orientation/`.
  Playtest also confirmed the new depth mapping reads as a chess board seen
  from behind one's own pieces.

## Stage 5 — Engine
Status: COMPLETE (2026-09-06)

Engine package (`Packages/com.chess4d.engine/Runtime`, shared source, no Unity):

- `AttackMap` (AttackMapService): per-cell attacker counts for both colours,
  recomputed whenever the position hash changes; a colour's attack on its own
  piece counts as a defence. Answers attacked / defended / in check / attacker
  cells. Cross-checked against the Core ray walk on 24,000 random cells across
  60 random plies, and on a hidden-axis case (rook at w=0 attacking a knight at
  w=5 through the same x,y,z). Compute cost is one attack-pattern walk per
  piece; well under a frame.
- `Evaluation`: material plus mobility (2 centipawns per pseudo-legal move
  difference). Piece values are the 2D values and are marked PROVISIONAL in
  the code.
- `SearchEngine`: single-threaded negamax alpha-beta, iterative deepening,
  transposition table (2^18 entries) keyed by the Core Zobrist hash, ordering
  TT move then MVV-LVA captures then killers then history, quiescence on
  captures and promotions, mate scores by ply, time and node limits. Make and
  unmake on the board it is given; never copies per node. Tests: finds the
  smothered knight mate in one, takes a hanging queen, returns a legal move
  from the start position within the time limit (depth 2, about 27k nodes in
  0.8 s, Debug build).
- `SelfPlay`: engine versus engine with an independent replay board; after
  every move the played board, the replay board and a full hash recompute
  must agree, and the engine move must be in the legal list.

Unity (`Assets/Scripts/Chess4D`):

- Players: each side Human or Engine (buttons), engine time 0.3 / 1 / 3 s.
  The search runs on a worker thread over a one-off copy of the position
  (`Board.CopyFrom`) so the renderer never sees make/unmake churn; the search
  itself stays single-threaded, and a WebGL build falls back to a blocking
  call. A result is discarded if the position changed while thinking.
- Threats from the attack map: red marker on every attacked piece in the
  visible volume, corner marks in the hidden-axis strip (pale = attacked by
  White, red = attacked by Black), attacker counts in the cell status line.
  Toggle button. This is the strip's threat display the spec deferred from
  Stage 3.
- Move animation, the three cases decided by the owner: both cells visible,
  the piece slides cell to cell (0.35 s); only the origin visible, a ghost
  slides half the visible displacement and fades out; only the destination
  visible, the piece fades in sliding the last half of the way; neither
  visible, the history panel flashes and the from and to layers of the hidden
  axis pulse in the widget. Undo animates in reverse. A pure hidden-axis move
  with no visible displacement lifts instead.
- Jump-to-last-move button pages to the destination and selects it. Nothing
  moves the view on its own: New game, Load, engine moves and undo all leave
  the view where it is.

Gate result (2026-09-06):

- Fuzz: 10,000 random legal moves over 400 random positions, every make/unmake
  restored the position text and hash exactly. PASS.
- Self-play (`EngineTests.SelfPlayGate`, Release build, 150 ms per move, depth
  cap 4, 200-ply cap, 0 to 3 random opening plies, seed 1): 20 games, 3,861
  plies, 51,452,607 nodes, 581 s, maximum depth reached 4, **0 failures**: no
  crash, no illegal move, no state desync between the played board, the replay
  board and a full hash recompute after every move. One game ended in
  checkmate at ply 61; nineteen reached the ply cap with material still on
  the board. Run at 20 games, not 100, as the spec allows when 100 would take
  hours; 100 games at this budget would take about 50 minutes and can be run
  with `dotnet test -c Release --filter SelfPlayGate` after raising the count.
  PASS.
- Animation cases and jump button demonstrated by the scripted engine
  walkthrough (`-chess4d-demo-engine`), with live counters logged at each
  capture: both-visible one piece animation at 45 percent, origin-visible one
  ghost, neither zero and zero, destination-visible one fade-in at 35 percent.
  Engine played White at depth 2 in 0.30 s, then engine versus engine for six
  plies. Screenshots and log under `docs/screenshots/stage5/`. PASS.
- 80 .NET tests green.

Findings for the record: at 288 pieces the Debug-build search reaches depth 2
in about 0.3 s and depth 3 needs several seconds; the spec's expectation of a
practical depth of 2 to 4 holds. The provisional 2D piece values are untested
against 4D reality.

## Stage 6 — Tablebase generator
Status: COMPLETE (2026-09-08)

`src/Chess4D.Tablebase`, console only, never in the game build:

- `Symmetry`: the hyperoctahedral group generated as every axis permutation
  times every reflection set (384 elements at n=4, 8 at n=2, dimension
  generic). Canonical form: white king into the fundamental domain
  (coordinates non-decreasing, at most side/2-1: 35 cells at n=4), then the
  king's stabiliser minimises the white piece's cell, then the black king's.
  Index = (king class, piece representative, black king cell, side to move).
  The black king is left unreduced under the pair stabiliser for a simple
  index; slots that are not their own canonical index are marked illegal in
  the init pass (62,388,368 of 428,933,120 at n=4). Unit tested before any
  generation: group order and bijectivity, invariance of the index under every
  transform, decode round trip and orbit membership.
- `ThreePiece`: allocation-free attack tests for K+X vs K, checked against the
  Core ray walk on 12,000 random positions for all four piece types.
- `Generator`: parallel init pass (legality, mates, stalemates, per-position
  count of distinct canonical black successors, escape marking when the piece
  can be captured), then retrograde passes by predecessor generation: a lost
  black-to-move position marks every legal white predecessor won; a won
  white-to-move position decrements the counters of its distinct canonical
  black predecessors, and a counter reaching zero is a loss. Passes are
  parallel over index ranges; writes within a pass are idempotent and counters
  use atomic decrements. Two-byte distance to mate with overflow assert.
  Checkpoint after every pass, resume on restart. Binary table plus summary.
- `Verify`: one-ply consistency sampling with the full Core rules, and engine
  spot checks. `Program`: `generate`, `list`, `verify`.

Validation at two dimensions (the reason to make it dimension generic): the
2D tables reproduce the published results exactly, K+Q vs K longest mate 19
plies white to move (10 moves) and 20 black to move, K+R vs K 31 and 32 (16
moves), K+B and K+N no wins, all white-to-move K+Q positions won; 1,500
consistency samples per table with zero failures; the engine agrees on every
sampled short mate. One bug was found and fixed by this validation: duplicate
index slots were being treated as positions.

Gate result (2026-09-08): PASS. All four 4D three-piece tables generated
(about 30 s each on 12 threads, 1.7 GB), 5,000 consistency samples per table
with zero failures, the unique K+Q mate confirmed by the engine's search, and
the findings written to `docs/FINDINGS.md`: **K+Q cannot force mate in 4D**
(one mate position class, 18 mate-in-one positions, nothing longer), and
**K+R, K+B and K+N have no checkmate positions at all**. Minimum mating
material is therefore at least two pieces beyond the king, which is the
deferred four-piece question. Table summaries under `docs/tablebase/`; the
858 MB binaries under `Builds/tablebase/` are not committed. Reproducible via
the explicit test `TablebaseFourDimensionsGate`.

Note on the spec's estimate: 358 million positions assumed no symmetric
positions; the exact orbit count is higher, and with the black king
unreduced the table is 429 million entries (818 MB at two bytes). Memory per
run is about 1.7 GB with the counters.

---

## Findings

Research results the spec asks for. Record them here as they land, then copy
the tablebase results to `docs/FINDINGS.md` in Stage 6.

- **4D perft depths 1 to 3** (Stage 2, 2026-09-05): 196 / 38,416 / 7,584,070.
  Depth 3 took 14.7 s single-threaded in a Debug build. Full write-up in
  `docs/PERFT_4D.md`.
- **Early check is possible, and the spec's claim was wrong** (Stage 2,
  2026-09-05). The spec said early check is impossible by construction because
  no piece reaches the enemy camp in under four moves. Reaching the camp is not
  required: once one shell pawn vacates, a slider checks from a distance along
  the opened line. Concrete line, asserted in `CheckIsPossibleAtPlyThree`:
  `P(2,1,3,3)-(2,3,3,3)`, `P(3,6,3,3)-(3,4,3,3)`, `Q(3,0,3,3)-(0,3,3,3)+`. The
  queen leaves along the (x,y) diagonal the first pawn opened and sees the King
  at (4,7,3,3) through the cell the Black pawn vacated. Ply 3 is the theoretical
  minimum, since no first move can give check. Section 2 of SPEC.md carries a
  correction note; no rule changed.
- **First available check over 1,000 random games** (seed 4, cap 300 plies): a
  check became available in all 1,000 games. Earliest ply 5, median 41, latest
  181. Piece types able to deliver the first available check (a game can count
  more than one): Queen 413, Bishop 318, Knight 249, Pawn 40, Rook 2. Random
  play never sampled the ply-3 line, which needs two specific pawn moves.
  Rooks almost never give the first check because their lines run along the
  packed shell axes. Histogram is in the test output.
- **Bishop parity property** held over 10,000 random legal moves, 1,637 bishop
  moves checked, zero violations.
- **Engine self-play**: games run, crashes, illegal moves, desyncs (Stage 5): _pending._
- **Tablebase**: can K+Q force mate, longest forced mate, minimum mating material (Stage 6): _pending._

---

## Blocked

Nothing yet. If a rule in SPEC.md section 2 appears wrong or impossible,
write it here and stop. Do not improvise a replacement.

---

## Session log

(append entries below)

### 2026-09-05 — Spec review (pre-Stage 0)

Reviewed SPEC.md and PROGRESS.md against the repo and this machine before any
code work. Corrections adjudicated by the owner and applied on branch
`spec-review`:

- Ground rule 5 rewritten: the perft table is the rules reference, the 2D code
  is kept only for Unity patterns and is deleted at the start of Stage 3.
- Single-threaded rule scoped to `SearchEngine` and `AttackMapService`; the
  tablebase tool may parallelise.
- Zobrist hashing moved into Core at Stage 1 so threefold repetition can be
  implemented there.
- Rotation utility: rotate about 3.5, expect `7 - v` on the swapped axis,
  exact integer permutation at phi = 0 and 90.
- Rendering during rotation decided: opacity `max(base, sin(phi))`, camera
  framed for 10 cells.
- Vacuous "no slider gives check" assertion replaced with a first-check-ply
  measurement over random games.
- Performance budget now covers an open mid-game position as well as the start.
- Tablebase: two-byte depth-to-mate, checkpoint and resume, four-piece estimate
  corrected to about 1.5 TB.
- Bishop all-diagonals count corrected from 88 to 72.
- Stage 0 packaging decided: local UPM packages sharing source with `/src`.
- `.gitignore` given negations so `/src/*.sln` and `/src/**/*.csproj` are tracked.

Gate result: not applicable.

### 2026-09-05 — Stage 0

Owner installed Unity 6.3 (6000.3.23f1, WebGL module). Homebrew dotnet-sdk cask had
only downloaded its pkg, so the SDK was unpacked from that pkg into `~/.dotnet`.
Branch `4d-rewrite` created from `spec-review`. Project upgraded in batch mode,
packages and `/src` solution created, all four gate items pass. See the Stage 0
section above for detail. Commits: 70ae3e8 (scaffolding), plus this closing commit.
Next: Stage 1, dimension-generic rules core validated by perft at n=2.

### 2026-09-05 — Stage 1

Owner directed continuing past Stage 0 in the same session. Rules core written into
the Core package; perft gate passed on the first run at all five depths plus five
published reference positions. Benchmarks recorded above. Gate result: PASS.
Next: Stage 2, switch to four dimensions and record the 4D perft numbers.

### 2026-09-05 — Stage 2

Owner directed continuing to Stage 2. All assertions pass with no core changes.
4D perft recorded in `docs/PERFT_4D.md`. The "early check impossible" claim in
SPEC.md section 2 was found false (check at ply 3) and annotated. Gate result:
PASS. Next: Stage 3, rendering and perspective in Unity. Its first step deletes
the old 2D rules code under `Assets/Scripts/ChessGame`.

### 2026-09-05 — Stage 3

Owner directed continuing to Stage 3. Old 2D rules code deleted. `AxisView`
added to Core with exact-endpoint tests. Unity rendering, rotation, paging,
orbit, HUD and hidden-axis widgets built in code; scene assembled by an editor
script in batch mode; macOS player built and driven through a scripted
walkthrough for the screenshot gate. Two build-side lessons recorded: a
standalone player idles when unfocused unless `runInBackground` is set, and it
stops presenting frames when its window is occluded, so the demo captures via
a render texture. Gate result: PASS. Next: Stage 4, game management and the
position editor.

### 2026-09-05 — Stage 4

Owner directed continuing to Stage 4. Game, Notation and PositionText added to
Core with tests; NOTATION.md written with the compact form as a proposal only;
play mode, promotion dialog, history with undo and redo, setup editor and
save/load built in the Unity HUD; gate verified by the scripted game
walkthrough. Gate result: PASS. Next: Stage 5, the engine (AttackMapService
and the single-threaded SearchEngine). Owner decision pending: the compact
notation in NOTATION.md section 2.

### 2026-09-06 — Owner decisions after Stage 4

- Compact notation proposal rejected (three encodings for four alike axes,
  1-based y against 0-based code, and the "looks like 2D chess" pitch was
  false because the shell spreads pawns across z and w). Decided: drop the
  brackets, `Q3033-0333+`. Same numbering as the code, six digits at 6D.
  Long tuple form stays in save files. `NOTATION.md` rewritten as approved;
  `Notation` emits the compact form and parses both; tests updated.
- Move animation moved into Stage 5 as part of its gate, with three cases
  (both cells visible: animate; one visible: animate the visible half;
  neither: flash the history line and mark the changed hidden layer) plus a
  jump-to-last-move button that pages the view. The view never moves on its
  own. SPEC.md Stage 5 updated.

### 2026-09-06 — Stage 5

Owner directed continuing to Stage 5 after the notation and animation
decisions. Engine package written (attack map, evaluation, search, self-play
harness) with tests including the fuzz gate; Unity gained engine players on a
worker thread, threat display from the attack map, the three animation cases,
and jump-to-last-move. Gate: fuzz PASS, walkthrough PASS, self-play
20 games with 0 failures PASS. Next: Stage 6, the tablebase generator.

### 2026-09-08 — Stage 6

Owner directed continuing. Generator written dimension generic and validated
at n=2 against the published 2D results before any 4D run; the validation
caught the duplicate-slot bug. All four 4D tables generated and verified;
findings recorded. Gate result: PASS. All six stages are complete. Remaining
open items for the owner: the four-piece tablebases (deferred by the spec),
six dimensions (deferred), the WebGL build (deferred), and the orientation
rule for perspectives raised at Stage 3.

### 2026-09-08 — Playtest correction to Stage 4

Owner playtested and found the board orientation wrong: y was vertical and
the camera fixed, so Black played backwards. Owner specified the fix (y as
depth; board turns to face the side to move after each move; suppressed when
y is not on screen; auto-flip toggle; never a move). Implemented as a
camera-only orientation behaviour and a slot-to-world remap, verified by
walkthrough frames, recorded above under Stage 4.

### 2026-09-13 — Playtest corrections to the HUD

Owner playtested on a Retina Mac and found the HUD pixelated, the perspective
and phi controls unclear, and no way back to the opening view. Fixes: the
canvas now uses constant pixel scaling that follows display DPI (1x to 2x,
pixel perfect) instead of shrinking from a virtual 1280x800; MSAA 4x on the
upper quality levels; the view line reads "across x  depth y  up z / hidden w
at layer 3"; the perspective section is labelled "Axes on screen" with a
one-line explanation of click versus shift+click; the slider is labelled with
the turn in progress ("Turn z out, w in: 37°") or "no turn armed"; and a Reset
view button (Home or 0) returns to the identity view, the opening orbit and
zoom, starting pages and isolation off, then re-applies the orientation rule.
Reset is a view operation and never enters history.

### 2026-09-19 — Four-piece question answered (owner asked: can K+Q+R force mate?)

Beyond the six stages; the spec had deferred this as a 1.5 TB cloud job. Owner
asked for it directly. Answer: **no, and neither can any other two pieces.**
All ten K+A+B vs K endings were solved exactly on the 8x8x8x8 board. K+Q+R has
51,625 won white-to-move positions up to symmetry out of roughly 6 x 10^11,
every one with the black king already on an edge, longest mate 7 plies;
K+Q+Q 106,000 and 9 plies; the rest fewer. Minimum mating material is more
than two pieces beyond the king. Full write-up in `docs/FINDINGS.md`.

Added to `src/Chess4D.Tablebase` (console only, nothing in the game build):

- `FourPieceGenerator` and the `generate4` command: dense K+A+B vs K with
  black-king captures resolved against the three-piece tables. Validated at
  2D against published results (K+B+N 33 moves, K+B+B 19 moves, K+N+N mates in
  one only, K+Q+R all won). Run in 4D at sides 4 and 5.
- `SparseSolver` and the `sparse` command: exact solve storing only decided
  positions, rules taken from `Board`. Reproduces the dense tables exactly at
  2D and at 4D sides 4 and 5, then solves side 8 in about a minute. Every
  stored position of all ten endings re-verified one ply deep: 0 failures.
- `SafeRegion` and the `safe` command: table-free drawing certificate. Proves
  the K+Q draw again (3,792 safe cells); inconclusive for K+Q+R.
- `Symmetry`: fundamental domain now handles odd sides ((side-1)/2; identical
  for even sides), and `RepId` made public. Existing tablebase tests still pass.
- `FourPieceTablebaseTests`: 8 tests. All 19 tablebase tests green
  (`dotnet test -c Release --filter FullyQualifiedName~Tablebase`, 18 s). The
  full suite was not re-run this session.

Not done: three extra pieces (K+Q+Q+R and similar). An attempt was stopped:
the decided set stops being sparse once an idle third piece can stand
anywhere, so it needs a different method. The 13 September HUD changes are
still uncommitted alongside this work.

### 2026-09-26 — Rule variants that restore the K+Q mate (owner's question)

Owner asked what rule changes would make K+Q vs K winnable in 4D while
staying believable and balanced. `BoardGeometry` gained two optional
parameters, `diagonalAxes` (default 2) and `kingAxes` (default = diagonal),
threaded through `Board.IsAttacked`/`Attackers`, `ThreePiece`, and the three
console tools as `--diag n --king n`; defaults reproduce the settled rules
exactly (perft and all tablebase tests unchanged). Queen directions are now
ordered by axis count so the King's are a prefix (`KingDirectionCount`).

Result, exact tables on the 8x8x8x8 board: widening the Queen alone does
almost nothing (80 directions: 0.43% won); slowing the King alone does little
(orthogonal King, settled Queen: 0.02%); both together, **orthogonal King
plus 3-axis diagonals, make K+Q vs K a forced win from every position in at
most 8 moves**. Under that variant a lone Bishop also mates (80 moves at
3-axis, 14 at 4-axis) while Rook, Knight and two Rooks do not, so piece
values invert. 3D checked for comparison (settled: drawn; either change
alone: won). Full tables in `docs/FINDINGS.md`, summaries under
`docs/tablebase/variants/`.

Also: `SafeRegion` masks widened to 64 bits (rejects Kings with more moves);
successor buffers in both dense generators sized by `G.King.Length` (the
80-move King overflowed a fixed 64); the engine spot check is skipped for
variants (`--verify 0 0`) because search with 80-direction pieces does not
finish. `RuleVariantTests`: direction counts, Board versus fast geometry
agreement under five variants, the 3D variant results, and an explicit 4D
gate. Not done: a separate diagonal rule for the Bishop (the balance repair
suggested in FINDINGS), and self-play under the variant.

### 2026-09-27 — The owner's King variant tested

Owner clarified the variant they had in mind: the King keeps a 2D king's 8
moves within its own x-y board and steps straight to the neighbouring board
along z or w, never diagonally across boards (12 moves in the open). Added
as `BoardGeometry(..., boardKing: true)` / `--boardking`, with
`QueenIsKing[]` replacing the prefix scheme in `Board.IsAttacked`, and the
`Symmetry` group and fundamental domain reduced to the 64 transforms that
respect the x-y board (tested: order, class count, index invariance).

Results, exact: with the settled Queen this King is still a draw on the
8x8x8x8 board (14,358 won classes, all on an edge, longest 9 moves); with
the 3-axis Queen K+Q wins every position at sides 6 and 7 (7 and 8 moves).
Unlike the orthogonal King, a lone 3-axis Bishop does not mate this King,
and the Rook still has no checkmate at all. K+Q+R with the settled Queen
wins every position at side 4 (19 moves) and exceeds 20 million won classes
at side 8 (exact count out of reach on this machine). Recorded in `docs/FINDINGS.md` with the comparison table;
summaries under `docs/tablebase/variants/`. 118 tests green.

Limitation found: the dense three-piece generator overflows the array
length at side 8 under the 64-element group (2^31 entries); the winning
side-8 figure is inferred from sides 6 and 7. Splitting `Generator.Values`
per (king, piece) pair as `FourPieceGenerator` does would remove it.

### 2026-09-27 — Endgame table for the owner (settled King, owner's King, 2D)

Owner asked for a table of forced mates across eight material sets in three
rule worlds. 2D and the settled King were already exact; for the owner's
King every four-piece ending was solved exactly on 4^4 and attempted on 8^4
with the sparse solver (exact where the result is a draw: K+R+R, K+B+B,
K+N+N; capped where it is not: K+Q+R, K+Q+B, K+R+B, K+B+N). Added `--cap` to
`sparse` and a won-set-by-edge-distance report when the cap is hit; the
K+Q+R run showed interior mates in one exist under the owner's King.
Diagnostic runs for K+Q+B, K+R+B and K+B+N were stopped after an hour
without result to save compute. Table in `docs/FINDINGS.md`; a Word copy
was given to the owner. The full-board four-piece table under the owner's
King is the remaining gap: about 9 trillion entries at the current indexing
(an earlier note said 36 GB; that was an arithmetic slip).

### 2026-10-01 — 5^4 four-piece tables under the owner's King

Owner asked whether the full-board four-piece question could be made
calculable. Answer recorded: pruning White's moves is sound (a "cage"
tablebase, a day or two of work), pruning Black's is not; the cheap hedge
is the next board size. `FourPieceGenerator` rewritten to one byte per
position with no successor counters (lost positions are re-examined when a
successor is solved); identical results at 2D and 4^4, 8 tests green.
Then K+Q+R and K+Q+B against the owner's King on 5^4 (6.5 billion entries,
6.2 GB each, about 35 minutes each, 5,000-sample consistency 0 failures):
both 100% won, longest 36 moves (K+Q+R) and 15 moves (K+Q+B). The K+Q+R
length nearly doubled from side 4, so on 8^4 it likely exceeds fifty moves;
K+Q+B grew from 11 to 15 and is the practical mating pair. FINDINGS table
updated; summaries under `docs/tablebase/variants/`.

### 2026-10-02 — Literature search

Owner asked for prior work on 4D mating material before any publication.
Result in `docs/LITERATURE.md`: the difficulty of mating in 4D is known
among variant designers (Parton, Joyce, Aikin, Reiniger) and the orthogonal
King has precedent (Chesseract), but no exhaustive 4D result was found; the
only tablebase work is H. G. Muller's for 3D Raumschach (2014). Our
generator reproduces his "KQK is won" (8 moves on 5x5x5) and confirms the
Reiniger/Joyce 4x4x4x4 observation. New finding from the cross-check: with
80-direction King and Queen, K+Q wins at sides 4, 5 and 6 (4, 8, 14 moves)
and is a draw at 7 and 8, so Reiniger's "any size board" conjecture is
false. Search limits recorded in the file (web only; some pages 403).

### 2026-10-04 — Academic literature review

Owner asked for a proper review (Scholar-class sources, not forums). Four
parallel streams covered retrograde analysis and tablebases, infinite
chess, pursuit-evasion, chess-graph domination, the angel problem,
symmetry reduction, verification of computed game results, and
higher-dimensional chess in print (Maack 1908 in German, Dawson, Dickins,
Gibbins, Parton, Pritchard/Beasley 2007, Reiniger, the variant designers'
primary rule pages, and Rinaldi & Chiru 2026 in full). Written up in
`docs/LITERATURE.md` with per-item read flags and a list of gaps.

Headline: no exhaustive 4D mating-material result exists in print. The
one refereed 8^4 paper (Rinaldi & Chiru 2026) is the natural predecessor:
same board, same two-axis Queen, endgames treated empirically. This
project is framed as extending it to exhaustive computation. Under its
Definitions 7 and 9 (two-axis sliders, Chebyshev 80-move King) our tables
find no checkmate position for K+Q or K+R on 8^4, 4^4 or 5^4, reported as a
finding inviting clarification rather than a verdict. Also checked exactly: Muller's
3D Raumschach "KQK is won" (agrees, 8 moves); the 4x4x4x4 full-king K+Q
win (agrees, 4 moves) and its failure from side 7 up; Aikin's Chesseract
K+Q (win, 20 moves on 4^4); Joyce's Hyperchess K+3Q draw and his exact
position (draw both ways; K+Q has no mate, K+2Q mates in one only, K+3Q
longest forced mate 70 moves on a tiny won set).

Code: `BoardGeometry` gained `pairDiagonals` (Joyce's rule) and a King
that may out-reach the Queen (`kingAxes` up to the dimension count, built
independently of the Queen's directions); `Board.IsAttacked`/`Attackers`
now check King attacks from the King's own direction list; `Symmetry`
handles the pair-swapping group (order 128); `ThreePiece` King rule fixed
for pair diagonals (it had let the King take cross-pair diagonals, caught
by the consistency check); `sparse --probe` to query a position. 121
tests green. Summaries under `docs/tablebase/variants/`.

### 2026-10-04 — Framing decision

Owner's direction: the work is an extension of Rinaldi & Chiru (2026)
into the computability of 4D endgames, not a refutation; no claim is made
that mate is impossible with enough material. "Refuted"/"false" wording
removed from LITERATURE.md and FINDINGS.md; the K+Q/K+R discrepancy is
stated as a reproducibility finding with the likely explanations and an
invitation to clarify. Checking their public engine code is now optional.

### 2026-10-04 — Cross-validation matrix

Owner asked whether the checkmates could be cross-validated against every
ruleset in the literature table. Ran seven rulesets (settled, Rinaldi-
Chiru, Dawson Normal Form, Chesseract, owner's board-King, Hyperchess,
Raumschach) against the four single pieces and the ten pairs, exact on
each variant's native board; 90-odd tables, every verification clean.
Table and readings in `docs/FINDINGS.md`. Muller's 2014 Raumschach mate
lengths (K+R+R 10, K+R+N 16) reproduced exactly. Under the 2026 paper's
80-move King nothing up to two pieces has a checkmate on 8^4 except
cooperative K+Q+Q corner mates. Logs under `docs/tablebase/matrix/`.

### 2026-10-04 (evening) — Reproducible dataset complete

`Chess4D.Tablebase matrix` rebuilt every cell from the registry:
`docs/tablebase/matrix.csv`, 126 cells (nine ruleset rows x fourteen
material sets), 172 minutes, zero verification failures, each row with its
command. Two build fixes on the way: quoted CSV fields broke resume
detection (recomputed cells; fixed with a proper CSV parser and a dedupe),
and 8^4 two-piece cells under reduced symmetry would have taken hours each
to hit the sparse cap (now: Queen pairs implied when K+Q alone wins, early
cap otherwise, and the owner's King's pairs moved to a 5^4 registry entry).
New exact results: owner's King on 5^4, K+Q+Q 12 moves, K+Q+N 27, all
queenless pairs draws. FINDINGS matrix section regenerated from the CSV;
PAPER.md updated.
