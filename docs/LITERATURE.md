# Prior work on mating material in higher-dimensional chess

Second pass, 2026-10-04. The first pass (2026-10-02) was a web search of
forums and variant pages and is superseded by this document; its findings
are folded into section 7. This pass is restricted to academic and printed
sources and was carried out in four parallel streams, each reading
abstracts and, where open, full texts, through the arXiv, CrossRef,
OpenAlex and zbMATH APIs plus web search. Semantic Scholar refused every
unauthenticated request (HTTP 429) and DBLP sits behind a bot wall, so
neither contributed; MathSciNet was not available. Publisher pages that
refused automated fetching (MDPI, Elsevier, Springer, chessvariants.com)
were read in a signed-in browser where needed. Every entry carries a flag:
[full text], [abstract], [metadata]. Items marked [metadata] should have
pages checked before final citation. Section 8 lists cross-checks run
against the printed claims with this project's solvers.

Project facts the review is judged against: board 8x8x8x8; rook, bishop,
queen and king move along lines changing at most two coordinates (king 32
moves, queen 32 directions); exhaustive retrograde tables show no single
piece and no pair of pieces from Q, R, B, N forces mate against a lone
king; mates exist only with the king on an edge; a cover inequality bounds
coverage of the king's 32 escape cells (20 for K+Q, 25 for K+Q+R).

## 0. Summary

1. **No exhaustive result on four-dimensional mating material exists in
   print.** The one refereed 8^4 chess paper (Rinaldi and Chiru 2026) sets
   up the board and ruleset, poses the endgame question, and answers it
   empirically with engine-assisted play and an informal strategy sketch
   (K+R "plausibly" wins; K+Q tested likewise). This project extends that
   work to exhaustive computation: three-piece endings solved exactly on
   8^4, four-piece endings exactly on small boards, and the frontier
   stated. One finding bears directly on their section 5.3: under their
   Definitions 7 and 9 as implemented here, K+Q and K+R have no checkmate
   position on 8^4 (nor on 4^4 or 5^4), so their engine-assisted
   demonstrations must have used a different criterion or rule; the point
   is reported as a finding inviting clarification, not as a verdict on
   their paper.
2. **The phenomenon is old folklore in the variant literature.** Maack
   (1908) wrote that mating is "decidedly harder in space" and proposed
   restricting the bare king; Beasley (2007) wrote that in three dimensions
   a rook or queen "doesn't" present a barrier and hand-analysed a 6D board
   where "K+Q can checkmate a bare K, but the mate cannot be forced"; Joyce
   (2004) gave a concrete K+3Q position he believed drawn (confirmed in
   section 8). None of these is a computation or a proof.
3. **The closest formal relatives are outside chess.** Evans and Hamkins
   (2014) define 3D infinite chess with exactly this project's piece
   convention and observe that on an edgeless 2D board only K+Q+Q among
   three-piece sets can even construct a mate. Pursuit-evasion theory has
   the "surrounding cop number" (occupy every neighbour of the evader) and
   cover-counting lemmas for queens that are 2D ancestors of the cover
   inequality; domination theory has Barr and Rao's bound for d-dimensional
   queens. Nobody has applied any of them to chess graphs in three or more
   dimensions.
4. **Terminology exists for the piece.** Cashman et al. (2024) call the
   two-coordinate queen a "line-queen", distinct from the (3^d-1)/2-line
   queen of the domination literature and from Ripa's k-queen.
