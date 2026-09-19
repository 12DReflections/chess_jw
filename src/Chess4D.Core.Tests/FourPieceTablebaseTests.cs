using System;
using Chess4D.Core;
using Chess4D.Engine;
using Chess4D.Tablebase;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    /// <summary>The four-piece solver is validated at n=2 against the published endgame results before its 4D numbers are trusted, as the three-piece one was.</summary>
    public class FourPieceTablebaseTests
    {
        // Published: K+B+N mates in at most 33 moves, K+B+B in at most 19, K+N+N cannot force anything beyond mates in one.
        [TestCase(PieceType.Bishop, PieceType.Knight, 65)]
        [TestCase(PieceType.Bishop, PieceType.Bishop, 37)]
        [TestCase(PieceType.Knight, PieceType.Knight, 1)]
        [TestCase(PieceType.Queen, PieceType.Rook, 11)] // exercises captures into won three-piece tables
        public void TwoDimensionalTablesMatchPublishedResults(PieceType a, PieceType b, int longestWtm)
        {
            var gen = new FourPieceGenerator(new BoardGeometry(2, 8), a, b, s => { });
            gen.Initialise();
            gen.Solve();
            Assert.That(gen.MaxWtmDistance, Is.EqualTo(longestWtm));
            if (a == PieceType.Queen) Assert.That(gen.Wins, Is.EqualTo(gen.LegalWtm), "K+Q+R wins every white-to-move position");
            var failures = gen.ConsistencySample(2000, 3);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void SafeRegionIsEmptyWhereMateCanBeForcedAndCertifiesTheQueenDrawInFourDimensions()
        {
            Assert.That(SafeRegion.Compute(new BoardGeometry(2, 8), new[] { PieceType.Queen }).SafeCells, Is.EqualTo(0));
            Assert.That(SafeRegion.Compute(new BoardGeometry(2, 8), new[] { PieceType.Rook }).SafeCells, Is.EqualTo(0));
            var q = SafeRegion.Compute(new BoardGeometry(4, 8), new[] { PieceType.Queen });
            Assert.That(q.SafeCells, Is.EqualTo(3792), "agrees with the tablebase: K+Q cannot force mate in 4D");
            Assert.That(q.CentreMaxCovered, Is.EqualTo(20));
        }

        [Test]
        public void QueenAndRookCannotForceMateEvenOnTheSideFourBoard()
        {
            var gen = new FourPieceGenerator(new BoardGeometry(4, 4), PieceType.Queen, PieceType.Rook, s => { });
            gen.Initialise();
            gen.Solve();
            Assert.That(gen.Wins, Is.EqualTo(1692));
            Assert.That(gen.MaxWtmDistance, Is.EqualTo(7));
            var failures = gen.ConsistencySample(2000, 5);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void SparseSolverAgreesWithTheDenseTables()
        {
            // The sparse solver takes its rules from Board, the dense ones from the fast geometry: two implementations, one answer.
            var q2 = SparseSolver.For(new BoardGeometry(2, 8), new[] { PieceType.Queen }, null);
            Assert.That(q2.Won.Count, Is.EqualTo(18081));
            Assert.That(q2.MaxWtmDistance, Is.EqualTo(19));
            var qr4 = SparseSolver.For(new BoardGeometry(4, 4), new[] { PieceType.Queen, PieceType.Rook }, null);
            Assert.That(qr4.Mates, Is.EqualTo(154));
            Assert.That(qr4.Won.Count, Is.EqualTo(1692));
            Assert.That(qr4.Lost.Count, Is.EqualTo(166));
            Assert.That(qr4.MaxWtmDistance, Is.EqualTo(7));
        }

        [Test]
        public void QueenAndRookCannotForceMateOnTheFullBoard()
        {
            var g = new BoardGeometry(4, 8);
            var qr = SparseSolver.For(g, new[] { PieceType.Queen, PieceType.Rook }, null);
            Assert.That(qr.Mates, Is.EqualTo(1282));
            Assert.That(qr.Won.Count, Is.EqualTo(51625));
            Assert.That(qr.Lost.Count, Is.EqualTo(1331));
            Assert.That(qr.MaxWtmDistance, Is.EqualTo(7));
            var w = new int[2];
            foreach (ulong key in qr.Won.Keys)
            {
                qr.Decode(key, out int wk, w, out int bk);
                bool onEdge = false;
                for (int i = 0; i < 4; i++) { int v = g.Coord(bk, i); if (v == 0 || v == 7) onEdge = true; }
                Assert.That(onEdge, Is.True, "every won position has the black king already on an edge");
            }

            Assert.That(qr.ProbeWtm(g.CellOf(2, 1, 1, 1), new[] { g.CellOf(1, 1, 1, 0), g.CellOf(1, 0, 0, 3) }, g.CellOf(0, 0, 0, 0)), Is.EqualTo(7), "one of the deepest wins");
            var failures = qr.VerifyOnePly();
            Assert.That(failures, Is.Empty, string.Join("\n", failures));

            // The engine's own search agrees on mates in three plies (seven is beyond it at this branching).
            var engine = new SearchEngine();
            int checkedCount = 0;
            foreach (var kv in qr.Won)
            {
                if (kv.Value != 3) continue;
                qr.Decode(kv.Key, out int wk, w, out int bk);
                var b = new Board(g);
                b.Clear();
                b.PlacePiece(wk, Piece.Make(PieceType.King, Color.White, true));
                b.PlacePiece(w[0], Piece.Make(PieceType.Queen, Color.White, true));
                b.PlacePiece(w[1], Piece.Make(PieceType.Rook, Color.White, true));
                b.PlacePiece(bk, Piece.Make(PieceType.King, Color.Black, true));
                b.SetSideToMove(Color.White);
                engine.ClearMemory();
                SearchResult r = engine.Search(b, new SearchLimits { MaxDepth = 4, TimeMs = 60000 });
                Assert.That(r.Score, Is.EqualTo(Evaluation.MateScore - 3), r.Line);
                if (++checkedCount == 5) break;
            }
            Assert.That(checkedCount, Is.EqualTo(5));
        }
    }
}
