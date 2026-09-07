using System;
using System.Collections.Generic;
using Chess4D.Core;
using Chess4D.Tablebase;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    /// <summary>Stage 6: canonicalisation is unit tested before anything is generated, and the generator is validated at n=2 against the published 2D results.</summary>
    public class TablebaseTests
    {
        [TestCase(2, 8, 10)]
        [TestCase(4, 384, 35)]
        public void GroupOrderAndKingClasses(int dims, int order, int classes)
        {
            var sym = new Symmetry(new BoardGeometry(dims, 8));
            Assert.That(sym.TransformCount, Is.EqualTo(order));
            Assert.That(sym.DomainCells.Length, Is.EqualTo(classes));
            var seen = new HashSet<string>();
            int cells = sym.CellCount;
            for (int t = 0; t < sym.TransformCount; t++)
            {
                var img = new int[cells];
                var hit = new bool[cells];
                for (int c = 0; c < cells; c++) { img[c] = sym.Apply(t, c); Assert.That(hit[img[c]], Is.False, "not a bijection"); hit[img[c]] = true; }
                Assert.That(seen.Add(string.Join(",", img)), Is.True, "duplicate transform");
            }
        }

        [Test]
        public void CanonicalIndexIsInvariantUnderEveryTransformAndRoundTrips()
        {
            var g = new BoardGeometry(4, 8);
            var sym = new Symmetry(g);
            var rng = new Random(9);
            for (int s = 0; s < 300; s++)
            {
                int wk = rng.Next(g.CellCount), wx = rng.Next(g.CellCount), bk = rng.Next(g.CellCount);
                if (wk == wx || wk == bk || wx == bk) continue;
                int stm = rng.Next(2);
                long idx = sym.Index(wk, wx, bk, stm);
                Assert.That(idx, Is.InRange(0, sym.EntryCount - 1));
                for (int t = 0; t < sym.TransformCount; t += 7)
                    Assert.That(sym.Index(sym.Apply(t, wk), sym.Apply(t, wx), sym.Apply(t, bk), stm), Is.EqualTo(idx), "transform " + t);
                sym.Decode(idx, out int cwk, out int cwx, out int cbk, out int cstm);
                Assert.That(cstm, Is.EqualTo(stm));
                Assert.That(sym.Index(cwk, cwx, cbk, cstm), Is.EqualTo(idx), "decode is a fixed point");
                // The decoded position is in the same orbit as the original.
                bool related = false;
                for (int t = 0; t < sym.TransformCount && !related; t++)
                    related = sym.Apply(t, wk) == cwk && sym.Apply(t, wx) == cwx && sym.Apply(t, bk) == cbk;
                Assert.That(related, Is.True, "decoded position not in the orbit");
            }
        }

        [Test]
        public void TableSizeMatchesTheSpecEstimate()
        {
            var sym = new Symmetry(new BoardGeometry(4, 8));
            TestContext.Out.WriteLine("4D pairs " + sym.PairCount + ", entries " + sym.EntryCount + " (" + sym.EntryCount * 2 / 1048576 + " MB at two bytes)");
            long positions = 4096L * 4095 * 4094 * 2;
            Assert.That(sym.EntryCount, Is.GreaterThanOrEqualTo(positions / 384), "cannot be below the orbit-counting bound");
            // The spec's 358 million ignores positions fixed by some symmetry; the exact orbit count is higher and the
            // black king's cell is left unreduced under the (king, piece) stabiliser for a simple index. About 20 percent over.
            Assert.That(sym.EntryCount, Is.LessThan(positions / 384 * 1.25), "within 25 percent of the bound");
        }

        [Test]
        public void FastAttackTestAgreesWithTheBoardOnRandomThreePiecePositions()
        {
            var g = new BoardGeometry(4, 8);
            var rng = new Random(4);
            foreach (PieceType type in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
            {
                var geo = new ThreePiece(g, type);
                var b = new Board(g);
                for (int s = 0; s < 3000; s++)
                {
                    int wk = rng.Next(g.CellCount), wx = rng.Next(g.CellCount), bk = rng.Next(g.CellCount);
                    if (wk == wx || wk == bk || wx == bk) continue;
                    b.Clear();
                    b.PlacePiece(wk, Piece.Make(PieceType.King, Color.White));
                    b.PlacePiece(wx, Piece.Make(type, Color.White));
                    b.PlacePiece(bk, Piece.Make(PieceType.King, Color.Black));
                    Assert.That(geo.BlackKingAttacked(wk, wx, bk), Is.EqualTo(b.IsAttacked(bk, Color.White)), type + " at " + g.CoordOf(wk) + " " + g.CoordOf(wx) + " " + g.CoordOf(bk));
                    Assert.That(geo.KingsAdjacent(wk, bk), Is.EqualTo(g.ChebyshevDistance(wk, bk) == 1 && CountDiff(g, wk, bk) <= 2));
                }
            }
        }

        private static int CountDiff(BoardGeometry g, int a, int b)
        {
            int n = 0;
            for (int i = 0; i < g.Dimensions; i++) if (g.Coord(a, i) != g.Coord(b, i)) n++;
            return n;
        }

        // Published 2D results: K+Q vs K mates in at most 10 moves, K+R vs K in at most 16; K+B and K+N cannot mate at all.
        [TestCase(PieceType.Queen, 19, 20)]
        [TestCase(PieceType.Rook, 31, 32)]
        [TestCase(PieceType.Bishop, -1, -1)]
        [TestCase(PieceType.Knight, -1, -1)]
        public void TwoDimensionalTablesMatchThePublishedResults(PieceType piece, int longestWtm, int longestBtm)
        {
            var gen = new Generator(new BoardGeometry(2, 8), piece) { Log = s => TestContext.Out.WriteLine(s), Threads = 4 };
            gen.Initialise();
            gen.Solve();
            TestContext.Out.WriteLine(gen.Summary());
            Assert.That(gen.MaxWtmDistance, Is.EqualTo(longestWtm));
            Assert.That(gen.MaxBtmDistance, Is.EqualTo(longestBtm));
            var failures = Verify.ConsistencySample(gen, 1500, 3, s => TestContext.Out.WriteLine(s));
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            if (longestWtm > 0)
            {
                var engineFailures = Verify.EngineSample(gen, 8, 4, 5, 5, s => TestContext.Out.WriteLine(s));
                Assert.That(engineFailures, Is.Empty, string.Join("\n", engineFailures));
            }
            else Assert.That(gen.Wins, Is.EqualTo(0));
        }

        [Test]
        public void KnownTwoDimensionalPositionsProbeCorrectly()
        {
            var g = new BoardGeometry(2, 8);
            var gen = new Generator(g, PieceType.Queen) { Log = s => { }, Threads = 4 };
            gen.Initialise();
            gen.Solve();
            // Black king a8, White king b6, White queen c7: mate in 1 for White to move (Qb7#).
            Assert.That(gen.Probe(g.CellOf(1, 5), g.CellOf(2, 6), g.CellOf(0, 7), 0), Is.EqualTo(1));
            // Same with Black to move: stalemate.
            Assert.That(gen.Probe(g.CellOf(1, 5), g.CellOf(2, 6), g.CellOf(0, 7), 1), Is.EqualTo(Generator.Stalemate));
            // Black king a8, White king c7, White queen b7: Black is checkmated.
            Assert.That(gen.Probe(g.CellOf(2, 6), g.CellOf(1, 6), g.CellOf(0, 7), 1), Is.EqualTo(0));
        }
    }
}