5. **The remedies discussed here all have precedent**: orthogonal king
   (Aikin's Chesseract; Reiniger's "crippled king"; Lewin's 1-dimensional
   king), bare king loses (Maack), held king (Joyce), stalemate as a win
   (Beasley), stronger pieces (Schmittberger's hook-move rook).
6. **Verification practice**: Hurd and Haworth (2010) insist that
   re-generation must be independent code, not a separate pass of the same
   code, which is what the dense and sparse solvers here provide; Pavlov
   (2026) proves that a one-ply consistency check certifies a table only
   when anchored by correct terminal and sub-table labels, which the
   `VerifyOnePly` pass here does check and the paper should say so.

## 1. Retrograde analysis and endgame tables

Methodology:

- Thompson, K. (1986). Retrograde analysis of certain endgames. *ICCA J.*
  9(3):131-139. doi:10.3233/icg-1986-9302. [metadata] Origin of the method.
- Thompson, K. (1996). 6-piece endgames. *ICCA J.* 19(4):215-226.
  doi:10.3233/icg-1996-19403. [abstract]
- Stiller, L. (1989). Parallel analysis of certain endgames. *ICCA J.*
  12(2):55-64. doi:10.3233/icg-1989-12202. [abstract] Stiller (1991). Group
  graphs and computational symmetry on massively parallel architecture.
  *J. Supercomputing* 5:99-117. doi:10.1007/BF00127839. [metadata] Stiller
  (1996). Multilinear algebra and chess endgames. *Games of No Chance*,
  MSRI 29, 151-192. [abstract] Group-theoretic treatment of board symmetry
  in retrograde analysis.
- Heinz, E. A. (1999). Endgame databases and efficient index schemes for
  chess. *ICCA J.* 22(1):22-32. doi:10.3233/icg-1999-22104. [abstract]
- Nalimov, E. V., Haworth, G. McC., Heinz, E. A. (2000). Space-efficient
  indexing of chess endgame tables. *ICGA J.* 23(3):148-162.
  doi:10.3233/icg-2000-23304. [full text] The side-to-move king confined to
  the a1-d1-d4 triangle (fundamental domain of D4, order 8) gives 462
  king-pair classes; the direct 2D precedent of this project's B4 (order
  384) canonicalisation.
- Wirth, C., Nievergelt, J. (1999). Exhaustive and heuristic retrograde
  analysis of the KPPKP endgame. *ICGA J.* 22(2):67-80. [metadata]
- Wu, R., Beal, D. F. (2001). Fast, memory-efficient retrograde algorithms.
  *ICGA J.* 24(3):147-159. doi:10.3233/icg-2001-24303. [abstract] Relevant
  to the one-byte-per-position design used here.
- Schaeffer, J. et al. (2007). Checkers is solved. *Science*
  317:1518-1522. doi:10.1126/science.1144079. [full text, verification
  passages]

Variants and "which material mates" as a mathematical question:

- Fang, H.-r., Hsu, T.-s., Hsu, S.-c. (2001). Construction of Chinese chess
  endgame databases by retrograde analysis. *CG 2000*, LNCS 2063, 96-114.
  [metadata] Wu and Beal (2001). *Information Sciences* 135. [metadata]
- Ciancarini, P., Favini, G. P. (2010). Playing the perfect Kriegspiel
  endgame. *Theor. Comput. Sci.* 411:3563-3577. [metadata]
- van Rijn, J. N., Vis, J. K. (2014). Endgame analysis of Dou Shou Qi.
  *ICGA J.* 37(2):120-124. arXiv:1604.07312. [abstract]
- Gehnen, M., Stannat, J. (2026). Endgames in fog of war chess. *FUN 2026*,
  LIPIcs. doi:10.4230/LIPIcs.FUN.2026.21. [abstract] K+Q wins, K+R does
  not, K+2R does, under a rule change: the same framing as this project.
- Ambrona, M. (2022). A practical algorithm for chess unwinnability. *FUN
  2022*, LIPIcs 226, 2:1-2:20. doi:10.4230/LIPIcs.FUN.2022.2. [abstract]
  "Can this side ever deliver mate" as an algorithmic problem.
- Malikovic, M., Janicic, P. (2013). *ICGA J.* 36(2):81-99; Maric, Janicic,
  Malikovic (2015). *CADE-25*, LNCS 9195:256-271; Janicic, Maric,
  Malikovic (2019). Computer-assisted proving of combinatorial conjectures
  over finite domains: a case study of a chess conjecture. *LMCS*
  15(1):34. doi:10.23638/LMCS-15(1:34)2019. [abstract] Machine-verified:
  K+R forces mate on every n x n board, n > 3. The 2D positive theorem
  that the 4D result contrasts with.
- Wastlund, J. (2024). The bishop and knight checkmate on a large
  chessboard. arXiv:2405.04421, to appear in *Games of No Chance 6*.
  [abstract] Mate length versus board size is a live question.
- Brunner, J., Demaine, E. D., Hendrickson, D., Wellman, J. (2020).
  Complexity of retrograde and helpmate chess problems. arXiv:2010.09271.
  [abstract] Background.
- Tudsuan, T., Thanatipanonda, T. (2026). Building Makruk endgame
  tablebases. *J. Sci. Ladkrabang* 35(1). [abstract] A variant with weak
  mating material (3.96% won).

Not found: any tablebase or exact endgame result for any 3D or 4D chess in
the refereed literature; any axiomatic treatment of mating material in
d >= 3.

## 2. Infinite chess and edgeless boards

