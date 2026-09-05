using System.Collections.Generic;
using Chess4D.Core;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    public class StartPositionTests
    {
        [Test]
        public void ShellGeneratorAtTwoDimensionsIsTheStandardSecondRank()
        {
            var g = new BoardGeometry(2, 8);
            List<int> shell = StartPosition.PawnShellCells(g, Color.White);
            Assert.That(shell.Count, Is.EqualTo(8));
            foreach (int cell in shell)
            {
                Assert.That(g.Coord(cell, BoardGeometry.AdvanceAxis), Is.EqualTo(1));
            }
            shell = StartPosition.PawnShellCells(g, Color.Black);
            Assert.That(shell.Count, Is.EqualTo(8));
            foreach (int cell in shell) Assert.That(g.Coord(cell, BoardGeometry.AdvanceAxis), Is.EqualTo(6));
        }

        [Test]
        public void TwoDimensionalSetupMatchesTheStandardFen()
        {
            var board = new Board(2, 8);
            StartPosition.Setup(board);
            Assert.That(Fen.Save(board), Is.EqualTo(Fen.StartPosition));
            Assert.That(board.PieceCount(Color.White), Is.EqualTo(16));
            Assert.That(board.PieceCount(Color.Black), Is.EqualTo(16));
            Assert.That(board.KingCell(Color.White), Is.EqualTo(board.G.CellOf(4, 0)));
            Assert.That(board.KingCell(Color.Black), Is.EqualTo(board.G.CellOf(4, 7)));
        }

        [Test]
        public void SetupAndFenLoadGiveTheSameHash()
        {
            var a = new Board(2, 8);
            StartPosition.Setup(a);
            var b = new Board(2, 8);
            Fen.Load(b, Fen.StartPosition);
            Assert.That(b.Hash, Is.EqualTo(a.Hash));
            Assert.That(b.CastlingRights(), Is.EqualTo(15));
        }
    }
}
