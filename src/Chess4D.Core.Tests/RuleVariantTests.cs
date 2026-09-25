using System;
using System.Collections.Generic;
using Chess4D.Core;
using Chess4D.Tablebase;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    /// <summary>Rule variants (SPEC section 6): wider diagonals and a slower King. The settled rules are the defaults and must be unchanged.</summary>
    public class RuleVariantTests
    {
        [TestCase(4, 8, 2, 0, 8, 24, 32, 32)]   // settled rules
        [TestCase(4, 8, 4, 0, 8, 72, 80, 80)]   // the 80-direction Queen and 72-direction Bishop of SPEC section 6
        [TestCase(4, 8, 4, 2, 8, 72, 80, 32)]   // full Queen, settled King
        [TestCase(4, 8, 2, 1, 8, 24, 32, 8)]    // orthogonal King
        [TestCase(3, 8, 3, 0, 6, 20, 26, 26)]
        public void DirectionCounts(int dims, int side, int diag, int king, int rook, int bishop, int queen, int kingDirs)
        {
            var g = new BoardGeometry(dims, side, diag, king);
            Assert.That(g.Rook.Length, Is.EqualTo(rook));
            Assert.That(g.Bishop.Length, Is.EqualTo(bishop));
            Assert.That(g.Queen.Length, Is.EqualTo(queen));
            Assert.That(g.King.Length, Is.EqualTo(kingDirs));
            Assert.That(g.KingDirectionCount, Is.EqualTo(kingDirs));
            for (int i = 1; i < g.Queen.Length; i++) Assert.That(g.Queen[i].Axes, Is.GreaterThanOrEqualTo(g.Queen[i - 1].Axes), "queen directions ordered by axis count");
        }

        [TestCase(2, 0)]
        [TestCase(4, 2)]
        [TestCase(4, 0)]
        [TestCase(3, 1)]
        [TestCase(2, 1)]
        public void BoardAndFastGeometryAgreeOnAttacks(int diag, int king)
        {
            // Board.IsAttacked walks direction lists; ThreePiece.Attacks reasons from coordinate differences. Both were changed for variants.
            var g = new BoardGeometry(4, 6, diag, king);
            var geo = new ThreePiece(g, PieceType.Queen);
            var rng = new Random(21);
            var b = new Board(g);
            foreach (PieceType t in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight, PieceType.King })
            {
                var fast = new ThreePiece(g, t == PieceType.King ? PieceType.Queen : t);
                for (int s = 0; s < 3000; s++)
                {
                    int from = rng.Next(g.CellCount), to = rng.Next(g.CellCount), blocker = rng.Next(g.CellCount);
                    if (from == to || blocker == from || blocker == to) continue;
                    b.Clear();
                    b.PlacePiece(from, Piece.Make(t, Color.White, true));
                    b.PlacePiece(blocker, Piece.Make(PieceType.Pawn, Color.Black, true));
                    bool slow = b.IsAttacked(to, Color.White);
                    bool quick = fast.Attacks(t, from, to, blocker);
                    Assert.That(quick, Is.EqualTo(slow), t + " from " + g.CoordOf(from).ToCompact() + " to " + g.CoordOf(to).ToCompact() + " blocker " + g.CoordOf(blocker).ToCompact());
                }
            }
        }

        /// <summary>The rule-variant finding: a King limited to orthogonal steps makes K+Q vs K a forced win in 4D, once the Queen has 3-axis diagonals. Cheap at 3D; the 4D case regenerates a full table.</summary>
        [TestCase(3, 2, 1, 39)]   // 3D, settled Queen, orthogonal King
        [TestCase(3, 3, 2, 71)]   // 3D, 26-direction Queen, settled King
        public void SlowKingOrWideQueenMakesQueenMateForcedInThreeDimensions(int dims, int diag, int king, int longestWtm)
        {
            var gen = new Generator(new BoardGeometry(dims, 8, diag, king), PieceType.Queen) { Log = s => { } };
            gen.Initialise();
            gen.Solve();
            Assert.That(gen.Wins, Is.EqualTo(gen.LegalWtm), "every white-to-move position is won");
            Assert.That(gen.MaxWtmDistance, Is.EqualTo(longestWtm));
            var failures = Verify.ConsistencySample(gen, 2000, 4);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test, Explicit("Regenerates a 4D table; about a minute and 1.7 GB")]
        public void OrthogonalKingAndThreeAxisQueenMakeQueenMateForcedInFourDimensions()
        {
            var gen = new Generator(new BoardGeometry(4, 8, 3, 1), PieceType.Queen) { Log = s => TestContext.Out.WriteLine(s) };
            gen.Initialise();
            gen.Solve();
            Assert.That(gen.Wins, Is.EqualTo(gen.LegalWtm));
            Assert.That(gen.MaxWtmDistance, Is.EqualTo(15));
        }
    }
}