- Brumleve, D., Hamkins, J. D., Schlicht, P. (2012). The mate-in-n problem
  of infinite chess is decidable. *CiE 2012*, LNCS 7318, 78-88.
  doi:10.1007/978-3-642-30870-3_9. arXiv:1201.5597. [abstract]
- Evans, C. D. A., Hamkins, J. D. (2014). Transfinite game values in
  infinite chess. *Integers* 14, G2. arXiv:1302.4377. [full text]
  Observation 2: on the edgeless board, "for positions with three pieces,
  there is no checkmate possibility other than two queens versus a king";
  K+Q+R mates. Section 4 defines 3D infinite chess with rooks on axes,
  bishops on planar diagonals ("this does not include the long
  three-dimensional diagonal, which is called the unicorn move"), queens
  likewise, and "Kings may move in the same directions as a queen, but only
  one step": **this project's convention exactly.** Also surveys
  Kubikschach (1851) and Raumschach (1907).
- Evans, Hamkins, Perlmutter (2015). A position in infinite chess with game
  value omega^4. arXiv:1510.08155. [abstract]
- Bolan, M., Tsevas, A. (2026). Universality of infinite chess.
  arXiv:2602.14277. [abstract]
- Guy, R. K., Nowakowski, R. J. (2002). Unsolved problems in combinatorial
  games. *More Games of No Chance*, MSRI 42, 457-473. [metadata] Source of
  the quarter-infinite K+R problem.
- Low, R. M., Stamp, M. (2006). King and rook vs. king on a quarter-infinite
  board. *Integers* 6, G03. [metadata] Kanungo, S., Low, R. M. (2007).
  *Integers* 7(1), G9. [abstract] Berlekamp, E., Low, R. M. (2018).
  Entrepreneurial chess. *Int. J. Game Theory* 47:379-415.
  doi:10.1007/s00182-017-0580-z. [full text] A single corner restores the
  K+R win; smallest decisive region 11 x 8.

Not found: any classification of mating material in infinite chess beyond
Observation 2; anything on mating material in infinite 3D chess; anything
on 4D infinite chess.

## 3. Pursuit-evasion (Cops and Robbers)

- Nowakowski, R. J., Winkler, P. (1983). Vertex-to-vertex pursuit in a
  graph. *Discrete Math.* 43:235-239. [metadata] Quilliot, A. (1978).
  Thesis, Paris VI. [metadata] Aigner, M., Fromme, M. (1984). A game of
  cops and robbers. *Discrete Appl. Math.* 8:1-12. [metadata]
- Maamoun, M., Meyniel, H. (1987). On a game of policemen and robber.
  *Discrete Appl. Math.* 17:307-309. [metadata] Hypercube Q_d needs
  ceil((d+1)/2) cops.
- Neufeld, S., Nowakowski, R. (1998). A game of cops and robbers played on
  products of graphs. *Discrete Math.* 186:253-268. [metadata]
- Bonato, A., Nowakowski, R. J. (2011). *The Game of Cops and Robbers on
  Graphs.* AMS SML 61. [metadata] Standard monograph.
- Bhattacharya, S., Paul, G., Sanyal, S. (2010). A cops and robber game in
  multidimensional grids. *Discrete Appl. Math.* 158(16):1745-1751.
  doi:10.1016/j.dam.2010.06.014. arXiv:0909.1381. [abstract] Exactly n
  cops are necessary and sufficient on an n-dimensional grid.
- Bonato, Gordinowicz, Kinnersley, Pralat (2013). The capture time of the
  hypercube. *Electron. J. Combin.* 20. doi:10.37236/2921. [abstract]
- Bonato, A., Chiniforooshan, E., Pralat, P. (2010). Cops and robbers from
  a distance. *Theor. Comput. Sci.* 411:3834-3844. [metadata] Capture at
  distance k: the analogue of giving check.
- Burgess, A. C. et al. (2020). Cops that surround a robber. *Discrete
  Appl. Math.* doi:10.1016/j.dam.2020.06.019. arXiv:1910.14200. [full
  text] Cops win by occupying every neighbour of the robber; Lemma 1.2
  sigma(G) >= delta(G); the 2D king graph needs 5 surrounding cops. The
  cover inequality is the attack-coverage analogue of Lemma 1.2.
- Crytser, D., Komarov, N., Mackey, J. (2014). Containment. arXiv:1405.3330.
  [abstract] Jungeblut, Schneider, Ueckerdt (2023). arXiv:2302.10577.
  [abstract] Clarke, Dyer, Kellough (2024). arXiv:2408.10452. [abstract]
- Hahn, A., Nicholson, N. R. (2018). Cops and robbers on toroidal chess
  graphs. arXiv:1810.10577 (preprint only). [full text] One queen-moving
  cop catches a king-moving robber on the torus; Lemma 4 counts how many
  of the robber's cells the queen protects: a cover-counting argument of
  the kind used here.
- Sullivan, B. W., Townsend, N., Werzanski, M. L. (2017). An introduction
  to lazy cops and robbers on graphs. *College Math. J.* 48(5):322-333.
  doi:10.4169/college.math.j.48.5.322. [metadata] A queen-cop guards at
  most three vertices on each robber line.
- Ambrose, S. et al. (2025). Cops and robbers on chess graphs.
  arXiv:2509.18516. [full text] Cop numbers of all n x n chess graphs
  (queen: 3 for 7 <= n <= 18, 4 for n >= 19); Theorem 1.3 on "royal
  graphs" with arbitrary direction sets, proved by counting cells covered
  per robber line. 2D only.
- Jones, J., Kinnersley, W. B. (2025). Limited-visibility cops and robbers
  on Hamming graphs. arXiv:2509.05196. [full text, introduction]
  d-dimensional rook graphs.
- Kinnersley, W. B., Townsend, N. (2021). arXiv:2107.14193; (2025)
  arXiv:2506.20753. [abstract] Fast robbers on grids and hypercubes.

Not found: any cop number or surrounding number for chess graphs in d >= 3;
any pursuit model with cops and robber as different pieces on a bounded
board in d >= 3; any treatment of checkmate (attack coverage plus the
capture-of-undefended-piece rule) as a pursuit-evasion win condition.

## 4. Domination in chess graphs

- Cockayne, E. J. (1990). Chessboard domination problems. *Discrete Math.*
  86:13-20. doi:10.1016/0012-365X(90)90344-H. [metadata] Canonical survey.
- Burger, A. P., Mynhardt, C. M. (2002). *Discrete Appl. Math.* 121:51-60.
  [metadata] Ostergard, P. R. J., Weakley, W. D. (2001). *Electron. J.
  Combin.* 8(1), R29. [metadata] Weakley (2002, 2022); Finozhenok and
  Weakley (2007). [metadata] Bozoki, Gal, Marosi, Weakley (2019).
  *Electron. J. Combin.* 26. doi:10.37236/6026. [abstract] Bird, W. H.
  (2017). PhD thesis, Victoria. [abstract] Karandikar and Dutta (2023)
  arXiv:2304.06620; Rostami and Bright (2025) arXiv:2508.11945 (SAT with
  certificates). [abstract]
- Favaron, O. et al. (2003). Irredundance and domination in kings graphs.
  *Discrete Math.* 262:131-147. [metadata] Watkins, J. J. (2004). *Across
  the Board.* Princeton. [metadata]
- Barr, J., Rao, S. (2006). The n-queens problem in higher dimensions.
  *Elem. Math.* 61(4):133-137. arXiv:0712.2309. [full text] A d-dimensional
  queen (all (3^d-1)/2 lines) attacks at most n(3^d-1)/2 cells; at least
  2n^(d-1)/(3^d-1) queens dominate the n^d board; n queens never suffice
  for d >= 3. The only d-general domination bound for queens.
- Ramani, M. (2026). On the structure of 3D queen domination.
  arXiv:2604.03793. [full text] Exact 3D values for n <= 6; per-position
  coverage counts (Theorem 2), the nearest relative of the cover
  inequality.
- Langlois-Remillard, A., Mussig, M., Roldan, E. (2025). Complexity of
  chess domination problems. *Res. Math. Sci.* 12:14.
  doi:10.1007/s40687-024-00492-5. [abstract] NP-complete for d >= 3.
- Ionascu, E. J., Pritikin, D., Wright, S. E. (2008). k-dependence and
  domination in kings graphs. *Amer. Math. Monthly* 115(9):820-836.
  [abstract] Moore, C., Mertens, S. (2024). arXiv:2407.19344. [abstract]
  d-dimensional king graphs.
- Cashman, C., Cooper, J., Marquez, R., Miller, S. J., Shuffelton, J.
  (2024, rev. 2025). Hyper-bishops, hyper-rooks, and hyper-queens.
  arXiv:2409.04423; *Mathematics Magazine* (2026)
  doi:10.1080/0025570X.2026.2630703. [full text] Definition 5.3:
  "line-bishop" (a 2D bishop in every 2-plane) and "line-queen" = line-rook
  + line-bishop: this project's queen. Probabilistic safe-square results,
  not domination numbers.
- Sawhney, M., Stoner, D. (2018). arXiv:1801.10607. [abstract] Rook
  domination in d dimensions as covering codes.

Not found: any domination number for a 4-dimensional queen graph under
either definition; any work on the line-queen beyond Cashman et al.; any
paper connecting board domination to domination of a single king's closed
neighbourhood. The cover inequality appears to be new.

## 5. The angel problem and dimension-dependent evasion

- Berlekamp, Conway, Guy (1982). *Winning Ways*, vol. 2, ch. 19.
  [metadata] The Devil catches a chess king (power-1 angel) on a 33 x 33
  board.
- Conway, J. H. (1996). The angel problem. *Games of No Chance*, MSRI 29,
  3-12. [abstract]
- Kutz, M. (2005). Conway's angel in three dimensions. *Theor. Comput.
  Sci.* 349(3):443-451. doi:10.1016/j.tcs.2005.08.034. [full text] The
  13-angel escapes in Z^3; the 3D hierarchy argument fails in 2D because
  obstacles must be 2-dimensional.
- Bollobas, B., Leader, I. (2006). The angel and the devil in three
  dimensions. *J. Combin. Theory A* 113(1):176-184.
  doi:10.1016/j.jcta.2005.03.009. [abstract; Oberwolfach Report 1/2006]
  Angel of sufficient speed escapes in 3D; "angel versus infinitely many
  devils" wins in 3D but not 2D; the Time-Bomb Conjecture (power-1 angel
  escapes in Z^3 moving monotonically) is **open**.
