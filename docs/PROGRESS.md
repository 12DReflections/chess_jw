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
Status: NOT STARTED

## Stage 5 — Engine
Status: NOT STARTED

## Stage 6 — Tablebase generator
Status: NOT STARTED

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
