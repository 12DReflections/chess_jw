using System.Diagnostics;
using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    /// <summary>Stage 1 exit gate. The expected numbers are published and must not be adjusted.</summary>
    public class PerftTests
    {
        private static Board Load(string fen)
        {
            var board = new Board(2, 8);
            Fen.Load(board, fen);
            return board;
        }

        [TestCase(1, 20L)]
        [TestCase(2, 400L)]
        [TestCase(3, 8902L)]
        [TestCase(4, 197281L)]
        [TestCase(5, 4865609L)]
        public void StandardOpeningPosition(int depth, long expected)
        {
            var board = new Board(2, 8);
            StartPosition.Setup(board);
            var sw = Stopwatch.StartNew();
            long n = Perft.Count(board, depth);
            sw.Stop();
            TestContext.Out.WriteLine("perft(" + depth + ") = " + n + " in " + sw.ElapsedMilliseconds + " ms");
            Assert.That(n, Is.EqualTo(expected));
        }

        // Kiwipete: castling, en passant, promotions, checks, pins. https://www.chessprogramming.org/Perft_Results
        [TestCase(1, 48L)]
        [TestCase(2, 2039L)]
        [TestCase(3, 97862L)]
        [TestCase(4, 4085603L)]
        public void Kiwipete(int depth, long expected)
        {
            var board = Load("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
            Assert.That(Perft.Count(board, depth), Is.EqualTo(expected));
        }

        // Position 3: en passant and pins on an open board.
        [TestCase(1, 14L)]
        [TestCase(2, 191L)]
        [TestCase(3, 2812L)]
        [TestCase(4, 43238L)]
        [TestCase(5, 674624L)]
        public void Position3(int depth, long expected)
        {
            var board = Load("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1");
            Assert.That(Perft.Count(board, depth), Is.EqualTo(expected));
        }

        // Position 4: promotions, including capture promotions, and castling out of/through attack.
        [TestCase(1, 6L)]
        [TestCase(2, 264L)]
        [TestCase(3, 9467L)]
        [TestCase(4, 422333L)]
        public void Position4(int depth, long expected)
        {
            var board = Load("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1");
            Assert.That(Perft.Count(board, depth), Is.EqualTo(expected));
        }

        [TestCase(1, 44L)]
        [TestCase(2, 1486L)]
        [TestCase(3, 62379L)]
        public void Position5(int depth, long expected)
        {
            var board = Load("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8");
            Assert.That(Perft.Count(board, depth), Is.EqualTo(expected));
        }

        [TestCase(1, 46L)]
        [TestCase(2, 2079L)]
        [TestCase(3, 89890L)]
        public void Position6(int depth, long expected)
        {
            var board = Load("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10");
            Assert.That(Perft.Count(board, depth), Is.EqualTo(expected));
        }
    }
}