- Kutz, M., Por, A. (2005). Angel, devil, and king. *COCOON 2005*, LNCS
  3595, 925-934. [metadata] Mathe, A. (2007). *CPC* 16(3):363-374; Kloster,
  O. (2007). *Theor. Comput. Sci.* 389:152-161; Bowditch, B. H. (2007).
  *CPC* 16(3):345-362; Gacs, P. (2007). arXiv:0706.2817. [abstract] The 2D
  solutions (power 2 wins).
- Songsuwan, Tangthanawatsakul, Kaemawichanurat (2022). Drunk angel and
  hiding devil. arXiv:2202.08988. [abstract] A random angel is caged with
  high probability iff dimension <= 2.
- Babic, V. (2025). The angel problem on triangular and hexagonal boards.
  *Publ. Inst. Math.* 118(132). [zbMATH summary]

Not found: any theorem that the power-1 angel escapes in 3D or 4D; any
angel result specific to dimension 4; any link made to chess mating
material. The analogy must be presented as heuristic.

## 6. Symmetry reduction and verification

Symmetry and indexing: Nalimov, Haworth, Heinz (2000) and Stiller
(1991, 1996) above; Silver, R. (1967). The group of automorphisms of the
game of 3-dimensional ticktacktoe. *Amer. Math. Monthly* 74(3):247-254.
[metadata]; Patashnik, O. (1980). Qubic. *Math. Mag.* 53(4):202-216.
[metadata]; Dvorak, P., Valla, T. (2021). Automorphisms of the cube n^d.
*Discrete Math.* 344(3):112234. doi:10.1016/j.disc.2020.112234.
[abstract] (which symmetries of n^d preserve lines); Sriphum, W.,
Chomsiri, T. (2026). Symmetry-reduced enumeration and canonical forms ...
under the hyperoctahedral group. *Symmetry* 18(9):1454. [abstract]
(canonical forms under B_d = C2 wr S_d on m^d grids); Irving, G. (2014).
Pentago is a first player win. arXiv:1404.0743. [abstract]; Rokicki et al.
(2013). *SIAM J. Discrete Math.* 27(2):1082-1105. [abstract].

