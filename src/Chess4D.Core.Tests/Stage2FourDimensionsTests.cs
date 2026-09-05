using System;
using System.Collections.Generic;
using System.Diagnostics;
using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    /// <summary>Stage 2 exit gate: the core at dimensions = 4.</summary>
    public class Stage2FourDimensionsTests
    {
        private static Board Start()
        {
            var b = new Board(4, 8);
            StartPosition.Setup(b);
            return b;
        }

        [Test]
        public void DirectionCountsAtFourDimensions()
        {
            var g = new BoardGeometry(4, 8);
            Assert.That(g.Rook.Length, Is.EqualTo(8));
            Assert.That(g.Bishop.Length, Is.EqualTo(24));
            Assert.That(g.Queen.Length, Is.EqualTo(32));
            Assert.That(g.King.Length, Is.EqualTo(32));
            Assert.That(g.Knight.Length, Is.EqualTo(48));
            Assert.That(g.PawnCapture[0].Length, Is.EqualTo(6));
        }

        [Test]
        public void StartingPositionHas144PiecesPerSideOfWhich136ArePawns()
        {
            var b = Start();
            foreach (Color c in new[] { Color.White, Color.Black })
            {
                Assert.That(b.PieceCount(c), Is.EqualTo(144), c + " pieces");
                var cells = new List<int>();
                b.GetPieceCells(c, cells);
                int pawns = 0;
                foreach (int cell in cells) if (Piece.TypeOf(b.GetPiece(cell)) == PieceType.Pawn) pawns++;
                Assert.That(pawns, Is.EqualTo(136), c + " pawns");
                Assert.That(StartPosition.PawnShellCells(b.G, c).Count, Is.EqualTo(136));
            }
            var g = b.G;
            Assert.That(Piece.Is(b.GetPiece(g.CellOf(4, 0, 3, 3)), PieceType.King, Color.White));
            Assert.That(Piece.Is(b.GetPiece(g.CellOf(3, 0, 3, 3)), PieceType.Queen, Color.White));
            Assert.That(Piece.Is(b.GetPiece(g.CellOf(4, 7, 3, 3)), PieceType.King, Color.Black));
            Assert.That(Piece.Is(b.GetPiece(g.CellOf(3, 7, 3, 3)), PieceType.Queen, Color.Black));
            // Shell layout: 72 pawns at y=1, 64 at y=0 beside the back rank.
            int y0 = 0, y1 = 0;
            var white = new List<int>();
            b.GetPieceCells(Color.White, white);
            foreach (int cell in white)
            {
                if (Piece.TypeOf(b.GetPiece(cell)) != PieceType.Pawn) continue;
                if (g.Coord(cell, 1) == 0) y0++; else if (g.Coord(cell, 1) == 1) y1++; else Assert.Fail("pawn outside the shell at " + g.CoordOf(cell));
            }
            Assert.That(y1, Is.EqualTo(72));
            Assert.That(y0, Is.EqualTo(64));
        }

        [Test]
        public void EveryCellAtChebyshevDistanceOneFromTheBackRankIsOccupied()
        {
            var b = Start();
            var g = b.G;
            foreach (Color c in new[] { Color.White, Color.Black })
            {
                foreach (int back in StartPosition.BackRankCells(g, c))
                {
                    foreach (var d in g.King)
                    {
                        int n = g.Step(back, d);
                        if (n < 0) continue;
                        Assert.That(b.GetPiece(n), Is.Not.EqualTo(0), "empty neighbour " + g.CoordOf(n) + " of " + g.CoordOf(back));
                        Assert.That(Piece.ColorOf(b.GetPiece(n)), Is.EqualTo(c));
                    }
                }
            }
        }

        [Test]
        public void DepthOnePerftIs196AndMatchesTheHandDerivation()
        {
            var b = Start();
            var g = b.G;
            var list = new MoveList(1024);
            b.GenerateLegal(list);
            int knight = 0, pawnY1 = 0, pawnY0 = 0, other = 0, captures = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Move m = list[i];
                if (m.IsCapture) captures++;
                switch (Piece.TypeOf(b.GetPiece(m.From)))
                {
                    case PieceType.Knight: knight++; break;
                    case PieceType.Pawn: if (g.Coord(m.From, 1) == 1) pawnY1++; else pawnY0++; break;
                    default: other++; break;
                }
            }
            Assert.That(other, Is.EqualTo(0), "back-rank sliders and king are blocked");
            Assert.That(knight, Is.EqualTo(52), "26 per knight");
            Assert.That(pawnY1, Is.EqualTo(144), "72 pawns, single and double step");
            Assert.That(pawnY0, Is.EqualTo(0), "y=0 pawns blocked");
            Assert.That(captures, Is.EqualTo(0));
            Assert.That(list.Count, Is.EqualTo(196));
            Assert.That(Perft.Count(b, 1), Is.EqualTo(196));
        }

        /// <summary>
        /// Research finding, Stage 2: check is possible at ply 3 in 4D, the same as in 2D.
        /// One pawn move opens the Queen's (x,y) diagonal out of the shell; the Black
        /// pawn at (3,6,3,3) vacating opens the return diagonal to the King at (4,7,3,3).
        /// The spec's "early check is impossible by construction" is therefore false.
        /// </summary>
        [Test]
        public void CheckIsPossibleAtPlyThree()
        {
            var b = Start();
            var g = b.G;
            var list = new MoveList(1024);
            b.GenerateLegal(list);
            var w1 = new Move(g.CellOf(2, 1, 3, 3), g.CellOf(2, 3, 3, 3), PieceType.None, MoveFlags.DoubleStep, g.CellOf(2, 2, 3, 3));
            Assert.That(list.Contains(w1), Is.True);
            b.Make(w1);
            b.GenerateLegal(list);
            var b1 = new Move(g.CellOf(3, 6, 3, 3), g.CellOf(3, 4, 3, 3), PieceType.None, MoveFlags.DoubleStep, g.CellOf(3, 5, 3, 3));
            Assert.That(list.Contains(b1), Is.True);
            b.Make(b1);
            b.GenerateLegal(list);
            var w2 = new Move(g.CellOf(3, 0, 3, 3), g.CellOf(0, 3, 3, 3));
            Assert.That(list.Contains(w2), Is.True, "queen slides out along the vacated (x,y) diagonal");
            b.Make(w2);
            Assert.That(b.SideToMove, Is.EqualTo(Color.Black));
            Assert.That(b.InCheck(), Is.True, "queen on (0,3,3,3) sees the king on (4,7,3,3) through the vacated (3,6,3,3)");
            var attackers = new List<int>();
            b.Attackers(b.KingCell(Color.Black), Color.White, attackers);
            Assert.That(attackers, Is.EqualTo(new[] { g.CellOf(0, 3, 3, 3) }));
            Assert.That(b.GetStatus(), Is.EqualTo(GameStatus.Ongoing), "check, not mate");
        }

        /// <summary>Recorded in docs/PERFT_4D.md. Nobody has published these; they are this project's reference.</summary>
        [TestCase(1, 196L)]
        [TestCase(2, 38416L)]
        [TestCase(3, 7584070L)]
        public void FourDimensionalPerft(int depth, long expected)
        {
            var b = Start();
            var sw = Stopwatch.StartNew();
            long n = Perft.Count(b, depth);
            TestContext.Out.WriteLine("4D perft(" + depth + ") = " + n + " in " + sw.ElapsedMilliseconds + " ms");
            Assert.That(n, Is.EqualTo(expected));
        }

        [Test, Explicit("Discovery run: prints 4D perft at depths 1..3 with timings")]
        public void Discover4DPerft()
        {
            var b = Start();
            for (int d = 1; d <= 3; d++)
            {
                var sw = Stopwatch.StartNew();
                long n = Perft.Count(b, d);
                TestContext.Out.WriteLine("4D perft(" + d + ") = " + n + " in " + sw.ElapsedMilliseconds + " ms");
            }
        }

        // ------------------------------------------------------------ random play helpers

        private static bool RandomMove(Board b, Random rng, MoveList list, out Move move)
        {
            b.GenerateLegal(list);
            if (list.Count == 0) { move = default; return false; }
            move = list[rng.Next(list.Count)];
            return true;
        }

        [Test]
        public void BishopCoordinateSumParityNeverChangesOverRandomPlay()
        {
            var rng = new Random(20260905);
            var b = Start();
            var g = b.G;
            var list = new MoveList(1024);
            int bishopMoves = 0, totalMoves = 0;
            while (totalMoves < 10000)
            {
                if (b.Ply >= 400 || !RandomMove(b, rng, list, out Move m))
                {
                    StartPosition.Setup(b);
                    continue;
                }
                if (Piece.TypeOf(b.GetPiece(m.From)) == PieceType.Bishop)
                {
                    int pf = 0, pt = 0;
                    for (int a = 0; a < g.Dimensions; a++) { pf += g.Coord(m.From, a); pt += g.Coord(m.To, a); }
                    Assert.That(pt & 1, Is.EqualTo(pf & 1), "bishop changed parity: " + m.ToString(g));
                    bishopMoves++;
                }
                b.Make(m);
                totalMoves++;
            }
            TestContext.Out.WriteLine("random legal moves: " + totalMoves + ", bishop moves checked: " + bishopMoves);
            Assert.That(bishopMoves, Is.GreaterThan(100), "the sample must actually contain bishop moves");
        }

        /// <summary>Replaces the vacuous "no slider gives check at the start" assertion: over random games, when does a check first become available, and by which piece?</summary>
        [Test]
        public void FirstAvailableCheckOverRandomGames()
        {
            const int games = 1000;
            const int maxPly = 300;
            var rng = new Random(4);
            var b = Start();
            var list = new MoveList(1024);
            var plies = new List<int>();
            var byType = new Dictionary<PieceType, int>();
            int noCheck = 0;
            var histogram = new SortedDictionary<int, int>();
            int earliestPly = int.MaxValue;
            string earliestLine = "";
            var lineMoves = new List<Move>();
            for (int gI = 0; gI < games; gI++)
            {
                StartPosition.Setup(b);
                lineMoves.Clear();
                bool found = false;
                while (b.Ply < maxPly)
                {
                    b.GenerateLegal(list);
                    if (list.Count == 0) break;
                    var typesGivingCheck = new HashSet<PieceType>();
                    for (int i = 0; i < list.Count; i++)
                    {
                        Move m = list[i];
                        PieceType mover = Piece.TypeOf(b.GetPiece(m.From));
                        b.Make(m);
                        if (b.InCheck()) typesGivingCheck.Add(m.IsPromotion ? m.Promotion : mover);
                        b.Unmake();
                    }
                    if (typesGivingCheck.Count > 0)
                    {
                        int ply = b.Ply + 1; // the move that would give check is move number Ply+1
                        if (ply < earliestPly)
                        {
                            earliestPly = ply;
                            var sb = new System.Text.StringBuilder();
                            foreach (var lm in lineMoves) sb.Append(lm.ToString(b.G)).Append("  ");
                            for (int i = 0; i < list.Count; i++)
                            {
                                Move m = list[i];
                                b.Make(m);
                                bool check = b.InCheck();
                                b.Unmake();
                                if (check) { sb.Append("then ").Append(m.ToString(b.G)).Append(" gives check by ").Append(Piece.TypeOf(b.GetPiece(m.From))); break; }
                            }
                            earliestLine = sb.ToString();
                        }
                        plies.Add(ply);
                        histogram.TryGetValue(ply, out int h); histogram[ply] = h + 1;
                        foreach (var t in typesGivingCheck) { byType.TryGetValue(t, out int n); byType[t] = n + 1; }
                        found = true;
                        break;
                    }
                    Move chosen = list[rng.Next(list.Count)];
                    lineMoves.Add(chosen);
                    b.Make(chosen);
                }
                if (!found) noCheck++;
            }
            plies.Sort();
            TestContext.Out.WriteLine("games: " + games + ", games with a check available within " + maxPly + " plies: " + plies.Count + ", none: " + noCheck);
            if (plies.Count > 0)
            {
                TestContext.Out.WriteLine("first-check ply: min " + plies[0] + ", median " + plies[plies.Count / 2] + ", max " + plies[plies.Count - 1]);
                foreach (var kv in byType) TestContext.Out.WriteLine("  by " + kv.Key + ": " + kv.Value);
                var sb = new System.Text.StringBuilder("histogram (ply:count):");
                foreach (var kv in histogram) sb.Append(' ').Append(kv.Key).Append(':').Append(kv.Value);
                TestContext.Out.WriteLine(sb.ToString());
                TestContext.Out.WriteLine("earliest line: " + earliestLine);
                // No first move can give check (depth-1 derivation: no captures, no back-rank moves),
                // so the theoretical minimum is ply 3. See CheckIsPossibleAtPlyThree for the line.
                Assert.That(plies[0], Is.GreaterThanOrEqualTo(3), "a check before ply 3 would be a generator bug");
            }
        }
    }
}
