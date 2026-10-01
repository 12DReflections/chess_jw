# Prior work on mating material in higher-dimensional chess

Web search carried out 2026-10-02. Not a systematic review: English-language
web sources only, no MathSciNet/Scopus/zbMATH query, no chess-problem
journals, no German Raumschach literature. Pages on chessvariants.com and the
full text of the MDPI paper could not be opened (HTTP 403); those entries
rest on search-engine excerpts and are marked.

## What exists

| Source | What it says | Status of the claim |
|---|---|---|
| V. R. Parton, Sphinx Chess (4D, 1970s), as reported by Joe Joyce | K+Q could not force mate even with a king restriction | designer's observation |
| Joe Joyce, Hyperchess / Hyperchess4 (4x4x4x4; designed 1970s, posted 2004); "Diagonals, Dimensions, and Draws" (Chess Variants Wiki) | "A king and three queens against a bare king is a draw without either a king restriction or queens getting more moves" (excerpt). Fix adopted: the held-king rule and only a few diagonals. With those rules, playtesting put the minimum to force mate at two bishops and a king. | playtesting, informal |
| Jim Aikin, Chesseract (4x4x4x4) | King moves one cell orthogonally only, never diagonally (excerpt) | rule design; prior art for the orthogonal King |
| Ben Reiniger, "Four Dimensional Chess" (note, 3 Feb 2009) and "Checkmate?" (Chess Variants Wiki, with Joe Joyce, 2010) | A full king has 3^N - 1 moves (8, 26, 80, 242). Lists fixes: bare royals loses, crippled king ("allowing the king to move in only one dimension works out well"), fortress, gladiator kings, god pieces, multiple royals. "I tend to believe that a fully armed 4D K+Q can mate a lone K on any size board, but am not positive"; K+Q mates on 4x4x4x4 "will not work so easily" on larger boards. | conjecture, no computation |
| H. G. Muller, chess.com forum "Minimum material to checkmate at Raumschach?" (March 2014) | For 3D Raumschach (5x5x5): "KQK is won"; "all 4-men without Q are general draws"; KRRK 97,272 wins, longest mate in 10; KRNK 39,048 wins, longest mate in 16 | computed (partial 4-men tables); the only prior tablebase result found, and it is 3D |
| O. Rinaldi (Unciuleanu), C.-G. Chiru, "A Mathematical Framework for Four-Dimensional Chess", AppliedMath 6(3):48, March 2026 | Board {1..8}^4, Chebyshev adjacency, king mobility 80; mobility formulas, parity invariants, an engine, random playouts. Notes that mating "appear[s] to require more careful multi-axis confinement" (excerpt). | no endgame tables, no mating-material result |
| Evans, Hamkins and co-authors, infinite chess (2012 onward) | Mate-in-n decidable; in 3D infinite chess every countable ordinal is a game value | theorems, infinite boards, different question |
| Bhattacharya, Paul, Sanyal, "Cops and Robber Game in Multidimensional Grids" (preprint 2010; poster ASCM 2007) | In an n-dimensional grid n cops are necessary and sufficient | theorem; the closest mathematical analogue of "more dimensions need more pursuers" |
| Ambrose et al., "Cops and robbers on chess graphs", arXiv 2509.18516 (2025) | Cop numbers of n x n knight and queen graphs | theorems, 2D only |
| 4*Chess (chessvariants.com) | A queen "can easily checkmate a lone king" under that variant's rules (excerpt) | designer's statement; rules not verified |

## How our results sit against it

- **The phenomenon is known.** Variant designers have said since the 1970s
  that a lone king is hard or impossible to mate in 4D, and every fix the
  owner and I discussed has a precedent: the orthogonal King (Chesseract;
  Reiniger's "crippled king"), bare king loses, stronger pieces. A paper
  must say so and cite them.
- **No exhaustive 4D result was found.** Nothing computed for any 4D
  ruleset: no tablebase, no proof, no classification. The only tablebase
  work found is Muller's for 3D Raumschach.
- **Cross-checks run against the prior claims** (2026-10-02, our generator):
  - Raumschach rules (5x5x5, 26-direction King and Queen): K+Q wins every
    position, longest mate 8 moves; K+R has no checkmate. Agrees with
    Muller's "KQK is won".
  - Reiniger and Joyce's 4x4x4x4 with fully 4D pieces (80/80): K+Q wins
    every position, longest mate 4 moves. Their observation is confirmed.
  - Reiniger's conjecture "on any size board" is false. With 80-direction
    King and Queen, K+Q vs K is a win at side 4 (4 moves), 5 (8 moves) and
    6 (14 moves) and collapses at side 7 (0.40% of positions) and 8 (0.11%).
    The threshold lies between 6 and 7.
- **What appears to be new**, subject to the limits of this search: exact
  results on 8x8x8x8; the classification of every one- and two-piece set
  under a 4D ruleset; the cover inequality as a stated, checkable bound; the
  sharp board-size threshold above; and the owner's board-King variant.
- **Rules differ between sources.** Most 4D variants give King and Queen
  all 80 directions or restrict diagonals ad hoc; this project's settled
  rules use 1- and 2-axis moves (32 directions). Results do not transfer
  between rulesets, so any comparison has to name the ruleset.

## Links

- https://www.chess.com/forum/view/chess960-chess-variants/minimum-material-to-checkmate-at-raumschach
- http://chessvariants.wikidot.com/checkmate
- http://chessvariants.wikidot.com/hype:a-study-in-bs
- http://math.iit.edu/~breiniger/Chess/chess-basicmath.pdf
- https://www.chessvariants.com/3d.dir/hyperchess.html (not opened)
- https://www.chessvariants.com/large.dir/contest/chesseract.html (not opened)
- https://www.chessvariants.com/rules/4chess-four-dimensional-chess (not opened)
- https://doi.org/10.3390/appliedmath6030048 (abstract only)
- https://www.dcs.warwick.ac.uk/~u1671158/papers/10-DAM.pdf
- https://arxiv.org/abs/2509.18516
- https://arxiv.org/pdf/1201.5597