Verification:

- Hurd, J. (2005). Formal verification of chess endgame databases. *TPHOLs
  Emerging Trends*, Oxford PRG-RR-05-02, 85-100. [full text, p. 1] HOL4 +
  BDD tables, correct by construction, 4 pieces only.
- Hurd, J., Haworth, G. (2010). Data assurance in opaque computations.
  *ACG 12*, LNCS 6048, 221-231. doi:10.1007/978-3-642-12993-3_20. [full
  text] "A verification check should not only be separate but
  independent"; Nalimov's check "shares 80% of its code with the
  generation code"; history of tablebase bugs (the FEG KNNK bug).
- Pavlov, A. (2026). Capture-quiet decomposition: a verification theorem
  for chess endgame tablebases. arXiv:2604.07907. [abstract] A WDL table
  is correct iff terminal labels are right, capture positions agree with
  verified sub-tables, and quiet positions are retrograde-consistent;
  self-consistency alone is satisfied by the all-draw labelling. Directly
  relevant to a "nothing is won" result.
- Fang, H.-r. (2006). Rule-tolerant verification algorithms for
  completeness of Chinese-chess endgame databases. *CG 2004*, LNCS 3846,
  129-144. [abstract]
- Edelkamp, S., Kissmann, P. (2008). *KI 2008*, LNCS 5243, 185-192.
  [metadata] Allis (1988) and Tromp (2008) on Connect-4 as two independent
  methods agreeing. [metadata/abstract] Marzion, E. (2023). coqtbgen (grey
  literature). [metadata]

