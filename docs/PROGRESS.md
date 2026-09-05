# Progress Log

Update this file at the end of every session. Append, never overwrite.
Read `SPEC.md` in full before starting any work.

Format per entry: date, stage, what was done, gate result, what is next.

---

## Environment

Fill in during Stage 0. Update whenever a tool version changes.

| Item | Value |
|---|---|
| Unity editor (target, per owner's decision) | _pending: Unity 6 LTS if WebGL matters, else 2022.3.47f1 already installed_ |
| Unity editor (actually in use) | _not yet upgraded; project file says 2020.3.19f1_ |
| .NET SDK | _not installed as of 2026-09-05_ |
| Machine | macOS 24.3 (Darwin), Apple Silicon, Homebrew present |
| Library packaging | Local UPM packages `Packages/com.chess4d.core` and `Packages/com.chess4d.engine`, source globbed into `/src` .NET projects. See SPEC.md Stage 0 item 5. |

---

## Stage 0 — Project setup
Status: NOT STARTED

## Stage 1 — Rules core, validated at n=2
Status: NOT STARTED
Gate: perft from the standard opening must equal 20 / 400 / 8902 / 197281 / 4865609

### Benchmarks (keep current; SPEC.md section 2 sets the budget)

Open benchmark position: _define in Stage 1 and describe it here as a list of `(piece, colour, (x,y,z,w))` tuples._

| Date | Position | Pseudo-legal gen | Legal gen | Attack query, one cell | Budget met |
|---|---|---|---|---|---|
| | starting | | | | |
| | open | | | | |

## Stage 2 — Four dimensions
Status: NOT STARTED

## Stage 3 — Rendering and perspective
Status: NOT STARTED

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

- **4D perft depths 1 to 3** (Stage 2): _pending. Also goes in `docs/PERFT_4D.md`._
- **First available check** over random self-play, by ply and piece type (Stage 2): _pending._
- **Bishop parity property** held over N random legal moves (Stage 2): _pending._
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

Gate result: not applicable. Next: owner picks the Unity version and installs
it plus the .NET SDK, then Stage 0 begins on `4d-rewrite`.
