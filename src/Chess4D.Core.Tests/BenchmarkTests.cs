using System.Diagnostics;
using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    /// <summary>Spec section 2 performance budget, at four dimensions, for the starting position and an open position.</summary>
    public class BenchmarkTests
    {
        public const double PseudoLegalBudgetMs = 5;
        public const double LegalBudgetMs = 50;
        public const double AttackQueryBudgetMs = 1;

        /// <summary>The open benchmark position. Recorded in PROGRESS.md; do not change without updating it.</summary>
        public static Board OpenPosition()
        {
            var b = new Board(4, 8);
            b.Clear();
            var g = b.G;
            b.PlacePiece(g.CellOf(4, 0, 3, 3), Piece.Make(PieceType.King, Color.White, true));
            b.PlacePiece(g.CellOf(4, 4, 3, 3), Piece.Make(PieceType.Queen, Color.White, true));
            b.PlacePiece(g.CellOf(0, 0, 3, 3), Piece.Make(PieceType.Rook, Color.White));
            b.PlacePiece(g.CellOf(2, 2, 1, 5), Piece.Make(PieceType.Bishop, Color.White, true));
            b.PlacePiece(g.CellOf(6, 3, 3, 3), Piece.Make(PieceType.Knight, Color.White, true));
            b.PlacePiece(g.CellOf(4, 3, 3, 3), Piece.Make(PieceType.Pawn, Color.White, true));
            b.PlacePiece(g.CellOf(1, 1, 2, 2), Piece.Make(PieceType.Pawn, Color.White));
            b.PlacePiece(g.CellOf(4, 7, 3, 3), Piece.Make(PieceType.King, Color.Black, true));
            b.PlacePiece(g.CellOf(3, 4, 2, 2), Piece.Make(PieceType.Queen, Color.Black, true));
            b.PlacePiece(g.CellOf(7, 7, 3, 3), Piece.Make(PieceType.Rook, Color.Black));
            b.PlacePiece(g.CellOf(2, 5, 3, 3), Piece.Make(PieceType.Knight, Color.Black, true));
            b.PlacePiece(g.CellOf(5, 5, 4, 4), Piece.Make(PieceType.Bishop, Color.Black, true));
            b.PlacePiece(g.CellOf(4, 4, 3, 2), Piece.Make(PieceType.Pawn, Color.Black, true));
            b.PlacePiece(g.CellOf(6, 6, 4, 4), Piece.Make(PieceType.Pawn, Color.Black));
            b.SetSideToMove(Color.White);
            return b;
        }

        private static double Time(System.Action action, int reps)
        {
            action(); // warm up
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < reps; i++) action();
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / reps;
        }

        private static void Report(string label, Board b)
        {
            var list = new MoveList(2048);
            double pseudo = Time(() => b.GeneratePseudoLegal(list), 20);
            int pseudoCount = list.Count;
            double legal = Time(() => b.GenerateLegal(list), 5);
            int legalCount = list.Count;
            int k = b.KingCell(b.SideToMove);
            double attack = Time(() => b.IsAttacked(k, Piece.Opposite(b.SideToMove)), 200);
            TestContext.Out.WriteLine(label + ": pseudo " + pseudoCount + " moves in " + pseudo.ToString("F3") + " ms; legal " + legalCount + " in " + legal.ToString("F3") + " ms; attack query " + attack.ToString("F4") + " ms");
            Assert.That(pseudo, Is.LessThan(PseudoLegalBudgetMs), "pseudo-legal budget");
            Assert.That(legal, Is.LessThan(LegalBudgetMs), "legal budget");
            Assert.That(attack, Is.LessThan(AttackQueryBudgetMs), "attack query budget");
        }

        [Test]
        public void FourDimensionalStartingPositionMeetsBudget()
        {
            var b = new Board(4, 8);
            StartPosition.Setup(b);
            Report("4D start", b);
        }

        [Test]
        public void FourDimensionalOpenPositionMeetsBudget()
        {
            Report("4D open", OpenPosition());
        }
    }
}