## 7. Higher-dimensional chess in print

German Raumschach literature:

- Maack, F. (1908). *Anleitung zum Raumschach (Dreidimensionales
  Schachspiel).* Selbstverlag, Hamburg. archive.org
  anleitungzumrau00maacgoog. [full text, OCR] Board 8x8x8 (the 5x5x5 came
  later). Queen: 26 directions; "Der König ... beherrscht also, inklusive
  seines Standfeldes, ein Terrain von 3x3x3 = 27 Würfelfeldern." p. 15:
  "es ist entschieden schwieriger, den König im Raum mattzusetzen als auf
  dem Brett", and the first printed king restriction: "Man könnte z. B.
  bestimmen, dass der seiner sämtlichen Steine beraubte König ... nicht
  mehr nach unten ziehen darf." Section 8 gives 50 mate positions
  (compositions), no forced-mate analysis.
- Maack (1907/1908). *Das Schachraumspiel.* A. Stein, Potsdam; (1913)
  *Spielregeln zum Raumschach*; (1919) *Raumschach - Einführung in die
  Spielpraxis*; (1909-1911, 1920-21) club periodicals. [metadata] Ahrens,
  W. (1907). *Wiener Schachzeitung* 10/11:312-317. [metadata]
- Binnewirtz, R. (2023). *Ein Streifzug durch die Raumschach-Historie.*
  thbrand.de, 11 pp. [full text] Historical essay with footnoted
  bibliography; notes Troitzky studied elementary Raumschach endgames in
  Dawson's 1926 series (contents not located).

English fairy-chess tradition:

- Dawson, T. R. (1926). Space-chess: the elements of the "Normal Form".
  *The Chess Amateur* XX-XXI, six parts; Dec 1926 pp. 91-93 is the
  four-dimensional instalment. [metadata] Dawson (1915). *BCM* 35:436-439;
  (1947) *Caissa's Fairy Tales*. [metadata]
- Dickins, A. (1971). *A Guide to Fairy Chess*, 2nd ed., Dover, pp. 16-19.
  [full text, page images] 4D Normal Form on 4x4x4x4: "The Queen and King
  combine the moves of Rook, Bishop, Unicorn and Balloon (the King having,
  of course, only a single-step)": the full 80-move king and 80-direction
  queen.
- Gibbins, N. M. (1944). Chess in three and four dimensions. *Math.
  Gazette* 28(279):46-50. doi:10.2307/3606355. [full text] Lattice
  geometry and tours; no endgame content.
- Fabel, K., Kemp, C. E. (1969). *Schach ohne Grenzen.* [metadata] Gruber
  (1993), Widlert (1995). *feenschach* 110, 116. [metadata]

Parton, Pritchard, Beasley:

- Parton, V. R. (1971). *Chessical Cubism, or Chess in Space.* [metadata;
  rules via Beasley] Sphinx Chess: 4D on nine 4x4 boards, king steps to
  the corresponding square of an adjacent board; "perpetual check is a
  win".
- Pritchard, D. B. (1994). *The Encyclopedia of Chess Variants.* ISBN
  0-9524142-0-1. [not accessed]
- Pritchard, D. B., ed. Beasley, J. (2007). *The Classified Encyclopedia
  of Chess Variants*, ch. 25, pp. 225-235 (PDF at jsbeasley.co.uk). [full
  text] p. 225: "In orthochess, a rook or queen presents a barrier which
  the opposing king cannot cross. In three dimensions, it doesn't, and
  mating even a bare king can present difficulties"; remedies: a small
  board or a double-move rook. p. 227 (Tedco 4x4x4): "K+R and K+B only
  draw against bare K, but K+Q is an easy win." p. 234 (Schmittberger):
  mate "difficult, if not impossible, even when you are three queens
  ahead". p. 235, Beasley's hand analysis of 6D Ecila on 2^6 with a
  1-dimensional king: "K+R v K is hopeless (it takes K+3R to checkmate a
  bare K). K+Q can checkmate a bare K, but the mate cannot be forced";
  stalemate-as-win repair. Lists Maack's 4D chess (1926) and Lewin's
  six-dimensional chess (1978, 1-dimensional king).
- Beasley, J. (c. 2007). *Variant Chess* 54 (Ecila article). [metadata]

Variant designers (web, primary rule statements; opened in a browser):

- Joyce, J. (2004). Hyperchess. chessvariants.com/3d.dir/hyperchess.html.
  [full text] 4x4x4x4; diagonals only within the x-y and z-w pairs
  ("Diagonals are evil"); king 16 moves, queen 16 directions; held-king
  rule. "Without some kind of restriction on the king's move, it is
  extremely difficult to get checkmate ... A king and three queens against
  a bare king is a draw without either a king restriction or queens
  getting more moves. BK @ 3333; WK @ 1122, WQs @ 1111, 1112, 1121; either
  moves first, is a draw." Checked in section 8.
- Aikin, J. Chesseract. chessvariants.com/large.dir/contest/chesseract.html.
  [full text] 4x4x4x4; "The Chesseract king can move exactly one cell
  orthogonally in any direction, and cannot move diagonally"; queen = rook
  + 2D-diagonal bishop (this project's queen). Prior art for the
  orthogonal king.
- Pacey, K. 4*Chess. chessvariants.com/rules/4chess-four-dimensional-chess.
  [full text] 4x4x4x4; king and queen with all 80 directions; "easily
  checkmating a lone 4*Chess King with just a 4*Chess Queen"; mates in one
  with R+2B, R+2N, 3R etc., all "in an extreme corner".
- Reiniger, B. (2009). Four dimensional chess. Unpublished note, IIT, 7
  pp. [full text] "if we allow a king to move in any direction by one
  square, he could reach a whopping 80 squares"; remedies including
  "Crippled king ... allowing the king to move in only one dimension works
  out well." Reiniger and Joyce (2010), Chess Variants Wiki "Checkmate?":
  "I tend to believe that a fully armed 4D K+Q can mate a lone K on any
  size board, but am not positive." Checked in section 8.

Academic treatments of higher-dimensional chess:

- Rinaldi (Unciuleanu), O., Chiru, C.-G. (2026). A mathematical framework
  for four-dimensional chess. *AppliedMath* 6(3):48, 17 March 2026.
  doi:10.3390/appliedmath6030048. [full text] Board {1..8}^4; king by
  Chebyshev adjacency ("up to 80 adjacent neighbors"); queen "along a
  single axis, or along a 2D diagonal in any coordinate plane ... We
  deliberately restrict the queen to one- and two-axis moves"; bishop
  exactly two coordinates. Multi-king start position. Section 5.3: "We
  tested basic reduced-material endgames (K + Q vs. K and K + R vs. K) ...
  using engine-assisted search. In these tests, the engine consistently
  found winning continuations for the stronger side from randomly sampled
  starting placements ... These results are empirical demonstrations
  rather than formal proofs." "Proposition 6 (K + R vs. K is plausibly a
  win in 4D; informal strategy sketch)", with a rook "on a controlling
  hyperplane"; "not a complete proof". No retrograde analysis or tablebase.
  Does not cite Maack, Dawson, Pritchard, Reiniger, Evans-Hamkins or Ripa.
  The natural predecessor of this project: same board, same queen; the
  endgame question it raises is answered exhaustively here (section 8).
- Ripa, M. (2026). Metric spaces in chess and international chess pieces
  graph diameters. *Recreational Math. Mag.* 13(22):31-65.
  doi:10.2478/rmm-2026-0003. arXiv:2311.00016. [full text, definitions]
  k-dimensional pieces with the Chebyshev king and a bishop on all equal-
  magnitude diagonals; move-graph diameters; no endgames. Shows no
  standard convention exists.
- Cashman et al. (2024). See section 4.
- Cristea, D.-M. et al. (2019). Neural network adaptability from 2D to 3D
  chess. *IEEE SACI 2019*. [metadata] Dzerjinsky et al. (2023). *LNNS*.
  [metadata] Fraenkel, A. S., Lichtenstein, D. (1981). *JCTA* 31:199-214.
  [metadata] 2D complexity anchor.
- Muller, H. G. (2014). Forum posts, chess.com, "Minimum material to
  checkmate at Raumschach?" [full text; not academic] Partial 4-men tables
  for 5x5x5 Raumschach: "KQK is won"; KRRK 97,272 wins, longest mate in 10;
  KRNK 39,048 wins, longest 16; "all 4-men without Q are general draws".
  The only prior tablebase computation found for any d >= 3 chess.

Patents: Cutler, G. (1994). US 5,338,040, Three-dimensional chess. [full
text] 4x4x4; king restricted to two of the three plane types; background
states prior 3D games made checkmate "very difficult to achieve". No 4D
ruleset patent found. Maack's 1907 German patent number not located.

## 8. Cross-checks run against the printed claims

All with this project's solvers (`generate`, `sparse`), 2026-10-02 and
2026-10-04, 5,000-sample or exhaustive one-ply consistency with the full
rules, zero failures. Rule variants were added to `BoardGeometry` for the
purpose (`--diag`, `--king`, `--boardking`, `--pairdiag`).

| Source and claim | Ruleset tested | Result | Verdict |
|---|---|---|---|
| Muller 2014: "KQK is won" in Raumschach | 5x5x5, 26-direction K and Q | K+Q wins every position, longest 8 moves; K+R and K+B have no checkmate | agrees |
| Pacey (4*Chess), Reiniger and Joyce: full 4D K+Q mates on 4x4x4x4 | 4^4, 80-direction K and Q | wins every position, longest 4 moves | agrees |
| Reiniger 2010: K+Q "can mate a lone K on any size board" | 80/80, sides 4-8 | win at sides 4, 5, 6 (4, 8, 14 moves); 0.40% at 7, 0.11% at 8 | **false**: threshold between 6 and 7 |
| Aikin (Chesseract): orthogonal king, two-axis queen | 4^4 | K+Q wins every position, longest 20 moves (on 8^4 the same rules give 0.02%) | consistent with a playable 4^4 game |
| Joyce 2004: Hyperchess K+3Q v K drawn; the given position | 4^4, pair diagonals, group order 128 | K+Q: no checkmate exists; K+2Q: 494 won classes, mates in one only; K+3Q: 8,029,990 won classes of about 1.4 x 10^9, longest 139 plies (70 moves); the position BK 3333, WK 1122, WQ 1111 1112 1121 is a **draw with either side to move** | agrees exactly |
| Rinaldi and Chiru 2026, section 5.3: engine-assisted K+Q and K+R wins on 8^4; Prop. 6 (informal) | 8^4, two-axis sliders, 80-move Chebyshev king (their Definitions 7 and 9) | **K+Q: zero checkmate positions, zero stalemates. K+R: zero checkmate positions.** Also zero on 4^4 and 5^4 | does not reproduce under the written definitions; clarification sought (see below) |

Reading of the last row: with an 80-move king every interior cell has 80
escapes and a corner cell 15; the two-axis queen and the white king
together cannot cover even the corner's 15, so no checkmate position
exists. The engine-assisted "winning continuations" of their section 5.3
therefore cannot have ended in checkmate under Definitions 7 and 9 as
written; the likely explanations are a rule the paper does not state (a
narrower king, a different queen), a win criterion other than checkmate
(king capture, or the multi-king loss rule of their start position), or a
heuristic score read as a win. Their engine is public; reading it would
settle which. None of this touches the paper's framework results, and the
authors label their endgame material as "empirical demonstrations rather
than formal proofs". This project should be read as taking up that
invitation.

The full ruleset-by-material matrix (seven rulesets, fourteen material
sets) is in `docs/FINDINGS.md`, "Cross-validation matrix"; it reproduces
Muller's Raumschach mate lengths (10 and 16 moves) exactly.

## 9. Gaps in this review

- MathSciNet not available; Semantic Scholar and DBLP unreachable to
  automated queries. A manual scan of ICGA Journal and Advances in
  Computer Games tables of contents for 3D tablebase notes was not done.
- Pritchard 1994 not accessed (the 2007 edition was read in full for the
  relevant chapter). Dawson 1926 and Troitzky's Raumschach endgame
  studies known only through secondary sources.
- The Hamkins-group and cops-and-robbers streams were read from
  abstracts for most items; the items flagged [metadata] need page
  checks before citation.
- No search of Russian or Japanese literature on 3D chess.
